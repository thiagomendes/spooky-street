using System;
using SpookyStreet.Controls;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Core.Parent;
using SpookyStreet.Core.Save;
using SpookyStreet.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using NvKeys = SpookyStreet.Core.Localization.LocalizationKeys.NonverbalUi;
using ParentKeys = SpookyStreet.Core.Localization.LocalizationKeys.Parent;

namespace SpookyStreet.UI.Nonverbal
{
    /// <summary>
    /// The non-verbal UX menus (docs/nonverbal-ux.md §2.6, §8; D23.4, D23.5), on their own canvas above the title,
    /// pause and notices: the parent gate (two marked felt spots held together for 3 s, a candy ring fills; a fresh
    /// <see cref="ParentGate"/> per guarded action) and the settings card with the <c>Helper</c> toggle and the music,
    /// sounds and voices volume sliders (S003), which sits behind the gate. S004 stream E: the card also holds every
    /// stored accessibility and control setting (<see cref="SettingsCard"/>) in three columns, and the Language, For
    /// grown-ups and Credits entries. One hybrid reading mode (D23.1): no first-launch choice and no words toggle.
    /// Everything runs on unscaled time, so it works on the pause screen.
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

        /// <summary>The player's preferences the volume sliders show and change (set by the game's audio).</summary>
        public PreferencesData Preferences { get; set; }

        /// <summary>Called while a volume slider moves (apply it; the owner saves it once the slider rests).</summary>
        public Action<PreferencesData> PreferencesChanged { get; set; }

        /// <summary>Music volume (0–1, the <c>MusicVol</c> bus).</summary>
        public Slider MusicSlider { get; private set; }

        /// <summary>Sound effects volume (0–1, the <c>SFXVol</c> bus; the night bed follows it).</summary>
        public Slider SoundsSlider { get; private set; }

        /// <summary>Voices volume (0–1, the <c>VoiceVol</c> bus).</summary>
        public Slider VoicesSlider { get; private set; }

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

