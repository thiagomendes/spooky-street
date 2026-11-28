using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Animation;

namespace SpookyStreet.Tests.Core
{
    public class PipClipsTests
    {
        [TestCase("Armature|Idle", "Idle")]
        [TestCase("Pip|Armature|Run", "Run")]
        [TestCase("Armature|Glide|BaseLayer", "Glide")]
        [TestCase("  Armature|WallSlide ", "WallSlide")]
        [TestCase("JumpStart", "JumpStart")]
        [TestCase("Armature|", "Armature")]
        [TestCase("", "")]
        [TestCase(null, "")]
        public void CleansExporterTakeNames(string take, string expected)
        {
            Assert.AreEqual(expected, PipClips.CleanClipName(take));
        }

        [Test]
        public void HintGlancePicksTheSideThePlaceIsOn()
        {
            // Unity axes on the ground: x right, z forward. Facing +z, a place at +x is to her right.
            Assert.AreEqual(PipClips.GlanceRight, PipClips.GlanceToward(0f, 1f, 3f, 1f));
            Assert.AreEqual(PipClips.GlanceLeft, PipClips.GlanceToward(0f, 1f, -3f, 1f));
            // Facing -x (west), north (+z) is to her right and south to her left.
            Assert.AreEqual(PipClips.GlanceRight, PipClips.GlanceToward(-1f, 0f, 0f, 5f));
            Assert.AreEqual(PipClips.GlanceLeft, PipClips.GlanceToward(-1f, 0f, 0f, -5f));
            // Behind her and off to one side still picks that side.
            Assert.AreEqual(PipClips.GlanceLeft, PipClips.GlanceToward(0f, 1f, -0.5f, -4f));
            Assert.AreEqual(PipClips.GlanceRight, PipClips.GlanceToward(0f, 1f, 0.5f, -4f));
            // No direction, no glance.
            Assert.IsNull(PipClips.GlanceToward(0f, 0f, 1f, 1f));
            Assert.IsNull(PipClips.GlanceToward(0f, 1f, 0f, 0f));

            Assert.AreEqual(90f, PipClips.GlanceAngle(0f, 1f, 2f, 0f), 1e-3f);
            Assert.AreEqual(-90f, PipClips.GlanceAngle(0f, 1f, -2f, 0f), 1e-3f);
            Assert.AreEqual(0f, PipClips.GlanceAngle(0f, 1f, 0f, 7f), 1e-3f);
            Assert.AreEqual(180f, PipClips.GlanceAngle(0f, 1f, 0f, -7f), 1e-3f);
            Assert.AreEqual(45f, PipClips.GlanceAngle(1f, 0f, 1f, -1f), 1e-3f);
            Assert.AreEqual(0f, PipClips.GlanceAngle(0f, 0f, 1f, 0f));
            for (float a = -175f; a <= 175f; a += 10f)
            {
                float rad = a * (float)System.Math.PI / 180f;
                float x = (float)System.Math.Sin(rad), z = (float)System.Math.Cos(rad);
                Assert.AreEqual(a, PipClips.GlanceAngle(0f, 1f, x, z), 1e-2f);
                Assert.AreEqual(a < 0f ? PipClips.GlanceLeft : PipClips.GlanceRight, PipClips.GlanceToward(0f, 1f, x, z));
            }

            Assert.IsTrue(PipClips.IsGlance(PipClips.GlanceLeft));
            Assert.IsTrue(PipClips.IsGlance("Pip_Rig|GlanceRight"));
            Assert.IsFalse(PipClips.IsGlance(PipClips.Interact));
            Assert.IsFalse(PipClips.IsGlance(null));
            // A model without the glances just does not glance: nothing stands in for them.
            Assert.IsNull(PipClips.Fallback(PipClips.GlanceLeft));
            Assert.IsNull(PipClips.Fallback(PipClips.GlanceRight));
            Assert.That(PipClips.GlanceTurn, Is.InRange(60f, 100f));
        }

        [Test]
        public void LoopingClipsMatchTheExportContract()
        {
            var loops = new[] { "Idle", "Walk", "Run", "JumpAir", "Fall", "Glide", "WallSlide", "LedgeHang" };
            var once = new[]
            {
                "JumpStart", "Land", "Roll", "Interact", "WallKick", "LedgeGrab", "LedgeClimb", "GlanceLeft", "GlanceRight",
                "HandTreat",
            };
            foreach (var name in loops)
            {
                Assert.IsTrue(PipClips.IsLooping(name), name);
                Assert.IsTrue(PipClips.IsLooping("Armature|" + name), "prefixed " + name);
            }

            foreach (var name in once)
            {
                Assert.IsFalse(PipClips.IsLooping(name), name);
            }

            Assert.IsFalse(PipClips.IsLooping("Take 001"));
            CollectionAssert.AreEquivalent(loops, PipClips.Looping);
            var all = new List<string>(loops);
            all.AddRange(once);
            CollectionAssert.AreEquivalent(all, PipClips.All);
            // The animator's mixer inputs follow this order, so the S003 clips were added at the end.
            Assert.AreEqual("Interact", PipClips.All[11]);
            Assert.AreEqual("WallKick", PipClips.All[12]);
            Assert.AreEqual("LedgeGrab", PipClips.All[13]);
            Assert.AreEqual("LedgeClimb", PipClips.All[14]);
            // ... and the S003a hint glances after them.
            Assert.AreEqual("GlanceLeft", PipClips.All[15]);
            Assert.AreEqual("GlanceRight", PipClips.All[16]);
            // ... and the living street's hand-out (a missing one is skipped; the street plays Interact instead).
            Assert.AreEqual("HandTreat", PipClips.All[17]);
            Assert.IsNull(PipClips.Fallback(PipClips.HandTreat));
            Assert.AreEqual(18, PipClips.All.Length);
        }

