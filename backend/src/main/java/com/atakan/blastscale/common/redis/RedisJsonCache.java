package com.atakan.blastscale.common.redis;

import com.atakan.blastscale.common.metrics.GameplayMetrics;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.dao.DataAccessException;
import org.springframework.data.redis.core.StringRedisTemplate;
import org.springframework.stereotype.Component;
import tools.jackson.databind.ObjectMapper;

import java.time.Duration;
import java.util.Optional;
import java.util.function.Supplier;

/**
 * Small <b>cache-aside</b> helper on top of Redis.
 *
 * <pre>
 *   GET key
 *     |
 *   hit? --yes--> return cached value
 *     |
 *    no
 *     |
 *   load from the source of truth (MySQL / Mongo)
 *     |
 *   SET key value EX ttl
 *     |
 *   return value
 * </pre>
 *
 * <p>Values are stored as JSON strings so they are readable with {@code redis-cli} during an
 * investigation. Every Redis failure is swallowed and logged: <b>a cache outage must never turn
 * into an API outage</b> — callers simply fall back to the loader (see README, "Failure
 * Scenarios"). Hit/miss/error counts are exported as Prometheus metrics per cache name.
 */
@Component
public class RedisJsonCache {

    private static final Logger log = LoggerFactory.getLogger(RedisJsonCache.class);

    private final StringRedisTemplate redis;
    private final ObjectMapper objectMapper;
    private final GameplayMetrics metrics;

    public RedisJsonCache(StringRedisTemplate redis, ObjectMapper objectMapper, GameplayMetrics metrics) {
        this.redis = redis;
        this.objectMapper = objectMapper;
        this.metrics = metrics;
    }

    /**
     * Returns the cached value or loads, caches and returns it.
     *
     * @param cacheName logical name used for metrics (e.g. {@code player_profile})
     * @param key       full Redis key (e.g. {@code player:123})
     */
    public <T> T getOrLoad(String cacheName, String key, Class<T> type, Duration ttl, Supplier<T> loader) {
        Optional<T> cached = get(cacheName, key, type);
        if (cached.isPresent()) {
            return cached.get();
        }
        T value = loader.get();
        if (value != null) {
            put(key, value, ttl);
        }
        return value;
    }

    public <T> Optional<T> get(String cacheName, String key, Class<T> type) {
        try {
            String json = redis.opsForValue().get(key);
            if (json == null) {
                metrics.cacheAccess(cacheName, "miss");
                return Optional.empty();
            }
            metrics.cacheAccess(cacheName, "hit");
            return Optional.of(objectMapper.readValue(json, type));
        } catch (DataAccessException e) {
            // Redis is unreachable: degrade gracefully to the loader.
            metrics.cacheAccess(cacheName, "error");
            log.warn("Redis unavailable while reading {} ({}), falling back to source", key, e.getMessage());
            return Optional.empty();
        } catch (RuntimeException e) {
            // Corrupt / incompatible JSON (e.g. after a deploy that changed the DTO shape): drop it.
            metrics.cacheAccess(cacheName, "error");
            log.warn("Discarding unreadable cache entry {}: {}", key, e.getMessage());
            evict(key);
            return Optional.empty();
        }
    }

    public void put(String key, Object value, Duration ttl) {
        try {
            redis.opsForValue().set(key, objectMapper.writeValueAsString(value), ttl);
        } catch (DataAccessException e) {
            log.warn("Redis unavailable while writing {} ({})", key, e.getMessage());
        }
    }

    public void evict(String key) {
        try {
            redis.delete(key);
        } catch (DataAccessException e) {
            // If we cannot evict, the entry simply expires with its TTL. Logged for visibility.
            log.warn("Redis unavailable while evicting {} ({})", key, e.getMessage());
        }
    }
}
