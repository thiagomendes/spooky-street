using System;
using System.Collections.Generic;
using SpookyStreet.Core.Case;

namespace SpookyStreet.Core.Nonverbal
{
    /// <summary>What asked for a hint picture: the radio, or the opt-in Helper after a long stall (D23.4).</summary>
    public enum HintTrigger
    {
        RadioPress,
        HelperStall,
    }

    /// <summary>The world and HUD pictures one hint shows (D26 §6, docs/mystery-first.md §6).</summary>
    public readonly struct HintVisuals : IEquatable<HintVisuals>
    {
        public HintVisuals(bool breeze, bool batTrail, bool moveDemo)
        {
            Breeze = breeze;
            BatTrail = batTrail;
            MoveDemo = moveDemo;
        }

        public static HintVisuals None => default;

        /// <summary>Tier 1 nudge: a breeze of leaves toward the area, a warm glow over the area, a witness glances there.</summary>
        public bool Breeze { get; }

        /// <summary>Tier 2 trail: the paper bats fly to the area and circle it (no goal sticker).</summary>
        public bool BatTrail { get; }

        /// <summary>Tier 3: the ghost hand (touch) or the button or key glyph demonstrates a move. Never Use.</summary>
        public bool MoveDemo { get; }

        public bool Any => Breeze || BatTrail || MoveDemo;

        public bool Equals(HintVisuals other) =>
            Breeze == other.Breeze && BatTrail == other.BatTrail && MoveDemo == other.MoveDemo;

        public override bool Equals(object obj) => obj is HintVisuals other && Equals(other);

        public override int GetHashCode() => (Breeze ? 1 : 0) | (BatTrail ? 2 : 0) | (MoveDemo ? 4 : 0);

        public override string ToString() => $"Breeze={Breeze} BatTrail={BatTrail} MoveDemo={MoveDemo}";
    }

