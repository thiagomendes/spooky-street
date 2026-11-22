using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpookyStreet.Core.World;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// The indoor camera (S002 interior polish): wherever Pip can stand in the owl house and however the player has
    /// turned the camera, the solved lens sits in room air, touches no solid, and has clear lines to Pip's head and
    /// chest (and to the character being talked to), with a boom long enough to frame all of Pip.
    /// </summary>
    public class InteriorCameraTests
    {
        private const float PivotHeight = 0.95f;
        private const float DefaultPitch = 4f;
        private static readonly float[] Yaws = { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f };

        /// <summary>Standing places inside the house: hall, attic and stair (sampled every 0.3–0.4 m).</summary>
        public static IEnumerable<(string Room, float X, float Y, float Z)> StandingPoints()
        {
            const float floor = CaseRouteLayout.FloorY;
            const float loft = CaseRouteLayout.LoftY;
            for (float x = -2.45f; x <= 1.2f; x += 0.4f)
            {
                for (float z = 11.45f; z <= 15.6f; z += 0.4f)
                {
                    if (Standable(x, floor, z))
                    {
                        yield return ("hall", x, floor, z);
                    }
                }
            }

            for (float x = -2.1f; x <= 2.1f; x += 0.4f)
            {
                for (float z = 12.9f; z <= 16.6f; z += 0.4f)
                {
                    bool onLoft = CaseRouteLayout.GroundAt(x, z, loft + 0.05f) > loft - 0.05f;
                    if (onLoft && Standable(x, loft, z) && InteriorCamera.RoofAt(x) > loft + 1.2f ||
                        onLoft && Standable(x, loft, z) && x < InteriorCamera.AlcoveInnerX &&
                        System.Math.Abs(z - CaseRouteLayout.WindowZ) < 0.5f)
                    {
                        yield return ("attic", x, loft, z);
                    }
                }
            }

            float stairZ = (CaseRouteLayout.StairMinZ + CaseRouteLayout.StairMaxZ) * 0.5f;
            for (float x = CaseRouteLayout.StairBottomX + 0.2f; x <= CaseRouteLayout.StairTopX - 0.1f; x += 0.3f)
            {
                yield return ("stair", x, InteriorCamera.StairSurface(x), stairZ);
            }
        }

        private static bool Standable(float x, float y, float z) =>
            CaseRouteLayout.Inside(x, z) && CaseRouteLayout.Blocking(x, y, z) == null &&
            CaseRouteLayout.Blocking(x + 0.15f, y, z) == null && CaseRouteLayout.Blocking(x - 0.15f, y, z) == null &&
            CaseRouteLayout.Blocking(x, y, z + 0.15f) == null && CaseRouteLayout.Blocking(x, y, z - 0.15f) == null &&
            !(x > CaseRouteLayout.NookX - 0.3f && z > CaseRouteLayout.NookMinZ - 0.3f && y < 1f) &&
            !(y < 1f && z > CaseRouteLayout.StairMinZ - 0.3f);

        private static InteriorShot Solve(float x, float y, float z, float yaw, bool doorOpen = false)
        {
            var request = new InteriorCameraRequest
            {
                PipX = x,
                PipY = y,
                PipZ = z,
                Yaw = yaw,
                Pitch = DefaultPitch,
                PivotHeight = PivotHeight,
                DoorOpen = doorOpen,
            };
            return InteriorCamera.Solve(request);
        }

        private static string Problem(InteriorShot shot, float x, float y, float z, bool doorOpen)
        {
            if (!shot.Active)
            {
                return "inactive";
            }

            if (!shot.Clear || shot.Distance < InteriorCamera.MinDistance)
            {
                return "tight " + shot;
            }

            if (!InteriorCamera.SphereClear(shot.CameraX, shot.CameraY, shot.CameraZ, doorOpen, InteriorCamera.Radius - 0.01f))
            {
                return "lens in a solid " + shot;
            }

            foreach (float h in new[] { InteriorCamera.HeadHeight, InteriorCamera.ChestHeight })
            {
                string b = InteriorCamera.FirstBlocker(shot.CameraX, shot.CameraY, shot.CameraZ, x, y + h, z, doorOpen);
                if (b != null)
                {
                    return $"{b} between the lens and Pip ({h}) " + shot;
                }
            }

            return null;
        }

        [Test]
        public void EveryStandingPlaceAndYawGetsAClearShot()
        {
            var failures = new List<string>();
            int count = 0;
            foreach (var p in StandingPoints())
            {
                foreach (float yaw in Yaws)
                {
                    foreach (bool door in new[] { false, true })
                    {
                        if (InteriorCamera.TouchesSolid(p.X, p.Y + 0.55f, p.Z, 0.15f, door) ||
                            InteriorCamera.TouchesSolid(p.X, p.Y + 0.9f, p.Z, 0.15f, door))
                        {
                            continue; // Pip cannot stand in the dressing (or in the swung-open door leaf)
                        }

                        count++;
                        var shot = Solve(p.X, p.Y, p.Z, yaw, door);
                        string problem = Problem(shot, p.X, p.Y, p.Z, door);
                        if (problem != null)
                        {
                            failures.Add($"{p.Room} ({p.X:F2}, {p.Y:F2}, {p.Z:F2}) yaw {yaw} door {door}: {problem}");
                        }
                    }
                }
            }

            Assert.Greater(count, 400, "the sweep covers the house");
            Assert.IsEmpty(failures, $"{failures.Count}/{count} shots:\n" + string.Join("\n", failures.Take(60)));
        }

        [Test]
        public void TheRoomsAreSampled()
        {
            var rooms = StandingPoints().Select(p => p.Room).Distinct().ToList();
            CollectionAssert.AreEquivalent(new[] { "hall", "attic", "stair" }, rooms);
        }

        [Test]
        public void OpenRoomsKeepThePlayersFraming()
        {
            // Middle of the attic and the hall facing the door: nothing to dodge, so no yaw swing and the full boom.
            var attic = Solve(0.2f, CaseRouteLayout.LoftY, 14.3f, 0f);
            Assert.AreEqual(InteriorRoom.Attic, attic.Room);
            Assert.AreEqual(0f, attic.YawOffset, 1e-3f);
            Assert.AreEqual(InteriorCamera.ProfileFor(InteriorRoom.Attic).Distance, attic.Distance, 1e-3f);
            var hall = Solve(0.1f, CaseRouteLayout.FloorY, 13.2f, 180f);
            Assert.AreEqual(InteriorRoom.Foyer, hall.Room);
            Assert.AreEqual(0f, hall.YawOffset, 1e-3f);
            Assert.Less(hall.CameraY, InteriorCamera.HallCeilingY - InteriorCamera.Radius + 1e-3f, "under the hall joists");
        }

        [Test]
        public void TheFoyerDoorShotStaysOffTheStair()
        {
            // The 23_foyer_door pose: Pip in the hall facing the open door, the camera behind toward the stair.
            var shot = Solve(0.1f, 0.65f, 13.2f, 180f, true);
            Assert.IsTrue(shot.Clear, shot.ToString());
            Assert.Less(shot.CameraZ, CaseRouteLayout.StairMinZ - InteriorCamera.Radius, "the lens stays in front of the stair");
            Assert.Less(shot.CameraY, InteriorCamera.HallCeilingY, "and below the loft");
        }

        [Test]
        public void TheAlcoveTurnsTheCameraIntoTheRoom()
        {
            // Just in by the window, facing into the attic: behind Pip is the window, so the lens swings into the room.
            var shot = Solve(-2.45f, CaseRouteLayout.LoftY, CaseRouteLayout.WindowZ, 90f);
            Assert.AreEqual(InteriorRoom.Alcove, shot.Room);
            Assert.IsTrue(shot.Clear, shot.ToString());
            Assert.Greater(shot.CameraX, CaseRouteLayout.InnerMinX + InteriorCamera.Radius, "the lens is inside the house");
        }

        [Test]
        public void TalkingKeepsTheCharacterInView()
        {
            var june = (X: -1.75f, Y: CaseRouteLayout.LoftY, Z: CaseRouteLayout.FrontZ + 4.05f);
            foreach (var pip in new[] { (-2.35f, 13.85f), (-1.2f, 14.2f), (-0.6f, 15.3f) })
            {
                foreach (float yaw in Yaws)
                {
                    var request = new InteriorCameraRequest
                    {
                        PipX = pip.Item1,
                        PipY = CaseRouteLayout.LoftY,
                        PipZ = pip.Item2,
                        Yaw = yaw,
                        Pitch = DefaultPitch,
                        PivotHeight = PivotHeight,
                        HasFocus = true,
                        FocusX = june.X,
                        FocusY = june.Y,
                        FocusZ = june.Z,
                    };
                    var shot = InteriorCamera.Solve(request);
                    Assert.IsTrue(shot.Clear, $"Pip {pip} yaw {yaw}: {shot}");
                    Assert.IsNull(InteriorCamera.FirstBlocker(shot.CameraX, shot.CameraY, shot.CameraZ, june.X,
                        june.Y + InteriorCamera.FocusHeight, june.Z, false), $"June hidden from {shot}");
                }
            }
        }

        [Test]
        public void OutsideTheHouseTheSolverStandsDown()
        {
            Assert.IsFalse(Solve(0f, 0f, 0f, 0f).Active);
            Assert.IsFalse(Solve(-4f, CaseRouteLayout.SillY, CaseRouteLayout.WindowZ, 90f).Active, "on the bridge");
        }

        [Test]
        public void TheProbeStopsAtTheFirstSolid()
        {
            // From the hall shoulder straight back (+Z) the stair's side stops the boom before it.
            float y = CaseRouteLayout.FloorY + PivotHeight + 0.1f;
            float d = InteriorCamera.ClearDistance(0f, y, 14.5f, 180f, 0f, 3f, 0f, CaseRouteLayout.FloorY, 14.5f, false);
            Assert.Less(14.5f + d, CaseRouteLayout.StairMinZ - InteriorCamera.Radius + 0.05f);
            Assert.Greater(d, 0.8f);
        }

        [Test]
        public void TheHallCeilingCoversTheJoistsAndBunting()
        {
            Assert.IsFalse(InteriorCamera.SphereClear(0f, 2.2f, 14f, false), "a lens in the joists");
            Assert.IsTrue(InteriorCamera.SphereClear(-1f, 1.8f, 14f, false));
            Assert.IsFalse(InteriorCamera.InAir(2f, 1.5f, 14f), "the workshop nook is not a camera place");
            CollectionAssert.Contains(InteriorCamera.BlockerNames, "Loft_FrontRail", "collider slabs are solids");
            CollectionAssert.Contains(InteriorCamera.BlockerNames, "Stair_Balustrade");
        }
    }
}
