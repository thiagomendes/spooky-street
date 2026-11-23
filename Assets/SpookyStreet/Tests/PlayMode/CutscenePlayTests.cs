using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Cutscenes;
using SpookyStreet.Core.Flow;
using SpookyStreet.Localization;
using SpookyStreet.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using N = SpookyStreet.Core.Case.CaseNotebook;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// S002.5b cutscenes in the running game (D28): a new game plays the opening and gives control on the arrival
    /// porch; skip works on touch, keyboard and gamepad; the third deduction plays the reveal once, built from the
    /// child's stickers; the notebook rewatches both; Continue and a reload never replay a seen cutscene; pause freezes
    /// it. Fresh temporary save folder per test.
    /// </summary>
    public class CutscenePlayTests : InputTestFixture
    {
        private Keyboard _keyboard;
        private Gamepad _gamepad;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _gamepad = InputSystem.AddDevice<Gamepad>();
            Localizer.ResetForTests();
            CaseDirector.UseTemporarySaveDirectory(playOpening: true);
            AmbientMotion.ReducedMotion = false;
        }

        public override void TearDown()
        {
            AmbientMotion.ReducedMotion = false;
            CaseCutscenes.AutoPlayOpening = false;
            base.TearDown();
        }

        private static GameBootstrap Game => GameBootstrap.Instance;

        private static CaseDirector Director => GameBootstrap.Instance.Director;

        private static CaseCutscenes Cutscenes => Director.Cutscenes;

        private static CutscenePlayer Player => Cutscenes.Player;

        private static IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(GameBootstrap.Instance);
        }

        private static IEnumerator NewGame()
        {
            yield return LoadGame();
            Game.Ui.NewGameButton.onClick.Invoke();
            yield return Frames(3);
            Assert.AreEqual(GameState.Playing, Game.Flow.State);
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                yield return null;
            }
        }

        private static IEnumerator UntilEnded(float limit)
        {
            float until = Time.time + limit;
            while (Player.Active && Time.time < until)
            {
                yield return null;
            }

            Assert.IsFalse(Player.Active, "the cutscene ends by itself");
        }

        private IEnumerator Tap(ButtonControl button)
        {
            Press(button, queueEventOnly: true);
            yield return null;
            yield return null;
            Release(button, queueEventOnly: true);
            yield return null;
        }

        private static float HudAlpha => Game.Ui.Hud.GetComponent<CanvasGroup>().alpha;

        private static void AssertOnThePorchInControl()
        {
            var pip = Game.Pip.transform.position;
            Assert.IsTrue(CaseStage.OnArrivalPorch(pip.x, pip.z), "play starts on the arrival porch: " + pip);
            Assert.Less(Vector3.Distance(pip, new Vector3(0f, 0.62f, -13.4f)), 0.15f, "at the spawn");
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(Game.Pip.transform.eulerAngles.y, 0f)), 1f, "facing the owl house");
            Assert.IsTrue(Game.Controls.GameplayEnabled, "control is back");
            Assert.IsFalse(Director.ModalOpen);
            Assert.AreEqual(1f, HudAlpha, "the HUD is back");
            Assert.AreEqual(Vector2.zero, Game.Controls.ScriptedMove, "nothing pushes the stick any more");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ANewGamePlaysTheOpeningThenGivesControlOnThePorch()
        {
            yield return NewGame();
            Assert.IsTrue(Cutscenes.Playing, "the opening plays before control");
            Assert.AreEqual(C01Cutscenes.OpeningId, Player.LastId);
            Assert.IsFalse(Game.Controls.GameplayEnabled, "controls are off while it plays");
            Assert.AreEqual(0f, HudAlpha, "the HUD steps aside");
            CollectionAssert.Contains(Cutscenes.Progress.Seen, C01Cutscenes.OpeningId, "marked seen before it plays");
            Assert.IsNotNull(Cutscenes.Boxes, "moving boxes on the porch");

            // Pip steps out onto the porch through the motor (the real walk), then the porch composition.
            float startZ = float.NaN;
            float walked = Game.Pip.DistanceWalked;
            var lines = new HashSet<string>();
            while (Player.Active)
            {
                var playback = Player.Playback;
                if (playback != null && playback.Beat != null)
                {
                    if (playback.Beat.Id == "boxes" && float.IsNaN(startZ))
                    {
                        startZ = Game.Pip.transform.position.z;
                    }

                    if (Player.Line.gameObject.activeInHierarchy)
                    {
                        lines.Add(Player.Line.text);
                    }

                    Assert.IsFalse(Game.Controls.GameplayEnabled);
                }

                yield return null;
            }

            Assert.Less(startZ, -14.4f, "Pip starts at the back of the porch");
            Assert.Greater(Game.Pip.DistanceWalked - walked, 0.8f, "Pip walked onto the porch");
            Assert.GreaterOrEqual(lines.Count, 3, "the text line plays its captions");
            yield return Frames(3);
            AssertOnThePorchInControl();
            Assert.AreEqual(0, Director.State.Count, "the three knocks happen in play, not in the opening");
            Assert.IsFalse(Player.LastSkipped);

            // The child can walk at once: the knocks come during the first steps.
            Press(_keyboard.wKey);
            yield return Wait(1.2f);
            Release(_keyboard.wKey);
            yield return Frames(3);
            Assert.IsTrue(Director.State.Has(CaseFact.HeardKnocks), "the arrival knocks play in play");
            Assert.AreEqual(1, Player.PlayCount);
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator SkipWorksOnTouchKeyboardAndGamepad()
        {
            // Touch: the skip disc skips at once.
            yield return NewGame();
            yield return Wait(1f);
            Player.Frame.SkipButton.onClick.Invoke();
            Assert.IsTrue(Player.LastSkipped);
            yield return Wait(CutscenePlayback.SkipExitBlend + 0.2f);
            Assert.IsFalse(Player.Active);
            AssertOnThePorchInControl();

            // Touch elsewhere asks first ("Press again to skip"), the second tap skips.
            yield return NewGame();
            yield return Wait(1f);
            Player.Frame.TapCatcher.onClick.Invoke();
            Assert.IsTrue(Player.Active, "one stray tap never skips");
            Assert.IsTrue(Player.Gate.Armed);
            yield return Frames(2);
            Assert.AreEqual(LocalizationKeysCutscenes.SkipAgain, Player.Frame.SkipCaption.GetComponent<LocalizedText>().Key);
            Player.Frame.TapCatcher.onClick.Invoke();
            Assert.IsTrue(Player.LastSkipped);
            yield return Wait(CutscenePlayback.SkipExitBlend + 0.2f);
            AssertOnThePorchInControl();

            // Keyboard: Space twice, in the middle of Pip's walk.
            yield return NewGame();
            while (Player.Playback.Beat == null || Player.Playback.Beat.Id != "boxes" || Player.Playback.BeatTime < 1f)
            {
                yield return null;
            }

            yield return Tap(_keyboard.spaceKey);
            Assert.IsTrue(Player.Active && Player.Gate.Armed);
            yield return Tap(_keyboard.spaceKey);
            Assert.IsTrue(Player.LastSkipped, "keyboard skips");
            yield return Wait(CutscenePlayback.SkipExitBlend + 0.2f);
            AssertOnThePorchInControl();

            // Gamepad: South twice, under reduced motion (a cut back, no blend).
            AmbientMotion.ReducedMotion = true;
            yield return NewGame();
            Assert.IsTrue(Player.ReducedMotion);
            Assert.AreEqual(1f, Player.Playback.Letterbox, "reduced motion: the bars are simply there");
            yield return Wait(2f);
            yield return Tap(_gamepad.buttonSouth);
            yield return Tap(_gamepad.buttonSouth);
            Assert.IsTrue(Player.LastSkipped, "gamepad skips");
            Assert.IsFalse(Player.Active, "reduced motion cuts straight back");
            yield return Frames(3);
            AssertOnThePorchInControl();
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator PauseFreezesTheCutscene()
        {
            yield return NewGame();
            yield return Wait(1.5f);
            Game.Flow.Pause();
            yield return null;
            float elapsed = Player.Playback.Elapsed;
            float until = Time.realtimeSinceStartup + 0.6f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
            }

            Assert.AreEqual(elapsed, Player.Playback.Elapsed, "paused: it does not run");
            Player.Frame.SkipButton.onClick.Invoke();
            Assert.IsFalse(Player.LastSkipped, "a tap while paused belongs to the pause menu");
            Game.Flow.Resume();
            yield return Wait(0.5f);
            Assert.Greater(Player.Playback.Elapsed, elapsed, "it runs again");
            Assert.IsFalse(Game.Controls.GameplayEnabled, "resume keeps controls off while it plays");
            Player.SkipNow();
            yield return Wait(CutscenePlayback.SkipExitBlend + 0.2f);
            AssertOnThePorchInControl();
        }

        /// <summary>Plays the case to the reopening with every clue found and nothing solved.</summary>
        private static IEnumerator PlayToTheReopening()
        {
            var director = Director;
            foreach (var action in new[]
                     {
                         CaseAction.Of(CaseActionKind.HearKnocks), CaseAction.Talk(Speaker.Bea), CaseAction.Talk(Speaker.Vale),
                         CaseAction.Of(CaseActionKind.SitOnBench), CaseAction.Of(CaseActionKind.ExamineCloth),
                         CaseAction.Of(CaseActionKind.EnterAttic), CaseAction.Of(CaseActionKind.ExamineSill),
                         CaseAction.Talk(Speaker.June), CaseAction.Of(CaseActionKind.EnterFoyer),
                         CaseAction.Of(CaseActionKind.LiftLatch), CaseAction.Of(CaseActionKind.ExamineGrille),
                         CaseAction.Of(CaseActionKind.LinkArm), CaseAction.Of(CaseActionKind.TurnCrank),
                     })
            {
                director.Perform(action);
                director.Dialogue.Skip();
                director.Cutaway.Skip();
                yield return Frames(2);
            }

            director.Captions.Clear();
            yield return Wait(0.3f);
            director.Perform(CaseAction.Of(CaseActionKind.ArriveAtPorch));
            Assert.IsTrue(director.State.Has(CaseFact.CaseClosed));
            Assert.IsTrue(director.FinalePlaying, "the reopening (the world action's show) plays");
            yield return Wait(FinaleView.IntroSeconds + 0.3f);
            while (director.Dialogue.IsOpen)
            {
                director.Dialogue.Skip();
                yield return Frames(2);
            }

            yield return Wait(FinaleView.HoldAfterLines + 0.4f);
            Assert.IsTrue(director.Notebook.IsOpen, "Kit asks; the notebook opens");
            Assert.IsFalse(Cutscenes.Playing, "no reveal before the child's deductions");
            Assert.IsFalse(Cutscenes.RevealPending);
        }

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator TheThirdDeductionPlaysTheRevealOnceAndAReloadNeverReplaysIt()
        {
            CaseCutscenes.AutoPlayOpening = false;
            yield return NewGame();
            yield return PlayToTheReopening();
            var director = Director;
            director.Notebook.Close();
            yield return Frames(2);

            // Never ahead of the child: the reveal refuses to play before the stamp.
            Assert.IsFalse(Cutscenes.Play(C01Cutscenes.RevealId, false));
            Assert.IsFalse(Cutscenes.WatchAgain(C01Cutscenes.RevealId));

            var entry = new[] { N.SillSnag, N.ValeDoor, N.BeaFlash };
            Assert.IsNotNull(director.SolveQuestion(N.QuestionSource, new[] { N.WoodenPiece, N.FloorHum }));
            Assert.IsNotNull(director.SolveQuestion(N.QuestionEntry, entry));
            yield return Frames(5);
            Assert.IsFalse(Cutscenes.Playing, "two deductions: no reveal yet");
            Assert.IsNotNull(director.SolveQuestion(N.QuestionMoved, new[] { N.LooseArm, N.OwlBooth }));
            Assert.IsTrue(director.State.Has(CaseFact.CaseStamped));
            yield return Frames(2);
            Assert.IsTrue(Cutscenes.Playing, "the third deduction plays the reveal");
            Assert.AreEqual(C01Cutscenes.RevealId, Player.LastId);
            Assert.IsFalse(director.FinalePlaying, "never on top of the reopening");
            Assert.IsFalse(Game.Controls.GameplayEnabled);
            var expected = new List<string>();
            expected.AddRange(CaseDeduction.Comic(N.Question(N.QuestionEntry), entry));
            expected.AddRange(CaseDeduction.Comic(N.Question(N.QuestionSource), new[] { N.WoodenPiece, N.FloorHum }));
            expected.AddRange(CaseDeduction.Comic(N.Question(N.QuestionMoved), new[] { N.LooseArm, N.OwlBooth }));
            CollectionAssert.AreEqual(expected, Player.Current.Stickers, "staged from the child's own stickers");

            // The child's sticker shows on each sticker shot; the page turns between the street and the interior.
            var stickers = new HashSet<string>();
            bool turned = false;
            bool pipHidden = false;
            while (Player.Active)
            {
                if (Player.Sticker != null && Player.Playback?.Beat?.StickerId != null)
                {
                    stickers.Add(Player.Playback.Beat.StickerId);
                }

                turned |= Player.PageTurning;
                pipHidden |= Game.Pip.GetComponentsInChildren<Renderer>().All(r => !r.enabled);
                yield return null;
            }

            CollectionAssert.AreEquivalent(expected, stickers);
            Assert.IsTrue(turned, "the page turns into the house");
            Assert.IsTrue(pipHidden, "Pip stays out of the reveal's frames");
            yield return Frames(3);
            Assert.IsTrue(Game.Controls.GameplayEnabled, "control comes back cleanly");
            Assert.AreEqual(1f, HudAlpha);
            Assert.IsTrue(Game.Pip.GetComponentsInChildren<Renderer>().Any(r => r.enabled), "Pip is back");
            if (director.Cast != null && director.Cast.Kit != null)
            {
                Assert.IsTrue(director.Cast.Kit.gameObject.activeSelf, "Kit is back at her booth");
                Assert.IsTrue(director.Cast.June.gameObject.activeSelf, "June is back on the porch");
            }

            if (director.House != null)
            {
                Assert.IsTrue(director.House.DoorOpen, "the door is as the facts say");
                Assert.IsFalse(director.House.ForceInteriorVisible);
            }

            // Once: nothing plays it again by itself.
            yield return Wait(2f);
            Assert.AreEqual(1, Player.PlayCount);
            CollectionAssert.Contains(Cutscenes.Progress.Seen, C01Cutscenes.RevealId);

            // Reload and Continue: no cutscene plays by itself, the stamp and the answers are kept.
            CaseCutscenes.AutoPlayOpening = true;
            yield return LoadGame();
            Assert.IsTrue(Director.State.Has(CaseFact.CaseStamped));
            Game.Ui.ContinueButton.onClick.Invoke();
            yield return Frames(10);
            Assert.AreEqual(GameState.Playing, Game.Flow.State);
            Assert.IsFalse(Cutscenes.Playing, "Continue never replays a seen cutscene");
            Assert.IsFalse(Cutscenes.RevealPending);
            Assert.AreEqual(0, Player.PlayCount);
            CollectionAssert.Contains(Cutscenes.Progress.Seen, C01Cutscenes.RevealId, "still seen after the reload");
            Assert.IsTrue(Game.Controls.GameplayEnabled);
        }

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator TheNotebookRewatchesBothCutscenes()
        {
            CaseCutscenes.AutoPlayOpening = false;
            yield return NewGame();
            yield return PlayToTheReopening();
            var director = Director;
            var notebook = director.Notebook;
            var rewatch = Cutscenes.Rewatch;

            // Before the stamp: the opening is offered from any page, the reveal is not.
            Assert.IsTrue(notebook.IsOpen);
            yield return Frames(2);
            Assert.IsTrue(rewatch.OpeningButton.gameObject.activeInHierarchy, "the opening is rewatchable");
            Assert.IsNull(rewatch.RevealButton, "no reveal before the case is stamped");
            var pipBefore = Game.Pip.transform.position;
            rewatch.OpeningButton.onClick.Invoke();
            yield return Frames(2);
            Assert.IsTrue(Cutscenes.Playing);
            Assert.IsTrue(Player.Rewatching);
            Assert.IsFalse(notebook.IsOpen, "the notebook steps aside");
            yield return Wait(2f);
            Player.SkipNow();
            yield return Wait(CutscenePlayback.SkipExitBlend + 0.3f);
            Assert.IsFalse(Cutscenes.Playing);
            Assert.IsTrue(notebook.IsOpen, "back to the notebook");
            Assert.Less(Vector3.Distance(pipBefore, Game.Pip.transform.position), 0.05f, "a rewatch never moves Pip");
            notebook.Close();
            yield return Frames(2);

            // Solve, let the reveal start and skip it, then rewatch it from the case page with the gamepad.
            director.SolveQuestion(N.QuestionSource, new[] { N.FloorHum, N.WoodenPiece });
            director.SolveQuestion(N.QuestionEntry, new[] { N.ValeDoor, N.Cloth, N.SillSnag });
            director.SolveQuestion(N.QuestionMoved, new[] { N.OwlBooth, N.LooseArm });
            yield return Frames(3);
            Assert.IsTrue(Cutscenes.Playing);
            int plays = Player.PlayCount;
            yield return Wait(1f);
            Player.SkipNow();
            yield return Wait(CutscenePlayback.SkipExitBlend + 0.3f);
            director.OpenNotebook();
            yield return Frames(3);
            Assert.IsNotNull(notebook.FinalCard, "the case page");
            Assert.IsNotNull(rewatch.RevealButton, "the case page offers the reveal");
            rewatch.RevealButton.onClick.Invoke();
            yield return Frames(2);
            Assert.IsTrue(Cutscenes.Playing && Player.Rewatching);
            Assert.AreEqual(C01Cutscenes.RevealId, Player.LastId);
            Assert.AreEqual(plays + 1, Player.PlayCount);
            yield return UntilEnded(45f);
            yield return Frames(3);
            Assert.IsTrue(notebook.IsOpen, "back to the case page after watching it through");

            // Keyboard and gamepad rewatch too: V on the case page plays the reveal.
            notebook.ShowPage(notebook.PageCount - 1);
            yield return Frames(2);
            yield return Tap(_keyboard.vKey);
            yield return Frames(2);
            Assert.IsTrue(Cutscenes.Playing, "V rewatches");
            Assert.AreEqual(C01Cutscenes.RevealId, Player.LastId);
            Player.SkipNow();
            yield return Wait(CutscenePlayback.SkipExitBlend + 0.3f);
            Assert.IsTrue(notebook.IsOpen);
            yield return Tap(_gamepad.buttonNorth);
            yield return Frames(2);
            Assert.IsTrue(Cutscenes.Playing, "gamepad North rewatches");
            Player.SkipNow();
            yield return Wait(CutscenePlayback.SkipExitBlend + 0.3f);
            notebook.Close();
            yield return Frames(3);
            Assert.IsTrue(Game.Controls.GameplayEnabled);
        }
    }

    /// <summary>The cutscene keys used by these tests (kept local so the test reads plainly).</summary>
    internal static class LocalizationKeysCutscenes
    {
        public const string SkipAgain = SpookyStreet.Core.Localization.LocalizationKeys.Cutscenes.SkipAgain;
    }
}
