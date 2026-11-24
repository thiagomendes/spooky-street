namespace SpookyStreet.Core.Localization
{
    public static partial class LocalizationKeys
    {
        /// <summary>
        /// Keepsakes (S004 §2.1): the captions of the three old sketch pages the paper bats piece together. Each caption
        /// describes what the drawing shows, never what it means (narrative bible §5), and no child-facing keepsake
        /// string ever states a total ("x of 30" belongs to the parent page alone, engagement §2.3).
        /// </summary>
        public static class Keepsakes
        {
            public const string PageLanterns = "keepsake.page.lanterns";
            public const string PageFriends = "keepsake.page.friends";
            public const string PageMoon = "keepsake.page.moon";


            // A2: the notebook's keepsakes page and the dress-up trunk (S004 §2.1). No total anywhere.

            /// <summary>Plural: the number beside the small stack of folded bats not yet in a page (never a total).</summary>
            public const string NotebookBatStack = "keepsake.notebook.bat_stack";

            /// <summary>Accessible name of the notebook's keepsakes page.</summary>
            public const string NotebookPage = "keepsake.notebook.page";

            /// <summary>Accessible name of the trunk screen's close disc.</summary>
            public const string TrunkClose = "keepsake.trunk.close";

            public const string PieceStarCape = "keepsake.piece.star_cape";
            public const string PiecePail = "keepsake.piece.lantern_pail";
            public const string PieceHat = "keepsake.piece.showman_hat";

            public static readonly string[] All =
            {
                PageLanterns, PageFriends, PageMoon, NotebookBatStack, NotebookPage, TrunkClose, PieceStarCape, PiecePail,
                PieceHat,
            };
        }
    }
}
