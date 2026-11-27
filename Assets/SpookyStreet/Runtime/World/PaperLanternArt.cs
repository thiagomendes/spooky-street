using System.Collections.Generic;
using SpookyStreet.Core.World;
using UnityEngine;

namespace SpookyStreet.World
{
    /// <summary>
    /// Spawns the paper lantern art (docs/living-street-lantern-art.md, <see cref="LanternArt"/>) with its baked
    /// materials and tints a lantern's paper to a picker colour. Art plumbing only: when to fold, hang or ring is the
    /// living street's code. The lantern's <c>PaperLantern_Glow</c> renderer gets a copy of the atlas glow material with
    /// <c>_BaseColor</c> = the paper colour and <c>_EmissionColor</c> = the light colour times
    /// <see cref="GlowLevels.PaperLantern"/>; everything else on the lantern (ink, wire, tassel) keeps the atlas material.
    /// Tinted materials are cached per source material and colour, so sixteen lanterns cost at most sixteen materials.
    /// </summary>
    public static class PaperLanternArt
    {
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int EmissionColorId = Shader.PropertyToID("_EmissionColor");
        private static readonly Dictionary<(Material, string), Material> Tinted = new Dictionary<(Material, string), Material>();

        /// <summary>The kit that loads the lantern models with the shared <c>Lanterns</c> atlas.</summary>
        public static ModelKit Kit(MaterialLibrary materials) =>
            new ModelKit(materials, LanternArt.Folder) { FallbackAtlas = LanternArt.Atlas };

        /// <summary>
        /// A lantern of <paramref name="shapeId"/> in <paramref name="colourId"/>, lit, hanging from
        /// <paramref name="localPosition"/> (its origin is the top of its ring). Null when the model is missing.
        /// </summary>
        public static GameObject SpawnLantern(ModelKit kit, string shapeId, string colourId, Transform parent,
            Vector3 localPosition, float yaw = 0f, float scale = 1f)
        {
            string model = LanternArt.Model(shapeId);
            var lantern = kit.Spawn(model, parent, localPosition, yaw, scale);
            if (lantern != null)
            {
                Tint(kit, lantern, shapeId, colourId);
            }

            return lantern;
        }

        /// <summary>Gives a spawned lantern's paper the colour <paramref name="colourId"/> (a refold changes it in place).</summary>
        public static void Tint(ModelKit kit, GameObject lantern, string shapeId, string colourId)
        {
            var paper = ModelKit.FindDeep(lantern.transform, LanternArt.PaperGlow);
            var renderer = paper != null ? paper.GetComponent<Renderer>() : null;
            if (renderer == null)
            {
                return;
            }

            MaterialLibrary.ApplyAll(renderer, TintedMaterial(kit.GlowMaterialFor(LanternArt.Model(shapeId), GlowLevels.PaperLantern), colourId));
        }

        /// <summary>The atlas glow material with the paper and light colours of <paramref name="colourId"/>.</summary>
        public static Material TintedMaterial(Material source, string colourId)
        {
            if (source == null)
            {
                return null;
            }

            var key = (source, colourId);
            if (Tinted.TryGetValue(key, out var material) && material != null)
            {
                return material;
            }

            material = new Material(source) { name = source.name + "_" + colourId };
            material.SetColor(BaseColorId, Hex(LanternArt.PaperRgb(colourId)));
            Color light = Hex(LanternArt.LightRgb(colourId)).linear;
            float k = GlowLevels.PaperLantern;
            // Linear HDR value set as a vector, as MaterialLibrary does: SetColor would treat it as sRGB.
            material.SetVector(EmissionColorId, new Vector4(light.r * k, light.g * k, light.b * k, 1f));
            Tinted[key] = material;
            return material;
        }

        /// <summary>Where a lantern hangs from a spawned hook (world space), or null when the hook has no hang point.</summary>
        public static Vector3? HangPoint(GameObject hook)
        {
            var hang = hook != null ? ModelKit.FindDeep(hook.transform, LanternArt.HookHang) : null;
            return hang != null ? hang.position : (Vector3?)null;
        }

        private static Color Hex(int rgb) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, 1f);
    }
}