        [Test]
        public void FallbackChainsEndAtIdleOrNothing()
        {
            foreach (var name in PipClips.All)
            {
                int guard = 0;
                string last = name;
                for (string n = PipClips.Fallback(name); n != null; n = PipClips.Fallback(n))
                {
                    last = n;
                    Assert.Less(++guard, PipClips.All.Length, "fallback loop from " + name);
                }

                Assert.IsTrue(last == PipClips.Idle || PipClips.Fallback(name) == null, name + " ends at " + last);
            }
        }

        [Test]
        public void ResolvePicksTheFirstPresentStandIn()
        {
            var present = new HashSet<string> { "Idle", "Walk", "JumpAir" };
            Assert.AreEqual("Walk", PipClips.Resolve("Run", present.Contains));
            Assert.AreEqual("JumpAir", PipClips.Resolve("JumpStart", present.Contains));
            Assert.AreEqual("Idle", PipClips.Resolve("Fall", present.Contains));
            Assert.AreEqual("Idle", PipClips.Resolve("Glide", present.Contains));
            Assert.IsNull(PipClips.Resolve("Roll", present.Contains));
            Assert.IsNull(PipClips.Resolve("Land", present.Contains));
        }

        [Test]
        public void WallAndLedgeClipsStandInAsBeforeTheyExisted()
        {
            // Without the S003 clips the take-off stands in for the kick and the climb (here through its own
            // stand-in, JumpAir), and a catch goes straight to the hang.
            var present = new HashSet<string> { "Idle", "JumpAir", "LedgeHang" };
            Assert.AreEqual("JumpAir", PipClips.Resolve(PipClips.WallKick, present.Contains));
            Assert.AreEqual("JumpAir", PipClips.Resolve(PipClips.LedgeClimb, present.Contains));
            Assert.IsNull(PipClips.Resolve(PipClips.LedgeGrab, present.Contains));
            present.Add("JumpStart");
            Assert.AreEqual("JumpStart", PipClips.Resolve(PipClips.WallKick, present.Contains));
            present.Add("WallKick");
            present.Add("LedgeGrab");
            present.Add("LedgeClimb");
            Assert.AreEqual("WallKick", PipClips.Resolve(PipClips.WallKick, present.Contains));
            Assert.AreEqual("LedgeGrab", PipClips.Resolve(PipClips.LedgeGrab, present.Contains));
            Assert.AreEqual("LedgeClimb", PipClips.Resolve(PipClips.LedgeClimb, present.Contains));
            foreach (var name in new[] { PipClips.WallKick, PipClips.LedgeGrab, PipClips.LedgeClimb })
            {
                Assert.IsTrue(PipClips.IsKnown("Pip_Rig|" + name), name);
                Assert.IsFalse(PipClips.IsLooping("Pip_Rig|" + name), name + " plays once");
            }
        }
    }

    public class PipPoseTrackerTests
    {
        private const float Dt = 1f / 60f;

        private static PipPoseInput Ground(float speed = 0f) =>
            new PipPoseInput { Grounded = true, VerticalSpeed = -2f, HorizontalSpeed = speed };

        private static PipPoseInput Air(float vy, float speed = 0f) =>
            new PipPoseInput { Grounded = false, VerticalSpeed = vy, HorizontalSpeed = speed };

        [Test]
        public void JumpPlaysStartAirFallThenLand()
        {
            var tracker = new PipPoseTracker();
            Assert.AreEqual(PipPose.Locomotion, tracker.Step(Ground(), Dt));

            Assert.AreEqual(PipPose.JumpStart, tracker.Step(Air(7f), Dt));
            Assert.IsTrue(tracker.TookOff);

            var seen = new List<PipPose>();
            float vy = 7f;
            for (int i = 0; i < 60; i++)
            {
                vy -= 25f * Dt;
                seen.Add(tracker.Step(Air(vy), Dt));
            }

            CollectionAssert.Contains(seen, PipPose.JumpAir);
            Assert.AreEqual(PipPose.Fall, seen[seen.Count - 1]);
            Assert.Less(seen.IndexOf(PipPose.JumpAir), seen.IndexOf(PipPose.Fall));

            Assert.AreEqual(PipPose.Land, tracker.Step(Ground(), Dt));
            Assert.IsTrue(tracker.Landed);
            for (int i = 0; i < 30; i++)
            {
                tracker.Step(Ground(), Dt);
            }

            Assert.AreEqual(PipPose.Locomotion, tracker.Pose);
        }

