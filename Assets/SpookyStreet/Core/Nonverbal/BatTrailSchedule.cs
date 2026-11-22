using System;

namespace SpookyStreet.Core.Nonverbal
{
    /// <summary>One paper bat of the tier-2 trail at a moment.</summary>
    public struct BatState
    {
        /// <summary>Which wave (0 = the press, 1 = the 4 s repeat).</summary>
        public int Wave;

        /// <summary>Launch order inside its wave.</summary>
        public int Index;

        /// <summary>0..1 along the route from Pip to the goal (eased), held at 1 while it fades.</summary>
        public float RouteProgress;

        public float Alpha;
    }

    /// <summary>
    /// The tier-2 "where" hint (docs/nonverbal-ux.md §6, orchestrator decision D23.7): paper bats launch from Pip one
    /// at a time, fly the route and fade at the goal; the wave repeats every 4 s for about 8 s after a radio press,
    /// then the trail waits for the next press. Pure: sample with seconds since the press.
    /// </summary>
    public sealed class BatTrailSchedule
    {
        public const float DefaultRepeatSeconds = 4f;
        public const float DefaultActiveSeconds = 8f;
        public const int DefaultBatsPerWave = 9;
        public const float DefaultLaunchInterval = 0.22f;
        public const float DefaultFlightSeconds = 1.6f;
        public const float DefaultFadeSeconds = 0.4f;
        public const float DefaultFirstLaunch = 0.1f;

        /// <summary>Budget: billboard quads alive at once (§10).</summary>
        public const int MaxConcurrentBats = 12;

        private float _pressedAt = float.NaN;

        public BatTrailSchedule()
            : this(DefaultRepeatSeconds, DefaultActiveSeconds, DefaultBatsPerWave, DefaultLaunchInterval, DefaultFlightSeconds,
                DefaultFadeSeconds, DefaultFirstLaunch)
        {
        }

        public BatTrailSchedule(float repeatSeconds, float activeSeconds, int batsPerWave, float launchInterval,
            float flightSeconds, float fadeSeconds, float firstLaunch)
        {
            if (repeatSeconds <= 0f || activeSeconds <= 0f || batsPerWave < 1 || launchInterval <= 0f || flightSeconds <= 0f ||
                fadeSeconds < 0f || firstLaunch < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(repeatSeconds), "bat trail timings must be positive");
            }

            RepeatSeconds = repeatSeconds;
            ActiveSeconds = activeSeconds;
            BatsPerWave = batsPerWave;
            LaunchInterval = launchInterval;
            FlightSeconds = flightSeconds;
            FadeSeconds = fadeSeconds;
            FirstLaunch = firstLaunch;
            Waves = Math.Max(1, (int)Math.Ceiling(activeSeconds / repeatSeconds - 1e-4));
        }

        public float RepeatSeconds { get; }

        public float ActiveSeconds { get; }

        public int BatsPerWave { get; }

        public float LaunchInterval { get; }

        public float FlightSeconds { get; }

        public float FadeSeconds { get; }

        public float FirstLaunch { get; }

        /// <summary>Waves per press: those starting inside the active window (2 with the defaults: 0 s and 4 s).</summary>
        public int Waves { get; }

        /// <summary>One wave from its start until its last bat has faded.</summary>
        public float WaveSeconds => FirstLaunch + (BatsPerWave - 1) * LaunchInterval + FlightSeconds + FadeSeconds;

        /// <summary>From the press until the last bat of the last wave has faded (about 8 s).</summary>
        public float TotalSeconds => (Waves - 1) * RepeatSeconds + WaveSeconds;

        /// <summary>When the goal beacon appears: the first bat arrives.</summary>
        public float BeaconFrom => FirstLaunch + FlightSeconds;

        public bool HasPress => !float.IsNaN(_pressedAt);

        /// <summary>The radio was pressed at tier 2 (again): restart the trail from <paramref name="now"/>.</summary>
        public void Press(float now) => _pressedAt = now;

        /// <summary>Stop at once (tier advanced, objective reached, case changed).</summary>
        public void Stop() => _pressedAt = float.NaN;

        public bool IsActive(float now) => HasPress && now >= _pressedAt && now - _pressedAt < TotalSeconds;

        /// <summary>The floating goal sticker pulses from the first arrival until the trail ends.</summary>
        public bool BeaconVisible(float now) => IsActive(now) && now - _pressedAt >= BeaconFrom;

        /// <summary>Bats alive at <paramref name="now"/>; zero when idle (waiting for the next press).</summary>
        public int Sample(float now, BatState[] into) => HasPress ? SampleSincePress(now - _pressedAt, into) : 0;

        /// <summary>Pure core: bats alive <paramref name="sincePress"/> seconds after a press.</summary>
        public int SampleSincePress(float sincePress, BatState[] into)
        {
            if (into == null)
            {
                throw new ArgumentNullException(nameof(into));
            }

            int count = 0;
            if (sincePress < 0f || sincePress >= TotalSeconds)
            {
                return 0;
            }

            for (int wave = 0; wave < Waves; wave++)
            {
                float waveStart = wave * RepeatSeconds;
                for (int i = 0; i < BatsPerWave; i++)
                {
                    float age = sincePress - (waveStart + FirstLaunch + i * LaunchInterval);
                    if (age < 0f || age >= FlightSeconds + FadeSeconds)
                    {
                        continue;
                    }

                    if (count >= into.Length)
                    {
                        return count;
                    }

                    float u = Math.Min(1f, age / FlightSeconds);
                    into[count++] = new BatState
                    {
                        Wave = wave,
                        Index = i,
                        RouteProgress = VignetteSampler.Apply(Ease.InOut, u),
                        Alpha = age <= FlightSeconds || FadeSeconds <= 0f ? 1f : 1f - (age - FlightSeconds) / FadeSeconds,
                    };
                }
            }

            return count;
        }
    }
}
