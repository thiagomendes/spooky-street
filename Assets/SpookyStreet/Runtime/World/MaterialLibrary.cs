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
        private static readonly int EmissionMapId = Shader.PropertyToID("_EmissionMap");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");

        /// <summary>
        /// Committed Lit material with the <c>_NORMALMAP</c> keyword on, so the player build keeps that shader
        /// variant for runtime-built textured materials (an unreferenced variant would be stripped).
        /// </summary>
        public const string TexturedBasePath = "Materials/Felt_Lit";

        /// <summary>
        /// Committed Lit material with <c>_NORMALMAP</c> and <c>_EMISSION</c> on: the template for emissive textured
        /// materials (glowing windows, lamps, jack-o'-lantern faces), so the player build keeps that variant too.
        /// </summary>
        public const string EmissiveBasePath = "Materials/Felt_Lit_Glow";

        /// <summary>Matte felt and fabric: low, soft highlights.</summary>
        public const float FeltSmoothness = 0.12f;

        private readonly Dictionary<Color, Material> _lit = new Dictionary<Color, Material>();
        private readonly Dictionary<Color, Material> _glow = new Dictionary<Color, Material>();
        private readonly Dictionary<(Texture2D, Texture2D, Texture2D, float), Material> _textured =
            new Dictionary<(Texture2D, Texture2D, Texture2D, float), Material>();
        private readonly Material _litBase;
        private Material _texturedBase;
        private Material _emissiveBase;
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
        /// base map and an optional tangent-space <paramref name="normal"/> map. With
        /// <paramref name="emissionIntensity"/> above zero the surface also glows: <c>_EMISSION</c> on,
        /// <paramref name="emission"/> (an sRGB emission colour mask, values up to 1) as the emission map, scaled by
        /// the HDR intensity so bloom (threshold 1) picks it up. Without an emission map the whole surface emits
        /// pumpkin amber at that intensity. Cached per texture set and intensity. Without a base colour texture it
        /// returns the plain paper palette material, so a model never draws untextured magenta.
        /// </summary>
        public Material Textured(Texture2D baseColor, Texture2D normal = null, float smoothness = FeltSmoothness,
            Texture2D emission = null, float emissionIntensity = 0f)
        {
            if (baseColor == null)
            {
                return emissionIntensity > 0f ? Glow(Palette.PumpkinAmber, emissionIntensity) : Get(Palette.WarmPaper);
            }

            bool emissive = emissionIntensity > 0f;
            var key = (baseColor, normal, emissive ? emission : null, emissive ? emissionIntensity : 0f);
            if (_textured.TryGetValue(key, out var material))
            {
                return material;
            }

            string name = (emissive ? "Emissive_" : "Textured_") + baseColor.name;
            material = new Material(emissive ? EmissiveBase() : TexturedBase()) { name = name };
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

            if (emissive && material.HasProperty(EmissionColorId))
            {
                // Linear HDR value set as a vector: SetColor would treat it as sRGB and inflate values above 1.
                Color tint = emission != null ? Color.white : Palette.PumpkinAmber.linear;
                material.SetVector(EmissionColorId,
                    new Vector4(tint.r * emissionIntensity, tint.g * emissionIntensity, tint.b * emissionIntensity, 1f));
                if (material.HasProperty(EmissionMapId))
                {
                    material.SetTexture(EmissionMapId, emission);
                }

                material.EnableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.RealtimeEmissive;
            }
            else
            {
                material.DisableKeyword("_EMISSION");
                material.globalIlluminationFlags = MaterialGlobalIlluminationFlags.EmissiveIsBlack;
            }

            _textured.Add(key, material);
            return material;
        }

        /// <summary>HDR emission of a material built by <see cref="Textured"/> (0 when it does not glow).</summary>
        public static float EmissionIntensity(Material material)
        {
            if (material == null || !material.IsKeywordEnabled("_EMISSION") || !material.HasProperty(EmissionColorId))
            {
                return 0f;
            }

            Vector4 c = material.GetVector(EmissionColorId);
            return Mathf.Max(c.x, Mathf.Max(c.y, c.z));
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

        private Material EmissiveBase()
        {
            if (_emissiveBase == null)
            {
                _emissiveBase = Resources.Load<Material>(EmissiveBasePath);
                if (_emissiveBase == null)
                {
                    _emissiveBase = TexturedBase();
                }
            }

            return _emissiveBase;
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