        [Test]
        public void OneFrameStepDownShowsNoAirPoseAndNoLanding()
        {
            var tracker = new PipPoseTracker();
            tracker.Step(Ground(2f), Dt);
            Assert.AreEqual(PipPose.Locomotion, tracker.Step(Air(-2.4f, 2f), Dt));
            Assert.AreEqual(PipPose.Locomotion, tracker.Step(Ground(2f), Dt));
            Assert.IsFalse(tracker.Landed);
        }

        [Test]
        public void WalkingOffAnEdgeFallsWithoutJumpStart()
        {
            var tracker = new PipPoseTracker();
            tracker.Step(Ground(2f), Dt);
            float vy = -2f;
            PipPose pose = PipPose.Locomotion;
            for (int i = 0; i < 20; i++)
            {
                vy -= 25f * Dt;
                pose = tracker.Step(Air(vy, 2f), Dt);
                Assert.AreNotEqual(PipPose.JumpStart, pose);
            }

            Assert.AreEqual(PipPose.Fall, pose);
        }

        [Test]
        public void FastLandingGoesStraightBackToTheRun()
        {
            var tracker = new PipPoseTracker();
            tracker.Step(Ground(4f), Dt);
            for (int i = 0; i < 30; i++)
            {
                tracker.Step(Air(-5f, 4f), Dt);
            }

            Assert.AreEqual(PipPose.Locomotion, tracker.Step(Ground(4f), Dt));
            Assert.IsTrue(tracker.Landed, "the landing still happened, it is just not shown over the run");
        }

        [Test]
        public void MovesetHooksOverrideTheAirPose()
        {
            var tracker = new PipPoseTracker();
            tracker.Step(Ground(), Dt);
            var glide = Air(-1f);
            glide.Gliding = true;
            Assert.AreEqual(PipPose.Glide, tracker.Step(glide, Dt));
            var wall = Air(-1f);
            wall.WallSliding = true;
            wall.Gliding = true;
            Assert.AreEqual(PipPose.WallSlide, tracker.Step(wall, Dt));
            var hang = Air(0f);
            hang.LedgeHanging = true;
            Assert.AreEqual(PipPose.LedgeHang, tracker.Step(hang, Dt));
            var groundedGlide = Ground();
            groundedGlide.Gliding = true;
            Assert.AreNotEqual(PipPose.Glide, tracker.Step(groundedGlide, Dt), "no gliding on the ground");
        }

        [Test]
        public void PausedFramesChangeNothing()
        {
            var tracker = new PipPoseTracker();
            tracker.Step(Ground(), Dt);
            Assert.AreEqual(PipPose.Locomotion, tracker.Step(Air(7f), 0f));
            Assert.IsFalse(tracker.TookOff);
        }

        [TestCase(0f)]
        [TestCase(0.5f)]
        [TestCase(1.6f)]
        [TestCase(2.8f)]
        [TestCase(4f)]
        [TestCase(9f)]
        public void LocomotionWeightsSumToOne(float speed)
        {
            var w = PipPoseTracker.Locomotion(speed, 1.6f, 4f);
            Assert.AreEqual(1f, w.Idle + w.Walk + w.Run, 1e-5f);
            Assert.GreaterOrEqual(w.Idle, 0f);
            Assert.GreaterOrEqual(w.Walk, 0f);
            Assert.GreaterOrEqual(w.Run, 0f);
        }

        [Test]
        public void LocomotionBlendsIdleWalkRunInOrder()
        {
            Assert.AreEqual(1f, PipPoseTracker.Locomotion(0f, 1.6f, 4f).Idle, 1e-5f);
            Assert.AreEqual(1f, PipPoseTracker.Locomotion(1.6f, 1.6f, 4f).Walk, 1e-5f);
            Assert.AreEqual(1f, PipPoseTracker.Locomotion(4f, 1.6f, 4f).Run, 1e-5f);
            var mid = PipPoseTracker.Locomotion(2.8f, 1.6f, 4f);
            Assert.AreEqual(0.5f, mid.Walk, 1e-4f);
            Assert.AreEqual(0.5f, mid.Run, 1e-4f);
        }

        [Test]
        public void PlaybackRateMatchesFeetToGroundWithinLimits()
        {
            Assert.AreEqual(1f, PipPoseTracker.PlaybackRate(1.6f, 1.6f), 1e-5f);
            Assert.AreEqual(4f / 4.5f, PipPoseTracker.PlaybackRate(4f, 4.5f), 1e-5f);
            Assert.AreEqual(0.5f, PipPoseTracker.PlaybackRate(0.1f, 1.6f), 1e-5f);
            Assert.AreEqual(1.6f, PipPoseTracker.PlaybackRate(10f, 1.6f), 1e-5f);
        }
    }
}
