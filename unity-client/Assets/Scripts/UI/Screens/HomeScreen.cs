using System.Collections;
using System.Text;
using BlastScale.Client.Audio;
using BlastScale.Client.Core;
using BlastScale.Client.Net;
using BlastScale.Client.Net.Dto;
using BlastScale.Client.UI.Fx;
using BlastScale.Client.UI.Gfx;
using UnityEngine;
using UnityEngine.UI;

namespace BlastScale.Client.UI.Screens
{
    /// <summary>
    /// Hub screen: level badge, coin/star/life counters with a regeneration countdown, the big
    /// "Play" button, cards for the daily reward, shop, leaderboard and events, and the music /
    /// sound toggles. Data is re-fetched every time the screen appears because the server is
    /// the source of truth.
    /// </summary>
    public sealed class HomeScreen : UiScreen
    {
        private const float RegenRefreshCooldown = 3f;

        private RectTransform _levelPill;
        private Text _levelLabel;
        private RectTransform _coinsPill;
        private Text _coinsLabel;
        private Text _starsLabel;
        private Text _livesLabel;
        private Text _nextLifeLabel;
        private Text _experimentsLabel;
        private Text _levelPreviewLabel;
        private Button _playButton;
        private RectTransform _dailyCard;
        private Text _dailyTitle;
        private Text _dailySubtitle;
        private Image _dailyGlow;
        private Button _musicButton;
        private Button _sfxButton;
        private Image _musicSlash;
        private Image _sfxSlash;

        private DailyRewardStatus _daily;
        private bool _refreshingProfile;
        private float _lastRegenRefresh = -100f;
        private long _shownCoins = -1;

        protected override void Build(RectTransform root)
        {
            RectTransform column = CreateContentColumn(root, 16f, 36, 20, 28);

            BuildTopRow(column);
            _nextLifeLabel = UiFactory.CreateLabel(column, "", UiTheme.TinySize, UiTheme.Muted, TextAnchor.MiddleRight, UiFont.Body, 30f);

            UiFactory.CreateSpacer(column, 0.6f);
            Text title = UiFactory.CreateTitle(column, "BlastScale", UiTheme.TitleSize, UiTheme.Text);
            UiFactory.AddOutline(title, 3f, new Color(0.2f, 0.05f, 0.35f, 0.6f));
            UiFactory.CreateLabel(column, "Tap. Blast. Score.", UiTheme.BodySize - 2, UiTheme.TextSoft, TextAnchor.MiddleCenter, UiFont.BodyBold);
            if (App.Flow.IsOffline)
            {
                RectTransform badgeRow = UiFactory.CreateRow(column, "OfflineRow", 52f, 0f);
                UiFactory.CreateSpacer(badgeRow);
                UiFactory.CreatePill(badgeRow, "OfflineBadge", null, Color.white, "OFFLINE DEMO", out _, 52f,
                    UiTheme.WithAlpha(UiTheme.Amber, 0.85f), UiTheme.TinySize, 0f, UiFont.BodyBold);
                UiFactory.CreateSpacer(badgeRow);
            }
            UiFactory.CreateSpacer(column, 0.6f);

            _levelPreviewLabel = UiFactory.CreateLabel(column, "", UiTheme.SmallSize, UiTheme.Muted, TextAnchor.MiddleCenter, UiFont.Body, 40f);
            _playButton = UiFactory.CreateButton(column, "Play", OnPlay, ButtonStyle.Primary, UiTheme.HeadingSize, 156f, -1f, IconFactory.Play());
            UiFactory.CreateGap(column, 4f);

            RectTransform row1 = UiFactory.CreateRow(column, "Row1", 210f, 16f);
            _dailyCard = CreateFeatureCard(row1, "Daily", IconFactory.Gift(), "Daily Reward", "Loading...", OnDailyReward, out _dailyTitle, out _dailySubtitle);
            _dailyGlow = UiFactory.CreateGlow(_dailyCard, UiTheme.WithAlpha(UiTheme.Gold, 0f), 26f);
            CreateFeatureCard(row1, "Shop", IconFactory.Bag(), "Shop", "Boosters & lives", () => App.Screens.Show(new ShopScreen()), out _, out _);
            RectTransform row2 = UiFactory.CreateRow(column, "Row2", 210f, 16f);
            CreateFeatureCard(row2, "Leaderboard", IconFactory.Trophy(), "Leaderboard", "Weekly top 50", () => App.Screens.Show(new LeaderboardScreen()), out _, out _);
            CreateFeatureCard(row2, "Events", IconFactory.Rocket(), "Events", "Live events", () => App.Screens.Show(new EventsScreen()), out _, out _);

            UiFactory.CreateSpacer(column, 0.4f);
            BuildBottomRow(column);
            _experimentsLabel = UiFactory.CreateLabel(column, "", UiTheme.TinySize, UiTheme.Muted, TextAnchor.MiddleCenter, UiFont.Body);
        }

