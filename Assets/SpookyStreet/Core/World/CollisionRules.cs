using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.World
{
    /// <summary>Shape of one collider in a <see cref="ColliderRecipe"/>.</summary>
    public enum ColliderShape
    {
        /// <summary>A box given in the model's own frame (optionally pitched about its local X).</summary>
        Box,

        /// <summary>A box fitted to the model's mesh bounds, optionally clamped to a height band.</summary>
        FittedBox,

        /// <summary>A capsule between two points (sphere centres) in the model's own frame.</summary>
        Capsule,
    }

    /// <summary>
    /// One collider of a model, in the model's own upright frame: metres, +Y up, the model's front toward -Z, and
    /// the placement's yaw and scale applied on top (Blender (x, y, z) exports to this frame as (x, z, y)).
    /// </summary>
    public readonly struct ColliderPart
    {
        private ColliderPart(string name, ColliderShape shape, float ax, float ay, float az, float bx, float by,
            float bz, float pitch, float radius)
        {
            Name = name;
            Shape = shape;
            AX = ax;
            AY = ay;
            AZ = az;
            BX = bx;
            BY = by;
            BZ = bz;
            Pitch = pitch;
            Radius = radius;
        }

        public string Name { get; }
        public ColliderShape Shape { get; }

        /// <summary>Box: centre. Capsule: first sphere centre. FittedBox: <see cref="AY"/> is the lowest y (NaN: mesh).</summary>
        public float AX { get; }
        public float AY { get; }
        public float AZ { get; }

        /// <summary>Box: size. Capsule: second sphere centre. FittedBox: <see cref="BY"/> is the highest y (NaN: mesh).</summary>
        public float BX { get; }
        public float BY { get; }
        public float BZ { get; }

        /// <summary>Box only: degrees about the model's local X (negative tips the -Z front down).</summary>
        public float Pitch { get; }

        /// <summary>Capsule only.</summary>
        public float Radius { get; }

        /// <summary>Highest point of the part (NaN for a fitted box without a clamp).</summary>
        public float Top
        {
            get
            {
                switch (Shape)
                {
                    case ColliderShape.Box:
                        double r = Pitch * Math.PI / 180.0;
                        return AY + (float)(0.5 * (BY * Math.Abs(Math.Cos(r)) + BZ * Math.Abs(Math.Sin(r))));
                    case ColliderShape.Capsule:
                        return Math.Max(AY, BY) + Radius;
                    default:
                        return BY;
                }
            }
        }

        public static ColliderPart Box(string name, float cx, float cy, float cz, float sx, float sy, float sz,
            float pitch = 0f) =>
            new ColliderPart(name, ColliderShape.Box, cx, cy, cz, sx, sy, sz, pitch, 0f);

        /// <summary>Box from min/max corners (no pitch).</summary>
        public static ColliderPart Span(string name, float x0, float y0, float z0, float x1, float y1, float z1) =>
            Box(name, (x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f, x1 - x0, y1 - y0, z1 - z0);

        public static ColliderPart Fitted(string name, float yMin = float.NaN, float yMax = float.NaN) =>
            new ColliderPart(name, ColliderShape.FittedBox, 0f, yMin, 0f, 0f, yMax, 0f, 0f, 0f);

        public static ColliderPart Capsule(string name, float ax, float ay, float az, float bx, float by, float bz,
            float radius) =>
            new ColliderPart(name, ColliderShape.Capsule, ax, ay, az, bx, by, bz, 0f, radius);
    }

    /// <summary>How one model blocks Pip: solid (with its parts) or decoration that Pip and the camera pass.</summary>
    public sealed class ColliderRecipe
    {
        public ColliderRecipe(string model, bool solid, string note, params ColliderPart[] parts)
        {
            Model = model;
            Solid = solid;
            Note = note;
            Parts = parts ?? Array.Empty<ColliderPart>();
        }

        public string Model { get; }
        public bool Solid { get; }
        public string Note { get; }
        public ColliderPart[] Parts { get; }

        /// <summary>
        /// Name of a greybox collider that blocks for this model (its own <see cref="Parts"/> may then be empty),
        /// e.g. the arrival porch footing under the porch kit.
        /// </summary>
        public string CoveredBy { get; private set; }

        public ColliderRecipe Covered(string greyboxCollider)
        {
            CoveredBy = greyboxCollider;
            return this;
        }
    }

    /// <summary>
    /// Collision audit of every original model placed in Lantern Lane (owner device feedback on TestFlight 13.1:
    /// "she passes through the treehouse" and "jumping a boundary hedge drops her into limbo"). The table is the
    /// single source of each model's colliders: simple boxes and capsules in the model's own frame, fitted to the
    /// meshes (bounds measured from the FBX exports and the Blender build scripts in tools/blender/env). Spec
    /// v1.2 §4: every rooftop, awning, fence top, hedge top and the treehouse is reachable, so solid pieces get a
    /// sensible standable top. Also the block's invisible boundary: walls just outside the boundary hedges, tall
    /// enough for the highest standable top plus the best jump, and ground under the whole lawn ring.
    /// Models without an entry here keep their placement's <see cref="PlacementCollider"/>.
    /// </summary>
    public static class CollisionRules
    {
        // --- The maple frame (tools/blender/env/maple.py): trunk lean, fork and limb stubs ----------------------

        /// <summary>Trunk centre line at height <paramref name="y"/> in the model frame (maple.py <c>lean</c>).</summary>
        public static void MapleTrunkAt(float y, out float x, out float z)
        {
            x = 0.05f * y + 0.015f * y * y;
            z = -0.02f * y;
        }

        /// <summary>The treehouse deck (props.py <c>build_maple</c>): 2.6 m square at 2.4 m, centred on the trunk lean.</summary>
        public const float DeckTop = 2.4f;
        public const float DeckHalf = 1.3f;
        public const float MapleForkY = 4.2f;

        /// <summary>Rail and corner posts on the deck's -Z and +X edges (kit rail 0.83 m, posts 0.95 m).</summary>
        public const float DeckRailHeight = 0.95f;

        // --- Boundary ----------------------------------------------------------------------------------------

        /// <summary>Inner faces of the boundary walls: the outer edge of the boundary hedge colliders and the ground.</summary>
        public const float WallInnerHalfX = 22f;
        public const float WallInnerHalfZ = 18f;
        public const float WallThickness = 1f;
        public const float WallBottom = -2f;

        /// <summary>
        /// Wall top. Highest standable top in the block (the owl house chimney, 7.4 m) plus the best chain jump
        /// (2.8 m) plus Pip (1.1 m) is 11.3 m; a glide only descends, so 14 m needs no ceiling cap.
        /// </summary>
        public const float WallTop = 14f;

        /// <summary>Greybox boundary hedge collider height under the street set: just under the hedge models' 1.29 m top.</summary>
        public const float BoundaryHedgeColliderHeight = 1.25f;

        /// <summary>Ground under the whole lawn ring (EnvironmentLayout.LawnMinX..LawnMaxZ), top at y = 0.</summary>
        public const float GroundRingThickness = 0.5f;

        /// <summary>Invisible walls closing the block (camera and ground probes ignore them).</summary>
        public static readonly ColliderPart[] BoundaryWalls = BuildWalls();

        /// <summary>Ground strips under the lawn ring outside the 44 × 36 m block ground (no overlap with it).</summary>
        public static readonly ColliderPart[] GroundRing = BuildGroundRing();

        // --- The audit table ---------------------------------------------------------------------------------

        public static readonly ColliderRecipe[] Table = BuildTable();

        private static readonly Dictionary<string, ColliderRecipe> ByModel = Index(Table);

        public static bool TryGet(string model, out ColliderRecipe recipe) =>
            ByModel.TryGetValue(model ?? string.Empty, out recipe);

        /// <summary>
        /// True when the placement blocks Pip: by its recipe, or (no recipe) by its own <see cref="PlacementCollider"/>.
        /// </summary>
        public static bool IsSolid(in Placement placement) =>
            TryGet(placement.Model, out var recipe) ? recipe.Solid : placement.Solid;

        /// <summary>True when the placement's collision is decided: it has a recipe or its own collider kind.</summary>
        public static bool IsAudited(in Placement placement) => TryGet(placement.Model, out _) || placement.Solid;

        /// <summary>Every environment placement the dressing spawns (house, street, bats and the street set).</summary>
        public static IEnumerable<Placement> PlacedModels()
        {
            var tables = new[]
            {
                EnvironmentLayout.House, EnvironmentLayout.Street, EnvironmentLayout.PaperBats,
                EnvironmentLayout.LawnTiles, EnvironmentLayout.PathTiles, EnvironmentLayout.SidewalkTiles,
                EnvironmentLayout.BoundaryHedges, EnvironmentLayout.NeighbourHouses, EnvironmentLayout.LeafScatters,
            };
            foreach (var table in tables)
            {
                foreach (var p in table)
                {
                    yield return p;
                }
            }
        }

        /// <summary>Highest standable top of any recipe part that stands inside the block.</summary>
        public const float HighestStandableTop = 7.4f;

        private static ColliderRecipe[] BuildTable()
        {
            const float nan = float.NaN;
            MapleTrunkAt(MapleForkY, out float forkX, out float forkZ);
            MapleTrunkAt(DeckTop, out float deckX, out float deckZ);
            float x0 = deckX - DeckHalf;
            float x1 = deckX + DeckHalf;
            float z0 = deckZ - DeckHalf;
            float z1 = deckZ + DeckHalf;
            float railTop = DeckTop + DeckRailHeight;

            // Maple limb stubs: maple.py LIMBS first nodes (Blender x, y, z -> model x, z, y), stretched 1.5 times
            // along their heading to cover the limb bases above the deck.
            float[,] limbs = { { -0.62f, 0.62f, 0.05f }, { 0.6f, 0.62f, -0.04f }, { -0.24f, 0.78f, 0.08f }, { 0.3f, 0.72f, -0.08f } };
            var maple = new List<ColliderPart>
            {
                ColliderPart.Capsule("TrunkLow", 0f, 0.45f, 0f, deckX, DeckTop, deckZ, 0.42f),
                ColliderPart.Capsule("TrunkHigh", deckX, DeckTop, deckZ, forkX, MapleForkY + 0.1f, forkZ, 0.3f),
            };
            for (int i = 0; i < limbs.GetLength(0); i++)
            {
                maple.Add(ColliderPart.Capsule("Limb" + i, forkX, MapleForkY, forkZ, forkX + 1.5f * limbs[i, 0],
                    MapleForkY + 1.5f * limbs[i, 1], forkZ + 1.5f * limbs[i, 2], 0.14f));
            }

            return new[]
            {
                // Owl house: body, roof steps, porch deck, steps and rails come from EnvironmentDressing's house
                // boxes (kept for the interior wiring); these add what the model shows above them.
                new ColliderRecipe(EnvironmentLayout.OwlHouse, true,
                    "house boxes (EnvironmentDressing) + porch roof slope, ridge cap, front peak and chimney",
                    // Porch roof: 2.9 m at the front eave (z -4.72) to 3.3 m against the wall, x ±3.26.
                    ColliderPart.Box("PorchRoof", 0f, 3.0f, -3.6f, 6.6f, 0.25f, 2.35f, -10f),
                    // Ridge above the high roof step (5.8 m) up to the 6.4 m ridge, behind the front gable peak.
                    ColliderPart.Span("RidgeCap", -0.55f, 5.8f, -0.5f, 0.55f, 6.45f, 4.3f),
                    // Front gable peak under the owl puppet and the wind wheel tower.
                    ColliderPart.Span("FrontPeak", -0.55f, 5.8f, -2.45f, 0.55f, 7.0f, -0.5f),
                    ColliderPart.Span("Chimney", 0.95f, 4.0f, 1.9f, 2.05f, 7.4f, 3.0f)),
                new ColliderRecipe(EnvironmentLayout.OwlHouseDressing, true,
                    "porch clutter and the pumpkins beside the house steps (stairs and door stay clear)",
                    ColliderPart.Span("PorchClutterWest", -1.8f, 0.6f, -3.05f, -1.2f, 1.45f, -2.0f),
                    ColliderPart.Span("PorchClutterEast", 0.95f, 0.6f, -2.8f, 1.55f, 1.0f, -2.0f),
                    ColliderPart.Span("StepPumpkinsWest", -1.8f, 0f, -5.3f, -1.2f, 0.6f, -4.5f),
                    ColliderPart.Span("StepPumpkinsEast", 1.0f, 0f, -5.05f, 1.5f, 0.45f, -4.5f)),

                // Arrival porch kit: the deck and steps stand on the greybox porch footing.
                new ColliderRecipe(EnvironmentLayout.KitFloor, true, "greybox ArrivalPorch footing").Covered("ArrivalPorch"),
                new ColliderRecipe(EnvironmentLayout.KitSteps, true, "greybox PorchStep footings").Covered("PorchStep0"),
                new ColliderRecipe(EnvironmentLayout.KitRail, true, "mesh bounds (1.0 m rail)", ColliderPart.Fitted("Rail")),
                new ColliderRecipe(EnvironmentLayout.Crate, true, "mesh bounds (0.6 m cube, standable)", ColliderPart.Fitted("Crate")),
                new ColliderRecipe(EnvironmentLayout.CandyBowl, true, "mesh bounds (on the crate)", ColliderPart.Fitted("Bowl")),

                new ColliderRecipe(EnvironmentLayout.Pumpkin, true, "mesh bounds", ColliderPart.Fitted("Pumpkin")),
                new ColliderRecipe(EnvironmentLayout.JackOLantern, true, "mesh bounds", ColliderPart.Fitted("Lantern")),
                new ColliderRecipe(EnvironmentLayout.Mailbox, true, "mesh bounds (post and box)", ColliderPart.Fitted("Mailbox")),
                new ColliderRecipe(EnvironmentLayout.StreetLamp, true, "pole + arm and lantern head (standable at 3 m)",
                    ColliderPart.Box("Pole", 0f, 1.65f, -0.05f, 0.3f, 3.3f, 0.3f),
                    ColliderPart.Span("Head", 0.1f, 2.2f, -0.35f, 1.07f, 3.0f, 0.35f)),

                new ColliderRecipe(EnvironmentLayout.Maple, true, "trunk capsules up to the fork + four limb bases",
                    maple.ToArray()),
                new ColliderRecipe(EnvironmentLayout.TreehousePlatform, true,
                    "deck floor at 2.4 m + rails on its -Z and +X edges (the open -X edge meets the shed roof)",
                    ColliderPart.Span("Deck", x0 - 0.04f, DeckTop - 0.15f, z0 - 0.02f, x1 + 0.04f, DeckTop, z1 + 0.02f),
                    ColliderPart.Span("RailFront", x0 + 0.01f, DeckTop, z0 - 0.02f, x1 + 0.04f, railTop, z0 + 0.12f),
                    ColliderPart.Span("RailSide", x1 - 0.1f, DeckTop, z0 - 0.02f, x1 + 0.04f, railTop, z1 - 0.01f)),

                new ColliderRecipe(EnvironmentLayout.Bench, true,
                    "seat (0.56 m) + backrest (0.94 m) + Mr. Vale's seated body at local x +0.28",
                    ColliderPart.Span("Seat", -0.75f, 0f, -0.3f, 0.75f, 0.56f, 0.2f),
                    ColliderPart.Span("Back", -0.75f, 0f, 0.18f, 0.75f, 0.94f, 0.38f),
                    ColliderPart.Span("ValeSeated", EnvironmentLayout.ValeBenchOffsetX - 0.27f, 0.56f, -0.3f,
                        EnvironmentLayout.ValeBenchOffsetX + 0.27f, 1.35f, 0.2f)),
                new ColliderRecipe(EnvironmentLayout.PartyTable, true, "table top (0.72 m) + the party things on it",
                    ColliderPart.Fitted("Table", nan, 0.72f),
                    ColliderPart.Span("TableThings", -0.65f, 0f, -0.35f, 0.65f, 1.0f, 0.25f)),

                new ColliderRecipe(EnvironmentLayout.Hedge, true, "mesh bounds (1.24 m, standable top)", ColliderPart.Fitted("Hedge")),
                new ColliderRecipe(EnvironmentLayout.FenceSection, true, "mesh bounds (1.28 m picket top)", ColliderPart.Fitted("Fence")),
                new ColliderRecipe(EnvironmentLayout.FenceGate, true, "the two gate posts (the open leaf and the opening stay clear)",
                    ColliderPart.Box("PostWest", -0.55f, 0.64f, 0.03f, 0.14f, 1.28f, 0.16f),
                    ColliderPart.Box("PostEast", 0.55f, 0.64f, 0.03f, 0.14f, 1.28f, 0.16f)),
                new ColliderRecipe(EnvironmentLayout.BoundaryHedgeLong, true,
                    "mesh bounds (1.29 m standable top); greybox hedge lines and boundary walls behind",
                    ColliderPart.Fitted("Hedge")),
                new ColliderRecipe(EnvironmentLayout.NeighbourHouseA, true, "mesh bounds (outside the walls)", ColliderPart.Fitted("House")),
                new ColliderRecipe(EnvironmentLayout.NeighbourHouseB, true, "mesh bounds (outside the walls)", ColliderPart.Fitted("House")),
                new ColliderRecipe(EnvironmentLayout.NeighbourHouseC, true, "mesh bounds (outside the walls)", ColliderPart.Fitted("House")),

                // Route kit pieces reused by the skilled ascent (placed by the route wiring).
                new ColliderRecipe(Awning, true, "shed wall slice + walkable canvas slope 1.35 -> 2.1 m",
                    ColliderPart.Span("Wall", -1.43f, 0f, -0.1f, 1.43f, 2.55f, 0.2f),
                    ColliderPart.Box("Canvas", 0f, 1.74f, -0.75f, 2.26f, 0.06f, 1.7f, -26.6f)),

                // Interior wiring (CaseRouteLayout): these models are carried by the authored slab colliders that
                // OwlHouseStage builds (ramp legs, shed, bridge, cabin walls, the hollow house shell, the partition).
                new ColliderRecipe(CaseRouteLayout.ShedRamp, true, "CaseRouteLayout ramp, landing and shed slabs")
                    .Covered("Ramp_Leg1"),
                new ColliderRecipe(CaseRouteLayout.SillBridge, true, "CaseRouteLayout bridge slab and guards").Covered("Bridge"),
                new ColliderRecipe(CaseRouteLayout.TreehouseCabin, true,
                    "CaseRouteLayout cabin walls (the deck is the TreehousePlatform recipe)").Covered("Cabin_North"),
                new ColliderRecipe(CaseRouteLayout.OwlHouseInterior, true,
                    "CaseRouteLayout shell, floor, stair, loft and rails").Covered("House_Floor"),
                new ColliderRecipe(CaseRouteLayout.WorkshopMechanism, true,
                    "behind the CaseRouteLayout workshop partition").Covered("Workshop_Partition"),
                new ColliderRecipe(CaseRouteLayout.ClothSnag, false, "decoration (evidence scrap on the bridge-head post)"),

                // Decoration: Pip and the camera pass through.
                new ColliderRecipe(EnvironmentLayout.PaperBat, false, "decoration (hangs in the air)"),
                new ColliderRecipe(EnvironmentLayout.Lawn, false, "ground decal; the greybox Ground and the ground ring carry Pip"),
                new ColliderRecipe(EnvironmentLayout.PathStones, false, "ground decal over the greybox Path"),
                new ColliderRecipe(EnvironmentLayout.Sidewalk, false, "4.5 cm slabs; Pip walks them on the ground"),
                new ColliderRecipe(EnvironmentLayout.LeafScatterA, false, "leaves"),
                new ColliderRecipe(EnvironmentLayout.LeafScatterB, false, "leaves"),
                new ColliderRecipe(EnvironmentLayout.LeafScatterC, false, "leaves"),
            };
        }

        /// <summary>The awning route piece (tools/blender/env/props.py build_awning).</summary>
        public const string Awning = "Awning";

        private static Dictionary<string, ColliderRecipe> Index(ColliderRecipe[] table)
        {
            var index = new Dictionary<string, ColliderRecipe>(StringComparer.Ordinal);
            foreach (var recipe in table)
            {
                index.Add(recipe.Model, recipe);
            }

            return index;
        }

        private static ColliderPart[] BuildWalls()
        {
            float x = WallInnerHalfX;
            float z = WallInnerHalfZ;
            float t = WallThickness;
            return new[]
            {
                ColliderPart.Span("Wall_North", -x - t, WallBottom, z, x + t, WallTop, z + t),
                ColliderPart.Span("Wall_South", -x - t, WallBottom, -z - t, x + t, WallTop, -z),
                ColliderPart.Span("Wall_West", -x - t, WallBottom, -z - t, -x, WallTop, z + t),
                ColliderPart.Span("Wall_East", x, WallBottom, -z - t, x + t, WallTop, z + t),
            };
        }

        private static ColliderPart[] BuildGroundRing()
        {
            float y0 = -GroundRingThickness;
            float minX = EnvironmentLayout.LawnMinX;
            float maxX = EnvironmentLayout.LawnMaxX;
            float minZ = EnvironmentLayout.LawnMinZ;
            float maxZ = EnvironmentLayout.LawnMaxZ;
            float bx = EnvironmentLayout.BlockHalfX;
            float bz = EnvironmentLayout.BlockHalfZ;
            var ring = new List<ColliderPart>();
            if (maxZ > bz)
            {
                ring.Add(ColliderPart.Span("GroundRing_North", minX, y0, bz, maxX, 0f, maxZ));
            }

            if (minZ < -bz)
            {
                ring.Add(ColliderPart.Span("GroundRing_South", minX, y0, minZ, maxX, 0f, -bz));
            }

            float z0 = Math.Max(minZ, -bz);
            float z1 = Math.Min(maxZ, bz);
            ring.Add(ColliderPart.Span("GroundRing_West", minX, y0, z0, -bx, 0f, z1));
            ring.Add(ColliderPart.Span("GroundRing_East", bx, y0, z0, maxX, 0f, z1));
            return ring.ToArray();
        }
    }
}