    /// <summary>A character that could glance toward a tier-1 area.</summary>
    public readonly struct LookCandidate
    {
        public LookCandidate(float x, float y, float z, bool inView, bool available = true)
        {
            X = x;
            Y = y;
            Z = z;
            InView = inView;
            Available = available;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        /// <summary>On screen (in front of the camera, inside the view).</summary>
        public bool InView { get; }

        /// <summary>Shown and free (not hidden indoors, not mid-conversation or gesture).</summary>
        public bool Available { get; }
    }

    /// <summary>
    /// The hint pictures' rules, engine-free (D26 §6, retiering S002 hints close): hints answer "where can I look?"
    /// and "how do I get there?", never "what does it mean?". Tier 1 is an area nudge (a breeze of leaves toward the
    /// area and a warm glow over it, the nearest witness glances that way), tier 2 the paper bats to the area, circling
    /// it, and tier 3 only demonstrates a move where a move blocks the way (<see cref="HintMoves"/>). Every target is
    /// an authored area (<see cref="HintAreas"/>), never an object. Nothing shows by itself unless the player turned
    /// the Helper on, and then only the tier-1 nudge.
    /// </summary>
    public static class HintPresentation
    {
        /// <summary>How long the glancing character holds the turn toward the area.</summary>
        public const float LookSeconds = 2f;

        /// <summary>The breeze and the area glow last this long per press (they also stop when the objective changes).</summary>
        public const float BreezeSeconds = 6f;

        /// <summary>Leaves alive at once in the breeze (pooled).</summary>
        public const int BreezeLeaves = 8;

        /// <summary>A character further than this from Pip does not glance (they would be too small to read).</summary>
        public const float MaxLookDistance = 16f;

        /// <summary>The move demonstration's loop count per press and one loop's length.</summary>
        public const int DemoLoops = 3;

        public const float DemoLoopSeconds = 2.2f;

        /// <summary>Height above an area's floor point where a character's glance and the bats' circle aim.</summary>
        public const float AreaLift = 1.2f;

        /// <summary>
        /// Which pictures a hint shows. A radio press shows its own tier (tier 3 only with a move to show). The Helper
        /// (opt-in, D23) may offer the tier-1 nudge after a long stall, never the trail or a demo (D26 §6).
        /// </summary>
        public static HintVisuals For(HintTrigger trigger, HintTier tier, NonverbalSettings settings, float stallSeconds = 0f,
            HintMove move = HintMove.None)
        {
            if (trigger == HintTrigger.RadioPress)
            {
                switch (tier)
                {
                    case HintTier.Observation:
                        return new HintVisuals(true, false, false);
                    case HintTier.Location:
                        return new HintVisuals(false, true, false);
                    case HintTier.Action:
                        return move != HintMove.None ? new HintVisuals(false, false, true) : new HintVisuals(false, true, false);
                    default:
                        return HintVisuals.None;
                }
            }

            return settings != null && settings.MayAutoShowTier3(stallSeconds)
                ? new HintVisuals(true, false, false)
                : HintVisuals.None;
        }

        /// <summary>
        /// The area a hint shows, seen from where Pip is (an area behind the house walls gives way to the threshold
        /// area on Pip's side, D24.1). False in free roam.
        /// </summary>
        public static bool TryArea(CaseObjective objective, float pipX, float pipZ, bool doorOpen, out HintArea area)
        {
            area = HintAreas.For(objective, pipX, pipZ, doorOpen);
            return area != null;
        }

        /// <summary>
        /// The character who glances toward a tier-1 area: the one nearest Pip (plan distance) among those available
        /// and on screen, within <see cref="MaxLookDistance"/>. −1 when nobody qualifies (the breeze alone still shows
        /// the way).
        /// </summary>
        public static int ChooseLooker(IReadOnlyList<LookCandidate> candidates, float pipX, float pipZ)
        {
            if (candidates == null)
            {
                return -1;
            }

            int best = -1;
            float bestDistance = MaxLookDistance * MaxLookDistance;
            for (int i = 0; i < candidates.Count; i++)
            {
                var c = candidates[i];
                if (!c.Available || !c.InView)
                {
                    continue;
                }

                float dx = c.X - pipX;
                float dz = c.Z - pipZ;
                float d = dx * dx + dz * dz;
                if (d <= bestDistance)
                {
                    bestDistance = d;
                    best = i;
                }
            }

            return best;
        }

        /// <summary>The control a move demonstration presses (the Jump control; held for the high jump and the glide).</summary>
        public static ControlPrompt PromptFor(HintMove move)
        {
            switch (move)
            {
                case HintMove.HighJump:
                case HintMove.Glide:
                    return ControlPrompt.HoldJump;
                case HintMove.Jump:
                case HintMove.WallKick:
                case HintMove.Ledge:
                    return ControlPrompt.Jump;
                default:
                    throw new ArgumentOutOfRangeException(nameof(move), move, "no move to demonstrate");
            }
        }

        /// <summary>True when the demo holds the control (a candy ring fills), false for a tap.</summary>
        public static bool Holds(HintMove move) => PromptFor(move) == ControlPrompt.HoldJump;

        /// <summary>The share of a bat's flight spent flying in from Pip; the rest circles the area once.</summary>
        public const float TrailArrive = 0.42f;

        /// <summary>The bats circle at this share of the area's radius, this high above its floor point.</summary>
        public const float TrailRingFactor = 0.9f;

        public const float TrailLift = 2.2f;

        /// <summary>The breeze's leaves end in a small swirl this wide round the area's centre, this high.</summary>
        public const float SwirlRadius = 0.5f;

        public const float SwirlLift = 0.8f;

        /// <summary>No circling bat or swirling leaf comes closer than this (3D metres) to a Use target's marker.</summary>
        public const float MinMarkerClearance = 0.7f;

        /// <summary>
        /// The trail's flight (tier 2): at progress <paramref name="u"/> (0..1) a bat that meets the area's ring at
        /// <paramref name="startAngle"/> radians is either still flying in (<paramref name="approach"/> &lt; 1: the
        /// share of the way flown, toward the ring point) or circling the area once at its radius. Offsets are plan
        /// metres from the area centre. The bats never fly to the area's centre, so they never sit on a thing in it.
        /// </summary>
        public static void TrailPose(float u, float startAngle, float radius, out float offsetX, out float offsetZ, out float approach)
        {
            u = Math.Max(0f, Math.Min(1f, u));
            if (u < TrailArrive)
            {
                approach = u / TrailArrive;
                offsetX = (float)Math.Cos(startAngle) * radius;
                offsetZ = (float)Math.Sin(startAngle) * radius;
                return;
            }

            approach = 1f;
            float lap = (u - TrailArrive) / (1f - TrailArrive);
            float angle = startAngle + lap * 2f * (float)Math.PI;
            offsetX = (float)Math.Cos(angle) * radius;
            offsetZ = (float)Math.Sin(angle) * radius;
        }
    }
}
