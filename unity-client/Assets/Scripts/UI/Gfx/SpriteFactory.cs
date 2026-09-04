using System.Collections.Generic;
using UnityEngine;

namespace BlastScale.Client.UI.Gfx
{
    /// <summary>
    /// Generates every sprite the UI needs at runtime from signed-distance functions: rounded
    /// rectangles (9-sliced), soft drop shadows, inner shadows, circles, glow discs, spinner arcs
    /// and gradients. Nothing is loaded from disk, so the project stays a pure-code repository;
    /// results are cached by their parameters because every texture is built once and shared.
    /// </summary>
    public static class SpriteFactory
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>UI sprites use 100 pixels per unit so one texture pixel equals one canvas unit.</summary>
        private const float PixelsPerUnit = 100f;

        // ------------------------------------------------------------------ rounded rectangles

        /// <summary>White 9-sliced rounded rectangle with anti-aliased corners; tint it with Image.color.</summary>
        public static Sprite RoundedRect(float radius)
        {
            string key = "rr" + radius;
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            int r = Mathf.CeilToInt(radius);
            int size = r * 2 + 6;
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedBoxDistance(x + 0.5f - half, y + 0.5f - half, half, half, radius);
                    pixels[y * size + x] = White(Coverage(d));
                }
            }
            Sprite sprite = Make(pixels, size, size, key, new Vector4(r + 2, r + 2, r + 2, r + 2));
            return sprite;
        }

        /// <summary>White 9-sliced rounded outline of the given thickness (card borders, focus rings).</summary>
        public static Sprite RoundedOutline(float radius, float thickness)
        {
            string key = "ro" + radius + "_" + thickness;
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            int r = Mathf.CeilToInt(radius);
            int size = r * 2 + 6;
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedBoxDistance(x + 0.5f - half, y + 0.5f - half, half, half, radius);
                    float outer = Coverage(d);
                    float inner = Coverage(d + thickness);
                    pixels[y * size + x] = White(Mathf.Clamp01(outer - inner));
                }
            }
            return Make(pixels, size, size, key, new Vector4(r + 2, r + 2, r + 2, r + 2));
        }

        /// <summary>
        /// Gloss overlay for buttons: a light band fading down from the top edge and a darker band
        /// rising from the bottom edge, baked into one sprite (white pixels on top, black at the
        /// bottom). Used untinted above a coloured body it gives the "candy" bevel look.
        /// </summary>
        public static Sprite Bevel(float radius)
        {
            string key = "bv" + radius;
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            int r = Mathf.CeilToInt(radius);
            int size = r * 2 + 6;
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            float border = r + 2;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedBoxDistance(x + 0.5f - half, y + 0.5f - half, half, half, radius);
                    float shape = Coverage(d);
                    // Texture rows go bottom-up: high y is the top of the sprite.
                    float fromTop = size - (y + 0.5f);
                    float fromBottom = y + 0.5f;
                    float gloss = Mathf.Clamp01(1f - fromTop / border) * 0.32f;
                    float shade = Mathf.Clamp01(1f - fromBottom / (border * 0.8f)) * 0.22f;
                    Color c = gloss >= shade
                        ? new Color(1f, 1f, 1f, gloss * shape)
                        : new Color(0f, 0f, 0f, shade * shape);
                    pixels[y * size + x] = c;
                }
            }
            return Make(pixels, size, size, key, new Vector4(border, border, border, border));
        }

        /// <summary>
        /// Soft drop shadow (9-sliced): a rounded rectangle whose alpha falls off like a gaussian
        /// outside the edge. Tint it black with the wanted opacity; place it slightly larger than
        /// and below the element it belongs to.
        /// </summary>
        public static Sprite Shadow(float radius, float blur)
        {
            string key = "sh" + radius + "_" + blur;
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            int r = Mathf.CeilToInt(radius);
            int b = Mathf.CeilToInt(blur * 2f);
            int size = (r + b) * 2 + 6;
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            float inner = half - b;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedBoxDistance(x + 0.5f - half, y + 0.5f - half, inner, inner, radius);
                    float alpha = d <= 0f ? 1f : Mathf.Exp(-(d * d) / (blur * blur));
                    pixels[y * size + x] = White(alpha);
                }
            }
            float border = r + b + 2;
            return Make(pixels, size, size, key, new Vector4(border, border, border, border));
        }

        /// <summary>Inner shadow (9-sliced): opaque at the edge fading to nothing inside; darkens a card's rim.</summary>
        public static Sprite InnerShadow(float radius, float blur)
        {
            string key = "is" + radius + "_" + blur;
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            int r = Mathf.CeilToInt(radius);
            int b = Mathf.CeilToInt(blur);
            int size = (r + b) * 2 + 6;
            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = RoundedBoxDistance(x + 0.5f - half, y + 0.5f - half, half, half, radius);
                    float shape = Coverage(d);
                    float depth = Mathf.Clamp01(-d / blur); // 0 at the edge, 1 deep inside
                    float alpha = (1f - depth) * (1f - depth) * shape;
                    pixels[y * size + x] = White(alpha);
                }
            }
            float border = r + b + 2;
            return Make(pixels, size, size, key, new Vector4(border, border, border, border));
        }

        // ------------------------------------------------------------------ circles

        /// <summary>Anti-aliased white disc.</summary>
        public static Sprite Circle(int size = 64)
        {
            string key = "ci" + size;
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            float radius = half - 1f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - half;
                    float dy = y + 0.5f - half;
                    float d = Mathf.Sqrt(dx * dx + dy * dy) - radius;
                    pixels[y * size + x] = White(Coverage(d));
                }
            }
            return Make(pixels, size, size, key, Vector4.zero);
        }

        /// <summary>Disc with a radial alpha falloff (bokeh circles, glows, particle sparks).</summary>
        public static Sprite SoftCircle(int size = 128, float hardness = 0.35f)
        {
            string key = "sc" + size + "_" + hardness;
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - half;
                    float dy = y + 0.5f - half;
                    float t = Mathf.Sqrt(dx * dx + dy * dy) / half;
                    float alpha = t <= hardness ? 1f : Mathf.Clamp01(1f - (t - hardness) / (1f - hardness));
                    alpha *= alpha;
                    pixels[y * size + x] = White(alpha);
                }
            }
            return Make(pixels, size, size, key, Vector4.zero);
        }

        /// <summary>A ring arc that fades along its length (a "comet") for the loading spinner.</summary>
        public static Sprite SpinnerArc(int size = 160, float thickness = 0.14f, float arc = 0.8f)
        {
            string key = "sp" + size + "_" + thickness + "_" + arc;
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            var pixels = new Color32[size * size];
            float half = size * 0.5f;
            float outer = half - 2f;
            float inner = outer - size * thickness;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x + 0.5f - half;
                    float dy = y + 0.5f - half;
                    float dist = Mathf.Sqrt(dx * dx + dy * dy);
                    float ring = Mathf.Min(Coverage(dist - outer), Coverage(inner - dist));
                    float angle = (Mathf.Atan2(dy, dx) / (2f * Mathf.PI) + 1f) % 1f; // 0..1 around
                    float alongArc = angle / arc;
                    float fade = alongArc <= 1f ? alongArc * alongArc : 0f;
                    pixels[y * size + x] = White(ring * fade);
                }
            }
            return Make(pixels, size, size, key, Vector4.zero);
        }

        // ------------------------------------------------------------------ gradients

        /// <summary>A 4 x height vertical gradient; stretch it over the whole screen for the background.</summary>
        public static Sprite VerticalGradient(Color top, Color bottom, int height = 256)
        {
            string key = "vg" + ColorUtility.ToHtmlStringRGBA(top) + ColorUtility.ToHtmlStringRGBA(bottom) + height;
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;

            const int width = 4;
            var pixels = new Color32[width * height];
            for (int y = 0; y < height; y++)
            {
                float t = y / (float)(height - 1); // row 0 is the bottom of the texture
                Color c = Color.Lerp(bottom, top, Mathf.SmoothStep(0f, 1f, t));
                for (int x = 0; x < width; x++)
                {
                    pixels[y * width + x] = c;
                }
            }
            return Make(pixels, width, height, key, Vector4.zero);
        }

        /// <summary>A plain 4x4 white square (confetti pieces, progress fills without rounding).</summary>
        public static Sprite Square()
        {
            const string key = "sq";
            if (Cache.TryGetValue(key, out Sprite cached)) return cached;
            var pixels = new Color32[16];
            for (int i = 0; i < 16; i++) pixels[i] = new Color32(255, 255, 255, 255);
            return Make(pixels, 4, 4, key, Vector4.zero);
        }

        // ------------------------------------------------------------------ math helpers (shared with the other factories)

        /// <summary>Signed distance from a point to a rounded box centred at the origin (negative inside).</summary>
        public static float RoundedBoxDistance(float px, float py, float halfWidth, float halfHeight, float radius)
        {
            float qx = Mathf.Abs(px) - halfWidth + radius;
            float qy = Mathf.Abs(py) - halfHeight + radius;
            float outside = Mathf.Sqrt(Mathf.Max(qx, 0f) * Mathf.Max(qx, 0f) + Mathf.Max(qy, 0f) * Mathf.Max(qy, 0f));
            float inside = Mathf.Min(Mathf.Max(qx, qy), 0f);
            return outside + inside - radius;
        }

        /// <summary>One-pixel anti-aliasing: full coverage half a pixel inside, none half a pixel outside.</summary>
        public static float Coverage(float signedDistance)
        {
            return Mathf.Clamp01(0.5f - signedDistance);
        }

        public static Color32 White(float alpha)
        {
            return new Color32(255, 255, 255, (byte)Mathf.RoundToInt(Mathf.Clamp01(alpha) * 255f));
        }

        /// <summary>Uploads the pixels and wraps them in a (possibly 9-sliced) sprite; caches by key.</summary>
        public static Sprite Make(Color32[] pixels, int width, int height, string key, Vector4 border)
        {
            var texture = new Texture2D(width, height, TextureFormat.RGBA32, false)
            {
                name = "gen_" + key,
                filterMode = FilterMode.Bilinear,
                wrapMode = TextureWrapMode.Clamp,
                hideFlags = HideFlags.HideAndDontSave
            };
            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            Sprite sprite = Sprite.Create(texture, new Rect(0f, 0f, width, height), new Vector2(0.5f, 0.5f), PixelsPerUnit, 0,
                SpriteMeshType.FullRect, border);
            sprite.name = key;
            sprite.hideFlags = HideFlags.HideAndDontSave;
            Cache[key] = sprite;
            return sprite;
        }

        /// <summary>Same as <see cref="Make(Color32[],int,int,string,Vector4)"/> for float colours.</summary>
        public static Sprite Make(Color[] pixels, int width, int height, string key, Vector4 border)
        {
            var converted = new Color32[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                converted[i] = pixels[i];
            }
            return Make(converted, width, height, key, border);
        }

        /// <summary>Looks up a cached sprite made by another factory.</summary>
        public static bool TryGetCached(string key, out Sprite sprite)
        {
            return Cache.TryGetValue(key, out sprite);
        }
    }
}
