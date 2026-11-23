using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Nonverbal;

namespace SpookyStreet.Tests.Core
{
    /// <summary>S004 F1: one focus map for every screen on gamepad and keyboard (D25): reachable, no traps, Back exits.</summary>
    public class UiFocusGraphTests
    {
        /// <summary>A settings-like card: a close disc on top, three toggles, two sliders, one wide button.</summary>
        private static UiFocusGraph Card()
        {
            var graph = new UiFocusGraph("card")
                .Add("close", 0, 2)
                .AddRow(1, "a", "b", "c")
                .AddRow(2, "slider1", "slider2")
                .Add("done", 3, 1);
            graph.Exit = "close";
            graph.DefaultFocus = "a";
            return graph;
        }

        private static void AssertSound(UiFocusGraph graph)
        {
            var problems = graph.Validate();
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void FocusStartsOnTheDefaultOrTheFirstEnabledElement()
        {
            var graph = Card();
            Assert.AreEqual("a", graph.Start());
            graph.SetEnabled("a", false);
            Assert.AreEqual("close", graph.Start(), "first enabled in focus order");
            graph.DefaultFocus = null;
            Assert.AreEqual("close", graph.Start());
            Assert.AreEqual("close", graph.Resolve("a"), "a disabled element never holds focus");
            Assert.AreEqual("close", graph.Resolve("nowhere"));
            Assert.AreEqual(new[] { "close", "a", "b", "c", "slider1", "slider2", "done" }, graph.Order);
        }

        [Test]
        public void LeftAndRightMoveAlongTheRowAndWrap()
        {
            var graph = Card();
            Assert.AreEqual("b", graph.Move("a", UiDirection.Right));
            Assert.AreEqual("c", graph.Move("b", UiDirection.Right));
            Assert.AreEqual("a", graph.Move("c", UiDirection.Right), "wraps to the row's start");
            Assert.AreEqual("c", graph.Move("a", UiDirection.Left), "wraps to the row's end");
            Assert.AreEqual("close", graph.Move("close", UiDirection.Right), "alone on its row: stays");
            graph.WrapRows = false;
            Assert.AreEqual("c", graph.Move("c", UiDirection.Right), "no wrap: stops at the edge");
        }

        [Test]
        public void UpAndDownLandOnTheNearestColumnOfTheNextRow()
        {
            var graph = Card();
            Assert.AreEqual("close", graph.Move("c", UiDirection.Up));
            Assert.AreEqual("c", graph.Move("close", UiDirection.Down), "column 2 under the close disc");
            Assert.AreEqual("slider1", graph.Move("a", UiDirection.Down));
            Assert.AreEqual("slider2", graph.Move("c", UiDirection.Down), "nearest column when the row is shorter");
            Assert.AreEqual("slider2", graph.Move("done", UiDirection.Up), "column 1: slider2 sits right above");
            Assert.AreEqual("done", graph.Move("done", UiDirection.Down), "bottom edge, no wrap by default");
            graph.WrapColumns = true;
            Assert.AreEqual("close", graph.Move("done", UiDirection.Down), "wraps to the top row");
            Assert.AreEqual("done", graph.Move("close", UiDirection.Up), "wraps to the bottom row");
        }

        [Test]
        public void TiesGoToTheLeftColumn()
        {
            var graph = new UiFocusGraph("tie").Add("top", 0, 1).AddRow(1, "left", "gap", "right");
            graph.SetEnabled("gap", false);
            graph.Exit = "top";
            Assert.AreEqual("left", graph.Move("top", UiDirection.Down));
            Assert.AreEqual("right", graph.Move("left", UiDirection.Right), "the disabled one is skipped");
        }

        [Test]
        public void DisabledElementsAreSkippedAndAWholeEmptyRowIsJumped()
        {
            var graph = Card();
            graph.SetEnabled("slider1", false);
            graph.SetEnabled("slider2", false);
            Assert.AreEqual("done", graph.Move("b", UiDirection.Down), "an empty row is passed over");
            Assert.AreEqual("b", graph.Move("done", UiDirection.Up), "column 1 again");
            AssertSound(graph);
        }

        [Test]
        public void LinksOverrideTheGridAndPassThroughDisabledTargets()
        {
            var graph = Card().Link("done", UiDirection.Up, "close").Link("close", UiDirection.Left, "slider1");
            Assert.AreEqual("close", graph.Move("done", UiDirection.Up));
            graph.SetEnabled("slider1", false);
            Assert.AreEqual("close", graph.Move("close", UiDirection.Left), "a disabled link target falls back to the grid");
            graph.Link("slider1", UiDirection.Left, "a");
            Assert.AreEqual("a", graph.Move("close", UiDirection.Left), "followed through the disabled element");
        }

        [Test]
        public void TabOrderStepsThroughEnabledElementsAndWraps()
        {
            var graph = Card();
            Assert.AreEqual("b", graph.Step("a", 1));
            Assert.AreEqual("close", graph.Step("a", -1));
            Assert.AreEqual("close", graph.Step("done", 1), "wraps");
            graph.SetEnabled("b", false);
            Assert.AreEqual("c", graph.Step("a", 1));
        }

        [Test]
        public void EveryElementIsReachableAndNoneIsATrap()
        {
            AssertSound(Card());

            // An island: an element on its own row with nothing linking to it is still reached by up and down…
            var island = Card().Add("far", 9, 9);
            AssertSound(island);

            // …but a one-way link that leaves no way back is a trap, and Validate names it.
            var trap = new UiFocusGraph("trap").AddRow(0, "close", "pit");
            trap.Exit = "close";
            trap.WrapRows = false;
            trap.Link("pit", UiDirection.Left, "pit");
            var problems = trap.Validate();
            Assert.AreEqual(1, problems.Count, string.Join("\n", problems));
            StringAssert.Contains("'pit' is a trap", problems[0]);
        }

        [Test]
        public void AnUnreachableElementIsReported()
        {
            // A link from every side of the start keeps focus off the second element.
            var graph = new UiFocusGraph("hidden").AddRow(0, "close", "lost");
            graph.Exit = "close";
            foreach (UiDirection d in Enum.GetValues(typeof(UiDirection)))
            {
                graph.Link("close", d, "close");
            }

            var problems = graph.Validate();
            Assert.AreEqual(1, problems.Count, string.Join("\n", problems));
            StringAssert.Contains("'lost' cannot be reached", problems[0]);
        }

        [Test]
        public void BackAlwaysExitsSoEveryScreenNeedsAnEnabledExit()
        {
            var graph = new UiFocusGraph("noexit").AddRow(0, "a", "b");
            StringAssert.Contains("no exit", string.Join("\n", graph.Validate()));
            graph.Exit = "a";
            AssertSound(graph);
            graph.SetEnabled("a", false);
            StringAssert.Contains("exit 'a' is not an enabled element", string.Join("\n", graph.Validate()));
            graph.Exit = "gone";
            StringAssert.Contains("exit 'gone'", string.Join("\n", graph.Validate()));
        }

        [Test]
        public void BrokenLinksAndDefaultsAreReported()
        {
            var graph = Card();
            graph.DefaultFocus = "missing";
            Assert.AreEqual("close", graph.Start());
            graph.Link("a", UiDirection.Up, "ghost");
            var all = string.Join("\n", graph.Validate());
            StringAssert.Contains("default focus 'missing'", all);
            StringAssert.Contains("links to missing 'ghost'", all);
        }

        [Test]
        public void AScreenWithNothingToFocusIsReported()
        {
            var graph = new UiFocusGraph("empty");
            Assert.IsNull(graph.Start());
            Assert.IsNull(graph.Move(null, UiDirection.Down));
            StringAssert.Contains("no enabled element", string.Join("\n", graph.Validate()));
        }

        [Test]
        public void IdsAreUniqueAndKnown()
        {
            var graph = Card();
            Assert.Throws<ArgumentException>(() => graph.Add("a", 5, 5));
            Assert.Throws<ArgumentException>(() => graph.Add("", 5, 5));
            Assert.Throws<ArgumentException>(() => graph.SetEnabled("nope", false));
            Assert.Throws<ArgumentException>(() => graph.Link("nope", UiDirection.Up, "a"));
            Assert.IsFalse(graph.Contains(null));
        }

        [Test]
        public void EveryStateOfTheNotebookShapedScreenIsSound()
        {
            // Wave 2 screens (keepsakes trunk, language screen, parent page, settings) follow this shape: a close disc,
            // page arrows at the sides, a grid of cards, and a bottom row. Each state is checked with what it shows.
            var graph = new UiFocusGraph("pages")
                .Add("close", 0, 4)
                .Add("prev", 1, 0).Add("card1", 1, 1).Add("card2", 1, 2).Add("card3", 1, 3).Add("next", 1, 4)
                .Add("card4", 2, 1).Add("card5", 2, 2).Add("card6", 2, 3).Add("hint", 3, 4);
            graph.Exit = "close";
            graph.DefaultFocus = "card1";
            AssertSound(graph);
            var states = new List<(string, string[])>
            {
                ("first page", new[] { "prev" }),
                ("last page", new[] { "next" }),
                ("half-full page", new[] { "card5", "card6", "card3" }),
                ("only the close disc", new[] { "prev", "next", "card1", "card2", "card3", "card4", "card5", "card6", "hint" }),
            };
            foreach (var (name, hidden) in states)
            {
                foreach (string id in graph.Order)
                {
                    graph.SetEnabled(id, Array.IndexOf(hidden, id) < 0);
                }

                var problems = graph.Validate();
                Assert.IsEmpty(problems, name + ":\n" + string.Join("\n", problems));
            }
        }

        [Test]
        public void TheNavigationPromptsHaveGlyphsOnEveryScheme()
        {
            var expectedPad = new Dictionary<ControlPrompt, PadButton>
            {
                { ControlPrompt.Submit, PadButton.South },
                { ControlPrompt.Back, PadButton.East },
                { ControlPrompt.PageLeft, PadButton.LeftShoulder },
                { ControlPrompt.PageRight, PadButton.RightShoulder },
                { ControlPrompt.Rewatch, PadButton.North },
            };
            var expectedKey = new Dictionary<ControlPrompt, string>
            {
                { ControlPrompt.Submit, "<Keyboard>/enter" },
                { ControlPrompt.Back, "<Keyboard>/escape" },
                { ControlPrompt.PageLeft, "<Keyboard>/q" },
                { ControlPrompt.PageRight, "<Keyboard>/r" },
                { ControlPrompt.Rewatch, "<Keyboard>/v" },
            };
            foreach (var pair in expectedPad)
            {
                var pad = ControlGlyphs.For(ControlScheme.Gamepad, pair.Key);
                Assert.AreEqual(pair.Value, pad.Pad, pair.Key.ToString());
                StringAssert.StartsWith("<Gamepad>/", pad.BindingPath);
                var key = ControlGlyphs.For(ControlScheme.KeyboardMouse, pair.Key);
                Assert.AreEqual(GlyphShape.Key, key.Shape);
                Assert.AreEqual(expectedKey[pair.Key], key.BindingPath);
                Assert.IsTrue(ControlGlyphs.For(ControlScheme.Touch, pair.Key).IsTouch, "touch shows the button itself");
            }
        }
    }
}
