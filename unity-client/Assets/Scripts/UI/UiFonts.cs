using UnityEngine;

namespace BlastScale.Client.UI
{
    /// <summary>The three typefaces of the UI.</summary>
    public enum UiFont
    {
        /// <summary>Fredoka One: titles, scores, big numbers.</summary>
        Display,

        /// <summary>Poppins Regular: paragraphs and secondary text.</summary>
        Body,

        /// <summary>Poppins SemiBold: labels, buttons, values.</summary>
        BodyBold
    }

    /// <summary>
    /// Loads the bundled OFL fonts (Assets/Fonts/Resources) as legacy dynamic fonts for UGUI
    /// <c>Text</c>. If a font is missing the built-in LegacyRuntime.ttf is used instead so the
    /// UI still renders; <see cref="UsingFallback"/> tells the README/report about it.
    /// </summary>
    public static class UiFonts
    {
        private static Font _display;
        private static Font _body;
        private static Font _bodyBold;
        private static Font _fallback;

        /// <summary>True when at least one bundled font could not be loaded.</summary>
        public static bool UsingFallback { get; private set; }

        public static Font Get(UiFont kind)
        {
            switch (kind)
            {
                case UiFont.Display:
                    return _display != null ? _display : (_display = Load("FredokaOne-Regular"));
                case UiFont.BodyBold:
                    return _bodyBold != null ? _bodyBold : (_bodyBold = Load("Poppins-SemiBold"));
                default:
                    return _body != null ? _body : (_body = Load("Poppins-Regular"));
            }
        }

        private static Font Load(string resourceName)
        {
            Font font = Resources.Load<Font>(resourceName);
            if (font != null)
            {
                return font;
            }
            UsingFallback = true;
            Debug.LogWarning("[UiFonts] Font '" + resourceName + "' not found in Resources; using LegacyRuntime.ttf");
            if (_fallback == null)
            {
                _fallback = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }
            return _fallback;
        }
    }
}
