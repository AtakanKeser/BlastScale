using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

namespace BlastScale.Client.Net.Dto
{
    /// <summary>
    /// Well-known remote configuration keys (mirrors <c>ConfigKeys.java</c>) with the same built-in
    /// defaults, used only when the server's config map lacks a key.
    /// </summary>
    public static class ConfigKeys
    {
        public const string DailyRewardCoins = "dailyRewardCoins";
        public const string DailyRewardStreakBonus = "dailyRewardStreakBonus";
        public const string MaxLives = "maxLives";
        public const string LifeRegenerationMinutes = "lifeRegenerationMinutes";
        public const string LifeRefillPrice = "lifeRefillPrice";
        public const string BoosterPrices = "boosterPrices";
        public const string RewardMultiplier = "rewardMultiplier";
        public const string RocketRaceEnabled = "rocketRaceEnabled";
        public const string LeaderboardEnabled = "leaderboardEnabled";

        public const int DefaultDailyRewardCoins = 100;
        public const int DefaultMaxLives = 5;
        public const int DefaultLifeRegenerationMinutes = 30;
        public const int DefaultLifeRefillPrice = 150;

        public static readonly Dictionary<string, int> DefaultBoosterPrices = new Dictionary<string, int>
        {
            { "HAMMER", 100 }, { "SHUFFLE", 80 }, { "EXTRA_MOVES", 120 }
        };
    }

    /// <summary>
    /// GET /api/v1/config (mirrors <c>ClientConfigResponse.java</c>): the effective key/value
    /// configuration for this player (experiments already applied), the experiments the player is
    /// bucketed into, and the server time for countdowns that must not trust the device clock.
    /// </summary>
    public sealed class ClientConfigResponse
    {
        public Dictionary<string, JToken> config;
        public List<ExperimentAssignmentView> experiments;
        public string serverTime;

        /// <summary>Integer config value with a fallback (values may arrive as numbers or numeric strings).</summary>
        public int GetInt(string key, int fallback)
        {
            JToken token = Get(key);
            if (token == null) return fallback;
            try
            {
                return token.Type == JTokenType.String ? int.Parse(token.ToString()) : token.Value<int>();
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        public bool GetBool(string key, bool fallback)
        {
            JToken token = Get(key);
            if (token == null) return fallback;
            try
            {
                return token.Type == JTokenType.String ? bool.Parse(token.ToString()) : token.Value<bool>();
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        public double GetDouble(string key, double fallback)
        {
            JToken token = Get(key);
            if (token == null) return fallback;
            try
            {
                return token.Type == JTokenType.String
                    ? double.Parse(token.ToString(), System.Globalization.CultureInfo.InvariantCulture)
                    : token.Value<double>();
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        /// <summary>A map value such as boosterPrices ({"HAMMER": 100, ...}); falls back when missing or malformed.</summary>
        public Dictionary<string, int> GetIntMap(string key, Dictionary<string, int> fallback)
        {
            JToken token = Get(key);
            if (token == null) return fallback;
            try
            {
                if (token.Type == JTokenType.String)
                {
                    token = JObject.Parse(token.ToString());
                }
                var map = token.ToObject<Dictionary<string, int>>();
                return map ?? fallback;
            }
            catch (Exception)
            {
                return fallback;
            }
        }

        private JToken Get(string key)
        {
            if (config != null && config.TryGetValue(key, out JToken token) && token != null && token.Type != JTokenType.Null)
            {
                return token;
            }
            return null;
        }
    }

    /// <summary>One experiment the player is assigned to: id, key ("reward_boost") and variant ("control"/"double").</summary>
    public sealed class ExperimentAssignmentView
    {
        public long id;
        public string key;
        public string variant;
    }
}
