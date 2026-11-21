namespace SpookyStreet.Core.Localization
{
    /// <summary>
    /// Stable semantic keys used by code. Every constant here must exist in the bundled English table
    /// (enforced by tests). Never derive saved progress from these strings or their values.
    /// </summary>
    public static class LocalizationKeys
    {
        public const string TitleGameName = "ui.title.game_name";
        public const string TitleCaseName = "ui.title.case_name";
        public const string TitleNewGame = "ui.title.new_game";
        public const string TitleContinue = "ui.title.continue";
        public const string TitleLanguageToggle = "ui.title.language_toggle_dev";
        public const string TitleBuildLabel = "ui.title.build_label";

        public const string PromptMoveTouch = "prompt.first.move_touch";
        public const string PromptMoveKeyboard = "prompt.first.move_keyboard";
        public const string PromptMoveGamepad = "prompt.first.move_gamepad";

        public const string HudPause = "ui.hud.pause";
        public const string HudJump = "ui.hud.jump";
        public const string HudUse = "ui.hud.use";

        public const string PauseHeading = "ui.pause.heading";
        public const string PauseResume = "ui.pause.resume";
        public const string PauseBackToTitle = "ui.pause.back_to_title";

        /// <summary>All keys referenced from code, for validation.</summary>
        public static readonly string[] All =
        {
            TitleGameName, TitleCaseName, TitleNewGame, TitleContinue, TitleLanguageToggle, TitleBuildLabel,
            PromptMoveTouch, PromptMoveKeyboard, PromptMoveGamepad,
            HudPause, HudJump, HudUse,
            PauseHeading, PauseResume, PauseBackToTitle,
        };
    }
}
