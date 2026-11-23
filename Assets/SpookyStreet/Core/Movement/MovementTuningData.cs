using System;

namespace SpookyStreet.Core.Movement
{
    /// <summary>
    /// Plain movement tuning values (spec §5). The Unity side exposes these through the
    /// PipMovementTuning ScriptableObject so they are editable as inspector data.
    /// </summary>
    [Serializable]
    public sealed class MovementTuningData
    {
        /// <summary>Maximum ground speed in m/s.</summary>
        public float MaxSpeed = 4f;

        /// <summary>Seconds to go from rest to MaxSpeed.</summary>
        public float AccelerationTime = 0.15f;

        /// <summary>Seconds to go from MaxSpeed to rest when input is released.</summary>
        public float DecelerationTime = 0.12f;

        /// <summary>
        /// Apex height in meters of a tapped jump (Jump released before <see cref="JumpBoostDelay"/>): the take-off
        /// speed reaches exactly this under plain gravity. Routes rely on it (S002 interior jump distances).
        /// </summary>
        public float JumpApexHeight = 1f;

        /// <summary>
        /// Apex height in meters while Jump is held through the rise (spec §5 v1.2 variable jump). The extra height
        /// comes from the hold boost, early in the arc (D21 amendment after TestFlight 12.1: 1.6 → 2.1 m).
        /// </summary>
        public float JumpHoldApexHeight = 2.1f;

        /// <summary>
        /// Seconds after take-off with Jump still held before the hold boost engages. A tap released sooner is a
        /// plain <see cref="JumpApexHeight"/> jump, however it lands within that time.
        /// </summary>
        public float JumpBoostDelay = 0.1f;

        /// <summary>
        /// Upward speed (m/s) the hold boost drives Pip to and holds for the boost phase: a sustained thrust, so a
        /// held jump visibly shoots up instead of only floating longer. Chain stages scale it by √(height scale).
        /// </summary>
        public float JumpBoostSpeed = 6f;

        /// <summary>Net upward acceleration (m/s²) that ramps the rise up to <see cref="JumpBoostSpeed"/>.</summary>
        public float JumpBoostAcceleration = 50f;

        /// <summary>
        /// Apex float of a held jump (seconds): past the apex with Jump still held, Pip hangs under reduced gravity
        /// before the cape may open. Every jump's glide waits this long past its apex, so the high jump and the
        /// glide read as two moves.
        /// </summary>
        public float ApexHangTime = 0.12f;

        /// <summary>Gravity multiplier during the apex float.</summary>
        public float ApexHangGravityScale = 0.25f;

        /// <summary>Gravity in m/s² (positive number, applied downward).</summary>
        public float Gravity = 25f;

        /// <summary>Seconds after leaving ground during which a jump is still accepted.</summary>
        public float CoyoteTime = 0.10f;

        /// <summary>
        /// Seconds a jump press is remembered before landing: a press this long before touchdown still jumps, and
        /// continues an open chain (S000.6 feel pass: 0.12 → 0.15 s so a thumb that is a little early still chains).
        /// A fresh press in the air spreads the cape only once it has been held longer than this, so an early chain
        /// press never brakes the fall into a glide and misses the landing.
        /// </summary>
        public float JumpBufferTime = 0.15f;

        /// <summary>Fraction of ground acceleration available in the air.</summary>
        public float AirControl = 0.6f;

        /// <summary>Terminal fall speed in m/s.</summary>
        public float MaxFallSpeed = 20f;

        /// <summary>Small downward speed kept while grounded so slopes and steps stay in contact.</summary>
        public float GroundStickSpeed = 2f;

        // --- Chain jump -------------------------------------------------------------------------

        /// <summary>Seconds after landing in which another jump continues the chain (feel pass: 0.2 → 0.35 s).</summary>
        public float ChainJumpWindow = 0.35f;

        /// <summary>
        /// Chain continues only at or above this fraction of MaxSpeed (feel pass: 0.5 → 0.25, so a light jog chains;
        /// standing still or creeping does not).
        /// </summary>
        public float ChainJumpMinSpeedFraction = 0.25f;

        /// <summary>Held height scale of the second chained jump (2.1 m → 2.4 m).</summary>
        public float ChainJumpSecondHeightScale = 8f / 7f;

        /// <summary>Held height scale of the third chained jump (2.1 m → 2.8 m, with a flip).</summary>
        public float ChainJumpThirdHeightScale = 4f / 3f;

        /// <summary>
        /// Tapped height scale of the second chained jump (1.0 m → 1.25 m). Kept as on TestFlight 12.1, where the
        /// owner already chained with taps; separate from the held scale so the high jump does not weaken it.
        /// </summary>
        public float ChainJumpSecondTapScale = 1.25f;

        /// <summary>Tapped height scale of the third chained jump (1.0 m → 1.5 m).</summary>
        public float ChainJumpThirdTapScale = 1.5f;

        // --- Bat-cape glide ---------------------------------------------------------------------

