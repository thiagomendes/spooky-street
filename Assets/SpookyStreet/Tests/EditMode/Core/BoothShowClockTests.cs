using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Street;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// S004 plan §2.3: the owl booth's show "on the hour" after case 01 (every 90 s of play on the street, the same at
    /// 30 and 60 fps, still while indoors or held) and the three-knock touch reward; both inert while the clock is off
    /// (before the stamp, or while a replay rests the memories).
    /// </summary>
    public class BoothShowClockTests
    {
        private static List<float> Run(float fps, float seconds, System.Func<float, bool> onStreet = null)
        {
            var clock = new BoothShowClock();
            clock.SetRunning(true);
            var shows = new List<float>();
            float dt = 1f / fps;
            int frames = (int)System.Math.Round(seconds * fps);
            for (int i = 1; i <= frames; i++)
            {
                float t = i * dt;
                if (clock.Advance(dt, onStreet == null || onStreet(t)) == BoothShow.OnTheHour)
                {
                    shows.Add(t);
                }
            }

            return shows;
        }

        [Test]
        public void ItShowsEveryNinetySecondsOfStreetPlayAt30And60Fps()
        {
            Assert.AreEqual(90f, BoothShowClock.IntervalSeconds);
            var at30 = Run(30f, 600f);
            var at60 = Run(60f, 600f);
            Assert.AreEqual(6, at30.Count, "ten minutes: six shows");
            Assert.AreEqual(at30.Count, at60.Count);
            for (int i = 0; i < at30.Count; i++)
            {
                float expected = (i + 1) * BoothShowClock.IntervalSeconds;
                Assert.AreEqual(expected, at30[i], 1f / 30f + 1e-3f, "30 fps: on the hour, within a frame");
                Assert.AreEqual(expected, at60[i], 1f / 60f + 1e-3f, "60 fps: on the hour, within a frame");
                Assert.AreEqual(at30[i], at60[i], 1f / 30f + 1e-3f, "the same moment at both frame rates");
            }
        }

        [Test]
        public void OnlyPlayOnTheStreetCounts()
        {
            // Ten seconds outside, ten indoors (or in a cutscene, the notebook, a crossing), and so on.
            var shows = Run(60f, 400f, t => ((int)(t / 10f)) % 2 == 0);
            Assert.AreEqual(2, shows.Count, "200 s of street play in 400 s");
            // 90 s of street play end at 170 s (or, by a float's hair, on the next frame outside at 180 s).
            Assert.That(shows[0], Is.InRange(169.9f, 180.1f));
            Assert.That(shows[1], Is.InRange(349.9f, 360.1f));

            // Paused (dt 0) and broken frames never count.
            var clock = new BoothShowClock();
            clock.SetRunning(true);
            for (int i = 0; i < 1000; i++)
            {
                Assert.AreEqual(BoothShow.None, clock.Advance(0f, true));
                Assert.AreEqual(BoothShow.None, clock.Advance(float.NaN, true));
            }

            Assert.AreEqual(0f, clock.Clock);
        }

        [Test]
        public void ALongFrameNeverStacksShows()
        {
            var clock = new BoothShowClock();
            clock.SetRunning(true);
            Assert.AreEqual(BoothShow.OnTheHour, clock.Advance(500f, true));
            Assert.Less(clock.Clock, BoothShowClock.IntervalSeconds);
            Assert.AreEqual(BoothShow.None, clock.Advance(1f / 60f, true), "one show, not five");
            Assert.AreEqual(1, clock.Shows);
        }

        [Test]
        public void ThreeKnocksInARowMakeTheOwlBow()
        {
            var clock = new BoothShowClock();
            clock.SetRunning(true);
            clock.Advance(60f, true);
            Assert.AreEqual(BoothShow.None, clock.Knock());
            clock.Advance(0.5f, true);
            Assert.AreEqual(BoothShow.None, clock.Knock());
            clock.Advance(0.5f, true);
            Assert.AreEqual(BoothShow.Knocked, clock.Knock(), "the third knock");
            Assert.AreEqual(0f, clock.Clock, "the hour starts over, so two shows never crowd each other");
            Assert.AreEqual(0, clock.Knocks);

            // Slow knocks start a new row.
            Assert.AreEqual(BoothShow.None, clock.Knock());
            clock.Advance(BoothShowClock.KnockGapSeconds + 0.1f, false);
            Assert.AreEqual(0, clock.Knocks, "the row lapses even indoors");
            Assert.AreEqual(BoothShow.None, clock.Knock());
            clock.Advance(1f, true);
            Assert.AreEqual(BoothShow.None, clock.Knock());
            clock.Advance(1f, true);
            Assert.AreEqual(BoothShow.Knocked, clock.Knock());

            // Six quick knocks: two bows.
            int bows = 0;
            for (int i = 0; i < 6; i++)
            {
                bows += clock.Knock() == BoothShow.Knocked ? 1 : 0;
                clock.Advance(0.3f, true);
            }

            Assert.AreEqual(2, bows);
        }

        [Test]
        public void ItIsInertWhileOff()
        {
            // Before the stamp, or while a replayed case rests its memories: no show, and knocks do nothing.
            var clock = new BoothShowClock();
            Assert.IsFalse(clock.Running);
            for (int i = 0; i < 10; i++)
            {
                Assert.AreEqual(BoothShow.None, clock.Advance(60f, true));
                Assert.AreEqual(BoothShow.None, clock.Knock());
            }

            Assert.AreEqual(0, clock.Shows);
            Assert.AreEqual(0, clock.Knocks);

            // Turning it on or off starts the hour over.
            clock.SetRunning(true);
            clock.Advance(80f, true);
            clock.Knock();
            clock.SetRunning(false);
            Assert.AreEqual(0f, clock.Clock);
            Assert.AreEqual(0, clock.Knocks);
            clock.SetRunning(true);
            Assert.AreEqual(BoothShow.None, clock.Advance(80f, true));
            Assert.AreEqual(BoothShow.OnTheHour, clock.Advance(10f, true));
        }
    }
}
