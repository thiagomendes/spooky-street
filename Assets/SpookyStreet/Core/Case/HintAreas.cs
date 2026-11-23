using System;
using System.Collections.Generic;
using SpookyStreet.Core.World;

namespace SpookyStreet.Core.Case
{
    /// <summary>
    /// One authored hint area (D26, docs/mystery-first.md §6): a place a hint leads to, never the object in it. The
    /// centre is kept clear of every Use target (<see cref="HintAreas.MinClearance"/>) and the radius is wide enough
    /// to hold the place, so the tier-1 glow and the tier-2 bats cover the area without picking out a thing.
    /// </summary>
    public sealed class HintArea
    {
        internal HintArea(string id, StagePoint centre, float radius)
        {
            Id = id;
            Centre = centre;
            Radius = radius;
        }

        /// <summary>Stable name (logs and tests only; never saved).</summary>
        public string Id { get; }

        /// <summary>The area's floor point (the glow lies here; the bats circle above it).</summary>
        public StagePoint Centre { get; }

        /// <summary>Plan radius of the area (metres).</summary>
        public float Radius { get; }

        /// <summary>True for an area in the separate interior (D24.1).</summary>
        public bool Inside => CaseRouteLayout.Inside(Centre.X, Centre.Z);

        public override string ToString() => Id + " " + Centre;
    }

    /// <summary>
    /// The hint area table of case 01 (D26 §6): which area each objective's hints point to, and the threshold areas a
    /// hint shows instead when its area is on the other side of the owl house walls (D24.1: June's window before the
    /// door opens, the front door after). Areas follow the rewritten sensory hint lines ("A breeze keeps tugging
    /// toward the side yard."). Pure data so Core tests prove every area stays clear of the clue objects.
    /// </summary>
    public static class HintAreas
    {
        public const string FrontYard = "front_yard";
        public const string BenchCorner = "bench_corner";
        public const string SideYard = "side_yard";
        public const string Attic = "attic";
        public const string Hall = "hall";
        public const string Workshop = "workshop";
        public const string ArrivalPorch = "arrival_porch";
        public const string WindowOutside = "june_window_outside";
        public const string WindowInside = "june_window_inside";
        public const string DoorOutside = "front_door_outside";
        public const string DoorInside = "front_door_inside";

        /// <summary>No area centre is closer than this (3D metres) to any Use target of the case.</summary>
        public const float MinClearance = 1.5f;

        /// <summary>Every area is at least this wide (plan radius), so it holds a place, not a point.</summary>
        public const float MinRadius = 2f;

        private const float Ifz = CaseRouteLayout.InteriorFrontZ;

        public static readonly IReadOnlyList<HintArea> All = new[]
        {
            // The blue house's front yard, between the gate and the porch steps (the knocks' house).
            new HintArea(FrontYard, new StagePoint(0f, 0.1f, 6.7f), 2.8f),
            // The plaza's east corner in front of Mr. Vale's bench (the bench and Vale stand at its edge).
            new HintArea(BenchCorner, new StagePoint(5.4f, 0.1f, 1.6f), 2.6f),
            // The side yard behind the hedge, at the foot of the shed ramp.
            new HintArea(SideYard, new StagePoint(-11.2f, 0.1f, 5.6f), 3f),
            // The attic loft, in front of June's stool and the stair top.
            new HintArea(Attic, new StagePoint(0.6f, CaseRouteLayout.LoftY, Ifz + 2.0f), 2.2f),
            // The hall by the front door.
            new HintArea(Hall, new StagePoint(0.7f, CaseRouteLayout.FloorY, Ifz + 2.6f), 2f),
            // The hall's east side, in front of the workshop partition (grille, panel, arm and crank at its edge).
            new HintArea(Workshop, new StagePoint(0.2f, CaseRouteLayout.FloorY, CaseRouteLayout.MechanismZ), 2.2f),
            // The arrival porch and the path just north of it.
            new HintArea(ArrivalPorch, new StagePoint(0.4f, 0.1f, -11.4f), 2.4f),
            // Thresholds (D24.1): the bridge's end before June's window, the alcove inside it, the path below the porch and
            // the hall inside the front door.
            new HintArea(WindowOutside, new StagePoint(-4.2f, CaseRouteLayout.ExteriorSillY, CaseRouteLayout.ExteriorWindowZ - 0.9f), 2f),
            new HintArea(WindowInside, new StagePoint(-2.1f, CaseRouteLayout.LoftY, Ifz + 0.9f), 2f),
            new HintArea(DoorOutside, new StagePoint(0f, 0.15f, CaseRouteLayout.FrontZ - 3.4f), 2f),
            new HintArea(DoorInside, new StagePoint(0.7f, CaseRouteLayout.FloorY, Ifz + 2.6f), 2f),
        };

        public static HintArea Get(string id)
        {
            foreach (var area in All)
            {
                if (area.Id == id)
                {
                    return area;
                }
            }

            return null;
        }

        /// <summary>The area an objective's hints point to, wherever Pip is (null in free roam).</summary>
        public static HintArea For(CaseObjective objective)
        {
            switch (objective)
            {
                case CaseObjective.Arrival:
                    return Get(FrontYard);
                case CaseObjective.CheckView:
                    return Get(BenchCorner);
                case CaseObjective.FindWayIn:
                    return Get(SideYard);
                case CaseObjective.MeetJune:
                    return Get(Attic);
                case CaseObjective.OpenDoor:
                    return Get(Hall);
                case CaseObjective.FindSource:
                case CaseObjective.LinkArm:
                case CaseObjective.TurnCrank:
                    return Get(Workshop);
                case CaseObjective.ReturnToPorch:
                    return Get(ArrivalPorch);
                default:
                    return null;
            }
        }

        /// <summary>
        /// The area as seen from where Pip is (D24.1): an area on the other side of the house walls gives way to the
        /// threshold area on Pip's side (June's window while the door is latched, the front door once it is open),
        /// because a glow behind a wall cannot be seen. Null in free roam.
        /// </summary>
        public static HintArea For(CaseObjective objective, float pipX, float pipZ, bool doorOpen)
        {
            var area = For(objective);
            if (area == null)
            {
                return null;
            }

            var via = Thresholds.WayTo(CaseRouteLayout.Inside(pipX, pipZ), area.Inside, doorOpen);
            if (via == null)
            {
                return area;
            }

            return via.Id == ThresholdId.FrontDoor
                ? Get(via.Inside ? DoorInside : DoorOutside)
                : Get(via.Inside ? WindowInside : WindowOutside);
        }

        /// <summary>3D distance from an area's centre to the nearest Use target of the case (tests and the doc).</summary>
        public static float Clearance(HintArea area, out string nearest)
        {
            if (area == null)
            {
                throw new ArgumentNullException(nameof(area));
            }

            float best = float.MaxValue;
            nearest = null;
            foreach (var point in CaseStage.AllPoints)
            {
                float dx = point.Position.X - area.Centre.X;
                float dy = point.Position.Y - area.Centre.Y;
                float dz = point.Position.Z - area.Centre.Z;
                float d = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                if (d < best)
                {
                    best = d;
                    nearest = point.Id;
                }
            }

            return best;
        }
    }
}
