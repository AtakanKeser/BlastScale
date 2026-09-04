using System.Collections;
using BlastScale.Client.Audio;
using BlastScale.Client.Core;
using BlastScale.Client.Net.Dto;
using BlastScale.Client.UI.Fx;
using BlastScale.Client.UI.Gfx;
using UnityEngine;
using UnityEngine.UI;

namespace BlastScale.Client.UI.Screens
{
    /// <summary>
    /// End-of-level summary. For a win it shows what the <b>server</b> decided (score, stars,
    /// reward strategy, event points) — never the client's own numbers — with confetti, stars
    /// popping in one by one, a coin count-up and coins flying into the wallet. For a loss it
    /// shows the score reached with an encouraging word and offers a retry.
    /// </summary>
    public sealed class ResultScreen : UiScreen
    {
        private static readonly string[] Encouragements =
        {
            "So close!", "Almost there!", "Nice try!", "You'll get it next time!"
        };

        private readonly LevelSession _session;
        private readonly LevelCompleteResponse _win;
        private readonly LevelFailResponse _loss;

        private RectTransform _card;
        private RectTransform _coinPill;
        private Text _coinPillLabel;
        private Text _rewardLabel;
        private readonly Image[] _stars = new Image[3];
        private long _walletCoinsShown;

        public ResultScreen(LevelSession session, LevelCompleteResponse win)
        {
            _session = session;
            _win = win;
        }

        public ResultScreen(LevelSession session, LevelFailResponse loss)
        {
            _session = session;
            _loss = loss;
        }

        public override ScreenTransition Transition => ScreenTransition.Fade;

        protected override void Build(RectTransform root)
        {
            Image scrim = UiFactory.CreatePanel(root, "Scrim", new Color(0.03f, 0.02f, 0.08f, 0.8f), true);
            UiFactory.Stretch(scrim.rectTransform);

            RectTransform column = CreateContentColumn(root, 16f, 40, 24, 40);
            BuildWalletRow(column);
            UiFactory.CreateSpacer(column);
            _card = UiFactory.CreateCard(column, "ResultCard", UiTheme.CardRadius + 8f, 40, 14, TextAnchor.UpperCenter, UiTheme.Hex("#252C55"));
            if (_win != null)
            {
                BuildWin(_card);
            }
            else
            {
                BuildLoss(_card);
            }
            UiFactory.CreateSpacer(column);
        }

        /// <summary>Coins and lives at the top; the coin pill is the target of the fly-to-wallet effect.</summary>
        private void BuildWalletRow(RectTransform column)
        {
            RectTransform row = UiFactory.CreateRow(column, "Wallet", 84f, 14f, TextAnchor.MiddleLeft);
            WalletSnapshot wallet = (_win != null ? _win.wallet : _loss.wallet) ?? App.State.Wallet;
            long coins = wallet != null ? wallet.coins : 0;
            long reward = _win != null && _win.reward != null && _win.status != LevelCompleteResponse.AlreadyProcessed ? _win.reward.coins : 0;
            _walletCoinsShown = coins - reward;
            _coinPill = UiFactory.CreatePill(row, "CoinPill", IconFactory.Coin(), Color.white, TimeFormat.Number(_walletCoinsShown), out _coinPillLabel, 84f, null, UiTheme.BodySize + 4, 220f);
            UiFactory.CreateSpacer(row);
            string lives = wallet != null ? wallet.lives + "/" + wallet.maxLives : "-";
            UiFactory.CreatePill(row, "LivesPill", IconFactory.Heart(), Color.white, lives, out _, 84f, null, UiTheme.BodySize + 4, 170f);
        }

