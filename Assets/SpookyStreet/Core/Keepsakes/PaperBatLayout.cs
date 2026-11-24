using System;
using System.Collections.Generic;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Movement;
using SpookyStreet.Core.World;

namespace SpookyStreet.Core.Keepsakes
{
    /// <summary>The spec §4 split: a third on the main loop, a third on skilled routes, a third behind small challenges.</summary>
    public enum BatTier
    {
        Loop,
        Skilled,
        Challenge,
    }

    /// <summary>The moves a bat asks for (S003 moveset). Walk is no flag at all.</summary>
    [Flags]
    public enum BatMoves
    {
        Walk = 0,
        TapJump = 1,
        HighJump = 2,
        ChainJump = 4,
        LedgeGrab = 8,
        WallJump = 16,
        Glide = 32,
    }

    /// <summary>
    /// One collectible paper bat in case 01's space. <see cref="X"/>, <see cref="Y"/>, <see cref="Z"/> is the bat's
    /// centre (world metres). A perched bat rests on a surface (<see cref="SitY"/>, NaN when it hangs from a beam or a
    /// hook with no collider under it) and is taken by Pip standing on <see cref="PerchY"/>; an airborne bat
    /// (<see cref="PerchY"/> NaN) floats on a glide or kick line and is taken in the air. <see cref="FromX"/>,
    /// <see cref="FromY"/>, <see cref="FromZ"/> is where the move that reaches it starts (the top she jumps, glides or
    /// kicks from), and <see cref="Anchor"/> the safe anchor a missed try restarts from.
    /// </summary>
    public readonly struct PaperBatSpot
    {
        public PaperBatSpot(int number, BatTier tier, string area, string spot, BatMoves needs, string anchor, float x,
            float y, float z, float sitY, float perchY, float fromX, float fromY, float fromZ)
        {
            Number = number;
            Tier = tier;
            Area = area;
            Spot = spot;
            Needs = needs;
            Anchor = anchor;
            X = x;
            Y = y;
            Z = z;
            SitY = sitY;
            PerchY = perchY;
            FromX = fromX;
            FromY = fromY;
            FromZ = fromZ;
        }

        /// <summary>1–30 (the table order of the S004 plan, adapted to the frozen level).</summary>
        public int Number { get; }

        /// <summary>Stable, place-keyed ID (never case-keyed, so a replay never touches it; never display text).</summary>
        public string Id => PaperBatLayout.IdFor(Number);

        public BatTier Tier { get; }

        /// <summary>Area name (documentation and logs only).</summary>
        public string Area { get; }

        /// <summary>Where it sits (documentation and logs only).</summary>
        public string Spot { get; }

        public BatMoves Needs { get; }

        /// <summary>The <see cref="CaseAnchors"/> ID a missed try restarts from.</summary>
        public string Anchor { get; }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        /// <summary>Top of the collider the bat rests on, or NaN (hangs from a beam or hook, or floats).</summary>
        public float SitY { get; }

        /// <summary>Top Pip stands on to take it, or NaN for a bat taken in the air.</summary>
        public float PerchY { get; }

        public bool Airborne => float.IsNaN(PerchY);

        public float FromX { get; }
        public float FromY { get; }
        public float FromZ { get; }

        /// <summary>In the separate owl house interior (D24.1).</summary>
        public bool Inside => CaseRouteLayout.Inside(X, Z);

        public override string ToString() => $"{Id} ({Tier}, {Area}: {Spot}) at ({X:F2}, {Y:F2}, {Z:F2})";
    }

