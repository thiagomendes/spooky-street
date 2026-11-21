using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpookyStreet.World
{
    /// <summary>
    /// Palette materials derived from committed URP base materials that the scene references (so their shaders
    /// always ship): a matte Lit base for surfaces and an Unlit base for glows and the sky card. Never falls back
    /// to Unity's built-in Default-Material, which URP draws as the magenta error shader on device.
    /// </summary>
    public sealed class MaterialLibrary
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");

        private readonly Dictionary<Color, Material> _lit = new Dictionary<Color, Material>();
        private readonly Dictionary<Color, Material> _glow = new Dictionary<Color, Material>();
        private readonly Material _litBase;
        private readonly Material _unlitBase;

        public MaterialLibrary(Material litBase, Material unlitBase)
        {
            _litBase = litBase;
            if (_litBase == null && GraphicsSettings.currentRenderPipeline != null)
            {
                _litBase = GraphicsSettings.currentRenderPipeline.defaultMaterial;
                Debug.LogWarning("[MaterialLibrary] No Lit base material assigned; using the render pipeline default.");
            }

            _unlitBase = unlitBase != null ? unlitBase : _litBase;
            if (_litBase == null)
            {
                Debug.LogError("[MaterialLibrary] No URP base material available; geometry will not render correctly.");
            }
        }

        /// <summary>Matte lit surface in a palette colour.</summary>
        public Material Get(Color color) => Cached(_lit, _litBase, color, "Palette_");

        /// <summary>Self-lit surface. Intensity above 1 drives HDR bloom on the active point of interest.</summary>
        public Material Glow(Color color, float intensity = 1f)
        {
            Color hdr = new Color(color.r * intensity, color.g * intensity, color.b * intensity, 1f);
            return Cached(_glow, _unlitBase, hdr, "Glow_");
        }

        /// <summary>Unlit textured surface (sky cards and other painted backdrops).</summary>
        public Material Unlit(Texture2D texture)
        {
            var material = new Material(_unlitBase) { name = "Unlit_" + texture.name };
            if (material.HasProperty(BaseMapId))
            {
                material.SetTexture(BaseMapId, texture);
            }

            if (material.HasProperty(BaseColorId))
            {
                material.SetColor(BaseColorId, Color.white);
            }

            return material;
        }

        public void Apply(Renderer renderer, Color color) => renderer.sharedMaterial = Get(color);

        private static Material Cached(Dictionary<Color, Material> cache, Material baseMaterial, Color color, string prefix)
        {
            if (cache.TryGetValue(color, out var material))
            {
                return material;
            }

            material = new Material(baseMaterial) { name = prefix + ColorUtility.ToHtmlStringRGB(color) };
            if (material.HasProperty(BaseColorId))
            {
                material.SetColor(BaseColorId, color);
            }
            else
            {
                material.color = color;
            }

            cache.Add(color, material);
            return material;
        }
    }
}