        private void BuildTopRow(RectTransform column)
        {
            RectTransform row = UiFactory.CreateRow(column, "TopRow", 84f, 12f, TextAnchor.MiddleLeft);
            _levelPill = UiFactory.CreatePill(row, "LevelPill", null, Color.white, "Level 1", out _levelLabel, 84f,
                UiTheme.WithAlpha(UiTheme.Violet, 0.6f), UiTheme.BodySize + 2);
            UiFactory.CreateSpacer(row);
            _coinsPill = UiFactory.CreatePill(row, "CoinsPill", IconFactory.Coin(), Color.white, "-", out _coinsLabel, 84f, null, UiTheme.BodySize, 170f);
            UiFactory.CreatePill(row, "StarsPill", IconFactory.Star(), UiTheme.Gold, "-", out _starsLabel, 84f, null, UiTheme.BodySize, 130f);
            UiFactory.CreatePill(row, "LivesPill", IconFactory.Heart(), Color.white, "-", out _livesLabel, 84f, null, UiTheme.BodySize, 150f);
        }

        private void BuildBottomRow(RectTransform column)
        {
            RectTransform row = UiFactory.CreateRow(column, "BottomRow", UiTheme.IconButtonSize - 12f, 12f, TextAnchor.MiddleLeft);
            AudioManager audio = App.Audio;
            _musicButton = UiFactory.CreateIconButton(row, "Music", IconFactory.Note(), OnToggleMusic, ButtonStyle.Ghost, UiTheme.IconButtonSize - 12f);
            _musicSlash = UiFactory.CreateImage(_musicButton.transform, "Slash", IconFactory.Slash(), UiTheme.Danger);
            UiFactory.Center(_musicSlash.rectTransform, 60f, 60f);
            _sfxButton = UiFactory.CreateIconButton(row, "Sound", IconFactory.SoundIcon(), OnToggleSfx, ButtonStyle.Ghost, UiTheme.IconButtonSize - 12f);
            _sfxSlash = UiFactory.CreateImage(_sfxButton.transform, "Slash", IconFactory.Slash(), UiTheme.Danger);
            UiFactory.Center(_sfxSlash.rectTransform, 60f, 60f);
            RefreshAudioButtons(audio);

            RectTransform texts = UiFactory.CreateRect(row, "Texts");
            UiFactory.AddVerticalLayout(texts, 0f, 0, TextAnchor.MiddleLeft);
            UiFactory.SetLayout(texts.gameObject, flexibleWidth: 1f);
            string who = App.State.Username ?? "?";
            UiFactory.CreateLabel(texts, who, UiTheme.SmallSize, UiTheme.TextSoft, TextAnchor.MiddleLeft, UiFont.BodyBold);
            string where = App.Flow.IsOffline ? "Progress is stored on this device" : ClientConfig.BaseUrl;
            Text whereLabel = UiFactory.CreateLabel(texts, where, UiTheme.TinySize, UiTheme.Muted, TextAnchor.MiddleLeft, UiFont.Body);
            whereLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            UiFactory.CreateButton(row, "Logout", () => App.Flow.Logout(), ButtonStyle.Ghost, UiTheme.SmallSize, UiTheme.IconButtonSize - 12f, 190f);
        }

