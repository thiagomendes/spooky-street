using System;
using System.Collections.Generic;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Crowd;
using SpookyStreet.Core.World;

namespace SpookyStreet.Core.LivingStreet
{
    /// <summary>How a neighbour's front door opens for a visitor (the door gag, a hinged leaf, never a blend shape).</summary>
    public readonly struct DoorGag
    {
        public DoorGag(float openDegrees, float openSeconds, float bounce, int figures, bool hop, float lampBoost)
        {
            OpenDegrees = openDegrees;
            OpenSeconds = openSeconds;
            Bounce = bounce;
            Figures = figures;
            Hop = hop;
            LampBoost = lampBoost;
        }

        /// <summary>How far the leaf swings out (degrees about its hinge).</summary>
        public float OpenDegrees { get; }

        /// <summary>How long the swing takes.</summary>
        public float OpenSeconds { get; }

        /// <summary>Overshoot of the swing (0: none), a springy fling.</summary>
        public float Bounce { get; }

        /// <summary>Grown-ups who step into the doorway (the Abbotts are two).</summary>
        public int Figures { get; }

        /// <summary>The figures pop up with a little hop.</summary>
        public bool Hop { get; }

        /// <summary>How much the porch lamp brightens while the door is open (Nana Pearl's warm light).</summary>
        public float LampBoost { get; }
    }

    /// <summary>One trick-or-treat door of the slice (living-street §4.1): a named neighbour, a porch house and a bell.</summary>
    public sealed class NeighbourDoor
    {
        internal NeighbourDoor(string id, string house, float x, float z, float yaw, float doorLocalX, float wallLocalZ,
            float standLocalZ, string speakerKey, DoorGag gag, string first, string again, string third)
        {
            Id = id;
            House = house;
            X = x;
            Z = z;
            Yaw = yaw;
            DoorLocalX = doorLocalX;
            WallLocalZ = wallLocalZ;
            SpeakerKey = speakerKey;
            Gag = gag;
            FirstLine = first;
            AgainLine = again;
            ThirdLine = third;
            EnvironmentLayout.LocalToWorld(x, z, yaw, doorLocalX, wallLocalZ, out float dx, out float dz);
            DoorX = dx;
            DoorZ = dz;
            EnvironmentLayout.LocalToWorld(x, z, yaw, doorLocalX, standLocalZ, out float sx, out float sz);
            StandX = sx;
            StandZ = sz;
            // The model's front faces -Z in its own frame, so a visitor facing the door looks toward +Z there.
            StandFacingYaw = Normalize(yaw);
        }

        /// <summary>Stable ID ("fogg"); the saved form is <see cref="StableId"/>.</summary>
        public string Id { get; }

        /// <summary>The saved memory ID (living-street §6 rule 8: <c>street.free.*</c>).</summary>
        public string StableId => "street.free.door." + Id;

        /// <summary>House model (an existing street-set porch house).</summary>
        public string House { get; }

        public float X { get; }
        public float Z { get; }

        /// <summary>Model yaw (the front faces -Z at yaw 0).</summary>
        public float Yaw { get; }

        /// <summary>Door centre across the front, in the house model's own frame.</summary>
        public float DoorLocalX { get; }

        /// <summary>The front wall's plane in the house model's own frame (the door sits in it).</summary>
        public float WallLocalZ { get; }

        /// <summary>The door's centre on the wall, world plan position.</summary>
        public float DoorX { get; }
        public float DoorZ { get; }

        /// <summary>Where a visitor stands at the foot of the steps to ring (world plan position).</summary>
        public float StandX { get; }
        public float StandZ { get; }

        /// <summary>Unity yaw a visitor at the stand faces (toward the door).</summary>
        public float StandFacingYaw { get; }

        /// <summary>The neighbour's name (speaker label key).</summary>
        public string SpeakerKey { get; }

        public DoorGag Gag { get; }

        /// <summary>The neighbour's line on Pip's first, second and later visits (living-street §4.1).</summary>
        public string FirstLine { get; }
        public string AgainLine { get; }
        public string ThirdLine { get; }

        /// <summary>The line for Pip's visit after <paramref name="visitsBefore"/> earlier visits.</summary>
        public string LineFor(int visitsBefore) =>
            visitsBefore <= 0 ? FirstLine : visitsBefore == 1 ? AgainLine : ThirdLine;

