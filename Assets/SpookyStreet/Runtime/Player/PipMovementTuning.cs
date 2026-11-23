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
        [Tooltip("Apex of a tapped jump (Jump released before the boost delay). Routes rely on it.")]
        [Min(0.1f)] public float jumpApexHeight = 1f;
        [Tooltip("Apex while Jump is held through the rise; releasing during the boost lands in between.")]
        [Min(0.1f)] public float jumpHoldApexHeight = 2.1f;
        [Tooltip("Seconds after take-off with Jump still held before the hold boost engages.")]
        [Min(0f)] public float jumpBoostDelay = 0.1f;
        [Tooltip("Upward speed the hold boost drives to and holds (m/s): the high jump visibly shoots up.")]
        [Min(0.5f)] public float jumpBoostSpeed = 6f;
        [Tooltip("Upward acceleration that ramps the rise to the boost speed (m/s²).")]
        [Min(1f)] public float jumpBoostAcceleration = 50f;
        [Tooltip("Apex float of a held jump before the cape may open (seconds).")]
        [Min(0f)] public float apexHangTime = 0.12f;
        [Range(0f, 1f)] public float apexHangGravityScale = 0.25f;
        [Min(1f)] public float gravity = 25f;
        [Min(0f)] public float coyoteTime = 0.10f;
        [Tooltip("A Jump press this long before landing still jumps (and chains).")]
        [Min(0f)] public float jumpBufferTime = 0.15f;
        [Range(0f, 1f)] public float airControl = 0.6f;
        [Min(1f)] public float maxFallSpeed = 20f;

        [Header("Chain jump (jump again right after landing while running)")]
        [Tooltip("Seconds after landing in which Jump continues the chain.")]
        [Min(0f)] public float chainJumpWindow = 0.35f;
        [Tooltip("Fraction of max speed needed to keep the chain going.")]
        [Range(0f, 1f)] public float chainJumpMinSpeedFraction = 0.25f;
        [Tooltip("Height scale of the 2nd chained jump (held 2.1 m to 2.4 m).")]
        [Min(1f)] public float chainJumpSecondHeightScale = 8f / 7f;
        [Tooltip("Height scale of the 3rd chained jump (held 2.1 m to 2.8 m).")]
        [Min(1f)] public float chainJumpThirdHeightScale = 4f / 3f;
        [Tooltip("Height scale of the 2nd chained jump when tapped (1.0 m to 1.25 m).")]
        [Min(1f)] public float chainJumpSecondTapScale = 1.25f;
        [Tooltip("Height scale of the 3rd chained jump when tapped (1.0 m to 1.5 m).")]
        [Min(1f)] public float chainJumpThirdTapScale = 1.5f;

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

        [Header("Wall slide and wall jump (marked walls only, S003)")]
        [Tooltip("A marked wall counts as touched within Pip's radius plus this (m).")]
        [Min(0f)] public float wallContactSlack = 0.12f;
        [Tooltip("Air needed under the feet before a slide can start (m).")]
        [Min(0f)] public float wallSlideMinFeetHeight = 0.35f;
        [Tooltip("How much the stick or the motion must point into the wall to start a slide.")]
        [Range(0f, 1f)] public float wallSlideIntoMin = 0.3f;
        [Tooltip("First seconds of a slide: Pip grips and slows right down.")]
        [Min(0f)] public float wallSlideGripTime = 0.2f;
        [Min(0.1f)] public float wallSlideGripSpeed = 0.6f;
        [Tooltip("Fall speed cap of the slide (m/s); the WallSlide clip is authored at about 1.5 m/s.")]
        [Min(0.1f)] public float wallSlideSpeed = 1.5f;
        [Min(1f)] public float wallSlideBrakeDeceleration = 30f;
        [Tooltip("Seconds the stick must point away before Pip lets go of the wall.")]
        [Min(0f)] public float wallSlideReleaseTime = 0.25f;
        [Tooltip("Height of a wall jump above the kick (m).")]
        [Min(0.1f)] public float wallJumpHeight = 1.2f;
        [Tooltip("Speed away from the wall at the kick (m/s).")]
        [Min(0f)] public float wallJumpAwaySpeed = 4.5f;
        [Tooltip("Seconds after leaving a wall in which Jump still kicks off it.")]
        [Min(0f)] public float wallJumpCoyoteTime = 0.2f;
        [Tooltip("Seconds of reduced air control after a kick.")]
        [Min(0f)] public float wallJumpControlLockTime = 0.25f;
        [Range(0f, 1f)] public float wallJumpLockedAirControl = 0.15f;

        [Header("Ledge grab (marked ledges only, S003)")]
        [Tooltip("Hands above the feet while hanging (the LedgeHang clip grips at 1.27 m).")]
        [Min(0.5f)] public float ledgeHangHandHeight = 1.27f;
        [Tooltip("Pip's centre hangs this far out from the face (m).")]
        [Min(0.1f)] public float ledgeHangWallOffset = 0.34f;
        [Tooltip("The catch window below and above the hanging hand height (m).")]
        [Min(0f)] public float ledgeGrabReachBelow = 0.35f;
        [Min(0f)] public float ledgeGrabReachAbove = 0.3f;
        [Tooltip("The edge may be this far beyond Pip's surface (m).")]
        [Min(0f)] public float ledgeGrabReachForward = 0.35f;
        [Tooltip("No catch while rising faster than this (m/s).")]
        [Min(0f)] public float ledgeGrabMaxRiseSpeed = 1.5f;
        [Min(0f)] public float ledgeMinHangClearance = 0.15f;
        [Min(0.01f)] public float ledgeSnapTime = 0.1f;
        [Min(0.05f)] public float ledgeClimbTime = 0.4f;
        [Range(0.05f, 0.95f)] public float ledgeClimbRiseFraction = 0.6f;
        [Min(0.1f)] public float ledgeClimbInset = 0.4f;
        [Min(0f)] public float ledgeClimbLift = 0.06f;
        [Tooltip("Seconds the stick must be pulled down or away to drop.")]
        [Min(0f)] public float ledgeDropHoldTime = 0.1f;
        [Min(0f)] public float ledgeDropPushSpeed = 1f;
        [Min(0f)] public float ledgeRegrabDelay = 0.4f;

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
            JumpBoostDelay = jumpBoostDelay,
            JumpBoostSpeed = jumpBoostSpeed,
            JumpBoostAcceleration = jumpBoostAcceleration,
            ApexHangTime = apexHangTime,
            ApexHangGravityScale = apexHangGravityScale,
            ChainJumpWindow = chainJumpWindow,
            ChainJumpMinSpeedFraction = chainJumpMinSpeedFraction,
            ChainJumpSecondHeightScale = chainJumpSecondHeightScale,
            ChainJumpThirdHeightScale = chainJumpThirdHeightScale,
            ChainJumpSecondTapScale = chainJumpSecondTapScale,
            ChainJumpThirdTapScale = chainJumpThirdTapScale,
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
            BodyRadius = radius,
            WallContactSlack = wallContactSlack,
            WallSlideMinFeetHeight = wallSlideMinFeetHeight,
            WallSlideIntoMin = wallSlideIntoMin,
            WallSlideGripTime = wallSlideGripTime,
            WallSlideGripSpeed = wallSlideGripSpeed,
            WallSlideSpeed = wallSlideSpeed,
            WallSlideBrakeDeceleration = wallSlideBrakeDeceleration,
            WallSlideReleaseTime = wallSlideReleaseTime,
            WallJumpHeight = wallJumpHeight,
            WallJumpAwaySpeed = wallJumpAwaySpeed,
            WallJumpCoyoteTime = wallJumpCoyoteTime,
            WallJumpControlLockTime = wallJumpControlLockTime,
            WallJumpLockedAirControl = wallJumpLockedAirControl,
            LedgeHangHandHeight = ledgeHangHandHeight,
            LedgeHangWallOffset = ledgeHangWallOffset,
            LedgeGrabReachBelow = ledgeGrabReachBelow,
            LedgeGrabReachAbove = ledgeGrabReachAbove,
            LedgeGrabReachForward = ledgeGrabReachForward,
            LedgeGrabMaxRiseSpeed = ledgeGrabMaxRiseSpeed,
            LedgeMinHangClearance = ledgeMinHangClearance,
            LedgeSnapTime = ledgeSnapTime,
            LedgeClimbTime = ledgeClimbTime,
            LedgeClimbRiseFraction = ledgeClimbRiseFraction,
            LedgeClimbInset = ledgeClimbInset,
            LedgeClimbLift = ledgeClimbLift,
            LedgeDropHoldTime = ledgeDropHoldTime,
            LedgeDropPushSpeed = ledgeDropPushSpeed,
            LedgeRegrabDelay = ledgeRegrabDelay,
        };
    }
}
