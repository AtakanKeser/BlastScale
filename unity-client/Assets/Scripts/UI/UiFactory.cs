using BlastScale.Client.UI.Gfx;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace BlastScale.Client.UI
{
    /// <summary>Visual variants of <see cref="UiFactory.CreateButton"/>.</summary>
    public enum ButtonStyle
    {
        /// <summary>Green: the main action of a screen (play, finish, claim).</summary>
        Primary,

        /// <summary>Blue: an important secondary action (login, buy).</summary>
        Blue,

        /// <summary>Grey-blue: navigation and neutral actions.</summary>
        Secondary,

        /// <summary>Red: destructive or risky (give up, logout).</summary>
        Danger,

        /// <summary>Gold: rewards and shop highlights.</summary>
        Gold,

        /// <summary>Translucent white: quiet actions on top of cards.</summary>
        Ghost
    }

    /// <summary>
    /// Builds every UGUI widget of the client from code, using the runtime generated sprites of
    /// <see cref="SpriteFactory"/> and the bundled fonts. Rounded cards with drop shadows, candy
    /// buttons with a bevel and press animation, pills, progress bars, inputs and scroll views
    /// are all produced here so screens read as layout, not as pixel plumbing.
    /// </summary>
    public static class UiFactory
    {
        /// <summary>Portrait phone reference resolution; the CanvasScaler scales everything from it.</summary>
        public const float ReferenceWidth = 1080f;
        public const float ReferenceHeight = 1920f;

        private const float ShadowSpread = UiTheme.ShadowBlur * 2f;
        private const float ShadowOffset = 8f;

        // ------------------------------------------------------------------ containers

        /// <summary>
        /// The one canvas of the game. Screen-space-camera when a camera is available (that is what
        /// lets tests render it into a portrait texture), otherwise overlay. Scales with the
        /// screen from 1080x1920 matching width and height equally.
        /// </summary>
        public static Canvas CreateCanvas(string name, Camera camera)
        {
            var go = new GameObject(name, typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            var canvas = go.GetComponent<Canvas>();
            if (camera != null)
            {
                canvas.renderMode = RenderMode.ScreenSpaceCamera;
                canvas.worldCamera = camera;
                canvas.planeDistance = 10f;
            }
            else
            {
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            }
            canvas.pixelPerfect = false;
            var scaler = go.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = 100f;
            return canvas;
        }

        /// <summary>An empty RectTransform child (UI coordinates are local, world position is not kept).</summary>
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

        /// <summary>Centre anchored rect with a fixed size.</summary>
        public static void Center(RectTransform rt, float width, float height)
        {
            rt.anchorMin = rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(width, height);
            rt.anchoredPosition = Vector2.zero;
        }

        /// <summary>An Image showing a sprite (or a flat rect when the sprite is null).</summary>
        public static Image CreateImage(Transform parent, string name, Sprite sprite, Color color, bool raycastTarget = false)
        {
            RectTransform rt = CreateRect(parent, name);
            var image = rt.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = color;
            image.raycastTarget = raycastTarget;
            if (sprite != null && sprite.border != Vector4.zero)
            {
                image.type = Image.Type.Sliced;
            }
            return image;
        }

        /// <summary>A flat tinted rectangle (scrims, dividers, plain fills).</summary>
        public static Image CreatePanel(Transform parent, string name, Color color, bool raycastTarget = false)
        {
            return CreateImage(parent, name, null, color, raycastTarget);
        }

        /// <summary>A rounded rectangle body (no shadow); pills and inputs are built on it.</summary>
        public static Image CreateRounded(Transform parent, string name, Color color, float radius, bool raycastTarget = false)
        {
            return CreateImage(parent, name, SpriteFactory.RoundedRect(radius), color, raycastTarget);
        }

        /// <summary>A vertical stack container (children sized by their preferred height).</summary>
        public static RectTransform CreateColumn(Transform parent, string name, float spacing = 16f, int padding = 0, TextAnchor alignment = TextAnchor.UpperCenter)
        {
            RectTransform rt = CreateRect(parent, name);
            AddVerticalLayout(rt, spacing, padding, alignment);
            return rt;
        }

        /// <summary>
        /// A horizontal container with a fixed height, e.g. a row of buttons. flexibleHeight is
        /// pinned to 0: a layout group would otherwise report the flexible height of its children
        /// (spacers, force-expanded items) and the row would swallow the column's spare space.
        /// </summary>
        public static RectTransform CreateRow(Transform parent, string name, float height, float spacing = 16f, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            RectTransform rt = CreateRect(parent, name);
            AddHorizontalLayout(rt, spacing, 0, alignment);
            SetLayout(rt.gameObject, preferredHeight: height, minHeight: height, flexibleWidth: 1f, flexibleHeight: 0f);
            return rt;
        }

        public static VerticalLayoutGroup AddVerticalLayout(RectTransform rt, float spacing, int padding, TextAnchor alignment = TextAnchor.UpperCenter)
        {
            return AddVerticalLayout(rt, spacing, new RectOffset(padding, padding, padding, padding), alignment);
        }

        public static VerticalLayoutGroup AddVerticalLayout(RectTransform rt, float spacing, RectOffset padding, TextAnchor alignment)
        {
            var layout = rt.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding;
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return layout;
        }

        public static HorizontalLayoutGroup AddHorizontalLayout(RectTransform rt, float spacing, int padding, TextAnchor alignment = TextAnchor.MiddleCenter)
        {
            return AddHorizontalLayout(rt, spacing, new RectOffset(padding, padding, padding, padding), alignment);
        }

        public static HorizontalLayoutGroup AddHorizontalLayout(RectTransform rt, float spacing, RectOffset padding, TextAnchor alignment)
        {
            var layout = rt.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.padding = padding;
            layout.childAlignment = alignment;
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            // Children keep their preferred height (centred by the alignment). Forcing the expansion
            // would also make the group advertise a flexible height to its parent column.
            layout.childForceExpandHeight = false;
            return layout;
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

        /// <summary>Marks a child as decoration: stretched over the parent and ignored by layout groups.</summary>
        public static void IgnoreLayout(RectTransform rt)
        {
            var element = rt.gameObject.GetComponent<LayoutElement>();
            if (element == null)
            {
                element = rt.gameObject.AddComponent<LayoutElement>();
            }
            element.ignoreLayout = true;
        }

        /// <summary>Invisible element that soaks up remaining space in a layout.</summary>
        public static RectTransform CreateSpacer(Transform parent, float flexible = 1f)
        {
            RectTransform rt = CreateRect(parent, "Spacer");
            SetLayout(rt.gameObject, flexibleHeight: flexible, flexibleWidth: flexible);
            return rt;
        }

        /// <summary>Fixed-height gap inside a column.</summary>
        public static RectTransform CreateGap(Transform parent, float height)
        {
            RectTransform rt = CreateRect(parent, "Gap");
            SetLayout(rt.gameObject, preferredHeight: height, minHeight: height);
            return rt;
        }

        /// <summary>Thin translucent line.</summary>
        public static Image CreateDivider(Transform parent)
        {
            Image line = CreatePanel(parent, "Divider", new Color(1f, 1f, 1f, 0.12f));
            SetLayout(line.gameObject, preferredHeight: 2f, minHeight: 2f);
            return line;
        }

        // ------------------------------------------------------------------ cards

        /// <summary>
        /// A rounded card with drop shadow and a subtle border. The returned rect carries a layout
        /// group (vertical by default) so callers just add content; the chrome children ignore
        /// the layout and stretch behind the content.
        /// </summary>
        public static RectTransform CreateCard(Transform parent, string name, float radius = UiTheme.CardRadius, int padding = 28,
            float spacing = 12f, TextAnchor alignment = TextAnchor.UpperCenter, Color? fill = null, bool horizontal = false, bool shadow = true)
        {
            RectTransform root = CreateRect(parent, name);
            if (horizontal)
            {
                AddHorizontalLayout(root, spacing, padding, alignment);
            }
            else
            {
                AddVerticalLayout(root, spacing, padding, alignment);
            }
            // Cards size themselves to their content; callers that want a stretching card
            // (the board) set flexibleHeight afterwards.
            SetLayout(root.gameObject, flexibleHeight: 0f);
            AddCardChrome(root, radius, fill ?? UiTheme.CardFill, shadow, true);
            return root;
        }

        /// <summary>Adds shadow, body and border behind an existing rect (all ignoring its layout group).</summary>
        public static void AddCardChrome(RectTransform root, float radius, Color fill, bool shadow, bool border)
        {
            if (shadow)
            {
                Image shadowImage = CreateImage(root, "Shadow", SpriteFactory.Shadow(radius, UiTheme.ShadowBlur), UiTheme.ShadowColor);
                Stretch(shadowImage.rectTransform, -ShadowSpread, -ShadowSpread, -ShadowSpread + ShadowOffset, -ShadowSpread - ShadowOffset);
                IgnoreLayout(shadowImage.rectTransform);
                shadowImage.transform.SetAsFirstSibling();
            }
            Image body = CreateImage(root, "Body", SpriteFactory.RoundedRect(radius), fill, true);
            Stretch(body.rectTransform);
            IgnoreLayout(body.rectTransform);
            body.transform.SetSiblingIndex(shadow ? 1 : 0);
            if (border)
            {
                Image outline = CreateImage(root, "Border", SpriteFactory.RoundedOutline(radius, 2f), UiTheme.CardBorder);
                Stretch(outline.rectTransform);
                IgnoreLayout(outline.rectTransform);
                outline.transform.SetSiblingIndex(shadow ? 2 : 1);
            }
        }

        /// <summary>A soft coloured glow behind an element (claimable reward, completed progress).</summary>
        public static Image CreateGlow(RectTransform host, Color color, float spread = 30f, float radius = UiTheme.CardRadius)
        {
            Image glow = CreateImage(host, "Glow", SpriteFactory.Shadow(radius, spread * 0.6f), color);
            Stretch(glow.rectTransform, -spread, -spread, -spread, -spread);
            IgnoreLayout(glow.rectTransform);
            glow.transform.SetAsFirstSibling();
            return glow;
        }

        // ------------------------------------------------------------------ text

        /// <summary>A text label in one of the bundled fonts; height comes from the text unless <paramref name="preferredHeight"/> is set.</summary>
        public static Text CreateLabel(Transform parent, string text, int fontSize, Color color, TextAnchor alignment = TextAnchor.MiddleCenter,
            UiFont font = UiFont.Body, float preferredHeight = 0f)
        {
            RectTransform rt = CreateRect(parent, "Label");
            var label = rt.gameObject.AddComponent<Text>();
            label.font = UiFonts.Get(font);
            label.text = text;
            label.fontSize = fontSize;
            label.color = color;
            label.alignment = alignment;
            label.fontStyle = FontStyle.Normal;
            label.horizontalOverflow = HorizontalWrapMode.Wrap;
            label.verticalOverflow = VerticalWrapMode.Overflow;
            label.raycastTarget = false;
            label.supportRichText = true;
            if (preferredHeight > 0f)
            {
                SetLayout(rt.gameObject, preferredHeight: preferredHeight, minHeight: preferredHeight);
            }
            return label;
        }

        /// <summary>Display-font text (titles, scores) with a soft shadow so it pops on any background.</summary>
        public static Text CreateTitle(Transform parent, string text, int fontSize, Color color, TextAnchor alignment = TextAnchor.MiddleCenter, float preferredHeight = 0f)
        {
            Text label = CreateLabel(parent, text, fontSize, color, alignment, UiFont.Display, preferredHeight);
            AddShadow(label, Mathf.Max(3f, fontSize * 0.05f));
            return label;
        }

        public static Shadow AddShadow(Text text, float distance = 3f, Color? color = null)
        {
            var shadow = text.gameObject.AddComponent<Shadow>();
            shadow.effectColor = color ?? UiTheme.TextShadow;
            shadow.effectDistance = new Vector2(0f, -distance);
            shadow.useGraphicAlpha = true;
            return shadow;
        }

        public static Outline AddOutline(Text text, float thickness = 2f, Color? color = null)
        {
            var outline = text.gameObject.AddComponent<Outline>();
            outline.effectColor = color ?? new Color(0f, 0f, 0f, 0.5f);
            outline.effectDistance = new Vector2(thickness, -thickness);
            outline.useGraphicAlpha = true;
            return outline;
        }

        // ------------------------------------------------------------------ buttons

        public static Color StyleColor(ButtonStyle style)
        {
            switch (style)
            {
                case ButtonStyle.Primary: return UiTheme.Primary;
                case ButtonStyle.Blue: return UiTheme.Blue;
                case ButtonStyle.Danger: return UiTheme.Danger;
                case ButtonStyle.Gold: return UiTheme.Amber;
                case ButtonStyle.Ghost: return new Color(1f, 1f, 1f, 0.14f);
                default: return UiTheme.Secondary;
            }
        }

        /// <summary>
        /// A candy button: shadow, rounded gradient body, bevel, bold white label (optional icon on
        /// the left) and the press animation of <see cref="ButtonJuice"/>. Fills its layout row
        /// unless <paramref name="preferredWidth"/> is given. The object is named "Button &lt;label&gt;".
        /// </summary>
        public static Button CreateButton(Transform parent, string label, UnityAction onClick, ButtonStyle style = ButtonStyle.Primary,
            int fontSize = UiTheme.BodySize, float height = UiTheme.ButtonHeight, float preferredWidth = -1f, Sprite icon = null, Color? iconColor = null)
        {
            RectTransform rt = CreateRect(parent, "Button " + label);
            SetLayout(rt.gameObject, preferredHeight: height, minHeight: height,
                flexibleWidth: preferredWidth < 0 ? 1f : 0f, preferredWidth: preferredWidth);
            float radius = Mathf.Min(UiTheme.ButtonRadius, height * 0.5f);
            Image body = BuildButtonChrome(rt, radius, StyleColor(style), style != ButtonStyle.Ghost);

            if (icon != null)
            {
                RectTransform row = CreateRect(rt, "Content");
                Stretch(row, 24f, 24f, 0f, 0f);
                AddHorizontalLayout(row, 14f, 0, TextAnchor.MiddleCenter);
                CreateIcon(row, icon, iconColor ?? Color.white, fontSize * 1.3f);
                Text caption = CreateLabel(row, label, fontSize, UiTheme.Text, TextAnchor.MiddleLeft, UiFont.BodyBold);
                AddShadow(caption, 2f);
            }
            else
            {
                Text caption = CreateLabel(rt, label, fontSize, UiTheme.Text, TextAnchor.MiddleCenter, UiFont.BodyBold);
                Stretch(caption.rectTransform, 16f, 16f, 4f, 4f);
                AddShadow(caption, 2f);
            }
            return FinishButton(rt, body, onClick);
        }

        /// <summary>A square button showing only an icon (back, close, mute...). Named "Button &lt;name&gt;".</summary>
        public static Button CreateIconButton(Transform parent, string name, Sprite icon, UnityAction onClick, ButtonStyle style = ButtonStyle.Secondary,
            float size = UiTheme.IconButtonSize, Color? iconColor = null, float iconScale = 0.5f)
        {
            RectTransform rt = CreateRect(parent, "Button " + name);
            SetLayout(rt.gameObject, preferredHeight: size, minHeight: size, preferredWidth: size, minWidth: size, flexibleWidth: 0f);
            Image body = BuildButtonChrome(rt, Mathf.Min(UiTheme.ButtonRadius, size * 0.5f), StyleColor(style), style != ButtonStyle.Ghost);
            Image iconImage = CreateImage(rt, "Icon", icon, iconColor ?? Color.white);
            Center(iconImage.rectTransform, size * iconScale, size * iconScale);
            return FinishButton(rt, body, onClick);
        }

        private static Image BuildButtonChrome(RectTransform rt, float radius, Color color, bool shadow)
        {
            if (shadow)
            {
                Image shadowImage = CreateImage(rt, "Shadow", SpriteFactory.Shadow(radius, UiTheme.ShadowBlur), UiTheme.ShadowColor);
                Stretch(shadowImage.rectTransform, -ShadowSpread, -ShadowSpread, -ShadowSpread + ShadowOffset, -ShadowSpread - ShadowOffset);
            }
            Image body = CreateImage(rt, "Body", SpriteFactory.RoundedRect(radius), color, true);
            Stretch(body.rectTransform);
            Image bevel = CreateImage(rt, "Bevel", SpriteFactory.Bevel(radius), Color.white);
            Stretch(bevel.rectTransform);
            return body;
        }

        private static Button FinishButton(RectTransform rt, Image body, UnityAction onClick)
        {
            var group = rt.gameObject.AddComponent<CanvasGroup>();
            var button = rt.gameObject.AddComponent<Button>();
            button.targetGraphic = body;
            ColorBlock colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = Color.white;
            colors.pressedColor = new Color(0.86f, 0.86f, 0.86f, 1f);
            colors.selectedColor = Color.white;
            colors.disabledColor = Color.white; // ButtonJuice paints the disabled look
            colors.fadeDuration = 0.05f;
            button.colors = colors;
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }
            var juice = rt.gameObject.AddComponent<ButtonJuice>();
            juice.Bind(button, group, body);
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

        /// <summary>Enables/disables with the desaturated 50 % look (use instead of Button.interactable).</summary>
        public static void SetButtonEnabled(Button button, bool enabled)
        {
            var juice = button.GetComponent<ButtonJuice>();
            if (juice != null)
            {
                juice.SetEnabled(enabled);
            }
            else
            {
                button.interactable = enabled;
            }
        }

        /// <summary>Recolours the body of an existing button.</summary>
        public static void SetButtonStyle(Button button, ButtonStyle style)
        {
            var juice = button.GetComponent<ButtonJuice>();
            if (juice != null)
            {
                juice.SetBodyColor(StyleColor(style));
            }
        }

        // ------------------------------------------------------------------ widgets

        /// <summary>An icon image with a fixed layout size.</summary>
        public static Image CreateIcon(Transform parent, Sprite sprite, Color color, float size)
        {
            Image image = CreateImage(parent, "Icon", sprite, color);
            image.preserveAspect = true;
            SetLayout(image.gameObject, preferredWidth: size, preferredHeight: size, minWidth: size, minHeight: size, flexibleWidth: 0f);
            return image;
        }

        /// <summary>
        /// A rounded pill "[icon] value" used for counters (coins, lives, moves). Inside a layout
        /// row its width follows its content; <paramref name="minWidth"/> keeps it from jumping
        /// when the value changes.
        /// </summary>
        public static RectTransform CreatePill(Transform parent, string name, Sprite icon, Color iconColor, string value, out Text valueLabel,
            float height = 84f, Color? fill = null, int fontSize = UiTheme.BodySize, float minWidth = 0f, UiFont font = UiFont.Display)
        {
            RectTransform root = CreateRect(parent, name);
            AddHorizontalLayout(root, 10f, new RectOffset(icon != null ? 14 : 26, 26, 0, 0), TextAnchor.MiddleCenter);
            SetLayout(root.gameObject, preferredHeight: height, minHeight: height, minWidth: minWidth, flexibleWidth: 0f, flexibleHeight: 0f);
            Image body = CreateImage(root, "Body", SpriteFactory.RoundedRect(height * 0.5f), fill ?? new Color(0f, 0f, 0f, 0.32f));
            Stretch(body.rectTransform);
            IgnoreLayout(body.rectTransform);
            Image border = CreateImage(root, "Border", SpriteFactory.RoundedOutline(height * 0.5f, 2f), new Color(1f, 1f, 1f, 0.12f));
            Stretch(border.rectTransform);
            IgnoreLayout(border.rectTransform);
            if (icon != null)
            {
                CreateIcon(root, icon, iconColor, height * 0.68f);
            }
            valueLabel = CreateLabel(root, value, fontSize, UiTheme.Text, TextAnchor.MiddleCenter, font);
            valueLabel.horizontalOverflow = HorizontalWrapMode.Overflow;
            AddShadow(valueLabel, 2f);
            return root;
        }

        /// <summary>A rounded track with a fill anchored to the left and a glow that lights up when complete.</summary>
        public static RectTransform CreateProgressBar(Transform parent, string name, float height, Color fillColor, out Image fill, out Image glow)
        {
            RectTransform root = CreateRect(parent, name);
            SetLayout(root.gameObject, preferredHeight: height, minHeight: height, flexibleWidth: 1f);
            float radius = height * 0.5f;
            glow = CreateImage(root, "Glow", SpriteFactory.Shadow(radius, 14f), UiTheme.WithAlpha(fillColor, 0f));
            Stretch(glow.rectTransform, -22f, -22f, -22f, -22f);
            Image track = CreateImage(root, "Track", SpriteFactory.RoundedRect(radius), new Color(0f, 0f, 0f, 0.35f));
            Stretch(track.rectTransform);
            Image inner = CreateImage(root, "TrackShadow", SpriteFactory.InnerShadow(radius, 8f), new Color(0f, 0f, 0f, 0.35f));
            Stretch(inner.rectTransform);
            fill = CreateImage(root, "Fill", SpriteFactory.RoundedRect(radius), fillColor);
            fill.rectTransform.pivot = new Vector2(0f, 0.5f);
            SetProgress(fill, 0f);
            Image gloss = CreateImage(fill.rectTransform, "Gloss", SpriteFactory.Bevel(radius), Color.white);
            Stretch(gloss.rectTransform);
            return root;
        }

        /// <summary>
        /// Sets a progress bar's fill to <paramref name="progress"/> (0..1). The fill is anchored by
        /// fraction of the track, so it stays correct when the screen size changes.
        /// </summary>
        public static void SetProgress(Image fill, float progress)
        {
            float clamped = Mathf.Clamp01(progress);
            float fraction = clamped <= 0f ? 0f : Mathf.Max(clamped, 0.04f);
            RectTransform rt = fill.rectTransform;
            rt.anchorMin = new Vector2(0f, 0f);
            rt.anchorMax = new Vector2(fraction, 1f);
            rt.offsetMin = new Vector2(4f, 4f);
            rt.offsetMax = new Vector2(fraction <= 0f ? 4f : -4f, -4f);
        }

        /// <summary>A small round count badge (booster inventory).</summary>
        public static Image CreateBadge(RectTransform host, string text, Color color, out Text label, float size = 52f)
        {
            Image badge = CreateImage(host, "Badge", SpriteFactory.Circle(64), color);
            RectTransform rt = badge.rectTransform;
            rt.anchorMin = rt.anchorMax = new Vector2(1f, 1f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.sizeDelta = new Vector2(size, size);
            rt.anchoredPosition = new Vector2(-10f, -8f);
            IgnoreLayout(rt);
            label = CreateLabel(rt, text, UiTheme.SmallSize, UiTheme.Text, TextAnchor.MiddleCenter, UiFont.Display);
            Stretch(label.rectTransform);
            AddShadow(label, 2f);
            return badge;
        }

        /// <summary>Single-line legacy input field on a dark rounded body (no TextMeshPro).</summary>
        public static InputField CreateInputField(Transform parent, string placeholder, bool password = false, float height = 110f)
        {
            RectTransform rt = CreateRect(parent, "Input " + placeholder);
            SetLayout(rt.gameObject, preferredHeight: height, minHeight: height, flexibleWidth: 1f);
            float radius = Mathf.Min(UiTheme.ButtonRadius, height * 0.5f);
            Image background = CreateImage(rt, "Body", SpriteFactory.RoundedRect(radius), UiTheme.InputFill, true);
            Stretch(background.rectTransform);
            Image border = CreateImage(rt, "Border", SpriteFactory.RoundedOutline(radius, 2f), UiTheme.CardBorder);
            Stretch(border.rectTransform);

            Text text = CreateLabel(rt, "", UiTheme.BodySize, UiTheme.Text, TextAnchor.MiddleLeft, UiFont.Body);
            text.supportRichText = false;
            Stretch(text.rectTransform, 30f, 30f, 8f, 8f);

            Text hint = CreateLabel(rt, placeholder, UiTheme.BodySize, UiTheme.Muted, TextAnchor.MiddleLeft, UiFont.Body);
            Stretch(hint.rectTransform, 30f, 30f, 8f, 8f);

            var input = rt.gameObject.AddComponent<InputField>();
            input.targetGraphic = background;
            input.textComponent = text;
            input.placeholder = hint;
            input.lineType = InputField.LineType.SingleLine;
            input.contentType = password ? InputField.ContentType.Password : InputField.ContentType.Standard;
            input.caretWidth = 3;
            input.selectionColor = UiTheme.WithAlpha(UiTheme.Blue, 0.6f);
            return input;
        }

        /// <summary>Vertical scroll view without a visible frame; add rows to <paramref name="content"/>.</summary>
        public static ScrollRect CreateScrollView(Transform parent, out RectTransform content, float spacing = 12f)
        {
            RectTransform rt = CreateRect(parent, "ScrollView");
            var catcher = rt.gameObject.AddComponent<Image>();
            catcher.color = Color.clear;
            catcher.raycastTarget = true; // receives the drag that scrolls the list
            SetLayout(rt.gameObject, flexibleHeight: 1f, flexibleWidth: 1f);

            RectTransform viewport = CreateRect(rt, "Viewport");
            Stretch(viewport, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();

            content = CreateRect(viewport, "Content");
            content.anchorMin = new Vector2(0f, 1f);
            content.anchorMax = new Vector2(1f, 1f);
            content.pivot = new Vector2(0.5f, 1f);
            content.offsetMin = Vector2.zero;
            content.offsetMax = Vector2.zero;
            AddVerticalLayout(content, spacing, new RectOffset(4, 4, 8, 40), TextAnchor.UpperCenter);
            var fitter = content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            var scroll = rt.gameObject.AddComponent<ScrollRect>();
            scroll.content = content;
            scroll.viewport = viewport;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Elastic;
            scroll.elasticity = 0.12f;
            scroll.inertia = true;
            scroll.decelerationRate = 0.12f;
            scroll.scrollSensitivity = 40f;
            return scroll;
        }
    }
}
