using System;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.World;

namespace SpookyStreet.Core.LivingStreet
{
    /// <summary>
    /// The living street's coherence rules that join it to the case (living-street §3.4, §6; D34): when groups may
    /// ring Pip's bell, which cap holds in the heart, and the clearances every walker and free-world target keeps.
    /// </summary>
    public static class LivingStreetRules
    {
        /// <summary>No walker, door or free-world target closer than this to a case Use or examine target (§3.4 rule 3).</summary>
        public const float ClueClearance = 1.5f;

        /// <summary>No walker closer than this to the bench-to-door sightline (Vale's view, CASE-03).</summary>
        public const float SightlineClearance = 1.0f;

        /// <summary>A stopped walker's gaze reaches this far; it never crosses a hint area (§3.4 rule 2).</summary>
        public const float GlanceReach = 12f;

        /// <summary>A walking heading inside the heart may not lead into a hint area within this distance.</summary>
        public const float WalkGlanceReach = 6f;

        /// <summary>
        /// Groups may visit Pip's porch once her lantern hangs lit, except while the radio's hint area is her porch
        /// (the case's last step): a crowd at her steps then would look like a pointer (§3.4 rule 2).
        /// </summary>
        public static bool PorchOpen(bool lanternLit, string activeHintArea) =>
            lanternLit && !string.Equals(activeHintArea, HintAreas.ArrivalPorch, StringComparison.Ordinal);

        /// <summary>Case 01 is open (its stamp not yet earned): the heart's cap is 3 (D34).</summary>
        public static bool CaseOpen(CaseState state) => state == null || !state.Has(CaseFact.CaseStamped);

        /// <summary>Distance from (x, z) to the nearest exterior case target (plan metres), and which.</summary>
        public static float ClueDistance(float x, float z, out string nearest)
        {
            float best = float.MaxValue;
            nearest = null;
            foreach (var point in CaseStage.AllPoints)
            {
                if (CaseRouteLayout.Inside(point.Position.X, point.Position.Z))
                {
                    continue;
                }

                float dx = point.Position.X - x;
                float dz = point.Position.Z - z;
                float d = (float)Math.Sqrt(dx * dx + dz * dz);
                if (d < best)
                {
                    best = d;
                    nearest = point.Id;
                }
            }

            return best;
        }

        /// <summary>Distance from (x, z) to the segment from the bench seat to the owl house's front door.</summary>
        public static float SightlineDistance(float x, float z)
        {
            var bench = CaseStage.Find(CaseStage.PointBench).Position;
            var door = CaseStage.Find(CaseStage.PointFrontDoor).Position;
            return SegmentDistance(x, z, bench.X, bench.Z, door.X, door.Z);
        }

        /// <summary>True when a gaze from (x, z) along Unity yaw <paramref name="yaw"/> crosses an outdoor hint area within <paramref name="reach"/>.</summary>
        public static bool GazeMeetsHintArea(float x, float z, float yaw, float reach, out string area)
        {
            double r = yaw * Math.PI / 180.0;
            float hx = (float)Math.Sin(r);
            float hz = (float)Math.Cos(r);
            foreach (var hint in HintAreas.All)
            {
                if (hint.Inside)
                {
                    continue;
                }

                float d = SegmentDistance(hint.Centre.X, hint.Centre.Z, x, z, x + hx * reach, z + hz * reach);
                if (d < hint.Radius)
                {
                    area = hint.Id;
                    return true;
                }
            }

            area = null;
            return false;
        }

        public static float SegmentDistance(float px, float pz, float ax, float az, float bx, float bz)
        {
            float vx = bx - ax;
            float vz = bz - az;
            float l2 = vx * vx + vz * vz;
            float t = l2 > 1e-8f ? ((px - ax) * vx + (pz - az) * vz) / l2 : 0f;
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            float dx = ax + vx * t - px;
            float dz = az + vz * t - pz;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
