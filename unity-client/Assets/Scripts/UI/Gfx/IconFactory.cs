using System;
using UnityEngine;

namespace BlastScale.Client.UI.Gfx
{
    /// <summary>
    /// Draws the handful of icons the game needs (coin, heart, star, rocket, trophy, boosters,
    /// navigation glyphs) into small textures at runtime. Every icon is a function from a point in
    /// [-1, 1]² to a colour; the rasteriser supersamples 4x4 per pixel for smooth edges. Coloured
    /// icons (coin, heart...) bake their palette; glyph icons are white so they can be tinted.
    /// </summary>
    public static class IconFactory
    {
        private const int Size = 128;
        private const int Supersample = 4;

        // ------------------------------------------------------------------ coloured icons

        /// <summary>Golden coin: disc, darker rim, lighter centre and a top-left highlight.</summary>
        public static Sprite Coin()
        {
            return Raster("icon_coin", (x, y) =>
            {
                float r = Mathf.Sqrt(x * x + y * y);
                if (r > 0.94f) return Color.clear;
                Color gold = Hex("#FFC93C");
                Color rim = Hex("#E0961E");
                Color deep = Hex("#F5B52A");
                Color c = r > 0.80f ? rim : r > 0.62f ? gold : deep;
                // highlight ellipse top-left
                float hx = (x + 0.30f) / 0.30f;
                float hy = (y - 0.32f) / 0.18f;
                if (hx * hx + hy * hy < 1f) c = Color.Lerp(c, Color.white, 0.55f);
                return c;
            });
        }

        /// <summary>Red heart with a soft top highlight; the shape is the classic implicit heart curve.</summary>
        public static Sprite Heart()
        {
            return Raster("icon_heart", (x, y) =>
            {
                float px = x * 1.25f;
                float py = y * 1.25f + 0.1f;
                if (!InsideHeart(px, py)) return Color.clear;
                Color fill = Hex("#FF4F79");
                Color edge = Hex("#D6265A");
                Color c = InsideHeart(px / 0.86f, py / 0.86f) ? fill : edge;
                float hx = (x + 0.36f) / 0.22f;
                float hy = (y - 0.35f) / 0.14f;
                if (hx * hx + hy * hy < 1f) c = Color.Lerp(c, Color.white, 0.5f);
                return Color.Lerp(c, Hex("#FF8AA6"), Mathf.Clamp01(y * 0.35f));
            });
        }

        /// <summary>Simple rocket pointing up: white body, red nose and fins, sky window.</summary>
        public static Sprite Rocket()
        {
            return Raster("icon_rocket", (x, y) =>
            {
                Color body = Hex("#F4F6FF");
                Color red = Hex("#FF5A5F");
                Color window = Hex("#38B6FF");
                Color flame = Hex("#FFB000");
                // fins
                if (y < -0.25f && y > -0.85f && Mathf.Abs(x) > 0.25f && Mathf.Abs(x) < 0.25f + (-0.25f - y) * 0.75f) return red;
                // flame
                if (y < -0.72f && Mathf.Abs(x) < 0.22f * (1f - (-0.72f - y) / 0.3f)) return flame;
                // body: rounded capsule from y=-0.75 to 0.85
                float bodyHalfWidth = 0.32f;
                if (Mathf.Abs(x) <= bodyHalfWidth && y >= -0.75f && y <= 0.35f)
                {
                    float wx = x / 0.16f;
                    float wy = (y + 0.05f) / 0.16f;
                    if (wx * wx + wy * wy < 1f) return window;
                    if (wx * wx + wy * wy < 1.6f) return Hex("#3A4160");
                    return body;
                }
                // nose cone
                if (y > 0.35f && y <= 0.95f && Mathf.Abs(x) <= bodyHalfWidth * (1f - (y - 0.35f) / 0.6f)) return red;
                return Color.clear;
            });
        }

