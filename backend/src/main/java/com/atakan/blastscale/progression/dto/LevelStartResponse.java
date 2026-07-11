package com.atakan.blastscale.progression.dto;

import com.atakan.blastscale.level.engine.BoardConfig;

import java.time.Instant;

/**
 * Everything the client needs to play: the seed reproduces the board locally, the board config
 * defines the rules, the session id ties the result back to this attempt.
 */
public record LevelStartResponse(
        String sessionId,
        int level,
        int seed,
        BoardConfig board,
        int configurationVersion,
        int livesRemaining,
        Instant startedAt,
        Instant expiresAt) {
}