        private static float Normalize(float yaw)
        {
            float y = yaw % 360f;
            return y < 0f ? y + 360f : y;
        }

        public override string ToString() => Id + " door at (" + DoorX.ToString("F1") + ", " + DoorZ.ToString("F1") + ")";
    }

    /// <summary>
    /// The living street's test slice in plan (docs/living-street.md §2.2 and §9 phase 1, D33/D34): the case block
    /// (the <i>heart</i>) stays as it is except for a gap in its east hedge where the lane runs through
    /// (z <see cref="GapMinZ"/>…<see cref="GapMaxZ"/>), and the lane continues east to the first corner of the Green:
    /// three porch houses with trick-or-treat doors (Mr. Fogg and Nana Pearl on the north side, the Abbotts on the
    /// south), a sidewalk, street lamps and the paper lantern table. The houses are the street set's reviewed porch
    /// houses; the north hedge of the heart stays closed, so the owl house's yard gains no new approach. Pure data, so
    /// Core tests prove the doors, the table and every walker stay clear of the case.
    /// </summary>
    public static class LivingStreetLayout
    {
        // --- The lane and its edges ------------------------------------------------------------------------

        /// <summary>The heart's east hedge opens here (a 6 m gap where Lantern Lane runs through).</summary>
        public const float GapMinZ = EnvironmentLayout.EastGapMinZ;
        public const float GapMaxZ = EnvironmentLayout.EastGapMaxZ;

        /// <summary>The east lane runs from the heart's edge to this x (the Green's first corner, inside the sky dome).</summary>
        public const float EastEndX = 46f;

        /// <summary>The east lane's south edge (its hedge's inner face); the north edge continues the heart's.</summary>
        public const float EastSouthZ = -14f;

        /// <summary>Hedge lines of the east lane (the boundary hedge model is 6 m long, 0.8 m deep).</summary>
        public const float EastHedgeX = 45.5f;
        public const float EastSouthHedgeZ = -13.5f;

        /// <summary>Outward nudge of a hedge that overlaps its neighbour (so the two never z-fight).</summary>
        public const float OverlapNudge = 0.12f;

        /// <summary>The walking lines of the lane (north and south of its centre) and the strollers' middle line.</summary>
        public const float NorthLineZ = 1.6f;
        public const float SouthLineZ = -1.6f;

        /// <summary>True inside the case block (the heart), where the crowd cap and the clue rules apply.</summary>
        public static bool InHeart(float x, float z) =>
            Math.Abs(x) < EnvironmentLayout.BlockHalfX && Math.Abs(z) < EnvironmentLayout.BlockHalfZ;

        /// <summary>True on the east lane (east of the heart, inside its edges).</summary>
        public static bool InEastLane(float x, float z) =>
            x >= EnvironmentLayout.BlockHalfX && x < EastEndX && z > EastSouthZ && z < EnvironmentLayout.BlockHalfZ;

        /// <summary>True anywhere Pip can walk outdoors: the heart or the east lane.</summary>
        public static bool Walkable(float x, float z) => InHeart(x, z) || InEastLane(x, z);

        // --- The doors ---------------------------------------------------------------------------------------

        public const string Fogg = "fogg";
        public const string Pearl = "pearl";
        public const string Abbotts = "abbotts";

        /// <summary>The door leaf (1.1 m door plus its trim) and the sill it stands on, in every house model.</summary>
        public const float DoorWidth = 1.14f;
        public const float DoorHeight = 2.12f;
        public const float DoorSill = 0.4f;

        /// <summary>Pip's centre within this of a stand (horizontal metres) offers "Ring".</summary>
        public const float BellReach = 1.5f;

