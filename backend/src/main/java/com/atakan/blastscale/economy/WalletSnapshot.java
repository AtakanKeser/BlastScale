package com.atakan.blastscale.economy;

import java.util.Map;

/**
 * Read model of a wallet as returned to clients. Lives already include lazy regeneration;
 * {@code nextLifeInSeconds} is 0 when lives are full.
 */
public record WalletSnapshot(
        long coins,
        int lives,
        int maxLives,
        long nextLifeInSeconds,
        int stars,
        Map<String, Integer> boosters) {
}
