using SpookyStreet.App;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Localization;
using SpookyStreet.Controls;
using SpookyStreet.Localization;
using SpookyStreet.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem.UI;
using UnityEngine.UI;

namespace SpookyStreet.UI
{
    /// <summary>
    /// Title, HUD and pause screens for the walking skeleton. Every visible string comes from a
    /// localization key. Screens are found by reference, never by scene object names.
    /// </summary>
    public sealed class GameUi : MonoBehaviour
    {
        /// <summary>Distance Pip must walk before the first prompt fades out.</summary>
        public const float PromptDismissDistance = 2f;

        /// <summary>Stick ghost centre, measured from the movement-side bottom corner of the safe area.</summary>
        public static readonly Vector2 StickRest = new Vector2(215f, 195f);

        private GameFlow _flow;
        private GameInput _input;
        private PipMotor _pip;
        private bool _leftHanded;

        private GameObject _title;
        private GameObject _hud;
        private GameObject _pause;
        private GameObject _prompt;
        private Button _newGameButton;
        private Button _continueButton;
        private Button _resumeButton;
        private Button _pauseButton;
        private GameObject _useButton;
        private LocalizedText _useVerb;
        private Image _useIcon;
        private string _useIconName;
        private RectTransform _safeArea;
        private Image _logo;
        private Text _caseName;
        private Vector2 _primarySlot;
        private Vector2 _secondarySlot;
        private float _promptStartDistance;
        private MoveTipView _tips;

        public GameObject TitleScreen => _title;

        /// <summary>The title's safe-area content (logo, case name, Continue, New game): the Continue recap joins it.</summary>
        public RectTransform TitleContent { get; private set; }

        /// <summary>Top-left of Continue's slot in <see cref="TitleContent"/> (its button is 440 × 112, pivot top-left).</summary>
        public Vector2 ContinueSlot => _primarySlot;
        public GameObject Hud => _hud;
        public GameObject PauseScreen => _pause;
        public GameObject FirstPrompt => _prompt;

        /// <summary>First-time moveset tips (paper tag near the Jump or Use disc).</summary>
        public MoveTipView Tips => _tips;
        public Button NewGameButton => _newGameButton;
        public Button ResumeButton => _resumeButton;
        public Button ContinueButton => _continueButton;
        public Button PauseButton => _pauseButton;

        /// <summary>The title's globe pill (language screen).</summary>
        public Button LanguageButton { get; private set; }

        /// <summary>The pause card's globe disc (language screen).</summary>
        public Button PauseLanguageButton { get; private set; }

        /// <summary>Opens the language screen (set by the language controller at boot).</summary>
        public System.Action OpenLanguages { get; set; }

        /// <summary>The title's gear (S004 stream E): opens the parent gate, then the settings card.</summary>
        public Button TitleSettingsButton { get; private set; }

        /// <summary>What the title's gear does (set by the game: the parent gate, then settings).</summary>
        public System.Action OpenSettings { get; set; }

        /// <summary>
        /// Contextual Use disc, shown in play: the interaction's verb when one is in range, otherwise "Roll" with the
        /// roll glyph on the ground and "Dive" in the air while a dive is available (spec §5 v1.2).
        /// </summary>
        public GameObject UseButton => _useButton;

        /// <summary>Localization key currently on the Use disc's verb tag.</summary>
        public string UseVerbKey => _useVerb != null ? _useVerb.Key : null;

        /// <summary>The safe-area rect the HUD lives in.</summary>
        public RectTransform SafeArea => _safeArea;

        /// <summary>Baked logo image on the title (null only if the sprite is missing).</summary>
        public Image Logo => _logo;

        /// <summary>Localized case name under the logo.</summary>
        public Text CaseName => _caseName;

