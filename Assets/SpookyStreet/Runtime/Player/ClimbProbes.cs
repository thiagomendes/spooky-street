using SpookyStreet.Core.Movement;
using SpookyStreet.World;
using UnityEngine;

namespace SpookyStreet.Player
{
    /// <summary>
    /// Measures the marked walls and ledges around Pip for the engine-free <see cref="ClimbRules"/> (S003). Only
    /// colliders carrying a <see cref="ClimbSurface"/> are considered (a handful, listed by
    /// <see cref="ClimbSurface.Active"/>, so no overlap query and no allocation per frame); the rules decide what the
    /// measurements mean. The room checks are capsule tests against everything solid except Pip.
    /// </summary>
    public static class ClimbProbes
    {
        /// <summary>Height above the feet where a wall must stand to be slid on (Pip's chest).</summary>
        public const float ChestHeight = 0.55f;

        /// <summary>The nearest marked wall whose face spans Pip's chest within the contact reach.</summary>
        public static WallProbe Wall(Vector3 feet, MovementTuningData tuning, float feetClearance)
        {
            var probe = new WallProbe { FeetClearance = feetClearance };
            Vector3 chest = feet + Vector3.up * ChestHeight;
            float reach = tuning.BodyRadius + tuning.WallContactSlack + 0.05f;
            float best = float.MaxValue;
            var surfaces = ClimbSurface.Active;
            for (int i = 0; i < surfaces.Count; i++)
            {
                var surface = surfaces[i];
                var collider = surface != null ? surface.SurfaceCollider : null;
                if (collider == null || !collider.enabled || (surface.Marks & SurfaceMarks.WallJump) == 0 ||
                    collider.bounds.SqrDistance(chest) > reach * reach)
                {
                    continue;
                }

                Vector3 closest = collider.ClosestPoint(chest);
                Vector3 offset = chest - closest;
                if (Mathf.Abs(offset.y) > 0.02f)
                {
                    continue; // the face ends below (or starts above) Pip's chest
                }

                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance < 1e-4f || distance > reach || distance >= best)
                {
                    continue;
                }

                Vector3 normal = offset / distance;
                if (!surface.Accepts(SurfaceMarks.WallJump, normal))
                {
                    continue;
                }

                best = distance;
                probe.Hit = true;
                probe.Marks = surface.Marks;
                probe.NormalX = normal.x;
                probe.NormalZ = normal.z;
                probe.Distance = distance;
            }

            return probe;
        }

        /// <summary>
        /// The nearest marked ledge whose face is within the catch reach, with its flat top, the room to hang below it
        /// and to stand on it, and the air under the hang spot. <paramref name="height"/> and <paramref name="radius"/>
        /// are Pip's capsule; <paramref name="mask"/> the layers that block her.
        /// </summary>
        public static LedgeProbe Ledge(Vector3 feet, MovementTuningData tuning, int mask, float height, float radius)
        {
            var probe = new LedgeProbe();
            float lowestTop = tuning.LedgeHangHandHeight - tuning.LedgeGrabReachBelow;
            float highestTop = tuning.LedgeHangHandHeight + tuning.LedgeGrabReachAbove;
            Vector3 sample = feet + Vector3.up * (lowestTop - 0.05f);
            float reach = tuning.BodyRadius + tuning.LedgeGrabReachForward;
            float best = float.MaxValue;
            var surfaces = ClimbSurface.Active;
            for (int i = 0; i < surfaces.Count; i++)
            {
                var surface = surfaces[i];
                var collider = surface != null ? surface.SurfaceCollider : null;
                if (collider == null || !collider.enabled || (surface.Marks & SurfaceMarks.Ledge) == 0)
                {
                    continue;
                }

                Bounds bounds = collider.bounds;
                if (bounds.max.y < feet.y + lowestTop - 0.1f || bounds.max.y > feet.y + highestTop + 0.1f)
                {
                    continue; // its top cannot be in the catch window
                }

                Vector3 closest = collider.ClosestPoint(sample);
                Vector3 offset = sample - closest;
                offset.y = 0f;
                float distance = offset.magnitude;
                if (distance < 1e-4f || distance > reach || distance >= best)
                {
                    continue; // inside the ledge's footprint, out of reach, or a nearer one was found
                }

                Vector3 normal = offset / distance;
                if (!surface.Accepts(SurfaceMarks.Ledge, normal))
                {
                    continue;
                }

                // The flat top just inside the edge.
                var edge = new Vector3(closest.x, feet.y, closest.z);
                float from = highestTop + 0.6f;
                var down = new Ray(edge - normal * 0.06f + Vector3.up * from, Vector3.down);
                if (!collider.Raycast(down, out RaycastHit top, from + 0.3f) || top.normal.y < 0.9f)
                {
                    continue;
                }

                float topY = top.point.y;
                Vector3 hangFeet = edge + normal * tuning.LedgeHangWallOffset;
                hangFeet.y = topY - tuning.LedgeHangHandHeight;
                Vector3 climbFeet = edge - normal * tuning.LedgeClimbInset;
                climbFeet.y = topY + tuning.LedgeClimbLift;

                best = distance;
                probe.Hit = true;
                probe.Marks = surface.Marks;
                probe.NormalX = normal.x;
                probe.NormalZ = normal.z;
                probe.EdgeDistance = distance;
                probe.TopAbove = topY - feet.y;
                probe.RoomToHang = Free(hangFeet, height, radius, mask);
                probe.RoomToClimb = Free(climbFeet, height, radius, mask);
                probe.HangClearance = Physics.Raycast(hangFeet + Vector3.up * 0.05f, Vector3.down, out RaycastHit ground,
                    5f, mask, QueryTriggerInteraction.Ignore)
                    ? ground.distance - 0.05f
                    : 100f;
            }

            return probe;
        }

        /// <summary>Pip's capsule (a little slimmer, lifted off the floor by the skin) overlaps nothing at these feet.</summary>
        public static bool Free(Vector3 feet, float height, float radius, int mask)
        {
            float r = Mathf.Max(0.05f, radius - 0.03f);
            Vector3 bottom = feet + Vector3.up * (r + 0.04f);
            Vector3 top = feet + Vector3.up * Mathf.Max(r + 0.05f, height - r);
            return !Physics.CheckCapsule(bottom, top, r, mask, QueryTriggerInteraction.Ignore);
        }
    }
}
