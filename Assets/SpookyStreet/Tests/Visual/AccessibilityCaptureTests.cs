using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Controls;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Localization;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Core.Parent;
using SpookyStreet.Localization;
using SpookyStreet.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using N = SpookyStreet.Core.Case.CaseNotebook;

namespace SpookyStreet.Tests.Visual
{
    /// <summary>
    /// S004 stream F2 frames (reserved 77–78, docs/S004-plan.md §3; docs/S004-a11y.md): 77 the deduction tray with the
    /// focus outline (ink band and candy stitch) and the gamepad glyphs; 77b the pause card with its focus back after the
    /// settings card closed; 77c the credits saying they scroll (keyboard); 77d the settings card with its own picture
    /// on every row; 78 the language screen with keyboard focus; 78b Bea's bubble at iPad 4:3, the whole stage fitted;
    /// 78c the Use disc reading "Mergulhar" in the air (pt-BR).
    /// </summary>
    [Category("Visual")]
    public class AccessibilityCaptureTests : InputTestFixture
    {
        private const int Width = 1278;
        private const int Height = 590;
        private const float MaxMagenta = 0.005f;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Gamepad>();
            CaseDirector.UseTemporarySaveDirectory();
            Localizer.ResetForTests();
            ControlSchemeTracker.Override = null;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("No graphics device (-nographics); visual captures need rendering.");
            }
        }

        public override void TearDown()
        {
            ControlSchemeTracker.Override = null;
            LanguageController.DeviceLocalesOverride = null;
            Localizer.ResetForTests();
            base.TearDown();
        }

        private static GameBootstrap Game => GameBootstrap.Instance;

        private static CaseDirector Director => Game.Director;

        private static IEnumerator Shot(string name, Canvas canvas = null, int width = Width, int height = Height)
        {
            yield return FrameCapture.Capture(Game.CameraRig.OutputCamera, canvas != null ? canvas : Director.UiCanvas,
                name, width, height);
            float magenta = FrameCapture.MagentaFraction(FrameCapture.LastFrame);
            Assert.LessOrEqual(magenta, MaxMagenta, $"{name}: {magenta:P1} of the frame is error-shader magenta");
            Assert.Greater(FrameCapture.MeanLuminance(FrameCapture.LastFrame), 0.02f, $"{name}: frame is black");
        }

        private static IEnumerator WaitReal(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
            }
        }

        private static IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }
        }

        private static IEnumerator Do(params CaseAction[] actions)
        {
            foreach (var action in actions)
            {
                Director.Perform(action);
                Director.Dialogue.Skip();
                Director.Cutaway.Skip();
                yield return null;
                yield return null;
            }

            Director.Captions.Clear();
            yield return WaitReal(0.5f);
        }

        private static IEnumerator PassGate()
        {
            var gate = Director.Nonverbal.Menus.Gate;
            gate.TouchDown(1, gate.ZoneA.CenterX, gate.ZoneA.CenterY);
            gate.TouchDown(2, gate.ZoneB.CenterX, gate.ZoneB.CenterY);
            yield return WaitReal(ParentGate.DefaultHoldSeconds + 0.4f);
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator CapturesTheAccessibilityPassFrames()
        {
            yield return Load();
            var menus = Director.Nonverbal.Menus;

            // 77d: the settings card from the title gear, every row with its own picture (keyboard focus on Camera speed).
            ControlSchemeTracker.Override = ControlScheme.KeyboardMouse;
            Game.Ui.TitleSettingsButton.onClick.Invoke();
            yield return null;
            yield return PassGate();
            Assert.IsTrue(menus.SettingsOpen, "77d: the gate passed");
            menus.SettingsNavigation.FocusOn(SettingsCard.CameraSpeed);
            yield return WaitReal(0.3f);
            yield return Shot("77d_settings_pictures", menus.Canvas);

            // 77c: the credits, saying they scroll: the paper fades at the bottom, a chevron, the page-down key.
            menus.SettingsNavigation.FocusOn(SettingsCard.Credits);
            menus.CreditsEntry.onClick.Invoke();
            yield return WaitReal(0.4f);
            var credits = Game.GrownUps.Credits.Sheet;
            Assert.IsTrue(credits.MoreBelowShowing, "77c: the credits say there is more below");
            yield return Shot("77c_credits_scroll_hint", credits.Canvas);
            credits.Close();
            yield return null;
            menus.CloseSettings();
            yield return null;

            // 78: the language screen by keyboard, the outline on the language rows.
            Game.Ui.LanguageButton.onClick.Invoke();
            yield return WaitReal(0.3f);
            var screen = Game.LanguageScreen;
            Assert.IsTrue(screen.IsOpen);
            screen.Navigation.Navigate(UiDirection.Down);
            yield return WaitReal(0.3f);
            Assert.IsTrue(screen.Navigation.Outline != null && screen.Navigation.Outline.Showing, "78: the outline shows");
            yield return Shot("78_language_keyboard", screen.Canvas);
            screen.CloseButton.onClick.Invoke();
            yield return WaitReal(0.3f);

            // Into play: the notebook's tray by gamepad (77).
            Game.Ui.NewGameButton.onClick.Invoke();
            yield return WaitReal(1.0f);
            yield return Do(CaseAction.Of(CaseActionKind.HearKnocks), CaseAction.Of(CaseActionKind.ExamineGrille),
                CaseAction.Of(CaseActionKind.LinkArm), CaseAction.Of(CaseActionKind.TurnCrank),
                CaseAction.Of(CaseActionKind.ExamineOwlBooth));
            Director.OpenNotebook();
            yield return null;
            var notebook = Director.Notebook;
            notebook.ShowPage(notebook.PageQuestions.IndexOf(N.Question(N.QuestionMoved)));
            yield return WaitReal(1.0f);
            ControlSchemeTracker.Override = ControlScheme.Gamepad;
            notebook.Navigate(0, 1);
            notebook.Navigate(1, 0);
            yield return WaitReal(0.6f);
            Director.Captions.Clear();
            Assert.IsTrue(notebook.FocusOutline != null && notebook.FocusOutline.Showing, "77: the outline shows");
            Assert.IsNotNull(notebook.FocusOutline.Edge, "77: the ink band under the stitch");
            Assert.IsTrue(notebook.SubmitPrompt.HasGlyph, "77: the Submit glyph rides the outline");
            Assert.AreEqual(PadButton.South, notebook.SubmitPrompt.LitButton);
            yield return Shot("77_a11y_tray_gamepad");
            notebook.Close();
            yield return WaitReal(0.3f);

            // 77b: the pause card after the settings card closed over it: focus is back on the gear.
            Game.Flow.Pause();
            yield return null;
            var pause = Game.Ui.PauseNavigation;
            pause.FocusOn(ScreenFocus.PauseSettings);
            Director.Nonverbal.SettingsButton.onClick.Invoke();
            yield return null;
            yield return PassGate();
            Assert.IsTrue(menus.SettingsOpen);
            menus.SettingsNavigation.Back();
            yield return WaitReal(0.3f);
            Assert.IsTrue(pause.IsTop, "77b: the pause card takes input back");
            Assert.AreEqual(ScreenFocus.PauseSettings, pause.Focus);
            Assert.IsTrue(pause.Outline.Showing, "77b: with the outline on the gear");
            yield return Shot("77b_pause_focus_back", Game.Ui.GetComponent<Canvas>());
            Game.Flow.Resume();
            yield return WaitReal(0.3f);
        }

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator CapturesTheBubbleAt4x3AndTheDiveVerbInPortuguese()
        {
            LanguageController.DeviceLocalesOverride = new[] { "pt-BR" };
            yield return Load();
            Assert.AreEqual("pt-BR", Localizer.ActiveLocale);
            Game.Ui.NewGameButton.onClick.Invoke();
            yield return WaitReal(1.0f);

            // 78c: in the air, the Use disc reads "Mergulhar" whole.
            Game.Pip.Teleport(new Vector3(-1.5f, 7f, -4f));
            float until = Time.time + 1f;
            while (Game.Ui.UseVerbKey != LocalizationKeys.HudDive && Time.time < until)
            {
                yield return null;
            }

            Assert.AreEqual(LocalizationKeys.HudDive, Game.Ui.UseVerbKey, "78c: the disc reads Dive in the air");
            yield return Shot("78c_dive_tag_ptbr", Game.Ui.GetComponent<Canvas>());
            yield return WaitReal(1.5f);

            // 78b: Bea's bubble at iPad 4:3 (1024 × 768): the stage scales down so the face disc and the next disc
            // stay on screen (it filled the width and was cut at both edges in 79b).
            var bea = CaseStage.Find(CaseStage.PointBea).Position;
            var bike = CaseStage.Find(CaseStage.PointBicycle).Position;
            var beaAt = new Vector3(bea.X, 0f, bea.Z);
            Vector3 away = (beaAt - new Vector3(bike.X, 0f, bike.Z)).normalized;
            Game.Pip.Teleport(beaAt + away * 2.4f + Vector3.up * 0.1f);
            Game.Pip.transform.rotation = Quaternion.LookRotation(-away, Vector3.up);
            Game.CameraRig.Orbit.RequestRecenter();
            yield return WaitReal(1.5f);
            Director.Perform(CaseAction.Talk(Speaker.Bea));
            yield return WaitReal(1.0f);
            Assert.IsTrue(Director.Dialogue.IsOpen && Director.Dialogue.ShowsVignette, "78b: Bea's bubble");
            yield return CheckFitsAt(1024, 768);
            yield return Shot("78b_bubble_4x3", null, 1024, 768);
            Director.Dialogue.Skip();
        }

        /// <summary>
        /// Lays the dialogue canvas out at <paramref name="width"/> × <paramref name="height"/> (as the capture does) and
        /// checks the bubble, the face disc and the next disc all sit inside the screen.
        /// </summary>
        private static IEnumerator CheckFitsAt(int width, int height)
        {
            var canvas = Director.UiCanvas;
            var camera = Game.CameraRig.OutputCamera;
            var target = new RenderTexture(width, height, 24);
            var previousMode = canvas.renderMode;
            var previousCamera = canvas.worldCamera;
            var previousTarget = camera.targetTexture;
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            camera.targetTexture = target;
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            var dialogue = Director.Dialogue;
            dialogue.FitStage();
            Canvas.ForceUpdateCanvases();
            Assert.Less(dialogue.StageScale, 1f, "4:3 scales the speech stage down");
            var screenRect = new Rect(0f, 0f, width, height);
            foreach (string part in new[] { "Stage/SpeechBubble", "Stage/Portrait", "Stage/SpeechBubble/NextDisc" })
            {
                var rect = (RectTransform)dialogue.transform.Find("Content/" + part);
                Assert.IsNotNull(rect, part);
                var corners = new Vector3[4];
                rect.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    Vector2 p = RectTransformUtility.WorldToScreenPoint(camera, corner);
                    Assert.IsTrue(p.x >= -2f && p.x <= width + 2f, $"78b: {part} corner {p} is off the 4:3 screen");
                }
            }

            camera.targetTexture = previousTarget;
            canvas.renderMode = previousMode;
            canvas.worldCamera = previousCamera;
            target.Release();
            Object.Destroy(target);
            yield return null;
        }
    }
}
