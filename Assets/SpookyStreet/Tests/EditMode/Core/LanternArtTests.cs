using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.World;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// The paper lantern art contract (docs/living-street-lantern-art.md): the shapes D35 allows, the picker's colours
    /// away from the clue materials, the glow under the attic window, and the names the living street spawns by.
    /// </summary>
    public class LanternArtTests
    {
        [Test]
        public void TheFourShapesAreBatCatGhostAndLeafNeverAReservedLook()
        {
            CollectionAssert.AreEqual(new[] { "bat", "cat", "ghost", "leaf" }, LanternArt.Shapes);
            foreach (string shape in LanternArt.Shapes)
            {
                foreach (string reserved in LanternArt.ReservedShapes)
                {
                    StringAssert.DoesNotContain(reserved, LanternArt.Model(shape).ToLowerInvariant(), shape);
                    StringAssert.DoesNotContain(reserved, LanternArt.ShapeTile(shape), shape);
                }
            }

            CollectionAssert.AreEquivalent(new[] { "moon", "star", "pumpkin" }, LanternArt.ReservedShapes);
            Assert.AreEqual("PaperLanternBat", LanternArt.Model(LanternArt.Bat));
            Assert.AreEqual("PaperLanternLeaf", LanternArt.Model(LanternArt.Leaf));
            Assert.Throws<ArgumentException>(() => LanternArt.Model("moon"));
            Assert.Throws<ArgumentException>(() => LanternArt.Model("Bat"), "IDs are exact, never display text");
        }

        private static (float h, float s, float v) Hsv(int rgb)
        {
            float r = ((rgb >> 16) & 0xFF) / 255f, g = ((rgb >> 8) & 0xFF) / 255f, b = (rgb & 0xFF) / 255f;
            float max = Math.Max(r, Math.Max(g, b)), min = Math.Min(r, Math.Min(g, b));
            float d = max - min;
            float h = 0f;
            if (d > 0f)
            {
                h = max == r ? 60f * (((g - b) / d) % 6f) : max == g ? 60f * ((b - r) / d + 2f) : 60f * ((r - g) / d + 4f);
            }

            return ((h + 360f) % 360f, max > 0f ? d / max : 0f, max);
        }

        [Test]
        public void ColoursAreTheArtBiblesFamiliesNeverSilverOrLavender()
        {
            CollectionAssert.AreEqual(new[] { "orange", "plum", "green", "yellow" }, LanternArt.Colours);
            // §3.4 rule 6: cloth silver (#DAE2EE, June's cape and its clues) and lavender foil (the decoy) are clue
            // materials. Silver is pale and grey; lavender is a pale blue-violet. Every paper and light must be saturated
            // and never a pale blue-violet.
            foreach (string colour in LanternArt.Colours)
            {
                foreach (int rgb in new[] { LanternArt.PaperRgb(colour), LanternArt.LightRgb(colour) })
                {
                    var (h, s, v) = Hsv(rgb);
                    Assert.Greater(s, 0.4f, colour + ": a saturated paper, never silver or grey");
                    bool paleViolet = h >= 230f && h <= 300f && s < 0.6f && v > 0.6f;
                    Assert.IsFalse(paleViolet, colour + $": #{rgb:X6} reads as lavender");
                    Assert.IsFalse(h > 190f && h < 280f, colour + ": no blue or violet light (the clue foils' family)");
                }
            }

            // The paper colours are the art bible's palette (the slice's picker swatches).
            Assert.AreEqual(0xEE7F2D, LanternArt.PaperRgb(LanternArt.Orange));
            Assert.AreEqual(0x7A3E6E, LanternArt.PaperRgb(LanternArt.Plum));
            Assert.AreEqual(0xF6D365, LanternArt.PaperRgb(LanternArt.Yellow));
            Assert.Throws<ArgumentException>(() => LanternArt.PaperRgb("silver"));
        }

        [Test]
        public void LanternsGlowLikeTheCrowdsLanternsFarUnderTheAtticWindow()
        {
            Assert.AreEqual(0.9f, GlowLevels.PaperLantern, 1e-6f, "the crowd kit's lantern level (CrowdArtCaptureTests)");
            Assert.Less(GlowLevels.PaperLantern, 1f, "under the bloom threshold: a soft inner glow");
            Assert.Less(GlowLevels.PaperLantern * 2f, GlowLevels.AtticWindow, "the attic stays the brightest point");
            Assert.Less(GlowLevels.PaperLantern, GlowLevels.Lamp, "under the street lamps");
            Assert.AreEqual(GlowLevels.PaperLantern, GlowLevels.IntensityFor(LanternArt.PaperGlow));
            Assert.AreEqual(GlowLevels.PaperLantern, GlowLevels.IntensityFor(LanternArt.TableSamplesGlow));
            Assert.IsTrue(GlowLevels.IsGlowPart(LanternArt.PaperGlow));
            Assert.IsFalse(GlowLevels.IsGlowPart(LanternArt.HookHang));
            Assert.IsFalse(GlowLevels.IsGlowPart(LanternArt.BellButton));
            Assert.AreEqual(GlowLevels.PaperBatEdge, GlowLevels.IntensityFor("PaperBat_Glow"), "the decorative bats keep theirs");
        }

        [Test]
        public void TheTableShowsEveryShapeInEveryColourOnce()
        {
            var shapes = new HashSet<string>();
            var colours = new HashSet<string>();
            foreach (var sample in LanternArt.TableSamples)
            {
                Assert.IsTrue(LanternArt.IsShape(sample.Key));
                Assert.IsTrue(LanternArt.IsColour(sample.Value));
                shapes.Add(sample.Key);
                colours.Add(sample.Value);
            }

            Assert.AreEqual(4, shapes.Count);
            Assert.AreEqual(4, colours.Count);
            Assert.Less(LanternArt.TableTopHeight, 0.75f, "a child's craft table (Pip is 1.05 m)");
            Assert.Greater(LanternArt.TablePoleHeight, 1.05f + 0.5f, "the lit samples hang over Pip's head, seen from the gameplay camera");
        }

        [Test]
        public void TheHookHangsTheLanternOverTheLawnBesidePipsPorch()
        {
            // Turned by PorchHookYaw, the arm's +X points west, out past the west rail (x = -1.95) over the lawn.
            double yaw = LanternArt.PorchHookYaw * Math.PI / 180.0;
            double hx = LanternArt.PorchHookX + LanternArt.HangX * Math.Cos(yaw) + LanternArt.HangZ * Math.Sin(yaw);
            double hz = LanternArt.PorchHookZ - LanternArt.HangX * Math.Sin(yaw) + LanternArt.HangZ * Math.Cos(yaw);
            Assert.Less(hx, -1.95 - 0.3, "west of the rail");
            Assert.That(hz, Is.InRange(-13.4, -12.8), "by the porch's front corner");
            float centre = LanternArt.PorchHookY + LanternArt.HangHeight - LanternArt.DropToCentre(LanternArt.Cat);
            Assert.That(centre, Is.InRange(2.2f, 2.6f), "about the slice's lantern height (2.35 m)");
            foreach (string shape in LanternArt.Shapes)
            {
                Assert.That(LanternArt.DropToCentre(shape), Is.InRange(0.12f, 0.3f), shape);
            }

            Assert.That(LanternArt.BellHeight, Is.InRange(0.8f, 1.1f), "a six-year-old reaches it from the deck");
        }

        [Test]
        public void PicturePathsAreStableAndInTheirFolder()
        {
            Assert.AreEqual("UI/LanternPicker/lantern_shape_ghost", LanternArt.ShapeTile(LanternArt.Ghost));
            Assert.AreEqual("UI/LanternPicker/lantern_colour_plum", LanternArt.ColourTile(LanternArt.Plum));
            Assert.IsTrue(LanternArt.IsTileAssetPath(LanternArt.TileAssetFolder + "lantern_shape_bat.png"));
            Assert.IsFalse(LanternArt.IsTileAssetPath("Assets/SpookyStreet/Content/Resources/UI/CostumeIcons/costume_icon_star_cape.png"));
            Assert.IsFalse(LanternArt.IsTileAssetPath(null));
            Assert.Throws<ArgumentException>(() => LanternArt.ColourTile("lavender"));
        }
    }
}