        /// <summary>Golden trophy cup with handles and a base.</summary>
        public static Sprite Trophy()
        {
            return Raster("icon_trophy", (x, y) =>
            {
                Color gold = Hex("#FFC93C");
                Color dark = Hex("#E0961E");
                // base
                if (y < -0.62f && y > -0.9f && Mathf.Abs(x) < 0.45f) return dark;
                if (y < -0.35f && y >= -0.62f && Mathf.Abs(x) < 0.12f) return dark;
                // cup: rounded bowl from y=-0.35 to 0.75, narrowing at the bottom
                if (y >= -0.35f && y <= 0.75f)
                {
                    float t = (y + 0.35f) / 1.1f;
                    float halfWidth = Mathf.Lerp(0.18f, 0.55f, Mathf.Sqrt(t));
                    if (Mathf.Abs(x) <= halfWidth)
                    {
                        float hx = (x + 0.18f) / 0.12f;
                        return hx * hx < 1f && y > -0.1f && y < 0.6f ? Color.Lerp(gold, Color.white, 0.5f) : gold;
                    }
                    // handles
                    float hxc = Mathf.Abs(x) - 0.62f;
                    float hyc = y - 0.35f;
                    float rr = Mathf.Sqrt(hxc * hxc + hyc * hyc);
                    if (rr < 0.28f && rr > 0.16f && y > 0.05f) return dark;
                }
                return Color.clear;
            });
        }

        /// <summary>Booster: a hammer with a grey head and a wooden handle, tilted.</summary>
        public static Sprite Hammer()
        {
            return Raster("icon_hammer", (x, y) =>
            {
                // rotate 40 degrees so the tool looks dynamic
                float a = -40f * Mathf.Deg2Rad;
                float rx = x * Mathf.Cos(a) - y * Mathf.Sin(a);
                float ry = x * Mathf.Sin(a) + y * Mathf.Cos(a);
                Color steel = Hex("#C9D1E4");
                Color steelDark = Hex("#8E99B8");
                Color wood = Hex("#C98A4B");
                if (Mathf.Abs(rx) < 0.16f && ry < 0.35f && ry > -0.95f) return wood;
                if (Mathf.Abs(rx) < 0.62f && ry >= 0.30f && ry < 0.72f)
                {
                    return ry > 0.62f ? Color.Lerp(steel, Color.white, 0.4f) : rx > 0.4f ? steelDark : steel;
                }
                return Color.clear;
            });
        }

        /// <summary>Booster: two crossing curved arrows (shuffle), white for tinting.</summary>
        public static Sprite Shuffle()
        {
            return Raster("icon_shuffle", (x, y) =>
            {
                Color w = Color.white;
                // two diagonals as thick lines
                float d1 = Mathf.Abs(y - x * 0.55f);
                float d2 = Mathf.Abs(y + x * 0.55f);
                bool inLine = (d1 < 0.13f || d2 < 0.13f) && Mathf.Abs(x) < 0.55f;
                // arrow heads on the right ends
                bool head1 = x > 0.45f && x < 0.9f && Mathf.Abs(y - 0.35f) < (0.9f - x) * 0.9f;
                bool head2 = x > 0.45f && x < 0.9f && Mathf.Abs(y + 0.35f) < (0.9f - x) * 0.9f;
                return inLine || head1 || head2 ? w : Color.clear;
            });
        }

        /// <summary>Booster: a lightning bolt (extra moves), white for tinting.</summary>
        public static Sprite Bolt()
        {
            return Raster("icon_bolt", (x, y) =>
            {
                // Upper stroke and lower stroke of a bolt as two sheared quads.
                bool upper = y > 0f && y < 0.95f && x > -0.35f + (y - 0.45f) * 0.55f && x < 0.30f + (y - 0.45f) * 0.55f;
                bool lower = y <= 0f && y > -0.95f && x > -0.30f + (y + 0.45f) * 0.55f && x < 0.35f + (y + 0.45f) * 0.55f;
                return upper || lower ? Color.white : Color.clear;
            });
        }

