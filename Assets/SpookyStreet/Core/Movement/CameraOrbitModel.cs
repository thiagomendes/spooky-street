using System;

namespace SpookyStreet.Core.Movement
{
    /// <summary>Camera orbit tuning (spec §5): manual orbit wins, auto-recenter waits for stable movement.</summary>
    [Serializable]
    public sealed class CameraOrbitTuning
    {
        public float MinPitch = -25f;
        public float MaxPitch = 60f;
        public float DefaultPitch = 4f;

        /// <summary>Seconds without manual orbit before auto-recenter may start.</summary>
        public float RecenterDelay = 1.5f;

        /// <summary>Seconds of steady movement required before auto-recenter starts.</summary>
        public float StableMovementTime = 0.5f;

        /// <summary>Minimum horizontal speed (m/s) that counts as moving.</summary>
        public float MovingSpeed = 1f;

        /// <summary>Auto-recenter turn speed in degrees per second (the fastest it ever turns).</summary>
        public float RecenterSpeed = 90f;

        /// <summary>
        /// Auto-recenter turns at this many degrees per second for each degree Pip's heading is off behind the
        /// lens (beyond <see cref="RecenterDeadZone"/>), up to <see cref="RecenterSpeed"/>. A proportional, gentle
        /// follow: the old full-speed chase turned even a 1° thumb wobble into a turn (owner, 2026-11-28 zigzag).
        /// </summary>
        public float RecenterGain = 2.5f;

        /// <summary>Heading offsets up to this many degrees leave the camera still (thumb wobble never turns it).</summary>
        public float RecenterDeadZone = 6f;

        /// <summary>Seconds of smoothing on the heading the camera recenters toward (a step's sway is not followed).</summary>
        public float RecenterHeadingSmoothing = 0.4f;

        /// <summary>Speed of the explicit recenter command in degrees per second.</summary>
        public float ManualRecenterSpeed = 540f;
    }

    /// <summary>Engine-free yaw/pitch state for the third-person camera pivot.</summary>
    public sealed class CameraOrbitModel
    {
        private readonly CameraOrbitTuning _tuning;
        private float _timeSinceManual = float.PositiveInfinity;
        private float _timeMovingSteadily;
        private bool _recenterRequested;
        private float _smoothedHeading;
        private bool _hasSmoothedHeading;

        public CameraOrbitModel(CameraOrbitTuning tuning, float initialYaw)
        {
            _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
            Yaw = NormalizeAngle(initialYaw);
            Pitch = tuning.DefaultPitch;
        }

        public float Yaw { get; private set; }

        public float Pitch { get; private set; }

        /// <summary>True while auto-recenter is actively turning the camera this frame.</summary>
        public bool AutoRecentering { get; private set; }

        /// <summary>Applies manual orbit (degrees). Cancels any recenter immediately.</summary>
        public void ApplyManual(float deltaYaw, float deltaPitch)
        {
            if (Math.Abs(deltaYaw) < 1e-5f && Math.Abs(deltaPitch) < 1e-5f)
            {
                return;
            }

            Yaw = NormalizeAngle(Yaw + deltaYaw);
            Pitch = Clamp(Pitch + deltaPitch, _tuning.MinPitch, _tuning.MaxPitch);
            _timeSinceManual = 0f;
            _recenterRequested = false;
            AutoRecentering = false;
        }

        /// <summary>
        /// A cut (a threshold crossing): the orbit jumps straight behind <paramref name="yaw"/> at the default pitch,
        /// with no recenter left pending and no manual-orbit grace carried over.
        /// </summary>
        public void Snap(float yaw)
        {
            Yaw = NormalizeAngle(yaw);
            Pitch = _tuning.DefaultPitch;
            _recenterRequested = false;
            _timeSinceManual = float.PositiveInfinity;
            _timeMovingSteadily = 0f;
            _hasSmoothedHeading = false;
            AutoRecentering = false;
        }

        /// <summary>Explicit recenter (button). Turns behind the character at ManualRecenterSpeed.</summary>
        public void RequestRecenter() => _recenterRequested = true;

        /// <summary>Advances timers and auto-recenter toward the character's heading.</summary>
        public void Tick(float deltaTime, float horizontalSpeed, float headingYaw)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            _timeSinceManual += deltaTime;
            _timeMovingSteadily = horizontalSpeed >= _tuning.MovingSpeed ? _timeMovingSteadily + deltaTime : 0f;
            if (!_hasSmoothedHeading || _timeMovingSteadily <= 0f)
            {
                _smoothedHeading = NormalizeAngle(headingYaw);
                _hasSmoothedHeading = true;
            }
            else
            {
                float smoothing = _tuning.RecenterHeadingSmoothing;
                float follow = smoothing > 1e-4f ? 1f - (float)Math.Exp(-deltaTime / smoothing) : 1f;
                _smoothedHeading = NormalizeAngle(_smoothedHeading + DeltaAngle(_smoothedHeading, headingYaw) * follow);
            }

            AutoRecentering = false;
            if (_recenterRequested)
            {
                Yaw = MoveTowardsAngle(Yaw, headingYaw, _tuning.ManualRecenterSpeed * deltaTime);
                Pitch = MoveTowards(Pitch, _tuning.DefaultPitch, _tuning.ManualRecenterSpeed * deltaTime);
                if (Math.Abs(DeltaAngle(Yaw, headingYaw)) < 0.5f && Math.Abs(Pitch - _tuning.DefaultPitch) < 0.5f)
                {
                    _recenterRequested = false;
                }

                return;
            }

            if (_timeSinceManual >= _tuning.RecenterDelay && _timeMovingSteadily >= _tuning.StableMovementTime)
            {
                float gap = Math.Abs(DeltaAngle(Yaw, _smoothedHeading));
                float speed = Math.Min(_tuning.RecenterSpeed, _tuning.RecenterGain * (gap - _tuning.RecenterDeadZone));
                if (speed > 0f)
                {
                    Yaw = MoveTowardsAngle(Yaw, _smoothedHeading, speed * deltaTime);
                    AutoRecentering = true;
                }
            }
        }

        public static float NormalizeAngle(float degrees)
        {
            degrees %= 360f;
            if (degrees > 180f)
            {
                degrees -= 360f;
            }
            else if (degrees <= -180f)
            {
                degrees += 360f;
            }

            return degrees;
        }

        public static float DeltaAngle(float from, float to) => NormalizeAngle(to - from);

        public static float MoveTowardsAngle(float current, float target, float maxDelta)
        {
            float delta = DeltaAngle(current, target);
            if (Math.Abs(delta) <= maxDelta)
            {
                return NormalizeAngle(target);
            }

            return NormalizeAngle(current + Math.Sign(delta) * maxDelta);
        }

        private static float MoveTowards(float current, float target, float maxDelta)
        {
            if (Math.Abs(target - current) <= maxDelta)
            {
                return target;
            }

            return current + Math.Sign(target - current) * maxDelta;
        }

        private static float Clamp(float value, float min, float max) => value < min ? min : value > max ? max : value;
    }
}
