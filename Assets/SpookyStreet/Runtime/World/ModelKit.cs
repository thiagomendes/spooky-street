using UnityEngine;

namespace SpookyStreet.World
{
    /// <summary>
    /// Loads original models (built by tools/blender) from <c>Resources/Models</c> and gives every renderer a URP
    /// Lit material built from the model's baked textures. Import settings live in
    /// <c>Editor/ModelImportRules.cs</c>: models arrive without materials, so every renderer is assigned here and
    /// nothing can draw with the error shader. Texture convention, next to the FBX: <c>&lt;Model&gt;_BaseColor</c>
    /// and optionally <c>&lt;Model&gt;_Normal</c>. Every lookup tolerates missing files so callers can keep a
    /// primitive fallback.
    /// </summary>
    public sealed class ModelKit
    {
        public const string ModelsRoot = "Models/";
        public const string BaseColorSuffix = "_BaseColor";
        public const string NormalSuffix = "_Normal";

        private readonly MaterialLibrary _materials;
        private readonly string _folder;

        /// <param name="folder">Resources-relative folder, e.g. "Models/Props/Porch".</param>
        public ModelKit(MaterialLibrary materials, string folder)
        {
            _materials = materials;
            _folder = string.IsNullOrEmpty(folder) ? string.Empty : folder.TrimEnd('/') + "/";
        }

        public string Folder => _folder;

        public GameObject Load(string model) => Resources.Load<GameObject>(_folder + model);

        /// <summary>True when the model exists (lets callers keep a primitive fallback).</summary>
        public bool Has(string model) => Load(model) != null;

        public Texture2D Texture(string name) => Resources.Load<Texture2D>(_folder + name);

        /// <summary>The model's baked Lit material, or the paper palette material if it has no BaseColor yet.</summary>
        public Material MaterialFor(string model) =>
            _materials.Textured(Texture(model + BaseColorSuffix), Texture(model + NormalSuffix));

        /// <summary>
        /// Instantiates <paramref name="model"/> under <paramref name="parent"/> with its baked material on every
        /// renderer, on <paramref name="parent"/>'s layer. Returns null when the model is missing.
        /// </summary>
        public GameObject Spawn(string model, Transform parent, Vector3 localPosition, float yaw = 0f, float scale = 1f,
            bool solid = false)
        {
            var prefab = Load(model);
            if (prefab == null)
            {
                return null;
            }

            var instance = Object.Instantiate(prefab, parent, false);
            instance.name = model;
            instance.transform.localPosition = localPosition;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            instance.transform.localScale = Vector3.one * scale;
            if (parent != null)
            {
                SetLayer(instance.transform, parent.gameObject.layer);
            }

            ApplyMaterial(instance, MaterialFor(model));
            if (solid)
            {
                AddBoundsCollider(instance);
            }

            return instance;
        }

        public static void ApplyMaterial(GameObject root, Material material)
        {
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                MaterialLibrary.ApplyAll(renderer, material);
            }
        }

        /// <summary>World-space bounds of every renderer under <paramref name="root"/>.</summary>
        public static Bounds RendererBounds(GameObject root)
        {
            var renderers = root.GetComponentsInChildren<Renderer>(true);
            if (renderers.Length == 0)
            {
                return new Bounds(root.transform.position, Vector3.zero);
            }

            var bounds = renderers[0].bounds;
            for (int i = 1; i < renderers.Length; i++)
            {
                bounds.Encapsulate(renderers[i].bounds);
            }

            return bounds;
        }

        public static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
            {
                SetLayer(root.GetChild(i), layer);
            }
        }

        private static void AddBoundsCollider(GameObject instance)
        {
            var world = RendererBounds(instance);
            var box = instance.AddComponent<BoxCollider>();
            box.center = instance.transform.InverseTransformPoint(world.center);
            Vector3 size = instance.transform.InverseTransformVector(world.size);
            box.size = new Vector3(Mathf.Abs(size.x), Mathf.Abs(size.y), Mathf.Abs(size.z));
        }
    }
}
