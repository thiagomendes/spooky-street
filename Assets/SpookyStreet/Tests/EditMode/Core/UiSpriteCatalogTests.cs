using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SpookyStreet.Core.Art;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// The baked UI sprites (tools/ui_mock/ui_mock.py --export) match the catalog the importer and runtime use:
    /// every entry has a PNG, every PNG is catalogued, sizes stay small and 9-slice borders fit.
    /// </summary>
    public class UiSpriteCatalogTests
    {
        private static string UiFolder()
        {
            string relative = Path.Combine("Assets", "SpookyStreet", "Content", "Resources", "UI");
            string dir = Directory.GetCurrentDirectory();
            for (int i = 0; i < 8 && dir != null; i++)
            {
                string candidate = Path.Combine(dir, relative);
                if (Directory.Exists(candidate))
                {
                    return candidate;
                }

                dir = Path.GetDirectoryName(dir);
            }

            Assert.Fail("Resources/UI folder not found from " + Directory.GetCurrentDirectory());
            return null;
        }

        /// <summary>Width and height from a PNG's IHDR chunk (big-endian at bytes 16..23).</summary>
        private static void PngSize(string path, out int width, out int height)
        {
            var bytes = new byte[24];
            using (var stream = File.OpenRead(path))
            {
                Assert.AreEqual(24, stream.Read(bytes, 0, 24), path + " is too short");
            }

            Assert.AreEqual(0x89, bytes[0], path + " is not a PNG");
            Assert.AreEqual((byte)'P', bytes[1], path + " is not a PNG");
            width = (bytes[16] << 24) | (bytes[17] << 16) | (bytes[18] << 8) | bytes[19];
            height = (bytes[20] << 24) | (bytes[21] << 16) | (bytes[22] << 8) | bytes[23];
        }

        [Test]
        public void EveryCataloguedSpriteHasAPngThatFitsItsBorder()
        {
            string folder = UiFolder();
            var names = new HashSet<string>();
            foreach (var spec in UiSpriteCatalog.All)
            {
                Assert.IsTrue(names.Add(spec.Name), "duplicate catalog entry " + spec.Name);
                string path = Path.Combine(folder, spec.Name + ".png");
                Assert.IsTrue(File.Exists(path), "missing baked sprite " + spec.Name + ".png; run ui_mock.py --export");
                PngSize(path, out int width, out int height);
                Assert.AreEqual(0, width % 4, spec.Name + " width must be a multiple of 4 (block compression)");
                Assert.AreEqual(0, height % 4, spec.Name + " height must be a multiple of 4 (block compression)");
                if (spec.Name == UiSpriteCatalog.Logo)
                {
                    Assert.LessOrEqual(width, UiSpriteCatalog.MaxLogoWidth, "logo too wide");
                }
                else
                {
                    Assert.LessOrEqual(width, UiSpriteCatalog.MaxSpriteSize, spec.Name + " too large");
                    Assert.LessOrEqual(height, UiSpriteCatalog.MaxSpriteSize, spec.Name + " too large");
                }

                Assert.AreEqual(spec.Draw != UiSpriteDraw.Simple, spec.HasBorder, spec.Name + " border vs draw mode");
                Assert.Less(spec.Left + spec.Right, width, spec.Name + " horizontal border leaves no centre");
                Assert.Less(spec.Bottom + spec.Top, height, spec.Name + " vertical border leaves no centre");
            }
        }

        [Test]
        public void EveryPngInTheFolderIsCatalogued()
        {
            foreach (string path in Directory.GetFiles(UiFolder(), "*.png"))
            {
                string name = Path.GetFileNameWithoutExtension(path);
                Assert.IsTrue(UiSpriteCatalog.TryFind(name, out _), name + ".png is not in UiSpriteCatalog");
            }
        }

        [Test]
        public void MapsAssetPathsToNames()
        {
            Assert.AreEqual("paper_card", UiSpriteCatalog.NameFromAssetPath(UiSpriteCatalog.AssetFolder + "paper_card.png"));
            Assert.IsNull(UiSpriteCatalog.NameFromAssetPath(UiSpriteCatalog.AssetFolder + "Sub/paper_card.png"));
            Assert.IsNull(UiSpriteCatalog.NameFromAssetPath("Assets/SpookyStreet/Content/Resources/Models/x.png"));
            Assert.IsNull(UiSpriteCatalog.NameFromAssetPath(UiSpriteCatalog.AssetFolder + "notes.json"));
            Assert.AreEqual("UI/logo", UiSpriteCatalog.ResourcePath(UiSpriteCatalog.Logo));
        }
    }
}
