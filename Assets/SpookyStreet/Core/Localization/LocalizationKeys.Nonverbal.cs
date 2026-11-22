namespace SpookyStreet.Core.Localization
{
    public static partial class LocalizationKeys
    {
        /// <summary>
        /// Non-verbal UX v2 (docs/nonverbal-ux.md, decision D23): the Helper settings row, memory replay controls and
        /// the parent gate. The reading choice and the words toggle were retired with D23.1 (one hybrid mode). The
        /// screens are pictures first; these strings are the optional text and the accessibility names. Kept in its own file like <see cref="CaseUi"/>; <see cref="NonverbalUi.All"/>
        /// is checked against en.json by NonverbalLocalizationTests.
        /// </summary>
        public static class NonverbalUi
        {
            public const string SettingsHelper = "ui.settings.helper";
            public const string MemoryWatchAgain = "ui.memory.watch_again";
            public const string MemorySkip = "ui.memory.skip";
            public const string ParentGateHint = "ui.parent_gate.hint";

            public static readonly string[] All =
            {
                SettingsHelper,
                MemoryWatchAgain,
                MemorySkip,
                ParentGateHint,
            };
        }
    }
}
