namespace SpookyStreet.Core.Localization
{
    public static partial class LocalizationKeys
    {
        /// <summary>
        /// S002 wiring: dialogue card, caption strip, notebook screen and title confirmation. Kept in its own file so
        /// parallel work on the shell keys does not collide; <see cref="CaseUi.All"/> is checked against en.json by
        /// its own test (CaseWiringTests).
        /// </summary>
        public static class CaseUi
        {
            public const string HudNotebook = "ui.hud.notebook";
            public const string DialogueContinue = "ui.dialogue.continue";
            public const string DialogueSkip = "ui.dialogue.skip";
            public const string SpeakerPip = "ui.speaker.pip";
            public const string SpeakerBea = "ui.speaker.bea";
            public const string SpeakerVale = "ui.speaker.vale";
            public const string SpeakerJune = "ui.speaker.june";
            public const string SpeakerKit = "ui.speaker.kit";
            public const string CaptionWithDirection = "ui.caption.with_direction";
            public const string DirectionAhead = "ui.caption.direction.ahead";
            public const string DirectionLeft = "ui.caption.direction.left";
            public const string DirectionRight = "ui.caption.direction.right";
            public const string DirectionBehind = "ui.caption.direction.behind";
            public const string NotebookClose = "ui.notebook.close";
            public const string NotebookFound = "ui.notebook.found_stamp";
            public const string NotebookUnknownMark = "ui.notebook.unknown_mark";
            public const string NotebookDiscoveryItem = "ui.notebook.discovery_item";
            public const string NewGameConfirm = "ui.title.new_game_confirm";
            public const string NewGameConfirmYes = "ui.title.new_game_confirm_yes";
            public const string NewGameConfirmNo = "ui.title.new_game_confirm_no";
            public const string TagKnocks = "c01.notebook.tag_knocks";
            public const string TagBea = "c01.notebook.tag_bea";
            public const string TagVale = "c01.notebook.tag_vale";
            public const string TagBench = "c01.notebook.tag_bench";
            public const string TagStreamer = "c01.notebook.tag_streamer";
            public const string TagCloth = "c01.notebook.tag_cloth";
            public const string TagSill = "c01.notebook.tag_sill";
            public const string TagJune = "c01.notebook.tag_june";
            public const string TagSweet = "c01.notebook.tag_sweet";

            public static readonly string[] All =
            {
                HudNotebook,
                DialogueContinue,
                DialogueSkip,
                SpeakerPip,
                SpeakerBea,
                SpeakerVale,
                SpeakerJune,
                SpeakerKit,
                CaptionWithDirection,
                DirectionAhead,
                DirectionLeft,
                DirectionRight,
                DirectionBehind,
                NotebookClose,
                NotebookFound,
                NotebookUnknownMark,
                NotebookDiscoveryItem,
                NewGameConfirm,
                NewGameConfirmYes,
                NewGameConfirmNo,
                TagKnocks,
                TagBea,
                TagVale,
                TagBench,
                TagStreamer,
                TagCloth,
                TagSill,
                TagJune,
                TagSweet,
            };
        }
    }
}
