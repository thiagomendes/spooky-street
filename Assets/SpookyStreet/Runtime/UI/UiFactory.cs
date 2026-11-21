using SpookyStreet.Localization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SpookyStreet.UI
{
    /// <summary>
    /// Builds the paper-cutout UI from code (docs/art-bible.md). Layout units are reference pixels on a 1334 × 750
    /// landscape canvas matched by height, so 2 units ≈ 1 pt on a 375-pt-tall iPhone; 96 units ≈ 48 pt is the
    /// minimum touch target (spec §5). Fonts are OFL-licensed (docs/asset-provenance.md): Andika for reading,
    /// Sniglet for display.
    /// </summary>
    public static class UiFactory
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1334f, 750f);
        public const float MinTouchTarget = 96f;
        public const int BodySize = 34;
        public const int ButtonSize = 40;

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

        /// <summary>Hard ink drop shadow: the "cut paper on a desk" look.</summary>
        public static void InkShadow(Graphic graphic, float distance = 6f, float alpha = 0.9f)
        {
            var shadow = graphic.gameObject.AddComponent<Shadow>();
            shadow.effectColor = WithAlpha(Palette.Ink, alpha);
            shadow.effectDistance = new Vector2(distance, -distance);
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
            text.resizeTextMinSize = Mathf.Max(12, fontSize * 2 / 3);
            text.resizeTextMaxSize = fontSize;
            if (!string.IsNullOrEmpty(key))
            {
                rect.gameObject.AddComponent<LocalizedText>().SetKey(key);
            }

            return text;
        }

        /// <summary>Paper card: rounded warm-paper rectangle with an ink shadow, slightly rotated like cut paper.</summary>
        public static Image PaperCard(Transform parent, string name, Color fill, Vector2 anchor, Vector2 position,
            Vector2 size, float rotation)
        {
            var card = Sprite(parent, name, IconFactory.RoundedRect, fill, anchor, position, size);
            card.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            InkShadow(card);
            return card;
        }

        /// <summary>Primary action: pumpkin-amber card with an ink label.</summary>
        public static Button PrimaryButton(Transform parent, string name, string key, Vector2 anchor, Vector2 position,
            Vector2 size, UnityAction onClick) =>
            CardButton(parent, name, key, anchor, position, size, onClick, Palette.PumpkinAmber, false);

        /// <summary>Secondary action: warm-paper card with an ink outline.</summary>
        public static Button SecondaryButton(Transform parent, string name, string key, Vector2 anchor, Vector2 position,
            Vector2 size, UnityAction onClick) =>
            CardButton(parent, name, key, anchor, position, size, onClick, Palette.WarmPaper, true);

        /// <summary>Back-compat helper used by older call sites: a primary button.</summary>
        public static Button TextButton(Transform parent, string name, string key, Vector2 anchor, Vector2 position,
            Vector2 size, UnityAction onClick) => PrimaryButton(parent, name, key, anchor, position, size, onClick);

        private static Button CardButton(Transform parent, string name, string key, Vector2 anchor, Vector2 position,
            Vector2 size, UnityAction onClick, Color fill, bool outline)
        {
            size = Vector2.Max(size, new Vector2(MinTouchTarget, MinTouchTarget));
            // uGUI draws children over their parent, so an outlined button is an ink card with the fill inset.
            var image = Sprite(parent, name, IconFactory.RoundedRect, outline ? Palette.Ink : fill, anchor, position, size,
                raycast: true);
            InkShadow(image);
            Image face = image;
            if (outline)
            {
                face = Sprite(image.transform, "Fill", IconFactory.RoundedRect, fill, new Vector2(0.5f, 0.5f), Vector2.zero,
                    size);
                Stretch(face.rectTransform);
                face.rectTransform.offsetMin = new Vector2(5f, 5f);
                face.rectTransform.offsetMax = new Vector2(-5f, -5f);
            }

            var button = image.gameObject.AddComponent<Button>();
            button.targetGraphic = face;
            var colors = button.colors;
            colors.normalColor = Color.white;
            colors.highlightedColor = new Color(1f, 0.96f, 0.9f);
            colors.selectedColor = new Color(1f, 0.96f, 0.9f);
            colors.pressedColor = new Color(0.8f, 0.74f, 0.66f);
            colors.fadeDuration = 0.06f;
            button.colors = colors;
            image.gameObject.AddComponent<PressScale>();
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            var label = Label(image.transform, "Label", key, ButtonSize, TextAnchor.MiddleCenter, Palette.Ink, BoldFont);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(20f, 8f);
            label.rectTransform.offsetMax = new Vector2(-20f, -8f);
            return button;
        }

        /// <summary>
        /// Thumb control: ink disc with a warm-paper ring, a line icon and an optional short caption (spec §5:
        /// icon + localized verb). Returns the disc image, which is the raycast target.
        /// </summary>
        public static Image InkDisc(Transform parent, string name, Sprite icon, string captionKey, float diameter,
            Vector2 anchor, Vector2 position)
        {
            diameter = Mathf.Max(diameter, MinTouchTarget);
            var disc = Sprite(parent, name, IconFactory.Disc, WithAlpha(Palette.Ink, 0.6f), anchor, position,
                new Vector2(diameter, diameter), raycast: true);
            var ring = Sprite(disc.transform, "Ring", IconFactory.Ring(0.07f), WithAlpha(Palette.WarmPaper, 0.9f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(diameter, diameter));
            ring.raycastTarget = false;

            bool hasCaption = !string.IsNullOrEmpty(captionKey);
            float iconSize = diameter * (hasCaption ? 0.42f : 0.5f);
            Sprite(disc.transform, "Icon", icon, Palette.WarmPaper, new Vector2(0.5f, 0.5f),
                new Vector2(0f, hasCaption ? diameter * 0.09f : 0f), new Vector2(iconSize, iconSize));
            if (hasCaption)
            {
                var caption = Label(disc.transform, "Caption", captionKey, 26, TextAnchor.MiddleCenter, Palette.WarmPaper,
                    BoldFont);
                Place(caption.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -diameter * 0.26f),
                    new Vector2(diameter * 0.8f, 34f));
            }

            disc.gameObject.AddComponent<PressScale>();
            return disc;
        }
    }

    /// <summary>Visible press feedback for every tappable control: a quick squash while the finger is down.</summary>
    public sealed class PressScale : MonoBehaviour, UnityEngine.EventSystems.IPointerDownHandler,
        UnityEngine.EventSystems.IPointerUpHandler, UnityEngine.EventSystems.IPointerExitHandler
    {
        public float PressedScale = 0.9f;
        private bool _pressed;

        public void OnPointerDown(UnityEngine.EventSystems.PointerEventData eventData) => _pressed = true;

        public void OnPointerUp(UnityEngine.EventSystems.PointerEventData eventData) => _pressed = false;

        public void OnPointerExit(UnityEngine.EventSystems.PointerEventData eventData) => _pressed = false;

        private void OnDisable()
        {
            _pressed = false;
            transform.localScale = Vector3.one;
        }

        private void Update()
        {
            float target = _pressed ? PressedScale : 1f;
            float current = Mathf.MoveTowards(transform.localScale.x, target, Time.unscaledDeltaTime * 4f);
            transform.localScale = new Vector3(current, current, 1f);
        }
    }
}
