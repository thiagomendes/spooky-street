using SpookyStreet.Core.World;
using UnityEngine;

namespace SpookyStreet.World
{
    /// <summary>
    /// Loads original models (built by tools/blender) from <c>Resources/Models</c> and gives every renderer a URP
    /// Lit material built from the model's baked textures. Import settings live in
    /// <c>Editor/ModelImportRules.cs</c>: models arrive without materials, so every renderer is assigned here and
    /// nothing can draw with the error shader. Texture convention, next to the FBX: <c>&lt;Model&gt;_BaseColor</c>
    /// and optionally <c>&lt;Model&gt;_Normal</c> and <c>&lt;Model&gt;_Emission</c>. Models baked into a shared
    /// atlas (no <c>&lt;Model&gt;_BaseColor</c>) use the folder atlas <see cref="FallbackAtlas"/> instead
    /// (<c>Env_BaseColor</c>, <c>Env_Normal</c>, <c>Env_Emission</c>). Renderers whose GameObject name ends in
    /// <c>_Glow</c> get an emissive variant (intensity from <see cref="GlowLevels"/>) so bloom picks them up.
    /// Every lookup tolerates missing files so callers can keep a primitive fallback.
    /// </summary>
    public sealed class ModelKit
    {
        public const string ModelsRoot = "Models/";
        public const string BaseColorSuffix = "_BaseColor";
        public const string NormalSuffix = "_Normal";
        public const string EmissionSuffix = "_Emission";
        public const string DefaultFallbackAtlas = "Env";

        private readonly MaterialLibrary _materials;
        private readonly string _folder;

        /// <param name="folder">Resources-relative folder, e.g. "Models/Props/Porch".</param>
        public ModelKit(MaterialLibrary materials, string folder)
        {
            _materials = materials;
            _folder = string.IsNullOrEmpty(folder) ? string.Empty : folder.TrimEnd('/') + "/";
        }

        public string Folder => _folder;

        /// <summary>Shared atlas name used when a model has no textures of its own (null or empty: none).</summary>
        public string FallbackAtlas { get; set; } = DefaultFallbackAtlas;

        public GameObject Load(string model) => Resources.Load<GameObject>(_folder + model);

        /// <summary>True when the model exists (lets callers keep a primitive fallback).</summary>
        public bool Has(string model) => Load(model) != null;

        public Texture2D Texture(string name) => Resources.Load<Texture2D>(_folder + name);

        /// <summary>
        /// Texture set name for <paramref name="model"/>: the model itself when <c>&lt;Model&gt;_BaseColor</c> exists,
        /// else <see cref="FallbackAtlas"/> when that atlas exists, else the model (whose lookups then miss).
        /// </summary>
        public string AtlasFor(string model)
        {
            if (Texture(model + BaseColorSuffix) != null || string.IsNullOrEmpty(FallbackAtlas))
            {
                return model;
            }

            return Texture(FallbackAtlas + BaseColorSuffix) != null ? FallbackAtlas : model;
        }

        /// <summary>The model's baked Lit material, or the paper palette material if it has no BaseColor yet.</summary>
        public Material MaterialFor(string model)
        {
            string atlas = AtlasFor(model);
            return _materials.Textured(Texture(atlas + BaseColorSuffix), Texture(atlas + NormalSuffix));
        }

        /// <summary>
        /// The model's emissive material at HDR <paramref name="intensity"/>: same base and normal maps plus the
        /// atlas emission mask. Glow parts' base colour texels already hold the light colour.
        /// </summary>
        public Material GlowMaterialFor(string model, float intensity)
        {
            string atlas = AtlasFor(model);
            return _materials.Textured(Texture(atlas + BaseColorSuffix), Texture(atlas + NormalSuffix),
                MaterialLibrary.FeltSmoothness, Texture(atlas + EmissionSuffix), Mathf.Max(0.01f, intensity));
        }

        /// <summary>
        /// Gives every renderer under <paramref name="root"/> the model's material, and every <c>*_Glow</c> renderer
        /// its emissive variant at the <see cref="GlowLevels"/> intensity for its name.
        /// </summary>
        public void ApplyMaterials(GameObject root, string model)
        {
            Material surface = MaterialFor(model);
            foreach (var renderer in root.GetComponentsInChildren<Renderer>(true))
            {
                string part = renderer.gameObject.name;
                Material material = GlowLevels.IsGlowPart(part)
                    ? GlowMaterialFor(model, GlowLevels.IntensityFor(part))
                    : surface;
                MaterialLibrary.ApplyAll(renderer, material);
            }
        }

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

            ApplyMaterials(instance, model);
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

        /// <summary>Depth-first search for a descendant (or <paramref name="root"/> itself) by exact name.</summary>
        public static Transform FindDeep(Transform root, string name)
        {
            if (root == null)
            {
                return null;
            }

            if (root.name == name)
            {
                return root;
            }

            for (int i = 0; i < root.childCount; i++)
            {
                var found = FindDeep(root.GetChild(i), name);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        /// <summary>
        /// Bounds of every mesh under <paramref name="root"/> in <paramref name="root"/>'s own frame (unaffected by
        /// its rotation, unlike world-space renderer bounds), from the mesh bounds so meshes need not be readable.
        /// </summary>
        public static Bounds LocalMeshBounds(GameObject root)
        {
            var filters = root.GetComponentsInChildren<MeshFilter>(true);
            Matrix4x4 toRoot = root.transform.worldToLocalMatrix;
            bool any = false;
            var bounds = new Bounds(Vector3.zero, Vector3.zero);
            foreach (var filter in filters)
            {
                var mesh = filter.sharedMesh;
                if (mesh == null)
                {
                    continue;
                }

                Matrix4x4 m = toRoot * filter.transform.localToWorldMatrix;
                Bounds b = mesh.bounds;
                for (int corner = 0; corner < 8; corner++)
                {
                    var p = new Vector3(
                        (corner & 1) == 0 ? b.min.x : b.max.x,
                        (corner & 2) == 0 ? b.min.y : b.max.y,
                        (corner & 4) == 0 ? b.min.z : b.max.z);
                    p = m.MultiplyPoint3x4(p);
                    if (!any)
                    {
                        bounds = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        bounds.Encapsulate(p);
                    }
                }
            }

            return bounds;
        }

        /// <summary>Adds a box collider fitted to the model's meshes in its own frame (yaw-independent).</summary>
        public static BoxCollider AddBoundsCollider(GameObject instance)
        {
            Bounds local = LocalMeshBounds(instance);
            var box = instance.AddComponent<BoxCollider>();
            box.center = local.center;
            box.size = local.size;
            return box;
        }
    }
}
