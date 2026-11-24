using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.Animation;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Save;
using SpookyStreet.Core.World;
using SpookyStreet.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Lines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;
using NoteKeys = SpookyStreet.Core.Localization.LocalizationKeys.Notebook;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// S002 wiring, exterior half: Use targets report case actions, the dialogue card and caption strip play the
    /// step's lines, the arrival cue plays once, the notebook shows the case, and saves restore the facts.
    /// Every test runs against a fresh temporary save folder.
    /// </summary>
    public class CaseWiringTests : InputTestFixture
    {
        private const string SceneName = "LanternLane";

        private Keyboard _keyboard;
        private string _saves;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            Localizer.ResetForTests();
            _saves = CaseDirector.UseTemporarySaveDirectory();
        }

        private static IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(GameBootstrap.Instance, "scene did not create GameBootstrap");
            Assert.IsNotNull(GameBootstrap.Instance.Director, "GameBootstrap did not create the case director");
        }

        private static IEnumerator StartGame()
        {
            yield return LoadGame();
            GameBootstrap.Instance.Ui.NewGameButton.onClick.Invoke();
            yield return Frames(10);
            Assert.AreEqual(GameState.Playing, GameBootstrap.Instance.Flow.State);
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

        /// <summary>Queued so the press is processed in the next frame's input update, before Update reads it.</summary>
        private IEnumerator Tap(KeyControl key)
        {
            Press(key, queueEventOnly: true);
            yield return null;
            yield return null;
            Release(key, queueEventOnly: true);
            yield return null;
        }

        /// <summary>Puts Pip <paramref name="distance"/> m from a stage point, on the side away from <paramref name="away"/>.</summary>
        private static IEnumerator StandAt(string pointId, Vector3 away, float distance)
        {
            var game = GameBootstrap.Instance;
            var point = CaseStage.Find(pointId);
            var at = new Vector3(point.Position.X, point.Position.Y, point.Position.Z);
            away.y = 0f;
            game.Pip.Teleport(at + away.normalized * distance + Vector3.up * 0.1f);
            yield return Wait(0.5f); // settle and let the interaction cool-down pass
            var target = game.Director.Targets.Current;
            Assert.IsNotNull(target, "no Use target near " + pointId);
            Assert.AreEqual(pointId, target.Id, "wrong Use target selected");
            Assert.AreEqual(CaseInteractions.VerbKey(target.Action.Kind), game.Ui.UseVerbKey, "Use disc shows the verb");
        }

        private static Vector3 PointPosition(string id)
        {
            var p = CaseStage.Find(id).Position;
            return new Vector3(p.X, p.Y, p.Z);
        }

        /// <summary>Beside Bea on the plaza side, away from her bicycle.</summary>
        private static IEnumerator StandByBea() =>
            StandAt(CaseStage.PointBea, PointPosition(CaseStage.PointBea) - PointPosition(CaseStage.PointBicycle), 1.0f);

        /// <summary>
        /// uGUI draws nothing when a line does not fit its rect (Andika's line box is ~1.5 × the font size): every
        /// non-empty label must show at least one character (CI run 45 frames).
        /// </summary>
        private static void AssertEveryLabelDraws(GameObject root)
        {
            Canvas.ForceUpdateCanvases();
            foreach (var text in root.GetComponentsInChildren<UnityEngine.UI.Text>())
            {
                if (string.IsNullOrEmpty(text.text))
                {
                    continue;
                }

                var settings = text.GetGenerationSettings(text.rectTransform.rect.size);
                var generator = new TextGenerator();
                generator.Populate(text.text, settings);
                Assert.Greater(generator.characterCountVisible, 0, $"{text.name} ('{text.text}') draws nothing: rect too short");
            }
        }

        [UnityTest]
        public IEnumerator TalkingToBeaRecordsBeaSpokeAndPlaysHerLine()
        {
            yield return StartGame();
            var game = GameBootstrap.Instance;
            var director = game.Director;
            yield return StandByBea();

            yield return Tap(_keyboard.eKey);
            Assert.IsTrue(director.State.Has(CaseFact.BeaSpoke), "talking to Bea records beaSpoke");
            Assert.IsTrue(director.Dialogue.IsOpen, "her line plays on the speech card");
            Assert.IsTrue(director.Captions.Raised, "captions lift above the speech card");
            Assert.AreEqual(Speaker.Bea, director.Dialogue.CurrentLine.Speaker);
            Assert.AreEqual(Lines.BeaSawCape, director.Dialogue.CurrentLine.Key);
            Assert.AreEqual("Something silver flashed up high, by the side of the house!", director.Dialogue.CurrentText);
            yield return null;
            AssertEveryLabelDraws(director.Dialogue.gameObject);
            Assert.IsFalse(game.Controls.GameplayEnabled, "the conversation holds gameplay input");
            Assert.IsTrue(File.Exists(SaveSlotPaths.ForCase(_saves).Latest), "a gained fact autosaves");

            var bea = game.Witnesses != null ? game.Witnesses.Bea : null;
            if (bea != null && bea.Avatar.IsAnimated)
            {
                yield return Frames(2);
                Assert.IsTrue(bea.Conversing);
                Assert.AreEqual(CharacterClips.Talk, bea.Avatar.Loop, "Bea plays her talk loop while speaking");
            }

            // Tap anywhere: the only line closes, control returns.
            yield return Wait(0.3f);
            director.Dialogue.Advance();
            yield return Frames(3);
            Assert.IsFalse(director.Dialogue.IsOpen);
            Assert.IsTrue(game.Controls.GameplayEnabled, "control returns after the conversation");
            if (bea != null)
            {
                Assert.IsFalse(bea.Conversing);
            }

            // A repeat plays her repeat line and gains nothing.
            var before = director.State;
            var step = director.Perform(CaseAction.Talk(Speaker.Bea));
            Assert.AreEqual(CaseOutcome.Repeated, step.Outcome);
            Assert.AreSame(before, director.State);
            Assert.AreEqual(Lines.BeaRepeat, director.Dialogue.CurrentLine.Key);
        }

        [UnityTest]
        public IEnumerator HoldingUseDoesNotSkipLines()
        {
            yield return StartGame();
            var director = GameBootstrap.Instance.Director;
            yield return StandByBea();

            Press(_keyboard.eKey, queueEventOnly: true);
            yield return Frames(3);
            Assert.IsTrue(director.Dialogue.IsOpen, "pressing Use opened the conversation");
            yield return Wait(1.2f); // still holding Use
            Assert.IsTrue(director.Dialogue.IsOpen, "holding Use must not dismiss the line");
            Assert.AreEqual(0, director.Dialogue.LineIndex);

            Release(_keyboard.eKey, queueEventOnly: true);
            yield return Frames(3);
            Assert.IsTrue(director.Dialogue.IsOpen, "releasing does not advance either");
            yield return Tap(_keyboard.eKey);
            yield return Frames(2);
            Assert.IsFalse(director.Dialogue.IsOpen, "a fresh press advances (closes the single line)");
        }

        [UnityTest]
        public IEnumerator BenchViewRecordsCheckedViewAndHandsTheCameraBack()
        {
            yield return StartGame();
            var game = GameBootstrap.Instance;
            var director = game.Director;
            // In front of the free end of the bench (the direction Mr. Vale faces).
            float facing = (EnvironmentLayout.BenchYaw + EnvironmentLayout.ModelFacingOffset) * Mathf.Deg2Rad;
            yield return StandAt(CaseStage.PointBench, new Vector3(Mathf.Sin(facing), 0f, Mathf.Cos(facing)), 1.2f);

            yield return Tap(_keyboard.eKey);
            Assert.IsTrue(director.State.Has(CaseFact.CheckedView), "sitting records checkedView");
            Assert.IsTrue(director.Bench.Active, "the camera drops to the bench viewpoint");
            Assert.AreEqual(CaseStage.BenchEyeHeight, director.Bench.CameraTransform.position.y, 0.01f);
            yield return Wait(0.5f);
            Assert.IsTrue(director.Bench.Active, "holds while Pip stays put");

            Press(_keyboard.sKey);
            yield return Frames(5);
            Release(_keyboard.sKey);
            Assert.IsFalse(director.Bench.Active, "moving hands the camera back");

            // Without moving it times out after the hold.
            director.Perform(CaseAction.Of(CaseActionKind.SitOnBench));
            Assert.IsTrue(director.Bench.Active);
            yield return Wait(BenchView.HoldSeconds + 0.3f);
            Assert.IsFalse(director.Bench.Active);
        }

        [UnityTest]
        public IEnumerator FrontDoorIsStuckFromOutsideWithNoFact()
        {
            yield return StartGame();
            var director = GameBootstrap.Instance.Director;
            yield return StandAt(CaseStage.PointFrontDoor, Vector3.back, 0.6f);
            Assert.AreEqual(CaseActionKind.TryFrontDoor, director.Targets.Current.Action.Kind,
                "from the porch the door offers Open, never the inside latch");

            var before = director.State;
            yield return Tap(_keyboard.eKey);
            Assert.AreEqual(CaseOutcome.DoorStuck, director.LastStep.Outcome);
            CollectionAssert.AreEqual(new[] { CaseFact.TriedDoor }, director.LastStep.Gained, "only the stuck-door sticker");
            Assert.AreEqual(before.Count + 1, director.State.Count);
            Assert.IsFalse(director.State.Has(CaseFact.FrontOpen));
            Assert.IsTrue(director.Dialogue.IsOpen);
            Assert.AreEqual(Speaker.Pip, director.Dialogue.CurrentLine.Speaker);
            Assert.AreEqual(Lines.ExamineDoorStuck, director.Dialogue.CurrentLine.Key);
            Assert.AreEqual("It won't budge.", director.Dialogue.CurrentText);
        }

        [UnityTest]
        public IEnumerator KnocksPlayOnceOnTheFirstSteps()
        {
            yield return StartGame();
            var director = GameBootstrap.Instance.Director;
            int heard = 0;
            director.Stepped += step =>
            {
                if (step.Gained.Contains(CaseFact.HeardKnocks))
                {
                    heard++;
                }
            };
            Assert.IsFalse(director.State.Has(CaseFact.HeardKnocks), "not before Pip moves");

            Press(_keyboard.wKey);
            yield return Wait(0.6f);
            Release(_keyboard.wKey);
            yield return Frames(2);
            Assert.IsTrue(director.State.Has(CaseFact.HeardKnocks), "the cue plays during the first steps");
            Assert.AreEqual(1, heard);
            Assert.AreEqual(Lines.CaptionArrivalKnocks, director.Captions.CurrentKey, "caption in the strip");
            Assert.IsTrue(director.Captions.HasDirectionCue, "with a direction arrow toward the house");
            StringAssert.StartsWith("[Three knocks from the blue house]", director.Captions.CurrentText);
            Assert.IsTrue(director.Knocks.Heard);
            Assert.IsFalse(director.Dialogue.IsOpen, "a caption never stops play");

            yield return Wait(1.5f);
            Assert.Greater(director.Knocks.WarmLight.intensity, 0f, "the attic window warms");
            Press(_keyboard.wKey);
            yield return Wait(1.0f);
            Release(_keyboard.wKey);
            GameBootstrap.Instance.Pip.Teleport(new Vector3(0f, 0.7f, -13.4f));
            yield return Frames(5);
            Assert.AreEqual(1, heard, "it plays once");
        }

        [UnityTest]
        public IEnumerator KnocksPlayEvenWhenPipGoesStraightPast()
        {
            yield return StartGame();
            var director = GameBootstrap.Instance.Director;
            GameBootstrap.Instance.Pip.Teleport(new Vector3(0f, 0.1f, -6f));
            yield return Frames(3);
            Assert.IsTrue(director.State.Has(CaseFact.HeardKnocks));
            Assert.AreEqual(Lines.CaptionArrivalKnocks, director.Captions.CurrentKey);
        }

        [UnityTest]
        public IEnumerator NotebookShowsOnlyRaisedQuestionsAndTheRadioMuses()
        {
            yield return StartGame();
            var game = GameBootstrap.Instance;
            var director = game.Director;
            Assert.IsTrue(director.NotebookButton.gameObject.activeInHierarchy, "notebook disc in the HUD");
            director.Perform(CaseAction.Of(CaseActionKind.HearKnocks));
            director.NotebookButton.onClick.Invoke();
            yield return Frames(2);

            var notebook = director.Notebook;
            Assert.IsTrue(notebook.IsOpen);
            Assert.IsFalse(game.Controls.GameplayEnabled, "the notebook holds gameplay input");
            Assert.AreEqual(1, notebook.QuestionLabels.Count, "only the knocks have asked anything (D26)");
            Assert.AreEqual(Localizer.Get(NoteKeys.QuestionSource), notebook.QuestionLabels[0].text);
            Assert.IsNull(notebook.AnswerLabels[0], "nothing answered before the child solves it");
            Assert.IsNull(notebook.FinalCard);
            Assert.AreEqual(2, notebook.Slots.Count, "two empty slots");
            Assert.AreEqual(12, notebook.OutlineCount, "one ? per clue still out there");

            // The radio on an open question: Pip muses and the missing clue's place lights, never a pairing.
            notebook.HintButton.onClick.Invoke();
            yield return null;
            Assert.IsNotNull(notebook.Thought, "Pip's musing");
            Assert.IsNotNull(notebook.PlaceBadge, "the missing clue shows its place");

            notebook.CloseButton.onClick.Invoke();
            yield return Frames(3);
            Assert.IsFalse(notebook.IsOpen);
            Assert.IsTrue(game.Controls.GameplayEnabled);

            // Witnesses raise the entry question; a found sticker sits in the tray and its tap reads its note.
            director.Perform(CaseAction.Talk(Speaker.Vale));
            director.Dialogue.Skip();
            director.Perform(CaseAction.Of(CaseActionKind.SitOnBench));
            yield return Frames(2);
            director.OpenNotebook();
            yield return Frames(2);
            Assert.AreEqual(2, notebook.QuestionLabels.Count);
            Assert.AreEqual(Localizer.Get(NoteKeys.QuestionEntry), notebook.QuestionLabels[1].text);
            Assert.AreEqual(2, notebook.TrayStickers.Count);
            AssertEveryLabelDraws(notebook.gameObject);
            notebook.TapSticker(CaseNotebook.BenchView);
            // D32: the tap opens the sticker's memory card, whose caption replaces the note in the caption strip.
            Assert.AreEqual(CaseNotebook.BenchView, notebook.MemorySticker, "the tap shows where it was found");
            Assert.AreEqual(StickerMemories.For(CaseNotebook.BenchView).CaptionKey, notebook.MemoryCard.CaptionKey);
            Assert.AreEqual(CaseNotebook.BenchView, notebook.HeldSticker, "tap-tap: the sticker is picked up");
            notebook.Close();
        }

        [UnityTest]
        public IEnumerator SaveThenReloadRestoresFactsAndContinues()
        {
            yield return StartGame();
            var director = GameBootstrap.Instance.Director;
            director.Perform(CaseAction.Of(CaseActionKind.HearKnocks));
            director.Perform(CaseAction.Talk(Speaker.Bea));
            director.Dialogue.Skip();
            director.Perform(CaseAction.Of(CaseActionKind.ExamineStreamer));
            director.Dialogue.Skip();
            Assert.IsTrue(director.HasSave);
            Assert.IsTrue(File.Exists(SaveSlotPaths.ForCase(_saves).Latest));

            // Reload the scene: the new director reads the same folder before play starts.
            yield return LoadGame();
            var game = GameBootstrap.Instance;
            var restored = game.Director;
            Assert.AreNotSame(director, restored);
            Assert.AreEqual(GameState.Title, game.Flow.State);
            Assert.AreEqual(SaveLoadStatus.Latest, restored.Resume.Status);
            Assert.IsTrue(restored.State.Has(CaseFact.BeaSpoke));
            Assert.IsTrue(restored.State.Has(CaseFact.ExaminedStreamer));
            Assert.IsTrue(restored.Knocks.Heard, "the world is rebuilt from facts before play");
            Assert.IsFalse(restored.Notice.IsOpen, "a clean resume needs no notice");
            Assert.IsTrue(game.Ui.ContinueButton.gameObject.activeSelf, "Continue is offered");

            game.Ui.ContinueButton.onClick.Invoke();
            yield return Frames(3);
            Assert.AreEqual(GameState.Playing, game.Flow.State);
            Assert.IsTrue(CaseStage.TryGetAnchor(restored.AnchorId, out var anchor));
            Vector3 pip = game.Pip.transform.position;
            Assert.Less(new Vector2(pip.x - anchor.X, pip.z - anchor.Z).magnitude, 0.6f, "Pip resumes at the saved anchor");
            Assert.IsTrue(restored.State.Has(CaseFact.BeaSpoke), "facts survive Continue");

            // New game with a case in progress asks first.
            game.Flow.Pause();
            game.Flow.QuitToTitle();
            yield return null;
            game.Ui.NewGameButton.onClick.Invoke();
            Assert.AreEqual(GameState.Title, game.Flow.State, "new game waits for confirmation");
            Assert.IsTrue(restored.Notice.IsOpen);
            restored.Notice.PrimaryButton.onClick.Invoke();
            yield return null;
            Assert.AreEqual(GameState.Playing, game.Flow.State);
            Assert.AreEqual(0, restored.State.Count, "a new case starts empty");
        }

        [UnityTest]
        public IEnumerator ClosedCaseShowsTheFinalCardAndReplayResetsOnlyTheCase()
        {
            yield return StartGame();
            var director = GameBootstrap.Instance.Director;
            // The interior is not staged yet: drive the reducer through the route directly.
            foreach (var action in new[]
                     {
                         CaseAction.Of(CaseActionKind.HearKnocks), CaseAction.Talk(Speaker.Vale),
                         CaseAction.Of(CaseActionKind.ExamineCloth),
                         CaseAction.Of(CaseActionKind.EnterAttic), CaseAction.Of(CaseActionKind.ExamineSill),
                         CaseAction.Talk(Speaker.June), CaseAction.Of(CaseActionKind.EnterFoyer),
                         CaseAction.Of(CaseActionKind.LiftLatch), CaseAction.Of(CaseActionKind.ExamineGrille),
                         CaseAction.Of(CaseActionKind.LinkArm), CaseAction.Of(CaseActionKind.TurnCrank),
                         CaseAction.Of(CaseActionKind.ArriveAtPorch),
                     })
            {
                director.Perform(action);
                director.Dialogue.Skip();
            }

            Assert.IsTrue(director.State.Has(CaseFact.CaseClosed), "the show is tied to world actions");
            Assert.IsFalse(director.State.Has(CaseFact.CaseStamped), "the stamp waits for the deductions");
            yield return Wait(FinaleView.IntroSeconds + 0.3f); // the reopening plays before its lines
            director.Dialogue.Skip();
            yield return Wait(FinaleView.HoldAfterLines + 0.4f);
            var notebook = director.Notebook;
            Assert.IsTrue(notebook.IsOpen, "Kit asks; the notebook opens on an open question");
            Assert.IsNull(notebook.FinalCard, "no Case closed card before the child explains the case");
            Assert.IsNotNull(notebook.CurrentQuestion);
            Assert.IsFalse(notebook.CurrentQuestion.IsSolved(director.State));
            notebook.Close();
            yield return Frames(2);

            Assert.IsNotNull(director.SolveQuestion(CaseNotebook.QuestionSource, new[] { CaseNotebook.FloorHum, CaseNotebook.WoodenPiece }));
            Assert.IsNotNull(director.SolveQuestion(CaseNotebook.QuestionEntry,
                new[] { CaseNotebook.ValeDoor, CaseNotebook.Cloth, CaseNotebook.SillSnag }));
            Assert.IsNull(director.SolveQuestion(CaseNotebook.QuestionMoved, new[] { CaseNotebook.LooseArm, CaseNotebook.Streamer }),
                "a wrong set records nothing");
            Assert.IsNotNull(director.SolveQuestion(CaseNotebook.QuestionMoved, new[] { CaseNotebook.LooseArm, CaseNotebook.OwlBooth }));
            Assert.IsTrue(director.State.Has(CaseFact.CaseStamped));
            director.OpenNotebook();
            yield return Frames(2);
            Assert.IsNotNull(notebook.FinalCard, "the final card appears once the case is stamped");
            Assert.IsNotNull(notebook.AnswerLabels[0]);
            Assert.IsNotNull(notebook.AnswerLabels[2]);
            notebook.ReplayButton.onClick.Invoke();
            Assert.IsTrue(director.Notice.IsOpen, "Replay asks for confirmation");
            Assert.AreEqual(NoteKeys.ReplayConfirm, director.Notice.BodyKey);
            director.Notice.PrimaryButton.onClick.Invoke();
            yield return Frames(2);
            Assert.AreEqual(0, director.State.Count);
            Assert.IsFalse(notebook.IsOpen);
            Assert.IsFalse(director.Knocks.Heard, "the world is rebuilt for the fresh case");
        }
    }
}
