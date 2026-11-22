using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.Nonverbal
{
    /// <summary>How a key is reached from the previous key (docs/nonverbal-ux.md §10).</summary>
    public enum Ease
    {
        Linear,

        /// <summary>Smoothstep: slow out, slow in (travel).</summary>
        InOut,

        /// <summary>Fast start, soft stop (arrivals, fades).</summary>
        Out,

        /// <summary>Overshoot then settle (pops, slides that "land").</summary>
        Back,

        /// <summary>Holds the previous value and snaps on the key (clock ticks).</summary>
        Step,
    }

    /// <summary>Transform of one puppet part in stage space.</summary>
    public readonly struct PuppetPose
    {
        public PuppetPose(float x, float y, float scale = 1f, float rotation = 0f, float alpha = 1f)
        {
            X = x;
            Y = y;
            Scale = scale;
            Rotation = rotation;
            Alpha = alpha;
        }

        /// <summary>0 = stage left edge, 1 = right edge. Slightly outside is allowed for entrances.</summary>
        public float X { get; }

        /// <summary>0 = stage bottom, 1 = top (Unity UI convention).</summary>
        public float Y { get; }

        /// <summary>1 = the part's nominal size on the stage.</summary>
        public float Scale { get; }

        /// <summary>Degrees, counter-clockwise (Unity UI z rotation).</summary>
        public float Rotation { get; }

        public float Alpha { get; }

        public PuppetPose With(float? x = null, float? y = null, float? scale = null, float? rotation = null, float? alpha = null) =>
            new PuppetPose(x ?? X, y ?? Y, scale ?? Scale, rotation ?? Rotation, alpha ?? Alpha);

        internal bool SameAs(PuppetPose o) =>
            Near(X, o.X) && Near(Y, o.Y) && Near(Scale, o.Scale) && Near(Rotation, o.Rotation) && Near(Alpha, o.Alpha);

        private static bool Near(float a, float b) => Math.Abs(a - b) < 1e-4f;
    }

    /// <summary>One keyframe. <see cref="Part"/> switches the sprite from this key on (null keeps the track's).</summary>
    public sealed class VignetteKey
    {
        public VignetteKey(float t, PuppetPose pose, Ease ease = Ease.InOut, string part = null)
        {
            T = t;
            Pose = pose;
            Ease = ease;
            Part = part;
        }

        public float T { get; }

        public PuppetPose Pose { get; }

        /// <summary>The curve used to arrive at this key from the previous one.</summary>
        public Ease Ease { get; }

        public string Part { get; }
    }

    /// <summary>One puppet part on the stage. Tracks draw in array order (first = back).</summary>
    public sealed class VignetteTrack
    {
        public VignetteTrack(string id, string part, params VignetteKey[] keys)
        {
            Id = id;
            Part = part;
            Keys = keys ?? Array.Empty<VignetteKey>();
        }

        /// <summary>Unique within its vignette; cues target tracks by this ID.</summary>
        public string Id { get; }

        /// <summary>Default sprite, a <see cref="PuppetParts"/> ID.</summary>
        public string Part { get; }

        public IReadOnlyList<VignetteKey> Keys { get; }

        /// <summary>True while the pose (or sprite) changes between key <paramref name="i"/> and the next.</summary>
        internal bool SegmentMoves(int i)
        {
            var a = Keys[i];
            var b = Keys[i + 1];
            return !a.Pose.SameAs(b.Pose) || !string.Equals(PartAt(i), PartAt(i + 1), StringComparison.Ordinal);
        }

        internal string PartAt(int keyIndex)
        {
            for (int i = keyIndex; i >= 0; i--)
            {
                if (Keys[i].Part != null)
                {
                    return Keys[i].Part;
                }
            }

            return Part;
        }
    }

    public enum CueKind
    {
        /// <summary>Overlay: the orange stitched key ring pulses on the target ("this matters").</summary>
        Ring,

        /// <summary>Modifier: the target thumps and wiggles to a stop (the ⊘ "never", the green check).</summary>
        Stamp,

        /// <summary>Overlay: a paper puff at the target ("goes in").</summary>
        Puff,

        /// <summary>Overlay: knock rings travel out of the target.</summary>
        Knock,

        /// <summary>World only: the speaker plays a gesture clip (<see cref="VignetteCue.Arg"/>, e.g. "Point").</summary>
        Gesture,

        /// <summary>World only: a babble syllable lands here (voice and picture feel like one sentence).</summary>
        BabbleSync,
    }

    /// <summary>A timed event. Overlays and modifiers count as moving parts while they run.</summary>
    public sealed class VignetteCue
    {
        public VignetteCue(float t, CueKind kind, string target, float duration, string arg = null)
        {
            T = t;
            Kind = kind;
            Target = target;
            Duration = duration;
            Arg = arg;
        }

        public float T { get; }

        public CueKind Kind { get; }

        /// <summary>The track it acts on or sits over; null for world-only cues.</summary>
        public string Target { get; }

        public float Duration { get; }

        public string Arg { get; }

        public float End => T + Duration;

        /// <summary>Drawn on the stage (not just a world event).</summary>
        public bool IsVisual => Kind == CueKind.Ring || Kind == CueKind.Stamp || Kind == CueKind.Puff || Kind == CueKind.Knock;

        /// <summary>Animates its target track rather than drawing its own sprite.</summary>
        public bool IsModifier => Kind == CueKind.Stamp;
    }

    /// <summary>One readable idea: a vignette has 1–3 beats (reduced motion holds each beat's end pose).</summary>
    public readonly struct VignetteBeat
    {
        public VignetteBeat(float start, float end)
        {
            Start = start;
            End = end;
        }

        public float Start { get; }

        public float End { get; }
    }

    /// <summary>
    /// A short paper-puppet animation (docs/nonverbal-ux.md §3, §5): parts from <see cref="PuppetParts"/> moved by
    /// keyframes, plus cues. Pure data; <see cref="VignetteSampler"/> evaluates it.
    /// </summary>
    public sealed class Vignette
    {
        public const float MinLength = 2f;
        public const float MaxLength = 4f;
        public const int MaxBeats = 3;
        public const int MaxMovingParts = 2;

        /// <summary>Budget: puppet images alive at once (§10).</summary>
        public const int MaxTracks = 12;

        public Vignette(string id, float length, int loopCount, VignetteBeat[] beats, VignetteTrack[] tracks, VignetteCue[] cues)
        {
            Id = id;
            Length = length;
            LoopCount = loopCount;
            Beats = beats ?? Array.Empty<VignetteBeat>();
            Tracks = tracks ?? Array.Empty<VignetteTrack>();
            Cues = cues ?? Array.Empty<VignetteCue>();
        }

        public string Id { get; }

        /// <summary>Seconds per loop.</summary>
        public float Length { get; }

        /// <summary>0 = loop until the player taps next (speech); n = play n times then hold the end (notebook).</summary>
        public int LoopCount { get; }

        public IReadOnlyList<VignetteBeat> Beats { get; }

        public IReadOnlyList<VignetteTrack> Tracks { get; }

        public IReadOnlyList<VignetteCue> Cues { get; }

        /// <summary>Time inside the loop for <paramref name="elapsed"/> seconds since Play.</summary>
        public float LocalTime(float elapsed)
        {
            if (elapsed <= 0f || Length <= 0f)
            {
                return 0f;
            }

            if (LoopCount > 0 && elapsed >= Length * LoopCount)
            {
                return Length;
            }

            return elapsed % Length;
        }

        /// <summary>A finite vignette has played all its loops (the view can stop sampling).</summary>
        public bool IsFinished(float elapsed) => LoopCount > 0 && elapsed >= Length * LoopCount;

        /// <summary>
        /// Reduced motion: the time to sample so each beat shows as one still pose (cross-fade between them in the
        /// view). Before the first beat ends this is the first beat's end; between beats it holds the last one.
        /// </summary>
        public float ReducedMotionTime(float localTime)
        {
            if (Beats.Count == 0)
            {
                return Length;
            }

            float hold = Beats[0].End;
            foreach (var beat in Beats)
            {
                if (localTime >= beat.Start)
                {
                    hold = beat.End;
                }
            }

            return hold;
        }

        public VignetteTrack FindTrack(string id)
        {
            foreach (var track in Tracks)
            {
                if (string.Equals(track.Id, id, StringComparison.Ordinal))
                {
                    return track;
                }
            }

            return null;
        }

        /// <summary>The most moving parts at any instant (tracks in motion, overlays, stamped tracks).</summary>
        public int MaxMovingAtOnce()
        {
            // Intervals [start, end) of motion, keyed by the part that moves (a stamp on a moving track is one part).
            var intervals = new List<(float Start, float End, string Part)>();
            foreach (var track in Tracks)
            {
                for (int i = 0; i + 1 < track.Keys.Count; i++)
                {
                    if (track.SegmentMoves(i))
                    {
                        intervals.Add((track.Keys[i].T, track.Keys[i + 1].T, "track:" + track.Id));
                    }
                }
            }

            for (int c = 0; c < Cues.Count; c++)
            {
                var cue = Cues[c];
                if (cue.IsVisual && cue.Duration > 0f)
                {
                    intervals.Add((cue.T, cue.End, cue.IsModifier ? "track:" + cue.Target : "cue:" + c));
                }
            }

            int max = 0;
            var moving = new HashSet<string>(StringComparer.Ordinal);
            foreach (var probe in intervals)
            {
                // The count only rises at an interval start, so probing every start finds the maximum.
                moving.Clear();
                foreach (var other in intervals)
                {
                    if (other.Start <= probe.Start + 1e-4f && probe.Start < other.End - 1e-4f)
                    {
                        moving.Add(other.Part);
                    }
                }

                max = Math.Max(max, moving.Count);
            }

            return max;
        }
    }

    /// <summary>The evaluated state of one track at a time.</summary>
    public struct PartState
    {
        public string TrackId;
        public string Part;
        public bool Visible;
        public PuppetPose Pose;
    }

    /// <summary>A cue running at a time; <see cref="Progress"/> is 0..1 over its duration.</summary>
    public struct CueState
    {
        public VignetteCue Cue;
        public float Elapsed;
        public float Progress;
    }

    /// <summary>Pure evaluation of a vignette. Allocation-free when the caller reuses its buffers.</summary>
    public static class VignetteSampler
    {
        /// <summary>Stamp wiggle: amplitude in degrees and number of swings.</summary>
        public const float StampWiggleDegrees = 14f;
        public const int StampWiggleSwings = 3;

        /// <summary>
        /// Fills <paramref name="parts"/> (one entry per track, in draw order) for local time <paramref name="t"/>
        /// and returns the count. A track is hidden before its first key; after its last key it holds the pose.
        /// </summary>
        public static int Sample(Vignette vignette, float t, PartState[] parts)
        {
            if (vignette == null)
            {
                throw new ArgumentNullException(nameof(vignette));
            }

            if (parts == null || parts.Length < vignette.Tracks.Count)
            {
                throw new ArgumentException("buffer smaller than the vignette's track count", nameof(parts));
            }

            for (int i = 0; i < vignette.Tracks.Count; i++)
            {
                parts[i] = SampleTrack(vignette.Tracks[i], t);
            }

            // Stamp cues wiggle their target to a stop.
            foreach (var cue in vignette.Cues)
            {
                if (!cue.IsModifier || t < cue.T || t >= cue.End || cue.Duration <= 0f)
                {
                    continue;
                }

                for (int i = 0; i < vignette.Tracks.Count; i++)
                {
                    if (string.Equals(parts[i].TrackId, cue.Target, StringComparison.Ordinal))
                    {
                        float p = (t - cue.T) / cue.Duration;
                        var pose = parts[i].Pose;
                        parts[i].Pose = pose.With(rotation: pose.Rotation + Wiggle(p));
                    }
                }
            }

            return vignette.Tracks.Count;
        }

        /// <summary>Convenience overload that allocates its own buffer (tests, tools).</summary>
        public static PartState[] Sample(Vignette vignette, float t)
        {
            var parts = new PartState[vignette.Tracks.Count];
            Sample(vignette, t, parts);
            return parts;
        }

        /// <summary>Adds the cues running at <paramref name="t"/> (instant cues fire for the frame they start in).</summary>
        public static void ActiveCues(Vignette vignette, float t, List<CueState> into)
        {
            into.Clear();
            foreach (var cue in vignette.Cues)
            {
                bool running = cue.Duration > 0f ? t >= cue.T && t < cue.End : Math.Abs(t - cue.T) < 1e-4f;
                if (running)
                {
                    float elapsed = t - cue.T;
                    into.Add(new CueState
                    {
                        Cue = cue,
                        Elapsed = elapsed,
                        Progress = cue.Duration > 0f ? elapsed / cue.Duration : 1f,
                    });
                }
            }
        }

        /// <summary>Cues whose start lies in (from, to]: fire world events (gesture, babble) once per frame step.</summary>
        public static void CuesStarted(Vignette vignette, float from, float to, List<VignetteCue> into)
        {
            into.Clear();
            foreach (var cue in vignette.Cues)
            {
                if (cue.T > from && cue.T <= to)
                {
                    into.Add(cue);
                }
            }
        }

        public static float Apply(Ease ease, float u)
        {
            u = u < 0f ? 0f : u > 1f ? 1f : u;
            switch (ease)
            {
                case Ease.Linear:
                    return u;
                case Ease.InOut:
                    return u * u * (3f - 2f * u);
                case Ease.Out:
                    return 1f - (1f - u) * (1f - u);
                case Ease.Back:
                    const float s = 1.70158f;
                    float v = u - 1f;
                    return 1f + v * v * ((s + 1f) * v + s);
                case Ease.Step:
                    return u >= 1f ? 1f : 0f;
                default:
                    throw new ArgumentOutOfRangeException(nameof(ease), ease, "unknown ease");
            }
        }

        /// <summary>Damped swing for a stamp, 0 at both ends.</summary>
        public static float Wiggle(float p)
        {
            p = p < 0f ? 0f : p > 1f ? 1f : p;
            return StampWiggleDegrees * (float)Math.Sin(2.0 * Math.PI * StampWiggleSwings * p) * (1f - p);
        }

        private static PartState SampleTrack(VignetteTrack track, float t)
        {
            var keys = track.Keys;
            var state = new PartState { TrackId = track.Id, Part = track.Part };
            if (keys.Count == 0)
            {
                return state;
            }

            if (t < keys[0].T)
            {
                state.Part = track.PartAt(0);
                state.Pose = keys[0].Pose;
                return state;
            }

            int i = keys.Count - 1;
            while (i > 0 && keys[i].T > t)
            {
                i--;
            }

            state.Part = track.PartAt(i);
            if (i == keys.Count - 1)
            {
                state.Pose = keys[i].Pose;
            }
            else
            {
                var a = keys[i];
                var b = keys[i + 1];
                float span = b.T - a.T;
                float e = Apply(b.Ease, span > 0f ? (t - a.T) / span : 1f);
                state.Pose = new PuppetPose(
                    Lerp(a.Pose.X, b.Pose.X, e),
                    Lerp(a.Pose.Y, b.Pose.Y, e),
                    Lerp(a.Pose.Scale, b.Pose.Scale, e),
                    Lerp(a.Pose.Rotation, b.Pose.Rotation, e),
                    Clamp01(Lerp(a.Pose.Alpha, b.Pose.Alpha, e)));
            }

            state.Visible = state.Pose.Alpha > 0.001f && state.Pose.Scale > 0.001f;
            return state;
        }

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }

    /// <summary>Authoring rules every vignette must satisfy (tests run it over all data).</summary>
    public static class VignetteValidator
    {
        public static List<string> Validate(Vignette v)
        {
            var errors = new List<string>();
            if (v == null)
            {
                errors.Add("null vignette");
                return errors;
            }

            string at = v.Id + ": ";
            if (string.IsNullOrEmpty(v.Id) || !v.Id.StartsWith("vg.", StringComparison.Ordinal))
            {
                errors.Add(at + "ID must start with 'vg.'");
            }

            if (v.Length < Vignette.MinLength || v.Length > Vignette.MaxLength)
            {
                errors.Add(at + "length " + v.Length + " s outside " + Vignette.MinLength + "–" + Vignette.MaxLength + " s");
            }

            if (v.LoopCount < 0)
            {
                errors.Add(at + "negative loop count");
            }

            if (v.Beats.Count < 1 || v.Beats.Count > Vignette.MaxBeats)
            {
                errors.Add(at + v.Beats.Count + " beats (1–" + Vignette.MaxBeats + ")");
            }

            float lastEnd = 0f;
            foreach (var beat in v.Beats)
            {
                if (beat.Start < lastEnd - 1e-4f || beat.End <= beat.Start || beat.End > v.Length + 1e-4f)
                {
                    errors.Add(at + "beats must be ordered, non-overlapping and inside the loop");
                }

                lastEnd = beat.End;
            }

            if (v.Tracks.Count == 0 || v.Tracks.Count > Vignette.MaxTracks)
            {
                errors.Add(at + v.Tracks.Count + " tracks (1–" + Vignette.MaxTracks + ")");
            }

            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var track in v.Tracks)
            {
                if (string.IsNullOrEmpty(track.Id) || !ids.Add(track.Id))
                {
                    errors.Add(at + "missing or duplicate track ID '" + track.Id + "'");
                }

                if (!PuppetParts.Contains(track.Part))
                {
                    errors.Add(at + track.Id + " uses unknown part '" + track.Part + "'");
                }

                if (track.Keys.Count == 0)
                {
                    errors.Add(at + track.Id + " has no keys");
                }

                float last = -1f;
                foreach (var key in track.Keys)
                {
                    if (key.T <= last || key.T < 0f || key.T > v.Length + 1e-4f)
                    {
                        errors.Add(at + track.Id + " key at " + key.T + " s is out of order or outside the loop");
                    }

                    last = key.T;
                    if (key.Part != null && !PuppetParts.Contains(key.Part))
                    {
                        errors.Add(at + track.Id + " switches to unknown part '" + key.Part + "'");
                    }

                    var p = key.Pose;
                    if (p.X < -0.3f || p.X > 1.3f || p.Y < -0.3f || p.Y > 1.3f || p.Scale < 0f || p.Alpha < 0f || p.Alpha > 1f)
                    {
                        errors.Add(at + track.Id + " key at " + key.T + " s has a pose off the stage");
                    }
                }
            }

            foreach (var cue in v.Cues)
            {
                if (cue.T < 0f || cue.Duration < 0f || cue.End > v.Length + 1e-4f)
                {
                    errors.Add(at + cue.Kind + " cue at " + cue.T + " s is outside the loop");
                }

                bool worldOnly = !cue.IsVisual;
                if (!worldOnly && v.FindTrack(cue.Target) == null)
                {
                    errors.Add(at + cue.Kind + " cue targets unknown track '" + cue.Target + "'");
                }

                if (cue.Kind == CueKind.Gesture && string.IsNullOrEmpty(cue.Arg))
                {
                    errors.Add(at + "gesture cue needs a clip name");
                }
            }

            int moving = v.MaxMovingAtOnce();
            if (moving > Vignette.MaxMovingParts)
            {
                errors.Add(at + moving + " parts move at once (at most " + Vignette.MaxMovingParts + ")");
            }

            return errors;
        }
    }
}