        /// <summary>Daily reward: a gift box with a ribbon.</summary>
        public static Sprite Gift()
        {
            return Raster("icon_gift", (x, y) =>
            {
                Color box = Hex("#FF6BC6");
                Color lid = Hex("#FF8FD4");
                Color ribbon = Hex("#FFE066");
                if (y > 0.2f && y < 0.55f && Mathf.Abs(x) < 0.85f) return Mathf.Abs(x) < 0.12f ? ribbon : lid;
                if (y <= 0.2f && y > -0.8f && Mathf.Abs(x) < 0.7f) return Mathf.Abs(x) < 0.12f ? ribbon : box;
                // bow loops
                float lx = Mathf.Abs(x) - 0.26f;
                float ly = y - 0.72f;
                if (lx * lx / 0.06f + ly * ly / 0.03f < 1f) return ribbon;
                return Color.clear;
            });
        }

        /// <summary>Shop: a shopping bag with handles.</summary>
        public static Sprite Bag()
        {
            return Raster("icon_bag", (x, y) =>
            {
                Color bag = Hex("#38B6FF");
                Color handle = Hex("#DCEFFF");
                if (y < 0.35f && y > -0.85f && Mathf.Abs(x) < 0.68f)
                {
                    return y > 0.15f ? Color.Lerp(bag, Color.white, 0.25f) : bag;
                }
                float hx = x / 0.38f;
                float hy = (y - 0.35f) / 0.5f;
                float rr = hx * hx + hy * hy;
                if (y > 0.35f && rr < 1f && rr > 0.55f) return handle;
                return Color.clear;
            });
        }

        /// <summary>Events: a small flag on a pole.</summary>
        public static Sprite Flag()
        {
            return Raster("icon_flag", (x, y) =>
            {
                Color pole = Hex("#DCDFF0");
                Color flag = Hex("#8F6BFF");
                if (Mathf.Abs(x + 0.55f) < 0.08f && y > -0.9f && y < 0.9f) return pole;
                if (x > -0.47f && y > 0.1f && y < 0.85f)
                {
                    float wave = Mathf.Sin((y - 0.1f) * 6f) * 0.08f;
                    if (x < 0.75f + wave) return flag;
                }
                return Color.clear;
            });
        }

        // ------------------------------------------------------------------ white glyphs (tint with Image.color)

        /// <summary>Five-pointed star, slightly darker rim so it reads on any tint.</summary>
        public static Sprite Star()
        {
            return Raster("icon_star", (x, y) =>
            {
                if (!InsideStar(x, y, 0.95f)) return Color.clear;
                return InsideStar(x, y, 0.78f) ? Color.white : new Color(0.72f, 0.72f, 0.72f, 1f);
            });
        }

        public static Sprite Play()
        {
            return Raster("icon_play", (x, y) => x > -0.55f && x < 0.75f && Mathf.Abs(y) < (0.75f - x) * 0.72f ? Color.white : Color.clear);
        }

        public static Sprite Back()
        {
            return Raster("icon_back", (x, y) =>
            {
                float d = Mathf.Abs(Mathf.Abs(y) - (x + 0.3f)) ;
                return x > -0.5f && x < 0.45f && d < 0.16f ? Color.white : Color.clear;
            });
        }

        public static Sprite Close()
        {
            return Raster("icon_close", (x, y) =>
                (Mathf.Abs(x - y) < 0.16f || Mathf.Abs(x + y) < 0.16f) && Mathf.Abs(x) < 0.7f && Mathf.Abs(y) < 0.7f ? Color.white : Color.clear);
        }

        public static Sprite Check()
        {
            return Raster("icon_check", (x, y) =>
            {
                bool left = x > -0.75f && x < -0.15f && Mathf.Abs(y + 0.2f - (x + 0.15f) * -1f) < 0.16f;
                bool right = x >= -0.2f && x < 0.8f && Mathf.Abs(y + 0.25f - (x + 0.2f) * 1f) < 0.16f;
                return left || right ? Color.white : Color.clear;
            });
        }

        /// <summary>Music note (toggle button).</summary>
        public static Sprite Note()
        {
            return Raster("icon_note", (x, y) =>
            {
                float hx = (x + 0.25f) / 0.32f;
                float hy = (y + 0.55f) / 0.24f;
                if (hx * hx + hy * hy < 1f) return Color.white;
                if (Mathf.Abs(x - 0.05f) < 0.08f && y > -0.55f && y < 0.85f) return Color.white;
                if (x >= 0.05f && x < 0.6f && Mathf.Abs(y - 0.85f + (x - 0.05f) * 0.6f) < 0.1f) return Color.white;
                return Color.clear;
            });
        }

