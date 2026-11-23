using System;
using NvKeys = SpookyStreet.Core.Localization.LocalizationKeys.NonverbalUi;
using PromptKeys = SpookyStreet.Core.Localization.LocalizationKeys.ControlPrompts;

namespace SpookyStreet.Core.Nonverbal
{
    /// <summary>The control family the player is using right now (D25: prompts and glyphs per control scheme).</summary>
    public enum ControlScheme
    {
        Touch,
        Gamepad,
        KeyboardMouse,
    }

    /// <summary>A control the UI shows a glyph for.</summary>
    public enum ControlPrompt
    {
        Use,
        Jump,
        HoldJump,
        Move,

        /// <summary>The speech bubble's next disc (advance a line).</summary>
        Next,

        /// <summary>The conversation's skip control.</summary>
        Skip,

        /// <summary>The parent gate's first and second spot.</summary>
        GateA,
        GateB,

        /// <summary>UI navigation (S004 F1): choose the focused element.</summary>
        Submit,

        /// <summary>UI navigation: leave the screen (or let go of what is held).</summary>
        Back,

        /// <summary>UI navigation: the previous and next page of a paged screen.</summary>
        PageLeft,
        PageRight,

        /// <summary>UI navigation: watch a cutscene again from the notebook.</summary>
        Rewatch,
    }

    /// <summary>How a glyph is drawn.</summary>
    public enum GlyphShape
    {
        /// <summary>Touch: the on-screen control itself (the ghost hand presses it; no extra glyph).</summary>
        TouchControl,

        /// <summary>Touch: the floating stick (the ghost hand drags it).</summary>
        TouchStick,

        /// <summary>A key cap with the key's name from the binding.</summary>
        Key,

        /// <summary>The four face buttons in a diamond, one of them lit (no platform letters or symbols).</summary>
        PadFace,

        /// <summary>A shoulder bumper, left or right.</summary>
        PadShoulder,

        /// <summary>The left stick, tilted toward the goal.</summary>
        PadStick,
    }

    /// <summary>Which face button a <see cref="GlyphShape.PadFace"/> glyph lights.</summary>
    public enum PadButton
    {
        None,
        South,
        East,
        West,
        North,
        LeftShoulder,
        RightShoulder,
    }

    /// <summary>One glyph: its shape, the Input System binding it names, and whether it is held.</summary>
    public readonly struct ControlGlyph
    {
        public ControlGlyph(GlyphShape shape, string bindingPath, PadButton pad = PadButton.None, bool hold = false)
        {
            Shape = shape;
            BindingPath = bindingPath;
            Pad = pad;
            Hold = hold;
        }

        public GlyphShape Shape { get; }

        /// <summary>The Input System control path (the key cap's label comes from it); null for touch.</summary>
        public string BindingPath { get; }

        public PadButton Pad { get; }

        /// <summary>Hold rather than press (a filling ring round the glyph).</summary>
        public bool Hold { get; }

        public bool IsTouch => Shape == GlyphShape.TouchControl || Shape == GlyphShape.TouchStick;

        public override string ToString() => $"{Shape} {BindingPath} {Pad}{(Hold ? " hold" : "")}";
    }

    /// <summary>
    /// The glyph for each prompt per control scheme (D25, docs/portability.md). Binding paths match
    /// <c>GameInput</c>, the dialogue's advance and skip actions and the parent gate's buttons; the runtime turns a
    /// key path into the key's display name, so no "tap", "WASD" or button letter is baked into any text. Gamepad
    /// glyphs are shapes (lit face button, bumper, stick), never one platform's letters or symbols.
    /// </summary>
    public static class ControlGlyphs
    {
        public const string KeyUse = "<Keyboard>/e";
        public const string KeyJump = "<Keyboard>/space";
        public const string KeyNext = "<Keyboard>/e";
        public const string KeySkip = "<Keyboard>/tab";
        public const string KeyGateA = "<Keyboard>/q";
        public const string KeyGateB = "<Keyboard>/p";
        public const string KeyUp = "<Keyboard>/w";
        public const string KeyDown = "<Keyboard>/s";
        public const string KeyLeft = "<Keyboard>/a";
        public const string KeyRight = "<Keyboard>/d";

        // UI navigation (S004 F1): the first binding of each GameInput UiNavigate action, so the glyph names a key that works.
        public const string KeySubmit = "<Keyboard>/enter";
        public const string KeyBack = "<Keyboard>/escape";
        public const string KeyPageLeft = "<Keyboard>/q";
        public const string KeyPageRight = "<Keyboard>/r";
        public const string KeyRewatch = "<Keyboard>/v";
        public const string PadUse = "<Gamepad>/buttonWest";
        public const string PadJump = "<Gamepad>/buttonSouth";
        public const string PadNext = "<Gamepad>/buttonSouth";
        public const string PadSkip = "<Gamepad>/buttonEast";
        public const string PadGateA = "<Gamepad>/leftShoulder";
        public const string PadGateB = "<Gamepad>/rightShoulder";
        public const string PadMove = "<Gamepad>/leftStick";
        public const string PadSubmit = "<Gamepad>/buttonSouth";
        public const string PadBack = "<Gamepad>/buttonEast";
        public const string PadPageLeft = "<Gamepad>/leftShoulder";
        public const string PadPageRight = "<Gamepad>/rightShoulder";
        public const string PadRewatch = "<Gamepad>/buttonNorth";

