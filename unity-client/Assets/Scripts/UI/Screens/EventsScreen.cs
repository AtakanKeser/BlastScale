using System.Collections;
using System.Collections.Generic;
using BlastScale.Client.Core;
using BlastScale.Client.Net;
using BlastScale.Client.Net.Dto;
using UnityEngine;
using UnityEngine.UI;

namespace BlastScale.Client.UI.Screens
{
    /// <summary>
    /// Live events (LiveOps): for each active event the player's points and rank, the time left
    /// and the current top standings. Events are configured from the admin panel at runtime, so
    /// this screen renders whatever the server sends without knowing the rules in advance.
    /// </summary>
    public sealed class EventsScreen : UiScreen
    {
        private RectTransform _content;
        private Text _infoLabel;

        protected override void Build(RectTransform root)
        {
            RectTransform column = CreateContentColumn(root, 16f, 40);
            CreateHeader(column, "Events", () => App.Flow.GoHome());
            _infoLabel = UiFactory.CreateLabel(column, "Loading...", UiTheme.SmallSize, UiTheme.Muted, TextAnchor.MiddleLeft);
            UiFactory.CreateScrollView(column, out _content, 16f);
        }

        protected override void OnShown()
        {
            Run(Load());
        }

        private IEnumerator Load()
        {
            var result = new ApiResult<List<PlayerEventView>>();
            yield return App.Api.GetJson(ApiRoutes.Events, result);
            if (!IsAlive) yield break;
            if (!result.Ok)
            {
                _infoLabel.text = "Events unavailable";
                App.Flow.ShowError(result.Error);
                yield break;
            }
            List<PlayerEventView> events = result.Value ?? new List<PlayerEventView>();
            _infoLabel.text = events.Count == 0 ? "No active events right now" : events.Count + " active event(s)";
            foreach (PlayerEventView view in events)
            {
                CreateCard(view);
            }
        }

        private void CreateCard(PlayerEventView view)
        {
            Image card = UiFactory.CreatePanel(_content, "Event " + view.id, UiTheme.PanelLight);
            UiFactory.AddVerticalLayout(card.rectTransform, 6f, 20, TextAnchor.UpperLeft);

            UiFactory.CreateLabel(card.transform, view.name, UiTheme.HeadingSize, UiTheme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.CreateLabel(card.transform, Describe(view), UiTheme.SmallSize, UiTheme.Muted, TextAnchor.MiddleLeft);

            if (view.type == "ROCKET_RACE")
            {
                string mine = view.eligible
                    ? "Your rockets: " + view.myPoints + (view.myRank.HasValue ? "  ·  rank #" + view.myRank.Value : "  ·  not ranked yet")
                    : "Reach the minimum level to take part";
                UiFactory.CreateLabel(card.transform, mine, UiTheme.BodySize, UiTheme.Accent, TextAnchor.MiddleLeft);
            }
            else if (view.type == "DOUBLE_REWARD")
            {
                UiFactory.CreateLabel(card.transform, "Level rewards are multiplied while this event runs", UiTheme.BodySize, UiTheme.Accent, TextAnchor.MiddleLeft);
            }

            if (view.top != null && view.top.Count > 0)
            {
                UiFactory.CreateLabel(card.transform, "Top players", UiTheme.SmallSize, UiTheme.Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
                foreach (Standing standing in view.top)
                {
                    bool me = standing.playerId == App.State.PlayerId;
                    string line = "#" + standing.rank + "  " + standing.name + "  —  " + standing.points;
                    if (standing.rewardCoins.HasValue) line += "  (" + standing.rewardCoins.Value + " coins)";
                    UiFactory.CreateLabel(card.transform, line, UiTheme.SmallSize, me ? UiTheme.Warning : UiTheme.Text, TextAnchor.MiddleLeft);
                }
            }
        }

        /// <summary>Type, time left and the rule values worth showing (points per level, multiplier...).</summary>
        private static string Describe(PlayerEventView view)
        {
            string text = view.type + "  ·  ends in " + TimeFormat.Duration(view.secondsRemaining);
            if (view.configuration != null)
            {
                if (view.configuration.TryGetValue("pointsPerLevel", out var points)) text += "  ·  " + points + " rocket(s) per level";
                if (view.configuration.TryGetValue("minimumLevel", out var min)) text += "  ·  from level " + min;
                if (view.configuration.TryGetValue("multiplier", out var mult)) text += "  ·  x" + mult;
            }
            return text;
        }
    }
}
