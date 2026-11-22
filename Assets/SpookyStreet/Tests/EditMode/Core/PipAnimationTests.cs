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
        public void LoopingClipsMatchTheExportContract()
        {
            var loops = new[] { "Idle", "Walk", "Run", "JumpAir", "Fall", "Glide", "WallSlide", "LedgeHang" };
            var once = new[] { "JumpStart", "Land", "Roll", "Interact" };
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
            CollectionAssert.AreEquivalent(new List<string>(loops) { "JumpStart", "Land", "Roll", "Interact" },
                PipClips.All);
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
