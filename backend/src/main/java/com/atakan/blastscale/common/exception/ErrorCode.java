package com.atakan.blastscale.common.exception;

import org.springframework.http.HttpStatus;

/**
 * Every business error the API can return, together with the HTTP status it maps to.
 *
 * <p>Clients (Unity, admin panel, k6 scripts) switch on the stable {@code code} string instead of
 * parsing human readable messages, so messages can change freely without breaking anything.
 */
public enum ErrorCode {
    // ----- generic -----
    VALIDATION_ERROR(HttpStatus.BAD_REQUEST),
    MALFORMED_REQUEST(HttpStatus.BAD_REQUEST),
    UNAUTHORIZED(HttpStatus.UNAUTHORIZED),
    FORBIDDEN(HttpStatus.FORBIDDEN),
    NOT_FOUND(HttpStatus.NOT_FOUND),
    CONFLICT(HttpStatus.CONFLICT),
    CONCURRENT_MODIFICATION(HttpStatus.CONFLICT),
    RATE_LIMITED(HttpStatus.TOO_MANY_REQUESTS),
    IDEMPOTENT_REQUEST_IN_PROGRESS(HttpStatus.CONFLICT),
    INTERNAL_ERROR(HttpStatus.INTERNAL_SERVER_ERROR),

    // ----- auth / player -----
    USERNAME_TAKEN(HttpStatus.CONFLICT),
    INVALID_CREDENTIALS(HttpStatus.UNAUTHORIZED),
    PLAYER_NOT_FOUND(HttpStatus.NOT_FOUND),

    // ----- economy -----
    INSUFFICIENT_COINS(HttpStatus.CONFLICT),
    NO_LIVES_LEFT(HttpStatus.CONFLICT),
    INSUFFICIENT_BOOSTERS(HttpStatus.CONFLICT),
    LIVES_ALREADY_FULL(HttpStatus.CONFLICT),
    DAILY_REWARD_ALREADY_CLAIMED(HttpStatus.CONFLICT),
    DUPLICATE_TRANSACTION(HttpStatus.CONFLICT),

    // ----- progression / anti-cheat -----
    LEVEL_LOCKED(HttpStatus.FORBIDDEN),
    LEVEL_NOT_FOUND(HttpStatus.NOT_FOUND),
    SESSION_NOT_FOUND(HttpStatus.NOT_FOUND),
    SESSION_NOT_ACTIVE(HttpStatus.CONFLICT),
    SESSION_EXPIRED(HttpStatus.CONFLICT),
    SESSION_LEVEL_MISMATCH(HttpStatus.UNPROCESSABLE_ENTITY),
    SUSPICIOUS_DURATION(HttpStatus.UNPROCESSABLE_ENTITY),
    SCORE_OUT_OF_RANGE(HttpStatus.UNPROCESSABLE_ENTITY),
    TOO_MANY_MOVES(HttpStatus.UNPROCESSABLE_ENTITY),
    INVALID_MOVE_SEQUENCE(HttpStatus.UNPROCESSABLE_ENTITY),
    SCORE_MISMATCH(HttpStatus.UNPROCESSABLE_ENTITY),
    OBJECTIVE_NOT_REACHED(HttpStatus.UNPROCESSABLE_ENTITY),

    // ----- live ops -----
    EVENT_NOT_FOUND(HttpStatus.NOT_FOUND),
    EVENT_INVALID_STATE(HttpStatus.CONFLICT),
    EVENT_INVALID_CONFIGURATION(HttpStatus.BAD_REQUEST),
    EXPERIMENT_NOT_FOUND(HttpStatus.NOT_FOUND),
    EXPERIMENT_INVALID_STATE(HttpStatus.CONFLICT),
    EXPERIMENT_INVALID_VARIANTS(HttpStatus.BAD_REQUEST),
    CONFIG_KEY_NOT_FOUND(HttpStatus.NOT_FOUND),
    LEADERBOARD_ALREADY_FINALIZED(HttpStatus.CONFLICT),
    LEADERBOARD_SEASON_ACTIVE(HttpStatus.CONFLICT),
    LEADERBOARD_UNAVAILABLE(HttpStatus.SERVICE_UNAVAILABLE);

    private final HttpStatus status;

    ErrorCode(HttpStatus status) {
        this.status = status;
    }

    public HttpStatus status() {
        return status;
    }
}
