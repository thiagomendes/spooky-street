using SpookyStreet.Player;
using UnityEngine;

namespace SpookyStreet.World
{
    /// <summary>
    /// S000 safe floor and orientation landmarks, built from primitives so they are cheap to replace.
    /// Units are meters, north is +Z, origin at the plaza (spec §4). Only the arrival porch, plaza floor,
    /// boundary hedges and the owl-house silhouette with its round attic window exist yet; the full
    /// block arrives in S001.
    /// </summary>
    public sealed class GreyboxBuilder
    {
        public static readonly Vector3 ArrivalSpawn = new Vector3(0f, 0.62f, -13.4f);

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

            // Plaza floor: 44 × 36 m block, top surface at y = 0.
            Box("Ground", new Vector3(0f, -0.25f, 0f), new Vector3(44f, 0.5f, 36f), Palette.Ground, safe: true);

            // Arrival porch (0, −13; 0.6 m) with two 0.2 m steps down toward the plaza.
            Box("ArrivalPorch", new Vector3(0f, 0.3f, -14.2f), new Vector3(4f, 0.6f, 2.4f), Palette.Timber, safe: true);
            for (int i = 0; i < 2; i++)
            {
                float height = 0.4f - i * 0.2f;
                Box("PorchStep" + i, new Vector3(0f, height * 0.5f, -12.8f + i * 0.4f), new Vector3(2.4f, height, 0.4f),
                    Palette.Timber, safe: true);
            }

            // Boundary hedges visually close the block; the gaps are filled at S001.
            Box("Hedge_North", new Vector3(0f, 0.7f, 17.5f), new Vector3(44f, 1.4f, 1f), Palette.Hedge, safe: false);
            Box("Hedge_South", new Vector3(0f, 0.7f, -17.5f), new Vector3(44f, 1.4f, 1f), Palette.Hedge, safe: false);
            Box("Hedge_West", new Vector3(-21.5f, 0.7f, 0f), new Vector3(1f, 1.4f, 36f), Palette.Hedge, safe: false);
            Box("Hedge_East", new Vector3(21.5f, 0.7f, 0f), new Vector3(1f, 1.4f, 36f), Palette.Hedge, safe: false);
            Box("Hedge_SideYard", new Vector3(-6f, 0.8f, 7f), new Vector3(6f, 1.6f, 1f), Palette.Hedge, safe: false);

            // Owl house silhouette (blue porch at 0, +10) with the round attic window readable from the porch.
            Box("OwlHouse", new Vector3(0f, 3.2f, 14f), new Vector3(9f, 6.4f, 6f), Palette.HouseIndigo, safe: false);
            Box("OwlHouse_Porch", new Vector3(0f, 0.15f, 10.2f), new Vector3(5f, 0.3f, 1.6f), Palette.Timber, safe: true);
            Box("OwlHouse_Door", new Vector3(0f, 1.1f, 10.98f), new Vector3(1.1f, 1.9f, 0.08f), Palette.Ink, safe: false);
            var window = Primitive(PrimitiveType.Cylinder, "AtticWindow_Round", new Vector3(0f, 5.4f, 10.96f),
                new Vector3(1.3f, 0.04f, 1.3f), Palette.PumpkinAmber, safe: false);
            window.transform.rotation = Quaternion.Euler(90f, 0f, 0f);
            Object.Destroy(window.GetComponent<Collider>());

            // Plaza maple breaks the space asymmetrically.
            Primitive(PrimitiveType.Cylinder, "Maple_Trunk", new Vector3(-3f, 1.5f, 2f), new Vector3(0.5f, 1.5f, 0.5f),
                Palette.Timber, safe: false);
            var canopy = Primitive(PrimitiveType.Sphere, "Maple_Canopy", new Vector3(-3f, 4f, 2f), new Vector3(3.5f, 3f, 3.5f),
                Palette.Hedge, safe: false);
            Object.Destroy(canopy.GetComponent<Collider>());

            BuildLighting();
            // Greybox never moves: batch it at runtime (editor static flags do not exist for runtime-built objects).
            StaticBatchingUtility.Combine(_root.gameObject);
            return _root;
        }

        private void BuildLighting()
        {
            RenderSettings.ambientMode = UnityEngine.Rendering.AmbientMode.Flat;
            RenderSettings.ambientLight = Color.Lerp(Palette.TwilightSky, Color.white, 0.25f);
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Palette.TwilightSky;
            RenderSettings.fogStartDistance = 25f;
            RenderSettings.fogEndDistance = 90f;

            var moon = new GameObject("Moonlight").AddComponent<Light>();
            moon.transform.SetParent(_root, false);
            moon.type = LightType.Directional;
            moon.color = Color.Lerp(Palette.ClothSilver, Palette.TwilightSky, 0.3f);
            moon.intensity = 0.7f;
            moon.shadows = LightShadows.Soft;
            moon.transform.rotation = Quaternion.Euler(40f, -35f, 0f);

            var porchLamp = new GameObject("ArrivalPorchLamp").AddComponent<Light>();
            porchLamp.transform.SetParent(_root, false);
            porchLamp.type = LightType.Point;
            porchLamp.color = Palette.PumpkinAmber;
            porchLamp.range = 6f;
            porchLamp.intensity = 2f;
            porchLamp.transform.position = new Vector3(1.6f, 2.2f, -14f);

            var atticLamp = new GameObject("AtticLamp").AddComponent<Light>();
            atticLamp.transform.SetParent(_root, false);
            atticLamp.type = LightType.Point;
            atticLamp.color = Palette.PumpkinAmber;
            atticLamp.range = 5f;
            atticLamp.intensity = 2.5f;
            atticLamp.transform.position = new Vector3(0f, 5.4f, 10.3f);
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
