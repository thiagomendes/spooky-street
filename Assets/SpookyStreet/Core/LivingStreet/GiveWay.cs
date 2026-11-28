using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.LivingStreet
{
    /// <summary>
    /// How walkers make room (owner, 2026-11-28: walkers are solid, Pip never walks through them, and she is never
    /// trapped). Walkers have soft, solid bodies (a capsule each in the game, <see cref="BodyRadius"/>), and they give
    /// way before Pip has to push:
    /// <list type="bullet">
    /// <item>A walker standing still (at a door, in a queue, admiring a porch) keeps <see cref="Room"/> between itself and
    /// Pip: it steps back when she comes close.</item>
    /// <item>When Pip walks toward walkers, the ones in front of her step out of a corridor <see cref="Corridor"/> wide
    /// on either side of her way, so a family parts and lets her through within a moment.</item>
    /// <item>No two walkers stand closer than <see cref="MinGap"/> (bodies never overlap).</item>
    /// <item>No step aside takes a walker nearer an obstacle than <see cref="StreetObstacles.WalkerClearance"/>
    /// (or nearer than it already was).</item>
    /// </list>
    /// Walking groups also slow and step aside as a group (<see cref="CrowdClock"/>). Offsets move at a walking pace
    /// (<see cref="Speed"/>), so a step aside is a step, never a jump. Engine-free and frame-rate independent.
    /// </summary>
    public static class GiveWay
    {
        /// <summary>Pip's capsule radius (the CharacterController's).</summary>
        public const float PipRadius = 0.28f;

        /// <summary>A walker's capsule radius: a child's and a grown-up's.</summary>
        public const float ChildRadius = 0.2f;

        public const float GrownUpRadius = 0.24f;

        public static float BodyRadius(bool grownUp) => grownUp ? GrownUpRadius : ChildRadius;

        /// <summary>A still walker keeps this from Pip's centre.</summary>
        public const float Room = 1.0f;

        /// <summary>Half-width of the way a walking Pip is given, and how far ahead of her it reaches.</summary>
        public const float Corridor = 0.8f;

        public const float CorridorAhead = 2.2f;

        /// <summary>No two walkers' centres closer than this.</summary>
        public const float MinGap = 0.55f;

        /// <summary>How fast a walker steps aside (m/s): a brisk step.</summary>
        public const float Speed = 1.3f;

        /// <summary>One walker's input: where its routine puts it, and whether Pip's room applies (it is standing).</summary>
        public struct Walker
        {
            public float X;
            public float Z;
            public bool Still;
            public bool GrownUp;
        }

        /// <summary>
        /// Moves each walker's offset (<paramref name="offsetX"/>, <paramref name="offsetZ"/>) toward where it should step
        /// to make room, at <see cref="Speed"/>. Pip at (<paramref name="pipX"/>, <paramref name="pipZ"/>) walking along
        /// (<paramref name="headX"/>, <paramref name="headZ"/>) when <paramref name="pipWalking"/>; <paramref name="pipNear"/>
        /// false (she is indoors) leaves only the walkers' own spacing. <paramref name="clearance"/> gives an obstacle
        /// clearance at a point (null: open ground).
        /// </summary>
        public static void Solve(IReadOnlyList<Walker> walkers, float[] offsetX, float[] offsetZ, float pipX, float pipZ,
            float headX, float headZ, bool pipWalking, bool pipNear, float dt, Func<float, float, float> clearance = null)
        {
            int n = walkers.Count;
            if (n == 0 || !(dt > 0f))
            {
                return;
            }

            var tx = new float[n];
            var tz = new float[n];
            float hl = (float)Math.Sqrt(headX * headX + headZ * headZ);
            bool walking = pipWalking && hl > 1e-3f;
            float hx = walking ? headX / hl : 0f;
            float hz = walking ? headZ / hl : 0f;
            for (int i = 0; i < n; i++)
            {
                var w = walkers[i];
                float bx = w.X, bz = w.Z;
                float ox = 0f, oz = 0f;
                if (pipNear)
                {
                    float dx = bx - pipX, dz = bz - pipZ;
                    float d = (float)Math.Sqrt(dx * dx + dz * dz);
                    float room = w.Still ? Room : PipRadius + BodyRadius(w.GrownUp) + 0.12f;
                    if (d < room)
                    {
                        float gap = room - d;
                        float ux = d < 1e-3f ? ((i & 1) == 0 ? 1f : -1f) : dx / d;
                        float uz = d < 1e-3f ? 0f : dz / d;
                        ox += ux * gap;
                        oz += uz * gap;
                    }

                    if (walking)
                    {
                        // In front of her and inside her way: step out of it to the nearer side.
                        float ahead = dx * hx + dz * hz;
                        float across = dx * hz - dz * hx; // + to her right
                        if (ahead > -0.2f && ahead < CorridorAhead && Math.Abs(across) < Corridor)
                        {
                            float side = across >= 0f ? 1f : -1f;
                            if (Math.Abs(across) < 0.05f)
                            {
                                side = (i & 1) == 0 ? 1f : -1f;
                            }

                            float push = side * Corridor - across;
                            ox += hz * push;
                            oz += -hx * push;
                        }
                    }
                }

                tx[i] = ox;
                tz[i] = oz;
            }

            // Spacing: push pairs apart (on where they are heading to).
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < n; i++)
                {
                    for (int k = i + 1; k < n; k++)
                    {
                        float ax = walkers[i].X + tx[i], az = walkers[i].Z + tz[i];
                        float bx = walkers[k].X + tx[k], bz = walkers[k].Z + tz[k];
                        float dx = bx - ax, dz = bz - az;
                        float d = (float)Math.Sqrt(dx * dx + dz * dz);
                        if (d >= MinGap)
                        {
                            continue;
                        }

                        float half = (MinGap - d) * 0.5f;
                        float ux = d < 1e-4f ? 1f : dx / d;
                        float uz = d < 1e-4f ? 0f : dz / d;
                        tx[i] -= ux * half;
                        tz[i] -= uz * half;
                        tx[k] += ux * half;
                        tz[k] += uz * half;
                    }
                }
            }

            float stepMax = Speed * dt;
            for (int i = 0; i < n; i++)
            {
                var w = walkers[i];
                float goalX = tx[i], goalZ = tz[i];
                if (clearance != null && (goalX != 0f || goalZ != 0f))
                {
                    // Never toward an obstacle: shorten the step until it keeps its clearance (or loses none).
                    float here = clearance(w.X, w.Z);
                    for (int tries = 0; tries < 4; tries++)
                    {
                        float c = clearance(w.X + goalX, w.Z + goalZ);
                        if (c >= StreetObstacles.WalkerClearance || c >= here - 1e-3f)
                        {
                            break;
                        }

                        goalX *= 0.5f;
                        goalZ *= 0.5f;
                    }
                }

                float ex = goalX - offsetX[i], ez = goalZ - offsetZ[i];
                float e = (float)Math.Sqrt(ex * ex + ez * ez);
                if (e <= stepMax)
                {
                    offsetX[i] = goalX;
                    offsetZ[i] = goalZ;
                }
                else
                {
                    offsetX[i] += ex / e * stepMax;
                    offsetZ[i] += ez / e * stepMax;
                }
            }
        }
    }
}
