using UnityEngine;
using UnityEngine.UI;

namespace BlastScale.Client.UI
{
    /// <summary>
    /// Semi-transparent full-screen panel shown while any API request is in flight. It also
    /// swallows clicks, which is what makes "the player cannot tap the board while the completion
    /// is being submitted" true without every screen having to think about it.
    /// </summary>
    public sealed class LoadingOverlay
    {
        private readonly GameObject _root;

        public LoadingOverlay(RectTransform layer)
        {
            Image backdrop = UiFactory.CreatePanel(layer, "LoadingOverlay", new Color(0f, 0f, 0f, 0.55f), true);
            UiFactory.Stretch(backdrop.rectTransform);
            Text label = UiFactory.CreateLabel(backdrop.transform, "Loading...", UiTheme.HeadingSize, UiTheme.Text);
            UiFactory.Stretch(label.rectTransform);
            _root = backdrop.gameObject;
            _root.SetActive(false);
        }

        /// <summary>Bound to <see cref="Net.ApiClient.BusyChanged"/>.</summary>
        public void SetVisible(bool visible)
        {
            if (_root != null)
            {
                _root.SetActive(visible);
            }
        }
    }
}
