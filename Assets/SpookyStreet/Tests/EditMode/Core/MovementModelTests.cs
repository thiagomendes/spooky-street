using System;
using NUnit.Framework;
using SpookyStreet.Core.Movement;

namespace SpookyStreet.Tests.Core
{
    public class MovementModelTests
    {
        private static MovementTuningData Tuning() => new MovementTuningData();

        /// <summary>Simulates flat ground at y = 0 for the given duration and frame rate.</summary>
        private static (float x, float y, float apex, MotorState state) Simulate(float fps, float seconds,
            Func<float, MotorInput> inputAt)
        {
            var tuning = Tuning();
            var state = MotorState.Initial;
            float dt = 1f / fps;
            float x = 0f, y = 0f, apex = 0f;
            int frames = (int)Math.Round(seconds * fps);
            bool grounded = true;
            for (int i = 0; i < frames; i++)
            {
                var step = MovementModel.Step(ref state, inputAt(i * dt), grounded, dt, tuning);
                x += step.DeltaX;
                y += step.DeltaY;
                if (y <= 0f)
                {
                    y = 0f;
                    grounded = true;
                }
                else
                {
                    grounded = false;
                }

                apex = Math.Max(apex, y);
            }

            return (x, y, apex, state);
        }

        [Test]
        public void ReachesMaxSpeedInAccelerationTime()
        {
            var tuning = Tuning();
            var state = MotorState.Initial;
            var input = new MotorInput { MoveX = 1f };
            float t = 0f;
            const float dt = 1f / 60f;
            while (state.HorizontalSpeed < tuning.MaxSpeed - 1e-4f && t < 1f)
            {
                MovementModel.Step(ref state, input, true, dt, tuning);
                t += dt;
            }

            Assert.That(state.HorizontalSpeed, Is.EqualTo(4f).Within(1e-4f));
            Assert.That(t, Is.EqualTo(0.15f).Within(dt + 1e-4f));
        }

        [Test]
        public void StopsWithinDecelerationTime()
        {
            var tuning = Tuning();
            var state = MotorState.Initial;
            state.VelocityX = tuning.MaxSpeed;
            float t = 0f;
            const float dt = 1f / 60f;
            while (state.HorizontalSpeed > 0f && t < 1f)
            {
                MovementModel.Step(ref state, default, true, dt, tuning);
                t += dt;
            }

            Assert.That(t, Is.EqualTo(0.12f).Within(dt + 1e-4f));
        }

        [Test]
        public void DiagonalInputIsClampedToMaxSpeed()
        {
            var tuning = Tuning();
            var state = MotorState.Initial;
            for (int i = 0; i < 60; i++)
            {
                MovementModel.Step(ref state, new MotorInput { MoveX = 1f, MoveZ = 1f }, true, 1f / 60f, tuning);
            }

            Assert.That(state.HorizontalSpeed, Is.EqualTo(tuning.MaxSpeed).Within(1e-4f));
        }

        [Test]
        public void JumpApexIsOneMeterAt30And60Fps()
        {
            Func<float, MotorInput> jumpOnce = t => new MotorInput { JumpPressed = t < 1e-6f };
            var at60 = Simulate(60f, 1.2f, jumpOnce);
            var at30 = Simulate(30f, 1.2f, jumpOnce);
            // Frames sample the arc, so the observed apex is at or just below the true 1 m apex.
            Assert.That(at60.apex, Is.EqualTo(1f).Within(0.01f));
            Assert.That(at30.apex, Is.EqualTo(1f).Within(0.02f));
            Assert.That(at60.apex, Is.LessThanOrEqualTo(1f + 1e-4f));
        }

        [Test]
        public void RunDistanceMatchesAcrossFrameRates()
        {
            Func<float, MotorInput> runRight = _ => new MotorInput { MoveX = 1f };
            var at60 = Simulate(60f, 1f, runRight);
            var at30 = Simulate(30f, 1f, runRight);
            // Exact: 0.15 s ramp covers 0.3 m, then 0.85 s at 4 m/s covers 3.4 m → 3.7 m.
            Assert.That(at60.x, Is.EqualTo(3.7f).Within(0.01f));
            Assert.That(at30.x, Is.EqualTo(at60.x).Within(0.01f));
        }

