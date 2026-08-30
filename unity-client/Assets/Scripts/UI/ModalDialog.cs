using System;
using BlastScale.Client.Audio;
using BlastScale.Client.UI.Fx;
using UnityEngine;
using UnityEngine.UI;

namespace BlastScale.Client.UI
{
    /// <summary>A button of a <see cref="ModalDialog"/>: caption, style and what happens when it is pressed.</summary>
    public sealed class ModalButton
    {
        public string Label { get; }
        public ButtonStyle Style { get; }
        public Action Action { get; }

        private ModalButton(string label, ButtonStyle style, Action action)
        {
            Label = label;
            Style = style;
            Action = action;
        }

        public static ModalButton Primary(string label, Action action)
        {
            return new ModalButton(label, ButtonStyle.Primary, action);
        }

        public static ModalButton Secondary(string label, Action action)
        {
            return new ModalButton(label, ButtonStyle.Secondary, action);
        }

        public static ModalButton Danger(string label, Action action)
        {
            return new ModalButton(label, ButtonStyle.Danger, action);
        }
    }

    /// <summary>
    /// Blocking question box ("Out of moves — use a booster?") on a dark scrim. The card scales
    /// in from 0.9 with a fade; only one is open at a time and every button closes the dialog
    /// before running its action.
    /// </summary>
    public sealed class ModalDialog
    {
        private readonly RectTransform _layer;
        private GameObject _current;

        public ModalDialog(RectTransform layer)
        {
            _layer = layer;
        }

        public bool IsOpen => _current != null;

        public void Show(string title, string message, params ModalButton[] buttons)
        {
            Close();
            Image scrim = UiFactory.CreatePanel(_layer, "Modal", UiTheme.Scrim, true);
            UiFactory.Stretch(scrim.rectTransform);
            _current = scrim.gameObject;
            var scrimGroup = scrim.gameObject.AddComponent<CanvasGroup>();
            scrimGroup.alpha = 0f;
            Tween.Fade(scrimGroup, 1f, 0.2f, Ease.OutQuad);

            RectTransform card = UiFactory.CreateCard(scrim.transform, "Box", UiTheme.CardRadius, 44, 22, TextAnchor.MiddleCenter,
                UiTheme.Hex("#242B52"));
            card.anchorMin = card.anchorMax = new Vector2(0.5f, 0.5f);
            card.pivot = new Vector2(0.5f, 0.5f);
            card.sizeDelta = new Vector2(920f, 0f);
            var fitter = card.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            UiFactory.CreateTitle(card, title, UiTheme.HeadingSize, UiTheme.Text);
            if (!string.IsNullOrEmpty(message))
            {
                UiFactory.CreateLabel(card, message, UiTheme.BodySize - 2, UiTheme.TextSoft);
            }
            UiFactory.CreateGap(card, 6f);
            foreach (ModalButton button in buttons)
            {
                ModalButton captured = button;
                UiFactory.CreateButton(card, captured.Label, () =>
                {
                    Close();
                    captured.Action?.Invoke();
                }, captured.Style, UiTheme.BodySize, UiTheme.ButtonHeight - 10f);
            }

            var cardGroup = card.gameObject.AddComponent<CanvasGroup>();
            cardGroup.alpha = 0f;
            Tween.Fade(cardGroup, 1f, 0.2f, Ease.OutQuad);
            Tween.ScaleFrom(card, 0.9f, 0.32f, Ease.OutBack);
            AudioManager.Play(Sfx.Whoosh, 1.3f, 0.35f);
        }

        /// <summary>Fades the dialog out and destroys it; <see cref="IsOpen"/> is false immediately.</summary>
        public void Close()
        {
            if (_current == null)
            {
                return;
            }
            GameObject closing = _current;
            _current = null;
            var group = closing.GetComponent<CanvasGroup>();
            if (group != null)
            {
                group.blocksRaycasts = false;
                group.interactable = false;
                Tween.Fade(group, 0f, 0.12f, Ease.Linear, 0f, () => UnityEngine.Object.Destroy(closing));
            }
            else
            {
                UnityEngine.Object.Destroy(closing);
            }
        }
    }
}
