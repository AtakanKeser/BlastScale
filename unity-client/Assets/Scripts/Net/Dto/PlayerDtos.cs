using System.Collections.Generic;

namespace BlastScale.Client.Net.Dto
{
    /// <summary>
    /// Snapshot of the player's resources (mirrors <c>WalletSnapshot.java</c>; the profile's
    /// <c>WalletSummary</c> has exactly the same shape so one class serves both).
    /// Lives already include lazy regeneration; <c>nextLifeInSeconds</c> is 0 when lives are full.
    /// Booster keys are "HAMMER", "SHUFFLE", "EXTRA_MOVES".
    /// </summary>
    public sealed class WalletSnapshot
    {
        public long coins;
        public int lives;
        public int maxLives;
        public long nextLifeInSeconds;
        public int stars;
        public Dictionary<string, int> boosters;

        /// <summary>Owned count of a booster type, 0 when the map is missing the key.</summary>
        public int BoosterCount(string boosterType)
        {
            return boosters != null && boosters.TryGetValue(boosterType, out int count) ? count : 0;
        }
    }

    /// <summary>GET /api/v1/players/me (mirrors <c>PlayerProfile.java</c>).</summary>
    public sealed class PlayerProfile
    {
        public long id;
        public string username;
        public string role;
        public int currentLevel;
        public string createdAt;
        public WalletSnapshot wallet;
    }
}
