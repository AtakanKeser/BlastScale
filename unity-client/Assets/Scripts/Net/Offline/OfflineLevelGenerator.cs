using System;
using System.Collections.Generic;
using BlastScale.Client.Net.Dto;
using Newtonsoft.Json.Linq;

namespace BlastScale.Client.Net.Offline
{
    /// <summary>
    /// The server's <c>ProceduralLevelGenerator</c> formula, reproduced for the offline demo so
    /// local levels ramp exactly like the real ones: more colours, fewer moves and a higher
    /// target as the level number grows.
    /// <code>
    ///   colours   = 4 (level &lt; 6) | 5 (level &lt; 20) | 6
    ///   moveLimit = max(14, 20 - level / 12)
    ///   target    = round(fraction * moveLimit * pointsPerMove / 10) * 10
    ///   fraction  = 0.35 + 0.45 * min(1, level / 60)
    ///   pointsPerMove = 250 / 190 / 125 for 4 / 5 / 6 colours
    ///   stars     = [target, 1.25 x target, 1.5 x target]
    /// </code>
    /// </summary>
    public static class OfflineLevelGenerator
    {
        public const int Rows = 8;
        public const int Cols = 8;
        public const string Source = "offline";

        /// <summary>The board rules of a level (what /levels/{n}/start returns as <c>board</c>).</summary>
        public static BoardConfigDto Board(int level)
        {
            int colors = level < 6 ? 4 : level < 20 ? 5 : 6;
            int moveLimit = Math.Max(14, 20 - level / 12);
            double pointsPerMove = colors == 4 ? 250 : colors == 5 ? 190 : 125;
            double fraction = 0.35 + 0.45 * Math.Min(1.0, level / 60.0);
            int target = (int)Math.Round(fraction * moveLimit * pointsPerMove / 10, MidpointRounding.AwayFromZero) * 10;
            return new BoardConfigDto
            {
                rows = Rows,
                cols = Cols,
                colorCount = colors,
                moveLimit = moveLimit,
                targetScore = target,
                starThresholds = new List<int>
                {
                    target,
                    (int)Math.Round(target * 1.25, MidpointRounding.AwayFromZero),
                    (int)Math.Round(target * 1.5, MidpointRounding.AwayFromZero)
                }
            };
        }

        /// <summary>The read-only preview document (what GET /levels/{n} returns).</summary>
        public static LevelDefinition Definition(int level, DateTime now)
        {
            BoardConfigDto board = Board(level);
            return new LevelDefinition
            {
                id = "offline-" + level,
                levelNumber = level,
                version = 1,
                rows = board.rows,
                cols = board.cols,
                colorCount = board.colorCount,
                moveLimit = board.moveLimit,
                targetScore = board.targetScore,
                starThresholds = board.starThresholds,
                specialRules = new Dictionary<string, JToken> { { "generator", "offline" } },
                source = Source,
                updatedAt = OfflineApiClient.Iso(now)
            };
        }
    }
}
