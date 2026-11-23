using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Case;
using Lines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;

namespace SpookyStreet.Tests.Core
{
    /// <summary>The spec §8 case checks that can run engine-free.</summary>
    public class CaseScenarioTests
    {
        /// <summary>
        /// CASE-01: both witnesses → bench → cloth → attic → sill (the entry deduction) → June → foyer → cam (the source
        /// deduction) → arm → crank (the moved deduction) → porch: the show and the "Case closed" stamp together.
        /// </summary>
        public static CasePlaythrough PlayCanonical()
        {
            var play = new CasePlaythrough();
            play.Do(CaseActionKind.HearKnocks);
            play.Talk(Speaker.Bea);
            play.Talk(Speaker.Vale);
            play.Do(CaseActionKind.SitOnBench);
            play.Do(CaseActionKind.ExamineCloth);
            play.Do(CaseActionKind.EnterAttic);
            play.Do(CaseActionKind.ExamineSill);
            play.Do(CaseAction.Solve(CaseNotebook.QuestionEntry, new[] { CaseNotebook.ValeDoor, CaseNotebook.Cloth, CaseNotebook.SillSnag }));
            play.Talk(Speaker.June);
            play.Do(CaseActionKind.EnterFoyer);
            play.Do(CaseActionKind.LiftLatch);
            play.Do(CaseActionKind.ExamineGrille);
            play.Do(CaseAction.Solve(CaseNotebook.QuestionSource, new[] { CaseNotebook.FloorHum, CaseNotebook.WoodenPiece }));
            play.Do(CaseActionKind.LinkArm);
            play.Do(CaseActionKind.TurnCrank);
            play.Do(CaseAction.Solve(CaseNotebook.QuestionMoved, new[] { CaseNotebook.LooseArm, CaseNotebook.OwlBooth }));
            play.Do(CaseActionKind.ArriveAtPorch);
            return play;
        }

        [Test]
        public void Case01CanonicalOrderClosesWithTheCorrectEvidence()
        {
            var play = PlayCanonical();
            Assert.IsTrue(play.State.Has(CaseFact.CaseClosed));
            Assert.IsTrue(play.State.Has(CaseFact.CaseStamped), "all three solved before the porch: stamped with the show");
            Assert.IsFalse(play.State.Has(CaseFact.AtticFirst), "witnesses came first");

            CollectionAssert.AreEqual(new[] { Lines.BeaSawCape }, play.LinesOf(Speaker.Bea));
            CollectionAssert.AreEqual(new[] { Lines.ValeDoorShut, Lines.ValeInviteSit }, play.LinesOf(Speaker.Vale));
            CollectionAssert.AreEqual(
                new[] { Lines.JuneGreet, Lines.JuneDoorSticks, Lines.JuneLatchThanks, Lines.JuneResolve }, play.LinesOf(Speaker.June));
            CollectionAssert.AreEqual(new[] { Lines.KitFirstShow }, play.LinesOf(Speaker.Kit));

            var page = CaseNotebook.Build(play.State);
            Assert.AreEqual(3, page.Questions.Count);
            foreach (var question in page.Questions)
            {
                Assert.IsTrue(question.Solved, question.QuestionKey);
            }

            Assert.IsNotNull(page.FinalCard);
            var ids = new List<string>();
            foreach (var entry in page.FinalCard.Discoveries)
            {
                ids.Add(entry.Id);
            }

            CollectionAssert.AreEqual(new[]
            {
                CaseNotebook.BeaFlash, CaseNotebook.ValeDoor, CaseNotebook.BenchView, CaseNotebook.Cloth, CaseNotebook.SillSnag,
                CaseNotebook.FloorHum, CaseNotebook.WoodenPiece, CaseNotebook.LooseArm, CaseNotebook.OwlBooth,
            }, ids, "the final card lists what this player found, in order");
            Assert.AreEqual(CaseNotebook.Stickers.Count, page.FinalCard.Total);

            var world = CaseWorldView.From(play.State);
            Assert.AreEqual(DoorState.Open, world.FrontDoor);
            Assert.AreEqual(OwlState.Bowing, world.Owl);
            Assert.AreEqual(JunePlacement.Porch, world.June);
            Assert.IsTrue(world.BoothOpen);
        }