        public static GameUi Create(GameFlow flow, GameInput input, PipMotor pip, BuildInfo buildInfo, bool leftHanded)
        {
            EnsureEventSystem();
            var canvas = UiFactory.CreateCanvas("GameUi", 10);
            var ui = canvas.gameObject.AddComponent<GameUi>();
            ui._flow = flow;
            ui._input = input;
            ui._pip = pip;
            ui._leftHanded = leftHanded;

            var safe = UiFactory.Stretch(UiFactory.Rect(canvas.transform, "SafeArea"));
            safe.gameObject.AddComponent<SafeAreaFitter>();
            ui._safeArea = safe;

            ui.BuildHud(safe);
            ui.BuildTitle(canvas.transform, buildInfo);
            ui.BuildPause(canvas.transform);

            flow.StateChanged += ui.OnStateChanged;
            ui.Apply(flow.State);
            return ui;
        }

        private static void EnsureEventSystem()
        {
            if (EventSystem.current != null)
            {
                return;
            }

            var go = new GameObject("EventSystem", typeof(EventSystem));
            // With no actions assigned the module installs the Input System's default UI actions on enable.
            go.AddComponent<InputSystemUIInputModule>();
        }

        private void OnDestroy()
        {
            if (_flow != null)
            {
                _flow.StateChanged -= OnStateChanged;
            }
        }

        private void Update()
        {
            RefreshUse();
            if (_flow.State == GameState.Playing && _prompt.activeSelf &&
                _pip.DistanceWalked - _promptStartDistance >= PromptDismissDistance)
            {
                _prompt.SetActive(false);
            }

            // One prompt at a time: no tip while the first "walk" prompt is up. Game time, so pause freezes it.
            if (_tips != null && _flow.State == GameState.Playing)
            {
                _tips.Tick(Time.deltaTime, _prompt.activeSelf);
            }
        }

        private void OnStateChanged(GameState previous, GameState current)
        {
            if (previous == GameState.Title && current == GameState.Playing)
            {
                ShowFirstPrompt();
            }

            if (current == GameState.Title && _tips != null)
            {
                _tips.HideNow();
            }

            Apply(current);
        }

        private void Apply(GameState state)
        {
            _title.SetActive(state == GameState.Title);
            _hud.SetActive(state == GameState.Playing);
            _pause.SetActive(state == GameState.Paused);
            GameObject titleFocus = _continueButton.gameObject.activeSelf ? _continueButton.gameObject
                : _newGameButton.gameObject;
            GameObject select = state == GameState.Title ? titleFocus
                : state == GameState.Paused ? _resumeButton.gameObject : null;
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(select);
            }
        }

        /// <summary>Interaction wins over the moveset on the Use disc, exactly as it does in the motor.</summary>
        private void RefreshUse()
        {
            if (_useVerb == null)
            {
                return;
            }

            var targets = _pip != null ? _pip.UseTargets : null;
            bool interact = targets != null && targets.HasTarget;
            // In the air the moveset verb reads "Dive" while a dive is available, so Use's air move is discoverable.
            string moveKey = _pip != null && _pip.CanDive ? LocalizationKeys.HudDive : LocalizationKeys.HudRoll;
            string key = interact ? targets.VerbKey ?? LocalizationKeys.HudUse : moveKey;
            string icon = interact ? targets.IconName ?? UiSpriteCatalog.IconExamine : UiSpriteCatalog.IconRoll;
            if (_useVerb.Key != key)
            {
                _useVerb.SetKey(key);
            }

            if (_useIcon != null && _useIconName != icon)
            {
                _useIconName = icon;
                _useIcon.sprite = UiFactory.Baked(icon);
            }
        }

        private void ShowFirstPrompt()
        {
            string key;
            switch (GameInput.DetectPromptDevice())
            {
                case PromptDevice.Keyboard:
                    key = LocalizationKeys.PromptMoveKeyboard;
                    break;
                case PromptDevice.Gamepad:
                    key = LocalizationKeys.PromptMoveGamepad;
                    break;
                default:
                    key = LocalizationKeys.PromptMoveTouch;
                    break;
            }

            _prompt.GetComponentInChildren<LocalizedText>(true).SetKey(key);
            _promptStartDistance = _pip.DistanceWalked;
            _prompt.SetActive(true);
        }

        /// <summary>True while the stick is on the right and the action discs on the left.</summary>
        public bool LeftHanded => _leftHanded;

