using NUnit.Framework;
using SpookyStreet.Core.Nonverbal;

namespace SpookyStreet.Tests.Core
{
    /// <summary>The tier-2 bat trail: repeats every 4 s for about 8 s per radio press (D23.7).</summary>
    public class BatTrailScheduleTests
    {
        private readonly BatState[] _buffer = new BatState[32];

        [Test]
        public void TwoWavesFourSecondsApartForAboutEightSeconds()
        {
            var trail = new BatTrailSchedule();
            Assert.AreEqual(2, trail.Waves);
            Assert.That(trail.TotalSeconds, Is.InRange(7.5f, 8.5f));
            Assert.Less(trail.WaveSeconds, trail.RepeatSeconds, "waves never overlap");

            Assert.AreEqual(0, trail.SampleSincePress(0.05f, _buffer), "first bat launches at 0.1 s");
            int n = trail.SampleSincePress(0.15f, _buffer);
            Assert.AreEqual(1, n);
            Assert.AreEqual(0, _buffer[0].Wave);

            n = trail.SampleSincePress(4.15f, _buffer);
            Assert.AreEqual(1, n, "the repeat starts at 4 s");
            Assert.AreEqual(1, _buffer[0].Wave);

            Assert.AreEqual(0, trail.SampleSincePress(trail.TotalSeconds, _buffer));
            Assert.AreEqual(0, trail.SampleSincePress(9f, _buffer), "then it waits for the next press");
            Assert.AreEqual(0, trail.SampleSincePress(-1f, _buffer));
        }

        [Test]
        public void BatsLaunchOneAtATimeFlyTheRouteAndFade()
        {
            var trail = new BatTrailSchedule();
            int n = trail.SampleSincePress(0.1f + 3 * 0.22f + 0.01f, _buffer);
            Assert.AreEqual(4, n);
            for (int i = 0; i + 1 < n; i++)
            {
                Assert.Greater(_buffer[i].RouteProgress, _buffer[i + 1].RouteProgress, "earlier bats are further along");
                Assert.AreEqual(i, _buffer[i].Index);
            }

            // The first bat arrives at 1.7 s, then fades over 0.4 s.
            trail.SampleSincePress(1.9f, _buffer);
            Assert.AreEqual(1f, _buffer[0].RouteProgress, 1e-5f);
            Assert.AreEqual(0.5f, _buffer[0].Alpha, 1e-3f);
        }

        [Test]
        public void StaysWithinTheQuadBudget()
        {
            var trail = new BatTrailSchedule();
            int max = 0;
            for (float t = 0f; t < trail.TotalSeconds; t += 0.01f)
            {
                max = System.Math.Max(max, trail.SampleSincePress(t, _buffer));
            }

            Assert.LessOrEqual(max, BatTrailSchedule.MaxConcurrentBats);
            Assert.Greater(max, 5, "a readable trail, not a single bat");
        }

        [Test]
        public void PressRestartsAndIdleSamplesNothing()
        {
            var trail = new BatTrailSchedule();
            Assert.IsFalse(trail.HasPress);
            Assert.AreEqual(0, trail.Sample(5f, _buffer));
            Assert.IsFalse(trail.IsActive(5f));

            trail.Press(100f);
            Assert.IsTrue(trail.IsActive(100f));
            Assert.IsFalse(trail.BeaconVisible(101f));
            Assert.IsTrue(trail.BeaconVisible(102f), "the goal sticker pulses once the first bat arrives");
            Assert.IsFalse(trail.IsActive(100f + trail.TotalSeconds + 0.01f));
            Assert.IsFalse(trail.BeaconVisible(109f));

            trail.Press(110f);
            Assert.IsTrue(trail.IsActive(113f));
            Assert.AreEqual(trail.SampleSincePress(3f, new BatState[32]), trail.Sample(113f, _buffer));

            trail.Stop();
            Assert.IsFalse(trail.IsActive(113f));
        }

        [Test]
        public void SmallBuffersAreFilledWithoutOverflow()
        {
            var trail = new BatTrailSchedule();
            Assert.AreEqual(2, trail.SampleSincePress(1.5f, new BatState[2]));
            Assert.Throws<System.ArgumentNullException>(() => trail.SampleSincePress(1f, null));
        }

        [Test]
        public void TimingsAreValidated()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new BatTrailSchedule(0f, 8f, 9, 0.22f, 1.6f, 0.4f, 0.1f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new BatTrailSchedule(4f, 8f, 0, 0.22f, 1.6f, 0.4f, 0.1f));
            Assert.AreEqual(3, new BatTrailSchedule(4f, 9f, 9, 0.22f, 1.6f, 0.4f, 0.1f).Waves);
        }
    }
}
