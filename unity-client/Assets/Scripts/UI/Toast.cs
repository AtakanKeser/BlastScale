using System.Collections;
using BlastScale.Client.UI.Fx;
using BlastScale.Client.UI.Gfx;
using UnityEngine;
using UnityEngine.UI;

namespace BlastScale.Client.UI
{
    /// <summary>
    /// Short message that slides down from the top of the screen and disappears by itself. Used
    /// for errors ("No lives left") and confirmations ("+100 coins"); it never blocks input.
    /// </summary>
    public sealed class Toast
    {
        private const float DefaultSeconds = 3f;
        private const float HiddenY = 260f;
        private const float ShownY = -36f;

        private readonly MonoBehaviour _runner;
        private readonly RectTransform _panel;
        private readonly Image _body;
        private readonly Image _icon;
        private readonly Text _label;
        private readonly Sprite _okIcon;
        private readonly Sprite _errorIcon;
        private Coroutine _hideRoutine;

        public Toast(MonoBehaviour runner, RectTransform layer)
        {
            _runner = runner;
            _okIcon = IconFactory.Check();
            _errorIcon = IconFactory.Close();

            _panel = UiFactory.CreateRect(layer, "Toast");
            _panel.anchorMin = new Vector2(0.5f, 1f);
            _panel.anchorMax = new Vector2(0.5f, 1f);
            _panel.pivot = new Vector2(0.5f, 1f);
            _panel.anchoredPosition = new Vector2(0f, HiddenY);
            _panel.sizeDelta = new Vector2(940f, 0f);
            UiFactory.AddHorizontalLayout(_panel, 16f, new RectOffset(28, 34, 20, 20), TextAnchor.MiddleLeft);
            var fitter = _panel.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Image shadow = UiFactory.CreateImage(_panel, "Shadow", SpriteFactory.Shadow(36f, UiTheme.ShadowBlur), UiTheme.ShadowColor);
            UiFactory.Stretch(shadow.rectTransform, -28f, -28f, -20f, -36f);
            UiFactory.IgnoreLayout(shadow.rectTransform);
            _body = UiFactory.CreateImage(_panel, "Body", SpriteFactory.RoundedRect(36f), UiTheme.Hex("#20264A"));
            UiFactory.Stretch(_body.rectTransform);
            UiFactory.IgnoreLayout(_body.rectTransform);
            Image border = UiFactory.CreateImage(_panel, "Border", SpriteFactory.RoundedOutline(36f, 2f), UiTheme.CardBorder);
            UiFactory.Stretch(border.rectTransform);
            UiFactory.IgnoreLayout(border.rectTransform);

            _icon = UiFactory.CreateIcon(_panel, _okIcon, UiTheme.Primary, 40f);
            _label = UiFactory.CreateLabel(_panel, "", UiTheme.BodySize - 2, UiTheme.Text, TextAnchor.MiddleLeft, UiFont.BodyBold);
            UiFactory.SetLayout(_label.gameObject, flexibleWidth: 1f);
            _panel.gameObject.SetActive(false);
        }

        /// <summary>Shows the message; a new message replaces the previous one immediately.</summary>
        public void Show(string message, bool isError = false, float seconds = DefaultSeconds)
        {
            if (_panel == null)
            {
                return;
            }
            _body.color = isError ? UiTheme.Hex("#B8323C") : UiTheme.Hex("#20264A");
            _icon.sprite = isError ? _errorIcon : _okIcon;
            _icon.color = isError ? Color.white : UiTheme.Primary;
            _label.text = message;
            bool wasHidden = !_panel.gameObject.activeSelf;
            _panel.gameObject.SetActive(true);
            if (wasHidden)
            {
                _panel.anchoredPosition = new Vector2(0f, HiddenY);
            }
            Tween.Kill(_panel);
            Tween.Move(_panel, new Vector2(0f, ShownY), 0.35f, Ease.OutBack);
            if (_hideRoutine != null)
            {
                _runner.StopCoroutine(_hideRoutine);
            }
            _hideRoutine = _runner.StartCoroutine(HideAfter(seconds));
        }

        private IEnumerator HideAfter(float seconds)
        {
            yield return new WaitForSecondsRealtime(seconds);
            if (_panel != null)
            {
                Tween.Move(_panel, new Vector2(0f, HiddenY), 0.25f, Ease.InCubic, 0f, () =>
                {
                    if (_panel != null) _panel.gameObject.SetActive(false);
                });
            }
            _hideRoutine = null;
        }
    }
}
