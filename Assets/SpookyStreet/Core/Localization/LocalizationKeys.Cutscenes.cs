namespace SpookyStreet.Core.Localization
{
    public static partial class LocalizationKeys
    {
        /// <summary>
        /// Cutscenes (S002.5b, D28): the skip disc, the notebook's rewatch buttons and case 01's caption lines. Captions
        /// describe only what is seen or heard (D26, narrative bible §5), never what it means.
        /// </summary>
        public static class Cutscenes
        {
            public const string Skip = "ui.cutscene.skip";
            public const string SkipAgain = "ui.cutscene.skip_again";
            public const string WatchOpening = "ui.cutscene.watch_opening";
            public const string WatchReveal = "ui.cutscene.watch_reveal";

            public const string OpeningTown = "c01.cutscene.opening.town";
            public const string OpeningBoxes = "c01.cutscene.opening.boxes";
            public const string OpeningWindow = "c01.cutscene.opening.window";
            public const string OpeningWave = "c01.cutscene.opening.wave";

            public const string RevealEntry = "c01.cutscene.reveal.entry";
            public const string RevealSource = "c01.cutscene.reveal.source";
            public const string RevealMoved = "c01.cutscene.reveal.moved";
            public const string RevealReopen = "c01.cutscene.reveal.reopen";
            public const string RevealPhoto = "c01.cutscene.reveal.photo";

            public static readonly string[] All =
            {
                Skip, SkipAgain, WatchOpening, WatchReveal,
                OpeningTown, OpeningBoxes, OpeningWindow, OpeningWave,
                RevealEntry, RevealSource, RevealMoved, RevealReopen, RevealPhoto,
            };
        }
    }
}
