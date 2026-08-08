package com.atakan.blastscale.security;

import com.atakan.blastscale.common.api.ApiError;
import com.atakan.blastscale.common.exception.ErrorCode;
import com.atakan.blastscale.common.metrics.GameplayMetrics;
import jakarta.servlet.FilterChain;
import jakarta.servlet.ServletException;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.dao.DataAccessException;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.http.MediaType;
import org.springframework.security.core.Authentication;
import org.springframework.security.core.context.SecurityContextHolder;
import org.springframework.security.oauth2.server.resource.authentication.JwtAuthenticationToken;
import org.springframework.stereotype.Component;
import org.springframework.web.filter.OncePerRequestFilter;
import tools.jackson.databind.ObjectMapper;

import java.io.IOException;
import java.time.Clock;
import java.time.Duration;
import java.time.Instant;
import java.util.Map;

/**
 * Fixed-window rate limiter backed by Redis.
 *
 * <p>Key layout: {@code rl:p:{playerId}:{minute}} for authenticated calls and
 * {@code rl:ip:{address}:{minute}} for anonymous ones (login/register). {@code INCR} is atomic, so
 * the counter is correct across all API replicas. The key expires after two minutes, keeping Redis
 * memory bounded without any cleanup job.
 *
 * <p>Fail-open policy: if Redis is unreachable the request is allowed. Rate limiting protects
 * against abuse; it must not become a single point of failure for legitimate players.
 */
@Component
public class RateLimitFilter extends OncePerRequestFilter {

    private static final Logger log = LoggerFactory.getLogger(RateLimitFilter.class);

    private final StringRedisTemplate redis;
    private final RateLimitProperties properties;
    private final ObjectMapper objectMapper;
    private final GameplayMetrics metrics;
    private final Clock clock;

    public RateLimitFilter(StringRedisTemplate redis, RateLimitProperties properties,
                           ObjectMapper objectMapper, GameplayMetrics metrics, Clock clock) {
        this.redis = redis;
        this.properties = properties;
        this.objectMapper = objectMapper;
        this.metrics = metrics;
        this.clock = clock;
    }

    @Override
    protected boolean shouldNotFilter(HttpServletRequest request) {
        // Health probes and Prometheus scrapes are internal traffic.
        return !properties.enabled() || request.getRequestURI().startsWith("/actuator");
    }

    @Override
    protected void doFilterInternal(HttpServletRequest request, HttpServletResponse response, FilterChain chain)
            throws ServletException, IOException {
        long window = Instant.now(clock).getEpochSecond() / 60;
        String subject = subject(request);
        String key = "rl:" + subject + ":" + window;

        long count;
        try {
            Long value = redis.opsForValue().increment(key);
            count = value == null ? 0 : value;
            if (count == 1) {
                redis.expire(key, Duration.ofMinutes(2));
            }
        } catch (DataAccessException e) {
            log.warn("Redis unavailable for rate limiting, allowing request: {}", e.getMessage());
            chain.doFilter(request, response);
            return;
        }

        int limit = subject.startsWith("ip:") ? properties.anonymousRequestsPerMinute() : properties.requestsPerMinute();
        response.setHeader("X-RateLimit-Limit", Integer.toString(limit));
        response.setHeader("X-RateLimit-Remaining", Long.toString(Math.max(0, limit - count)));

        if (count > limit) {
            metrics.rateLimitRejected();
            response.setStatus(ErrorCode.RATE_LIMITED.status().value());
            response.setContentType(MediaType.APPLICATION_JSON_VALUE);
            response.setHeader("Retry-After", "60");
            ApiError body = new ApiError(ErrorCode.RATE_LIMITED.name(),
                    "Too many requests, slow down", Map.of("limitPerMinute", limit),
                    Instant.now(clock), request.getRequestURI());
            response.getWriter().write(objectMapper.writeValueAsString(body));
            return;
        }
        chain.doFilter(request, response);
    }

    /**
     * Player id when authenticated, otherwise the client IP. The address comes from
     * {@code request.getRemoteAddr()}, which Spring's forwarded-header support already resolved
     * from {@code X-Forwarded-For}; nginx overwrites that header with the real peer address
     * (see infra/nginx/nginx.conf), so a client cannot dodge the limit by spoofing it.
     */
    private static String subject(HttpServletRequest request) {
        Authentication auth = SecurityContextHolder.getContext().getAuthentication();
        if (auth instanceof JwtAuthenticationToken jwt) {
            return "p:" + jwt.getToken().getSubject();
        }
        return "ip:" + request.getRemoteAddr();
    }
}
