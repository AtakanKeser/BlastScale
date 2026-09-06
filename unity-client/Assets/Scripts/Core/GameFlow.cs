using System;
using System.Collections;
using System.Collections.Generic;
using BlastScale.Client.Net;
using BlastScale.Client.Net.Dto;
using BlastScale.Client.UI;
using BlastScale.Client.UI.Screens;
using Newtonsoft.Json.Linq;
using UnityEngine;

namespace BlastScale.Client.Core
{
    /// <summary>
    /// Multi-step flows shared by several screens (sign in, start a level, submit a result...).
    /// Screens stay thin: they render state and call into here. Every method is a coroutine that
    /// talks to the server through <see cref="IApiClient"/> and reports problems via toast/modal.
    /// The offline demo swaps the client implementation; the flows themselves do not change.
    /// </summary>
    public sealed class GameFlow
    {
        /// <summary>
        /// The server rejects completions faster than 150 ms per TAP (SUSPICIOUS_DURATION). A human
        /// cannot be that fast, but the client measures from a later instant than the server (the
        /// start response had to travel first), so a tiny local wait guarantees we never trip it.
        /// </summary>
        public const float MinSecondsPerTap = 0.15f;

        /// <summary>PlayerPrefs key of the last username that signed in successfully (pre-filled on the login screen).</summary>
        public const string LastUsernameKey = "blastscale.lastUsername";

        private readonly AppContext _app;

        public GameFlow(AppContext app)
        {
            _app = app;
        }

        private GameState State => _app.State;
        private IApiClient Api => _app.Api;

        /// <summary>True while playing against the local stand-in instead of a server.</summary>
        public bool IsOffline => _app.IsOffline;

        // ------------------------------------------------------------------ authentication

        /// <summary>
        /// Guest sign-in with the device id. <paramref name="onFailure"/> (optional) lets the login
        /// screen react to a failure (e.g. re-check the server status); the user-facing message is
        /// shown here in every case.
        /// </summary>
        public IEnumerator LoginAsGuest(Action<ApiException> onFailure = null)
        {
            var request = new GuestLoginRequest { deviceId = DeviceIdentity.Get() };
            yield return Authenticate(ApiRoutes.AuthGuest, request, onFailure);
        }

        public IEnumerator Login(string username, string password, Action<ApiException> onFailure = null)
        {
            yield return Authenticate(ApiRoutes.AuthLogin, new LoginRequest { username = username, password = password }, onFailure, username);
        }

        public IEnumerator Register(string username, string password, Action<ApiException> onFailure = null)
        {
            yield return Authenticate(ApiRoutes.AuthRegister, new RegisterRequest { username = username, password = password }, onFailure, username);
        }

        /// <summary>
        /// After a successful sign-in the username is kept so the next launch pre-fills it. This
        /// runs inside the authentication flow (before the home screen replaces the login screen)
        /// because a screen-guarded coroutine stops as soon as its screen is dismissed.
        /// </summary>
        private static void RememberUsername(string username)
        {
            if (!string.IsNullOrEmpty(username))
            {
                PlayerPrefs.SetString(LastUsernameKey, username);
                PlayerPrefs.Save();
            }
        }

        /// <summary>
        /// Switches every call to the local offline client and signs in as the demo player. Levels
        /// are generated locally, completions are replayed by the local engine, coins and lives
        /// live in PlayerPrefs — no server is contacted.
        /// </summary>
        public IEnumerator StartOfflineDemo()
        {
            State.Logout();
            _app.UseOffline(true);
            yield return LoginAsGuest();
        }

        /// <summary>
        /// Exchanges credentials for a token, loads config + profile, then shows the home screen.
        /// <paramref name="usernameToRemember"/> is stored once the server accepted the credentials.
        /// </summary>
        private IEnumerator Authenticate<TRequest>(string path, TRequest request, Action<ApiException> onFailure, string usernameToRemember = null)
        {
            var auth = new ApiResult<AuthResponse>();
            yield return Api.PostJson(path, request, auth);
            if (!auth.Ok)
            {
                // "Retry" in the dialog repeats exactly this attempt (same route, same request).
                ShowAuthError(auth.Error, () => _app.Runner.StartCoroutine(Authenticate(path, request, onFailure, usernameToRemember)));
                onFailure?.Invoke(auth.Error);
                yield break;
            }
            State.SetAuth(auth.Value);
            RememberUsername(usernameToRemember);
            yield return LoadStartupData();
            if (!State.IsAuthenticated)
            {
                yield break; // the token was rejected while loading; the login screen is already back
            }
            _app.Screens.Show(new HomeScreen());
        }

        // ------------------------------------------------------------------ sign-in errors

