using System;
using NUnit.Framework;
using SpookyStreet.Core.Animation;
using SpookyStreet.Core.Characters;
using SpookyStreet.Core.World;

namespace SpookyStreet.Tests.Core
{
    public class CharacterClipsTests
    {
        [Test]
        public void WitnessLoopsLoopAndTheWavePlaysOnce()
        {
            foreach (var name in new[] { "Idle", "Talk", "SitIdle", "SitTalk", "KneeTap", CharacterClips.Walk })
            {
                Assert.IsTrue(CharacterClips.IsLooping(name), name);
                Assert.IsTrue(CharacterClips.IsLooping("Bea_Rig|" + name), "prefixed " + name);
            }

            Assert.AreEqual("Walk", CharacterClips.Walk);
            Assert.IsTrue(CharacterClips.IsLooping("June_Rig|Walk"));
            Assert.IsFalse(CharacterClips.IsLooping("Wave"));
            Assert.IsFalse(CharacterClips.IsLooping("June_Rig|Point"));
            Assert.IsFalse(CharacterClips.IsLooping("Bea_Rig|Point"));
            Assert.IsFalse(CharacterClips.IsLooping("Kit_Rig|OpenBooth"));
            Assert.IsFalse(CharacterClips.IsLooping("Vale_Rig|Wave"));
            Assert.IsFalse(CharacterClips.IsLooping("Take 001"));
        }

        [Test]
        public void TestimonyGesturesPlayOnceAndBelongToOneCastMemberEach()
        {
            CollectionAssert.AreEqual(new[] { "ShadeEyes", "CheckWatch", "Listen", "Ponder" }, CharacterClips.TestimonyGestures);
            foreach (var name in CharacterClips.TestimonyGestures)
            {
                Assert.IsFalse(CharacterClips.IsLooping(name), name);
                Assert.IsFalse(CharacterClips.IsLooping("Bea_Rig|" + name), "prefixed " + name);
                Assert.IsFalse(PipClips.IsKnown(name), name + " is not one of Pip's clips");
            }

            Assert.AreEqual(CharacterClips.ShadeEyes, CharacterClips.TestimonyGestureOf("Bea"));
            Assert.AreEqual(CharacterClips.CheckWatch, CharacterClips.TestimonyGestureOf("Vale"));
            Assert.AreEqual(CharacterClips.Listen, CharacterClips.TestimonyGestureOf("June"));
            Assert.AreEqual(CharacterClips.Ponder, CharacterClips.TestimonyGestureOf("Kit"));
            Assert.IsNull(CharacterClips.TestimonyGestureOf("Pip"));
            Assert.IsNull(CharacterClips.TestimonyGestureOf(null));
        }

        [Test]
        public void OnlyGesturesAimedAheadTurnTheBody()
        {
            // Bea looks up high along her own forward, and she points: the game turns her to the place.
            Assert.IsTrue(CharacterClips.TurnsToAim(CharacterClips.ShadeEyes));
            Assert.IsTrue(CharacterClips.TurnsToAim(CharacterClips.Point));
            Assert.IsTrue(CharacterClips.TurnsToAim(CharacterClips.Wave));
            // The others are about the speaker: turning would aim them at a place (D26) and spin Vale on his bench.
            Assert.IsFalse(CharacterClips.TurnsToAim(CharacterClips.CheckWatch));
            Assert.IsFalse(CharacterClips.TurnsToAim("Vale_Rig|CheckWatch"));
            Assert.IsFalse(CharacterClips.TurnsToAim(CharacterClips.Listen));
            Assert.IsFalse(CharacterClips.TurnsToAim(CharacterClips.Ponder));
            // An unknown gesture keeps the old behaviour (it will not play anyway).
            Assert.IsTrue(CharacterClips.TurnsToAim("NoSuchGesture"));
            Assert.IsTrue(CharacterClips.TurnsToAim(null));
        }

        [Test]
        public void PipClassificationIsUnchanged()
        {
            foreach (var name in PipClips.All)
            {
                Assert.AreEqual(PipClips.IsLooping(name), CharacterClips.IsLooping(name), name);
            }
        }

