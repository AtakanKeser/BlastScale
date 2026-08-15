using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BlastScale.Client.UI
{
    /// <summary>
    /// Helpers that build UGUI hierarchies from code. The whole client UI is created at runtime
    /// (no prefabs, no sprites, no TextMeshPro) so the project stays a pure-code repository that is
    /// easy to read in a diff; these helpers keep the screens short.
    /// </summary>
    public static class UiFactory
    {
        /// <summary>Portrait phone reference resolution; the CanvasScaler scales everything from it.</summary>
        public const float ReferenceWidth = 1080f;
        public const float ReferenceHeight = 1920f;

        private static Font _font;

        /// <summary>The built-in runtime font (Arial replacement shipped with Unity), loaded once.</summary>
        public static Font Font
        {
            get
            {
                if (_font == null)
                {
                    _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }
                return _font;
            }
        }

        // ------------------------------------------------------------------ containers

        /// <summary>Screen-space overlay canvas that scales with the screen size.</summary>
        public static Canvas CreateCanvas(string name)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            return canvas;
        }

        /// <summary>An empty RectTransform child (world position is not kept: UI coordinates are local).</summary>
        public static RectTransform CreateRect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return (RectTransform)go.transform;
        }

        /// <summary>Anchors the rect to all four edges of its parent with a uniform padding.</summary>
        public static void Stretch(RectTransform rt, float padding = 0f)
        {
            Stretch(rt, padding, padding, padding, padding);
        }

        public static void Stretch(RectTransform rt, float left, float right, float top, float bottom)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.offsetMin = new Vector2(left, bottom);
            rt.offsetMax = new Vector2(-right, -top);
        }

        /// <summary>A tinted rectangle. Set <paramref name="raycastTarget"/> for backdrops that must block input.</summary>
        public static Image CreatePanel(Transform parent, string name, Color color, bool raycastTarget = false)
        {
            RectTransform rt = CreateRect(parent, name);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycastTarget;
            return image;
        }

        /// <summary>A vertical stack container (children sized by their preferred height).</summary>
        public static RectTransform CreateColumn(Transform parent, string name, float spacing = 16f, int padding = 0, TextAnchor alignment = TextAnchor.UpperCenter)
        {
            RectTransform rt = CreateRect(parent, name);
            AddVerticalLayout(rt, spacing, padding, alignment);
            return rt;
        }

        /// <summary>A horizontal container with a fixed height, e.g. a row of buttons.</summary>
        public static RectTransform CreateRow(Transform parent, string name, float height, float spacing = 16f, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            RectTransform rt = CreateRect(parent, name);
            AddHorizontalLayout(rt, spacing, 0, alignment);
            SetLayout(rt.gameObject, preferredHeight: height, minHeight: height, flexibleWidth: 1f);
            return rt;
        }

        public static VerticalLayoutGroup AddVerticalLayout(RectTransform rt, float spacing, int padding, TextAnchor alignment = TextAnchor.UpperCenter)
        {
            var layout = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static HorizontalLayoutGroup AddHorizontalLayout(RectTransform rt, float spacing, int padding, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            var layout = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = new RectOffset(padding, padding, padding, padding);
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = true;
            return layout;
        }

        /// <summary>Fixed-column grid used for the board; the cell size is adjusted later by <see cref="BoardGridFitter"/>.</summary>
        public static GridLayoutGroup AddGrid(RectTransform rt, int columns, float cellSize, float spacing)
        {
            var grid = rt.gameObject.AddComponent<GridLayoutGroup>();
            grid.constraint = GridLayoutGroup.Constraint.FixedColumnCount;
            grid.constraintCount = columns;
            grid.cellSize = new Vector2(cellSize, cellSize);
            grid.spacing = new Vector2(spacing, spacing);
            grid.childAlignment = TextAnchor.MiddleCenter;
            grid.startCorner = GridLayoutGroup.Corner.UpperLeft;
            grid.startAxis = GridLayoutGroup.Axis.Horizontal;
            return grid;
        }

        /// <summary>Adds (or reuses) a LayoutElement; negative values leave the corresponding setting untouched.</summary>
        public static LayoutElement SetLayout(GameObject go, float preferredHeight = -1f, float preferredWidth = -1f,
            float flexibleHeight = -1f, float flexibleWidth = -1f, float minHeight = -1f, float minWidth = -1f)
        {
            var element = go.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = go.AddComponent<LayoutElement>();
            }
            if (preferredHeight >= 0) element.preferredHeight = preferredHeight;
            if (preferredWidth >= 0) element.preferredWidth = preferredWidth;
            if (flexibleHeight >= 0) element.flexibleHeight = flexibleHeight;
            if (flexibleWidth >= 0) element.flexibleWidth = flexibleWidth;
            if (minHeight >= 0) element.minHeight = minHeight;
            if (minWidth >= 0) element.minWidth = minWidth;
            return element;
        }

        /// <summary>Invisible element that soaks up remaining space in a layout.</summary>
        public static RectTransform CreateSpacer(Transform parent, float flexible = 1f)
        {
            RectTransform rt = CreateRect(parent, "Spacer");
            SetLayout(rt.gameObject, flexibleHeight: flexible, flexibleWidth: flexible);
            return rt;
        }

        /// <summary>Thin horizontal line.</summary>
        public static Image CreateDivider(Transform parent)
        {
            Image line = CreatePanel(parent, "Divider", UiTheme.Secondary);
            SetLayout(line.gameObject, preferredHeight: 3f, minHeight: 3f);
            return line;
        }

        // ------------------------------------------------------------------ widgets

        /// <summary>A text label. Inside layouts its height comes from the text unless <paramref name="preferredHeight"/> is given.</summary>
        public static Text CreateLabel(Transform parent, string text, int fontSize, Color color,
            TextAnchor alignment = TextAnchor.MiddleCenter, FontStyle style = FontStyle.Normal, float preferredHeight = 0f)
        {
            RectTransform rt = CreateRect(parent, "Label");
            var label = rt.gameObject.AddComponent<Text>();
            label.font = Font;
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = style;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            if (preferredHeight > 0f)
            {
                SetLayout(rt.gameObject, preferredHeight: preferredHeight, minHeight: preferredHeight);
            }
            return label;
        }

        /// <summary>A flat button with a centred caption; fills the width of its layout row by default.</summary>
        public static Button CreateButton(Transform parent, string label, UnityAction onClick, Color color,
            int fontSize = UiTheme.BodySize, float height = 110f, float preferredWidth = -1f)
        {
            RectTransform rt = CreateRect(parent, "Button " + label);
            var image = rt.gameObject.AddComponent<Image>();
            image.color = color;
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(0.9f, 0.9f, 0.9f, 1f);
            colors.pressedColor = new Color(0.7f, 0.7f, 0.7f, 1f);
            colors.disabledColor = new Color(0.55f, 0.55f, 0.55f, 0.5f);
            button.colors = colors;
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }
            SetLayout(rt.gameObject, preferredHeight: height, minHeight: height,
                flexibleWidth: preferredWidth < 0 ? 1f : 0f, preferredWidth: preferredWidth);
            Text caption = CreateLabel(rt, label, fontSize, UiTheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
            Stretch(caption.rectTransform, 12f);
            return button;
        }

        /// <summary>The caption of a button created by <see cref="CreateButton"/>.</summary>
        public static void SetButtonLabel(Button button, string text)
        {
            var caption = button.GetComponentInChildren<Text>();
            if (caption != null)
            {
                caption.text = text;
            }
        }

        /// <summary>Single-line legacy input field (Text based, no TextMeshPro).</summary>
        public static InputField CreateInputField(Transform parent, string placeholder, bool password = false, float height = 100f)
        {
            RectTransform rt = CreateRect(parent, "Input " + placeholder);
            var background = rt.gameObject.AddComponent<Image>();
            background.color = UiTheme.InputBackground;
            SetLayout(rt.gameObject, preferredHeight: height, minHeight: height, flexibleWidth: 1f);

            Text text = CreateLabel(rt, "", UiTheme.BodySize, UiTheme.Text, TextAnchor.MiddleLeft);
            text.supportRichText = false;
            Stretch(text.rectTransform, 24f, 24f, 8f, 8f);

            Text hint = CreateLabel(rt, placeholder, UiTheme.BodySize, UiTheme.Muted, TextAnchor.MiddleLeft, FontStyle.Italic);
            Stretch(hint.rectTransform, 24f, 24f, 8f, 8f);

            var input = rt.gameObject.AddComponent<InputField>();
            input.targetGraphic = background;
            input.textComponent = text;
            input.placeholder = hint;
            input.lineType = InputField.LineType.SingleLine;
            input.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
            input.caretWidth = 3;
            input.selectionColor = UiTheme.Accent;
            return input;
        }

        /// <summary>Vertical scroll view; add rows to <paramref name="content"/>, it grows with them.</summary>
        public static ScrollRect CreateScrollView(Transform parent, out RectTransform content, float spacing = 8f)
        {
            RectTransform rt = CreateRect(parent, "ScrollView");
            var background = rt.gameObject.AddComponent<Image>();
            background.color = UiTheme.Panel;
            background.raycastTarget = true; // receives the drag that scrolls the list
            SetLayout(rt.gameObject, flexibleHeight: 1f, flexibleWidth: 1f);

            RectTransform viewport = CreateRect(rt, "Viewport");
            Stretch(viewport, 8f);
            viewport.gameObject.AddComponent<RectMask2D>();

            content = CreateRect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            AddVerticalLayout(content, spacing, 8);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = rt.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 40f;
            return scroll;
        }

        /// <summary>A small "title over value" tile used for the home screen statistics.</summary>
        public static Text CreateStatTile(Transform parent, string title, string value)
        {
            Image tile = CreatePanel(parent, "Stat " + title, UiTheme.Panel);
            SetLayout(tile.gameObject, flexibleWidth: 1f, preferredHeight: 150f, minHeight: 150f);
            AddVerticalLayout(tile.rectTransform, 4f, 12, TextAnchor.MiddleCenter);
            CreateLabel(tile.transform, title, UiTheme.SmallSize, UiTheme.Muted);
            return CreateLabel(tile.transform, value, UiTheme.HeadingSize, UiTheme.Text, TextAnchor.MiddleCenter, FontStyle.Bold);
        }
    }
}
