using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace BlastScale.Client.Net.Dto
{
    // DTOs of the leaderboard, live event and level preview endpoints. Field names are the JSON names.

    /// <summary>GET /api/v1/leaderboards/weekly (mirrors <c>LeaderboardView.java</c>); myRank is null when unranked.</summary>
    public sealed class LeaderboardView
    {
        public string season;
        public string endsAt;
        public bool finalized;
        public List<Entry> players;
        public int? myRank;
        public long myScore;

        public sealed class Entry
        {
            public int rank;
            public long playerId;
            public string name;
            public long score;
        }
    }

    /// <summary>A row of an event ranking (mirrors <c>LiveEventView.Standing</c>).</summary>
    public sealed class Standing
    {
        public int rank;
        public long playerId;
        public string name;
        public long points;
        public int? rewardCoins;
    }

    /// <summary>GET /api/v1/events (mirrors <c>PlayerEventView.java</c>): the event, my standing and the top 10.</summary>
    public sealed class PlayerEventView
    {
        public long id;
        public string type;
        public string name;
        public string startAt;
        public string endAt;
        public long secondsRemaining;
        public Dictionary<string, JToken> configuration;
        public long myPoints;
        public int? myRank;
        public bool eligible;
        public List<Standing> top;
    }

    /// <summary>GET /api/v1/levels/{n} (mirrors <c>LevelDefinition.java</c>): read-only level preview.</summary>
    public sealed class LevelDefinition
    {
        public string id;
        public int levelNumber;
        public int version;
        public int rows;
        public int cols;
        public int colorCount;
        public int moveLimit;
        public int targetScore;
        public List<int> starThresholds;
        public Dictionary<string, JToken> specialRules;
        public string source;
        public string updatedAt;
    }
}
