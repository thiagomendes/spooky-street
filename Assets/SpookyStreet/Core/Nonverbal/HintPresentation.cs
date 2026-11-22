using System;
using System.Collections.Generic;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.World;

namespace SpookyStreet.Core.Nonverbal
{
    /// <summary>What asked for a hint picture: the radio, or the opt-in Helper after a long stall (D23.4).</summary>
    public enum HintTrigger
    {
        RadioPress,
        HelperStall,
    }

    /// <summary>The world and HUD pictures one hint shows (docs/nonverbal-ux.md §6).</summary>
    public readonly struct HintVisuals : IEquatable<HintVisuals>
    {
        public HintVisuals(bool sparkle, bool batTrail, bool ghostDemo)
        {
            Sparkle = sparkle;
            BatTrail = batTrail;
            GhostDemo = ghostDemo;
        }

        public static HintVisuals None => default;

        /// <summary>Tier 1 "look": star flecks on the target and the nearest character's head turns to it.</summary>
        public bool Sparkle { get; }

        /// <summary>Tier 2 "where": the paper-bat trail and the goal sticker.</summary>
        public bool BatTrail { get; }

        /// <summary>Tier 3 "show me": the ghost hand (touch) or the pulsing button or key glyph.</summary>
        public bool GhostDemo { get; }

        public bool Any => Sparkle || BatTrail || GhostDemo;

        public bool Equals(HintVisuals other) =>
            Sparkle == other.Sparkle && BatTrail == other.BatTrail && GhostDemo == other.GhostDemo;

        public override bool Equals(object obj) => obj is HintVisuals other && Equals(other);

        public override int GetHashCode() => (Sparkle ? 1 : 0) | (BatTrail ? 2 : 0) | (GhostDemo ? 4 : 0);

        public override string ToString() => $"Sparkle={Sparkle} BatTrail={BatTrail} GhostDemo={GhostDemo}";
    }

    /// <summary>The action the tier-3 demonstration shows.</summary>
    public enum HintDemo
    {
        /// <summary>Push the stick (or the key) toward the goal.</summary>
        Move,

        /// <summary>Press Use: the goal is in reach.</summary>
        Use,

        /// <summary>Hold Jump for the high jump: the goal is just above Pip.</summary>
        HoldJump,
    }

    /// <summary>A character that could turn its head toward a tier-1 target.</summary>
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
    /// The hint pictures' rules, engine-free (docs/nonverbal-ux.md §6, D23.4): which tier shows what and when, the
    /// tier-1 target and the character who looks at it, and the action the tier-3 demonstration shows. The radio
    /// gives tier 1 (sparkle + look), tier 2 (bat trail) and tier 3 (ghost demo) one press at a time; nothing shows
    /// by itself unless the player turned the Helper on and the stall reached <see cref="NonverbalSettings.AutoHintStallSeconds"/>.
    /// </summary>
    public static class HintPresentation
    {
        /// <summary>How long the looking character holds the turn toward the target.</summary>
        public const float LookSeconds = 2f;

        /// <summary>The sparkle's length per press (it also stops when the objective changes).</summary>
        public const float SparkleSeconds = 6f;

        /// <summary>Star flecks alive at once (pooled).</summary>
        public const int SparkleFlecks = 6;

        /// <summary>A character further than this from Pip does not look (they would be too small to read).</summary>
        public const float MaxLookDistance = 16f;

        /// <summary>The ghost demonstration's loop count per press and one loop's length.</summary>
        public const int DemoLoops = 3;

        public const float DemoLoopSeconds = 2.2f;

        /// <summary>The Use demonstration is chosen once Pip is this close (plan metres) to the target.</summary>
        public const float UseReach = 2.2f;

        /// <summary>The high-jump demonstration: the goal is at least this far above Pip, and this close in plan.</summary>
        public const float HighJumpRise = 1.2f;

        public const float HighJumpReach = 4f;

        /// <summary>Lift above the stage point for anchors that have no marker height (porch, treehouse, attic).</summary>
        public const float AnchorLift = 1.2f;

