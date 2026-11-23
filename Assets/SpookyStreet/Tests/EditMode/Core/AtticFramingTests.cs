using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.World;
using Lines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// S002 attic framing. Right after the dormer arrival (frame 37) the lens sat in the alcove looking east along the
    /// front gable 1.4 m away, and in the owner's frame (39) a lens 0.7 m from the same wall looked west along it: a
    /// plain plank wall smeared over a third of the frame. <see cref="InteriorCamera.FrameCover"/> measures that (the
    /// share of a 24 × 12 screen grid where the first surface is architecture closer than 1.0 m, or a wall or slope
    /// within 3.2 m seen at a glancing angle), and the solver pays for it, so D24's "Pip and the room, no
    /// architecture filling the frame" holds over the whole interior. Also the indoor perception of the arrival knocks.
    /// </summary>
    public class AtticFramingTests
    {
        private const float PivotHeight = 0.95f;
        private const float DefaultPitch = 4f;
        private const float Ifz = CaseRouteLayout.InteriorFrontZ;

        /// <summary>The run-101 lenses: frame 37 (gameplay, after the page) and frame 39 (the owner's TestFlight 23 lens).</summary>
        private static readonly (float X, float Y, float Z, float Yaw, float Pitch) Frame37 = (-3.54f, 5.07f, Ifz + 1.5f, 90f, 4f);

        private static readonly (float X, float Y, float Z, float Yaw, float Pitch) Frame39 = (0.16f, 6.3f, Ifz + 0.86f, -80f, 39f);

        /// <summary>Pip by the trunk at the front gable, facing west (the owner's frame).</summary>
        private static readonly (float X, float Y, float Z) ByTheTrunk = (-1.5f, CaseRouteLayout.LoftY + 0.03f, Ifz + 1.15f);

        private static InteriorShot Solve(float x, float y, float z, float yaw, bool doorOpen = false) =>
            InteriorCamera.Solve(new InteriorCameraRequest
            {
                PipX = x,
                PipY = y,
                PipZ = z,
                Yaw = yaw,
                Pitch = DefaultPitch,
                PivotHeight = PivotHeight,
                DoorOpen = doorOpen,
            });

        [Test]
        public void TheCoverCheckSeesTheWallFillingFrames37And39()
        {
            float f37 = InteriorCamera.FrameCover(Frame37.X, Frame37.Y, Frame37.Z, Frame37.Yaw, Frame37.Pitch, false);
            float f39 = InteriorCamera.FrameCover(Frame39.X, Frame39.Y, Frame39.Z, Frame39.Yaw, Frame39.Pitch, false);
            TestContext.Progress.WriteLine($"[AtticFramingTests] run 101 frame cover: 37 {f37:P0}, 39 {f39:P0}");
            Assert.Greater(f37, 0.2f, "frame 37: the front gable along the right of the frame");
            Assert.Greater(f39, 0.25f, "frame 39: the front gable along the left of the frame");
            // Neither wall is near the lens: no surface within 0.4 m, so the plain near-distance check misses both.
            Assert.AreEqual(0f, InteriorCamera.NearCover(Frame37.X, Frame37.Y, Frame37.Z, Frame37.Yaw, Frame37.Pitch, false, 0.4f));
            Assert.AreEqual(0f, InteriorCamera.NearCover(Frame39.X, Frame39.Y, Frame39.Z, Frame39.Yaw, Frame39.Pitch, false, 0.4f));
            // Looking across the attic at the front gable from its middle is the room, not a slab.
            var across = Solve(0.4f, CaseRouteLayout.LoftY, Ifz + 3.0f, 180f);
            Assert.Less(InteriorCamera.FrameCover(across, 180f, false), 0.06f, across.ToString());
        }

        [Test]
        public void TheDormerArrivalLooksIntoTheRoom()
        {
            var a = Thresholds.DormerInside;
            var shot = Solve(a.ArriveX, a.ArriveY, a.ArriveZ, a.ArriveYaw);
            float cover = InteriorCamera.FrameCover(shot, a.ArriveYaw, false);
            TestContext.Progress.WriteLine($"[AtticFramingTests] dormer arrival: {shot}, frame cover {cover:P0}");
            Assert.IsTrue(shot.Clear, shot.ToString());
            Assert.GreaterOrEqual(shot.Distance, InteriorCamera.ComfortDistance, shot.ToString());
            Assert.LessOrEqual(cover, 0.12f, "no wall fills the arrival frame: " + shot);
            Assert.Less(shot.YawOffset, 0f, "the lens turns to look north-east, into the attic, not along the gable");
            Assert.IsTrue(InteriorSafety.InsideShell(shot.CameraX, shot.CameraY, shot.CameraZ, InteriorSafety.ShellMargin));
            Assert.IsFalse(InteriorCamera.TouchesSolid(shot.CameraX, shot.CameraY, shot.CameraZ, InteriorCamera.Radius - 0.01f, false),
                "clear of June's window shutter and every other solid");
        }

        [Test]
        public void PipByTheTrunkFacingWestIsFramedWithoutTheGable()
        {
            var p = ByTheTrunk;
            var shot = Solve(p.X, p.Y, p.Z, 270f);
            float cover = InteriorCamera.FrameCover(shot, 270f, false);
            TestContext.Progress.WriteLine($"[AtticFramingTests] by the trunk facing west: {shot}, frame cover {cover:P0}");
            Assert.IsTrue(shot.Clear, shot.ToString());
            Assert.LessOrEqual(cover, 0.12f, shot.ToString());
            Assert.Greater(shot.CameraZ, p.Z + 0.5f, "the lens sits north of Pip, away from the gable");
        }

        [Test]
        public void JunesWindowShutterIsACameraSolid()
        {
            CollectionAssert.Contains(InteriorCamera.BlockerNames, "Attic_WindowShutter");
            // The leaf swung back off the north jamb, at sill height (no collider; the lens used to sit against it).
            float z = CaseRouteLayout.InnerWindowZ + CaseRouteLayout.WindowHalfWidth + 0.05f;
            Assert.IsTrue(InteriorCamera.TouchesSolid(CaseRouteLayout.InnerMinX + 0.25f, CaseRouteLayout.LoftY + 0.6f, z, 0.01f, false));
        }

        /// <summary>
        /// Every place Pip can stand in the hall, the attic and the alcove, at four yaws: no solved lens has architecture
        /// within 0.4 m over more than a post's edge, and hardly any shot has a wall filling a fifth of the frame (with
        /// the run-101 solver walls filled more than a fifth of about 900 of these shots: see docs/S002-attic-framing.md).
        /// </summary>
        [Test]
        public void NoSolvedShotHasAWallFillingTheFrame()
        {
            var cover = new Dictionary<string, List<float>>();
            var worst = new List<(float Cover, string Shot)>();
            float near = 0f;
            foreach (var p in InteriorCameraTests.StandingPoints())
            {
                if (p.Room == "cabin" || InteriorCamera.TouchesSolid(p.X, p.Y + 0.55f, p.Z, 0.15f, false) ||
                    InteriorCamera.TouchesSolid(p.X, p.Y + 0.9f, p.Z, 0.15f, false))
                {
                    continue; // Pip cannot stand in the dressing
                }

                foreach (float yaw in new[] { 0f, 90f, 180f, 270f })
                {
                    var shot = Solve(p.X, p.Y, p.Z, yaw);
                    string room = shot.Room.ToString();
                    float c = InteriorCamera.FrameCover(shot, yaw, false);
                    if (!cover.TryGetValue(room, out var list))
                    {
                        cover[room] = list = new List<float>();
                    }

                    list.Add(c);
                    worst.Add((c, $"{p.Room} ({p.X:F2}, {p.Y:F2}, {p.Z:F2}) yaw {yaw}: {shot} cover {c:P0}"));
                    near = Math.Max(near, InteriorCamera.NearCover(shot, yaw, false, 0.4f));
                }
            }

            foreach (var kv in cover)
            {
                TestContext.Progress.WriteLine($"[AtticFramingTests] {kv.Key}: {kv.Value.Count} shots, mean frame cover " +
                                               $"{kv.Value.Average():P1}, over 20 % {kv.Value.Count(c => c > 0.2f)}, " +
                                               $"max {kv.Value.Max():P0}");
            }

            foreach (var w in worst.OrderByDescending(w => w.Cover).Take(5))
            {
                TestContext.Progress.WriteLine("  " + w.Shot);
            }

            Assert.LessOrEqual(near, 0.05f, "no lens has more than a post's edge within 0.4 m in view");
            foreach (string room in new[] { "Hall", "Attic" })
            {
                var list = cover[room];
                Assert.Less(list.Count(c => c > 0.2f), list.Count * 0.03f, room + ": hardly a wall fills a fifth");
                Assert.Less(list.Max(), 0.35f, room);
                Assert.Less(list.Average(), 0.08f, room);
            }

            // The alcove is the dormer's 1.5 m wide nook: its cheeks show, but never as a third of the frame.
            Assert.Less(cover["Alcove"].Max(), 0.4f);
            Assert.Less(cover["Alcove"].Average(), 0.15f);

            Assert.Less(cover["Stair"].Average(), 0.3f, "the stair's flight walls always show, but not as slabs");
        }

        // ------------------------------------------------------------------------------------------- the knocks

        [Test]
        public void IndoorsTheKnocksAreHeardThroughTheFloorFromTheWorkshop()
        {
            var a = Thresholds.DormerInside;
            Assert.IsTrue(CaptionPerception.Indoors(a.ArriveX, a.ArriveZ), "the attic is indoors");
            Assert.IsFalse(CaptionPerception.Indoors(0f, -13.4f), "the arrival porch is the street");
            // On the street: the knocks from the blue house, pointing at its attic window.
            Assert.AreEqual(Lines.CaptionArrivalKnocks, CaptionPerception.KeyFor(Lines.CaptionArrivalKnocks, false));
            Assert.AreEqual(SoundSource.AtticWindow, CaptionPerception.SourceFor(Lines.CaptionArrivalKnocks, false));
            // Inside the blue house: the floor hum, pointing at the workshop below, never "from the blue house".
            Assert.AreEqual(Lines.CaptionFoyerHum, CaptionPerception.KeyFor(Lines.CaptionArrivalKnocks, true));
            Assert.AreEqual(SoundSource.Workshop, CaptionPerception.SourceFor(Lines.CaptionArrivalKnocks, true));
            Assert.AreEqual(SoundSource.Workshop, CaptionPerception.SourceFor(Lines.CaptionFoyerHum, true));
            // Other lines are untouched.
            Assert.AreEqual(Lines.CaptionOwlBows, CaptionPerception.KeyFor(Lines.CaptionOwlBows, true));
            Assert.AreEqual(SoundSource.None, CaptionPerception.SourceFor(Lines.CaptionOwlBows, true));
            // A sound caption does not survive a threshold crossing; what Pip sees does.
            Assert.IsFalse(CaptionPerception.HeardAcrossThreshold(Lines.CaptionArrivalKnocks));
            Assert.IsFalse(CaptionPerception.HeardAcrossThreshold(Lines.CaptionFoyerHum));
            Assert.IsTrue(CaptionPerception.HeardAcrossThreshold(Lines.CaptionOwlBows));
        }
    }
}