        [Test]
        public void ClipSetsMatchTheExports()
        {
            Assert.AreEqual("Idle", CharacterClips.Standing.Rest);
            Assert.AreEqual("Talk", CharacterClips.Standing.Engaged);
            Assert.AreEqual("Wave", CharacterClips.Standing.Greeting);
            Assert.AreEqual("SitIdle", CharacterClips.Seated.Rest);
            Assert.AreEqual("SitTalk", CharacterClips.Seated.Engaged);
            Assert.AreEqual("Wave", CharacterClips.Seated.Greeting);
        }
    }

    public class WitnessReactionTests
    {
        private const float Dt = 1f / 30f;

        private static void Run(WitnessReaction r, float distance, float seconds)
        {
            for (float t = 0f; t < seconds; t += Dt)
            {
                r.Step(distance, Dt);
            }
        }

        [Test]
        public void StaysIdleWhilePipIsAway()
        {
            var r = new WitnessReaction();
            Run(r, 10f, 3f);
            Assert.AreEqual(WitnessState.Idle, r.State);
            Assert.IsFalse(r.WantsEngagedLoop);
            Assert.IsFalse(r.Attending);
            Assert.AreEqual(0, r.WaveCount);
        }

        [Test]
        public void ApproachWavesOnceThenTalks()
        {
            var r = new WitnessReaction { WaveDuration = 1.5f };
            r.Step(10f, Dt);
            r.Step(3f, Dt);
            Assert.AreEqual(WitnessState.Waving, r.State);
            Assert.IsTrue(r.WaveStarted);
            Assert.IsTrue(r.Attending);
            Assert.IsFalse(r.WantsEngagedLoop, "the wave starts and ends on the rest loop");
            r.Step(3f, Dt);
            Assert.IsFalse(r.WaveStarted, "WaveStarted is one step only");
            Run(r, 3f, 1.6f);
            Assert.AreEqual(WitnessState.Talking, r.State);
            Assert.IsTrue(r.WantsEngagedLoop);
            Run(r, 2f, 5f);
            Assert.AreEqual(1, r.WaveCount, "one wave per approach");
        }

        [Test]
        public void HysteresisKeepsTalkingOnTheEdge()
        {
            var r = new WitnessReaction { NearRadius = 4f, FarRadius = 4.75f, WaveDuration = 0f };
            r.Step(3.9f, Dt);
            Assert.AreEqual(WitnessState.Talking, r.State);
            // Wobbling between the two radii never leaves the talk loop.
            for (int i = 0; i < 120; i++)
            {
                r.Step(i % 2 == 0 ? 4.1f : 4.7f, Dt);
            }

            Assert.AreEqual(WitnessState.Talking, r.State);
        }

        [Test]
        public void ReturnsToIdleAfterADelayAndGreetsTheNextApproach()
        {
            var r = new WitnessReaction { ReturnDelay = 1.5f, WaveDuration = 1f };
            r.Step(2f, Dt);
            Run(r, 2f, 1.2f);
            Assert.AreEqual(WitnessState.Talking, r.State);

            Run(r, 8f, 1f);
            Assert.AreEqual(WitnessState.Lingering, r.State, "still talking just after Pip leaves");
            Assert.IsTrue(r.WantsEngagedLoop);
            Run(r, 8f, 0.7f);
            Assert.AreEqual(WitnessState.Idle, r.State);

            r.Step(3f, Dt);
            Assert.AreEqual(WitnessState.Waving, r.State);
            Assert.AreEqual(2, r.WaveCount);
        }

        [Test]
        public void ComingBackBeforeSettlingDoesNotWaveAgain()
        {
            var r = new WitnessReaction { ReturnDelay = 2f, WaveDuration = 0.5f };
            r.Step(2f, Dt);
            Run(r, 2f, 1f);
            Run(r, 6f, 1f);
            Assert.AreEqual(WitnessState.Lingering, r.State);
            r.Step(3f, Dt);
            Assert.AreEqual(WitnessState.Talking, r.State);
            Assert.AreEqual(1, r.WaveCount);
        }

