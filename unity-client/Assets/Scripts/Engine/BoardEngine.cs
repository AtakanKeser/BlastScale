using System.Collections.Generic;

namespace BlastScale.Engine
{
    /// <summary>
    /// Stateless facade (port of <c>BoardEngine.java</c>): replay a full move list and report the
    /// result. The server runs exactly this to validate a completion; the client uses it in tests
    /// and to double-check a session before submitting it.
    /// </summary>
    public static class BoardEngine
    {
        /// <param name="extraMovesUsed">
        /// whether the EXTRA_MOVES booster was activated (adds <see cref="BoardConfig.ExtraMovesBonus"/> moves)
        /// </param>
        public static SimulationResult Simulate(BoardConfig config, int seed, IReadOnlyList<Move> moves, bool extraMovesUsed)
        {
            var state = new BoardState(config, seed);
            int moveLimit = config.MoveLimit + (extraMovesUsed ? BoardConfig.ExtraMovesBonus : 0);
            for (int i = 0; i < moves.Count; i++)
            {
                string problem = state.Apply(moves[i], moveLimit);
                if (problem != null)
                {
                    return new SimulationResult(false, "move " + i + ": " + problem, state.Score, state.MovesUsed,
                        state.HammersUsed, state.ShufflesUsed, false, 0);
                }
            }
            bool reached = state.ObjectiveReached;
            return new SimulationResult(true, null, state.Score, state.MovesUsed, state.HammersUsed,
                state.ShufflesUsed, reached, reached ? config.StarsFor(state.Score) : 0);
        }
    }
}
