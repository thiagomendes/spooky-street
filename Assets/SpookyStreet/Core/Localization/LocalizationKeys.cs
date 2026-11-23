namespace SpookyStreet.Core.Localization
{
    /// <summary>
    /// Stable semantic keys used by code. Every constant here must exist in the bundled English table
    /// (enforced by tests). Never derive saved progress from these strings or their values.
    /// </summary>
    public static partial class LocalizationKeys
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

        /// <summary>Use disc verb while Pip is in the air and a dive is available.</summary>
        public const string HudDive = "ui.hud.dive";

        /// <summary>First-time moveset tips (each shown once ever, on a paper tag near the control).</summary>
        public const string TipHoldJump = "tip.move.hold_jump";
        public const string TipChainJump = "tip.move.chain_jump";
        public const string TipGlide = "tip.move.glide";
        public const string TipDive = "tip.move.dive";

        /// <summary>First wall slide (S003): Jump kicks off the wall.</summary>
        public const string TipWallJump = "tip.move.wall_jump";

        /// <summary>First ledge hang (S003): Jump climbs up.</summary>
        public const string TipLedgeClimb = "tip.move.ledge_climb";

        public const string PauseHeading = "ui.pause.heading";
        public const string PauseResume = "ui.pause.resume";
        public const string PauseBackToTitle = "ui.pause.back_to_title";

        private static readonly string[] Shell =
        {
            TitleGameName, TitleCaseName, TitleNewGame, TitleContinue, TitleLanguageToggle, TitleBuildLabel, TitleContinueDetail,
            PromptMoveTouch, PromptMoveKeyboard, PromptMoveGamepad,
            HudPause, HudJump, HudUse, HudRoll, HudDive,
            TipHoldJump, TipChainJump, TipGlide, TipDive, TipWallJump, TipLedgeClimb,
            PauseHeading, PauseResume, PauseBackToTitle,
        };

        /// <summary>All keys referenced from code, for validation.</summary>
        public static readonly string[] All = Concat(Shell, CaseLines.All, Notebook.All, Hints.All, Save.All, Verbs.All, Cutscenes.All,
            Recap.All, Keepsakes.All);

        /// <summary>Case 01 script: captions, examine text and dialogue (S002).</summary>
        public static class CaseLines
        {
            public const string CaptionArrivalKnocks = "c01.caption.arrival_knocks";
            public const string CaptionFoyerHum = "c01.caption.foyer_hum";
            public const string CaptionGrille = "c01.caption.grille";
            public const string CaptionArmLinked = "c01.caption.arm_linked";
            public const string CaptionCrankGap = "c01.caption.crank_gap";
            public const string CaptionOwlBows = "c01.caption.owl_bows";
            public const string ExamineStreamer = "c01.examine.streamer";
            public const string ExamineCloth = "c01.examine.cloth";
            public const string ExamineSill = "c01.examine.sill";
            public const string ExamineOwlPanel = "c01.examine.owl_panel";
            public const string ExamineOwlBooth = "c01.examine.owl_booth";
            public const string ExamineDoorStuck = "c01.examine.door_stuck";
            public const string ExamineBowl = "c01.examine.bowl";
            public const string ExamineBowlSweet = "c01.examine.bowl_sweet";
            public const string BeaSawCape = "c01.line.bea.saw_cape";
            public const string BeaRepeat = "c01.line.bea.repeat";
            public const string BeaDoorOpen = "c01.line.bea.door_open";
            public const string BeaClosed = "c01.line.bea.closed";
            public const string ValeDoorShut = "c01.line.vale.door_shut";
            public const string ValeInviteSit = "c01.line.vale.invite_sit";
            public const string ValeRepeat = "c01.line.vale.repeat";
            public const string ValeWasShut = "c01.line.vale.was_shut";
            public const string ValeOpenNow = "c01.line.vale.open_now";
            public const string ValeClosed = "c01.line.vale.closed";
            public const string ValeSavedSweet = "c01.line.vale.saved_sweet";
            public const string JuneGreet = "c01.line.june.greet";
            public const string JuneGreetShortcut = "c01.line.june.greet_shortcut";
            public const string JuneDoorSticks = "c01.line.june.door_sticks";
            public const string JuneDoorAlreadyOpen = "c01.line.june.door_already_open";
            public const string JuneLatchReminder = "c01.line.june.latch_reminder";
            public const string JuneLatchThanks = "c01.line.june.latch_thanks";
            public const string JuneListenFloor = "c01.line.june.listen_floor";
            public const string JuneGoOutside = "c01.line.june.go_outside";
            public const string JuneResolve = "c01.line.june.resolve";
            public const string JuneClosed = "c01.line.june.closed";
            public const string KitFirstShow = "c01.line.kit.first_show";
            public const string KitThanks = "c01.line.kit.thanks";
            public const string KitAsk = "c01.line.kit.ask";

            // The street remembers case 01 (S003a, D29): each friend's changed greeting on a later visit.
            public const string BeaRemembers = "c01.line.bea.remembers";
            public const string ValeRemembers = "c01.line.vale.remembers";
            public const string JuneRemembers = "c01.line.june.remembers";
            public const string KitRemembers = "c01.line.kit.remembers";

            public static readonly string[] All =
            {
                CaptionArrivalKnocks,
                CaptionFoyerHum,
                CaptionGrille,
                CaptionArmLinked,
                CaptionCrankGap,
                CaptionOwlBows,
                ExamineStreamer,
                ExamineCloth,
                ExamineSill,
                ExamineOwlPanel,
                ExamineOwlBooth,
                ExamineDoorStuck,
                ExamineBowl,
                ExamineBowlSweet,
                BeaSawCape,
                BeaRepeat,
                BeaDoorOpen,
                BeaClosed,
                ValeDoorShut,
                ValeInviteSit,
                ValeRepeat,
                ValeWasShut,
                ValeOpenNow,
                ValeClosed,
                ValeSavedSweet,
                JuneGreet,
                JuneGreetShortcut,
                JuneDoorSticks,
                JuneDoorAlreadyOpen,
                JuneLatchReminder,
                JuneLatchThanks,
                JuneListenFloor,
                JuneGoOutside,
                JuneResolve,
                JuneClosed,
                KitFirstShow,
                KitThanks,
                KitAsk,
                BeaRemembers,
                ValeRemembers,
                JuneRemembers,
                KitRemembers,
            };
        }

        /// <summary>Notebook screen and its case 01 content (S002).</summary>
        public static class Notebook
        {
            public const string Title = "ui.notebook.title";
            public const string Unknown = "ui.notebook.unknown";
            public const string CaseClosedStamp = "ui.notebook.case_closed_stamp";
            public const string FinalHeading = "ui.notebook.final_heading";
            public const string CluesCount = "ui.notebook.clues_count";
            public const string BackToStreet = "ui.notebook.back_to_street";
            public const string Replay = "ui.notebook.replay";
            public const string ReplayConfirm = "ui.notebook.replay_confirm";
            public const string ReplayConfirmYes = "ui.notebook.replay_confirm_yes";
            public const string ReplayConfirmNo = "ui.notebook.replay_confirm_no";
            public const string QuestionEntry = "c01.notebook.q_entry";
            public const string QuestionSource = "c01.notebook.q_source";
            public const string QuestionMoved = "c01.notebook.q_moved";
            public const string AnswerEntry = "c01.notebook.a_entry";
            public const string AnswerSource = "c01.notebook.a_source";
            public const string AnswerMoved = "c01.notebook.a_moved";
            public const string NoteBea = "c01.notebook.note_bea";
            public const string NoteVale = "c01.notebook.note_vale";
            public const string NoteBench = "c01.notebook.note_bench";
            public const string NoteStreamer = "c01.notebook.note_streamer";
            public const string NoteCloth = "c01.notebook.note_cloth";
            public const string NoteSill = "c01.notebook.note_sill";
            public const string NoteDoor = "c01.notebook.note_door";
            public const string NoteHum = "c01.notebook.note_hum";
            public const string NotePiece = "c01.notebook.note_piece";
            public const string NoteOwlPanel = "c01.notebook.note_owl_panel";
            public const string NoteOwlBooth = "c01.notebook.note_owl_booth";
            public const string NoteArm = "c01.notebook.note_arm";
            public const string NoteSweet = "c01.notebook.note_sweet";

            public static readonly string[] All =
            {
                Title,
                Unknown,
                CaseClosedStamp,
                FinalHeading,
                CluesCount,
                BackToStreet,
                Replay,
                ReplayConfirm,
                ReplayConfirmYes,
                ReplayConfirmNo,
                QuestionEntry,
                QuestionSource,
                QuestionMoved,
                AnswerEntry,
                AnswerSource,
                AnswerMoved,
                NoteBea,
                NoteVale,
                NoteBench,
                NoteStreamer,
                NoteCloth,
                NoteSill,
                NoteDoor,
                NoteHum,
                NotePiece,
                NoteOwlPanel,
                NoteOwlBooth,
                NoteArm,
                NoteSweet,
            };
        }

        /// <summary>Radio hints (S002, retiered by S002.5b, D26 §6): a sensory area line and an area trail line per
        /// objective, and one line per demonstrable move for tier 3 (the per-objective action lines are retired).</summary>
        public static class Hints
        {
            public const string HudHint = "ui.hud.hint";
            public const string ArrivalObserve = "c01.hint.arrival.observe";
            public const string ArrivalLocate = "c01.hint.arrival.locate";
            public const string CheckViewObserve = "c01.hint.check_view.observe";
            public const string CheckViewLocate = "c01.hint.check_view.locate";
            public const string FindWayInObserve = "c01.hint.find_way_in.observe";
            public const string FindWayInLocate = "c01.hint.find_way_in.locate";
            public const string MeetJuneObserve = "c01.hint.meet_june.observe";
            public const string MeetJuneLocate = "c01.hint.meet_june.locate";
            public const string OpenDoorObserve = "c01.hint.open_door.observe";
            public const string OpenDoorLocate = "c01.hint.open_door.locate";
            public const string FindSourceObserve = "c01.hint.find_source.observe";
            public const string FindSourceLocate = "c01.hint.find_source.locate";
            public const string LinkArmObserve = "c01.hint.link_arm.observe";
            public const string LinkArmLocate = "c01.hint.link_arm.locate";
            public const string TurnCrankObserve = "c01.hint.turn_crank.observe";
            public const string TurnCrankLocate = "c01.hint.turn_crank.locate";
            public const string ReturnObserve = "c01.hint.return_to_porch.observe";
            public const string ReturnLocate = "c01.hint.return_to_porch.locate";
            public const string FreeRoamObserve = "c01.hint.free_roam.observe";
            public const string FreeRoamAllFound = "c01.hint.free_roam.all_found";
            public const string FreeRoamLocate = "c01.hint.free_roam.locate";
            public const string MoveJump = "ui.hint.move.jump";
            public const string MoveHighJump = "ui.hint.move.high_jump";
            public const string MoveGlide = "ui.hint.move.glide";
            public const string MoveWallKick = "ui.hint.move.wall_kick";
            public const string MoveLedge = "ui.hint.move.ledge";

            public static readonly string[] All =
            {
                HudHint,
                ArrivalObserve,
                ArrivalLocate,
                CheckViewObserve,
                CheckViewLocate,
                FindWayInObserve,
                FindWayInLocate,
                MeetJuneObserve,
                MeetJuneLocate,
                OpenDoorObserve,
                OpenDoorLocate,
                FindSourceObserve,
                FindSourceLocate,
                LinkArmObserve,
                LinkArmLocate,
                TurnCrankObserve,
                TurnCrankLocate,
                ReturnObserve,
                ReturnLocate,
                FreeRoamObserve,
                FreeRoamAllFound,
                FreeRoamLocate,
                MoveJump,
                MoveHighJump,
                MoveGlide,
                MoveWallKick,
                MoveLedge,
            };
        }

        /// <summary>Save notices: newer schema, recovery, fresh start (S002).</summary>
        public static class Save
        {
            public const string NewerHeading = "ui.save.newer_heading";
            public const string NewerBody = "ui.save.newer_body";
            public const string Recovered = "ui.save.recovered";
            public const string UnreadableHeading = "ui.save.unreadable_heading";
            public const string UnreadableBody = "ui.save.unreadable_body";
            public const string StartFresh = "ui.save.start_fresh";
            public const string Back = "ui.save.back";

            public static readonly string[] All =
            {
                NewerHeading,
                NewerBody,
                Recovered,
                UnreadableHeading,
                UnreadableBody,
                StartFresh,
                Back,
            };
        }

        /// <summary>Interaction verbs on the Use button (spec §5).</summary>
        public static class Verbs
        {
            public const string Talk = "ui.verb.talk";
            public const string Look = "ui.verb.look";
            public const string Sit = "ui.verb.sit";
            public const string Turn = "ui.verb.turn";
            public const string Lift = "ui.verb.lift";
            public const string Connect = "ui.verb.connect";
            public const string Open = "ui.verb.open";

            public static readonly string[] All =
            {
                Talk,
                Look,
                Sit,
                Turn,
                Lift,
                Connect,
                Open,
            };
        }

        private static string[] Concat(params string[][] groups)
        {
            int count = 0;
            foreach (var group in groups)
            {
                count += group.Length;
            }

            var all = new string[count];
            int at = 0;
            foreach (var group in groups)
            {
                group.CopyTo(all, at);
                at += group.Length;
            }

            return all;
        }
    }
}
