using System.Collections;
using UnityEngine;
using UnityEngine.UI;

namespace BlastScale.Client.UI
{
    /// <summary>
    /// Short message at the bottom of the screen that disappears by itself. Used for errors
    /// ("No lives left") and confirmations ("+100 coins"); it never blocks input.
    /// </summary>
    public sealed class Toast
    {
        private const float DefaultSeconds = 3f;

        private readonly MonoBehaviour _runner;
        private readonly Image _panel;
        private readonly Text _label;
        private Coroutine _hideRoutine;

        public Toast(MonoBehaviour runner, RectTransform layer)
        {
            _runner = runner;
            _panel = UiFactory.CreatePanel(layer, "Toast", UiTheme.PanelLight);
            RectTransform rt = _panel.rectTransform;
            rt.anchorMin = new Vector2(0.5f, 0f);
            rt.anchorMax = new Vector2(0.5f, 0f);
            rt.pivot = new Vector2(0.5f, 0f);
            rt.anchoredPosition = new Vector2(0f, 200f);
            rt.sizeDelta = new Vector2(960f, 0f);
            UiFactory.AddVerticalLayout(rt, 0f, 24, TextAnchor.MiddleCenter);
            var fitter = rt.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _label = UiFactory.CreateLabel(rt, "", UiTheme.BodySize, UiTheme.Text);
            _panel.gameObject.SetActive(false);
        }

        /// <summary>Shows the message; a new message replaces the previous one immediately.</summary>
        public void Show(string message, bool isError = false, float seconds = DefaultSeconds)
        {
            if (_panel == null)
            {
                return;
            }
            _panel.color = isError ? UiTheme.Danger : UiTheme.PanelLight;
            _label.text = message;
            _panel.gameObject.SetActive(true);
            if (_hideRoutine != null)
            {
                _runner.StopCoroutine(_hideRoutine);
            }
            _hideRoutine = _runner.StartCoroutine(HideAfter(seconds));
        }

        private IEnumerator HideAfter(float seconds)
        {
            yield return new WaitForSeconds(seconds);
            if (_panel != null)
            {
                _panel.gameObject.SetActive(false);
            }
            _hideRoutine = null;
        }
    }
}
