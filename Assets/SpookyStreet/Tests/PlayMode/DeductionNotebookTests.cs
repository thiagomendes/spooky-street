using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Flow;
using SpookyStreet.Localization;
using SpookyStreet.UI;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using N = SpookyStreet.Core.Case.CaseNotebook;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// S002.5a (D26): the deduction notebook in the running game. Questions appear as the world raises them; the child
    /// answers by placing found stickers with a touch drag, tap-tap, or the keyboard cursor (D25); a wrong set wobbles
    /// home while Pip thinks; a right set draws the comic from the child's stickers; the "Case closed" card needs the
    /// three deductions and the reopening. Nothing is pre-solved. Fresh temporary save folder per test.
    /// </summary>
    public class DeductionNotebookTests : InputTestFixture
    {
        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            Localizer.ResetForTests();
            CaseDirector.UseTemporarySaveDirectory();
        }

        private static CaseDirector Director => GameBootstrap.Instance.Director;

        private static IEnumerator StartGame()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(GameBootstrap.Instance);
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

        private IEnumerator Tap(KeyControl key)
        {
            Press(key, queueEventOnly: true);
            yield return null;
            yield return null;
            Release(key, queueEventOnly: true);
            yield return null;
        }

        /// <summary>Drives the world through the reducer (the stage tests cover the real Use targets).</summary>
        private static IEnumerator Do(params CaseAction[] actions)
        {
            foreach (var action in actions)
            {
                Director.Perform(action);
                Director.Dialogue.Skip();
                Director.Cutaway.Skip();
                yield return Frames(2);
            }

            Director.Captions.Clear();
            yield return Wait(0.4f);
        }

        private static CaseAction A(CaseActionKind kind) => CaseAction.Of(kind);

        private static IEnumerator OpenOn(string questionId)
        {
            Director.OpenNotebook();
            yield return Frames(2);
            var notebook = Director.Notebook;
            Assert.IsTrue(notebook.IsOpen);
            int page = notebook.PageQuestions.IndexOf(N.Question(questionId));
            Assert.GreaterOrEqual(page, 0, questionId + " has been raised");
            notebook.ShowPage(page);
            yield return Wait(0.5f);
        }

        private static IEnumerator Verdict()
        {
            var notebook = Director.Notebook;
            float until = Time.time + 6f;
            while (notebook.Checking && Time.time < until)
            {
                yield return null;
            }

            Assert.IsFalse(notebook.Checking, "the verdict arrives");
        }

        private static Vector2 Screen(RectTransform rect) => RectTransformUtility.WorldToScreenPoint(null, rect.position);

        /// <summary>A real touch drag through the sticker's own handlers: lift, follow the thumb, drop on the slot.</summary>
        private static IEnumerator Drag(string stickerId, int slot)
        {
            var notebook = Director.Notebook;
            var sticker = notebook.TraySticker(stickerId);
            Assert.IsNotNull(sticker, stickerId + " is in the tray");
            var data = new PointerEventData(EventSystem.current) { position = Screen(sticker.Rect), button = PointerEventData.InputButton.Left };
            data.pressPosition = data.position;
            ExecuteEvents.Execute(sticker.gameObject, data, ExecuteEvents.beginDragHandler);
            yield return null;
            Vector2 target = Screen(notebook.Slots[slot].rectTransform);
            for (int i = 1; i <= 4; i++)
            {
                data.position = Vector2.Lerp(data.pressPosition, target, i / 4f);
                ExecuteEvents.Execute(sticker.gameObject, data, ExecuteEvents.dragHandler);
                yield return null;
            }

            ExecuteEvents.Execute(sticker.gameObject, data, ExecuteEvents.endDragHandler);
            yield return Frames(2);
        }

        private static void AssertNothingSolvedOnThePages()
        {
            var notebook = Director.Notebook;
            for (int p = 0; p < notebook.PageQuestions.Count; p++)
            {
                if (notebook.PageQuestions[p].IsSolved(Director.State))
                {
                    continue;
                }

                Assert.IsNull(notebook.AnswerLabels[p], notebook.PageQuestions[p].Id + ": no answer words before it is solved");
            }

            Assert.IsNull(notebook.FinalCard);
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator CanonicalPlaythroughRaisesQuestionsWobblesAWrongSetAndClosesTheCase()
        {
            yield return StartGame();
            var director = Director;
            var notebook = director.Notebook;
            Assert.AreEqual(0, N.Raised(director.State).Count, "the notebook asks nothing before the world does");

            yield return Do(A(CaseActionKind.HearKnocks));
            CollectionAssert.AreEqual(new[] { N.Question(N.QuestionSource) }, N.Raised(director.State));
            yield return Do(CaseAction.Talk(Speaker.Bea), CaseAction.Talk(Speaker.Vale), A(CaseActionKind.SitOnBench),
                A(CaseActionKind.ExamineStreamer), A(CaseActionKind.ExamineCloth), A(CaseActionKind.EnterAttic),
                A(CaseActionKind.ExamineSill));
            Assert.AreEqual(2, N.Raised(director.State).Count, "the witnesses raised how June got in");
            Assert.IsFalse(director.State.Has(CaseFact.SolvedEntry), "climbing in does not answer it any more");

            // A wrong set (the shiny decoy) by tap-tap: it wobbles home and Pip thinks. No count, no red.
            yield return OpenOn(N.QuestionEntry);
            AssertNothingSolvedOnThePages();
            foreach (string id in new[] { N.ValeDoor, N.Cloth, N.Streamer })
            {
                notebook.TapSticker(id);
                notebook.TapSlot(notebook.CurrentBoard.FirstEmpty());
                yield return null;
            }

            Assert.IsTrue(notebook.CurrentBoard.IsFull);
            Assert.IsTrue(notebook.Checking, "Pip pauses before the verdict");
            yield return Verdict();
            Assert.AreEqual(DeductionVerdict.Wrong, notebook.LastCheck.Verdict);
            Assert.IsTrue(notebook.CurrentBoard.IsEmpty, "the first wrong try sends every sticker home");
            Assert.IsNotNull(notebook.Thought, "Pip thinks");
            Assert.AreEqual(6, notebook.TrayStickers.Count, "all six found stickers are back in the tray");
            Assert.IsFalse(director.State.Has(CaseFact.SolvedEntry));

            // The right set: the slots glow, the case records it, and the comic is drawn from these stickers.
            foreach (string id in new[] { N.SillSnag, N.ValeDoor, N.BeaFlash })
            {
                notebook.TapSticker(id);
                notebook.TapSlot(notebook.CurrentBoard.FirstEmpty());
                yield return null;
            }

            yield return Verdict();
            Assert.AreEqual(DeductionVerdict.Right, notebook.LastCheck.Verdict);
            Assert.IsTrue(director.State.Has(CaseFact.SolvedEntry));
            var saved = director.Answers.Get(N.QuestionEntry);
            Assert.IsNotNull(saved, "the child's own set is kept with the solved fact");
            CollectionAssert.AreEqual(new[] { N.ValeDoor, N.SillSnag, N.BeaFlash }, CaseDeduction.Comic(N.Question(N.QuestionEntry), saved));
            Assert.AreEqual(3, notebook.ComicPanels.Count, "one panel per sticker the child placed");
            yield return Wait(4f);
            Assert.IsFalse(notebook.ComicPlaying, "it plays twice and holds");
            Assert.IsNotNull(notebook.FoundStamp);
            Assert.IsTrue(notebook.FoundStamp.activeSelf, "Found it!");
            notebook.Close();
            yield return Frames(3);

            // Into the house: the hall's hum, the grille, the arm, the crank. Each question waits for the child.
            yield return Do(CaseAction.Talk(Speaker.June), A(CaseActionKind.EnterFoyer), A(CaseActionKind.LiftLatch),
                A(CaseActionKind.ExamineGrille));
            Assert.AreEqual(3, N.Raised(director.State).Count, "the workshop raised what the knocks should move");
            Assert.IsFalse(director.State.Has(CaseFact.SolvedSource), "seeing the piece does not answer it");
            yield return OpenOn(N.QuestionSource);
            notebook.TapSticker(N.WoodenPiece);
            notebook.TapSlot(0);
            notebook.TapSticker(N.FloorHum);
            notebook.TapSlot(1);
            yield return Verdict();
            Assert.IsTrue(director.State.Has(CaseFact.SolvedSource));
            notebook.Close();
            yield return Frames(3);

            yield return Do(A(CaseActionKind.LinkArm), A(CaseActionKind.TurnCrank));
            Assert.IsFalse(director.State.Has(CaseFact.SolvedMoved), "the bow does not answer it");
            yield return OpenOn(N.QuestionMoved);
            notebook.TapSticker(N.LooseArm);
            notebook.TapSlot(0);
            notebook.TapSticker(N.OwlBooth);
            notebook.TapSlot(1);
            yield return Verdict();
            Assert.IsTrue(director.State.Has(CaseFact.SolvedMoved));
            Assert.IsFalse(director.State.Has(CaseFact.CaseStamped), "no stamp before the show");
            notebook.Close();
            yield return Frames(3);

            // The porch: the show and the stamp together; the final card follows the finale.
            director.Perform(A(CaseActionKind.ArriveAtPorch));
            Assert.IsTrue(director.LastStep.ClosedCase && director.LastStep.StampedCase);
            yield return Wait(FinaleView.IntroSeconds + 0.3f);
            director.Dialogue.Skip();
            yield return Wait(FinaleView.HoldAfterLines + 0.5f);
            Assert.IsTrue(notebook.IsOpen);
            Assert.IsNotNull(notebook.FinalCard, "Case closed");
            Assert.AreEqual(notebook.PageCount - 1, notebook.CurrentPage);
            notebook.Close();

            // The answers survive a reload with the child's own sets.
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            yield return null;
            Assert.IsTrue(Director.State.Has(CaseFact.CaseStamped));
            CollectionAssert.AreEquivalent(new[] { N.SillSnag, N.ValeDoor, N.BeaFlash }, Director.Answers.Get(N.QuestionEntry));
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator NothingIsPreSolvedAfterEveryWorldAction()
        {
            yield return StartGame();
            yield return Do(A(CaseActionKind.HearKnocks), CaseAction.Talk(Speaker.Bea), A(CaseActionKind.EnterAttic),
                CaseAction.Talk(Speaker.June), A(CaseActionKind.EnterFoyer), A(CaseActionKind.LiftLatch),
                A(CaseActionKind.ExamineGrille), A(CaseActionKind.LinkArm), A(CaseActionKind.TurnCrank));
            var notebook = Director.Notebook;
            Director.OpenNotebook();
            yield return Frames(3);
            Assert.AreEqual(3, notebook.PageCount);
            for (int p = 0; p < notebook.PageCount; p++)
            {
                notebook.ShowPage(p);
                yield return Frames(2);
                Assert.IsNull(notebook.AnswerLabels[p], "no answer words");
                Assert.AreEqual(0, notebook.ComicPanels.Count, "no comic");
                Assert.IsNull(notebook.FoundStamp, "no stamp");
                Assert.IsTrue(notebook.CurrentBoard.IsEmpty, "no sticker placed for the child");
                foreach (var slot in notebook.Slots)
                {
                    Assert.AreEqual(notebook.Slots[0].rectTransform.sizeDelta, slot.rectTransform.sizeDelta,
                        "every slot has the same shape: nothing says which clue goes where");
                    Assert.AreEqual(0, slot.GetComponentsInChildren<NotebookSticker>(true).Length,
                        "an empty slot holds nothing: no pre-shaped clue, no tick");
                }
            }

            Assert.IsNull(notebook.FinalCard);
            notebook.Close();
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator TouchDragPlacesStickersAndAWrongDropGoesHome()
        {
            yield return StartGame();
            yield return Do(A(CaseActionKind.HearKnocks), A(CaseActionKind.EnterFoyer), A(CaseActionKind.ExamineGrille),
                A(CaseActionKind.ExamineStreamer));
            yield return OpenOn(N.QuestionSource);
            var notebook = Director.Notebook;
            int page = notebook.CurrentPage;

            yield return Drag(N.FloorHum, 1);
            Assert.AreEqual(page, notebook.CurrentPage, "dragging a sticker never swipes the page");
            Assert.AreEqual(N.FloorHum, notebook.CurrentBoard.Slots[1], "dropped on the slot it was let go over");
            Assert.IsNotNull(notebook.SlotSticker(1));
            Assert.IsNull(notebook.TraySticker(N.FloorHum), "it left the tray");

            // A wrong second sticker: the pair wobbles home.
            yield return Drag(N.Streamer, 0);
            Assert.IsTrue(notebook.CurrentBoard.IsFull);
            yield return Verdict();
            Assert.AreEqual(DeductionVerdict.Wrong, notebook.LastCheck.Verdict);
            Assert.IsTrue(notebook.CurrentBoard.IsEmpty);
            Assert.IsNotNull(notebook.TraySticker(N.FloorHum), "back in the tray");

            // Right by drag: the comic.
            yield return Drag(N.WoodenPiece, 0);
            yield return Drag(N.FloorHum, 1);
            yield return Verdict();
            Assert.AreEqual(DeductionVerdict.Right, notebook.LastCheck.Verdict);
            Assert.IsTrue(Director.State.Has(CaseFact.SolvedSource));
            Assert.AreEqual(2, notebook.ComicPanels.Count);
            notebook.Close();
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator TheKeyboardCursorPicksPlacesAndSolves()
        {
            yield return StartGame();
            yield return Do(A(CaseActionKind.HearKnocks), A(CaseActionKind.ExamineGrille), A(CaseActionKind.LinkArm),
                A(CaseActionKind.TurnCrank));
            yield return OpenOn(N.QuestionMoved);
            var notebook = Director.Notebook;
            var tray = new List<string>();
            foreach (var s in notebook.TrayStickers)
            {
                tray.Add(s.StickerId);
            }

            Assert.AreEqual(CursorRow.Tray, notebook.CursorRow);
            foreach (string id in new[] { N.LooseArm, N.OwlBooth })
            {
                // Down to the tray, home to the left, then right to the sticker; Enter picks, Enter places.
                yield return Tap(_keyboard.downArrowKey);
                for (int i = 0; i < 6; i++)
                {
                    yield return Tap(_keyboard.leftArrowKey);
                }

                int index = new List<string>(TrayIds(notebook)).IndexOf(id);
                Assert.GreaterOrEqual(index, 0, id);
                for (int i = 0; i < index; i++)
                {
                    yield return Tap(_keyboard.rightArrowKey);
                }

                Assert.AreEqual(index, notebook.CursorIndex);
                yield return Tap(_keyboard.enterKey);
                Assert.AreEqual(id, notebook.HeldSticker, "Enter picks the sticker");
                Assert.AreEqual(CursorRow.Slots, notebook.CursorRow, "the cursor jumps to the first empty slot");
                yield return Tap(_keyboard.enterKey);
                Assert.IsNull(notebook.HeldSticker);
                Assert.GreaterOrEqual(notebook.CurrentBoard.SlotOf(id), 0, "Enter places it");
            }

            yield return Verdict();
            Assert.AreEqual(DeductionVerdict.Right, notebook.LastCheck.Verdict);
            Assert.IsTrue(Director.State.Has(CaseFact.SolvedMoved));

            // Escape closes the notebook.
            yield return Tap(_keyboard.escapeKey);
            yield return Frames(2);
            Assert.IsFalse(notebook.IsOpen);
        }

        private static IEnumerable<string> TrayIds(NotebookView notebook)
        {
            foreach (var s in notebook.TrayStickers)
            {
                yield return s.StickerId;
            }
        }
    }
}
