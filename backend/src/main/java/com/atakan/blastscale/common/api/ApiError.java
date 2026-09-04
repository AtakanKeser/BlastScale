package com.atakan.blastscale.common.api;

import java.time.Instant;
import java.util.Map;

/**
 * Uniform JSON error body returned by every endpoint.
 *
 * <pre>
 * {
 *   "code": "NO_LIVES_LEFT",
 *   "message": "You have no lives left. Next life in 1240 seconds.",
 *   "details": { "nextLifeInSeconds": 1240 },
 *   "timestamp": "2026-09-04T12:00:00Z",
 *   "path": "/api/v1/levels/42/start"
 * }
 * </pre>
 */
public record ApiError(
        String code,
        String message,
        Map<String, Object> details,
        Instant timestamp,
        String path) {
}
