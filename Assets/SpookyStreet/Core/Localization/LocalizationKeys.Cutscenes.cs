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
            /// <summary>The old photo: the seed cutscene's first line (it moved there from the reveal, S004).</summary>
            public const string RevealPhoto = "c01.cutscene.reveal.photo";

            // S004 stream C: the seed and "the street remembers" (perception only; never a later case's answer).
            public const string WatchSeed = "ui.cutscene.watch_seed";
            public const string WatchStreet = "ui.cutscene.watch_street";
            public const string SeedOwl = "c01.cutscene.seed.owl";
            public const string SeedMoon = "c01.cutscene.seed.moon";
            public const string SeedCrest = "c01.cutscene.seed.crest";
            public const string StreetLane = "c01.cutscene.street.lane";
            public const string StreetHour = "c01.cutscene.street.hour";
            public const string StreetShow = "c01.cutscene.street.show";
            public const string StreetBea = "c01.cutscene.street.bea";

            public static readonly string[] All =
            {
                Skip, SkipAgain, WatchOpening, WatchReveal,
                OpeningTown, OpeningBoxes, OpeningWindow, OpeningWave,
                RevealEntry, RevealSource, RevealMoved, RevealReopen, RevealPhoto,
                WatchSeed, WatchStreet, SeedOwl, SeedMoon, SeedCrest, StreetLane, StreetHour, StreetShow, StreetBea,
            };
        }
    }
}