            // S004 F2: Back (Escape, East) cancels the gate on every scheme, and the gate sits on the screen stack, so
            // nothing under it (the pause card, the title, the language screen) answers a press while it is up.
            GateNavigation = UiFocusScreen.Attach(_gatePanel, ScreenFocus.Gate(), content);
            GateNavigation.Bind(ScreenFocus.GateClose, GateCloseButton);
            _gatePanel.SetActive(false);
        }

        /// <summary>Gamepad and keyboard on the gate: its close disc, which Back presses.</summary>
        public UiFocusScreen GateNavigation { get; private set; }

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

        /// <summary>The card's size before it is fitted to the safe area (any aspect ratio, D25).</summary>
        public static readonly Vector2 CardSize = new Vector2(1420f, 650f);

        private const float ColumnWidth = 440f;
        private const float RowHeight = 100f;
        private const float RowsTop = -150f;

        private readonly System.Collections.Generic.Dictionary<string, Image> _ticks =
            new System.Collections.Generic.Dictionary<string, Image>();

        private readonly System.Collections.Generic.Dictionary<string, Slider> _sliders =
            new System.Collections.Generic.Dictionary<string, Slider>();

        private readonly System.Collections.Generic.Dictionary<string, Button> _switches =
            new System.Collections.Generic.Dictionary<string, Button>();

        private RectTransform _card;

        /// <summary>Gamepad and keyboard on the settings card (F1).</summary>
        public UiFocusScreen SettingsNavigation { get; private set; }

        /// <summary>The settings card (tests, captures).</summary>
        public RectTransform SettingsCardRect => _card;

        /// <summary>Opens the language screen (set by the game; the card hides while it is up).</summary>
        public Action OpenLanguage { get; set; }

        /// <summary>Opens the "For grown-ups" page (set by the game).</summary>
        public Action OpenGrownUps { get; set; }

        /// <summary>Opens the credits (set by the game).</summary>
        public Action OpenCredits { get; set; }

        public Button LanguageEntry { get; private set; }

        public Button GrownUpsEntry { get; private set; }

        public Button CreditsEntry { get; private set; }

        /// <summary>A preference switch by its <see cref="SettingsCard"/> id (tests).</summary>
        public Button Switch(string id) => _switches.TryGetValue(id, out var b) ? b : null;

        /// <summary>A switch shows its check (tests).</summary>
        public bool SwitchShowsOn(string id) => _ticks.TryGetValue(id, out var t) && t.enabled;

        /// <summary>A slider by its <see cref="SettingsCard"/> id (tests).</summary>
        public Slider SliderFor(string id) => _sliders.TryGetValue(id, out var s) ? s : null;

        private void BuildSettings()
        {
            var scrim = UiFactory.Panel(_root, "NonverbalSettings", UiFactory.WithAlpha(Palette.NightViolet, 0.86f));
            _settings = scrim.gameObject;
            var content = UiFactory.Stretch(UiFactory.Rect(scrim.transform, "Content"));
            content.gameObject.AddComponent<SafeAreaFitter>();
            var card = UiFactory.PaperCard(content, "SettingsCard", new Vector2(0.5f, 0.5f), Vector2.zero, CardSize, -0.4f);
            _card = card.rectTransform;
            UiFitInside.Attach(_card, CardSize, 16f);

            var gear = UiFactory.Sprite(card.transform, "Gear", UiFactory.Baked(UiSpriteCatalog.IconGear), UiFactory.Ink,
                new Vector2(0f, 1f), new Vector2(70f, -56f), new Vector2(56f, 56f));
            gear.preserveAspect = true;
            var heading = UiFactory.Label(card.transform, "Heading", ParentKeys.SettingsHeading, 44, TextAnchor.MiddleLeft,
                UiFactory.Ink, UiFactory.DisplayFont);
            UiFactory.Place(heading.rectTransform, new Vector2(0f, 1f), new Vector2(112f, -56f), new Vector2(700f, 64f),
                new Vector2(0f, 0.5f));

            for (int c = 0; c < SettingsCard.Columns; c++)
            {
                var title = UiFactory.Label(card.transform, "Column" + c, SettingsCard.ColumnHeadingKeys[c], 28,
                    TextAnchor.MiddleLeft, Graphite, UiFactory.BoldFont);
                UiFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(ColumnX(c), -110f),
                    new Vector2(ColumnWidth - 20f, 40f));
            }

            var helper = SettingsCard.HelperRow;
            HelperToggle = SwitchRow(card.transform, "HelperRow", helper.Picture, helper.LabelKey, helper.Column, helper.Row,
                ToggleHelper, out _helperTick);
            foreach (var row in SettingsCard.All)
            {
                if (row.IsSlider)
                {
                    var r = row;
                    _sliders[row.Id] = SliderRow(card.transform, RowName(row.Id), row.Picture, row.LabelKey, row.Column,
                        row.Row, v => SetSlider(r, v));
                }
                else
                {
                    string id = row.Id;
                    _switches[id] = SwitchRow(card.transform, RowName(id), row.Picture, row.LabelKey, row.Column, row.Row,
                        () => ToggleSetting(id), out var tick);
                    _ticks[id] = tick;
                }
            }

            MusicSlider = _sliders[SettingsCard.Music];
            SoundsSlider = _sliders[SettingsCard.Sounds];
            VoicesSlider = _sliders[SettingsCard.Voices];

            var entries = new Button[SettingsCard.Entries.Count];
            for (int i = 0; i < entries.Length; i++)
            {
                var entry = SettingsCard.Entries[i];
                UnityEngine.Events.UnityAction open;
                switch (entry.Id)
                {
                    case SettingsCard.Language:
                        open = () => OpenSubpage(OpenLanguage);
                        break;
                    case SettingsCard.GrownUps:
                        open = () => OpenSubpage(OpenGrownUps);
                        break;
                    default:
                        open = () => OpenSubpage(OpenCredits);
                        break;
                }

                entries[i] = UiFactory.SecondaryButton(card.transform, "Entry_" + entry.Id, entry.LabelKey,
                    new Vector2(0.5f, 0f), new Vector2(ColumnX(i), 70f), new Vector2(ColumnWidth - 40f, 96f), open);
            }

            LanguageEntry = entries[0];
            GrownUpsEntry = entries[1];
            CreditsEntry = entries[2];

            var close = UiFactory.FeltDisc(card.transform, "CloseSettings", UiSpriteCatalog.FeltDiscNight, Palette.WarmPaper,
                UiSpriteCatalog.IconClose, Palette.WarmPaper, null, UiFactory.SystemDiscDiameter, Vector2.one,
                new Vector2(-62f, -56f), 1f);
            SettingsCloseButton = close.gameObject.AddComponent<Button>();
            SettingsCloseButton.transition = Selectable.Transition.None;
            SettingsCloseButton.onClick.AddListener(() => _settings.SetActive(false));
            _settings.SetActive(false);

            // Gamepad and keyboard (F1): the rows on a grid, Back closes; left and right move a focused slider.
            var nav = UiFocusScreen.Attach(_settings, SettingsCard.BuildFocus(), card.transform);
            nav.Bind(SettingsCard.Close, SettingsCloseButton);
            nav.Bind(SettingsCard.Helper, HelperToggle);
            foreach (var pair in _switches)
            {
                nav.Bind(pair.Key, pair.Value);
            }

            foreach (var pair in _sliders)
            {
                nav.Bind(pair.Key, (RectTransform)pair.Value.transform.parent, null);
            }

            nav.Bind(SettingsCard.Language, LanguageEntry);
            nav.Bind(SettingsCard.GrownUps, GrownUpsEntry);
            nav.Bind(SettingsCard.Credits, CreditsEntry);
            nav.DirectionOverride = StepFocusedSlider;
            SettingsNavigation = nav;
        }

        private static string RowName(string id) => "Row_" + id;

        private static float ColumnX(int column) => (column - (SettingsCard.Columns - 1) * 0.5f) * (ColumnWidth + 20f);

        private static float RowY(int row) => RowsTop - (row - 1) * RowHeight;

        /// <summary>Left and right on a focused slider move it one step; anything else moves focus.</summary>
        private bool StepFocusedSlider(UiDirection direction)
        {
            if (direction != UiDirection.Left && direction != UiDirection.Right)
            {
                return false;
            }

            var focus = SettingsNavigation != null ? SettingsNavigation.Focus : null;
            if (focus == null || !_sliders.TryGetValue(focus, out var slider) || Preferences == null)
            {
                return false;
            }

            float value = SettingsCard.Step(Preferences, focus, direction == UiDirection.Right ? 1 : -1);
            _refreshing = true;
            slider.SetValueWithoutNotify(value);
            _refreshing = false;
            PreferencesChanged?.Invoke(Preferences);
            return true;
        }

        /// <summary>Hides the card while a page it opened is up; <see cref="ReturnToSettings"/> brings it back.</summary>
        private void OpenSubpage(Action open)
        {
            if (open == null)
            {
                return;
            }

            _returnFocus = SettingsNavigation != null ? SettingsNavigation.Focus : null;
            _settings.SetActive(false);
            open();
        }

        private string _returnFocus;

        /// <summary>
        /// A page opened from the card closed: the card comes back as it was (no new gate needed), with focus on the
        /// entry that opened the page (S004 F2), so a gamepad or keyboard player is where they left off.
        /// </summary>
        public void ReturnToSettings()
        {
            _settings.SetActive(true);
            RefreshSettings();
            if (_returnFocus != null && SettingsNavigation != null)
            {
                SettingsNavigation.FocusOn(_returnFocus);
            }
        }

        private Button SwitchRow(Transform card, string name, string art, string key, int column, int rowIndex,
            UnityEngine.Events.UnityAction onClick, out Image tick)
        {
            var row = UiFactory.Rect(card, name);
            UiFactory.Place(row, new Vector2(0.5f, 1f), new Vector2(ColumnX(column), RowY(rowIndex)),
                new Vector2(ColumnWidth, RowHeight - 4f));
            var hit = row.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f); // the whole row is the switch, not only the disc
            var icon = UiFactory.Sprite(row, "Picture", PuppetAtlas.Get(art), Color.white, new Vector2(0f, 0.5f),
                new Vector2(34f, 0f), new Vector2(64f, 64f));
            icon.preserveAspect = true;
            var label = UiFactory.Label(row, "Label", key, 30, TextAnchor.MiddleLeft, UiFactory.Ink, UiFactory.BoldFont);
            UiFactory.Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(76f, 0f),
                new Vector2(ColumnWidth - 76f - 108f, RowHeight - 12f), new Vector2(0f, 0.5f));
            // The switch: a felt disc that shows a green check when on, an empty bone disc when off (shape, not colour).
            var disc = UiFactory.FeltDisc(row, "Switch", UiSpriteCatalog.FeltDiscBone, Palette.HauntedPurple,
                UiSpriteCatalog.IconCheck, new Color32(0x8D, 0xBF, 0x3F, 0xFF), null, 96f, new Vector2(1f, 0.5f),
                new Vector2(-52f, 0f), 1f);
            disc.raycastTarget = false;
            tick = disc.transform.Find("Icon").GetComponent<Image>();
            var button = row.gameObject.AddComponent<Button>();
            button.targetGraphic = hit;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(onClick);
            AccessibleName.Set(row.gameObject, key);
            return button;
        }

        /// <summary>A slider row: picture, label and a wide felt slider (a 64 pt thumb for small fingers).</summary>
        private Slider SliderRow(Transform card, string name, string art, string key, int column, int rowIndex,
            UnityEngine.Events.UnityAction<float> onChange)
        {
            var row = UiFactory.Rect(card, name);
            UiFactory.Place(row, new Vector2(0.5f, 1f), new Vector2(ColumnX(column), RowY(rowIndex)),
                new Vector2(ColumnWidth, RowHeight - 4f));
            var icon = UiFactory.Sprite(row, "Picture", PuppetAtlas.Get(art), Color.white, new Vector2(0f, 0.5f),
                new Vector2(34f, 0f), new Vector2(64f, 64f));
            icon.preserveAspect = true;
            var label = UiFactory.Label(row, "Label", key, 28, TextAnchor.MiddleLeft, UiFactory.Ink, UiFactory.BoldFont);
            UiFactory.Place(label.rectTransform, new Vector2(0f, 0.5f), new Vector2(76f, 0f), new Vector2(150f, RowHeight - 12f),
                new Vector2(0f, 0.5f));

            const float width = 196f;
            var area = UiFactory.Rect(row, "Slider");
            UiFactory.Place(area, new Vector2(1f, 0.5f), new Vector2(-width * 0.5f - 8f, 0f), new Vector2(width, 88f));
            var hit = area.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f); // the whole row height is grabbable
            var track = UiFactory.Sprite(area, "Track", UiFactory.Circle, UiFactory.WithAlpha(UiFactory.Ink, 0.22f),
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width, 16f));
            track.type = Image.Type.Simple;
            var fillArea = UiFactory.Rect(area, "FillArea");
            UiFactory.Place(fillArea, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width - 32f, 16f));
            var fill = UiFactory.Panel(fillArea, "Fill", Palette.CandyCornYellow, false);
            var handleArea = UiFactory.Rect(area, "HandleArea");
            UiFactory.Place(handleArea, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(width - 32f, 88f));
            var handle = UiFactory.Sprite(handleArea, "Handle", UiFactory.Circle, Palette.HauntedPurple,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(64f, 64f));
            handle.rectTransform.anchorMin = new Vector2(0f, 0f);
            handle.rectTransform.anchorMax = new Vector2(0f, 1f);
            handle.rectTransform.sizeDelta = new Vector2(60f, -24f);
            var slider = area.gameObject.AddComponent<Slider>();
            slider.fillRect = fill.rectTransform;
            slider.handleRect = handle.rectTransform;
            slider.targetGraphic = handle;
            slider.transition = Selectable.Transition.None;
            slider.navigation = new Navigation { mode = Navigation.Mode.None };
            slider.direction = Slider.Direction.LeftToRight;
            slider.minValue = 0f;
            slider.maxValue = 1f;
            slider.value = 1f;
            slider.onValueChanged.AddListener(onChange);
            AccessibleName.Set(area.gameObject, key);
            return slider;
        }

        private void SetSlider(SettingRow row, float value)
        {
            if (Preferences == null || _refreshing)
            {
                return;
            }

            row.SetValue(Preferences, value);
            PreferencesChanged?.Invoke(Preferences);
        }

        private void ToggleSetting(string id)
        {
            if (Preferences == null)
            {
                return;
            }

            SettingsCard.Toggle(Preferences, id);
            RefreshSettings();
            PreferencesChanged?.Invoke(Preferences);
        }

        private bool _refreshing;

        /// <summary>The settings card (call from behind <see cref="RequireParent"/>).</summary>
        public void OpenSettings()
        {
            _returnFocus = null;
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
            if (Preferences == null)
            {
                return;
            }

            _refreshing = true;
            foreach (var row in SettingsCard.All)
            {
                if (row.IsSlider)
                {
                    _sliders[row.Id].SetValueWithoutNotify(row.GetValue(Preferences));
                }
                else
                {
                    _ticks[row.Id].enabled = row.Get(Preferences);
                }
            }

            _refreshing = false;
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