        /// <summary>A tappable card with an icon, a title and a subtitle (the four hub doors).</summary>
        private RectTransform CreateFeatureCard(Transform parent, string name, Sprite icon, string title, string subtitle,
            UnityEngine.Events.UnityAction onClick, out Text titleLabel, out Text subtitleLabel)
        {
            RectTransform card = UiFactory.CreateCard(parent, "Card " + name, UiTheme.CardRadius, 20, 6, TextAnchor.MiddleCenter);
            UiFactory.SetLayout(card.gameObject, flexibleWidth: 1f);
            UiFactory.CreateIcon(card, icon, Color.white, 84f);
            titleLabel = UiFactory.CreateLabel(card, title, UiTheme.BodySize, UiTheme.Text, TextAnchor.MiddleCenter, UiFont.BodyBold);
            subtitleLabel = UiFactory.CreateLabel(card, subtitle, UiTheme.TinySize, UiTheme.Muted, TextAnchor.MiddleCenter, UiFont.Body);
            var group = card.gameObject.AddComponent<CanvasGroup>();
            var button = card.gameObject.AddComponent<Button>();
            Image body = card.Find("Body").GetComponent<Image>();
            button.targetGraphic = body;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(onClick);
            var juice = card.gameObject.AddComponent<ButtonJuice>();
            juice.Bind(button, group, body);
            return card;
        }

        protected override void OnShown()
        {
            RefreshAll(false);
            Tween.Pulse(_playButton.transform, 0.035f, 1.4f);
            Tween.Pulse(_levelPill, 0.04f, 1.8f);
            Run(LoadProfileAndPreview());
            Run(LoadConfigIfMissing());
            Run(LoadDailyStatus());
            Run(TickLives());
        }

        protected override void OnDismissed()
        {
            Tween.Kill(_playButton.transform);
            Tween.Kill(_levelPill);
        }

        // ------------------------------------------------------------------ loading

        private IEnumerator LoadProfileAndPreview()
        {
            _refreshingProfile = true;
            yield return App.Flow.RefreshProfile();
            _refreshingProfile = false;
            if (!IsAlive) yield break;
            RefreshAll(true);

            var level = new ApiResult<LevelDefinition>();
            yield return App.Api.GetJson(ApiRoutes.Level(App.State.CurrentLevel), level);
            if (!IsAlive) yield break;
            if (level.Ok && level.Value != null)
            {
                LevelDefinition d = level.Value;
                _levelPreviewLabel.text = d.rows + "x" + d.cols + " board  ·  " + d.colorCount + " colours  ·  " + d.moveLimit +
                                          " moves  ·  target " + TimeFormat.Number(d.targetScore);
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
                RefreshDailyCard();
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
                    RefreshAll(true);
                }
                yield return new WaitForSecondsRealtime(1f);
            }
        }

        // ------------------------------------------------------------------ rendering

        private void RefreshAll(bool animate)
        {
            GameState state = App.State;
            _levelLabel.text = "Level " + state.CurrentLevel;
            long coins = state.Wallet != null ? state.Wallet.coins : 0;
            if (_shownCoins >= 0 && animate && coins != _shownCoins)
            {
                AnimateCoins(_shownCoins, coins);
            }
            else
            {
                _coinsLabel.text = state.Wallet != null ? TimeFormat.Number(coins) : "-";
            }
            _shownCoins = coins;
            _starsLabel.text = state.Wallet != null ? state.Wallet.stars.ToString() : "-";
            UiFactory.SetButtonLabel(_playButton, "Play Level " + state.CurrentLevel);
            RefreshLives();
            RefreshExperiments();
            RefreshDailyCard();
        }

        private void AnimateCoins(long from, long to)
        {
            Tween.Float(from, to, 0.6f, v => _coinsLabel.text = TimeFormat.Number((long)v), Ease.OutCubic, 0f, null, _coinsLabel);
            Tween.Punch(_coinsPill, 0.15f, 0.4f);
        }

        private void RefreshLives()
        {
            GameState state = App.State;
            if (state.Wallet == null)
            {
                _livesLabel.text = "-";
                _nextLifeLabel.text = "";
                return;
            }
            _livesLabel.text = state.Wallet.lives + "/" + state.Wallet.maxLives;
            _nextLifeLabel.text = state.Wallet.lives >= state.Wallet.maxLives
                ? "Lives are full"
                : "Next life in " + TimeFormat.Countdown(state.NextLifeInSecondsNow);
            UiFactory.SetButtonEnabled(_playButton, state.Wallet.lives > 0);
        }

