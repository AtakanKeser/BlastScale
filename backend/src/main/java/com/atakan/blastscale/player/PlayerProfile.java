package com.atakan.blastscale.player;

import java.time.Instant;

/**
 * Read model returned by {@code GET /api/v1/players/me} and cached in Redis under
 * {@code player:{id}}. Wallet information is attached by the economy module (see PlayerService).
 */
public record PlayerProfile(
        long id,
        String username,
        String role,
        int currentLevel,
        Instant createdAt,
        WalletSummary wallet) {

    /** Snapshot of the player's resources at the time the profile was built. */
    public record WalletSummary(
            long coins,
            int lives,
            int maxLives,
            long nextLifeInSeconds,
            int stars,
            java.util.Map<String, Integer> boosters) {
    }
}
