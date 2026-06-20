package com.atakan.blastscale.level.engine;

import java.util.List;

/** Stateless facade: replay a full move list and report the result. */
public final class BoardEngine {

    private BoardEngine() {
    }

    /**
     * @param extraMovesUsed whether the EXTRA_MOVES booster was activated (adds
     *                       {@link BoardConfig#EXTRA_MOVES_BONUS} moves)
     */
    public static SimulationResult simulate(BoardConfig config, int seed, List<Move> moves, boolean extraMovesUsed) {
        BoardState state = new BoardState(config, seed);
        int moveLimit = config.moveLimit() + (extraMovesUsed ? BoardConfig.EXTRA_MOVES_BONUS : 0);
        for (int i = 0; i < moves.size(); i++) {
            String problem = state.apply(moves.get(i), moveLimit);
            if (problem != null) {
                return new SimulationResult(false, "move " + i + ": " + problem, state.score(), state.movesUsed(),
                        state.hammersUsed(), state.shufflesUsed(), false, 0);
            }
        }
        boolean reached = state.objectiveReached();
        return new SimulationResult(true, null, state.score(), state.movesUsed(), state.hammersUsed(),
                state.shufflesUsed(), reached, reached ? config.starsFor(state.score()) : 0);
    }
}
