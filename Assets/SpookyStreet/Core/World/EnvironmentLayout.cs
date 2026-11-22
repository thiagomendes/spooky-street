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
            new Placement(Bench, 8f, 0f, 0f, 144f, collider: PlacementCollider.Bounds, halfX: 0.75f, halfZ: 0.75f),
            new Placement(Pumpkin, 1.45f, 0f, 2.4f, -30f, collider: PlacementCollider.Bounds, halfX: 0.2f, halfZ: 0.2f),

            // Front yard (z = 5): hedge west of the gate, the gate swung open on the path, picket fence east.
            new Placement(JackOLantern, 1.45f, 0f, 4.2f, -12f, collider: PlacementCollider.Bounds, halfX: 0.35f, halfZ: 0.35f),
            new Placement(Pumpkin, 2.3f, 0f, 4.35f, 15f, collider: PlacementCollider.Bounds, halfX: 0.2f, halfZ: 0.2f),
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
