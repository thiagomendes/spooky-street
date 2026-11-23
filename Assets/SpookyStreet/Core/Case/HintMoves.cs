using System;
using System.Collections.Generic;
using SpookyStreet.Core.World;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.Hints;

namespace SpookyStreet.Core.Case
{
    /// <summary>A move the tier-3 hint can demonstrate (D26 §6: moves only, never Use on a clue).</summary>
    public enum HintMove
    {
        /// <summary>No move blocks Pip here: the radio stops at tier 2 (two pips).</summary>
        None,
        Jump,
        HighJump,
        Glide,
        WallKick,
        Ledge,
    }

    /// <summary>One authored spot where a move is the way on: Pip's feet inside it, the move, and where it leads.</summary>
    public sealed class HintMoveSpot
    {
        internal HintMoveSpot(string id, HintMove move, StagePoint centre, float radius, float minY, float maxY, StagePoint toward)
        {
            Id = id;
            Move = move;
            Centre = centre;
            Radius = radius;
            MinY = minY;
            MaxY = maxY;
            Toward = toward;
        }

        public string Id { get; }

        public HintMove Move { get; }

        public StagePoint Centre { get; }

        public float Radius { get; }

        public float MinY { get; }

        public float MaxY { get; }

        /// <summary>Where the move takes Pip (the demo pushes the stick toward it).</summary>
        public StagePoint Toward { get; }

        public bool Contains(float x, float y, float z)
        {
            float dx = x - Centre.X;
            float dz = z - Centre.Z;
            return dx * dx + dz * dz <= Radius * Radius && y >= MinY && y <= MaxY;
        }
    }

    /// <summary>
    /// Tier 3 (D26 §6, owner answer Q5a): the radio's third tier only demonstrates a move, and only where a move is
    /// the blocker (case 01: the skilled ascent, crate → awning → shed roof, while the hint leads up to June's
    /// window). Everywhere else there is no tier 3 and the radio stops at two pips. Never Use, Talk or a deduction.
    /// </summary>
    public static class HintMoves
    {
        public static readonly IReadOnlyList<HintMoveSpot> Spots = BuildSpots();

        /// <summary>Every move the tier-3 demo can show (tests: nothing else).</summary>
        public static readonly IReadOnlyList<HintMove> Demonstrable = new[]
        {
            HintMove.Jump, HintMove.HighJump, HintMove.Glide, HintMove.WallKick, HintMove.Ledge,
        };

        /// <summary>Objectives whose way on can be a move: the climb up to June's window (outside only).</summary>
        public static bool ClimbsUp(CaseObjective objective) =>
            objective == CaseObjective.FindWayIn || objective == CaseObjective.MeetJune;

        /// <summary>The move to demonstrate for <paramref name="objective"/> with Pip at (x, y, z); None when no move blocks.</summary>
        public static HintMove For(CaseObjective objective, float x, float y, float z) => SpotFor(objective, x, y, z)?.Move ?? HintMove.None;

        /// <summary>The spot Pip stands in for <paramref name="objective"/>, or null.</summary>
        public static HintMoveSpot SpotFor(CaseObjective objective, float x, float y, float z)
        {
            if (!ClimbsUp(objective) || CaseRouteLayout.Inside(x, z))
            {
                return null;
            }

            foreach (var spot in Spots)
            {
                if (spot.Contains(x, y, z))
                {
                    return spot;
                }
            }

            return null;
        }

        /// <summary>The tier-3 line for a move ("Tap Jump to hop up!"); null for <see cref="HintMove.None"/>.</summary>
        public static string LineKey(HintMove move)
        {
            switch (move)
            {
                case HintMove.Jump:
                    return Keys.MoveJump;
                case HintMove.HighJump:
                    return Keys.MoveHighJump;
                case HintMove.Glide:
                    return Keys.MoveGlide;
                case HintMove.WallKick:
                    return Keys.MoveWallKick;
                case HintMove.Ledge:
                    return Keys.MoveLedge;
                default:
                    return null;
            }
        }

        private static HintMoveSpot[] BuildSpots()
        {
            var a = CaseRouteLayout.SkilledAscent;
            if (a.Length < 5)
            {
                throw new InvalidOperationException("the skilled ascent changed shape");
            }

            StagePoint P(RoutePoint r) => new StagePoint(r.X, r.Y, r.Z);
            return new[]
            {
                // In the yard pocket by the crate: hop onto it.
                new HintMoveSpot("crate", HintMove.Jump, P(a[0]), 2.2f, -0.3f, 0.45f, P(a[1])),
                // On the crate: up onto the awning's low edge.
                new HintMoveSpot("crate_top", HintMove.Jump, P(a[1]), 0.75f, 0.45f, 1.2f, P(a[2])),
                // On the awning: the held jump over its wall lip onto the shed roof.
                new HintMoveSpot("awning", HintMove.HighJump,
                    new StagePoint(CaseRouteLayout.AwningX, CaseRouteLayout.AwningLowY,
                        CaseRouteLayout.AwningWallZ - CaseRouteLayout.AwningRun * 0.5f),
                    1.4f, 1.2f, 2.3f, P(a[4])),
            };
        }
    }
}
