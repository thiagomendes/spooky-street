using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpookyStreet.World
{
    /// <summary>
    /// One palette-tinted material per colour, all derived from a URP Lit base material that the scene
    /// references (so the shader is always in the player build). Never falls back to Unity's built-in
    /// Default-Material: under URP that renders as the magenta error shader on device.
    /// </summary>
    public sealed class MaterialLibrary
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");

        private readonly Dictionary<Color, Material> _byColor = new Dictionary<Color, Material>();
        private readonly Material _base;

        public MaterialLibrary(Material baseMaterial)
        {
            _base = baseMaterial;
            if (_base == null && GraphicsSettings.currentRenderPipeline != null)
            {
                _base = GraphicsSettings.currentRenderPipeline.defaultMaterial;
                Debug.LogWarning("[MaterialLibrary] No base material assigned; using the render pipeline default.");
            }

            if (_base == null)
            {
                Debug.LogError("[MaterialLibrary] No URP base material available; geometry will not render correctly.");
            }
        }

        public Material BaseMaterial => _base;

        public Material Get(Color color)
        {
            if (_byColor.TryGetValue(color, out var material))
            {
                return material;
            }

            material = new Material(_base) { name = "Palette_" + ColorUtility.ToHtmlStringRGB(color) };
            if (material.HasProperty(BaseColorId))
            {
                material.SetColor(BaseColorId, color);
            }
            else
            {
                material.color = color;
            }

            _byColor.Add(color, material);
            return material;
        }

        /// <summary>Paints a primitive or other renderer with a palette colour.</summary>
        public void Apply(Renderer renderer, Color color) => renderer.sharedMaterial = Get(color);
    }
}
