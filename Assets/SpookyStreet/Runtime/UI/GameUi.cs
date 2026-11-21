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

            // Floating stick zone: the lower ~45 % of the movement-thumb side.
            var stickZone = UiFactory.Rect(_hud.transform, "StickZone");
            stickZone.anchorMin = new Vector2(_leftHanded ? 0.55f : 0f, 0f);
            stickZone.anchorMax = new Vector2(_leftHanded ? 1f : 0.45f, 0.75f);
            stickZone.offsetMin = Vector2.zero;
            stickZone.offsetMax = Vector2.zero;
            var zoneImage = stickZone.gameObject.AddComponent<Image>();
            zoneImage.color = new Color(0f, 0f, 0f, 0f);
            var stickBase = UiFactory.Place(UiFactory.Rect(stickZone, "StickBase"), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(220f, 220f));
            var baseImage = stickBase.gameObject.AddComponent<Image>();
            baseImage.sprite = UiFactory.Circle;
            baseImage.color = new Color(1f, 1f, 1f, 0.18f);
            baseImage.raycastTarget = false;
            var knob = UiFactory.Place(UiFactory.Rect(stickZone, "StickKnob"), new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(110f, 110f));
            var knobImage = knob.gameObject.AddComponent<Image>();
            knobImage.sprite = UiFactory.Circle;
            knobImage.color = new Color(Palette.WarmPaper.r, Palette.WarmPaper.g, Palette.WarmPaper.b, 0.6f);
            knobImage.raycastTarget = false;
            var stick = stickZone.gameObject.AddComponent<FloatingStick>();
            stick.BaseVisual = stickBase;
            stick.KnobVisual = knob;
            touch.Stick = stick;

            // Action cluster on the other thumb. Use stays hidden until interactables exist (S001/S002).
            var jump = UiFactory.RoundButtonVisual(_hud.transform, "JumpButton", LocalizationKeys.HudJump,
                new Vector2(MirrorX(1f), 0f), MirrorOffset(new Vector2(-140f, 140f)), 150f);
            touch.JumpButton = jump.gameObject.AddComponent<TouchButton>();
            var use = UiFactory.RoundButtonVisual(_hud.transform, "UseButton", LocalizationKeys.HudUse,
                new Vector2(MirrorX(1f), 0f), MirrorOffset(new Vector2(-310f, 110f)), 120f);
            use.gameObject.SetActive(false);

            var pause = UiFactory.TextButton(_hud.transform, "PauseButton", LocalizationKeys.HudPause,
                new Vector2(MirrorX(1f), 1f), MirrorOffset(new Vector2(-90f, -60f)), new Vector2(150f, 96f),
                () => _flow.Pause());
            pause.GetComponentInChildren<Text>().fontSize = 30;

            // First prompt: top centre with an opaque backing so the screen centre stays clear.
            var prompt = UiFactory.Place(UiFactory.Rect(_hud.transform, "FirstPrompt"), new Vector2(0.5f, 1f),
                new Vector2(0f, -70f), new Vector2(820f, 90f));
            var promptBacking = prompt.gameObject.AddComponent<Image>();
            promptBacking.color = new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.85f);
            promptBacking.raycastTarget = false;
            var promptText = UiFactory.Label(prompt, "Text", LocalizationKeys.PromptMoveTouch, 34, TextAnchor.MiddleCenter,
                Palette.WarmPaper);
            UiFactory.Stretch(promptText.rectTransform);
            promptText.rectTransform.offsetMin = new Vector2(24f, 6f);
            promptText.rectTransform.offsetMax = new Vector2(-24f, -6f);
            _prompt = prompt.gameObject;
            _prompt.SetActive(false);

            _input.Touch = touch;
        }

        private void BuildTitle(Transform canvas, BuildInfo buildInfo)
        {
            var backdrop = UiFactory.Panel(canvas, "TitleBackdrop", new Color(Palette.TwilightSky.r,
                Palette.TwilightSky.g, Palette.TwilightSky.b, 0.78f));
            backdrop.transform.SetSiblingIndex(0);
            _title = backdrop.gameObject;

            var content = UiFactory.Rect(_title.transform, "Content");
            content.gameObject.AddComponent<SafeAreaFitter>();

            var gameName = UiFactory.Label(content, "GameName", LocalizationKeys.TitleGameName, 96, TextAnchor.MiddleCenter,
                Palette.WarmPaper);
            UiFactory.Place(gameName.rectTransform, new Vector2(0.5f, 0.68f), Vector2.zero, new Vector2(1100f, 130f));
            var caseName = UiFactory.Label(content, "CaseName", LocalizationKeys.TitleCaseName, 44, TextAnchor.MiddleCenter,
                Palette.ClothSilver);
            UiFactory.Place(caseName.rectTransform, new Vector2(0.5f, 0.55f), Vector2.zero, new Vector2(1100f, 70f));

            // Continue appears in S002 once saves exist; New game never needs confirmation without a save.
            var continueButton = UiFactory.TextButton(content, "ContinueButton", LocalizationKeys.TitleContinue,
                new Vector2(0.5f, 0.32f), new Vector2(0f, 60f), new Vector2(420f, 110f), null);
            continueButton.gameObject.SetActive(false);
            _newGameButton = UiFactory.TextButton(content, "NewGameButton", LocalizationKeys.TitleNewGame,
                new Vector2(0.5f, 0.32f), Vector2.zero, new Vector2(420f, 110f), () => _flow.StartNewGame());

            var build = UiFactory.Label(content, "BuildLabel", null, 22, TextAnchor.LowerLeft, Palette.ClothSilver);
            UiFactory.Place(build.rectTransform, new Vector2(0f, 0f), new Vector2(330f, 30f), new Vector2(620f, 40f));
            build.gameObject.AddComponent<LocalizedText>().SetKey(LocalizationKeys.TitleBuildLabel,
                () => new object[] { Application.version, buildInfo.Identifier });

            if (Debug.isDebugBuild)
            {
                var toggle = UiFactory.TextButton(content, "LanguageToggleDev", LocalizationKeys.TitleLanguageToggle,
                    new Vector2(1f, 0f), new Vector2(-220f, 70f), new Vector2(400f, 96f), Localizer.TogglePseudo);
                var label = toggle.GetComponentInChildren<LocalizedText>();
                label.GetComponent<Text>().fontSize = 26;
                label.SetKey(LocalizationKeys.TitleLanguageToggle, () => new object[] { Localizer.Catalog.Active.NativeName });
            }
        }

        private void BuildPause(Transform canvas)
        {
            var backdrop = UiFactory.Panel(canvas, "PauseBackdrop", new Color(Palette.Ink.r, Palette.Ink.g, Palette.Ink.b, 0.8f));
            _pause = backdrop.gameObject;
            var content = UiFactory.Rect(_pause.transform, "Content");
            content.gameObject.AddComponent<SafeAreaFitter>();

            var heading = UiFactory.Label(content, "Heading", LocalizationKeys.PauseHeading, 72, TextAnchor.MiddleCenter,
                Palette.WarmPaper);
            UiFactory.Place(heading.rectTransform, new Vector2(0.5f, 0.7f), Vector2.zero, new Vector2(900f, 110f));
            _resumeButton = UiFactory.TextButton(content, "ResumeButton", LocalizationKeys.PauseResume,
                new Vector2(0.5f, 0.48f), Vector2.zero, new Vector2(420f, 110f), () => _flow.Resume());
            UiFactory.TextButton(content, "BackToTitleButton", LocalizationKeys.PauseBackToTitle,
                new Vector2(0.5f, 0.3f), Vector2.zero, new Vector2(420f, 110f), () => _flow.QuitToTitle());
        }
    }
}
