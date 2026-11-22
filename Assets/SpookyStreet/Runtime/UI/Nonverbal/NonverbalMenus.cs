using System;
using SpookyStreet.Controls;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.Nonverbal;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using NvKeys = SpookyStreet.Core.Localization.LocalizationKeys.NonverbalUi;

namespace SpookyStreet.UI.Nonverbal
{
    /// <summary>
    /// The non-verbal UX menus (docs/nonverbal-ux.md §2.6, §8; D23.4, D23.5), on their own canvas above the title,
    /// pause and notices: the parent gate (two marked felt spots held together for 3 s, a candy ring fills; a fresh
    /// <see cref="ParentGate"/> per guarded action) and the settings card with the <c>Helper</c> toggle, which sits
    /// behind the gate. One hybrid reading mode (D23.1): no first-launch choice and no words toggle. Everything runs
    /// on unscaled time, so it works on the pause screen.
    /// </summary>
    public sealed class NonverbalMenus : MonoBehaviour
    {
        public const int SortingOrder = 16;

        private static readonly Color Graphite = new Color32(0x4A, 0x44, 0x58, 0xFF);

        private RectTransform _root;
        private GameObject _gatePanel;
        private RectTransform _gateArea;
        private Image[] _spots;
        private Image _gateRing;
        private Text _gateHint;
        private ControlGlyphView[] _gateGlyphs;
        private ControlScheme? _gateScheme;
        private GameObject _settings;
        private Image _helperTick;
        private Action _onGatePassed;

        public Canvas Canvas { get; private set; }

        public Button GateCloseButton { get; private set; }
        public Button SettingsCloseButton { get; private set; }
        public Button HelperToggle { get; private set; }

        /// <summary>The gate guarding the action in progress (null when no gate is up).</summary>
        public ParentGate Gate { get; private set; }

        public bool GateOpen => _gatePanel.activeSelf;
        public bool SettingsOpen => _settings.activeSelf;

        /// <summary>Settings the toggles show and change (set by the owner).</summary>
        public NonverbalSettings Settings { get; set; }

        /// <summary>Called after a toggle changed <see cref="Settings"/> (save it).</summary>
        public Action<NonverbalSettings> SettingsChanged { get; set; }

        public static NonverbalMenus Create(Transform parent)
        {
            var canvas = UiFactory.CreateCanvas("NonverbalMenus", SortingOrder);
            canvas.transform.SetParent(parent, false);
            var menus = canvas.gameObject.AddComponent<NonverbalMenus>();
            menus.Canvas = canvas;
            menus._root = (RectTransform)canvas.transform;
            menus.BuildGate();
            menus.BuildSettings();
            return menus;
        }

        // ------------------------------------------------------------------------------------- parent gate

