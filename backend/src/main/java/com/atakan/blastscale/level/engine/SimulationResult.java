package com.atakan.blastscale.level.engine;

/**
 * Outcome of replaying a move list on the server.
 *
 * @param valid            false when any move was illegal (out of bounds, single block, over the limit)
 * @param rejectionReason  why it was invalid (null when valid)
 * @param score            score computed by the server — the only score that counts
 * @param movesUsed        TAP moves consumed
 * @param hammersUsed      HAMMER boosters consumed
 * @param shufflesUsed     SHUFFLE boosters consumed
 * @param objectiveReached score >= target
 * @param stars            0-3 according to the level's thresholds (0 when the objective was missed)
 */
public record SimulationResult(
        boolean valid,
        String rejectionReason,
        int score,
        int movesUsed,
        int hammersUsed,
        int shufflesUsed,
        boolean objectiveReached,
        int stars) {
}
