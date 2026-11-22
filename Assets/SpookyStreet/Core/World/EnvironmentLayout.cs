using System;

namespace SpookyStreet.Core.World
{
    /// <summary>How a placed model blocks Pip (and the camera boom).</summary>
    public enum PlacementCollider
    {
        /// <summary>Decoration only: Pip and the camera pass through.</summary>
        None,

        /// <summary>A box fitted to the model's mesh bounds in its own frame.</summary>
        Bounds,

        /// <summary>A slim box around a tree trunk (the crown stays open).</summary>
        Trunk,

        /// <summary>A slim box around a lamp pole (the arm and lantern stay open).</summary>
        Pole,

        /// <summary>An upright capsule around a standing character.</summary>
        Capsule,
    }

    /// <summary>
    /// One original model in the street, in Unity world space (metres, north is +Z, origin at the plaza, yaw in
    /// degrees about +Y). <see cref="HalfX"/>/<see cref="HalfZ"/> are a world-aligned footprint used only by the
    /// route and density checks; the runtime fits real colliders to the meshes.
    /// </summary>
    public readonly struct Placement
    {
        public Placement(string model, float x, float y, float z, float yaw = 0f, float scale = 1f,
            PlacementCollider collider = PlacementCollider.None, float halfX = 0.25f, float halfZ = 0.25f,
            bool detail = true)
        {
            Model = model;
            X = x;
            Y = y;
            Z = z;
            Yaw = yaw;
            Scale = scale;
            Collider = collider;
            HalfX = halfX;
            HalfZ = halfZ;
            Detail = detail;
        }

        public string Model { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float Yaw { get; }
        public float Scale { get; }
        public PlacementCollider Collider { get; }
        public float HalfX { get; }
        public float HalfZ { get; }

        /// <summary>Counts as a "meaningful detail" for the spec's density rule (one every ~1.5 m of path).</summary>
        public bool Detail { get; }

        public bool Solid => Collider != PlacementCollider.None;
    }

    /// <summary>
    /// Where the original environment models stand around the arrival route (spec §4 map; composition from the
    /// approved key frame docs/evidence/s000.2/env_keyframe_v2.png): arrival porch with the candy bowl, a path
    /// lined with pumpkins, mailbox and street lamp, the plaza maple, bench and party table, the front-yard hedge,
    /// gate and picket fence, and the owl house with the side-yard maple and treehouse. Model names match the FBX
    /// files in <c>Resources/Models/Environment</c>. Pure data so EditMode tests can prove the mandatory route stays
    /// open and the dressing stays dense.
    /// </summary>
    public static class EnvironmentLayout
    {
        // Model names (FBX file names).
        public const string OwlHouse = "OwlHouse";
        public const string OwlHouseDressing = "OwlHouseDressing";
        public const string CandyBowl = "CandyBowl";
        public const string Crate = "Crate";
        public const string KitFloor = "Kit_Floor";
        public const string KitSteps = "Kit_Steps";
        public const string KitRail = "Kit_Rail";
        public const string Pumpkin = "Pumpkin";
        public const string JackOLantern = "JackOLantern";
        public const string Mailbox = "Mailbox";
        public const string StreetLamp = "StreetLamp";
        public const string Maple = "Maple";
        public const string TreehousePlatform = "TreehousePlatform";
        public const string Bench = "Bench";
        public const string PartyTable = "PartyTable";
        public const string Hedge = "Hedge";
        public const string FenceGate = "FenceGate";
        public const string FenceSection = "FenceSection";
        public const string PaperBat = "PaperBat";

        // Named children inside the models (export report, tools/blender/env/env_manifest.json).
        public const string AtticWindowGlow = "AtticWindow_Glow";
        public const string WindWheel = "WindWheel";
        public const string OwlPuppet = "OwlPuppet";
        public const string FrontDoor = "FrontDoor";
        public const string Gate = "Gate";
        public const string LampGlow = "Lamp_Glow";

        /// <summary>The greybox house front (z = 11) plus 2 m: the owl house origin is the base centre of body + porch.</summary>
        public const float HouseX = 0f;
        public const float HouseZ = 13f;
        public const float HouseYaw = 0f; // the model's front faces -Z (toward the arrival porch) with no yaw

        /// <summary>Front-yard line: hedge, gate and picket fence.</summary>
        public const float FrontYardZ = 5f;

        /// <summary>Gate leaf swung into the yard (degrees about the hinge), so the opening reads as an invitation.</summary>
        public const float GateOpenYaw = -100f;

        // Mandatory route (spec §4): arrival steps → path → plaza → gate → owl house steps, along x = 0.
        public const float RouteStartZ = -12.4f;
        public const float RouteEndZ = 8.3f;

        /// <summary>Path half-width (the greybox path is 1.6 m wide).</summary>
        public const float PathHalfWidth = 0.8f;

        /// <summary>
        /// Half-width that no solid placement may intrude on: 1.2 m of clear passage, more than twice Pip's
        /// 0.56 m capsule, so the route never needs precision.
        /// </summary>
        public const float MandatoryRouteHalfWidth = 0.6f;

        /// <summary>Details count toward path density when they are this close to the route's centre line.</summary>
        public const float DetailReach = 3f;

        /// <summary>Largest allowed gap between details along the route (spec v1.3: a detail every ~1.5 m).</summary>
        public const float MaxDetailGap = 2f;

        /// <summary>Owl house and its Halloween dressing share one origin.</summary>
        public static readonly Placement[] House =
        {
            new Placement(OwlHouse, HouseX, 0f, HouseZ, HouseYaw, detail: false),
            new Placement(OwlHouseDressing, HouseX, 0f, HouseZ, HouseYaw, detail: false),
        };

        /// <summary>Everything else, south (arrival) to north (owl house).</summary>
        public static readonly Placement[] Street =
        {
            // Arrival porch (0, -13; 0.6 m): kit deck, steps down toward the plaza, side rails, and the candy bowl
            // on a crate at the front-left corner (the opening shot's foreground, spec §1). Kit pieces face -Z
            // natively, so yaw 180 turns their fronts toward the plaza.
            new Placement(KitFloor, -1f, 0f, -13f, 180f, detail: false),
            new Placement(KitFloor, 1f, 0f, -13f, 180f, detail: false),
            new Placement(KitSteps, 0f, 0f, -13f, 180f, halfX: 0.95f, halfZ: 0.36f),
            new Placement(KitRail, -1.95f, 0.6f, -14f, 90f, collider: PlacementCollider.Bounds, halfX: 0.1f, halfZ: 1.06f),
            new Placement(KitRail, 1.95f, 0.6f, -14f, 90f, collider: PlacementCollider.Bounds, halfX: 0.1f, halfZ: 1.06f),
            new Placement(Crate, -1.4f, 0.6f, -13.45f, 15f, collider: PlacementCollider.Bounds, halfX: 0.4f, halfZ: 0.4f),
            new Placement(CandyBowl, -1.4f, 1.21f, -13.45f, -20f, halfX: 0.3f, halfZ: 0.3f),

            // Path edge: a detail every ~1.5 m, alternating sides, never on the path.
            new Placement(Pumpkin, 1.35f, 0f, -11.7f, 0f, collider: PlacementCollider.Bounds, halfX: 0.2f, halfZ: 0.2f),
            new Placement(JackOLantern, 1.8f, 0f, -11.25f, -15f, collider: PlacementCollider.Bounds, halfX: 0.35f, halfZ: 0.35f),
            new Placement(Mailbox, -1.45f, 0f, -10.6f, 12f, collider: PlacementCollider.Bounds, halfX: 0.2f, halfZ: 0.3f),
            new Placement(StreetLamp, 2.2f, 0f, -9.6f, 0f, collider: PlacementCollider.Pole, halfX: 0.2f, halfZ: 0.2f),
            new Placement(JackOLantern, -1.5f, 0f, -8.2f, 20f, collider: PlacementCollider.Bounds, halfX: 0.35f, halfZ: 0.35f),
            new Placement(Pumpkin, 1.45f, 0f, -6.9f, 40f, collider: PlacementCollider.Bounds, halfX: 0.2f, halfZ: 0.2f),
            new Placement(Pumpkin, -1.35f, 0f, -5.4f, 0f, collider: PlacementCollider.Bounds, halfX: 0.2f, halfZ: 0.2f),
            new Placement(Pumpkin, -1.8f, 0f, -5.0f, 70f, 0.8f, PlacementCollider.Bounds, 0.16f, 0.16f),
            new Placement(JackOLantern, 1.5f, 0f, -3.9f, -10f, collider: PlacementCollider.Bounds, halfX: 0.35f, halfZ: 0.35f),
            new Placement(Pumpkin, -1.4f, 0f, -2.4f, 25f, collider: PlacementCollider.Bounds, halfX: 0.2f, halfZ: 0.2f),

            // Plaza (0, 0): the maple breaks the space asymmetrically; bench (+8, 0) turned toward the owl house
            // door (Vale's viewpoint); a party table between them closes the east side without blocking it.
            new Placement(Maple, -4.5f, 0f, 2f, 160f, collider: PlacementCollider.Trunk, halfX: 0.4f, halfZ: 0.4f),
            new Placement(Pumpkin, 1.55f, 0f, -0.9f, 10f, collider: PlacementCollider.Bounds, halfX: 0.2f, halfZ: 0.2f),
            new Placement(JackOLantern, -1.5f, 0f, 0.8f, 25f, collider: PlacementCollider.Bounds, halfX: 0.35f, halfZ: 0.35f),
            new Placement(PartyTable, 4.6f, 0f, -2.6f, -15f, collider: PlacementCollider.Bounds, halfX: 0.9f, halfZ: 0.6f),
            new Placement(Bench, BenchX, 0f, BenchZ, BenchYaw, collider: PlacementCollider.Bounds, halfX: 0.75f, halfZ: 0.75f),
            new Placement(Pumpkin, 1.45f, 0f, 2.4f, -30f, collider: PlacementCollider.Bounds, halfX: 0.2f, halfZ: 0.2f),

            // Front yard (z = 5): hedge west of the gate, the gate swung open on the path, picket fence east. The two
            // pumpkins in front of the fence stand on the sidewalk slabs.
            new Placement(JackOLantern, 1.45f, SidewalkTop, 4.2f, -12f, collider: PlacementCollider.Bounds, halfX: 0.35f, halfZ: 0.35f),
            new Placement(Pumpkin, 2.3f, SidewalkTop, 4.35f, 15f, collider: PlacementCollider.Bounds, halfX: 0.2f, halfZ: 0.2f),
            new Placement(Hedge, -1.97f, 0f, FrontYardZ, 0f, collider: PlacementCollider.Bounds, halfX: 1.17f, halfZ: 0.48f),
            new Placement(FenceGate, -0.1f, 0f, FrontYardZ, 0f, halfX: 0.61f, halfZ: 0.1f),
            new Placement(FenceSection, 1.7f, 0f, FrontYardZ, 0f, collider: PlacementCollider.Bounds, halfX: 1.08f, halfZ: 0.1f),
            new Placement(FenceSection, 3.8f, 0f, FrontYardZ, 0f, collider: PlacementCollider.Bounds, halfX: 1.08f, halfZ: 0.1f),
            new Placement(FenceSection, 5.9f, 0f, FrontYardZ, 0f, collider: PlacementCollider.Bounds, halfX: 1.08f, halfZ: 0.1f),
            new Placement(FenceSection, 8f, 0f, FrontYardZ, 0f, collider: PlacementCollider.Bounds, halfX: 1.08f, halfZ: 0.1f),

            // Side-yard hedge corridor (spec §4 side yard, -10, +8), replacing the greybox block at (-6, 7).
            new Placement(Hedge, -4.17f, 0f, 7f, 0f, collider: PlacementCollider.Bounds, halfX: 1.17f, halfZ: 0.48f),
            new Placement(Hedge, -6.5f, 0f, 7f, 180f, collider: PlacementCollider.Bounds, halfX: 1.17f, halfZ: 0.48f),
            new Placement(Hedge, -8.83f, 0f, 7f, 0f, collider: PlacementCollider.Bounds, halfX: 1.17f, halfZ: 0.48f),

            // Maple with the treehouse deck (2.4 m) toward the shed and treehouse (spec §4, -8, +13). The
            // platform shares the maple's origin. No ramp yet, so the deck has no collider and nothing invites
            // a climb from below.
            new Placement(Maple, -9.5f, 0f, 12f, 200f, collider: PlacementCollider.Trunk, halfX: 0.4f, halfZ: 0.4f),
            new Placement(TreehousePlatform, -9.5f, 0f, 12f, 200f, detail: false),
        };

        // --- Witnesses of the first case (S000.5): Bea with her bicycle, Mr. Vale on the bench -----------------

        // Model names (FBX file names): the characters in Resources/Models/Characters/<Name>, the bicycle with the
        // environment models.
        public const string Bea = "Bea";
        public const string Vale = "Vale";
        public const string BeaBicycle = "BeaBicycle";

        /// <summary>
        /// Character and environment models face -Z in their own frame (Blender -Y, the same import as Pip), so a
        /// model at yaw Y faces the Unity yaw Y + 180.
        /// </summary>
        public const float ModelFacingOffset = 180f;

        /// <summary>The plaza bench (spec §4: +8, 0), turned toward the owl house door (Mr. Vale's viewpoint).</summary>
        public const float BenchX = 8f;
        public const float BenchZ = 0f;
        public const float BenchYaw = 144f;

        /// <summary>Seat height of Bench.fbx; Mr. Vale's seated clips put his pelvis on it.</summary>
        public const float BenchSeatHeight = 0.47f;

        /// <summary>
        /// Mr. Vale sits this far along the bench's local X from its centre, clear of the cushion at -0.42 (the
        /// review's stand-in bench, tools/blender/export/witness_review.py). His root stands on the ground under the
        /// seat with the bench's yaw; the clips carry the seated pelvis.
        /// </summary>
        public const float ValeBenchOffsetX = 0.28f;

        /// <summary>
        /// Bea west of the plaza (spec §4), between the maple and the arrival path, readable from both, well clear of
        /// the route. Facing about east toward the plaza so the point in her Talk clip (55 degrees to her left, 11
        /// up) lands on the owl house front and attic window.
        /// </summary>
        public const float BeaX = -7.2f;
        public const float BeaZ = -1.3f;
        public const float BeaFacingYaw = 85f;

        /// <summary>Where Bea points in Talk, relative to her facing (negative = to her left).</summary>
        public const float BeaPointYaw = -55f;

        /// <summary>Where Mr. Vale points in SitTalk, relative to his facing (about straight ahead, slightly left).</summary>
        public const float ValePointYaw = -6f;

        /// <summary>
        /// The bicycle beside Bea as reviewed (witness_review.py BIKE_AT, BIKE_YAW): 0.86 m to her right and 0.36 m
        /// behind her, turned 66 degrees so the front wheel and basket come round to her right hand.
        /// </summary>
        public const float BikeRight = 0.86f;
        public const float BikeBehind = 0.36f;
        public const float BikeYawRelative = -66f;

        /// <summary>Bicycle footprint (local X across the handlebar, local Z along the 1.26 m wheelbase).</summary>
        public const float BikeWidth = 0.6f;
        public const float BikeLength = 1.3f;

        /// <summary>Radius and height of Bea's capsule collider.</summary>
        public const float WitnessRadius = 0.25f;
        public const float WitnessHeight = 1.4f;

        /// <summary>Bea, her bicycle and Mr. Vale. Yaw is model yaw (as every placement): facing is yaw + 180.</summary>
        public static readonly Placement[] Witnesses = BuildWitnesses();

        /// <summary>
        /// World position of a point given in a model's own frame (<paramref name="localX"/>, <paramref name="localZ"/>)
        /// for a model at (<paramref name="x"/>, <paramref name="z"/>) turned by <paramref name="yaw"/> (Unity yaw).
        /// </summary>
        public static void LocalToWorld(float x, float z, float yaw, float localX, float localZ, out float worldX,
            out float worldZ)
        {
            double r = yaw * Math.PI / 180.0;
            float c = (float)Math.Cos(r);
            float s = (float)Math.Sin(r);
            worldX = x + localX * c + localZ * s;
            worldZ = z - localX * s + localZ * c;
        }

        private static Placement[] BuildWitnesses()
        {
            float beaYaw = BeaFacingYaw - ModelFacingOffset;
            // In the model's own frame the character faces -Z with her right toward -X, so the bicycle (to her
            // right, behind her) is at (-right, +behind).
            LocalToWorld(BeaX, BeaZ, beaYaw, -BikeRight, BikeBehind, out float bikeX, out float bikeZ);
            float bikeYaw = beaYaw + BikeYawRelative;
            Footprint(BikeWidth, BikeLength, bikeYaw, out float bikeHalfX, out float bikeHalfZ);
            LocalToWorld(BenchX, BenchZ, BenchYaw, ValeBenchOffsetX, 0f, out float valeX, out float valeZ);
            return new[]
            {
                new Placement(Bea, BeaX, 0f, BeaZ, beaYaw, collider: PlacementCollider.Capsule,
                    halfX: WitnessRadius, halfZ: WitnessRadius, detail: false),
                new Placement(BeaBicycle, bikeX, 0f, bikeZ, bikeYaw, collider: PlacementCollider.Bounds,
                    halfX: bikeHalfX, halfZ: bikeHalfZ, detail: false),
                // Covered by the bench's collider.
                new Placement(Vale, valeX, 0f, valeZ, BenchYaw, halfX: 0.3f, halfZ: 0.3f, detail: false),
            };
        }

        /// <summary>
        /// Folded paper bats as decoration (not yet collectibles): circling the street lamp, under the owl house
        /// porch eave and in the plaza maple. Y is the bat's centre.
        /// </summary>
        public static readonly Placement[] PaperBats =
        {
            new Placement(PaperBat, 3.35f, 3.1f, -9.3f, -20f, detail: false),
            new Placement(PaperBat, 2.45f, 3.35f, -9.95f, 25f, detail: false),
            new Placement(PaperBat, -2.1f, 2.45f, 8.6f, 15f, detail: false),
            new Placement(PaperBat, 2.35f, 2.3f, 8.7f, -25f, detail: false),
            new Placement(PaperBat, -2.6f, 3.0f, -1.6f, 30f, detail: false), // clear of the maple crown
            new Placement(PaperBat, -0.9f, 2.0f, 4.7f, -10f, detail: false),
        };

        // --- Street set (S000.4): ground, path, sidewalk, boundary hedges, neighbours, leaves -------------------

        // Model names (FBX file names, tools/blender/env/street_manifest.json).
        public const string Lawn = "Lawn";
        public const string PathStones = "PathStones";
        public const string Sidewalk = "Sidewalk";
        public const string LeafScatterA = "LeafScatterA";
        public const string LeafScatterB = "LeafScatterB";
        public const string LeafScatterC = "LeafScatterC";
        public const string BoundaryHedgeLong = "BoundaryHedgeLong";
        public const string NeighbourHouseA = "NeighbourHouseA";
        public const string NeighbourHouseB = "NeighbourHouseB";
        public const string NeighbourHouseC = "NeighbourHouseC";

        /// <summary>Shared texture atlas of every street-set model except <see cref="Lawn"/> (Street_BaseColor...).</summary>
        public const string StreetAtlas = "Street";

        /// <summary>Every street-set model; all must be committed before the greybox ground and skyline step aside.</summary>
        public static readonly string[] StreetSetModels =
        {
            Lawn, PathStones, Sidewalk, LeafScatterA, LeafScatterB, LeafScatterC, BoundaryHedgeLong,
            NeighbourHouseA, NeighbourHouseB, NeighbourHouseC,
        };

        /// <summary>Playable block half-extents (spec §4: about 44 × 36 m around the plaza).</summary>
        public const float BlockHalfX = 22f;
        public const float BlockHalfZ = 18f;

        /// <summary>Lawn tile edge: the lawn's heights and paint repeat every 4 m, so tiles share one rotation.</summary>
        public const float LawnTile = 4f;
        public const float LawnYaw = 0f;

        /// <summary>Lawn grid extent (tile edges): the block plus a ring under the neighbours and the far edges.</summary>
        public const float LawnMinX = -30f;
        public const float LawnMaxX = 30f;
        public const float LawnMinZ = -18f;
        public const float LawnMaxZ = 34f;

        /// <summary>Path strip length along Z (1.6 m wide); strips interlock every 2 m at one rotation.</summary>
        public const float PathTile = 2f;
        public const float PathYaw = 0f;

        /// <summary>First strip starts under the arrival steps (hidden there) so the stones run right up to them.</summary>
        public const float PathStartZ = -13.5f;

        /// <summary>
        /// Laid stones stop here: the owl house dressing has its own baked stepping stones from about z = 7.35 to
        /// the porch steps, which stay uncovered.
        /// </summary>
        public const float PathEndZ = 6.6f;

        /// <summary>Sidewalk module: 2 × 2 m, running along X, kerb on its -Z (street, plaza and camera) side.</summary>
        public const float SidewalkTile = 2f;
        public const float SidewalkZ = 3.5f;
        public const float SidewalkYaw = 0f;
        public const float SidewalkHalfX = 21f;

        /// <summary>
        /// The sidewalk's bed lies at 6 mm and the lawn under it swells to 18 mm, so the run sits 15 mm up and its
        /// bed always covers the grass. Slab tops then reach <see cref="SidewalkTop"/>.
        /// </summary>
        public const float SidewalkLift = 0.015f;
        public const float SidewalkTop = 0.045f;
        public const float SidewalkFrontZ = SidewalkZ - SidewalkTile * 0.5f;
        public const float SidewalkBackZ = SidewalkZ + SidewalkTile * 0.5f;

        /// <summary>Boundary hedge module: 6 m long (along its local X), 0.8 m deep; butted at 6 m centres.</summary>
        public const float HedgeLength = 6f;
        public const float HedgeDepth = 0.8f;

        /// <summary>Hedge lines on the greybox hedge colliders (which stay and do the blocking).</summary>
        public const float HedgeLineZ = 17.5f;
        public const float HedgeLineX = 21.5f;

        /// <summary>Leaves rest on the sidewalk slabs (their lowest point is modelled just above the lawn).</summary>
        public const float LeafOnSidewalkY = SidewalkLift + 0.015f;

        /// <summary>Lawn tiles on a 4 m grid, all at <see cref="LawnYaw"/>, row by row from the south.</summary>
        public static readonly Placement[] LawnTiles = BuildLawn();

        /// <summary>Stepping-stone strips over the lawn along the route, skipping the sidewalk crossing.</summary>
        public static readonly Placement[] PathTiles = BuildPath();

        /// <summary>The sidewalk in front of the owl house's yard line, edge to edge of the block.</summary>
        public static readonly Placement[] SidewalkTiles = BuildSidewalk();

        /// <summary>Boundary hedges closing the four block edges (spec §4: visually close inaccessible alleys).</summary>
        public static readonly Placement[] BoundaryHedges = BuildHedges();

        /// <summary>
        /// Neighbour houses beyond the hedges (skyline only, never walkable), fronts turned toward the street and
        /// the arrival camera, clear of the owl house gable so the attic window keeps its negative space.
        /// </summary>
        public static readonly Placement[] NeighbourHouses =
        {
            Neighbour(NeighbourHouseC, -21.5f, 23f, -16f),
            Neighbour(NeighbourHouseB, -10.3f, 24.5f, -5f),
            Neighbour(NeighbourHouseA, 11.5f, 24f, 12f),
            Neighbour(NeighbourHouseC, 21.5f, 23.2f, 20f),
            Neighbour(NeighbourHouseA, 29f, 3f, 80f), // east side, beyond the bench
        };

        /// <summary>Fallen maple leaves beside the walk (on lawn or sidewalk, never on the path stones).</summary>
        public static readonly Placement[] LeafScatters =
        {
            Leaves(LeafScatterA, -1.7f, 0f, -9.3f, 30f),
            Leaves(LeafScatterB, 1.3f, 0f, -7.6f, 0f),
            Leaves(LeafScatterC, -2.6f, 0f, -3.2f, 70f),
            Leaves(LeafScatterA, 2.4f, 0f, -1.6f, -40f),
            Leaves(LeafScatterB, -2.4f, 0f, 1.4f, 20f),
            Leaves(LeafScatterA, -3.1f, LeafOnSidewalkY, 3.6f, 120f),
            Leaves(LeafScatterC, 5.6f, LeafOnSidewalkY, 3.4f, 5f),
            Leaves(LeafScatterB, 1.8f, 0f, 6.1f, 45f),
            Leaves(LeafScatterA, -6.6f, 0f, -0.6f, 200f),
            Leaves(LeafScatterB, 7.1f, 0f, 1.3f, -60f),
        };

        /// <summary>Model footprints (width along local X, depth along local Z) for the bounds checks.</summary>
        public static void Size(string model, out float width, out float depth)
        {
            switch (model)
            {
                case NeighbourHouseA: width = 8f; depth = 7.7f; break;
                case NeighbourHouseB: width = 10.5f; depth = 9f; break;
                case NeighbourHouseC: width = 7.8f; depth = 7.7f; break;
                case LeafScatterA: width = 1.3f; depth = 1.1f; break;
                case LeafScatterB: width = 0.8f; depth = 0.7f; break;
                case LeafScatterC: width = 2.3f; depth = 0.8f; break;
                case BoundaryHedgeLong: width = HedgeLength; depth = HedgeDepth; break;
                case Lawn: width = LawnTile; depth = LawnTile; break;
                case PathStones: width = PathHalfWidth * 2f; depth = PathTile; break;
                case Sidewalk: width = SidewalkTile; depth = SidewalkTile; break;
                default: width = 0.5f; depth = 0.5f; break;
            }
        }

        /// <summary>World-aligned half-extents of a <paramref name="width"/> × <paramref name="depth"/> rectangle turned by <paramref name="yaw"/>.</summary>
        public static void Footprint(float width, float depth, float yaw, out float halfX, out float halfZ)
        {
            double r = yaw * Math.PI / 180.0;
            float c = (float)Math.Abs(Math.Cos(r));
            float s = (float)Math.Abs(Math.Sin(r));
            halfX = 0.5f * (c * width + s * depth);
            halfZ = 0.5f * (s * width + c * depth);
        }

        /// <summary>True when the placement's footprint lies wholly outside the playable block.</summary>
        public static bool OutsideBlock(in Placement p) =>
            Math.Abs(p.X) - p.HalfX >= BlockHalfX || Math.Abs(p.Z) - p.HalfZ >= BlockHalfZ;

        /// <summary>True when two placements' world-aligned footprints overlap.</summary>
        public static bool Overlaps(in Placement a, in Placement b) =>
            Math.Abs(a.X - b.X) < a.HalfX + b.HalfX && Math.Abs(a.Z - b.Z) < a.HalfZ + b.HalfZ;

        private static Placement Sized(string model, float x, float y, float z, float yaw)
        {
            Size(model, out float width, out float depth);
            Footprint(width, depth, yaw, out float halfX, out float halfZ);
            return new Placement(model, x, y, z, yaw, halfX: halfX, halfZ: halfZ, detail: false);
        }

        private static Placement Neighbour(string model, float x, float z, float yaw) => Sized(model, x, 0f, z, yaw);

        private static Placement Leaves(string model, float x, float y, float z, float yaw) => Sized(model, x, y, z, yaw);

        private static Placement[] BuildLawn()
        {
            int columns = (int)Math.Round((LawnMaxX - LawnMinX) / LawnTile);
            int rows = (int)Math.Round((LawnMaxZ - LawnMinZ) / LawnTile);
            var tiles = new Placement[columns * rows];
            for (int j = 0; j < rows; j++)
            {
                for (int i = 0; i < columns; i++)
                {
                    tiles[j * columns + i] = Sized(Lawn, LawnMinX + LawnTile * (i + 0.5f), 0f,
                        LawnMinZ + LawnTile * (j + 0.5f), LawnYaw);
                }
            }

            return tiles;
        }

        private static Placement[] BuildPath()
        {
            var tiles = new System.Collections.Generic.List<Placement>();
            for (float z0 = PathStartZ; z0 + PathTile <= SidewalkFrontZ + 1e-3f; z0 += PathTile)
            {
                tiles.Add(Sized(PathStones, 0f, 0f, z0 + PathTile * 0.5f, PathYaw));
            }

            for (float z0 = SidewalkBackZ; z0 + PathTile <= PathEndZ + 1e-3f; z0 += PathTile)
            {
                tiles.Add(Sized(PathStones, 0f, 0f, z0 + PathTile * 0.5f, PathYaw));
            }

            return tiles.ToArray();
        }

        private static Placement[] BuildSidewalk()
        {
            int count = (int)Math.Round(2f * SidewalkHalfX / SidewalkTile);
            var tiles = new Placement[count];
            for (int i = 0; i < count; i++)
            {
                tiles[i] = Sized(Sidewalk, -SidewalkHalfX + SidewalkTile * (i + 0.5f), SidewalkLift, SidewalkZ,
                    SidewalkYaw);
            }

            return tiles;
        }

        private static Placement[] BuildHedges()
        {
            // North and south rows span x = -21..21; the west and east columns span z = -18..18 and close the
            // corners. Each hedge's clipped front (-Z in the model) faces into the block.
            var hedges = new System.Collections.Generic.List<Placement>();
            for (int i = 0; i < 7; i++)
            {
                float x = -18f + HedgeLength * i;
                hedges.Add(Sized(BoundaryHedgeLong, x, 0f, HedgeLineZ, 0f));
                hedges.Add(Sized(BoundaryHedgeLong, x, 0f, -HedgeLineZ, 180f));
            }

            for (int i = 0; i < 6; i++)
            {
                float z = -15f + HedgeLength * i;
                hedges.Add(Sized(BoundaryHedgeLong, -HedgeLineX, 0f, z, -90f));
                hedges.Add(Sized(BoundaryHedgeLong, HedgeLineX, 0f, z, 90f));
            }

            return hedges.ToArray();
        }

        /// <summary>True when a solid placement intrudes on the mandatory route's clear passage.</summary>
        public static bool BlocksRoute(in Placement placement)
        {
            if (!placement.Solid)
            {
                return false;
            }

            bool overlapsZ = placement.Z + placement.HalfZ > RouteStartZ && placement.Z - placement.HalfZ < RouteEndZ;
            bool overlapsX = placement.X + placement.HalfX > -MandatoryRouteHalfWidth &&
                             placement.X - placement.HalfX < MandatoryRouteHalfWidth;
            return overlapsZ && overlapsX;
        }

        /// <summary>True when a placement stands on the visible path (even decoration should not).</summary>
        public static bool OnPath(in Placement placement) =>
            placement.Y < 0.5f &&
            placement.Z + placement.HalfZ > RouteStartZ && placement.Z - placement.HalfZ < FrontYardZ - 0.5f &&
            placement.X + placement.HalfX > -PathHalfWidth && placement.X - placement.HalfX < PathHalfWidth;

        /// <summary>
        /// Largest distance along the route (z from <paramref name="startZ"/> to <paramref name="endZ"/>) without a
        /// detail within <see cref="DetailReach"/> of the centre line. Repeated models still count, as each marks
        /// a new spot on the walk.
        /// </summary>
        public static float LargestDetailGap(Placement[] placements, float startZ, float endZ)
        {
            if (placements == null)
            {
                throw new ArgumentNullException(nameof(placements));
            }

            var stops = new float[placements.Length + 2];
            int count = 0;
            stops[count++] = startZ;
            foreach (var p in placements)
            {
                if (p.Detail && Math.Abs(p.X) - p.HalfX <= DetailReach && p.Z >= startZ && p.Z <= endZ)
                {
                    stops[count++] = p.Z;
                }
            }

            stops[count++] = endZ;
            Array.Sort(stops, 0, count);
            float largest = 0f;
            for (int i = 1; i < count; i++)
            {
                largest = Math.Max(largest, stops[i] - stops[i - 1]);
            }

            return largest;
        }
    }
}
