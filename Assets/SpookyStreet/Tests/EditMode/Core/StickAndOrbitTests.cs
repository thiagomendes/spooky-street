using NUnit.Framework;
using SpookyStreet.Core.Movement;

namespace SpookyStreet.Tests.Core
{
    public class StickAndOrbitTests
    {
        [Test]
        public void StickDeadZoneAndSaturation()
        {
            StickMath.Evaluate(5f, 0f, 100f, 0.1f, out float x, out float y);
            Assert.AreEqual(0f, x);
            Assert.AreEqual(0f, y);

            StickMath.Evaluate(300f, 0f, 100f, 0.1f, out x, out y);
            Assert.That(x, Is.EqualTo(1f).Within(1e-5f));

            StickMath.Evaluate(55f, 0f, 100f, 0.1f, out x, out _);
            Assert.That(x, Is.EqualTo(0.5f).Within(1e-5f), "dead zone is rescaled, not subtracted");
        }

        [Test]
        public void FloatingOriginTrailsFinger()
        {
            StickMath.FollowOrigin(0f, 0f, 150f, 0f, 100f, out float ox, out float oy);
            Assert.That(ox, Is.EqualTo(50f).Within(1e-4f));
            Assert.AreEqual(0f, oy);
        }

        [Test]
        public void ManualOrbitBlocksAutoRecenter()
        {
            var tuning = new CameraOrbitTuning();
            var orbit = new CameraOrbitModel(tuning, 0f);
            orbit.ApplyManual(90f, 0f);
            for (int i = 0; i < 60; i++)
            {
                orbit.Tick(1f / 60f, 4f, 0f); // 1 s of running, still inside the 1.5 s delay
            }

            Assert.That(orbit.Yaw, Is.EqualTo(90f).Within(1e-3f));
        }

        [Test]
        public void AutoRecenterStartsAfterDelayAndStableMovement()
        {
            var tuning = new CameraOrbitTuning();
            var orbit = new CameraOrbitModel(tuning, 90f);
            for (int i = 0; i < 180; i++)
            {
                orbit.Tick(1f / 60f, 4f, 0f);
            }

            Assert.Less(orbit.Yaw, 90f);
            orbit.ApplyManual(1f, 0f);
            Assert.IsFalse(orbit.AutoRecentering, "manual orbit wins immediately");
        }

        [Test]
        public void NoAutoRecenterWhileStanding()
        {
            var orbit = new CameraOrbitModel(new CameraOrbitTuning(), 90f);
            for (int i = 0; i < 300; i++)
            {
                orbit.Tick(1f / 60f, 0f, 0f);
            }

            Assert.That(orbit.Yaw, Is.EqualTo(90f).Within(1e-3f));
        }

        [Test]
        public void PitchIsClamped()
        {
            var tuning = new CameraOrbitTuning();
            var orbit = new CameraOrbitModel(tuning, 0f);
            orbit.ApplyManual(0f, 500f);
            Assert.AreEqual(tuning.MaxPitch, orbit.Pitch);
            orbit.ApplyManual(0f, -500f);
            Assert.AreEqual(tuning.MinPitch, orbit.Pitch);
        }

        [Test]
        public void ExplicitRecenterReturnsBehindCharacter()
        {
            var orbit = new CameraOrbitModel(new CameraOrbitTuning(), 170f);
            orbit.RequestRecenter();
            for (int i = 0; i < 60; i++)
            {
                orbit.Tick(1f / 60f, 0f, -170f);
            }

            Assert.That(CameraOrbitModel.DeltaAngle(orbit.Yaw, -170f), Is.EqualTo(0f).Within(0.5f));
        }

        [Test]
        public void SmallHeadingOffsetLeavesTheCameraStill()
        {
            // Thumb wobble sways Pip a few degrees; the lens must not chase it (that chase made her zigzag).
            var tuning = new CameraOrbitTuning();
            var orbit = new CameraOrbitModel(tuning, 0f);
            for (int i = 0; i < 300; i++)
            {
                orbit.Tick(1f / 60f, 4f, tuning.RecenterDeadZone - 1f);
            }

            Assert.That(orbit.Yaw, Is.EqualTo(0f).Within(1e-3f));
        }

