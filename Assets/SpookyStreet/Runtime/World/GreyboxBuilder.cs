using SpookyStreet.Player;
using UnityEngine;

namespace SpookyStreet.World
{
    /// <summary>
    /// S000.1 greybox of Lantern Lane's arrival view, built from primitives so it stays cheap to replace.
    /// Units are meters, north is +Z, origin at the plaza (spec §4). The composition serves the opening image
    /// (spec §1): a child-height view past a candy bowl, down a path, to the dark blue house whose round attic
    /// window glows above the hedge. The full block arrives in S001.
    /// </summary>
    public sealed class GreyboxBuilder
    {
        public static readonly Vector3 ArrivalSpawn = new Vector3(0f, 0.62f, -13.4f);

        /// <summary>Centre of the round attic window on the owl house's gable (the opening's focal point).</summary>
        public static readonly Vector3 AtticWindow = new Vector3(0f, 7.55f, 10.9f);

        private const float HouseFront = 11f;

        private readonly MaterialLibrary _materials;
        private readonly int _safeLayer;
        private Transform _root;

        public GreyboxBuilder(MaterialLibrary materials)
        {
            _materials = materials;
            int layer = LayerMask.NameToLayer(SafeGroundTracker.SafeGroundLayerName);
            _safeLayer = layer >= 0 ? layer : 0;
        }

        public Transform Build()
        {
            _root = new GameObject("Greybox_LanternLane").transform;

            BuildGround();
            BuildArrivalPorch();
            BuildOwlHouse();
            BuildPlaza();
            BuildBoundary();
            BuildSkyline();
            BuildSkyCard();

            // Greybox never moves: batch it at runtime (editor static flags do not exist for runtime-built objects).
            StaticBatchingUtility.Combine(_root.gameObject);
            return _root;
        }

        private void BuildGround()
        {
            // Playable block: 44 × 36 m, top surface at y = 0. The outer ground fills the view beyond the hedges.
            Box("Ground", new Vector3(0f, -0.25f, 0f), new Vector3(44f, 0.5f, 36f), Palette.Ground, safe: true);
            Decor(Box("OuterGround", new Vector3(0f, -0.45f, 0f), new Vector3(160f, 0.5f, 160f),
                Color.Lerp(Palette.Ground, Palette.Ink, 0.35f), safe: false));

            // Leading line from the arrival steps to the owl house door.
            Box("Path", new Vector3(0f, 0.01f, -1.4f), new Vector3(1.6f, 0.02f, 21.6f), Palette.Path, safe: true);
        }

        private void BuildArrivalPorch()
        {
            // Arrival porch (0, −13; 0.6 m) with two 0.2 m steps down toward the plaza.
            Box("ArrivalPorch", new Vector3(0f, 0.3f, -14.2f), new Vector3(4f, 0.6f, 2.4f), Palette.Timber, safe: true);
            for (int i = 0; i < 2; i++)
            {
                float height = 0.4f - i * 0.2f;
                Box("PorchStep" + i, new Vector3(0f, height * 0.5f, -12.8f + i * 0.4f), new Vector3(2.4f, height, 0.4f),
                    Palette.Timber, safe: true);
            }

            // Candy bowl in the foreground of the opening shot (spec §1).
            Decor(Primitive(PrimitiveType.Cylinder, "CandyBowl", new Vector3(1.3f, 0.69f, -13.05f),
                new Vector3(0.55f, 0.09f, 0.55f), Palette.PumpkinAmber, safe: false));
            Color[] candy = { Palette.WarmPaper, Palette.PipCape, Palette.Moonlight };
            for (int i = 0; i < candy.Length; i++)
            {
                float angle = i * 2.1f;
                Decor(Primitive(PrimitiveType.Sphere, "Candy" + i,
                    new Vector3(1.3f + Mathf.Cos(angle) * 0.12f, 0.8f, -13.05f + Mathf.Sin(angle) * 0.12f),
                    Vector3.one * 0.11f, candy[i], safe: false));
            }

            // Porch lamp: post plus a warm lamp box.
            Primitive(PrimitiveType.Cylinder, "PorchLampPost", new Vector3(1.75f, 1.7f, -14.95f),
                new Vector3(0.08f, 1.1f, 0.08f), Palette.Timber, safe: false);
            var lamp = Box("PorchLampBox", new Vector3(1.75f, 2.95f, -14.95f), new Vector3(0.24f, 0.3f, 0.24f),
                Palette.PumpkinAmber, safe: false);
            lamp.GetComponent<Renderer>().sharedMaterial = _materials.Glow(Palette.PumpkinAmber, 2.5f);
            Decor(lamp);
        }

