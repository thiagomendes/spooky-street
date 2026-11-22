using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.World;

namespace SpookyStreet.Tests.Core
{
    public class EnvironmentLayoutTests
    {
        private static IEnumerable<Placement> All()
        {
            foreach (var p in EnvironmentLayout.House)
            {
                yield return p;
            }

            foreach (var p in EnvironmentLayout.Street)
            {
                yield return p;
            }

            foreach (var p in EnvironmentLayout.PaperBats)
            {
                yield return p;
            }
        }

        [Test]
        public void NothingSolidBlocksTheMandatoryRoute()
        {
            var offenders = new List<string>();
            foreach (var p in All())
            {
                if (EnvironmentLayout.BlocksRoute(p))
                {
                    offenders.Add($"{p.Model} at ({p.X}, {p.Z})");
                }
            }

            Assert.IsEmpty(offenders, "solid dressing inside the route's clear passage:\n" + string.Join("\n", offenders));
        }

        [Test]
        public void NoGroundDressingStandsOnThePath()
        {
            var offenders = new List<string>();
            foreach (var p in All())
            {
                bool porchOrHouse = p.Z < EnvironmentLayout.RouteStartZ || p.Model == EnvironmentLayout.OwlHouse ||
                                    p.Model == EnvironmentLayout.OwlHouseDressing;
                if (!porchOrHouse && EnvironmentLayout.OnPath(p))
                {
                    offenders.Add($"{p.Model} at ({p.X}, {p.Z})");
                }
            }

            Assert.IsEmpty(offenders, "dressing standing on the path:\n" + string.Join("\n", offenders));
        }

        [Test]
        public void PathHasADetailAboutEveryOneAndAHalfMetres()
        {
            float gap = EnvironmentLayout.LargestDetailGap(EnvironmentLayout.Street, EnvironmentLayout.RouteStartZ,
                EnvironmentLayout.FrontYardZ);
            Assert.LessOrEqual(gap, EnvironmentLayout.MaxDetailGap);
        }

        [Test]
        public void LargestDetailGapMeasuresEmptyStretches()
        {
            var placements = new[]
            {
                new Placement("A", 1.5f, 0f, 2f),
                new Placement("B", -1.5f, 0f, 3f),
                new Placement("FarAway", 9f, 0f, 6f),
                new Placement("NotADetail", 1.5f, 0f, 7f, detail: false),
            };
            Assert.AreEqual(7f, EnvironmentLayout.LargestDetailGap(placements, 0f, 10f), 1e-4f);
        }

        [Test]
        public void BlocksRouteOnlyForSolidPiecesOnTheCentreLine()
        {
            var fence = new Placement("Fence", 0.5f, 0f, 0f, collider: PlacementCollider.Bounds, halfX: 1f, halfZ: 0.1f);
            var decor = new Placement("Bat", 0f, 2f, 0f);
            var beside = new Placement("Fence", 2f, 0f, 0f, collider: PlacementCollider.Bounds, halfX: 1f, halfZ: 0.1f);
            var beyond = new Placement("Fence", 0f, 0f, 30f, collider: PlacementCollider.Bounds, halfX: 1f, halfZ: 0.1f);
            Assert.IsTrue(EnvironmentLayout.BlocksRoute(fence));
            Assert.IsFalse(EnvironmentLayout.BlocksRoute(decor));
            Assert.IsFalse(EnvironmentLayout.BlocksRoute(beside));
            Assert.IsFalse(EnvironmentLayout.BlocksRoute(beyond));
        }

        [Test]
        public void HouseStandsWhereTheGreyboxHouseStood()
        {
            Assert.AreEqual(2, EnvironmentLayout.House.Length);
            foreach (var p in EnvironmentLayout.House)
            {
                Assert.AreEqual(13f, p.Z, 1e-4f, "house origin is 2 m behind the greybox front (z = 11)");
                Assert.AreEqual(0f, p.X, 1e-4f);
                Assert.AreEqual(0f, p.Yaw, 1e-4f);
            }
        }

