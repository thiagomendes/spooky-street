using System.Collections;
using System.Collections.Generic;
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
using UnityEngine.UI;
using N = SpookyStreet.Core.Case.CaseNotebook;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// S004 F1 (D25): the one UI navigation base. A sample screen built on <see cref="UiFocusScreen"/> navigates by
    /// keyboard and gamepad with the stitched focus outline and per-scheme prompts, and still answers taps; the
    /// notebook, migrated onto the shared <see cref="UiNavigateActions"/> map, still picks, places, turns pages and
    /// closes by gamepad, with its cursor ringed by <see cref="UiFocus"/>.
    /// </summary>
    public class UiNavigationTests : InputTestFixture
    {
        private Keyboard _keyboard;
        private Gamepad _gamepad;
        private readonly List<GameObject> _made = new List<GameObject>();

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
            foreach (var go in _made)
            {
                if (go != null)
                {
                    Object.Destroy(go);
                }
            }

            _made.Clear();
            ControlSchemeTracker.Override = null;
            base.TearDown();
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
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

        // ---------------------------------------------------------------------------------------- the sample screen

        private sealed class Sample
        {
            public GameObject Root;
            public UiFocusScreen Nav;
            public readonly Dictionary<string, Button> Buttons = new Dictionary<string, Button>();
            public readonly Dictionary<string, int> Clicks = new Dictionary<string, int>();
            public int PagesLeft;
            public int PagesRight;
        }

        /// <summary>A card like the wave 2 screens: a close disc, three cards in a row, a "done" button below.</summary>
        private Sample BuildSample()
        {
            if (EventSystem.current == null)
            {
                var events = new GameObject("EventSystem", typeof(EventSystem),
                    typeof(UnityEngine.InputSystem.UI.InputSystemUIInputModule));
                _made.Add(events);
            }

            var canvas = UiFactory.CreateCanvas("SampleCanvas", 50);
            _made.Add(canvas.gameObject);
            var sample = new Sample();
            var root = UiFactory.Stretch(UiFactory.Rect(canvas.transform, "SampleScreen"));
            sample.Root = root.gameObject;
            var center = new Vector2(0.5f, 0.5f);
            var places = new Dictionary<string, Vector2>
            {
                { "close", new Vector2(300f, 200f) },
                { "card1", new Vector2(-220f, 0f) },
                { "card2", new Vector2(0f, 0f) },
                { "card3", new Vector2(220f, 0f) },
                { "done", new Vector2(0f, -200f) },
            };
            foreach (var pair in places)
            {
                string id = pair.Key;
                var image = UiFactory.Sprite(root, id, IconFactory.RoundedRect, Palette.WarmPaper, center, pair.Value,
                    new Vector2(160f, 110f));
                var button = image.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                sample.Clicks[id] = 0;
                button.onClick.AddListener(() => sample.Clicks[id]++);
                sample.Buttons[id] = button;
            }

            sample.Buttons["close"].onClick.AddListener(() => sample.Root.SetActive(false));
            var graph = new UiFocusGraph("sample")
                .Add("close", 0, 2)
                .AddRow(1, "card1", "card2", "card3")
                .Add("done", 2, 1);
            graph.Exit = "close";
            graph.DefaultFocus = "card1";
            Assert.IsEmpty(graph.Validate());
            var overlay = UiFactory.Stretch(UiFactory.Rect(root, "Overlay"));
            sample.Nav = UiFocusScreen.Attach(sample.Root, graph, overlay);
            foreach (var pair in sample.Buttons)
            {
                sample.Nav.Bind(pair.Key, pair.Value);
            }

            sample.Nav.PageLeft = () => sample.PagesLeft++;
            sample.Nav.PageRight = () => sample.PagesRight++;
            return sample;
        }

        private static void AssertRinged(Sample sample, string id)
        {
            Assert.AreEqual(id, sample.Nav.Focus);
            Assert.IsNotNull(sample.Nav.Outline, "the outline exists");
            Assert.IsTrue(sample.Nav.Outline.Showing, "the outline shows on " + id);
            Assert.AreSame(sample.Buttons[id].transform, sample.Nav.Outline.Target, "the outline rings " + id);
            Assert.IsNotNull(sample.Nav.Outline.Thread, "the candy-corn stitch is drawn");
            Vector2 at = sample.Nav.Outline.transform.position;
            Vector2 target = sample.Buttons[id].transform.position;
            Assert.Less(Vector2.Distance(at, target), 1f, "the outline sits on its target");
        }

        [UnityTest]
        public IEnumerator ASampleScreenNavigatesByKeyboard()
        {
            ControlSchemeTracker.Override = ControlScheme.KeyboardMouse;
            var sample = BuildSample();
            var submitPrompt = sample.Nav.AddPrompt(ControlPrompt.Submit, sample.Root.transform, new Vector2(0f, 0f),
                new Vector2(60f, 60f), 56f);
            yield return Frames(2);
            AssertRinged(sample, "card1");
            Assert.AreEqual("Enter", submitPrompt.KeyLabel, "the prompt names the key that submits");

            yield return Tap(_keyboard.rightArrowKey);
            AssertRinged(sample, "card2");
            yield return Tap(_keyboard.enterKey);
            Assert.AreEqual(1, sample.Clicks["card2"], "Enter presses the focused button");
            yield return Tap(_keyboard.dKey);
            AssertRinged(sample, "card3");
            yield return Tap(_keyboard.rightArrowKey);
            AssertRinged(sample, "card1"); // the row wraps
            yield return Tap(_keyboard.sKey);
            AssertRinged(sample, "done");
            yield return Tap(_keyboard.spaceKey);
            Assert.AreEqual(1, sample.Clicks["done"]);
            yield return Tap(_keyboard.upArrowKey);
            yield return Tap(_keyboard.upArrowKey);
            AssertRinged(sample, "close");
            yield return Tap(_keyboard.pageDownKey);
            yield return Tap(_keyboard.qKey);
            Assert.AreEqual(1, sample.PagesRight);
            Assert.AreEqual(1, sample.PagesLeft);

            // Back exits from anywhere, through the close button's own action.
            yield return Tap(_keyboard.downArrowKey);
            Assert.AreEqual("card3", sample.Nav.Focus);
            yield return Tap(_keyboard.escapeKey);
            Assert.IsFalse(sample.Root.activeSelf, "Escape closes the screen");
            Assert.AreEqual(1, sample.Clicks["close"]);
            Assert.IsFalse(sample.Nav.Actions.Enabled, "a closed screen listens to nothing");
        }

        [UnityTest]
        public IEnumerator ASampleScreenNavigatesByGamepadAndItsPromptsFollowTheScheme()
        {
            ControlSchemeTracker.Override = ControlScheme.Gamepad;
            var sample = BuildSample();
            var submitPrompt = sample.Nav.AddPrompt(ControlPrompt.Submit, sample.Root.transform, Vector2.zero,
                new Vector2(60f, 60f), 56f);
            var backPrompt = sample.Nav.AddPrompt(ControlPrompt.Back, sample.Root.transform, Vector2.zero,
                new Vector2(130f, 60f), 56f);
            yield return Frames(2);
            Assert.AreEqual(PadButton.South, submitPrompt.LitButton);
            Assert.AreEqual(PadButton.East, backPrompt.LitButton);
            AssertRinged(sample, "card1");

            yield return Tap(_gamepad.dpad.right);
            yield return Tap(_gamepad.dpad.right);
            AssertRinged(sample, "card3");
            yield return Tap(_gamepad.buttonSouth);
            Assert.AreEqual(1, sample.Clicks["card3"]);
            yield return Tap(_gamepad.dpad.up);
            AssertRinged(sample, "close");
            yield return Tap(_gamepad.dpad.down);
            AssertRinged(sample, "card3");
            yield return Tap(_gamepad.rightShoulder);
            yield return Tap(_gamepad.leftShoulder);
            Assert.AreEqual(1, sample.PagesRight);
            Assert.AreEqual(1, sample.PagesLeft);

            // A greyed card is skipped and cannot hold focus.
            sample.Buttons["card2"].interactable = false;
            yield return Tap(_gamepad.dpad.left);
            AssertRinged(sample, "card1");

            // The prompts and the outline follow the scheme: key caps on the keyboard, nothing extra on touch.
            ControlSchemeTracker.Override = ControlScheme.KeyboardMouse;
            Assert.AreEqual("Enter", submitPrompt.KeyLabel);
            Assert.IsFalse(string.IsNullOrEmpty(backPrompt.KeyLabel), "the Back prompt names its key");
            ControlSchemeTracker.Override = ControlScheme.Touch;
            yield return Frames(2);
            Assert.IsFalse(submitPrompt.HasGlyph, "touch: the button itself is the prompt");
            Assert.IsFalse(sample.Nav.Outline.Showing, "touch: no focus outline");
            ControlSchemeTracker.Override = ControlScheme.Gamepad;
            yield return Frames(2);
            AssertRinged(sample, "card1");

            // A held item is let go before Back leaves the screen.
            bool holding = true;
            sample.Nav.BackOverride = () =>
            {
                if (!holding)
                {
                    return false;
                }

                holding = false;
                return true;
            };
            yield return Tap(_gamepad.buttonEast);
            Assert.IsTrue(sample.Root.activeSelf, "the first Back lets go of the held item");
            yield return Tap(_gamepad.buttonEast);
            Assert.IsFalse(sample.Root.activeSelf, "the second Back exits");
        }

        [UnityTest]
        public IEnumerator TouchStillPressesTheButtonsDirectly()
        {
            ControlSchemeTracker.Override = ControlScheme.Touch;
            var sample = BuildSample();
            yield return Frames(2);
            Assert.IsTrue(sample.Nav.Outline == null || !sample.Nav.Outline.Showing, "no outline under a finger");
            var tap = new PointerEventData(EventSystem.current) { button = PointerEventData.InputButton.Left };
            ExecuteEvents.Execute(sample.Buttons["card2"].gameObject, tap, ExecuteEvents.pointerClickHandler);
            Assert.AreEqual(1, sample.Clicks["card2"]);
            ExecuteEvents.Execute(sample.Buttons["close"].gameObject, tap, ExecuteEvents.pointerClickHandler);
            Assert.IsFalse(sample.Root.activeSelf, "the close disc closes on a tap, as Back does on a pad");
        }

        [Test]
        public void TheNavigationMapBindsWhatItsGlyphsShow()
        {
            using (var actions = GameInput.CreateUiNavigate())
            {
                Assert.AreEqual(UiNavigateActions.MapName, actions.Map.name);
                foreach (var prompt in new[]
                         {
                             ControlPrompt.Submit, ControlPrompt.Back, ControlPrompt.PageLeft, ControlPrompt.PageRight,
                             ControlPrompt.Rewatch,
                         })
                {
                    var action = actions.For(prompt);
                    Assert.IsNotNull(action, prompt.ToString());
                    var paths = new List<string>();
                    foreach (var binding in action.bindings)
                    {
                        paths.Add(binding.path);
                    }

                    Assert.AreEqual(ControlGlyphs.For(ControlScheme.KeyboardMouse, prompt).BindingPath, paths[0],
                        prompt + ": the key glyph is the first binding");
                    CollectionAssert.Contains(paths, ControlGlyphs.For(ControlScheme.Gamepad, prompt).BindingPath,
                        prompt + ": the gamepad glyph is bound");
                }

                Assert.IsNull(actions.For(ControlPrompt.Jump), "gameplay prompts are not UI navigation");
            }
        }

        // ---------------------------------------------------------------------------------------- the notebook

        private static CaseDirector Director => GameBootstrap.Instance.Director;

        private static IEnumerator StartGame()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            yield return null;
            GameBootstrap.Instance.Ui.NewGameButton.onClick.Invoke();
            yield return Frames(10);
            Assert.AreEqual(GameState.Playing, GameBootstrap.Instance.Flow.State);
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
                yield return Frames(2);
            }

            Director.Captions.Clear();
            yield return Wait(0.4f);
        }

        private static CaseAction A(CaseActionKind kind) => CaseAction.Of(kind);

        private static List<string> TrayIds(NotebookView notebook)
        {
            var ids = new List<string>();
            foreach (var s in notebook.TrayStickers)
            {
                ids.Add(s.StickerId);
            }

            return ids;
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator TheNotebookStillWorksByGamepadWithTheFocusOutline()
        {
            yield return StartGame();
            yield return Do(A(CaseActionKind.HearKnocks), A(CaseActionKind.ExamineGrille), A(CaseActionKind.LinkArm),
                A(CaseActionKind.TurnCrank));
            Director.OpenNotebook();
            yield return Frames(2);
            var notebook = Director.Notebook;
            Assert.IsTrue(notebook.IsOpen);
            Assert.IsTrue(notebook.UiActionsEnabled, "the shared map is on while the notebook is open");
            int moved = notebook.PageQuestions.IndexOf(N.Question(N.QuestionMoved));
            Assert.GreaterOrEqual(moved, 0);

            // The bumpers turn the pages (no outline until the pad moves the cursor, as before).
            int start = notebook.CurrentPage;
            if (notebook.PageCount > 1)
            {
                yield return Tap(start < notebook.PageCount - 1 ? _gamepad.rightShoulder : _gamepad.leftShoulder);
                Assert.AreNotEqual(start, notebook.CurrentPage, "a bumper turns the page");
            }

            notebook.ShowPage(moved);
            yield return Wait(0.5f);
            Assert.IsTrue(notebook.FocusOutline == null || !notebook.FocusOutline.Showing);

            foreach (string id in new[] { N.LooseArm, N.OwlBooth })
            {
                yield return Tap(_gamepad.dpad.down);
                for (int i = 0; i < 6; i++)
                {
                    yield return Tap(_gamepad.dpad.left);
                }

                int index = TrayIds(notebook).IndexOf(id);
                Assert.GreaterOrEqual(index, 0, id);
                for (int i = 0; i < index; i++)
                {
                    yield return Tap(_gamepad.dpad.right);
                }

                Assert.AreEqual(CursorRow.Tray, notebook.CursorRow);
                Assert.AreEqual(index, notebook.CursorIndex);
                Assert.IsTrue(notebook.FocusOutline != null && notebook.FocusOutline.Showing, "the cursor is ringed");
                Assert.AreSame(notebook.TraySticker(id).Rect, notebook.FocusOutline.Target, "the outline rings " + id);

                yield return Tap(_gamepad.buttonSouth);
                Assert.AreEqual(id, notebook.HeldSticker, "South picks the sticker");
                Assert.AreEqual(CursorRow.Slots, notebook.CursorRow);
                Assert.AreSame(notebook.Slots[notebook.CursorIndex].rectTransform, notebook.FocusOutline.Target,
                    "the outline follows to the slot");

                if (id == N.LooseArm)
                {
                    // East lets go of a held sticker first; the notebook stays open.
                    yield return Tap(_gamepad.buttonEast);
                    Assert.IsNull(notebook.HeldSticker);
                    Assert.IsTrue(notebook.IsOpen);
                    yield return Tap(_gamepad.dpad.down);
                    int again = TrayIds(notebook).IndexOf(id);
                    for (int i = 0; i < 6; i++)
                    {
                        yield return Tap(_gamepad.dpad.left);
                    }

                    for (int i = 0; i < again; i++)
                    {
                        yield return Tap(_gamepad.dpad.right);
                    }

                    yield return Tap(_gamepad.buttonSouth);
                    Assert.AreEqual(id, notebook.HeldSticker);
                }

                yield return Tap(_gamepad.buttonSouth);
                Assert.IsNull(notebook.HeldSticker);
                Assert.GreaterOrEqual(notebook.CurrentBoard.SlotOf(id), 0, "South places it");
            }

            float until = Time.time + 6f;
            while (notebook.Checking && Time.time < until)
            {
                yield return null;
            }

            Assert.AreEqual(DeductionVerdict.Right, notebook.LastCheck.Verdict);
            Assert.IsTrue(Director.State.Has(CaseFact.SolvedMoved));

            yield return Tap(_gamepad.buttonEast);
            yield return Frames(2);
            Assert.IsFalse(notebook.IsOpen, "East closes the notebook");
            Assert.IsFalse(notebook.UiActionsEnabled, "and its map is off");
        }
    }
}
