namespace SpookyStreet.Core.Flow
{
    /// <summary>
    /// Focus graphs of the screens that used the uGUI selection until the S004 accessibility pass (stream F2): the
    /// title, the pause card, the notice card and the parent gate. On the shared <see cref="UiFocusGraph"/> they get the
    /// candy outline, the per-scheme prompts and the screen stack, so a card that closes over the pause card hands
    /// focus back to it (the pause card had none after the settings card or the language screen closed).
    /// </summary>
    public static class ScreenFocus
    {
        // ------------------------------------------------------------------------------------- title

        public const string TitleContinue = "continue";
        public const string TitleNewGame = "new_game";
        public const string TitleSettings = "settings";
        public const string TitleLanguage = "language";

        /// <summary>
        /// The title: Continue over New game on the left, the grown-ups' gear top-right and the language pill
        /// bottom-right. Focus starts on Continue (New game without a save). Back never starts anything: the screen
        /// sends it back to the first button (<see cref="UiFocusGraph.Exit"/> is New game only to satisfy the rule that
        /// every screen has one; the runtime's Back override takes the press first).
        /// </summary>
        public static UiFocusGraph Title()
        {
            var graph = new UiFocusGraph("title");
            graph.Add(TitleContinue, 1, 0);
            graph.Add(TitleNewGame, 2, 0);
            graph.Add(TitleSettings, 0, 1);
            graph.Add(TitleLanguage, 3, 1);
            graph.Link(TitleContinue, UiDirection.Right, TitleSettings);
            graph.Link(TitleNewGame, UiDirection.Right, TitleLanguage);
            graph.Link(TitleSettings, UiDirection.Left, TitleContinue);
            graph.Link(TitleLanguage, UiDirection.Left, TitleNewGame);
            graph.Exit = TitleNewGame;
            graph.DefaultFocus = TitleContinue;
            return graph;
        }

        // ------------------------------------------------------------------------------------- pause

        public const string PauseResume = "resume";
        public const string PauseToTitle = "to_title";
        public const string PauseLanguage = "language";
        public const string PauseSettings = "settings";

        /// <summary>
        /// The pause card: the globe top-left and the gear top-right (added by the case layer), Resume, then Back to
        /// title. Back resumes, like the Resume button.
        /// </summary>
        public static UiFocusGraph Pause()
        {
            var graph = new UiFocusGraph("pause");
            graph.Add(PauseResume, 1, 1);
            graph.Add(PauseToTitle, 2, 1);
            graph.Add(PauseLanguage, 0, 0);
            graph.Add(PauseSettings, 0, 2);
            graph.Exit = PauseResume;
            graph.DefaultFocus = PauseResume;
            return graph;
        }

        // ------------------------------------------------------------------------------------- notice

        public const string NoticePrimary = "primary";
        public const string NoticeSecondary = "secondary";

        /// <summary>
        /// The notice card: its secondary (the safe choice, such as "No") left of its primary. Focus starts on the
        /// primary as before; Back is the secondary when there is one, else the primary (the card's only way out).
        /// </summary>
        public static UiFocusGraph Notice(bool hasSecondary)
        {
            var graph = new UiFocusGraph("notice");
            graph.AddRow(0, NoticeSecondary, NoticePrimary);
            graph.DefaultFocus = NoticePrimary;
            graph.Exit = hasSecondary ? NoticeSecondary : NoticePrimary;
            if (!hasSecondary)
            {
                graph.SetEnabled(NoticeSecondary, false);
            }

            return graph;
        }

        // ------------------------------------------------------------------------------------- parent gate

        public const string GateClose = "close";

        /// <summary>
        /// The parent gate: its close disc is the only element and the exit, so Back (Escape, East) cancels the gate on
        /// every scheme. The spots themselves are held (both bumpers, or Q and P), never focused.
        /// </summary>
        public static UiFocusGraph Gate()
        {
            var graph = new UiFocusGraph("parent_gate");
            graph.Add(GateClose, 0, 0);
            graph.Exit = GateClose;
            return graph;
        }
    }
}
