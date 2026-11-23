using System;
using System.Collections.Generic;
using SpookyStreet.Core.Animation;
using SpookyStreet.Core.Case;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;

namespace SpookyStreet.Core.Characters
{
    /// <summary>A testimony gesture cued by one spoken line (S003a): the clip, when it starts and where it faces.</summary>
    public readonly struct LineGesture
    {
        public LineGesture(string clip, float delay, StagePoint? aim = null)
        {
            Clip = clip;
            Delay = delay;
            Aim = aim;
        }

        /// <summary>The speaker's testimony gesture (<see cref="CharacterClips.TestimonyGestureOf"/>).</summary>
        public string Clip { get; }

        /// <summary>Seconds after the line appears, so the gesture lands on the words it belongs to (game time).</summary>
        public float Delay { get; }

        /// <summary>
        /// The place the speaker turns toward while the gesture plays, only for a gesture aimed along the body
        /// (<see cref="CharacterClips.TurnsToAim"/>); null keeps the speaker's own facing.
        /// </summary>
        public StagePoint? Aim { get; }

        public override string ToString() => $"{Clip} +{Delay:0.0}s";
    }

    /// <summary>
    /// Which spoken lines of case 01 play their speaker's testimony gesture (S003a, D26). Data keyed by the line's
    /// localization key, so a translation never moves a gesture. Only lines where the body adds meaning to the words
    /// get one, never every line and never a line after the case is explained:
    /// <list type="bullet">
    /// <item>Bea shades her eyes on "Something silver flashed up high" and "Up high, over the hedge", turned toward the
    /// side of the house her words name (the side yard behind the hedge), never at a thing: she saw a flash, not who.</item>
    /// <item>Mr. Vale checks his watch on his lines about when ("all evening", "when I looked"); it points nowhere.</item>
    /// <item>June cups her ear when she asks about the knocks or says "Listen…"; she leans to her own side, not toward
    /// the floor or the workshop, and does not beat the rhythm.</item>
    /// <item>Aunt Kit ponders on "So, detective, what happened here?": she asks, she never tells.</item>
    /// </list>
    /// </summary>
    public static class LineGestures
    {
        /// <summary>No gesture starts later than this after its line appears (lines are short; a tap may move on).</summary>
        public const float MaxDelay = 2f;

        /// <summary>
        /// Where Bea turns to shade her eyes: up high over the side yard, behind the hedge by the side of the house (the
        /// place her line names; the side yard hint area's centre, 3 m up). Only its direction on the ground is used.
        /// </summary>
        public static readonly StagePoint BeaLookedUpHigh = Above(HintAreas.Get(HintAreas.SideYard).Centre, 3f);

        private static readonly Dictionary<string, LineGesture> Gestures = new Dictionary<string, LineGesture>(StringComparer.Ordinal)
        {
            // Bea, by her bicycle: where she looked (up high, over the hedge), as she says it.
            { Keys.BeaSawCape, new LineGesture(CharacterClips.ShadeEyes, 0.3f, BeaLookedUpHigh) },
            { Keys.BeaRepeat, new LineGesture(CharacterClips.ShadeEyes, 0.3f, BeaLookedUpHigh) },

            // Mr. Vale, seated: the watch on "all evening" and on "when I looked". Not on his first account (the
            // bench view is what that line is about) and not on the invitation to sit.
            { Keys.ValeRepeat, new LineGesture(CharacterClips.CheckWatch, 0.6f) },
            { Keys.ValeWasShut, new LineGesture(CharacterClips.CheckWatch, 0.3f) },

            // June: the hand to her ear on the question about the knocks (the end of her greeting) and on "Listen…".
            { Keys.JuneGreet, new LineGesture(CharacterClips.Listen, 1.2f) },
            { Keys.JuneGreetShortcut, new LineGesture(CharacterClips.Listen, 1.6f) },
            { Keys.JuneListenFloor, new LineGesture(CharacterClips.Listen, 0.9f) },

            // Aunt Kit asks the child to explain the case.
            { Keys.KitAsk, new LineGesture(CharacterClips.Ponder, 0.2f) },
        };

        /// <summary>The line keys that cue a gesture.</summary>
        public static IEnumerable<string> LineKeys => Gestures.Keys;

        /// <summary>The gesture <paramref name="lineKey"/> cues; false for every other line (most lines).</summary>
        public static bool TryGet(string lineKey, out LineGesture gesture)
        {
            if (lineKey != null && Gestures.TryGetValue(lineKey, out gesture))
            {
                return true;
            }

            gesture = default;
            return false;
        }

        /// <summary>The case 01 speaker of a line key (<c>c01.line.&lt;speaker&gt;.*</c>), or null for other keys.</summary>
        public static Speaker? SpeakerOf(string lineKey)
        {
            const string prefix = "c01.line.";
            if (lineKey == null || !lineKey.StartsWith(prefix, StringComparison.Ordinal))
            {
                return null;
            }

            int dot = lineKey.IndexOf('.', prefix.Length);
            string who = dot > 0 ? lineKey.Substring(prefix.Length, dot - prefix.Length) : null;
            switch (who)
            {
                case "pip": return Speaker.Pip;
                case "bea": return Speaker.Bea;
                case "vale": return Speaker.Vale;
                case "june": return Speaker.June;
                case "kit": return Speaker.Kit;
                default: return null;
            }
        }

        private static StagePoint Above(StagePoint p, float up) => new StagePoint(p.X, p.Y + up, p.Z);
    }
}
