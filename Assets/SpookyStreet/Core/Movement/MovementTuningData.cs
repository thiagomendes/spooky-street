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

        /// <summary>Height scale of the second chained jump (held: 2.1 m → 2.4 m; tapped: 1.0 → 1.14 m).</summary>
        public float ChainJumpSecondHeightScale = 8f / 7f;

        /// <summary>Height scale of the third chained jump (held: 2.1 m → 2.8 m with a flip; tapped: 1.33 m).</summary>
        public float ChainJumpThirdHeightScale = 4f / 3f;

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
        public float JumpBoostDurationFor(float heightScale)
        {
            float scale = Math.Max(0.01f, heightScale);
            float g = Math.Max(0.01f, Gravity);
            float target = EffectiveHoldApexHeight * scale;
            float v0 = JumpVelocityFor(scale);
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

        public MovementTuningData Clone() => (MovementTuningData)MemberwiseClone();
    }
}
