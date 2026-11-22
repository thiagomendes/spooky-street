namespace SpookyStreet.Core.Localization
{
    public static partial class LocalizationKeys
    {
        /// <summary>
        /// Lines that name a control and so differ per control scheme (D25, docs/portability.md): the parent gate's
        /// line (touch spots, both shoulder buttons, or Q and P) and the speech card's "continue" line. Picked by
        /// <c>ControlGlyphs</c>; <see cref="ControlPrompts.All"/> is checked against en.json by ControlGlyphsTests.
        /// </summary>
        public static class ControlPrompts
        {
            public const string ParentGateHintGamepad = "ui.parent_gate.hint_gamepad";
            public const string ParentGateHintKeyboard = "ui.parent_gate.hint_keyboard";

            /// <summary>Touch keeps the existing "Tap to continue" line.</summary>
            public const string DialogueContinueTouch = "ui.dialogue.continue";
            public const string DialogueContinueButton = "ui.dialogue.continue_button";

            public static readonly string[] All =
            {
                ParentGateHintGamepad,
                ParentGateHintKeyboard,
                DialogueContinueTouch,
                DialogueContinueButton,
            };
        }
    }
}
