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
        public void AngleHelpersWrap()
        {
            Assert.AreEqual(-170f, CameraOrbitModel.NormalizeAngle(190f));
            Assert.AreEqual(20f, CameraOrbitModel.DeltaAngle(170f, -170f));
        }
    }
}