        public static ControlGlyph For(ControlScheme scheme, ControlPrompt prompt)
        {
            switch (scheme)
            {
                case ControlScheme.Gamepad:
                    return Gamepad(prompt);
                case ControlScheme.KeyboardMouse:
                    return Keyboard(prompt);
                default:
                    return prompt == ControlPrompt.Move
                        ? new ControlGlyph(GlyphShape.TouchStick, null)
                        : new ControlGlyph(GlyphShape.TouchControl, null, PadButton.None, IsHold(prompt));
            }
        }

        /// <summary>The key-move glyph for a screen-space direction (x right, y up/forward): the dominant axis's key.</summary>
        public static ControlGlyph MoveKey(float x, float y)
        {
            string path = Math.Abs(x) > Math.Abs(y) ? (x > 0f ? KeyRight : KeyLeft) : (y >= 0f ? KeyUp : KeyDown);
            return new ControlGlyph(GlyphShape.Key, path, PadButton.None, true);
        }

        /// <summary>The parent gate's line for the scheme: touch spots, both shoulder buttons, or Q and P.</summary>
        public static string ParentGateHintKey(ControlScheme scheme)
        {
            switch (scheme)
            {
                case ControlScheme.Gamepad:
                    return PromptKeys.ParentGateHintGamepad;
                case ControlScheme.KeyboardMouse:
                    return PromptKeys.ParentGateHintKeyboard;
                default:
                    return NvKeys.ParentGateHint;
            }
        }

        /// <summary>The v1 speech card's "continue" line: "tap" on touch, "press" with a button or key glyph otherwise.</summary>
        public static string ContinueKey(ControlScheme scheme) =>
            scheme == ControlScheme.Touch ? PromptKeys.DialogueContinueTouch : PromptKeys.DialogueContinueButton;

        private static bool IsHold(ControlPrompt prompt) =>
            prompt == ControlPrompt.HoldJump || prompt == ControlPrompt.GateA || prompt == ControlPrompt.GateB ||
            prompt == ControlPrompt.Move;

        private static ControlGlyph Gamepad(ControlPrompt prompt)
        {
            switch (prompt)
            {
                case ControlPrompt.Use:
                    return new ControlGlyph(GlyphShape.PadFace, PadUse, PadButton.West);
                case ControlPrompt.Jump:
                    return new ControlGlyph(GlyphShape.PadFace, PadJump, PadButton.South);
                case ControlPrompt.HoldJump:
                    return new ControlGlyph(GlyphShape.PadFace, PadJump, PadButton.South, true);
                case ControlPrompt.Move:
                    return new ControlGlyph(GlyphShape.PadStick, PadMove, PadButton.None, true);
                case ControlPrompt.Next:
                    return new ControlGlyph(GlyphShape.PadFace, PadNext, PadButton.South);
                case ControlPrompt.Skip:
                    return new ControlGlyph(GlyphShape.PadFace, PadSkip, PadButton.East);
                case ControlPrompt.GateA:
                    return new ControlGlyph(GlyphShape.PadShoulder, PadGateA, PadButton.LeftShoulder, true);
                case ControlPrompt.GateB:
                    return new ControlGlyph(GlyphShape.PadShoulder, PadGateB, PadButton.RightShoulder, true);
                case ControlPrompt.Submit:
                    return new ControlGlyph(GlyphShape.PadFace, PadSubmit, PadButton.South);
                case ControlPrompt.Back:
                    return new ControlGlyph(GlyphShape.PadFace, PadBack, PadButton.East);
                case ControlPrompt.PageLeft:
                    return new ControlGlyph(GlyphShape.PadShoulder, PadPageLeft, PadButton.LeftShoulder);
                case ControlPrompt.PageRight:
                    return new ControlGlyph(GlyphShape.PadShoulder, PadPageRight, PadButton.RightShoulder);
                case ControlPrompt.Rewatch:
                    return new ControlGlyph(GlyphShape.PadFace, PadRewatch, PadButton.North);
                default:
                    throw new ArgumentOutOfRangeException(nameof(prompt), prompt, "unknown prompt");
            }
        }

        private static ControlGlyph Keyboard(ControlPrompt prompt)
        {
            switch (prompt)
            {
                case ControlPrompt.Use:
                    return new ControlGlyph(GlyphShape.Key, KeyUse);
                case ControlPrompt.Jump:
                    return new ControlGlyph(GlyphShape.Key, KeyJump);
                case ControlPrompt.HoldJump:
                    return new ControlGlyph(GlyphShape.Key, KeyJump, PadButton.None, true);
                case ControlPrompt.Move:
                    return new ControlGlyph(GlyphShape.Key, KeyUp, PadButton.None, true);
                case ControlPrompt.Next:
                    return new ControlGlyph(GlyphShape.Key, KeyNext);
                case ControlPrompt.Skip:
                    return new ControlGlyph(GlyphShape.Key, KeySkip);
                case ControlPrompt.GateA:
                    return new ControlGlyph(GlyphShape.Key, KeyGateA, PadButton.None, true);
                case ControlPrompt.GateB:
                    return new ControlGlyph(GlyphShape.Key, KeyGateB, PadButton.None, true);
                case ControlPrompt.Submit:
                    return new ControlGlyph(GlyphShape.Key, KeySubmit);
                case ControlPrompt.Back:
                    return new ControlGlyph(GlyphShape.Key, KeyBack);
                case ControlPrompt.PageLeft:
                    return new ControlGlyph(GlyphShape.Key, KeyPageLeft);
                case ControlPrompt.PageRight:
                    return new ControlGlyph(GlyphShape.Key, KeyPageRight);
                case ControlPrompt.Rewatch:
                    return new ControlGlyph(GlyphShape.Key, KeyRewatch);
                default:
                    throw new ArgumentOutOfRangeException(nameof(prompt), prompt, "unknown prompt");
            }
        }
    }
}