        private void BuildWin(RectTransform card)
        {
            bool replayed = _win.status == LevelCompleteResponse.AlreadyProcessed;
            UiFactory.CreateTitle(card, "Level " + _win.level + " cleared!", UiTheme.ScoreSize - 8, UiTheme.Text);

            RectTransform starsRow = UiFactory.CreateRow(card, "Stars", 120f, 10f);
            Sprite star = IconFactory.Star();
            UiFactory.CreateSpacer(starsRow);
            for (int i = 0; i < 3; i++)
            {
                _stars[i] = UiFactory.CreateIcon(starsRow, star, UiTheme.StarOff, i == 1 ? 120f : 96f);
            }
            UiFactory.CreateSpacer(starsRow);

            UiFactory.CreateLabel(card, "SCORE", UiTheme.SmallSize, UiTheme.Muted, TextAnchor.MiddleCenter, UiFont.BodyBold);
            Text score = UiFactory.CreateTitle(card, TimeFormat.Number(_win.score), UiTheme.ScoreSize, UiTheme.Text);
            if (_win.newBestScore)
            {
                RectTransform bestRow = UiFactory.CreateRow(card, "Best", 56f, 0f);
                UiFactory.CreateSpacer(bestRow);
                UiFactory.CreatePill(bestRow, "BestPill", IconFactory.Trophy(), Color.white, "New best!", out _, 56f, UiTheme.WithAlpha(UiTheme.Gold, 0.4f), UiTheme.SmallSize, 0f, UiFont.BodyBold);
                UiFactory.CreateSpacer(bestRow);
            }
            _ = score;

            RectTransform reward = UiFactory.CreateCard(card, "Reward", UiTheme.CardRadius, 24, 10, TextAnchor.MiddleCenter, UiTheme.CardFillStrong, false, false);
            if (_win.reward != null)
            {
                RectTransform coinRow = UiFactory.CreateRow(reward, "Coins", 90f, 14f);
                UiFactory.CreateSpacer(coinRow);
                UiFactory.CreateIcon(coinRow, IconFactory.Coin(), Color.white, 72f);
                _rewardLabel = UiFactory.CreateTitle(coinRow, "+0", UiTheme.ScoreSize - 4, UiTheme.Gold, TextAnchor.MiddleLeft);
                _rewardLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
                UiFactory.CreateSpacer(coinRow);

                RectTransform tagRow = UiFactory.CreateRow(reward, "Tags", 52f, 12f);
                UiFactory.CreateSpacer(tagRow);
                UiFactory.CreatePill(tagRow, "StrategyPill", null, Color.white, RewardTag(_win.reward), out _, 52f,
                    UiTheme.WithAlpha(StrategyColor(_win.reward.strategy), 0.35f), UiTheme.TinySize, 0f, UiFont.BodyBold);
                if (_win.firstClear)
                {
                    UiFactory.CreatePill(tagRow, "FirstClearPill", null, Color.white, "First clear bonus", out _, 52f,
                        UiTheme.WithAlpha(UiTheme.Sky, 0.35f), UiTheme.TinySize, 0f, UiFont.BodyBold);
                }
                UiFactory.CreateSpacer(tagRow);
            }
            if (_win.eventPoints != null)
            {
                foreach (EventPointsAwarded points in _win.eventPoints)
                {
                    RectTransform row = UiFactory.CreateRow(reward, "Event", 56f, 10f);
                    UiFactory.CreateSpacer(row);
                    UiFactory.CreateIcon(row, points.type == "ROCKET_RACE" ? IconFactory.Rocket() : IconFactory.Flag(), Color.white, 44f);
                    string unit = points.type == "ROCKET_RACE" ? (points.points == 1 ? " Rocket" : " Rockets") : " points";
                    Text label = UiFactory.CreateLabel(row, "+" + points.points + unit + "  ·  " + points.name + " total " + points.totalPoints,
                        UiTheme.SmallSize, UiTheme.TextSoft, TextAnchor.MiddleLeft, UiFont.BodyBold);
                    label.horizontalOverflow = HorizontalWrapMode.Overflow;
                    UiFactory.CreateSpacer(row);
                }
            }
            if (replayed)
            {
                UiFactory.CreateLabel(reward, "This result had already been processed (retried request) — no double reward.",
                    UiTheme.TinySize, UiTheme.Muted);
            }

            UiFactory.CreateGap(card, 6f);
            UiFactory.CreateButton(card, "Next: level " + _win.nextLevel, () => Run(App.Flow.StartLevel(_win.nextLevel)),
                ButtonStyle.Primary, UiTheme.HeadingSize - 6, UiTheme.ButtonHeight, -1f, IconFactory.Play());
            UiFactory.CreateButton(card, "Home", () => App.Flow.GoHome(), ButtonStyle.Secondary, UiTheme.BodySize, UiTheme.ButtonHeight - 10f);
        }

        private void BuildLoss(RectTransform card)
        {
            int level = _session != null ? _session.Level : _loss.level;
            int target = _session != null ? _session.TargetScore : 0;
            UiFactory.CreateTitle(card, "Out of moves", UiTheme.ScoreSize - 8, UiTheme.Danger);
            UiFactory.CreateLabel(card, Encouragements[Random.Range(0, Encouragements.Length)], UiTheme.HeadingSize - 8, UiTheme.Text, TextAnchor.MiddleCenter, UiFont.BodyBold);
            UiFactory.CreateLabel(card, "Level " + level + " not cleared", UiTheme.BodySize, UiTheme.TextSoft);

            UiFactory.CreateLabel(card, "SCORE", UiTheme.SmallSize, UiTheme.Muted, TextAnchor.MiddleCenter, UiFont.BodyBold);
            UiFactory.CreateTitle(card, TimeFormat.Number(_loss.score), UiTheme.ScoreSize, UiTheme.Text);
            if (target > 0)
            {
                UiFactory.CreateLabel(card, "Target was " + TimeFormat.Number(target), UiTheme.SmallSize, UiTheme.Muted);
            }
            if (_loss.status == LevelCompleteResponse.AlreadyProcessed)
            {
                UiFactory.CreateLabel(card, "(this attempt was already closed)", UiTheme.TinySize, UiTheme.Muted);
            }
            UiFactory.CreateGap(card, 6f);
            UiFactory.CreateButton(card, "Try again", () => Run(App.Flow.StartLevel(level)), ButtonStyle.Primary, UiTheme.HeadingSize - 6,
                UiTheme.ButtonHeight, -1f, IconFactory.Play());
            UiFactory.CreateButton(card, "Home", () => App.Flow.GoHome(), ButtonStyle.Secondary, UiTheme.BodySize, UiTheme.ButtonHeight - 10f);
        }

