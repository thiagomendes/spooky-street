using UnityEditor;
using UnityEngine;

namespace SpookyStreet.Editor
{
    /// <summary>
    /// Import settings for every model under <c>Content/Resources/Models</c> (docs/asset-provenance.md), kept in
    /// code so they are reviewable and survive .meta regeneration:
    /// no imported materials (the runtime builds URP materials from the palette and the kit atlases, so nothing
    /// can fall back to the magenta error shader), axis conversion baked into the hierarchy, Generic rig with
    /// named, correctly looping clips for characters, and no animation data for props.
    /// </summary>
    public sealed class ModelImportRules : AssetPostprocessor
    {
        public const string ModelsRoot = "Assets/SpookyStreet/Content/Resources/Models/";

        private static readonly string[] LoopingClips =
        {
            "Idle", "Unarmed_Idle", "Walking_A", "Running_A", "Jump_Idle", "Sit_Chair_Idle",
        };

        public override uint GetVersion() => 2;

        private bool IsGameModel => assetPath.StartsWith(ModelsRoot, System.StringComparison.Ordinal);

        private bool IsCharacter => assetPath.Contains("/Characters/");

        private void OnPreprocessModel()
        {
            if (!IsGameModel)
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
            importer.isReadable = false;
            importer.addCollider = false;
            importer.meshCompression = ModelImporterMeshCompression.Off;
            if (IsCharacter)
            {
                importer.animationType = ModelImporterAnimationType.Generic;
                importer.avatarSetup = ModelImporterAvatarSetup.CreateFromThisModel;
                importer.optimizeGameObjects = false; // the game attaches wings/antenna motion to bones
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
            if (!IsGameModel || !IsCharacter)
            {
                return;
            }

            var importer = (ModelImporter)assetImporter;
            var clips = importer.defaultClipAnimations;
            for (int i = 0; i < clips.Length; i++)
            {
                clips[i].name = CleanClipName(clips[i].takeName);
                clips[i].loopTime = System.Array.IndexOf(LoopingClips, clips[i].name) >= 0;
                clips[i].lockRootRotation = true;
                clips[i].lockRootHeightY = true;
                clips[i].lockRootPositionXZ = true;
                clips[i].keepOriginalOrientation = true;
                clips[i].keepOriginalPositionY = true;
                clips[i].keepOriginalPositionXZ = true;
            }

            importer.clipAnimations = clips;
        }

        /// <summary>Blender exports takes as "Armature|Action"; the game looks clips up by the action name.</summary>
        public static string CleanClipName(string takeName)
        {
            int bar = takeName.LastIndexOf('|');
            return bar >= 0 ? takeName.Substring(bar + 1) : takeName;
        }

        private void OnPreprocessTexture()
        {
            if (!IsGameModel)
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            importer.textureType = TextureImporterType.Default;
            importer.sRGBTexture = true;
            importer.mipmapEnabled = true;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.filterMode = FilterMode.Bilinear;
            importer.maxTextureSize = 1024;
        }
    }
}
