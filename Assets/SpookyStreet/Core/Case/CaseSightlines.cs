using System;
using System.Collections.Generic;
using Verbs = SpookyStreet.Core.Localization.LocalizationKeys.Verbs;

namespace SpookyStreet.Core.Case
{
    /// <summary>Named viewpoints and targets of the deduction (spec §8 CASE-03). Scenes place a marker per name.</summary>
    public enum SightPoint
    {
        /// <summary>Eye height of a child seated on Mr. Vale's bench.</summary>
        BenchSeat,

        /// <summary>Eye height of Pip standing on the treehouse deck.</summary>
        TreehouseDeck,

        /// <summary>Bea's eye beside her bicycle.</summary>
        BeaCorner,

        FrontDoor,

        /// <summary>Cape height on June's side route (where Bea saw the silver cape).</summary>
        SideRouteCape,

        /// <summary>Foot height on the same spot (what the hedge hid from Bea).</summary>
        SideRouteFeet,
    }

    /// <summary>One expected sightline: from a viewpoint, the target is (or is not) visible.</summary>
    public readonly struct SightExpectation
    {
        public SightExpectation(SightPoint from, SightPoint to, bool visible)
        {
            From = from;
            To = to;
            Visible = visible;
        }

        public SightPoint From { get; }

        public SightPoint To { get; }

        public bool Visible { get; }

        public override string ToString() => From + (Visible ? " sees " : " cannot see ") + To;
    }

    /// <summary>An axis-aligned solid that blocks sight (a hedge, the house body), in metres.</summary>
    public readonly struct SightOccluder
    {
        public SightOccluder(float minX, float minY, float minZ, float maxX, float maxY, float maxZ)
        {
            MinX = Math.Min(minX, maxX);
            MinY = Math.Min(minY, maxY);
            MinZ = Math.Min(minZ, maxZ);
            MaxX = Math.Max(minX, maxX);
            MaxY = Math.Max(minY, maxY);
            MaxZ = Math.Max(minZ, maxZ);
        }

        public float MinX { get; }
        public float MinY { get; }
        public float MinZ { get; }
        public float MaxX { get; }
        public float MaxY { get; }
        public float MaxZ { get; }
    }

    /// <summary>
    /// CASE-03 as data: the bench sees the door but not the side route; the treehouse sees both; Bea saw the cape
    /// over the hedge but not June's feet. The world is built from code (D5), so a layout test can check its own
    /// marker positions and occluders against <see cref="Expectations"/> with <see cref="IsVisible"/>; a PlayMode
    /// test can do the same with physics linecasts.
    /// </summary>
    public static class CaseSightlines
    {
        public static readonly IReadOnlyList<SightExpectation> Expectations = new[]
        {
            new SightExpectation(SightPoint.BenchSeat, SightPoint.FrontDoor, true),
            new SightExpectation(SightPoint.BenchSeat, SightPoint.SideRouteCape, false),
            new SightExpectation(SightPoint.TreehouseDeck, SightPoint.FrontDoor, true),
            new SightExpectation(SightPoint.TreehouseDeck, SightPoint.SideRouteCape, true),
            new SightExpectation(SightPoint.BeaCorner, SightPoint.SideRouteCape, true),
            new SightExpectation(SightPoint.BeaCorner, SightPoint.SideRouteFeet, false),
        };

        /// <summary>True when the segment between the two points crosses no occluder (slab test).</summary>
        public static bool IsVisible(float ax, float ay, float az, float bx, float by, float bz, IEnumerable<SightOccluder> occluders)
        {
            foreach (var box in occluders)
            {
                if (SegmentHitsBox(ax, ay, az, bx, by, bz, box))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool SegmentHitsBox(float ax, float ay, float az, float bx, float by, float bz, SightOccluder box)
        {
            double t0 = 0, t1 = 1;
            return Slab(ax, bx - ax, box.MinX, box.MaxX, ref t0, ref t1)
                && Slab(ay, by - ay, box.MinY, box.MaxY, ref t0, ref t1)
                && Slab(az, bz - az, box.MinZ, box.MaxZ, ref t0, ref t1);
        }

        private static bool Slab(double origin, double delta, double min, double max, ref double t0, ref double t1)
        {
            if (Math.Abs(delta) < 1e-9)
            {
                return origin >= min && origin <= max;
            }

            double a = (min - origin) / delta;
            double b = (max - origin) / delta;
            if (a > b)
            {
                double swap = a;
                a = b;
                b = swap;
            }

            t0 = Math.Max(t0, a);
            t1 = Math.Min(t1, b);
            return t0 <= t1;
        }
    }

    /// <summary>Use-button verbs per interaction (spec §5: icon + localized verb).</summary>
    public static class CaseInteractions
    {
        /// <summary>The verb key for an interaction, or null for trigger-only kinds (Enter/Hear/Arrive).</summary>
        public static string VerbKey(CaseActionKind kind)
        {
            switch (kind)
            {
                case CaseActionKind.Talk:
                    return Verbs.Talk;
                case CaseActionKind.SitOnBench:
                    return Verbs.Sit;
                case CaseActionKind.ExamineStreamer:
                case CaseActionKind.ExamineCloth:
                case CaseActionKind.ExamineSill:
                case CaseActionKind.ExamineGrille:
                case CaseActionKind.ExamineCandyBowl:
                    return Verbs.Look;
                case CaseActionKind.TryFrontDoor:
                    return Verbs.Open;
                case CaseActionKind.LiftLatch:
                    return Verbs.Lift;
                case CaseActionKind.LinkArm:
                    return Verbs.Connect;
                case CaseActionKind.TurnCrank:
                    return Verbs.Turn;
                default:
                    return null;
            }
        }

        public static bool IsTrigger(CaseActionKind kind) => VerbKey(kind) == null;
    }
}
