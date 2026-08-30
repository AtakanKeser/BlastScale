using System.Collections;
using BlastScale.Client.UI.Fx;
using BlastScale.Client.UI.Gfx;
using UnityEngine;
using UnityEngine.UI;

namespace BlastScale.Client.UI
{
    /// <summary>
    /// Dimmed full-screen panel with a spinning ring, shown while an API request is in flight. It
    /// swallows clicks, which is what makes "the player cannot tap the board while the completion
    /// is being submitted" true without every screen having to think about it. To avoid a flash
    /// on fast calls it only becomes visible after a short grace period.
    /// </summary>
    public sealed class LoadingOverlay
    {
        private const float GraceSeconds = 0.18f;

        private readonly MonoBehaviour _runner;
        private readonly GameObject _root;
        private readonly CanvasGroup _group;
        private Coroutine _showRoutine;
        private bool _visible;

        public LoadingOverlay(RectTransform layer, MonoBehaviour runner)
        {
            _runner = runner;
            Image backdrop = UiFactory.CreatePanel(layer, "LoadingOverlay", new Color(0.02f, 0.02f, 0.06f, 0.55f), true);
            UiFactory.Stretch(backdrop.rectTransform);
            _root = backdrop.gameObject;
            _group = _root.AddComponent<CanvasGroup>();

            Image ring = UiFactory.CreateImage(backdrop.transform, "Spinner", SpriteFactory.SpinnerArc(160), UiTheme.Sky);
            UiFactory.Center(ring.rectTransform, 150f, 150f);
            ring.gameObject.AddComponent<Spinner>();

            Text label = UiFactory.CreateLabel(backdrop.transform, "Loading", UiTheme.SmallSize, UiTheme.TextSoft, TextAnchor.MiddleCenter, UiFont.BodyBold);
            label.rectTransform.anchorMin = label.rectTransform.anchorMax = new Vector2(0.5f, 0.5f);
            label.rectTransform.sizeDelta = new Vector2(400f, 50f);
            label.rectTransform.anchoredPosition = new Vector2(0f, -120f);
            _root.SetActive(false);
        }

        /// <summary>Bound to <see cref="Net.IApiClient.BusyChanged"/>.</summary>
        public void SetVisible(bool visible)
        {
            if (_root == null || _visible == visible)
            {
                return;
            }
            _visible = visible;
            if (_showRoutine != null)
            {
                _runner.StopCoroutine(_showRoutine);
                _showRoutine = null;
            }
            if (visible)
            {
                _showRoutine = _runner.StartCoroutine(ShowAfterGrace());
            }
            else
            {
                _root.SetActive(false);
            }
        }

        private IEnumerator ShowAfterGrace()
        {
            yield return new WaitForSecondsRealtime(GraceSeconds);
            _showRoutine = null;
            if (_visible && _root != null)
            {
                _root.SetActive(true);
                _group.alpha = 0f;
                Tween.Fade(_group, 1f, 0.15f);
            }
        }
    }
}
