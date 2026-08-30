using BlastScale.Client.Audio;
using BlastScale.Client.Core;
using BlastScale.Client.UI.Fx;
using UnityEngine;

namespace BlastScale.Client.UI
{
    /// <summary>
    /// Shows exactly one screen at a time inside the screen layer of the canvas, with an animated
    /// hand-over (slide or fade) between the old and the new one. There is no back-stack on
    /// purpose: every screen knows where "back" leads, which keeps navigation explicit.
    /// </summary>
    public sealed class ScreenManager
    {
        private const float SlideSeconds = 0.28f;
        private const float FadeSeconds = 0.22f;

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

        /// <summary>Dismisses the current screen (animated out) and shows the new one (animated in).</summary>
        public void Show(UiScreen screen)
        {
            UiScreen old = _current;
            _current = screen;
            screen.Show(_app, _layer);

            if (old != null)
            {
                old.Dismiss();
            }
            ScreenTransition transition = screen.Transition;
            float width = Mathf.Max(_layer.rect.width, UiFactory.ReferenceWidth);

            switch (transition)
            {
                case ScreenTransition.Slide:
                {
                    screen.Root.anchoredPosition = new Vector2(old != null ? width : width * 0.25f, 0f);
                    screen.Group.alpha = 0f;
                    Tween.Move(screen.Root, Vector2.zero, SlideSeconds, Ease.OutCubic);
                    Tween.Fade(screen.Group, 1f, SlideSeconds * 0.8f, Ease.OutQuad);
                    if (old != null)
                    {
                        AudioManager.Play(Sfx.Whoosh, 1f, 0.5f);
                        Tween.Move(old.Root, new Vector2(-width * 0.35f, 0f), SlideSeconds, Ease.OutCubic);
                        Tween.Fade(old.Group, 0f, SlideSeconds * 0.9f, Ease.InQuad, 0f, old.Destroy);
                    }
                    break;
                }
                case ScreenTransition.Fade:
                {
                    screen.Group.alpha = 0f;
                    Tween.Fade(screen.Group, 1f, FadeSeconds, Ease.OutQuad);
                    if (old != null)
                    {
                        Tween.Fade(old.Group, 0f, FadeSeconds, Ease.InQuad, 0f, old.Destroy);
                    }
                    break;
                }
                default:
                    old?.Destroy();
                    break;
            }
        }
    }
}
