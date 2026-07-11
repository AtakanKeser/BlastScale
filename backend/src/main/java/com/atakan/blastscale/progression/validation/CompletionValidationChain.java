package com.atakan.blastscale.progression.validation;

import com.atakan.blastscale.common.metrics.GameplayMetrics;
import org.springframework.stereotype.Component;

import java.util.List;

/** Runs the validators in {@code @Order} order and stops at the first rejection. */
@Component
public class CompletionValidationChain {

    private final List<CompletionValidator> validators;
    private final GameplayMetrics metrics;

    public CompletionValidationChain(List<CompletionValidator> validators, GameplayMetrics metrics) {
        this.validators = validators;
        this.metrics = metrics;
    }

    /** @return the first failing result, or {@code null} when every validator passed */
    public Rejection validate(LevelCompletionContext context) {
        for (CompletionValidator validator : validators) {
            ValidationResult result = validator.validate(context);
            if (!result.valid()) {
                metrics.completionRejected(validator.name());
                return new Rejection(validator.name(), result);
            }
        }
        return null;
    }

    public List<String> validatorNames() {
        return validators.stream().map(CompletionValidator::name).toList();
    }

    public record Rejection(String validator, ValidationResult result) {
    }
}