    /// <summary>
    /// Where the 30 paper bats hide in case 01's space (S004 §2.1, stream A1), with the move each needs and the safe
    /// anchor each restarts from, and the placement rules the tests prove:
    /// <list type="number">
    /// <item>10 / 10 / 10 by tier; loop bats need only walking and a tap jump.</item>
    /// <item>No bat marks a clue: every bat is at least <see cref="ClueClearance"/> from every Use and examine target
    /// (the decoy and the talk spots included); no bat in the workshop nook.</item>
    /// <item>No bat leads into a hidden answer: no bat in the side yard and treehouse region
    /// (<see cref="InHiddenRegion"/>) is visible from the plaza, front-yard or bench eye points
    /// (<see cref="EyePoints"/>, <see cref="VisibleFrom"/>).</item>
    /// <item>Every bat is reachable with its move (<see cref="Reachable"/>, from the real tuning), and every challenge
    /// restarts within 2 s of walking from its anchor.</item>
    /// <item>Interior bats only in the attic loft and the hall (D24 camera band).</item>
    /// </list>
    /// The level is frozen (S003), so the spots use only existing surfaces; the plan's spots that need geometry the street
    /// does not have (a gate arch, neighbour awnings, stacked planters, a porch lattice, a treehouse flagpole) moved to
    /// the nearest real surface with the same move (docs/S004-keepsakes.md).
    /// </summary>
    public static class PaperBatLayout
    {
        public const string IdPrefix = "bat.lantern_lane.";

        /// <summary>Spec §4: no bat closer than this to a clue or Use target (horizontal metres).</summary>
        public const float ClueClearance = 1.5f;

        /// <summary>A target this far above or below a bat is on another floor (the hall under the attic).</summary>
        public const float OtherFloorHeight = 2f;

        /// <summary>Pip's centre above her feet (capsule 1.1 m) and the pickup sphere round it (the runtime uses both).</summary>
        public const float ChestHeight = 0.55f;

        public const float PickupRadius = 0.7f;

        /// <summary>Margin a jump must clear above a top it lands on.</summary>
        public const float JumpMargin = 0.1f;

        /// <summary>Walking (with the automatic step) climbs at most this.</summary>
        public const float StepRise = 0.2f;

        /// <summary>Spec §5: a challenge restarts within 2 s; at walking speed (4 m/s) that is 8 m from its anchor.</summary>
        public const float MaxRestartDistance = 8f;

        /// <summary>Horizontal reach of a wall kick from the face (4.5 m/s away for about 0.3 s, plus the pickup).</summary>
        public const float WallKickReach = 1.6f;

        /// <summary>Bats keep this far apart (each is its own find).</summary>
        public const float MinSpacing = 1.5f;

        /// <summary>The side-yard region's edges: west of the house front and north of the side-yard hedges' south face.</summary>
        public const float HiddenRegionMaxX = -3.2f;

        public const float HiddenRegionMinZ = 6.52f;

        /// <summary>Eye height of a child on the street (Pip is 1.1 m; CaseSightlines uses child eye heights).</summary>
        public const float EyeHeight = 1.0f;

        public static string IdFor(int number) => IdPrefix + number.ToString("00");

        public static readonly IReadOnlyList<PaperBatSpot> All = Build();

