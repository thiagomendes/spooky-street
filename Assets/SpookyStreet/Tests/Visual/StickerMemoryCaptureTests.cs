using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Controls;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Localization;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using N = SpookyStreet.Core.Case.CaseNotebook;

namespace SpookyStreet.Tests.Visual
{
    /// <summary>
    /// D32 sticker memory frames (docs/S004-sticker-memory.md): 82 the silver cloth's memory opened by a tap (English,
    /// mid-drift on the treehouse ramp), 83 the loose arm's memory in Brazilian Portuguese (an interior memory drawn
    /// while Pip stands outside), 84 the decoy streamer's memory opened by Inspect on a gamepad, its glyph by the outline.
    /// </summary>
    [Category("Visual")]
    public class StickerMemoryCaptureTests : InputTestFixture
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
            ControlSchemeTracker.Override = ControlScheme.Touch;
            Localizer.ResetForTests();
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("No graphics device (-nographics); visual captures need rendering.");
            }
        }

        public override void TearDown()
        {
            ControlSchemeTracker.Override = null;
            Localizer.ResetForTests();
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

        /// <summary>Lets the memory run <paramref name="seconds"/>, then holds the notebook's clock for the capture.</summary>
        private static IEnumerator RunThenHold(float seconds)
        {
            var notebook = Director.Notebook;
            yield return Wait(seconds);
            notebook.CanAnimate = () => false;
            Director.Captions.Clear();
        }

        private static void AssertPicture(string frame, string stickerId)
        {
            var notebook = Director.Notebook;
            Assert.AreEqual(stickerId, notebook.MemorySticker, frame + ": the memory is open");
            Assert.IsTrue(notebook.MemoryCard.ShowsPlace, frame + ": the real place is drawn");
            Assert.Greater(notebook.MemoryCard.RenderedFrames, 2, frame + ": the picture has been drawn");
        }

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator CapturesStickerMemories()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            yield return null;
            GameBootstrap.Instance.Ui.NewGameButton.onClick.Invoke();
            yield return Wait(1.0f);
            var notebook = Director.Notebook;
            var animate = notebook.CanAnimate;

            // 82: a tap on the silver cloth lifts it and opens its memory: the ramp, drifting in, the caption below.
            yield return Do(A(CaseActionKind.HearKnocks), CaseAction.Talk(Speaker.Bea), CaseAction.Talk(Speaker.Vale),
                A(CaseActionKind.SitOnBench), A(CaseActionKind.ExamineStreamer), A(CaseActionKind.ExamineCloth));
            yield return OpenOn(N.QuestionEntry);
            notebook.TapSticker(N.Cloth);
            yield return RunThenHold(1.5f);
            AssertPicture("82", N.Cloth);
            Assert.AreEqual(N.Cloth, notebook.HeldSticker, "82: the tap also picked the sticker up");
            yield return Shot("82_sticker_memory_cloth");
            notebook.CanAnimate = animate;
            notebook.CloseMemory();

            // 84: the gamepad: the outline on the decoy streamer, its Inspect glyph (West), its memory.
            ControlSchemeTracker.Override = ControlScheme.Gamepad;
            notebook.TapSticker(N.Cloth); // let go of the cloth
            notebook.CloseMemory();
            Assert.IsNull(notebook.HeldSticker);
            notebook.Navigate(0, 1);
            int index = -1;
            for (int i = 0; i < notebook.TrayStickers.Count; i++)
            {
                if (notebook.TrayStickers[i].StickerId == N.Streamer)
                {
                    index = i;
                }
            }

            Assert.GreaterOrEqual(index, 0, "84: the streamer is in the tray");
            notebook.Navigate(-9, 0);
            for (int i = 0; i < index; i++)
            {
                notebook.Navigate(1, 0);
            }

            yield return null;
            notebook.InspectAtCursor();
            yield return RunThenHold(2.7f);
            AssertPicture("84", N.Streamer);
            Assert.IsNull(notebook.HeldSticker, "84: Inspect never picks up");
            Assert.IsTrue(notebook.InspectPrompt != null && notebook.InspectPrompt.HasGlyph, "84: the Inspect glyph shows");
            Assert.AreEqual(PadButton.West, notebook.InspectPrompt.LitButton);
            yield return Shot("84_sticker_memory_streamer_pad");
            notebook.CanAnimate = animate;
            notebook.Close();
            ControlSchemeTracker.Override = ControlScheme.Touch;

            // 83: Brazilian Portuguese: the loose arm's memory in the workshop, drawn while Pip is out on the street.
            yield return Do(A(CaseActionKind.ExamineGrille), A(CaseActionKind.LinkArm), A(CaseActionKind.TurnCrank));
            var pt = Resources.Load<TextAsset>("Localization/pt-BR");
            Assert.IsNotNull(pt, "pt-BR ships in the app");
            Localizer.Catalog.SetActive(StringTable.Parse(pt.text));
            yield return OpenOn(N.QuestionMoved);
            Assert.IsTrue(notebook.InspectSticker(N.LooseArm));
            yield return RunThenHold(2.0f);
            AssertPicture("83", N.LooseArm);
            Localizer.Catalog.Active.TryGet(StickerMemories.For(N.LooseArm).CaptionKey, out string caption);
            Assert.AreEqual(caption, notebook.MemoryCard.Caption.text, "83: the caption is the Portuguese line");
            yield return Shot("83_sticker_memory_arm_ptbr");
            notebook.CanAnimate = animate;
            notebook.Close();
            Localizer.Catalog.ResetToFallback();
        }
    }
}
