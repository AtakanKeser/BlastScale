package com.atakan.blastscale.common.idempotency;

/**
 * Outcome of an idempotent execution.
 *
 * @param value    the response, either freshly computed or replayed from the store
 * @param replayed {@code true} when the request had already been processed and the stored
 *                 response was returned without executing the business logic again
 */
public record IdempotentResult<T>(T value, boolean replayed) {
}