        /// <summary>
        /// Left-handed controls (settings, S004 stream E), live: every HUD element (built by this class or added to
        /// the HUD by the case, the radio and the hint demo) is mirrored across the screen's centre line, and the
        /// floating stick's ghost and the move tips' slots follow. Calling it again with the same value does nothing.
        /// </summary>
        public void SetLeftHanded(bool leftHanded)
        {
            if (leftHanded == _leftHanded || _hud == null)
            {
                return;
            }

            _leftHanded = leftHanded;
            foreach (Transform child in _hud.transform)
            {
                if (child is RectTransform rect)
                {
                    MirrorRect(rect);
                }
            }

            var stick = _hud.GetComponentInChildren<FloatingStick>(true);
            if (stick != null)
            {
                if (stick.BaseVisual != null)
                {
                    MirrorAnchors(stick.BaseVisual);
                }

                if (stick.KnobVisual != null)
                {
                    MirrorAnchors(stick.KnobVisual);
                }

                stick.RestPosition = new Vector2(-stick.RestPosition.x, stick.RestPosition.y);
                stick.ResetStick();
            }

            if (_tips != null)
            {
                _tips.MirrorSlots();
            }
        }

        private static void MirrorRect(RectTransform rect)
        {
            MirrorAnchors(rect);
            rect.anchoredPosition = new Vector2(-rect.anchoredPosition.x, rect.anchoredPosition.y);
            var euler = rect.localEulerAngles;
            rect.localRotation = Quaternion.Euler(euler.x, euler.y, -euler.z);
        }

        private static void MirrorAnchors(RectTransform rect)
        {
            var min = rect.anchorMin;
            var max = rect.anchorMax;
            rect.anchorMin = new Vector2(1f - max.x, min.y);
            rect.anchorMax = new Vector2(1f - min.x, max.y);
            rect.pivot = new Vector2(1f - rect.pivot.x, rect.pivot.y);
        }

        private float MirrorX(float anchorX) => _leftHanded ? 1f - anchorX : anchorX;

        private Vector2 MirrorOffset(Vector2 offset) => _leftHanded ? new Vector2(-offset.x, offset.y) : offset;

        /// <summary>Soft ink shade behind a HUD cluster (mock: radial 25–30 %), so bone icons read on any backdrop.</summary>
        private static void ClusterShade(Transform parent, string name, Vector2 anchor, Vector2 position, float size,
            float alpha)
        {
            UiFactory.Sprite(parent, name, IconFactory.RadialFade, UiFactory.WithAlpha(Palette.InkBlack, alpha), anchor,
                position, new Vector2(size, size));
        }

