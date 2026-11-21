using System;

namespace SpookyStreet.Core.Movement
{
    /// <summary>Per-frame input to the motor, already converted to world space (X/Z plane).</summary>
    public struct MotorInput
    {
        /// <summary>Desired direction × strength on world X, magnitude ≤ 1.</summary>
        public float MoveX;

        /// <summary>Desired direction × strength on world Z, magnitude ≤ 1.</summary>
        public float MoveZ;

        /// <summary>True only on the frame the jump control went down (edge, never held).</summary>
        public bool JumpPressed;
    }

    /// <summary>Mutable motor state carried between frames.</summary>
    public struct MotorState
    {
        public float VelocityX;
        public float VelocityY;
        public float VelocityZ;
        public bool Grounded;

        /// <summary>Seconds since the motor last stood on ground (0 while grounded).</summary>
        public float TimeSinceGrounded;

        /// <summary>Seconds since the last unconsumed jump press.</summary>
        public float TimeSinceJumpPressed;

        /// <summary>True between take-off and the next landing.</summary>
        public bool InJump;

        public static MotorState Initial => new MotorState
        {
            Grounded = true,
            TimeSinceGrounded = 0f,
            TimeSinceJumpPressed = float.PositiveInfinity,
        };

        public float HorizontalSpeed => (float)Math.Sqrt(VelocityX * VelocityX + VelocityZ * VelocityZ);
    }

    /// <summary>Displacement for this frame, to be passed to the character controller.</summary>
    public struct MotorStep
    {
        public float DeltaX;
        public float DeltaY;
        public float DeltaZ;
        public bool Jumped;
    }

    /// <summary>
    /// Engine-free character motor. All timers are in seconds and every integrator is exact for
    /// constant acceleration within a frame, so the same input produces the same trajectory at
    /// 30 and 60 fps (spec §5 "consistent time semantics").
    /// </summary>
    public static class MovementModel
    {
        public static MotorStep Step(ref MotorState state, MotorInput input, bool groundedAtFrameStart, float deltaTime,
            MovementTuningData tuning)
        {
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            var step = new MotorStep();
            if (deltaTime <= 0f)
            {
                return step;
            }

            // --- Grounding, coyote and buffer timers ---------------------------------------
            if (groundedAtFrameStart && state.VelocityY <= 0f)
            {
                state.Grounded = true;
                state.TimeSinceGrounded = 0f;
                state.InJump = false;
            }
            else
            {
                state.Grounded = false;
                state.TimeSinceGrounded += deltaTime;
            }

            if (input.JumpPressed)
            {
                state.TimeSinceJumpPressed = 0f;
            }

            bool jumpBuffered = state.TimeSinceJumpPressed <= tuning.JumpBufferTime;
            bool canJump = !state.InJump && state.TimeSinceGrounded <= tuning.CoyoteTime;
            if (jumpBuffered && canJump)
            {
                state.VelocityY = tuning.JumpVelocity;
                state.InJump = true;
                state.Grounded = false;
                state.TimeSinceJumpPressed = float.PositiveInfinity;
                state.TimeSinceGrounded = float.PositiveInfinity;
                step.Jumped = true;
            }

            state.TimeSinceJumpPressed += deltaTime;

            // --- Horizontal: accelerate toward the target velocity --------------------------
            float inputMagnitude = (float)Math.Sqrt(input.MoveX * input.MoveX + input.MoveZ * input.MoveZ);
            float inputScale = inputMagnitude > 1f ? 1f / inputMagnitude : 1f;
            float targetX = input.MoveX * inputScale * tuning.MaxSpeed;
            float targetZ = input.MoveZ * inputScale * tuning.MaxSpeed;

            float startX = state.VelocityX;
            float startZ = state.VelocityZ;
            bool speedingUp = targetX * targetX + targetZ * targetZ > startX * startX + startZ * startZ;
            float rate = speedingUp ? tuning.AccelerationRate : tuning.DecelerationRate;
            if (!state.Grounded)
            {
                rate *= tuning.AirControl;
            }

            float maxDelta = rate * deltaTime;
            float diffX = targetX - startX;
            float diffZ = targetZ - startZ;
            float diffLength = (float)Math.Sqrt(diffX * diffX + diffZ * diffZ);
            float reachTime;
            if (diffLength <= maxDelta || diffLength < 1e-6f)
            {
                state.VelocityX = targetX;
                state.VelocityZ = targetZ;
                reachTime = rate > 0f ? diffLength / rate : 0f;
            }
            else
            {
                state.VelocityX = startX + diffX / diffLength * maxDelta;
                state.VelocityZ = startZ + diffZ / diffLength * maxDelta;
                reachTime = deltaTime;
            }

            // Exact displacement: linear ramp for reachTime, then constant velocity.
            float rampTime = Math.Min(reachTime, deltaTime);
            float holdTime = deltaTime - rampTime;
            step.DeltaX = (startX + state.VelocityX) * 0.5f * rampTime + state.VelocityX * holdTime;
            step.DeltaZ = (startZ + state.VelocityZ) * 0.5f * rampTime + state.VelocityZ * holdTime;

            // --- Vertical: exact constant-gravity integration ---------------------------------
            if (state.Grounded && !step.Jumped)
            {
                state.VelocityY = -tuning.GroundStickSpeed;
                step.DeltaY = state.VelocityY * deltaTime;
            }
            else
            {
                float startY = state.VelocityY;
                float endY = startY - tuning.Gravity * deltaTime;
                if (endY < -tuning.MaxFallSpeed)
                {
                    // Accelerate until terminal speed, then fall at terminal speed.
                    float toTerminal = Math.Max(0f, (startY + tuning.MaxFallSpeed) / tuning.Gravity);
                    toTerminal = Math.Min(toTerminal, deltaTime);
                    step.DeltaY = startY * toTerminal - 0.5f * tuning.Gravity * toTerminal * toTerminal
                                  - tuning.MaxFallSpeed * (deltaTime - toTerminal);
                    endY = -tuning.MaxFallSpeed;
                }
                else
                {
                    step.DeltaY = startY * deltaTime - 0.5f * tuning.Gravity * deltaTime * deltaTime;
                }

                state.VelocityY = endY;
            }

            return step;
        }

        /// <summary>Call when the controller reports a ceiling hit so upward speed is cancelled.</summary>
        public static void OnCeilingHit(ref MotorState state)
        {
            if (state.VelocityY > 0f)
            {
                state.VelocityY = 0f;
            }
        }
    }
}
