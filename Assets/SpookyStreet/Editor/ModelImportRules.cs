using SpookyStreet.Core.Animation;
using SpookyStreet.Core.World;
using UnityEditor;
using UnityEngine;

namespace SpookyStreet.EditorTools
{
    /// <summary>
    /// Import settings for every original model and baked texture under <c>Content/Resources/Models</c>, kept in
    /// code so they are reviewable and survive .meta regeneration (tools/unity_meta.py writes bare importer metas):
    /// no imported materials (the runtime builds URP Lit materials from the baked textures, so nothing can fall
    /// back to the magenta error shader), axis conversion baked into the hierarchy, a Generic rig with clean,
    /// correctly looping clip names for characters (contract in <see cref="CharacterClips"/>), and no animation for props.
    /// Textures: sRGB colour, NormalMap type for <c>*_Normal</c>, linear masks, mipmaps, ASTC on iOS at 2048.
    /// </summary>
    public sealed class ModelImportRules : AssetPostprocessor
    {
        public const string ModelsRoot = "Assets/SpookyStreet/Content/Resources/Models/";
        public const int MaxTextureSize = 2048;
        public const string IosPlatform = "iPhone";

        public override uint GetVersion() => 7; // 7: June's KneeTap loops

        public static bool IsGameModelPath(string path) => path.StartsWith(ModelsRoot, System.StringComparison.Ordinal);

        public static bool IsCharacterPath(string path) => IsGameModelPath(path) && path.Contains("/Characters/");

        /// <summary>
        /// Street-set models (EnvironmentLayout.StreetSetModels) are combined at runtime with
        /// <c>StaticBatchingUtility.Combine</c>, which needs readable meshes; every other model stays GPU-only.
        /// </summary>
        public static bool IsStaticBatchedPath(string path)
        {
            if (!IsGameModelPath(path) || !path.Contains("/Environment/"))
            {
                return false;
            }

            return System.Array.IndexOf(EnvironmentLayout.StreetSetModels, BaseName(path)) >= 0;
        }

        /// <summary>Texture role from the baked file name: "_Normal" maps, linear masks, or colour.</summary>
        public static TextureImporterType TextureTypeFor(string path) =>
            BaseName(path).EndsWith("_Normal", System.StringComparison.OrdinalIgnoreCase)
                ? TextureImporterType.NormalMap
                : TextureImporterType.Default;

        /// <summary>Colour data (BaseColor, face maps) is sRGB; normal, roughness, AO and mask data are linear.</summary>
        public static bool IsColorTexture(string path)
        {
            string name = BaseName(path);
            foreach (var suffix in new[] { "_Normal", "_Roughness", "_AO", "_Mask", "_ORM", "_Metallic" })
            {
                if (name.EndsWith(suffix, System.StringComparison.OrdinalIgnoreCase))
                {
                    return false;
                }
            }

            return true;
        }

        private static string BaseName(string path) => System.IO.Path.GetFileNameWithoutExtension(path);

        private void OnPreprocessModel()
        {
            if (!IsGameModelPath(assetPath))
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            importer.materialImportMode = ModelImporterMaterialImportMode.None;
            importer.importCameras = false;
            importer.importLights = false;
            importer.importVisibility = false;
            importer.importBlendShapes = false;
            importer.bakeAxisConversion = true;
            importer.isReadable = IsStaticBatchedPath(assetPath);
            importer.addCollider = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            importer.importNormals = ModelImporterNormals.Import;
            importer.importTangents = ModelImporterTangents.CalculateMikk; // baked normal maps need MikkTSpace
            if (IsCharacterPath(assetPath))
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.optimizeGameObjects = false; // the moveset may attach props to bones
                importer.importAnimation = true;
            }
            else
            {
                importer.animationType = ModelImporterAnimationType.None;
                importer.importAnimation = false;
            }
        }

        private void OnPreprocessAnimation()
        {
            if (!IsCharacterPath(assetPath))
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
            {
                string name = PipClips.CleanClipName(clips[i].takeName);
                bool loop = CharacterClips.IsLooping(name);
                clips[i].name = name;
                clips[i].loopTime = loop;
                clips[i].wrapMode = loop ? WrapMode.Loop : WrapMode.ClampForever;
                // Clips are authored in place (Mr. Vale's seated pelvis is in his clips); no root motion.
                clips[i].lockRootRotation = true;
                clips[i].lockRootHeightY = true;
                clips[i].lockRootPositionXZ = true;
                clips[i].keepOriginalOrientation = true;
                clips[i].keepOriginalPositionY = true;
                clips[i].keepOriginalPositionXZ = true;
            }

            importer.clipAnimations = clips;
        }

        private void OnPreprocessTexture()
        {
            if (!IsGameModelPath(assetPath))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            var type = TextureTypeFor(assetPath);
            bool normal = type == TextureImporterType.NormalMap;
            importer.textureType = type;
            // A minimal .meta leaves the shape unset and Unity then imports a cube map, which Resources.Load<Texture2D>
            // cannot return (CI run 23: Pip drawn with the flat fallback colour).
            importer.textureShape = TextureImporterShape.Texture2D;
            importer.sRGBTexture = !normal && IsColorTexture(assetPath);
            importer.mipmapEnabled = true;
            importer.streamingMipmaps = false;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Trilinear;
            importer.anisoLevel = 2;
            importer.maxTextureSize = MaxTextureSize;
            importer.textureCompression = TextureImporterCompression.Compressed;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = IosPlatform,
                overridden = true,
                maxTextureSize = MaxTextureSize,
                format = normal ? TextureImporterFormat.ASTC_5x5 : TextureImporterFormat.ASTC_6x6,
                compressionQuality = (int)TextureCompressionQuality.Normal,
            });
        }
    }
}
