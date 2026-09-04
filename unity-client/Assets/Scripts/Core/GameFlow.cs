using System.Collections;
using BlastScale.Client.Net;
using BlastScale.Client.Net.Dto;
using BlastScale.Client.UI;
using BlastScale.Client.UI.Screens;
using UnityEngine;

namespace BlastScale.Client.Core
{
    /// <summary>
    /// Multi-step flows shared by several screens (sign in, start a level, submit a result...).
    /// Screens stay thin: they render state and call into here. Every method is a coroutine that
    /// talks to the server through <see cref="ApiClient"/> and reports problems via toast/modal.
    /// </summary>
    public sealed class GameFlow
    {
        /// <summary>
        /// The server rejects completions faster than 150 ms per TAP (SUSPICIOUS_DURATION). A human
        /// cannot be that fast, but the client measures from a later instant than the server (the
        /// start response had to travel first), so a tiny local wait guarantees we never trip it.
        /// </summary>
        public const float MinSecondsPerTap = 0.15f;

        private readonly AppContext _app;

        public GameFlow(AppContext app)
        {
            _app = app;
        }

        private GameState State => _app.State;
        private ApiClient Api => _app.Api;

        // ------------------------------------------------------------------ authentication

        public IEnumerator LoginAsGuest()
        {
            var request = new GuestLoginRequest { deviceId = DeviceIdentity.Get() };
            yield return Authenticate(ApiRoutes.AuthGuest, request);
        }

        public IEnumerator Login(string username, string password)
        {
            yield return Authenticate(ApiRoutes.AuthLogin, new LoginRequest { username = username, password = password });
        }

        public IEnumerator Register(string username, string password)
        {
            yield return Authenticate(ApiRoutes.AuthRegister, new RegisterRequest { username = username, password = password });
        }

        /// <summary>Exchanges credentials for a token, loads config + profile, then shows the home screen.</summary>
        private IEnumerator Authenticate<TRequest>(string path, TRequest request)
        {
            var auth = new ApiResult<AuthResponse>();
            yield return Api.PostJson(path, request, auth);
            if (!auth.Ok)
            {
                ShowError(auth.Error);
                yield break;
            }
            State.SetAuth(auth.Value);
            yield return LoadStartupData();
            if (!State.IsAuthenticated)
            {
                yield break; // the token was rejected while loading; the login screen is already back
            }
            _app.Screens.Show(new HomeScreen());
        }

        /// <summary>Remote config first (prices, lives), then the profile (wallet, level).</summary>
        public IEnumerator LoadStartupData()
        {
            var config = new ApiResult<ClientConfigResponse>();
            yield return Api.GetJson(ApiRoutes.Config, config);
            if (config.Ok)
            {
                State.SetConfig(config.Value);
            }
            else
            {
                ShowError(config.Error);
            }
            yield return RefreshProfile();
        }

        /// <summary>GET /players/me — the wallet inside already includes lazily regenerated lives.</summary>
        public IEnumerator RefreshProfile()
        {
            var profile = new ApiResult<PlayerProfile>();
            yield return Api.GetJson(ApiRoutes.PlayerMe, profile);
            if (profile.Ok)
            {
                State.SetProfile(profile.Value);
            }
            else
            {
                ShowError(profile.Error);
            }
        }

        public void Logout()
        {
            State.Logout();
            _app.Screens.Show(new LoginScreen());
        }

        public void GoHome()
        {
            _app.Screens.Show(new HomeScreen());
        }

        // ------------------------------------------------------------------ gameplay

        /// <summary>POST /levels/{n}/start: consumes a life, builds the local board from the seed, opens the board.</summary>
        public IEnumerator StartLevel(int level)
        {
            var start = new ApiResult<LevelStartResponse>();
            yield return Api.PostEmpty(ApiRoutes.LevelStart(level), start);
            if (!start.Ok)
            {
                ShowError(start.Error);
                yield break;
            }
            State.Session = new LevelSession(start.Value);
            if (State.Wallet != null)
            {
                // The start response only carries the remaining lives; the countdown is refreshed
                // by the next profile fetch (home screen).
                State.Wallet.lives = start.Value.livesRemaining;
            }
            _app.Screens.Show(new GameplayScreen());
        }

