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
    /// witnesses' (tools/blender/export/witness_anims.py). Bea stands (Idle, Talk, Wave); Mr. Vale sits on the
    /// bench (SitIdle, SitTalk and a seated Wave) and has no Idle, so his required clip is SitIdle.
    /// </summary>
    public static class CharacterClips
    {
        public const string Idle = PipClips.Idle;
        public const string Talk = "Talk";
        public const string SitIdle = "SitIdle";
        public const string SitTalk = "SitTalk";
        public const string KneeTap = "KneeTap";
        public const string Point = "Point";
        public const string OpenBooth = "OpenBooth";
        public const string Wave = "Wave";

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
