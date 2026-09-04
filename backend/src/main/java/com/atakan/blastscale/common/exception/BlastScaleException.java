package com.atakan.blastscale.common.exception;

import java.util.Map;

/**
 * Single runtime exception type used across all modules.
 *
 * <p>Instead of dozens of tiny exception classes, a business failure is expressed as an
 * {@link ErrorCode} (which already knows its HTTP status) plus a message and optional details.
 * {@link com.atakan.blastscale.common.api.GlobalExceptionHandler} turns it into a JSON
 * {@link com.atakan.blastscale.common.api.ApiError} response.
 */
public class BlastScaleException extends RuntimeException {

    private final ErrorCode code;
    private final Map<String, Object> details;

    public BlastScaleException(ErrorCode code, String message) {
        this(code, message, Map.of());
    }

    public BlastScaleException(ErrorCode code, String message, Map<String, Object> details) {
        super(message);
        this.code = code;
        this.details = details == null ? Map.of() : Map.copyOf(details);
    }

    public ErrorCode code() {
        return code;
    }

    public Map<String, Object> details() {
        return details;
    }

    // ----- small factories for the most common cases, keeps call sites readable -----

    public static BlastScaleException notFound(ErrorCode code, String message) {
        return new BlastScaleException(code, message);
    }

    public static BlastScaleException conflict(ErrorCode code, String message) {
        return new BlastScaleException(code, message);
    }
}
