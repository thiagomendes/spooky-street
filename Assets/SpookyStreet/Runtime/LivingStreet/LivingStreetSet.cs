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
    /// The east lane in the world (living-street §2.2, §9 phase 1): the three porch houses (Mr. Fogg's, Nana Pearl's and
    /// the Abbotts', docs/living-street-houses-art.md, walkable up to their steps), the lane's hedges and sidewalk, lamps
    /// and pumpkins, the Green's first corner with the lantern table under string lights, and on Pip's own porch a crook
    /// post for her lantern. Static pieces sit under one batched root, culled as a whole with the lane (§8 sectors); each
    /// door has its own hinged leaf (the door gag rig, never a blend shape), its porch lamp and its neighbour, hidden until
    /// the door opens; each house carries its own push button and lit hall. Same pattern as
    /// <see cref="EnvironmentDressing"/>: without models it falls back to plain boxes, a paper leaf, a flat lit doorway
    /// and a bell disc.
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
            /// <summary>The small warm light in front of the door (Mr. Fogg's and Nana Pearl's), or null.</summary>
            public Light PorchLight;
            /// <summary>The house model (null when the house fell back to a box).</summary>
            public Transform House;
            public readonly List<CrowdBody> Figures = new List<CrowdBody>();
            public readonly List<Vector3> FigureRest = new List<Vector3>();
            public float Clock;

            /// <summary>The warm light that comes on in front of the door at the first ring (someone is coming).</summary>
            public Light HallLight;

            /// <summary>Dark glass over the lit window beside the door while the hall light is off.</summary>
            public Renderer WindowCover;

            /// <summary>The neighbour's shadow crossing that window after the second ring.</summary>
            public Renderer Silhouette;

            public Mesh SilhouetteMesh;
            public Vector3[] SilhouetteVerts;
        }

        /// <summary>The hall light's level once on, its reach and where it hangs (in front of the door, under the hood).</summary>
        public const float HallLightIntensity = 0.75f;
        public const float HallLightRange = 3.2f;

        /// <summary>The shadow and the dark glass sit in the window, between the lit room card and the paper cut-outs.</summary>
        public const float WindowCoverDepth = -0.0109f;
        public const float SilhouetteDepth = -0.0116f;

        private readonly List<DoorRig> _doors = new List<DoorRig>();
        private readonly List<Renderer> _static = new List<Renderer>();
        private MaterialLibrary _materials;
        private Transform _lanternShape;
        private ModelKit _lanterns;
        private Transform _bellButton;
        private Vector3 _bellRest;
        private float _bellPress;
        private Vector3 _bellDirection = Vector3.back;

        /// <summary>The lantern craft table's model (null when it falls back to a box).</summary>
        public Transform TableArt { get; private set; }

        /// <summary>Pip's porch hook (the LanternHook model; null when it falls back to a plain post).</summary>
        public Transform Hook { get; private set; }

        /// <summary>The push button on the hook post that groups press to ring (never a bell shape, §3.4 rule 6).</summary>
        public Transform BellButton => _bellButton;

        /// <summary>How far the button is pressed in now (0 rest, 1 fully in).</summary>
        public float BellPressed => _bellPress;

        /// <summary>
        /// The lane's art (docs/living-street-houses-art.md, "The swap"), all in <see cref="LaneHouseArt.Folder"/>: each
        /// door's house, leaf and porch lamp. When a model is missing its paper placeholder stands in.
        /// </summary>
        public static class ArtNames
        {
            /// <summary>The house (its own atlas, LOD group, lit rooms and hall, push button).</summary>
            public static string House(NeighbourDoor door) => LaneHouseArt.House(door.Id);

            /// <summary>The door's leaf, pivot on its hinge edge at the sill, closed along +X, front toward -Z.</summary>
            public static string DoorLeaf(NeighbourDoor door) => LaneHouseArt.DoorLeaf(door.Id);

            /// <summary>The porch lamp, origin in its glass (<see cref="LaneHouseArt.LampGlow"/>).</summary>
            public static string PorchLamp(NeighbourDoor door) => LaneHouseArt.PorchLamp(door.Id);
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
            _lanterns = materials != null ? PaperLanternArt.Kit(materials) : null;
            float correction = environment != null ? environment.ModelYawCorrection : 0f;
            bool models = environment != null && environment.UsesModels;
            bool streetSet = environment != null && environment.UsesStreetSet;
            StaticRoot = new GameObject("LivingStreet_Static").transform;
            StaticRoot.SetParent(transform, false);

            var env = materials != null ? new ModelKit(materials, EnvironmentDressing.ModelFolder) { FallbackAtlas = ModelKit.DefaultFallbackAtlas } : null;
            var street = materials != null ? new ModelKit(materials, EnvironmentDressing.ModelFolder) { FallbackAtlas = EnvironmentLayout.StreetAtlas } : null;

            // Each lane house finds its atlas by its own name; its leaf and lamp share it (a kit per door, below).
            var houses = materials != null ? new ModelKit(materials, LaneHouseArt.Folder) { FallbackAtlas = null } : null;
            foreach (var door in LivingStreetLayout.Doors)
            {
                var house = models && houses != null
                    ? houses.Spawn(ArtNames.House(door), StaticRoot, new Vector3(door.X, 0f, door.Z), door.Yaw + correction)
                    : null;
                if (house != null)
                {
                    UsesModels = true;
                    Spawned++;
                    ModelKit.AddBoundsCollider(house);
                }
                else
                {
                    LaneHouseArt.Footprint(door.Id, out float w, out float d);
                    var box = Box(StaticRoot, "House_" + door.Id, new Vector3(door.X, 3f, door.Z), new Vector3(w * 0.8f, 6f, d * 0.7f),
                        Palette.HouseIndigo, true);
                    box.transform.localRotation = Quaternion.Euler(0f, door.Yaw, 0f);
                }

                var parts = house != null ? new ModelKit(materials, LaneHouseArt.Folder) { FallbackAtlas = LaneHouseArt.Atlas(door.Id) } : null;
                var rig = BuildDoor(door, parts, correction);
                rig.House = house != null ? house.transform : null;
                _doors.Add(rig);
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
                    if (p.Model == LanternArt.Table)
                    {
                        continue; // the lantern art's own kit and atlas (below)
                    }

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

            // The lantern craft table (docs/living-street-lantern-art.md): its own string of four lit samples.
            var table = models && _lanterns != null
                ? _lanterns.Spawn(LanternArt.Table, StaticRoot, new Vector3(LivingStreetLayout.TableX, 0f, LivingStreetLayout.TableZ),
                    LivingStreetLayout.TableYaw + correction, 1f, solid: true)
                : null;
            if (table != null)
            {
                Spawned++;
                TableArt = table.transform;
            }
            else if (models)
            {
                Box(StaticRoot, "LanternTable", new Vector3(LivingStreetLayout.TableX, 0.5f, LivingStreetLayout.TableZ),
                    new Vector3(1.3f, 1f, 0.6f), Palette.Timber, true);
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
        }

        /// <summary>
        /// Pip's porch hook (docs/living-street-lantern-art.md): the LanternHook model clamped to the west rail's front
        /// corner, its fretwork arm reaching over the lawn; her lantern hangs from its <c>LanternHang</c> child, and groups
        /// press its <c>BellButton</c> (a push button, never a hanging bell: a bell is one of the Players' motifs). Outside
        /// the batched root so the button can move. A plain post is the fallback when the art is missing.
        /// </summary>
        private void BuildPorchPost()
        {
            var at = new Vector3(LanternArt.PorchHookX, LanternArt.PorchHookY, LanternArt.PorchHookZ);
            var hook = _lanterns != null ? _lanterns.Spawn(LanternArt.Hook, transform, at, LanternArt.PorchHookYaw) : null;
            Vector3 hang;
            if (hook != null)
            {
                Spawned++;
                Hook = hook.transform;
                foreach (var r in hook.GetComponentsInChildren<Renderer>(true))
                {
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }

                hang = PaperLanternArt.HangPoint(hook) ?? at + new Vector3(-LanternArt.HangX, LanternArt.HangHeight, 0f);
                _bellButton = ModelKit.FindDeep(hook.transform, LanternArt.BellButton);
            }
            else
            {
                float x = LivingStreetLayout.LanternPostX;
                float z = LivingStreetLayout.LanternPostZ;
                float deck = 0.6f;
                float y = LivingStreetLayout.LanternY;
                Primitive(PrimitiveType.Cylinder, StaticRoot, "LanternPost", new Vector3(x, deck + (y + 0.45f - deck) * 0.5f, z),
                    new Vector3(0.07f, (y + 0.45f - deck) * 0.5f, 0.07f), Palette.Timber, null);
                Primitive(PrimitiveType.Cube, StaticRoot, "LanternArm", new Vector3(x - 0.22f, y + 0.42f, z),
                    new Vector3(0.5f, 0.05f, 0.05f), Palette.Timber, null);
                var button = Primitive(PrimitiveType.Cylinder, transform, "BellButton", new Vector3(x, deck + LanternArt.BellHeight, z + 0.06f),
                    new Vector3(0.08f, 0.01f, 0.08f), Palette.WarmPaper, null);
                button.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
                _bellButton = button.transform;
                hang = new Vector3(x - 0.42f, y + 0.4f, z);
            }

            if (_bellButton != null)
            {
                // The button goes into the post: from its rest, toward the post's axis (horizontally).
                _bellRest = _bellButton.position;
                var into = at - _bellRest;
                into.y = 0f;
                _bellDirection = into.sqrMagnitude > 1e-6f ? into.normalized : Vector3.back;
            }

            PorchLantern = new GameObject("PipLantern").transform;
            PorchLantern.SetParent(transform, false);
            PorchLantern.position = hang;
            PorchLantern.gameObject.SetActive(false);
        }

        /// <summary>A group (or Pip) presses the porch button: it goes in about 8 mm along the hook's front and springs back.</summary>
        public void PressBell() => _bellPress = 1f;

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
            var outer = new GameObject("Lantern_" + lantern).transform;
            var root = outer;
            root.SetParent(parent, false);
            var colour = Palette.Hex(LanternCraft.ColourRgb(lantern.Colour));
            var glow = _materials != null ? _materials.Glow(colour, GlowPipLantern) : null;

            // The lantern art (docs/living-street-lantern-art.md): the figure lantern hangs from its ring at the origin, and
            // only its PaperLantern_Glow child takes the chosen paper colour (ink, wire and tassel stay ink).
            var art = _lanterns != null
                ? PaperLanternArt.SpawnLantern(_lanterns, LanternCraft.ShapeId(lantern.Shape), LanternCraft.ColourId(lantern.Colour),
                    root, Vector3.zero, 0f, size)
                : null;
            if (art != null)
            {
                foreach (var r in art.GetComponentsInChildren<Renderer>(true))
                {
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }

                return root;
            }

            // Fallback (no art committed): paper primitives, centred a little under the hang point.
            var hangOffset = new GameObject("Hang").transform;
            hangOffset.SetParent(root, false);
            hangOffset.localPosition = new Vector3(0f, -0.25f * size, 0f);
            root = hangOffset;

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
            return outer;
        }

        /// <summary>Pip's lantern glows a little under the street lamps and far under the attic window (GlowLevels).</summary>
        public const float GlowPipLantern = 1.3f;

        /// <summary>
        /// One door: the leaf on its hinge, the porch lamp, the porch light and the neighbour(s). With the house's art
        /// (<paramref name="art"/>, the house's own atlas) the leaf and lamp are its models and the house carries the push
        /// button and the lit hall; without it, paper placeholders (a flat leaf with a knob, a lamp ball, a bell disc and a
        /// flat lit doorway card).
        /// </summary>
        private DoorRig BuildDoor(NeighbourDoor door, ModelKit art, float correction)
        {
            var rig = new DoorRig { Door = door };
            var frame = new GameObject("Door_" + door.Id).transform;
            frame.SetParent(transform, false);
            frame.position = new Vector3(door.X, 0f, door.Z);
            frame.rotation = Quaternion.Euler(0f, door.Yaw, 0f);

            // The leaf on its hinge (the door's left edge, seen from the street); it swings out toward the visitor.
            rig.Hinge = new GameObject("Hinge").transform;
            rig.Hinge.SetParent(frame, false);
            rig.Hinge.localPosition = new Vector3(door.DoorLocalX - LivingStreetLayout.DoorWidth * 0.5f, LivingStreetLayout.DoorSill,
                door.WallLocalZ - LaneHouseArt.HingeInset);
            var leafArt = art != null ? art.Spawn(ArtNames.DoorLeaf(door), rig.Hinge, Vector3.zero, correction) : null;
            if (leafArt != null)
            {
                Spawned++;
                rig.Leaf = leafArt.transform;
            }
            else
            {
                Color paint = door.Id == LivingStreetLayout.Abbotts ? Palette.Hex(0xB5602A)
                    : door.Id == LivingStreetLayout.Pearl ? Palette.HauntedPurple : Palette.Plum;
                rig.Leaf = Primitive(PrimitiveType.Cube, rig.Hinge, "Leaf",
                    new Vector3(LivingStreetLayout.DoorWidth * 0.5f, LivingStreetLayout.DoorHeight * 0.5f, 0f),
                    new Vector3(LivingStreetLayout.DoorWidth, LivingStreetLayout.DoorHeight, 0.05f), paint, null).transform;
                Primitive(PrimitiveType.Sphere, rig.Hinge, "Knob", new Vector3(LivingStreetLayout.DoorWidth - 0.14f, 1.0f, -0.05f),
                    Vector3.one * 0.07f, Palette.CandyCornYellow, null);
            }

            // The porch lamp by the door, its glass driven by the rig (Nana Pearl's warms up while she answers).
            var lampAt = new Vector3(door.DoorLocalX + LaneHouseArt.LampAcross, LaneHouseArt.LampHeight, door.WallLocalZ - LaneHouseArt.LampOut);
            float glow = LivingStreetLayout.PorchLampGlow;
            var lampArt = art != null ? art.Spawn(ArtNames.PorchLamp(door), frame, lampAt, correction) : null;
            var glass = lampArt != null ? ModelKit.FindDeep(lampArt.transform, LaneHouseArt.LampGlow) : null;
            if (glass != null && glass.GetComponent<Renderer>() != null)
            {
                Spawned++;
                rig.Lamp = glass.GetComponent<Renderer>();
                rig.LampRest = art.GlowMaterialFor(ArtNames.PorchLamp(door), glow);
                rig.LampBright = art.GlowMaterialFor(ArtNames.PorchLamp(door), glow + door.Gag.LampBoost);
                rig.Lamp.sharedMaterial = rig.LampRest;
            }
            else
            {
                if (lampArt != null)
                {
                    Destroy(lampArt);
                }

                rig.LampRest = _materials != null ? _materials.Glow(Palette.PumpkinAmber, glow) : null;
                rig.LampBright = _materials != null ? _materials.Glow(Palette.PumpkinAmber, glow + door.Gag.LampBoost) : null;
                rig.Lamp = Primitive(PrimitiveType.Sphere, frame, "PorchLamp", lampAt, Vector3.one * 0.2f, Palette.PumpkinAmber, rig.LampRest);
            }

            if (art == null)
            {
                // Placeholders the house art carries itself: the lit doorway behind the leaf and the bell's push button.
                var doorway = Primitive(PrimitiveType.Cube, frame, "Doorway",
                    new Vector3(door.DoorLocalX, LivingStreetLayout.DoorSill + LivingStreetLayout.DoorHeight * 0.5f, door.WallLocalZ - 0.045f),
                    new Vector3(LivingStreetLayout.DoorWidth - 0.06f, LivingStreetLayout.DoorHeight - 0.04f, 0.01f), Palette.PumpkinAmber,
                    _materials != null ? _materials.Glow(Palette.PumpkinAmber, 0.85f) : null);
                doorway.shadowCastingMode = ShadowCastingMode.Off;
                Primitive(PrimitiveType.Cylinder, frame, "Doorbell",
                    new Vector3(door.DoorLocalX + LaneHouseArt.BellAcross, LaneHouseArt.BellHeight, door.WallLocalZ - 0.03f),
                    new Vector3(0.07f, 0.01f, 0.07f), Palette.CandyCornYellow, null).transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            }

            foreach (var r in frame.GetComponentsInChildren<Renderer>(true))
            {
                r.shadowCastingMode = ShadowCastingMode.Off;
            }

            // A small warm light in front of the fronts that face away from the moon (no shadows; under the owl house's).
            if (LivingStreetLayout.HasPorchLight(door))
            {
                LivingStreetLayout.PorchLightLocal(door, out float lx, out float ly, out float lz);
                var light = new GameObject("PorchLight").AddComponent<Light>();
                light.transform.SetParent(frame, false);
                light.transform.localPosition = new Vector3(lx, ly, lz);
                light.type = LightType.Point;
                light.color = Palette.PumpkinAmber;
                light.intensity = LivingStreetLayout.PorchLight(door, false);
                light.range = LivingStreetLayout.PorchLightRange;
                light.shadows = LightShadows.None;
                light.renderMode = LightRenderMode.Auto;
                rig.PorchLight = light;
            }

            BuildHallSigns(rig, frame);

            // The neighbour(s) in the doorway, facing the street: the crowd kit's neighbours in house clothes
            // (CrowdKit.Neighbours), the placeholder if the kit is missing.
            var ids = LivingStreetLayout.NeighboursAt(door);
            for (int i = 0; i < ids.Count; i++)
            {
                var look = new WalkerLook(ids[i]);
                var body = _kitBodies.Create(look, frame, _materials) ?? _placeholders.Create(look, frame, _materials);
                float across = ids.Count == 1 ? 0.18f : (i == 0 ? -0.22f : 0.38f);
                var rest = new Vector3(door.DoorLocalX + across, LivingStreetLayout.DoorSill, door.WallLocalZ - LivingStreetLayout.NeighbourOut(door));
                body.Root.localPosition = rest;
                // Bodies face +Z in their own frame; the house front faces -Z, so the neighbour turns to the street.
                body.Root.localRotation = Quaternion.Euler(0f, 180f, 0f);
                body.SetDetail(WalkerDetail.Hidden);
                rig.Figures.Add(body);
                rig.FigureRest.Add(rest);
            }

            return rig;
        }

        /// <summary>
        /// The signs between rings (owner, 2026-11-28: a six-year-old must know to ring again): a warm hall light in front of
        /// the door that comes on at the first ring, the lit window beside the door dark until then (the light switching
        /// on in there), and the neighbour's shadow crossing that window after the second ring. The shadow is a few flat
        /// bands clipped to the window (<see cref="DoorStoops.SilhouetteBand"/>), rebuilt only while it crosses.
        /// </summary>
        private void BuildHallSigns(DoorRig rig, Transform frame)
        {
            var door = rig.Door;
            DoorStoops.HallWindow(door, out float centre, out float bottom, out float top, out float width, out float wall, out _);
            var cover = Primitive(PrimitiveType.Quad, frame, "HallWindowDark", new Vector3(centre, (bottom + top) * 0.5f, wall + WindowCoverDepth),
                new Vector3(width, top - bottom, 1f), Palette.Hex(0x1E1A30), null);
            rig.WindowCover = cover;

            var go = new GameObject("HallShadow");
            go.transform.SetParent(frame, false);
            var mesh = new Mesh { name = "HallShadow_" + door.Id };
            mesh.MarkDynamic();
            int bands = DoorStoops.SilhouetteBands;
            rig.SilhouetteVerts = new Vector3[bands * 4];
            var tris = new int[bands * 6];
            for (int b = 0; b < bands; b++)
            {
                int v = b * 4;
                // Clockwise seen from the street (-Z): bottom-left, top-left, top-right; bottom-left, top-right, bottom-right.
                tris[b * 6] = v;
                tris[b * 6 + 1] = v + 3;
                tris[b * 6 + 2] = v + 2;
                tris[b * 6 + 3] = v;
                tris[b * 6 + 4] = v + 2;
                tris[b * 6 + 5] = v + 1;
            }

            // Normals toward the street and plain UVs: the felt shader lights and samples every vertex.
            var normals = new Vector3[bands * 4];
            var uvs = new Vector2[bands * 4];
            for (int i = 0; i < normals.Length; i++)
            {
                normals[i] = Vector3.back;
                uvs[i] = new Vector2(0.5f, 0.5f);
            }

            mesh.vertices = rig.SilhouetteVerts;
            mesh.normals = normals;
            mesh.uv = uvs;
            mesh.triangles = tris;
            mesh.bounds = new Bounds(new Vector3(centre, (bottom + top) * 0.5f, wall), new Vector3(width + 0.2f, top - bottom + 0.2f, 0.2f));
            go.AddComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.AddComponent<MeshRenderer>();
            renderer.sharedMaterial = _materials != null ? _materials.Glow(Palette.Hex(0x5A3C52), 0.55f) : null;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.enabled = false;
            rig.Silhouette = renderer;
            rig.SilhouetteMesh = mesh;

            var light = new GameObject("HallLight").AddComponent<Light>();
            light.transform.SetParent(frame, false);
            light.transform.localPosition = new Vector3((door.DoorLocalX + centre) * 0.5f, DoorStoops.Sill + 1.9f, door.WallLocalZ - 0.7f);
            light.type = LightType.Point;
            light.color = Palette.CandyCornYellow;
            light.intensity = 0f;
            light.range = HallLightRange;
            light.shadows = LightShadows.None;
            light.enabled = false;
            rig.HallLight = light;
        }

        /// <summary>
        /// Shows the doors as the Core routine says: the leaf on its hinge curve, the hall light, the dark window lighting
        /// up and the shadow crossing it, the neighbours on their poses with their clips (OpenDoorPeek at the door, the walk
        /// out and down the step, GiveTreat), the lamp; and the porch button.
        /// </summary>
        public void ApplyDoors(NeighbourDoors doors, float dt)
        {
            if (_bellButton != null)
            {
                _bellPress = Mathf.MoveTowards(_bellPress, 0f, dt * 3f);
                _bellButton.position = _bellRest + _bellDirection * (0.008f * _bellPress);
            }

            foreach (var rig in _doors)
            {
                string id = rig.Door.Id;
                rig.Hinge.localRotation = Quaternion.Euler(0f, doors.SwingDegrees(id), 0f);
                bool shown = doors.NeighbourShown(id);
                rig.Clock += dt;
                for (int i = 0; i < rig.Figures.Count; i++)
                {
                    var body = rig.Figures[i];
                    bool visible = doors.Figure(id, i, out float x, out float z, out float y, out float yaw, out float speed,
                        out var clip, out float clipTime, out bool turning);
                    body.SetDetail(visible ? WalkerDetail.Full : WalkerDetail.Hidden);
                    if (!visible)
                    {
                        continue;
                    }

                    // The second Abbott hops with glee in the doorway while the other hands out the treats (their gag).
                    float hop = rig.Door.Gag.Hop && i > 0 && doors.Phase(id) == DoorPhase.Giving
                        ? Mathf.Abs(Mathf.Sin(rig.Clock * 5.5f)) * 0.09f
                        : 0f;
                    body.Root.SetPositionAndRotation(new Vector3(x, y + hop, z), Quaternion.Euler(0f, yaw, 0f));
                    var pose = new WalkerPose
                    {
                        X = x, Z = z, Y = y, Yaw = yaw, Speed = speed, Clip = clip, ClipTime = clipTime, Turning = turning,
                    };
                    // A neighbour waiting at the treat spot chats (Talk) instead of standing stiff.
                    if (clip == WalkerClip.Locomotion && speed < 0.05f && !turning && doors.Phase(id) == DoorPhase.Giving)
                    {
                        pose.Clip = i > 0 ? WalkerClip.Talk : WalkerClip.Locomotion;
                        pose.ClipTime = rig.Clock;
                    }

                    body.Animate(rig.Clock, pose, dt);
                }

                if (rig.Lamp != null && rig.LampBright != null)
                {
                    rig.Lamp.sharedMaterial = shown && rig.Door.Gag.LampBoost > 0f ? rig.LampBright : rig.LampRest;
                }

                if (rig.PorchLight != null)
                {
                    rig.PorchLight.intensity = LivingStreetLayout.PorchLight(rig.Door, shown);
                }

                float hall = doors.HallLight(id);
                if (rig.HallLight != null)
                {
                    rig.HallLight.enabled = hall > 0.001f;
                    rig.HallLight.intensity = HallLightIntensity * hall;
                }

                if (rig.WindowCover != null)
                {
                    rig.WindowCover.enabled = hall < 0.02f;
                }

                ApplySilhouette(rig, doors.Silhouette(id));
            }
        }

        private static void ApplySilhouette(DoorRig rig, float progress)
        {
            if (rig.Silhouette == null)
            {
                return;
            }

            bool any = false;
            DoorStoops.HallWindow(rig.Door, out _, out _, out _, out _, out float wall, out _);
            float z = wall + SilhouetteDepth;
            var v = rig.SilhouetteVerts;
            for (int b = 0; b < DoorStoops.SilhouetteBands; b++)
            {
                int k = b * 4;
                if (progress >= 0f && DoorStoops.SilhouetteBand(rig.Door, progress, b, out float l, out float r, out float lo, out float hi))
                {
                    v[k] = new Vector3(l, lo, z);
                    v[k + 1] = new Vector3(r, lo, z);
                    v[k + 2] = new Vector3(r, hi, z);
                    v[k + 3] = new Vector3(l, hi, z);
                    any = true;
                }
                else
                {
                    v[k] = v[k + 1] = v[k + 2] = v[k + 3] = new Vector3(0f, 0f, z);
                }
            }

            if (any || rig.Silhouette.enabled)
            {
                rig.SilhouetteMesh.vertices = v;
            }

            rig.Silhouette.enabled = any;
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