        /// <summary>
        /// The three doors (house models measured from tools/blender/env/street.py: each house is exported centred, its
        /// front wall 3 m in front of the centre; the gambrel house's long porch puts its door 1.9 m back from the porch).
        /// </summary>
        public static readonly IReadOnlyList<NeighbourDoor> Doors = new[]
        {
            // Mr. Fogg: the tower house, north side. Booming and silly: he flings the door wide with a bounce.
            new NeighbourDoor(Fogg, EnvironmentLayout.NeighbourHouseA, 28.6f, 9.6f, 0f, 0.5f, -3.0f, -4.6f,
                Localization.LocalizationKeys.LivingStreet.SpeakerFogg, new DoorGag(115f, 0.35f, 0.18f, 1, false, 0f),
                Localization.LocalizationKeys.LivingStreet.FoggFirst, Localization.LocalizationKeys.LivingStreet.FoggAgain,
                Localization.LocalizationKeys.LivingStreet.FoggThird),
            // Nana Pearl: the gambrel house with the long porch, north side. Slow and warm: the door eases open and
            // her porch lamp glows brighter.
            new NeighbourDoor(Pearl, EnvironmentLayout.NeighbourHouseB, 39.2f, 10.6f, 0f, 0f, -1.9f, -5.3f,
                Localization.LocalizationKeys.LivingStreet.SpeakerPearl, new DoorGag(70f, 1.4f, 0f, 1, false, 0.8f),
                Localization.LocalizationKeys.LivingStreet.PearlFirst, Localization.LocalizationKeys.LivingStreet.PearlAgain,
                Localization.LocalizationKeys.LivingStreet.PearlThird),
            // The Abbotts: the crooked house, south side, facing the lane. Two dads pop into the doorway together.
            new NeighbourDoor(Abbotts, EnvironmentLayout.NeighbourHouseC, 32.6f, -8.6f, 180f, -1.85f, -3.0f, -4.6f,
                Localization.LocalizationKeys.LivingStreet.SpeakerAbbotts, new DoorGag(95f, 0.6f, 0.06f, 2, true, 0f),
                Localization.LocalizationKeys.LivingStreet.AbbottsFirst, Localization.LocalizationKeys.LivingStreet.AbbottsAgain,
                Localization.LocalizationKeys.LivingStreet.AbbottsThird),
        };

        public static NeighbourDoor Door(string id)
        {
            foreach (var door in Doors)
            {
                if (string.Equals(door.Id, id, StringComparison.Ordinal))
                {
                    return door;
                }
            }

            return null;
        }

        /// <summary>The door whose stand Pip (feet at x, z) is within <see cref="BellReach"/> of, or null.</summary>
        public static NeighbourDoor DoorInReach(float x, float z)
        {
            NeighbourDoor best = null;
            float bestD = BellReach * BellReach;
            foreach (var door in Doors)
            {
                float dx = door.StandX - x;
                float dz = door.StandZ - z;
                float d = dx * dx + dz * dz;
                if (d <= bestD)
                {
                    bestD = d;
                    best = door;
                }
            }

            return best;
        }

        /// <summary>
        /// The neighbours in their doorways, built from the crowd kit's grown-up parts without costume pieces (they answer
        /// the door in house clothes): placeholders for the named neighbours until their own art (living-street §4.1).
        /// The Abbotts are two. Every part is in the kit (<see cref="CrowdKit.AdultParts"/>), one per slot.
        /// </summary>
        public static readonly IReadOnlyList<CrowdWalkerRecipe> NeighbourRecipes = new[]
        {
            new CrowdWalkerRecipe("Neighbour_Fogg", CrowdKit.AdultModel, "Body_King", "Hands_T6", "Shoes_Ink", "Head_King", "Hair_Crop"),
            new CrowdWalkerRecipe("Neighbour_Pearl", CrowdKit.AdultModel, "Body_CatMum", "Hands_T2", "Shoes_Bone", "Head_CatMum", "Hair_Bun"),
            new CrowdWalkerRecipe("Neighbour_AbbottA", CrowdKit.AdultModel, "Body_Skeleton", "Hands_T4", "Shoes_Orange", "Head_King", "Hair_Short"),
            new CrowdWalkerRecipe("Neighbour_AbbottB", CrowdKit.AdultModel, "Body_King", "Hands_T2", "Shoes_Bone", "Head_King", "Hair_Crop"),
        };

        /// <summary>The neighbour recipe with <paramref name="id"/>, or null.</summary>
        public static CrowdWalkerRecipe NeighbourRecipe(string id)
        {
            foreach (var recipe in NeighbourRecipes)
            {
                if (recipe.Id == id)
                {
                    return recipe;
                }
            }

            return null;
        }

