namespace SpookyStreet.Core.Localization
{
    public static partial class LocalizationKeys
    {
        /// <summary>
        /// The language screen (S004 stream D1): switching needs no gate; downloading and removing are behind the
        /// parent gate. Language names themselves come from each table's native name, never from these keys.
        /// </summary>
        public static class Languages
        {
            public const string Heading = "ui.language.heading";
            public const string Use = "ui.language.use";
            public const string InUse = "ui.language.in_use";
            public const string InGame = "ui.language.in_game";
            public const string Downloaded = "ui.language.downloaded";
            public const string SizeKb = "ui.language.size_kb";
            public const string More = "ui.language.more";
            public const string Download = "ui.language.download";
            public const string Update = "ui.language.update";
            public const string Remove = "ui.language.remove";
            public const string Downloading = "ui.language.downloading";
            public const string Checking = "ui.language.checking";
            public const string None = "ui.language.none";
            public const string Offline = "ui.language.offline";
            public const string Failed = "ui.language.failed";

            public static readonly string[] All =
            {
                Heading, Use, InUse, InGame, Downloaded, SizeKb, More, Download, Update, Remove, Downloading, Checking,
                None, Offline, Failed,
            };
        }
    }
}
