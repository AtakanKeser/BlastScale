package com.atakan.blastscale.common.api;

import com.atakan.blastscale.common.exception.BlastScaleException;
import com.atakan.blastscale.common.exception.ErrorCode;
import jakarta.servlet.http.HttpServletRequest;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;
import org.springframework.dao.DataIntegrityViolationException;
import org.springframework.dao.OptimisticLockingFailureException;
import org.springframework.dao.PessimisticLockingFailureException;
import org.springframework.http.HttpStatus;
import org.springframework.http.ResponseEntity;
import org.springframework.http.converter.HttpMessageNotReadableException;
import org.springframework.security.access.AccessDeniedException;
import org.springframework.security.core.AuthenticationException;
import org.springframework.validation.FieldError;
import org.springframework.web.bind.MethodArgumentNotValidException;
import org.springframework.web.bind.MissingRequestHeaderException;
import org.springframework.web.bind.annotation.ExceptionHandler;
import org.springframework.web.bind.annotation.RestControllerAdvice;
import org.springframework.web.servlet.resource.NoResourceFoundException;

import java.time.Clock;
import java.time.Instant;
import java.util.LinkedHashMap;
import java.util.Map;

/**
 * Translates exceptions into the uniform {@link ApiError} JSON body.
 *
 * <p>Design rules:
 * <ul>
 *   <li>Business errors ({@link BlastScaleException}) are expected and logged at DEBUG only.</li>
 *   <li>Concurrency failures (optimistic lock / unique constraint) become 409 so that a client
 *       can safely retry; they are never surfaced as 500.</li>
 *   <li>Everything unexpected is logged with a stack trace and returned as an opaque 500 —
 *       internal details never leak to the client.</li>
 * </ul>
 */
@RestControllerAdvice
public class GlobalExceptionHandler {

    private static final Logger log = LoggerFactory.getLogger(GlobalExceptionHandler.class);

    private final Clock clock;

    public GlobalExceptionHandler(Clock clock) {
        this.clock = clock;
    }

    @ExceptionHandler(BlastScaleException.class)
    public ResponseEntity<ApiError> handleBusiness(BlastScaleException ex, HttpServletRequest request) {
        log.debug("Business error {} on {}: {}", ex.code(), request.getRequestURI(), ex.getMessage());
        return build(ex.code(), ex.getMessage(), ex.details(), request);
    }

    @ExceptionHandler(MethodArgumentNotValidException.class)
    public ResponseEntity<ApiError> handleValidation(MethodArgumentNotValidException ex, HttpServletRequest request) {
        // Collect "field -> message" pairs so the client can highlight the offending inputs.
        Map<String, Object> fieldErrors = new LinkedHashMap<>();
        for (FieldError fieldError : ex.getBindingResult().getFieldErrors()) {
            fieldErrors.put(fieldError.getField(), fieldError.getDefaultMessage());
        }
        return build(ErrorCode.VALIDATION_ERROR, "Request validation failed", fieldErrors, request);
    }

    @ExceptionHandler({HttpMessageNotReadableException.class, MissingRequestHeaderException.class})
    public ResponseEntity<ApiError> handleMalformed(Exception ex, HttpServletRequest request) {
        return build(ErrorCode.MALFORMED_REQUEST, "Malformed request: " + rootMessage(ex), Map.of(), request);
    }

    @ExceptionHandler(NoResourceFoundException.class)
    public ResponseEntity<ApiError> handleNoResource(NoResourceFoundException ex, HttpServletRequest request) {
        return build(ErrorCode.NOT_FOUND, "No such endpoint", Map.of(), request);
    }

    @ExceptionHandler(AuthenticationException.class)
    public ResponseEntity<ApiError> handleAuthentication(AuthenticationException ex, HttpServletRequest request) {
        return build(ErrorCode.UNAUTHORIZED, "Authentication required", Map.of(), request);
    }

    @ExceptionHandler(AccessDeniedException.class)
    public ResponseEntity<ApiError> handleAccessDenied(AccessDeniedException ex, HttpServletRequest request) {
        return build(ErrorCode.FORBIDDEN, "You are not allowed to perform this action", Map.of(), request);
    }

    /**
     * Two requests raced on the same row (e.g. the same wallet). The losing request gets a 409
     * and can retry; nothing was partially applied because the transaction rolled back.
     */
    @ExceptionHandler({OptimisticLockingFailureException.class, PessimisticLockingFailureException.class})
    public ResponseEntity<ApiError> handleLockConflict(Exception ex, HttpServletRequest request) {
        // Covers @Version conflicts as well as InnoDB deadlocks / lock wait timeouts
        // (CannotAcquireLockException, DeadlockLoserDataAccessException): the transaction was rolled
        // back cleanly, so a retry is safe and the client is told so with a 409, never a 500.
        log.warn("Lock conflict on {}: {}", request.getRequestURI(), rootMessage(ex));
        return build(ErrorCode.CONCURRENT_MODIFICATION, "The resource was modified concurrently, please retry", Map.of(), request);
    }

    /**
     * A unique constraint fired. In this code base that almost always means a duplicate reward
     * or a duplicate registration slipped past the application-level checks — the database is
     * the last line of defence, and we answer with 409 rather than 500.
     */
    @ExceptionHandler(DataIntegrityViolationException.class)
    public ResponseEntity<ApiError> handleDataIntegrity(DataIntegrityViolationException ex, HttpServletRequest request) {
        log.warn("Data integrity violation on {}: {}", request.getRequestURI(), rootMessage(ex));
        return build(ErrorCode.CONFLICT, "The request conflicts with existing data", Map.of(), request);
    }

    @ExceptionHandler(Exception.class)
    public ResponseEntity<ApiError> handleUnexpected(Exception ex, HttpServletRequest request) {
        log.error("Unhandled exception on {} {}", request.getMethod(), request.getRequestURI(), ex);
        return build(ErrorCode.INTERNAL_ERROR, "Unexpected error", Map.of(), request);
    }

    private ResponseEntity<ApiError> build(ErrorCode code, String message, Map<String, Object> details, HttpServletRequest request) {
        HttpStatus status = code.status();
        ApiError body = new ApiError(code.name(), message, details, Instant.now(clock), request.getRequestURI());
        return ResponseEntity.status(status).body(body);
    }

    private static String rootMessage(Throwable t) {
        Throwable root = t;
        while (root.getCause() != null && root.getCause() != root) {
            root = root.getCause();
        }
        String msg = root.getMessage();
        return msg == null ? root.getClass().getSimpleName() : msg;
    }
}