        /// <summary>Sound icon with waves (SFX toggle).</summary>
        public static Sprite SoundIcon()
        {
            return Raster("icon_sound", (x, y) =>
            {
                if (x > -0.85f && x < -0.45f && Mathf.Abs(y) < 0.3f) return Color.white;
                if (x >= -0.45f && x < 0.0f && Mathf.Abs(y) < 0.3f + (x + 0.45f) * 1.1f) return Color.white;
                float r = Mathf.Sqrt((x - 0.05f) * (x - 0.05f) + y * y);
                if (x > 0.1f && ((r > 0.42f && r < 0.54f) || (r > 0.7f && r < 0.82f)) && Mathf.Abs(y) < r * 0.75f) return Color.white;
                return Color.clear;
            });
        }

        /// <summary>A diagonal bar drawn over a toggle icon to show "off".</summary>
        public static Sprite Slash()
        {
            return Raster("icon_slash", (x, y) => Mathf.Abs(x + y) < 0.12f && Mathf.Abs(x) < 0.85f ? Color.white : Color.clear);
        }

        /// <summary>Small four-point sparkle for particles.</summary>
        public static Sprite Sparkle()
        {
            return Raster("icon_sparkle", (x, y) =>
            {
                float ax = Mathf.Abs(x), ay = Mathf.Abs(y);
                float v = Mathf.Sqrt(ax) + Mathf.Sqrt(ay);
                return v < 0.95f ? Color.white : Color.clear;
            });
        }

        // ------------------------------------------------------------------ rasteriser

        private static Sprite Raster(string key, Func<float, float, Color> shader)
        {
            if (SpriteFactory.TryGetCached(key, out Sprite cached)) return cached;
            var pixels = new Color[Size * Size];
            float inv = 1f / Supersample;
            for (int y = 0; y < Size; y++)
            {
                for (int x = 0; x < Size; x++)
                {
                    float r = 0f, g = 0f, b = 0f, a = 0f;
                    for (int sy = 0; sy < Supersample; sy++)
                    {
                        for (int sx = 0; sx < Supersample; sx++)
                        {
                            float px = ((x + (sx + 0.5f) * inv) / Size) * 2f - 1f;
                            float py = ((y + (sy + 0.5f) * inv) / Size) * 2f - 1f;
                            Color c = shader(px, py);
                            r += c.r * c.a;
                            g += c.g * c.a;
                            b += c.b * c.a;
                            a += c.a;
                        }
                    }
                    if (a > 0f)
                    {
                        // Premultiplied average keeps edges from picking up the colour of transparent samples.
                        pixels[y * Size + x] = new Color(r / a, g / a, b / a, a / (Supersample * Supersample));
                    }
                    else
                    {
                        pixels[y * Size + x] = Color.clear;
                    }
                }
            }
            return SpriteFactory.Make(pixels, Size, Size, key, Vector4.zero);
        }

        private static bool InsideHeart(float x, float y)
        {
            float a = x * x + y * y - 1f;
            return a * a * a - x * x * y * y * y <= 0f;
        }

        /// <summary>Point-in-star test via the polar radius of a 5-point star with the given outer radius.</summary>
        private static bool InsideStar(float x, float y, float outer)
        {
            float r = Mathf.Sqrt(x * x + y * y);
            if (r < 0.0001f) return true;
            float angle = Mathf.Atan2(y, x) - Mathf.PI / 2f; // point up
            float sector = 2f * Mathf.PI / 5f;
            float a = Mathf.Repeat(angle, sector);
            a = Mathf.Abs(a - sector / 2f); // 0 at the star tip, sector/2 at the valley
            float inner = outer * 0.5f;
            // Linear edge between the tip (radius outer at a = sector/2) and the valley (inner at a = 0).
            float t = a / (sector / 2f);
            float edgeRadius = 1f / Mathf.Lerp(1f / inner, 1f / outer, t);
            return r <= edgeRadius;
        }

        private static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color c) ? c : Color.magenta;
        }
    }
}
