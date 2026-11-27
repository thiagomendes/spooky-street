using System.Collections.Generic;
using SpookyStreet.App;
using SpookyStreet.Core.LivingStreet;
using SpookyStreet.Core.World;
using SpookyStreet.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpookyStreet.LivingStreet
{
    /// <summary>
    /// The east lane in the world (living-street §2.2, §9 phase 1): the three porch houses (the street set's reviewed
    /// neighbour houses, now walkable up to their steps), the lane's hedges and sidewalk, lamps and pumpkins, the Green's
    /// first corner with the lantern table under string lights, and on Pip's own porch a crook post for her lantern.
    /// Static pieces sit under one batched root, culled as a whole with the lane (§8 sectors); each door has a hinged
    /// leaf (the door gag rig, never a blend shape), a lit doorway behind it and its neighbour, hidden until the door
    /// opens. Same pattern as <see cref="EnvironmentDressing"/>: without models it falls back to plain boxes.
    /// </summary>
    public sealed class LivingStreetSet : MonoBehaviour
    {
        /// <summary>One door's rig in the world.</summary>
        public sealed class DoorRig
        {
            public NeighbourDoor Door;
            public Transform Hinge;
            public Transform Leaf;
            public Renderer Lamp;
            public Material LampRest;
            public Material LampBright;
            public readonly List<CrowdBody> Figures = new List<CrowdBody>();
            public readonly List<Vector3> FigureRest = new List<Vector3>();
            public float Clock;
        }

        private readonly List<DoorRig> _doors = new List<DoorRig>();
        private readonly List<Renderer> _static = new List<Renderer>();
        private MaterialLibrary _materials;
        private Transform _lanternShape;
        private ModelKit _env;

        /// <summary>
        /// The swap point for the slice's pieces that have no art yet (living-street §7, crowd-art doc "not done"): when the
        /// art pass commits a model under these names in <c>Models/Environment</c>, it replaces the paper placeholder.
        /// </summary>
        public static class ArtNames
        {
            /// <summary>A front door leaf, pivot on its hinge edge at the sill, closed along +X, front toward -Z.</summary>
            public const string DoorLeaf = "LivingStreet_DoorLeaf";

            /// <summary>Pip's paper lantern in a shape (D35: bat, cat, ghost, leaf), about 0.4 m, paper parts tinted.</summary>
            public static string Lantern(LanternShape shape) => "LivingStreet_Lantern_" + LanternCraft.ShapeId(shape);
        }
        private readonly ICrowdBodyFactory _kitBodies = new CrowdKitBodyFactory();
        private readonly ICrowdBodyFactory _placeholders = new PlaceholderCrowdBodies();

        public Transform StaticRoot { get; private set; }

        public IReadOnlyList<DoorRig> Doors => _doors;

        /// <summary>The crook post on Pip's porch and the lantern hanging from it (shown once she has folded one).</summary>
        public Transform PorchLantern { get; private set; }

        /// <summary>True while Pip's lantern hangs lit on her porch.</summary>
        public bool PorchLanternLit => PorchLantern != null && PorchLantern.gameObject.activeSelf;

        /// <summary>The lantern Pip folded (what the porch shows).</summary>
        public PipLantern? ShownLantern { get; private set; }

        /// <summary>True when the house models were spawned (false: greybox boxes).</summary>
        public bool UsesModels { get; private set; }

        /// <summary>Instances spawned (tests, the log).</summary>
        public int Spawned { get; private set; }

        public static LivingStreetSet Create(Transform parent, MaterialLibrary materials, EnvironmentDressing environment)
        {
            var go = new GameObject("LivingStreet_EastLane");
            go.transform.SetParent(parent, false);
            var set = go.AddComponent<LivingStreetSet>();
            set.Build(materials, environment);
            return set;
        }

        private void Build(MaterialLibrary materials, EnvironmentDressing environment)
        {
            _materials = materials;
            float correction = environment != null ? environment.ModelYawCorrection : 0f;
            bool models = environment != null && environment.UsesModels;
            bool streetSet = environment != null && environment.UsesStreetSet;
            StaticRoot = new GameObject("LivingStreet_Static").transform;
            StaticRoot.SetParent(transform, false);

            var env = materials != null ? new ModelKit(materials, EnvironmentDressing.ModelFolder) { FallbackAtlas = ModelKit.DefaultFallbackAtlas } : null;
            _env = env;
            var street = materials != null ? new ModelKit(materials, EnvironmentDressing.ModelFolder) { FallbackAtlas = EnvironmentLayout.StreetAtlas } : null;

            foreach (var door in LivingStreetLayout.Doors)
            {
                var house = models && street != null
                    ? street.Spawn(door.House, StaticRoot, new Vector3(door.X, 0f, door.Z), door.Yaw + correction)
                    : null;
                if (house != null)
                {
                    UsesModels = true;
                    Spawned++;
                    ModelKit.AddBoundsCollider(house);
                }
                else
                {
                    EnvironmentLayout.Size(door.House, out float w, out float d);
                    var box = Box(StaticRoot, "House_" + door.Id, new Vector3(door.X, 3f, door.Z), new Vector3(w * 0.8f, 6f, d * 0.7f),
                        Palette.HouseIndigo, true);
                    box.transform.localRotation = Quaternion.Euler(0f, door.Yaw, 0f);
                }

                _doors.Add(BuildDoor(door));
            }

            if (streetSet && street != null)
            {
                foreach (var p in LivingStreetLayout.Hedges)
                {
                    Spawned += street.Spawn(p.Model, StaticRoot, new Vector3(p.X, p.Y, p.Z), p.Yaw + correction, p.Scale) != null ? 1 : 0;
                }

                foreach (var p in LivingStreetLayout.SidewalkTiles)
                {
                    Spawned += street.Spawn(p.Model, StaticRoot, new Vector3(p.X, p.Y, p.Z), p.Yaw + correction, p.Scale) != null ? 1 : 0;
                }
            }
            else
            {
                foreach (var p in LivingStreetLayout.Hedges)
                {
                    Box(StaticRoot, "LaneHedge", new Vector3(p.X, 0.65f, p.Z), new Vector3(p.HalfX * 2f, 1.3f, p.HalfZ * 2f), Palette.Hedge, false);
                }
            }

            if (models && env != null)
            {
                foreach (var p in LivingStreetLayout.Props)
                {
                    var prop = env.Spawn(p.Model, StaticRoot, new Vector3(p.X, p.Y, p.Z), p.Yaw + correction, p.Scale);
                    if (prop == null)
                    {
                        continue;
                    }

                    Spawned++;
                    if (p.Collider == PlacementCollider.Pole)
                    {
                        var pole = prop.AddComponent<BoxCollider>();
                        pole.center = new Vector3(0f, 1.55f, 0f);
                        pole.size = new Vector3(0.3f, 3.1f, 0.3f);
                    }
                    else if (p.Collider == PlacementCollider.Bounds)
                    {
                        ModelKit.AddBoundsCollider(prop);
                    }
                }
            }
            else
            {
                Box(StaticRoot, "LanternTable", new Vector3(LivingStreetLayout.TableX, 0.5f, LivingStreetLayout.TableZ),
                    new Vector3(1.6f, 1f, 0.9f), Palette.Timber, true);
            }

            BuildGreenCorner();
            BuildPorchPost();

            foreach (var r in StaticRoot.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
                _static.Add(r);
            }

            StaticBatchingUtility.Combine(StaticRoot.gameObject);
        }

        /// <summary>Paper lanterns on the table and string lights between three posts over it (the Green's first corner).</summary>
        private void BuildGreenCorner()
        {
            var posts = LivingStreetLayout.StringLightPosts;
            float top = LivingStreetLayout.StringLightHeight;
            var glowWarm = _materials != null ? _materials.Glow(Palette.CandyCornYellow, 1.1f) : null;
            var glowPlum = _materials != null ? _materials.Glow(Palette.Hex(0xC07AB8), 1.0f) : null;
            for (int i = 0; i < posts.Length; i++)
            {
                var p = posts[i];
                Primitive(PrimitiveType.Cylinder, StaticRoot, "LightPost" + i, new Vector3(p.X, top * 0.5f, p.Z),
                    new Vector3(0.09f, top * 0.5f, 0.09f), Palette.Timber, null);
                var next = posts[(i + 1) % posts.Length];
                var a = new Vector3(p.X, top, p.Z);
                var b = new Vector3(next.X, top, next.Z);
                int bulbs = Mathf.Max(4, Mathf.RoundToInt(Vector3.Distance(a, b) / 0.6f));
                for (int k = 1; k < bulbs; k++)
                {
                    float t = k / (float)bulbs;
                    var at = Vector3.Lerp(a, b, t) + Vector3.down * (Mathf.Sin(t * Mathf.PI) * 0.45f);
                    Primitive(PrimitiveType.Sphere, StaticRoot, "Bulb", at, Vector3.one * 0.11f, Palette.CandyCornYellow,
                        k % 3 == 0 ? glowPlum : glowWarm);
                }
            }

            // Three folded lanterns waiting on the table: a moon, a star and a pumpkin, in paper colours.
            var table = new Vector3(LivingStreetLayout.TableX, 1.12f, LivingStreetLayout.TableZ);
            var shapes = new[] { LanternShape.Cat, LanternShape.Ghost, LanternShape.Leaf };
            var colours = new[] { LanternColour.Yellow, LanternColour.Plum, LanternColour.Orange };
            for (int i = 0; i < shapes.Length; i++)
            {
                var lantern = BuildLantern(StaticRoot, new PipLantern(shapes[i], colours[i]), 0.55f);
                lantern.position = table + new Vector3((i - 1) * 0.45f, 0.08f, (i % 2) * 0.12f);
            }
        }

        /// <summary>A crook post on the west rail's front corner of Pip's porch, with a little bell under its arm.</summary>
        private void BuildPorchPost()
        {
            float x = LivingStreetLayout.LanternPostX;
            float z = LivingStreetLayout.LanternPostZ;
            float deck = 0.6f;
            float y = LivingStreetLayout.LanternY;
            Primitive(PrimitiveType.Cylinder, StaticRoot, "LanternPost", new Vector3(x, deck + (y + 0.45f - deck) * 0.5f, z),
                new Vector3(0.07f, (y + 0.45f - deck) * 0.5f, 0.07f), Palette.Timber, null);
            Primitive(PrimitiveType.Cube, StaticRoot, "LanternArm", new Vector3(x + 0.22f, y + 0.42f, z),
                new Vector3(0.5f, 0.05f, 0.05f), Palette.Timber, null);
            Primitive(PrimitiveType.Cylinder, StaticRoot, "PipBell", new Vector3(x + 0.08f, y - 0.35f, z),
                new Vector3(0.1f, 0.05f, 0.1f), Palette.CandyCornYellow, null);

            PorchLantern = new GameObject("PipLantern").transform;
            PorchLantern.SetParent(transform, false);
            PorchLantern.position = new Vector3(x + 0.42f, y, z);
            PorchLantern.gameObject.SetActive(false);
        }

        /// <summary>Hangs Pip's folded lantern, lit, on her porch (null takes it down: a New game).</summary>
        public void ShowLantern(PipLantern? lantern)
        {
            ShownLantern = lantern;
            if (_lanternShape != null)
            {
                Destroy(_lanternShape.gameObject);
                _lanternShape = null;
            }

            if (!lantern.HasValue)
            {
                PorchLantern.gameObject.SetActive(false);
                return;
            }

            _lanternShape = BuildLantern(PorchLantern, lantern.Value, 1f);
            _lanternShape.localPosition = Vector3.zero;
            PorchLantern.gameObject.SetActive(true);
        }

        /// <summary>A paper lantern of a shape and colour, about 0.4 m, glowing from inside (a string loop on top).</summary>
        public Transform BuildLantern(Transform parent, PipLantern lantern, float size)
        {
            var root = new GameObject("Lantern_" + lantern).transform;
            root.SetParent(parent, false);
            var colour = Palette.Hex(LanternCraft.ColourRgb(lantern.Colour));
            var glow = _materials != null ? _materials.Glow(colour, GlowPipLantern) : null;

            // Swap point: the lantern's own model when the art pass commits it (tinted with the chosen paper colour).
            var art = _env != null ? _env.Spawn(ArtNames.Lantern(lantern.Shape), root, Vector3.zero, 0f, size) : null;
            if (art != null)
            {
                foreach (var r in art.GetComponentsInChildren<Renderer>(true))
                {
                    r.sharedMaterial = glow != null ? glow : r.sharedMaterial;
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }

                return root;
            }

            var ink = Palette.InkBlack;
            float s = 0.38f * size;
            switch (lantern.Shape)
            {
                case LanternShape.Cat:
                    // A round head with two pointed ears and two ink eyes.
                    Primitive(PrimitiveType.Sphere, root, "Head", Vector3.zero, new Vector3(s * 0.7f, s * 0.62f, s * 0.55f), colour, glow);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Primitive(PrimitiveType.Cube, root, "Ear", new Vector3(side * s * 0.2f, s * 0.3f, 0f),
                            new Vector3(s * 0.2f, s * 0.2f, 0.05f * size), colour, glow).transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                        Primitive(PrimitiveType.Sphere, root, "Eye", new Vector3(side * s * 0.12f, s * 0.04f, -s * 0.27f),
                            Vector3.one * s * 0.09f, ink, null);
                    }

                    break;
                case LanternShape.Ghost:
                    // A friendly sheet ghost: a round top over a short sheet, two ink eyes.
                    Primitive(PrimitiveType.Sphere, root, "Top", new Vector3(0f, s * 0.12f, 0f), new Vector3(s * 0.6f, s * 0.6f, s * 0.5f), colour, glow);
                    Primitive(PrimitiveType.Cylinder, root, "Sheet", new Vector3(0f, -s * 0.12f, 0f), new Vector3(s * 0.6f, s * 0.22f, s * 0.5f), colour, glow);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Primitive(PrimitiveType.Sphere, root, "Eye", new Vector3(side * s * 0.11f, s * 0.14f, -s * 0.24f),
                            Vector3.one * s * 0.09f, ink, null);
                    }

                    break;
                case LanternShape.Bat:
                    Primitive(PrimitiveType.Sphere, root, "Body", Vector3.zero, new Vector3(s * 0.55f, s * 0.6f, s * 0.5f), colour, glow);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Primitive(PrimitiveType.Cube, root, "Wing", new Vector3(side * s * 0.5f, s * 0.08f, 0f),
                            new Vector3(s * 0.6f, s * 0.32f, 0.03f * size), colour, glow).transform.localRotation =
                            Quaternion.Euler(0f, 0f, side * -18f);
                    }

                    break;
                default:
                    // A three-lobed maple leaf, flat paper, with its stem.
                    Primitive(PrimitiveType.Cube, root, "Lobe", new Vector3(0f, s * 0.1f, 0f), new Vector3(s * 0.4f, s * 0.4f, 0.05f * size),
                        colour, glow).transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    for (int side = -1; side <= 1; side += 2)
                    {
                        Primitive(PrimitiveType.Cube, root, "Lobe", new Vector3(side * s * 0.2f, -s * 0.03f, 0f),
                            new Vector3(s * 0.3f, s * 0.3f, 0.05f * size), colour, glow).transform.localRotation = Quaternion.Euler(0f, 0f, 45f);
                    }

                    Primitive(PrimitiveType.Cylinder, root, "Stem", new Vector3(0f, -s * 0.32f, 0f), new Vector3(0.02f * size, s * 0.12f, 0.02f * size),
                        Palette.Hex(0x6B5A4E), null);
                    break;
            }

            // A paper string to the hook.
            Primitive(PrimitiveType.Cylinder, root, "String", new Vector3(0f, s * 0.55f, 0f), new Vector3(0.012f, s * 0.22f, 0.012f), ink, null);
            return root;
        }

        /// <summary>Pip's lantern glows a little under the street lamps and far under the attic window (GlowLevels).</summary>
        public const float GlowPipLantern = 1.3f;

        private DoorRig BuildDoor(NeighbourDoor door)
        {
            var rig = new DoorRig { Door = door };
            var frame = new GameObject("Door_" + door.Id).transform;
            frame.SetParent(transform, false);
            frame.position = new Vector3(door.X, 0f, door.Z);
            frame.rotation = Quaternion.Euler(0f, door.Yaw, 0f);

            // The lit doorway behind the leaf (seen only while the door is open).
            var doorway = Primitive(PrimitiveType.Cube, frame, "Doorway",
                new Vector3(door.DoorLocalX, LivingStreetLayout.DoorSill + LivingStreetLayout.DoorHeight * 0.5f, door.WallLocalZ - 0.045f),
                new Vector3(LivingStreetLayout.DoorWidth - 0.06f, LivingStreetLayout.DoorHeight - 0.04f, 0.01f), Palette.PumpkinAmber,
                _materials != null ? _materials.Glow(Palette.PumpkinAmber, 0.85f) : null);
            doorway.shadowCastingMode = ShadowCastingMode.Off;

            // The leaf on its hinge (the door's left edge, seen from the street); it swings out toward the visitor.
            rig.Hinge = new GameObject("Hinge").transform;
            rig.Hinge.SetParent(frame, false);
            rig.Hinge.localPosition = new Vector3(door.DoorLocalX - LivingStreetLayout.DoorWidth * 0.5f, LivingStreetLayout.DoorSill,
                door.WallLocalZ - 0.08f);
            Color paint = door.Id == LivingStreetLayout.Abbotts ? Palette.Hex(0xB5602A)
                : door.Id == LivingStreetLayout.Pearl ? Palette.HauntedPurple : Palette.Plum;
            var leafArt = _env != null ? _env.Spawn(ArtNames.DoorLeaf, rig.Hinge, Vector3.zero) : null;
            var leaf = leafArt != null ? leafArt.GetComponentInChildren<Renderer>() : Primitive(PrimitiveType.Cube, rig.Hinge, "Leaf",
                new Vector3(LivingStreetLayout.DoorWidth * 0.5f, LivingStreetLayout.DoorHeight * 0.5f, 0f),
                new Vector3(LivingStreetLayout.DoorWidth, LivingStreetLayout.DoorHeight, 0.05f), paint, null);
            rig.Leaf = leaf.transform;
            Primitive(PrimitiveType.Sphere, rig.Hinge, "Knob", new Vector3(LivingStreetLayout.DoorWidth - 0.14f, 1.0f, -0.05f),
                Vector3.one * 0.07f, Palette.CandyCornYellow, null);

            // The porch lamp by the door (Nana Pearl's warms up while she answers) and the bell button.
            var lampAt = new Vector3(door.DoorLocalX + LivingStreetLayout.DoorWidth * 0.5f + 0.32f, LivingStreetLayout.DoorSill + 1.85f,
                door.WallLocalZ - 0.12f);
            rig.LampRest = _materials != null ? _materials.Glow(Palette.PumpkinAmber, 0.9f) : null;
            rig.LampBright = _materials != null ? _materials.Glow(Palette.PumpkinAmber, 0.9f + door.Gag.LampBoost) : null;
            rig.Lamp = Primitive(PrimitiveType.Sphere, frame, "PorchLamp", lampAt, Vector3.one * 0.2f, Palette.PumpkinAmber, rig.LampRest);
            Primitive(PrimitiveType.Cylinder, frame, "Doorbell",
                new Vector3(door.DoorLocalX + LivingStreetLayout.DoorWidth * 0.5f + 0.18f, LivingStreetLayout.DoorSill + 1.1f, door.WallLocalZ - 0.03f),
                new Vector3(0.07f, 0.01f, 0.07f), Palette.CandyCornYellow, null).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);

            // The neighbour(s) in the doorway, facing the street: the crowd kit's grown-up in house clothes (their own art
            // comes later; the kit factory is the swap point), the placeholder if the kit is missing.
            var ids = LivingStreetLayout.NeighboursAt(door);
            for (int i = 0; i < ids.Count; i++)
            {
                var look = new WalkerLook(ids[i]);
                var body = _kitBodies.Create(look, frame, _materials) ?? _placeholders.Create(look, frame, _materials);
                float across = ids.Count == 1 ? 0.18f : (i == 0 ? -0.22f : 0.38f);
                var rest = new Vector3(door.DoorLocalX + across, LivingStreetLayout.DoorSill, door.WallLocalZ - 0.3f);
                body.Root.localPosition = rest;
                // Bodies face +Z in their own frame; the house front faces -Z, so the neighbour turns to the street.
                body.Root.localRotation = Quaternion.Euler(0f, 180f, 0f);
                body.SetDetail(WalkerDetail.Hidden);
                rig.Figures.Add(body);
                rig.FigureRest.Add(rest);
            }

            return rig;
        }

        /// <summary>Shows the doors as the Core routine says: the leaf's swing, the neighbour, the lamp.</summary>
        public void ApplyDoors(NeighbourDoors doors, float dt)
        {
            foreach (var rig in _doors)
            {
                string id = rig.Door.Id;
                rig.Hinge.localRotation = Quaternion.Euler(0f, doors.SwingDegrees(id), 0f);
                bool shown = doors.NeighbourShown(id);
                rig.Clock += dt;
                for (int i = 0; i < rig.Figures.Count; i++)
                {
                    var body = rig.Figures[i];
                    body.SetDetail(shown ? WalkerDetail.Full : WalkerDetail.Hidden);
                    if (!shown)
                    {
                        continue;
                    }

                    float hop = rig.Door.Gag.Hop ? Mathf.Abs(Mathf.Sin((rig.Clock + i * 0.25f) * 6f)) * 0.12f : 0f;
                    body.Root.localPosition = rig.FigureRest[i] + Vector3.up * hop;
                    body.Animate(rig.Clock, 0f, doors.LastRinger(id) == Ringer.Group ? WalkerGesture.Cheer : WalkerGesture.Look, dt);
                }

                if (rig.Lamp != null && rig.LampBright != null)
                {
                    rig.Lamp.sharedMaterial = shown && rig.Door.Gag.LampBoost > 0f ? rig.LampBright : rig.LampRest;
                }
            }
        }

        /// <summary>World position of a door's centre at head height (the caption's direction and the treat's start).</summary>
        public static Vector3 DoorPoint(NeighbourDoor door) => new Vector3(door.DoorX, LivingStreetLayout.DoorSill + 1.5f, door.DoorZ);

        private Renderer Primitive(PrimitiveType type, Transform parent, string name, Vector3 at, Vector3 scale, Color color,
            Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            if (material != null)
            {
                renderer.sharedMaterial = material;
            }
            else
            {
                _materials?.Apply(renderer, color);
            }

            renderer.shadowCastingMode = ShadowCastingMode.Off;
            return renderer;
        }

        private GameObject Box(Transform parent, string name, Vector3 at, Vector3 size, Color color, bool solid)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.localPosition = at;
            go.transform.localScale = size;
            _materials?.Apply(go.GetComponent<Renderer>(), color);
            if (!solid)
            {
                Destroy(go.GetComponent<Collider>());
            }

            return go;
        }
    }
}
