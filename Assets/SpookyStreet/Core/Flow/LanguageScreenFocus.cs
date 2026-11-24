namespace SpookyStreet.Core.Flow
{
    /// <summary>
    /// The language screen's focus graph (S004 D1 on the F1 base): one row per language slot with its side button
    /// (Remove or Update) left of its main button (Use or Download), then More languages, then the close disc, which
    /// is the exit. Slots with no button (the language in use, empty rows) are disabled by the screen each frame.
    /// </summary>
    public static class LanguageScreenFocus
    {
        /// <summary>Language rows the screen shows at most.</summary>
        public const int Rows = 3;

        public const string Close = "close";
        public const string More = "more";

        public static string Main(int row) => "row" + row + ".main";

        public static string Side(int row) => "row" + row + ".side";

        public static UiFocusGraph Build()
        {
            var graph = new UiFocusGraph("languages");
            for (int i = 0; i < Rows; i++)
            {
                graph.AddRow(i + 1, Side(i), Main(i));
            }

            graph.Add(More, Rows + 1, 0);
            graph.Add(Close, 0, 1); // added last: focus starts on a language, else More, else Close
            graph.Exit = Close;
            graph.DefaultFocus = Main(0);
            return graph;
        }
    }
}
