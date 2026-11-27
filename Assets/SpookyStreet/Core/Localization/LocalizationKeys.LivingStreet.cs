namespace SpookyStreet.Core.Localization
{
    public static partial class LocalizationKeys
    {
        /// <summary>
        /// The living street's test slice (docs/living-street.md §4.1, §4.2, §4.10; D33, D34): the three trick-or-treat
        /// doors and their neighbours, the trick-or-treaters' calls, the paper lantern table and Pip handing out treats.
        /// Lines are greetings and gags only: nobody on the street talks about the case (§3.4 rule 1), and nothing counts
        /// treats or doors in front of the child (engagement §2). The frame-time readout lives on the grown-ups' page.
        /// </summary>
        public static class LivingStreet
        {
            public const string SpeakerFogg = "ui.speaker.fogg";
            public const string SpeakerPearl = "ui.speaker.pearl";
            public const string SpeakerAbbotts = "ui.speaker.abbotts";

            public const string FoggFirst = "line.fogg.first";
            public const string FoggAgain = "line.fogg.again";
            public const string FoggThird = "line.fogg.third";
            public const string PearlFirst = "line.pearl.first";
            public const string PearlAgain = "line.pearl.again";
            public const string PearlThird = "line.pearl.third";
            public const string AbbottsFirst = "line.abbotts.first";
            public const string AbbottsAgain = "line.abbotts.again";
            public const string AbbottsThird = "line.abbotts.third";

            /// <summary>Pip at a neighbour's door, before the neighbour answers.</summary>
            public const string PipTrickOrTreat = "line.pip.trick_or_treat";

            /// <summary>Pip hands a sweet to a group at her own porch.</summary>
            public const string PipHappyHalloween = "line.pip.happy_halloween";

            /// <summary>Pip's lantern is folded and hung on her porch.</summary>
            public const string PipMyLantern = "line.pip.my_lantern";

            /// <summary>A trick-or-treat group at a door (Pip's or a neighbour's).</summary>
            public const string CrowdTrickOrTreat = "line.crowd.trick_or_treat";

            /// <summary>A group thanks Pip for her treat.</summary>
            public const string CrowdThanks = "line.crowd.thanks";

            public const string VerbRing = "ui.verb.ring";
            public const string VerbMake = "ui.verb.make";
            public const string VerbTreat = "ui.verb.treat";

            public const string ShapeMoon = "ui.lantern.shape.moon";
            public const string ShapeBat = "ui.lantern.shape.bat";
            public const string ShapeStar = "ui.lantern.shape.star";
            public const string ShapePumpkin = "ui.lantern.shape.pumpkin";
            public const string ColourOrange = "ui.lantern.colour.orange";
            public const string ColourPlum = "ui.lantern.colour.plum";
            public const string ColourGreen = "ui.lantern.colour.green";
            public const string ColourYellow = "ui.lantern.colour.yellow";
            public const string Fold = "ui.lantern.fold";
            public const string Close = "ui.lantern.close";

            /// <summary>The notebook's street page (a scrapbook: Pip's porch and the neighbours she has met).</summary>
            public const string StreetPage = "ui.street_page.title";

            // The grown-ups' page (test builds only): the frame-time readout of living-street §8, and the one total.
            public const string FrameHeading = "ui.parent.frame.heading";
            public const string FrameLine = "ui.parent.frame.line";
            public const string FrameCrowd = "ui.parent.frame.crowd";
            public const string FrameWaiting = "ui.parent.frame.waiting";
            public const string NeighboursMet = "ui.parent.neighbours_met";

            public static readonly string[] All =
            {
                SpeakerFogg, SpeakerPearl, SpeakerAbbotts, FoggFirst, FoggAgain, FoggThird, PearlFirst, PearlAgain, PearlThird,
                AbbottsFirst, AbbottsAgain, AbbottsThird, PipTrickOrTreat, PipHappyHalloween, PipMyLantern, CrowdTrickOrTreat,
                CrowdThanks, VerbRing, VerbMake, VerbTreat, ShapeMoon, ShapeBat, ShapeStar, ShapePumpkin, ColourOrange,
                ColourPlum, ColourGreen, ColourYellow, Fold, Close, StreetPage, FrameHeading, FrameLine, FrameCrowd,
                FrameWaiting, NeighboursMet,
            };
        }
    }
}
