using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.Crowd
{
    /// <summary>The timing of one trick-or-treat clip: its name, length at <see cref="CrowdClipTimings.Fps"/>, and the
    /// normalised time of its one marker (the treat let go, the hand on the door, the treat landing), or -1.</summary>
    public readonly struct CrowdClipTiming
    {
        public CrowdClipTiming(string name, int frames, float marker, string markerName)
        {
            Name = name;
            Frames = frames;
            Marker = marker;
            MarkerName = markerName;
        }

        public string Name { get; }

        /// <summary>Length in frames at 30 fps; the clip's last key is frame <see cref="Frames"/>.</summary>
        public int Frames { get; }

        public float Seconds => Frames / CrowdClipTimings.Fps;

        /// <summary>Normalised time (0..1) of the marker, or -1 when the clip has none.</summary>
        public float Marker { get; }

        /// <summary>What happens at the marker ("release", "door", "drop", "hop"), for logs and tests.</summary>
        public string MarkerName { get; }

        public float MarkerSeconds => Marker < 0f ? -1f : Marker * Seconds;
    }

    /// <summary>
    /// The trick-or-treat clips of the crowd bodies and Pip (tools/blender/crowd/crowd_anims.py and
    /// tools/blender/export/pip_anims.py; docs/living-street-crowd-art.md "Trick-or-treat clips"): names, lengths and
    /// the normalised times the game sequences on. All are one-shots, in place, at 30 fps.
    /// <list type="bullet">
    /// <item>Grown-up (<see cref="CrowdKit.AdultModel"/> and the door neighbours' <see cref="CrowdKit.NeighbourModel"/>):
    /// <see cref="GiveTreat"/>, <see cref="OpenDoorPeek"/>, <see cref="WalkStart"/>, <see cref="WalkStop"/>.</item>
    /// <item>Child (<see cref="CrowdKit.ChildModel"/>): <see cref="HoldPail"/>, <see cref="Cheer"/>,
    /// <see cref="WalkStart"/>, <see cref="WalkStop"/>.</item>
    /// <item>Pip: <see cref="HandTreat"/> (on her own rig, in Pip.fbx).</item>
    /// </list>
    /// GiveTreat, OpenDoorPeek, HoldPail, Cheer and HandTreat start and end on Idle frame 0. WalkStart starts on Idle
    /// frame 0 and ends on Walk frame 0 (left foot strike); WalkStop starts on Walk frame 0 and ends on Idle frame 0.
    /// </summary>
    public static class CrowdClipTimings
    {
        public const float Fps = 30f;

        /// <summary>Grown-up: the bowl, tin or basket held at the waist in the right hand (the <c>Pail</c> bone), the free
        /// (left) hand reaches in, takes a sweet, leans forward and down and places it in a child's pail at
        /// <see cref="GiveTreatReleaseForward"/> / <see cref="GiveTreatReleaseHeight"/>, then straightens.</summary>
        public const string GiveTreat = "GiveTreat";

        public const int GiveTreatFrames = 48;

        /// <summary>The left hand is in the bowl and closes on a sweet: parent a treat to <see cref="TreatHandBone"/> here.</summary>
        public const float GiveTreatPickUp = 0.25f;

        /// <summary>The sweet is let go over the child's pail.</summary>
        public const float GiveTreatRelease = 0.55f;

        /// <summary>Grown-up: stepping into the doorway with a small lean, the left hand reaching for the edge of the
        /// door leaf (moved by code), then a friendly look down at the children. Bells only: no knocking.</summary>
        public const string OpenDoorPeek = "OpenDoorPeek";

        public const int OpenDoorPeekFrames = 36;

        /// <summary>The left hand reaches the door edge: start swinging the leaf here.</summary>
        public const float OpenDoorPeekHandOnDoor = 0.25f;

        /// <summary>Child: lifts the pail forward with both hands, holds it out, a small bob when the sweet drops in,
        /// then brings it back.</summary>
        public const string HoldPail = "HoldPail";

        public const int HoldPailFrames = 48;

        /// <summary>The sweet lands in the pail (the little bob).</summary>
        public const float HoldPailDrop = 0.6f;

        /// <summary>Child: a small happy hop with both arms up, after getting a sweet.</summary>
        public const string Cheer = "Cheer";

        public const int CheerFrames = 27;

        /// <summary>The top of the hop.</summary>
        public const float CheerHop = 0.45f;

        /// <summary>Both bodies: from Idle frame 0 into Walk frame 0. Move the root by <see cref="RootTravel"/>.</summary>
        public const string WalkStart = "WalkStart";

        public const int WalkStartFrames = 12;

        /// <summary>Both bodies: from Walk frame 0 (start it when the Walk cycle wraps) to Idle frame 0. Move the root by
        /// <see cref="RootTravel"/>.</summary>
        public const string WalkStop = "WalkStop";

        public const int WalkStopFrames = 12;

        /// <summary>Pip: she lifts her pail (her treat bowl) in front of her, reaches in with her free hand and places a
        /// sweet forward in a child's pail at child height; release at <see cref="HandTreatRelease"/>.
        /// The same name as <c>PipClips.HandTreat</c>.</summary>
        public const string HandTreat = "HandTreat";

        public const int HandTreatFrames = 48;

        public const float HandTreatPickUp = 0.25f;

        public const float HandTreatRelease = 0.55f;

        /// <summary>The bone the sweet rides on between pick-up and release (the knuckles of the free hand).</summary>
        public const string TreatHandBone = "Fingers_L";

        /// <summary>Where the grown-up's knuckles are at the release, in the giver's own frame (metres ahead of the
        /// root and above the ground).</summary>
        public const float GiveTreatReleaseForward = 0.45f;

        public const float GiveTreatReleaseHeight = 0.62f;

        /// <summary>Where Pip's knuckles are at the release (a forward reach at child height).</summary>
        public const float HandTreatReleaseForward = 0.38f;

        public const float HandTreatReleaseHeight = 0.62f;

        /// <summary>Where the middle of the child's pail mouth is while it is held out (from the drop until
        /// it is brought back), in the child's own frame. A child receiving from a giver stands
        /// release-forward + this ahead of the giver, facing it.</summary>
        public const float HoldPailMouthForward = 0.26f;

        public const float HoldPailMouthHeight = 0.56f;

        public static readonly CrowdClipTiming[] GrownUp =
        {
            new CrowdClipTiming(GiveTreat, GiveTreatFrames, GiveTreatRelease, "release"),
            new CrowdClipTiming(OpenDoorPeek, OpenDoorPeekFrames, OpenDoorPeekHandOnDoor, "door"),
            new CrowdClipTiming(WalkStart, WalkStartFrames, -1f, null),
            new CrowdClipTiming(WalkStop, WalkStopFrames, -1f, null),
        };

        public static readonly CrowdClipTiming[] Child =
        {
            new CrowdClipTiming(HoldPail, HoldPailFrames, HoldPailDrop, "drop"),
            new CrowdClipTiming(Cheer, CheerFrames, CheerHop, "hop"),
            new CrowdClipTiming(WalkStart, WalkStartFrames, -1f, null),
            new CrowdClipTiming(WalkStop, WalkStopFrames, -1f, null),
        };

        public static readonly CrowdClipTiming[] Pip =
        {
            new CrowdClipTiming(HandTreat, HandTreatFrames, HandTreatRelease, "release"),
        };

        /// <summary>The new clips of a crowd model (<see cref="CrowdKit.ChildModel"/>, <see cref="CrowdKit.AdultModel"/>
        /// or <see cref="CrowdKit.NeighbourModel"/>); empty for anything else.</summary>
        public static IReadOnlyList<CrowdClipTiming> For(string model)
        {
            if (model == CrowdKit.ChildModel)
            {
                return Child;
            }

            if (model == CrowdKit.AdultModel || model == CrowdKit.NeighbourModel)
            {
                return GrownUp;
            }

            return Array.Empty<CrowdClipTiming>();
        }

        /// <summary>The timing of a clip by name (any body, Pip included), or null.</summary>
        public static CrowdClipTiming? Of(string name)
        {
            foreach (var set in new[] { GrownUp, Child, Pip })
            {
                foreach (var t in set)
                {
                    if (t.Name == name)
                    {
                        return t;
                    }
                }
            }

            return null;
        }

        /// <summary>How far (m) the root travels over the whole WalkStart or WalkStop of a model, so the planted foot
        /// stays put. Child and grown-up differ because their legs and walk cadence do.</summary>
        public static float WalkTransitionDistance(string clip, string model)
        {
            bool grownUp = model == CrowdKit.AdultModel || model == CrowdKit.NeighbourModel;
            if (clip == WalkStart)
            {
                return grownUp ? AdultWalkStartDistance : ChildWalkStartDistance;
            }

            if (clip == WalkStop)
            {
                return grownUp ? AdultWalkStopDistance : ChildWalkStopDistance;
            }

            return 0f;
        }

        public const float ChildWalkStartDistance = 0.2f;
        public const float ChildWalkStopDistance = 0.2f;
        public const float AdultWalkStartDistance = 0.3f;
        public const float AdultWalkStopDistance = 0.3f;

        /// <summary>
        /// Metres the root has travelled forward at normalised time <paramref name="u"/> of WalkStart or WalkStop
        /// (0 at u = 0, <see cref="WalkTransitionDistance"/> at u = 1). WalkStart speeds up from standing to
        /// <see cref="CrowdKit.WalkSpeed"/> at its end; WalkStop slows from that speed to standing. The clips are
        /// in place and were posed against exactly this travel, so moving the root by it keeps the feet planted.
        /// 0 for any other clip.
        /// </summary>
        public static float RootTravel(string clip, string model, float u)
        {
            float d = WalkTransitionDistance(clip, model);
            if (d <= 0f)
            {
                return 0f;
            }

            u = u < 0f ? 0f : u > 1f ? 1f : u;
            float frames = clip == WalkStart ? WalkStartFrames : WalkStopFrames;
            float k = CrowdKit.WalkSpeed(model) * (frames / Fps) / d; // end (start) speed in units of d per clip
            return clip == WalkStart ? d * Ease(u, k) : d * (1f - Ease(1f - u, k));
        }

        /// <summary>g(0) = 0, g(1) = 1, g'(0) = 0, g'(1) = k: a cubic that starts at rest and ends at speed k.
        /// Monotonic for 0 &lt; k &lt;= 3.</summary>
        public static float Ease(float u, float k)
        {
            float a = 3f - k;
            float b = k - 2f;
            return a * u * u + b * u * u * u;
        }
    }
}