        private void BuildGate()
        {
            var scrim = UiFactory.Panel(_root, "ParentGate", UiFactory.WithAlpha(Palette.NightViolet, 0.94f));
            _gatePanel = scrim.gameObject;
            var content = UiFactory.Stretch(UiFactory.Rect(scrim.transform, "Content"));
            content.gameObject.AddComponent<SafeAreaFitter>();
            _gateArea = UiFactory.Stretch(UiFactory.Rect(content, "GateArea"));
            _gateArea.offsetMin = new Vector2(40f, 40f);
            _gateArea.offsetMax = new Vector2(-40f, -40f);

            var hint = UiFactory.Label(content, "Hint", NvKeys.ParentGateHint, 34, TextAnchor.MiddleCenter, Palette.WarmPaper,
                UiFactory.BoldFont);
            UiFactory.Place(hint.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(0f, -120f), new Vector2(760f, 60f));
            _gateHint = hint;
            _gateRing = UiFactory.Sprite(content, "Progress", UiFactory.Baked(UiSpriteCatalog.StitchRing),
                Palette.CandyCornYellow, new Vector2(0.5f, 0.5f), new Vector2(0f, 30f), new Vector2(170f, 170f));
            _gateRing.type = Image.Type.Filled;
            _gateRing.fillMethod = Image.FillMethod.Radial360;
            _gateRing.fillOrigin = (int)Image.Origin360.Top;
            var track = UiFactory.Sprite(_gateRing.transform, "Track", UiFactory.Baked(UiSpriteCatalog.StitchRing),
                UiFactory.WithAlpha(Palette.WarmPaper, 0.25f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(170f, 170f));
            track.transform.SetAsFirstSibling();

            _spots = new Image[2];
            for (int i = 0; i < 2; i++)
            {
                _spots[i] = UiFactory.FeltDisc(_gateArea, "Spot" + i, UiSpriteCatalog.FeltDiscOrange, Palette.CandyCornYellow,
                    UiSpriteCatalog.IconCheck, UiFactory.WithAlpha(UiFactory.Ink, 0f), null, 150f, Vector2.zero,
                    Vector2.zero, 1f);
                _spots[i].raycastTarget = false;
            }

            // D25: with a gamepad or keyboard each spot carries its button (a shoulder bumper, or the Q and P keys).
            _gateGlyphs = new ControlGlyphView[2];
            for (int i = 0; i < 2; i++)
            {
                _gateGlyphs[i] = ControlGlyphView.Create(_spots[i].transform, "Glyph", new Vector2(0.5f, 0.5f),
                    Vector2.zero, 76f);
            }

            var close = UiFactory.FeltDisc(content, "CloseGate", UiSpriteCatalog.FeltDiscNight, Palette.WarmPaper,
                UiSpriteCatalog.IconClose, Palette.WarmPaper, null, UiFactory.SystemDiscDiameter, new Vector2(0f, 1f),
                new Vector2(70f, -66f), 1f);
            GateCloseButton = close.gameObject.AddComponent<Button>();
            GateCloseButton.transition = Selectable.Transition.None;
            GateCloseButton.onClick.AddListener(CancelGate);
            _gatePanel.SetActive(false);
        }

        /// <summary>
        /// Puts up a fresh parent gate (one per guarded action); <paramref name="onPassed"/> runs only when both spots
        /// were held for the full 3 s. Release resets; the gate closes itself after its timeout.
        /// </summary>
        public ParentGate RequireParent(Action onPassed)
        {
            Gate = new ParentGate();
            _buttonA = false;
            _buttonB = false;
            _onGatePassed = onPassed;
            _gatePanel.SetActive(true);
            Canvas.ForceUpdateCanvases();
            PlaceSpot(_spots[0], Gate.ZoneA);
            PlaceSpot(_spots[1], Gate.ZoneB);
            _gateScheme = null;
            ApplyGateScheme();
            RefreshGate();
            return Gate;
        }

        /// <summary>The gate's line and the spots' glyphs follow the active control scheme (touch, gamepad, keyboard).</summary>
        private void ApplyGateScheme()
        {
            var scheme = ControlSchemeTracker.Current;
            if (_gateScheme == scheme)
            {
                return;
            }

            _gateScheme = scheme;
            var text = _gateHint != null ? _gateHint.GetComponent<SpookyStreet.Localization.LocalizedText>() : null;
            if (text != null)
            {
                text.SetKey(ControlGlyphs.ParentGateHintKey(scheme));
            }

            _gateGlyphs[0].Show(ControlGlyphs.For(scheme, ControlPrompt.GateA));
            _gateGlyphs[1].Show(ControlGlyphs.For(scheme, ControlPrompt.GateB));
        }

        /// <summary>The gate line's key now (tests).</summary>
        public string GateHintKey => _gateHint != null
            ? _gateHint.GetComponent<SpookyStreet.Localization.LocalizedText>()?.Key
            : null;

        /// <summary>The spots' glyphs (tests).</summary>
        public ControlGlyphView GateGlyph(int spot) => _gateGlyphs[spot];

        private void PlaceSpot(Image spot, GateZone zone)
        {
            var size = _gateArea.rect.size;
            var rect = spot.rectTransform;
            rect.anchoredPosition = new Vector2(zone.CenterX * size.x, zone.CenterY * size.y);
        }

        public void CancelGate()
        {
            Gate?.Cancel();
            CloseGate();
        }

        private void CloseGate()
        {
            Gate?.ReleaseAll();
            Gate = null;
            _buttonA = false;
            _buttonB = false;
            _onGatePassed = null;
            _gatePanel.SetActive(false);
        }

        /// <summary>Feeds a touch in screen pixels to the gate (also used by tests with the mouse or fake touches).</summary>
        public void FeedTouch(int id, Vector2 screen, bool down, bool up)
        {
            if (Gate == null)
            {
                return;
            }

            if (up)
            {
                Gate.TouchUp(id);
                return;
            }

            var cam = Canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : Canvas.worldCamera;
            if (!RectTransformUtility.ScreenPointToLocalPointInRectangle(_gateArea, screen, cam, out var local))
            {
                return;
            }

            var r = _gateArea.rect;
            float x = (local.x - r.xMin) / r.width;
            float y = (local.y - r.yMin) / r.height;
            if (down)
            {
                Gate.TouchDown(id, x, y);
            }
            else
            {
                Gate.TouchMove(id, x, y);
            }
        }

        private void ReadTouches()
        {
            var screen = Touchscreen.current;
            if (screen == null)
            {
                return;
            }

            foreach (var touch in screen.touches)
            {
                int id = touch.touchId.ReadValue();
                var phase = touch.phase.ReadValue();
                switch (phase)
                {
                    case UnityEngine.InputSystem.TouchPhase.Began:
                        FeedTouch(id, touch.position.ReadValue(), true, false);
                        break;
                    case UnityEngine.InputSystem.TouchPhase.Moved:
                    case UnityEngine.InputSystem.TouchPhase.Stationary:
                        FeedTouch(id, touch.position.ReadValue(), false, false);
                        break;
                    case UnityEngine.InputSystem.TouchPhase.Ended:
                    case UnityEngine.InputSystem.TouchPhase.Canceled:
                        FeedTouch(id, touch.position.ReadValue(), false, true);
                        break;
                }
            }
        }

        /// <summary>Virtual touch IDs for the button path (outside any real touch ID range).</summary>
        public const int ButtonTouchA = 100001;
        public const int ButtonTouchB = 100002;

        /// <summary>
        /// Portability (D25): with a gamepad or keyboard the two spots are two buttons held together, through the same
        /// gate logic: both shoulder buttons, or Q and P (far apart on the keyboard). Each held button presses its spot.
        /// </summary>
        public void FeedButtons(bool holdA, bool holdB)
        {
            if (Gate == null)
            {
                return;
            }

            FeedButton(ButtonTouchA, holdA, Gate.ZoneA);
            FeedButton(ButtonTouchB, holdB, Gate.ZoneB);
        }

        private bool _buttonA;
        private bool _buttonB;

        private void FeedButton(int id, bool held, GateZone zone)
        {
            ref bool was = ref id == ButtonTouchA ? ref _buttonA : ref _buttonB;
            if (held && !was)
            {
                Gate.TouchDown(id, zone.CenterX, zone.CenterY);
            }
            else if (!held && was)
            {
                Gate.TouchUp(id);
            }

            was = held;
        }

        private void ReadButtons()
        {
            var pad = Gamepad.current;
            var keys = Keyboard.current;
            bool a = (pad != null && pad.leftShoulder.isPressed) || (keys != null && keys.qKey.isPressed);
            bool b = (pad != null && pad.rightShoulder.isPressed) || (keys != null && keys.pKey.isPressed);
            if (a || b || _buttonA || _buttonB)
            {
                FeedButtons(a, b);
            }
        }

        private void RefreshGate()
        {
            if (Gate == null)
            {
                return;
            }

            _gateRing.fillAmount = Gate.Progress;
            for (int i = 0; i < 2; i++)
            {
                bool held = Gate.IsZoneHeld(i);
                _spots[i].rectTransform.localScale = Vector3.one * (held ? 0.92f : 1f);
                _spots[i].color = held ? new Color(0.85f, 0.85f, 0.85f, 1f) : Color.white;
            }
        }

        // ------------------------------------------------------------------------------------- settings

        private void BuildSettings()
        {
            var scrim = UiFactory.Panel(_root, "NonverbalSettings", UiFactory.WithAlpha(Palette.NightViolet, 0.8f));
            _settings = scrim.gameObject;
            var content = UiFactory.Stretch(UiFactory.Rect(scrim.transform, "Content"));
            content.gameObject.AddComponent<SafeAreaFitter>();
            var card = UiFactory.PaperCard(content, "SettingsCard", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(720f, 300f), -0.8f);
            var gear = UiFactory.Sprite(card.transform, "Gear", UiFactory.Baked(UiSpriteCatalog.IconGear), UiFactory.Ink,
                new Vector2(0.5f, 1f), new Vector2(0f, -66f), new Vector2(72f, 72f));
            gear.preserveAspect = true;
            HelperToggle = Row(card.transform, "HelperRow", "pg.hands", NonverbalSettings.HelperLabelKey, -180f,
                ToggleHelper, out _helperTick);
            var close = UiFactory.FeltDisc(card.transform, "CloseSettings", UiSpriteCatalog.FeltDiscNight, Palette.WarmPaper,
                UiSpriteCatalog.IconClose, Palette.WarmPaper, null, UiFactory.SystemDiscDiameter, Vector2.one,
                new Vector2(-20f, -20f), 1f);
            SettingsCloseButton = close.gameObject.AddComponent<Button>();
            SettingsCloseButton.transition = Selectable.Transition.None;
            SettingsCloseButton.onClick.AddListener(() => _settings.SetActive(false));
            _settings.SetActive(false);
        }

        private static Button Row(Transform card, string name, string art, string key, float y,
            UnityEngine.Events.UnityAction onClick, out Image tick)
        {
            var row = UiFactory.Rect(card, name);
            UiFactory.Place(row, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(620f, 104f));
            var icon = UiFactory.Sprite(row, "Picture", PuppetAtlas.Get(art), Color.white, new Vector2(0f, 0.5f),
                new Vector2(52f, 0f), new Vector2(96f, 96f));
            icon.preserveAspect = true;
            var label = UiFactory.Label(row, "Label", key, 34, TextAnchor.MiddleLeft, UiFactory.Ink, UiFactory.BoldFont);
            UiFactory.Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(120f, 0f), new Vector2(360f, 70f),
                new Vector2(0f, 0.5f));
            // The switch: a felt disc that shows a green check when on, an empty bone disc when off (shape, not colour).
            var disc = UiFactory.FeltDisc(row, "Switch", UiSpriteCatalog.FeltDiscBone, Palette.HauntedPurple,
                UiSpriteCatalog.IconCheck, new Color32(0x8D, 0xBF, 0x3F, 0xFF), null, 100f, new Vector2(1f, 0.5f),
                new Vector2(-56f, 0f), 1f);
            tick = disc.transform.Find("Icon").GetComponent<Image>();
            var button = disc.gameObject.AddComponent<Button>();
            button.targetGraphic = disc;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(onClick);
            return button;
        }

        /// <summary>The settings card with the Helper toggle (call from behind <see cref="RequireParent"/>).</summary>
        public void OpenSettings()
        {
            _settings.SetActive(true);
            RefreshSettings();
        }

        public void CloseSettings() => _settings.SetActive(false);

        private void ToggleHelper()
        {
            if (Settings == null)
            {
                return;
            }

            Settings.HelperAutoHints = !Settings.HelperAutoHints;
            RefreshSettings();
            SettingsChanged?.Invoke(Settings);
        }

        private void RefreshSettings()
        {
            _helperTick.enabled = Settings != null && Settings.HelperAutoHints;
        }

        /// <summary>Closes every menu (back to title from pause, scene teardown).</summary>
        public void CloseAll()
        {
            CloseGate();
            _settings.SetActive(false);
        }

        private void Update()
        {
            if (Gate == null)
            {
                return;
            }

            ApplyGateScheme();
            ReadTouches();
            ReadButtons();
            Gate.Update(Time.unscaledDeltaTime);
            RefreshGate();
            if (Gate.State == ParentGateState.Passed)
            {
                var passed = _onGatePassed;
                CloseGate();
                passed?.Invoke();
            }
            else if (Gate.IsFinished)
            {
                CloseGate();
            }
        }
    }
}