        /// <summary>POST /levels/{n}/complete with the recorded moves; the server replays them and pays the reward.</summary>
        public IEnumerator SubmitCompletion(LevelSession session, ApiResult<LevelCompleteResponse> result)
        {
            float minSeconds = session.TapCount * MinSecondsPerTap;
            float elapsed = Time.realtimeSinceStartup - session.StartedAtRealtime;
            if (elapsed < minSeconds)
            {
                yield return new WaitForSeconds(minSeconds - elapsed);
            }
            yield return Api.PostJson(ApiRoutes.LevelComplete(session.Level), session.ToCompleteRequest(), result, session.CompletionKey);
            if (result.Ok && result.Value != null)
            {
                State.SetWallet(result.Value.wallet);
                State.AdvanceLevel(result.Value.nextLevel);
            }
        }

        /// <summary>POST /levels/{n}/fail — closes the session and charges the boosters that were used.</summary>
        public IEnumerator SubmitFailure(LevelSession session, ApiResult<LevelFailResponse> result)
        {
            yield return Api.PostJson(ApiRoutes.LevelFail(session.Level), session.ToFailRequest(), result);
            if (result.Ok && result.Value != null)
            {
                State.SetWallet(result.Value.wallet);
            }
        }

        // ------------------------------------------------------------------ economy

        /// <summary>POST /economy/daily-reward with a fresh Idempotency-Key per attempt.</summary>
        public IEnumerator ClaimDailyReward(ApiResult<DailyRewardResult> result)
        {
            yield return Api.PostEmpty(ApiRoutes.DailyReward, result, ApiClient.NewIdempotencyKey());
            if (result.Ok && result.Value != null)
            {
                State.SetWallet(result.Value.wallet);
            }
        }

        public IEnumerator BuyBooster(string boosterType, int quantity, ApiResult<PurchaseResult> result)
        {
            var request = new PurchaseBoosterRequest { boosterType = boosterType, quantity = quantity };
            yield return Api.PostJson(ApiRoutes.ShopBoosters, request, result, ApiClient.NewIdempotencyKey());
            if (result.Ok && result.Value != null)
            {
                State.SetWallet(result.Value.wallet);
            }
        }

        public IEnumerator BuyLives(ApiResult<PurchaseResult> result)
        {
            yield return Api.PostEmpty(ApiRoutes.ShopLives, result, ApiClient.NewIdempotencyKey());
            if (result.Ok && result.Value != null)
            {
                State.SetWallet(result.Value.wallet);
            }
        }

        // ------------------------------------------------------------------ errors

        /// <summary>
        /// Central error presentation: the stable code drives the behaviour, the server's message
        /// is what the player reads. Codes without special handling become an error toast.
        /// </summary>
        public void ShowError(ApiException error)
        {
            if (error == null)
            {
                return;
            }
            switch (error.Code)
            {
                case "NO_LIVES_LEFT":
                {
                    long seconds = error.DetailLong("nextLifeInSeconds", 0);
                    string text = seconds > 0
                        ? "Next life in " + TimeFormat.Countdown(seconds) + ". Refill your lives in the shop?"
                        : error.Message;
                    _app.Modal.Show("No lives left", text,
                        ModalButton.Primary("Open shop", () => _app.Screens.Show(new ShopScreen())),
                        ModalButton.Secondary("Later", null));
                    break;
                }
                case "UNAUTHORIZED":
                    // ApiClient already raised Unauthorized -> bootstrap showed the login screen.
                    _app.Toast.Show("Please sign in again", true);
                    break;
                case ApiException.NetworkErrorCode:
                    _app.Toast.Show(error.Message + "\nServer: " + ClientConfig.BaseUrl, true, 4f);
                    break;
                default:
                    _app.Toast.Show(error.Message, true);
                    break;
            }
        }
    }
}