        private void BuildHud(RectTransform safe)
        {
            _hud = UiFactory.Stretch(UiFactory.Rect(safe, "Hud")).gameObject;
            var touch = _hud.AddComponent<TouchControls>();

            // Drag on empty space orbits; this transparent layer sits under every other control.
            var orbit = UiFactory.Panel(_hud.transform, "OrbitArea", new Color(0f, 0f, 0f, 0f));
            touch.OrbitArea = orbit.gameObject.AddComponent<OrbitDragArea>();

            var bottomCorner = new Vector2(MirrorX(1f), 0f);
            var topCorner = new Vector2(MirrorX(1f), 1f);
            ClusterShade(_hud.transform, "ActionShade", bottomCorner, MirrorOffset(new Vector2(-220f, 120f)), 560f, 0.3f);
            ClusterShade(_hud.transform, "SystemShade", topCorner, MirrorOffset(new Vector2(-60f, -60f)), 260f, 0.25f);

            BuildStick(touch);

            // Action cluster on the other thumb: icon + short verb (spec §5). Jump: night felt with a candy-corn
            // stitch. Use: the orange disc with a paper verb tag. With no interaction in range it reads "Roll" with
            // the roll glyph (roll on the ground, dive in the air); an interaction swaps in its own verb and glyph.
            var jump = UiFactory.FeltDisc(_hud.transform, "JumpButton", UiSpriteCatalog.FeltDiscNight,
                Palette.CandyCornYellow, UiSpriteCatalog.IconJump, Palette.WarmPaper, LocalizationKeys.HudJump,
                UiFactory.JumpDiameter, bottomCorner, MirrorOffset(new Vector2(-140f, 140f)), UiFactory.HudFeltAlpha);
            touch.JumpButton = jump.gameObject.AddComponent<TouchButton>();

            var use = UiFactory.FeltDisc(_hud.transform, "UseButton", UiSpriteCatalog.FeltDiscOrange,
                Palette.CandyCornYellow, UiSpriteCatalog.IconRoll, Palette.InkBlack, null, UiFactory.UseDiameter,
                bottomCorner, MirrorOffset(new Vector2(-330f, 118f)), 1f);
            var verb = UiFactory.PaperTag(use.transform, "VerbTag", LocalizationKeys.HudRoll, UiFactory.CaptionSize,
                new Vector2(0.5f, 0f), new Vector2(0f, -10f), new Vector2(150f, 54f), -2f, UiFactory.PlayPaper);
            verb.raycastTarget = false;
            _useVerb = verb.GetComponentInChildren<LocalizedText>(true);
            var useIcon = use.transform.Find("Icon");
            _useIcon = useIcon != null ? useIcon.GetComponent<Image>() : null;
            _useIconName = UiSpriteCatalog.IconRoll;
            touch.UseButton = use.gameObject.AddComponent<TouchButton>();
            _useButton = use.gameObject;

            // System disc top-right: Pause. The notebook disc joins it when the notebook exists (S002); no bat
            // counter until paper bats can be collected.
            var pauseDisc = UiFactory.FeltDisc(_hud.transform, "PauseButton", UiSpriteCatalog.FeltDiscNight,
                Palette.WarmPaper, UiSpriteCatalog.IconPause, Palette.WarmPaper, null, UiFactory.SystemDiscDiameter,
                topCorner, MirrorOffset(new Vector2(-60f, -60f)), UiFactory.HudFeltAlpha);
            var pause = pauseDisc.gameObject.AddComponent<Button>();
            pause.targetGraphic = pauseDisc;
            pause.transition = Selectable.Transition.None;
            pause.onClick.AddListener(() => _flow.Pause());
            _pauseButton = pause;

            // First prompt: a paper tag just above the stick ghost, so "here" is unambiguous and the centre stays clear.
            var corner = new Vector2(_leftHanded ? 1f : 0f, 0f);
            var tag = UiFactory.PaperTag(_hud.transform, "FirstPrompt", LocalizationKeys.PromptMoveTouch,
                UiFactory.BodySize, corner,
                new Vector2(_leftHanded ? -(StickRest.x + 70f) : StickRest.x + 70f, StickRest.y + 170f),
                new Vector2(460f, 92f), _leftHanded ? 1.5f : -1.5f, UiFactory.PlayPaper);
            _prompt = tag.gameObject;
            _prompt.SetActive(false);

            // First-time moveset tips: a paper tag above the Jump disc (hold, chain, glide) or the Use disc (dive).
            _tips = MoveTipView.Create(_hud.transform, _pip, bottomCorner, MirrorOffset(new Vector2(-300f, 300f)),
                MirrorOffset(new Vector2(-390f, 300f)), _leftHanded);

            _input.Touch = touch;
        }

