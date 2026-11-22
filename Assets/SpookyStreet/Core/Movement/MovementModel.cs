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

        /// <summary>True while the jump control is down (variable jump height and the bat-cape glide).</summary>
        public bool JumpHeld;

        /// <summary>True only on the frame the Use control went down (edge, never held).</summary>
        public bool UsePressed;

        /// <summary>
        /// True while a contextual interaction is in range (the Use button shows its verb). Interaction always
        /// wins over roll and dive; the motor then only reports <see cref="MotorStep.Interacted"/>.
        /// </summary>
        public bool InteractionAvailable;

        /// <summary>
        /// Metres of free air below Pip's feet while airborne (a downward probe), or 0 when unknown / no ground in
        /// reach. Lets a press just before touchdown stay a buffered jump instead of spreading the cape.
        /// </summary>
        public float GroundDistance;

        /// <summary>Unit facing on world X/Z; aims a dive or roll started with almost no speed.</summary>
        public float FacingX;

        public float FacingZ;
    }

    /// <summary>What a press of Use does this frame (interaction first, then the moveset).</summary>
    public enum UseAction
    {
        None,
        Interact,
        Roll,
        Dive,
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

        /// <summary>Jump was let go since take-off: no hold boost (or no more of it) and no apex float.</summary>
        public bool JumpReleased;

        /// <summary>Seconds of the current jump left before the hold boost engages (Jump still held).</summary>
        public float BoostDelayLeft;

        /// <summary>
        /// Seconds of hold boost left in the current jump (pending or running); 0 once it has run out, Jump was let
        /// go, Pip dived or hit a ceiling.
        /// </summary>
        public float BoostTimeLeft;

        /// <summary>Upward speed the hold boost drives toward in the current jump (m/s).</summary>
        public float BoostSpeed;

        /// <summary>The hold boost engaged in the current jump: it is a high jump.</summary>
        public bool Boosted;

        /// <summary>Seconds of apex float left in the current jump.</summary>
        public float HangTimeLeft;

        /// <summary>Seconds of the current jump spent at or past its apex (falling); the glide waits for the hang.</summary>
        public float FallTime;

        /// <summary>Stage (1–3) of the last jump in the current chain; 0 when no chain is open.</summary>
        public int ChainStage;

        /// <summary>Seconds on the ground since the last landing (infinite before the first one).</summary>
        public float TimeSinceLanded;

        /// <summary>Bat cape spread: fall speed is clamped and air control rises.</summary>
        public bool Gliding;

        /// <summary>Seconds left in the current roll (0 when not rolling).</summary>
        public float RollTimeLeft;

        /// <summary>Seconds before another ground roll may start.</summary>
        public float RollCooldownLeft;

        /// <summary>In an air dive (ends in a roll on landing).</summary>
        public bool Diving;

        /// <summary>A dive was used since leaving the ground (one per airtime).</summary>
        public bool DiveUsed;

        /// <summary>Unit direction of the current roll or dive.</summary>
        public float MoveDirX;

        public float MoveDirZ;

        /// <summary>Horizontal speed held during the current roll or dive (m/s).</summary>
        public float MoveSpeed;

        public static MotorState Initial => new MotorState
        {
            Grounded = true,
            TimeSinceGrounded = 0f,
            TimeSinceJumpPressed = float.PositiveInfinity,
            TimeSinceLanded = float.PositiveInfinity,
            MoveDirZ = 1f,
        };

        public float HorizontalSpeed => (float)Math.Sqrt(VelocityX * VelocityX + VelocityZ * VelocityZ);

        public bool Rolling => RollTimeLeft > 0f;

        /// <summary>
        /// Rising with Jump still held since take-off: the held, higher jump (the avatar stretches while this is
        /// true so the difference from a tap is visible).
        /// </summary>
        public bool HoldingJumpRise => InJump && !JumpReleased && !Diving && VelocityY > 0f;

        /// <summary>The hold boost is driving Pip up right now (high jump thrust).</summary>
        public bool Boosting => InJump && !JumpReleased && !Diving && BoostTimeLeft > 0f && BoostDelayLeft <= 0f;

        /// <summary>Use would dive now (airborne past coyote time, no dive yet this airtime, not rolling).</summary>
        public bool CanDive(MovementTuningData tuning) =>
            !Grounded && TimeSinceGrounded > tuning.CoyoteTime && !Diving && !DiveUsed && !Rolling;
    }

    /// <summary>Displacement for this frame, to be passed to the character controller, plus what happened.</summary>
    public struct MotorStep
    {
        public float DeltaX;
        public float DeltaY;
        public float DeltaZ;
        public bool Jumped;

        /// <summary>Chain stage (1–3) of the jump that started this frame; 0 without a jump.</summary>
        public int ChainStage;

        /// <summary>Touched down this frame after being airborne.</summary>
        public bool Landed;

        /// <summary>The hold boost engaged this frame (Jump still held about 0.1 s after take-off).</summary>
        public bool BoostStarted;

        public bool GlideStarted;
        public bool GlideEnded;
        public bool RollStarted;
        public bool RollEnded;
        public bool DiveStarted;

        /// <summary>A dive touched down this frame (a roll starts with it).</summary>
        public bool DiveLanded;

        /// <summary>Use went to the contextual interaction this frame (the motor did nothing with it).</summary>
        public bool Interacted;
    }

    /// <summary>
    /// Engine-free character motor. All timers are in seconds and every integrator is exact for
    /// constant acceleration within a frame, so the same input produces the same trajectory at
    /// 30 and 60 fps (spec §5 "consistent time semantics").
    /// Moveset (spec §5 v1.2), all on Jump and Use: variable jump height, chain jump, bat-cape glide, ground
    /// roll and air dive. Wall slide/jump and ledge grab need climbable geometry and are not here yet: they
    /// would add a state flag beside <see cref="MotorState.Gliding"/>, a probe result in
    /// <see cref="MotorInput"/> and their own branch in the vertical integration, like the glide.
    /// </summary>
    public static class MovementModel
    {
        private const float DegToRad = (float)(Math.PI / 180.0);

        /// <summary>
        /// What pressing Use would do now: the interaction when one is in range, else a roll (on the ground,
        /// fast enough, not rolling or cooling down) or a dive (in the air, once per airtime), else nothing.
        /// </summary>
        public static UseAction ResolveUse(MotorState state, MotorInput input, MovementTuningData tuning)
        {
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            if (input.InteractionAvailable)
            {
                return UseAction.Interact;
            }

            if (state.Grounded)
            {
                bool fastEnough = state.HorizontalSpeed >= tuning.RollMinSpeedFraction * tuning.MaxSpeed - 1e-4f;
                return !state.Rolling && state.RollCooldownLeft <= 0f && fastEnough ? UseAction.Roll : UseAction.None;
            }

            return !state.Diving && !state.DiveUsed && !state.Rolling ? UseAction.Dive : UseAction.None;
        }

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

            // --- Grounding, landing, coyote and buffer timers ------------------------------------
            bool wasGrounded = state.Grounded;
            if (groundedAtFrameStart && state.VelocityY <= 0f)
            {
                state.Grounded = true;
                state.TimeSinceGrounded = 0f;
                state.InJump = false;
                state.DiveUsed = false;
                if (!wasGrounded)
                {
                    step.Landed = true;
                    state.TimeSinceLanded = 0f;
                }
                else
                {
                    state.TimeSinceLanded += deltaTime;
                }

                if (state.Gliding)
                {
                    state.Gliding = false;
                    step.GlideEnded = true;
                }

                if (state.Diving)
                {
                    // The dive lands in a roll that carries its speed (spec §5 "short forward dive").
                    state.Diving = false;
                    step.DiveLanded = true;
                    StartRoll(ref state, state.MoveDirX, state.MoveDirZ, Math.Max(state.HorizontalSpeed, state.MoveSpeed),
                        input, tuning);
                    step.RollStarted = true;
                }

                if (state.ChainStage > 0 && state.TimeSinceLanded > tuning.ChainJumpWindow)
                {
                    state.ChainStage = 0;
                }
            }
            else
            {
                state.Grounded = false;
                state.TimeSinceGrounded += deltaTime;
                // A roll ends when Pip really leaves the ground; a one-frame step-down keeps it.
                if (state.Rolling && state.TimeSinceGrounded > tuning.CoyoteTime)
                {
                    EndRoll(ref state, ref step, tuning);
                }
            }

            if (input.JumpPressed)
            {
                state.TimeSinceJumpPressed = 0f;
            }

            bool jumpBuffered = state.TimeSinceJumpPressed <= tuning.JumpBufferTime;
            bool canJump = !state.InJump && !state.Diving && state.TimeSinceGrounded <= tuning.CoyoteTime;
            if (jumpBuffered && canJump)
            {
                int stage = 1;
                bool running = state.HorizontalSpeed >= tuning.ChainJumpMinSpeedFraction * tuning.MaxSpeed - 1e-4f;
                if (state.ChainStage > 0 && state.ChainStage < 3 && state.TimeSinceLanded <= tuning.ChainJumpWindow &&
                    running)
                {
                    stage = state.ChainStage + 1;
                }

                if (state.Rolling)
                {
                    // Jumping out of a roll keeps its momentum.
                    EndRoll(ref state, ref step, tuning);
                }

                float scale = tuning.ChainHeightScale(stage);
                state.ChainStage = stage;
                state.VelocityY = tuning.JumpVelocityFor(scale);
                state.InJump = true;
                state.JumpReleased = false;
                state.BoostDelayLeft = Math.Max(0f, tuning.JumpBoostDelay);
                state.BoostTimeLeft = tuning.JumpBoostDurationFor(scale);
                state.BoostSpeed = tuning.JumpBoostSpeedFor(scale);
                state.Boosted = false;
                state.HangTimeLeft = Math.Max(0f, tuning.ApexHangTime);
                state.FallTime = 0f;
                state.Grounded = false;
                state.TimeSinceJumpPressed = float.PositiveInfinity;
                state.TimeSinceGrounded = float.PositiveInfinity;
                state.TimeSinceLanded = float.PositiveInfinity;
                step.Jumped = true;
                step.ChainStage = stage;
            }

            state.TimeSinceJumpPressed += deltaTime;
            if (!input.JumpHeld)
            {
                state.JumpReleased = true;
            }

            if (state.JumpReleased)
            {
                // A tap (or a hold let go) gets no more boost: the rest of the rise is plain gravity.
                state.BoostTimeLeft = 0f;
            }

            // --- Use: interaction first, then roll (ground) or dive (air) ------------------------
            if (!state.Rolling && state.RollCooldownLeft > 0f)
            {
                state.RollCooldownLeft = Math.Max(0f, state.RollCooldownLeft - deltaTime);
            }

            if (input.UsePressed && !step.Jumped)
            {
                switch (ResolveUse(state, input, tuning))
                {
                    case UseAction.Interact:
                        step.Interacted = true;
                        break;
                    case UseAction.Roll:
                        StartRoll(ref state, state.VelocityX, state.VelocityZ, state.HorizontalSpeed + tuning.RollSpeedBoost,
                            input, tuning);
                        step.RollStarted = true;
                        break;
                    case UseAction.Dive:
                        // Use while gliding folds the cape into the dive.
                        if (state.Gliding)
                        {
                            step.GlideEnded = true;
                        }

                        StartDive(ref state, input, tuning);
                        step.DiveStarted = true;
                        break;
                }
            }

            // --- Bat-cape glide: Jump held while falling (after the apex and its hang) ---------------
            // A fresh press that will touch down within the jump buffer is a buffered (chain) jump, not a glide: on
            // touch every press is briefly a hold, and spreading the cape there would brake the fall past the buffer
            // and lose the jump. Higher up a fresh press glides at once. In a jump the cape waits until Pip has been
            // past the apex for the apex hang (TestFlight 12.1: "when I hold, she just comes down gliding"), so a
            // held high jump peaks, floats, and only then opens the cape.
            bool pressPending = state.TimeSinceJumpPressed <= tuning.JumpBufferTime;
            bool landingJump = pressPending && !state.Gliding &&
                               TimeToGround(state.VelocityY, input.GroundDistance, tuning.Gravity) <= tuning.JumpBufferTime;
            bool pastApexHang = !state.InJump || state.FallTime >= tuning.ApexHangTime - 1e-5f;
            bool wantGlide = !state.Grounded && input.JumpHeld && state.VelocityY <= 0f && !state.Diving &&
                             !state.Rolling && !landingJump && pastApexHang;
            if (wantGlide && !state.Gliding)
            {
                state.Gliding = true;
                step.GlideStarted = true;
            }
            else if (!wantGlide && state.Gliding)
            {
                state.Gliding = false;
                step.GlideEnded = true;
            }

            // --- Horizontal -----------------------------------------------------------------------
            if (state.Rolling || state.Diving)
            {
                float turn = (state.Diving ? tuning.DiveTurnRateDegrees : tuning.RollTurnRateDegrees) * DegToRad * deltaTime;
                SteerToward(ref state.MoveDirX, ref state.MoveDirZ, input.MoveX, input.MoveZ, turn);
                state.VelocityX = state.MoveDirX * state.MoveSpeed;
                state.VelocityZ = state.MoveDirZ * state.MoveSpeed;
                step.DeltaX = state.VelocityX * deltaTime;
                step.DeltaZ = state.VelocityZ * deltaTime;
            }
            else
            {
                Accelerate(ref state, input, deltaTime, tuning, ref step);
            }

            // --- Vertical: exact piecewise constant-acceleration integration ----------------------
            if (state.Grounded && !step.Jumped)
            {
                state.VelocityY = -tuning.GroundStickSpeed;
                step.DeltaY = state.VelocityY * deltaTime;
            }
            else
            {
                state.VelocityY = IntegrateAir(ref state, ref step, deltaTime, tuning, out float dy);
                step.DeltaY = dy;
            }

            // --- Roll timer -----------------------------------------------------------------------
            if (state.Rolling)
            {
                state.RollTimeLeft -= deltaTime;
                if (state.RollTimeLeft <= 1e-6f)
                {
                    EndRoll(ref state, ref step, tuning);
                }
            }

            return step;
        }

        /// <summary>
        /// Airborne vertical motion, split exactly at each phase boundary inside the frame: the boost delay (plain
        /// gravity), the hold boost (ramp up to the boost speed, then hold it), the rise to the apex, the apex float
        /// of a held high jump, then the fall (capped by the glide). Returns the new vertical speed.
        /// </summary>
        private static float IntegrateAir(ref MotorState state, ref MotorStep step, float deltaTime,
            MovementTuningData tuning, out float dy)
        {
            float v = state.VelocityY;
            float time = deltaTime;
            float g = tuning.Gravity;
            float terminal = state.Gliding ? -tuning.GlideFallSpeed : -tuning.MaxFallSpeed;
            dy = 0f;
            for (int guard = 0; guard < 8 && time > 0f; guard++)
            {
                if (state.BoostTimeLeft > 0f && state.InJump && !state.Diving)
                {
                    if (state.BoostDelayLeft > 0f)
                    {
                        float wait = Math.Min(time, state.BoostDelayLeft);
                        float slice = wait;
                        dy += Approach(ref v, -tuning.MaxFallSpeed, g, ref slice, true);
                        state.BoostDelayLeft -= wait;
                        time -= wait;
                        if (state.BoostDelayLeft <= 1e-6f)
                        {
                            state.BoostDelayLeft = 0f;
                            state.Boosted = true;
                            step.BoostStarted = true;
                        }

                        continue;
                    }

                    float burn = Math.Min(time, state.BoostTimeLeft);
                    float boostSlice = burn;
                    float rate = v < state.BoostSpeed ? tuning.JumpBoostAcceleration : g;
                    dy += Approach(ref v, state.BoostSpeed, rate, ref boostSlice, true);
                    state.BoostTimeLeft -= burn;
                    time -= burn;
                    if (state.BoostTimeLeft <= 1e-6f)
                    {
                        state.BoostTimeLeft = 0f;
                    }

                    continue;
                }

                if (v > 0f)
                {
                    // Rising to the apex under plain gravity (stops at the apex if it is inside this frame).
                    dy += Approach(ref v, 0f, g, ref time, false);
                    continue;
                }

                bool hang = state.InJump && state.Boosted && !state.JumpReleased && !state.Diving && !state.Gliding &&
                            state.HangTimeLeft > 0f;
                if (hang)
                {
                    float floatTime = Math.Min(time, state.HangTimeLeft);
                    float hangSlice = floatTime;
                    dy += Approach(ref v, terminal, g * tuning.ApexHangGravityScale, ref hangSlice, true);
                    state.HangTimeLeft -= floatTime;
                    state.FallTime += floatTime;
                    time -= floatTime;
                    continue;
                }

                float fall = time;
                float fallRate = v >= terminal ? g : state.Gliding ? tuning.GlideBrakeDeceleration : g;
                dy += Approach(ref v, terminal, fallRate, ref time, true);
                state.FallTime += fall;
            }

            return v;
        }

        /// <summary>
        /// Seconds of free fall until <paramref name="groundDistance"/> metres are covered from vertical speed
        /// <paramref name="velocityY"/>; infinite when the distance is unknown (≤ 0).
        /// </summary>
        public static float TimeToGround(float velocityY, float groundDistance, float gravity)
        {
            if (groundDistance <= 0f || gravity <= 0f)
            {
                return float.PositiveInfinity;
            }

            float down = Math.Max(0f, -velocityY);
            return (-down + (float)Math.Sqrt(down * down + 2f * gravity * groundDistance)) / gravity;
        }

        /// <summary>Call when the controller reports a ceiling hit so upward speed is cancelled.</summary>
        public static void OnCeilingHit(ref MotorState state)
        {
            if (state.VelocityY > 0f)
            {
                state.VelocityY = 0f;
            }

            state.BoostTimeLeft = 0f;
        }

        private static void Accelerate(ref MotorState state, MotorInput input, float deltaTime, MovementTuningData tuning,
            ref MotorStep step)
        {
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
                rate *= state.Gliding ? tuning.GlideAirControl : tuning.AirControl;
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
        }

        /// <summary>
        /// Moves <paramref name="v"/> toward <paramref name="target"/> at constant <paramref name="rate"/> for up to
        /// <paramref name="time"/> seconds and returns the exact displacement. Consumes the time used; with
        /// <paramref name="holdAtTarget"/> the rest of the time is spent at the target speed.
        /// </summary>
        private static float Approach(ref float v, float target, float rate, ref float time, bool holdAtTarget)
        {
            if (time <= 0f)
            {
                return 0f;
            }

            float diff = target - v;
            float d = 0f;
            if (rate > 0f && diff != 0f)
            {
                float a = diff > 0f ? rate : -rate;
                float t = Math.Min(time, Math.Abs(diff) / rate);
                d = v * t + 0.5f * a * t * t;
                v = t < time ? target : v + a * t;
                time -= t;
            }

            if (holdAtTarget && time > 0f)
            {
                d += v * time;
                time = 0f;
            }

            return d;
        }

        private static void StartRoll(ref MotorState state, float dirX, float dirZ, float speed, MotorInput input,
            MovementTuningData tuning)
        {
            PickDirection(ref dirX, ref dirZ, input);
            state.MoveDirX = dirX;
            state.MoveDirZ = dirZ;
            state.MoveSpeed = speed;
            state.RollTimeLeft = Math.Max(1e-3f, tuning.RollDuration);
            state.Gliding = false;
        }

        private static void EndRoll(ref MotorState state, ref MotorStep step, MovementTuningData tuning)
        {
            state.RollTimeLeft = 0f;
            state.RollCooldownLeft = tuning.RollCooldown;
            step.RollEnded = true;
        }

        private static void StartDive(ref MotorState state, MotorInput input, MovementTuningData tuning)
        {
            float dirX = state.VelocityX;
            float dirZ = state.VelocityZ;
            if (dirX * dirX + dirZ * dirZ < 0.25f)
            {
                dirX = input.MoveX;
                dirZ = input.MoveZ;
            }

            PickDirection(ref dirX, ref dirZ, input);
            state.MoveDirX = dirX;
            state.MoveDirZ = dirZ;
            state.MoveSpeed = Math.Max(state.HorizontalSpeed, tuning.DiveSpeed);
            state.VelocityY = tuning.DiveHopVelocity;
            state.Diving = true;
            state.DiveUsed = true;
            state.Gliding = false;
            state.JumpReleased = true;
            state.BoostTimeLeft = 0f;
        }

        /// <summary>Normalizes the direction, falling back to the input's facing, then +Z.</summary>
        private static void PickDirection(ref float x, ref float z, MotorInput input)
        {
            float length = (float)Math.Sqrt(x * x + z * z);
            if (length < 1e-3f)
            {
                x = input.FacingX;
                z = input.FacingZ;
                length = (float)Math.Sqrt(x * x + z * z);
            }

            if (length < 1e-3f)
            {
                x = 0f;
                z = 1f;
                return;
            }

            x /= length;
            z /= length;
        }

        /// <summary>Turns the unit direction toward the stick by at most <paramref name="maxRadians"/>.</summary>
        private static void SteerToward(ref float dirX, ref float dirZ, float inputX, float inputZ, float maxRadians)
        {
            if (inputX * inputX + inputZ * inputZ < 0.01f || maxRadians <= 0f)
            {
                return;
            }

            double current = Math.Atan2(dirZ, dirX);
            double target = Math.Atan2(inputZ, inputX);
            double diff = target - current;
            while (diff > Math.PI)
            {
                diff -= 2.0 * Math.PI;
            }

            while (diff < -Math.PI)
            {
                diff += 2.0 * Math.PI;
            }

            double turned = current + Math.Max(-maxRadians, Math.Min(maxRadians, diff));
            dirX = (float)Math.Cos(turned);
            dirZ = (float)Math.Sin(turned);
        }
    }
}