        private void RefreshExperiments()
        {
            ClientConfigResponse config = App.State.Config;
            if (config == null)
            {
                _experimentsLabel.text = "";
                return;
            }
            var sb = new StringBuilder();
            if (config.experiments != null && config.experiments.Count > 0)
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
                if (sb.Length > 0) sb.Append("  ·  ");
                sb.Append("reward x").Append(multiplier.ToString("0.##"));
            }
            _experimentsLabel.text = sb.ToString();
        }

        private void RefreshDailyCard()
        {
            if (_daily == null)
            {
                _dailyTitle.text = "Daily Reward";
                _dailySubtitle.text = "Checking...";
                _dailyGlow.color = UiTheme.WithAlpha(UiTheme.Gold, 0f);
                return;
            }
            if (_daily.available)
            {
                _dailyTitle.text = "Daily Reward";
                _dailySubtitle.text = "Claim +" + _daily.nextRewardCoins + " coins";
                _dailySubtitle.color = UiTheme.Gold;
                Tween.Kill(_dailyGlow);
                _dailyGlow.color = UiTheme.WithAlpha(UiTheme.Gold, 0.55f);
                Tween.Run(0.9f, t => _dailyGlow.color = UiTheme.WithAlpha(UiTheme.Gold, Mathf.Lerp(0.35f, 0.8f, t)), Ease.InOutSine, 0f, null, _dailyGlow, -1, true);
            }
            else
            {
                long seconds = App.State.SecondsUntil(_daily.nextClaimAt);
                _dailyTitle.text = "Streak " + _daily.currentStreak;
                _dailySubtitle.text = "Next in " + TimeFormat.Duration(seconds);
                _dailySubtitle.color = UiTheme.Muted;
                Tween.Kill(_dailyGlow);
                _dailyGlow.color = UiTheme.WithAlpha(UiTheme.Gold, 0f);
            }
        }

        private void RefreshAudioButtons(AudioManager audio)
        {
            bool music = audio == null || audio.MusicEnabled;
            bool sfx = audio == null || audio.SfxEnabled;
            _musicSlash.gameObject.SetActive(!music);
            _sfxSlash.gameObject.SetActive(!sfx);
        }

        // ------------------------------------------------------------------ actions

        private void OnPlay()
        {
            Run(App.Flow.StartLevel(App.State.CurrentLevel));
        }

        private void OnToggleMusic()
        {
            AudioManager audio = App.Audio;
            if (audio == null) return;
            audio.MusicEnabled = !audio.MusicEnabled;
            RefreshAudioButtons(audio);
        }

        private void OnToggleSfx()
        {
            AudioManager audio = App.Audio;
            if (audio == null) return;
            audio.SfxEnabled = !audio.SfxEnabled;
            RefreshAudioButtons(audio);
            AudioManager.Play(Sfx.UiClick);
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
                AudioManager.Play(Sfx.CoinBurst);
                if (App.Fx != null)
                {
                    App.Fx.Burst(_dailyCard.position, UiTheme.Gold, 16, 900f, 26f, 0.7f);
                    App.Fx.Sparkle(_dailyCard.position, UiTheme.Gold, 12, 80f);
                    App.Fx.FlyCoins(_dailyCard.position, _coinsPill.position, 6, () =>
                    {
                        if (IsAlive) Tween.Punch(_coinsPill, 0.14f, 0.3f);
                    });
                }
                App.Toast.Show("+" + result.Value.coins + " coins! Streak: " + result.Value.streak + " day(s)");
                _daily = new DailyRewardStatus
                {
                    available = false,
                    currentStreak = result.Value.streak,
                    nextClaimAt = result.Value.nextClaimAt
                };
                RefreshAll(true);
            }
            else
            {
                if (result.Error.Code == "DAILY_REWARD_ALREADY_CLAIMED" && _daily != null)
                {
                    _daily.available = false;
                    _daily.nextClaimAt = result.Error.DetailString("nextClaimAt", _daily.nextClaimAt);
                    RefreshDailyCard();
                }
                App.Flow.ShowError(result.Error);
            }
        }
    }
}
