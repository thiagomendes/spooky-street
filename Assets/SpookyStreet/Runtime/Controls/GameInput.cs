using System;
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

    public enum PromptDevice
    {
        Touch,
        Keyboard,
        Gamepad,
    }
}
