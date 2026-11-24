using System;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Nonverbal;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpookyStreet.Controls
{
    /// <summary>
    /// One action map shared by keyboard/mouse and gamepad (spec §5), plus the on-screen touch controls.
    /// Gameplay actions are edge-triggered where it matters: a held Jump or Use never repeats.
    /// Bindings live in code so they are reviewable in diffs and identical on every platform.
    /// </summary>
    public sealed class GameInput : IDisposable
    {
        private readonly InputActionMap _gameplay;
        private readonly InputActionMap _system;

        public GameInput()
        {
            _gameplay = new InputActionMap("Gameplay");
            Move = _gameplay.AddAction("Move", InputActionType.Value, expectedControlLayout: "Vector2");
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/w").With("Down", "<Keyboard>/s")
                .With("Left", "<Keyboard>/a").With("Right", "<Keyboard>/d");
            Move.AddCompositeBinding("2DVector")
                .With("Up", "<Keyboard>/upArrow").With("Down", "<Keyboard>/downArrow")
                .With("Left", "<Keyboard>/leftArrow").With("Right", "<Keyboard>/rightArrow");
            Move.AddBinding("<Gamepad>/leftStick");

            LookPointer = _gameplay.AddAction("LookPointer", InputActionType.Value, "<Mouse>/delta",
                expectedControlLayout: "Vector2");
            LookStick = _gameplay.AddAction("LookStick", InputActionType.Value, "<Gamepad>/rightStick",
                expectedControlLayout: "Vector2");

            Jump = _gameplay.AddAction("Jump", InputActionType.Button, "<Keyboard>/space");
            Jump.AddBinding("<Gamepad>/buttonSouth");

            Use = _gameplay.AddAction("Use", InputActionType.Button, "<Keyboard>/e");
            Use.AddBinding("<Gamepad>/buttonWest");

            Recenter = _gameplay.AddAction("Recenter", InputActionType.Button, "<Keyboard>/r");
            Recenter.AddBinding("<Gamepad>/rightStickPress");

            // Pause lives in its own map so it keeps working while gameplay is suspended.
            _system = new InputActionMap("System");
            Pause = _system.AddAction("Pause", InputActionType.Button, "<Keyboard>/escape");
            Pause.AddBinding("<Gamepad>/start");
            _system.Enable();
        }

        public InputAction Move { get; }
        public InputAction LookPointer { get; }
        public InputAction LookStick { get; }
        public InputAction Jump { get; }
        public InputAction Use { get; }
        public InputAction Recenter { get; }
        public InputAction Pause { get; }

        /// <summary>On-screen controls; null until the HUD is built.</summary>
        public TouchControls Touch { get; set; }

        /// <summary>
        /// A stick pushed by a cutscene (S002.5b: Pip walks onto the porch through the motor, so the real walk and
        /// collisions play). Screen/camera space like <see cref="ReadMove"/>; zero outside cutscenes.
        /// </summary>
        public Vector2 ScriptedMove { get; set; }

        public bool GameplayEnabled => _gameplay.enabled;

        public void SetGameplayEnabled(bool enabled)
        {
            if (enabled)
            {
                _gameplay.Enable();
            }
            else
            {
                _gameplay.Disable();
                if (Touch != null)
                {
                    Touch.ResetAll();
                }
            }
        }

        /// <summary>Movement from all sources, magnitude ≤ 1, in screen/camera space (x right, y forward).</summary>
        public Vector2 ReadMove()
        {
            Vector2 value = _gameplay.enabled ? Move.ReadValue<Vector2>() : Vector2.zero;
            if (Touch != null)
            {
                value += Touch.StickValue;
            }

            value += ScriptedMove;
            return Vector2.ClampMagnitude(value, 1f);
        }

        public bool ConsumeJumpPressed()
        {
            bool pressed = _gameplay.enabled && Jump.WasPressedThisFrame();
            if (Touch != null && Touch.ConsumeJump())
            {
                pressed = true;
            }

            return pressed;
        }

        /// <summary>True while Jump is down on any device (variable jump height and the bat-cape glide).</summary>
        public bool ReadJumpHeld()
        {
            bool held = _gameplay.enabled && Jump.IsPressed();
            return held || (Touch != null && Touch.JumpHeld);
        }

        /// <summary>Use pressed this frame on any device (edge, never repeats while held).</summary>
        public bool ConsumeUsePressed()
        {
            bool pressed = _gameplay.enabled && Use.WasPressedThisFrame();
            if (Touch != null && Touch.ConsumeUse())
            {
                pressed = true;
            }

            return pressed;
        }

        public bool ConsumeRecenterPressed() => _gameplay.enabled && Recenter.WasPressedThisFrame();

        public bool PausePressedThisFrame() => Pause.WasPressedThisFrame();

        /// <summary>Mouse delta counts only while the right button is held, so the cursor stays usable.</summary>
        public Vector2 ReadMouseOrbitPixels()
        {
            if (!_gameplay.enabled || Mouse.current == null || !Mouse.current.rightButton.isPressed)
            {
                return Vector2.zero;
            }

            return LookPointer.ReadValue<Vector2>();
        }

        public Vector2 ReadStickOrbit() => _gameplay.enabled ? LookStick.ReadValue<Vector2>() : Vector2.zero;

        /// <summary>
        /// A fresh <see cref="UiNavigateActions"/> map (S004 F1): every screen that takes gamepad and keyboard owns one,
        /// enables it while it is open and disposes it with itself, so screens never fight over one map's state.
        /// </summary>
        public static UiNavigateActions CreateUiNavigate() => new UiNavigateActions();

        /// <summary>Which device family the first prompt should describe.</summary>
        public static PromptDevice DetectPromptDevice()
        {
            if (Touchscreen.current != null && Application.isMobilePlatform)
            {
                return PromptDevice.Touch;
            }

            if (Gamepad.current != null && Keyboard.current == null)
            {
                return PromptDevice.Gamepad;
            }

            return Keyboard.current != null ? PromptDevice.Keyboard : PromptDevice.Touch;
        }

        public void Dispose()
        {
            _gameplay.Dispose();
            _system.Dispose();
        }
    }

    /// <summary>
    /// The one UI navigation map (S004 F1, D25): move (d-pad, left stick, arrows, WASD), submit, back, page left and
    /// right, and rewatch. Every screen's gamepad and keyboard control goes through these actions, never through a
    /// device. The first binding of each action is the one <see cref="ControlGlyphs"/> draws (Enter, Escape, Q, R, V;
    /// gamepad South, East, the bumpers, North), so a prompt always names a key or button that works. The bindings
    /// are the notebook's from S002.5a, unchanged. Touch needs none of it: touch screens press their own buttons.
    /// </summary>
    public sealed class UiNavigateActions : IDisposable
    {
        public const string MapName = "UiNavigate";

        private readonly InputActionMap _map;

        internal UiNavigateActions()
        {
            _map = new InputActionMap(MapName);
            Left = Button("Left", "<Keyboard>/leftArrow", ControlGlyphs.KeyLeft, "<Gamepad>/dpad/left", "<Gamepad>/leftStick/left");
            Right = Button("Right", "<Keyboard>/rightArrow", ControlGlyphs.KeyRight, "<Gamepad>/dpad/right", "<Gamepad>/leftStick/right");
            Up = Button("Up", "<Keyboard>/upArrow", ControlGlyphs.KeyUp, "<Gamepad>/dpad/up", "<Gamepad>/leftStick/up");
            Down = Button("Down", "<Keyboard>/downArrow", ControlGlyphs.KeyDown, "<Gamepad>/dpad/down", "<Gamepad>/leftStick/down");
            Submit = Button("Submit", ControlGlyphs.KeySubmit, "<Keyboard>/space", "<Keyboard>/e", ControlGlyphs.PadSubmit);
            Back = Button("Back", ControlGlyphs.KeyBack, "<Keyboard>/backspace", ControlGlyphs.PadBack);
            PageLeft = Button("PageLeft", ControlGlyphs.KeyPageLeft, "<Keyboard>/pageUp", ControlGlyphs.PadPageLeft);
            PageRight = Button("PageRight", ControlGlyphs.KeyPageRight, "<Keyboard>/pageDown", ControlGlyphs.PadPageRight);
            Rewatch = Button("Rewatch", ControlGlyphs.KeyRewatch, ControlGlyphs.PadRewatch);
            Inspect = Button("Inspect", ControlGlyphs.KeyInspect, "<Keyboard>/i", ControlGlyphs.PadInspect);
        }

        public InputAction Left { get; }
        public InputAction Right { get; }
        public InputAction Up { get; }
        public InputAction Down { get; }
        public InputAction Submit { get; }
        public InputAction Back { get; }
        public InputAction PageLeft { get; }
        public InputAction PageRight { get; }
        public InputAction Rewatch { get; }

        /// <summary>D32: open the focused sticker's memory card without picking it up (F, I, gamepad West).</summary>
        public InputAction Inspect { get; }

        public InputActionMap Map => _map;

        public bool Enabled => _map.enabled;

        public void Enable() => _map.Enable();

        public void Disable() => _map.Disable();

        /// <summary>The action that prompt names (Submit, Back, PageLeft, PageRight, Rewatch, Inspect); null for others.</summary>
        public InputAction For(ControlPrompt prompt)
        {
            switch (prompt)
            {
                case ControlPrompt.Submit:
                    return Submit;
                case ControlPrompt.Back:
                    return Back;
                case ControlPrompt.PageLeft:
                    return PageLeft;
                case ControlPrompt.PageRight:
                    return PageRight;
                case ControlPrompt.Rewatch:
                    return Rewatch;
                case ControlPrompt.Inspect:
                    return Inspect;
                default:
                    return null;
            }
        }

        /// <summary>
        /// The direction pressed this frame (edge, never repeats while held), left before right before up before
        /// down when several land together; null when none, or while the map is off.
        /// </summary>
        public UiDirection? DirectionPressed()
        {
            if (!_map.enabled)
            {
                return null;
            }

            if (Left.WasPressedThisFrame())
            {
                return UiDirection.Left;
            }

            if (Right.WasPressedThisFrame())
            {
                return UiDirection.Right;
            }

            if (Up.WasPressedThisFrame())
            {
                return UiDirection.Up;
            }

            if (Down.WasPressedThisFrame())
            {
                return UiDirection.Down;
            }

            return null;
        }

        /// <summary>Pressed this frame, and only while the map is on.</summary>
        public bool Pressed(InputAction action) => _map.enabled && action != null && action.WasPressedThisFrame();

        private InputAction Button(string name, params string[] bindings)
        {
            var action = _map.AddAction(name, InputActionType.Button, bindings[0]);
            for (int i = 1; i < bindings.Length; i++)
            {
                action.AddBinding(bindings[i]);
            }

            return action;
        }

        public void Dispose() => _map.Dispose();
    }

    public enum PromptDevice
    {
        Touch,
        Keyboard,
        Gamepad,
    }
}