        [Test]
        public void AutoRecenterIsProportionalAndCapped()
        {
            var tuning = new CameraOrbitTuning();
            var orbit = new CameraOrbitModel(tuning, 0f);
            for (int i = 0; i < 120; i++)
            {
                orbit.Tick(1f / 60f, 4f, 90f); // heading smoothing and the steady-movement wait settle
            }

            float before = orbit.Yaw;
            orbit.Tick(0.1f, 4f, 90f);
            float gap = 90f - before;
            float expected = System.Math.Min(tuning.RecenterSpeed, tuning.RecenterGain * (gap - tuning.RecenterDeadZone)) * 0.1f;
            Assert.That(orbit.Yaw - before, Is.EqualTo(expected).Within(0.5f), "turns faster the further off it is");
            Assert.LessOrEqual(orbit.Yaw - before, tuning.RecenterSpeed * 0.1f + 1e-3f, "never faster than RecenterSpeed");

            for (int i = 0; i < 600; i++)
            {
                orbit.Tick(1f / 60f, 4f, 90f);
            }

            Assert.That(CameraOrbitModel.DeltaAngle(orbit.Yaw, 90f), Is.EqualTo(0f).Within(tuning.RecenterDeadZone + 0.5f),
                "ends up behind her, within the dead zone");
        }

        [Test]
        public void StickMagnetPullsNearAxesOntoThem()
        {
            var tuning = new StickSteeringTuning();
            Assert.AreEqual(0f, StickSteering.Magnetize(tuning.SnapDegrees - 0.5f, tuning), 1e-4f, "near up is exactly up");
            Assert.AreEqual(90f, StickSteering.Magnetize(90f + tuning.SnapDegrees - 0.5f, tuning), 1e-4f);
            Assert.AreEqual(-135f, StickSteering.Magnetize(-135f - tuning.SnapDegrees + 0.5f, tuning), 1e-4f, "diagonals too");
            Assert.AreEqual(22.5f, StickSteering.Magnetize(22.5f, tuning), 1e-4f, "between magnets the aim is free");
            Assert.AreEqual(tuning.BlendDegrees, StickSteering.Magnetize(tuning.BlendDegrees, tuning), 1e-3f,
                "the pull fades out continuously");

            float previous = StickSteering.Magnetize(-180f, tuning);
            for (float a = -179.5f; a <= 180f; a += 0.5f)
            {
                float m = StickSteering.Magnetize(a, tuning);
                Assert.GreaterOrEqual(CameraOrbitModel.DeltaAngle(previous, m), -1e-3f, "never reverses at " + a);
                Assert.LessOrEqual(System.Math.Abs(CameraOrbitModel.DeltaAngle(a, m)), tuning.SnapDegrees + 1e-3f,
                    "never more than the snap away from the thumb at " + a);
                previous = m;
            }
        }

        [Test]
        public void StickSteeringKeepsStrengthAndPassesDeliberateTurns()
        {
            var steering = new StickSteering(new StickSteeringTuning());
            steering.Step(1f / 60f, 0.05f, 0.5f, out float x, out float y);
            Assert.AreEqual(0f, x, 1e-4f, "a slightly crooked push walks straight");
            Assert.AreEqual(System.Math.Sqrt(0.05 * 0.05 + 0.5 * 0.5), System.Math.Sqrt(x * x + y * y), 1e-4,
                "walk or run strength is untouched");

            steering.Step(1f / 60f, 0.8f, 0.3f, out x, out y);
            Assert.AreEqual(0.8f, x, 1e-4f, "a big change of aim passes at once");
            Assert.AreEqual(0.3f, y, 1e-4f);

            steering.Step(1f / 60f, 0f, 0f, out x, out y);
            Assert.AreEqual(0f, x);
            Assert.AreEqual(0f, y);

            // Keyboard and d-pad directions are already on the magnets: unchanged, first frame.
            steering.Step(1f / 60f, 0.70710677f, 0.70710677f, out x, out y);
            Assert.AreEqual(0.70710677f, x, 1e-4f);
            Assert.AreEqual(0.70710677f, y, 1e-4f);
        }

        [Test]
        public void AngleHelpersWrap()
        {
            Assert.AreEqual(-170f, CameraOrbitModel.NormalizeAngle(190f));
            Assert.AreEqual(20f, CameraOrbitModel.DeltaAngle(170f, -170f));
        }
    }
}
