using SpookyStreet.Localization;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace SpookyStreet.UI
{
    /// <summary>
    /// Builds placeholder uGUI from code. Layout units are reference pixels on a 1334 × 750 landscape canvas
    /// scaled by height, so 96 units ≈ 48 pt on a 375-pt-tall iPhone (spec §5 minimum hit area; validate on device).
    /// </summary>
    public static class UiFactory
    {
        public static readonly Vector2 ReferenceResolution = new Vector2(1334f, 750f);
        public const float MinTouchTarget = 96f;

        private static Font s_font;
        private static Sprite s_circle;

        public static Font DefaultFont
        {
            get
            {
                if (s_font == null)
                {
                    s_font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                }

                return s_font;
            }
        }

        /// <summary>Anti-aliased circle generated at runtime so the HUD needs no imported texture.</summary>
        public static Sprite Circle
        {
            get
            {
                if (s_circle != null)
                {
                    return s_circle;
                }

                const int size = 128;
                var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
                {
                    name = "UiCircle",
                    wrapMode = TextureWrapMode.Clamp,
                    filterMode = FilterMode.Bilinear,
                };
                var pixels = new Color32[size * size];
                float r = size * 0.5f;
                for (int y = 0; y < size; y++)
                {
                    for (int x = 0; x < size; x++)
                    {
                        float dx = x + 0.5f - r;
                        float dy = y + 0.5f - r;
                        float alpha = Mathf.Clamp01(r - Mathf.Sqrt(dx * dx + dy * dy));
                        pixels[y * size + x] = new Color32(255, 255, 255, (byte)(alpha * 255f));
                    }
                }

                texture.SetPixels32(pixels);
                texture.Apply(false, true);
                s_circle = Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
                return s_circle;
            }
        }

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

        public static RectTransform Place(RectTransform rect, Vector2 anchor, Vector2 position, Vector2 size)
        {
            rect.anchorMin = anchor;
            rect.anchorMax = anchor;
            rect.pivot = new Vector2(0.5f, 0.5f);
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

        public static Text Label(Transform parent, string name, string key, int fontSize, TextAnchor alignment,
            Color color)
        {
            var rect = Rect(parent, name);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = DefaultFont;
            text.fontSize = fontSize;
            text.alignment = alignment;
            text.color = color;
            text.raycastTarget = false;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = Mathf.Max(12, fontSize / 2);
            text.resizeTextMaxSize = fontSize;
            if (!string.IsNullOrEmpty(key))
            {
                rect.gameObject.AddComponent<LocalizedText>().SetKey(key);
            }

            return text;
        }

        public static Button TextButton(Transform parent, string name, string key, Vector2 anchor, Vector2 position,
            Vector2 size, UnityAction onClick)
        {
            size = Vector2.Max(size, new Vector2(MinTouchTarget, MinTouchTarget));
            var rect = Place(Rect(parent, name), anchor, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = Palette.WarmPaper;
            var button = rect.gameObject.AddComponent<Button>();
            var colors = button.colors;
            colors.highlightedColor = Color.Lerp(Palette.WarmPaper, Palette.PumpkinAmber, 0.35f);
            colors.selectedColor = Palette.PumpkinAmber;
            colors.pressedColor = Palette.PumpkinAmber;
            button.colors = colors;
            if (onClick != null)
            {
                button.onClick.AddListener(onClick);
            }

            var label = Label(rect, "Label", key, 40, TextAnchor.MiddleCenter, Palette.Ink);
            Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(16f, 8f);
            label.rectTransform.offsetMax = new Vector2(-16f, -8f);
            return button;
        }

        public static Image RoundButtonVisual(Transform parent, string name, string key, Vector2 anchor, Vector2 position,
            float diameter)
        {
            diameter = Mathf.Max(diameter, MinTouchTarget);
            var rect = Place(Rect(parent, name), anchor, position, new Vector2(diameter, diameter));
            var image = rect.gameObject.AddComponent<Image>();
            image.sprite = Circle;
            image.color = new Color(Palette.WarmPaper.r, Palette.WarmPaper.g, Palette.WarmPaper.b, 0.55f);
            var label = Label(rect, "Label", key, 30, TextAnchor.MiddleCenter, Palette.Ink);
            Stretch(label.rectTransform);
            return image;
        }
    }
}
