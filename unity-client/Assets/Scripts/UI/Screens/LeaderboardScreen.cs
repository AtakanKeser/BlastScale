using System.Collections;
using BlastScale.Client.Core;
using BlastScale.Client.Net;
using BlastScale.Client.Net.Dto;
using BlastScale.Client.UI.Fx;
using BlastScale.Client.UI.Gfx;
using UnityEngine;
using UnityEngine.UI;

namespace BlastScale.Client.UI.Screens
{
    /// <summary>Weekly leaderboard: top players of the current ISO week plus the caller's own rank.</summary>
    public sealed class LeaderboardScreen : UiScreen
    {
        private const int Limit = 50;

        private Text _infoLabel;
        private Text _myRankLabel;
        private RectTransform _content;
        private RectTransform _myRankCard;

        protected override void Build(RectTransform root)
        {
            RectTransform column = CreateContentColumn(root, 14f, 36, 20, 28);
            CreateHeader(column, "Leaderboard", () => App.Flow.GoHome());
            _infoLabel = UiFactory.CreateLabel(column, "Loading...", UiTheme.SmallSize - 2, UiTheme.Muted, TextAnchor.MiddleLeft, UiFont.BodyBold, 36f);
            UiFactory.CreateScrollView(column, out _content, 10f);
            _myRankCard = UiFactory.CreateCard(column, "MyRank", UiTheme.CardRadius, 22, 10, TextAnchor.MiddleCenter, UiTheme.WithAlpha(UiTheme.Violet, 0.35f));
            _myRankLabel = UiFactory.CreateLabel(_myRankCard, "", UiTheme.BodySize, UiTheme.Text, TextAnchor.MiddleCenter, UiFont.BodyBold);
        }

        protected override void OnShown()
        {
            Run(Load());
        }

        private IEnumerator Load()
        {
            var result = new ApiResult<LeaderboardView>();
            yield return App.Api.GetJson(ApiRoutes.WeeklyLeaderboard(Limit), result);
            if (!IsAlive) yield break;
            if (!result.Ok || result.Value == null)
            {
                _infoLabel.text = "Leaderboard unavailable";
                App.Flow.ShowError(result.Error);
                yield break;
            }
            Render(result.Value);
        }

        private void Render(LeaderboardView view)
        {
            long endsIn = App.State.SecondsUntil(view.endsAt);
            _infoLabel.text = "Season " + view.season + (view.finalized ? "  ·  finalized" : "  ·  ends in " + TimeFormat.Duration(endsIn));

            if (view.players == null || view.players.Count == 0)
            {
                RectTransform empty = UiFactory.CreateCard(_content, "Empty", UiTheme.CardRadius, 30, 8, TextAnchor.MiddleCenter);
                UiFactory.CreateIcon(empty, IconFactory.Trophy(), Color.white, 96f);
                UiFactory.CreateLabel(empty, "No scores yet this week.\nClear a level to appear here!", UiTheme.BodySize - 2, UiTheme.TextSoft);
            }
            else
            {
                int index = 0;
                foreach (LeaderboardView.Entry entry in view.players)
                {
                    bool me = entry.playerId == App.State.PlayerId;
                    RectTransform row = CreateRow(entry.rank, entry.name, TimeFormat.Number(entry.score), me);
                    Tween.ScaleFrom(row, 0.9f, 0.3f, Ease.OutBack, 0.03f * index++);
                }
            }
            _myRankLabel.text = view.myRank.HasValue
                ? "Your rank: #" + view.myRank.Value + "  ·  " + TimeFormat.Number(view.myScore) + " points"
                : "You are not ranked yet";
            Tween.ScaleFrom(_myRankCard, 0.9f, 0.35f, Ease.OutBack, 0.2f);
        }

        /// <summary>A rank medal, the name and the score; the top three get gold/silver/bronze medals.</summary>
        private RectTransform CreateRow(int rank, string name, string score, bool highlight)
        {
            RectTransform row = UiFactory.CreateCard(_content, "Row " + rank, 30f, 14, 16, TextAnchor.MiddleLeft,
                highlight ? UiTheme.WithAlpha(UiTheme.Violet, 0.4f) : UiTheme.CardFill, true, false);
            UiFactory.SetLayout(row.gameObject, preferredHeight: 92f, minHeight: 92f);

            Color medal = rank == 1 ? UiTheme.Gold : rank == 2 ? UiTheme.Hex("#C9D1E4") : rank == 3 ? UiTheme.Hex("#D08A4E") : new Color(1f, 1f, 1f, 0.12f);
            RectTransform badge = UiFactory.CreateRect(row, "Rank");
            UiFactory.SetLayout(badge.gameObject, preferredWidth: 64f, preferredHeight: 64f, minWidth: 64f, flexibleWidth: 0f);
            Image circle = UiFactory.CreateImage(badge, "Circle", SpriteFactory.Circle(64), medal);
            UiFactory.Stretch(circle.rectTransform);
            Text rankLabel = UiFactory.CreateLabel(badge, rank.ToString(), UiTheme.SmallSize, rank <= 3 ? UiTheme.Hex("#2A1B3D") : UiTheme.Text, TextAnchor.MiddleCenter, UiFont.Display);
            UiFactory.Stretch(rankLabel.rectTransform);

            Text nameLabel = UiFactory.CreateLabel(row, name, UiTheme.BodySize - 2, UiTheme.Text, TextAnchor.MiddleLeft, UiFont.BodyBold);
            UiFactory.SetLayout(nameLabel.gameObject, flexibleWidth: 1f);
            Text scoreLabel = UiFactory.CreateLabel(row, score, UiTheme.BodySize - 2, UiTheme.Gold, TextAnchor.MiddleRight, UiFont.Display);
            UiFactory.SetLayout(scoreLabel.gameObject, preferredWidth: 220f, flexibleWidth: 0f);
            return row;
        }
    }
}
