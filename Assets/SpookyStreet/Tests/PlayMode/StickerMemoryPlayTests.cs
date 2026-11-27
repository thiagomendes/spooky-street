using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Controls;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Nonverbal;
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
    /// D32 (owner, 2026-11-24): every notebook sticker shows a short memory of where the child found it. A tap on a tray
    /// sticker picks it up (tap-tap, unchanged) and shows its memory; holding any sticker shows the memory without picking
    /// it up or sending it home; Inspect (gamepad West, F) shows it under the outline. None of it places a sticker or
    /// counts a try, and drag and tap-tap placement work as before. Fresh temporary save folder per test.
    /// </summary>
    public class StickerMemoryPlayTests : InputTestFixture
    {
        private Keyboard _keyboard;
        private Gamepad _gamepad;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _gamepad = InputSystem.AddDevice<Gamepad>();
            Localizer.ResetForTests();
            CaseDirector.UseTemporarySaveDirectory();
            ControlSchemeTracker.Override = null;
        }

        public override void TearDown()
        {
            ControlSchemeTracker.Override = null;
            base.TearDown();
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

        private IEnumerator Tap(ButtonControl button)
        {
            Press(button, queueEventOnly: true);
            yield return null;
            yield return null;
            Release(button, queueEventOnly: true);
            yield return null;
        }

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

        /// <summary>The workshop is done: "What should the knocks move?" is open with its stickers in the tray.</summary>
        private static IEnumerator OpenOnTheWorkshopQuestion()
        {
            yield return Do(A(CaseActionKind.HearKnocks), A(CaseActionKind.ExamineGrille), A(CaseActionKind.LinkArm),
                A(CaseActionKind.TurnCrank));
            Director.OpenNotebook();
            yield return Frames(2);
            var notebook = Director.Notebook;
            int page = notebook.PageQuestions.IndexOf(N.Question(N.QuestionMoved));
            Assert.GreaterOrEqual(page, 0);
            notebook.ShowPage(page);
            yield return Wait(0.5f);
        }

        private static Vector2 Screen(RectTransform rect) => RectTransformUtility.WorldToScreenPoint(null, rect.position);

        private static PointerEventData Pointer(RectTransform at)
        {
            var data = new PointerEventData(EventSystem.current)
            {
                position = Screen(at), button = PointerEventData.InputButton.Left,
            };
            data.pressPosition = data.position;
            return data;
        }

        /// <summary>A finger held still on the sticker, then lifted: down, wait, up, click (as the event system sends).</summary>
        private static IEnumerator Hold(NotebookSticker sticker, float seconds)
        {
            var data = Pointer(sticker.Rect);
            ExecuteEvents.Execute(sticker.gameObject, data, ExecuteEvents.pointerDownHandler);
            yield return Wait(seconds);
            ExecuteEvents.Execute(sticker.gameObject, data, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(sticker.gameObject, data, ExecuteEvents.pointerClickHandler);
            yield return null;
        }

        private static IEnumerator Drag(string stickerId, int slot)
        {
            var notebook = Director.Notebook;
            var sticker = notebook.TraySticker(stickerId);
            Assert.IsNotNull(sticker, stickerId + " is in the tray");
            var data = Pointer(sticker.Rect);
            ExecuteEvents.Execute(sticker.gameObject, data, ExecuteEvents.pointerDownHandler);
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

        private static List<string> Snapshot(DeductionBoard board) => board.Slots.ToList();

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ATapOrAHoldShowsTheMemoryAndNeverPlaces()
        {
            yield return StartGame();
            yield return OpenOnTheWorkshopQuestion();
            var notebook = Director.Notebook;
            var board = notebook.CurrentBoard;
            int facts = Director.State.Count;
            Assert.IsFalse(notebook.MemoryOpen);

            // Tap: the sticker lifts (tap-tap, unchanged) and its memory opens over the page, with its caption.
            notebook.TapSticker(N.LooseArm);
            yield return Frames(2);
            Assert.IsTrue(notebook.MemoryOpen, "the first tap shows the memory");
            Assert.AreEqual(N.LooseArm, notebook.MemorySticker);
            Assert.AreEqual(N.LooseArm, notebook.HeldSticker, "and picks the sticker up, as before");
            var card = notebook.MemoryCard;
            Assert.AreEqual(StickerMemories.For(N.LooseArm).CaptionKey, card.CaptionKey);
            Assert.AreEqual(Localizer.Get(card.CaptionKey), card.Caption.text, "the caption reads in the current language");
            Assert.IsTrue(board.IsEmpty, "a memory never places");
            Assert.AreEqual(0, board.Tries, "a memory never counts a try");
            Assert.AreEqual(facts, Director.State.Count, "a memory never records anything");
            var stage = Director.Nonverbal.MemoryStage;
            Assert.IsNotNull(stage, "the world side is wired");
            Assert.AreEqual(N.LooseArm, stage.Current.StickerId);

            // The card sits above the tray: the tray's stickers stay uncovered.
            var cardRect = (RectTransform)card.transform;
            foreach (var s in notebook.TrayStickers)
            {
                Assert.IsFalse(Overlaps(cardRect, s.Rect), s.StickerId + " stays in view under the card");
            }

            // It puts itself away after its few seconds; the sticker is still held, so a slot tap places it.
            yield return Wait(StickerMemories.CardSeconds + 0.3f);
            Assert.IsFalse(notebook.MemoryOpen, "the card closes by itself");
            Assert.IsNull(stage.Current, "the world is given back");
            Assert.AreEqual(N.LooseArm, notebook.HeldSticker);
            notebook.TapSlot(0);
            yield return null;
            Assert.AreEqual(N.LooseArm, board.Slots[0], "tap-tap still places");

            // Hold a tray sticker: the memory opens, nothing is picked.
            var booth = notebook.TraySticker(N.OwlBooth);
            yield return Hold(booth, NotebookSticker.HoldSeconds + 0.2f);
            Assert.AreEqual(N.OwlBooth, notebook.MemorySticker, "holding shows the memory");
            Assert.IsNull(notebook.HeldSticker, "holding never picks a sticker up");
            Assert.IsNotNull(notebook.TraySticker(N.OwlBooth), "it stays in the tray");
            var before = Snapshot(board);

            // Hold the sticker in the slot: its memory, and it stays in its slot.
            var placed = notebook.SlotSticker(0);
            yield return Hold(placed, NotebookSticker.HoldSeconds + 0.2f);
            Assert.AreEqual(N.LooseArm, notebook.MemorySticker, "a placed sticker shows its memory too");
            CollectionAssert.AreEqual(before, board.Slots, "holding never sends it home");
            Assert.AreEqual(0, board.Tries);

            // A short tap on the placed sticker still sends it home (unchanged).
            notebook.TapSticker(N.LooseArm);
            yield return null;
            Assert.IsTrue(board.IsEmpty);
            Assert.IsFalse(notebook.MemoryOpen, "sending a sticker home puts the memory away");

            // A press on the card closes it.
            Assert.IsTrue(notebook.InspectSticker(N.WoodenPiece));
            notebook.MemoryCard.CloseButton.onClick.Invoke();
            Assert.IsFalse(notebook.MemoryOpen);
            Assert.IsFalse(notebook.InspectSticker(N.Cloth), "a clue not found yet has nothing to recall");
            Assert.AreEqual(facts, Director.State.Count);
            notebook.Close();
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator DragAndTapTapStillPlaceAndSolveWithMemoriesAround()
        {
            yield return StartGame();
            yield return OpenOnTheWorkshopQuestion();
            var notebook = Director.Notebook;

            // A memory is open; a drag closes it at once (the slots come into view) and places as before.
            notebook.TapSticker(N.WoodenPiece);
            yield return null;
            Assert.IsTrue(notebook.MemoryOpen);
            yield return Drag(N.LooseArm, 0);
            Assert.IsFalse(notebook.MemoryOpen, "a drag puts the memory away");
            Assert.AreEqual(N.LooseArm, notebook.CurrentBoard.Slots[0], "drag still places");
            Assert.IsNull(notebook.HeldSticker);

            // Inspecting between moves changes nothing.
            Assert.IsTrue(notebook.InspectSticker(N.WoodenPiece));
            Assert.AreEqual(0, notebook.CurrentBoard.Tries);
            Assert.AreEqual(N.LooseArm, notebook.CurrentBoard.Slots[0]);

            // Tap-tap the owl: the memory opens on the pick and closes on the place; the set is right.
            notebook.TapSticker(N.OwlBooth);
            Assert.AreEqual(N.OwlBooth, notebook.MemorySticker);
            notebook.TapSlot(notebook.CurrentBoard.FirstEmpty());
            Assert.IsFalse(notebook.MemoryOpen, "placing puts the memory away");
            yield return Verdict();
            Assert.AreEqual(DeductionVerdict.Right, notebook.LastCheck.Verdict);
            Assert.IsTrue(Director.State.Has(CaseFact.SolvedMoved));

            // On a solved page a tap still shows the memory (and never picks anything).
            yield return Wait(0.5f);
            notebook.TapSticker(N.WoodenPiece);
            Assert.AreEqual(N.WoodenPiece, notebook.MemorySticker);
            notebook.Close();
            Assert.IsFalse(notebook.MemoryOpen, "closing the notebook puts the memory away");
            Assert.IsNull(Director.Nonverbal.MemoryStage.Current);
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator GamepadAndKeyboardInspectWithTheirOwnGlyph()
        {
            yield return StartGame();
            yield return OpenOnTheWorkshopQuestion();
            var notebook = Director.Notebook;
            var board = notebook.CurrentBoard;
            ControlSchemeTracker.Override = ControlScheme.Gamepad;

            // Down to the tray: the outline rings the first sticker and the Inspect glyph (the West button) rides it.
            yield return Tap(_gamepad.dpad.down);
            Assert.AreEqual(CursorRow.Tray, notebook.CursorRow);
            string first = notebook.TrayStickers[notebook.CursorIndex].StickerId;
            Assert.IsNotNull(notebook.InspectPrompt, "the Inspect glyph shows by the outline");
            Assert.IsTrue(notebook.InspectPrompt.HasGlyph);
            Assert.AreEqual(PadButton.West, notebook.InspectPrompt.LitButton, "the gamepad glyph lights West");

            yield return Tap(_gamepad.buttonWest);
            Assert.AreEqual(first, notebook.MemorySticker, "West shows the memory of the ringed sticker");
            Assert.IsNull(notebook.HeldSticker, "Inspect never picks up");
            Assert.IsTrue(board.IsEmpty);
            yield return Tap(_gamepad.buttonWest);
            Assert.IsFalse(notebook.MemoryOpen, "West again puts it away");

            yield return Tap(_gamepad.buttonWest);
            Assert.IsTrue(notebook.MemoryOpen);
            yield return Tap(_gamepad.buttonEast);
            Assert.IsFalse(notebook.MemoryOpen, "East puts the memory away first");
            Assert.IsTrue(notebook.IsOpen, "and the notebook stays open");

            // South picks (its memory opens, as a tap does) and South places it in the first empty slot.
            yield return Tap(_gamepad.buttonSouth);
            Assert.AreEqual(first, notebook.HeldSticker);
            Assert.AreEqual(first, notebook.MemorySticker);
            yield return Tap(_gamepad.buttonSouth);
            Assert.IsNull(notebook.HeldSticker);
            Assert.IsFalse(notebook.MemoryOpen);
            Assert.AreEqual(first, board.Slots[0], "South still places");

            // The cursor on the slot: Inspect shows the placed sticker's memory and leaves it in place.
            Assert.AreEqual(CursorRow.Slots, notebook.CursorRow);
            notebook.Navigate(-5, 0);
            yield return Tap(_gamepad.buttonWest);
            Assert.AreEqual(first, notebook.MemorySticker, "a sticker in a slot inspects too");
            Assert.AreEqual(first, board.Slots[0]);
            Assert.AreEqual(0, board.Tries);

            // The keyboard: F inspects, its key cap rides the outline.
            ControlSchemeTracker.Override = ControlScheme.KeyboardMouse;
            yield return Tap(_keyboard.downArrowKey);
            Assert.IsFalse(notebook.MemoryOpen, "moving the cursor puts the memory away");
            Assert.AreEqual("<Keyboard>/f", notebook.InspectPrompt.Glyph.BindingPath, "the key glyph shows F");
            string ringed = notebook.TrayStickers[notebook.CursorIndex].StickerId;
            yield return Tap(_keyboard.fKey);
            Assert.AreEqual(ringed, notebook.MemorySticker, "F shows the memory");
            Assert.IsNull(notebook.HeldSticker);

            // Under reduced motion the memory is a still frame of the settled shot.
            notebook.CloseMemory();
            notebook.ReducedMotion = true;
            Assert.IsTrue(notebook.InspectSticker(ringed));
            var playback = notebook.MemoryCard.Playback;
            Assert.IsFalse(playback.Moving, "reduced motion: no moving shot");
            Assert.AreEqual(1f, playback.Progress);
            yield return Frames(8);
            if (notebook.MemoryCard.ShowsPlace)
            {
                Assert.LessOrEqual(notebook.MemoryCard.RenderedFrames, 2, "a still frame stops drawing");
            }

            notebook.ReducedMotion = false;
            notebook.Close();
        }

        private static bool Overlaps(RectTransform a, RectTransform b)
        {
            var ca = new Vector3[4];
            var cb = new Vector3[4];
            a.GetWorldCorners(ca);
            b.GetWorldCorners(cb);
            return ca[0].x < cb[2].x && cb[0].x < ca[2].x && ca[0].y < cb[2].y && cb[0].y < ca[2].y;
        }
    }
}