        public static bool IsKnown(string id)
        {
            if (id == null || !id.StartsWith(IdPrefix, StringComparison.Ordinal) || id.Length != IdPrefix.Length + 2)
            {
                return false;
            }

            foreach (var spot in All)
            {
                if (string.Equals(spot.Id, id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryGet(string id, out PaperBatSpot spot)
        {
            foreach (var s in All)
            {
                if (string.Equals(s.Id, id, StringComparison.Ordinal))
                {
                    spot = s;
                    return true;
                }
            }

            spot = default;
            return false;
        }

        // ---------------------------------------------------------------------------------------------- the rules

        /// <summary>The side yard and treehouse region behind the hedge corridor (June's route lives there).</summary>
        public static bool InHiddenRegion(float x, float z) =>
            !CaseRouteLayout.Inside(x, z) && x < HiddenRegionMaxX && z > HiddenRegionMinZ;

        /// <summary>The eye points the region must stay hidden from: the plaza, the front-yard gate and the bench seat.</summary>
        public static IReadOnlyList<StagePoint> EyePoints
        {
            get
            {
                EnvironmentLayout.LocalToWorld(EnvironmentLayout.BenchX, EnvironmentLayout.BenchZ, EnvironmentLayout.BenchYaw,
                    CaseStage.BenchSeatLocalX, 0f, out float seatX, out float seatZ);
                return new[]
                {
                    new StagePoint(0f, EyeHeight, -0.5f),
                    new StagePoint(-0.1f, EyeHeight, EnvironmentLayout.FrontYardZ - 0.3f),
                    new StagePoint(seatX, CaseStage.BenchEyeHeight, seatZ),
                };
            }
        }

        /// <summary>
        /// The street's sight blockers outside the stage slabs: the hedges (front yard and side yard) at their 1.24 m top.
        /// The picket fence is see-through and stays out.
        /// </summary>
        public static IReadOnlyList<SightOccluder> HedgeOccluders
        {
            get
            {
                var list = new List<SightOccluder>();
                foreach (var p in EnvironmentLayout.Street)
                {
                    if (p.Model == EnvironmentLayout.Hedge)
                    {
                        list.Add(new SightOccluder(p.X - p.HalfX, 0f, p.Z - p.HalfZ, p.X + p.HalfX, HedgeTop, p.Z + p.HalfZ));
                    }
                }

                return list;
            }
        }

        /// <summary>Top of the hedge model's collider (CollisionRules: "mesh bounds (1.24 m, standable top)").</summary>
        public const float HedgeTop = 1.24f;

        /// <summary>
        /// True when the bat at <paramref name="spot"/> can be seen from <paramref name="eye"/>: the segment crosses no
        /// hedge and passes through no exterior stage slab (the shed, ramp walls, cabin, house body), sampled every 5 cm.
        /// The last 0.15 m is skipped so the bat's own perch never hides it.
        /// </summary>
        public static bool VisibleFrom(StagePoint eye, float x, float y, float z)
        {
            if (!CaseSightlines.IsVisible(eye.X, eye.Y, eye.Z, x, y, z, HedgeOccluders))
            {
                return false;
            }

            float dx = x - eye.X, dy = y - eye.Y, dz = z - eye.Z;
            float length = (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
            int steps = (int)(length / 0.05f);
            for (int i = 1; i < steps; i++)
            {
                float t = i / (float)steps;
                if (t * length > length - 0.15f)
                {
                    break;
                }

                float px = eye.X + dx * t, py = eye.Y + dy * t, pz = eye.Z + dz * t;
                foreach (var slab in CaseRouteLayout.Slabs)
                {
                    if (slab.Group != SlabGroup.Interior && slab.Blocks(px, pz, py, py, 0f))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        /// <summary>Every clue and Use target the bats keep clear of: the case's Use targets, the cloth and the wooden owl.</summary>
        public static IReadOnlyList<StagePoint> ClueMarks
        {
            get
            {
                var marks = new List<StagePoint>();
                foreach (var point in CaseStage.AllPoints)
                {
                    marks.Add(point.Position);
                }

                marks.Add(new StagePoint(CaseRouteLayout.ClothX, CaseRouteLayout.ClothY, CaseRouteLayout.ClothZ));
                marks.Add(CaseStage.WoodenOwl);
                return marks;
            }
        }

        /// <summary>Nearest clue or Use target on the bat's floor (horizontal metres; infinity when none).</summary>
        public static float NearestClue(in PaperBatSpot spot)
        {
            float best = float.PositiveInfinity;
            foreach (var mark in ClueMarks)
            {
                if (Math.Abs(mark.Y - spot.Y) >= OtherFloorHeight)
                {
                    continue;
                }

                float dx = mark.X - spot.X, dz = mark.Z - spot.Z;
                best = Math.Min(best, (float)Math.Sqrt(dx * dx + dz * dz));
            }

            return best;
        }

        /// <summary>The workshop nook behind the partition (never entered; no bat there).</summary>
        public static bool InWorkshopNook(float x, float z) =>
            CaseRouteLayout.Inside(x, z) && x > CaseRouteLayout.NookX - 0.1f && z > CaseRouteLayout.NookMinZ;

        /// <summary>How high above her launch top Pip's feet can rise with <paramref name="moves"/> (the best one).</summary>
        public static float Rise(BatMoves moves, MovementTuningData tuning)
        {
            float rise = StepRise;
            if ((moves & BatMoves.TapJump) != 0)
            {
                rise = Math.Max(rise, tuning.JumpApexHeight - JumpMargin);
            }

            if ((moves & BatMoves.HighJump) != 0)
            {
                rise = Math.Max(rise, tuning.EffectiveHoldApexHeight - JumpMargin);
            }

            if ((moves & BatMoves.ChainJump) != 0)
            {
                rise = Math.Max(rise, tuning.EffectiveHoldApexHeight * tuning.ChainJumpThirdHeightScale - JumpMargin);
            }

            if ((moves & BatMoves.LedgeGrab) != 0)
            {
                // The catch takes a top up to 1.57 m above the feet at a held jump's apex (S003: 0.92–1.57 m window).
                rise = Math.Max(rise, tuning.EffectiveHoldApexHeight + tuning.LedgeHangHandHeight + tuning.LedgeGrabReachAbove);
            }

            return rise;
        }

        /// <summary>The apex of the jump that starts an airborne try (a tap, or a held jump when the bat asks for one).</summary>
        public static float LaunchApex(BatMoves moves, MovementTuningData tuning) =>
            (moves & (BatMoves.HighJump | BatMoves.ChainJump)) != 0 ? tuning.EffectiveHoldApexHeight : tuning.JumpApexHeight;

        /// <summary>
        /// True when the bat can be taken with its moves (engine-free; the PlayMode raycast proves each sit surface):
        /// a perched bat's perch is within the move's rise of its launch top and the bat within the pickup of Pip standing
        /// there (a tap at the perch included); a glide bat lies above the glide line from its launch; a kick bat lies
        /// within the kick's apex and reach of the marked wall. Airborne bats also float above what a held jump from the
        /// ground under them reaches, so they truly ask for their move.
        /// </summary>
        public static bool Reachable(in PaperBatSpot spot, MovementTuningData tuning, out string why)
        {
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            float hx = spot.X - spot.FromX, hz = spot.Z - spot.FromZ;
            float horizontal = (float)Math.Sqrt(hx * hx + hz * hz);
            if (!spot.Airborne)
            {
                float rise = spot.PerchY - spot.FromY;
                if (rise > Rise(spot.Needs, tuning) + 1e-4f)
                {
                    why = $"perch {spot.PerchY:F2} is {rise:F2} above its launch; {spot.Needs} rises {Rise(spot.Needs, tuning):F2}";
                    return false;
                }

                float reach = ChestHeight + PickupRadius + (spot.Needs != BatMoves.Walk ? tuning.JumpApexHeight : 0f);
                float above = spot.Y - spot.PerchY;
                if (above < 0f || above > reach)
                {
                    why = $"bat {above:F2} above its perch; reach {reach:F2}";
                    return false;
                }

                if (horizontal > 3f)
                {
                    why = $"launch {horizontal:F2} m from the bat";
                    return false;
                }

                why = null;
                return true;
            }

            float ground = CaseRouteLayout.GroundAt(spot.X, spot.Z, spot.Y);
            float fromGround = ground + tuning.EffectiveHoldApexHeight + ChestHeight + PickupRadius;
            if (spot.Y <= fromGround)
            {
                why = $"airborne bat at {spot.Y:F2} is within a held jump from the ground ({fromGround:F2})";
                return false;
            }

            if ((spot.Needs & BatMoves.Glide) != 0)
            {
                float descent = tuning.GlideFallSpeed / tuning.MaxSpeed;
                float line = spot.FromY + LaunchApex(spot.Needs, tuning) + ChestHeight - descent * horizontal;
                if (spot.Y > line + PickupRadius)
                {
                    why = $"bat {spot.Y:F2} above the glide line {line:F2} at {horizontal:F2} m";
                    return false;
                }

                if (spot.Y < ground + ChestHeight)
                {
                    why = "bat below a landed Pip";
                    return false;
                }

                why = null;
                return true;
            }

            if ((spot.Needs & BatMoves.WallJump) != 0)
            {
                // From the highest slide (chest at the wall top) the kick rises WallJumpHeight.
                float apexChest = spot.FromY + tuning.WallJumpHeight;
                if (spot.Y > apexChest + PickupRadius || horizontal > WallKickReach)
                {
                    why = $"bat {spot.Y:F2}, {horizontal:F2} m out: the kick's chest apex is {apexChest:F2}";
                    return false;
                }

                why = null;
                return true;
            }

            why = "an airborne bat needs a glide or a wall kick";
            return false;
        }

        // ---------------------------------------------------------------------------------------------- the layout

        private static PaperBatSpot[] Build()
        {
            const float nan = float.NaN;
            const BatMoves walk = BatMoves.Walk;
            const BatMoves tap = BatMoves.TapJump;
            const BatMoves high = BatMoves.HighJump;
            const BatMoves chain = BatMoves.ChainJump;
            const float deck = CaseRouteLayout.TreehouseY;
            const float floor = CaseRouteLayout.FloorY;
            const float loft = CaseRouteLayout.LoftY;
            const float ifz = CaseRouteLayout.InteriorFrontZ;

            // Treehouse cabin spots in its own frame (+y the yard side, -y north, -x the door side toward the shed).
            Cabin(-0.8f, 0.8f, out float innerX, out float innerZ);
            Cabin(-0.8f, -0.95f, out float roofX, out float roofZ);
            float roofTop = deck + CaseRouteLayout.CabinEave + 0.1f + (CaseRouteLayout.CabinHalf + 0.3f - 0.95f) *
                CaseRouteLayout.CabinRoofSlope;

            return new[]
            {
                // ---- Loop: walk and tap jump only (a third on the main loop) ----
                new PaperBatSpot(1, BatTier.Loop, "arrival porch", "on the east rail's north post, the first thing in reach",
                    walk, CaseAnchors.ArrivalPorch, 1.95f, 1.72f, -13.05f, 1.6f, 0.6f, 1.55f, 0.6f, -13.1f),
                new PaperBatSpot(2, BatTier.Loop, "arrival porch", "on the west rail's back post, behind the candy-bowl crate",
                    walk, CaseAnchors.ArrivalPorch, -1.95f, 1.72f, -14.95f, 1.6f, 0.6f, -1.6f, 0.6f, -14.9f),
                new PaperBatSpot(3, BatTier.Loop, "plaza", "on the party table's free corner", tap, CaseAnchors.Plaza,
                    5.16f, 0.84f, -1.98f, 0.72f, 0.72f, 5.6f, 0f, -1.4f),
                new PaperBatSpot(4, BatTier.Loop, "arrival path", "at the street lamp's foot, on the plaza side", walk,
                    CaseAnchors.ArrivalPorch, 2.2f, 0.12f, -9.25f, 0f, 0f, 1.6f, 0f, -9.25f),
                new PaperBatSpot(5, BatTier.Loop, "plaza south-west", "in the fallen leaves between the path and Bea's corner",
                    walk, CaseAnchors.BicycleCorner, -2.6f, 0.12f, -3.2f, 0f, 0f, -2f, 0f, -3.2f),
                new PaperBatSpot(6, BatTier.Loop, "bench corner", "on the picket fence's east end post", tap,
                    CaseAnchors.Bench, 9.0f, 1.4f, 5.0f, 1.28f, 0f, 9.0f, 0f, 4.4f),
                new PaperBatSpot(7, BatTier.Loop, "side yard", "on the ramp by its west skirt, facing in", walk,
                    CaseAnchors.SideYard, -13.1f, 0.54f, 9.5f, 0.42f, 0.42f, -12.45f, 0.42f, 9.5f),
                new PaperBatSpot(8, BatTier.Loop, "treehouse", "under the cabin's roof beam, beside the door", tap,
                    CaseAnchors.Treehouse, innerX, deck + 1.35f, innerZ, nan, deck, -8.2f, deck, 12.4f),
                new PaperBatSpot(9, BatTier.Loop, "attic loft", "on a roof beam over the loft's front, away from the sill",
                    tap, CaseAnchors.Attic, 0.8f, loft + 1.45f, ifz + 1.4f, nan, loft, 0.6f, loft, ifz + 2.0f),
                new PaperBatSpot(10, BatTier.Loop, "hall", "on the coat hook by the stair foot", walk, CaseAnchors.Foyer,
                    -3.4f, floor + 0.95f, ifz + 2.5f, nan, floor, -2.9f, floor, ifz + 2.6f),

                // ---- Skilled routes (a third) ----
                new PaperBatSpot(11, BatTier.Skilled, "side yard", "on the fixed crate of the skilled ascent", tap,
                    CaseAnchors.SideYard, CaseRouteLayout.CrateX, CaseRouteLayout.CrateSize + 0.12f, CaseRouteLayout.CrateZ,
                    CaseRouteLayout.CrateSize, CaseRouteLayout.CrateSize, CaseRouteLayout.CrateX, 0f,
                    CaseRouteLayout.CrateZ - 0.8f),
                new PaperBatSpot(12, BatTier.Skilled, "side yard", "on the shed roof's west edge, behind the awning wall",
                    tap, CaseAnchors.Treehouse, -7.65f, deck + 0.12f, 12.0f, deck, deck, CaseRouteLayout.AwningX, 1.92f,
                    CaseRouteLayout.AwningWallZ - 0.45f),
                new PaperBatSpot(13, BatTier.Skilled, "front yard", "on the front hedge's top, west of the gate", high,
                    CaseAnchors.Plaza, -2.6f, HedgeTop + 0.12f, EnvironmentLayout.FrontYardZ, HedgeTop, HedgeTop, -2.6f, 0f,
                    EnvironmentLayout.FrontYardZ - 0.9f),
                new PaperBatSpot(14, BatTier.Skilled, "front yard", "along the picket fence top", high, CaseAnchors.Plaza,
                    4.8f, 1.4f, EnvironmentLayout.FrontYardZ, 1.28f, 1.28f, 4.8f, EnvironmentLayout.SidewalkTop, 4.2f),
                new PaperBatSpot(15, BatTier.Skilled, "owl house", "on the porch roof's west end, from the porch clutter",
                    high, CaseAnchors.BluePorch, -2.8f, 3.18f, 9.0f, 3.06f, 3.06f, -1.5f, 1.45f, 10.4f),
                new PaperBatSpot(16, BatTier.Skilled, "owl house", "on the low roof step's east side", high | tap,
                    CaseAnchors.BluePorch, 2.0f, 4.12f, 12.5f, 4.0f, 4.0f, 2.0f, 3.25f, 10.5f),
                new PaperBatSpot(17, BatTier.Skilled, "bench corner", "on the east boundary hedge's top", high,
                    CaseAnchors.Bench, 21.45f, 1.41f, 1.0f, 1.29f, 1.29f, 20.6f, 0f, 1.0f),
                new PaperBatSpot(18, BatTier.Skilled, "arrival side", "on the south boundary hedge's top behind the porch",
                    high, CaseAnchors.ArrivalPorch, -6.0f, 1.41f, -17.45f, 1.29f, 1.29f, -6.0f, 0f, -16.6f),
                new PaperBatSpot(19, BatTier.Skilled, "owl house roof", "on the ridge cap's back end", high,
                    CaseAnchors.BluePorch, 0f, 6.57f, 16.5f, 6.45f, 6.45f, 1.0f, 5.8f, 15.5f),
                new PaperBatSpot(20, BatTier.Skilled, "treehouse", "on the cabin roof's north slope, facing away from the plaza",
                    chain, CaseAnchors.Treehouse, roofX, roofTop + 0.12f, roofZ, roofTop, roofTop, -7.9f, deck, 12.6f),

                // ---- Challenges (a third): each under 30 s, restarting from its anchor ----
                new PaperBatSpot(21, BatTier.Challenge, "arrival path", "on the street lamp's head", high | BatMoves.LedgeGrab,
                    CaseAnchors.ArrivalPorch, 2.75f, 3.12f, -9.55f, 3.0f, 3.0f, 2.2f, 0f, -10.3f),
                new PaperBatSpot(22, BatTier.Challenge, "side yard", "in the air off the shed's west wall, above the yard pocket",
                    BatMoves.WallJump, CaseAnchors.Treehouse, -8.85f, 3.45f, 14.75f, nan, nan, CaseRouteLayout.RoofMinX, deck,
                    14.75f),
                new PaperBatSpot(23, BatTier.Challenge, "front yard", "in the air over the path, gliding off the porch roof",
                    tap | BatMoves.Glide, CaseAnchors.BluePorch, 0f, 3.5f, 6.5f, nan, nan, 0f, 2.95f, 8.3f),
                new PaperBatSpot(24, BatTier.Challenge, "side yard", "in the air behind the cabin, gliding off its roof toward the ramp",
                    tap | BatMoves.Glide, CaseAnchors.Treehouse, -11.2f, 3.6f, 13.6f, nan, nan, roofX, roofTop, roofZ),
                new PaperBatSpot(25, BatTier.Challenge, "owl house", "on the porch roof's east end, chain-jumped from the deck",
                    chain, CaseAnchors.BluePorch, 3.1f, 3.09f, 8.5f, 2.97f, 2.97f, 2.6f, floor, 9.4f),
                new PaperBatSpot(26, BatTier.Challenge, "front yard east", "in the air east of the house, gliding off the porch roof",
                    tap | BatMoves.Glide, CaseAnchors.BluePorch, 5.2f, 3.5f, 7.6f, nan, nan, 3.1f, 2.97f, 8.5f),
                new PaperBatSpot(27, BatTier.Challenge, "arrival path", "in the air over the path, gliding off the lamp head",
                    tap | BatMoves.Glide, CaseAnchors.ArrivalPorch, 2.9f, 3.4f, -7.4f, nan, nan, 2.75f, 3.0f, -9.55f),
                new PaperBatSpot(28, BatTier.Challenge, "owl house roof", "in the air east of the house, the long glide off the chimney",
                    tap | BatMoves.Glide, CaseAnchors.BluePorch, 6.5f, 4.6f, 15.0f, nan, nan, 1.5f, 7.4f, 15.45f),
                new PaperBatSpot(29, BatTier.Challenge, "arrival porch", "in the air east of the porch, a held jump and glide off the rail",
                    high | BatMoves.Glide, CaseAnchors.ArrivalPorch, 3.6f, 3.5f, -12.0f, nan, nan, 1.95f, 1.6f, -13.3f),
                new PaperBatSpot(30, BatTier.Challenge, "owl house roof", "on the chimney top, the block's highest point",
                    chain | high, CaseAnchors.BluePorch, 1.5f, 7.52f, 15.45f, 7.4f, 7.4f, 1.0f, 5.8f, 15.4f),
            };
        }

        private static void Cabin(float cx, float cy, out float x, out float z) =>
            CaseRouteLayout.CabinToWorld(CaseRouteLayout.CabinPlatX + cx, CaseRouteLayout.CabinPlatY + cy, out x, out z);
    }
}