        [Test]
        public void TheWaveFinishesEvenIfPipLeaves()
        {
            var r = new WitnessReaction { WaveDuration = 1f, ReturnDelay = 1f };
            r.Step(3f, Dt);
            Run(r, 9f, 0.5f);
            Assert.AreEqual(WitnessState.Waving, r.State);
            Run(r, 9f, 0.6f);
            Assert.AreEqual(WitnessState.Lingering, r.State);
            Run(r, 9f, 1.1f);
            Assert.AreEqual(WitnessState.Idle, r.State);
        }

        [Test]
        public void PausedStepsChangeNothing()
        {
            var r = new WitnessReaction();
            r.Step(10f, 0f);
            r.Step(2f, 0f);
            Assert.AreEqual(WitnessState.Idle, r.State);
            r.Step(2f, Dt);
            Assert.AreEqual(WitnessState.Waving, r.State);
            for (int i = 0; i < 300; i++)
            {
                r.Step(2f, 0f);
            }

            Assert.AreEqual(WitnessState.Waving, r.State, "pause must freeze the wave");
        }

        [TestCase(0f, 30f, 35f, 30f)]
        [TestCase(0f, 90f, 35f, 35f)]
        [TestCase(0f, -90f, 35f, -35f)]
        [TestCase(170f, -170f, 35f, 20f)]
        [TestCase(-170f, 170f, 35f, -20f)]
        [TestCase(85f, 85f, 35f, 0f)]
        public void LookYawTakesTheShortWayAndClamps(float facing, float target, float max, float expected)
        {
            Assert.AreEqual(expected, WitnessReaction.LookYaw(facing, target, max), 1e-3f);
        }

        [Test]
        public void YawOfFollowsUnityConvention()
        {
            Assert.AreEqual(0f, WitnessReaction.YawOf(0f, 1f), 1e-3f);
            Assert.AreEqual(90f, WitnessReaction.YawOf(1f, 0f), 1e-3f);
            Assert.AreEqual(-90f, WitnessReaction.YawOf(-1f, 0f), 1e-3f);
        }
    }

    public class WitnessLayoutTests
    {
        private static Placement Find(string model)
        {
            foreach (var p in EnvironmentLayout.Witnesses)
            {
                if (p.Model == model)
                {
                    return p;
                }
            }

            Assert.Fail(model + " missing from EnvironmentLayout.Witnesses");
            return default;
        }

        private static Placement Bench()
        {
            foreach (var p in EnvironmentLayout.Street)
            {
                if (p.Model == EnvironmentLayout.Bench)
                {
                    return p;
                }
            }

            Assert.Fail("bench missing");
            return default;
        }

        private static float Facing(in Placement p) => p.Yaw + EnvironmentLayout.ModelFacingOffset;

        [Test]
        public void WitnessesAndTheBikeStayOffTheRouteAndThePath()
        {
            foreach (var p in EnvironmentLayout.Witnesses)
            {
                Assert.IsFalse(EnvironmentLayout.BlocksRoute(p), p.Model + " blocks the mandatory route");
                Assert.IsFalse(EnvironmentLayout.OnPath(p), p.Model + " stands on the path");
                Assert.IsFalse(EnvironmentLayout.OutsideBlock(p), p.Model + " outside the block");
                // Leave at least a Pip-sized gap (1 m) to the path edge so the plaza stays open.
                Assert.Greater(Math.Abs(p.X) - p.HalfX, EnvironmentLayout.PathHalfWidth + 1f, p.Model);
            }
        }

        [Test]
        public void SolidWitnessPlacementsDoNotOverlapSolidDressing()
        {
            foreach (var w in EnvironmentLayout.Witnesses)
            {
                if (!w.Solid)
                {
                    continue;
                }

                foreach (var p in EnvironmentLayout.Street)
                {
                    if (p.Solid)
                    {
                        Assert.IsFalse(EnvironmentLayout.Overlaps(w, p), $"{w.Model} overlaps {p.Model} at ({p.X}, {p.Z})");
                    }
                }
            }
        }

