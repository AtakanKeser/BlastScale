package com.atakan.blastscale.economy;

import java.time.Duration;
import java.time.Instant;

/**
 * Pure life-regeneration arithmetic, shared by reads (display) and writes (consuming a life).
 *
 * <p>Instead of a timer that adds a life every N minutes (which would need a scheduler touching
 * millions of rows), regeneration is computed lazily from {@code livesUpdatedAt}:
 * <pre>
 *   regenerated = floor((now - livesUpdatedAt) / regenInterval)
 *   lives       = min(maxLives, lives + regenerated)
 * </pre>
 * When lives are below the maximum, {@code livesUpdatedAt} moves forward by whole intervals only,
 * so partial progress towards the next life is never lost. When lives are full, the timer resets.
 */
public final class LifeRegeneration {

    private LifeRegeneration() {
    }

    public record Result(int lives, Instant livesUpdatedAt, long nextLifeInSeconds) {
    }

    public static Result apply(int lives, Instant livesUpdatedAt, Instant now, int maxLives, int regenMinutes) {
        if (lives >= maxLives) {
            // Full: nothing to regenerate; the reference point follows "now" so that the next
            // life lost starts its timer from the moment it is lost.
            return new Result(lives, now, 0);
        }
        Duration interval = Duration.ofMinutes(Math.max(1, regenMinutes));
        long elapsed = Duration.between(livesUpdatedAt, now).toMillis();
        if (elapsed < 0) {
            elapsed = 0; // clock skew between replicas: never regenerate "negative" time
        }
        long regenerated = elapsed / interval.toMillis();
        int newLives = (int) Math.min(maxLives, lives + regenerated);
        if (newLives >= maxLives) {
            return new Result(newLives, now, 0);
        }
        Instant newReference = livesUpdatedAt.plusMillis(regenerated * interval.toMillis());
        long nextIn = (newReference.toEpochMilli() + interval.toMillis() - now.toEpochMilli() + 999) / 1000;
        return new Result(newLives, newReference, Math.max(1, nextIn));
    }
}
