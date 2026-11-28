using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpookyStreet.Core.Crowd;
using SpookyStreet.Core.LivingStreet;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// Trick-or-treating on the living street (owner, 2026-11-28): the door opens on the third ring with signs between
    /// rings, eases open on a hinge curve, the neighbour hands one treat to each child in turn (and to Pip), a door never
    /// gathers a mob (one group visits, one waits a few metres back, others walk on), families stand in an arc and walk
    /// together with planted feet on paths that keep clear of hedges and walls, every leg leads to a goal, and walkers
    /// give way so Pip is never trapped.
    /// </summary>
    public class TrickOrTreatTests
    {
        private const float Dt = 1f / 60f;

        private static NeighbourDoor Fogg => LivingStreetLayout.Door(LivingStreetLayout.Fogg);

        private static CrowdInputs Away(bool porch = false, bool caseOpen = true) =>
            new CrowdInputs { PipX = -12f, PipZ = -8f, PipOutside = true, CaseOpen = caseOpen, PorchOpen = porch };

        private static void Run(NeighbourDoors doors, float seconds, float pipX = 0f, float pipZ = -1000f, List<DoorEvent> events = null)
        {
            for (float t = 0f; t < seconds; t += Dt)
            {
                doors.Advance(Dt, pipX, pipZ, events);
            }
        }

        // ------------------------------------------------------------------------------------------ the doorbell

        [Test]
        public void BetweenRingsTheHouseShowsSomeoneIsComingAndNothingIsTimedAgainstTheChild()
        {
            var doors = new NeighbourDoors();
            var door = Fogg;
            Assert.AreEqual(0f, doors.HallLight(door.Id));
            Assert.IsTrue(doors.CanRing(door.Id, Ringer.Pip));
            var events = new List<DoorEvent>();
            Assert.AreEqual(1, doors.Ring(door.Id, Ringer.Pip, 0, 1, -1, events).Ring);
            Assert.IsFalse(doors.CanRing(door.Id, Ringer.Pip), "the bell sounds before the next ring is taken");
            Run(doors, 0.5f);
            Assert.AreEqual(1f, doors.HallLight(door.Id), 1e-4f, "the first ring turns the hall light on");
            Assert.AreEqual(-1f, doors.Silhouette(door.Id), "nobody crosses the window yet");

            // A long wait after one ring changes nothing: the door neither opens nor gives up while Pip stays.
            Run(doors, 60f, door.StandX, door.StandZ);
            Assert.AreEqual(DoorPhase.Called, doors.Phase(door.Id));
            Assert.AreEqual(0f, doors.SwingDegrees(door.Id));
            Assert.IsFalse(doors.NeighbourShown(door.Id));
            Assert.IsTrue(doors.CanRing(door.Id, Ringer.Pip));

            Assert.AreEqual(2, doors.Ring(door.Id, Ringer.Pip, 0, 1, -1, events).Ring);
            Assert.IsTrue(events.Any(e => e.Kind == DoorEventKind.Coming), "the second ring: \"Coming!\"");
            bool crossed = false;
            float last = -1f;
            for (float t = 0f; t < NeighbourDoors.SilhouetteDelay + NeighbourDoors.SilhouetteSeconds + 0.3f; t += Dt)
            {
                doors.Advance(Dt);
                float p = doors.Silhouette(door.Id);
                if (p >= 0f)
                {
                    Assert.GreaterOrEqual(p, last, "the shadow walks one way, toward the door");
                    last = p;
                    crossed |= Enumerable.Range(0, DoorStoops.SilhouetteBands)
                        .Any(b => DoorStoops.SilhouetteBand(door, p, b, out _, out _, out _, out _));
                }
            }

            Assert.IsTrue(crossed, "the neighbour's shadow crosses the lit window");
            Assert.AreEqual(DoorPhase.Called, doors.Phase(door.Id), "two rings: still shut");
            Assert.IsTrue(doors.Ring(door.Id, Ringer.Pip, 0, 1, -1, events).Opens, "the third ring opens it");
        }

        [Test]
        public void AChildWhoWalksAwayLeavesTheHouseToSettleWithNothingLost()
        {
            var doors = new NeighbourDoors();
            doors.Ring(Fogg.Id, Ringer.Pip);
            Run(doors, 2f);
            doors.Abandon(Fogg.Id);
            Assert.AreEqual(DoorPhase.Closed, doors.Phase(Fogg.Id));
            Assert.IsFalse(doors.Busy(Fogg.Id));
            Run(doors, 1f);
            Assert.AreEqual(0f, doors.HallLight(Fogg.Id), 1e-4f, "the light goes off");
            Assert.AreEqual(1, doors.Ring(Fogg.Id, Ringer.Pip).Ring, "ringing again starts over");
        }

        [Test]
        public void SilhouetteBandsStayInsideTheWindow()
        {
            foreach (var door in LivingStreetLayout.Doors)
            {
                DoorStoops.HallWindow(door, out float centre, out float bottom, out float top, out float width, out _, out _);
                int shown = 0;
                for (float p = 0f; p <= 1f; p += 0.05f)
                {
                    for (int b = 0; b < DoorStoops.SilhouetteBands; b++)
                    {
                        if (!DoorStoops.SilhouetteBand(door, p, b, out float l, out float r, out float lo, out float hi))
                        {
                            continue;
                        }

                        shown++;
                        Assert.GreaterOrEqual(l, centre - width * 0.5f - 1e-4f, door.Id);
                        Assert.LessOrEqual(r, centre + width * 0.5f + 1e-4f, door.Id);
                        Assert.GreaterOrEqual(lo, bottom - 1e-4f, door.Id);
                        Assert.LessOrEqual(hi, top + 1e-4f, door.Id);
                    }
                }

                Assert.Greater(shown, 20, door.Id + ": the shadow shows in its window");
                Assert.IsFalse(Enumerable.Range(0, DoorStoops.SilhouetteBands).Any(b => DoorStoops.SilhouetteBand(door, 1f, b, out _, out _, out _, out _)),
                    door.Id + ": gone past the window at the end");
            }
        }

        [Test]
        public void TheDoorEasesOpenOnASmoothHingeCurveNeverASnap()
        {
            foreach (var door in LivingStreetLayout.Doors)
            {
                Assert.GreaterOrEqual(door.Gag.OpenSeconds, 0.8f, door.Id + ": no snap");
                Assert.AreEqual(0f, NeighbourDoors.HingeOpen(0f, door.Gag.Bounce));
                Assert.Less(NeighbourDoors.HingeOpen(0.05f, door.Gag.Bounce), 0.02f, door.Id + ": a gentle start off the latch");
                Assert.AreEqual(1f, NeighbourDoors.HingeOpen(1.4f, door.Gag.Bounce), 1e-3f, door.Id + ": settled");
                Assert.AreEqual(1f, NeighbourDoors.HingeOpen(2f, door.Gag.Bounce), 1e-3f, door.Id);

                // In play: from the third ring to the door shut, the leaf never moves more than a small step per frame.
                var doors = new NeighbourDoors();
                for (int r = 0; r < NeighbourDoors.RingsToOpen; r++)
                {
                    Assert.IsNotNull(doors.Ring(door.Id, Ringer.Group, 0, 1, 0));
                    if (r < NeighbourDoors.RingsToOpen - 1)
                    {
                        Run(doors, NeighbourDoors.RingBeat + 0.05f);
                    }
                }

                float last = 0f;
                float most = 0f;
                bool peekFirst = false;
                for (float t = 0f; t < 20f && doors.Busy(door.Id); t += Dt)
                {
                    doors.Advance(Dt);
                    if (doors.Turn(door.Id) >= 0 && doors.WaitingForRecipient(door.Id))
                    {
                        doors.RecipientReady(door.Id);
                    }

                    float swing = doors.SwingDegrees(door.Id);
                    most = Math.Max(most, Math.Abs(swing - last));
                    if (!peekFirst && swing > 0f)
                    {
                        doors.Figure(door.Id, 0, out _, out _, out _, out _, out _, out var clip, out float clipTime, out _);
                        Assert.AreEqual(WalkerClip.OpenDoorPeek, clip, door.Id + ": the hand is on the door as it moves");
                        Assert.GreaterOrEqual(clipTime, WalkerClips.PeekHandOnDoor - 0.02f, door.Id);
                        peekFirst = true;
                    }

                    last = swing;
                }

                TestContext.WriteLine($"{door.Id}: largest swing step {most:F2} degrees per frame at 60 fps");
                Assert.IsTrue(peekFirst, door.Id);
                Assert.Less(most, 4.5f, door.Id + ": a smooth swing (a snap would jump tens of degrees)");
                Assert.AreEqual(DoorPhase.Closed, doors.Phase(door.Id));
            }
        }

        [Test]
        public void TheNeighbourComesOutAndHandsPipHerTreatOnlyWhenSheIsThere()
        {
            var door = Fogg;
            var doors = new NeighbourDoors();
            for (int r = 0; r < 3; r++)
            {
                doors.Ring(door.Id, Ringer.Pip);
                if (r < 2)
                {
                    Run(doors, NeighbourDoors.RingBeat + 0.05f, door.StandX, door.StandZ);
                }
            }

            // Pip waits at the foot of the steps: the neighbour comes down to her and gives the treat at the release.
            var events = new List<DoorEvent>();
            Run(doors, 12f, door.StandX, door.StandZ, events);
            var release = events.Where(e => e.Kind == DoorEventKind.Released).ToList();
            Assert.AreEqual(1, release.Count, "one treat for Pip");
            Assert.AreEqual(-1, release[0].Recipient);
            Assert.IsTrue(events.Any(e => e.Kind == DoorEventKind.Arrived));
            Assert.AreEqual(DoorPhase.Closed, doors.Phase(door.Id));

            // She rings and runs off before he comes out: he looks for her, goes back in, nothing is lost.
            doors = new NeighbourDoors();
            for (int r = 0; r < 3; r++)
            {
                doors.Ring(door.Id, Ringer.Pip);
                Run(doors, NeighbourDoors.RingBeat + 0.05f, door.StandX, door.StandZ);
            }

            events.Clear();
            Run(doors, 15f, door.StandX + 20f, door.StandZ - 10f, events);
            Assert.IsFalse(events.Any(e => e.Kind == DoorEventKind.Released), "no treat thrown after her");
            Assert.AreEqual(DoorPhase.Closed, doors.Phase(door.Id));
        }

        [Test]
        public void TheNeighbourWalksDownTheStepWithItsFeetOnTheDeckTheTreadAndTheGround()
        {
            foreach (var door in LivingStreetLayout.Doors)
            {
                Assert.AreEqual(DoorStoops.Sill, DoorStoops.GroundY(door, -0.3f), 1e-4f, door.Id);
                Assert.AreEqual(DoorStoops.Sill, DoorStoops.GroundY(door, DoorStoops.DeckDepth(door) - 0.1f), 1e-4f, door.Id);
                Assert.AreEqual(DoorStoops.Sill - DoorStoops.Rise, DoorStoops.GroundY(door, DoorStoops.DeckDepth(door) + DoorStoops.Tread * 0.5f), 1e-4f, door.Id);
                Assert.AreEqual(0f, DoorStoops.GroundY(door, DoorStoops.TreatOut(door)), 1e-4f, door.Id + ": the treat spot is on the ground");
                float previous = DoorStoops.Sill;
                for (float o = -0.3f; o < DoorStoops.TreatOut(door); o += 0.01f)
                {
                    float y = DoorStoops.GroundY(door, o);
                    Assert.LessOrEqual(y, previous + 1e-5f, door.Id + ": only ever down");
                    Assert.Less(previous - y, 0.03f, door.Id + ": no drop at a riser");
                    previous = y;
                }
            }
        }

        // ------------------------------------------------------------------------------------- one treat each

        [Test]
        public void EachChildOfAVisitingGroupGetsOneTreatInItsPailAtTheRelease()
        {
            var crowd = new CrowdClock();
            var doorEvents = new List<DoorEvent>();
            var crowdEvents = new List<CrowdEvent>();
            var treats = new Dictionary<string, List<int>>();
            var receivers = new HashSet<int>();
            var complete = new HashSet<string>();
            int visits = 0;
            float worstReach = 0f;
            for (int frame = 0; frame < 60 * 60 * 8; frame++)
            {
                doorEvents.Clear();
                crowdEvents.Clear();
                crowd.Advance(Dt, Away(), crowdEvents, doorEvents);
                foreach (var e in doorEvents)
                {
                    if (e.Kind == DoorEventKind.Rang && e.Ring == 1)
                    {
                        visits++;
                        treats[e.DoorId + "#" + e.Group + "#" + visits] = new List<int>();
                    }

                    if (e.Kind == DoorEventKind.Done && e.Group >= 0)
                    {
                        complete.Add(treats.Keys.Last(k => k.StartsWith(e.DoorId + "#" + e.Group + "#", StringComparison.Ordinal)));
                    }

                    if (e.Kind != DoorEventKind.Released)
                    {
                        continue;
                    }

                    Assert.GreaterOrEqual(e.Group, 0, "a group's visit");
                    int walker = crowd.ChildWalker(e.Group, e.Recipient);
                    Assert.GreaterOrEqual(walker, 0, "the treat goes to a child");
                    Assert.IsFalse(crowd.IsGrownUp(walker));
                    var key = treats.Keys.Last(k => k.StartsWith(e.DoorId + "#" + e.Group + "#", StringComparison.Ordinal));
                    treats[key].Add(e.Recipient);
                    receivers.Add(walker);

                    // The child stands a hand-out distance in front of the neighbour, facing it.
                    var pose = crowd.Poses[walker];
                    crowd.Doors.Figure(e.DoorId, 0, out float gx, out float gz, out _, out float gyaw, out _, out var clip, out _, out _);
                    Assert.AreEqual(WalkerClip.GiveTreat, clip, "the neighbour is handing it over");
                    Assert.AreEqual(WalkerClip.HoldPail, pose.Clip, "the child holds out its pail");
                    Assert.AreEqual(WalkerClips.PailDrop, pose.ClipTime, 0.05f, "it lands at the pail's own drop");
                    float d = (float)Math.Sqrt((pose.X - gx) * (pose.X - gx) + (pose.Z - gz) * (pose.Z - gz));
                    worstReach = Math.Max(worstReach, Math.Abs(d - WalkerClips.ReceiveDistance));
                    Assert.Less(Math.Abs(Stride.DeltaYaw(pose.Yaw, gyaw + 180f)), 10f, "facing the neighbour");
                }
            }

            TestContext.WriteLine($"{visits} visits; worst hand-to-pail distance error {worstReach:F3} m");
            Assert.Greater(visits, 4);
            Assert.Less(worstReach, 0.12f, "the hand is over the pail");
            Assert.Greater(complete.Count, 4);
            foreach (var pair in treats.Where(p => complete.Contains(p.Key)))
            {
                int group = int.Parse(pair.Key.Split('#')[1]);
                CollectionAssert.AreEqual(Enumerable.Range(0, crowd.ChildrenOf(group)), pair.Value,
                    pair.Key + ": one treat for each child, in turn");
            }

            Assert.GreaterOrEqual(receivers.Count, 4, "every trick-or-treating child gets treats");
        }

        [Test]
        public void ChildrenWaitInASmallArcFacingTheDoorNeverStacked()
        {
            foreach (var door in LivingStreetLayout.Doors)
            {
                var spots = new List<(float X, float Z, string What)>();
                for (int count = 1; count <= 3; count++)
                {
                    for (int i = 0; i < count; i++)
                    {
                        DoorStoops.WaitSlot(door, i, count, out float x, out float z, out float yaw);
                        if (count == 3)
                        {
                            spots.Add((x, z, "slot " + i));
                        }

                        DoorStoops.Point(door, DoorStoops.ReceiveOut(door) - WalkerClips.ReceiveDistance, 0f, out float gx, out float gz);
                        Assert.Less(Math.Abs(Stride.DeltaYaw(yaw, Stride.YawTo(x, z, gx, gz))), 1f, "facing the neighbour's spot");
                        Assert.Less(Math.Abs(Stride.DeltaYaw(yaw, door.StandFacingYaw)), 45f, "toward the door");
                    }
                }

                DoorStoops.GrownUpSlot(door, out float ax, out float az, out _);
                spots.Add((ax, az, "grown-up"));
                DoorStoops.Point(door, DoorStoops.ReceiveOut(door), 0f, out float rx, out float rz);
                spots.Add((rx, rz, "receive"));
                DoorStoops.Point(door, DoorStoops.TreatOut(door), 0f, out float tx, out float tz);
                for (int a = 0; a < spots.Count; a++)
                {
                    var s = spots[a];
                    for (int b = a + 1; b < spots.Count; b++)
                    {
                        float d = (float)Math.Sqrt((s.X - spots[b].X) * (s.X - spots[b].X) + (s.Z - spots[b].Z) * (s.Z - spots[b].Z));
                        Assert.GreaterOrEqual(d, GiveWay.MinGap, $"{door.Id}: {s.What} and {spots[b].What} overlap");
                    }

                    Assert.GreaterOrEqual(LivingStreetRules.ClueDistance(s.X, s.Z, out string clue), LivingStreetRules.ClueClearance,
                        $"{door.Id} {s.What} near {clue}");
                    Assert.GreaterOrEqual(StreetObstacles.Clearance(s.X, s.Z, out string what), StreetObstacles.WalkerClearance,
                        $"{door.Id} {s.What} against {what}");
                }

                Assert.Greater(DoorStoops.TreatOut(door), DoorStoops.DeckDepth(door) + DoorStoops.Tread,
                    door.Id + ": the neighbour's treat spot is on the ground just past the step");
                float treatToReceive = (float)Math.Sqrt((tx - rx) * (tx - rx) + (tz - rz) * (tz - rz));
                Assert.AreEqual(WalkerClips.ReceiveDistance, treatToReceive, 1e-3f, door.Id + ": a hand-out apart");
            }
        }

        // --------------------------------------------------------------------------------------- the crowd cap

        /// <summary>Four families on the door circuit (twice the slice), so doors are contended.</summary>
        private static IReadOnlyList<CrowdRoute> Crowded()
        {
            var a = CrowdRoutes.All[0];
            var b = CrowdRoutes.All[1];
            return new[]
            {
                a, b,
                new CrowdRoute("group_c", a.Speed, a.Plain, a.Detours.ToArray(), a.Members, 14f),
                new CrowdRoute("group_d", b.Speed, b.Plain, b.Detours.ToArray(), b.Members, 21f),
            };
        }

        [Test]
        public void ADoorNeverGathersAMobOneVisitsOneWaitsTheRestWalkOn()
        {
            var crowd = new CrowdClock(Crowded());
            int queuedSeen = 0;
            int skipped = 0;
            var wasQueued = new bool[crowd.GroupCount];
            var ringsPerDoor = LivingStreetLayout.Doors.ToDictionary(d => d.Id, d => 0);
            var events = new List<CrowdEvent>();
            int mostAtADoor = 0;
            for (int frame = 0; frame < 60 * 60 * 15; frame++)
            {
                events.Clear();
                crowd.Advance(Dt, Away(), events);
                foreach (var e in events.Where(e => e.Kind == CrowdEventKind.DoorRung && e.Ring == 1))
                {
                    ringsPerDoor[e.DoorId]++;
                }

                foreach (var door in LivingStreetLayout.Doors)
                {
                    int visiting = 0, waiting = 0;
                    for (int g = 0; g < crowd.GroupCount; g++)
                    {
                        if (crowd.DoorOf(g) != door.Id)
                        {
                            continue;
                        }

                        if (crowd.IsQueued(g))
                        {
                            waiting++;
                        }
                        else
                        {
                            visiting++;
                        }
                    }

                    Assert.LessOrEqual(visiting, 1, $"{door.Id}: one family at the door (frame {frame}, holder {crowd.Doors.Holder(door.Id)}, queued {crowd.Doors.Queued(door.Id)}, phase {crowd.Doors.Phase(door.Id)}, groups at it: {string.Join(" ", Enumerable.Range(0, crowd.GroupCount).Where(g => crowd.DoorOf(g) == door.Id).Select(g => g + (crowd.IsQueued(g) ? "q" : "") + (crowd.IsStopped(g) ? "s" : "w")))})");
                    Assert.LessOrEqual(waiting, 1, $"{door.Id}: one family waiting its turn");

                    // Walkers near the door: the visiting family only (the waiting one stands a few metres back).
                    CrowdRoutes.Gathering(door, out float gx, out float gz);
                    int near = crowd.Poses.Count(p => (p.X - gx) * (p.X - gx) + (p.Z - gz) * (p.Z - gz) < 2.2f * 2.2f);
                    mostAtADoor = Math.Max(mostAtADoor, near);
                }

                for (int g = 0; g < crowd.GroupCount; g++)
                {
                    bool q = crowd.IsQueued(g);
                    queuedSeen += q && !wasQueued[g] ? 1 : 0;
                    wasQueued[g] = q;
                }
            }

            int laps = Enumerable.Range(0, crowd.GroupCount).Sum(crowd.Laps);
            int possible = laps * LivingStreetLayout.Doors.Count;
            skipped = possible - ringsPerDoor.Values.Sum();
            TestContext.WriteLine($"queued {queuedSeen} times; visits {string.Join(", ", ringsPerDoor)}; laps {laps}; " +
                                  $"most walkers by a door {mostAtADoor}");
            Assert.Greater(queuedSeen, 0, "a family waited its turn");
            Assert.LessOrEqual(mostAtADoor, 4, "never a mob by a door (a family of three, and one passer-by at most)");
            Assert.IsTrue(ringsPerDoor.Values.All(v => v > 2), "every door is visited");
        }

        [Test]
        public void TheCaseRulesHoldWithTheDoorsAndTheArcs()
        {
            // 20 simulated minutes with Pip wandering, the lantern lit: the heart's cap, the clue clearance (arcs, receive
            // spots and steps aside included), no walker in an obstacle, and families that stay together.
            var crowd = new CrowdClock();
            var random = new Random(28);
            float pipX = 0f, pipZ = -12f;
            float closestClue = float.MaxValue, closestObstacle = float.MaxValue, widest = 0f;
            string clueAt = null, obstacleAt = null;
            for (int frame = 0; frame < 60 * 60 * 20; frame++)
            {
                if (frame % 120 == 0)
                {
                    pipX = (float)(random.NextDouble() * 60 - 18);
                    pipZ = (float)(random.NextDouble() * 24 - 12);
                }

                var inputs = new CrowdInputs { PipX = pipX, PipZ = pipZ, PipOutside = true, CaseOpen = true, PorchOpen = true };
                crowd.Advance(Dt, inputs);
                Assert.LessOrEqual(crowd.WalkersInHeart, CrowdClock.CaseHeartCap);
                if (frame % 6 != 0)
                {
                    continue;
                }

                for (int w = 0; w < crowd.WalkerCount; w++)
                {
                    var p = crowd.Poses[w];
                    float c = LivingStreetRules.ClueDistance(p.X, p.Z, out string clue);
                    if (c < closestClue)
                    {
                        closestClue = c;
                        clueAt = $"walker {w} at ({p.X:F2}, {p.Z:F2}) near {clue}";
                    }

                    float o = StreetObstacles.Clearance(p.X, p.Z, out string what);
                    if (o < closestObstacle)
                    {
                        closestObstacle = o;
                        obstacleAt = $"walker {w} at ({p.X:F2}, {p.Z:F2}) by {what}";
                    }
                }

                for (int g = 0; g < crowd.GroupCount; g++)
                {
                    int first = crowd.FirstWalker(g);
                    int size = crowd.RouteOf(g).Size;
                    for (int a = 0; a < size; a++)
                    {
                        for (int b = a + 1; b < size; b++)
                        {
                            var pa = crowd.Poses[first + a];
                            var pb = crowd.Poses[first + b];
                            widest = Math.Max(widest, (float)Math.Sqrt((pa.X - pb.X) * (pa.X - pb.X) + (pa.Z - pb.Z) * (pa.Z - pb.Z)));
                        }
                    }
                }
            }

            TestContext.WriteLine($"closest to a clue {closestClue:F2} m ({clueAt}); closest to an obstacle {closestObstacle:F2} m " +
                                  $"({obstacleAt}); widest family {widest:F2} m");
            Assert.GreaterOrEqual(closestClue, LivingStreetRules.ClueClearance, clueAt);
            Assert.GreaterOrEqual(closestObstacle, StreetObstacles.WalkerClearance - 0.12f, obstacleAt + ": never inside a hedge, wall or post");
            Assert.LessOrEqual(widest, CohesionDistance, "a family walks and stands together");
        }

        /// <summary>No two members of a family are ever farther apart than this.</summary>
        private const float CohesionDistance = 3.6f;

        // --------------------------------------------------------------------------------------- paths and goals

        [Test]
        public void EveryLegLeadsToAGoal()
        {
            foreach (var route in CrowdRoutes.All)
            {
                foreach (var trip in route.Detours)
                {
                    Assert.AreEqual(1, trip.Nodes.Count(n => n.IsStop), $"{route.Id}: the trip at {trip.At} has one goal");
                    // It comes back to the sidewalk it left, on the far side of the door (or to its branch).
                    var last = trip.Nodes[trip.Nodes.Length - 1];
                    Assert.AreEqual(route.Plain[trip.At].Z, last.Z, 1e-4f, $"{route.Id}: the trip comes back to the sidewalk");
                    var next = route.Plain[trip.At + 1];
                    var branch = route.Plain[trip.At];
                    float onward = (last.X - branch.X) * (next.X - branch.X) + (last.Z - branch.Z) * (next.Z - branch.Z);
                    Assert.GreaterOrEqual(onward, 0f, $"{route.Id}: it rejoins the sidewalk the way the family was going");
                    if (trip.Goal == RouteGoal.Door)
                    {
                        Assert.AreEqual(RouteNodeKind.Door, trip.GoalNode.Kind);
                        Assert.AreEqual(trip.DoorId, trip.GoalNode.DoorId);
                    }
                }

                // Around the lap, the next goal is never far: no aimless wandering.
                var lap = route.Lap(true);
                var stops = Enumerable.Range(0, lap.Length).Where(i => lap[i].IsStop).ToList();
                Assert.GreaterOrEqual(stops.Count, 2, route.Id + ": goals on every lap");
                float longest = 0f, run = 0f;
                for (int k = 0; k < lap.Length * 2; k++)
                {
                    var a = lap[k % lap.Length];
                    var b = lap[(k + 1) % lap.Length];
                    run += (float)Math.Sqrt((b.X - a.X) * (b.X - a.X) + (b.Z - a.Z) * (b.Z - a.Z));
                    if (b.IsStop)
                    {
                        longest = Math.Max(longest, run);
                        run = 0f;
                    }
                }

                TestContext.WriteLine($"{route.Id}: {stops.Count} goals a lap, longest walk between goals {longest:F1} m");
                Assert.Less(longest, 50f, route.Id + ": every leg leads to a goal");
            }
        }

        [Test]
        public void NoWalkerPathEntersAHedgeFenceWallOrPost()
        {
            foreach (var route in CrowdRoutes.All)
            {
                foreach (bool porch in new[] { false, true })
                {
                    var lap = route.Lap(porch).ToList();
                    lap.Add(lap[1]);
                    var path = CrowdPath.Build(null, lap);
                    float worst = float.MaxValue;
                    string where = null;
                    for (float s = 0f; s < path.Length; s += 0.1f)
                    {
                        foreach (var member in route.Members)
                        {
                            path.At(s - member.Back, out float x, out float z, out float hx, out float hz);
                            float px = x + hz * member.Lateral;
                            float pz = z - hx * member.Lateral;
                            float c = StreetObstacles.Clearance(px, pz, out string what);
                            if (c < worst)
                            {
                                worst = c;
                                where = $"{route.Id} {member.Look} at ({px:F2}, {pz:F2}) by {what}";
                            }
                        }
                    }

                    TestContext.WriteLine($"{route.Id} (porch {porch}): closest to an obstacle {worst:F2} m ({where})");
                    Assert.GreaterOrEqual(worst, StreetObstacles.WalkerClearance, where);
                }
            }
        }

        [Test]
        public void CornersAreCurvesSoAFamilyTurnsTogether()
        {
            foreach (var route in CrowdRoutes.All)
            {
                // The closed lap with the node before its start and the one after its end, as the clock builds its legs.
                var lap = route.Lap(true).ToList();
                lap.Insert(0, lap[lap.Count - 2]);
                lap.Add(lap[2]);
                var path = CrowdPath.Build(null, lap);
                float lastYaw = float.NaN;
                float worst = 0f;
                string where = null;
                for (float s = path.NodeS[1]; s < path.NodeS[path.NodeS.Count - 2]; s += 0.05f)
                {
                    path.At(s, out float x, out float z, out float hx, out float hz);
                    float yaw = (float)(Math.Atan2(hx, hz) * 180.0 / Math.PI);
                    bool stop = path.Marks.Any(m => m.Node.IsStop && Math.Abs(m.S - s) < 0.06f);
                    if (!float.IsNaN(lastYaw) && !stop)
                    {
                        float step = Math.Abs(Stride.DeltaYaw(lastYaw, yaw));
                        if (step > worst)
                        {
                            worst = step;
                            where = $"({x:F1}, {z:F1})";
                        }
                    }

                    lastYaw = yaw;
                }

                // 5 cm of walking turns the heading by at most a few degrees (a pivot on a corner would turn 90 at once).
                TestContext.WriteLine($"{route.Id}: sharpest turn {worst:F1} degrees per 5 cm at {where}");
                Assert.Less(worst, 8f, route.Id + " pivots at " + where);
            }
        }

        // --------------------------------------------------------------------------------------- planted feet

        [Test]
        public void AStrideMovesTheRootExactlyByTheClipsTravelSoFeetStayPlanted()
        {
            foreach (string model in new[] { CrowdKit.ChildModel, CrowdKit.AdultModel })
            {
                var stride = new Stride(0f, 0f, 0f, 0f, 2f, 0f, model);
                float start = CrowdClipTimings.WalkTransitionDistance(CrowdClipTimings.WalkStart, model);
                float stop = CrowdClipTimings.WalkTransitionDistance(CrowdClipTimings.WalkStop, model);
                Assert.AreEqual(WalkerClips.Seconds(WalkerClip.WalkStart) + WalkerClips.Seconds(WalkerClip.WalkStop) +
                                (2f - start - stop) / CrowdKit.WalkSpeed(model), stride.Duration, 1e-3f);
                float lastZ = 0f;
                for (float t = 0f; t <= stride.Duration + 1e-4f; t += Dt)
                {
                    stride.Sample(t, out float x, out float z, out _, out _, out float speed, out var clip, out float clipTime);
                    Assert.GreaterOrEqual(z, lastZ - 1e-5f, "never backwards");
                    Assert.LessOrEqual(z - lastZ, CrowdKit.WalkSpeed(model) * Dt * 1.05f + 1e-4f, "never faster than the walk");
                    if (clip == WalkerClip.WalkStart)
                    {
                        Assert.AreEqual(CrowdClipTimings.RootTravel(CrowdClipTimings.WalkStart, model, clipTime / WalkerClips.Seconds(WalkerClip.WalkStart)),
                            z, 1e-4f, "the start moves the root by the clip's own travel");
                    }

                    lastZ = z;
                }

                stride.Sample(stride.Duration, out _, out float endZ, out _, out _, out float endSpeed, out var endClip, out _);
                Assert.AreEqual(2f, endZ, 1e-4f);
                Assert.AreEqual(0f, endSpeed, 1e-4f);
                Assert.AreEqual(WalkerClip.Locomotion, endClip);
            }

            // A turn with no travel steps round slowly; it is never a slide.
            var turn = new Stride(0f, 0f, 0f, 0f, 0f, 90f, CrowdKit.ChildModel);
            Assert.IsTrue(turn.Turning);
            Assert.GreaterOrEqual(turn.Duration, 90f / Stride.TurnRate - 1e-4f);
        }

        [Test]
        public void EveryNewClipHasAShippedStandIn()
        {
            foreach (WalkerClip clip in Enum.GetValues(typeof(WalkerClip)))
            {
                if (clip == WalkerClip.Locomotion)
                {
                    continue;
                }

                CollectionAssert.Contains(CrowdKit.Clips, WalkerClips.Fallback(clip), clip + " falls back to a shipped clip");
                Assert.IsNotNull(WalkerClips.Name(clip));
            }

            Assert.AreEqual(1.6f, WalkerClips.Seconds(WalkerClip.GiveTreat), 1e-3f);
            Assert.AreEqual(0.88f, WalkerClips.GiveRelease, 1e-3f);
            Assert.AreEqual(0.96f, WalkerClips.PailDrop, 1e-3f);
            Assert.AreEqual(0.3f, WalkerClips.PeekHandOnDoor, 1e-3f);
            Assert.AreEqual(WalkerClips.PailDrop - WalkerClips.GiveRelease, NeighbourDoors.PailLead, 1e-4f);
        }

        // ---------------------------------------------------------------------------------------- giving way

        private static List<GiveWay.Walker> Ring(float cx, float cz, float radius, int count, bool still)
        {
            var list = new List<GiveWay.Walker>();
            for (int i = 0; i < count; i++)
            {
                double a = i * 2 * Math.PI / count;
                list.Add(new GiveWay.Walker { X = cx + radius * (float)Math.Cos(a), Z = cz + radius * (float)Math.Sin(a), Still = still, GrownUp = i % 3 == 0 });
            }

            return list;
        }

        [Test]
        public void ABoxedInPipAlwaysGetsAWayOutWithinAMoment()
        {
            // Eight walkers close round Pip on open lane ground; she pushes east, then (another time) north.
            foreach (var (hx, hz) in new[] { (1f, 0f), (0f, 1f), (-0.6f, 0.8f) })
            {
                const float px = 33f, pz = 0f;
                var walkers = Ring(px, pz, 0.55f, 8, true);
                var ox = new float[walkers.Count];
                var oz = new float[walkers.Count];
                float clearAt = -1f;
                for (float t = 0f; t < 3f; t += Dt)
                {
                    GiveWay.Solve(walkers, ox, oz, px, pz, hx, hz, true, true, Dt, StreetObstacles.Clearance);
                    bool clear = true;
                    for (int i = 0; i < walkers.Count; i++)
                    {
                        float wx = walkers[i].X + ox[i] - px, wz = walkers[i].Z + oz[i] - pz;
                        float ahead = wx * hx + wz * hz;
                        float across = Math.Abs(wx * hz - wz * hx);
                        float body = GiveWay.PipRadius + GiveWay.BodyRadius(walkers[i].GrownUp) + 0.03f;
                        clear &= ahead < -body || ahead > 2f + body || across > body;
                    }

                    if (clear)
                    {
                        clearAt = t;
                        break;
                    }
                }

                TestContext.WriteLine($"heading ({hx}, {hz}): a way out after {clearAt:F2} s");
                Assert.GreaterOrEqual(clearAt, 0f, "the walkers make room");
                Assert.Less(clearAt, 1.5f, "within a moment");
            }
        }

        [Test]
        public void StillWalkersKeepRoomAroundPipAndNobodyOverlaps()
        {
            const float px = 33f, pz = 0f;
            var walkers = Ring(px, pz, 0.3f, 5, true);
            walkers.Add(new GiveWay.Walker { X = 36f, Z = 0f, Still = true });
            walkers.Add(new GiveWay.Walker { X = 36.1f, Z = 0.05f, Still = true });
            var ox = new float[walkers.Count];
            var oz = new float[walkers.Count];
            float firstStep = -1f;
            for (float t = 0f; t < 3f; t += Dt)
            {
                GiveWay.Solve(walkers, ox, oz, px, pz, 0f, 0f, false, true, Dt, StreetObstacles.Clearance);
                if (firstStep < 0f)
                {
                    firstStep = (float)Math.Sqrt(ox[0] * ox[0] + oz[0] * oz[0]);
                }
            }

            Assert.LessOrEqual(firstStep, GiveWay.Speed * Dt + 1e-4f, "a step aside is a step, never a jump");
            for (int i = 0; i < walkers.Count; i++)
            {
                float x = walkers[i].X + ox[i], z = walkers[i].Z + oz[i];
                if (i < 5)
                {
                    Assert.GreaterOrEqual((float)Math.Sqrt((x - px) * (x - px) + (z - pz) * (z - pz)), GiveWay.Room - 0.12f, "room round Pip");
                }

                for (int k = i + 1; k < walkers.Count; k++)
                {
                    float d = (float)Math.Sqrt((x - walkers[k].X - ox[k]) * (x - walkers[k].X - ox[k]) + (z - walkers[k].Z - oz[k]) * (z - walkers[k].Z - oz[k]));
                    Assert.GreaterOrEqual(d, GiveWay.MinGap - 0.12f, $"walkers {i} and {k} overlap");
                }
            }
        }

        [Test]
        public void AStepAsideNeverTakesAWalkerIntoAHedge()
        {
            // A child standing by the lane's north hedge row with Pip coming at it from the street side.
            float hedgeZ = SpookyStreet.Core.World.EnvironmentLayout.HedgeLineZ;
            var walkers = new List<GiveWay.Walker> { new GiveWay.Walker { X = 33f, Z = hedgeZ - 0.9f, Still = true } };
            var ox = new float[1];
            var oz = new float[1];
            float before = StreetObstacles.Clearance(walkers[0].X, walkers[0].Z);
            for (float t = 0f; t < 2f; t += Dt)
            {
                GiveWay.Solve(walkers, ox, oz, 33f, hedgeZ - 1.4f, 0f, 1f, true, true, Dt, StreetObstacles.Clearance);
            }

            float after = StreetObstacles.Clearance(walkers[0].X + ox[0], walkers[0].Z + oz[0]);
            Assert.GreaterOrEqual(after, Math.Min(before, StreetObstacles.WalkerClearance) - 1e-3f, "never pushed into the hedge");
        }

        [Test]
        public void WalkersPartForPipInTheRunningCrowd()
        {
            // Pip walks into group A on the lane and through it: nobody stays inside her body, and she gets past.
            var crowd = new CrowdClock();
            for (int i = 0; i < 60 * 4; i++)
            {
                crowd.Advance(Dt, Away());
            }

            var lead = crowd.Poses[0];
            double yaw = lead.Yaw * Math.PI / 180.0;
            float fx = (float)Math.Sin(yaw), fz = (float)Math.Cos(yaw);
            float pipX = lead.X + fx * 3f, pipZ = lead.Z + fz * 3f;
            float closest = float.MaxValue;
            for (int i = 0; i < 60 * 4; i++)
            {
                // Pip walks toward the family at a child's pace.
                pipX -= fx * 1.2f * Dt;
                pipZ -= fz * 1.2f * Dt;
                var inputs = new CrowdInputs { PipX = pipX, PipZ = pipZ, PipOutside = true, CaseOpen = true, PipHeadingX = -fx, PipHeadingZ = -fz, PipWalking = true };
                crowd.Advance(Dt, inputs);
                for (int w = 0; w < 3; w++)
                {
                    var p = crowd.Poses[w];
                    closest = Math.Min(closest, (float)Math.Sqrt((p.X - pipX) * (p.X - pipX) + (p.Z - pipZ) * (p.Z - pipZ)));
                }
            }

            TestContext.WriteLine($"closest a walker came to Pip's centre: {closest:F2} m");
            Assert.GreaterOrEqual(closest, GiveWay.PipRadius + GiveWay.ChildRadius - 0.08f, "they make way before she has to push");
        }
    }
}
