using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Cutscenes;
using SpookyStreet.Core.World;

namespace SpookyStreet.Tests.Core
{
    /// <summary>The S004 seed and keepsake art contract (names, sizes, places): what streams A and C build on.</summary>
    public class KeepsakeArtTests
    {
        [Test]
        public void ThePhotoHangsOnThePartitionClearOfThePanelAndTheRails()
        {
            // interior.py: the service panel spans x -0.25..0.65 and z 1.08..1.95 in the mechanism's frame; the dado
            // rail is at 1.5 and the frieze rail at 3.0 (hall floor at 0.5).
            float left = KeepsakeArt.PhotoLocalX - KeepsakeArt.PhotoWidth / 2f;
            float right = KeepsakeArt.PhotoLocalX + KeepsakeArt.PhotoWidth / 2f;
            float bottom = KeepsakeArt.PhotoLocalY - KeepsakeArt.PhotoHeight / 2f;
            float top = KeepsakeArt.PhotoLocalY + KeepsakeArt.PhotoHeight / 2f;
            Assert.Less(right, -0.25f - 0.1f, "clear of the service panel");
            Assert.Greater(left, -2.4f, "on the partition");
            Assert.Greater(bottom, 1.5f + 0.1f, "above the dado rail (the push rod no longer crosses it)");
            Assert.Less(top, 3.0f - 0.1f, "below the frieze rail");
            Assert.Greater(KeepsakeArt.PhotoWidth, KeepsakeArt.PhotoHeight, "a landscape frame");
            Assert.AreEqual(0.7f, KeepsakeArt.PhotoWidth, 1e-4f);
            Assert.AreEqual(0.45f, KeepsakeArt.PhotoHeight, 1e-4f);
        }

        [Test]
        public void ThePhotoIsInTheHallAndTheCutsceneAnchorDefaultsToIt()
        {
            Assert.IsTrue(CaseRouteLayout.Inside(KeepsakeArt.PhotoWorldX - 0.5f, KeepsakeArt.PhotoWorldZ), "the hall side of the photo is inside");
            Assert.That(KeepsakeArt.PhotoWorldX, Is.EqualTo(CaseRouteLayout.NookX).Within(0.06f), "on the partition's foyer face");
            Assert.That(KeepsakeArt.PhotoWorldY, Is.InRange(1.6f + 0.6f, 2.6f), "upper wall, about 1.7 m over the hall floor");
            var anchor = CutsceneAnchors.Default(CutsceneAnchors.KitPhoto);
            Assert.AreEqual(KeepsakeArt.PhotoWorldX, anchor.Position.X, 1e-4f);
            Assert.AreEqual(KeepsakeArt.PhotoWorldY, anchor.Position.Y, 1e-4f);
            Assert.AreEqual(KeepsakeArt.PhotoWorldZ, anchor.Position.Z, 1e-4f);
            Assert.AreEqual(270f, anchor.FacingYaw, 1e-4f, "it faces west, into the hall");
        }

        [Test]
        public void FivePuppetsOnePerPlayerWithTheOwlFirstAndTheMoonAmongThem()
        {
            Assert.AreEqual(5, KeepsakeArt.Puppets.Length);
            Assert.AreEqual(KeepsakeArt.PuppetOwl, KeepsakeArt.Puppets[0], "Kit holds the owl, on the left");
            CollectionAssert.Contains(KeepsakeArt.Puppets, KeepsakeArt.PuppetMoon, "case 02's seed glints on the moon");
            CollectionAssert.AllItemsAreUnique(KeepsakeArt.Puppets);
            foreach (string puppet in KeepsakeArt.Puppets)
            {
                StringAssert.StartsWith("Puppet_", puppet);
                Assert.IsFalse(GlowLevels.IsGlowPart(puppet), "gilt paper catches a lamp's light; it does not glow");
            }
        }

        [Test]
        public void TheCrestHangsOverTheBoothInFrontOfThePorchAndReadsFromThePlaza()
        {
            Assert.AreEqual(0.35f, KeepsakeArt.CrestDiameter, 1e-4f);
            Assert.AreEqual(0f, KeepsakeArt.CrestLocalX, 1e-4f, "on the door's axis, over the ticket shutter");
            Assert.Less(KeepsakeArt.CrestLocalZ, -4f, "in front of the porch beam (the porch front is 4 m before the house origin)");
            Assert.Greater(KeepsakeArt.CrestLocalZ, -4.3f, "under the eave");
            float bottom = KeepsakeArt.CrestLocalY - KeepsakeArt.CrestDiameter / 2f;
            Assert.Greater(bottom, 0.6f + 1.6f, "clear over Pip's head on the porch (deck at 0.6 m)");
            // At the plaza's far edge (about 12 m away) a 0.35 m sign still spans over 1.5 degrees: a phone shows it.
            double degrees = 2.0 * Math.Atan(KeepsakeArt.CrestDiameter / 2.0 / 12.0) * 180.0 / Math.PI;
            Assert.Greater(degrees, 1.5);
        }

