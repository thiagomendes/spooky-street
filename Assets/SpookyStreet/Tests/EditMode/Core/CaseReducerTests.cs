using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpookyStreet.Core.Case;

namespace SpookyStreet.Tests.Core
{
    public class CaseDefinitionTests
    {
        [Test]
        public void EveryFactHasOneStableSemanticId()
        {
            var ids = new HashSet<string>();
            var facts = new HashSet<CaseFact>();
            foreach (var definition in CaseDefinition.Facts)
            {
                StringAssert.IsMatch(@"^c01\.[a-z_]+\.[a-z_]+$", definition.StableId);
                Assert.IsTrue(ids.Add(definition.StableId), "duplicate id " + definition.StableId);
                Assert.IsTrue(facts.Add(definition.Fact), "fact defined twice " + definition.Fact);
                Assert.IsTrue(CaseDefinition.TryParse(definition.StableId, out var parsed));
                Assert.AreEqual(definition.Fact, parsed);
            }

            Assert.AreEqual(Enum.GetValues(typeof(CaseFact)).Length, facts.Count, "every enum value is in the table");
            Assert.IsFalse(CaseDefinition.TryParse("c01.nope", out _));
            Assert.IsFalse(CaseDefinition.TryParse(null, out _));
        }

        [Test]
        public void SpecBeatNamesMapToTheSpecExampleIds()
        {
            string[] specNames =
            {
                "heardKnocks", "beaSpoke", "valeSpoke", "checkedView", "enteredByWindow", "foundJune", "frontOpen",
                "sawCam", "linkedArm", "testedOwl", "caseClosed",
            };
            var byName = new Dictionary<string, FactDefinition>();
            foreach (var definition in CaseDefinition.Facts)
            {
                byName[definition.BeatName] = definition;
            }

            foreach (string name in specNames)
            {
                Assert.IsTrue(byName.ContainsKey(name), "spec beat name missing: " + name);
            }

            // Spec §7 names these two IDs explicitly.
            Assert.AreEqual("c01.attic.entered", byName["enteredByWindow"].StableId);
            Assert.AreEqual("c01.workshop.linked", byName["linkedArm"].StableId);
        }

        [Test]
        public void OnlyFrontOpenAndTestedOwlAreRequired()
        {
            var required = new List<CaseFact>();
            foreach (var definition in CaseDefinition.Facts)
            {
                if (definition.Role == FactRole.Required)
                {
                    required.Add(definition.Fact);
                }
            }

            CollectionAssert.AreEquivalent(new[] { CaseFact.FrontOpen, CaseFact.TestedOwl }, required);
            CollectionAssert.AreEquivalent(required, CaseDefinition.EndingRequires);
        }

        [Test]
        public void UnknownOrImpossibleAnchorsFallBackToThePlaza()
        {
            Assert.AreEqual(CaseAnchors.Plaza, CaseAnchors.Resolve("c01.anchor.moon", CaseState.Empty));
            Assert.AreEqual(CaseAnchors.Plaza, CaseAnchors.Resolve(null, CaseState.Empty));
            Assert.AreEqual(CaseAnchors.Plaza, CaseAnchors.Resolve(CaseAnchors.Foyer, CaseState.Empty), "inside without the window");
            Assert.AreEqual(CaseAnchors.Bench, CaseAnchors.Resolve(CaseAnchors.Bench, CaseState.Empty));
            var inside = CaseState.FromFacts(new[] { CaseFact.EnteredByWindow });
            Assert.AreEqual(CaseAnchors.Foyer, CaseAnchors.Resolve(CaseAnchors.Foyer, inside));
            foreach (string id in CaseAnchors.All)
            {
                StringAssert.IsMatch(@"^c01\.anchor\.[a-z_]+$", id);
            }
        }
    }

    public class CaseReducerTests
    {
        [Test]
        public void EveryActionIsIdempotentFromEveryReachableState()
        {
            foreach (var state in ReachableStates())
            {
                foreach (var action in CasePlaythrough.AllActions())
                {
                    var once = state.Apply(action);
                    var twice = once.State.Apply(action);
                    Assert.AreEqual(0, twice.Gained.Count, $"{action} gained twice from {state}");
                    Assert.AreSame(once.State, twice.State, $"{action} changed state twice from {state}");
                    Assert.IsFalse(twice.ShouldAutosave);
                    Assert.IsFalse(twice.PlayCutaway, "the cutaway plays only on the first test");
                }
            }
        }