        private void BuildOwlHouse()
        {
            // Body: dark blue house, front face at z = 11.
            Box("OwlHouse", new Vector3(0f, 3.2f, HouseFront + 3f), new Vector3(9f, 6.4f, 6f), Palette.HouseIndigo,
                safe: false);

            // Gable wall (indigo) under a darker two-plank roof with eaves.
            var gable = new GameObject("OwlHouse_Gable", typeof(MeshFilter), typeof(MeshRenderer));
            gable.transform.SetParent(_root, false);
            gable.transform.localPosition = new Vector3(0f, 6.4f, HouseFront + 3f);
            gable.GetComponent<MeshFilter>().sharedMesh = MeshFactory.Prism(9f, 2.8f, 6f);
            _materials.Apply(gable.GetComponent<MeshRenderer>(), Palette.HouseIndigo);

            float slope = Mathf.Atan2(2.8f, 4.5f) * Mathf.Rad2Deg;
            for (int side = -1; side <= 1; side += 2)
            {
                var plank = Box(side < 0 ? "Roof_West" : "Roof_East",
                    new Vector3(side * 2.3f, 6.4f + 1.5f, HouseFront + 3f), new Vector3(5.9f, 0.24f, 6.6f),
                    Palette.RoofShadow, safe: false);
                plank.transform.localRotation = Quaternion.Euler(0f, 0f, -side * slope);
            }

            // Round attic window in the gable: ink frame, glowing pane, ink mullions (reads as a window, not a moon).
            Decor(Disc("AtticWindow_Frame", AtticWindow + new Vector3(0f, 0f, 0.05f), 1.6f, _materials.Get(Palette.Ink)));
            Decor(Disc("AtticWindow_Pane", AtticWindow + new Vector3(0f, 0f, 0.02f), 1.25f,
                _materials.Glow(Palette.PumpkinAmber, 4f)));
            Decor(Box("AtticWindow_MullionH", AtticWindow, new Vector3(1.25f, 0.08f, 0.04f), Palette.Ink, safe: false));
            Decor(Box("AtticWindow_MullionV", AtticWindow, new Vector3(0.08f, 1.25f, 0.04f), Palette.Ink, safe: false));

            // Closed front door, two shuttered ground-floor windows, and the house porch.
            Box("OwlHouse_Door", new Vector3(0f, 1.1f, HouseFront - 0.02f), new Vector3(1.1f, 1.9f, 0.08f), Palette.Ink,
                safe: false);
            for (int side = -1; side <= 1; side += 2)
            {
                Decor(Box("OwlHouse_Shutter" + side, new Vector3(side * 2.6f, 2.5f, HouseFront - 0.02f),
                    new Vector3(1.1f, 1.3f, 0.06f), Palette.RoofShadow, safe: false));
            }

            Box("OwlHouse_Porch", new Vector3(0f, 0.15f, HouseFront - 0.8f), new Vector3(5f, 0.3f, 1.6f), Palette.Timber,
                safe: true);
        }

        private void BuildPlaza()
        {
            // Plaza maple breaks the space asymmetrically (spec §4).
            Primitive(PrimitiveType.Cylinder, "Maple_Trunk", new Vector3(-4.5f, 1.5f, 2f), new Vector3(0.5f, 1.5f, 0.5f),
                Palette.Timber, safe: false);
            Decor(Primitive(PrimitiveType.Sphere, "Maple_Canopy", new Vector3(-4.5f, 4.1f, 2f),
                new Vector3(3.6f, 3f, 3.6f), Palette.Hedge, safe: false));
        }

        private void BuildBoundary()
        {
            Box("Hedge_North", new Vector3(0f, 0.7f, 17.5f), new Vector3(44f, 1.4f, 1f), Palette.Hedge, safe: false);
            Box("Hedge_South", new Vector3(0f, 0.7f, -17.5f), new Vector3(44f, 1.4f, 1f), Palette.Hedge, safe: false);
            Box("Hedge_West", new Vector3(-21.5f, 0.7f, 0f), new Vector3(1f, 1.4f, 36f), Palette.Hedge, safe: false);
            Box("Hedge_East", new Vector3(21.5f, 0.7f, 0f), new Vector3(1f, 1.4f, 36f), Palette.Hedge, safe: false);
            Box("Hedge_SideYard", new Vector3(-6f, 0.8f, 7f), new Vector3(6f, 1.6f, 1f), Palette.Hedge, safe: false);
        }

