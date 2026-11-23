namespace SpookyStreet.Core.Localization
{
    public static partial class LocalizationKeys
    {
        /// <summary>
        /// The Continue picture recap on the title (S002.5b, <c>Core.Save.ContinueRecap</c>): a heading and the sticker
        /// count under the pictures. Never a route, a hint or an answer (D26).
        /// </summary>
        public static class Recap
        {
            public const string Heading = "ui.title.recap_heading";
            public const string Stickers = "ui.title.recap_stickers";

            public static readonly string[] All = { Heading, Stickers };
        }
    }
}
