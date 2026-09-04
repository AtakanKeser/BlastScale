using System;
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using BlastScale.Client.Net.Dto;
using BlastScale.Engine;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BlastScale.Client.Net.Offline
{
    /// <summary>
    /// A stand-in for the backend that answers every route of <see cref="ApiRoutes"/> locally:
    /// levels come from <see cref="OfflineLevelGenerator"/>, completions are validated by replaying
    /// the moves through the shared <see cref="BoardEngine"/> (exactly what the server does), and
    /// coins, lives, boosters and best scores live in <see cref="OfflineSave"/>. It exists so the
    /// game can be shown and tested without a server ("Offline demo" on the login screen); the
    /// wire contract (DTOs, error codes, idempotency replay) mirrors the real API so screens do
    /// not know the difference.
    /// </summary>
    public sealed class OfflineApiClient : IApiClient
    {
        public const string DemoUsername = "Demo player";
        public const long DemoPlayerId = 1;

        private const int LevelCompleteBaseCoins = 50;
        private const int CoinsPerStar = 25;
        private const int FirstClearBonusCoins = 50;
        private const int DailyRewardCoins = 100;
        private const int DailyRewardStreakBonus = 25;
        private const int MaxStreakBonusDays = 7;
        private const int LifeRefillPrice = 150;
        private const double DoubleRewardMultiplier = 2.0;

        private static readonly string[] BotNames =
        {
            "Nova", "Pixel", "Comet", "Juniper", "Marble", "Ziggy", "Echo", "Saffron", "Bolt"
        };

        /// <summary>One started level: what the server would keep in its session table.</summary>
        private sealed class Session
        {
            public string Id;
            public int Level;
            public int Seed;
            public BoardConfigDto Board;
            public bool Closed;
            public LevelCompleteResponse Result;
        }

        private readonly Dictionary<string, Session> _sessions = new Dictionary<string, Session>();
        private readonly Dictionary<string, object> _idempotent = new Dictionary<string, object>();
        private OfflineSave _save;
        private string _username = DemoUsername;
        private int _activeRequests;

        public event Action<bool> BusyChanged;

        /// <summary>Never raised: the demo token cannot expire.</summary>
        public event Action Unauthorized;

        public int ActiveRequests => _activeRequests;

        /// <summary>Forgets the demo progress (tests start every run from level 1 with full lives).</summary>
        public static void ResetSave()
        {
            OfflineSave.Reset();
        }

        // ------------------------------------------------------------------ IApiClient

        public IEnumerator GetJson<T>(string path, ApiResult<T> result)
        {
            return Execute("GET", path, null, null, result);
        }

        public IEnumerator PostJson<TReq, TRes>(string path, TReq body, ApiResult<TRes> result, string idempotencyKey = null)
        {
            string json = body == null ? null : JsonConvert.SerializeObject(body, ApiClient.JsonSettings);
            return Execute("POST", path, json, idempotencyKey, result);
        }

        public IEnumerator PostEmpty<TRes>(string path, ApiResult<TRes> result, string idempotencyKey = null)
        {
            return Execute("POST", path, null, idempotencyKey, result);
        }

        /// <summary>Runs the request after one frame of pretend latency so callers behave exactly like online.</summary>
        private IEnumerator Execute<TRes>(string method, string path, string json, string idempotencyKey, ApiResult<TRes> result)
        {
            result.Error = null;
            result.Value = default;
            result.Replayed = false;
            SetBusy(true);
            yield return null;
            try
            {
                if (_save == null)
                {
                    _save = OfflineSave.Load();
                }
                object response = Route(method, path, json, idempotencyKey, out bool replayed);
                result.Replayed = replayed;
                result.Value = Coerce<TRes>(response);
            }
            catch (ApiException e)
            {
                result.Error = e;
            }
            catch (Exception e)
            {
                Debug.LogException(e);
                result.Error = new ApiException("INTERNAL_ERROR", "Offline demo error: " + e.Message, 500, path, null);
            }
            finally
            {
                SetBusy(false);
            }
            _ = Unauthorized; // the event is part of the contract but never fires offline
        }

        /// <summary>Goes through JSON so the handler can return whatever DTO shape the caller expects.</summary>
        private static TRes Coerce<TRes>(object response)
        {
            if (response == null) return default;
            if (response is TRes typed) return typed;
            string json = JsonConvert.SerializeObject(response, ApiClient.JsonSettings);
            return JsonConvert.DeserializeObject<TRes>(json, ApiClient.JsonSettings);
        }

        private void SetBusy(bool starting)
        {
            int before = _activeRequests;
            _activeRequests = Math.Max(0, _activeRequests + (starting ? 1 : -1));
            if ((before == 0) != (_activeRequests == 0))
            {
                BusyChanged?.Invoke(_activeRequests > 0);
            }
        }

        // ------------------------------------------------------------------ routing

        private object Route(string method, string fullPath, string json, string idempotencyKey, out bool replayed)
        {
            replayed = false;
            if (!string.IsNullOrEmpty(idempotencyKey) && _idempotent.TryGetValue(idempotencyKey, out object stored))
            {
                replayed = true;
                return stored;
            }

            string path = fullPath;
            int query = path.IndexOf('?');
            if (query >= 0) path = path.Substring(0, query);
            if (path.StartsWith(ClientConfig.ApiPrefix)) path = path.Substring(ClientConfig.ApiPrefix.Length);

            object response = Handle(method, path, json);
            if (!string.IsNullOrEmpty(idempotencyKey))
            {
                _idempotent[idempotencyKey] = response;
            }
            _save.Save();
            return response;
        }

        private object Handle(string method, string path, string json)
        {
            long now = NowUnix();
            _save.RegenerateLives(now);
            switch (path)
            {
                case "/auth/guest":
                case "/auth/login":
                case "/auth/register":
                    return Authenticate(path, json);
                case "/config":
                    return Config();
                case "/players/me":
                    return Profile(now);
                case "/economy/wallet":
                    return _save.ToWallet(now);
                case "/economy/daily-reward":
                    return method == "GET" ? (object)DailyStatus() : ClaimDaily(now, path);
                case "/economy/shop/boosters":
                    return BuyBoosters(json, now, path);
                case "/economy/shop/lives":
                    return BuyLives(now, path);
                case "/progress":
                    return Progress();
                case "/leaderboards/weekly":
                    return Leaderboard();
                case "/events":
                    return Events();
            }
            if (path.StartsWith("/levels/"))
            {
                string[] parts = path.Split(new[] { '/' }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length >= 2 && int.TryParse(parts[1], out int level))
                {
                    string action = parts.Length >= 3 ? parts[2] : null;
                    if (action == null) return OfflineLevelGenerator.Definition(level, DateTime.UtcNow);
                    if (action == "start") return StartLevel(level, now, path);
                    if (action == "complete") return CompleteLevel(level, json, now, path);
                    if (action == "fail") return FailLevel(level, json, now, path);
                }
            }
            throw new ApiException("NOT_FOUND", "No offline handler for " + method + " " + path, 404, path, null);
        }

        // ------------------------------------------------------------------ auth / profile / config

        private object Authenticate(string path, string json)
        {
            if (path != "/auth/guest" && !string.IsNullOrEmpty(json))
            {
                JObject body = JObject.Parse(json);
                string username = body.Value<string>("username");
                if (!string.IsNullOrEmpty(username)) _username = username;
            }
            return new AuthResponse
            {
                token = "offline-demo-token",
                expiresAt = Iso(DateTime.UtcNow.AddDays(1)),
                playerId = DemoPlayerId,
                username = _username,
                role = "PLAYER"
            };
        }

        private static ClientConfigResponse Config()
        {
            var config = new Dictionary<string, JToken>
            {
                { ConfigKeys.DailyRewardCoins, DailyRewardCoins },
                { ConfigKeys.DailyRewardStreakBonus, DailyRewardStreakBonus },
                { ConfigKeys.MaxLives, 5 },
                { ConfigKeys.LifeRegenerationMinutes, OfflineSave.LifeRegenerationMinutes },
                { ConfigKeys.LifeRefillPrice, LifeRefillPrice },
                { ConfigKeys.BoosterPrices, JToken.FromObject(ConfigKeys.DefaultBoosterPrices) },
                { "levelCompleteBaseCoins", LevelCompleteBaseCoins },
                { "coinsPerStar", CoinsPerStar },
                { "firstClearBonusCoins", FirstClearBonusCoins },
                { ConfigKeys.RewardMultiplier, 1.0 },
                { ConfigKeys.RocketRaceEnabled, true },
                { ConfigKeys.LeaderboardEnabled, true },
                { "offlineDemo", true }
            };
            return new ClientConfigResponse
            {
                config = config,
                experiments = new List<ExperimentAssignmentView>
                {
                    new ExperimentAssignmentView { id = 1, key = "life_timer_v2", variant = "B" }
                },
                serverTime = Iso(DateTime.UtcNow)
            };
        }

        private PlayerProfile Profile(long now)
        {
            return new PlayerProfile
            {
                id = DemoPlayerId,
                username = _username,
                role = "PLAYER",
                currentLevel = _save.currentLevel,
                createdAt = Iso(DateTime.UtcNow.AddDays(-3)),
                wallet = _save.ToWallet(now)
            };
        }

        private ProgressView Progress()
        {
            var levels = new List<ProgressView.LevelEntry>();
            foreach (KeyValuePair<string, int> entry in _save.bestStars)
            {
                int level = int.Parse(entry.Key);
                levels.Add(new ProgressView.LevelEntry
                {
                    level = level,
                    stars = entry.Value,
                    bestScore = _save.bestScores.TryGetValue(entry.Key, out int best) ? best : 0,
                    attempts = 1,
                    cleared = true
                });
            }
            return new ProgressView { currentLevel = _save.currentLevel, totalStars = _save.stars, levels = levels };
        }

        // ------------------------------------------------------------------ economy

        private DailyRewardStatus DailyStatus()
        {
            string today = Today();
            bool claimedToday = _save.lastDailyClaimDay == today;
            int nextStreak = NextStreak(today);
            return new DailyRewardStatus
            {
                available = !claimedToday,
                currentStreak = _save.dailyStreak,
                nextRewardCoins = CoinsForStreak(claimedToday ? nextStreak + 1 : nextStreak),
                nextClaimAt = claimedToday ? Iso(DateTime.UtcNow.Date.AddDays(1)) : Iso(DateTime.UtcNow)
            };
        }

        private DailyRewardResult ClaimDaily(long now, string path)
        {
            string today = Today();
            if (_save.lastDailyClaimDay == today)
            {
                throw new ApiException("DAILY_REWARD_ALREADY_CLAIMED", "You already claimed today's reward", 409, path,
                    new Dictionary<string, JToken> { { "nextClaimAt", Iso(DateTime.UtcNow.Date.AddDays(1)) } });
            }
            int streak = NextStreak(today);
            int coins = CoinsForStreak(streak);
            _save.dailyStreak = streak;
            _save.lastDailyClaimDay = today;
            _save.coins += coins;
            return new DailyRewardResult
            {
                coins = coins,
                streak = streak,
                nextClaimAt = Iso(DateTime.UtcNow.Date.AddDays(1)),
                wallet = _save.ToWallet(now)
            };
        }

        private int NextStreak(string today)
        {
            string yesterday = DateTime.UtcNow.Date.AddDays(-1).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            return _save.lastDailyClaimDay == yesterday ? _save.dailyStreak + 1 : 1;
        }

        private static int CoinsForStreak(int streak)
        {
            return DailyRewardCoins + DailyRewardStreakBonus * Math.Min(Math.Max(0, streak - 1), MaxStreakBonusDays);
        }

        private PurchaseResult BuyBoosters(string json, long now, string path)
        {
            var request = JsonConvert.DeserializeObject<PurchaseBoosterRequest>(json ?? "{}", ApiClient.JsonSettings);
            if (request == null || !ConfigKeys.DefaultBoosterPrices.TryGetValue(request.boosterType ?? "", out int price))
            {
                throw new ApiException("VALIDATION_ERROR", "Unknown booster type", 400, path, null);
            }
            int quantity = Math.Max(1, Math.Min(20, request.quantity));
            long cost = (long)price * quantity;
            if (_save.coins < cost)
            {
                throw new ApiException("INSUFFICIENT_COINS", "Not enough coins (" + cost + " needed)", 409, path, null);
            }
            _save.coins -= cost;
            _save.AddBooster(request.boosterType, quantity);
            return new PurchaseResult { item = request.boosterType, quantity = quantity, coinsSpent = cost, wallet = _save.ToWallet(now) };
        }

        private PurchaseResult BuyLives(long now, string path)
        {
            if (_save.lives >= _save.maxLives)
            {
                throw new ApiException("LIVES_ALREADY_FULL", "Your lives are already full", 409, path, null);
            }
            if (_save.coins < LifeRefillPrice)
            {
                throw new ApiException("INSUFFICIENT_COINS", "Not enough coins (" + LifeRefillPrice + " needed)", 409, path, null);
            }
            int gained = _save.maxLives - _save.lives;
            _save.coins -= LifeRefillPrice;
            _save.lives = _save.maxLives;
            _save.nextLifeAt = 0;
            return new PurchaseResult { item = "LIVES", quantity = gained, coinsSpent = LifeRefillPrice, wallet = _save.ToWallet(now) };
        }

        // ------------------------------------------------------------------ gameplay

        private LevelStartResponse StartLevel(int level, long now, string path)
        {
            if (level < 1 || level > _save.currentLevel)
            {
                throw new ApiException("LEVEL_LOCKED", "Level " + level + " is locked", 403, path, null);
            }
            if (_save.lives <= 0)
            {
                throw new ApiException("NO_LIVES_LEFT", "No lives left", 409, path,
                    new Dictionary<string, JToken> { { "nextLifeInSeconds", _save.NextLifeInSeconds(now) } });
            }
            _save.SpendLife(now);
            var session = new Session
            {
                Id = Guid.NewGuid().ToString(),
                Level = level,
                Seed = UnityEngine.Random.Range(int.MinValue, int.MaxValue),
                Board = OfflineLevelGenerator.Board(level)
            };
            _sessions[session.Id] = session;
            return new LevelStartResponse
            {
                sessionId = session.Id,
                level = level,
                seed = session.Seed,
                board = session.Board,
                configurationVersion = 1,
                livesRemaining = _save.lives,
                startedAt = Iso(DateTime.UtcNow),
                expiresAt = Iso(DateTime.UtcNow.AddMinutes(30))
            };
        }

        private LevelCompleteResponse CompleteLevel(int level, string json, long now, string path)
        {
            var request = JsonConvert.DeserializeObject<LevelCompleteRequest>(json ?? "{}", ApiClient.JsonSettings);
            Session session = FindSession(request?.sessionId, level, path);
            if (session.Closed)
            {
                LevelCompleteResponse replay = Coerce<LevelCompleteResponse>(session.Result);
                replay.status = LevelCompleteResponse.AlreadyProcessed;
                replay.wallet = _save.ToWallet(now);
                return replay;
            }
            List<Move> moves = ToMoves(request.moves);
            BoardConfig config = session.Board.ToEngineConfig();
            SimulationResult sim = BoardEngine.Simulate(config, session.Seed, moves, request.extraMovesUsed);
            if (!sim.Valid)
            {
                throw new ApiException("INVALID_MOVE_SEQUENCE", "Move sequence rejected: " + sim.RejectionReason, 422, path, null);
            }
            if (!sim.ObjectiveReached)
            {
                throw new ApiException("OBJECTIVE_NOT_REACHED", "The target score was not reached", 422, path, null);
            }
            if (sim.Score != request.score)
            {
                throw new ApiException("SCORE_MISMATCH", "Client score does not match the replay", 422, path, null);
            }

            string key = level.ToString(CultureInfo.InvariantCulture);
            bool firstClear = !_save.bestStars.ContainsKey(key);
            int previousStars = firstClear ? 0 : _save.bestStars[key];
            int previousBest = _save.bestScores.TryGetValue(key, out int best) ? best : 0;
            bool doubleReward = DoubleRewardActive();
            long baseCoins = LevelCompleteBaseCoins + (long)CoinsPerStar * sim.Stars + (firstClear ? FirstClearBonusCoins : 0);
            double multiplier = doubleReward ? DoubleRewardMultiplier : 1.0;
            long coins = (long)Math.Round(baseCoins * multiplier, MidpointRounding.AwayFromZero);

            // Boosters are charged at the end exactly like the server (from the replayed moves).
            _save.AddBooster(BoosterTypes.Hammer, -sim.HammersUsed);
            _save.AddBooster(BoosterTypes.Shuffle, -sim.ShufflesUsed);
            if (request.extraMovesUsed) _save.AddBooster(BoosterTypes.ExtraMoves, -1);

            _save.coins += coins;
            _save.stars += Math.Max(0, sim.Stars - previousStars);
            _save.bestStars[key] = Math.Max(previousStars, sim.Stars);
            _save.bestScores[key] = Math.Max(previousBest, sim.Score);
            _save.currentLevel = Math.Max(_save.currentLevel, level + 1);
            EnsureSeason();
            _save.weeklyScore += sim.Score;
            _save.rocketPoints += 1;
            session.Closed = true;

            var response = new LevelCompleteResponse
            {
                status = LevelCompleteResponse.Completed,
                sessionId = session.Id,
                level = level,
                score = sim.Score,
                stars = sim.Stars,
                firstClear = firstClear,
                newBestScore = sim.Score > previousBest,
                reward = new Reward
                {
                    coins = coins,
                    stars = sim.Stars,
                    multiplier = multiplier,
                    strategy = doubleReward ? "DOUBLE_REWARD_EVENT" : "STANDARD"
                },
                wallet = _save.ToWallet(now),
                nextLevel = level + 1,
                eventPoints = new List<EventPointsAwarded>
                {
                    new EventPointsAwarded { eventId = 2, name = "Rocket Race", type = "ROCKET_RACE", points = 1, totalPoints = _save.rocketPoints }
                }
            };
            session.Result = response;
            return response;
        }

        private LevelFailResponse FailLevel(int level, string json, long now, string path)
        {
            var request = JsonConvert.DeserializeObject<LevelFailRequest>(json ?? "{}", ApiClient.JsonSettings);
            Session session = FindSession(request?.sessionId, level, path);
            if (session.Closed)
            {
                return new LevelFailResponse
                {
                    status = LevelCompleteResponse.AlreadyProcessed, sessionId = session.Id, level = level, score = 0, wallet = _save.ToWallet(now)
                };
            }
            List<Move> moves = ToMoves(request.moves);
            SimulationResult sim = BoardEngine.Simulate(session.Board.ToEngineConfig(), session.Seed, moves, request.extraMovesUsed);
            _save.AddBooster(BoosterTypes.Hammer, -sim.HammersUsed);
            _save.AddBooster(BoosterTypes.Shuffle, -sim.ShufflesUsed);
            if (request.extraMovesUsed) _save.AddBooster(BoosterTypes.ExtraMoves, -1);
            session.Closed = true;
            return new LevelFailResponse
            {
                status = "FAILED", sessionId = session.Id, level = level, score = sim.Score, wallet = _save.ToWallet(now)
            };
        }

        private Session FindSession(string sessionId, int level, string path)
        {
            if (string.IsNullOrEmpty(sessionId) || !_sessions.TryGetValue(sessionId, out Session session))
            {
                throw new ApiException("SESSION_NOT_FOUND", "Unknown session", 404, path, null);
            }
            if (session.Level != level)
            {
                throw new ApiException("SESSION_LEVEL_MISMATCH", "Session belongs to another level", 422, path, null);
            }
            return session;
        }

        private static List<Move> ToMoves(List<MoveDto> dtos)
        {
            var moves = new List<Move>();
            if (dtos == null) return moves;
            foreach (MoveDto dto in dtos)
            {
                var type = (MoveType)Enum.Parse(typeof(MoveType), dto.type ?? "TAP");
                moves.Add(new Move(type, dto.row, dto.col));
            }
            return moves;
        }

        // ------------------------------------------------------------------ live ops

        /// <summary>The demo runs a permanent "Double Reward Weekend" so the result screen can show the tag.</summary>
        private static bool DoubleRewardActive()
        {
            return true;
        }

        private LeaderboardView Leaderboard()
        {
            EnsureSeason();
            var entries = new List<LeaderboardView.Entry>();
            var seasonRandom = new SeededRandom(_save.weeklySeason.GetHashCode());
            for (int i = 0; i < BotNames.Length; i++)
            {
                entries.Add(new LeaderboardView.Entry { playerId = 100 + i, name = BotNames[i], score = 900 + seasonRandom.NextInt(9000) });
            }
            if (_save.weeklyScore > 0)
            {
                entries.Add(new LeaderboardView.Entry { playerId = DemoPlayerId, name = _username, score = _save.weeklyScore });
            }
            entries.Sort((a, b) => b.score.CompareTo(a.score));
            int? myRank = null;
            for (int i = 0; i < entries.Count; i++)
            {
                entries[i].rank = i + 1;
                if (entries[i].playerId == DemoPlayerId) myRank = i + 1;
            }
            return new LeaderboardView
            {
                season = _save.weeklySeason,
                endsAt = Iso(NextMonday()),
                finalized = false,
                players = entries,
                myRank = myRank,
                myScore = _save.weeklyScore
            };
        }

        private List<PlayerEventView> Events()
        {
            long secondsToWeekend = (long)(NextMonday() - DateTime.UtcNow).TotalSeconds;
            var standings = new List<Standing>();
            var rng = new SeededRandom(42);
            for (int i = 0; i < 5; i++)
            {
                standings.Add(new Standing { playerId = 100 + i, name = BotNames[i], points = 3 + rng.NextInt(12) });
            }
            if (_save.rocketPoints > 0)
            {
                standings.Add(new Standing { playerId = DemoPlayerId, name = _username, points = _save.rocketPoints });
            }
            standings.Sort((a, b) => b.points.CompareTo(a.points));
            int? myRank = null;
            for (int i = 0; i < standings.Count; i++)
            {
                standings[i].rank = i + 1;
                standings[i].rewardCoins = i < 3 ? 300 - i * 100 : (int?)null;
                if (standings[i].playerId == DemoPlayerId) myRank = i + 1;
            }
            return new List<PlayerEventView>
            {
                new PlayerEventView
                {
                    id = 1,
                    type = "DOUBLE_REWARD",
                    name = "Double Reward Weekend",
                    startAt = Iso(DateTime.UtcNow.AddDays(-1)),
                    endAt = Iso(NextMonday()),
                    secondsRemaining = secondsToWeekend,
                    configuration = new Dictionary<string, JToken> { { "multiplier", 2 } },
                    eligible = true,
                    top = new List<Standing>()
                },
                new PlayerEventView
                {
                    id = 2,
                    type = "ROCKET_RACE",
                    name = "Rocket Race",
                    startAt = Iso(DateTime.UtcNow.AddDays(-1)),
                    endAt = Iso(NextMonday()),
                    secondsRemaining = secondsToWeekend,
                    configuration = new Dictionary<string, JToken> { { "pointsPerLevel", 1 }, { "minimumLevel", 1 } },
                    myPoints = _save.rocketPoints,
                    myRank = myRank,
                    eligible = true,
                    top = standings
                }
            };
        }

        /// <summary>Weekly scores reset when the ISO week changes, like the server's seasons.</summary>
        private void EnsureSeason()
        {
            string season = Season(DateTime.UtcNow);
            if (_save.weeklySeason != season)
            {
                _save.weeklySeason = season;
                _save.weeklyScore = 0;
            }
        }

        // ------------------------------------------------------------------ time helpers

        public static string Iso(DateTime utc)
        {
            return utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'", CultureInfo.InvariantCulture);
        }

        private static long NowUnix()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        }

        private static string Today()
        {
            return DateTime.UtcNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }

        /// <summary>ISO-8601 season id ("2026-W36"): the week containing the year's first Thursday is week 1.</summary>
        private static string Season(DateTime utc)
        {
            DateTime date = utc.Date;
            int dayOfWeek = ((int)date.DayOfWeek + 6) % 7; // Monday = 0
            DateTime thursday = date.AddDays(3 - dayOfWeek);
            int week = (thursday.DayOfYear - 1) / 7 + 1;
            return thursday.Year + "-W" + week.ToString("00", CultureInfo.InvariantCulture);
        }

        private static DateTime NextMonday()
        {
            DateTime today = DateTime.UtcNow.Date;
            int days = ((int)DayOfWeek.Monday - (int)today.DayOfWeek + 7) % 7;
            if (days == 0) days = 7;
            return today.AddDays(days);
        }
    }
}