        /// <summary>CASE-02: attic first, skip the cloth, crank early, repeat talks, back to the bench after solving.</summary>
        [Test]
        public void Case02CuriousChildKeepsEveryLineInTimeAndTheSameTruth()
        {
            var play = new CasePlaythrough();
            play.Do(CaseActionKind.HearKnocks);
            play.Do(CaseActionKind.EnterAttic);
            Assert.IsTrue(play.State.Has(CaseFact.EnteredByWindow), "route proof before any interview");
            play.Talk(Speaker.June);
            play.Talk(Speaker.June);
            play.Do(CaseActionKind.EnterFoyer);
            Assert.AreEqual(CaseOutcome.CrankGap, play.Do(CaseActionKind.TurnCrank).Outcome, "crank touched early");
            play.Do(CaseActionKind.LiftLatch);
            play.Talk(Speaker.Bea);
            play.Talk(Speaker.Bea);
            play.Talk(Speaker.Vale);
            play.Talk(Speaker.Vale);
            play.Talk(Speaker.June);
            play.Do(CaseActionKind.ExamineGrille);
            play.Do(CaseActionKind.LinkArm);
            play.Do(CaseActionKind.TurnCrank);
            play.Talk(Speaker.June);
            play.Do(CaseActionKind.ArriveAtPorch);
            Assert.IsTrue(play.State.Has(CaseFact.CaseClosed));
            Assert.IsFalse(play.State.Has(CaseFact.CaseStamped), "the show plays; the stamp waits for the deductions");
            CollectionAssert.AreEqual(new[] { Lines.KitFirstShow, Lines.KitAsk }, play.LinesOf(Speaker.Kit));
            // The cloth was skipped: Bea's flash covers the hidden side, the sill snag the silver up high.
            play.Do(CaseActionKind.ExamineSill);
            play.Do(CaseAction.Solve(CaseNotebook.QuestionSource, new[] { CaseNotebook.WoodenPiece, CaseNotebook.FloorHum }));
            play.Do(CaseAction.Solve(CaseNotebook.QuestionMoved, new[] { CaseNotebook.OwlBooth, CaseNotebook.LooseArm }));
            Assert.IsFalse(play.State.Has(CaseFact.CaseStamped));
            play.Do(CaseAction.Solve(CaseNotebook.QuestionEntry,
                new[] { CaseNotebook.BeaFlash, CaseNotebook.SillSnag, CaseNotebook.ValeDoor }));
            Assert.IsTrue(play.State.Has(CaseFact.CaseStamped), "the last deduction stamps the case");
            play.Talk(Speaker.Kit);

            // Return to the bench after solving: missing clues stay discoverable after closure.
            var bench = play.Do(CaseActionKind.SitOnBench);
            CollectionAssert.AreEqual(new[] { CaseFact.CheckedView }, bench.Gained);
            play.Talk(Speaker.Vale);
            play.Do(CaseActionKind.ExamineCandyBowl);
            play.Do(CaseActionKind.TurnCrank);

            Assert.IsFalse(play.State.Has(CaseFact.ExaminedCloth), "the cloth was skipped");
            CollectionAssert.AreEqual(
                new[] { Lines.JuneGreetShortcut, Lines.JuneDoorSticks, Lines.JuneLatchReminder, Lines.JuneLatchThanks,
                        Lines.JuneListenFloor, Lines.JuneGoOutside, Lines.JuneResolve },
                play.LinesOf(Speaker.June), "June acknowledges the shortcut");
            CollectionAssert.AreEqual(
                new[] { Lines.ValeWasShut, Lines.ValeOpenNow, Lines.ValeWasShut, Lines.ValeOpenNow, Lines.ValeClosed, Lines.ValeSavedSweet },
                play.LinesOf(Speaker.Vale), "past tense once the door is open");
            CollectionAssert.DoesNotContain(play.LinesOf(Speaker.Vale), Lines.ValeDoorShut);
            CollectionAssert.AreEqual(new[] { Lines.BeaDoorOpen, Lines.BeaDoorOpen }, play.LinesOf(Speaker.Bea));

            // Same final truth as the canonical order.
            var canonical = PlayCanonical();
            var mine = CaseNotebook.Build(play.State);
            var theirs = CaseNotebook.Build(canonical.State);
            Assert.AreEqual(theirs.Questions.Count, mine.Questions.Count);
            for (int i = 0; i < mine.Questions.Count; i++)
            {
                Assert.IsTrue(mine.Questions[i].Solved);
                Assert.AreEqual(theirs.Questions[i].AnswerKey, mine.Questions[i].AnswerKey);
            }

            var a = CaseWorldView.From(play.State);
            var b = CaseWorldView.From(canonical.State);
            Assert.AreEqual(b.FrontDoor, a.FrontDoor);
            Assert.AreEqual(b.Owl, a.Owl);
            Assert.AreEqual(b.June, a.June);
            Assert.AreEqual(b.BoothOpen, a.BoothOpen);
        }

