using System;
using SpookyStreet.Core.Art;
using SpookyStreet.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace SpookyStreet.UI
{
    /// <summary>
    /// The cinematic frame shared by the memory replay (D23) and the cutscenes (D28): ink letterbox bars that slide in
    /// with a weight, an optional pencil tint and soft vignette, a clear tap catcher over the world, and the round skip
    /// disc at the top right of the safe area with its small caption in the bar and a candy progress ring that fills
    /// over the scene (how long is left, without numbers). Pure presentation: the owner decides what a tap means.
    /// </summary>
    public sealed class CinematicFrame
    {
        private readonly Image _tint;
        private readonly Image _vignette;
        private readonly RectTransform _topBar;
        private readonly RectTransform _bottomBar;
        private readonly Image _ring;
        private readonly Color _tintColor;
        private readonly float _tintAlpha;
        private readonly float _vignetteAlpha;
        private float _skipScale = 1f;

        private CinematicFrame(RectTransform overlay, float barHeight, Color tint, float tintAlpha, float vignetteAlpha)
        {
            Overlay = overlay;
            BarHeight = barHeight;
            _tintColor = tint;
            _tintAlpha = tintAlpha;
            _vignetteAlpha = vignetteAlpha;
            _tint = UiFactory.Panel(overlay, "PencilTint", UiFactory.WithAlpha(tint, 0f), raycast: false);
            _vignette = UiFactory.Panel(overlay, "Vignette", UiFactory.WithAlpha(Palette.NightViolet, 0f), raycast: false);
            _vignette.sprite = EdgeFade();
            _topBar = Bar("TopBar", new Vector2(0f, 1f));
            _bottomBar = Bar("BottomBar", new Vector2(0f, 0f));

            var catcher = UiFactory.Panel(overlay, "TapToSkip", new Color(0f, 0f, 0f, 0f));
            var catcherButton = catcher.gameObject.AddComponent<Button>();
            catcherButton.transition = Selectable.Transition.None;
            catcherButton.onClick.AddListener(() => Tapped?.Invoke());
            TapCatcher = catcherButton;

            SafeArea = UiFactory.Stretch(UiFactory.Rect(overlay, "SafeArea"));
            SafeArea.gameObject.AddComponent<SafeAreaFitter>();
        }

        /// <summary>The overlay root (children drawn over the world, under the skip disc).</summary>
        public RectTransform Overlay { get; }

        /// <summary>The safe-area rect inside the overlay (the skip disc lives here).</summary>
        public RectTransform SafeArea { get; }

        public float BarHeight { get; }

        public Button TapCatcher { get; }

        public Button SkipButton { get; private set; }

        /// <summary>The skip disc's small caption in the letterbox.</summary>
        public Text SkipCaption { get; private set; }

        public RectTransform TopBar => _topBar;

        public RectTransform BottomBar => _bottomBar;

        /// <summary>A tap anywhere over the world (not on the disc).</summary>
        public event Action Tapped;

        /// <summary>The skip disc was pressed.</summary>
        public event Action SkipPressed;

        public bool Visible => Overlay.gameObject.activeSelf;

        /// <summary>0..1 of the candy ring around the skip disc.</summary>
        public float Progress
        {
            get => _ring != null ? _ring.fillAmount : 0f;
            set
            {
                if (_ring != null)
                {
                    _ring.fillAmount = Mathf.Clamp01(value);
                }
            }
        }

        /// <summary>Extra scale on the skip disc (it grows when a first press arms the skip).</summary>
        public float SkipEmphasis
        {
            get => _skipScale;
            set => _skipScale = Mathf.Max(0.5f, value);
        }

        /// <summary>
        /// Builds the frame under <paramref name="canvasRoot"/>: bars <paramref name="barHeight"/> tall at full weight, the
        /// tint and vignette at the given alphas (0 leaves them off), the skip disc captioned with <paramref name="skipKey"/>.
        /// </summary>
        public static CinematicFrame Create(RectTransform canvasRoot, string name, float barHeight, string skipKey, Color tint,
            float tintAlpha, float vignetteAlpha)
        {
            var overlay = UiFactory.Stretch(UiFactory.Rect(canvasRoot, name));
            var frame = new CinematicFrame(overlay, barHeight, tint, tintAlpha, vignetteAlpha);
            frame.BuildSkip(skipKey);
            overlay.gameObject.SetActive(false);
            return frame;
        }

        private void BuildSkip(string skipKey)
        {
            var disc = UiFactory.FeltDisc(SafeArea, "SkipDisc", UiSpriteCatalog.FeltDiscNight, Palette.WarmPaper,
                UiSpriteCatalog.IconSkip, Palette.WarmPaper, null, UiFactory.SystemDiscDiameter, Vector2.one,
                new Vector2(-62f, -60f - BarHeight * 0.5f), 0.92f);
            var button = disc.gameObject.AddComponent<Button>();
            button.targetGraphic = disc;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => SkipPressed?.Invoke());
            SkipButton = button;
            // Its name, as the accessibility name and as a small caption in the letterbox beside the disc.
            AccessibleName.Set(disc.gameObject, skipKey);
            var caption = UiFactory.Label(disc.transform, "Caption", skipKey, 26, TextAnchor.MiddleRight,
                UiFactory.WithAlpha(Palette.WarmPaper, 0.85f), UiFactory.BoldFont);
            UiFactory.Place(caption.rectTransform, new Vector2(0f, 0.5f), new Vector2(-18f, 0f), new Vector2(300f, 40f),
                new Vector2(1f, 0.5f));
            SkipCaption = caption;
            var ring = UiFactory.Sprite(disc.transform, "Progress", UiFactory.Baked(UiSpriteCatalog.StitchRing),
                Palette.CandyCornYellow, new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.one * (UiFactory.SystemDiscDiameter + 16f));
            ring.type = Image.Type.Filled;
            ring.fillMethod = Image.FillMethod.Radial360;
            ring.fillOrigin = (int)Image.Origin360.Top;
            ring.fillClockwise = true;
            ring.raycastTarget = false;
            _ring = ring;
        }

        /// <summary>Changes the skip caption's key (e.g. "Press again to skip" while armed).</summary>
        public void SetSkipCaption(string key)
        {
            var text = SkipCaption != null ? SkipCaption.GetComponent<LocalizedText>() : null;
            if (text != null && text.Key != key)
            {
                text.SetKey(key);
            }
        }

        public void Show()
        {
            Overlay.gameObject.SetActive(true);
            Overlay.SetAsLastSibling();
        }

        public void Hide()
        {
            SetLook(0f);
            Overlay.gameObject.SetActive(false);
        }

        /// <summary>Lays the frame out for weight <paramref name="w"/> (0 = gone, 1 = full letterbox, tint and vignette).</summary>
        public void SetLook(float w)
        {
            _tint.color = UiFactory.WithAlpha(_tintColor, _tintAlpha * w);
            _tint.enabled = _tintAlpha > 0f;
            _vignette.color = UiFactory.WithAlpha(Palette.NightViolet, _vignetteAlpha * w);
            _vignette.enabled = _vignetteAlpha > 0f;
            _topBar.sizeDelta = new Vector2(0f, BarHeight * w);
            _bottomBar.sizeDelta = new Vector2(0f, BarHeight * w);
            if (SkipButton != null)
            {
                SkipButton.transform.localScale = Vector3.one * (Mathf.Lerp(0.6f, 1f, w) * _skipScale);
            }
        }

        private RectTransform Bar(string name, Vector2 anchor)
        {
            var bar = UiFactory.Panel(Overlay, name, Palette.InkBlack, raycast: false).rectTransform;
            bar.anchorMin = anchor;
            bar.anchorMax = new Vector2(1f, anchor.y);
            bar.pivot = new Vector2(0.5f, anchor.y);
            bar.sizeDelta = new Vector2(0f, BarHeight);
            return bar;
        }

        private static Sprite s_edge;

        /// <summary>Clear in the middle, opaque at the edges (the cinematic vignette).</summary>
        private static Sprite EdgeFade()
        {
            if (s_edge != null)
            {
                return s_edge;
            }

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Clamp01((Mathf.Sqrt(dx * dx * 0.7f + dy * dy) - 0.55f) / 0.6f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, d * d));
                }
            }

            texture.Apply();
            s_edge = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
            return s_edge;
        }
    }
}
