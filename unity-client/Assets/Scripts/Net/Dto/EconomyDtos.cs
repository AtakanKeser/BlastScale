namespace BlastScale.Client.Net.Dto
{
    // DTOs of /api/v1/economy/* (mirrors economy/dto/*.java). Field names are the JSON names.

    /// <summary>Booster type names exactly as the server's <c>BoosterType</c> enum spells them.</summary>
    public static class BoosterTypes
    {
        public const string Hammer = "HAMMER";
        public const string Shuffle = "SHUFFLE";
        public const string ExtraMoves = "EXTRA_MOVES";

        public static readonly string[] All = { Hammer, Shuffle, ExtraMoves };

        /// <summary>Human readable label for the shop and the gameplay HUD.</summary>
        public static string Label(string type)
        {
            switch (type)
            {
                case Hammer: return "Hammer";
                case Shuffle: return "Shuffle";
                case ExtraMoves: return "+5 Moves";
                default: return type;
            }
        }
    }

    /// <summary>GET /api/v1/economy/daily-reward.</summary>
    public sealed class DailyRewardStatus
    {
        public bool available;
        public int currentStreak;
        public int nextRewardCoins;
        public string nextClaimAt;
    }

    /// <summary>POST /api/v1/economy/daily-reward.</summary>
    public sealed class DailyRewardResult
    {
        public int coins;
        public int streak;
        public string nextClaimAt;
        public WalletSnapshot wallet;
    }

    /// <summary>POST /api/v1/economy/shop/boosters — quantity 1..20.</summary>
    public sealed class PurchaseBoosterRequest
    {
        public string boosterType;
        public int quantity;
    }

    /// <summary>Response of both shop endpoints.</summary>
    public sealed class PurchaseResult
    {
        public string item;
        public int quantity;
        public long coinsSpent;
        public WalletSnapshot wallet;
    }
}
