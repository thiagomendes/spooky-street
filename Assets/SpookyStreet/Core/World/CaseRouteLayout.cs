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

        /// <summary>The owl house shell: outer walls, roof plates, door blocker. The camera passes through these
        /// while Pip is inside, so it can look in through the culled near walls.</summary>
        Shell,

        /// <summary>Inside the house: foyer floor, stair, loft, rails, partition, furniture.</summary>
        Interior,

        /// <summary>Already built elsewhere (the porch deck): listed so the route checks see it; not built here.</summary>
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

    /// <summary>
    /// The interior half of case 01 (S002 interior wiring): where the treehouse-route models, the owl house interior
    /// and the workshop mechanism stand, and every authored collider of the mandatory route (side yard → shed ramp →
    /// shed roof → bridge → June's sill → attic → stair → foyer → front door) and of the skilled ascent (crate →
    /// awning → shed roof). Model frames and heights come from the Blender scripts
    /// (<c>tools/blender/env/routes.py</c>, <c>interior.py</c>; suggested placements in
    /// <c>env_interior_review.py</c>): the owl house builder space maps to world as x = bx, z = 11 + by, y = bz;
    /// the ramp frame as x = rx − 9.5, z = ry + 12. Pure data so EditMode tests prove the ramp is 1:5 and ≥ 1.6 m
    /// wide, the route is continuous without jumps and the skilled steps fit the 1.0 m tap jump (spec §4, MAP-01).
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
        public const string Cam = "Cam";
        public const string Follower = "Follower";
        public const string LinkArmSeated = "LinkArm_Seated";
        public const string LinkArmSlipped = "LinkArm_Slipped";
        public const string ServicePanel = "ServicePanel";
        public const string Crank = "Crank";

        // --- The owl house (builder space: body x -3..3, y 0..6, front wall at y = 0) ---------------------------

        /// <summary>World z of the house front wall (builder y = 0).</summary>
        public const float FrontZ = EnvironmentLayout.HouseZ - 2f;

        public const float FloorY = 0.6f;
        public const float LoftY = 2.5f;
        public const float LoftUnderside = 2.34f;
        public const float InnerMinX = -2.86f;
        public const float InnerMaxX = 2.86f;
        public const float InnerMinZ = FrontZ + 0.14f;
        public const float InnerMaxZ = FrontZ + 5.86f;
        public const float OuterX = 3.1f;
        public const float BackZ = FrontZ + 6f;

        /// <summary>The loft starts here (builder y 1.5); the entry bay in front of it is open to the roof.</summary>
        public const float LoftFrontZ = FrontZ + 1.5f;

        /// <summary>Roof knee and apex (the ceiling slope, builder KNEE/APEX).</summary>
        public const float KneeY = 2.86f;
        public const float ApexY = 6.27f;

        // Stair along the back wall, rising east: bottom tread at x -1.72 on the floor, loft lip at x 0.8.
        public const float StairBottomX = -1.72f;
        public const float StairTopX = 0.8f;
        public const float StairMinZ = FrontZ + 4.86f;
        public const float StairMaxZ = InnerMaxZ;
        public const float StairHoleMinX = -1.44f;

        // June's window: 1.0 m wide, sill 2.5 m, spring line 0.95 m above the sill, 1.45 m apex.
        public const float WindowX = -3.045f;
        public const float WindowZ = FrontZ + 3f;
        public const float WindowHalfWidth = 0.5f;
        public const float SillY = 2.5f;
        public const float WindowSpring = 0.95f;
        public const float WindowApex = 1.45f;

        /// <summary>Dormer alcove behind the window (builder ALC_Y0..ALC_Y1).</summary>
        public const float AlcoveMinZ = FrontZ + 2.3f;
        public const float AlcoveMaxZ = FrontZ + 3.7f;
        public const float AlcoveEaveY = 3.9f;

        // Front door: hinge at x -0.55; opening to x 0.68, head at 2.62.
        public const float DoorHingeX = -0.55f;
        public const float DoorMaxX = 0.68f;
        public const float DoorTopY = 2.62f;

        // Workshop nook east of the partition, its floor 0.5 m below the foyer.
        public const float NookX = 1.45f;
        public const float NookMinZ = FrontZ + 1.65f;
        public const float NookFloorY = 0.1f;

        /// <summary>Mechanism frame origin (builder MECH) and yaw: its front (-Y) faces the foyer (-X).</summary>
        public const float MechanismX = NookX;
        public const float MechanismZ = FrontZ + 3.1f;
        public const float MechanismYaw = 90f;

        // --- The treehouse routes (ramp frame: x = world X + 9.5, y = world Z - 12) ------------------------------

        public const float RampOriginX = -9.5f;
        public const float RampOriginZ = 12f;

        /// <summary>Treehouse deck and the shed roof height; the low-shed landing (1:5 from the ground).</summary>
        public const float TreehouseY = 2.4f;
        public const float LandingY = 1.52f;

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

        /// <summary>The new models, each at its suggested placement (yaw is model yaw, as every placement).</summary>
        public static readonly Placement[] Models =
        {
            new Placement(OwlHouseInterior, EnvironmentLayout.HouseX, 0f, EnvironmentLayout.HouseZ, EnvironmentLayout.HouseYaw, detail: false),
            new Placement(WorkshopMechanism, MechanismX, NookFloorY, MechanismZ, MechanismYaw, detail: false),
            new Placement(ShedRamp, RampOriginX, 0f, RampOriginZ, 0f, detail: false),
            new Placement(SillBridge, BridgeStartX + 1.2f, 0f, BridgeZ, 0f, detail: false),
            new Placement(TreehouseCabin, RampOriginX, 0f, RampOriginZ, 200f, detail: false),
            new Placement(ClothSnag, ClothX, ClothY, ClothZ, 90f, detail: false),
            new Placement(Awning, AwningX, 0f, AwningWallZ, 0f, detail: false),
            new Placement(EnvironmentLayout.Crate, CrateX, 0f, CrateZ, 12f, collider: PlacementCollider.Bounds,
                halfX: 0.38f, halfZ: 0.38f, detail: false),
        };

        /// <summary>The cloth on the bridge-head post (child eye level from the shed roof).</summary>
        public const float ClothX = RoofMaxX - 0.12f;
        public const float ClothY = TreehouseY + 0.98f;
        public const float ClothZ = BridgeZ + BridgeWidth * 0.5f + 0.1f;

        /// <summary>The treehouse cabin's frame: the platform centre in the maple frame, turned by the maple's yaw.</summary>
        public const float CabinYaw = 200f;
        public const float CabinHalf = 1.12f;

        public static readonly StageSlab[] Slabs = BuildSlabs();

        /// <summary>The mandatory route, plaza to the open front door (spec §4 "main accessible loop").</summary>
        public static readonly RoutePoint[] MandatoryRoute =
        {
            new RoutePoint("plaza", 0f, 0f, -0.5f),
            new RoutePoint("plaza west", -3f, 0f, -0.2f),
            new RoutePoint("sidewalk west", -3.2f, 0.045f, 3.9f),
            new RoutePoint("side yard", -11.2f, 0.045f, 3.9f),
            new RoutePoint("ramp foot", -12.45f, 0f, 6.9f),
            new RoutePoint("ramp start", -12.45f, 0f, 7.5f),
            new RoutePoint("landing", -12.45f, LandingY, 15.85f),
            new RoutePoint("leg 2 foot", -11.3f, LandingY, 15.85f),
            new RoutePoint("leg 2 top", -7.0f, TreehouseY, 15.85f),
            new RoutePoint("shed roof", -6.5f, TreehouseY, 14.0f),
            new RoutePoint("bridge head", -5.6f, TreehouseY, BridgeZ),
            new RoutePoint("bridge end", -3.45f, 2.5f, BridgeZ),
            new RoutePoint("sill", -2.95f, SillY, BridgeZ),
            new RoutePoint("attic", -2.2f, LoftY, 14.0f),
            new RoutePoint("loft east", 1.35f, LoftY, 14.4f),
            new RoutePoint("loft back", 1.35f, LoftY, 16.36f),
            new RoutePoint("stair top", 0.95f, LoftY, 16.36f),
            new RoutePoint("stair bottom", -2.15f, FloorY, 16.36f),
            new RoutePoint("foyer back", -2.35f, FloorY, 15.3f),
            new RoutePoint("foyer", -0.6f, FloorY, 12.4f),
            new RoutePoint("doorway", 0.05f, FloorY, 11.4f),
            new RoutePoint("porch", 0.05f, FloorY, 10.3f),
        };

        /// <summary>The skilled ascent: crate (0.6) → awning (1.35 → 2.1) → over the awning wall onto the shed roof.</summary>
        public static readonly RoutePoint[] SkilledAscent =
        {
            new RoutePoint("yard", CrateX, 0f, CrateZ - 1.0f),
            new RoutePoint("crate", CrateX, CrateSize, CrateZ),
            new RoutePoint("awning low", AwningX, AwningLowY, AwningWallZ - AwningRun + 0.1f),
            new RoutePoint("awning high", AwningX, AwningHighY, AwningWallZ - 0.15f),
            new RoutePoint("shed roof", AwningX, TreehouseY, RoofMinZ + 0.4f),
        };

        /// <summary>Pip is inside the house shell (x, z only).</summary>
        public static bool Inside(float x, float z) =>
            x > -OuterX + 0.15f && x < OuterX - 0.15f && z > FrontZ + 0.08f && z < BackZ - 0.05f;

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

        /// <summary>Highest walk-slab top at (x, z) at or below <paramref name="maxY"/>; the lawn (0) otherwise.</summary>
        public static float GroundAt(float x, float z, float maxY)
        {
            float best = 0f;
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
        public static string Blocking(float x, float y, float z)
        {
            foreach (var slab in Slabs)
            {
                if (slab.Kind == SlabKind.Wall && slab.Blocks(x, z, y + StepOffset + 0.02f, y + PipHeight, PipRadius))
                {
                    return slab.Name;
                }
            }

            return null;
        }

        private static StageSlab[] BuildSlabs()
        {
            var s = new List<StageSlab>();

            // ---- Shed ramp (routes.py build_shed_ramp): decks, sheds, guard rails at the deck edges. ----
            float leg1X = (Leg1MinX + Leg1MaxX) * 0.5f;
            float legWidth = Leg1MaxX - Leg1MinX; // 1.9
            float leg2Z = (Leg1EndZ + RampNorthZ) * 0.5f;
            float leg2Width = RampNorthZ - Leg1EndZ; // 1.7
            Walk(s, "Ramp_Leg1", SlabGroup.Route, leg1X, 0f, Leg1StartZ, leg1X, LandingY, Leg1EndZ, legWidth, 0.4f);
            Walk(s, "Ramp_Landing", SlabGroup.Route, leg1X, LandingY, Leg1EndZ, leg1X, LandingY, RampNorthZ, legWidth, LandingY);
            Walk(s, "Ramp_Leg2", SlabGroup.Route, Leg1MaxX, LandingY, leg2Z, Leg2EndX, TreehouseY, leg2Z, leg2Width, 0.4f);
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
                LandingY + 1.0f, Leg1EndZ, 0.1f, 3f);
            Wall(s, "Ramp_Leg1East", SlabGroup.Route, Leg1MaxX + 0.05f, 1.0f, Leg1StartZ + 0.15f, Leg1MaxX + 0.05f,
                LandingY + 1.0f, Leg1EndZ - 0.05f, 0.1f, 3f);
            Wall(s, "Ramp_LandingWest", SlabGroup.Route, Leg1MinX - 0.05f, LandingY + 1f, Leg1EndZ, Leg1MinX - 0.05f,
                LandingY + 1f, RampNorthZ, 0.1f, LandingY + 1f);
            Wall(s, "Ramp_North", SlabGroup.Route, Leg1MinX, LandingY + 1f, RampNorthZ + 0.05f, Leg1MaxX,
                LandingY + 1f, RampNorthZ + 0.05f, 0.1f, LandingY + 1f);
            Wall(s, "Ramp_Leg2North", SlabGroup.Route, Leg1MaxX, LandingY + 1f, RampNorthZ + 0.05f, Leg2EndX,
                TreehouseY + 1f, RampNorthZ + 0.05f, 0.1f, 3.5f);
            Wall(s, "Ramp_Leg2South", SlabGroup.Route, Leg1MaxX + 0.1f, LandingY + 1f, Leg1EndZ - 0.05f, RoofMinX,
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
            Walk(s, "Bridge", SlabGroup.Route, BridgeStartX - 0.05f, TreehouseY, BridgeZ, BridgeEndX, SillY, BridgeZ,
                BridgeWidth, 0.25f);
            for (int side = -1; side <= 1; side += 2)
            {
                float z = BridgeZ + side * (BridgeWidth * 0.5f + 0.05f);
                Wall(s, side < 0 ? "Bridge_GuardSouth" : "Bridge_GuardNorth", SlabGroup.Route, BridgeStartX + 0.05f,
                    TreehouseY + 0.8f, z, BridgeEndX - 0.05f, SillY + 0.8f, z, 0.08f, 0.8f);
            }

            // ---- Skilled ascent: the awning slope and its wall slice (the crate fits its own bounds). ----
            Walk(s, "Awning_Slope", SlabGroup.Route, AwningX, AwningLowY, AwningWallZ - AwningRun, AwningX, AwningHighY,
                AwningWallZ - 0.06f, AwningWidth, 0.08f);
            Wall(s, "Awning_Wall", SlabGroup.Route, AwningX - 1.3f, AwningWallTopY, AwningWallZ + 0.04f, AwningX + 1.3f,
                AwningWallTopY, AwningWallZ + 0.04f, 0.16f, AwningWallTopY);

            // ---- Treehouse cabin walls on the platform (the deck itself belongs to the street set). ----
            CabinWalls(s);

            // ---- The owl house shell ----
            Walk(s, "House_Floor", SlabGroup.Interior, 0f, FloorY, FrontZ + 0.02f, 0f, FloorY, BackZ, 2f * OuterX, FloorY);
            // Front wall either side of the door, the head over it and the gable above.
            Wall(s, "Shell_FrontWest", SlabGroup.Shell, -OuterX, KneeY, FrontZ + 0.07f, DoorHingeX - 0.05f, KneeY,
                FrontZ + 0.07f, 0.16f, KneeY);
            Wall(s, "Shell_FrontEast", SlabGroup.Shell, DoorMaxX + 0.02f, KneeY, FrontZ + 0.07f, OuterX, KneeY,
                FrontZ + 0.07f, 0.16f, KneeY);
            Wall(s, "Shell_DoorHead", SlabGroup.Shell, DoorHingeX - 0.05f, KneeY, FrontZ + 0.07f, DoorMaxX + 0.02f, KneeY,
                FrontZ + 0.07f, 0.16f, KneeY - DoorTopY);
            Wall(s, "Shell_FrontGable", SlabGroup.Shell, -OuterX, ApexY, FrontZ + 0.07f, OuterX, ApexY, FrontZ + 0.07f,
                0.16f, ApexY - KneeY);
            Wall(s, "Shell_Back", SlabGroup.Shell, -OuterX, ApexY, BackZ - 0.07f, OuterX, ApexY, BackZ - 0.07f, 0.16f, ApexY);
            Wall(s, "Shell_East", SlabGroup.Shell, OuterX - 0.12f, KneeY, FrontZ, OuterX - 0.12f, KneeY, BackZ, 0.24f, KneeY);
            // West wall: full height except under June's window, where it stops at the sill.
            float jambS = WindowZ - WindowHalfWidth;
            float jambN = WindowZ + WindowHalfWidth;
            Wall(s, "Shell_WestSouth", SlabGroup.Shell, -OuterX + 0.12f, KneeY, FrontZ, -OuterX + 0.12f, KneeY,
                AlcoveMinZ, 0.24f, KneeY);
            Wall(s, "Shell_WestNorth", SlabGroup.Shell, -OuterX + 0.12f, KneeY, AlcoveMaxZ, -OuterX + 0.12f, KneeY, BackZ,
                0.24f, KneeY);
            Wall(s, "Shell_WestUnderSill", SlabGroup.Shell, -OuterX + 0.12f, SillY, AlcoveMinZ, -OuterX + 0.12f, SillY,
                AlcoveMaxZ, 0.24f, SillY);
            // The window: jambs to the spring line plus the arch shoulders, a head above the arch's crown clearance.
            float shoulderY = SillY + WindowSpring + 0.37f; // the arch is 0.35 m either side of centre at this height
            Wall(s, "Window_JambSouth", SlabGroup.Shell, -OuterX + 0.12f, shoulderY, AlcoveMinZ, -OuterX + 0.12f,
                shoulderY, jambS, 0.24f, shoulderY - SillY);
            Wall(s, "Window_JambNorth", SlabGroup.Shell, -OuterX + 0.12f, shoulderY, jambN, -OuterX + 0.12f, shoulderY,
                AlcoveMaxZ, 0.24f, shoulderY - SillY);
            Wall(s, "Window_Head", SlabGroup.Shell, -OuterX + 0.12f, 4.6f, AlcoveMinZ, -OuterX + 0.12f, 4.6f, AlcoveMaxZ,
                0.24f, 4.6f - shoulderY);
            Walk(s, "Window_Sill", SlabGroup.Shell, -OuterX - 0.38f, SillY, WindowZ, InnerMinX + 0.05f, SillY, WindowZ,
                2f * WindowHalfWidth, 0.12f);
            // Dormer cheeks and ceiling behind the window.
            Wall(s, "Dormer_South", SlabGroup.Shell, -OuterX, 4.2f, AlcoveMinZ - 0.05f, -1.9f, 4.2f, AlcoveMinZ - 0.05f,
                0.1f, 4.2f - LoftY);
            Wall(s, "Dormer_North", SlabGroup.Shell, -OuterX, 4.2f, AlcoveMaxZ + 0.05f, -1.9f, 4.2f, AlcoveMaxZ + 0.05f,
                0.1f, 4.2f - LoftY);
            Wall(s, "Dormer_Ceiling", SlabGroup.Shell, -OuterX, 4.45f, WindowZ, -1.9f, 4.45f, WindowZ,
                AlcoveMaxZ - AlcoveMinZ, 0.12f);
            // Roof plates following the ceiling slope (open over the dormer on the west side).
            RoofPlate(s, "Roof_East", 1, FrontZ - 0.2f, BackZ + 0.2f);
            RoofPlate(s, "Roof_WestSouth", -1, FrontZ - 0.2f, AlcoveMinZ - 0.05f);
            RoofPlate(s, "Roof_WestNorth", -1, AlcoveMaxZ + 0.05f, BackZ + 0.2f);
            // The door leaf while latched (the runtime disables it once the door is open).
            Wall(s, DoorBlockerName, SlabGroup.Shell, DoorHingeX - 0.05f, DoorTopY, FrontZ + 0.07f, DoorMaxX + 0.02f,
                DoorTopY, FrontZ + 0.07f, 0.16f, DoorTopY - FloorY + 0.05f);

            // ---- Inside: loft (around the stairwell), stair, rails, partition, furniture ----
            Walk(s, "Loft_Main", SlabGroup.Interior, 0f, LoftY, LoftFrontZ, 0f, LoftY, StairMinZ, 2f * InnerMaxX, 0.16f);
            Walk(s, "Loft_West", SlabGroup.Interior, (InnerMinX + StairHoleMinX) * 0.5f, LoftY, StairMinZ,
                (InnerMinX + StairHoleMinX) * 0.5f, LoftY, InnerMaxZ, StairHoleMinX - InnerMinX, 0.16f);
            Walk(s, "Loft_East", SlabGroup.Interior, (StairTopX + InnerMaxX) * 0.5f, LoftY, StairMinZ,
                (StairTopX + InnerMaxX) * 0.5f, LoftY, InnerMaxZ, InnerMaxX - StairTopX, 0.16f);
            float stairZ = (StairMinZ + StairMaxZ) * 0.5f;
            Walk(s, "Stair", SlabGroup.Interior, StairBottomX - 0.05f, FloorY, stairZ, StairTopX + 0.02f, LoftY, stairZ,
                StairMaxZ - StairMinZ, 0.2f);
            Wall(s, "Stair_Stringer", SlabGroup.Interior, StairBottomX, FloorY + 0.9f, StairMinZ - 0.04f, StairTopX,
                LoftY + 0.05f, StairMinZ - 0.04f, 0.08f, 0.9f);
            Wall(s, "Loft_FrontRail", SlabGroup.Interior, InnerMinX, LoftY + 0.98f, LoftFrontZ + 0.08f, InnerMaxX,
                LoftY + 0.98f, LoftFrontZ + 0.08f, 0.1f, 1.1f);
            Wall(s, "Loft_HoleRailSouth", SlabGroup.Interior, StairHoleMinX - 0.07f, LoftY + 0.98f, StairMinZ - 0.07f,
                StairTopX + 0.06f, LoftY + 0.98f, StairMinZ - 0.07f, 0.1f, 0.98f);
            Wall(s, "Loft_HoleRailWest", SlabGroup.Interior, StairHoleMinX - 0.07f, LoftY + 0.98f, StairMinZ - 0.07f,
                StairHoleMinX - 0.07f, LoftY + 0.98f, InnerMaxZ, 0.1f, 0.98f);
            Wall(s, "Workshop_Partition", SlabGroup.Interior, NookX + 0.05f, LoftUnderside, NookMinZ, NookX + 0.05f,
                LoftUnderside, InnerMaxZ, 0.12f, LoftUnderside - NookFloorY);
            Wall(s, "Workshop_South", SlabGroup.Interior, NookX, LoftUnderside, NookMinZ + 0.05f, InnerMaxX, LoftUnderside,
                NookMinZ + 0.05f, 0.12f, LoftUnderside - NookFloorY);
            Wall(s, "Foyer_Bench", SlabGroup.Interior, InnerMinX + 0.37f, FloorY + 0.41f, FrontZ + 1.85f,
                InnerMinX + 0.37f, FloorY + 0.41f, FrontZ + 2.95f, 0.36f, 0.41f);
            Wall(s, "Attic_CraftTable", SlabGroup.Interior, 0.65f, LoftY + 0.47f, FrontZ + 2.35f, 1.75f, LoftY + 0.47f,
                FrontZ + 2.35f, 0.66f, 0.47f);
            Wall(s, "Attic_CratesEast", SlabGroup.Interior, 1.9f, LoftY + 0.72f, FrontZ + 5.3f, 2.55f, LoftY + 0.72f,
                FrontZ + 5.3f, 0.5f, 0.72f);
            Wall(s, "Attic_CratesWest", SlabGroup.Interior, -2.45f, LoftY + 0.36f, FrontZ + 5.35f, -1.95f, LoftY + 0.36f,
                FrontZ + 5.35f, 0.45f, 0.36f);

            // ---- Already built by EnvironmentDressing: the owl house porch deck (route checks only). ----
            Walk(s, "Porch_Deck", SlabGroup.Reference, 0f, FloorY, FrontZ - 2f, 0f, FloorY, FrontZ, 6f, FloorY);
            return s.ToArray();
        }

        /// <summary>Name of the door-leaf blocker that the runtime switches off once the door is open.</summary>
        public const string DoorBlockerName = "Shell_DoorLeaf";

        private static void CabinWalls(List<StageSlab> s)
        {
            // Platform centre in the maple frame (trunk lean, routes.py PLAT) turned by the cabin's yaw.
            const float platX = 0.2064f;
            const float platY = -0.048f;
            const float h = CabinHalf;
            const float top = TreehouseY + 1.45f;
            CabinWall(s, "Cabin_North", platX - h, platY - h, platX + h, platY - h, top);
            CabinWall(s, "Cabin_West", platX + h, platY - h, platX + h, platY + h, top);
            CabinWall(s, "Cabin_Parapet", platX - h, platY + h, platX + h, platY + h, TreehouseY + 0.75f);
            // East wall beside the door (door opening local y -1.02..-0.12).
            CabinWall(s, "Cabin_EastSouth", platX - h, platY - 0.1f, platX - h, platY + h, top);
            CabinWall(s, "Cabin_EastNorth", platX - h, platY - h, platX - h, platY - 1.04f, top);
        }

        private static void CabinWall(List<StageSlab> s, string name, float ax, float ay, float bx, float by, float top)
        {
            CabinToWorld(ax, ay, out float x0, out float z0);
            CabinToWorld(bx, by, out float x1, out float z1);
            Wall(s, name, SlabGroup.Route, x0, top, z0, x1, top, z1, 0.08f, top - TreehouseY);
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

        private static void RoofPlate(List<StageSlab> s, string name, int side, float z0, float z1)
        {
            // Plate top 0.25 m above the ceiling line, from beyond the eave to the ridge.
            float slope = (ApexY - KneeY) / InnerMaxX;
            float eaveX = side * (OuterX + 0.1f);
            float eaveY = KneeY + (InnerMaxX - (OuterX + 0.1f)) * slope + 0.25f;
            float z = (z0 + z1) * 0.5f;
            // Width runs along z (across the slope's line), so the line goes eave → ridge.
            s.Add(new StageSlab(name, SlabKind.Wall, SlabGroup.Shell, eaveX, eaveY, z, 0f, ApexY + 0.25f, z, z1 - z0, 0.22f));
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
