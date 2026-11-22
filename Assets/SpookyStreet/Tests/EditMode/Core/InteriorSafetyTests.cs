using System;
using System.Collections.Generic;
using System.Text;
using NUnit.Framework;
using SpookyStreet.Core.World;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// The owl house interior after TestFlight 23 ("right after entering through the window, I got stuck"): the
    /// sky dome's opaque sphere cut through the south of the interior, so a lens there saw a flat purple wall where the
    /// attic should be; the sky is now hidden while Pip or the lens is indoors. The interior's standing places, a flood
    /// fill over the collider layout at Pip's capsule size, form one main area joining the hall and the attic, with no
    /// pocket Pip can get into but not out of; every arrival point and safe anchor is in it. The lens is clamped inside
    /// the shell. The safety net sets Pip down on the nearest safe anchor when Pip is off the walkable area or pushed
    /// without getting anywhere for several seconds.
    /// </summary>
    public class InteriorSafetyTests
    {
        private const float Ifz = CaseRouteLayout.InteriorFrontZ;

        private static InteriorWalkMap s_closed;
        private static InteriorWalkMap s_open;

        private static InteriorWalkMap Map(bool doorOpen)
        {
            if (doorOpen)
            {
                return s_open ?? (s_open = new InteriorWalkMap(true));
            }

            return s_closed ?? (s_closed = new InteriorWalkMap(false));
        }

        private static string Describe(InteriorWalkMap map, List<int> nodes, int max = 12)
        {
            var sb = new StringBuilder();
            for (int i = 0; i < nodes.Count && i < max; i++)
            {
                int n = nodes[i];
                sb.Append($"({map.X(n):F2}, {map.Y(n):F2}, depth {map.Z(n) - Ifz:F2}) ");
            }

            if (nodes.Count > max)
            {
                sb.Append($"… {nodes.Count} in all");
            }

            return sb.ToString();
        }

        // ------------------------------------------------------------------------------------------- the bug

        [Test]
        public void TheSkyDomeCutsThroughTheInteriorSoTheSkyIsHiddenIndoors()
        {
            // The TestFlight 23 frame, reproduced from the art (docs/S002-dormer-stuck.md): Pip beside the attic trunk,
            // the lens up and to the east looking west. The lens is in the attic's air and inside the dome's sphere, and
            // the attic beyond depth ~1.4 is outside it: the dome's inner face hid it as a flat purple wall.
            const float lensX = 0.16f, lensY = 6.3f, lensZ = Ifz + 0.86f;
            Assert.IsTrue(InteriorCamera.InAir(lensX, lensY, lensZ), "the lens was in the attic's air");
            Assert.IsTrue(SkyModel.InsideDome(lensX, lensY, lensZ), "the lens was inside the dome's sphere");
            Assert.IsFalse(SkyModel.InsideDome(-1.1f, CaseRouteLayout.LoftY + 0.75f, Ifz + 2.75f),
                "June on her stool is beyond the dome: hidden behind it from that lens");
            Assert.IsTrue(InteriorSafety.SkyHidden(-1.5f, Ifz + 1.15f, lensX, lensZ), "the sky is hidden there now");

            // Everywhere indoors the sky is hidden, wherever the dome's sphere falls.
            int inside = 0, beyond = 0;
            for (float x = CaseRouteLayout.InnerMinX; x <= CaseRouteLayout.InnerMaxX; x += 0.2f)
            {
                for (float z = CaseRouteLayout.InnerMinZ; z <= CaseRouteLayout.InnerMaxZ; z += 0.2f)
                {
                    for (float y = CaseRouteLayout.FloorY + 0.2f; y < 8f; y += 0.3f)
                    {
                        if (!InteriorCamera.InAir(x, y, z))
                        {
                            continue;
                        }

                        Assert.IsTrue(InteriorSafety.SkyHidden(x, z, x, z), $"sky hidden with the lens at ({x}, {y}, {z})");
                        Assert.IsTrue(InteriorSafety.InsideShell(x, y, z), $"air ({x}, {y}, {z}) is inside the shell");
                        if (SkyModel.InsideDome(x, y, z))
                        {
                            inside++;
                        }
                        else
                        {
                            beyond++;
                        }
                    }
                }
            }

            Assert.Greater(inside, 0, "part of the interior's air is inside the dome's sphere");
            Assert.Greater(beyond, 0, "part of it is beyond the dome");
            // On the street the sky stays.
            Assert.IsFalse(InteriorSafety.SkyHidden(0f, 5f, 0f, 1f), "the street keeps its sky");
            Assert.IsFalse(InteriorSafety.SkyHidden(Thresholds.DormerOutside.ArriveX, Thresholds.DormerOutside.ArriveZ,
                -6f, 14f), "the bridge keeps its sky");
        }

        [Test]
        public void OnlyTheDomeReachesTheInteriorOfAllTheSkyPieces()
        {
            // Horizontal distance from the dome's axis to the nearest corner of the interior's plan.
            double near = CaseRouteLayout.InteriorFrontZ - 0.45 - SkyModel.DomeCentreZ;
            foreach (var layer in SkyModel.Layers)
            {
                Assert.Less(layer.Radius + 3f, near, $"cloud ring at {layer.Radius} m stays clear of the interior");
            }

            double ring = MagicSkyRing(SkyModel.ShootingStarFloorElevation);
            Assert.Less(ring, near, "shooting stars stay clear of the interior");
        }

        private static double MagicSkyRing(float elevation) => 49.5 * Math.Cos(elevation * Math.PI / 180.0);

        // ------------------------------------------------------------------------------------- the walk map

        [Test]
        public void TheHallAndTheAtticAreOneMainAreaJoinedByTheStair([Values(false, true)] bool doorOpen)
        {
            var map = Map(doorOpen);
            int attic = map.NodeAt(Thresholds.DormerInside.ArriveX, Thresholds.DormerInside.ArriveY,
                Thresholds.DormerInside.ArriveZ);
            int hall = map.NodeAt(Thresholds.DoorInside.ArriveX, Thresholds.DoorInside.ArriveY, Thresholds.DoorInside.ArriveZ);
            Assert.GreaterOrEqual(attic, 0, "Pip can stand at the dormer arrival");
            Assert.GreaterOrEqual(hall, 0, "Pip can stand at the hall arrival");
            Assert.IsTrue(map.WalkConnected(attic, hall), "the attic and the hall are joined on foot (the stair)");
            int main = 0;
            for (int n = 0; n < map.Count; n++)
            {
                if (map.Main[n])
                {
                    main++;
                }
            }

            TestContext.WriteLine($"door open {doorOpen}: {map.Count} standing places, {main} in the main area");
            Assert.Greater(main, 2000, "the main area is the bulk of the floors");
        }

        [Test]
        public void NoPocketPipCanGetIntoButNotOutOf([Values(false, true)] bool doorOpen)
        {
            var map = Map(doorOpen);
            var traps = map.Traps();
            Assert.IsEmpty(traps, "places reachable from the main area with no way back: " + Describe(map, traps));
        }

        [Test]
        public void EveryFloorPipCanReachConnectsToTheMainFloors([Values(false, true)] bool doorOpen)
        {
            // Every standing place on a floor (walk slab) Pip can get to by any move is back on the main floors by
            // walking, dropping or jumping; the floors Pip can never reach are listed (boxed in under the stair, or the hall
            // slab running under the front wall).
            var map = Map(doorOpen);
            var unreachable = new List<int>();
            for (int n = 0; n < map.Count; n++)
            {
                if (!map.OnFloor(n))
                {
                    continue;
                }

                if (map.ReachableFromMain[n])
                {
                    Assert.IsTrue(map.CanReachMain[n], $"floor at ({map.X(n):F2}, {map.Y(n):F2}, {map.Z(n):F2})");
                }
                else
                {
                    unreachable.Add(n);
                }
            }

            TestContext.WriteLine("floors Pip never reaches: " + Describe(map, unreachable, 40));
            foreach (int n in unreachable)
            {
                // Boxed in under the two flights, or the hall slab's lip under the front wall beside the doorway.
                bool underStair = map.X(n) < CaseRouteLayout.StairTopX && map.Z(n) > CaseRouteLayout.StairFootZ &&
                                  map.Y(n) < CaseRouteLayout.LandingY;
                bool underFrontWall = map.Z(n) < CaseRouteLayout.InnerMinZ && Math.Abs(map.X(n)) > CaseRouteLayout.DoorHalfWidth;
                Assert.IsTrue(underStair || underFrontWall,
                    $"floor ({map.X(n):F2}, {map.Y(n):F2}, depth {map.Z(n) - Ifz:F2}) is cut off");
            }
        }

        [Test]
        public void EveryArrivalAndSafeAnchorIsInTheMainArea([Values(false, true)] bool doorOpen)
        {
            var map = Map(doorOpen);
            foreach (var end in Thresholds.Ends)
            {
                if (!end.Inside)
                {
                    continue;
                }

                int n = map.NodeAt(end.ArriveX, end.ArriveY, end.ArriveZ);
                Assert.GreaterOrEqual(n, 0, end + " arrival is a standing place");
                Assert.IsTrue(map.Main[n], end + " arrival is in the main area");
                Assert.IsTrue(InteriorRescue.OnWalkableArea(end.ArriveX, end.ArriveY, end.ArriveZ, doorOpen),
                    end + " arrival is walkable for the safety net");
            }

            foreach (var a in InteriorSafety.Anchors)
            {
                int n = map.NodeAt(a.X, a.Y, a.Z);
                Assert.GreaterOrEqual(n, 0, a + " is a standing place");
                Assert.IsTrue(map.Main[n], a + " is in the main area");
                Assert.IsTrue(InteriorRescue.OnWalkableArea(a.X, a.Y, a.Z, doorOpen), a + " is walkable");
                Assert.IsTrue(InteriorWalkMap.CanStand(a.X, a.Y, a.Z, doorOpen, 0.05f), a + " has room for Pip");
                Assert.IsFalse(Thresholds.NearAnyZone(a.X, a.Y, a.Z, Thresholds.ArrivalClearance), a + " is clear of the zones");
            }
        }

        [Test]
        public void TheDormerArrivalHasRoomToWalkAway()
        {
            var a = Thresholds.DormerInside;
            float east = InteriorWalkMap.FreeRun(a.ArriveX, a.ArriveY, a.ArriveZ, 1f, 0f, 3f, false);
            float north = InteriorWalkMap.FreeRun(a.ArriveX, a.ArriveY, a.ArriveZ, 0f, 1f, 3f, false);
            float west = InteriorWalkMap.FreeRun(a.ArriveX, a.ArriveY, a.ArriveZ, -1f, 0f, 3f, false);
            float south = InteriorWalkMap.FreeRun(a.ArriveX, a.ArriveY, a.ArriveZ, 0f, -1f, 3f, false);
            TestContext.WriteLine($"free run from the dormer arrival: east {east:F2}, north {north:F2}, west {west:F2} " +
                                  $"(then the window's exit zone), south {south:F2} (the trunk)");
            Assert.GreaterOrEqual(east, 2f, "east into the attic");
            Assert.GreaterOrEqual(north, 2f, "north past June");
            Assert.Greater(west, 1f, "west to the window");
            Assert.Greater(south, 0.3f, "south to the trunk");
        }

        // ------------------------------------------------------------------------------------- the lens

        [Test]
        public void ShellReachKeepsTheLensInsideTheShell()
        {
            // Open attic: the full boom fits.
            float hy = CaseRouteLayout.LoftY + 1.05f;
            Assert.AreEqual(2.2f, InteriorSafety.ShellReach(0.5f, hy, Ifz + 3.5f, 0f, 20f, 2.2f), 0.03f, "open attic");
            // Pip with her back to the west knee wall, the lens behind her: the boom stops inside the wall.
            float d = InteriorSafety.ShellReach(-3.2f, hy, Ifz + 4.5f, 90f, 10f, 2.2f);
            Assert.Less(d, 0.4f, "a boom behind Pip into the knee wall is short");
            InteriorCamera.BackDirection(90f, 10f, out float bx, out float by, out float bz);
            Assert.IsTrue(InteriorSafety.InsideShell(-3.2f + bx * d, hy + by * d, Ifz + 4.5f + bz * d, InteriorSafety.ShellMargin),
                "the lens stays inside the shell");
            // A shoulder outside the shell gives no boom at all.
            Assert.AreEqual(0f, InteriorSafety.ShellReach(-5f, hy, Ifz + 4.5f, 90f, 10f, 2.2f));
        }

        [Test]
        public void EverySolvedShotIsInsideTheShell()
        {
            int shots = 0;
            for (float x = -3.2f; x <= 3.2f; x += 0.4f)
            {
                for (float depth = 0.6f; depth <= 6.6f; depth += 0.4f)
                {
                    foreach (float y in new[] { CaseRouteLayout.FloorY, CaseRouteLayout.LoftY })
                    {
                        float z = Ifz + depth;
                        if (!InteriorWalkMap.CanStand(x, y, z, false, 0.05f))
                        {
                            continue;
                        }

                        for (float yaw = 0f; yaw < 360f; yaw += 45f)
                        {
                            var shot = InteriorCamera.Solve(new InteriorCameraRequest
                            {
                                PipX = x, PipY = y, PipZ = z, Yaw = yaw, Pitch = 12f, PivotHeight = 0.9f,
                            });
                            if (!shot.Active)
                            {
                                continue;
                            }

                            shots++;
                            Assert.IsTrue(InteriorSafety.InsideShell(shot.CameraX, shot.CameraY, shot.CameraZ, InteriorSafety.ShellMargin),
                                $"shot for Pip at ({x:F1}, {y:F1}, depth {depth:F1}) yaw {yaw}: {shot}");
                        }
                    }
                }
            }

            Assert.Greater(shots, 500);
        }

        // ------------------------------------------------------------------------------------- the safety net

        private static float RunUntilRescue(InteriorRescue rescue, float fps, float seconds, float x, float y, float z,
            bool grounded, float stick, out RescueReason reason, float jitter = 0f)
        {
            float dt = 1f / fps;
            float t = 0f;
            int i = 0;
            while (t < seconds)
            {
                float j = jitter * (i++ % 2 == 0 ? 1f : -1f);
                t += dt;
                reason = rescue.Step(dt, x + j, y, z, grounded, stick, true);
                if (reason != RescueReason.None)
                {
                    return t;
                }
            }

            reason = RescueReason.None;
            return t;
        }

        [Test]
        public void PushingWithoutGettingAnywhereRescuesAfterSeveralSeconds([Values(30f, 60f)] float fps)
        {
            var a = Thresholds.DormerInside;
            var rescue = new InteriorRescue();
            float t = RunUntilRescue(rescue, fps, 10f, a.ArriveX, a.ArriveY, a.ArriveZ, true, 1f, out var reason, 0.1f);
            Assert.AreEqual(RescueReason.Stuck, reason);
            Assert.AreEqual(InteriorRescue.StuckSeconds, t, 2.5f / fps, "after the same time at any frame rate");
        }

        [Test]
        public void WalkingPushingLightlyOrBeingBusyNeverRescues()
        {
            var a = Thresholds.DormerInside;
            var rescue = new InteriorRescue();
            const float dt = 1f / 60f;
            // Walking east at 2 m/s for 6 s.
            for (int i = 0; i < 360; i++)
            {
                float x = a.ArriveX + 2f * dt * (i % 120);
                Assert.AreEqual(RescueReason.None, rescue.Step(dt, x, a.ArriveY, a.ArriveZ, true, 1f, true), "walking");
            }

            rescue.Reset();
            RunUntilRescue(rescue, 60f, 8f, a.ArriveX, a.ArriveY, a.ArriveZ, true, 0.3f, out var reason);
            Assert.AreEqual(RescueReason.None, reason, "a light touch on the stick is not pushing");
            for (int i = 0; i < 600; i++)
            {
                Assert.AreEqual(RescueReason.None, rescue.Step(dt, a.ArriveX, a.ArriveY, a.ArriveZ, true, 1f, false),
                    "no control (a page-turn, a conversation)");
            }

            // Releasing the stick starts the count again.
            RunUntilRescue(rescue, 60f, 3f, a.ArriveX, a.ArriveY, a.ArriveZ, true, 1f, out reason);
            rescue.Step(dt, a.ArriveX, a.ArriveY, a.ArriveZ, true, 0f, true);
            RunUntilRescue(rescue, 60f, 3f, a.ArriveX, a.ArriveY, a.ArriveZ, true, 1f, out reason);
            Assert.AreEqual(RescueReason.None, reason, "two short pushes");
            // On the street the safety net stays out of it.
            RunUntilRescue(rescue, 60f, 8f, 0f, 0f, 5f, true, 1f, out reason);
            Assert.AreEqual(RescueReason.None, reason, "the street");
        }

        [Test]
        public void OffTheWalkableAreaRescuesQuickly()
        {
            var rescue = new InteriorRescue();
            // Standing on nothing under the loft, in mid-air of the hall (as if wedged between two colliders).
            float t = RunUntilRescue(rescue, 60f, 3f, 1.0f, 2.5f, Ifz + 3.0f, true, 0f, out var reason);
            Assert.AreEqual(RescueReason.OffWalkable, reason);
            Assert.AreEqual(InteriorRescue.OffWalkableSeconds, t, 0.05f);
            // Out of the shell entirely (beside the interior, in the air).
            t = RunUntilRescue(rescue, 60f, 3f, CaseRouteLayout.InnerMaxX + 1.5f, 3f, Ifz + 3f, false, 0f, out reason);
            Assert.AreEqual(RescueReason.OffWalkable, reason);
            // On top of the roof (a glitch out through the slope): outside the shell, off the walkable area.
            float roofTop = CaseRouteLayout.KneeY + (CaseRouteLayout.InnerMaxX - 1.5f) * CaseRouteLayout.RoofSlope + 0.25f;
            Assert.IsFalse(InteriorRescue.OnWalkableArea(1.5f, roofTop, Ifz + 3f, false), "on the roof plate");
            Assert.IsTrue(InteriorRescue.OnWalkableArea(2.95f, 4.72f, Ifz + 0.66f, false), "on the crates under the slope");
            // Sunk into the trunk by the front wall.
            Assert.IsFalse(InteriorRescue.OnWalkableArea(-1.3f, CaseRouteLayout.LoftY, Ifz + 0.5f, false), "in the trunk");
            // A jump over the open attic floor is fine (not grounded, over the shell).
            RunUntilRescue(rescue, 60f, 3f, 0.5f, CaseRouteLayout.LoftY + 0.8f, Ifz + 3f, false, 0f, out reason);
            Assert.AreEqual(RescueReason.None, reason, "in the air over the attic");
        }

        [Test]
        public void TheRescueSetsPipDownOnTheNearestSafeAnchor()
        {
            // From the TestFlight 23 spot by the trunk: the dormer arrival.
            var near = InteriorSafety.Nearest(-1.5f, CaseRouteLayout.LoftY, Ifz + 1.15f);
            Assert.AreEqual("attic arrival", near.Name);
            // Stuck right at it: the next one along.
            var away = InteriorSafety.Nearest(near.X, near.Y, near.Z, true);
            Assert.AreNotEqual(near.Name, away.Name);
            Assert.AreEqual(CaseRouteLayout.LoftY, away.Y, 1e-3f, "still in the attic");
            // From the hall: a hall anchor, never one upstairs.
            var hall = InteriorSafety.Nearest(1.5f, CaseRouteLayout.FloorY, Ifz + 3.5f);
            Assert.AreEqual(CaseRouteLayout.FloorY, hall.Y, 1e-3f, hall.ToString());
        }
    }
}
