using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.World
{
    /// <summary>What an authored collider slab is for.</summary>
    public enum SlabKind
    {
        /// <summary>Its top is a walkable surface (safe ground).</summary>
        Walk,

        /// <summary>A wall, rail or roof plate: blocks Pip, never stood on.</summary>
        Wall,
    }

    /// <summary>Which part of the stage a slab belongs to (the runtime groups and toggles them).</summary>
    public enum SlabGroup
    {
        /// <summary>Treehouse routes: ramp, shed, bridge, cabin, awning.</summary>
        Route,

        /// <summary>The owl house interior (D24.1: its own closed space away from the street): walls, floors, stair,
        /// loft, rails, partition, roof plates and furniture.</summary>
        Interior,

        /// <summary>Built elsewhere (the porch deck and the owl house's solid body by EnvironmentDressing; the awning
        /// from its CollisionRules recipe): listed so the route checks see it; not built by the case.</summary>
        Reference,
    }

    /// <summary>
    /// One authored box collider described by the centre line of its TOP face, from
    /// (<see cref="StartX"/>, <see cref="StartY"/>, <see cref="StartZ"/>) to the end point, <see cref="Width"/> wide
    /// (horizontal, across the line) and <see cref="Depth"/> thick below the top. A level start and end make a floor or
    /// a wall top; a rising line makes a ramp. Pure data so EditMode tests can prove slopes, widths and continuity.
    /// </summary>
    public readonly struct StageSlab
    {
        public StageSlab(string name, SlabKind kind, SlabGroup group, float startX, float startY, float startZ,
            float endX, float endY, float endZ, float width, float depth)
        {
            Name = name;
            Kind = kind;
            Group = group;
            StartX = startX;
            StartY = startY;
            StartZ = startZ;
            EndX = endX;
            EndY = endY;
            EndZ = endZ;
            Width = width;
            Depth = depth;
        }

        public string Name { get; }
        public SlabKind Kind { get; }
        public SlabGroup Group { get; }
        public float StartX { get; }
        public float StartY { get; }
        public float StartZ { get; }
        public float EndX { get; }
        public float EndY { get; }
        public float EndZ { get; }
        public float Width { get; }
        public float Depth { get; }

        /// <summary>Horizontal length of the centre line.</summary>
        public float Run
        {
            get
            {
                float dx = EndX - StartX;
                float dz = EndZ - StartZ;
                return (float)Math.Sqrt(dx * dx + dz * dz);
            }
        }

        public float Rise => EndY - StartY;

        /// <summary>Rise over run (0 for a level slab).</summary>
        public float Gradient => Run > 1e-4f ? Math.Abs(Rise) / Run : 0f;

        /// <summary>
        /// Height of the top face above the ground point (x, z), or false when the point is not over the slab
        /// (shrunk by <paramref name="margin"/> on every side).
        /// </summary>
        public bool TryTopAt(float x, float z, float margin, out float y)
        {
            y = 0f;
            float run = Run;
            if (run < 1e-4f)
            {
                return false;
            }

            float fx = (EndX - StartX) / run;
            float fz = (EndZ - StartZ) / run;
            float px = x - StartX;
            float pz = z - StartZ;
            float along = px * fx + pz * fz;
            float across = -px * fz + pz * fx;
            if (along < margin || along > run - margin || Math.Abs(across) > Width * 0.5f - margin)
            {
                return false;
            }

            y = StartY + Rise * (along / run);
            return true;
        }

        /// <summary>True when a vertical segment at (x, z) from <paramref name="y0"/> to <paramref name="y1"/>,
        /// inflated by <paramref name="radius"/>, touches this slab's volume.</summary>
        public bool Blocks(float x, float z, float y0, float y1, float radius)
        {
            float run = Run;
            if (run < 1e-4f)
            {
                return false;
            }

            float fx = (EndX - StartX) / run;
            float fz = (EndZ - StartZ) / run;
            float px = x - StartX;
            float pz = z - StartZ;
            float along = px * fx + pz * fz;
            float across = -px * fz + pz * fx;
            if (along < -radius || along > run + radius || Math.Abs(across) > Width * 0.5f + radius)
            {
                return false;
            }

            float t = Math.Max(0f, Math.Min(1f, along / run));
            float top = StartY + Rise * t;
            return y1 > top - Depth && y0 < top;
        }

        public override string ToString() => Name;
    }

    /// <summary>A point on a route (world metres) for the walkability checks.</summary>
    public readonly struct RoutePoint
    {
        public RoutePoint(string name, float x, float y, float z)
        {
            Name = name;
            X = x;
            Y = y;
            Z = z;
        }

        public string Name { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public override string ToString() => $"{Name} ({X:F2}, {Y:F2}, {Z:F2})";
    }


    /// <summary>One stretch of the mandatory route between thresholds: walked without a jump, it ends in the exit zone
    /// of <see cref="Exit"/> (or nowhere for the last leg); the next leg starts at that threshold's arrival point.</summary>
    public readonly struct RouteLeg
    {
        public RouteLeg(string name, ThresholdId exit, params RoutePoint[] points)
        {
            Name = name;
            Exit = exit;
            Points = points;
        }

        public string Name { get; }
        public ThresholdId Exit { get; }
        public RoutePoint[] Points { get; }

        public override string ToString() => Name;
    }

    /// <summary>
    /// The interior half of case 01 (S002 interior wiring, house v3): where the treehouse-route models, the owl house
    /// interior and the workshop mechanism stand, and every authored collider of the mandatory route (side yard → shed
    /// ramp → shed roof → bridge → June's window | attic → stair → hall → front door | porch) and of the skilled ascent
    /// (crate → awning → shed roof).
    /// <para>D24.1: the owl house is "bigger on the inside". The exterior keeps street scale and stays solid (its body
    /// boxes are EnvironmentDressing's); the rooms are a separate, closed model up the lane at
    /// <see cref="InteriorOriginZ"/>, outside the boundary walls, reached through the two thresholds
    /// (<see cref="Thresholds"/>). Interior builder space (interior.py: x across, y depth from the front wall, z up)
    /// maps to world as x = bx, y = bz, z = <see cref="InteriorFrontZ"/> + by. The ramp frame maps as x = rx − 9.5,
    /// z = ry + 12.</para>
    /// Pure data so EditMode tests prove the ramp is 1:5 and ≥ 1.6 m wide, every leg is continuous without jumps, the
    /// skilled steps fit the 1.0 m tap jump and the camera has room (spec §4, MAP-01, D24).
    /// </summary>
    public static class CaseRouteLayout
    {
        // Model names (FBX file names).
        public const string OwlHouseInterior = "OwlHouseInterior";
        public const string WorkshopMechanism = "WorkshopMechanism";
        public const string ShedRamp = "ShedRamp";
        public const string SillBridge = "SillBridge";
        public const string TreehouseCabin = "TreehouseCabin";
        public const string ClothSnag = "ClothSnag";
        public const string Awning = "Awning";

        /// <summary>Shared atlas of the four route models.</summary>
        public const string RoutesAtlas = "Routes";

        // Named children (tools/blender/env/env_manifest.json, interior_manifest.json).
        public const string OwlHouseBody = "OwlHouseBody";
        public const string Porch = "Porch";
        public const string DoorRecessCard = "DoorRecessCard";
        public const string SideWindowPane = "SideWindowPane";
        public const string Attic = "Attic";
        public const string NookWall = "NookWall";
        public const string InnerDoor = "InnerDoor";
        public const string DoorLatch = "DoorLatch";
        public const string SillSnag = "SillSnag";
        public const string StreetViewGlow = "StreetView_Glow";
        public const string RoomCardGlow = "RoomCard_Glow";
        public const string Cam = "Cam";
        public const string Follower = "Follower";
        public const string LinkArmSeated = "LinkArm_Seated";
        public const string LinkArmSlipped = "LinkArm_Slipped";
        public const string ServicePanel = "ServicePanel";
        public const string Crank = "Crank";

        // --- The owl house exterior on the street (unchanged by house v3; solid) --------------------------------

        /// <summary>World z of the exterior front wall (builder y = 0).</summary>
        public const float FrontZ = EnvironmentLayout.HouseZ - 2f;

        public const float OuterX = 3.1f;
        public const float BackZ = FrontZ + 6f;

        /// <summary>The exterior's solid body box (EnvironmentDressing): x ±3.05, z 11…17, 2.8 m high.</summary>
        public const float BodyHalfX = 3.05f;
        public const float BodyTopY = 2.8f;

        /// <summary>Porch deck and hall floor height (the same on both sides of the door).</summary>
        public const float FloorY = 0.6f;

        /// <summary>June's window on the exterior west wall: 1.0 m wide, sill 2.5 m, 1.45 m apex.</summary>
        public const float ExteriorWindowX = -3.045f;
        public const float ExteriorWindowZ = FrontZ + 3f;
        public const float ExteriorSillY = 2.5f;
        public const float WindowHalfWidth = 0.5f;
        public const float WindowSpring = 0.95f;
        public const float WindowApex = 1.45f;

        /// <summary>The front door opening (both sides): x ±0.57, head 2.62 m; the leaves hinge at x −0.55.</summary>
        public const float DoorHalfWidth = 0.57f;
        public const float DoorHingeX = -0.55f;
        public const float DoorTopY = 2.62f;

        // --- The owl house interior (D24.1: away from the street; interior.py frame) ----------------------------

        /// <summary>Where the interior model's origin stands: up the lane, 42 m beyond the north boundary wall (z 18),
        /// so the street never includes it and it never includes the street.</summary>
        public const float InteriorOriginX = 0f;

        public const float InteriorOriginZ = 60f;

        /// <summary>World z of the interior's builder y = 0 (the FBX origin sits at builder y = 2).</summary>
        public const float InteriorFrontZ = InteriorOriginZ - 2f;

        public const float LoftY = 4.0f;
        public const float LoftUnderside = 3.86f;
        public const float InnerMinX = -3.6f;
        public const float InnerMaxX = 3.6f;
        public const float InnerMinZ = InteriorFrontZ + 0.14f;
        public const float InnerMaxZ = InteriorFrontZ + 7.0f;

        /// <summary>Attic knee walls and ridge (ceiling line), and the roof's rise per metre toward the centre (40°).</summary>
        public const float KneeY = 5.2f;
        public const float ApexY = 8.224f;
        public const float RoofSlope = 0.84f;

        // Stair: lower flight north along the west wall, a corner landing, upper flight east to the middle of the attic.
        public const float StairBayX = -2.1f;
        public const float StairFootZ = InteriorFrontZ + 2.98f;
        public const float LandingZ = InteriorFrontZ + 5.5f;
        public const float LandingY = 2.3f;
        public const float UpperFlightMinZ = InteriorFrontZ + 5.6f;
        public const float UpperFlightZ = InteriorFrontZ + 6.3f;
        public const float UpperFlightStartX = -2.38f;
        public const float StairTopX = 0.14f;

        /// <summary>Rise per metre of both flights (0.1889 m risers on 0.28 m treads, 34°).</summary>
        public const float StairGradient = (LandingY - FloorY) / (LandingZ - StairFootZ);

        /// <summary>The stairwell in the loft: over the west bay from here north, and over the upper flight.</summary>
        public const float StairwellMinZ = InteriorFrontZ + 3.10f;

        // June's window in the attic's west wall: the sill is the attic floor; the reveal runs out to x −3.76.
        public const float InnerWindowX = InnerMinX;
        public const float InnerWindowZ = InteriorFrontZ + 1.5f;
        public const float SillY = LoftY;

        /// <summary>Dormer alcove behind the window (builder ALC_Y0..ALC_Y1), its eave and ridge.</summary>
        public const float AlcoveMinZ = InteriorFrontZ + 0.75f;
        public const float AlcoveMaxZ = InteriorFrontZ + 2.25f;
        public const float AlcoveEaveY = 5.8f;
        public const float AlcoveRidgeY = 6.4f;

        // Workshop nook east of the partition, its floor 0.5 m below the hall.
        public const float NookX = 2.2f;
        public const float NookMinZ = InteriorFrontZ + 2.4f;
        public const float NookFloorY = 0.1f;

        /// <summary>Mechanism frame origin (builder MECH) and yaw: its front (-Y) faces the hall (-X).</summary>
        public const float MechanismX = NookX;
        public const float MechanismZ = InteriorFrontZ + 4.6f;
        public const float MechanismYaw = 90f;

        // --- The treehouse routes (ramp frame: x = world X + 9.5, y = world Z - 12) ------------------------------

        public const float RampOriginX = -9.5f;
        public const float RampOriginZ = 12f;

        /// <summary>Treehouse deck and the shed roof height; the low-shed landing (1:5 from the ground).</summary>
        public const float TreehouseY = 2.4f;
        public const float RampLandingY = 1.52f;

        // Leg 1 runs north along the west side, leg 2 east onto the tall shed roof.
        public const float Leg1MinX = RampOriginX - 3.9f;
        public const float Leg1MaxX = RampOriginX - 2.0f;
        public const float Leg1StartZ = RampOriginZ - 4.6f;
        public const float Leg1EndZ = RampOriginZ + 3.0f;
        public const float RampNorthZ = RampOriginZ + 4.7f;
        public const float Leg2EndX = RampOriginX + 2.4f;
        public const float RoofMinX = RampOriginX + 1.55f;
        public const float RoofMaxX = RampOriginX + 3.75f;
        public const float RoofMinZ = RampOriginZ - 0.45f;

        /// <summary>Bridge from the shed roof (2.40 m) to June's sill (2.50 m): 2.4 m span, 0.8 m wide.</summary>
        public const float BridgeZ = RampOriginZ + 2f;
        public const float BridgeWidth = 0.8f;
        public const float BridgeStartX = RoofMaxX;
        public const float BridgeEndX = RoofMaxX + 2.4f;

        /// <summary>The awning slope (from routes.py placement and props.build_awning): 2.1 m wide, z_lo 1.35, z_hi 2.1.</summary>
        public const float AwningX = -7.106f;
        public const float AwningWallZ = RoofMinZ - 0.13f;
        public const float AwningLowY = 1.35f;
        public const float AwningHighY = 2.1f;
        public const float AwningRun = 1.5f;
        public const float AwningWidth = 2.1f;
        public const float AwningWallTopY = 2.53f;

        public const float CrateX = RampOriginX + 2.4f;
        public const float CrateZ = AwningWallZ - 1.95f;
        public const float CrateSize = 0.6f;

        /// <summary>Spec §4: start the ramp at 1:5, minimum usable width 1.6 m.</summary>
        public const float MaxRampGradient = 0.2f + 1e-3f;
        public const float MinRampWidth = 1.6f;

        /// <summary>Pip's capsule (PipMovementTuning) and the automatic step, for the route checks.</summary>
        public const float PipRadius = 0.28f;
        public const float PipHeight = 1.1f;
        public const float StepOffset = 0.2f;

        /// <summary>The tap jump's apex (spec §5) that every skilled-ascent step must fit under.</summary>
        public const float TapJumpApex = 1.0f;

        /// <summary>The cloth on the bridge-head post (child eye level from the shed roof).</summary>
        public const float ClothX = RoofMaxX - 0.12f;
        public const float ClothY = TreehouseY + 0.98f;
        public const float ClothZ = BridgeZ + BridgeWidth * 0.5f + 0.1f;

        /// <summary>The new models, each at its placement (yaw is model yaw, as every placement).</summary>
        public static readonly Placement[] Models =
        {
            new Placement(OwlHouseInterior, InteriorOriginX, 0f, InteriorOriginZ, 0f, detail: false),
            new Placement(WorkshopMechanism, MechanismX, NookFloorY, MechanismZ, MechanismYaw, detail: false),
            new Placement(ShedRamp, RampOriginX, 0f, RampOriginZ, 0f, detail: false),
            new Placement(SillBridge, BridgeStartX + 1.2f, 0f, BridgeZ, 0f, detail: false),
            new Placement(TreehouseCabin, RampOriginX, 0f, RampOriginZ, 200f, detail: false),
            new Placement(ClothSnag, ClothX, ClothY, ClothZ, 90f, detail: false),
            new Placement(Awning, AwningX, 0f, AwningWallZ, 0f, detail: false),
            new Placement(EnvironmentLayout.Crate, CrateX, 0f, CrateZ, 12f, collider: PlacementCollider.Bounds,
                halfX: 0.38f, halfZ: 0.38f, detail: false),
        };

        // --- The treehouse cabin (routes.py build_cabin; cabin frame: +y the yard side, -x the door side) ------------

        /// <summary>The treehouse cabin's frame: the platform centre in the maple frame, turned by the maple's yaw.</summary>
        public const float CabinYaw = 200f;
        public const float CabinHalf = 1.12f;

        /// <summary>The lookout bay's outer wall (cabin y).</summary>
        public const float CabinBayY = 1.75f;

        /// <summary>Main eave (wall top) and the closed ridge above the deck; the bay's low eave.</summary>
        public const float CabinEave = 1.7f;
        public const float CabinRidge = 2.33f;
        public const float CabinBayEave = 1.36f;

        /// <summary>Roof pitch of the cabin (33°) as rise per metre.</summary>
        public const float CabinRoofSlope = 0.6494f;

        /// <summary>Platform centre in the maple frame (trunk lean, routes.py PLAT); the trunk stands here too.</summary>
        public const float CabinPlatX = 0.2064f;
        public const float CabinPlatY = -0.048f;

        public static readonly StageSlab[] Slabs = BuildSlabs();

        /// <summary>
        /// The mandatory route in legs (spec §4 "main accessible loop"): plaza → June's window (outside), the attic →
        /// down the stair → the open front door (inside), then the porch. Each leg ends in its threshold's exit zone.
        /// </summary>
        public static readonly RouteLeg[] MandatoryLegs =
        {
            new RouteLeg("street to June's window", ThresholdId.Dormer,
                new RoutePoint("plaza", 0f, 0f, -0.5f),
                new RoutePoint("plaza west", -3f, 0f, -0.2f),
                new RoutePoint("sidewalk west", -3.2f, 0.045f, 3.9f),
                new RoutePoint("side yard", -11.2f, 0.045f, 3.9f),
                new RoutePoint("ramp foot", -12.45f, 0f, 6.9f),
                new RoutePoint("ramp start", -12.45f, 0f, 7.5f),
                new RoutePoint("landing", -12.45f, RampLandingY, 15.85f),
                new RoutePoint("leg 2 foot", -11.3f, RampLandingY, 15.85f),
                new RoutePoint("leg 2 top", -7.0f, TreehouseY, 15.85f),
                new RoutePoint("shed roof", -6.5f, TreehouseY, 14.0f),
                new RoutePoint("bridge head", -5.6f, TreehouseY, BridgeZ),
                new RoutePoint("bridge end", -3.7f, ExteriorSillY, BridgeZ),
                new RoutePoint("at the window", -3.34f, ExteriorSillY, BridgeZ)),
            new RouteLeg("attic, stair and hall", ThresholdId.FrontDoor,
                new RoutePoint("dormer arrival", -1.7f, LoftY, InteriorFrontZ + 1.5f),
                new RoutePoint("attic front", -0.6f, LoftY, InteriorFrontZ + 1.6f),
                new RoutePoint("attic middle", 0.6f, LoftY, InteriorFrontZ + 4.5f),
                new RoutePoint("by the stairwell", 0.6f, LoftY, UpperFlightZ),
                new RoutePoint("stair top", 0.4f, LoftY, UpperFlightZ),
                new RoutePoint("upper flight foot", -2.1f, LandingY + 0.19f, UpperFlightZ),
                new RoutePoint("landing", -2.85f, LandingY, UpperFlightZ),
                new RoutePoint("landing south", -2.85f, LandingY, LandingZ),
                new RoutePoint("stair foot", -2.85f, FloorY, StairFootZ),
                new RoutePoint("entry corner", -2.85f, FloorY, InteriorFrontZ + 2.6f),
                new RoutePoint("hall", -1.2f, FloorY, InteriorFrontZ + 1.6f),
                new RoutePoint("before the door", 0f, FloorY, InteriorFrontZ + 0.6f),
                new RoutePoint("in the doorway", 0f, FloorY, InteriorFrontZ + 0.05f)),
            new RouteLeg("porch", ThresholdId.None,
                new RoutePoint("door arrival", 0f, FloorY, FrontZ - 1.55f),
                new RoutePoint("porch front", 0.05f, FloorY, FrontZ - 1.8f)),
        };

        /// <summary>From the porch back in through the open front door (the door works both ways once it is open).</summary>
        public static readonly RouteLeg PorchToDoor = new RouteLeg("porch to the door", ThresholdId.FrontDoor,
            new RoutePoint("porch", 0.05f, FloorY, FrontZ - 0.7f),
            new RoutePoint("at the door", 0.05f, FloorY, FrontZ - 0.3f));

        /// <summary>The skilled ascent: crate (0.6) → awning (1.35 → 2.1) → over the awning wall onto the shed roof.</summary>
        public static readonly RoutePoint[] SkilledAscent =
        {
            new RoutePoint("yard", CrateX, 0f, CrateZ - 1.0f),
            new RoutePoint("crate", CrateX, CrateSize, CrateZ),
            new RoutePoint("awning low", AwningX, AwningLowY, AwningWallZ - AwningRun + 0.1f),
            new RoutePoint("awning high", AwningX, 1.92f, AwningWallZ - 0.45f),
            new RoutePoint("shed roof", AwningX, TreehouseY, RoofMinZ + 0.4f),
        };

        /// <summary>The point (x, z) is in the owl house interior (its plan, with the doorway and the window reveal).
        /// Nothing else stands up the lane, so the plan alone decides.</summary>
        public static bool Inside(float x, float z) =>
            x > InnerMinX - 0.45f && x < InnerMaxX + 0.3f && z > InteriorFrontZ - 0.45f && z < InnerMaxZ + 0.3f;

        /// <summary>The lower flight's tread line at world z (the hall floor before it, the landing after it).</summary>
        public static float LowerFlightAt(float z) =>
            Math.Max(FloorY, Math.Min(LandingY, FloorY + (z - StairFootZ) * StairGradient));

        /// <summary>The upper flight's tread line at world x (the landing before it, the attic floor after it).</summary>
        public static float UpperFlightAt(float x) =>
            Math.Max(LandingY, Math.Min(LoftY, LandingY + (x - UpperFlightStartX) * StairGradient));

        /// <summary>Ramp slabs of the mandatory route (legs and landing), for the 1:5 and width checks.</summary>
        public static IEnumerable<StageSlab> RampSlabs()
        {
            foreach (var slab in Slabs)
            {
                if (slab.Kind == SlabKind.Walk && slab.Name.StartsWith("Ramp", StringComparison.Ordinal))
                {
                    yield return slab;
                }
            }
        }

        /// <summary>Highest walk-slab top at (x, z) at or below <paramref name="maxY"/>; the lawn (0) otherwise (and
        /// nothing at all, −100, in the interior, which has no lawn under it).</summary>
        public static float GroundAt(float x, float z, float maxY)
        {
            float best = Inside(x, z) ? -100f : 0f;
            foreach (var slab in Slabs)
            {
                if (slab.Kind == SlabKind.Walk && slab.TryTopAt(x, z, 0f, out float y) && y <= maxY && y > best)
                {
                    best = y;
                }
            }

            return best;
        }

        /// <summary>First wall slab that a standing Pip at (x, y, z) would touch (above the step), or null.</summary>
        public static string Blocking(float x, float y, float z, bool doorOpen = false)
        {
            foreach (var slab in Slabs)
            {
                if (doorOpen && slab.Name == DoorBlockerName)
                {
                    continue;
                }

                if (slab.Kind == SlabKind.Wall && slab.Blocks(x, z, y + StepOffset + 0.02f, y + PipHeight, PipRadius))
                {
                    return slab.Name;
                }
            }

            return null;
        }

        /// <summary>Name of the inner door leaf's blocker that the runtime switches off once the door is open.</summary>
        public const string DoorBlockerName = "Hall_DoorLeaf";

        private static StageSlab[] BuildSlabs()
        {
            var s = new List<StageSlab>();

            // ---- Shed ramp (routes.py build_shed_ramp): decks, sheds, guard rails at the deck edges. ----
            float leg1X = (Leg1MinX + Leg1MaxX) * 0.5f;
            float legWidth = Leg1MaxX - Leg1MinX; // 1.9
            float leg2Z = (Leg1EndZ + RampNorthZ) * 0.5f;
            float leg2Width = RampNorthZ - Leg1EndZ; // 1.7
            Walk(s, "Ramp_Leg1", SlabGroup.Route, leg1X, 0f, Leg1StartZ, leg1X, RampLandingY, Leg1EndZ, legWidth, 0.4f);
            Walk(s, "Ramp_Landing", SlabGroup.Route, leg1X, RampLandingY, Leg1EndZ, leg1X, RampLandingY, RampNorthZ, legWidth, RampLandingY);
            Walk(s, "Ramp_Leg2", SlabGroup.Route, Leg1MaxX, RampLandingY, leg2Z, Leg2EndX, TreehouseY, leg2Z, leg2Width, 0.4f);
            // The tall shed: solid to its roof; north of leg 2's arrival it starts at leg 2's end.
            Walk(s, "Shed_RoofSouth", SlabGroup.Route, (RoofMinX + RoofMaxX) * 0.5f, TreehouseY, RoofMinZ,
                (RoofMinX + RoofMaxX) * 0.5f, TreehouseY, Leg1EndZ, RoofMaxX - RoofMinX, TreehouseY);
            Walk(s, "Shed_RoofNorth", SlabGroup.Route, (Leg2EndX + RoofMaxX) * 0.5f, TreehouseY, Leg1EndZ,
                (Leg2EndX + RoofMaxX) * 0.5f, TreehouseY, RampNorthZ, RoofMaxX - Leg2EndX, TreehouseY);
            // The threshold boards to the treehouse platform (the platform itself is the street set's).
            Walk(s, "Shed_Threshold", SlabGroup.Route, RoofMinX - 0.25f, TreehouseY, RoofMinZ, RoofMinX - 0.25f,
                TreehouseY, RampOriginZ + 0.9f, 0.5f, 0.2f);
            // Low shed under the landing, and the skirts closing both legs to the ground.
            Wall(s, "Ramp_Leg1West", SlabGroup.Route, Leg1MinX - 0.05f, 1.0f, Leg1StartZ + 0.15f, Leg1MinX - 0.05f,
                RampLandingY + 1.0f, Leg1EndZ, 0.1f, 3f);
            Wall(s, "Ramp_Leg1East", SlabGroup.Route, Leg1MaxX + 0.05f, 1.0f, Leg1StartZ + 0.15f, Leg1MaxX + 0.05f,
                RampLandingY + 1.0f, Leg1EndZ - 0.05f, 0.1f, 3f);
            Wall(s, "Ramp_LandingWest", SlabGroup.Route, Leg1MinX - 0.05f, RampLandingY + 1f, Leg1EndZ, Leg1MinX - 0.05f,
                RampLandingY + 1f, RampNorthZ, 0.1f, RampLandingY + 1f);
            Wall(s, "Ramp_North", SlabGroup.Route, Leg1MinX, RampLandingY + 1f, RampNorthZ + 0.05f, Leg1MaxX,
                RampLandingY + 1f, RampNorthZ + 0.05f, 0.1f, RampLandingY + 1f);
            Wall(s, "Ramp_Leg2North", SlabGroup.Route, Leg1MaxX, RampLandingY + 1f, RampNorthZ + 0.05f, Leg2EndX,
                TreehouseY + 1f, RampNorthZ + 0.05f, 0.1f, 3.5f);
            Wall(s, "Ramp_Leg2South", SlabGroup.Route, Leg1MaxX + 0.1f, RampLandingY + 1f, Leg1EndZ - 0.05f, RoofMinX,
                TreehouseY + 1f, Leg1EndZ - 0.05f, 0.1f, 3.5f);
            Wall(s, "Shed_NorthRail", SlabGroup.Route, Leg2EndX, TreehouseY + 1f, RampNorthZ + 0.05f, RoofMaxX,
                TreehouseY + 1f, RampNorthZ + 0.05f, 0.1f, 1f);
            Wall(s, "Shed_WestRail", SlabGroup.Route, RoofMinX - 0.05f, TreehouseY + 1f, RampOriginZ + 1.1f,
                RoofMinX - 0.05f, TreehouseY + 1f, Leg1EndZ, 0.1f, 1f);
            // East rail along the roof edge, open at the bridge head.
            float bridgeS = BridgeZ - BridgeWidth * 0.5f - 0.1f;
            float bridgeN = BridgeZ + BridgeWidth * 0.5f + 0.1f;
            Wall(s, "Shed_EastRailSouth", SlabGroup.Route, RoofMaxX + 0.05f, TreehouseY + 1f, RoofMinZ + 0.1f,
                RoofMaxX + 0.05f, TreehouseY + 1f, bridgeS, 0.1f, 1f);
            Wall(s, "Shed_EastRailNorth", SlabGroup.Route, RoofMaxX + 0.05f, TreehouseY + 1f, bridgeN,
                RoofMaxX + 0.05f, TreehouseY + 1f, RampNorthZ, 0.1f, 1f);

            // ---- Bridge to the sill, with guards so the crossing needs no precision. ----
            Walk(s, "Bridge", SlabGroup.Route, BridgeStartX - 0.05f, TreehouseY, BridgeZ, BridgeEndX, ExteriorSillY, BridgeZ,
                BridgeWidth, 0.25f);
            for (int side = -1; side <= 1; side += 2)
            {
                float z = BridgeZ + side * (BridgeWidth * 0.5f + 0.05f);
                Wall(s, side < 0 ? "Bridge_GuardSouth" : "Bridge_GuardNorth", SlabGroup.Route, BridgeStartX + 0.05f,
                    TreehouseY + 0.8f, z, BridgeEndX - 0.05f, ExteriorSillY + 0.8f, z, 0.08f, 0.8f);
            }

            // June's sill outside the window: the solid body stops Pip at the window plane, in the dormer's exit zone.
            Walk(s, "Window_Sill", SlabGroup.Route, ExteriorWindowX - 0.43f, ExteriorSillY, ExteriorWindowZ,
                ExteriorWindowX + 0.05f, ExteriorSillY, ExteriorWindowZ, 2f * WindowHalfWidth, 0.12f);

            // ---- Skilled ascent: the awning slope and its wall slice (the crate fits its own bounds). ----
            // Built from the Awning's CollisionRules recipe; listed here for the route checks.
            Walk(s, "Awning_Slope", SlabGroup.Reference, AwningX, AwningLowY, AwningWallZ - AwningRun, AwningX, AwningHighY,
                AwningWallZ - 0.06f, AwningWidth, 0.08f);
            Wall(s, "Awning_Wall", SlabGroup.Reference, AwningX - 1.3f, AwningWallTopY, AwningWallZ + 0.04f, AwningX + 1.3f,
                AwningWallTopY, AwningWallZ + 0.04f, 0.16f, AwningWallTopY);

            // ---- The treehouse cabin: closed walls, the lookout bay's boards and the roof (D24). ----
            Cabin(s);

            // ---- The exterior owl house: EnvironmentDressing's solid body and roof steps and the porch deck. ----
            Wall(s, "House_Body", SlabGroup.Reference, 0f, BodyTopY, FrontZ, 0f, BodyTopY, BackZ, 2f * BodyHalfX, BodyTopY);
            Wall(s, "House_RoofLow", SlabGroup.Reference, 0f, 4.0f, FrontZ - 0.3f, 0f, 4.0f, BackZ + 0.3f, 5f, 1.2f);
            Wall(s, "House_RoofHigh", SlabGroup.Reference, 0f, 5.8f, FrontZ - 0.3f, 0f, 5.8f, BackZ + 0.3f, 2.6f, 1.8f);
            Walk(s, "Porch_Deck", SlabGroup.Reference, 0f, FloorY, FrontZ - 2f, 0f, FloorY, FrontZ, 6f, FloorY);

            Interior(s);
            return s.ToArray();
        }

        /// <summary>World z of interior builder depth <paramref name="by"/>.</summary>
        private static float Z(float by) => InteriorFrontZ + by;

        /// <summary>The interior's colliders (house v3 anchor tables, docs/S002-house-v3.md §3).</summary>
        private static void Interior(List<StageSlab> s)
        {
            const SlabGroup g = SlabGroup.Interior;
            const float fy = FloorY;
            const float top = 8.5f; // above the ridge

            // ---- Floors: the hall (with the doorway), the strip by the east window, the attic and the window reveal ----
            Walk(s, "Hall_Floor", g, -0.7f, fy, Z(-0.4f), -0.7f, fy, Z(7.0f), 5.8f, fy);
            Walk(s, "Hall_FloorEast", g, 2.9f, fy, Z(0.1f), 2.9f, fy, Z(2.4f), 1.4f, fy);
            Walk(s, "Loft_West", g, -2.85f, LoftY, Z(0.1f), -2.85f, LoftY, StairwellMinZ, 1.5f, 0.22f);
            Walk(s, "Loft_Middle", g, -0.98f, LoftY, Z(0.1f), -0.98f, LoftY, UpperFlightMinZ, 2.24f, 0.22f);
            Walk(s, "Loft_East", g, 1.87f, LoftY, Z(0.1f), 1.87f, LoftY, Z(7.0f), 3.46f, 0.22f);
            Walk(s, "Attic_WindowReveal", g, InnerMinX + 0.1f, LoftY, InnerWindowZ, InnerMinX - 0.35f, LoftY, InnerWindowZ,
                2f * WindowHalfWidth, 0.22f);

            // ---- Stair: the lower flight north up the west bay, the landing, the upper flight east ----
            Walk(s, "Stair_Lower", g, -2.85f, fy, StairFootZ, -2.85f, LandingY, LandingZ, 1.5f, 0.3f);
            Walk(s, "Stair_Landing", g, -2.85f, LandingY, LandingZ, -2.85f, LandingY, Z(7.0f), 1.5f, LandingY - fy);
            Walk(s, "Stair_Upper", g, UpperFlightStartX, LandingY, UpperFlightZ, StairTopX + 0.02f,
                LoftY + 0.02f * StairGradient, UpperFlightZ, 1.4f, 0.3f);
            // The lower flight's closed side and balustrade toward the hall (x −2.12), stepped up to the rail line.
            const float rail = 0.95f;
            float[] cuts = { 3.19f, 3.8f, 4.4f, 5.0f, 5.5f };
            for (int i = 1; i < cuts.Length; i++)
            {
                float h = LowerFlightAt(Z(cuts[i])) + rail;
                Wall(s, "Stair_Side" + i, g, -2.12f, h, Z(cuts[i - 1]), -2.12f, h, Z(cuts[i]), 0.08f, h);
            }

            Wall(s, "Stair_Post", g, -2.13f, 5.0f, Z(5.55f), -2.03f, 5.0f, Z(5.55f), 0.1f, 5.0f - fy);
            Wall(s, "Stair_Wall", g, StairBayX, LoftUnderside, Z(5.55f), StairTopX, LoftUnderside, Z(5.55f), 0.1f,
                LoftUnderside);
            Wall(s, "Stair_EndWall", g, StairTopX, LoftUnderside, Z(5.5f), StairTopX, LoftUnderside, Z(7.0f), 0.1f,
                LoftUnderside);
            // Loft rails round the stairwell (0.95 m).
            Wall(s, "Loft_RailWest", g, -2.04f, LoftY + rail, StairwellMinZ, -2.04f, LoftY + rail, Z(5.5f), 0.08f, rail);
            Wall(s, "Loft_RailSouth", g, InnerMinX, LoftY + rail, Z(3.04f), StairBayX, LoftY + rail, Z(3.04f), 0.08f, rail);
            Wall(s, "Loft_RailNorth", g, StairBayX, LoftY + rail, Z(5.55f), StairTopX, LoftY + rail, Z(5.55f), 0.08f, rail);

            // ---- Workshop partition and the nook's south wall (the nook is behind them, never entered) ----
            Wall(s, "Workshop_Partition", g, NookX, LoftUnderside, NookMinZ, NookX, LoftUnderside, InnerMaxZ, 0.1f,
                LoftUnderside);
            Wall(s, "Workshop_South", g, NookX, LoftUnderside, NookMinZ, InnerMaxX, LoftUnderside, NookMinZ, 0.1f,
                LoftUnderside);

            // ---- Shell: front wall round the door, back, east, west round June's window, roof plates, dormer ----
            Wall(s, "Front_West", g, InnerMinX - 0.2f, top, Z(0.04f), -DoorHalfWidth, top, Z(0.04f), 0.2f, top);
            Wall(s, "Front_East", g, DoorHalfWidth, top, Z(0.04f), InnerMaxX + 0.2f, top, Z(0.04f), 0.2f, top);
            Wall(s, "Front_DoorHead", g, -DoorHalfWidth, top, Z(0.04f), DoorHalfWidth, top, Z(0.04f), 0.2f, top - DoorTopY);
            Wall(s, DoorBlockerName, g, -DoorHalfWidth, DoorTopY, Z(0.04f), DoorHalfWidth, DoorTopY, Z(0.04f), 0.2f,
                DoorTopY - fy + 0.05f);
            // The door's view box (painted porch): Pip stops a step into it, well inside the door's exit zone.
            Wall(s, "Front_ViewStop", g, -0.9f, 3.3f, Z(-0.38f), 0.9f, 3.3f, Z(-0.38f), 0.08f, 3.3f);
            Wall(s, "Back", g, InnerMinX - 0.2f, top, Z(7.1f), InnerMaxX + 0.2f, top, Z(7.1f), 0.2f, top);
            Wall(s, "East", g, InnerMaxX + 0.1f, KneeY + 0.1f, Z(-0.1f), InnerMaxX + 0.1f, KneeY + 0.1f, Z(7.2f), 0.2f,
                KneeY + 0.1f);
            const float jambS = InnerWindowZ - WindowHalfWidth;
            const float jambN = InnerWindowZ + WindowHalfWidth;
            Wall(s, "West_South", g, InnerMinX - 0.1f, KneeY + 0.1f, Z(-0.1f), InnerMinX - 0.1f, KneeY + 0.1f, jambS, 0.2f,
                KneeY + 0.1f);
            Wall(s, "West_North", g, InnerMinX - 0.1f, KneeY + 0.1f, jambN, InnerMinX - 0.1f, KneeY + 0.1f, Z(7.2f), 0.2f,
                KneeY + 0.1f);
            Wall(s, "West_UnderSill", g, InnerMinX - 0.1f, LoftY - 0.01f, jambS, InnerMinX - 0.1f, LoftY - 0.01f, jambN,
                0.2f, LoftY);
            Wall(s, "Window_Head", g, InnerMinX - 0.1f, AlcoveRidgeY, jambS, InnerMinX - 0.1f, AlcoveRidgeY, jambN, 0.2f,
                AlcoveRidgeY - (SillY + WindowApex));
            // The window's view box (painted yard): Pip stops a step into the reveal, inside the dormer's exit zone.
            Wall(s, "Window_ViewStop", g, -3.86f, 6.0f, AlcoveMinZ - 0.1f, -3.86f, 6.0f, AlcoveMaxZ + 0.1f, 0.1f, 2.1f);
            RoofPlate(s, "Roof_East", 1, Z(-0.1f), Z(7.2f));
            RoofPlate(s, "Roof_WestSouth", -1, Z(-0.1f), AlcoveMinZ);
            RoofPlate(s, "Roof_WestNorth", -1, AlcoveMaxZ, Z(7.2f));
            Wall(s, "Dormer_South", g, InnerMinX - 0.2f, AlcoveRidgeY + 0.3f, AlcoveMinZ - 0.05f, -2.1f,
                AlcoveRidgeY + 0.3f, AlcoveMinZ - 0.05f, 0.1f, AlcoveRidgeY + 0.3f - KneeY);
            Wall(s, "Dormer_North", g, InnerMinX - 0.2f, AlcoveRidgeY + 0.3f, AlcoveMaxZ + 0.05f, -2.1f,
                AlcoveRidgeY + 0.3f, AlcoveMaxZ + 0.05f, 0.1f, AlcoveRidgeY + 0.3f - KneeY);
            Wall(s, "Dormer_Roof", g, InnerMinX - 0.2f, AlcoveRidgeY + 0.25f, InnerWindowZ, -2.1f, AlcoveRidgeY + 0.25f,
                InnerWindowZ, AlcoveMaxZ - AlcoveMinZ + 0.2f, 0.22f);

            // ---- Furniture with a collider (docs/S002-house-v3.md §3.7; builder x and depth, world heights) ----
            Box(s, "Hall_BenchAndRack", 0.45f, 1.95f, 6.59f, 7.0f, fy, 2.35f);
            Box(s, "Hall_Console", 3.24f, 3.6f, 0.8f, 1.8f, fy, 1.35f);
            Box(s, "Hall_BlanketChest", -3.5f, -3.02f, 1.17f, 2.03f, fy, 1.1f);
            Box(s, "Hall_Clock", 2.67f, 3.13f, 2.12f, 2.4f, fy, 2.7f);
            Box(s, "Hall_Crate", -1.87f, -1.33f, 4.97f, 5.43f, fy, 0.96f);
            Box(s, "Hall_UmbrellaStand", -1.09f, -0.81f, 0.28f, 0.56f, fy, 1.12f);
            Box(s, "Attic_CraftTable", 2.1f, 3.16f, 2.35f, 3.45f, LoftY, 4.47f);
            Box(s, "Attic_TrunkEast", 2.74f, 3.26f, 4.27f, 5.23f, LoftY, 4.8f);
            Box(s, "Attic_TrunkBack", 2.84f, 3.26f, 5.5f, 6.2f, LoftY, 4.5f);
            Box(s, "Attic_TrunkAndHatBoxes", -2.3f, -0.35f, 0.25f, 0.78f, LoftY, 4.55f);
            Box(s, "Attic_Crates", 2.05f, 3.25f, 0.3f, 1.02f, LoftY, 4.72f);
            Box(s, "Attic_Pumpkin", 1.4f, 1.8f, 0.35f, 0.75f, LoftY, 4.32f);
            Box(s, "Attic_JuneStool", -1.27f, -0.93f, 2.58f, 2.92f, LoftY, 4.3f);
            Box(s, "Attic_Bookshelf", 1.27f, 2.53f, 6.69f, 7.0f, LoftY, 5.34f);
        }

        /// <summary>A furniture box from builder ranges (x, depth) and world heights.</summary>
        private static void Box(List<StageSlab> s, string name, float x0, float x1, float by0, float by1, float y0, float y1)
        {
            float x = (x0 + x1) * 0.5f;
            Wall(s, name, SlabGroup.Interior, x, y1, Z(by0), x, y1, Z(by1), x1 - x0, y1 - y0);
        }

        private static void RoofPlate(List<StageSlab> s, string name, int side, float z0, float z1)
        {
            // Plate top 0.25 m above the ceiling line, from beyond the wall to the ridge.
            float wallX = side * (InnerMaxX + 0.2f);
            const float eaveY = KneeY - 0.2f * RoofSlope + 0.25f;
            float z = (z0 + z1) * 0.5f;
            // Width runs along z (across the slope's line), so the line goes eave → ridge.
            s.Add(new StageSlab(name, SlabKind.Wall, SlabGroup.Interior, wallX, eaveY, z, 0f, ApexY + 0.25f, z, z1 - z0,
                0.22f));
        }

        private static void Cabin(List<StageSlab> s)
        {
            const float h = CabinHalf;
            const float deck = TreehouseY;
            const float eave = deck + CabinEave;
            const float bay = deck + CabinBayEave + 0.06f;
            // Walls on all four sides (routes.py build_cabin): north and west carry small windows, the east wall the
            // door (cabin y −1.02…−0.12), the yard side opens into the lookout bay. No parapet any more.
            CabinWall(s, "Cabin_North", -h, -h, h, -h, eave);
            CabinWall(s, "Cabin_West", h, -h, h, h, eave);
            CabinWall(s, "Cabin_EastSouth", -h, -0.1f, -h, h, eave);
            CabinWall(s, "Cabin_EastNorth", -h, -h, -h, -1.04f, eave);
            CabinWall(s, "Cabin_DoorHead", -h, -1.04f, -h, -0.1f, eave, deck + 1.45f);
            CabinWall(s, "Cabin_BayWest", h, h, h, CabinBayY, bay);
            CabinWall(s, "Cabin_BayEast", -h, h, -h, CabinBayY, bay);
            CabinWall(s, "Cabin_BayOuter", -h, CabinBayY, h, CabinBayY, bay);
            // The bay's boards beyond the platform edge (the deck itself is the TreehousePlatform recipe).
            CabinToWorld(CabinPlatX, CabinPlatY + 1.2f, out float bx0, out float bz0);
            CabinToWorld(CabinPlatX, CabinPlatY + 1.81f, out float bx1, out float bz1);
            Walk(s, "Cabin_BayFloor", SlabGroup.Route, bx0, deck, bz0, bx1, deck, bz1, 2f * h, 0.12f);
            // The roof: two slopes up to the boarded ridge, and the bay's lean-to (heads bump, never pass).
            CabinRoof(s, "Cabin_RoofNorth", -h - 0.3f, -0.3f);
            CabinRoof(s, "Cabin_RoofSouth", h + 0.3f, 0.3f);
            CabinToWorld(CabinPlatX, CabinPlatY + h, out float lx0, out float lz0);
            CabinToWorld(CabinPlatX, CabinPlatY + CabinBayY + 0.2f, out float lx1, out float lz1);
            s.Add(new StageSlab("Cabin_RoofBay", SlabKind.Wall, SlabGroup.Route, lx0, deck + 1.74f + 0.2f, lz0, lx1,
                deck + CabinBayEave + 0.2f, lz1, 2f * h + 0.3f, 0.18f));
        }

        /// <summary>The cabin's roof underside above the deck at cabin y (the bay's lean-to past the main wall).</summary>
        public static float CabinCeilingAt(float cy)
        {
            if (cy > CabinHalf)
            {
                float t = Math.Min(1f, (cy - CabinHalf) / (CabinBayY - CabinHalf));
                return 1.74f + (CabinBayEave - 1.74f) * t;
            }

            float under = CabinEave - 0.1f + (CabinHalf + 0.3f - Math.Max(0.3f, Math.Abs(cy))) * CabinRoofSlope;
            return Math.Min(CabinRidge - 0.05f, under);
        }

        /// <summary>Cabin roof slope from the eave line at cabin y <paramref name="eaveY"/> to the ridge.</summary>
        private static void CabinRoof(List<StageSlab> s, string name, float eaveY, float ridgeY)
        {
            const float under = TreehouseY + CabinEave - 0.1f;
            CabinToWorld(CabinPlatX, CabinPlatY + eaveY, out float x0, out float z0);
            CabinToWorld(CabinPlatX, CabinPlatY + ridgeY, out float x1, out float z1);
            float ridge = under + (Math.Abs(eaveY) - Math.Abs(ridgeY)) * CabinRoofSlope;
            s.Add(new StageSlab(name, SlabKind.Wall, SlabGroup.Route, x0, under + 0.2f, z0, x1, ridge + 0.2f, z1,
                2f * CabinHalf + 0.6f, 0.18f));
        }

        private static void CabinWall(List<StageSlab> s, string name, float ax, float ay, float bx, float by, float top,
            float bottom = TreehouseY)
        {
            CabinToWorld(CabinPlatX + ax, CabinPlatY + ay, out float x0, out float z0);
            CabinToWorld(CabinPlatX + bx, CabinPlatY + by, out float x1, out float z1);
            Wall(s, name, SlabGroup.Route, x0, top, z0, x1, top, z1, 0.08f, top - bottom);
        }

        /// <summary>Maple-frame plan point (Blender x, y) to world (x, z): the maple stands at the ramp origin.</summary>
        public static void CabinToWorld(float mx, float my, out float x, out float z)
        {
            double a = CabinYaw * Math.PI / 180.0;
            float c = (float)Math.Cos(a);
            float sn = (float)Math.Sin(a);
            x = RampOriginX + mx * c + my * sn;
            z = RampOriginZ - mx * sn + my * c;
        }

        /// <summary>World (x, z) to the cabin frame: centred on the platform, +y the yard side, −x the door side.</summary>
        public static void WorldToCabin(float x, float z, out float cx, out float cy)
        {
            double a = CabinYaw * Math.PI / 180.0;
            float c = (float)Math.Cos(a);
            float sn = (float)Math.Sin(a);
            float dx = x - RampOriginX;
            float dz = z - RampOriginZ;
            cx = dx * c - dz * sn - CabinPlatX;
            cy = dx * sn + dz * c - CabinPlatY;
        }

        private static void Walk(List<StageSlab> s, string name, SlabGroup group, float x0, float y0, float z0, float x1,
            float y1, float z1, float width, float depth) =>
            s.Add(new StageSlab(name, SlabKind.Walk, group, x0, y0, z0, x1, y1, z1, width, depth));

        private static void Wall(List<StageSlab> s, string name, SlabGroup group, float x0, float y0, float z0, float x1,
            float y1, float z1, float width, float depth) =>
            s.Add(new StageSlab(name, SlabKind.Wall, group, x0, y0, z0, x1, y1, z1, width, depth));

        public static StageSlab Find(string name)
        {
            foreach (var slab in Slabs)
            {
                if (slab.Name == name)
                {
                    return slab;
                }
            }

            throw new ArgumentException("no slab " + name, nameof(name));
        }
    }
}
