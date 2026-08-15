namespace BlastScale.Client.Net
{
    /// <summary>
    /// Every backend path in one place, mirroring the Spring controllers' request mappings, so a
    /// renamed endpoint is a one-line change here instead of a hunt through the screens.
    /// </summary>
    public static class ApiRoutes
    {
        private const string P = ClientConfig.ApiPrefix;

        // AuthController
        public const string AuthGuest = P + "/auth/guest";
        public const string AuthRegister = P + "/auth/register";
        public const string AuthLogin = P + "/auth/login";

        // PlayerController / ConfigController
        public const string PlayerMe = P + "/players/me";
        public const string Config = P + "/config";

        // EconomyController
        public const string Wallet = P + "/economy/wallet";
        public const string DailyReward = P + "/economy/daily-reward";
        public const string ShopBoosters = P + "/economy/shop/boosters";
        public const string ShopLives = P + "/economy/shop/lives";

        // ProgressionController
        public const string Progress = P + "/progress";

        public static string LevelStart(int level)
        {
            return P + "/levels/" + level + "/start";
        }

        public static string LevelComplete(int level)
        {
            return P + "/levels/" + level + "/complete";
        }

        public static string LevelFail(int level)
        {
            return P + "/levels/" + level + "/fail";
        }

        // LevelController
        public static string Level(int level)
        {
            return P + "/levels/" + level;
        }

        // LeaderboardController / LiveEventController
        public static string WeeklyLeaderboard(int limit)
        {
            return P + "/leaderboards/weekly?limit=" + limit;
        }

        public const string Events = P + "/events";

        /// <summary>Auth endpoints are public; a 401 from them means bad credentials, not an expired token.</summary>
        public static bool IsAuthRoute(string path)
        {
            return path != null && path.StartsWith(P + "/auth/");
        }
    }
}
