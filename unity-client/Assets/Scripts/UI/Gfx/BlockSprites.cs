using System.Collections.Generic;
using UnityEngine;

namespace BlastScale.Client.UI.Gfx
{
    /// <summary>
    /// The board's block art: one texture per colour index with a rounded square, a vertical
    /// gradient (lighter top), a glossy highlight ellipse, a two pixel darker outline and a soft
    /// drop shadow baked into the transparent margin. Generated once per colour and cached.
    /// </summary>
    public static class BlockSprites
    {
        /// <summary>Texture size; the visible square is <see cref="Inner"/> pixels, the rest is shadow margin.</summary>
        public const int Size = 160;

        /// <summary>Side of the visible rounded square inside the texture.</summary>
        public const int Inner = 128;

        /// <summary>How much larger than the cell the Image must be so the square matches the cell.</summary>
        public const float ImageScale = Size / (float)Inner;

        private const float Radius = 30f;
        private const float OutlineWidth = 2.5f;
        private const float ShadowBlur = 9f;
        private const float ShadowOffsetY = -7f;

        private static readonly Dictionary<int, Sprite> Cache = new Dictionary<int, Sprite>();
        private static Sprite _slot;

        /// <summary>The sprite for a colour index (see <see cref="UiTheme.BlockColor"/>).</summary>
        public static Sprite Get(int colorIndex)
        {
            if (Cache.TryGetValue(colorIndex, out Sprite cached)) return cached;
            Sprite sprite = Build(UiTheme.BlockColor(colorIndex), "block_" + colorIndex);
            Cache[colorIndex] = sprite;
            return sprite;
        }

        /// <summary>Faint rounded square drawn behind every cell (the grid pattern of the board).</summary>
        public static Sprite Slot()
        {
            if (_slot != null) return _slot;
            _slot = SpriteFactory.RoundedRect(Radius * 0.8f);
            return _slot;
        }

        private static Sprite Build(Color baseColor, string key)
        {
            var pixels = new Color[Size * Size];
            float half = Inner * 0.5f;
            float centre = Size * 0.5f;

            Color top = Color.Lerp(baseColor, Color.white, 0.32f);
            Color bottom = Color.Lerp(baseColor, Color.black, 0.12f);
            Color outline = Color.Lerp(baseColor, Color.black, 0.42f);

            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float px = x + 0.5f - centre;
                    float py = y + 0.5f - centre;

                    // Shadow first (offset downwards; y grows upwards in texture space).
                    float ds = SpriteFactory.RoundedBoxDistance(px, py - ShadowOffsetY, half, half, Radius);
                    float shadowAlpha = ds <= 0f ? 1f : Mathf.Exp(-(ds * ds) / (ShadowBlur * ShadowBlur));
                    Color c = new Color(0f, 0f, 0f, shadowAlpha * 0.42f);

                    float d = SpriteFactory.RoundedBoxDistance(px, py, half, half, Radius);
                    float body = SpriteFactory.Coverage(d);
                    if (body > 0f)
                    {
                        float t = (py + half) / Inner; // 0 bottom, 1 top
                        Color fill = Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, t));
                        // glossy highlight ellipse near the top
                        float hx = px / (half * 0.62f);
                        float hy = (py - half * 0.52f) / (half * 0.26f);
                        float ellipse = hx * hx + hy * hy;
                        if (ellipse < 1f)
                        {
                            float g = (1f - ellipse) * 0.45f;
                            fill = Color.Lerp(fill, Color.white, g);
                        }
                        // rim light at the very top edge and darker outline
                        float rim = Mathf.Clamp01(1f - (-d) / 4f);
                        fill = Color.Lerp(fill, Color.white, rim * 0.18f * Mathf.Clamp01(t));
                        float outlineMix = SpriteFactory.Coverage(d + OutlineWidth); // 1 inside the inner edge
                        Color withOutline = Color.Lerp(outline, fill, outlineMix);
                        c = Blend(c, new Color(withOutline.r, withOutline.g, withOutline.b, body));
                    }
                    pixels[y * Size + x] = c;
                }
            }
            return SpriteFactory.Make(pixels, Size, Size, key, Vector4.zero);
        }

        /// <summary>Standard "over" alpha blending of <paramref name="top"/> onto <paramref name="under"/>.</summary>
        private static Color Blend(Color under, Color top)
        {
            float a = top.a + under.a * (1f - top.a);
            if (a <= 0f) return Color.clear;
            float r = (top.r * top.a + under.r * under.a * (1f - top.a)) / a;
            float g = (top.g * top.a + under.g * under.a * (1f - top.a)) / a;
            float b = (top.b * top.a + under.b * under.a * (1f - top.a)) / a;
            return new Color(r, g, b, a);
        }
    }
}
