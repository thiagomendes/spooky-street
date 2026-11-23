using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Street;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Core.World;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// The hint pictures' rules after the S002.5b retiering (D26 §6, docs/mystery-first.md §6): tier 1 is an area
    /// nudge, tier 2 the bats to the area, tier 3 only a move; every target is an authored area that stays clear of
    /// the clue objects; nothing shows by itself while the Helper is off, and the Helper only nudges.
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
                HintPresentation.For(HintTrigger.RadioPress, HintTier.Action, off, 0f, HintMove.Jump));
            Assert.AreEqual(new HintVisuals(false, true, false),
                HintPresentation.For(HintTrigger.RadioPress, HintTier.Action, off),
                "no move to show: tier 3 does not exist here, the trail stands");
        }

        [Test]
        public void WithoutAPressNothingShowsWhileHelperIsOff()
        {
            var off = Helper(false);
            foreach (HintTier tier in Enum.GetValues(typeof(HintTier)))
            {
                foreach (float stall in new[] { 0f, 60f, NonverbalSettings.AutoHintStallSeconds, 10000f })
                {
                    var v = HintPresentation.For(HintTrigger.HelperStall, tier, off, stall, HintMove.Jump);
                    Assert.IsFalse(v.Any, $"tier {tier}, stall {stall}: nothing shows by itself");
                }
            }

            Assert.IsFalse(HintPresentation.For(HintTrigger.HelperStall, HintTier.Action, null, 10000f).Any);
        }

        [Test]
        public void TheHelperOnlyNudgesAfterTheStall()
        {
            var on = Helper(true);
            Assert.IsFalse(HintPresentation.For(HintTrigger.HelperStall, HintTier.Observation, on,
                NonverbalSettings.AutoHintStallSeconds - 1f).Any, "not before the stall");
            var v = HintPresentation.For(HintTrigger.HelperStall, HintTier.Action, on,
                NonverbalSettings.AutoHintStallSeconds, HintMove.Jump);
            Assert.IsTrue(v.Breeze, "the Helper may offer the tier-1 nudge");
            Assert.IsFalse(v.MoveDemo, "never a demo (D26 §6)");
            Assert.IsFalse(v.BatTrail, "never the trail");
        }

        [Test]
        public void EveryObjectiveButFreeRoamPointsToAnAreaNotAnObject()
        {
            foreach (CaseObjective objective in Enum.GetValues(typeof(CaseObjective)))
            {
                var area = HintAreas.For(objective);
                if (objective == CaseObjective.FreeRoam)
                {
                    Assert.IsNull(area);
                    continue;
                }

                Assert.IsNotNull(area, objective + " has an area");
            }

            foreach (var area in HintAreas.All)
            {
                Assert.GreaterOrEqual(area.Radius, HintAreas.MinRadius, area.Id + " holds a place, not a point");
                float clearance = HintAreas.Clearance(area, out string nearest);
                Assert.GreaterOrEqual(clearance, HintAreas.MinClearance,
                    $"{area.Id} centre is {clearance:F2} m from the {nearest} target: the glow would pick it out");
            }
        }

        [Test]
        public void TheTierTargetsAreAreasForEveryPipPositionAndDoorState()
        {
            // Pip anywhere on a coarse grid of the street and the interior, door shut or open: the target is always
            // one of the authored areas (never an interaction point), on Pip's side of the house walls.
            var areas = new HashSet<HintArea>(HintAreas.All);
            var spots = new List<(float x, float z)>();
            for (float x = -14f; x <= 10f; x += 4f)
            {
                for (float z = -14f; z <= 18f; z += 4f)
                {
                    spots.Add((x, z));
                }
            }

            spots.Add((0.5f, CaseRouteLayout.InteriorFrontZ + 2.5f));
            spots.Add((-1f, CaseRouteLayout.InteriorFrontZ + 4f));
            foreach (CaseObjective objective in Enum.GetValues(typeof(CaseObjective)))
            {
                foreach (var (x, z) in spots)
                {
                    foreach (bool door in new[] { false, true })
                    {
                        bool has = HintPresentation.TryArea(objective, x, z, door, out var area);
                        Assert.AreEqual(objective != CaseObjective.FreeRoam, has, objective.ToString());
                        if (!has)
                        {
                            continue;
                        }

                        Assert.IsTrue(areas.Contains(area), $"{objective}: {area} is an authored area");
                        Assert.AreEqual(CaseRouteLayout.Inside(x, z), area.Inside,
                            $"{objective} from ({x}, {z}), door {door}: {area.Id} is on Pip's side of the walls");
                    }
                }
            }
        }

        [Test]
        public void AnAreaBehindTheHouseWallsShowsAtTheThresholdOnPipsSide()
        {
            float outX = 0f;
            float outZ = CaseRouteLayout.FrontZ - 6f;
            Assert.IsTrue(HintPresentation.TryArea(CaseObjective.TurnCrank, outX, outZ, false, out var shut));
            Assert.AreEqual(HintAreas.WindowOutside, shut.Id, "June's window while the door is latched");
            Assert.IsTrue(HintPresentation.TryArea(CaseObjective.TurnCrank, outX, outZ, true, out var open));
            Assert.AreEqual(HintAreas.DoorOutside, open.Id, "the front door once it is open");

            var crank = CaseStage.Find(CaseStage.PointCrank).Position;
            Assert.IsTrue(HintPresentation.TryArea(CaseObjective.TurnCrank, crank.X - 1f, crank.Z, true, out var inside));
            Assert.AreEqual(HintAreas.Workshop, inside.Id, "inside: the workshop area itself");
            Assert.IsTrue(HintPresentation.TryArea(CaseObjective.CheckView, crank.X - 1f, crank.Z, false, out var leave));
            Assert.AreEqual(HintAreas.WindowInside, leave.Id, "inside with the bench to find: back to June's window");
        }

        [Test]
        public void TheTrailCirclesTheAreaAtItsRadiusNeverItsCentre()
        {
            const float radius = 2.5f;
            for (float u = 0f; u <= 1f; u += 0.01f)
            {
                HintPresentation.TrailPose(u, 0.7f, radius, out float ox, out float oz, out float approach);
                float r = (float)Math.Sqrt(ox * ox + oz * oz);
                Assert.AreEqual(radius, r, 1e-3f, $"u {u}: the bat aims at or flies on the ring");
                Assert.AreEqual(u < HintPresentation.TrailArrive ? u / HintPresentation.TrailArrive : 1f, approach, 1e-4f);
            }

            HintPresentation.TrailPose(1f, 0.7f, radius, out float ex, out float ez, out _);
            Assert.AreEqual(Math.Cos(0.7f) * radius, ex, 1e-3f, "one full lap");
            Assert.AreEqual(Math.Sin(0.7f) * radius, ez, 1e-3f);
        }

        [Test]
        public void TheCirclingBatsAndSwirlingLeavesNeverSitOnAThing()
        {
            // Tier 1 and 2 show the area: the bats circle its ring and the leaves swirl at its centre, so neither ever
            // comes to rest on a Use target (its floating marker point).
            foreach (var area in HintAreas.All)
            {
                float ring = area.Radius * HintPresentation.TrailRingFactor;
                for (int i = 0; i < 72; i++)
                {
                    float a = i * 5f * (float)Math.PI / 180f;
                    AssertClear(area, area.Centre.X + (float)Math.Cos(a) * ring, area.Centre.Y + HintPresentation.TrailLift,
                        area.Centre.Z + (float)Math.Sin(a) * ring, "bat");
                    AssertClear(area, area.Centre.X + (float)Math.Cos(a) * HintPresentation.SwirlRadius,
                        area.Centre.Y + HintPresentation.SwirlLift, area.Centre.Z + (float)Math.Sin(a) * HintPresentation.SwirlRadius,
                        "leaf");
                }
            }
        }

        [Test]
        public void TargetsTheStreetRemembersNeverMeetAHint()
        {
            // First evening, then a replay over a remembering street: whenever a remembered-only target is offered,
            // the case is closed, so the objective is free roam and no hint area is shown (D29 rests memories).
            var street = StreetState.Empty;
            foreach (var pass in new[] { "first", "replay" })
            {
                foreach (var step in CaseScenarioTests.PlayCanonical().Steps)
                {
                    street = StreetRules.Observe(street, CaseDefinition.CaseId, step.State);
                    var world = StreetView.From(street, CaseDefinition.CaseId, step.State).Apply(CaseWorldView.From(step.State));
                    foreach (var point in CaseStage.AllPoints)
                    {
                        if (CaseStage.OnlyOnceRemembered(point.Id) && CaseStage.IsOffered(point, world, false, false))
                        {
                            Assert.IsNull(HintAreas.For(CaseHints.ObjectiveFor(step.State)), $"{pass}: {point.Id} with a hint");
                        }
                    }
                }

                street = StreetRules.AfterReplay(street);
            }

            Assert.IsFalse(street.IsEmpty, "the replay ran over a remembering street");
        }

        private static void AssertClear(HintArea area, float x, float y, float z, string what)
        {
            foreach (var point in CaseStage.AllPoints)
            {
                if (CaseStage.OnlyOnceRemembered(point.Id))
                {
                    continue; // never shares the street with a hint (see the test below)
                }

                float dx = point.Position.X - x;
                float dy = point.Position.Y + point.MarkerHeight - y;
                float dz = point.Position.Z - z;
                float d = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                Assert.GreaterOrEqual(d, HintPresentation.MinMarkerClearance,
                    $"{area.Id}: a {what} at ({x:F2}, {y:F2}, {z:F2}) passes {d:F2} m from the {point.Id} target");
            }
        }

        [Test]
        public void TheNearestCharacterInViewGlances()
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
        public void NobodyGlancesWhenNoOneIsInViewOrNear()
        {
            Assert.AreEqual(-1, HintPresentation.ChooseLooker(new[] { new LookCandidate(1f, 0f, 1f, false) }, 0f, 0f));
            Assert.AreEqual(-1, HintPresentation.ChooseLooker(
                new[] { new LookCandidate(HintPresentation.MaxLookDistance + 1f, 0f, 0f, true) }, 0f, 0f));
            Assert.AreEqual(-1, HintPresentation.ChooseLooker(Array.Empty<LookCandidate>(), 0f, 0f));
            Assert.AreEqual(-1, HintPresentation.ChooseLooker(null, 0f, 0f));
        }

        [Test]
        public void TierThreeIsMovesOnly()
        {
            // Every move the demo can show presses the Jump control (tapped or held); nothing presses Use or moves
            // the stick to a target.
            foreach (var move in HintMoves.Demonstrable)
            {
                var prompt = HintPresentation.PromptFor(move);
                Assert.IsTrue(prompt == ControlPrompt.Jump || prompt == ControlPrompt.HoldJump, move + " → " + prompt);
                Assert.IsNotNull(HintMoves.LineKey(move), move + " has its move line");
            }

            Assert.AreEqual(ControlPrompt.HoldJump, HintPresentation.PromptFor(HintMove.HighJump));
            Assert.AreEqual(ControlPrompt.HoldJump, HintPresentation.PromptFor(HintMove.Glide));
            Assert.AreEqual(ControlPrompt.Jump, HintPresentation.PromptFor(HintMove.Jump));
            Assert.Throws<ArgumentOutOfRangeException>(() => HintPresentation.PromptFor(HintMove.None));
            Assert.IsNull(HintMoves.LineKey(HintMove.None));
        }

        [Test]
        public void AMoveIsOfferedOnlyOnTheSkilledAscentWhileTheHintLeadsUp()
        {
            var a = CaseRouteLayout.SkilledAscent;
            Assert.AreEqual(HintMove.Jump, HintMoves.For(CaseObjective.FindWayIn, a[0].X, a[0].Y, a[0].Z), "by the crate: hop");
            Assert.AreEqual(HintMove.Jump, HintMoves.For(CaseObjective.FindWayIn, a[1].X, a[1].Y, a[1].Z), "on the crate");
            Assert.AreEqual(HintMove.HighJump, HintMoves.For(CaseObjective.MeetJune, a[3].X, a[3].Y, a[3].Z), "on the awning");

            // Everywhere else, and for every other objective, there is no tier 3.
            Assert.AreEqual(HintMove.None, HintMoves.For(CaseObjective.FindWayIn, 0f, 0f, -0.5f), "the plaza");
            Assert.AreEqual(HintMove.None, HintMoves.For(CaseObjective.FindWayIn, -12.45f, 0f, 7.5f), "the ramp foot (walk)");
            foreach (CaseObjective objective in Enum.GetValues(typeof(CaseObjective)))
            {
                if (HintMoves.ClimbsUp(objective))
                {
                    continue;
                }

                foreach (var p in a)
                {
                    Assert.AreEqual(HintMove.None, HintMoves.For(objective, p.X, p.Y, p.Z), $"{objective} at {p}");
                }
            }

            foreach (var spot in HintMoves.Spots)
            {
                Assert.IsFalse(CaseRouteLayout.Inside(spot.Centre.X, spot.Centre.Z), spot.Id + " is outside");
                CollectionAssert.Contains(HintMoves.Demonstrable, spot.Move);
            }
        }
    }
}
