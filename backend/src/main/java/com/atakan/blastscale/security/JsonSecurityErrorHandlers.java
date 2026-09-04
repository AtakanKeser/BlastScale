package com.atakan.blastscale.security;

import com.atakan.blastscale.common.api.ApiError;
import com.atakan.blastscale.common.exception.ErrorCode;
import jakarta.servlet.http.HttpServletRequest;
import jakarta.servlet.http.HttpServletResponse;
import org.springframework.http.MediaType;
import org.springframework.security.access.AccessDeniedException;
import org.springframework.security.core.AuthenticationException;
import org.springframework.security.web.AuthenticationEntryPoint;
import org.springframework.security.web.access.AccessDeniedHandler;
import org.springframework.stereotype.Component;
import tools.jackson.databind.ObjectMapper;

import java.io.IOException;
import java.time.Clock;
import java.time.Instant;
import java.util.Map;

/**
 * Security failures happen inside the filter chain, before any controller — so the
 * {@code @RestControllerAdvice} cannot format them. These handlers write the same
 * {@link ApiError} JSON shape for 401 and 403 so clients see one consistent error contract.
 */
@Component
public class JsonSecurityErrorHandlers implements AuthenticationEntryPoint, AccessDeniedHandler {

    private final ObjectMapper objectMapper;
    private final Clock clock;

    public JsonSecurityErrorHandlers(ObjectMapper objectMapper, Clock clock) {
        this.objectMapper = objectMapper;
        this.clock = clock;
    }

    @Override
    public void commence(HttpServletRequest request, HttpServletResponse response,
                         AuthenticationException authException) throws IOException {
        write(request, response, ErrorCode.UNAUTHORIZED, "Authentication required");
    }

    @Override
    public void handle(HttpServletRequest request, HttpServletResponse response,
                       AccessDeniedException accessDeniedException) throws IOException {
        write(request, response, ErrorCode.FORBIDDEN, "You are not allowed to perform this action");
    }

    void write(HttpServletRequest request, HttpServletResponse response, ErrorCode code, String message)
            throws IOException {
        response.setStatus(code.status().value());
        response.setContentType(MediaType.APPLICATION_JSON_VALUE);
        ApiError body = new ApiError(code.name(), message, Map.of(), Instant.now(clock), request.getRequestURI());
        response.getWriter().write(objectMapper.writeValueAsString(body));
    }
}
