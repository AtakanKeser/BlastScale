using BlastScale.Client.Core;
using BlastScale.Client.Net.Dto;
using UnityEngine;
using UnityEngine.UI;

namespace BlastScale.Client.UI.Screens
{
    /// <summary>
    /// End-of-level summary. For a win it shows what the <b>server</b> decided (score, stars,
    /// reward strategy, event points) — never the client's own numbers — and offers the next level.
    /// For a loss it shows the score reached and offers a retry.
    /// </summary>
    public sealed class ResultScreen : UiScreen
    {
        private readonly LevelSession _session;
        private readonly LevelCompleteResponse _win;
        private readonly LevelFailResponse _loss;

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

        protected override void Build(RectTransform root)
        {
            RectTransform column = CreateContentColumn(root, 20f, 60);
            UiFactory.CreateSpacer(column);
            if (_win != null)
            {
                BuildWin(column);
            }
            else
            {
                BuildLoss(column);
            }
            UiFactory.CreateSpacer(column);
        }

        private void BuildWin(RectTransform column)
        {
            bool replayed = _win.status == LevelCompleteResponse.AlreadyProcessed;
            UiFactory.CreateLabel(column, "Level " + _win.level + " cleared!", UiTheme.TitleSize, UiTheme.Success, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.CreateLabel(column, TimeFormat.Stars(_win.stars), 110, UiTheme.Warning);
            string scoreLine = "Score " + TimeFormat.Number(_win.score);
            if (_win.newBestScore) scoreLine += "  ·  new best!";
            UiFactory.CreateLabel(column, scoreLine, UiTheme.HeadingSize, UiTheme.Text);

            Image rewardPanel = UiFactory.CreatePanel(column, "Reward", UiTheme.Panel);
            UiFactory.AddVerticalLayout(rewardPanel.rectTransform, 8f, 24, TextAnchor.MiddleCenter);
            if (_win.reward != null)
            {
                UiFactory.CreateLabel(rewardPanel.transform, "+" + TimeFormat.Number(_win.reward.coins) + " coins", UiTheme.HeadingSize, UiTheme.Success, TextAnchor.MiddleCenter, FontStyle.Bold);
                string detail = "Reward strategy: " + _win.reward.strategy + "  ·  multiplier x" + _win.reward.multiplier.ToString("0.##");
                if (_win.firstClear) detail += "\nFirst clear bonus included";
                UiFactory.CreateLabel(rewardPanel.transform, detail, UiTheme.SmallSize, UiTheme.Muted);
            }
            if (_win.eventPoints != null && _win.eventPoints.Count > 0)
            {
                foreach (EventPointsAwarded points in _win.eventPoints)
                {
                    UiFactory.CreateLabel(rewardPanel.transform,
                        points.name + ": +" + points.points + " (total " + points.totalPoints + ")",
                        UiTheme.BodySize, UiTheme.Accent);
                }
            }
            if (replayed)
            {
                UiFactory.CreateLabel(rewardPanel.transform, "This result had already been processed (retried request) — no double reward.",
                    UiTheme.SmallSize, UiTheme.Muted);
            }
            WalletSnapshot wallet = _win.wallet ?? App.State.Wallet;
            if (wallet != null)
            {
                UiFactory.CreateLabel(column, "Coins " + TimeFormat.Number(wallet.coins) + "  ·  Lives " + wallet.lives + "/" + wallet.maxLives +
                                              "  ·  Stars " + wallet.stars, UiTheme.BodySize, UiTheme.Muted);
            }

            UiFactory.CreateButton(column, "Next: level " + _win.nextLevel, () => Run(App.Flow.StartLevel(_win.nextLevel)),
                UiTheme.Accent, UiTheme.HeadingSize, 130f);
            UiFactory.CreateButton(column, "Home", () => App.Flow.GoHome(), UiTheme.Secondary);
        }

        private void BuildLoss(RectTransform column)
        {
            int level = _session != null ? _session.Level : _loss.level;
            int target = _session != null ? _session.TargetScore : 0;
            UiFactory.CreateLabel(column, "Out of moves", UiTheme.TitleSize, UiTheme.Danger, TextAnchor.MiddleCenter, FontStyle.Bold);
            UiFactory.CreateLabel(column, "Level " + level + " not cleared", UiTheme.HeadingSize, UiTheme.Text);
            string scoreLine = "Score " + TimeFormat.Number(_loss.score);
            if (target > 0) scoreLine += " / target " + TimeFormat.Number(target);
            UiFactory.CreateLabel(column, scoreLine, UiTheme.BodySize, UiTheme.Muted);
            WalletSnapshot wallet = _loss.wallet ?? App.State.Wallet;
            if (wallet != null)
            {
                UiFactory.CreateLabel(column, "Lives left: " + wallet.lives + "/" + wallet.maxLives, UiTheme.BodySize, UiTheme.Muted);
            }
            if (_loss.status == LevelCompleteResponse.AlreadyProcessed)
            {
                UiFactory.CreateLabel(column, "(this attempt was already closed)", UiTheme.SmallSize, UiTheme.Muted);
            }
            UiFactory.CreateButton(column, "Retry level " + level, () => Run(App.Flow.StartLevel(level)), UiTheme.Accent, UiTheme.HeadingSize, 130f);
            UiFactory.CreateButton(column, "Home", () => App.Flow.GoHome(), UiTheme.Secondary);
        }
    }
}
