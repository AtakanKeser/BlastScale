using System;
using System.Collections.Generic;
using BlastScale.Client.Net.Dto;
using Newtonsoft.Json;
using UnityEngine;

namespace BlastScale.Client.Net.Offline
{
    /// <summary>
    /// Everything the offline demo remembers between runs (coins, lives, level, boosters, daily
    /// reward, best scores...), stored as JSON in PlayerPrefs. It plays the role of the server's
    /// database; nothing in here is ever sent anywhere. Field names are lower camel case because
    /// the object is serialised with the same settings as the API DTOs.
    /// </summary>
    public sealed class OfflineSave
    {
        public const string PrefKey = "blastscale.offline.save";
        public const int LifeRegenerationMinutes = 30;

        public long coins = 500;
        public int lives = 5;
        public int maxLives = 5;

        /// <summary>Unix seconds when the next life regenerates; 0 while lives are full.</summary>
        public long nextLifeAt;

        public int stars;
        public int currentLevel = 1;
        public Dictionary<string, int> boosters = new Dictionary<string, int>
        {
            { BoosterTypes.Hammer, 2 }, { BoosterTypes.Shuffle, 1 }, { BoosterTypes.ExtraMoves, 1 }
        };

        /// <summary>Best stars per level (key = level number as string, JSON friendly).</summary>
        public Dictionary<string, int> bestStars = new Dictionary<string, int>();

        public Dictionary<string, int> bestScores = new Dictionary<string, int>();

        /// <summary>"yyyy-MM-dd" (UTC) of the last daily reward claim; null when never claimed.</summary>
        public string lastDailyClaimDay;

        public int dailyStreak;

        /// <summary>Score accumulated in the current weekly leaderboard season.</summary>
        public long weeklyScore;

        public string weeklySeason;

        /// <summary>Rocket race points (one per cleared level while the event runs).</summary>
        public long rocketPoints;

        public static OfflineSave Load()
        {
            string json = PlayerPrefs.GetString(PrefKey, "");
            if (!string.IsNullOrEmpty(json))
            {
                try
                {
                    OfflineSave save = JsonConvert.DeserializeObject<OfflineSave>(json, ApiClient.JsonSettings);
                    if (save != null)
                    {
                        save.boosters ??= new Dictionary<string, int>();
                        save.bestStars ??= new Dictionary<string, int>();
                        save.bestScores ??= new Dictionary<string, int>();
                        return save;
                    }
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[Offline] Could not read the saved demo state, starting fresh: " + e.Message);
                }
            }
            return new OfflineSave();
        }

        public void Save()
        {
            PlayerPrefs.SetString(PrefKey, JsonConvert.SerializeObject(this, ApiClient.JsonSettings));
            PlayerPrefs.Save();
        }

        /// <summary>Forgets the demo progress (used by tests and when the player wants a fresh start).</summary>
        public static void Reset()
        {
            PlayerPrefs.DeleteKey(PrefKey);
            PlayerPrefs.Save();
        }

        /// <summary>Lazily regenerates lives up to now, like the server does on every wallet read.</summary>
        public void RegenerateLives(long nowUnix)
        {
            long interval = LifeRegenerationMinutes * 60L;
            while (lives < maxLives && nextLifeAt > 0 && nowUnix >= nextLifeAt)
            {
                lives++;
                nextLifeAt += interval;
            }
            if (lives >= maxLives)
            {
                nextLifeAt = 0;
            }
        }

        /// <summary>Consumes one life and starts the regeneration timer when the wallet was full.</summary>
        public void SpendLife(long nowUnix)
        {
            bool wasFull = lives >= maxLives;
            lives = Math.Max(0, lives - 1);
            if (wasFull || nextLifeAt == 0)
            {
                nextLifeAt = nowUnix + LifeRegenerationMinutes * 60L;
            }
        }

        public long NextLifeInSeconds(long nowUnix)
        {
            if (lives >= maxLives || nextLifeAt == 0) return 0;
            return Math.Max(0, nextLifeAt - nowUnix);
        }

        public int Booster(string type)
        {
            return boosters.TryGetValue(type, out int count) ? count : 0;
        }

        public void AddBooster(string type, int delta)
        {
            boosters[type] = Math.Max(0, Booster(type) + delta);
        }

        public WalletSnapshot ToWallet(long nowUnix)
        {
            return new WalletSnapshot
            {
                coins = coins,
                lives = lives,
                maxLives = maxLives,
                nextLifeInSeconds = NextLifeInSeconds(nowUnix),
                stars = stars,
                boosters = new Dictionary<string, int>(boosters)
            };
        }
    }
}
