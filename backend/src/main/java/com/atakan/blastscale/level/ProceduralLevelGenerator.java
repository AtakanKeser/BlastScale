package com.atakan.blastscale.level;

import java.time.Instant;
import java.util.List;
import java.util.Map;

/**
 * Generates a sensible level when no hand-made document exists (or MongoDB is unreachable).
 * Difficulty ramps with the level number: more colours, fewer moves, higher target.
 * The curve was calibrated with {@code GreedySolver} so that a simple "largest group first"
 * strategy clears most levels but rarely with three stars.
 */
public final class ProceduralLevelGenerator {

    public static final String SOURCE = "procedural";

    private ProceduralLevelGenerator() {
    }

    public static LevelDefinition generate(int level, Instant now) {
        int colors = level < 6 ? 4 : level < 20 ? 5 : 6;
        int moveLimit = Math.max(14, 20 - level / 12);
        // Average points the greedy bot scores per move for a given colour count (measured).
        double pointsPerMove = switch (colors) {
            case 4 -> 250;
            case 5 -> 190;
            default -> 125;
        };
        // Fraction of the bot's expected total the player must reach: 35% at level 1, 80% from level 60.
        double fraction = 0.35 + 0.45 * Math.min(1.0, level / 60.0);
        int target = (int) Math.round(fraction * moveLimit * pointsPerMove / 10) * 10;
        List<Integer> thresholds = List.of(target, (int) Math.round(target * 1.25), (int) Math.round(target * 1.5));
        return new LevelDefinition(level, 1, 8, 8, colors, moveLimit, target, thresholds,
                Map.of("generator", "v2"), SOURCE, now);
    }
}
