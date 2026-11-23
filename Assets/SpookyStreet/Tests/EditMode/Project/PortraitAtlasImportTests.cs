using System.IO;
using NUnit.Framework;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.EditorTools;
using UnityEditor;
using UnityEngine;

namespace SpookyStreet.Tests.Project
{
    /// <summary>
    /// The dialogue portrait atlas (tools/blender/export/portraits.py) imports like the other UI sprites and holds a
    /// drawn badge in every (speaker, emotion) cell of <see cref="PortraitAtlasLayout"/>.
    /// </summary>
    public class PortraitAtlasImportTests
    {
        [Test]
        public void AtlasImportsAsAFullSize2DSpriteLikeTheOtherUiSprites()
        {
            string path = PortraitAtlasLayout.AssetPath;
            Assert.IsTrue(UiSpriteImportRules.IsPortraitAtlasPath(path));
            Assert.AreEqual(PortraitAtlasLayout.AtlasSize, UiSpriteImportRules.MaxSizeFor(path));
            var importer = AssetImporter.GetAtPath(path) as TextureImporter;
            Assert.IsNotNull(importer, path + " has no texture importer");
            Assert.AreEqual(TextureImporterType.Sprite, importer.textureType);
            Assert.AreEqual(TextureImporterShape.Texture2D, importer.textureShape);
            Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode);
            Assert.IsFalse(importer.mipmapEnabled, "no mipmaps");
            Assert.IsTrue(importer.sRGBTexture);
            Assert.IsTrue(importer.alphaIsTransparency);
            Assert.IsFalse(importer.isReadable);
            Assert.AreEqual(TextureWrapMode.Clamp, importer.wrapMode);
            Assert.AreEqual(FilterMode.Bilinear, importer.filterMode);
            Assert.AreEqual(Vector4.zero, importer.spriteBorder);
            Assert.AreEqual(PortraitAtlasLayout.AtlasSize, importer.maxTextureSize, "the atlas keeps its full size");
            var ios = importer.GetPlatformTextureSettings(UiSpriteImportRules.IosPlatform);
            Assert.IsTrue(ios.overridden, "iOS override");
            Assert.AreEqual(TextureImporterFormat.ASTC_4x4, ios.format, "iOS format");
            Assert.AreEqual(PortraitAtlasLayout.AtlasSize, ios.maxTextureSize, "iOS size");

            var texture = Resources.Load<Texture2D>(PortraitAtlasLayout.ResourcePath);
            Assert.IsNotNull(texture, "Resources/" + PortraitAtlasLayout.ResourcePath);
            Assert.AreEqual(PortraitAtlasLayout.AtlasSize, texture.width);
            Assert.AreEqual(PortraitAtlasLayout.AtlasSize, texture.height);
        }

        [Test]
        public void EverySpeakerAndEmotionHasADrawnBadgeThatCutsIntoASprite()
        {
            // The imported texture is compressed and not readable: read the source PNG for the pixel checks.
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.IsTrue(source.LoadImage(File.ReadAllBytes(PortraitAtlasLayout.AssetPath)));
                Assert.AreEqual(PortraitAtlasLayout.AtlasSize, source.width);
                Assert.AreEqual(PortraitAtlasLayout.AtlasSize, source.height);
                var texture = Resources.Load<Texture2D>(PortraitAtlasLayout.ResourcePath);
                Assert.IsNotNull(texture);
                foreach (var speaker in PortraitAtlasLayout.Speakers)
                {
                    foreach (var emotion in PortraitAtlasLayout.Emotions)
                    {
                        Assert.IsTrue(PortraitAtlasLayout.TryGet(speaker, emotion, out var rect), speaker + " " + emotion);
                        int cx = rect.X + Mathf.RoundToInt(PortraitAtlasLayout.DiscCenterX * rect.Width);
                        int cy = rect.Y + Mathf.RoundToInt(PortraitAtlasLayout.DiscCenterY * rect.Height);
                        int r = Mathf.RoundToInt(PortraitAtlasLayout.DiscRadius * rect.Width);
                        Assert.Greater(source.GetPixel(cx, cy).a, 0.99f, rect.Id + " is empty at the disc's centre");
                        Assert.Greater(source.GetPixel(cx, cy - r + 6).a, 0.99f, rect.Id + " has no disc");
                        Assert.Greater(source.GetPixel(cx - r + 6, cy).a, 0.99f, rect.Id + " has no disc");
                        // the bottom corners lie outside the disc, and nothing may rise out of it down there
                        Assert.Less(source.GetPixel(rect.X + 2, rect.Y + 2).a, 0.01f, rect.Id + " bottom left");
                        Assert.Less(source.GetPixel(rect.X + rect.Width - 3, rect.Y + 2).a, 0.01f, rect.Id + " bottom right");

                        var sprite = Sprite.Create(texture, new Rect(rect.X, rect.Y, rect.Width, rect.Height),
                            new Vector2(0.5f, 0.5f), 100f, 0, SpriteMeshType.FullRect);
                        Assert.IsNotNull(sprite, rect.Id);
                        Assert.AreEqual(rect.Width, Mathf.RoundToInt(sprite.rect.width), rect.Id);
                        Object.DestroyImmediate(sprite);
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }

        [Test]
        public void TheGutterBetweenCellsIsEmpty()
        {
            var source = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            try
            {
                Assert.IsTrue(source.LoadImage(File.ReadAllBytes(PortraitAtlasLayout.AssetPath)));
                foreach (var rect in PortraitAtlasLayout.All)
                {
                    for (int i = 0; i < rect.Width; i += 7)
                    {
                        Assert.Less(source.GetPixel(rect.X + i, rect.Y - 2).a, 0.01f, rect.Id + " bleeds below");
                        Assert.Less(source.GetPixel(rect.X - 2, rect.Y + i).a, 0.01f, rect.Id + " bleeds left");
                    }
                }
            }
            finally
            {
                Object.DestroyImmediate(source);
            }
        }
    }
}