        [Test]
        public void TheMobileHasThreeRingsOfTenHooksNamedInHangingOrder()
        {
            Assert.AreEqual(30, KeepsakeArt.MobileHooks, "one hook per paper bat (spec section 4)");
            var names = new HashSet<string>();
            for (int n = 1; n <= KeepsakeArt.MobileHooks; n++)
            {
                Assert.IsTrue(names.Add(KeepsakeArt.Hook(n)));
            }

            Assert.AreEqual("Hook_01", KeepsakeArt.Hook(1));
            Assert.AreEqual("Hook_30", KeepsakeArt.Hook(30));
            Assert.AreEqual("Ring2", KeepsakeArt.Ring(2));
            Assert.AreEqual("Ring3_Close", KeepsakeArt.RingClose(3));
            Assert.AreEqual("KeepsakeLantern1_Glow", KeepsakeArt.RingLantern(1));
            Assert.Throws<ArgumentOutOfRangeException>(() => KeepsakeArt.Hook(0));
            Assert.Throws<ArgumentOutOfRangeException>(() => KeepsakeArt.Hook(31));
            Assert.Throws<ArgumentOutOfRangeException>(() => KeepsakeArt.Ring(4));
        }

        [Test]
        public void KeepsakeLanternsGlowWarmButNeverBloom()
        {
            // S004 plan, stream B acceptance: the lantern pail's inner glow stays under the bloom threshold (1).
            Assert.Less(GlowLevels.KeepsakeLantern, 1f);
            Assert.Greater(GlowLevels.KeepsakeLantern, 0.5f, "still clearly lit");
            Assert.IsTrue(GlowLevels.IsGlowPart(KeepsakeArt.PailGlow));
            Assert.AreEqual(GlowLevels.KeepsakeLantern, GlowLevels.IntensityFor(KeepsakeArt.PailGlow));
            for (int ring = 1; ring <= KeepsakeArt.MobileRings; ring++)
            {
                Assert.IsTrue(GlowLevels.IsGlowPart(KeepsakeArt.RingLantern(ring)));
                Assert.AreEqual(GlowLevels.KeepsakeLantern, GlowLevels.IntensityFor(KeepsakeArt.RingLantern(ring)));
            }

            Assert.Less(GlowLevels.KeepsakeLantern, GlowLevels.Lamp, "below every practical lamp");
            // The crest sign's painted face is lit paint: clearly under the bloom threshold and under the lanterns.
            Assert.IsTrue(GlowLevels.IsGlowPart(KeepsakeArt.CrestFace));
            Assert.AreEqual(GlowLevels.CrestSign, GlowLevels.IntensityFor(KeepsakeArt.CrestFace));
            Assert.That(GlowLevels.CrestSign, Is.InRange(0.3f, 0.8f));
            Assert.Less(GlowLevels.CrestSign, GlowLevels.KeepsakeLantern);
            Assert.Less(GlowLevels.KeepsakeLantern, GlowLevels.AtticWindow);
            Assert.AreEqual(GlowLevels.PaperBatEdge, GlowLevels.IntensityFor("PaperBat_Glow"), "the decorative bat keeps its edge");
        }

        [Test]
        public void SketchPagesAreThreeSpritesInTheirOwnFolder()
        {
            Assert.AreEqual(3, KeepsakeArt.SketchPages, "one page every ten bats");
            Assert.AreEqual("UI/SketchPages/sketch_page_1", KeepsakeArt.SketchPage(1));
            Assert.AreEqual("UI/SketchPages/sketch_page_3", KeepsakeArt.SketchPage(3));
            Assert.Throws<ArgumentOutOfRangeException>(() => KeepsakeArt.SketchPage(4));
            Assert.IsTrue(KeepsakeArt.IsSketchPageAssetPath(KeepsakeArt.SketchPageAssetFolder + "sketch_page_2.png"));
            Assert.IsFalse(KeepsakeArt.IsSketchPageAssetPath("Assets/SpookyStreet/Content/Resources/UI/paper_card.png"));
            Assert.IsFalse(KeepsakeArt.IsSketchPageAssetPath(null));
            Assert.AreEqual(0, KeepsakeArt.SketchPageWidth % 4, "block compression");
            Assert.AreEqual(0, KeepsakeArt.SketchPageHeight % 4, "block compression");
        }

        [Test]
        public void CostumePiecesNameTheirBonesAndFolder()
        {
            StringAssert.StartsWith("Models/", KeepsakeArt.CostumeFolder);
            StringAssert.DoesNotContain("Characters", KeepsakeArt.CostumeFolder, "the pieces import as static props, not as rigs");
            StringAssert.StartsWith(KeepsakeArt.CostumeFolder, KeepsakeArt.StarCapeTexture);
            Assert.AreEqual("Head", KeepsakeArt.HatBone);
            Assert.AreEqual("Pail", KeepsakeArt.PailBone);
            Assert.AreNotEqual(KeepsakeArt.ShowmanHat, KeepsakeArt.LanternPail);
        }
    }
}
