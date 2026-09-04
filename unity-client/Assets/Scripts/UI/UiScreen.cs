using System.Collections;
using BlastScale.Client.Core;
using BlastScale.Client.UI.Gfx;
using UnityEngine;
using UnityEngine.UI;

namespace BlastScale.Client.UI
{
    /// <summary>How a screen enters and how the previous one leaves.</summary>
    public enum ScreenTransition
    {
        /// <summary>Slides in from the right while the old screen slides out to the left (default).</summary>
        Slide,

        /// <summary>Cross-fades (used by the result screen, which appears over the finished board).</summary>
        Fade,

        /// <summary>Immediate swap.</summary>
        None
    }

    /// <summary>
    /// Base class of every screen. A screen is a plain object that builds its UGUI hierarchy under
    /// a root rect (with a CanvasGroup for transitions) when shown and destroys it when hidden.
    /// Coroutines started through <see cref="Run"/> are guarded: once the screen is dismissed they
    /// stop at the next yield, so a slow network reply can never touch a destroyed hierarchy.
    /// </summary>
    public abstract class UiScreen
    {
        private bool _alive;

        /// <summary>Shared services (state, API, navigation, toast...).</summary>
        protected AppContext App { get; private set; }

        /// <summary>Root rect of the screen; null once destroyed.</summary>
        public RectTransform Root { get; private set; }

        /// <summary>Drives the fade of the whole screen during transitions.</summary>
        public CanvasGroup Group { get; private set; }

        /// <summary>True while the screen is the live one (built and not dismissed).</summary>
        protected bool IsAlive => _alive && Root != null;

        /// <summary>Same as <see cref="IsAlive"/> for callers outside the screen (manager, tests).</summary>
        public bool Alive => IsAlive;

        /// <summary>Which animation the ScreenManager uses when this screen appears.</summary>
        public virtual ScreenTransition Transition => ScreenTransition.Slide;

        /// <summary>Builds the hierarchy and starts the screen's work.</summary>
        public void Show(AppContext app, RectTransform parent)
        {
            App = app;
            Root = UiFactory.CreateRect(parent, GetType().Name);
            UiFactory.Stretch(Root);
            Group = Root.gameObject.AddComponent<CanvasGroup>();
            _alive = true;
            Build(Root);
            OnShown();
        }

        /// <summary>
        /// Marks the screen as leaving: guarded coroutines stop and input is ignored, but the
        /// hierarchy stays for the exit animation until <see cref="Destroy"/>.
        /// </summary>
        public void Dismiss()
        {
            if (!_alive) return;
            _alive = false;
            if (Group != null)
            {
                Group.interactable = false;
                Group.blocksRaycasts = false;
            }
            OnDismissed();
        }

        /// <summary>Tears the hierarchy down.</summary>
        public void Destroy()
        {
            _alive = false;
            if (Root != null)
            {
                Object.Destroy(Root.gameObject);
                Root = null;
            }
        }

        /// <summary>Dismiss and destroy at once (no exit animation).</summary>
        public void Hide()
        {
            Dismiss();
            Destroy();
        }

        /// <summary>Creates the widgets under <paramref name="root"/>.</summary>
        protected abstract void Build(RectTransform root);

        /// <summary>Called after <see cref="Build"/>; the place to start loading data and entrance animations.</summary>
        protected virtual void OnShown()
        {
        }

        /// <summary>Called when the screen starts leaving (stop pulses, timers...).</summary>
        protected virtual void OnDismissed()
        {
        }

        /// <summary>Starts a coroutine that is abandoned automatically when the screen is dismissed.</summary>
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

        /// <summary>Standard header row: a round back button on the left and the display-font title next to it.</summary>
        protected RectTransform CreateHeader(Transform parent, string title, UnityEngine.Events.UnityAction onBack)
        {
            RectTransform row = UiFactory.CreateRow(parent, "Header", UiTheme.IconButtonSize, 20f, TextAnchor.MiddleLeft);
            if (onBack != null)
            {
                UiFactory.CreateIconButton(row, "Back", IconFactory.Back(), onBack, ButtonStyle.Ghost);
            }
            Text label = UiFactory.CreateTitle(row, title, UiTheme.HeadingSize + 6, UiTheme.Text, TextAnchor.MiddleLeft);
            UiFactory.SetLayout(label.gameObject, flexibleWidth: 1f);
            return row;
        }

        /// <summary>The padded vertical column most screens put their content in.</summary>
        protected RectTransform CreateContentColumn(RectTransform root, float spacing = 20f, int padding = 40)
        {
            return CreateContentColumn(root, spacing, padding, padding, padding);
        }

        protected RectTransform CreateContentColumn(RectTransform root, float spacing, int horizontalPadding, int topPadding, int bottomPadding)
        {
            RectTransform column = UiFactory.CreateRect(root, "Column");
            UiFactory.AddVerticalLayout(column, spacing, new RectOffset(horizontalPadding, horizontalPadding, topPadding, bottomPadding), TextAnchor.UpperCenter);
            UiFactory.Stretch(column);
            return column;
        }
    }
}