        /// <summary>Who answers <paramref name="door"/> (one recipe, two for the Abbotts).</summary>
        public static IReadOnlyList<string> NeighboursAt(NeighbourDoor door)
        {
            if (door == null)
            {
                return Array.Empty<string>();
            }

            switch (door.Id)
            {
                case Fogg: return new[] { "Neighbour_Fogg" };
                case Pearl: return new[] { "Neighbour_Pearl" };
                default: return new[] { "Neighbour_AbbottA", "Neighbour_AbbottB" };
            }
        }

        /// <summary>The house models as placements (for the bounds checks; the runtime fits boxes to the meshes).</summary>
        public static readonly Placement[] Houses = BuildHouses();

        // --- The paper lantern table (the Green's first corner) -------------------------------------------------

        /// <summary>The party table the lanterns are folded on (PartyTable model), turned toward the lane.</summary>
        public const float TableX = 41.6f;
        public const float TableZ = -6.4f;
        public const float TableYaw = 200f;

        /// <summary>Where Pip stands to fold (the table's lane side) and how close she must be.</summary>
        public const float TableUseX = 41.4f;
        public const float TableUseZ = -5.0f;
        public const float TableReach = 1.6f;

        public static bool TableInReach(float x, float z)
        {
            float dx = x - TableUseX;
            float dz = z - TableUseZ;
            return dx * dx + dz * dz <= TableReach * TableReach;
        }

        // --- Pip's porch (the arrival porch: her lantern and her bell) -------------------------------------------

        /// <summary>Pip's paper lantern hangs from a crook post on the porch's west rail, front corner.</summary>
        public const float LanternPostX = -1.95f;
        public const float LanternPostZ = -13.15f;
        public const float LanternY = 2.35f;

        /// <summary>Where a group stands at the foot of Pip's steps to ring her bell, facing the porch (south).</summary>
        public const float PorchStandX = 0.35f;
        public const float PorchStandZ = -11.5f;
        public const float PorchStandYaw = 180f;

        /// <summary>Pip within this of the porch stand can hand out a treat (she is on her deck or at its steps).</summary>
        public const float TreatReach = 3.0f;

        /// <summary>A group waits longer at Pip's bell while she is this close (she may be coming).</summary>
        public const float PorchNear = 7f;

        public static bool TreatInReach(float x, float z)
        {
            float dx = x - PorchStandX;
            float dz = z - PorchStandZ;
            return dx * dx + dz * dz <= TreatReach * TreatReach;
        }

        // --- The lane's dressing --------------------------------------------------------------------------------

        /// <summary>The boundary hedges of the east lane (north row, south row and the east end).</summary>
        public static readonly Placement[] Hedges = BuildHedges();

        /// <summary>The sidewalk continues east along the north side of the lane.</summary>
        public static readonly Placement[] SidewalkTiles = BuildSidewalk();

        /// <summary>Street lamps, pumpkins on the stoops and a lamp by the lantern table (decoration, off the walkers' lines).</summary>
        public static readonly Placement[] Props =
        {
            new Placement(EnvironmentLayout.StreetLamp, 26.2f, 0f, 4.9f, 0f, collider: PlacementCollider.Pole, halfX: 0.2f, halfZ: 0.2f),
            new Placement(EnvironmentLayout.StreetLamp, 36.4f, 0f, 4.9f, 0f, collider: PlacementCollider.Pole, halfX: 0.2f, halfZ: 0.2f),
            new Placement(EnvironmentLayout.StreetLamp, 27.8f, 0f, -4.4f, 180f, collider: PlacementCollider.Pole, halfX: 0.2f, halfZ: 0.2f),
            new Placement(EnvironmentLayout.StreetLamp, 44.2f, 0f, -3.6f, 180f, collider: PlacementCollider.Pole, halfX: 0.2f, halfZ: 0.2f),
            new Placement(EnvironmentLayout.JackOLantern, 30.3f, 0f, 5.6f, -20f, collider: PlacementCollider.Bounds, halfX: 0.35f, halfZ: 0.35f),
            new Placement(EnvironmentLayout.Pumpkin, 27.6f, 0f, 5.5f, 30f, collider: PlacementCollider.Bounds, halfX: 0.2f, halfZ: 0.2f),
            new Placement(EnvironmentLayout.JackOLantern, 40.6f, 0f, 5.9f, 15f, collider: PlacementCollider.Bounds, halfX: 0.35f, halfZ: 0.35f),
            new Placement(EnvironmentLayout.Pumpkin, 37.7f, 0f, 5.8f, -40f, collider: PlacementCollider.Bounds, halfX: 0.2f, halfZ: 0.2f),
            new Placement(EnvironmentLayout.JackOLantern, 33.2f, 0f, -5.0f, 160f, collider: PlacementCollider.Bounds, halfX: 0.35f, halfZ: 0.35f),
            new Placement(EnvironmentLayout.Pumpkin, 35.7f, 0f, -5.1f, 200f, collider: PlacementCollider.Bounds, halfX: 0.2f, halfZ: 0.2f),
            new Placement(EnvironmentLayout.PartyTable, TableX, 0f, TableZ, TableYaw, collider: PlacementCollider.Bounds, halfX: 0.9f, halfZ: 0.6f),
            new Placement(EnvironmentLayout.Crate, 43.0f, 0f, -6.9f, 25f, collider: PlacementCollider.Bounds, halfX: 0.4f, halfZ: 0.4f),
        };

