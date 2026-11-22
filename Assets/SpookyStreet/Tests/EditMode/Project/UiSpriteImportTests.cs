using NUnit.Framework;
using SpookyStreet.Core.Art;
using SpookyStreet.EditorTools;
using UnityEditor;
using UnityEngine;

namespace SpookyStreet.Tests.Project
{
    /// <summary>The baked UI sprites import as 2D sprites with their catalog borders (never cube maps).</summary>
    public class UiSpriteImportTests
    {
        [Test]
        public void ClassifiesUiSpritePaths()
        {
            Assert.IsTrue(UiSpriteImportRules.IsUiSpritePath(UiSpriteCatalog.AssetFolder + "paper_card.png"));
            Assert.IsFalse(UiSpriteImportRules.IsUiSpritePath(ModelImportRules.ModelsRoot + "Props/Atlas.png"));
            Assert.AreEqual(new Vector4(44f, 44f, 44f, 44f),
                UiSpriteImportRules.BorderFor(UiSpriteCatalog.AssetFolder + "paper_card.png"));
        }

        [Test]
        public void EveryUiSpriteImportsAsA2DSpriteWithItsBorder()
        {
            foreach (var spec in UiSpriteCatalog.All)
            {
                string path = UiSpriteCatalog.AssetFolder + spec.Name + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                Assert.IsNotNull(importer, path + " has no texture importer");
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType, spec.Name);
                Assert.AreEqual(TextureImporterShape.Texture2D, importer.textureShape, spec.Name);
                Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode, spec.Name);
                Assert.IsFalse(importer.mipmapEnabled, spec.Name + " must not have mipmaps");
                Assert.AreEqual((float)UiSpriteCatalog.PixelsPerUnit, importer.spritePixelsPerUnit, spec.Name);
                Assert.AreEqual(new Vector4(spec.Left, spec.Bottom, spec.Right, spec.Top), importer.spriteBorder,
                    spec.Name + " border");
                var ios = importer.GetPlatformTextureSettings(UiSpriteImportRules.IosPlatform);
                Assert.IsTrue(ios.overridden, spec.Name + " iOS override");
                Assert.AreEqual(TextureImporterFormat.ASTC_4x4, ios.format, spec.Name + " iOS format");

                var sprite = Resources.Load<Sprite>(UiSpriteCatalog.ResourcePath(spec.Name));
                Assert.IsNotNull(sprite, spec.Name + " does not load as a Sprite from Resources");
                Assert.AreEqual(new Vector4(spec.Left, spec.Bottom, spec.Right, spec.Top), sprite.border,
                    spec.Name + " runtime border");
            }
        }
    }
}
