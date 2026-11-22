using SpookyStreet.Core.Art;
using SpookyStreet.Localization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SpookyStreet.UI
{
    /// <summary>
    /// Builds the craft UI from code (docs/ui-language.md): bone paper cards and tags, felt discs and buttons, running
    /// stitches and an ink outline, all from the baked sprites in <c>Resources/UI</c> (<see cref="UiSprites"/>).
    /// Layout units are reference pixels on a 1334 × 750 landscape canvas matched by height, so 2 units ≈ 1 pt on a
    /// 375-pt-tall iPhone; 96 units ≈ 48 pt is the minimum touch target. Fonts are OFL-licensed
    /// (docs/asset-provenance.md): Andika for reading, Sniglet for display.
    /// </summary>
    public static class UiFactory
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1334f, 750f);

        /// <summary>Reference units per point at the 750-unit-tall reference.</summary>
        public const float UnitsPerPoint = 2f;

        /// <summary>Minimum hit area for anything tappable (48 pt; the platform floor is 44 pt).</summary>
        public const float MinTouchTarget = 96f;

        public const int BodySize = 34;
        public const int ButtonSize = 40;
        public const int CaptionSize = 26;

        // docs/ui-language.md touch targets (reference units): Jump 78 pt, Use 65 pt, system discs 50 pt.
        public const float JumpDiameter = 150f;
        public const float UseDiameter = 130f;
        public const float SystemDiscDiameter = 100f;

        /// <summary>Felt discs in play are 88–92 % opaque so the world still reads through them.</summary>
        public const float HudFeltAlpha = 0.9f;

        /// <summary>Text and outlines on paper and orange (docs/ui-language.md Ink #1E1A26).</summary>
        public static Color Ink => Palette.InkBlack;

        /// <summary>
        /// Paper in play is bone dimmed a little so a label never out-shines the lit attic window (brightness rule);
        /// modal screens use full bone because they dim the world first.
        /// </summary>
        public static Color PlayPaper => new Color(0.93f, 0.93f, 0.93f, 1f);

        private static Font s_body;
        private static Font s_bold;
        private static Font s_display;

        public static Font BodyFont => s_body != null ? s_body : s_body = LoadFont("Fonts/Andika-Regular");

        public static Font BoldFont => s_bold != null ? s_bold : s_bold = LoadFont("Fonts/Andika-Bold");

        public static Font DisplayFont => s_display != null ? s_display : s_display = LoadFont("Fonts/Sniglet-ExtraBold");

        /// <summary>Kept for callers that only need a circle sprite.</summary>
        public static Sprite Circle => IconFactory.Disc;

        private static Font LoadFont(string path)
        {
            var font = Resources.Load<Font>(path);
            if (font == null)
            {
                Debug.LogWarning($"[UiFactory] Font Resources/{path} missing; falling back to the built-in font.");
                font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            }

            return font;
        }

        public static Color WithAlpha(Color c, float alpha) => new Color(c.r, c.g, c.b, alpha);

        public static Canvas CreateCanvas(string name, int sortingOrder)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var canvas = go.AddComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = sortingOrder;
            var scaler = go.AddComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = ReferenceResolution;
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 1f;
            scaler.referencePixelsPerUnit = 100f; // baked sprites import at 200 px per unit: 2 sprite px per unit
            go.AddComponent<GraphicRaycaster>();
            return canvas;
        }

        public static RectTransform Rect(Transform parent, string name)
        {
            var go = new GameObject(name, typeof(RectTransform));
            var rect = (RectTransform)go.transform;
            rect.SetParent(parent, false);
            return rect;
        }

        public static RectTransform Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero;
            rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
            return rect;
        }

        public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size,
            Vector2? pivot = null)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = pivot ?? new Vector2(0.5f, 0.5f);
            rect.anchoredPosition = position;
            rect.sizeDelta = size;
            return rect;
        }

        public static Image Panel(Transform parent, string name, Color color, bool raycast = true)
        {
            var rect = Stretch(Rect(parent, name));
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        public static Image Sprite(Transform parent, string name, Sprite sprite, Color color, Vector2 anchor,
            Vector2 position, Vector2 size, bool raycast = false)
        {
            var rect = Place(Rect(parent, name), anchor, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = sprite != null && sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
            image.color = color;
            image.raycastTarget = raycast;
            return image;
        }

        /// <summary>A baked sprite by catalog name (procedural stand-in when missing).</summary>
        public static Sprite Baked(string name) => UiSprites.Get(name);

        /// <summary>
        /// Puts a baked surface (felt or paper) on an image. Baked surfaces carry their own colour, so the tint is
        /// white (or <paramref name="tint"/>); when the PNG is missing the procedural stand-in gets the material colour.
        /// </summary>
        public static void ApplySurface(Image image, string spriteName, Color materialColor, Color tint)
        {
            var sprite = UiSprites.TryLoad(spriteName);
            if (sprite != null)
            {
                image.sprite = sprite;
                image.color = tint;
            }
            else
            {
                image.sprite = UiSprites.Get(spriteName);
                image.color = new Color(materialColor.r * tint.r, materialColor.g * tint.g, materialColor.b * tint.b,
                    tint.a);
            }

            image.type = image.sprite != null && image.sprite.border != Vector4.zero ? Image.Type.Sliced : Image.Type.Simple;
        }

        /// <summary>Hard ink drop shadow: the "cut paper on a desk" look.</summary>
        public static void InkShadow(Graphic graphic, float distance = 6f, float alpha = 0.9f) =>
            InkShadow(graphic, new Vector2(distance, -distance), alpha);

        public static void InkShadow(Graphic graphic, Vector2 offset, float alpha)
        {
            var shadow = graphic.gameObject.AddComponent<Shadow>();
            shadow.effectColor = WithAlpha(Palette.InkBlack, alpha);
            shadow.effectDistance = offset;
        }

        public static Text Label(Transform parent, string name, string key, int fontSize, TextAnchor alignment,
            Color color, Font font = null)
        {
            var rect = Rect(parent, name);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font != null ? font : BodyFont;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            // Never below 13 pt (26 units) for labels, whatever the language.
            text.resizeTextMinSize = Mathf.Min(fontSize, Mathf.Max(26, fontSize * 2 / 3));
            text.resizeTextMaxSize = fontSize;
            if (!string.IsNullOrEmpty(key))
            {
                rect.gameObject.AddComponent<LocalizedText>().SetKey(key);
            }

            return text;
        }

        /// <summary>Tiled running stitch over a card or button (tint = thread colour). Null when the sprite is missing.</summary>
        public static Image StitchLayer(Transform parent, Color thread)
        {
            var sprite = UiSprites.Get(UiSpriteCatalog.StitchRect);
            if (sprite == null)
            {
                return null;
            }

            var image = Stretch(Rect(parent, "Stitch")).gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.type = Image.Type.Tiled;
            image.fillCenter = false;
            image.color = thread;
            image.raycastTarget = false;
            return image;
        }

        /// <summary>
        /// Paper card: deckled bone paper with an ink outline and a running stitch (purple by default), a hard ink
        /// shadow (4/6 ref units) and a slight cut-paper rotation.
        /// </summary>
        public static Image PaperCard(Transform parent, string name, Vector2 anchor, Vector2 position, Vector2 size,
            float rotation, Color? stitch = null, Color? tint = null)
        {
            var card = Sprite(parent, name, null, Color.white, anchor, position, size);
            ApplySurface(card, UiSpriteCatalog.PaperCard, Palette.WarmPaper, tint ?? Color.white);
            card.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            InkShadow(card, new Vector2(4f, -6f), 0.6f);
            StitchLayer(card.transform, stitch ?? Palette.HauntedPurple);
            return card;
        }

        /// <summary>Small bone paper tag (labels, verb tags, the first prompt): deckled, ink outline, no stitch.</summary>
        public static Image PaperTag(Transform parent, string name, string key, int fontSize, Vector2 anchor,
            Vector2 position, Vector2 size, float rotation, Color tint)
        {
            var tag = Sprite(parent, name, null, Color.white, anchor, position, size);
            ApplySurface(tag, UiSpriteCatalog.PaperTag, Palette.WarmPaper, tint);
            tag.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            InkShadow(tag, new Vector2(3f, -4f), 0.55f);
            var text = Label(tag.transform, "Text", key, fontSize, TextAnchor.MiddleCenter, Ink, BoldFont);
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(18f, 4f);
            text.rectTransform.offsetMax = new Vector2(-18f, -4f);
            return tag;
        }

        /// <summary>Primary action: orange felt, candy stitch, ink label. One per screen.</summary>
        public static Button PrimaryButton(Transform parent, string name, string key, Vector2 anchor, Vector2 position,
            Vector2 size, UnityAction onClick, string icon = null) =>
            BuildButton(parent, name, key, anchor, position, size, onClick, icon, CraftButtonStyle.Primary);

        /// <summary>Secondary action: bone paper, purple stitch (orange while pressed), ink label.</summary>
        public static Button SecondaryButton(Transform parent, string name, string key, Vector2 anchor, Vector2 position,
            Vector2 size, UnityAction onClick, string icon = null) =>
            BuildButton(parent, name, key, anchor, position, size, onClick, icon, CraftButtonStyle.Secondary);

        /// <summary>Back-compat helper used by older call sites: a primary button.</summary>
        public static Button TextButton(Transform parent, string name, string key, Vector2 anchor, Vector2 position,
            Vector2 size, UnityAction onClick) => PrimaryButton(parent, name, key, anchor, position, size, onClick);

        public static Button BuildButton(Transform parent, string name, string key, Vector2 anchor, Vector2 position,
            Vector2 size, UnityAction onClick, string icon, CraftButtonStyle style)
        {
            size = Vector2.Max(size, new Vector2(MinTouchTarget, MinTouchTarget));
            var face = Sprite(parent, name, null, Color.white, anchor, position, size, raycast: true);
            InkShadow(face, new Vector2(4f, -6f), 0.6f);
            var stitch = StitchLayer(face.transform, Palette.HauntedPurple);

            var button = face.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            // States are drawn by CraftButton/PressScale (darker felt, stitch colour), not by a colour tint.
            button.transition = Selectable.Transition.None;
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            var press = face.gameObject.AddComponent<PressScale>();
            press.Darken = new Graphic[] { face };
            press.Stitch = stitch;

            float iconSize = Mathf.Min(size.y * 0.42f, 52f);
            Image iconImage = null;
            float textLeft = 24f;
            if (!string.IsNullOrEmpty(icon))
            {
                iconImage = Sprite(face.transform, "Icon", Baked(icon), Ink, new Vector2(0f, 0.5f),
                    new Vector2(30f + iconSize * 0.5f, 0f), new Vector2(iconSize, iconSize));
                textLeft = 40f + iconSize;
            }

            var label = Label(face.transform, "Label", key, ButtonSize, TextAnchor.MiddleCenter, Ink, BoldFont);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(textLeft, 10f);
            label.rectTransform.offsetMax = new Vector2(-24f, -8f);

            var detail = Label(face.transform, "Detail", null, CaptionSize, TextAnchor.MiddleCenter,
                WithAlpha(Ink, 0.8f), BoldFont);
            Stretch(detail.rectTransform);
            detail.rectTransform.anchorMax = new Vector2(1f, 0.42f);
            detail.rectTransform.offsetMin = new Vector2(textLeft, 10f);
            detail.rectTransform.offsetMax = new Vector2(-24f, 0f);
            detail.gameObject.SetActive(false);

            var craft = face.gameObject.AddComponent<CraftButton>();
            craft.Face = face;
            craft.Stitch = stitch;
            craft.Icon = iconImage;
            craft.Label = label;
            craft.Detail = detail;
            craft.Press = press;
            craft.SetStyle(style);
            return button;
        }

        /// <summary>
        /// Thumb or system control: a felt disc with a stitched ring, a line icon and an optional short caption
        /// (spec §5: icon + localized verb). Night felt for Jump and system discs, orange for Use. Returns the disc
        /// image, which is the raycast target; pressing darkens the felt and squashes the disc.
        /// </summary>
        public static Image FeltDisc(Transform parent, string name, string feltSprite, Color thread, string icon,
            Color iconColor, string captionKey, float diameter, Vector2 anchor, Vector2 position, float alpha)
        {
            diameter = Mathf.Max(diameter, MinTouchTarget);
            var disc = Sprite(parent, name, null, Color.white, anchor, position, new Vector2(diameter, diameter),
                raycast: true);
            ApplySurface(disc, feltSprite, FeltColor(feltSprite), WithAlpha(Color.white, alpha));
            InkShadow(disc, new Vector2(3f, -4f), 0.45f);
            var ring = Sprite(disc.transform, "Ring", Baked(UiSpriteCatalog.StitchRing), thread, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(diameter, diameter));
            ring.raycastTarget = false;

            bool hasCaption = !string.IsNullOrEmpty(captionKey);
            float iconSize = diameter * (hasCaption ? 0.4f : 0.48f);
            Sprite(disc.transform, "Icon", Baked(icon), iconColor, new Vector2(0.5f, 0.5f),
                new Vector2(0f, hasCaption ? diameter * 0.11f : 0f), new Vector2(iconSize, iconSize));
            if (hasCaption)
            {
                var caption = Label(disc.transform, "Caption", captionKey, CaptionSize, TextAnchor.MiddleCenter,
                    iconColor, BoldFont);
                Place(caption.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -diameter * 0.22f),
                    new Vector2(diameter * 0.74f, 34f));
            }

            var press = disc.gameObject.AddComponent<PressScale>();
            press.Darken = new Graphic[] { disc };
            return disc;
        }

        private static Color FeltColor(string feltSprite)
        {
            switch (feltSprite)
            {
                case UiSpriteCatalog.FeltDiscOrange:
                    return Palette.JackOrange;
                case UiSpriteCatalog.FeltDiscBone:
                    return Palette.WarmPaper;
                default:
                    return Palette.NightViolet;
            }
        }
    }
}
