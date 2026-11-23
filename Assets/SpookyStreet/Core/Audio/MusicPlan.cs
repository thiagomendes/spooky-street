using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.Audio
{
    /// <summary>Where Pip is, for the score.</summary>
    public enum MusicPlace
    {
        /// <summary>The title screen: the street theme, like the street.</summary>
        Title,
        Street,

        /// <summary>Inside the owl house: the sparse exploration loop only, a little lower.</summary>
        Indoors,
    }

    public enum MusicCommandKind
    {
        /// <summary>Start the event from its beginning, fading in over the seconds given (0: at once).</summary>
        Start,

        /// <summary>Stop the event, fading out over the seconds given.</summary>
        Stop,
    }

    /// <summary>What the plan asks the rig to do.</summary>
    public readonly struct MusicCommand
    {
        public MusicCommand(MusicCommandKind kind, string eventId, float fadeSeconds)
        {
            Kind = kind;
            EventId = eventId;
            FadeSeconds = fadeSeconds;
        }

        public MusicCommandKind Kind { get; }
        public string EventId { get; }
        public float FadeSeconds { get; }

        public override string ToString() => $"{Kind} {EventId} ({FadeSeconds:0.0} s)";
    }

    /// <summary>
    /// The score's rules (D20.2), engine-free and tested; <c>GameAudio</c> runs it on unscaled time (music carries on
    /// under the pause card) and applies its commands to the rig:
    /// <list type="bullet">
    /// <item><b>Street and title:</b> the street theme (seed 101, the storybook waltz) plays one pass, then the
    /// exploration loop (seed 111, its sparse twin) takes over for <see cref="ExplorationPassesPerTheme"/> passes, then
    /// the theme comes back, and so on. Each change is a <see cref="LoopCrossfadeSeconds"/> crossfade that starts that
    /// long before the pass ends, so the new loop starts from its beginning as the old one reaches its seam.</item>
    /// <item><b>Indoors:</b> the exploration loop only, for as long as Pip is inside; a theme still playing crosses into
    /// it over <see cref="PlaceCrossfadeSeconds"/> (under the page-turn). The bus is also lowered
    /// (<see cref="IndoorTrimDb"/>) so the floor hum and the mechanism's knocks, the clues heard indoors, stay clear.
    /// Back outside, the exploration pass that is playing finishes and the theme returns.</item>
    /// <item><b>Cues</b> (<see cref="PlayCue"/>): the case-closed sting and the reveal play on their own; the loop fades
    /// out under them (<see cref="CueInSeconds"/>) and comes back from its start after the cue
    /// (<see cref="ResumeFadeSeconds"/>). A loop id as a cue jumps straight to that loop (the opening restarts the
    /// theme). A newer cue replaces an older one.</item>
    /// <item><b>Silence</b> (<see cref="StopAll"/>): everything fades out and stays silent until a cue or
    /// <see cref="Resume"/>.</item>
    /// </list>
    /// </summary>
    public sealed class MusicPlan
    {
        public const float FirstFadeInSeconds = 1.5f;
        public const float LoopCrossfadeSeconds = 3f;
        public const float PlaceCrossfadeSeconds = 1.5f;
        public const float CueInSeconds = 0.4f;
        public const float CueOutSeconds = 0.5f;
        public const float ResumeFadeSeconds = 2f;
        public const int ExplorationPassesPerTheme = 2;

        /// <summary>The score indoors: lower, so the clues heard through the floor stay on top.</summary>
        public const float IndoorTrimDb = -5f;

        /// <summary>Under the pause card the music carries on (docs/S003-sfx-wiring.md), a little lower.</summary>
        public const float PausedTrimDb = -6f;

        /// <summary>A duck held by a cutscene (<see cref="AudioCues.DuckMusic"/>).</summary>
        public const float HeldDuckDb = -10f;

        private string _cue;
        private double _cueClock;
        private MusicPlace _place = MusicPlace.Street;
        private bool _started;

        /// <summary>The loop playing (null before the first update, during a cue and while silenced).</summary>
        public string Loop { get; private set; }

        /// <summary>Seconds into the loop's current pass (unscaled).</summary>
        public double LoopClock { get; private set; }

        /// <summary>Passes of <see cref="Loop"/> still to play before the next change, counting the current one.</summary>
        public int PassesLeft { get; private set; }

        /// <summary>The sting or reveal playing (null otherwise).</summary>
        public string Cue => _cue;

        /// <summary>Silenced by <see cref="StopAll"/>.</summary>
        public bool Silenced { get; private set; }

        /// <summary>The bus trim for a place, the pause and a held duck (dB, 0 or below).</summary>
        public static float TrimDb(MusicPlace place, bool paused, bool held) =>
            (place == MusicPlace.Indoors ? IndoorTrimDb : 0f) + (paused ? PausedTrimDb : 0f) + (held ? HeldDuckDb : 0f);

        /// <summary>Advances the plan by <paramref name="dt"/> seconds at <paramref name="place"/>.</summary>
        public void Update(double dt, MusicPlace place, List<MusicCommand> commands)
        {
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            bool wasIndoors = _place == MusicPlace.Indoors;
            _place = place;
            if (Silenced) return;

            if (_cue != null)
            {
                _cueClock += dt;
                if (_cueClock < VoiceMusicEvents.MusicSeconds(_cue)) return;
                commands.Add(new MusicCommand(MusicCommandKind.Stop, _cue, 0f)); // it has ended; release its source
                _cue = null;
                StartLoop(LoopFor(place), ResumeFadeSeconds, commands);
                return;
            }

            if (Loop == null)
            {
                StartLoop(LoopFor(place), _started ? ResumeFadeSeconds : FirstFadeInSeconds, commands);
                return;
            }

            bool indoors = place == MusicPlace.Indoors;
            if (indoors && Loop == VoiceMusicEvents.MusicStreetTheme)
            {
                CrossTo(VoiceMusicEvents.MusicExploration, PlaceCrossfadeSeconds, commands);
                return;
            }

            if (indoors) PassesLeft = int.MaxValue;
            else if (wasIndoors) PassesLeft = 1; // this exploration pass finishes, then the theme returns

            LoopClock += dt;
            double pass = VoiceMusicEvents.MusicSeconds(Loop);
            if (PassesLeft <= 1 && LoopClock >= pass - LoopCrossfadeSeconds)
            {
                CrossTo(Next(Loop), LoopCrossfadeSeconds, commands);
                return;
            }

            while (LoopClock >= pass)
            {
                LoopClock -= pass; // the clip loops by itself; one more pass of the same loop
                if (PassesLeft != int.MaxValue) PassesLeft--;
            }
        }

        /// <summary>
        /// Plays a cue now: the sting or the reveal over a faded loop, or a loop id (street theme, exploration) to jump
        /// to that loop. Unknown ids are ignored (false).
        /// </summary>
        public bool PlayCue(string id, List<MusicCommand> commands)
        {
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            if (!VoiceMusicEvents.IsMusic(id)) return false;
            Silenced = false;
            _started = true;
            if (_cue != null)
            {
                commands.Add(new MusicCommand(MusicCommandKind.Stop, _cue, CueOutSeconds));
                _cue = null;
            }

            if (VoiceMusicEvents.IsMusicLoop(id))
            {
                if (Loop == id)
                {
                    commands.Add(new MusicCommand(MusicCommandKind.Stop, Loop, CueInSeconds));
                    Loop = null;
                }

                if (Loop != null) CrossTo(id, PlaceCrossfadeSeconds, commands);
                else StartLoop(id, CueInSeconds, commands);
                return true;
            }

            if (Loop != null)
            {
                commands.Add(new MusicCommand(MusicCommandKind.Stop, Loop, CueInSeconds));
                Loop = null;
            }

            _cue = id;
            _cueClock = 0.0;
            commands.Add(new MusicCommand(MusicCommandKind.Start, id, 0f));
            return true;
        }

        /// <summary>Ends the sting or reveal early (the loop comes back); false when none plays.</summary>
        public bool StopCue(float fadeSeconds, List<MusicCommand> commands)
        {
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            if (_cue == null) return false;
            commands.Add(new MusicCommand(MusicCommandKind.Stop, _cue, fadeSeconds));
            _cue = null;
            if (!Silenced) StartLoop(LoopFor(_place), ResumeFadeSeconds, commands);
            return true;
        }

        /// <summary>Fades everything out and stays silent until <see cref="Resume"/> or a cue.</summary>
        public void StopAll(float fadeSeconds, List<MusicCommand> commands)
        {
            if (commands == null) throw new ArgumentNullException(nameof(commands));
            if (_cue != null) commands.Add(new MusicCommand(MusicCommandKind.Stop, _cue, fadeSeconds));
            if (Loop != null) commands.Add(new MusicCommand(MusicCommandKind.Stop, Loop, fadeSeconds));
            _cue = null;
            Loop = null;
            Silenced = true;
        }

        /// <summary>Ends a silence: the next update brings the loop for the place back in.</summary>
        public void Resume() => Silenced = false;

        private static string LoopFor(MusicPlace place) =>
            place == MusicPlace.Indoors ? VoiceMusicEvents.MusicExploration : VoiceMusicEvents.MusicStreetTheme;

        private static string Next(string loop) =>
            loop == VoiceMusicEvents.MusicStreetTheme ? VoiceMusicEvents.MusicExploration : VoiceMusicEvents.MusicStreetTheme;

        private static int PassesOf(string loop) =>
            loop == VoiceMusicEvents.MusicStreetTheme ? 1 : ExplorationPassesPerTheme;

        private void StartLoop(string loop, float fadeIn, List<MusicCommand> commands)
        {
            _started = true;
            Loop = loop;
            LoopClock = 0.0;
            PassesLeft = _place == MusicPlace.Indoors ? int.MaxValue : PassesOf(loop);
            commands.Add(new MusicCommand(MusicCommandKind.Start, loop, fadeIn));
        }

        private void CrossTo(string loop, float seconds, List<MusicCommand> commands)
        {
            if (Loop != null) commands.Add(new MusicCommand(MusicCommandKind.Stop, Loop, seconds));
            StartLoop(loop, seconds, commands);
        }
    }
}