        /// <summary>String-light posts of the Green's corner (the lights hang between them over the table).</summary>
        public static readonly StagePoint[] StringLightPosts =
        {
            new StagePoint(39.4f, 0f, -7.6f), new StagePoint(44.4f, 0f, -7.9f), new StagePoint(44.6f, 0f, -1.0f),
        };

        public const float StringLightHeight = 2.9f;

        private static Placement[] BuildHouses()
        {
            var list = new List<Placement>();
            foreach (var door in Doors)
            {
                EnvironmentLayout.Size(door.House, out float w, out float d);
                EnvironmentLayout.Footprint(w, d, door.Yaw, out float hx, out float hz);
                list.Add(new Placement(door.House, door.X, 0f, door.Z, door.Yaw, collider: PlacementCollider.Bounds,
                    halfX: hx, halfZ: hz, detail: false));
            }

            return list.ToArray();
        }

        private static Placement Hedge(float x, float z, float yaw)
        {
            EnvironmentLayout.Size(EnvironmentLayout.BoundaryHedgeLong, out float w, out float d);
            EnvironmentLayout.Footprint(w, d, yaw, out float hx, out float hz);
            return new Placement(EnvironmentLayout.BoundaryHedgeLong, x, 0f, z, yaw, halfX: hx, halfZ: hz, detail: false);
        }

        private static Placement[] BuildHedges()
        {
            var list = new List<Placement>();
            float l = EnvironmentLayout.HedgeLength;
            // North row (behind the north houses) and south row, from the heart's edge to the east end.
            for (float x = EnvironmentLayout.BlockHalfX + l * 0.5f; x - l * 0.5f < EastEndX - 0.5f; x += l)
            {
                list.Add(Hedge(x, EnvironmentLayout.HedgeLineZ, 0f));
                list.Add(Hedge(x, EastSouthHedgeZ, 180f));
            }

            // East end: 6 m modules from the south row up; the last one overlaps and steps out so it never z-fights.
            float z0 = EastSouthZ;
            float top = EnvironmentLayout.BlockHalfZ;
            for (float z = z0 + l * 0.5f; ; z += l)
            {
                if (z + l * 0.5f > top + 1e-3f)
                {
                    list.Add(Hedge(EastHedgeX + OverlapNudge, top - l * 0.5f, 90f));
                    break;
                }

                list.Add(Hedge(EastHedgeX, z, 90f));
                if (z + l * 0.5f >= top - 1e-3f)
                {
                    break;
                }
            }

            return list.ToArray();
        }

        private static Placement[] BuildSidewalk()
        {
            float tile = EnvironmentLayout.SidewalkTile;
            var list = new List<Placement>();
            for (float x0 = EnvironmentLayout.SidewalkHalfX; x0 + tile <= EastEndX - 1f + 1e-3f; x0 += tile)
            {
                EnvironmentLayout.Footprint(tile, tile, 0f, out float hx, out float hz);
                list.Add(new Placement(EnvironmentLayout.Sidewalk, x0 + tile * 0.5f, EnvironmentLayout.SidewalkLift,
                    EnvironmentLayout.SidewalkZ, EnvironmentLayout.SidewalkYaw, halfX: hx, halfZ: hz, detail: false));
            }

            return list.ToArray();
        }
    }
}
