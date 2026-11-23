using System;

namespace SpookyStreet.Core.Animation
{
    /// <summary>
    /// The clips of a witness: the loop shown at rest, the loop shown while Pip is near, and the one-shot greeting
    /// that plays once when Pip approaches (it starts and ends on the rest loop's first frame).
    /// </summary>
    public readonly struct WitnessClipSet
    {
        public WitnessClipSet(string rest, string engaged, string greeting)
        {
            Rest = rest;
            Engaged = engaged;
            Greeting = greeting;
        }

        public string Rest { get; }
        public string Engaged { get; }
        public string Greeting { get; }
    }

    /// <summary>
    /// The clip contract for every character the import rules see: Pip's clips (<see cref="PipClips"/>) plus the
    /// witnesses' (tools/blender/export/witness_anims.py). Bea stands (Idle, Talk, Wave, Point); Mr. Vale sits on the
    /// bench (SitIdle, SitTalk and a seated Wave) and has no Idle, so his required clip is SitIdle.
    /// </summary>
    public static class CharacterClips
    {
        public const string Idle = PipClips.Idle;

        /// <summary>The in-place walk loop of a character that walks an authored path (June, to the foyer). The same
        /// name as Pip's, so <see cref="PipClips.Looping"/> already classifies it as looping.</summary>
        public const string Walk = PipClips.Walk;
        public const string Talk = "Talk";
        public const string SitIdle = "SitIdle";
        public const string SitTalk = "SitTalk";
        public const string KneeTap = "KneeTap";
        public const string Point = "Point";
        public const string OpenBooth = "OpenBooth";
        public const string Wave = "Wave";

        /// <summary>Bea's testimony gesture (S003a): a hand over her eyes, head back, searching up high. Where she
        /// looked, never what was there (D26). The clip aims along her own forward: turn her to the place first.</summary>
        public const string ShadeEyes = "ShadeEyes";

        /// <summary>Mr. Vale's testimony gesture (S003a), seated: he checks his wristwatch, two beats, looks up.
        /// About when, never what. It aims nowhere: he must not be turned on his bench for it.</summary>
        public const string CheckWatch = "CheckWatch";

        /// <summary>June's testimony gesture (S003a): a hand cupped behind her ear ("Listen…"). It leans to her own
        /// side, not toward a place.</summary>
        public const string Listen = "Listen";

        /// <summary>Aunt Kit's testimony gesture (S003a): hand to her cheek, head tilted, then she looks up, asking
        /// ("So, detective, what happened here?"). She asks; she never tells.</summary>
        public const string Ponder = "Ponder";

        /// <summary>The S003a testimony gestures, one per cast member, all one-shots that start and end on the
        /// character's rest loop (frame 0). None of them points at a clue.</summary>
        public static readonly string[] TestimonyGestures = { ShadeEyes, CheckWatch, Listen, Ponder };

        /// <summary>
        /// The testimony gesture of a cast member by model name ("Bea", "Vale", "June", "Kit"), or null for anyone
        /// else. A model that lacks the clip simply does not gesture (<c>Witness.PlayGesture</c> returns false).
        /// </summary>
        public static string TestimonyGestureOf(string model)
        {
            switch (model)
            {
                case "Bea": return ShadeEyes;
                case "Vale": return CheckWatch;
                case "June": return Listen;
                case "Kit": return Ponder;
                default: return null;
            }
        }

        /// <summary>
        /// True when a gesture is aimed along the character's forward, so the body turns to the cue's point while it
        /// plays (Point, ShadeEyes, a wave at someone). False for the gestures that are about the speaker and aim
        /// nowhere (CheckWatch, Listen, Ponder): turning for those would point a non-pointing gesture at a place,
        /// and would spin Mr. Vale on his bench.
        /// </summary>
        public static bool TurnsToAim(string clipName)
        {
            switch (PipClips.CleanClipName(clipName))
            {
                case CheckWatch:
                case Listen:
                case Ponder:
                    return false;
                default:
                    return true;
            }
        }

        /// <summary>Witness clips that cycle; <see cref="Wave"/> plays once.</summary>
        public static readonly string[] WitnessLooping = { Talk, SitIdle, SitTalk, KneeTap };

        public static readonly WitnessClipSet Standing = new WitnessClipSet(Idle, Talk, Wave);
        public static readonly WitnessClipSet Seated = new WitnessClipSet(SitIdle, SitTalk, Wave);

        /// <summary>True for every looping clip of any character (exporter take prefixes are ignored).</summary>
        public static bool IsLooping(string clipName)
        {
            if (PipClips.IsLooping(clipName))
            {
                return true;
            }

            return Array.IndexOf(WitnessLooping, PipClips.CleanClipName(clipName)) >= 0;
        }
    }
}
