using System;

namespace SpookyStreet.Core.Animation
{
    /// <summary>
    /// A short squash-and-stretch kick (spec §5 "every move has its own squash-and-stretch"): <see cref="Kick"/>
    /// starts it at full amount and it eases out to zero over <see cref="Duration"/>. Positive values mean wide and
    /// flat (cape spread, roll tuck); the avatar maps the value to a volume-preserving scale.
    /// </summary>
    public sealed class SquashPulse
    {
        /// <summary>Seconds from the kick back to rest.</summary>
        public float Duration = 0.22f;

        private float _amount;
        private float _time = float.PositiveInfinity;

        public float Value { get; private set; }

        public void Kick(float amount)
        {
            _amount = amount;
            _time = 0f;
            Value = amount;
        }

        public float Step(float deltaTime)
        {
            if (deltaTime > 0f)
            {
                _time += deltaTime;
            }

            if (Duration <= 0f || _time >= Duration)
            {
                Value = 0f;
                return Value;
            }

            float remaining = 1f - _time / Duration;
            Value = _amount * remaining * remaining;
            return Value;
        }

        /// <summary>Horizontal and vertical scale for a squash value, keeping the volume roughly constant.</summary>
        public static void ToScale(float value, out float horizontal, out float vertical)
        {
            horizontal = 1f + value;
            vertical = 1f / Math.Max(0.5f, (1f + value) * (1f + value));
        }
    }
}
