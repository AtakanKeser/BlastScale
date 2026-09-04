namespace BlastScale.Engine
{
    /// <summary>
    /// Outcome of replaying a move list (port of <c>SimulationResult.java</c>).
    /// </summary>
    public sealed class SimulationResult
    {
        /// <summary>false when any move was illegal (out of bounds, single block, over the limit)</summary>
        public bool Valid { get; }

        /// <summary>why it was invalid (null when valid)</summary>
        public string RejectionReason { get; }

        /// <summary>score computed by the replay — on the server this is the only score that counts</summary>
        public int Score { get; }

        /// <summary>TAP moves consumed</summary>
        public int MovesUsed { get; }

        /// <summary>HAMMER boosters consumed</summary>
        public int HammersUsed { get; }

        /// <summary>SHUFFLE boosters consumed</summary>
        public int ShufflesUsed { get; }

        /// <summary>score >= target</summary>
        public bool ObjectiveReached { get; }

        /// <summary>0-3 according to the level's thresholds (0 when the objective was missed)</summary>
        public int Stars { get; }

        public SimulationResult(bool valid, string rejectionReason, int score, int movesUsed, int hammersUsed,
            int shufflesUsed, bool objectiveReached, int stars)
        {
            Valid = valid;
            RejectionReason = rejectionReason;
            Score = score;
            MovesUsed = movesUsed;
            HammersUsed = hammersUsed;
            ShufflesUsed = shufflesUsed;
            ObjectiveReached = objectiveReached;
            Stars = stars;
        }

        public override string ToString()
        {
            return "SimulationResult(valid=" + Valid + ", reason=" + RejectionReason + ", score=" + Score +
                   ", movesUsed=" + MovesUsed + ", hammers=" + HammersUsed + ", shuffles=" + ShufflesUsed +
                   ", objectiveReached=" + ObjectiveReached + ", stars=" + Stars + ")";
        }
    }
}
