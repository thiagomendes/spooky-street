using SpookyStreet.App;
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
        private Button _resumeButton;
        private float _promptStartDistance;

        public GameObject TitleScreen => _title;
        public GameObject Hud => _hud;
        public GameObject PauseScreen => _pause;
        public GameObject FirstPrompt => _prompt;
        public Button NewGameButton => _newGameButton;
        public Button ResumeButton => _resumeButton;

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
            if (_flow.State == GameState.Playing && _prompt.activeSelf &&
                _pip.DistanceWalked - _promptStartDistance >= PromptDismissDistance)
            {
                _prompt.SetActive(false);
            }
        }

        private void OnStateChanged(GameState previous, GameState current)
        {
            if (previous == GameState.Title && current == GameState.Playing)
            {
                ShowFirstPrompt();
            }

            Apply(current);
        }

        private void Apply(GameState state)
        {
            _title.SetActive(state == GameState.Title);
            _hud.SetActive(state == GameState.Playing);
            _pause.SetActive(state == GameState.Paused);
            GameObject select = state == GameState.Title ? _newGameButton.gameObject
                : state == GameState.Paused ? _resumeButton.gameObject : null;
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(select);
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

        private float MirrorX(float anchorX) => _leftHanded ? 1f - anchorX : anchorX;

        private Vector2 MirrorOffset(Vector2 offset) => _leftHanded ? new Vector2(-offset.x, offset.y) : offset;

        private void BuildHud(RectTransform safe)
        {
            _hud = UiFactory.Stretch(UiFactory.Rect(safe, "Hud")).gameObject;
            var touch = _hud.AddComponent<TouchControls>();

            // Drag on empty space orbits; this transparent layer sits under every other control.
            var orbit = UiFactory.Panel(_hud.transform, "OrbitArea", new Color(0f, 0f, 0f, 0f));
            touch.OrbitArea = orbit.gameObject.AddComponent<OrbitDragArea>();

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
            var stickBase = UiFactory.Sprite(visuals, "StickBase", IconFactory.Disc, UiFactory.WithAlpha(Palette.Ink, 0.45f),
                corner, Vector2.zero, new Vector2(220f, 220f)).rectTransform;
            UiFactory.Sprite(stickBase, "Ring", IconFactory.Ring(0.05f), Palette.WarmPaper, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(220f, 220f));
            for (int i = 0; i < 4; i++)
            {
                float angle = i * 90f;
                Vector2 dir = Quaternion.Euler(0f, 0f, angle) * Vector2.up;
                var arrow = UiFactory.Sprite(stickBase, "Arrow", IconFactory.StickArrow, Palette.WarmPaper,
                    new Vector2(0.5f, 0.5f), dir * 82f, new Vector2(40f, 40f));
                arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, angle);
            }

            var knob = UiFactory.Sprite(visuals, "StickKnob", IconFactory.Disc, Palette.WarmPaper, corner, Vector2.zero,
                new Vector2(96f, 96f));
            UiFactory.InkShadow(knob, 4f, 0.6f);
            var stick = stickZone.gameObject.AddComponent<FloatingStick>();
            stick.BaseVisual = stickBase;
            stick.KnobVisual = knob.rectTransform;
            stick.Visuals = group;
            stick.RestPosition = new Vector2(_leftHanded ? -StickRest.x : StickRest.x, StickRest.y);
            touch.Stick = stick;

            // Action cluster on the other thumb: icon + short verb (spec §5). Use stays hidden until interactables
            // exist (S001/S002).
            var jump = UiFactory.InkDisc(_hud.transform, "JumpButton", IconFactory.ChevronUp, LocalizationKeys.HudJump, 150f,
                new Vector2(MirrorX(1f), 0f), MirrorOffset(new Vector2(-140f, 140f)));
            touch.JumpButton = jump.gameObject.AddComponent<TouchButton>();
            var use = UiFactory.InkDisc(_hud.transform, "UseButton", IconFactory.Disc, LocalizationKeys.HudUse, 120f,
                new Vector2(MirrorX(1f), 0f), MirrorOffset(new Vector2(-320f, 110f)));
            use.gameObject.SetActive(false);

            var pauseDisc = UiFactory.InkDisc(_hud.transform, "PauseButton", IconFactory.PauseBars, null, 96f,
                new Vector2(MirrorX(1f), 1f), MirrorOffset(new Vector2(-78f, -66f)));
            var pause = pauseDisc.gameObject.AddComponent<Button>();
            pause.targetGraphic = pauseDisc;
            pause.onClick.AddListener(() => _flow.Pause());

            // First prompt: a paper tag just above the stick ghost, so "here" is unambiguous and the centre stays clear.
            var tag = UiFactory.PaperCard(_hud.transform, "FirstPrompt", Palette.WarmPaper, corner,
                new Vector2(_leftHanded ? -(StickRest.x + 80f) : StickRest.x + 80f, StickRest.y + 175f),
                new Vector2(460f, 92f), _leftHanded ? 1.5f : -1.5f);
            var promptText = UiFactory.Label(tag.transform, "Text", LocalizationKeys.PromptMoveTouch, UiFactory.BodySize,
                TextAnchor.MiddleCenter, Palette.Ink, UiFactory.BoldFont);
            UiFactory.Stretch(promptText.rectTransform);
            promptText.rectTransform.offsetMin = new Vector2(22f, 6f);
            promptText.rectTransform.offsetMax = new Vector2(-22f, -6f);
            _prompt = tag.gameObject;
            _prompt.SetActive(false);

            _input.Touch = touch;
        }

        private void BuildTitle(Transform canvas, BuildInfo buildInfo)
        {
            // No full-screen scrim: the lit street is the title art (spec §1). A soft ink fade behind the left column
            // keeps the text readable.
            var root = UiFactory.Stretch(UiFactory.Rect(canvas, "TitleScreen"));
            root.SetSiblingIndex(0);
            _title = root.gameObject;
            var fade = UiFactory.Panel(root, "ReadingFade", UiFactory.WithAlpha(Palette.Ink, 0.7f), raycast: false);
            fade.sprite = IconFactory.HorizontalFade;
            fade.rectTransform.anchorMax = new Vector2(0.62f, 1f);

            var content = UiFactory.Stretch(UiFactory.Rect(root, "Content"));
            content.gameObject.AddComponent<SafeAreaFitter>();
            var left = new Vector2(0f, 0.5f);

            var gameName = UiFactory.Label(content, "GameName", LocalizationKeys.TitleGameName, 104, TextAnchor.MiddleLeft,
                Palette.WarmPaper, UiFactory.DisplayFont);
            UiFactory.Place(gameName.rectTransform, new Vector2(0.08f, 0.68f), Vector2.zero, new Vector2(760f, 140f), left);
            UiFactory.InkShadow(gameName, 5f);
            var caseName = UiFactory.Label(content, "CaseName", LocalizationKeys.TitleCaseName, 40, TextAnchor.MiddleLeft,
                UiFactory.WithAlpha(Palette.WarmPaper, 0.85f), UiFactory.BoldFont);
            UiFactory.Place(caseName.rectTransform, new Vector2(0.08f, 0.55f), Vector2.zero, new Vector2(760f, 60f), left);
            UiFactory.InkShadow(caseName, 3f, 0.8f);

            // Continue appears in S002 once saves exist; New game never needs confirmation without a save.
            var buttonSize = new Vector2(440f, 112f);
            var buttonOffset = new Vector2(buttonSize.x * 0.5f, 0f);
            var continueButton = UiFactory.PrimaryButton(content, "ContinueButton", LocalizationKeys.TitleContinue,
                new Vector2(0.08f, 0.36f), buttonOffset + new Vector2(0f, 64f), buttonSize, null);
            continueButton.gameObject.SetActive(false);
            _newGameButton = UiFactory.PrimaryButton(content, "NewGameButton", LocalizationKeys.TitleNewGame,
                new Vector2(0.08f, 0.36f), buttonOffset, buttonSize, () => _flow.StartNewGame());
            _newGameButton.transform.localRotation = Quaternion.Euler(0f, 0f, 1.5f);

            if (buildInfo.Identifier != "editor")
            {
                var build = UiFactory.Label(content, "BuildLabel", null, 22, TextAnchor.LowerLeft,
                    UiFactory.WithAlpha(Palette.WarmPaper, 0.6f));
                UiFactory.Place(build.rectTransform, Vector2.zero, new Vector2(40f, 24f), new Vector2(620f, 36f),
                    Vector2.zero);
                build.gameObject.AddComponent<LocalizedText>().SetKey(LocalizationKeys.TitleBuildLabel,
                    () => new object[] { Application.version, buildInfo.Identifier });
            }

            if (Debug.isDebugBuild)
            {
                BuildLanguagePill(content);
            }
        }

        /// <summary>Development-only pseudo-locale switch: a small paper pill with a globe, bottom-right.</summary>
        private static void BuildLanguagePill(Transform parent)
        {
            var size = new Vector2(330f, 96f);
            var pill = UiFactory.Sprite(parent, "LanguageToggleDev", IconFactory.RoundedRect,
                UiFactory.WithAlpha(Palette.WarmPaper, 0.92f), Vector2.right, new Vector2(-size.x * 0.5f - 36f, 72f), size,
                raycast: true);
            UiFactory.InkShadow(pill, 4f, 0.7f);
            var button = pill.gameObject.AddComponent<Button>();
            button.targetGraphic = pill;
            button.onClick.AddListener(Localizer.TogglePseudo);
            pill.gameObject.AddComponent<PressScale>();
            UiFactory.Sprite(pill.transform, "Globe", IconFactory.Globe, Palette.Ink, new Vector2(0f, 0.5f),
                new Vector2(46f, 0f), new Vector2(44f, 44f));
            var label = UiFactory.Label(pill.transform, "Label", null, 26, TextAnchor.MiddleLeft, Palette.Ink,
                UiFactory.BoldFont);
            UiFactory.Stretch(label.rectTransform);
            label.rectTransform.offsetMin = new Vector2(80f, 6f);
            label.rectTransform.offsetMax = new Vector2(-18f, -6f);
            label.gameObject.AddComponent<LocalizedText>().SetKey(LocalizationKeys.TitleLanguageToggle,
                () => new object[] { Localizer.Catalog.Active.NativeName });
        }

        private void BuildPause(Transform canvas)
        {
            var backdrop = UiFactory.Panel(canvas, "PauseBackdrop", UiFactory.WithAlpha(Palette.Ink, 0.7f));
            _pause = backdrop.gameObject;
            var content = UiFactory.Stretch(UiFactory.Rect(_pause.transform, "Content"));
            content.gameObject.AddComponent<SafeAreaFitter>();

            var card = UiFactory.PaperCard(content, "PauseCard", Palette.WarmPaper, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(560f, 460f), -1f);
            var top = new Vector2(0.5f, 1f);
            var heading = UiFactory.Label(card.transform, "Heading", LocalizationKeys.PauseHeading, 64,
                TextAnchor.MiddleCenter, Palette.Ink, UiFactory.DisplayFont);
            UiFactory.Place(heading.rectTransform, top, new Vector2(0f, -88f), new Vector2(480f, 96f));
            _resumeButton = UiFactory.PrimaryButton(card.transform, "ResumeButton", LocalizationKeys.PauseResume, top,
                new Vector2(0f, -230f), new Vector2(400f, 108f), () => _flow.Resume());
            UiFactory.SecondaryButton(card.transform, "BackToTitleButton", LocalizationKeys.PauseBackToTitle, top,
                new Vector2(0f, -360f), new Vector2(400f, 96f), () => _flow.QuitToTitle());
        }
    }
}