        /// <summary>Fall speed (m/s, positive) while the cape is spread.</summary>
        public float GlideFallSpeed = 1.75f;

        /// <summary>Upward deceleration (m/s²) that brakes a fast fall down to GlideFallSpeed.</summary>
        public float GlideBrakeDeceleration = 30f;

        /// <summary>Fraction of ground acceleration available while gliding (a little more than AirControl).</summary>
        public float GlideAirControl = 0.8f;

        // --- Roll and dive (Use with no interaction in range) -----------------------------------

        /// <summary>Roll lasts this long (seconds).</summary>
        public float RollDuration = 0.5f;

        /// <summary>Speed added to the current ground speed when a roll starts (m/s).</summary>
        public float RollSpeedBoost = 1.5f;

        /// <summary>Seconds after a roll ends before another can start.</summary>
        public float RollCooldown = 0.25f;

        /// <summary>A ground roll needs at least this fraction of MaxSpeed.</summary>
        public float RollMinSpeedFraction = 0.6f;

        /// <summary>How fast the stick can bend a roll's direction (degrees per second).</summary>
        public float RollTurnRateDegrees = 120f;

        /// <summary>Horizontal speed of an air dive (m/s); a faster Pip keeps its speed.</summary>
        public float DiveSpeed = 5.5f;

        /// <summary>Small upward pop when the dive starts (m/s).</summary>
        public float DiveHopVelocity = 3f;

        /// <summary>How fast the stick can bend a dive's direction (degrees per second).</summary>
        public float DiveTurnRateDegrees = 90f;

        // --- Wall slide and wall jump (S003; only on surfaces marked WallJump) -------------------

        /// <summary>Pip's capsule radius (PipMovementTuning.radius): contact distances are measured from her surface.</summary>
        public float BodyRadius = 0.28f;

        /// <summary>A marked wall counts as touched while its face is within the radius plus this (metres).</summary>
        public float WallContactSlack = 0.12f;

        /// <summary>A slide needs at least this much air under Pip's feet, so a hop into a wall never sticks to it.</summary>
        public float WallSlideMinFeetHeight = 0.35f;

        /// <summary>The stick or Pip's motion must point this much into the wall (cosine × speed fraction) to start a slide.</summary>
        public float WallSlideIntoMin = 0.3f;

        /// <summary>First seconds of a slide, when Pip grips and slows right down (the slide reads as a catch).</summary>
        public float WallSlideGripTime = 0.2f;

        /// <summary>Fall speed during the grip (m/s).</summary>
        public float WallSlideGripSpeed = 0.6f;

        /// <summary>Fall speed cap of the slide (m/s): the WallSlide clip is authored at about 1.5 m/s.</summary>
        public float WallSlideSpeed = 1.5f;

        /// <summary>Upward deceleration that brakes a faster fall into the slide (m/s²).</summary>
        public float WallSlideBrakeDeceleration = 30f;

        /// <summary>Seconds the stick must point away from the wall before Pip lets go (a brush of the thumb does not).</summary>
        public float WallSlideReleaseTime = 0.25f;

        /// <summary>A wall jump rises this high above the kick under plain gravity (metres).</summary>
        public float WallJumpHeight = 1.2f;

        /// <summary>Horizontal speed away from the wall at the kick (m/s; a little above the run).</summary>
        public float WallJumpAwaySpeed = 4.5f;

        /// <summary>Seconds after leaving a wall (or touching it without sliding) in which Jump still kicks off it.</summary>
        public float WallJumpCoyoteTime = 0.2f;

        /// <summary>Seconds after a kick with reduced air control, so a stick still held at the wall does not cancel it.</summary>
        public float WallJumpControlLockTime = 0.25f;

        /// <summary>Fraction of ground acceleration during the control lock.</summary>
        public float WallJumpLockedAirControl = 0.15f;

        // --- Ledge grab and climb (S003; only on surfaces marked Ledge) ---------------------------

        /// <summary>Height of the hands above the feet while hanging: the LedgeHang clip's left hand grips at 1.27 m.</summary>
        public float LedgeHangHandHeight = 1.27f;

        /// <summary>Pip's centre hangs this far out from the face (her radius, the skin and a little air).</summary>
        public float LedgeHangWallOffset = 0.34f;

        /// <summary>The catch window reaches this far below the hanging hand height (Pip rose a little short).</summary>
        public float LedgeGrabReachBelow = 0.35f;

        /// <summary>The catch window reaches this far above the hanging hand height (Pip is already falling past).</summary>
        public float LedgeGrabReachAbove = 0.3f;

        /// <summary>The edge may be this far beyond Pip's surface and still be caught (metres).</summary>
        public float LedgeGrabReachForward = 0.35f;

        /// <summary>No catch while rising faster than this (m/s): a jump past a ledge reaches its top first.</summary>
        public float LedgeGrabMaxRiseSpeed = 1.5f;

