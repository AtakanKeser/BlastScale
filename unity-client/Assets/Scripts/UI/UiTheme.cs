using UnityEngine;

namespace BlastScale.Client.UI
{
    /// <summary>
    /// The visual identity in one place: palette, font sizes, corner radii and the six block
    /// colours. Every sprite is generated at runtime, so these values are the whole art direction
    /// ("juicy casual" on a dark navy to purple gradient).
    /// </summary>
    public static class UiTheme
    {
        // ----- background gradient -----
        public static readonly Color BackgroundTop = Hex("#151B3B");
        public static readonly Color BackgroundBottom = Hex("#3B1D62");

        // ----- surfaces -----
        public static readonly Color CardFill = new Color(1f, 1f, 1f, 0.09f);
        public static readonly Color CardFillStrong = new Color(1f, 1f, 1f, 0.16f);
        public static readonly Color CardBorder = new Color(1f, 1f, 1f, 0.16f);
        public static readonly Color ShadowColor = new Color(0f, 0f, 0f, 0.38f);
        public static readonly Color InputFill = new Color(0f, 0f, 0f, 0.30f);
        public static readonly Color BoardFill = new Color(0f, 0f, 0f, 0.28f);
        public static readonly Color SlotFill = new Color(1f, 1f, 1f, 0.06f);
        public static readonly Color Scrim = new Color(0.03f, 0.02f, 0.08f, 0.72f);

        // ----- text -----
        public static readonly Color Text = Hex("#FFFFFF");
        public static readonly Color TextSoft = Hex("#D5D9F0");
        public static readonly Color Muted = Hex("#9AA2C8");
        public static readonly Color TextShadow = new Color(0f, 0f, 0f, 0.45f);

        // ----- accents -----
        public static readonly Color Primary = Hex("#2ED47A");   // green: play / confirm
        public static readonly Color Blue = Hex("#3D8BFF");      // secondary primary
        public static readonly Color Secondary = Hex("#525B8A"); // neutral grey-blue
        public static readonly Color Danger = Hex("#FF5A5F");
        public static readonly Color Gold = Hex("#FFC93C");
        public static readonly Color Amber = Hex("#FFB000");
        public static readonly Color Heart = Hex("#FF4F79");
        public static readonly Color Sky = Hex("#38B6FF");
        public static readonly Color Violet = Hex("#8F6BFF");
        public static readonly Color Pink = Hex("#FF6BC6");
        public static readonly Color Lime = Hex("#7ED957");
        public static readonly Color StarOff = new Color(1f, 1f, 1f, 0.22f);

        // ----- typography (reference units) -----
        public const int TitleSize = 88;
        public const int ScoreSize = 76;
        public const int HeadingSize = 48;
        public const int BodySize = 34;
        public const int SmallSize = 28;
        public const int TinySize = 24;

        // ----- metrics -----
        public const float ButtonHeight = 130f;
        public const float IconButtonSize = 112f;
        public const float ButtonRadius = 34f;
        public const float CardRadius = 40f;
        public const float PillRadius = 42f;
        public const float ShadowBlur = 14f;

        /// <summary>The six block colours; <c>colorCount</c> of a level picks the first N.</summary>
        private static readonly Color[] BlockColors =
        {
            Hex("#FF5A5F"), // 0 coral red
            Hex("#FFB000"), // 1 amber
            Hex("#7ED957"), // 2 lime
            Hex("#38B6FF"), // 3 sky
            Hex("#8F6BFF"), // 4 violet
            Hex("#FF6BC6")  // 5 pink
        };

        /// <summary>Colour of a board cell; -1 (empty) maps to a transparent colour.</summary>
        public static Color BlockColor(int colorIndex)
        {
            if (colorIndex < 0)
            {
                return Color.clear;
            }
            return BlockColors[colorIndex % BlockColors.Length];
        }

        public static int BlockColorCount => BlockColors.Length;

        public static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.magenta;
        }

        public static Color WithAlpha(Color color, float alpha)
        {
            return new Color(color.r, color.g, color.b, alpha);
        }

        public static Color Lighten(Color color, float amount)
        {
            return Color.Lerp(color, Color.white, amount);
        }

        public static Color Darken(Color color, float amount)
        {
            return Color.Lerp(color, Color.black, amount);
        }

        /// <summary>Grey version of a colour (disabled buttons keep their shape but lose their identity).</summary>
        public static Color Desaturate(Color color, float amount)
        {
            float grey = color.r * 0.3f + color.g * 0.59f + color.b * 0.11f;
            return Color.Lerp(color, new Color(grey, grey, grey, color.a), amount);
        }
    }
}