        private void BuildSkyline()
        {
            // Decorative roofs beyond the hedges supply skyline only (spec §4). Never walkable.
            var houses = new[]
            {
                new Vector4(-16f, 22f, 7f, 5.5f), new Vector4(-8.5f, 24f, 6f, 7f), new Vector4(9f, 23f, 7.5f, 6f),
                new Vector4(17f, 21.5f, 6f, 5f), new Vector4(-27f, 6f, 6f, 6.5f), new Vector4(27f, 3f, 6.5f, 5.5f),
            };
            foreach (var h in houses)
            {
                var position = new Vector3(h.x, 0f, h.y);
                float width = h.z;
                float wallHeight = h.w;
                Decor(Box("Skyline_House", position + new Vector3(0f, wallHeight * 0.5f, 0f),
                    new Vector3(width, wallHeight, 5f), Palette.RoofShadow, safe: false));
                var roof = new GameObject("Skyline_Roof", typeof(MeshFilter), typeof(MeshRenderer));
                roof.transform.SetParent(_root, false);
                roof.transform.localPosition = position + new Vector3(0f, wallHeight, 0f);
                roof.transform.localRotation = Quaternion.Euler(0f, Mathf.Abs(h.x) > 20f ? 90f : 0f, 0f);
                roof.GetComponent<MeshFilter>().sharedMesh = MeshFactory.Prism(width + 0.6f, width * 0.35f, 5.4f);
                _materials.Apply(roof.GetComponent<MeshRenderer>(), Palette.RoofShadow);
            }
        }

        private void BuildSkyCard()
        {
            // Painted twilight gradient behind the skyline: horizon glow fading up into the spec's sky blue.
            const int height = 64;
            var gradient = new Texture2D(2, height, TextureFormat.RGB24, false)
            {
                name = "SkyGradient",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            for (int y = 0; y < height; y++)
            {
                float t = Mathf.SmoothStep(0f, 1f, Mathf.Clamp01(y / (height * 0.45f)));
                Color c = Color.Lerp(Palette.SkyHorizon, Palette.TwilightSky, t);
                gradient.SetPixel(0, y, c);
                gradient.SetPixel(1, y, c);
            }

            gradient.Apply(false, true);
            var card = GameObject.CreatePrimitive(PrimitiveType.Quad);
            card.name = "SkyCard";
            Object.Destroy(card.GetComponent<Collider>());
            card.transform.SetParent(_root, false);
            card.transform.localPosition = new Vector3(0f, 24f, 34f);
            card.transform.localScale = new Vector3(170f, 52f, 1f);
            card.GetComponent<Renderer>().sharedMaterial = _materials.Unlit(gradient);
        }

        private GameObject Disc(string name, Vector3 centre, float diameter, Material material)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cylinder);
            go.name = name;
            go.transform.SetParent(_root, false);
            go.transform.localPosition = centre;
            go.transform.localRotation = Quaternion.Euler(90f, 0f, 0f);
            go.transform.localScale = new Vector3(diameter, 0.02f, diameter);
            go.GetComponent<Renderer>().sharedMaterial = material;
            return go;
        }

        /// <summary>Strips the collider from purely decorative pieces (scaled-primitive colliders lie).</summary>
        private static GameObject Decor(GameObject go)
        {
            var collider = go.GetComponent<Collider>();
            if (collider != null)
            {
                Object.Destroy(collider);
            }

            return go;
        }

        private GameObject Box(string name, Vector3 center, Vector3 size, Color color, bool safe) =>
            Primitive(PrimitiveType.Cube, name, center, size, color, safe);

        private GameObject Primitive(PrimitiveType type, string name, Vector3 center, Vector3 scale, Color color, bool safe)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            go.transform.SetParent(_root, false);
            go.transform.localPosition = center;
            go.transform.localScale = scale;
            go.layer = safe ? _safeLayer : 0;
            _materials.Apply(go.GetComponent<Renderer>(), color);
            return go;
        }
    }
}