        /// <summary>Floating stick ghost: a translucent night felt ring with a bone stitch and stitched chevrons.</summary>
        private void BuildStick(TouchControls touch)
        {
            // Floating stick zone: the lower ~75 % of the movement-thumb side. Pivot in the outer bottom corner so
            // the ghost's rest position is measured from that corner.
            float side = _leftHanded ? 1f : 0f;
            var stickZone = UiFactory.Rect(_hud.transform, "StickZone");
            stickZone.anchorMin = new Vector2(_leftHanded ? 0.55f : 0f, 0f);
            stickZone.anchorMax = new Vector2(_leftHanded ? 1f : 0.45f, 0.75f);
            stickZone.pivot = new Vector2(side, 0f);
            stickZone.offsetMin = Vector2.zero;
            stickZone.offsetMax = Vector2.zero;
            var zoneImage = stickZone.gameObject.AddComponent<Image>();
            zoneImage.color = new Color(0f, 0f, 0f, 0f);

            var visuals = UiFactory.Stretch(UiFactory.Rect(stickZone, "StickVisuals"));
            var group = visuals.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            var corner = new Vector2(side, 0f);
            var baseSize = new Vector2(220f, 220f);
            var stickBase = UiFactory.Sprite(visuals, "StickBase", null, Color.white, corner, Vector2.zero, baseSize);
            UiFactory.ApplySurface(stickBase, UiSpriteCatalog.FeltDiscNight, Palette.NightViolet,
                UiFactory.WithAlpha(Color.white, 0.6f));
            UiFactory.Sprite(stickBase.transform, "Ring", UiFactory.Baked(UiSpriteCatalog.StitchRing),
                UiFactory.WithAlpha(Palette.WarmPaper, 0.8f), new Vector2(0.5f, 0.5f), Vector2.zero, baseSize * 0.94f);
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f;
                Vector2 dir = Quaternion.Euler(0f, 0f, angle) * Vector2.up;
                var arrow = UiFactory.Sprite(stickBase.transform, "Arrow", UiFactory.Baked(UiSpriteCatalog.StickChevron),
                    UiFactory.WithAlpha(Palette.WarmPaper, 0.85f), new Vector2(0.5f, 0.5f), dir * 76f,
                    new Vector2(42f, 42f));
                arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            var knob = UiFactory.Sprite(visuals, "StickKnob", null, Color.white, corner, Vector2.zero,
                new Vector2(96f, 96f));
            UiFactory.ApplySurface(knob, UiSpriteCatalog.FeltDiscBone, Palette.WarmPaper, new Color(0.9f, 0.9f, 0.9f, 0.9f));
            UiFactory.InkShadow(knob, new Vector2(3f, -4f), 0.5f);
            UiFactory.Sprite(knob.transform, "Ring", UiFactory.Baked(UiSpriteCatalog.StitchRing),
                UiFactory.WithAlpha(Palette.HauntedPurple, 0.8f), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(80f, 80f));

            var stick = stickZone.gameObject.AddComponent<FloatingStick>();
            stick.BaseVisual = stickBase.rectTransform;
            stick.KnobVisual = knob.rectTransform;
            stick.Visuals = group;
            stick.RestPosition = new Vector2(_leftHanded ? -StickRest.x : StickRest.x, StickRest.y);
            touch.Stick = stick;
        }

        private void BuildTitle(Transform canvas, BuildInfo buildInfo)
        {
            // No full-screen scrim: the lit street is the title art (spec §1). A soft night-violet fade behind the
            // left column keeps the logo and buttons readable.
            var root = UiFactory.Stretch(UiFactory.Rect(canvas, "TitleScreen"));
            root.SetSiblingIndex(0);
            _title = root.gameObject;
            var fade = UiFactory.Panel(root, "ReadingFade", UiFactory.WithAlpha(Palette.NightViolet, 0.82f), raycast: false);
            fade.sprite = IconFactory.HorizontalFade;
            fade.rectTransform.anchorMax = new Vector2(0.62f, 1f);

            var content = UiFactory.Stretch(UiFactory.Rect(root, "Content"));
            content.gameObject.AddComponent<SafeAreaFitter>();
            TitleContent = content;
            var topLeft = new Vector2(0f, 1f);

            // Baked stitched logo: the product name as art, not localized (docs/ui-language.md). If the PNG is ever
            // missing the Sniglet label keeps the title readable.
            var logoSprite = UiSprites.TryLoad(UiSpriteCatalog.Logo);
            var logoSize = new Vector2(600f, 600f / 2.116f);
            if (logoSprite != null)
            {
                logoSize.y = logoSize.x * logoSprite.rect.height / logoSprite.rect.width;
                _logo = UiFactory.Sprite(content, "Logo", logoSprite, Color.white, topLeft, new Vector2(30f, -26f), logoSize);
                _logo.rectTransform.pivot = topLeft;
                _logo.rectTransform.anchoredPosition = new Vector2(30f, -26f);
                _logo.preserveAspect = true;
                _logo.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 1.5f);
                UiFactory.InkShadow(_logo, new Vector2(5f, -8f), 0.45f);
            }
            else
            {
                UiSprites.Get(UiSpriteCatalog.Logo); // records the missing sprite for tests
                var gameName = UiFactory.Label(content, "GameName", LocalizationKeys.TitleGameName, 104,
                    TextAnchor.MiddleLeft, Palette.WarmPaper, UiFactory.DisplayFont);
                UiFactory.Place(gameName.rectTransform, topLeft, new Vector2(40f, -26f), logoSize, topLeft);
                UiFactory.InkShadow(gameName, 5f);
            }