        [Test]
        public void ArrivalHasTheCandyBowlAndTheHouseHasAGateOnTheRoute()
        {
            bool bowl = false;
            bool gate = false;
            foreach (var p in EnvironmentLayout.Street)
            {
                bowl |= p.Model == EnvironmentLayout.CandyBowl && p.Z < -12.5f && p.Y > 0.6f;
                gate |= p.Model == EnvironmentLayout.FenceGate && !p.Solid && System.Math.Abs(p.X) < 0.6f;
            }

            Assert.IsTrue(bowl, "candy bowl must stand raised on the arrival porch");
            Assert.IsTrue(gate, "an open, non-solid gate must sit on the route");
        }

        [Test]
        public void AtticWindowIsTheBrightestGlow()
        {
            Assert.Greater(GlowLevels.AtticWindow, GlowLevels.BrightestPractical);
            Assert.AreEqual(GlowLevels.AtticWindow, GlowLevels.IntensityFor("AtticWindow_Glow"));
            foreach (var name in new[] { "SideWindow_Glow", "Lamp_Glow", "Face_Glow", "PaperBat_Glow", "Mystery_Glow" })
            {
                Assert.Less(GlowLevels.IntensityFor(name), GlowLevels.AtticWindow, name);
                Assert.Greater(GlowLevels.IntensityFor(name), 1f, name + " should still reach bloom");
            }

            Assert.Less(GlowLevels.Moon, 1f, "the moon stays under the bloom threshold");
        }

        [Test]
        public void GlowPartsAreRecognisedBySuffix()
        {
            Assert.IsTrue(GlowLevels.IsGlowPart("AtticWindow_Glow"));
            Assert.IsTrue(GlowLevels.IsGlowPart("PaperBat_Glow"));
            Assert.IsFalse(GlowLevels.IsGlowPart("PaperBat"));
            Assert.IsFalse(GlowLevels.IsGlowPart("Glow_Lamp"));
            Assert.IsFalse(GlowLevels.IsGlowPart(null));
        }
    }

    public class AmbientMotionModelTests
    {
        [Test]
        public void WheelAngleWrapsAndAdvancesWithTime()
        {
            Assert.AreEqual(0f, AmbientMotionModel.SpinDegrees(0, 36f), 1e-4f);
            Assert.AreEqual(36f, AmbientMotionModel.SpinDegrees(1, 36f), 1e-4f);
            Assert.AreEqual(0f, AmbientMotionModel.SpinDegrees(10, 36f), 1e-3f);
            Assert.AreEqual(324f, AmbientMotionModel.SpinDegrees(-1, 36f), 1e-4f);
        }

        [Test]
        public void OscillationStaysWithinAmplitude()
        {
            for (int i = 0; i < 200; i++)
            {
                float value = AmbientMotionModel.Oscillate(i * 0.137, 0.45f, 0.07f, 0.3f);
                Assert.LessOrEqual(System.Math.Abs(value), 0.07f + 1e-6f);
            }

            Assert.AreEqual(1f, AmbientMotionModel.Oscillate(0.25, 1f, 1f), 1e-5f);
        }

        [Test]
        public void ReducedMotionStopsDecorativeSwayButKeepsASlowWheel()
        {
            Assert.AreEqual(0f, AmbientMotionModel.DecorativeScale(true));
            Assert.AreEqual(1f, AmbientMotionModel.DecorativeScale(false));
            Assert.Less(AmbientMotionModel.WheelSpeed(true), AmbientMotionModel.WheelSpeed(false));
            Assert.Greater(AmbientMotionModel.WheelSpeed(true), 0f);
        }

        [Test]
        public void PhasesDifferBetweenNeighbours()
        {
            for (int i = 0; i < 10; i++)
            {
                float a = AmbientMotionModel.PhaseFor(i);
                float b = AmbientMotionModel.PhaseFor(i + 1);
                Assert.That(a, Is.InRange(0f, 1f));
                Assert.Greater(System.Math.Abs(a - b), 0.1f);
            }
        }
    }
}
