using NUnit.Framework;
using SpookyStreet.Core.Nonverbal;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// The notebook's Inspect glyph rides a corner of the focus outline that the open memory card cannot cover
    /// (docs/S004-sticker-memory.md §6, frame 92: the card hid it at the outline's top-left).
    /// </summary>
    public class GlyphPlacementTests
    {
        private const float Glyph = 46f;

        // Canvas-like numbers from frame 92: the memory card over the page, a tray sticker just under its lower edge.
        private static readonly UiBox Card = new UiBox(-330f, -20f, 330f, 292f);
        private static readonly UiBox TraySticker = new UiBox(-80f, -100f, 0f, -26f);

        private static UiBox At((float X, float Y) p) => UiBox.Around(p.X, p.Y, Glyph);

        [Test]
        public void WithNoMemoryOpenTheGlyphRidesTheBottomLeftCorner()
        {
            var p = GlyphPlacement.Inspect(TraySticker, null, Glyph);
            Assert.AreEqual((TraySticker.XMin, TraySticker.YMin), p);
        }

        [Test]
        public void TheCardNeverCoversTheGlyphOnATraySticker()
        {
            // The old top-left corner sat under the card's lower edge (what frame 92 showed).
            Assert.IsTrue(UiBox.Around(TraySticker.XMin, TraySticker.YMax, Glyph).Overlaps(Card), "the old corner was covered");
            var p = GlyphPlacement.Inspect(TraySticker, Card, Glyph);
            Assert.IsFalse(At(p).Overlaps(Card), "the glyph is clear of the card at " + At(p));
            Assert.AreEqual((TraySticker.XMin, TraySticker.YMin), p, "bottom-left");
        }

        [Test]
        public void ACardOverTheBottomCornersSendsTheGlyphToAClearCorner()
        {
            // A card hanging low over the left of the outline: the top-right is the next clear corner (the bottom-right
            // is the Submit glyph's).
            var low = new UiBox(-400f, -130f, -60f, 300f);
            var p = GlyphPlacement.Inspect(TraySticker, low, Glyph);
            Assert.IsFalse(At(p).Overlaps(low));
            Assert.AreEqual((TraySticker.XMax, TraySticker.YMax), p, "top-right");
        }

        [Test]
        public void AnOutlineUnderTheCardPutsTheGlyphJustBelowIt()
        {
            // A slot sticker's memory: the card covers the whole outline, so the glyph waits under the card's edge.
            var slot = new UiBox(-40f, 100f, 40f, 180f);
            var p = GlyphPlacement.Inspect(slot, Card, Glyph);
            Assert.IsFalse(At(p).Overlaps(Card));
            Assert.AreEqual(slot.CenterX, p.X, 1e-4f, "level with the outline");
            Assert.AreEqual(Card.YMin - GlyphPlacement.CardGap - Glyph * 0.5f, p.Y, 1e-4f);
        }
    }
}
