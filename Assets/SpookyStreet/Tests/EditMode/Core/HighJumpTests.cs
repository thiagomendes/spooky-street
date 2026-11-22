using System;
using NUnit.Framework;
using SpookyStreet.Core.Animation;
using SpookyStreet.Core.Movement;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// High jump after the owner's TestFlight 12.1 feedback ("holding or tapping quickly, the jump is the same; when I
    /// hold, she comes down gliding"): a tap still peaks at 1.0 m (routes rely on it), a hold engages a sustained
    /// boost about 0.1 s after take-off and peaks at 2.1 m, and the cape cannot open until Pip has passed the apex
    /// and floated there for the apex hang. Flat ground at y = 0, as in MovesetTests.
    /// </summary>
    public class HighJumpTests
    {
        private sealed class Sim
        {
            public MotorState State = MotorState.Initial;
            public readonly MovementTuningData Tuning = new MovementTuningData();
            public readonly float Dt;
            public float Time;
            public float Y;
            public bool Grounded = true;

            public Sim(float fps) => Dt = 1f / fps;

            public MotorStep Frame(MotorInput input)
            {
                input.GroundDistance = Grounded ? 0f : Math.Max(1e-3f, Y);
                var step = MovementModel.Step(ref State, input, Grounded, Dt, Tuning);
                Time += Dt;
                Y += step.DeltaY;
                if (Y <= 0f)
                {
                    Y = 0f;
                    Grounded = true;
                }
                else
                {
                    Grounded = false;
                }

                return step;
            }
        }

        /// <summary>Jumps holding Jump for <paramref name="heldFor"/> seconds (∞: the whole jump); returns the apex.</summary>
        private static float Apex(float fps, float heldFor, out int boosts)
        {
            var sim = new Sim(fps);
            float apex = 0f;
            boosts = 0;
            for (int i = 0; i < 2000; i++)
            {
                float t = sim.Time;
                var step = sim.Frame(new MotorInput { JumpPressed = i == 0, JumpHeld = t < heldFor - 1e-4f });
                boosts += step.BoostStarted ? 1 : 0;
                apex = Math.Max(apex, sim.Y);
                if (sim.Grounded && i > 0)
                {
                    break;
                }
            }

            return apex;
        }

        [Test]
        public void TapPeaksAtOneMetreAndAFullHoldAtTwoPointOne([Values(30f, 60f)] float fps)
        {
            float tap = Apex(fps, 0f, out int tapBoosts);
            float hold = Apex(fps, float.PositiveInfinity, out int holdBoosts);
            Assert.That(tap, Is.EqualTo(1.0f).Within(0.1f), "tap apex: the routes' 1.0 m");
            Assert.That(hold, Is.EqualTo(2.1f).Within(0.1f), "held apex: the high jump");
            Assert.That(tap, Is.EqualTo(1.0f).Within(0.02f), "exact integration: the tap is 1.0 m at any frame rate");
            Assert.That(hold, Is.EqualTo(2.1f).Within(0.02f), "and the hold 2.1 m");
            Assert.AreEqual(0, tapBoosts, "a tap never boosts");
            Assert.AreEqual(1, holdBoosts, "a hold boosts once");
        }

        [Test]
        public void AThumbTapUpToTheBoostDelayIsStillAPlainOneMetreJump([Values(30f, 60f)] float fps)
        {
            // A finger on glass is down for a few frames even on a quick tap: that must not change the height.
            float quick = Apex(fps, 0.06f, out int boosts);
            Assert.AreEqual(0, boosts);
            Assert.That(quick, Is.EqualTo(1.0f).Within(0.02f));
        }

        [Test]
        public void TheHoldBoostEngagesAfterAboutATenthAndShootsPipUp([Values(30f, 60f)] float fps)
        {
            var sim = new Sim(fps);
            var tap = new Sim(fps);
            float boostAt = -1f;
            float speedBefore = 0f;
            float lowestRise = float.PositiveInfinity;
            float fastestAfter = 0f;
            sim.Frame(new MotorInput { JumpPressed = true, JumpHeld = true });
            tap.Frame(new MotorInput { JumpPressed = true });
            while (sim.Time < 0.3f - 1e-4f)
            {
                speedBefore = sim.State.VelocityY;
                var step = sim.Frame(new MotorInput { JumpHeld = true });
                tap.Frame(default);
                if (step.BoostStarted)
                {
                    boostAt = sim.Time;
                    lowestRise = speedBefore;
                }

                if (boostAt > 0f)
                {
                    fastestAfter = Math.Max(fastestAfter, sim.State.VelocityY);
                }
            }

            Assert.That(boostAt, Is.InRange(sim.Tuning.JumpBoostDelay - 1e-3f, sim.Tuning.JumpBoostDelay + sim.Dt + 1e-3f),
                "the boost engages about 0.1 s after take-off");
            float atEngage = sim.Tuning.JumpVelocity - sim.Tuning.Gravity * sim.Tuning.JumpBoostDelay;
            Assert.Less(atEngage, lowestRise, "slowing down under gravity until the boost");
            Assert.Greater(fastestAfter, atEngage + 1f, "the rise speeds up again: a thrust, not a lower gravity");
            Assert.Greater(sim.Y, tap.Y + 0.5f, "0.3 s in, the held jump is already well above the tap");
        }

        [Test]
        public void GlideCannotBeginBeforeTheApexPlusTheHang([Values(30f, 60f)] float fps)
        {
            var sim = new Sim(fps);
            float apex = 0f;
            float apexAt = 0f;
            float hang = sim.Tuning.ApexHangTime;
            bool glided = false;
            for (int i = 0; i < 4000 && !glided; i++)
            {
                var before = sim.State;
                float frameStart = sim.Time;
                var step = sim.Frame(new MotorInput { JumpPressed = i == 0, JumpHeld = true });
                if (sim.Y > apex)
                {
                    apex = sim.Y;
                    apexAt = sim.Time;
                }

                if (step.GlideStarted)
                {
                    glided = true;
                    Assert.LessOrEqual(before.VelocityY, 0f, "falling");
                    Assert.GreaterOrEqual(before.FallTime, hang - 1e-4f, "past the apex for the whole hang");
                    // The sampled peak is within one frame of the true apex.
                    Assert.GreaterOrEqual(frameStart, apexAt + hang - sim.Dt - 1e-4f, "the cape waits for the apex hang");
                    Assert.That(apex, Is.GreaterThanOrEqualTo(2.0f), "the high jump peaked before the cape opened");
                }
            }

            Assert.IsTrue(glided, "held on, the cape opens after the hang");
        }

        [Test]
        public void TheApexHangFloatsOnlyAHeldHighJump()
        {
            float DropOverHang(bool held)
            {
                var sim = new Sim(60f);
                float peak = 0f;
                float drop = 0f;
                bool pastApex = false;
                float t = 0f;
                for (int i = 0; i < 600; i++)
                {
                    sim.Frame(new MotorInput { JumpPressed = i == 0, JumpHeld = held || sim.State.VelocityY > 0f || i == 0 });
                    peak = Math.Max(peak, sim.Y);
                    if (!pastApex && sim.State.VelocityY <= 0f)
                    {
                        pastApex = true;
                    }

                    if (pastApex)
                    {
                        t += sim.Dt;
                        if (t >= 0.12f - 1e-4f)
                        {
                            drop = peak - sim.Y;
                            break;
                        }
                    }
                }

                return drop;
            }

            float floated = DropOverHang(true);
            float released = DropOverHang(false);
            Assert.Less(floated, 0.1f, "held through the apex, Pip hangs there");
            Assert.Greater(released, floated * 2f, "let go at the apex, Pip falls straight away");
        }

        [Test]
        public void AFreshPressRightAfterATappedApexWaitsForTheHangToo()
        {
            var sim = new Sim(60f);
            sim.Frame(new MotorInput { JumpPressed = true });
            while (sim.State.VelocityY > 0f)
            {
                sim.Frame(default);
            }

            var press = sim.Frame(new MotorInput { JumpPressed = true, JumpHeld = true });
            Assert.IsFalse(press.GlideStarted, "just past the apex: no cape yet");
            bool glided = false;
            for (int i = 0; i < 60 && !sim.Grounded && !glided; i++)
            {
                var step = sim.Frame(new MotorInput { JumpHeld = true });
                if (step.GlideStarted)
                {
                    glided = true;
                    Assert.GreaterOrEqual(sim.State.FallTime, sim.Tuning.ApexHangTime - 1e-4f);
                }
            }

            Assert.IsTrue(glided, "then the held press glides");
        }

        [Test]
        public void ACeilingOrADiveEndsTheBoost()
        {
            var sim = new Sim(60f);
            sim.Frame(new MotorInput { JumpPressed = true, JumpHeld = true });
            for (int i = 0; i < 8; i++)
            {
                sim.Frame(new MotorInput { JumpHeld = true });
            }

            Assert.IsTrue(sim.State.Boosting, "boosting 0.15 s in");
            MovementModel.OnCeilingHit(ref sim.State);
            Assert.IsFalse(sim.State.Boosting);
            Assert.AreEqual(0f, sim.State.VelocityY);
            sim.Frame(new MotorInput { JumpHeld = true });
            Assert.Less(sim.State.VelocityY, 0f, "no thrust back into the ceiling");

            var dive = new Sim(60f);
            dive.Frame(new MotorInput { MoveX = 1f, JumpPressed = true, JumpHeld = true });
            for (int i = 0; i < 8; i++)
            {
                dive.Frame(new MotorInput { MoveX = 1f, JumpHeld = true });
            }

            dive.Frame(new MotorInput { MoveX = 1f, JumpHeld = true, UsePressed = true });
            Assert.IsTrue(dive.State.Diving);
            Assert.IsFalse(dive.State.Boosting, "the dive replaces the boost");
            Assert.That(dive.State.VelocityY, Is.LessThan(dive.Tuning.DiveHopVelocity));
        }

        [Test]
        public void ChainedHighJumpsStayTheStrongestMove()
        {
            var t = new MovementTuningData();
            Assert.That(t.EffectiveHoldApexHeight * t.ChainHeightScale(2), Is.EqualTo(2.4f).Within(1e-3f));
            Assert.That(t.EffectiveHoldApexHeight * t.ChainHeightScale(3), Is.EqualTo(2.8f).Within(1e-3f));
            Assert.That(t.JumpApexHeight * t.ChainTapScale(2), Is.EqualTo(1.25f).Within(1e-3f), "tapped 2nd as on 12.1");
            Assert.That(t.JumpApexHeight * t.ChainTapScale(3), Is.EqualTo(1.5f).Within(1e-3f), "tapped 3rd as on 12.1");
            Assert.Greater(t.JumpBoostDurationForStage(3), t.JumpBoostDurationForStage(1), "a higher stage boosts longer");
            Assert.Greater(t.JumpBoostDurationForStage(2), 0.1f);
            Assert.Greater(t.JumpBoostDurationFor(1f), 0.1f, "the boost is a sustained phase, not a kick");
        }

        [Test]
        public void TheBoostPuffsPaperAndFlaresTheCapeUnlessReducedMotion()
        {
            Assert.AreEqual(MoveBursts.BoostPuff, MoveFeedbackRules.BurstsFor(new MotorStep { BoostStarted = true }, false));
            Assert.AreEqual(MoveBursts.None, MoveFeedbackRules.BurstsFor(new MotorStep { BoostStarted = true }, true),
                "reduced motion: no puff");
            Assert.That(MoveFeedbackRules.PieceCount(MoveBursts.BoostPuff), Is.InRange(4, 10), "a small puff");
            Assert.Less(MoveFeedbackRules.BoostFlareStretch, MoveFeedbackRules.TakeOffStretch(1),
                "the cape flare stretches more than a plain take-off");
            Assert.AreEqual(MoveFeedbackRules.MaxStretch,
                MoveFeedbackRules.CombinedSquash(MoveFeedbackRules.BoostFlareStretch, MoveFeedbackRules.HeldJumpStretch),
                "flare plus held stretch is capped");
            SquashPulse.ToScale(MoveFeedbackRules.MaxStretch, out _, out float tall);
            Assert.Less(tall, 1.45f, "a stretch, not a distortion");
        }
    }
}