        [Test]
        public void EveryLineInEveryReachableStateMakesTemporalSense()
        {
            foreach (var state in CaseReducerTests.ReachableStates())
            {
                foreach (var action in CasePlaythrough.AllActions())
                {
                    new CasePlaythrough(state).Do(action);
                }

                foreach (var speaker in new[] { Speaker.Bea, Speaker.Vale, Speaker.June })
                {
                    Assert.Greater(CaseScript.Conversation(speaker, state).Count, 0, $"{speaker} silent in {state}");
                }

                if (state.Has(CaseFact.FrontOpen))
                {
                    foreach (var line in CaseScript.Conversation(Speaker.Vale, state))
                    {
                        Assert.AreNotEqual(Lines.ValeDoorShut, line.Key);
                        Assert.AreNotEqual(Lines.ValeRepeat, line.Key);
                    }
                }
            }
        }

        /// <summary>
        /// CASE-03 (visible deduction), engine-free half: the sightline contract and the evaluator a layout test uses.
        /// The geometry half (bench blocked by the real hedge, treehouse clear) needs S001's markers in the block.
        /// </summary>
        [Test]
        public void Case03SightlineContractExplainsTheContradiction()
        {
            var expected = new Dictionary<string, bool>();
            foreach (var e in CaseSightlines.Expectations)
            {
                expected[e.From + ">" + e.To] = e.Visible;
            }

            Assert.IsTrue(expected["BenchSeat>FrontDoor"], "Vale really could see the door");
            Assert.IsFalse(expected["BenchSeat>SideRouteCape"], "the hedge hides the side from the bench");
            Assert.IsTrue(expected["TreehouseDeck>FrontDoor"]);
            Assert.IsTrue(expected["TreehouseDeck>SideRouteCape"], "both sightlines clear from the treehouse");
            Assert.IsTrue(expected["BeaCorner>SideRouteCape"], "Bea saw the cape over the hedge");
            Assert.IsFalse(expected["BeaCorner>SideRouteFeet"], "but not June's feet");

            // Evaluator: a 1.2 m hedge blocks a seated eye (1.0 m) from foot height behind it, not from 2.4 m above.
            var hedge = new[] { new SightOccluder(-1f, 0f, 4.5f, 1f, 1.2f, 5.5f) };
            Assert.IsFalse(CaseSightlines.IsVisible(0f, 1.0f, 0f, 0f, 0.2f, 10f, hedge));
            Assert.IsTrue(CaseSightlines.IsVisible(0f, 2.4f, 0f, 0f, 2.4f, 10f, hedge));
            Assert.IsTrue(CaseSightlines.IsVisible(3f, 1.0f, 0f, 3f, 0.2f, 10f, hedge), "beside the hedge");

            // Both accounts are true and both end up as stickers in the shared tray.
            var state = PlayCanonical().State;
            var found = new List<string>();
            foreach (var sticker in CaseNotebook.Build(state).Tray)
            {
                found.Add(sticker.Id);
            }

            CollectionAssert.IsSubsetOf(new[] { CaseNotebook.BeaFlash, CaseNotebook.ValeDoor, CaseNotebook.BenchView }, found);
        }
    }
}
