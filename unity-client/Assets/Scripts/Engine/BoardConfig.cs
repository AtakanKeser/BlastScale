using System;
using System.Collections.Generic;

namespace BlastScale.Engine
{
    /// <summary>
    /// The subset of a level definition the engine needs (port of <c>BoardConfig.java</c>).
    /// Kept free of any Unity or JSON dependency so the engine stays a pure, unit-testable library;
    /// the network layer converts its DTO into this class.
    /// </summary>
    public sealed class BoardConfig
    {
        /// <summary>Extra moves granted by the EXTRA_MOVES booster.</summary>
        public const int ExtraMovesBonus = 5;

        public int Rows { get; }
        public int Cols { get; }
        public int ColorCount { get; }
        public int MoveLimit { get; }
        public int TargetScore { get; }

        /// <summary>Ascending scores for 1, 2 and 3 stars (the first one is the objective).</summary>
        public IReadOnlyList<int> StarThresholds { get; }

        public BoardConfig(int rows, int cols, int colorCount, int moveLimit, int targetScore, IReadOnlyList<int> starThresholds)
        {
            if (rows <= 0) throw new ArgumentOutOfRangeException(nameof(rows));
            if (cols <= 0) throw new ArgumentOutOfRangeException(nameof(cols));
            if (colorCount <= 0) throw new ArgumentOutOfRangeException(nameof(colorCount));
            Rows = rows;
            Cols = cols;
            ColorCount = colorCount;
            MoveLimit = moveLimit;
            TargetScore = targetScore;
            StarThresholds = starThresholds ?? Array.Empty<int>();
        }

        /// <summary>Points for popping a group of <paramref name="size"/> blocks; quadratic to reward planning big groups.</summary>
        public static int GroupScore(int size)
        {
            return size * size * 10;
        }

        /// <summary>Number of thresholds the score reaches, exactly like the Java version (no ordering assumption).</summary>
        public int StarsFor(int score)
        {
            int stars = 0;
            foreach (int threshold in StarThresholds)
            {
                if (score >= threshold)
                {
                    stars++;
                }
            }
            return stars;
        }
    }
}
