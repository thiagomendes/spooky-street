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
        private static readonly int BumpMapId = Shader.PropertyToID("_BumpMap");
        private static readonly int SmoothnessId = Shader.PropertyToID("_Smoothness");

        /// <summary>
        /// Committed Lit material with the <c>_NORMALMAP</c> keyword on, so the player build keeps that shader
        /// variant for runtime-built textured materials (an unreferenced variant would be stripped).
        /// </summary>
        public const string TexturedBasePath = "Materials/Felt_Lit";

        /// <summary>Matte felt and fabric: low, soft highlights.</summary>
        public const float FeltSmoothness = 0.12f;

        private readonly Dictionary<Color, Material> _lit = new Dictionary<Color, Material>();
        private readonly Dictionary<Color, Material> _glow = new Dictionary<Color, Material>();
        private readonly Dictionary<(Texture2D, Texture2D), Material> _textured =
            new Dictionary<(Texture2D, Texture2D), Material>();
        private readonly Material _litBase;
        private Material _texturedBase;
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

        /// <summary>
        /// Matte URP Lit surface sampling baked textures (original models): <paramref name="baseColor"/> as the
        /// base map and an optional tangent-space <paramref name="normal"/> map. Cached per texture pair. Without a
        /// base colour texture it returns the plain paper palette material, so a model never draws untextured magenta.
        /// </summary>
        public Material Textured(Texture2D baseColor, Texture2D normal = null, float smoothness = FeltSmoothness)
        {
            if (baseColor == null)
            {
                return Get(Palette.WarmPaper);
            }

            var key = (baseColor, normal);
            if (_textured.TryGetValue(key, out var material))
            {
                return material;
            }

            material = new Material(TexturedBase()) { name = "Textured_" + baseColor.name };
            if (material.HasProperty(BaseMapId))
            {
                material.SetTexture(BaseMapId, baseColor);
            }

            if (material.HasProperty(BaseColorId))
            {
                material.SetColor(BaseColorId, Color.white);
            }

            if (material.HasProperty(SmoothnessId))
            {
                material.SetFloat(SmoothnessId, smoothness);
            }

            if (normal != null && material.HasProperty(BumpMapId))
            {
                material.SetTexture(BumpMapId, normal);
                material.EnableKeyword("_NORMALMAP");
            }
            else
            {
                material.DisableKeyword("_NORMALMAP");
            }

            _textured.Add(key, material);
            return material;
        }

        public void Apply(Renderer renderer, Color color) => renderer.sharedMaterial = Get(color);

        /// <summary>Gives every sub-mesh of <paramref name="renderer"/> the same material.</summary>
        public static void ApplyAll(Renderer renderer, Material material)
        {
            int count = Mathf.Max(1, renderer.sharedMaterials.Length);
            var materials = new Material[count];
            for (int i = 0; i < count; i++)
            {
                materials[i] = material;
            }

            renderer.sharedMaterials = materials;
        }

        private Material TexturedBase()
        {
            if (_texturedBase == null)
            {
                _texturedBase = Resources.Load<Material>(TexturedBasePath);
                if (_texturedBase == null)
                {
                    _texturedBase = _litBase;
                }
            }

            return _texturedBase;
        }

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