        [Test]
        public void NoReachableStateCanDeadlockTheCase()
        {
            // From anywhere, the mandatory route still closes the case: optional evidence and order never block it.
            CaseAction[] route =
            {
                CaseAction.Of(CaseActionKind.EnterAttic), CaseAction.Of(CaseActionKind.LiftLatch),
                CaseAction.Of(CaseActionKind.LinkArm), CaseAction.Of(CaseActionKind.TurnCrank),
                CaseAction.Of(CaseActionKind.ArriveAtPorch),
            };
            int count = 0;
            foreach (var start in ReachableStates())
            {
                var state = start;
                foreach (var action in route)
                {
                    state = state.Apply(action).State;
                }

                Assert.IsTrue(state.Has(CaseFact.CaseClosed), "deadlock from " + start);
                count++;
            }

            Assert.Greater(count, 100, "the search explored the state space");
        }

        [Test]
        public void OptionalFactsNeverGateAnyAction()
        {
            // Removing every optional fact from a state never changes which route facts an action awards.
            foreach (var state in ReachableStates())
            {
                var stripped = new List<CaseFact>();
                foreach (var fact in state.Facts)
                {
                    if (CaseDefinition.Get(fact).Role != FactRole.Optional)
                    {
                        stripped.Add(fact);
                    }
                }

                var bare = CaseState.FromFacts(stripped);
                foreach (var action in CasePlaythrough.AllActions())
                {
                    Assert.AreEqual(RouteGains(state.Apply(action)), RouteGains(bare.Apply(action)), $"{action} from {state}");
                }
            }
        }

        [Test]
        public void TheWorldAcceptsCorrectActionsEarly()
        {
            var state = CaseState.Empty;

            // Straight to the workshop: linking the arm also proves Pip saw the cam.
            var link = state.Apply(CaseAction.Of(CaseActionKind.LinkArm));
            CollectionAssert.AreEqual(new[] { CaseFact.SawCam, CaseFact.LinkedArm }, link.Gained);

            // Lifting the inside latch proves the window entry, with no interviews at all.
            var latch = CaseState.Empty.Apply(CaseAction.Of(CaseActionKind.LiftLatch));
            CollectionAssert.Contains(latch.Gained, CaseFact.EnteredByWindow);
            CollectionAssert.Contains(latch.Gained, CaseFact.FrontOpen);
        }

        [Test]
        public void EnteringTheAtticFirstAwardsTheRouteProofAndTheShortcut()
        {
            var step = CaseState.Empty.Apply(CaseAction.Of(CaseActionKind.EnterAttic));
            CollectionAssert.AreEqual(new[] { CaseFact.EnteredByWindow, CaseFact.AtticFirst }, step.Gained);
            Assert.AreEqual(CaseOutcome.Progressed, step.Outcome);

            var afterTalk = CaseState.Empty.Apply(CaseAction.Talk(Speaker.Vale)).State
                .Apply(CaseAction.Of(CaseActionKind.EnterAttic));
            CollectionAssert.AreEqual(new[] { CaseFact.EnteredByWindow }, afterTalk.Gained, "no shortcut after an interview");
        }

        [Test]
        public void TheEndingNeedsFrontOpenTestedOwlAndThePorch()
        {
            var play = new CasePlaythrough();
            Assert.AreEqual(CaseOutcome.NotYet, play.Do(CaseActionKind.ArriveAtPorch).Outcome);
            play.Do(CaseActionKind.LinkArm);
            play.Do(CaseActionKind.TurnCrank);
            Assert.IsTrue(play.State.Has(CaseFact.TestedOwl));
            Assert.AreEqual(CaseOutcome.NotYet, play.Do(CaseActionKind.ArriveAtPorch).Outcome, "door still latched");
            Assert.IsFalse(play.State.Has(CaseFact.CaseClosed));
            play.Do(CaseActionKind.LiftLatch);
            Assert.IsFalse(play.State.Has(CaseFact.CaseClosed), "closure waits for the porch");
            var close = play.Do(CaseActionKind.ArriveAtPorch);
            Assert.IsTrue(close.ClosedCase);
            Assert.IsTrue(close.ShouldAutosave);
            Assert.AreEqual(CaseOutcome.None, play.Do(CaseActionKind.ArriveAtPorch).Outcome, "closing twice does nothing");

            var openOnly = CaseState.FromFacts(new[] { CaseFact.FrontOpen }).Apply(CaseAction.Of(CaseActionKind.ArriveAtPorch));
            Assert.AreEqual(CaseOutcome.NotYet, openOnly.Outcome);
        }