        [Test]
        public void BeaIsWestOfThePlazaWithHerBikeAtHerRightBehindHer()
        {
            var bea = Find(EnvironmentLayout.Bea);
            var bike = Find(EnvironmentLayout.BeaBicycle);
            Assert.Less(bea.X, -5f, "Bea stands west of the plaza");
            Assert.Less(bike.X, -5f, "the bicycle is west of the plaza (spec §4)");
            Assert.AreEqual(PlacementCollider.Capsule, bea.Collider);
            Assert.AreEqual(PlacementCollider.Bounds, bike.Collider);

            double facing = Facing(bea) * Math.PI / 180.0;
            float fx = (float)Math.Sin(facing), fz = (float)Math.Cos(facing);
            float rx = fz, rz = -fx; // Unity: right of forward (x, z) is (z, -x)
            float dx = bike.X - bea.X, dz = bike.Z - bea.Z;
            Assert.AreEqual(EnvironmentLayout.BikeRight, dx * rx + dz * rz, 1e-3f, "to her right");
            Assert.AreEqual(-EnvironmentLayout.BikeBehind, dx * fx + dz * fz, 1e-3f, "behind her");
            Assert.AreEqual(-66f, WitnessReaction.DeltaAngle(bea.Yaw, bike.Yaw), 1e-3f);
        }

        [Test]
        public void BeasTalkPointLandsOnTheOwlHouse()
        {
            var bea = Find(EnvironmentLayout.Bea);
            float pointYaw = Facing(bea) + EnvironmentLayout.BeaPointYaw;
            // The owl house front spans x = -3..3 at z = 11 (attic window at x = 0).
            float left = WitnessReaction.YawOf(-3f - bea.X, 11f - bea.Z);
            float right = WitnessReaction.YawOf(3f - bea.X, 11f - bea.Z);
            float attic = WitnessReaction.YawOf(EnvironmentLayout.HouseX - bea.X, 11f - bea.Z);
            Assert.That(WitnessReaction.DeltaAngle(left, pointYaw), Is.GreaterThanOrEqualTo(0f), "points left of the house");
            Assert.That(WitnessReaction.DeltaAngle(pointYaw, right), Is.GreaterThanOrEqualTo(0f), "points right of the house");
            Assert.Less(Math.Abs(WitnessReaction.DeltaAngle(pointYaw, attic)), 6f, "aim the attic window");
        }

        [Test]
        public void ValeSitsOnTheBenchFacingTheOwlHouseDoor()
        {
            var vale = Find(EnvironmentLayout.Vale);
            var bench = Bench();
            Assert.AreEqual(8f, bench.X);
            Assert.AreEqual(0f, bench.Z);
            Assert.AreEqual(bench.Yaw, vale.Yaw, "Mr. Vale takes the bench's yaw");
            Assert.AreEqual(0f, vale.Y);
            Assert.IsFalse(vale.Solid, "the bench collider covers him");

            float dx = vale.X - bench.X, dz = vale.Z - bench.Z;
            double r = bench.Yaw * Math.PI / 180.0;
            float alongX = dx * (float)Math.Cos(r) - dz * (float)Math.Sin(r);
            Assert.AreEqual(EnvironmentLayout.ValeBenchOffsetX, alongX, 1e-3f, "offset along the bench");
            Assert.AreEqual(EnvironmentLayout.ValeBenchOffsetX, Math.Sqrt(dx * dx + dz * dz), 1e-3f);

            float pointYaw = Facing(vale) + EnvironmentLayout.ValePointYaw;
            float door = WitnessReaction.YawOf(EnvironmentLayout.HouseX - vale.X, 11f - vale.Z);
            Assert.Less(Math.Abs(WitnessReaction.DeltaAngle(pointYaw, door)), 12f, "points at the owl house door");
        }

        [Test]
        public void LocalToWorldMatchesUnityYaw()
        {
            EnvironmentLayout.LocalToWorld(0f, 0f, 90f, 1f, 0f, out float x, out float z);
            Assert.AreEqual(0f, x, 1e-4f);
            Assert.AreEqual(-1f, z, 1e-4f); // Quaternion.Euler(0, 90, 0) * right = back
            EnvironmentLayout.LocalToWorld(2f, 3f, 90f, 0f, 1f, out x, out z);
            Assert.AreEqual(3f, x, 1e-4f);
            Assert.AreEqual(3f, z, 1e-4f);
        }
    }
}
