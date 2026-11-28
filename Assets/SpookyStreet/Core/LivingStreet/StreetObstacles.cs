using System;
using System.Collections.Generic;
using SpookyStreet.Core.World;

namespace SpookyStreet.Core.LivingStreet
{
    /// <summary>
    /// Everything a walker must never walk into on the slice's street, as plan boxes: the hedges (the heart's boundary
    /// and garden hedges, the lane's rows), the houses (the heart's and the lane's footprints), the lane's lamp posts,
    /// pumpkins, the lantern table and the crate, and the heart's solid street furniture; plus the invisible walls
    /// (<see cref="CollisionRules.InsideWalls"/>). Walker paths are planned to keep <see cref="WalkerClearance"/> from all
    /// of it, and a step aside for Pip never takes a walker closer (owner, 2026-11-28: no walker heads into a hedge,
    /// bumps it and turns away).
    /// </summary>
    public static class StreetObstacles
    {
        /// <summary>A walker's body radius plus a little air: paths and side-steps keep this from every obstacle.</summary>
        public const float WalkerClearance = 0.3f;

        /// <summary>One obstacle: an axis-aligned plan box (a footprint already turned by its yaw).</summary>
        public readonly struct Box
        {
            public Box(string name, float x, float z, float halfX, float halfZ)
            {
                Name = name;
                X = x;
                Z = z;
                HalfX = halfX;
                HalfZ = halfZ;
            }

            public string Name { get; }
            public float X { get; }
            public float Z { get; }
            public float HalfX { get; }
            public float HalfZ { get; }

            /// <summary>Distance from (x, z) to the box (0 inside).</summary>
            public float Distance(float x, float z)
            {
                float dx = Math.Max(0f, Math.Abs(x - X) - HalfX);
                float dz = Math.Max(0f, Math.Abs(z - Z) - HalfZ);
                return (float)Math.Sqrt(dx * dx + dz * dz);
            }

            public override string ToString() => Name + " at (" + X.ToString("F1") + ", " + Z.ToString("F1") + ")";
        }

        /// <summary>The obstacles walkers could reach (the heart's south half and the east lane).</summary>
        public static readonly IReadOnlyList<Box> All = Build();

        /// <summary>
        /// Distance from (x, z) to the nearest obstacle or wall (0 inside one), and its name. Doors' own steps are part of
        /// their houses; only the door neighbours walk there.
        /// </summary>
        public static float Clearance(float x, float z, out string nearest)
        {
            nearest = null;
            float best = float.MaxValue;
            foreach (var box in All)
            {
                float d = box.Distance(x, z);
                if (d < best)
                {
                    best = d;
                    nearest = box.Name;
                }
            }

            // The invisible walls: how far inside them (searched in 5 cm steps up to a metre).
            if (!CollisionRules.InsideWalls(x, z, 0f))
            {
                nearest = "the walls";
                return 0f;
            }

            for (float m = 0.05f; m <= 1f; m += 0.05f)
            {
                if (!CollisionRules.InsideWalls(x, z, m))
                {
                    if (m - 0.05f < best)
                    {
                        best = m - 0.05f;
                        nearest = "the walls";
                    }

                    break;
                }
            }

            return best;
        }

        public static float Clearance(float x, float z) => Clearance(x, z, out _);

        private static IReadOnlyList<Box> Build()
        {
            var list = new List<Box>();
            void Add(Placement p)
            {
                if (p.HalfX > 0f && p.HalfZ > 0f)
                {
                    list.Add(new Box(p.Model, p.X, p.Z, p.HalfX, p.HalfZ));
                }
            }

            foreach (var p in CollisionRules.PlacedModels())
            {
                bool hedge = p.Model == EnvironmentLayout.BoundaryHedgeLong || p.Model == EnvironmentLayout.Hedge;
                if (hedge || CollisionRules.IsSolid(p))
                {
                    Add(p);
                }
            }

            foreach (var p in LivingStreetLayout.Hedges)
            {
                Add(p);
            }

            foreach (var p in LivingStreetLayout.Props)
            {
                Add(p);
            }

            foreach (var door in LivingStreetLayout.Doors)
            {
                // The house art's footprint (its steps stand inside it; the treat spot is just past them).
                LaneHouseArt.Footprint(door.Id, out float w, out float d);
                EnvironmentLayout.Footprint(w, d, door.Yaw, out float hx, out float hz);
                list.Add(new Box("house " + door.Id, door.X, door.Z, hx, hz));
            }

            foreach (var post in LivingStreetLayout.StringLightPosts)
            {
                list.Add(new Box("string-light post", post.X, post.Z, 0.1f, 0.1f));
            }

            return list;
        }
    }
}
