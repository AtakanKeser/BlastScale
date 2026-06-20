package com.atakan.blastscale.level.engine;

import java.util.List;

/**
 * The subset of a level definition the engine needs. Kept separate from the MongoDB document so
 * the engine is a pure, dependency-free library that tests can drive directly.
 *
 * @param starThresholds ascending scores for 1, 2 and 3 stars (the first one is the objective)
 */
public record BoardConfig(int rows, int cols, int colorCount, int moveLimit, int targetScore, List<Integer> starThresholds) {

    /** Extra moves granted by the EXTRA_MOVES booster. */
    public static final int EXTRA_MOVES_BONUS = 5;

    /** Points for popping a group of {@code size} blocks; quadratic to reward planning big groups. */
    public static int groupScore(int size) {
        return size * size * 10;
    }

    public int starsFor(int score) {
        int stars = 0;
        for (int threshold : starThresholds) {
            if (score >= threshold) {
                stars++;
            }
        }
        return stars;
    }
}
