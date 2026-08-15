using System.Collections.Generic;
using BlastScale.Engine;

namespace BlastScale.Client.Net.Dto
{
    // DTOs of the gameplay API (mirrors progression/dto/*.java). Field names are the JSON names.

    /// <summary>The engine rules of a level as sent by the server (mirrors <c>BoardConfig.java</c>).</summary>
    public sealed class BoardConfigDto
    {
        public int rows;
        public int cols;
        public int colorCount;
        public int moveLimit;
        public int targetScore;
        public List<int> starThresholds;

        /// <summary>Converts into the pure engine type (the engine has no JSON dependency by design).</summary>
        public BoardConfig ToEngineConfig()
        {
            return new BoardConfig(rows, cols, colorCount, moveLimit, targetScore,
                starThresholds != null ? starThresholds.ToArray() : new int[0]);
        }
    }

    /// <summary>POST /api/v1/levels/{level}/start: the seed reproduces the board, the session id ties the result back.</summary>
    public sealed class LevelStartResponse
    {
        public string sessionId;
        public int level;
        public int seed;
        public BoardConfigDto board;
        public int configurationVersion;
        public int livesRemaining;
        public string startedAt;
        public string expiresAt;
    }

    /// <summary>One recorded move on the wire (mirrors <c>Move.java</c>); type is "TAP" | "HAMMER" | "SHUFFLE".</summary>
    public sealed class MoveDto
    {
        public string type;
        public int row;
        public int col;

        public static MoveDto From(Move move)
        {
            return new MoveDto { type = move.Type.ToString(), row = move.Row, col = move.Col };
        }

        public static List<MoveDto> From(IReadOnlyList<Move> moves)
        {
            var list = new List<MoveDto>(moves.Count);
            foreach (Move move in moves)
            {
                list.Add(From(move));
            }
            return list;
        }
    }

    /// <summary>
    /// POST /api/v1/levels/{level}/complete. The server replays <c>moves</c>; <c>score</c> and
    /// <c>movesUsed</c> are only cross-checked, never trusted.
    /// </summary>
    public sealed class LevelCompleteRequest
    {
        public string sessionId;
        public int score;
        public int movesUsed;
        public List<MoveDto> moves;
        public bool extraMovesUsed;
    }

    /// <summary>What the player receives for a level (mirrors <c>Reward.java</c>).</summary>
    public sealed class Reward
    {
        public long coins;
        public int stars;
        public double multiplier;
        public string strategy;
    }

    /// <summary>Points awarded by a live event on completion (mirrors <c>EventPointsAwarded.java</c>).</summary>
    public sealed class EventPointsAwarded
    {
        public long eventId;
        public string name;
        public string type;
        public long points;
        public long totalPoints;
    }

    /// <summary>Response of /complete; status is "COMPLETED" or "ALREADY_PROCESSED" (retry of a closed session).</summary>
    public sealed class LevelCompleteResponse
    {
        public const string Completed = "COMPLETED";
        public const string AlreadyProcessed = "ALREADY_PROCESSED";

        public string status;
        public string sessionId;
        public int level;
        public int score;
        public int stars;
        public bool firstClear;
        public bool newBestScore;
        public Reward reward;
        public WalletSnapshot wallet;
        public int nextLevel;
        public List<EventPointsAwarded> eventPoints;
    }

    /// <summary>POST /api/v1/levels/{level}/fail — moves are needed so used boosters can be charged.</summary>
    public sealed class LevelFailRequest
    {
        public string sessionId;
        public List<MoveDto> moves;
        public bool extraMovesUsed;
    }

    /// <summary>Response of /fail; status "FAILED" or "ALREADY_PROCESSED".</summary>
    public sealed class LevelFailResponse
    {
        public string status;
        public string sessionId;
        public int level;
        public int score;
        public WalletSnapshot wallet;
    }

    /// <summary>GET /api/v1/progress (mirrors <c>ProgressView.java</c>).</summary>
    public sealed class ProgressView
    {
        public int currentLevel;
        public int totalStars;
        public List<LevelEntry> levels;

        public sealed class LevelEntry
        {
            public int level;
            public int stars;
            public int bestScore;
            public int attempts;
            public bool cleared;
            public string completedAt;
        }
    }
}