        [Test]
        public void CoyoteTimeAllowsLateJump()
        {
            var tuning = Tuning();
            var state = MotorState.Initial;
            MovementModel.Step(ref state, default, true, 1f / 60f, tuning);
            // Walked off a ledge: airborne for 0.08 s (< 0.10 coyote) then press jump.
            for (int i = 0; i < 4; i++)
            {
                MovementModel.Step(ref state, default, false, 0.02f, tuning);
            }

            var step = MovementModel.Step(ref state, new MotorInput { JumpPressed = true }, false, 0.01f, tuning);
            Assert.IsTrue(step.Jumped);
        }

        [Test]
        public void JumpAfterCoyoteWindowIsRejected()
        {
            var tuning = Tuning();
            var state = MotorState.Initial;
            MovementModel.Step(ref state, default, true, 1f / 60f, tuning);
            for (int i = 0; i < 8; i++)
            {
                MovementModel.Step(ref state, default, false, 0.02f, tuning);
            }

            var step = MovementModel.Step(ref state, new MotorInput { JumpPressed = true }, false, 0.02f, tuning);
            Assert.IsFalse(step.Jumped);
        }

        [Test]
        public void BufferedJumpFiresOnLanding()
        {
            var tuning = Tuning();
            var state = MotorState.Initial;
            state.Grounded = false;
            state.InJump = false;
            state.TimeSinceGrounded = 1f;
            state.VelocityY = -3f;
            MovementModel.Step(ref state, new MotorInput { JumpPressed = true }, false, 0.05f, tuning);
            var landing = MovementModel.Step(ref state, default, true, 0.05f, tuning);
            Assert.IsTrue(landing.Jumped, "a press 0.05 s before landing (< 0.12 buffer) should jump on landing");
        }

        [Test]
        public void StaleBufferedJumpDoesNotFire()
        {
            var tuning = Tuning();
            var state = MotorState.Initial;
            state.Grounded = false;
            state.TimeSinceGrounded = 1f;
            state.VelocityY = -3f;
            MovementModel.Step(ref state, new MotorInput { JumpPressed = true }, false, 0.1f, tuning);
            MovementModel.Step(ref state, default, false, 0.1f, tuning);
            var landing = MovementModel.Step(ref state, default, true, 0.02f, tuning);
            Assert.IsFalse(landing.Jumped);
        }

        [Test]
        public void HoldingJumpDoesNotRepeat()
        {
            var tuning = Tuning();
            var state = MotorState.Initial;
            var first = MovementModel.Step(ref state, new MotorInput { JumpPressed = true }, true, 1f / 60f, tuning);
            Assert.IsTrue(first.Jumped);
            int jumps = 0;
            for (int i = 0; i < 120; i++)
            {
                // JumpPressed is an edge; a held button sends false after the first frame.
                if (MovementModel.Step(ref state, default, i > 40, 1f / 60f, tuning).Jumped)
                {
                    jumps++;
                }
            }

            Assert.AreEqual(0, jumps);
        }

        [Test]
        public void FallSpeedIsCapped()
        {
            var tuning = Tuning();
            var state = MotorState.Initial;
            for (int i = 0; i < 300; i++)
            {
                MovementModel.Step(ref state, default, false, 1f / 30f, tuning);
            }

            Assert.That(state.VelocityY, Is.EqualTo(-tuning.MaxFallSpeed).Within(1e-4f));
        }

        [Test]
        public void ZeroDeltaTimeIsANoOp()
        {
            var state = MotorState.Initial;
            var step = MovementModel.Step(ref state, new MotorInput { MoveX = 1f, JumpPressed = true }, true, 0f,
                Tuning());
            Assert.IsFalse(step.Jumped);
            Assert.AreEqual(0f, state.VelocityX);
        }
    }
}
