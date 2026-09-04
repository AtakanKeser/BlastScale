using UnityEngine;

namespace BlastScale.Client.UI
{
    /// <summary>
    /// Colours and font sizes shared by every screen. Everything is drawn with plain tinted UGUI
    /// images (no sprites), so the palette is the whole visual identity of the client.
    /// </summary>
    public static class UiTheme
    {
        public static readonly Color Background = Hex("#141826");
        public static readonly Color Panel = Hex("#1F2437");
        public static readonly Color PanelLight = Hex("#2A3049");
        public static readonly Color Accent = Hex("#3D7BFF");
        public static readonly Color Secondary = Hex("#3A4160");
        public static readonly Color Success = Hex("#2EBD6B");
        public static readonly Color Danger = Hex("#E5484D");
        public static readonly Color Warning = Hex("#F5A524");
        public static readonly Color Text = Hex("#F4F6FB");
        public static readonly Color Muted = Hex("#9AA3BD");
        public static readonly Color InputBackground = Hex("#0F1220");
        public static readonly Color Highlight = Hex("#2F3B63");

        public const int TitleSize = 72;
        public const int HeadingSize = 46;
        public const int BodySize = 34;
        public const int SmallSize = 28;

        /// <summary>Block colours by colour index; levels use at most 6 colours, extra entries are a safety net.</summary>
        private static readonly Color[] BlockColors =
        {
            Hex("#E5484D"), // 0 red
            Hex("#3D7BFF"), // 1 blue
            Hex("#2EBD6B"), // 2 green
            Hex("#F5D90A"), // 3 yellow
            Hex("#A855F7"), // 4 purple
            Hex("#F97316"), // 5 orange
            Hex("#22D3EE"), // 6 cyan
            Hex("#EC4899")  // 7 pink
        };

        /// <summary>Colour of a board cell; -1 (empty) is never rendered but maps to the background just in case.</summary>
        public static Color BlockColor(int colorIndex)
        {
            if (colorIndex < 0)
            {
                return Background;
            }
            return BlockColors[colorIndex % BlockColors.Length];
        }

        private static Color Hex(string hex)
        {
            return ColorUtility.TryParseHtmlString(hex, out Color color) ? color : Color.magenta;
        }
    }
}
