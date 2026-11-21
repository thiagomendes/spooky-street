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

        /// <summary>Jump apex height in meters.</summary>
        public float JumpApexHeight = 1f;

        /// <summary>Gravity in m/s² (positive number, applied downward).</summary>
        public float Gravity = 25f;

        /// <summary>Seconds after leaving ground during which a jump is still accepted.</summary>
        public float CoyoteTime = 0.10f;

        /// <summary>Seconds a jump press is remembered before landing.</summary>
        public float JumpBufferTime = 0.12f;

        /// <summary>Fraction of ground acceleration available in the air.</summary>
        public float AirControl = 0.6f;

        /// <summary>Terminal fall speed in m/s.</summary>
        public float MaxFallSpeed = 20f;

        /// <summary>Small downward speed kept while grounded so slopes and steps stay in contact.</summary>
        public float GroundStickSpeed = 2f;

        public float AccelerationRate => MaxSpeed / Math.Max(AccelerationTime, 0.0001f);

        public float DecelerationRate => MaxSpeed / Math.Max(DecelerationTime, 0.0001f);

        /// <summary>Initial upward speed that reaches exactly JumpApexHeight under Gravity.</summary>
        public float JumpVelocity => (float)Math.Sqrt(2.0 * Gravity * JumpApexHeight);

        public MovementTuningData Clone() => (MovementTuningData)MemberwiseClone();
    }
}
