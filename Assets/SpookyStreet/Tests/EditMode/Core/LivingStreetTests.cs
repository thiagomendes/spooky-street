using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Keepsakes;
using SpookyStreet.Core.Localization;
using SpookyStreet.Core.LivingStreet;
using SpookyStreet.Core.Save;
using SpookyStreet.Core.World;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// The living street's test slice (docs/living-street.md §9 phase 1, D33/D34; docs/living-street-slice.md): the east
    /// lane's layout, the crowd's rules (the heart's cap, clue clearance, no glances at hint areas, the bench's view,
    /// stepping aside for Pip, frame-rate independence), the doors, the lantern, the neighbourhood file and the iPhone
    /// budget.
    /// </summary>
    public class LivingStreetTests
    {
        private const string Dir = "/saves";

        // ------------------------------------------------------------------------------------------------- layout

        [Test]
        public void TheDoorsAndTheTableStandOnTheEastLaneClearOfTheCase()
        {
            Assert.AreEqual(3, LivingStreetLayout.Doors.Count);
            foreach (var door in LivingStreetLayout.Doors)
            {
                Assert.IsTrue(LivingStreetLayout.InEastLane(door.StandX, door.StandZ), door + ": stand on the east lane");
                Assert.IsFalse(LivingStreetLayout.InHeart(door.DoorX, door.DoorZ), door + " is not in the case block");
                Assert.Greater(LivingStreetRules.ClueDistance(door.StandX, door.StandZ, out _), 10f);
                StringAssert.StartsWith("street.free.door.", door.StableId, "free-world memory IDs (§6 rule 8)");
                Assert.IsTrue(CollisionRules.InsideWalls(door.StandX, door.StandZ, 0.5f), door + ": Pip can reach its steps");

                // The stand is in front of the door and faces it.
                double yaw = door.StandFacingYaw * Math.PI / 180.0;
                float toDoorX = door.DoorX - door.StandX;
                float toDoorZ = door.DoorZ - door.StandZ;
                Assert.Greater(toDoorX * Math.Sin(yaw) + toDoorZ * Math.Cos(yaw), 1.4, door + ": the stand faces the door");
            }

            Assert.IsTrue(LivingStreetLayout.InEastLane(LivingStreetLayout.TableUseX, LivingStreetLayout.TableUseZ));
            Assert.Greater(LivingStreetRules.ClueDistance(LivingStreetLayout.TableUseX, LivingStreetLayout.TableUseZ, out _), 20f);
            Assert.AreEqual(3, LivingStreetLayout.Doors.Select(d => d.Gag.OpenDegrees).Distinct().Count(),
                "each house answers its own way");
        }

        [Test]
        public void TheHousesFitTheLaneInsideItsHedgesAndTheSky()
        {
            foreach (var house in LivingStreetLayout.Houses)
            {
                EnvironmentLayout.Size(house.Model, out float w, out float d);
                EnvironmentLayout.Footprint(w, d, house.Yaw, out float hx, out float hz);
                Assert.GreaterOrEqual(house.X - hx, EnvironmentLayout.BlockHalfX + 1f, house.Model + " stays east of the heart");
                Assert.LessOrEqual(house.X + hx, LivingStreetLayout.EastHedgeX - 0.4f, house.Model + " stays inside the east hedge");
                Assert.LessOrEqual(house.Z + hz, EnvironmentLayout.HedgeLineZ - 0.4f + 1.5f, house.Model + " north edge");
                Assert.GreaterOrEqual(house.Z - hz, LivingStreetLayout.EastSouthHedgeZ - 1.5f, house.Model + " south edge");
                // The roof's top stays inside the painted sky dome (radius 52 m), so no roof ever pokes through the sky.
                Assert.IsTrue(SkyModel.InsideDome(house.X + Math.Sign(house.X) * hx, 13.5f, house.Z + Math.Sign(house.Z) * hz),
                    house.Model + " inside the sky dome");
            }

            foreach (var hedge in LivingStreetLayout.Hedges)
            {
                Assert.IsTrue(SkyModel.InsideDome(hedge.X + hedge.HalfX, 1.3f, hedge.Z + Math.Sign(hedge.Z) * hedge.HalfZ),
                    $"hedge at ({hedge.X}, {hedge.Z}) inside the sky dome");
            }

            // The lane's walking lines stay clear of every house, prop and the table.
            foreach (var p in LivingStreetLayout.Houses.Concat(LivingStreetLayout.Props))
            {
                for (float x = 22.5f; x < 45f; x += 0.25f)
                {
                    foreach (float z in new[] { LivingStreetLayout.NorthLineZ, LivingStreetLayout.SouthLineZ, 0.4f, -0.4f })
                    {
                        bool inside = Math.Abs(x - p.X) < p.HalfX + 0.3f && Math.Abs(z - p.Z) < p.HalfZ + 0.3f;
                        Assert.IsFalse(inside && p.Model != EnvironmentLayout.NeighbourHouseB,
                            $"{p.Model} at ({p.X}, {p.Z}) stands on a walking line at ({x}, {z})");
                    }
                }
            }
        }

        [Test]
        public void TheEastLaneHedgesCloseItAndTheWallsHoldItsEdges()
        {
            Assert.IsTrue(CollisionRules.InsideWalls(30f, 0f, 0.3f), "the lane is open");
            Assert.IsTrue(CollisionRules.InsideWalls(21.9f, 0f, 0.3f), "the gap is open");
            Assert.IsFalse(CollisionRules.InsideWalls(30f, -15f, 0.3f), "past the lane's south edge");
            Assert.IsFalse(CollisionRules.InsideWalls(46.5f, 0f, 0.3f), "past the lane's east end");
            Assert.IsTrue(CollisionRules.InsideWalls(0f, -16f, 0.3f), "the heart keeps its south");
            Assert.IsFalse(CollisionRules.InsideWalls(23f, -16f, 0.3f), "the heart's south-east corner stays closed");

            // A wall stands on every outer edge of the lane.
            AssertInsideAnyWall(CollisionRules.WallInnerEastX + 0.5f, 0f);
            AssertInsideAnyWall(34f, CollisionRules.WallInnerEastLaneSouthZ - 0.5f);
            AssertInsideAnyWall(34f, CollisionRules.WallInnerHalfZ + 0.5f);
            AssertInsideAnyWall(CollisionRules.WallInnerHalfX + 0.5f, -16f);

            // Hedge modules butt along the lane's edges (north, south, east) with no hole.
            var north = LivingStreetLayout.Hedges.Where(h => Math.Abs(h.Z - EnvironmentLayout.HedgeLineZ) < 1e-3f).Select(h => h.X).OrderBy(x => x).ToList();
            var south = LivingStreetLayout.Hedges.Where(h => Math.Abs(h.Z - LivingStreetLayout.EastSouthHedgeZ) < 1e-3f).Select(h => h.X).OrderBy(x => x).ToList();
            var east = LivingStreetLayout.Hedges.Where(h => h.X >= LivingStreetLayout.EastHedgeX - 1e-3f).Select(h => h.Z).OrderBy(z => z).ToList();
            AssertRun(north, EnvironmentLayout.BlockHalfX, LivingStreetLayout.EastEndX - 0.5f, "north");
            AssertRun(south, EnvironmentLayout.BlockHalfX, LivingStreetLayout.EastEndX - 0.5f, "south");
            AssertRun(east, LivingStreetLayout.EastSouthZ, EnvironmentLayout.BlockHalfZ, "east");

            // The gap stays the only way between the heart and the lane at ground level, and the heart's north stays closed.
            Assert.AreEqual(LivingStreetLayout.GapMinZ, EnvironmentLayout.EastGapMinZ);
        }

        private static void AssertRun(List<float> centres, float from, float to, string edge)
        {
            float reach = from;
            foreach (float c in centres)
            {
                Assert.LessOrEqual(c - EnvironmentLayout.HedgeLength * 0.5f, reach + 1e-3f, $"gap in the lane's {edge} hedge before {c}");
                reach = Math.Max(reach, c + EnvironmentLayout.HedgeLength * 0.5f);
            }

            Assert.GreaterOrEqual(reach, to - 1e-3f, $"the lane's {edge} hedge stops short");
        }

        private static void AssertInsideAnyWall(float x, float z)
        {
            foreach (var wall in CollisionRules.BoundaryWalls)
            {
                if (Math.Abs(x - wall.AX) < wall.BX * 0.5f && Math.Abs(z - wall.AZ) < wall.BZ * 0.5f)
                {
                    Assert.GreaterOrEqual(wall.Top, 14f - 1e-3f, wall.Name);
                    return;
                }
            }

            Assert.Fail($"({x}, {z}) is open");
        }

        // ---------------------------------------------------------------------------------------- crowd: routes

        /// <summary>Every walker position a loop can produce: both laps, every member's lateral slot, stepping aside.</summary>
        private static IEnumerable<(float X, float Z, string Where)> RouteSamples()
        {
            foreach (var route in CrowdRoutes.All)
            {
                foreach (bool porch in new[] { false, true })
                {
                    var lap = route.Lap(porch);
                    for (int i = 0; i < lap.Length - 1; i++)
                    {
                        var a = lap[i];
                        var b = lap[i + 1];
                        float len = (float)Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z));
                        if (len < 1e-4f)
                        {
                            continue;
                        }

                        float hx = (b.X - a.X) / len;
                        float hz = (b.Z - a.Z) / len;
                        for (float t = 0f; t <= len; t += 0.1f)
                        {
                            for (float lateral = -1.0f; lateral <= 1.0f; lateral += 0.25f)
                            {
                                yield return (a.X + hx * t + hz * lateral, a.Z + hz * t - hx * lateral,
                                    $"{route.Id} {a} → {b}");
                            }
                        }
                    }
                }
            }
        }

        [Test]
        public void SixToEightWalkersInGroupsWithAParentAndStrollers()
        {
            Assert.That(CrowdRoutes.WalkerCount, Is.InRange(6, 8));
            int groupsWithParent = CrowdRoutes.All.Count(r => r.PorchExcursion != null &&
                                                             r.Members.Count(m => m.Look.Body == CrowdBodyKind.GrownUp) == 1 &&
                                                             r.Members.Count(m => m.Look.Body == CrowdBodyKind.SmallChild) >= 2);
            Assert.GreaterOrEqual(groupsWithParent, 2, "trick-or-treat groups: children with one grown-up");
            Assert.IsTrue(CrowdRoutes.All.Any(r => r.PorchExcursion == null && r.Members.Any(m => m.Look.Body == CrowdBodyKind.GrownUp)),
                "strollers");
            foreach (var route in CrowdRoutes.All)
            {
                Assert.AreEqual(route.Plain[0].X, route.Plain[route.Plain.Length - 1].X, 1e-4f, route.Id + " closes its loop");
                Assert.AreEqual(route.Plain[0].Z, route.Plain[route.Plain.Length - 1].Z, 1e-4f, route.Id + " closes its loop");
                foreach (var member in route.Members)
                {
                    Assert.IsTrue(CrowdLooks.IsAllowed(member.Look.Recipe), route.Id + ": " + member.Look + " is a reserved look");
                }
            }

            // Each group visits every door; the doors are rung, never knocked (§3.4 rule 5).
            foreach (var route in CrowdRoutes.All.Where(r => r.PorchExcursion != null))
            {
                CollectionAssert.AreEquivalent(LivingStreetLayout.Doors.Select(d => d.Id),
                    route.Lap(false).Where(n => n.Kind == RouteNodeKind.Door).Select(n => n.DoorId));
                Assert.AreEqual(1, route.Lap(true).Count(n => n.Kind == RouteNodeKind.PipPorch));
            }
        }

        [Test]
        public void ReservedLooksNeverJoinTheCrowd()
        {
            foreach (string reserved in CrowdLooks.Reserved)
            {
                Assert.IsFalse(CrowdLooks.IsAllowed(reserved), reserved);
                Assert.IsFalse(CrowdLooks.Costumes.Any(c => c.Contains(reserved)), reserved + " in the kit's themes");
            }

            Assert.IsFalse(CrowdLooks.IsAllowed("owl_witch"), "Mrs. Ortega's owl motif waits for the stamp");
            Assert.IsFalse(CrowdLooks.IsAllowed(null));
            Assert.IsFalse(CrowdLooks.IsAllowed("NoSuchRecipe"), "only the kit's recipes");
            foreach (string id in CrowdLooks.Costumes)
            {
                Assert.IsTrue(CrowdLooks.IsAllowed(id), id);
            }
        }

        [Test]
        public void TheWalkersAreKitRecipesAtTheirClipsPace()
        {
            var used = CrowdRoutes.All.SelectMany(r => r.Members).Select(m => m.Look.Recipe).ToList();
            Assert.AreEqual(used.Count, used.Distinct().Count(), "every walker wears its own recipe");
            foreach (string id in used)
            {
                Assert.IsNotNull(SpookyStreet.Core.Crowd.CrowdKit.Find(id), id + " is a crowd kit recipe");
            }

            // Feet never slide: groups and strollers move at the Walk clip's own ground speed.
            Assert.AreEqual(SpookyStreet.Core.Crowd.CrowdKit.WalkSpeed(SpookyStreet.Core.Crowd.CrowdKit.ChildModel), CrowdRoutes.GroupSpeed, 1e-4f);
            Assert.AreEqual(SpookyStreet.Core.Crowd.CrowdKit.WalkSpeed(SpookyStreet.Core.Crowd.CrowdKit.AdultModel), CrowdRoutes.StrollSpeed, 1e-4f);
        }

        [Test]
        public void TheNeighboursAreBuiltFromTheKitsGrownUpParts()
        {
            foreach (var door in LivingStreetLayout.Doors)
            {
                var ids = LivingStreetLayout.NeighboursAt(door);
                Assert.AreEqual(door.Gag.Figures, ids.Count, door.Id + ": one figure per neighbour in the doorway");
                foreach (string id in ids)
                {
                    var recipe = LivingStreetLayout.NeighbourRecipe(id);
                    Assert.IsNotNull(recipe, id);
                    Assert.IsTrue(recipe.IsGrownUp);
                    var slots = new HashSet<string>();
                    foreach (string part in recipe.Parts)
                    {
                        string slot = SpookyStreet.Core.Crowd.CrowdKit.SlotOf(recipe.Model, part);
                        Assert.IsNotNull(slot, id + ": " + part + " is in the grown-up body");
                        Assert.IsTrue(slots.Add(slot), id + ": one part per slot (" + slot + ")");
                    }

                    Assert.IsTrue(CrowdLooks.IsAllowed(id), id);
                }
            }
        }

        [Test]
        public void NoWalkerComesWithinOneAndAHalfMetresOfAClue()
        {
            float worst = float.MaxValue;
            string where = null, what = null;
            foreach (var s in RouteSamples())
            {
                float d = LivingStreetRules.ClueDistance(s.X, s.Z, out string nearest);
                if (d < worst)
                {
                    worst = d;
                    where = $"({s.X:F2}, {s.Z:F2}) on {s.Where}";
                    what = nearest;
                }
            }

            TestContext.WriteLine($"closest a walker comes to a case target: {worst:F2} m ({what}) at {where}");
            Assert.GreaterOrEqual(worst, LivingStreetRules.ClueClearance, $"{what} at {where}");
        }

        [Test]
        public void WalkersStayOutOfTheSideYardTheBenchsViewAndTheHedges()
        {
            foreach (var s in RouteSamples())
            {
                Assert.IsTrue(CollisionRules.InsideWalls(s.X, s.Z, 0.3f), $"({s.X:F2}, {s.Z:F2}) on {s.Where} leaves the street");
                Assert.IsFalse(PaperBatLayout.InHiddenRegion(s.X, s.Z), $"({s.X:F2}, {s.Z:F2}) on {s.Where} is in the side yard");
                if (LivingStreetLayout.InHeart(s.X, s.Z))
                {
                    Assert.Greater(LivingStreetRules.SightlineDistance(s.X, s.Z), LivingStreetRules.SightlineClearance,
                        $"({s.X:F2}, {s.Z:F2}) on {s.Where} stands in the bench's view of the door");
                    if (s.X < 18f)
                    {
                        Assert.Less(s.Z, EnvironmentLayout.SidewalkFrontZ - 2f, $"({s.X:F2}, {s.Z:F2}) on {s.Where}: heart legs stay south of the plaza");
                    }
                }
                else if (s.X > EnvironmentLayout.BlockHalfX - 1f && s.X < EnvironmentLayout.BlockHalfX + 1f)
                {
                    Assert.That(s.Z, Is.InRange(LivingStreetLayout.GapMinZ + 0.3f, LivingStreetLayout.GapMaxZ - 0.3f),
                        $"({s.X:F2}, {s.Z:F2}) on {s.Where} crosses the hedge outside the gap");
                }
            }
        }

        [Test]
        public void NoStopFacesAHintAreaAndNoHeartLegHeadsIntoOne()
        {
            foreach (var route in CrowdRoutes.All)
            {
                foreach (bool porch in new[] { false, true })
                {
                    var lap = route.Lap(porch);
                    for (int i = 0; i < lap.Length; i++)
                    {
                        var node = lap[i];
                        if (node.IsStop)
                        {
                            bool meets = LivingStreetRules.GazeMeetsHintArea(node.X, node.Z, node.FaceYaw, LivingStreetRules.GlanceReach,
                                out string area);
                            // Pip's own porch is the one stop inside a hint area; groups go there only while the radio's
                            // area is elsewhere (LivingStreetRules.PorchOpen), so no stop ever faces the area a hint shows.
                            if (node.Kind == RouteNodeKind.PipPorch)
                            {
                                Assert.AreEqual(HintAreas.ArrivalPorch, area, "Pip's porch stop faces her own porch");
                                Assert.IsFalse(LivingStreetRules.PorchOpen(true, HintAreas.ArrivalPorch));
                                Assert.IsTrue(LivingStreetRules.PorchOpen(true, HintAreas.FrontYard));
                                Assert.IsFalse(LivingStreetRules.PorchOpen(false, HintAreas.FrontYard));
                                continue;
                            }

                            Assert.IsFalse(meets, $"{route.Id}: the stop at {node} faces the {area} hint area");
                        }

                        if (i + 1 < lap.Length && LivingStreetLayout.InHeart(node.X, node.Z) && lap[i + 1].Kind != RouteNodeKind.PipPorch)
                        {
                            var next = lap[i + 1];
                            float yaw = (float)(Math.Atan2(next.X - node.X, next.Z - node.Z) * 180.0 / Math.PI);
                            if (Math.Abs(next.X - node.X) + Math.Abs(next.Z - node.Z) < 1e-3f)
                            {
                                continue;
                            }

                            Assert.IsFalse(LivingStreetRules.GazeMeetsHintArea(node.X, node.Z, yaw, LivingStreetRules.WalkGlanceReach, out string area),
                                $"{route.Id}: walking from {node} heads into the {area} hint area");
                        }
                    }
                }
            }
        }

        // ------------------------------------------------------------------------------------------ crowd: clock

        private static CrowdInputs Inputs(float pipX = 0f, float pipZ = -40f, bool caseOpen = true, bool porch = false) =>
            new CrowdInputs { PipX = pipX, PipZ = pipZ, PipOutside = true, CaseOpen = caseOpen, PorchOpen = porch };

        [Test]
        public void TheHeartNeverHoldsMoreThanThreeWalkersWhileTheCaseIsOpen()
        {
            var crowd = new CrowdClock();
            var random = new Random(34);
            int most = 0;
            int heartVisits = 0;
            bool wasInside = false;
            float pipX = 0f, pipZ = -12f;
            for (int frame = 0; frame < 60 * 60 * 20; frame++)
            {
                if (frame % 120 == 0)
                {
                    pipX = (float)(random.NextDouble() * 60 - 18);
                    pipZ = (float)(random.NextDouble() * 24 - 12);
                }

                crowd.Advance(1f / 60f, Inputs(pipX, pipZ, true, porch: true));
                int inHeart = crowd.WalkersInHeart;
                most = Math.Max(most, inHeart);
                Assert.LessOrEqual(inHeart, CrowdClock.CaseHeartCap, $"frame {frame}: {inHeart} walkers in the heart");
                Assert.LessOrEqual(crowd.HeartCommitted, CrowdClock.CaseHeartCap);
                if (inHeart > 0 && !wasInside)
                {
                    heartVisits++;
                }

                wasInside = inHeart > 0;
            }

            TestContext.WriteLine($"most walkers in the heart: {most}; visits {heartVisits}");
            Assert.AreEqual(CrowdClock.CaseHeartCap, most, "the heart sees a group (it is not left empty)");
            Assert.Greater(heartVisits, 4, "groups and strollers keep coming through");
        }

        [Test]
        public void TheClockIsTheSameAtThirtyAndSixtyFramesPerSecond()
        {
            var a = new CrowdClock();
            var b = new CrowdClock();
            for (int i = 0; i < 30 * 600; i++)
            {
                a.Advance(1f / 30f, Inputs());
                b.Advance(1f / 60f, Inputs());
                b.Advance(1f / 60f, Inputs());
            }

            for (int w = 0; w < a.WalkerCount; w++)
            {
                Assert.AreEqual(a.Poses[w].X, b.Poses[w].X, 0.05f, "walker " + w);
                Assert.AreEqual(a.Poses[w].Z, b.Poses[w].Z, 0.05f, "walker " + w);
            }

            // Frozen (a cutscene) and paused (dt 0) change nothing.
            var before = a.Poses.Select(p => (p.X, p.Z)).ToArray();
            var frozen = Inputs();
            frozen.Frozen = true;
            a.Advance(1f, frozen);
            a.Advance(0f, Inputs());
            CollectionAssert.AreEqual(before, a.Poses.Select(p => (p.X, p.Z)).ToArray());
        }

        [Test]
        public void GroupsRingEveryDoorAndOnlyRingPipsBellWhileHerPorchIsOpen()
        {
            var crowd = new CrowdClock();
            var events = new List<CrowdEvent>();
            for (int i = 0; i < 60 * 60 * 6; i++)
            {
                crowd.Advance(1f / 60f, Inputs(), events);
            }

            CollectionAssert.IsSubsetOf(LivingStreetLayout.Doors.Select(d => d.Id).ToList(),
                events.Where(e => e.Kind == CrowdEventKind.DoorRung).Select(e => e.DoorId).Distinct().ToList());
            Assert.IsFalse(events.Any(e => e.Kind == CrowdEventKind.PorchRung), "no lantern, no visit");

            events.Clear();
            bool served = false;
            for (int i = 0; i < 60 * 60 * 6 && !served; i++)
            {
                // Pip waits on her porch with the lantern lit.
                crowd.Advance(1f / 60f, Inputs(0.2f, -13.6f, porch: true), events);
                int waiting = crowd.WaitingAtPorch;
                if (waiting >= 0)
                {
                    Assert.IsTrue(LivingStreetLayout.TreatInReach(0.2f, -13.6f), "Pip on her deck can hand out a treat");
                    Assert.IsTrue(crowd.Serve(waiting));
                    Assert.IsFalse(crowd.Serve(waiting), "one treat per visit");
                    served = true;
                }
            }

            Assert.IsTrue(served, "a group came to Pip's bell");
            Assert.IsTrue(events.Any(e => e.Kind == CrowdEventKind.PorchRung));
            // Each child in turn steps up to Pip, gets one treat in its pail, cheers and steps back.
            for (int i = 0; i < 60 * 15 && !events.Any(e => e.Kind == CrowdEventKind.PorchLeft); i++)
            {
                crowd.Advance(1f / 60f, Inputs(0.2f, -13.6f, porch: true), events);
            }

            Assert.IsTrue(events.Any(e => e.Kind == CrowdEventKind.PorchLeft), "the group cheers and moves on");
            var treats = events.Where(e => e.Kind == CrowdEventKind.PipTreatReleased).Select(e => e.Walker).ToList();
            int group = events.First(e => e.Kind == CrowdEventKind.PorchLeft).Group;
            Assert.AreEqual(crowd.ChildrenOf(group), treats.Count, "one treat for each child");
            CollectionAssert.AllItemsAreUnique(treats);
            Assert.IsFalse(treats.Any(crowd.IsGrownUp), "the grown-up gets none");
            Assert.AreEqual(-1, crowd.WaitingAtPorch);
        }

        [Test]
        public void AGroupWaitsAtPipsStepsOnlyALittleWhenSheIsAway()
        {
            var crowd = new CrowdClock();
            var events = new List<CrowdEvent>();
            float rang = -1f;
            float left = -1f;
            for (int i = 0; i < 60 * 60 * 8 && left < 0f; i++)
            {
                events.Clear();
                crowd.Advance(1f / 60f, Inputs(40f, 0f, porch: true), events);
                if (rang < 0f && events.Any(e => e.Kind == CrowdEventKind.PorchRung))
                {
                    rang = i / 60f;
                }

                if (rang >= 0f && events.Any(e => e.Kind == CrowdEventKind.PorchLeft))
                {
                    left = i / 60f;
                }
            }

            Assert.GreaterOrEqual(rang, 0f, "a group rang");
            Assert.AreEqual(CrowdClock.PorchBaseWait, left - rang, 0.1f, "no pressure: a short wait, then they move on");
        }

        [Test]
        public void WalkersStepAsideForPipAndNeverStopForGood()
        {
            // Group A's family on a long, plain walk along the lane (no door on the way), so the test sees one long leg.
            var a = CrowdRoutes.All[0];
            var lane = new[]
            {
                new RouteNode(26f, 0.2f), new RouteNode(43f, 0.2f), new RouteNode(43f, -0.9f), new RouteNode(26f, -0.9f),
                new RouteNode(26f, 0.2f),
            };
            var crowd = new CrowdClock(new[] { new CrowdRoute("lane", a.Speed, lane, null, a.Members, 2f) });
            // Let it walk to the middle of the leg, then stand Pip a metre in front of its first child.
            for (int i = 0; i < 60 * 3; i++)
            {
                crowd.Advance(1f / 60f, Inputs());
            }

            var lead = crowd.Poses[0];
            double yaw = lead.Yaw * Math.PI / 180.0;
            float pipX = lead.X + (float)Math.Sin(yaw) * 0.9f;
            float pipZ = lead.Z + (float)Math.Cos(yaw) * 0.9f;
            float start = crowd.LapMetres(0);
            crowd.Advance(1f / 60f, Inputs(pipX, pipZ));
            Assert.Less(crowd.Poses[0].Speed, CrowdRoutes.GroupSpeed * 0.8f, "the group slows for Pip");
            float sidestep = 0f;
            for (int i = 0; i < 60 * 12; i++)
            {
                crowd.Advance(1f / 60f, Inputs(pipX, pipZ));
                // How far the lead child stands off its walking line (the group steps aside, away from Pip).
                sidestep = Math.Max(sidestep, Math.Abs(crowd.Poses[0].Z - lead.Z));
            }

            Assert.Greater(sidestep, 0.3f, "the group steps aside");

            Assert.Greater(crowd.LapMetres(0), start + 2.5f, "the group passes Pip instead of stopping for good");
        }

        // ------------------------------------------------------------------------------------------------- doors

        [Test]
        public void ADoorOpensOnTheThirdRingWithItsGagThenShuts()
        {
            var doors = new NeighbourDoors();
            var fogg = LivingStreetLayout.Door(LivingStreetLayout.Fogg);
            var first = doors.Ring(fogg.Id, Ringer.Pip, 0);
            Assert.IsNotNull(first);
            Assert.AreEqual(1, first.Ring);
            Assert.IsFalse(first.Opens);
            Assert.IsNull(doors.Ring(fogg.Id, Ringer.Group), "a door Pip rang ignores another visitor");
            Assert.IsNull(doors.Ring(fogg.Id, Ringer.Pip, 0), "the next ring waits until the bell has sounded");
            Assert.AreEqual(DoorPhase.Called, doors.Phase(fogg.Id));
            for (int i = 0; i < 60 * 2; i++)
            {
                doors.Advance(1f / 60f);
            }

            Assert.AreEqual(0f, doors.SwingDegrees(fogg.Id), "still shut after two seconds and one ring: no timer opens it");
            Assert.AreEqual(2, doors.Ring(fogg.Id, Ringer.Pip, 0).Ring, "the second ring: someone is coming");
            for (int i = 0; i < 60 * 2; i++)
            {
                doors.Advance(1f / 60f);
            }

            var third = doors.Ring(fogg.Id, Ringer.Pip, 0);
            Assert.IsTrue(third.Opens);
            Assert.AreEqual(LocalizationKeys.LivingStreet.FoggFirst, third.LineKey);
            Assert.IsTrue(third.TreatForPip);

            // Pip waits at his steps: he comes out, gives her one treat, goes back in and the door shuts.
            float peak = 0f;
            float t = 0f;
            int released = 0;
            var events = new List<DoorEvent>();
            while (doors.Busy(fogg.Id) && t < 30f)
            {
                events.Clear();
                doors.Advance(1f / 60f, fogg.StandX, fogg.StandZ, events);
                t += 1f / 60f;
                peak = Math.Max(peak, doors.SwingDegrees(fogg.Id));
                released += events.Count(e => e.Kind == DoorEventKind.Released && e.Recipient == -1);
            }

            Assert.Greater(peak, fogg.Gag.OpenDegrees, "Mr. Fogg swings the door past wide open (his springy gag)");
            Assert.AreEqual(1, released, "one treat for Pip");
            Assert.AreEqual(DoorPhase.Closed, doors.Phase(fogg.Id));
            Assert.AreEqual(0f, doors.SwingDegrees(fogg.Id));

            // Pearl's door eases open without overshoot; the Abbotts are two in the doorway.
            var pearl = LivingStreetLayout.Door(LivingStreetLayout.Pearl);
            Assert.AreEqual(0f, pearl.Gag.Bounce);
            Assert.Greater(pearl.Gag.LampBoost, 0f);
            Assert.AreEqual(2, LivingStreetLayout.Door(LivingStreetLayout.Abbotts).Gag.Figures);
            Assert.IsNull(doors.Ring("nobody", Ringer.Pip));
        }

        [Test]
        public void PipsVisitsPickFirstAgainAndThirdLines()
        {
            foreach (var door in LivingStreetLayout.Doors)
            {
                Assert.AreEqual(door.FirstLine, door.LineFor(0));
                Assert.AreEqual(door.AgainLine, door.LineFor(1));
                Assert.AreEqual(door.ThirdLine, door.LineFor(2));
                Assert.AreEqual(door.ThirdLine, door.LineFor(9));
                Assert.AreEqual(3, new[] { door.FirstLine, door.AgainLine, door.ThirdLine }.Distinct().Count());
                var doors = new NeighbourDoors();
                Assert.IsNull(doors.Ring(door.Id, Ringer.Group, 0, 2, 0).LineKey, "groups get the bell and the cheer, no subtitle");
            }

            var state = NeighbourhoodState.Empty;
            for (int i = 0; i < 5; i++)
            {
                state = state.WithVisit(LivingStreetLayout.Pearl);
            }

            Assert.AreEqual(NeighbourhoodState.MaxVisits, state.Visits(LivingStreetLayout.Pearl), "visits are capped, never a tally");
            Assert.AreEqual(1, state.NeighboursMet);
            Assert.AreSame(state, state.WithVisit("nobody"));
        }

        // ------------------------------------------------------------------------------------- lantern and save

        [Test]
        public void TheLanternUnlocksPipsBellAndKeepsItsShapeAndColour()
        {
            var state = NeighbourhoodState.Empty;
            Assert.IsFalse(state.LanternLit);
            Assert.IsFalse(LivingStreetRules.PorchOpen(state.LanternLit, null));
            state = state.WithLantern(new PipLantern(LanternShape.Bat, LanternColour.Plum));
            Assert.IsTrue(state.LanternLit);
            Assert.IsTrue(LivingStreetRules.PorchOpen(state.LanternLit, null));
            Assert.AreEqual(4, LanternCraft.Shapes.Count);
            Assert.AreEqual(4, LanternCraft.Colours.Count);
            CollectionAssert.AreEqual(new[] { "bat", "cat", "ghost", "leaf" }, LanternCraft.Shapes.Select(LanternCraft.ShapeId).ToArray(),
                "D35: bat, cat, ghost and leaf");
            foreach (var shape in LanternCraft.Shapes)
            {
                CollectionAssert.DoesNotContain(LanternCraft.ReservedShapes, LanternCraft.ShapeId(shape), "never moon, star or pumpkin");
                Assert.IsTrue(LanternCraft.TryShape(LanternCraft.ShapeId(shape), out var back));
                Assert.AreEqual(shape, back);
                CollectionAssert.Contains(LocalizationKeys.All, LanternCraft.ShapeKey(shape));
            }

            foreach (var colour in LanternCraft.Colours)
            {
                Assert.IsTrue(LanternCraft.TryColour(LanternCraft.ColourId(colour), out var back));
                Assert.AreEqual(colour, back);
                CollectionAssert.Contains(LocalizationKeys.All, LanternCraft.ColourKey(colour));
                // Never the case's silver cloth or lavender foil (§3.4 rule 6).
                Assert.AreNotEqual(0xDAE2EE, LanternCraft.ColourRgb(colour));
            }
        }

        [Test]
        public void TheNeighbourhoodFileRoundTripsAndDropsUnknownIds()
        {
            var state = NeighbourhoodState.Empty.WithLantern(new PipLantern(LanternShape.Ghost, LanternColour.Green))
                .WithVisit(LivingStreetLayout.Fogg).WithVisit(LivingStreetLayout.Fogg).WithVisit(LivingStreetLayout.Abbotts);
            string json = NeighbourhoodCodec.Serialize(state);
            StringAssert.Contains("street.free.door.fogg", json);
            Assert.IsTrue(NeighbourhoodCodec.TryRead(json, out var back, out _));
            Assert.AreEqual(state, back);
            Assert.AreEqual(2, back.Visits(LivingStreetLayout.Fogg));

            Assert.IsTrue(NeighbourhoodCodec.TryRead(
                "{\"schemaVersion\": 1, \"lanternShape\": \"owl\", \"lanternColour\": \"plum\", \"doorVisits\": [\"street.free.door.ghost\", \"street.free.door.pearl\"]}",
                out var odd, out _));
            Assert.IsFalse(odd.LanternLit, "an unknown shape is not a lantern");
            Assert.AreEqual(1, odd.Visits(LivingStreetLayout.Pearl));
            Assert.IsFalse(NeighbourhoodCodec.TryRead("{\"schemaVersion\": 9, \"doorVisits\": []}", out _, out bool newer));
            Assert.IsTrue(newer);
            Assert.AreEqual(SaveCheck.Invalid, NeighbourhoodCodec.Check("not json"));
        }

        [Test]
        public void ReplayKeepsTheNeighbourhoodAndNewGameClearsIt()
        {
            var fs = new MemorySaveFileSystem();
            var store = new NeighbourhoodStore(fs, Dir);
            store.Load();
            Assert.IsTrue(store.MakeLantern(new PipLantern(LanternShape.Leaf, LanternColour.Yellow)));
            Assert.IsTrue(store.Visit(LivingStreetLayout.Pearl));
            Assert.IsFalse(store.MakeLantern(new PipLantern(LanternShape.Leaf, LanternColour.Yellow)), "no change, no write");
            Assert.IsTrue(fs.Files.Keys.Any(k => k.Contains("neighbourhood")), "a separate file (§10)");
            Assert.IsFalse(fs.Files.Keys.Any(k => k.Contains("neighbourhood") && k.Contains("case")));

            var reloaded = new NeighbourhoodStore(fs, Dir);
            Assert.AreEqual(store.State, reloaded.Load());

            Assert.AreEqual(store.State, reloaded.Replay(), "Replay case keeps the free world (D31)");
            reloaded.Reset();
            Assert.IsTrue(reloaded.State.IsEmpty, "New game clears it (D31)");
            Assert.IsTrue(new NeighbourhoodStore(fs, Dir).Load().IsEmpty, "and the cleared file is what loads next");
        }

        [Test]
        public void ANewerNeighbourhoodFileIsKeptUntilNewGame()
        {
            var fs = new MemorySaveFileSystem();
            var paths = NeighbourhoodStore.PathsFor(Dir);
            fs.Files[paths.Latest] = "{\"schemaVersion\": 7, \"doorVisits\": []}";
            var store = new NeighbourhoodStore(fs, Dir);
            Assert.IsTrue(store.Load().IsEmpty);
            Assert.IsTrue(store.WritesBlocked);
            store.Visit(LivingStreetLayout.Fogg);
            Assert.AreEqual("{\"schemaVersion\": 7, \"doorVisits\": []}", fs.Files[paths.Latest], "never written over");
            store.Reset();
            Assert.IsFalse(store.WritesBlocked);
        }

        // ------------------------------------------------------------------------------------------------ budget

        [Test]
        public void TheBudgetAnimatesTheNearestWalkersPerTier()
        {
            var distances = new[] { 3f, 25f, 6f, 14f, 9f, 2f, 30f, 11f };
            var view = new[] { true, true, true, true, true, false, true, true };
            var result = new WalkerDetail[8];
            var order = new int[8];
            CrowdBudget.For(CrowdTier.High).Decide(distances, view, result, order);
            Assert.AreEqual(WalkerDetail.Full, result[0]);
            Assert.AreEqual(WalkerDetail.Hidden, result[5], "off screen: not drawn or animated");
            Assert.AreEqual(WalkerDetail.Reduced, result[3], "beyond 12 m: 15 Hz");
            Assert.AreEqual(WalkerDetail.Standee, result[1], "beyond 18 m: a paper standee");

            CrowdBudget.For(CrowdTier.Low).Decide(distances, view, result, order);
            int animated = result.Count(r => r == WalkerDetail.Full || r == WalkerDetail.Reduced);
            Assert.LessOrEqual(animated, CrowdBudget.For(CrowdTier.Low).AnimatedInView);
            Assert.AreEqual(WalkerDetail.Standee, result[3], "the low tier keeps far walkers as standees");

            Assert.AreEqual(8, CrowdBudget.For(CrowdTier.High).AnimatedInView);
            Assert.AreEqual(6, CrowdBudget.For(CrowdTier.Medium).AnimatedInView);
            Assert.AreEqual(4, CrowdBudget.For(CrowdTier.Low).AnimatedInView);
            Assert.AreEqual(CrowdTier.Low, CrowdBudget.TierForQualityLevel(0));
            Assert.AreEqual(CrowdTier.High, CrowdBudget.TierForQualityLevel(3));
            Assert.LessOrEqual(CrowdRoutes.WalkerCount, CrowdBudget.For(CrowdTier.Low).AlivePerSector);

            // 15 Hz: about one reduced update per four frames at 60 fps.
            int ticks = 0;
            for (int f = 1; f <= 600; f++)
            {
                ticks += CrowdBudget.ReducedTick(f / 60.0, (f - 1) / 60.0, 3) ? 1 : 0;
            }

            Assert.AreEqual(150, ticks, 1);
        }

        [Test]
        public void TheCrowdClockFitsItsCpuBudget()
        {
            // The logic half of the crowd's CPU (the device readout measures the whole). Generous for a CI runner.
            var crowd = new CrowdClock();
            var inputs = Inputs(10f, -5f, porch: true);
            for (int i = 0; i < 600; i++)
            {
                crowd.Advance(1f / 60f, inputs);
            }

            var watch = System.Diagnostics.Stopwatch.StartNew();
            const int frames = 20000;
            for (int i = 0; i < frames; i++)
            {
                crowd.Advance(1f / 60f, inputs);
            }

            double ms = watch.Elapsed.TotalMilliseconds / frames;
            TestContext.WriteLine($"crowd clock: {ms * 1000.0:F1} µs per frame for {crowd.WalkerCount} walkers");
            Assert.Less(ms, CrowdBudget.For(CrowdTier.Low).CpuMs * 0.25, "the clock uses a small part of the low tier's budget");
        }

        [Test]
        public void FrameStatsGiveThePercentilesAndTheReadoutStaysOffTheChildsScreen()
        {
            var stats = new FrameTimeStats();
            Assert.IsFalse(stats.Ready);
            for (int i = 0; i < 2000; i++)
            {
                stats.Record(i % 50 == 0 ? 40f : i % 10 == 0 ? 20f : 16.7f, 0.2f);
            }

            stats.Record(900f, 0.2f); // a hitch from loading is not play
            Assert.IsTrue(stats.Ready);
            Assert.AreEqual(16.7f, stats.Median, 1e-3f);
            Assert.AreEqual(20f, stats.P95, 1e-3f);
            Assert.AreEqual(40f, stats.P99, 1e-3f);
            Assert.IsFalse(stats.MeetsTargets);
            Assert.AreEqual(0.2f, stats.CrowdMs, 1e-4f);
            stats.Reset();
            Assert.AreEqual(0, stats.Count);

            Assert.IsTrue(FrameReadoutPolicy.Shown(true, false, false));
            Assert.IsTrue(FrameReadoutPolicy.Shown(false, true, false), "TestFlight builds");
            Assert.IsFalse(FrameReadoutPolicy.Shown(false, false, false));
            Assert.IsFalse(FrameReadoutPolicy.Shown(true, true, true), "never in a store release");
        }

        [Test]
        public void EveryNewStringIsAKeyAndNoneCountsTreatsForTheChild()
        {
            var english = StringTable.Parse(LocalizationTests.BundledEnglishJson());
            foreach (string key in LocalizationKeys.LivingStreet.All)
            {
                Assert.IsTrue(english.TryGetEntry(key, out var entry), key);
                if (key.StartsWith("line.", StringComparison.Ordinal) || key.StartsWith("ui.lantern.", StringComparison.Ordinal) ||
                    key.StartsWith("ui.verb.", StringComparison.Ordinal))
                {
                    Assert.IsFalse(entry.Value.Any(char.IsDigit), key + ": no numbers in the child's flow");
                    StringAssert.DoesNotContain("{0}", entry.Value, key);
                    foreach (string word in new[] { "knock", "june", "owl", "blue house", "attic" })
                    {
                        StringAssert.DoesNotContain(word, entry.Value.ToLowerInvariant(), key + ": nobody talks about the case");
                    }
                }
            }
        }
    }
}
