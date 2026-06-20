package com.atakan.blastscale.level.engine;

import java.util.ArrayList;
import java.util.List;

/**
 * Simple bot: always pops the largest group. Used to calibrate level difficulty, to generate valid
 * move lists in tests and by the k6 load test (which ports the same logic to JavaScript).
 */
public final class GreedySolver {

    private GreedySolver() {
    }

    /** @return the moves played (stops as soon as the objective is reached or moves run out) */
    public static List<Move> solve(BoardConfig config, int seed) {
        BoardState state = new BoardState(config, seed);
        List<Move> moves = new ArrayList<>();
        while (!state.objectiveReached() && state.movesUsed() < config.moveLimit()) {
            List<int[]> best = null;
            for (List<int[]> group : state.groups()) {
                if (best == null || group.size() > best.size()) {
                    best = group;
                }
            }
            if (best == null) {
                break;
            }
            Move move = Move.tap(best.get(0)[0], best.get(0)[1]);
            if (state.apply(move, config.moveLimit()) != null) {
                break;
            }
            moves.add(move);
        }
        return moves;
    }
}
