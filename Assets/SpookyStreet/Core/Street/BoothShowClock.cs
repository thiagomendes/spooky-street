namespace SpookyStreet.Core.Street
{
    /// <summary>What the show clock asks the street for on a step.</summary>
    public enum BoothShow
    {
        None,

        /// <summary>"On the hour": the booth bell dings, the owl bows three times, Mr. Vale checks his watch if in view.</summary>
        OnTheHour,

        /// <summary>The child knocked three times on the booth's shutter: the owl bows (a touch reward, engagement §1.9).</summary>
        Knocked,
    }

    /// <summary>
    /// The owl booth's show after case 01 (S004 plan §2.3): once the street remembers the case, the rooftop owl's wind
    /// cycle becomes a show "on the hour", every <see cref="IntervalSeconds"/> of play on the street (paused, indoors, in
    /// a cutscene or a conversation, the clock stands still), and knocking <see cref="KnocksForBow"/> times on the
    /// booth's shutter makes the owl bow at once. Before the stamp, or while a replayed case rests its memories, the
    /// clock is off and knocks do nothing, so no clue of the case changes. Pure and frame-rate independent.
    /// </summary>
    public sealed class BoothShowClock
    {
        /// <summary>Seconds of play on the street between two shows.</summary>
        public const float IntervalSeconds = 90f;

        /// <summary>Knocks in a row that make the owl bow.</summary>
        public const int KnocksForBow = 3;

        /// <summary>A knock more than this long after the last one starts a new row.</summary>
        public const float KnockGapSeconds = 2.5f;

        private float _sinceKnock;

        /// <summary>True once the street remembers case 01 and its memories are not resting.</summary>
        public bool Running { get; private set; }

        /// <summary>Seconds of street play since the last show.</summary>
        public float Clock => (float)_clock;

        // Counted in double: float sums of 1/60 s drift by a frame over a few minutes.
        private double _clock;

        /// <summary>Knocks in the current row.</summary>
        public int Knocks { get; private set; }

        /// <summary>Shows so far (both kinds), for tests and the log.</summary>
        public int Shows { get; private set; }

        /// <summary>Turns the clock on or off (the street's memories). Turning it either way starts the hour over.</summary>
        public void SetRunning(bool running)
        {
            if (running == Running)
            {
                return;
            }

            Running = running;
            _clock = 0.0;
            Knocks = 0;
        }

        /// <summary>
        /// Advances by <paramref name="dt"/> seconds of game time. <paramref name="onStreet"/> is false while Pip is
        /// indoors or anything holds the screen (the clock stands still then). At most one show per step.
        /// </summary>
        public BoothShow Advance(float dt, bool onStreet)
        {
            if (!(dt > 0f))
            {
                return BoothShow.None;
            }

            _sinceKnock += dt;
            if (Knocks > 0 && _sinceKnock > KnockGapSeconds)
            {
                Knocks = 0;
            }

            if (!Running || !onStreet)
            {
                return BoothShow.None;
            }

            _clock += dt;
            if (_clock < IntervalSeconds - 1e-4)
            {
                return BoothShow.None;
            }

            // Whatever the frame rate, the next hour counts from this one's mark (a long frame never stacks shows).
            _clock -= IntervalSeconds;
            if (_clock > IntervalSeconds)
            {
                _clock = 0.0;
            }

            Shows++;
            return BoothShow.OnTheHour;
        }

        /// <summary>
        /// One knock on the booth's shutter. Returns <see cref="BoothShow.Knocked"/> on the
        /// <see cref="KnocksForBow"/>th knock in a row; the hour then starts over, so two shows never crowd each other.
        /// Inert (always <see cref="BoothShow.None"/>) while the clock is off.
        /// </summary>
        public BoothShow Knock()
        {
            if (!Running)
            {
                return BoothShow.None;
            }

            if (Knocks > 0 && _sinceKnock > KnockGapSeconds)
            {
                Knocks = 0;
            }

            Knocks++;
            _sinceKnock = 0f;
            if (Knocks < KnocksForBow)
            {
                return BoothShow.None;
            }

            Knocks = 0;
            _clock = 0.0;
            Shows++;
            return BoothShow.Knocked;
        }
    }
}