        /// <summary>Hanging needs at least this much air under the feet (otherwise Pip would stand, not hang).</summary>
        public float LedgeMinHangClearance = 0.15f;

        /// <summary>Seconds of the generous snap onto the hang spot.</summary>
        public float LedgeSnapTime = 0.1f;

        /// <summary>Seconds of the climb up from the hang onto the top.</summary>
        public float LedgeClimbTime = 0.4f;

        /// <summary>Share of the climb spent rising; the rest steps over the edge.</summary>
        public float LedgeClimbRiseFraction = 0.6f;

        /// <summary>The climb ends with Pip's centre this far in from the edge (metres).</summary>
        public float LedgeClimbInset = 0.4f;

        /// <summary>The climb lifts the feet this much above the top before stepping over it (metres).</summary>
        public float LedgeClimbLift = 0.06f;

        /// <summary>Seconds the stick must be pulled down or away before Pip drops.</summary>
        public float LedgeDropHoldTime = 0.1f;

        /// <summary>Speed away from the wall when Pip lets go (m/s), so she clears the ledge as she falls.</summary>
        public float LedgeDropPushSpeed = 1f;

        /// <summary>Seconds after a drop before any ledge can be caught again.</summary>
        public float LedgeRegrabDelay = 0.4f;

        public float AccelerationRate => MaxSpeed / Math.Max(AccelerationTime, 0.0001f);

        public float DecelerationRate => MaxSpeed / Math.Max(DecelerationTime, 0.0001f);

        /// <summary>Held apex height, never below the tapped one.</summary>
        public float EffectiveHoldApexHeight => Math.Max(JumpHoldApexHeight, Math.Max(0.01f, JumpApexHeight));

        /// <summary>Take-off speed of a plain (stage 1) jump: reaches exactly the tapped apex under Gravity.</summary>
        public float JumpVelocity => JumpVelocityFor(1f);

        /// <summary>Take-off speed for a jump whose apex heights are scaled by <paramref name="heightScale"/>.</summary>
        public float JumpVelocityFor(float heightScale) =>
            (float)Math.Sqrt(2.0 * Gravity * Math.Max(0.01f, JumpApexHeight) * Math.Max(0.01f, heightScale));

        /// <summary>Hold-boost speed for a jump scaled by <paramref name="heightScale"/>.</summary>
        public float JumpBoostSpeedFor(float heightScale) =>
            Math.Max(0.1f, JumpBoostSpeed) * (float)Math.Sqrt(Math.Max(0.01f, heightScale));

        /// <summary>
        /// Seconds the hold boost lasts once engaged (its ramp up to the boost speed plus the hold at that speed) so
        /// that a jump held through the rise peaks at exactly the held apex times <paramref name="heightScale"/>.
        /// Exact for constant acceleration; at least the ramp when the boost alone would overshoot.
        /// </summary>
        public float JumpBoostDurationFor(float heightScale) => JumpBoostDurationFor(heightScale, heightScale);

        /// <summary>
        /// Boost duration for a jump whose tapped apex is scaled by <paramref name="tapScale"/> (take-off speed) and
        /// whose held apex is scaled by <paramref name="holdScale"/> (boost speed and target).
        /// </summary>
        public float JumpBoostDurationFor(float tapScale, float holdScale)
        {
            float scale = Math.Max(0.01f, holdScale);
            float g = Math.Max(0.01f, Gravity);
            float target = EffectiveHoldApexHeight * scale;
            float v0 = JumpVelocityFor(tapScale);
            float delay = Math.Max(0f, JumpBoostDelay);
            float vd = v0 - g * delay;
            float yd = v0 * delay - 0.5f * g * delay * delay;
            float vb = JumpBoostSpeedFor(scale);
            float rampRate = vd < vb ? Math.Max(0.01f, JumpBoostAcceleration) : g;
            float ramp = Math.Abs(vb - vd) / rampRate;
            float yr = yd + (vd + vb) * 0.5f * ramp;
            float hold = (target - yr - vb * vb / (2f * g)) / vb;
            return ramp + Math.Max(0f, hold);
        }

        /// <summary>Height scale of chain stage 1, 2 or 3.</summary>
        public float ChainHeightScale(int stage) =>
            stage >= 3 ? ChainJumpThirdHeightScale : stage == 2 ? ChainJumpSecondHeightScale : 1f;

        /// <summary>Tapped height scale of chain stage 1, 2 or 3 (sets the take-off speed).</summary>
        public float ChainTapScale(int stage) =>
            stage >= 3 ? ChainJumpThirdTapScale : stage == 2 ? ChainJumpSecondTapScale : 1f;

        /// <summary>Hold-boost duration of chain stage 1, 2 or 3.</summary>
        public float JumpBoostDurationForStage(int stage) =>
            JumpBoostDurationFor(ChainTapScale(stage), ChainHeightScale(stage));

        public MovementTuningData Clone() => (MovementTuningData)MemberwiseClone();
    }
}
