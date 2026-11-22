using SpookyStreet.Core.Movement;
using UnityEngine;

namespace SpookyStreet.Player
{
    /// <summary>Inspector-editable movement and camera tuning (spec §5 "expose these values as inspector data").</summary>
    [CreateAssetMenu(menuName = "Spooky Street/Pip Movement Tuning", fileName = "PipMovementTuning")]
    public sealed class PipMovementTuning : ScriptableObject
    {
        [Header("Ground and air (meters, seconds)")]
        [Min(0.1f)] public float maxSpeed = 4f;
        [Min(0.01f)] public float accelerationTime = 0.15f;
        [Min(0.01f)] public float decelerationTime = 0.12f;
        [Tooltip("Apex of a tapped jump (Jump released at once).")]
        [Min(0.1f)] public float jumpApexHeight = 1f;
        [Tooltip("Apex while Jump is held through the rise; releasing early cuts in between.")]
        [Min(0.1f)] public float jumpHoldApexHeight = 1.6f;
        [Min(1f)] public float gravity = 25f;
        [Min(0f)] public float coyoteTime = 0.10f;
        [Min(0f)] public float jumpBufferTime = 0.12f;
        [Range(0f, 1f)] public float airControl = 0.6f;
        [Min(1f)] public float maxFallSpeed = 20f;

        [Header("Chain jump (jump again right after landing while running)")]
        [Min(0f)] public float chainJumpWindow = 0.2f;
        [Tooltip("Fraction of max speed needed to keep the chain going.")]
        [Range(0f, 1f)] public float chainJumpMinSpeedFraction = 0.5f;
        [Min(1f)] public float chainJumpSecondHeightScale = 1.25f;
        [Min(1f)] public float chainJumpThirdHeightScale = 1.5f;

        [Header("Bat-cape glide (hold Jump while falling)")]
        [Min(0.5f)] public float glideFallSpeed = 1.75f;
        [Tooltip("Upward deceleration that brakes a fast fall to the glide speed (m/s²).")]
        [Min(1f)] public float glideBrakeDeceleration = 30f;
        [Range(0f, 1f)] public float glideAirControl = 0.8f;

        [Header("Roll (Use on the ground) and dive (Use in the air)")]
        [Min(0.1f)] public float rollDuration = 0.5f;
        [Min(0f)] public float rollSpeedBoost = 1.5f;
        [Min(0f)] public float rollCooldown = 0.25f;
        [Tooltip("Fraction of max speed needed to roll.")]
        [Range(0f, 1f)] public float rollMinSpeedFraction = 0.6f;
        [Min(0f)] public float rollTurnRateDegrees = 120f;
        [Min(0f)] public float diveSpeed = 5.5f;
        [Min(0f)] public float diveHopVelocity = 3f;
        [Min(0f)] public float diveTurnRateDegrees = 90f;

        [Header("Body")]
        [Min(0.5f)] public float height = 1.1f;
        [Min(0.1f)] public float radius = 0.28f;
        [Tooltip("Ankle-level steps climbed automatically.")]
        [Min(0f)] public float stepOffset = 0.2f;
        [Range(10f, 70f)] public float slopeLimit = 45f;
        [Min(90f)] public float turnSpeedDegrees = 720f;

        [Header("Safety")]
        [Tooltip("Falling this far below the last safe anchor returns Pip to it.")]
        [Min(1f)] public float fallResetDepth = 5f;
        [Tooltip("Continuous falling longer than this returns Pip to the last safe anchor.")]
        [Min(0.2f)] public float maxFallTime = 1.4f;

        [Header("Camera")]
        public float cameraDistance = 3.8f;
        [Tooltip("Pivot height on Pip: roughly upper torso.")]
        public float pivotHeight = 0.95f;
        [Tooltip("Camera offset to Pip's right, so Pip sits left of centre and the path-door-window axis stays clear.")]
        public float shoulderSide = 0.35f;
        [Tooltip("Extra rise above the pivot so the camera sits slightly above shoulder line.")]
        public float shoulderRise = 0.35f;
        public float fieldOfView = 50f;
        public float touchDegreesPerPixel = 0.22f;
        public float mouseDegreesPerPixel = 0.12f;
        public float stickDegreesPerSecond = 160f;
        public bool invertY;

        public MovementTuningData ToData() => new MovementTuningData
        {
            MaxSpeed = maxSpeed,
            AccelerationTime = accelerationTime,
            DecelerationTime = decelerationTime,
            JumpApexHeight = jumpApexHeight,
            Gravity = gravity,
            CoyoteTime = coyoteTime,
            JumpBufferTime = jumpBufferTime,
            AirControl = airControl,
            MaxFallSpeed = maxFallSpeed,
            JumpHoldApexHeight = jumpHoldApexHeight,
            ChainJumpWindow = chainJumpWindow,
            ChainJumpMinSpeedFraction = chainJumpMinSpeedFraction,
            ChainJumpSecondHeightScale = chainJumpSecondHeightScale,
            ChainJumpThirdHeightScale = chainJumpThirdHeightScale,
            GlideFallSpeed = glideFallSpeed,
            GlideBrakeDeceleration = glideBrakeDeceleration,
            GlideAirControl = glideAirControl,
            RollDuration = rollDuration,
            RollSpeedBoost = rollSpeedBoost,
            RollCooldown = rollCooldown,
            RollMinSpeedFraction = rollMinSpeedFraction,
            RollTurnRateDegrees = rollTurnRateDegrees,
            DiveSpeed = diveSpeed,
            DiveHopVelocity = diveHopVelocity,
            DiveTurnRateDegrees = diveTurnRateDegrees,
        };
    }
}
