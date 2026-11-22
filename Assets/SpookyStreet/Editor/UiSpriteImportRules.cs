using SpookyStreet.Core.Art;
using UnityEditor;
using UnityEngine;

namespace SpookyStreet.EditorTools
{
    /// <summary>
    /// Import settings for the baked UI craft sprites in <c>Content/Resources/UI</c> (written by
    /// <c>tools/ui_mock/ui_mock.py --export</c>). Kept in code because tools/unity_meta.py writes bare importer metas,
    /// and a bare texture meta once imported as a cube map: the shape and type are forced here. Sprite, single, 2D,
    /// 9-slice borders from <see cref="UiSpriteCatalog"/>, 200 pixels per unit, full-rect mesh (needed for sliced and
    /// tiled drawing), no mipmaps, clamp, sRGB with alpha, ASTC 4×4 on iOS.
    /// </summary>
    public sealed class UiSpriteImportRules : AssetPostprocessor
    {
        public const string IosPlatform = "iPhone";
        public const int MaxTextureSize = 1024;

        public override uint GetVersion() => 1;

        public static bool IsUiSpritePath(string path) => UiSpriteCatalog.NameFromAssetPath(path) != null;

        /// <summary>Unity's sprite border order: x = left, y = bottom, z = right, w = top (pixels).</summary>
        public static Vector4 BorderFor(string path)
        {
            string name = UiSpriteCatalog.NameFromAssetPath(path);
            if (name == null || !UiSpriteCatalog.TryFind(name, out var spec))
            {
                return Vector4.zero;
            }

            return new Vector4(spec.Left, spec.Bottom, spec.Right, spec.Top);
        }

        private void OnPreprocessTexture()
        {
            string name = UiSpriteCatalog.NameFromAssetPath(assetPath);
            if (name == null)
            {
                return;
            }

            if (!UiSpriteCatalog.TryFind(name, out _))
            {
                Debug.LogWarning($"[UiSpriteImportRules] {assetPath} is not in UiSpriteCatalog; importing without borders.");
            }

            var importer = (TextureImporter)assetImporter;
            var settings = new TextureImporterSettings();
            importer.ReadTextureSettings(settings);
            settings.textureType = TextureImporterType.Sprite;
            settings.textureShape = TextureImporterShape.Texture2D;
            settings.spriteMode = (int)SpriteImportMode.Single;
            settings.spriteMeshType = SpriteMeshType.FullRect;
            settings.spriteExtrude = 1;
            settings.spriteGenerateFallbackPhysicsShape = false;
            settings.spritePixelsPerUnit = UiSpriteCatalog.PixelsPerUnit;
            settings.spriteBorder = BorderFor(assetPath);
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
