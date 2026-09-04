using System.Collections;
using System.Text;
using BlastScale.Client.Core;
using BlastScale.Client.Net;
using BlastScale.Client.Net.Dto;
using UnityEngine;
using UnityEngine.UI;

namespace BlastScale.Client.UI.Screens
{
    /// <summary>
    /// Hub screen: level, coins, stars, lives with a regeneration countdown, the experiments the
    /// player is bucketed into, and the doors to gameplay, daily reward, shop, leaderboard and events.
    /// Data is re-fetched every time the screen appears because the server is the source of truth.
    /// </summary>
    public sealed class HomeScreen : UiScreen
    {
        private const float RegenRefreshCooldown = 3f;

        private Text _usernameLabel;
        private Text _levelValue;
        private Text _coinsValue;
        private Text _starsValue;
        private Text _livesValue;
        private Text _nextLifeValue;
        private Text _experimentsLabel;
        private Text _levelPreviewLabel;
        private Button _playButton;
        private Button _dailyButton;

        private DailyRewardStatus _daily;
        private bool _refreshingProfile;
        private float _lastRegenRefresh = -100f;

        protected override void Build(RectTransform root)
        {
            RectTransform column = CreateContentColumn(root, 18f, 40);

            RectTransform title = UiFactory.CreateRow(column, "Title", 100f, 16f, TextAnchor.MiddleLeft);
            Text heading = UiFactory.CreateLabel(title, "BlastScale", UiTheme.TitleSize - 16, UiTheme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.SetLayout(heading.gameObject, flexibleWidth: 1f);
            UiFactory.CreateButton(title, "Logout", () => App.Flow.Logout(), UiTheme.Secondary, UiTheme.SmallSize, 80f, 200f);
            _usernameLabel = UiFactory.CreateLabel(column, "", UiTheme.SmallSize, UiTheme.Muted, TextAnchor.MiddleLeft);

            RectTransform stats = UiFactory.CreateRow(column, "Stats", 150f, 16f);
            _levelValue = UiFactory.CreateStatTile(stats, "Level", "-");
            _coinsValue = UiFactory.CreateStatTile(stats, "Coins", "-");
            _starsValue = UiFactory.CreateStatTile(stats, "Stars", "-");

            RectTransform lives = UiFactory.CreateRow(column, "Lives", 150f, 16f);
            _livesValue = UiFactory.CreateStatTile(lives, "Lives", "-");
            _nextLifeValue = UiFactory.CreateStatTile(lives, "Next life in", "-");

            _experimentsLabel = UiFactory.CreateLabel(column, "", UiTheme.SmallSize, UiTheme.Muted, TextAnchor.MiddleLeft);

            UiFactory.CreateSpacer(column, 0.5f);
            _levelPreviewLabel = UiFactory.CreateLabel(column, "", UiTheme.BodySize, UiTheme.Muted);
            _playButton = UiFactory.CreateButton(column, "Play", OnPlay, UiTheme.Accent, UiTheme.HeadingSize, 140f);
            _dailyButton = UiFactory.CreateButton(column, "Daily reward", OnDailyReward, UiTheme.Success);

            RectTransform row1 = UiFactory.CreateRow(column, "Row1", 110f);
            UiFactory.CreateButton(row1, "Shop", () => App.Screens.Show(new ShopScreen()), UiTheme.Secondary);
            UiFactory.CreateButton(row1, "Leaderboard", () => App.Screens.Show(new LeaderboardScreen()), UiTheme.Secondary);
            RectTransform row2 = UiFactory.CreateRow(column, "Row2", 110f);
            UiFactory.CreateButton(row2, "Events", () => App.Screens.Show(new EventsScreen()), UiTheme.Secondary);
            UiFactory.CreateButton(row2, "Refresh", () => Run(LoadProfileAndPreview()), UiTheme.Secondary);

            UiFactory.CreateSpacer(column);
            UiFactory.CreateLabel(column, "Server: " + ClientConfig.BaseUrl, UiTheme.SmallSize, UiTheme.Muted);
        }

        protected override void OnShown()
        {
            RefreshAll();
            Run(LoadProfileAndPreview());
            Run(LoadConfigIfMissing());
            Run(LoadDailyStatus());
            Run(TickLives());
        }

        // ------------------------------------------------------------------ loading

        private IEnumerator LoadProfileAndPreview()
        {
            _refreshingProfile = true;
            yield return App.Flow.RefreshProfile();
            _refreshingProfile = false;
            if (!IsAlive) yield break;
            RefreshAll();

            var level = new ApiResult<LevelDefinition>();
            yield return App.Api.GetJson(ApiRoutes.Level(App.State.CurrentLevel), level);
            if (!IsAlive) yield break;
            if (level.Ok && level.Value != null)
            {
                LevelDefinition d = level.Value;
                _levelPreviewLabel.text = "Level " + d.levelNumber + ": " + d.rows + "x" + d.cols + " board, " + d.colorCount +
                                          " colors, " + d.moveLimit + " moves, target " + TimeFormat.Number(d.targetScore);
            }
        }

        private IEnumerator LoadConfigIfMissing()
        {
            if (App.State.Config != null)
            {
                RefreshExperiments();
                yield break;
            }
            var config = new ApiResult<ClientConfigResponse>();
            yield return App.Api.GetJson(ApiRoutes.Config, config);
            if (!IsAlive) yield break;
            if (config.Ok)
            {
                App.State.SetConfig(config.Value);
                RefreshExperiments();
            }
            else
            {
                App.Flow.ShowError(config.Error);
            }
        }

        private IEnumerator LoadDailyStatus()
        {
            var status = new ApiResult<DailyRewardStatus>();
            yield return App.Api.GetJson(ApiRoutes.DailyReward, status);
            if (!IsAlive) yield break;
            if (status.Ok)
            {
                _daily = status.Value;
                RefreshDailyButton();
            }
        }

        /// <summary>Once a second: update the countdown and, when it hits zero, ask the server for the new life.</summary>
        private IEnumerator TickLives()
        {
            while (IsAlive)
            {
                RefreshLives();
                GameState state = App.State;
                bool regenDue = state.Wallet != null && state.Wallet.lives < state.Wallet.maxLives && state.NextLifeInSecondsNow == 0;
                if (regenDue && !_refreshingProfile && Time.realtimeSinceStartup - _lastRegenRefresh > RegenRefreshCooldown)
                {
                    _lastRegenRefresh = Time.realtimeSinceStartup;
                    _refreshingProfile = true;
                    yield return App.Flow.RefreshProfile();
                    _refreshingProfile = false;
                    if (!IsAlive) yield break;
                    RefreshAll();
                }
                yield return new WaitForSeconds(1f);
            }
        }

        // ------------------------------------------------------------------ rendering

        private void RefreshAll()
        {
            GameState state = App.State;
            _usernameLabel.text = "Signed in as " + (state.Username ?? "?") + "  (player #" + state.PlayerId + ")";
            _levelValue.text = state.CurrentLevel.ToString();
            _coinsValue.text = state.Wallet != null ? TimeFormat.Number(state.Wallet.coins) : "-";
            _starsValue.text = state.Wallet != null ? state.Wallet.stars.ToString() : "-";
            UiFactory.SetButtonLabel(_playButton, "Play level " + state.CurrentLevel);
            RefreshLives();
            RefreshExperiments();
            RefreshDailyButton();
        }

        private void RefreshLives()
        {
            GameState state = App.State;
            if (state.Wallet == null)
            {
                _livesValue.text = "-";
                _nextLifeValue.text = "-";
                return;
            }
            _livesValue.text = state.Wallet.lives + " / " + state.Wallet.maxLives;
            _nextLifeValue.text = state.Wallet.lives >= state.Wallet.maxLives ? "full" : TimeFormat.Countdown(state.NextLifeInSecondsNow);
            _playButton.interactable = state.Wallet.lives > 0;
        }

        private void RefreshExperiments()
        {
            ClientConfigResponse config = App.State.Config;
            if (config == null)
            {
                _experimentsLabel.text = "Config: not loaded";
                return;
            }
            var sb = new StringBuilder();
            if (config.experiments == null || config.experiments.Count == 0)
            {
                sb.Append("Experiments: none");
            }
            else
            {
                sb.Append("Experiments: ");
                for (int i = 0; i < config.experiments.Count; i++)
                {
                    ExperimentAssignmentView e = config.experiments[i];
                    if (i > 0) sb.Append(", ");
                    sb.Append(e.key).Append(" = ").Append(e.variant);
                }
            }
            double multiplier = config.GetDouble(ConfigKeys.RewardMultiplier, 1.0);
            if (multiplier != 1.0)
            {
                sb.Append("  ·  reward x").Append(multiplier.ToString("0.##"));
            }
            _experimentsLabel.text = sb.ToString();
        }

        private void RefreshDailyButton()
        {
            if (_daily == null)
            {
                UiFactory.SetButtonLabel(_dailyButton, "Daily reward");
                return;
            }
            if (_daily.available)
            {
                UiFactory.SetButtonLabel(_dailyButton, "Claim daily reward: +" + _daily.nextRewardCoins + " coins");
                _dailyButton.image.color = UiTheme.Success;
            }
            else
            {
                long seconds = App.State.SecondsUntil(_daily.nextClaimAt);
                UiFactory.SetButtonLabel(_dailyButton, "Daily reward claimed (streak " + _daily.currentStreak + ") · next in " + TimeFormat.Duration(seconds));
                _dailyButton.image.color = UiTheme.Secondary;
            }
        }

        // ------------------------------------------------------------------ actions

        private void OnPlay()
        {
            Run(App.Flow.StartLevel(App.State.CurrentLevel));
        }

        private void OnDailyReward()
        {
            if (_daily != null && !_daily.available)
            {
                App.Toast.Show("Already claimed today. Next reward in " + TimeFormat.Duration(App.State.SecondsUntil(_daily.nextClaimAt)));
                return;
            }
            Run(ClaimDailyReward());
        }

        private IEnumerator ClaimDailyReward()
        {
            var result = new ApiResult<DailyRewardResult>();
            yield return App.Flow.ClaimDailyReward(result);
            if (!IsAlive) yield break;
            if (result.Ok && result.Value != null)
            {
                App.Toast.Show("+" + result.Value.coins + " coins! Streak: " + result.Value.streak + " day(s)");
                _daily = new DailyRewardStatus
                {
                    available = false,
                    currentStreak = result.Value.streak,
                    nextClaimAt = result.Value.nextClaimAt
                };
                RefreshAll();
            }
            else
            {
                if (result.Error.Code == "DAILY_REWARD_ALREADY_CLAIMED" && _daily != null)
                {
                    _daily.available = false;
                    _daily.nextClaimAt = result.Error.DetailString("nextClaimAt", _daily.nextClaimAt);
                    RefreshDailyButton();
                }
                App.Flow.ShowError(result.Error);
            }
        }
    }
}
