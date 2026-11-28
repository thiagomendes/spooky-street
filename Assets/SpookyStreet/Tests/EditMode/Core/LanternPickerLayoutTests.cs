using NUnit.Framework;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.LivingStreet;
using L = SpookyStreet.Core.LivingStreet.LanternPickerLayout;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// The lantern picker's layout and contrast (docs/living-street-slice.md §6): the names have room at full size, the
    /// board fits a 4:3 screen clear of the close disc, cards that are not chosen stay readable, and the chosen card
    /// stands out by a mat, its size and its stitch, never by fading the others. The fonts' real widths are measured in
    /// the running game (<c>LivingStreetPlayTests</c>); this is the arithmetic behind them.
    /// </summary>
    public class LanternPickerLayoutTests
    {
        [Test]
        public void TheLongestNameHasRoomWithMarginAtFullSize()
        {
            // pt-BR "Fantasminha" is the longest name: about 165 units in Andika Bold at 26 (measured from the font file).
            const float fantasminha = 165.3f;
            Assert.GreaterOrEqual(L.LabelFontSize, 26, "labels never go under 13 pt (26 units)");
            Assert.GreaterOrEqual(L.LabelTextBudget, fantasminha, "the longest name fits with its margins");
            Assert.GreaterOrEqual(L.MinLabelMargin, 10f);
            Assert.LessOrEqual(L.LabelWidth, L.CardWidth - 2f * L.LabelInset + 0.01f);
        }

        [Test]
        public void TheBoardFitsA43ScreenAndClearsTheCloseDisc()
        {
            Assert.LessOrEqual(L.BoardWidth, L.NarrowestCanvasWidth - 2f * 16f, "16 units of gutter on a 4:3 iPad");
            foreach (float width in new[] { L.NarrowestCanvasWidth, 1334f, 1624f })
            {
                Assert.IsTrue(L.ClearOfCloseDisc(width), "close disc at canvas width " + width);
            }

            Assert.Greater(L.BoardTop - L.BoardHeight, 60f, "the board leaves Pip's feet and the bottom of the screen free");
        }

        [Test]
        public void TheChosenMatNeverReachesItsNeighbours()
        {
            float pitch = L.ColumnX(1) - L.ColumnX(0);
            float matHalf = (L.CardWidth + 2f * L.ChosenMatPad) * L.ChosenScale * 0.5f;
            float neighbourEdge = pitch - L.CardWidth * 0.5f;
            const float tilt = 3f; // the cards' ±1.5° cut-paper tilt moves their corners about this much
            Assert.Less(matHalf + tilt, neighbourEdge, "the mat stays in the gap between cards");
            Assert.Less(L.ColumnX(3) + matHalf, L.BoardWidth * 0.5f, "the outer mat stays on the board");
        }

        [Test]
        public void RestingCardsReadAndTheChosenCardStandsOut()
        {
            // A card that is not chosen: full paper and a full-strength picture, so its name is ink on bone.
            int resting = ColourContrast.Multiply(L.PaperRgb, L.RestingCardTintRgb);
            Assert.AreEqual(1f, L.RestingArtAlpha, "pictures are never faded");
            Assert.GreaterOrEqual(ColourContrast.Ratio(L.InkRgb, resting), ColourContrast.TextAAA, "name on a resting card");

            // The old look for the record: a 55 % picture over a dimmed card washed its ink lines out.
            int oldCard = ColourContrast.Multiply(L.PaperRgb, 0xDBD6CC);
            int oldInk = ColourContrast.Over(L.InkRgb, oldCard, 0.55);
            Assert.Less(ColourContrast.Ratio(oldInk, oldCard), ColourContrast.TextAA, "what the child saw before");
            Assert.Greater(ColourContrast.Ratio(L.InkRgb, resting), 2.5 * ColourContrast.Ratio(oldInk, oldCard));

            // The chosen card: a plum mat around it (WCAG 1.4.11 against the board's paper), a larger card, an orange
            // stitch: three cues, none of them only a hue.
            int mat = ColourContrast.Multiply(L.PaperRgb, L.ChosenMatRgb);
            Assert.GreaterOrEqual(ColourContrast.Ratio(mat, L.PaperRgb), ColourContrast.NonText, "the mat against the board");
            Assert.GreaterOrEqual(ColourContrast.Ratio(L.ChosenStitchRgb, L.PaperRgb), 2.0, "the orange stitch on paper");
            Assert.AreNotEqual(L.ChosenStitchRgb, L.RestingStitchRgb);
            Assert.Greater(L.ChosenScale, 1.03f);
            Assert.GreaterOrEqual(L.ChosenMatPad, 8f, "the mat shows as a clear band");
        }

        [Test]
        public void ContrastArithmeticMatchesTheUiLanguage()
        {
            // docs/ui-language.md: ink on bone 13.8:1, bone on night 11.5:1, ink on orange 6.3:1.
            Assert.AreEqual(13.8, ColourContrast.Ratio(0x1E1A26, 0xF4E6C8), 0.15);
            Assert.AreEqual(21.0, ColourContrast.Ratio(0x000000, 0xFFFFFF), 1e-6);
            Assert.AreEqual(1.0, ColourContrast.Ratio(0x777777, 0x777777), 1e-9);
            Assert.AreEqual(0x7F7F7F, ColourContrast.Multiply(0xFFFFFF, 0x7F7F7F));
            Assert.AreEqual(0x808080, ColourContrast.Over(0xFFFFFF, 0x000000, 128 / 255.0) & 0xFEFEFE);
        }
    }
}
