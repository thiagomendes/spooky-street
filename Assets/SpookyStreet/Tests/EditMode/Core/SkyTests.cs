using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.World;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// The handcrafted sky's timing and artwork: shooting stars every 20–40 s, bats now and then, cloud rings that
    /// wrap seamlessly, reduced motion that removes the flashes and slows the drift, a pause that freezes it all,
    /// and paint that stays under the bloom threshold.
    /// </summary>
    public class SkyTests
    {
        private const float Step = 1f / 30f;

        [Test]
        public void ShootingStarsComeEveryTwentyToFortySeconds()
        {
            var schedule = new SkySchedule(5);
            Assert.That(schedule.ShootingStarIn, Is.InRange(SkyModel.ShootingStarMinInterval, SkyModel.ShootingStarMaxInterval));

            var waits = new List<float>();
            float waited = 0f;
            float streak = 0f;
            bool wasActive = false;
            for (int i = 0; i < 30 * 60 * 60; i++) // an hour of play
            {
                schedule.Advance(Step, false);
                if (schedule.ShootingStarActive)
                {
                    if (!wasActive)
                    {
                        waits.Add(waited);
                        streak = 0f;
                    }

                    streak += Step;
                    waited = 0f;
                }
                else
                {
                    if (wasActive)
                    {
                        Assert.That(streak, Is.InRange(SkyModel.ShootingStarSeconds - 2f * Step,
                            SkyModel.ShootingStarSeconds + 2f * Step), "a streak is brief");
                    }

                    waited += Step;
                }

                wasActive = schedule.ShootingStarActive;
            }

            Assert.Greater(waits.Count, 80, "about two shooting stars a minute at most, one every 40 s at least");
            Assert.AreEqual(waits.Count, schedule.ShootingStarsStarted);
            float shortest = float.MaxValue;
            float longest = 0f;
            foreach (float wait in waits)
            {
                Assert.That(wait, Is.InRange(SkyModel.ShootingStarMinInterval - 2f * Step,
                    SkyModel.ShootingStarMaxInterval + 2f * Step));
                shortest = Math.Min(shortest, wait);
                longest = Math.Max(longest, wait);
            }

            Assert.Greater(longest - shortest, 10f, "the wait is random, not a metronome");
        }

        [Test]
        public void IntervalsStayInsideTheirRange()
        {
            var random = new Random(3);
            for (int i = 0; i < 2000; i++)
            {
                float wait = SkyModel.NextInterval(random, SkyModel.ShootingStarMinInterval, SkyModel.ShootingStarMaxInterval);
                Assert.That(wait, Is.InRange(20f, 40f));
            }
        }

        [Test]
        public void ShootingStarsFallThroughTheUpperSkyNearTheView()
        {
            var random = new Random(11);
            for (int i = 0; i < 500; i++)
            {
                ShootingStarArc arc = SkyModel.NextArc(random);
                Assert.That(Math.Abs(arc.StartAzimuth), Is.LessThanOrEqualTo(SkyModel.ShootingStarViewWindow));
                Assert.Greater(Math.Abs(arc.SweepDegrees), 10f, "a streak travels");
                float previous = float.MaxValue;
                for (int s = 0; s <= 10; s++)
                {
                    float elevation = arc.ElevationAt(s / 10f);
                    Assert.That(elevation, Is.InRange(SkyModel.ShootingStarFloorElevation, SkyModel.ShootingStarMaxElevation));
                    Assert.LessOrEqual(elevation, previous, "a shooting star only falls");
                    previous = elevation;
                }
            }

            Assert.AreEqual(0f, SkyModel.ShootingStarGlow(0f));
            Assert.AreEqual(0f, SkyModel.ShootingStarGlow(1f));
            Assert.Greater(SkyModel.ShootingStarGlow(0.3f), 0.9f);
            Assert.AreEqual(0f, SkyModel.ShootingStarTrail(0f));
            Assert.AreEqual(1f, SkyModel.ShootingStarTrail(0.6f));
        }

        [Test]
        public void BatsCrossTheMoonNowAndThen()
        {
            var schedule = new SkySchedule(9);
            Assert.That(schedule.BatsIn, Is.InRange(SkyModel.BatFirstMinDelay, SkyModel.BatFirstMaxDelay),
                "the first flight comes early enough to be seen in the opening");
            for (int i = 0; i < 30 * 60 * 20; i++)
            {
                schedule.Advance(Step, false);
                if (schedule.BatsActive)
                {
                    Assert.That(schedule.Flight.Count, Is.InRange(SkyModel.BatMinCount, SkyModel.BatMaxCount));
                    Assert.AreEqual(1, Math.Abs(schedule.Flight.Direction));
                }
                else
                {
                    Assert.LessOrEqual(schedule.BatsIn, SkyModel.BatMaxInterval);
                }
            }

            // 20 minutes: a flight every 26–52 s plus its 7.5 s crossing.
            Assert.That(schedule.BatFlightsStarted, Is.InRange(18, 38));
        }

        [Test]
        public void TheLeadBatPassesOverTheMoonDisc()
        {
            var random = new Random(21);
            for (int i = 0; i < 200; i++)
            {
                BatFlight flight = SkyModel.NextFlight(random);
                SkyModel.BatPose(flight, 0, 0.5f, out float x, out float y, out float flap);
                Assert.Less(Math.Abs(x), 0.05f, "half-way through, the lead bat is at the moon's centre line");
                Assert.Less(Math.Abs(y), 0.75f, "and inside the disc");
                Assert.That(flap, Is.InRange(-1f, 1f));

                SkyModel.BatPose(flight, 0, 0f, out float startX, out _, out _);
                SkyModel.BatPose(flight, 0, 1f, out float endX, out _, out _);
                Assert.AreEqual(-flight.Direction * SkyModel.BatTravelRadii, startX, 1e-4f, "enters well off the moon");
                Assert.AreEqual(flight.Direction * SkyModel.BatTravelRadii, endX, 1e-4f, "and leaves on the other side");

                for (int b = 1; b < flight.Count; b++)
                {
                    SkyModel.BatPose(flight, b, 0.5f, out float followerX, out float followerY, out _);
                    Assert.Less(followerX * flight.Direction, 0f, "followers trail the lead bat");
                    Assert.Less(Math.Abs(followerY), 1.4f, "and stay close to the moon");
                }
            }
        }

        [Test]
        public void ReducedMotionRemovesShootingStarsAndBatsAndSlowsTheClouds()
        {
            var schedule = new SkySchedule(5);
            for (int i = 0; i < 30 * 60 * 10; i++)
            {
                schedule.Advance(Step, true);
                Assert.IsFalse(schedule.ShootingStarActive, "no shooting star in reduced motion");
                Assert.IsFalse(schedule.BatsActive, "no bats in reduced motion");
            }

            Assert.AreEqual(0, schedule.ShootingStarsStarted);
            Assert.AreEqual(0, schedule.BatFlightsStarted);
            Assert.AreEqual(600.0 * SkyModel.ReducedCloudScale, schedule.CloudClock, 0.05, "clouds keep drifting, slowly");
            Assert.Less(SkyModel.CloudSpeedScale(true), SkyModel.CloudSpeedScale(false));
            Assert.Less(SkyModel.TwinkleSpeedScale(true), SkyModel.TwinkleSpeedScale(false));
            Assert.Greater(schedule.TwinkleClock, 0.0);

            // Turning reduced motion on drops a running streak and flight at once.
            schedule.StartShootingStar(new ShootingStarArc(0f, 40f, 15f, 8f), 0.2f);
            schedule.StartBats(new BatFlight(3, 1, 0f, 0f), 0.2f);
            Assert.IsTrue(schedule.ShootingStarActive);
            Assert.IsTrue(schedule.BatsActive);
            schedule.Advance(Step, true);
            Assert.IsFalse(schedule.ShootingStarActive);
            Assert.IsFalse(schedule.BatsActive);
            Assert.That(schedule.ShootingStarIn, Is.InRange(SkyModel.ShootingStarMinInterval, SkyModel.ShootingStarMaxInterval));

            // And they come back when it is turned off again.
            for (int i = 0; i < 30 * 60 * 2; i++)
            {
                schedule.Advance(Step, false);
            }

            Assert.Greater(schedule.ShootingStarsStarted, 1);
            Assert.Greater(schedule.BatFlightsStarted, 1);
        }

        [Test]
        public void APausedGameFreezesTheSky()
        {
            var schedule = new SkySchedule(5);
            schedule.Advance(3f, false);
            schedule.StartShootingStar(new ShootingStarArc(0f, 40f, 15f, 8f), 0.4f);
            double clouds = schedule.CloudClock;
            double twinkle = schedule.TwinkleClock;
            float bats = schedule.BatsIn;
            for (int i = 0; i < 100; i++)
            {
                schedule.Advance(0f, false); // Time.deltaTime under timeScale 0
            }

            Assert.AreEqual(clouds, schedule.CloudClock);
            Assert.AreEqual(twinkle, schedule.TwinkleClock);
            Assert.AreEqual(bats, schedule.BatsIn);
            Assert.IsTrue(schedule.ShootingStarActive);
            Assert.AreEqual(0.4f, schedule.ShootingStarProgress, 1e-6f, "a streak holds mid-flight while paused");
        }

        [Test]
        public void CloudRingsWrapSeamlessly()
        {
            foreach (CloudLayer layer in SkyModel.Layers)
            {
                double lap = 360.0 / layer.DegreesPerSecond;
                Assert.Greater(lap, 240.0, "clouds drift slowly");
                Assert.AreEqual(SkyModel.CloudAngle(12.5, layer.DegreesPerSecond, layer.StartDegrees),
                    SkyModel.CloudAngle(12.5 + lap * 7, layer.DegreesPerSecond, layer.StartDegrees), 1e-2f,
                    "a full lap brings the ring back to the same place");

                float previous = SkyModel.CloudAngle(0.0, layer.DegreesPerSecond, layer.StartDegrees);
                for (double t = Step; t < lap * 2.2; t += 0.5)
                {
                    float angle = SkyModel.CloudAngle(t, layer.DegreesPerSecond, layer.StartDegrees);
                    Assert.That(angle, Is.GreaterThanOrEqualTo(0f).And.LessThan(360f));
                    float moved = SkyModel.DeltaAngle(previous, angle);
                    Assert.That(moved, Is.InRange(0f, layer.DegreesPerSecond * 0.5f + 1e-2f),
                        "the ring only ever creeps forward, also across the 360° wrap");
                    previous = angle;
                }
            }

            Assert.That(SkyModel.CloudAngle(0.0, 1f, -30f), Is.EqualTo(330f).Within(1e-3f), "negative starts wrap too");
            Assert.That(SkyModel.CloudAngle(1e7, 0.8f, 0f), Is.GreaterThanOrEqualTo(0f).And.LessThan(360f));
            Assert.AreEqual(-20f, SkyModel.DeltaAngle(10f, 350f), 1e-4f);
            Assert.AreEqual(20f, SkyModel.DeltaAngle(350f, 10f), 1e-4f);
        }

        [Test]
        public void CloudRingsSitInLayersAndTheMiddleOnePassesTheMoon()
        {
            Assert.That(SkyModel.Layers.Length, Is.InRange(2, 3), "a few rings, one draw call each");
            int clouds = 0;
            for (int i = 0; i < SkyModel.Layers.Length; i++)
            {
                clouds += SkyModel.Layers[i].Clouds;
                if (i > 0)
                {
                    Assert.Less(SkyModel.Layers[i].Radius, SkyModel.Layers[i - 1].Radius, "far to near");
                    Assert.Greater(SkyModel.Layers[i].DegreesPerSecond, SkyModel.Layers[i - 1].DegreesPerSecond,
                        "nearer rings drift faster (parallax)");
                }
            }

            Assert.GreaterOrEqual(clouds, 4);
            Assert.That(SkyModel.MoonLayer, Is.InRange(0, SkyModel.Layers.Length - 1));
        }

        [Test]
        public void TheSkyNeverReachesTheBloomThreshold()
        {
            Assert.Less(SkyModel.BrightestValue, 1f);
            Assert.Less(GlowLevels.Moon, SkyModel.BrightestValue + 1e-6f);
            Assert.Less(SkyModel.HaloOpacity, 0.6f, "a soft halo, not a second moon");
            Assert.Greater(GlowLevels.AtticWindow, 1f, "the attic window is the one thing that blooms wide");

            byte[] moon = SkyPainter.PaintMoon(64, GlowLevels.Moon);
            foreach (byte value in moon)
            {
                Assert.LessOrEqual(value, (int)Math.Round(GlowLevels.Moon * 255f));
            }

            byte[] sky = SkyPainter.PaintSky(128, 64, true);
            foreach (byte value in sky)
            {
                Assert.Less(value, (int)(SkyModel.BrightestValue * 255f));
            }
        }

        [Test]
        public void SkyPaintTilesAroundTheDomeAndDeepensOverhead()
        {
            const int width = 256;
            const int height = 128;
            byte[] sky = SkyPainter.PaintSky(width, height, false);
            Assert.AreEqual(width * height * 3, sky.Length);
            CollectionAssert.AreEqual(sky, SkyPainter.PaintSky(width, height, false), "the paint is deterministic");

            // The left and right edges meet as smoothly as any two neighbouring columns.
            double seam = 0.0;
            double inside = 0.0;
            for (int y = 0; y < height; y++)
            {
                for (int k = 0; k < 3; k++)
                {
                    seam += Math.Abs(sky[(y * width + width - 1) * 3 + k] - sky[(y * width) * 3 + k]);
                    inside += Math.Abs(sky[(y * width + 101) * 3 + k] - sky[(y * width + 100) * 3 + k]);
                }
            }

            Assert.Less(seam, inside * 2.0 + height * 3, "the sky must wrap without a seam");

            double horizon = RowLuminance(sky, width, 2);
            double middle = RowLuminance(sky, width, height / 2);
            double zenith = RowLuminance(sky, width, height - 3);
            Assert.Greater(horizon, middle * 1.5, "horizon mist is clearly lighter than the night above it");
            Assert.Greater(middle, zenith * 1.3, "and the sky deepens toward the zenith");
        }

        [Test]
        public void StarsSitAboveTheMistAndHaveTheirOwnTwinklePhase()
        {
            const int width = 512;
            const int height = 256;
            SkyPainter.Star[] stars = SkyPainter.Stars(width, height);
            Assert.AreEqual(SkyPainter.StarCount, stars.Length);
            var phases = new HashSet<int>();
            foreach (SkyPainter.Star star in stars)
            {
                Assert.That(star.Y / height, Is.InRange(SkyPainter.StarFloorV, SkyPainter.StarCeilingV));
                Assert.That(star.Level, Is.InRange(0.15f, 1f));
                phases.Add((int)(star.Phase * 64f));
            }

            Assert.Greater(phases.Count, 48, "phases are spread, so stars never pulse together");

            byte[] mask = SkyPainter.PaintStarMask(width, height);
            Assert.AreEqual(width * height * 2, mask.Length);
            int lit = 0;
            int floorRow = (int)(SkyPainter.StarFloorV * height) - 8;
            for (int y = 0; y < height; y++)
            {
                for (int x = 0; x < width; x++)
                {
                    if (mask[(y * width + x) * 2] > 0)
                    {
                        lit++;
                        Assert.GreaterOrEqual(y, floorRow, "no stars down in the mist and cloud strips");
                    }
                }
            }

            Assert.That(lit, Is.InRange(SkyPainter.StarCount, width * height / 20), "stars are sparse points, not a wash");
            Assert.Greater(SkyPainter.ZenithStretch(0.85f), SkyPainter.ZenithStretch(0.3f));
        }

        [Test]
        public void PaperCloudsAreSingleCutSheetsWithAMoonlitTopEdge()
        {
            for (int seed = 0; seed < 40; seed++)
            {
                const float width = 14f;
                const float height = 3.6f;
                PaperShape cloud = SkyShapes.Cloud(seed, width, height);
                Assert.AreEqual((SkyShapes.CloudColumns + 1) * 4, cloud.VertexCount);
                float area = 0f;
                for (int t = 0; t < cloud.Triangles.Length; t += 3)
                {
                    float signed = SignedArea(cloud, t);
                    Assert.GreaterOrEqual(signed, -1e-4f, "every triangle winds the same way (no folds)");
                    area += signed;
                }

                Assert.That(area, Is.InRange(width * height * 0.3f, width * height * 0.95f), "a cloud, not a sliver or a box");

                float highest = float.MinValue;
                for (int i = 0; i < cloud.VertexCount; i++)
                {
                    Assert.That(cloud.X[i], Is.InRange(-width * 0.5f - 1e-3f, width * 0.5f + 1e-3f));
                    Assert.That(cloud.Y[i], Is.InRange(-height * 0.2f, height * 1.1f));
                    Assert.That(cloud.Shade[i], Is.InRange(0f, 1f));
                    float rim = (float)Math.Sqrt(cloud.RimX[i] * cloud.RimX[i] + cloud.RimY[i] * cloud.RimY[i]);
                    Assert.LessOrEqual(rim, 1f + 1e-4f);
                    highest = Math.Max(highest, cloud.Y[i]);
                }

                // Middle columns: thick enough to be one piece, rim on the cut edges only.
                for (int c = SkyShapes.CloudColumns / 4; c <= SkyShapes.CloudColumns * 3 / 4; c++)
                {
                    int v = c * 4;
                    Assert.Greater(cloud.Y[v + 3] - cloud.Y[v], height * 0.2f, "the sheet never pinches apart");
                    Assert.Greater(cloud.RimY[v + 3], 0.2f, "top edge catches the moon");
                    Assert.Less(cloud.RimY[v], 0f, "underside faces down");
                    Assert.AreEqual(0f, cloud.RimX[v + 1]);
                    Assert.AreEqual(0f, cloud.RimY[v + 1]);
                    Assert.AreEqual(0f, cloud.RimY[v + 2]);
                }

                Assert.Greater(highest, height * 0.8f);
            }
        }

        [Test]
        public void ThePaperBatIsASymmetricSilhouette()
        {
            PaperShape bat = SkyShapes.Bat(1.5f);
            float left = float.MaxValue;
            float right = float.MinValue;
            float area = 0f;
            for (int i = 0; i < bat.VertexCount; i++)
            {
                left = Math.Min(left, bat.X[i]);
                right = Math.Max(right, bat.X[i]);
                Assert.AreEqual(0f, bat.RimX[i]);
                Assert.AreEqual(0f, bat.RimY[i], "bats are plain silhouettes");
            }

            for (int t = 0; t < bat.Triangles.Length; t += 3)
            {
                float signed = SignedArea(bat, t);
                Assert.GreaterOrEqual(signed, -1e-5f);
                area += signed;
            }

            Assert.AreEqual(-0.75f, left, 1e-4f);
            Assert.AreEqual(0.75f, right, 1e-4f);
            Assert.That(area, Is.InRange(0.15f, 0.6f));

            // Mirror symmetry: every vertex has a twin across the body.
            for (int i = 0; i < bat.VertexCount; i++)
            {
                bool twin = false;
                for (int j = 0; j < bat.VertexCount && !twin; j++)
                {
                    twin = Math.Abs(bat.X[i] + bat.X[j]) < 1e-4f && Math.Abs(bat.Y[i] - bat.Y[j]) < 1e-4f;
                }

                Assert.IsTrue(twin, $"vertex {i} has no mirror twin");
            }
        }

        private static float SignedArea(PaperShape shape, int t)
        {
            int a = shape.Triangles[t];
            int b = shape.Triangles[t + 1];
            int c = shape.Triangles[t + 2];
            return 0.5f * ((shape.X[b] - shape.X[a]) * (shape.Y[c] - shape.Y[a]) -
                           (shape.X[c] - shape.X[a]) * (shape.Y[b] - shape.Y[a]));
        }

        private static double RowLuminance(byte[] rgb, int width, int row)
        {
            double sum = 0.0;
            for (int x = 0; x < width; x++)
            {
                int i = (row * width + x) * 3;
                sum += 0.2126 * rgb[i] + 0.7152 * rgb[i + 1] + 0.0722 * rgb[i + 2];
            }

            return sum / width;
        }
    }
}
