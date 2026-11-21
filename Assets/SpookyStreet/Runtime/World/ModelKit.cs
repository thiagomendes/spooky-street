using UnityEngine;

namespace SpookyStreet.World
{
    /// <summary>
    /// Loads imported models from <c>Resources/Models</c> (docs/asset-provenance.md) and gives them URP materials
    /// built from the kit's colour atlas. Import settings live in <c>Editor/ModelImportRules.cs</c>: models arrive
    /// without materials, so every renderer is assigned here and nothing can draw with the error shader.
    /// </summary>
    public sealed class ModelKit
    {
        private readonly MaterialLibrary _materials;
        private readonly Material _atlas;
        private readonly string _folder;

        public ModelKit(MaterialLibrary materials, string folder, string atlasName)
        {
            _materials = materials;
            _folder = folder.TrimEnd('/') + "/";
            var texture = Resources.Load<Texture2D>(_folder + atlasName);
            if (texture == null)
            {
                Debug.LogWarning($"[ModelKit] Atlas Resources/{_folder}{atlasName} missing; models will be paper-coloured.");
            }

            _atlas = materials.Textured(texture);
        }

        /// <summary>True when the kit's prefab exists (lets callers keep a primitive fallback).</summary>
        public bool Has(string model) => Resources.Load<GameObject>(_folder + model) != null;

        /// <summary>
        /// Instantiates <paramref name="model"/> under <paramref name="parent"/> at a ground position and yaw.
        /// Returns null (with a warning) when the model is missing, so the street still builds.
        /// </summary>
        public GameObject Spawn(string model, Transform parent, Vector3 position, float yaw = 0f, float scale = 1f,
            bool solid = false)
        {
            var prefab = Resources.Load<GameObject>(_folder + model);
            if (prefab == null)
            {
                Debug.LogWarning($"[ModelKit] Model Resources/{_folder}{model} missing.");
                return null;
            }

            var instance = Object.Instantiate(prefab, parent, false);
            instance.name = model;
            instance.transform.localPosition = position;
            instance.transform.localRotation = Quaternion.Euler(0f, yaw, 0f);
            instance.transform.localScale = Vector3.one * scale;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                MaterialLibrary.ApplyAll(renderer, _atlas);
            }

            if (solid)
            {
                AddBoundsCollider(instance);
            }

            return instance;
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
