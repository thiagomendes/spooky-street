using System;
using SpookyStreet.Core.Nonverbal;
using UnityEngine;
using UnityEngine.InputSystem;

namespace SpookyStreet.Controls
{
    /// <summary>
    /// The control scheme the player is using now (D25: prompts and glyphs per scheme). It starts from
    /// <see cref="GameInput.DetectPromptDevice"/> and follows the device of the last performed Input System action
    /// (gameplay, dialogue, UI pointer and navigation actions alike), so a touch on the HUD, a gamepad button or a key
    /// switches every glyph. No device is polled directly. Tests pin a scheme with <see cref="Override"/>.
    /// </summary>
    public static class ControlSchemeTracker
    {
        private static bool s_hooked;
        private static ControlScheme s_current;
        private static ControlScheme? s_override;

        /// <summary>Raised on the main thread when <see cref="Current"/> changes.</summary>
        public static event Action<ControlScheme> Changed;

        public static ControlScheme Current
        {
            get
            {
                Hook();
                return s_override ?? s_current;
            }
        }

        /// <summary>Pins the scheme (tests, captures); null follows the devices again.</summary>
        public static ControlScheme? Override
        {
            get => s_override;
            set
            {
                var before = Current;
                s_override = value;
                Notify(before);
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetStatics()
        {
            if (s_hooked)
            {
                InputSystem.onActionChange -= OnActionChange;
            }

            s_hooked = false;
            s_override = null;
            Changed = null;
        }

        private static void Hook()
        {
            if (s_hooked)
            {
                return;
            }

            s_hooked = true;
            s_current = From(GameInput.DetectPromptDevice());
            InputSystem.onActionChange += OnActionChange;
        }

        public static ControlScheme From(PromptDevice device)
        {
            switch (device)
            {
                case PromptDevice.Gamepad:
                    return ControlScheme.Gamepad;
                case PromptDevice.Keyboard:
                    return ControlScheme.KeyboardMouse;
                default:
                    return ControlScheme.Touch;
            }
        }

        /// <summary>The scheme a device belongs to; null for devices that do not change it.</summary>
        public static ControlScheme? Classify(InputDevice device)
        {
            switch (device)
            {
                case Touchscreen _:
                    return ControlScheme.Touch;
                case Gamepad _:
                    return ControlScheme.Gamepad;
                case Keyboard _:
                case Mouse _:
                    return ControlScheme.KeyboardMouse;
                default:
                    return null;
            }
        }

        private static void OnActionChange(object target, InputActionChange change)
        {
            if (change != InputActionChange.ActionPerformed || !(target is InputAction action))
            {
                return;
            }

            var control = action.activeControl;
            var scheme = control != null ? Classify(control.device) : null;
            if (!scheme.HasValue || scheme.Value == s_current)
            {
                return;
            }

            // Mouse motion alone (an idle mouse on a desk nudged while playing with a gamepad) does not switch.
            if (control.device is Mouse && !(control is UnityEngine.InputSystem.Controls.ButtonControl))
            {
                return;
            }

            var before = Current;
            s_current = scheme.Value;
            Notify(before);
        }

        private static void Notify(ControlScheme before)
        {
            var now = s_override ?? s_current;
            if (now != before)
            {
                Changed?.Invoke(now);
            }
        }
    }
}
