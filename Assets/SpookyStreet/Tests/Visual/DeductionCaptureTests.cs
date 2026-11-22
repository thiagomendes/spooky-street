using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.Case;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using N = SpookyStreet.Core.Case.CaseNotebook;

namespace SpookyStreet.Tests.Visual
{
    /// <summary>
    /// S002.5a deduction notebook frames (D26, mock docs/evidence/s002/mystery_first/mf_notebook_deduction_sheet.jpg):
    /// an open question with the shared tray (45), a wrong try wobbling home while Pip thinks (46), Pip's musing with
    /// the missing clue's place (47), the solved comic drawn from the child's stickers (48) and the case-closed card (49).
    /// </summary>
    [Category("Visual")]
    public class DeductionCaptureTests : InputTestFixture
    {
        private const int Width = 1278;
        private const int Height = 590;
        private const float MaxMagenta = 0.005f;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            CaseDirector.UseTemporarySaveDirectory();
            SpookyStreet.Controls.ControlSchemeTracker.Override = null;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("No graphics device (-nographics); visual captures need rendering.");
            }
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
            yield return Wait(0.5f);
        }

        private static CaseAction A(CaseActionKind kind) => CaseAction.Of(kind);

        private static IEnumerator OpenOn(string questionId)
        {
            Director.OpenNotebook();
            yield return null;
            var notebook = Director.Notebook;
            notebook.ShowPage(notebook.PageQuestions.IndexOf(N.Question(questionId)));
            yield return Wait(1.0f); // the page settles and the stickers wiggle in
            Director.Captions.Clear();
        }

        private static void Place(params string[] ids)
        {
            var notebook = Director.Notebook;
            foreach (string id in ids)
            {
                notebook.TapSticker(id);
                notebook.TapSlot(notebook.CurrentBoard.FirstEmpty());
            }
        }

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator CapturesTheDeductionNotebook()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            yield return null;
            GameBootstrap.Instance.Ui.NewGameButton.onClick.Invoke();
            yield return Wait(1.0f);
            var notebook = Director.Notebook;

            // 45: "How did June get in?" with two stickers placed and the shared tray (found stickers + "?" outlines).
            yield return Do(A(CaseActionKind.HearKnocks), CaseAction.Talk(Speaker.Bea), CaseAction.Talk(Speaker.Vale),
                A(CaseActionKind.SitOnBench), A(CaseActionKind.ExamineStreamer), A(CaseActionKind.ExamineCloth));
            yield return OpenOn(N.QuestionEntry);
            Place(N.ValeDoor, N.Cloth);
            yield return Wait(0.4f);
            Director.Captions.Clear();
            yield return Shot("45_deduction_tray");

            // 46: the third sticker is the shiny decoy: mid-wobble home, Pip thinking.
            Place(N.Streamer);
            float until = Time.time + 3f;
            while (notebook.Thought == null && Time.time < until)
            {
                yield return null;
            }

            Assert.IsNotNull(notebook.Thought, "46_deduction_wobble: Pip should be thinking");
            yield return Wait(0.18f);
            Director.Captions.Clear();
            yield return Shot("46_deduction_wobble");
            yield return Wait(2.5f);

            // 47: stuck on "Where are the knocks coming from?": Pip muses (listen, then look) and the missing clue's
            // "?" lights with its place (the house), never which stickers go together.
            notebook.ShowPage(notebook.PageQuestions.IndexOf(N.Question(N.QuestionSource)));
            yield return Wait(0.8f);
            notebook.HintButton.onClick.Invoke();
            yield return Wait(0.9f);
            Assert.IsNotNull(notebook.PlaceBadge, "47_deduction_musing: the place badge should show");
            yield return Shot("47_deduction_musing");

            // 48: the right set: the comic from the child's stickers, after it has played and holds.
            notebook.Close();
            yield return Do(A(CaseActionKind.EnterAttic), A(CaseActionKind.ExamineSill));
            yield return OpenOn(N.QuestionEntry);
            Place(N.SillSnag, N.ValeDoor, N.BeaFlash);
            until = Time.time + 4f;
            while (notebook.Checking && Time.time < until)
            {
                yield return null;
            }

            yield return Wait(3.6f);
            Assert.AreEqual(3, notebook.ComicPanels.Count, "48_deduction_comic: three panels");
            Director.Captions.Clear();
            yield return Shot("48_deduction_comic");
            notebook.Close();

            // 49: the rest of the case, the deductions, the porch: the case-closed card.
            yield return Do(CaseAction.Talk(Speaker.June), A(CaseActionKind.EnterFoyer), A(CaseActionKind.LiftLatch),
                A(CaseActionKind.ExamineGrille), A(CaseActionKind.LinkArm), A(CaseActionKind.TurnCrank));
            Assert.IsNotNull(Director.SolveQuestion(N.QuestionSource, new[] { N.FloorHum, N.WoodenPiece }));
            Assert.IsNotNull(Director.SolveQuestion(N.QuestionMoved, new[] { N.OwlBooth, N.LooseArm }));
            Director.Perform(A(CaseActionKind.ArriveAtPorch));
            yield return Wait(FinaleView.IntroSeconds + 0.3f);
            Director.Dialogue.Skip();
            yield return Wait(FinaleView.HoldAfterLines + 1.0f);
            Assert.IsNotNull(notebook.FinalCard, "49_case_closed: the final card should show");
            Director.Captions.Clear();
            yield return Shot("49_case_closed");
            notebook.Close();
        }
    }
}
