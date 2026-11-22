using System;
using NUnit.Framework;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Core.World;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// The hint pictures' rules (docs/nonverbal-ux.md §6, D23.4): which tier shows what and when, the tier-1 sparkle
    /// target and the character who looks at it, and the action the tier-3 demonstration shows.
    /// </summary>
    public class HintPresentationTests
    {
        private static NonverbalSettings Helper(bool on) => new NonverbalSettings { HelperAutoHints = on };

        [Test]
        public void EachRadioPressShowsItsTiersPicture()
        {
            var off = Helper(false);
            Assert.AreEqual(new HintVisuals(true, false, false),
                HintPresentation.For(HintTrigger.RadioPress, HintTier.Observation, off));
            Assert.AreEqual(new HintVisuals(false, true, false),
                HintPresentation.For(HintTrigger.RadioPress, HintTier.Location, off));
            Assert.AreEqual(new HintVisuals(false, false, true),
                HintPresentation.For(HintTrigger.RadioPress, HintTier.Action, off));
        }

        [Test]
        public void WithoutAPressTiersOneAndThreeNeverShowWhileHelperIsOff()
        {
            var off = Helper(false);
            foreach (HintTier tier in Enum.GetValues(typeof(HintTier)))
            {
                foreach (float stall in new[] { 0f, 60f, NonverbalSettings.AutoHintStallSeconds, 10000f })
                {
                    var v = HintPresentation.For(HintTrigger.HelperStall, tier, off, stall);
                    Assert.IsFalse(v.Any, $"tier {tier}, stall {stall}: nothing shows by itself");
                }
            }

            Assert.IsFalse(HintPresentation.For(HintTrigger.HelperStall, HintTier.Action, null, 10000f).Any);
        }

        [Test]
        public void HelperShowsLookAndShowMeAfterTheStallOnly()
        {
            var on = Helper(true);
            Assert.IsFalse(HintPresentation.For(HintTrigger.HelperStall, HintTier.Observation, on,
                NonverbalSettings.AutoHintStallSeconds - 1f).Any, "not before the stall");
            var v = HintPresentation.For(HintTrigger.HelperStall, HintTier.Observation, on,
                NonverbalSettings.AutoHintStallSeconds);
            Assert.IsTrue(v.Sparkle, "the Helper may show 'look here'");
            Assert.IsTrue(v.GhostDemo, "and 'show me'");
            Assert.IsFalse(v.BatTrail, "the trail stays a press-only picture");
        }

        [Test]
        public void EveryObjectiveButFreeRoamHasASparkleTarget()
        {
            foreach (CaseObjective objective in Enum.GetValues(typeof(CaseObjective)))
            {
                bool has = HintPresentation.TryTarget(objective, out var target, out string part);
                if (objective == CaseObjective.FreeRoam)
                {
                    Assert.IsFalse(has);
                    continue;
                }

                Assert.IsTrue(has, objective + " has a target");
                StringAssert.StartsWith("pg.", part);
                Assert.Greater(target.Y, 0f, objective + " target is above the ground");
            }
        }

        [Test]
        public void TheSparkleSitsOnTheObjectAtItsMarkerHeight()
        {
            Assert.IsTrue(HintPresentation.TryTarget(CaseObjective.TurnCrank, out var crank, out string part));
            Assert.AreEqual("pg.crank", part);
            var point = CaseStage.Find(CaseStage.PointCrank);
            Assert.AreEqual(point.Position.X, crank.X, 1e-4f);
            Assert.AreEqual(point.Position.Y + point.MarkerHeight, crank.Y, 1e-4f);
            Assert.AreEqual(point.Position.Z, crank.Z, 1e-4f);
        }

        [Test]
        public void ATargetBehindTheHouseWallsSparklesAtTheThresholdOnPipsSide()
        {
            // Pip outside, the latch inside: the sparkle goes to June's window while the door is shut.
            float outX = 0f;
            float outZ = CaseRouteLayout.FrontZ - 6f;
            Assert.IsFalse(CaseRouteLayout.Inside(outX, outZ));
            Assert.IsTrue(HintPresentation.TryTarget(CaseObjective.TurnCrank, outX, outZ, false, out var shut, out string part));
            Assert.AreEqual("pg.june_window", part);
            var dormer = Thresholds.WayTo(false, true, false);
            Assert.AreEqual(dormer.CentreX, shut.X, 1e-4f);
            Assert.AreEqual(dormer.CentreZ, shut.Z, 1e-4f);

            Assert.IsTrue(HintPresentation.TryTarget(CaseObjective.TurnCrank, outX, outZ, true, out _, out part));
            Assert.AreEqual("pg.door_open", part, "the open front door once it is open");

            // Pip inside: straight to the crank.
            var crank = CaseStage.Find(CaseStage.PointCrank).Position;
            Assert.IsTrue(HintPresentation.TryTarget(CaseObjective.TurnCrank, crank.X - 1f, crank.Z, true, out var direct,
                out part));
            Assert.AreEqual("pg.crank", part);
            Assert.AreEqual(crank.X, direct.X, 1e-4f);
        }

        [Test]
        public void TheNearestCharacterInViewLooks()
        {
            var candidates = new[]
            {
                new LookCandidate(10f, 0f, 0f, true),
                new LookCandidate(3f, 0f, 0f, false), // nearest, but off screen
                new LookCandidate(5f, 0f, 0f, true),
                new LookCandidate(1f, 0f, 0f, true, false), // hidden (indoors, talking)
            };
            Assert.AreEqual(2, HintPresentation.ChooseLooker(candidates, 0f, 0f));
        }

        [Test]
        public void NobodyLooksWhenNoOneIsInViewOrNear()
        {
            Assert.AreEqual(-1, HintPresentation.ChooseLooker(new[] { new LookCandidate(1f, 0f, 1f, false) }, 0f, 0f));
            Assert.AreEqual(-1, HintPresentation.ChooseLooker(
                new[] { new LookCandidate(HintPresentation.MaxLookDistance + 1f, 0f, 0f, true) }, 0f, 0f));
            Assert.AreEqual(-1, HintPresentation.ChooseLooker(Array.Empty<LookCandidate>(), 0f, 0f));
            Assert.AreEqual(-1, HintPresentation.ChooseLooker(null, 0f, 0f));
        }

        [Test]
        public void TheDemoShowsUseInReachTheHighJumpBelowAGoalAndAPushOtherwise()
        {
            var goal = new StagePoint(0f, HintPresentation.AnchorLift, 0f);
            Assert.AreEqual(HintDemo.Use, HintPresentation.DemoFor(10f, 0f, 0f, goal, true), "a Use target in range");
            Assert.AreEqual(HintDemo.Use, HintPresentation.DemoFor(1.5f, 0f, 0f, goal, false), "close and level");
            Assert.AreEqual(HintDemo.Move, HintPresentation.DemoFor(10f, 0f, 0f, goal, false), "far away");
            var high = new StagePoint(0f, 2.4f + HintPresentation.AnchorLift, 0f);
            Assert.AreEqual(HintDemo.HoldJump, HintPresentation.DemoFor(2f, 0f, 0f, high, false), "just below a high goal");
            Assert.AreEqual(HintDemo.Move, HintPresentation.DemoFor(12f, 0f, 0f, high, false), "a high goal far away");
            Assert.AreEqual(ControlPrompt.HoldJump, HintPresentation.PromptFor(HintDemo.HoldJump));
            Assert.AreEqual(ControlPrompt.Use, HintPresentation.PromptFor(HintDemo.Use));
            Assert.AreEqual(ControlPrompt.Move, HintPresentation.PromptFor(HintDemo.Move));
        }
    }
}