            // Case-name strip: a paper strip with a plum stitch under the logo.
            float stripTop = -(26f + logoSize.y + 14f);
            var strip = UiFactory.PaperCard(content, "CaseNameStrip", topLeft, new Vector2(60f, stripTop),
                new Vector2(580f, 72f), -1.5f, Palette.Plum);
            strip.rectTransform.pivot = topLeft;
            strip.rectTransform.anchoredPosition = new Vector2(60f, stripTop);
            var caseName = UiFactory.Label(strip.transform, "CaseName", LocalizationKeys.TitleCaseName, 34,
                TextAnchor.MiddleCenter, UiFactory.Ink, UiFactory.BoldFont);
            UiFactory.Stretch(caseName.rectTransform);
            caseName.rectTransform.offsetMin = new Vector2(26f, 8f);
            caseName.rectTransform.offsetMax = new Vector2(-26f, -8f);
            _caseName = caseName;

            // Continue (orange, with a sub-label hook for the save summary) appears in S002 once saves exist. Until
            // then New game is the screen's only action, so it takes the orange primary style and Continue's slot.
            float slotTop = stripTop - 72f - 34f;
            _primarySlot = new Vector2(50f, slotTop);
            _secondarySlot = new Vector2(64f, slotTop - 112f - 22f);
            _continueButton = UiFactory.PrimaryButton(content, "ContinueButton", LocalizationKeys.TitleContinue, topLeft,
                _primarySlot, new Vector2(440f, 112f), null, UiSpriteCatalog.IconPlay);
            _continueButton.GetComponent<RectTransform>().pivot = topLeft;
            _continueButton.GetComponent<RectTransform>().anchoredPosition = _primarySlot;
            _continueButton.transform.localRotation = Quaternion.Euler(0f, 0f, 1f);
            _newGameButton = UiFactory.SecondaryButton(content, "NewGameButton", LocalizationKeys.TitleNewGame, topLeft,
                _secondarySlot, new Vector2(360f, 96f), () => _flow.StartNewGame());
            _newGameButton.GetComponent<RectTransform>().pivot = topLeft;
            _newGameButton.transform.localRotation = Quaternion.Euler(0f, 0f, -1.2f);
            SetHasSave(false);

            if (buildInfo.Identifier != "editor")
            {
                var build = UiFactory.Label(content, "BuildLabel", null, 22, TextAnchor.LowerLeft,
                    UiFactory.WithAlpha(Palette.WarmPaper, 0.6f));
                UiFactory.Place(build.rectTransform, Vector2.zero, new Vector2(40f, 24f), new Vector2(620f, 36f),
                    Vector2.zero);
                build.gameObject.AddComponent<LocalizedText>().SetKey(LocalizationKeys.TitleBuildLabel,
                    () => new object[] { Application.version, buildInfo.Identifier });
            }

            // The globe pill opens the language screen (S004 D1), in every build: English, the built-in languages
            // and, behind the parent gate, downloads.
            LanguageButton = BuildLanguagePill(content, () => OpenLanguages?.Invoke());

