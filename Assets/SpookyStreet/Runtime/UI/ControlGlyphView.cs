using SpookyStreet.App;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.Nonverbal;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpookyStreet.UI
{
    /// <summary>
    /// One control glyph in the craft style (D25: glyphs per control scheme): a bone key cap with the key's name
    /// (from the Input System binding, so it follows the keyboard), the four face buttons as a felt diamond with the
    /// bound one lit, a felt shoulder bumper, or the stick with its knob tilted. Touch glyphs draw nothing: on touch
    /// the on-screen control itself is the glyph. A stitched candy ring fills for held prompts. No raycasts.
    /// </summary>
    public sealed class ControlGlyphView : MonoBehaviour
    {
        private RectTransform _root;
        private Image _key;
        private Text _keyText;
        private RectTransform _face;
        private Image[] _faceButtons;
        private Image _shoulder;
        private RectTransform _stick;
        private RectTransform _knob;
        private Image _holdRing;
        private Image _backing;
        private float _size;

        /// <summary>
        /// A bone paper disc behind the face-button diamond (S004 F2, frame 77): the night-felt buttons vanish on dark
        /// felt (the notebook tray, the night sky), so prompts placed there carry it. Key caps and bumpers are paper already.
        /// </summary>
        public bool Backed
        {
            get => _backing != null && _backing.enabled;
            set
            {
                if (_backing != null)
                {
                    _backing.enabled = value;
                }
            }
        }

        public ControlGlyph Glyph { get; private set; }

        public bool HasGlyph => gameObject.activeSelf && !Glyph.IsTouch;

        /// <summary>The key cap's text (the key's display name), or null.</summary>
        public string KeyLabel => _key != null && _key.gameObject.activeSelf ? _keyText.text : null;

        /// <summary>The lit face or shoulder button, or None.</summary>
        public PadButton LitButton => HasGlyph ? Glyph.Pad : PadButton.None;

        public RectTransform Rect => _root;

        public static ControlGlyphView Create(Transform parent, string name, Vector2 anchor, Vector2 position, float size)
        {
            var root = UiFactory.Place(UiFactory.Rect(parent, name), anchor, position, new Vector2(size, size));
            var view = root.gameObject.AddComponent<ControlGlyphView>();
            view._root = root;
            view._size = size;
            view.Build();
            root.gameObject.SetActive(false);
            return view;
        }

        private void Build()
        {
            var centre = new Vector2(0.5f, 0.5f);
            _holdRing = UiFactory.Sprite(_root, "HoldRing", UiFactory.Baked(UiSpriteCatalog.StitchRing),
                Palette.CandyCornYellow, centre, Vector2.zero, Vector2.one * _size * 1.32f);
            _holdRing.type = Image.Type.Filled;
            _holdRing.fillMethod = Image.FillMethod.Radial360;
            _holdRing.fillOrigin = (int)Image.Origin360.Top;

            // In-play paper is dimmed a little below bone (docs/ui-language.md brightness rule).
            _key = UiFactory.Sprite(_root, "KeyCap", UiFactory.Baked(UiSpriteCatalog.GlyphKey), UiFactory.PlayPaper, centre,
                Vector2.zero, Vector2.one * _size);
            UiFactory.InkShadow(_key, new Vector2(3f, -4f), 0.45f);
            _keyText = UiFactory.Label(_key.transform, "Label", null, Mathf.RoundToInt(_size * 0.42f),
                TextAnchor.MiddleCenter, UiFactory.Ink, UiFactory.BoldFont);
            UiFactory.Stretch(_keyText.rectTransform);
            _keyText.rectTransform.offsetMin = new Vector2(10f, _size * 0.16f);
            _keyText.rectTransform.offsetMax = new Vector2(-10f, -4f);

            _face = UiFactory.Place(UiFactory.Rect(_root, "FaceButtons"), centre, Vector2.zero, Vector2.one * _size);
            _backing = UiFactory.Sprite(_face, "Backing", UiFactory.Baked(UiSpriteCatalog.FeltDiscBone), UiFactory.PlayPaper,
                centre, Vector2.zero, Vector2.one * _size * 1.12f);
            UiFactory.InkShadow(_backing, new Vector2(2f, -3f), 0.45f);
            _backing.enabled = false;
            _faceButtons = new Image[4];
            var spots = new[] { new Vector2(0f, -0.3f), new Vector2(0.3f, 0f), new Vector2(-0.3f, 0f), new Vector2(0f, 0.3f) };
            for (int i = 0; i < 4; i++)
            {
                _faceButtons[i] = UiFactory.Sprite(_face, "Button" + i, UiFactory.Baked(UiSpriteCatalog.FeltDiscNight),
                    Color.white, centre, spots[i] * _size, Vector2.one * _size * 0.36f);
                UiFactory.Sprite(_faceButtons[i].transform, "Ring", UiFactory.Baked(UiSpriteCatalog.StitchRing),
                    UiFactory.WithAlpha(Palette.WarmPaper, 0.85f), centre, Vector2.zero, Vector2.one * _size * 0.36f);
            }

            // A bone paper bumper (reads on orange and on night felt), the inner end rounder than the outer.
            _shoulder = UiFactory.Sprite(_root, "Shoulder", UiFactory.Baked(UiSpriteCatalog.PaperTag),
                UiFactory.PlayPaper, centre, Vector2.zero, new Vector2(_size * 1.2f, _size * 0.52f));
            UiFactory.Sprite(_shoulder.transform, "Lit", IconFactory.Disc, Palette.JackOrange, new Vector2(0.78f, 0.5f),
                Vector2.zero, Vector2.one * _size * 0.2f);
            UiFactory.InkShadow(_shoulder, new Vector2(3f, -4f), 0.45f);

            _stick = UiFactory.Place(UiFactory.Rect(_root, "Stick"), centre, Vector2.zero, Vector2.one * _size);
            var stickBase = UiFactory.Sprite(_stick, "Base", UiFactory.Baked(UiSpriteCatalog.FeltDiscNight), Color.white,
                centre, Vector2.zero, Vector2.one * _size);
            UiFactory.Sprite(stickBase.transform, "Ring", UiFactory.Baked(UiSpriteCatalog.StitchRing),
                UiFactory.WithAlpha(Palette.WarmPaper, 0.85f), centre, Vector2.zero, Vector2.one * _size * 0.94f);
            _knob = UiFactory.Sprite(_stick, "Knob", UiFactory.Baked(UiSpriteCatalog.FeltDiscBone), Color.white, centre,
                Vector2.zero, Vector2.one * _size * 0.5f).rectTransform;
            UiFactory.InkShadow(_knob.GetComponent<Image>(), new Vector2(2f, -3f), 0.5f);

            foreach (var graphic in _root.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }
        }

        /// <summary>Shows <paramref name="glyph"/>; touch glyphs hide the view (the on-screen control is the glyph).</summary>
        public void Show(ControlGlyph glyph)
        {
            Glyph = glyph;
            bool visible = !glyph.IsTouch;
            gameObject.SetActive(visible);
            if (!visible)
            {
                return;
            }

            _key.gameObject.SetActive(glyph.Shape == GlyphShape.Key);
            _face.gameObject.SetActive(glyph.Shape == GlyphShape.PadFace);
            _shoulder.gameObject.SetActive(glyph.Shape == GlyphShape.PadShoulder);
            _stick.gameObject.SetActive(glyph.Shape == GlyphShape.PadStick);
            _holdRing.gameObject.SetActive(glyph.Hold);
            _holdRing.fillAmount = 0f;
            switch (glyph.Shape)
            {
                case GlyphShape.Key:
                    _keyText.text = KeyName(glyph.BindingPath);
                    float wide = Mathf.Max(_size, _keyText.preferredWidth + 44f);
                    _key.rectTransform.sizeDelta = new Vector2(Mathf.Min(wide, _size * 3f), _size);
                    break;
                case GlyphShape.PadFace:
                    int lit = glyph.Pad == PadButton.South ? 0 : glyph.Pad == PadButton.East ? 1
                        : glyph.Pad == PadButton.West ? 2 : glyph.Pad == PadButton.North ? 3 : -1;
                    for (int i = 0; i < 4; i++)
                    {
                        bool on = i == lit;
                        _faceButtons[i].sprite = UiFactory.Baked(on ? UiSpriteCatalog.FeltDiscOrange : UiSpriteCatalog.FeltDiscNight);
                        _faceButtons[i].rectTransform.localScale = Vector3.one * (on ? 1.18f : 0.92f);
                        _faceButtons[i].color = on ? Color.white : UiFactory.WithAlpha(Color.white, 0.8f);
                    }

                    break;
                case GlyphShape.PadShoulder:
                    _shoulder.rectTransform.localScale = new Vector3(glyph.Pad == PadButton.RightShoulder ? -1f : 1f, 1f, 1f);
                    _shoulder.rectTransform.localRotation =
                        Quaternion.Euler(0f, 0f, glyph.Pad == PadButton.RightShoulder ? -8f : 8f);
                    break;
                case GlyphShape.PadStick:
                    StickDirection = Vector2.zero;
                    break;
            }
        }

        /// <summary>The key's display name from its binding path ("Space", "E", "Tab").</summary>
        public static string KeyName(string bindingPath)
        {
            if (string.IsNullOrEmpty(bindingPath))
            {
                return string.Empty;
            }

            string name = InputControlPath.ToHumanReadableString(bindingPath,
                InputControlPath.HumanReadableStringOptions.OmitDevice);
            return name.Length == 1 ? name.ToUpperInvariant() : name;
        }

        /// <summary>The stick glyph's knob tilt (unit circle, x right, y up).</summary>
        public Vector2 StickDirection
        {
            set
            {
                if (_knob != null)
                {
                    _knob.anchoredPosition = Vector2.ClampMagnitude(value, 1f) * _size * 0.26f;
                }
            }
        }

        /// <summary>Held prompts: the candy ring's fill (0..1).</summary>
        public float HoldProgress
        {
            set
            {
                if (_holdRing != null)
                {
                    _holdRing.fillAmount = Mathf.Clamp01(value);
                }
            }
        }

        /// <summary>Pulse scale (1 = rest).</summary>
        public float Pulse
        {
            set => _root.localScale = Vector3.one * value;
        }
    }
}
