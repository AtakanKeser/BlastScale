using System.Collections;
using BlastScale.Client.Core;
using BlastScale.Client.Net;
using BlastScale.Client.Net.Dto;
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

        protected override void Build(RectTransform root)
        {
            RectTransform column = CreateContentColumn(root, 16f, 40);
            CreateHeader(column, "Weekly leaderboard", () => App.Flow.GoHome());
            _infoLabel = UiFactory.CreateLabel(column, "Loading...", UiTheme.SmallSize, UiTheme.Muted, TextAnchor.MiddleLeft);
            UiFactory.CreateScrollView(column, out _content);
            _myRankLabel = UiFactory.CreateLabel(column, "", UiTheme.BodySize, UiTheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold, 70f);
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
                UiFactory.CreateLabel(_content, "No scores yet this week. Clear a level to appear here!", UiTheme.BodySize, UiTheme.Muted, TextAnchor.MiddleCenter, FontStyle.Normal, 120f);
            }
            else
            {
                foreach (LeaderboardView.Entry entry in view.players)
                {
                    bool me = entry.playerId == App.State.PlayerId;
                    CreateRow("#" + entry.rank, entry.name, TimeFormat.Number(entry.score), me);
                }
            }
            _myRankLabel.text = view.myRank.HasValue
                ? "Your rank: #" + view.myRank.Value + "  ·  score " + TimeFormat.Number(view.myScore)
                : "You are not ranked yet";
        }

        /// <summary>A three-column row; the player's own row is highlighted.</summary>
        private void CreateRow(string rank, string name, string score, bool highlight)
        {
            Image panel = UiFactory.CreatePanel(_content, "Row " + rank, highlight ? UiTheme.Highlight : UiTheme.PanelLight);
            UiFactory.SetLayout(panel.gameObject, preferredHeight: 80f, minHeight: 80f);
            UiFactory.AddHorizontalLayout(panel.rectTransform, 12f, 16);
            Text rankLabel = UiFactory.CreateLabel(panel.transform, rank, UiTheme.BodySize, UiTheme.Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.SetLayout(rankLabel.gameObject, preferredWidth: 120f);
            Text nameLabel = UiFactory.CreateLabel(panel.transform, name, UiTheme.BodySize, UiTheme.Text, TextAnchor.MiddleLeft);
            UiFactory.SetLayout(nameLabel.gameObject, flexibleWidth: 1f);
            Text scoreLabel = UiFactory.CreateLabel(panel.transform, score, UiTheme.BodySize, UiTheme.Warning, TextAnchor.MiddleRight, FontStyle.Bold);
            UiFactory.SetLayout(scoreLabel.gameObject, preferredWidth: 260f);
        }
    }
}