            // The grown-ups' gear (S004 stream E), top-right: the parent gate, then settings, the progress page and
            // credits. Its own small disc, away from the child's buttons on the left.
            var gear = UiFactory.FeltDisc(content, "SettingsButton", UiSpriteCatalog.FeltDiscNight, Palette.WarmPaper,
                UiSpriteCatalog.IconGear, Palette.WarmPaper, null, UiFactory.SystemDiscDiameter, Vector2.one,
                new Vector2(-70f, -66f), 1f);
            TitleSettingsButton = gear.gameObject.AddComponent<Button>();
            TitleSettingsButton.targetGraphic = gear;
            TitleSettingsButton.transition = Selectable.Transition.None;
            TitleSettingsButton.onClick.AddListener(() => OpenSettings?.Invoke());
        }

        /// <summary>
        /// Title button hook for saves (S002): with a save, Continue is the orange primary and New game turns paper;
        /// without one, Continue is hidden and New game is the only, orange, action in Continue's slot.
        /// </summary>
        public void SetHasSave(bool hasSave)
        {
            _continueButton.gameObject.SetActive(hasSave);
            var craft = _newGameButton.GetComponent<CraftButton>();
            craft.SetStyle(hasSave ? CraftButtonStyle.Secondary : CraftButtonStyle.Primary);
            var rect = (RectTransform)_newGameButton.transform;
            rect.anchoredPosition = hasSave ? _secondarySlot : _primarySlot;
            rect.sizeDelta = hasSave ? new Vector2(360f, 96f) : new Vector2(440f, 112f);
        }

        /// <summary>The language screen's opener: a small paper pill with a globe and the current language, bottom-right.</summary>
        private static Button BuildLanguagePill(Transform parent, UnityEngine.Events.UnityAction open)
        {
            var size = new Vector2(500f, 96f);
            var pill = UiFactory.SecondaryButton(parent, "LanguageToggleDev", null, Vector2.right,
                new Vector2(-size.x * 0.5f - 36f, 72f), size, open, UiSpriteCatalog.IconGlobe);
            pill.transform.localRotation = Quaternion.Euler(0f, 0f, -1f);
            var label = pill.GetComponent<CraftButton>().Label;
            label.fontSize = 28;
            label.resizeTextMaxSize = 28;
            label.alignment = TextAnchor.MiddleLeft;
            label.gameObject.AddComponent<LocalizedText>().SetKey(LocalizationKeys.TitleLanguageToggle,
                () => new object[] { Localizer.Catalog.Active.NativeName });
            return pill;
        }

        private void BuildPause(Transform canvas)
        {
            // Modal: a night-violet scrim dims the world first, then a stitched paper card with the existing actions.
            var backdrop = UiFactory.Panel(canvas, "PauseBackdrop", UiFactory.WithAlpha(Palette.NightViolet, 0.8f));
            _pause = backdrop.gameObject;
            var content = UiFactory.Stretch(UiFactory.Rect(_pause.transform, "Content"));
            content.gameObject.AddComponent<SafeAreaFitter>();

            var card = UiFactory.PaperCard(content, "PauseCard", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(560f, 440f), -1f);
            var top = new Vector2(0.5f, 1f);
            var heading = UiFactory.Label(card.transform, "Heading", LocalizationKeys.PauseHeading, 64,
                TextAnchor.MiddleCenter, UiFactory.Ink, UiFactory.DisplayFont);
            UiFactory.Place(heading.rectTransform, top, new Vector2(0f, -80f), new Vector2(480f, 96f));
            _resumeButton = UiFactory.PrimaryButton(card.transform, "ResumeButton", LocalizationKeys.PauseResume, top,
                new Vector2(0f, -200f), new Vector2(440f, 112f), () => _flow.Resume(), UiSpriteCatalog.IconPlay);
            UiFactory.SecondaryButton(card.transform, "BackToTitleButton", LocalizationKeys.PauseBackToTitle, top,
                new Vector2(0f, -340f), new Vector2(400f, 96f), () => _flow.QuitToTitle());

            // Top-left globe (the settings gear takes the top-right corner): the language screen, no gate to switch.
            var globe = UiFactory.FeltDisc(card.transform, "LanguageButton", UiSpriteCatalog.FeltDiscNight,
                Palette.WarmPaper, UiSpriteCatalog.IconGlobe, Palette.WarmPaper, null, UiFactory.SystemDiscDiameter,
                new Vector2(0f, 1f), new Vector2(30f, -30f), 1f);
            PauseLanguageButton = globe.gameObject.AddComponent<Button>();
            PauseLanguageButton.targetGraphic = globe;
            PauseLanguageButton.transition = Selectable.Transition.None;
            PauseLanguageButton.onClick.AddListener(() => OpenLanguages?.Invoke());
        }
    }
}