        // ------------------------------------------------------------------ celebration

        protected override void OnShown()
        {
            Run(_win != null ? Celebrate() : Commiserate());
        }

        private IEnumerator Celebrate()
        {
            Tween.ScaleFrom(_card, 0.7f, 0.5f, Ease.OutBack);
            AudioManager.Play(Sfx.WinJingle);
            if (App.Fx != null)
            {
                App.Fx.Confetti(90, 2.6f);
            }
            yield return Tween.WaitSeconds(0.35f);

            for (int i = 0; i < _win.stars && i < 3; i++)
            {
                Image star = _stars[i];
                star.color = UiTheme.Gold;
                Tween.ScaleFrom(star.transform, 0.1f, 0.5f, Ease.OutBack);
                AudioManager.Play(Sfx.StarChime, 1f + i * 0.12f);
                if (App.Fx != null) App.Fx.Sparkle(star.transform.position, UiTheme.Gold, 14, 60f);
                yield return Tween.WaitSeconds(0.28f);
            }

            if (_rewardLabel != null && _win.reward != null && _win.status != LevelCompleteResponse.AlreadyProcessed)
            {
                long coins = _win.reward.coins;
                float lastTick = -1f;
                float elapsed = 0f;
                const float duration = 0.8f;
                while (elapsed < duration)
                {
                    elapsed += Time.unscaledDeltaTime * Tween.TimeScale;
                    float t = Easing.Evaluate(Ease.OutCubic, Mathf.Clamp01(elapsed / duration));
                    _rewardLabel.text = "+" + TimeFormat.Number((long)(coins * t));
                    if (elapsed - lastTick > 0.07f)
                    {
                        lastTick = elapsed;
                        AudioManager.Play(Sfx.CoinTick, 0.9f + t * 0.4f, 0.6f);
                    }
                    yield return null;
                }
                _rewardLabel.text = "+" + TimeFormat.Number(coins);
                Tween.Punch(_rewardLabel.transform, 0.2f, 0.4f);

                int flying = Mathf.Clamp((int)(coins / 40), 4, 8);
                long perCoin = coins / flying;
                long remaining = coins;
                if (App.Fx != null)
                {
                    App.Fx.FlyCoins(_rewardLabel.transform.position, _coinPill.position, flying, () =>
                    {
                        if (!IsAlive) return;
                        long add = remaining > perCoin * 2 ? perCoin : remaining;
                        remaining -= add;
                        _walletCoinsShown += add;
                        _coinPillLabel.text = TimeFormat.Number(_walletCoinsShown);
                        Tween.Punch(_coinPill, 0.16f, 0.3f);
                        AudioManager.Play(Sfx.CoinTick, 1.3f, 0.7f);
                    });
                }
                else
                {
                    _walletCoinsShown += coins;
                    _coinPillLabel.text = TimeFormat.Number(_walletCoinsShown);
                }
            }
        }

        private IEnumerator Commiserate()
        {
            Tween.ScaleFrom(_card, 0.8f, 0.4f, Ease.OutBack);
            AudioManager.Play(Sfx.LoseSting);
            yield return Tween.WaitSeconds(0.4f);
            Tween.Shake(_card, 12f, 0.45f);
        }

        // ------------------------------------------------------------------ helpers

        /// <summary>"Double Reward Weekend x2" style tag from the server's strategy name and multiplier.</summary>
        private static string RewardTag(Reward reward)
        {
            string name;
            switch (reward.strategy)
            {
                case "DOUBLE_REWARD_EVENT": name = "Double Reward Weekend"; break;
                case "EXPERIMENT": name = "Experiment reward"; break;
                case "STANDARD": name = "Standard reward"; break;
                default: name = reward.strategy ?? "Reward"; break;
            }
            return reward.multiplier != 1.0 ? name + "  x" + reward.multiplier.ToString("0.##") : name;
        }

        private static Color StrategyColor(string strategy)
        {
            switch (strategy)
            {
                case "DOUBLE_REWARD_EVENT": return UiTheme.Pink;
                case "EXPERIMENT": return UiTheme.Violet;
                default: return UiTheme.Blue;
            }
        }
    }
}
