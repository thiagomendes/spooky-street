using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpookyStreet.Core.Nonverbal;

namespace SpookyStreet.Tests.Core
{
    /// <summary>The baked PuppetAtlas holds exactly the puppet parts (docs/nonverbal-ux.md §12 step 9).</summary>
    public class PuppetAtlasLayoutTests
    {
        [Test]
        public void AtlasHoldsExactlyThePuppetParts()
        {
            var atlas = PuppetAtlasLayout.Parts.Select(r => r.Id).ToList();
            CollectionAssert.AreEquivalent(PuppetParts.All, atlas);
            Assert.AreEqual(atlas.Count, atlas.Distinct(StringComparer.Ordinal).Count(), "no duplicate cells");
            foreach (var extra in PuppetAtlasLayout.Extras)
            {
                Assert.IsFalse(PuppetParts.Contains(extra.Id), extra.Id + " is an extra, not a part");
                StringAssert.StartsWith("x.", extra.Id);
            }
        }

        [Test]
        public void EveryVignettePartAndFlipbookFrameResolves()
        {
            foreach (var vignette in C01Vignettes.All)
            {
                foreach (var track in vignette.Tracks)
                {
                    Assert.IsTrue(PuppetAtlasLayout.TryGet(track.Part, out _), vignette.Id + " " + track.Part);
                    foreach (var key in track.Keys)
                    {
                        if (key.Part != null)
                        {
                            Assert.IsTrue(PuppetAtlasLayout.TryGet(key.Part, out _), vignette.Id + " " + key.Part);
                        }
                    }
                }
            }

            foreach (string frame in PuppetAtlasLayout.JuneRunFrames)
            {
                Assert.IsTrue(PuppetAtlasLayout.TryGet(frame, out _), frame);
            }

            Assert.IsTrue(PuppetAtlasLayout.TryGet(PuppetAtlasLayout.Book, out _));
            Assert.IsTrue(PuppetAtlasLayout.TryGet(PuppetAtlasLayout.Pictures, out _));
            Assert.IsTrue(PuppetAtlasLayout.TryGet(PuppetAtlasLayout.BubbleTail, out _));
        }

        [Test]
        public void RectsFitTheAtlasWithoutOverlapping()
        {
            var all = new List<PuppetRect>(PuppetAtlasLayout.Parts);
            all.AddRange(PuppetAtlasLayout.Extras);
            for (int i = 0; i < all.Count; i++)
            {
                var a = all[i];
                Assert.That(a.X >= 0 && a.Y >= 0 && a.X + a.Width <= PuppetAtlasLayout.AtlasSize &&
                            a.Y + a.Height <= PuppetAtlasLayout.AtlasSize, a.Id + " outside the atlas");
                Assert.Greater(a.StageHeight, 0f, a.Id);
                for (int j = i + 1; j < all.Count; j++)
                {
                    var b = all[j];
                    bool overlap = a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height &&
                                   b.Y < a.Y + a.Height;
                    Assert.IsFalse(overlap, a.Id + " overlaps " + b.Id);
                }
            }

            Assert.IsTrue(PuppetAtlasLayout.TryGet(PuppetParts.Stage, out var stage));
            Assert.Greater(stage.Border, 0, "the stage panel is 9-sliced");
        }
    }
}
