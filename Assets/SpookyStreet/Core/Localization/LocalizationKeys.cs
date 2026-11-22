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

        /// <summary>Second line on Continue once saves exist (S002): chapter and paper bats found.</summary>
        public const string TitleContinueDetail = "ui.title.continue_detail";

        public const string PromptMoveTouch = "prompt.first.move_touch";
        public const string PromptMoveKeyboard = "prompt.first.move_keyboard";
        public const string PromptMoveGamepad = "prompt.first.move_gamepad";

        public const string HudPause = "ui.hud.pause";
        public const string HudJump = "ui.hud.jump";
        public const string HudUse = "ui.hud.use";

        /// <summary>Use disc verb while no interaction is in range (Use rolls, or dives in the air).</summary>
        public const string HudRoll = "ui.hud.roll";

        public const string PauseHeading = "ui.pause.heading";
        public const string PauseResume = "ui.pause.resume";
        public const string PauseBackToTitle = "ui.pause.back_to_title";

        /// <summary>All keys referenced from code, for validation.</summary>
        public static readonly string[] All =
        {
            TitleGameName, TitleCaseName, TitleNewGame, TitleContinue, TitleLanguageToggle, TitleBuildLabel, TitleContinueDetail,
            PromptMoveTouch, PromptMoveKeyboard, PromptMoveGamepad,
            HudPause, HudJump, HudUse, HudRoll,
            PauseHeading, PauseResume, PauseBackToTitle,
        };
    }
}
