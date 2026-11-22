using System;

namespace SpookyStreet.Core.Characters
{
    public enum WitnessState
    {
        /// <summary>Pip is away: the rest loop (Idle / SitIdle).</summary>
        Idle,

        /// <summary>Pip just came near: the greeting one-shot (Wave) over the rest loop.</summary>
        Waving,

        /// <summary>Pip is near: the engaged loop (Talk / SitTalk).</summary>
        Talking,

        /// <summary>Pip has left; the engaged loop runs a little longer before the witness settles back to rest.</summary>
        Lingering,
    }

    /// <summary>
    /// Ambient reaction of a witness to Pip's distance (S000.5: presence only; conversations are S002). Pip coming
    /// within <see cref="NearRadius"/> starts a wave (once per approach), then the talk loop while Pip stays. Pip
    /// must go beyond <see cref="FarRadius"/> (hysteresis, so standing on the edge does not flicker) and stay away
    /// for <see cref="ReturnDelay"/> before the witness returns to rest; only then is the next approach greeted
    /// again. A zero or negative step (pause) changes nothing.
    /// </summary>
    public sealed class WitnessReaction
    {
        private float _timer;

        public float NearRadius { get; set; } = 4f;
        public float FarRadius { get; set; } = 4.75f;
        public float ReturnDelay { get; set; } = 1.5f;

        /// <summary>Length of the greeting clip in seconds; 0 skips the wave (no clip).</summary>
        public float WaveDuration { get; set; } = 1.5f;

        public WitnessState State { get; private set; } = WitnessState.Idle;

        /// <summary>True only for the step on which a wave began (the caller starts the one-shot then).</summary>
        public bool WaveStarted { get; private set; }

        /// <summary>True while the engaged loop (Talk / SitTalk) should play under any one-shot.</summary>
        public bool WantsEngagedLoop => State == WitnessState.Talking || State == WitnessState.Lingering;

        /// <summary>True while the witness pays attention to Pip (turns their head toward Pip).</summary>
        public bool Attending => State != WitnessState.Idle;

        /// <summary>Number of waves started since construction or <see cref="Reset"/>.</summary>
        public int WaveCount { get; private set; }

        public void Reset()
        {
            State = WitnessState.Idle;
            WaveStarted = false;
            WaveCount = 0;
            _timer = 0f;
        }

        /// <summary>Advances by <paramref name="dt"/> seconds with Pip <paramref name="distance"/> metres away.</summary>
        public WitnessState Step(float distance, float dt)
        {
            WaveStarted = false;
            if (!(dt > 0f))
            {
                return State;
            }

            bool near = distance <= NearRadius;
            bool away = distance > Math.Max(FarRadius, NearRadius);
            switch (State)
            {
                case WitnessState.Idle:
                    if (near)
                    {
                        if (WaveDuration > 0f)
                        {
                            State = WitnessState.Waving;
                            _timer = WaveDuration;
                            WaveStarted = true;
                            WaveCount++;
                        }
                        else
                        {
                            State = WitnessState.Talking;
                        }
                    }

                    break;

                case WitnessState.Waving:
                    // The wave always finishes; then talk if Pip is still around, otherwise linger.
                    _timer -= dt;
                    if (_timer <= 0f)
                    {
                        if (away)
                        {
                            State = WitnessState.Lingering;
                            _timer = ReturnDelay;
                        }
                        else
                        {
                            State = WitnessState.Talking;
                        }
                    }

                    break;

                case WitnessState.Talking:
                    if (away)
                    {
                        State = WitnessState.Lingering;
                        _timer = ReturnDelay;
                    }

                    break;

                case WitnessState.Lingering:
                    if (!away)
                    {
                        State = WitnessState.Talking; // came back before settling: no second wave
                        break;
                    }

                    _timer -= dt;
                    if (_timer <= 0f)
                    {
                        State = WitnessState.Idle;
                    }

                    break;
            }

            return State;
        }

        /// <summary>
        /// Signed yaw (degrees, positive = clockwise from above, as Unity's yaw) from <paramref name="facingYaw"/>
        /// toward <paramref name="targetYaw"/>, wrapped to [-180, 180] and clamped to ±<paramref name="maxTurn"/>.
        /// </summary>
        public static float LookYaw(float facingYaw, float targetYaw, float maxTurn)
        {
            float delta = DeltaAngle(facingYaw, targetYaw);
            float limit = Math.Abs(maxTurn);
            return Math.Max(-limit, Math.Min(limit, delta));
        }

        /// <summary>Shortest signed difference from <paramref name="from"/> to <paramref name="to"/> in degrees.</summary>
        public static float DeltaAngle(float from, float to)
        {
            double d = (to - from) % 360.0;
            if (d > 180.0)
            {
                d -= 360.0;
            }
            else if (d < -180.0)
            {
                d += 360.0;
            }

            return (float)d;
        }

        /// <summary>Unity yaw (degrees) of the horizontal direction (<paramref name="dx"/>, <paramref name="dz"/>).</summary>
        public static float YawOf(float dx, float dz) => (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI);
    }
}
