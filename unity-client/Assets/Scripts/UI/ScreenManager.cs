using BlastScale.Client.Core;
using UnityEngine;

namespace BlastScale.Client.UI
{
    /// <summary>
    /// Shows exactly one screen at a time inside the screen layer of the canvas. There is no
    /// back-stack on purpose: every screen knows where "back" leads, which keeps navigation
    /// explicit and trivially testable.
    /// </summary>
    public sealed class ScreenManager
    {
        private readonly RectTransform _layer;
        private AppContext _app;
        private UiScreen _current;

        public ScreenManager(RectTransform layer)
        {
            _layer = layer;
        }

        /// <summary>Late binding because the context and the manager reference each other.</summary>
        public void Bind(AppContext app)
        {
            _app = app;
        }

        public UiScreen Current => _current;

        /// <summary>Hides the current screen (if any) and shows the new one.</summary>
        public void Show(UiScreen screen)
        {
            if (_current != null)
            {
                _current.Hide();
            }
            _current = screen;
            screen.Show(_app, _layer);
        }
    }
}
