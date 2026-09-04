package com.atakan.blastscale.progression.validation;

import com.atakan.blastscale.common.exception.ErrorCode;

import java.util.Map;

/** Outcome of one validator. */
public record ValidationResult(boolean valid, ErrorCode code, String message, Map<String, Object> details) {

    public static ValidationResult ok() {
        return new ValidationResult(true, null, null, Map.of());
    }

    public static ValidationResult fail(ErrorCode code, String message) {
        return new ValidationResult(false, code, message, Map.of());
    }

    public static ValidationResult fail(ErrorCode code, String message, Map<String, Object> details) {
        return new ValidationResult(false, code, message, details);
    }
}