        /// <summary>Which pictures a hint shows. Tiers 1 and 3 never show without a press unless the Helper is on.</summary>
        public static HintVisuals For(HintTrigger trigger, HintTier tier, NonverbalSettings settings, float stallSeconds = 0f)
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
                        return new HintVisuals(false, false, true);
                    default:
                        return HintVisuals.None;
                }
            }

            // The Helper (opt-in) may show "look here" and "show me" by itself after a long stall; never otherwise.
            return settings != null && settings.MayAutoShowTier3(stallSeconds)
                ? new HintVisuals(true, false, true)
                : HintVisuals.None;
        }

        /// <summary>
        /// The tier-1 target for an objective and the sticker part, seen from where Pip is (D24.1): a target on the
        /// other side of the owl house walls is shown at the threshold on Pip's side instead (the open front door,
        /// or June's window while the door is latched), because a sparkle behind a wall cannot be seen. The target
        /// sits at the point's marker height (the thing itself), anchors at <see cref="AnchorLift"/>. False in free
        /// roam.
        /// </summary>
        public static bool TryTarget(CaseObjective objective, float pipX, float pipZ, bool doorOpen, out StagePoint target,
            out string part)
        {
            if (!TryTarget(objective, out target, out part))
            {
                return false;
            }

            bool pipInside = CaseRouteLayout.Inside(pipX, pipZ);
            bool goalInside = CaseRouteLayout.Inside(target.X, target.Z);
            var via = Thresholds.WayTo(pipInside, goalInside, doorOpen);
            if (via != null)
            {
                target = new StagePoint(via.CentreX, via.FloorY + AnchorLift, via.CentreZ);
                part = via.Id == ThresholdId.FrontDoor ? "pg.door_open" : "pg.june_window";
            }

            return true;
        }

        /// <summary>The interaction point an objective is met at (null when it is met by arriving somewhere).</summary>
        public static string PointIdFor(CaseObjective objective)
        {
            switch (objective)
            {
                case CaseObjective.CheckView:
                    return CaseStage.PointBench;
                case CaseObjective.OpenDoor:
                    return CaseStage.PointLatch;
                case CaseObjective.FindSource:
                    return CaseStage.PointGrille;
                case CaseObjective.LinkArm:
                    return CaseStage.PointLinkArm;
                case CaseObjective.TurnCrank:
                    return CaseStage.PointCrank;
                default:
                    return null;
            }
        }

        /// <summary>The objective's target and sticker part wherever Pip is; false in free roam.</summary>
        public static bool TryTarget(CaseObjective objective, out StagePoint target, out string part)
        {
            string anchor = null;
            string point = PointIdFor(objective);
            switch (objective)
            {
                case CaseObjective.Arrival:
                    anchor = CaseAnchors.BluePorch;
                    part = "pg.house";
                    break;
                case CaseObjective.CheckView:
                    part = "pg.bench";
                    break;
                case CaseObjective.FindWayIn:
                    anchor = CaseAnchors.Treehouse;
                    part = "pg.treehouse";
                    break;
                case CaseObjective.MeetJune:
                    anchor = CaseAnchors.Attic;
                    part = "pg.june";
                    break;
                case CaseObjective.OpenDoor:
                    part = "pg.latch";
                    break;
                case CaseObjective.FindSource:
                    part = "pg.grille";
                    break;
                case CaseObjective.LinkArm:
                    part = "pg.arm";
                    break;
                case CaseObjective.TurnCrank:
                    part = "pg.crank";
                    break;
                case CaseObjective.ReturnToPorch:
                    anchor = CaseAnchors.ArrivalPorch;
                    part = "pg.porch";
                    break;
                default:
                    target = default;
                    part = null;
                    return false;
            }

            if (anchor != null)
            {
                if (!CaseStage.TryGetAnchor(anchor, out var p))
                {
                    target = default;
                    return false;
                }

                target = new StagePoint(p.X, p.Y + AnchorLift, p.Z);
                return true;
            }

            var found = CaseStage.Find(point);
            if (found == null)
            {
                target = default;
                return false;
            }

            var at = found.Position;
            target = new StagePoint(at.X, at.Y + found.MarkerHeight, at.Z);
            return true;
        }

        /// <summary>
        /// The character who turns to look at a tier-1 target: the one nearest Pip (plan distance) among those
        /// available and on screen, within <see cref="MaxLookDistance"/>. −1 when nobody qualifies (nobody looks;
        /// the sparkle alone still shows the target).
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

        /// <summary>
        /// The action the tier-3 demonstration shows, from Pip to the target: Use once the target is in reach, the
        /// high jump when it is just above Pip, otherwise a push toward it.
        /// </summary>
        public static HintDemo DemoFor(float pipX, float pipY, float pipZ, StagePoint target, bool useInRange)
        {
            if (useInRange)
            {
                return HintDemo.Use;
            }

            float dx = target.X - pipX;
            float dz = target.Z - pipZ;
            float plan = (float)Math.Sqrt(dx * dx + dz * dz);
            float rise = target.Y - AnchorLift - pipY;
            if (plan <= UseReach && rise < HighJumpRise)
            {
                return HintDemo.Use;
            }

            return rise >= HighJumpRise && plan <= HighJumpReach ? HintDemo.HoldJump : HintDemo.Move;
        }

        /// <summary>The demo's control prompt.</summary>
        public static ControlPrompt PromptFor(HintDemo demo)
        {
            switch (demo)
            {
                case HintDemo.Use:
                    return ControlPrompt.Use;
                case HintDemo.HoldJump:
                    return ControlPrompt.HoldJump;
                default:
                    return ControlPrompt.Move;
            }
        }
    }
}
