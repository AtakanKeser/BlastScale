using System.Collections;
using BlastScale.Client.Core;
using UnityEngine;
using UnityEngine.UI;

namespace BlastScale.Client.UI
{
    /// <summary>
    /// Base class of every screen. A screen is a plain object that builds its UGUI hierarchy under
    /// a root panel when shown and destroys it when hidden. Coroutines started through
    /// <see cref="Run"/> are guarded: once the screen is hidden they stop at the next yield, so a
    /// slow network reply can never touch a destroyed hierarchy.
    /// </summary>
    public abstract class UiScreen
    {
        /// <summary>Shared services (state, API, navigation, toast...).</summary>
        protected AppContext App { get; private set; }

        /// <summary>Root panel of the screen; null once hidden.</summary>
        public RectTransform Root { get; private set; }

        /// <summary>True while the screen's hierarchy exists.</summary>
        protected bool IsAlive => Root != null;

        /// <summary>Builds the hierarchy and starts the screen's work.</summary>
        public void Show(AppContext app, RectTransform parent)
        {
            App = app;
            Image panel = UiFactory.CreatePanel(parent, GetType().Name, UiTheme.Background, true);
            Root = panel.rectTransform;
            UiFactory.Stretch(Root);
            Build(Root);
            OnShown();
        }

        /// <summary>Tears the hierarchy down; guarded coroutines stop on their next yield.</summary>
        public void Hide()
        {
            if (Root != null)
            {
                Object.Destroy(Root.gameObject);
                Root = null;
            }
        }

        /// <summary>Creates the widgets under <paramref name="root"/>.</summary>
        protected abstract void Build(RectTransform root);

        /// <summary>Called after <see cref="Build"/>; the place to start loading data.</summary>
        protected virtual void OnShown()
        {
        }

        /// <summary>Starts a coroutine that is abandoned automatically when the screen is hidden.</summary>
        protected Coroutine Run(IEnumerator routine)
        {
            return App.Runner.StartCoroutine(Guarded(routine));
        }

        private IEnumerator Guarded(IEnumerator inner)
        {
            // A nested IEnumerator (an API call) still runs to completion so the request finishes
            // cleanly; only the continuation of the screen logic is skipped once it is dead.
            while (IsAlive && inner.MoveNext())
            {
                yield return inner.Current;
            }
        }

        // ------------------------------------------------------------------ shared building blocks

        /// <summary>Standard header row: a back button on the left and the title next to it.</summary>
        protected RectTransform CreateHeader(Transform parent, string title, UnityEngine.Events.UnityAction onBack)
        {
            RectTransform row = UiFactory.CreateRow(parent, "Header", 110f, 20f, TextAnchor.MiddleLeft);
            if (onBack != null)
            {
                UiFactory.CreateButton(row, "< Back", onBack, UiTheme.Secondary, UiTheme.SmallSize, 90f, 200f);
            }
            Text label = UiFactory.CreateLabel(row, title, UiTheme.HeadingSize, UiTheme.Text, TextAnchor.MiddleLeft, FontStyle.Bold);
            UiFactory.SetLayout(label.gameObject, flexibleWidth: 1f);
            return row;
        }

        /// <summary>The padded vertical column most screens put their content in.</summary>
        protected RectTransform CreateContentColumn(RectTransform root, float spacing = 20f, int padding = 40)
        {
            RectTransform column = UiFactory.CreateColumn(root, "Column", spacing, padding);
            UiFactory.Stretch(column);
            return column;
        }
    }
}