        [Test]
        public void CrankBeforeTheArmIsATapAndAGapWithNoFact()
        {
            var state = CaseState.FromFacts(new[] { CaseFact.EnteredByWindow, CaseFact.FrontOpen, CaseFact.SawCam });
            var early = state.Apply(CaseAction.Of(CaseActionKind.TurnCrank));
            Assert.AreEqual(CaseOutcome.CrankGap, early.Outcome);
            Assert.AreEqual(0, early.Gained.Count);
            Assert.AreSame(state, early.State);
            Assert.IsFalse(early.PlayCutaway);
            Assert.AreEqual(1, early.Lines.Count);
            Assert.AreEqual(SpookyStreet.Core.Localization.LocalizationKeys.CaseLines.CaptionCrankGap, early.Lines[0].Key);

            var linked = state.Apply(CaseAction.Of(CaseActionKind.LinkArm)).State;
            var test = linked.Apply(CaseAction.Of(CaseActionKind.TurnCrank));
            CollectionAssert.AreEqual(new[] { CaseFact.TestedOwl }, test.Gained);
            Assert.IsTrue(test.PlayCutaway);
            var again = test.State.Apply(CaseAction.Of(CaseActionKind.TurnCrank));
            Assert.AreEqual(CaseOutcome.ShowReplayed, again.Outcome);
            Assert.AreEqual(0, again.Gained.Count);
        }

        [Test]
        public void FrontDoorFromOutsideIsStuckUntilTheLatchIsLifted()
        {
            var stuck = CaseState.Empty.Apply(CaseAction.Of(CaseActionKind.TryFrontDoor));
            Assert.AreEqual(CaseOutcome.DoorStuck, stuck.Outcome);
            Assert.AreEqual(0, stuck.Gained.Count);
            var open = CaseState.FromFacts(new[] { CaseFact.FrontOpen }).Apply(CaseAction.Of(CaseActionKind.TryFrontDoor));
            Assert.AreEqual(CaseOutcome.None, open.Outcome);
            Assert.AreEqual(0, open.Lines.Count);
        }

        [Test]
        public void AuntKitIsAbsentUntilTheReopening()
        {
            var step = CaseState.Empty.Apply(CaseAction.Talk(Speaker.Kit));
            Assert.AreEqual(CaseOutcome.Absent, step.Outcome);
            Assert.AreEqual(0, step.Lines.Count);
        }

        [Test]
        public void ActionFactoriesRejectNonsense()
        {
            Assert.Throws<ArgumentException>(() => CaseAction.Of(CaseActionKind.Talk));
            Assert.Throws<ArgumentException>(() => CaseAction.Talk(Speaker.Pip));
            Assert.Throws<ArgumentException>(() => CaseAction.Talk(Speaker.Caption));
        }

        [Test]
        public void VerbsCoverEveryInteractionAndNoTrigger()
        {
            foreach (CaseActionKind kind in Enum.GetValues(typeof(CaseActionKind)))
            {
                bool trigger = kind == CaseActionKind.HearKnocks || kind == CaseActionKind.EnterAttic ||
                               kind == CaseActionKind.EnterFoyer || kind == CaseActionKind.ArriveAtPorch;
                Assert.AreEqual(trigger, CaseInteractions.IsTrigger(kind), kind.ToString());
            }
        }

        private static string RouteGains(CaseStep step)
        {
            var parts = new List<string>();
            foreach (var fact in step.Gained)
            {
                if (CaseDefinition.Get(fact).Role != FactRole.Optional)
                {
                    parts.Add(fact.ToString());
                }
            }

            return string.Join(",", parts);
        }

        /// <summary>Breadth-first search over every fact set reachable from a new case through any actions.</summary>
        internal static List<CaseState> ReachableStates()
        {
            var seen = new HashSet<int>();
            var result = new List<CaseState>();
            var queue = new Queue<CaseState>();
            queue.Enqueue(CaseState.Empty);
            seen.Add(0);
            while (queue.Count > 0)
            {
                var state = queue.Dequeue();
                result.Add(state);
                foreach (var action in CasePlaythrough.AllActions())
                {
                    var next = state.Apply(action).State;
                    if (seen.Add(Mask(next)))
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            return result;
        }

        internal static int Mask(CaseState state)
        {
            int mask = 0;
            foreach (var fact in state.Facts)
            {
                mask |= 1 << (int)fact;
            }

            return mask;
        }
    }
}
