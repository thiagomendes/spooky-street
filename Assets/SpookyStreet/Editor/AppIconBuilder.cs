using System.IO;
using System.Linq;
using SpookyStreet.Core.Art;
using UnityEditor;
using UnityEditor.Build;
using UnityEngine;

namespace SpookyStreet.EditorTools
{
    /// <summary>
    /// Generates the placeholder app icon at build time (no binary art in git) and assigns it as the
    /// default icon and the iOS App Store icon. App Store Connect rejects uploads without a 1024 px icon.
    /// </summary>
    public static class AppIconBuilder
    {
        public const int Size = 1024;
        public const string IconPath = "Assets/SpookyStreet/Content/Generated/AppIcon.png";

        /// <summary>Creates the opaque RGB icon texture in memory.</summary>
        public static Texture2D CreateTexture(int size = Size)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGB24, false) { name = "AppIcon" };
            texture.SetPixelData(AppIconPainter.PaintRgb(size), 0);
            texture.Apply(false, false);
            return texture;
        }

        /// <summary>Writes, imports and assigns the icon. Call before BuildPipeline.BuildPlayer.</summary>
        public static Texture2D EnsureIcon()
        {
            Texture2D generated = CreateTexture();
            byte[] png = generated.EncodeToPNG();
            Object.DestroyImmediate(generated);

            Directory.CreateDirectory(Path.GetDirectoryName(IconPath));
            File.WriteAllBytes(IconPath, png);
            AssetDatabase.ImportAsset(IconPath, ImportAssetOptions.ForceSynchronousImport);

            var importer = (TextureImporter)AssetImporter.GetAtPath(IconPath);
            importer.textureType = TextureImporterType.Default;
            importer.npotScale = TextureImporterNPOTScale.None;
            importer.mipmapEnabled = false;
            importer.alphaSource = TextureImporterAlphaSource.None;
            importer.textureCompression = TextureImporterCompression.Uncompressed;
            importer.maxTextureSize = Size;
            importer.isReadable = true;
            importer.SaveAndReimport();

            var icon = AssetDatabase.LoadAssetAtPath<Texture2D>(IconPath);
            PlayerSettings.SetIcons(NamedBuildTarget.Unknown, new[] { icon }, IconKind.Any);
            foreach (IconKind kind in new[] { IconKind.Application, IconKind.Store })
            {
                int slots = PlayerSettings.GetIconSizes(NamedBuildTarget.iOS, kind).Length;
                if (slots > 0)
                {
                    PlayerSettings.SetIcons(NamedBuildTarget.iOS, Enumerable.Repeat(icon, slots).ToArray(), kind);
                }
            }

            Debug.Log($"[AppIconBuilder] Assigned generated {Size}px icon from {IconPath}");
            return icon;
        }
    }
}