        /// <summary>
        /// True for every failure that means "nothing usable answered at that URL": no connection,
        /// timeout, a 5xx, a body we could not parse, or a non-JSON answer from something that is
        /// not a BlastScale server (a synthetic HTTP_xxx code).
        /// </summary>
        public static bool IsServerUnreachable(ApiException error)
        {
            if (error == null) return false;
            return error.IsNetworkError
                   || error.HttpStatus >= 500
                   || error.Code == ApiException.ParseErrorCode
                   || error.Code.StartsWith("HTTP_", StringComparison.Ordinal);
        }

        /// <summary>
        /// Short, friendly copy for the sign-in error codes; null when the server's own message is
        /// the best thing to show.
        /// </summary>
        public static string FriendlyAuthMessage(ApiException error)
        {
            if (error == null) return null;
            switch (error.Code)
            {
                case "USERNAME_TAKEN":
                    return "That username is already taken. Pick another one, or sign in if it is yours.";
                case "INVALID_CREDENTIALS":
                    return "Wrong username or password.";
                case "VALIDATION_ERROR":
                {
                    Dictionary<string, string> fields = FieldMessages(error);
                    if (fields.Count == 0) return "Please check what you entered.";
                    var parts = new List<string>();
                    foreach (KeyValuePair<string, string> field in fields)
                    {
                        parts.Add(Capitalize(field.Key) + ": " + field.Value);
                    }
                    return string.Join("\n", parts);
                }
                case "RATE_LIMITED":
                {
                    long limit = error.DetailLong("limitPerMinute", 0);
                    return limit > 0
                        ? "Too many attempts (limit " + limit + " per minute). Wait a moment and try again."
                        : "Too many attempts. Wait a moment and try again.";
                }
                default:
                    return null;
            }
        }

        /// <summary>
        /// The per-field messages of a VALIDATION_ERROR (<c>details</c> is a flat "field -> message"
        /// map, e.g. <c>{"username": "size must be between 3 and 32"}</c>); empty for other codes.
        /// </summary>
        public static Dictionary<string, string> FieldMessages(ApiException error)
        {
            var result = new Dictionary<string, string>();
            if (error == null || error.Code != "VALIDATION_ERROR") return result;
            foreach (KeyValuePair<string, JToken> detail in error.Details)
            {
                if (detail.Value == null || detail.Value.Type == JTokenType.Null) continue;
                result[detail.Key] = detail.Value.Type == JTokenType.String ? detail.Value.Value<string>() : detail.Value.ToString();
            }
            return result;
        }

        private static string Capitalize(string text)
        {
            return string.IsNullOrEmpty(text) ? text : char.ToUpperInvariant(text[0]) + text.Substring(1);
        }

        /// <summary>
        /// Presents a failed sign-in attempt: an unreachable server becomes a dialog that offers the
        /// offline demo or a retry; the business codes become friendly toasts.
        /// </summary>
        private void ShowAuthError(ApiException error, Action retry)
        {
            if (error == null) return;
            if (IsServerUnreachable(error))
            {
                string url = ClientConfig.BaseUrl;
                string detail = error.IsNetworkError
                    ? "Nothing answered there."
                    : "The answer was not from a BlastScale server (" + error.Message + ").";
                _app.Modal.Show("Could not reach the server at " + url,
                    detail + "\n\nStart the backend with <b>docker compose up</b> in the repository and check the URL, " +
                    "or play the offline demo — it needs no server at all.",
                    ModalButton.Primary("Offline demo", () => _app.Runner.StartCoroutine(StartOfflineDemo())),
                    ModalButton.Secondary("Retry", retry));
                return;
            }
            string friendly = FriendlyAuthMessage(error);
            if (friendly != null)
            {
                _app.Toast.Show(friendly, true, error.Code == "VALIDATION_ERROR" ? 5f : 3.5f);
                return;
            }
            ShowError(error);
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

        /// <summary>Forgets the session (and leaves the offline demo) and shows the login screen.</summary>
        public void Logout()
        {
            State.Logout();
            _app.UseOffline(false);
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
                    _app.Modal.Show("Cannot reach the server", error.Message + "\n\nServer: " + ClientConfig.BaseUrl,
                        ModalButton.Primary("OK", null));
                    break;
                default:
                    if (error.HttpStatus >= 500 || error.Code == ApiException.ParseErrorCode)
                    {
                        // Server-side failures deserve a dialog the player must acknowledge;
                        // business rules ("not enough coins") are fine as a passing toast.
                        _app.Modal.Show("Something went wrong", error.Message + "\n(" + error.Code + ")", ModalButton.Primary("OK", null));
                    }
                    else
                    {
                        _app.Toast.Show(error.Message, true);
                    }
                    break;
            }
        }
    }
}
