namespace SpookyStreet.Core.LivingStreet
{
    /// <summary>
    /// The lantern picker's layout and look in canvas units (reference 1334 x 750, the canvas matches its height), so
    /// the rules are tested without the engine (<c>Runtime/LivingStreet/LanternView.cs</c> draws from these).
    /// <list type="bullet">
    /// <item>Names fit: each card is wide enough that the longest shape name (pt-BR "Fantasminha", about 165 units in
    /// Andika Bold at 26) keeps at least <see cref="MinLabelMargin"/> of paper on each side inside its label box, at
    /// full size. The text is never shortened or shrunk for it (layout first).</item>
    /// <item>The board fits the narrowest screen the game supports (4:3, 1000 units wide) and stays clear of the close
    /// disc in the top-right corner.</item>
    /// <item>Readable resting cards: a card that is not chosen keeps full paper and its picture at full strength, so its
    /// name reads as ink on bone (13.8:1). The chosen card stands out by shape and colour together, never by fading the
    /// others: it sits on a plum paper mat (well above 3:1 against the board), grows, and its stitch turns orange.</item>
    /// </list>
    /// </summary>
    public static class LanternPickerLayout
    {
        public const float ReferenceHeight = 750f;

        /// <summary>The narrowest canvas: a 4:3 iPad (the canvas matches height, so 750 x 4 / 3).</summary>
        public const float NarrowestCanvasWidth = 1000f;

        public const float CardWidth = 208f;
        public const float CardHeight = 184f;
        public const float CardGap = 22f;

        /// <summary>Paper between the outer cards and the board's edge.</summary>
        public const float BoardSide = 28f;

        public const float BoardHeight = 512f;

        /// <summary>The board's centre sits this far above the screen's centre (negative: below).</summary>
        public const float BoardOffsetY = -4f;

        public const int LabelFontSize = 26;

        /// <summary>The name's box is the card less this much on each side.</summary>
        public const float LabelInset = 8f;

        /// <summary>The longest name keeps at least this much free paper on each side of its box.</summary>
        public const float MinLabelMargin = 10f;

        // The close disc (UiFactory.SystemDiscDiameter, anchored to the safe area's top-right corner).
        public const float CloseDiscDiameter = 100f;
        public const float CloseDiscInsetX = 60f;
        public const float CloseDiscInsetY = 58f;

        // Look (0xRRGGBB): the paper, the ink, the chosen card's mat and stitch.
        public const int PaperRgb = 0xF4E6C8;       // Palette.WarmPaper (bone)
        public const int InkRgb = 0x1E1A26;         // Palette.InkBlack
        public const int ChosenMatRgb = 0x7A3E6E;   // Palette.Plum, tinting the paper mat
        public const int ChosenStitchRgb = 0xEE7F2D; // Palette.JackOrange
        public const int RestingStitchRgb = 0x5B3A7A; // Palette.HauntedPurple, every paper card's stitch

        /// <summary>A card that is not chosen keeps full paper (it used to be dimmed to about 85 %).</summary>
        public const int RestingCardTintRgb = 0xFFFFFF;

        /// <summary>A card that is not chosen keeps its picture at full strength (it used to be faded to 55 %).</summary>
        public const float RestingArtAlpha = 1f;

        public const float ChosenScale = 1.06f;

        /// <summary>The chosen card's mat shows this much plum around the card.</summary>
        public const float ChosenMatPad = 10f;

        public static float BoardWidth => 4f * CardWidth + 3f * CardGap + 2f * BoardSide;

        public static float LabelWidth => CardWidth - 2f * LabelInset;

        /// <summary>The widest a shape's name may be at full size (the box less its margins).</summary>
        public static float LabelTextBudget => LabelWidth - 2f * MinLabelMargin;

        /// <summary>Centre x of shape card (or colour swatch) <paramref name="index"/> of four.</summary>
        public static float ColumnX(int index) => (index - 1.5f) * (CardWidth + CardGap);

        /// <summary>The board's top edge, measured from the canvas's bottom.</summary>
        public static float BoardTop => ReferenceHeight * 0.5f + BoardOffsetY + BoardHeight * 0.5f;

        /// <summary>The board's right edge on a canvas <paramref name="canvasWidth"/> wide.</summary>
        public static float BoardRight(float canvasWidth) => canvasWidth * 0.5f + BoardWidth * 0.5f;

        /// <summary>True when the board and the chosen card's mat never touch the close disc on this canvas.</summary>
        public static bool ClearOfCloseDisc(float canvasWidth)
        {
            float discLeft = canvasWidth - CloseDiscInsetX - CloseDiscDiameter * 0.5f;
            float discBottom = ReferenceHeight - CloseDiscInsetY - CloseDiscDiameter * 0.5f;
            return BoardRight(canvasWidth) < discLeft || BoardTop < discBottom;
        }
    }
}
