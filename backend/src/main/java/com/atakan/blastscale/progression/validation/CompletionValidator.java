package com.atakan.blastscale.progression.validation;

/**
 * One link of the anti-cheat chain (Chain of Responsibility). Validators are Spring beans ordered
 * with {@code @Order}; {@link CompletionValidationChain} runs them until the first failure.
 *
 * <p>Why this pattern: new rules (device fingerprinting, per-level speed profiles, ...) can be
 * added as new classes without changing the completion orchestration in ProgressionService.
 */
public interface CompletionValidator {

    /** Short name used in metrics/telemetry ({@code blastscale_completion_rejected_total{validator=...}}). */
    String name();

    ValidationResult validate(LevelCompletionContext context);
}
