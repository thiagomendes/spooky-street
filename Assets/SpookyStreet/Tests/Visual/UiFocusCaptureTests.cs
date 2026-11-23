using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Controls;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Nonverbal;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using N = SpookyStreet.Core.Case.CaseNotebook;

namespace SpookyStreet.Tests.Visual
{
    /// <summary>
    /// S004 F1 frames (reserved 80–81, docs/S004-plan.md §3): the stitched UI focus outline on the notebook, driven
    /// through the shared UiNavigate actions: on a tray sticker with the keyboard (80), and on a slot with a sticker held
    /// on the gamepad (81).
    /// </summary>
    [Category("Visual")]
    public class UiFocusCaptureTests : InputTestFixture
    {
        private const int Width = 1278;
        private const int Height = 590;
        private const float MaxMagenta = 0.005f;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            CaseDirector.UseTemporarySaveDirectory();
            ControlSchemeTracker.Override = null;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("No graphics device (-nographics); visual captures need rendering.");
            }
        }

        public override void TearDown()
        {
            ControlSchemeTracker.Override = null;
            base.TearDown();
        }

        private static CaseDirector Director => GameBootstrap.Instance.Director;

        private static IEnumerator Shot(string name)
        {
            var game = GameBootstrap.Instance;
            yield return FrameCapture.Capture(game.CameraRig.OutputCamera, game.Director.UiCanvas, name, Width, Height);
            float magenta = FrameCapture.MagentaFraction(FrameCapture.LastFrame);
            Assert.LessOrEqual(magenta, MaxMagenta, $"{name}: {magenta:P1} of the frame is error-shader magenta");
            Assert.Greater(FrameCapture.MeanLuminance(FrameCapture.LastFrame), 0.02f, $"{name}: frame is black");
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                yield return null;
            }
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator CapturesTheFocusOutlineOnTheNotebook()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            yield return null;
            GameBootstrap.Instance.Ui.NewGameButton.onClick.Invoke();
            yield return Wait(1.0f);
            foreach (var action in new[]
                     {
                         CaseAction.Of(CaseActionKind.HearKnocks), CaseAction.Of(CaseActionKind.ExamineGrille),
                         CaseAction.Of(CaseActionKind.LinkArm), CaseAction.Of(CaseActionKind.TurnCrank),
                     })
            {
                Director.Perform(action);
                Director.Dialogue.Skip();
                Director.Cutaway.Skip();
                yield return null;
                yield return null;
            }

            yield return Wait(0.5f);
            Director.OpenNotebook();
            yield return null;
            var notebook = Director.Notebook;
            notebook.ShowPage(notebook.PageQuestions.IndexOf(N.Question(N.QuestionMoved)));
            yield return Wait(1.0f);

            // 80: keyboard: the cursor on the second tray sticker, ringed by the candy-corn stitch.
            ControlSchemeTracker.Override = ControlScheme.KeyboardMouse;
            notebook.Navigate(0, 1);
            notebook.Navigate(1, 0);
            yield return Wait(0.6f);
            Director.Captions.Clear();
            Assert.IsTrue(notebook.FocusOutline != null && notebook.FocusOutline.Showing, "80: the outline shows");
            Assert.AreEqual(CursorRow.Tray, notebook.CursorRow);
            yield return Shot("80_ui_focus_tray");

            // 81: gamepad: the sticker is held (lifted, orange stitch) and the outline has followed to the first slot.
            ControlSchemeTracker.Override = ControlScheme.Gamepad;
            notebook.Submit();
            yield return Wait(0.6f);
            Director.Captions.Clear();
            Assert.IsNotNull(notebook.HeldSticker, "81: a sticker is held");
            Assert.AreEqual(CursorRow.Slots, notebook.CursorRow);
            Assert.AreSame(notebook.Slots[notebook.CursorIndex].rectTransform, notebook.FocusOutline.Target);
            yield return Shot("81_ui_focus_slot");
            notebook.Close();
        }
    }
}
