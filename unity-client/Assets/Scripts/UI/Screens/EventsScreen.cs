using System.Collections;
using System.Collections.Generic;
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
            RectTransform column = CreateContentColumn(root, 14f, 36, 20, 28);
            CreateHeader(column, "Events", () => App.Flow.GoHome());
            _infoLabel = UiFactory.CreateLabel(column, "Loading...", UiTheme.SmallSize - 2, UiTheme.Muted, TextAnchor.MiddleLeft, UiFont.BodyBold, 36f);
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
            _infoLabel.text = events.Count == 0 ? "No active events right now" : events.Count + " active event" + (events.Count == 1 ? "" : "s");
            int index = 0;
            foreach (PlayerEventView view in events)
            {
                RectTransform card = CreateCard(view);
                Tween.ScaleFrom(card, 0.9f, 0.35f, Ease.OutBack, 0.08f * index++);
            }
        }

        private RectTransform CreateCard(PlayerEventView view)
        {
            bool rocket = view.type == "ROCKET_RACE";
            RectTransform card = UiFactory.CreateCard(_content, "Event " + view.id, UiTheme.CardRadius, 24, 10, TextAnchor.UpperLeft);

            RectTransform head = UiFactory.CreateRow(card, "Head", 84f, 16f, TextAnchor.MiddleLeft);
            UiFactory.CreateIcon(head, rocket ? IconFactory.Rocket() : IconFactory.Gift(), Color.white, 76f);
            RectTransform texts = UiFactory.CreateRect(head, "Texts");
            UiFactory.AddVerticalLayout(texts, 2f, 0, TextAnchor.MiddleLeft);
            UiFactory.SetLayout(texts.gameObject, flexibleWidth: 1f);
            UiFactory.CreateLabel(texts, view.name, UiTheme.BodySize + 2, UiTheme.Text, TextAnchor.MiddleLeft, UiFont.BodyBold);
            UiFactory.CreateLabel(texts, Describe(view), UiTheme.TinySize, UiTheme.Muted, TextAnchor.MiddleLeft, UiFont.Body);
            UiFactory.CreatePill(head, "TimeLeft", null, Color.white, TimeFormat.Duration(view.secondsRemaining) + " left", out _, 52f,
                UiTheme.WithAlpha(rocket ? UiTheme.Sky : UiTheme.Pink, 0.35f), UiTheme.TinySize, 0f, UiFont.BodyBold);

            if (rocket)
            {
                string mine = view.eligible
                    ? "Your rockets: " + view.myPoints + (view.myRank.HasValue ? "  ·  rank #" + view.myRank.Value : "  ·  not ranked yet")
                    : "Reach the minimum level to take part";
                UiFactory.CreateLabel(card, mine, UiTheme.SmallSize, UiTheme.Sky, TextAnchor.MiddleLeft, UiFont.BodyBold);
            }
            else if (view.type == "DOUBLE_REWARD")
            {
                UiFactory.CreateLabel(card, "Level rewards are multiplied while this event runs", UiTheme.SmallSize, UiTheme.Pink, TextAnchor.MiddleLeft, UiFont.BodyBold);
            }

            if (view.top != null && view.top.Count > 0)
            {
                UiFactory.CreateDivider(card);
                UiFactory.CreateLabel(card, "TOP PLAYERS", UiTheme.TinySize, UiTheme.Muted, TextAnchor.MiddleLeft, UiFont.BodyBold);
                foreach (Standing standing in view.top)
                {
                    bool me = standing.playerId == App.State.PlayerId;
                    RectTransform row = UiFactory.CreateRow(card, "Standing", 44f, 12f, TextAnchor.MiddleLeft);
                    Text rank = UiFactory.CreateLabel(row, "#" + standing.rank, UiTheme.SmallSize - 2, me ? UiTheme.Gold : UiTheme.Muted, TextAnchor.MiddleLeft, UiFont.Display);
                    UiFactory.SetLayout(rank.gameObject, preferredWidth: 70f, flexibleWidth: 0f);
                    Text name = UiFactory.CreateLabel(row, standing.name, UiTheme.SmallSize - 2, me ? UiTheme.Gold : UiTheme.Text, TextAnchor.MiddleLeft, UiFont.BodyBold);
                    UiFactory.SetLayout(name.gameObject, flexibleWidth: 1f);
                    string points = standing.points + (rocket ? " rockets" : " pts");
                    if (standing.rewardCoins.HasValue) points += "  ·  " + standing.rewardCoins.Value + " coins";
                    Text score = UiFactory.CreateLabel(row, points, UiTheme.TinySize, UiTheme.TextSoft, TextAnchor.MiddleRight, UiFont.Body);
                    score.horizontalOverflow = HorizontalWrapMode.Overflow;
                    UiFactory.SetLayout(score.gameObject, preferredWidth: 300f, flexibleWidth: 0f);
                }
            }
            return card;
        }

        /// <summary>Type and the rule values worth showing (points per level, multiplier...).</summary>
        private static string Describe(PlayerEventView view)
        {
            string text = view.type.Replace('_', ' ');
            if (view.configuration != null)
            {
                if (view.configuration.TryGetValue("pointsPerLevel", out var points)) text += "  ·  " + points + " rocket(s) per level";
                if (view.configuration.TryGetValue("minimumLevel", out var min)) text += "  ·  from level " + min;
                if (view.configuration.TryGetValue("multiplier", out var mult)) text += "  ·  rewards x" + mult;
            }
            return text;
        }
    }
}
