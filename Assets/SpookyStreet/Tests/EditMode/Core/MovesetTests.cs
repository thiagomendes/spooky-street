using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Animation;
using SpookyStreet.Core.Movement;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// Spec §5 v1.2 moveset on Jump and Use: variable jump height, chain jump, bat-cape glide, roll, dive and
    /// interaction priority. Flat ground at y = 0; the "grounded" flag fed to the motor is the previous frame's
    /// contact, as the CharacterController reports it.
    /// </summary>
    public class MovesetTests
    {
        private sealed class Sim
        {
            public MotorState State = MotorState.Initial;
            public readonly MovementTuningData Tuning = new MovementTuningData();
            public readonly float Dt;
            public float X;
            public float Y;
            public bool Grounded = true;

            public Sim(float fps) => Dt = 1f / fps;

            public MotorStep Frame(MotorInput input)
            {
                var step = MovementModel.Step(ref State, input, Grounded, Dt, Tuning);
                X += step.DeltaX;
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

            public void Run(float seconds, MotorInput input)
            {
                int frames = (int)Math.Round(seconds / Dt);
                for (int i = 0; i < frames; i++)
                {
                    Frame(input);
                }
            }

            /// <summary>Jumps once and returns the highest point; <paramref name="heldFor"/> &lt; 0 holds to the apex.</summary>
            public float JumpApex(float heldFor)
            {
                float apex = 0f;
                float t = 0f;
                bool first = true;
                for (int i = 0; i < 1000; i++)
                {
                    bool held = heldFor < 0f ? State.VelocityY > 0f || first : t < heldFor;
                    Frame(new MotorInput { JumpPressed = first, JumpHeld = held });
                    first = false;
                    t += Dt;
                    apex = Math.Max(apex, Y);
                    if (Grounded && i > 0)
                    {
                        break;
                    }
                }

                return apex;
            }
        }

        private static readonly MotorInput RunRight = new MotorInput { MoveX = 1f };

        // --- Variable jump ------------------------------------------------------------------------

        [Test]
        public void TapJumpReachesTheTapHeightAndHoldReachesTheHoldHeight([Values(30f, 60f)] float fps)
        {
            float tolerance = fps < 45f ? 0.03f : 0.015f;
            float tap = new Sim(fps).JumpApex(0f);
            float hold = new Sim(fps).JumpApex(-1f);
            Assert.That(tap, Is.EqualTo(1f).Within(tolerance), "tapped jump apex ≈ 1 m");
            Assert.That(hold, Is.EqualTo(1.6f).Within(tolerance), "held jump apex ≈ 1.6 m");
        }

        [Test]
        public void ReleasingEarlyCutsTheRise()
        {
            float tap = new Sim(60f).JumpApex(0f);
            float early = new Sim(60f).JumpApex(0.1f);
            float hold = new Sim(60f).JumpApex(-1f);
            Assert.Greater(early, tap + 0.1f, "holding for 0.1 s rises above a tap");
            Assert.Less(early, hold - 0.1f, "releasing at 0.1 s cuts the rise below a full hold");
        }

        [Test]
        public void HeldJumpApexMatchesAcrossFrameRates()
        {
            Assert.That(new Sim(30f).JumpApex(0.15f), Is.EqualTo(new Sim(60f).JumpApex(0.15f)).Within(0.05f));
        }

        // --- Chain jump ---------------------------------------------------------------------------

        /// <summary>Runs right, jumping on each landing frame (holding to the apex); returns stages and apexes.</summary>
        private static (List<int> stages, List<float> apexes) Chain(float moveX, int jumps, float waitAfterLanding = 0f)
        {
            var sim = new Sim(60f);
            var run = new MotorInput { MoveX = moveX };
            sim.Run(0.5f, run);
            var stages = new List<int>();
            var apexes = new List<float>();
            bool press = true;
            float apex = 0f;
            for (int i = 0; i < 2000 && apexes.Count < jumps; i++)
            {
                bool wasGrounded = sim.Grounded;
                var step = sim.Frame(new MotorInput
                {
                    MoveX = moveX,
                    JumpPressed = press,
                    JumpHeld = press || sim.State.VelocityY > 0f,
                });
                press = false;
                if (step.Jumped)
                {
                    stages.Add(step.ChainStage);
                    apex = 0f;
                }

                apex = Math.Max(apex, sim.Y);
                if (!wasGrounded && sim.Grounded && stages.Count > apexes.Count)
                {
                    apexes.Add(apex);
                    sim.Run(waitAfterLanding, run);
                    press = true;
                }
            }

            return (stages, apexes);
        }

        [Test]
        public void ChainJumpRisesHigherOnTheSecondAndThirdThenRestarts()
        {
            var (stages, apexes) = Chain(1f, 4);
            CollectionAssert.AreEqual(new[] { 1, 2, 3, 1 }, stages);
            Assert.That(apexes[0], Is.EqualTo(1.6f).Within(0.02f));
            Assert.That(apexes[1], Is.EqualTo(2.0f).Within(0.02f));
            Assert.That(apexes[2], Is.EqualTo(2.4f).Within(0.02f));
            Assert.That(apexes[3], Is.EqualTo(1.6f).Within(0.02f));
        }

        [Test]
        public void ChainResetsWhenPipLandsAndWaits()
        {
            var (stages, _) = Chain(1f, 2, waitAfterLanding: 0.3f);
            CollectionAssert.AreEqual(new[] { 1, 1 }, stages);
        }

        [Test]
        public void ChainNeedsRunningSpeed()
        {
            var (stages, _) = Chain(0.3f, 2);
            CollectionAssert.AreEqual(new[] { 1, 1 }, stages, "1.2 m/s is below half of max speed");
        }

        [Test]
        public void TappedChainJumpsScaleTheTapHeight()
        {
            var tuning = new MovementTuningData();
            // Released at take-off, the cut gravity gives the tap apex times the chain scale.
            float v = tuning.JumpVelocityFor(tuning.ChainHeightScale(3));
            float apex = v * v / (2f * tuning.Gravity * tuning.JumpCutGravityMultiplier);
            Assert.That(apex, Is.EqualTo(tuning.JumpApexHeight * 1.5f).Within(1e-3f));
        }

        // --- Bat-cape glide -----------------------------------------------------------------------

        [Test]
        public void HoldingJumpGlidesOnlyAfterTheApex([Values(30f, 60f)] float fps)
        {
            var sim = new Sim(fps);
            bool sawGlide = false;
            bool first = true;
            for (int i = 0; i < (int)(4f * fps); i++)
            {
                float vyBefore = sim.State.VelocityY;
                var step = sim.Frame(new MotorInput { JumpPressed = first, JumpHeld = true });
                first = false;
                if (sim.State.Gliding)
                {
                    sawGlide = true;
                    Assert.LessOrEqual(vyBefore, 0f, "the cape only spreads once Pip is falling");
                    Assert.GreaterOrEqual(sim.State.VelocityY, -sim.Tuning.GlideFallSpeed - 1e-3f, "glide fall speed");
                }

                if (vyBefore > 0f)
                {
                    Assert.IsFalse(sim.State.Gliding, "no glide while rising");
                }

                if (step.Landed)
                {
                    Assert.IsFalse(sim.State.Gliding, "landing folds the cape");
                    break;
                }
            }

            Assert.IsTrue(sawGlide);
        }

        [Test]
        public void GlideSlowsTheDescentOnlyWhileHeld()
        {
            float LandingTime(Func<float, bool> heldAt)
            {
                var sim = new Sim(60f);
                float t = 0f;
                sim.Frame(new MotorInput { JumpPressed = true, JumpHeld = true });
                for (int i = 0; i < 600; i++)
                {
                    t += sim.Dt;
                    var step = sim.Frame(new MotorInput { JumpHeld = heldAt(t) });
                    if (step.Landed)
                    {
                        return t;
                    }
                }

                return float.PositiveInfinity;
            }

            float plain = LandingTime(t => t < 0.36f); // released at the apex: ordinary fall
            float glide = LandingTime(_ => true);
            float partial = LandingTime(t => t < 0.7f); // let go half-way down
            // 1.6 m at 1.75 m/s ≈ 0.9 s of descent instead of ≈ 0.36 s falling freely.
            Assert.Greater(glide, plain + 0.4f);
            Assert.Less(partial, glide - 0.1f, "releasing Jump ends the glide");
            Assert.Greater(partial, plain);
        }

        [Test]
        public void GlideBrakesAFastFallToGlideSpeed()
        {
            var sim = new Sim(60f);
            sim.Y = 20f;
            sim.Grounded = false;
            sim.State.Grounded = false;
            sim.State.TimeSinceGrounded = 1f;
            sim.State.VelocityY = -10f;
            var first = sim.Frame(new MotorInput { JumpHeld = true });
            Assert.IsTrue(first.GlideStarted);
            Assert.Greater(sim.State.VelocityY, -10f, "braking starts at once");
            sim.Run(0.3f, new MotorInput { JumpHeld = true });
            Assert.That(sim.State.VelocityY, Is.EqualTo(-sim.Tuning.GlideFallSpeed).Within(1e-4f));

            var release = sim.Frame(default);
            Assert.IsTrue(release.GlideEnded);
            sim.Run(0.3f, default);
            Assert.Less(sim.State.VelocityY, -4f, "without the cape Pip falls freely again");
        }

        [Test]
        public void GlideGivesALittleMoreAirControl()
        {
            float SpeedAfterOneFrame(bool held)
            {
                var sim = new Sim(60f);
                sim.Y = 5f;
                sim.Grounded = false;
                sim.State.Grounded = false;
                sim.State.TimeSinceGrounded = 1f;
                sim.State.VelocityY = -1f;
                sim.Frame(new MotorInput { MoveX = 1f, JumpHeld = held });
                return sim.State.HorizontalSpeed;
            }

            Assert.Greater(SpeedAfterOneFrame(true), SpeedAfterOneFrame(false));
        }

        // --- Roll ----------------------------------------------------------------------------------

        [Test]
        public void RollBurstsForItsDurationThenCoolsDown([Values(30f, 60f)] float fps)
        {
            var sim = new Sim(fps);
            sim.Run(0.5f, RunRight);
            var start = sim.Frame(new MotorInput { MoveX = 1f, UsePressed = true });
            Assert.IsTrue(start.RollStarted);
            Assert.That(sim.State.HorizontalSpeed, Is.EqualTo(4f + sim.Tuning.RollSpeedBoost).Within(1e-3f),
                "keeps momentum plus a small boost");

            float rolled = sim.Dt;
            float x0 = sim.X;
            for (int i = 0; i < 100 && sim.State.Rolling; i++)
            {
                Assert.IsFalse(sim.Frame(new MotorInput { MoveX = 1f, UsePressed = true }).RollStarted,
                    "a roll cannot restart while rolling");
                rolled += sim.Dt;
            }

            Assert.That(rolled, Is.InRange(sim.Tuning.RollDuration - 1e-4f, sim.Tuning.RollDuration + sim.Dt + 1e-4f));
            Assert.Greater(sim.X - x0, 4f * (rolled - sim.Dt) * 1.2f, "the roll covers more ground than running");

            Assert.IsFalse(sim.Frame(new MotorInput { MoveX = 1f, UsePressed = true }).RollStarted,
                "cannot chain rolls instantly");
            sim.Run(sim.Tuning.RollCooldown, RunRight);
            Assert.IsTrue(sim.Frame(new MotorInput { MoveX = 1f, UsePressed = true }).RollStarted,
                "after the cooldown Use rolls again");
        }

        [Test]
        public void RollNeedsSpeed()
        {
            var sim = new Sim(60f);
            var slow = new MotorInput { MoveX = 0.5f };
            sim.Run(0.5f, slow);
            Assert.AreEqual(UseAction.None, MovementModel.ResolveUse(sim.State, slow, sim.Tuning),
                "2 m/s is below 60 % of max speed");
            var step = sim.Frame(new MotorInput { MoveX = 0.5f, UsePressed = true });
            Assert.IsFalse(step.RollStarted);
            Assert.IsFalse(sim.State.Rolling);
        }

        [Test]
        public void JumpingOutOfARollKeepsItsSpeed()
        {
            var sim = new Sim(60f);
            sim.Run(0.5f, RunRight);
            sim.Frame(new MotorInput { MoveX = 1f, UsePressed = true });
            sim.Run(0.1f, RunRight);
            var jump = sim.Frame(new MotorInput { MoveX = 1f, JumpPressed = true, JumpHeld = true });
            Assert.IsTrue(jump.Jumped);
            Assert.IsTrue(jump.RollEnded);
            Assert.IsFalse(sim.State.Rolling);
            Assert.Greater(sim.State.HorizontalSpeed, 4.5f);
        }

        // --- Dive ----------------------------------------------------------------------------------

        [Test]
        public void UseInTheAirDivesForwardAndLandsInARoll()
        {
            var sim = new Sim(60f);
            sim.Run(0.5f, RunRight);
            sim.Frame(new MotorInput { MoveX = 1f, JumpPressed = true });
            sim.Run(0.1f, RunRight);
            var dive = sim.Frame(new MotorInput { MoveX = 1f, UsePressed = true, JumpHeld = true });
            Assert.IsTrue(dive.DiveStarted);
            Assert.IsTrue(sim.State.Diving);
            Assert.That(sim.State.HorizontalSpeed, Is.EqualTo(sim.Tuning.DiveSpeed).Within(1e-3f));
            Assert.Greater(sim.State.VelocityY, 0f, "a small hop starts the dive");
            Assert.Greater(sim.State.VelocityX, 0f, "dives the way Pip was going");

            bool landedInRoll = false;
            for (int i = 0; i < 300; i++)
            {
                var step = sim.Frame(new MotorInput { MoveX = 1f, UsePressed = i == 2, JumpHeld = true });
                Assert.IsFalse(step.DiveStarted, "one dive per airtime");
                if (!sim.State.Grounded && sim.State.Diving)
                {
                    Assert.IsFalse(sim.State.Gliding, "no glide during a dive");
                }

                if (step.DiveLanded)
                {
                    Assert.IsTrue(step.RollStarted);
                    Assert.IsTrue(sim.State.Rolling);
                    landedInRoll = true;
                    break;
                }
            }

            Assert.IsTrue(landedInRoll);
        }

        [Test]
        public void DiveFromStandingUsesTheFacing()
        {
            var sim = new Sim(60f);
            sim.Frame(new MotorInput { JumpPressed = true });
            sim.Run(0.1f, default);
            sim.Frame(new MotorInput { UsePressed = true, FacingX = 0f, FacingZ = -1f });
            Assert.IsTrue(sim.State.Diving);
            Assert.That(sim.State.VelocityZ, Is.EqualTo(-sim.Tuning.DiveSpeed).Within(1e-3f));
        }

        // --- Interaction priority -------------------------------------------------------------------

        [Test]
        public void InteractionWinsOverRollAndDive()
        {
            var sim = new Sim(60f);
            sim.Run(0.5f, RunRight);
            var withTarget = new MotorInput { MoveX = 1f, UsePressed = true, InteractionAvailable = true };
            Assert.AreEqual(UseAction.Interact, MovementModel.ResolveUse(sim.State, withTarget, sim.Tuning));
            var ground = sim.Frame(withTarget);
            Assert.IsTrue(ground.Interacted);
            Assert.IsFalse(ground.RollStarted);
            Assert.IsFalse(sim.State.Rolling);
            Assert.AreEqual(UseAction.Roll, MovementModel.ResolveUse(sim.State, RunRight, sim.Tuning),
                "without a target the same press would roll");

            sim.Frame(new MotorInput { MoveX = 1f, JumpPressed = true });
            sim.Run(0.1f, RunRight);
            var air = sim.Frame(withTarget);
            Assert.IsTrue(air.Interacted);
            Assert.IsFalse(air.DiveStarted);
            Assert.IsFalse(sim.State.Diving);
        }

        [Test]
        public void MovesetAddsNothingWithoutJumpHeldOrUse()
        {
            // The mandatory route needs walking and a basic jump only: with no Use and no held Jump nothing
            // from the moveset ever triggers.
            var sim = new Sim(60f);
            for (int i = 0; i < 240; i++)
            {
                var step = sim.Frame(new MotorInput { MoveX = 1f, JumpPressed = i % 50 == 0 });
                Assert.IsFalse(step.RollStarted || step.DiveStarted || step.GlideStarted || step.Interacted);
            }
        }

        // --- Camera and squash helpers ---------------------------------------------------------------

        [Test]
        public void CameraHeightIgnoresSmallAirMotionAndFollowsLargeOnes()
        {
            var follow = new CameraHeightFollow();
            follow.Reset(0f);
            follow.Step(0.25f, false, 1f / 60f);
            Assert.AreEqual(0f, follow.Height, "a small hop inside the band does not move the camera");
            follow.Step(1.0f, false, 1f / 60f);
            Assert.That(follow.Height, Is.EqualTo(1.0f - follow.AirTolerance).Within(1e-5f));

            // A steady glide descent drags the band down smoothly: the pivot moves exactly with Pip, no bob.
            float previous = follow.Height;
            for (int i = 1; i <= 30; i++)
            {
                float y = 1.0f - 1.75f * i / 60f - follow.AirTolerance * 2f;
                follow.Step(y, false, 1f / 60f);
                Assert.LessOrEqual(follow.Height, previous + 1e-6f, "the pivot never rises during a descent");
                previous = follow.Height;
            }

            for (int i = 0; i < 60; i++)
            {
                follow.Step(0f, true, 1f / 60f);
            }

            Assert.That(follow.Height, Is.EqualTo(0f).Within(1e-3f), "on the ground the pivot settles onto Pip");
            follow.Step(10f, true, 1f / 60f);
            Assert.AreEqual(10f, follow.Height, "a teleport snaps");
        }

        [Test]
        public void SquashPulseEasesBackToRest()
        {
            var pulse = new SquashPulse { Duration = 0.2f };
            pulse.Kick(0.05f);
            Assert.AreEqual(0.05f, pulse.Value);
            float mid = pulse.Step(0.1f);
            Assert.That(mid, Is.InRange(0.001f, 0.05f));
            Assert.AreEqual(0f, pulse.Step(0.11f));
            SquashPulse.ToScale(0.05f, out float wide, out float tall);
            Assert.Greater(wide, 1f);
            Assert.Less(tall, 1f);
        }
    }
}
