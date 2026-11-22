using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpookyStreet.Core.World;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// The indoor camera (S002 interior polish, house v3): wherever Pip can stand in the owl house interior (hall, stair,
    /// attic) and in the treehouse cabin, and however the player has turned the camera, the solved lens sits in room
    /// air, touches no solid, and has clear lines to Pip's head and chest (and to the character being talked to). The
    /// bigger rooms give the boom room: at least 1.8 m in the hall and the attic, 1.0 m in the cabin (D24).
    /// </summary>
    public class InteriorCameraTests
    {
        private const float PivotHeight = 0.95f;
        private const float DefaultPitch = 4f;
        private const float Ifz = CaseRouteLayout.InteriorFrontZ;
        private static readonly float[] Yaws = { 0f, 45f, 90f, 135f, 180f, 225f, 270f, 315f };

        /// <summary>
        /// The shortest boom each place must give at every yaw: 1.8 m on the open floor of the hall and the attic
        /// (D24's bigger rooms: at least <see cref="OpenFloorClearance"/> from any wall, rail or furniture), and the
        /// 1.3 m that frames all of Pip right against them, on the stair and in the dormer alcove; 1.0 m in the cabin.
        /// </summary>
        private static float RequiredBoom(string room, float x, float y, float z)
        {
            if (room == "cabin")
            {
                return 1.0f;
            }

            bool open = room != "stair" && InteriorCamera.RoomOf(x, y, z) != InteriorRoom.Alcove && OpenFloor(x, y, z);
            return open ? 1.8f : InteriorCamera.MinDistance;
        }

        /// <summary>How far from every interior wall, rail and piece of furniture the open floor starts.</summary>
        private const float OpenFloorClearance = 1.0f;

        private static bool OpenFloor(float x, float y, float z)
        {
            foreach (var slab in CaseRouteLayout.Slabs)
            {
                if (slab.Group == SlabGroup.Interior && slab.Kind == SlabKind.Wall &&
                    slab.Blocks(x, z, y + 0.2f, y + 2.4f, OpenFloorClearance))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>Standing places: hall, attic, stair and cabin (sampled every 0.3 m).</summary>
        public static IEnumerable<(string Room, float X, float Y, float Z)> StandingPoints()
        {
            const float floor = CaseRouteLayout.FloorY;
            const float loft = CaseRouteLayout.LoftY;
            for (float x = -3.3f; x <= 3.31f; x += 0.3f)
            {
                for (float by = 0.45f; by <= 6.71f; by += 0.3f)
                {
                    float z = Ifz + by;
                    if (Math.Abs(CaseRouteLayout.GroundAt(x, z, floor + 0.05f) - floor) < 0.01f && Standable(x, floor, z) &&
                        InteriorCamera.RoomOf(x, floor, z) == InteriorRoom.Hall)
                    {
                        yield return ("hall", x, floor, z);
                    }

                    bool onLoft = Math.Abs(CaseRouteLayout.GroundAt(x, z, loft + 0.05f) - loft) < 0.01f;
                    bool headroom = InteriorCamera.RoofAt(x) > loft + CaseRouteLayout.PipHeight + 0.05f;
                    if (onLoft && headroom && Standable(x, loft, z))
                    {
                        yield return ("attic", x, loft, z);
                    }
                }
            }

            for (float by = 3.1f; by <= 5.4f; by += 0.3f)
            {
                float z = Ifz + by;
                yield return ("stair", -2.85f, CaseRouteLayout.LowerFlightAt(z), z);
            }

            foreach (float x in new[] { -3.2f, -2.6f })
            {
                foreach (float by in new[] { 5.8f, 6.5f })
                {
                    yield return ("stair", x, CaseRouteLayout.LandingY, Ifz + by);
                }
            }

            for (float x = -1.9f; x <= -0.05f; x += 0.3f)
            {
                yield return ("stair", x, CaseRouteLayout.UpperFlightAt(x), CaseRouteLayout.UpperFlightZ);
            }

            foreach (var p in CabinPoints())
            {
                yield return ("cabin", p.X, CaseRouteLayout.TreehouseY, p.Z);
            }
        }

        /// <summary>Where Pip can stand in the cabin: inside its walls, clear of the trunk and the seat.</summary>
        public static IEnumerable<(float X, float Z)> CabinPoints()
        {
            float deck = CaseRouteLayout.TreehouseY;
            for (float cx = -0.8f; cx <= 0.81f; cx += 0.3f)
            {
                for (float cy = -0.8f; cy <= 1.41f; cy += 0.3f)
                {
                    CaseRouteLayout.CabinToWorld(CaseRouteLayout.CabinPlatX + cx, CaseRouteLayout.CabinPlatY + cy,
                        out float x, out float z);
                    if (CaseRouteLayout.Blocking(x, deck, z) == null &&
                        !InteriorCamera.TouchesSolid(x, deck + 0.55f, z, CaseRouteLayout.PipRadius, false) &&
                        !InteriorCamera.TouchesSolid(x, deck + 0.25f, z, CaseRouteLayout.PipRadius, false))
                    {
                        yield return (x, z);
                    }
                }
            }
        }

        private static bool Standable(float x, float y, float z) =>
            CaseRouteLayout.Inside(x, z) && CaseRouteLayout.GroundAt(x, z, y + 3.2f) < y + 0.2f &&
            CaseRouteLayout.Blocking(x, y, z) == null &&
            CaseRouteLayout.Blocking(x + 0.1f, y, z) == null && CaseRouteLayout.Blocking(x - 0.1f, y, z) == null &&
            CaseRouteLayout.Blocking(x, y, z + 0.1f) == null && CaseRouteLayout.Blocking(x, y, z - 0.1f) == null &&
            !Thresholds.NearAnyZone(x, y, z, 0f);

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

        private static string Problem(InteriorShot shot, float x, float y, float z, bool doorOpen, float required)
        {
            if (!shot.Active)
            {
                return "inactive";
            }

            if (!shot.Clear || shot.Distance < required - 1e-3f)
            {
                return "tight " + shot;
            }

            if (!InteriorCamera.SphereClear(shot.CameraX, shot.CameraY, shot.CameraZ, doorOpen, InteriorCamera.Radius - 0.01f))
            {
                return "lens in a solid or out of the room " + shot;
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
            var counts = new Dictionary<string, int>();
            foreach (var p in StandingPoints())
            {
                bool house = p.Room != "cabin";
                foreach (float yaw in Yaws)
                {
                    foreach (bool door in house ? new[] { false, true } : new[] { false })
                    {
                        if (InteriorCamera.TouchesSolid(p.X, p.Y + 0.55f, p.Z, 0.15f, door) ||
                            InteriorCamera.TouchesSolid(p.X, p.Y + 0.9f, p.Z, 0.15f, door))
                        {
                            continue; // Pip cannot stand in the dressing (or in the swung-open door leaf)
                        }

                        counts[p.Room] = counts.TryGetValue(p.Room, out int n) ? n + 1 : 1;
                        var shot = Solve(p.X, p.Y, p.Z, yaw, door);
                        string problem = Problem(shot, p.X, p.Y, p.Z, door, RequiredBoom(p.Room, p.X, p.Y, p.Z));
                        if (problem != null)
                        {
                            failures.Add($"{p.Room} ({p.X:F2}, {p.Y:F2}, {p.Z:F2}) yaw {yaw} door {door}: {problem}");
                        }
                    }
                }
            }

            Assert.Greater(counts["hall"], 800, "the sweep covers the hall");
            Assert.Greater(counts["attic"], 800, "the sweep covers the attic");
            Assert.Greater(counts["stair"], 150, "the sweep covers the stair");
            Assert.Greater(counts["cabin"], 40, "the sweep covers the cabin");
            int total = counts.Values.Sum();
            Assert.IsEmpty(failures, $"{failures.Count}/{total} shots:\n" + string.Join("\n", failures.Take(60)));
        }

        [Test]
        public void TheRoomsAreSampled()
        {
            var rooms = StandingPoints().Select(p => p.Room).Distinct().ToList();
            CollectionAssert.AreEquivalent(new[] { "hall", "attic", "stair", "cabin" }, rooms);
        }

        [Test]
        public void TheBiggerRoomsFitTheBoom()
        {
            // D24: the hall and attic hold the 2.0–2.4 m boom; the cabin asks for 1.0–1.2 m.
            Assert.That(InteriorCamera.ProfileFor(InteriorRoom.Hall).Distance, Is.InRange(2.0f, 2.4f));
            Assert.That(InteriorCamera.ProfileFor(InteriorRoom.Attic).Distance, Is.InRange(2.0f, 2.4f));
            Assert.That(InteriorCamera.ProfileFor(InteriorRoom.Cabin).Distance, Is.InRange(1.0f, 1.2f));
            var hall = StandingPoints().Where(p => p.Room == "hall").ToList();
            var attic = StandingPoints().Where(p => p.Room == "attic").ToList();
            float Mean(List<(string Room, float X, float Y, float Z)> points) =>
                points.SelectMany(p => Yaws.Select(yaw => Solve(p.X, p.Y, p.Z, yaw).Distance)).Average();
            Assert.Greater(Mean(hall), 2.0f, "most hall shots get (nearly) the whole boom");
            Assert.Greater(Mean(attic), 1.9f, "most attic shots get (nearly) the whole boom");
        }

        [Test]
        public void OpenRoomsKeepThePlayersFraming()
        {
            // Middle of the attic and of the hall: nothing to dodge, so no yaw swing and the full boom.
            var attic = Solve(0.6f, CaseRouteLayout.LoftY, Ifz + 3.2f, 0f);
            Assert.AreEqual(InteriorRoom.Attic, attic.Room);
            Assert.AreEqual(0f, attic.YawOffset, 1e-3f, attic.ToString());
            Assert.AreEqual(InteriorCamera.ProfileFor(InteriorRoom.Attic).Distance, attic.Distance, 1e-3f);
            var hall = Solve(0f, CaseRouteLayout.FloorY, Ifz + 2.2f, 180f);
            Assert.AreEqual(InteriorRoom.Hall, hall.Room);
            Assert.AreEqual(0f, hall.YawOffset, 1e-3f, hall.ToString());
            Assert.AreEqual(InteriorCamera.ProfileFor(InteriorRoom.Hall).Distance, hall.Distance, 1e-3f);
            Assert.Less(hall.CameraY, InteriorCamera.HallCeilingY + 1e-3f, "under the hall ceiling");
        }

        [Test]
        public void TheDoorShotFramesTheDoorFromTheHall()
        {
            // The hall-and-door pose: Pip in the hall facing the open door, the camera behind in the hall.
            var shot = Solve(0f, CaseRouteLayout.FloorY, Ifz + 1.8f, 180f, true);
            Assert.IsTrue(shot.Clear, shot.ToString());
            Assert.GreaterOrEqual(shot.Distance, 2.0f, shot.ToString());
            Assert.Greater(shot.CameraZ, Ifz + 1.8f, "the lens is behind Pip, looking at the door");
        }

        [Test]
        public void TheAlcoveTurnsTheCameraIntoTheRoom()
        {
            // Just in by the window, facing into the attic: behind Pip is the window, so the lens swings into the room.
            var shot = Solve(-3.0f, CaseRouteLayout.LoftY, CaseRouteLayout.InnerWindowZ, 90f);
            Assert.AreEqual(InteriorRoom.Alcove, shot.Room);
            Assert.IsTrue(shot.Clear, shot.ToString());
            Assert.Greater(shot.CameraX, CaseRouteLayout.InnerMinX + InteriorCamera.Radius, "the lens is inside the house");
            // At the dormer arrival point, facing in, the full boom fits behind Pip in the alcove.
            var arrival = Thresholds.DormerInside;
            var arrive = Solve(arrival.ArriveX, arrival.ArriveY, arrival.ArriveZ, arrival.ArriveYaw);
            Assert.IsTrue(arrive.Clear, arrive.ToString());
            Assert.GreaterOrEqual(arrive.Distance, 1.8f, arrive.ToString());
        }

        [Test]
        public void TalkingKeepsTheCharacterInView()
        {
            var june = SpookyStreet.Core.Case.CaseStage.JuneAttic.Position;
            foreach (var pip in new[] { (-2.5f, Ifz + 1.5f), (-0.2f, Ifz + 2.4f), (-0.6f, Ifz + 3.8f), (-2.0f, Ifz + 2.4f) })
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
        public void OutsideTheRoomsTheSolverStandsDown()
        {
            Assert.IsFalse(Solve(0f, 0f, 0f, 0f).Active);
            Assert.IsFalse(Solve(-4f, CaseRouteLayout.ExteriorSillY, CaseRouteLayout.ExteriorWindowZ, 90f).Active, "on the bridge");
            Assert.IsFalse(Solve(0f, CaseRouteLayout.FloorY, CaseRouteLayout.FrontZ - 1f, 0f).Active, "on the porch");
            Assert.IsFalse(Solve(-6.6f, CaseRouteLayout.TreehouseY, 13.4f, 0f).Active, "on the shed roof");
        }

        [Test]
        public void TheProbeStopsAtTheFirstSolid()
        {
            // From the hall shoulder straight back (−Z, toward the front wall) the wall stops the boom before it.
            float y = CaseRouteLayout.FloorY + PivotHeight + 0.1f;
            float z = Ifz + 1.2f;
            float d = InteriorCamera.ClearDistance(1.5f, y, z, 0f, 0f, 3f, 1.5f, CaseRouteLayout.FloorY, z, false);
            Assert.Less(z - d, CaseRouteLayout.InnerMinZ + InteriorCamera.Radius + 0.1f);
            Assert.Greater(z - d, CaseRouteLayout.InnerMinZ + InteriorCamera.Radius - 0.01f, "never into the wall");
            Assert.Greater(d, 0.6f);
        }

        [Test]
        public void TheCeilingsBeamsAndNookAreNotCameraPlaces()
        {
            Assert.IsFalse(InteriorCamera.SphereClear(0.5f, 3.75f, Ifz + 2f, false), "a lens in the hall's beams");
            Assert.IsTrue(InteriorCamera.SphereClear(1f, 2.6f, Ifz + 2f, false), "the hall is roomy");
            Assert.IsFalse(InteriorCamera.InAir(3f, 1.5f, CaseRouteLayout.MechanismZ), "the workshop nook is not a camera place");
            Assert.IsTrue(InteriorCamera.InAir(-2.85f, 3.9f, Ifz + 4.5f), "the stairwell is open to the roof");
            Assert.IsFalse(InteriorCamera.InAir(-2.85f, 1.2f, Ifz + 5f), "the space under the stair is boxed in");
            Assert.IsFalse(InteriorCamera.InAir(0f, 7.0f, Ifz + 3f), "under the collar ties");
            Assert.IsFalse(InteriorCamera.InAir(-5f, 4.5f, CaseRouteLayout.InnerWindowZ), "outside the window");
            CollectionAssert.Contains(InteriorCamera.BlockerNames, "Loft_RailNorth", "collider slabs are solids");
            CollectionAssert.Contains(InteriorCamera.BlockerNames, "Stair_Side1");
            CollectionAssert.Contains(InteriorCamera.BlockerNames, "Cabin_Trunk0");
        }

        [Test]
        public void TheCabinHasItsOwnShortProfile()
        {
            CaseRouteLayout.CabinToWorld(CaseRouteLayout.CabinPlatX - 0.6f, CaseRouteLayout.CabinPlatY + 0.9f, out float x,
                out float z);
            Assert.AreEqual(InteriorRoom.Cabin, InteriorCamera.RoomOf(x, CaseRouteLayout.TreehouseY, z));
            Assert.IsFalse(InteriorCamera.InAir(x, CaseRouteLayout.TreehouseY + 2.4f, z), "the roof closes the cabin");
            CaseRouteLayout.CabinToWorld(CaseRouteLayout.CabinPlatX - 1.6f, CaseRouteLayout.CabinPlatY - 0.6f, out float ox,
                out float oz);
            Assert.AreEqual(InteriorRoom.None, InteriorCamera.RoomOf(ox, CaseRouteLayout.TreehouseY, oz), "outside the door");
            Assert.IsFalse(InteriorCamera.InAir(ox, CaseRouteLayout.TreehouseY + 1f, oz), "the lens never leaves the cabin");
        }
    }
}
