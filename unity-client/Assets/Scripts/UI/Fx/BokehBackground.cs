using BlastScale.Client.UI.Gfx;
using UnityEngine;
using UnityEngine.UI;

namespace BlastScale.Client.UI.Fx
{
    /// <summary>
    /// The living backdrop behind every screen: a vertical navy to purple gradient with a dozen
    /// large, very transparent bokeh discs that drift slowly, wobble and breathe. Cheap (a handful
    /// of images moved per frame) and it makes even the login screen feel alive.
    /// </summary>
    public sealed class BokehBackground : MonoBehaviour
    {
        private struct Disc
        {
            public RectTransform Rect;
            public Image Image;
            public Vector2 Position;
            public Vector2 Velocity;
            public float Phase;
            public float BaseAlpha;
            public float Size;
        }

        private const int DiscCount = 14;

        private RectTransform _layer;
        private Disc[] _discs;

        /// <summary>Builds the gradient and the discs under <paramref name="layer"/> (stretched to the screen).</summary>
        public static BokehBackground Create(RectTransform layer)
        {
            Image gradient = UiFactory.CreateImage(layer, "Gradient", SpriteFactory.VerticalGradient(UiTheme.BackgroundTop, UiTheme.BackgroundBottom), Color.white);
            UiFactory.Stretch(gradient.rectTransform);

            // A faint radial glow near the top centre gives the gradient some depth.
            Image glow = UiFactory.CreateImage(layer, "Glow", SpriteFactory.SoftCircle(128, 0.1f), new Color(0.55f, 0.45f, 1f, 0.16f));
            glow.rectTransform.anchorMin = glow.rectTransform.anchorMax = new Vector2(0.5f, 0.85f);
            glow.rectTransform.sizeDelta = new Vector2(1500f, 1500f);

            var background = layer.gameObject.AddComponent<BokehBackground>();
            background._layer = layer;
            background.BuildDiscs();
            return background;
        }

        private void BuildDiscs()
        {
            Sprite sprite = SpriteFactory.SoftCircle(128, 0.3f);
            Color[] tints =
            {
                new Color(1f, 1f, 1f), UiTheme.Violet, UiTheme.Pink, UiTheme.Sky, new Color(1f, 1f, 1f), UiTheme.Violet
            };
            _discs = new Disc[DiscCount];
            for (int i = 0; i < DiscCount; i++)
            {
                Image image = UiFactory.CreateImage(_layer, "Bokeh " + i, sprite, Color.white);
                RectTransform rect = image.rectTransform;
                rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 0.5f);
                float size = Random.Range(140f, 460f);
                rect.sizeDelta = new Vector2(size, size);
                Color tint = tints[i % tints.Length];
                float alpha = Random.Range(0.035f, 0.10f);
                _discs[i] = new Disc
                {
                    Rect = rect,
                    Image = image,
                    Position = new Vector2(Random.Range(-600f, 600f), Random.Range(-1000f, 1000f)),
                    Velocity = new Vector2(Random.Range(-14f, 14f), Random.Range(8f, 26f)),
                    Phase = Random.Range(0f, 10f),
                    BaseAlpha = alpha,
                    Size = size
                };
                image.color = new Color(tint.r, tint.g, tint.b, alpha);
                rect.anchoredPosition = _discs[i].Position;
            }
        }

        private void Update()
        {
            if (_discs == null) return;
            float dt = Time.unscaledDeltaTime;
            float time = Time.unscaledTime;
            Rect bounds = _layer.rect;
            float halfW = Mathf.Max(bounds.width, 1080f) * 0.5f + 300f;
            float halfH = Mathf.Max(bounds.height, 1920f) * 0.5f + 300f;
            for (int i = 0; i < _discs.Length; i++)
            {
                Disc d = _discs[i];
                d.Position += d.Velocity * dt;
                // wrap around so the drift never ends
                if (d.Position.y > halfH) d.Position.y = -halfH;
                if (d.Position.x > halfW) d.Position.x = -halfW;
                if (d.Position.x < -halfW) d.Position.x = halfW;
                float wobbleX = Mathf.Sin(time * 0.35f + d.Phase) * 22f;
                float wobbleY = Mathf.Cos(time * 0.27f + d.Phase * 1.3f) * 16f;
                d.Rect.anchoredPosition = d.Position + new Vector2(wobbleX, wobbleY);
                float breathe = 0.75f + 0.25f * Mathf.Sin(time * 0.6f + d.Phase * 2f);
                Color c = d.Image.color;
                d.Image.color = new Color(c.r, c.g, c.b, d.BaseAlpha * breathe);
                float s = 1f + 0.06f * Mathf.Sin(time * 0.45f + d.Phase);
                d.Rect.localScale = new Vector3(s, s, 1f);
                _discs[i] = d;
            }
        }
    }
}
