using SpookyStreet.Core.World;
using UnityEditor;
using UnityEngine;

namespace SpookyStreet.EditorTools
{
    /// <summary>
    /// Import settings for the S004 sketch pages in <c>Content/Resources/UI/SketchPages</c> (written by
    /// <c>tools/blender/pages/sketch_pages.py</c>). Kept in code because tools/unity_meta.py writes bare importer metas:
    /// a single 2D sprite at its full 1024 × 768, sRGB with alpha (the page has a ragged, transparent edge), no mipmaps,
    /// clamp, ASTC 4×4 on iOS. The models and textures of the other keepsakes follow <see cref="ModelImportRules"/>.
    /// </summary>
    public sealed class KeepsakeArtImportRules : AssetPostprocessor
    {
        public const string IosPlatform = "iPhone";
        public const int MaxTextureSize = 1024;
        public const int PixelsPerUnit = 100;

        public override uint GetVersion() => 1;

        private void OnPreprocessTexture()
        {
            if (!KeepsakeArt.IsSketchPageAssetPath(assetPath))
            {
                return;
            }

            var importer = (TextureImporter)assetImporter;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureType = TextureImporterType.Sprite;
            settings.textureShape = TextureImporterShape.Texture2D;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteGenerateFallbackPhysicsShape = false;
            settings.spritePixelsPerUnit = PixelsPerUnit;
            settings.spriteAlignment = (int)SpriteAlignment.Center;
            settings.mipmapEnabled = false;
            settings.sRGBTexture = true;
            settings.alphaSource = TextureImporterAlphaSource.FromInput;
            settings.alphaIsTransparency = true;
            settings.readable = false;
            settings.npotScale = TextureImporterNPOTScale.None;
            settings.wrapMode = TextureWrapMode.Clamp;
            settings.filterMode = FilterMode.Bilinear;
            settings.aniso = 1;
            importer.SetTextureSettings(settings);
            importer.maxTextureSize = MaxTextureSize;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SetPlatformTextureSettings(new TextureImporterPlatformSettings
            {
                name = IosPlatform,
                overridden = true,
                maxTextureSize = MaxTextureSize,
                format = TextureImporterFormat.ASTC_4x4,
                compressionQuality = (int)TextureCompressionQuality.Normal,
            });
        }
    }
}
