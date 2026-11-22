using System;

namespace SpookyStreet.Core.Animation
{
    /// <summary>The body state Pip's animation shows; the animator maps each to clips.</summary>
    public enum PipPose
    {
        /// <summary>On the ground: idle/walk/run blended by speed.</summary>
        Locomotion,
        JumpStart,
        JumpAir,
        Fall,
        Land,
        Glide,
        WallSlide,
        LedgeHang,
    }

    /// <summary>What the motor (and later the moveset) reports each frame.</summary>
    public struct PipPoseInput
    {
        public bool Grounded;

        /// <summary>m/s, positive while rising.</summary>
        public float VerticalSpeed;

        /// <summary>m/s on the ground plane.</summary>
        public float HorizontalSpeed;

        public bool Gliding;
        public bool WallSliding;
        public bool LedgeHanging;
    }

    /// <summary>Idle/walk/run weights; they always sum to 1.</summary>
    public struct LocomotionWeights
    {
        public float Idle;
        public float Walk;
        public float Run;
    }

    /// <summary>
    /// Engine-free animation state for Pip: picks the pose from motor state with small hysteresis, so a one-frame
    /// loss of ground on a step does not flash the air pose or play a landing, and blends idle/walk/run by speed.
    /// All durations are seconds and all speeds m/s.
    /// </summary>
    public sealed class PipPoseTracker
    {
        /// <summary>Upward speed at take-off that counts as a jump (walking off an edge never rises).</summary>
        public float TakeoffSpeed = 0.5f;

        /// <summary>How long JumpStart shows before the air loop; the animator sets it from the clip length.</summary>
        public float JumpStartDuration = 0.2f;

        /// <summary>Unjumped air time before the air pose shows (hides step-down flicker).</summary>
        public float MinAirTimeForAirPose = 0.12f;

        /// <summary>Air time needed before touchdown plays Land.</summary>
        public float MinAirTimeForLand = 0.25f;

        public float LandDuration = 0.25f;

        /// <summary>Above this ground speed the landing gives way to the run straight away.</summary>
        public float LandCancelSpeed = 3f;

        /// <summary>Vertical speed below which the air pose switches from JumpAir to Fall.</summary>
        public float FallSpeed = -1.5f;

        private bool _wasGrounded = true;
        private bool _jumped;
        private float _airTime;
        private float _startTimer;
        private float _landTimer;

        public PipPose Pose { get; private set; } = PipPose.Locomotion;

        /// <summary>Seconds since Pip left the ground (0 while grounded).</summary>
        public float AirTime => _airTime;

        /// <summary>True for the step in which a jump take-off was detected (restart JumpStart).</summary>
        public bool TookOff { get; private set; }

        /// <summary>True for the step in which a landing worth showing began (restart Land).</summary>
        public bool Landed { get; private set; }

        public void Reset()
        {
            _wasGrounded = true;
            _jumped = false;
            _airTime = 0f;
            _startTimer = 0f;
            _landTimer = 0f;
            TookOff = false;
            Landed = false;
            Pose = PipPose.Locomotion;
        }

        public PipPose Step(PipPoseInput input, float deltaTime)
        {
            TookOff = false;
            Landed = false;
            if (deltaTime <= 0f)
            {
                return Pose;
            }

            if (input.Grounded)
            {
                if (!_wasGrounded && _airTime >= MinAirTimeForLand)
                {
                    _landTimer = LandDuration;
                    Landed = true;
                }

                _airTime = 0f;
                _startTimer = 0f;
                _jumped = false;
            }
            else
            {
                if (_wasGrounded && input.VerticalSpeed > TakeoffSpeed)
                {
                    _startTimer = JumpStartDuration;
                    _landTimer = 0f;
                    _jumped = true;
                    TookOff = true;
                }

                _airTime += deltaTime;
            }

            _wasGrounded = input.Grounded;
            Pose = Choose(input);
            _startTimer = Math.Max(0f, _startTimer - deltaTime);
            _landTimer = Math.Max(0f, _landTimer - deltaTime);
            return Pose;
        }

        private PipPose Choose(PipPoseInput input)
        {
            if (input.LedgeHanging)
            {
                return PipPose.LedgeHang;
            }

            if (!input.Grounded)
            {
                if (input.WallSliding)
                {
                    return PipPose.WallSlide;
                }

                if (input.Gliding)
                {
                    return PipPose.Glide;
                }

                if (_startTimer > 0f)
                {
                    return PipPose.JumpStart;
                }

                if (_jumped || _airTime >= MinAirTimeForAirPose)
                {
                    return input.VerticalSpeed > FallSpeed ? PipPose.JumpAir : PipPose.Fall;
                }

                return PipPose.Locomotion;
            }

            return _landTimer > 0f && input.HorizontalSpeed < LandCancelSpeed ? PipPose.Land : PipPose.Locomotion;
        }

        /// <summary>
        /// Idle → walk as speed rises to 60% of the walk clip's speed, then walk → run between the walk clip's
        /// speed and <paramref name="fullRunSpeed"/> (usually Pip's top speed, which is below the run clip's).
        /// </summary>
        public static LocomotionWeights Locomotion(float speed, float walkSpeed, float fullRunSpeed)
        {
            walkSpeed = Math.Max(0.1f, walkSpeed);
            fullRunSpeed = Math.Max(walkSpeed + 0.1f, fullRunSpeed);
            float moving = InverseLerp(0.05f, walkSpeed * 0.6f, speed);
            float run = InverseLerp(walkSpeed, fullRunSpeed, speed);
            return new LocomotionWeights
            {
                Idle = 1f - moving,
                Walk = moving * (1f - run),
                Run = moving * run,
            };
        }

        /// <summary>
        /// Playback rate for a locomotion clip authored at <paramref name="clipSpeed"/> so the feet match the
        /// ground, clamped so a slow shuffle still steps and a sprint never blurs.
        /// </summary>
        public static float PlaybackRate(float speed, float clipSpeed, float min = 0.5f, float max = 1.6f)
        {
            float rate = clipSpeed > 0f ? speed / clipSpeed : 1f;
            return Math.Min(max, Math.Max(min, rate));
        }

        private static float InverseLerp(float a, float b, float value)
        {
            if (b <= a)
            {
                return value >= b ? 1f : 0f;
            }

            float t = (value - a) / (b - a);
            return t < 0f ? 0f : t > 1f ? 1f : t;
        }
    }
}
