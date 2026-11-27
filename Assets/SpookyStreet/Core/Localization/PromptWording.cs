using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;

namespace SpookyStreet.Core.Localization
{
    /// <summary>
    /// The prompt lint of the S004 accessibility pass (stream F2, D25): shared text never names one control scheme.
    /// "Tap", "drag", "WASD", "arrow keys", "left stick" or "shoulder buttons" (and their Portuguese forms) may only
    /// appear in the keys picked per scheme (<see cref="SchemeKeys"/>); every other line is read on touch, gamepad and
    /// keyboard alike, so it says "press" or names the on-screen button, and the glyph beside it shows the control.
    /// Sound captions ("[Tap. …]", "[Click!]") are sounds, not instructions, and are not checked.
    /// </summary>
    public static class PromptWording
    {
        /// <summary>The lines that exist once per scheme: only these may name a touch gesture, a key or a pad control.</summary>
        public static IReadOnlyCollection<string> SchemeKeys => SchemeKeySet;

        private static readonly HashSet<string> SchemeKeySet = new HashSet<string>(StringComparer.Ordinal)
        {
            LocalizationKeys.PromptMoveTouch,
            LocalizationKeys.PromptMoveKeyboard,
            LocalizationKeys.PromptMoveGamepad,
            LocalizationKeys.ControlPrompts.ParentGateHintGamepad,
            LocalizationKeys.ControlPrompts.ParentGateHintKeyboard,
            LocalizationKeys.ControlPrompts.DialogueContinueTouch,
            LocalizationKeys.ControlPrompts.DialogueContinueButton,
        };

        private static readonly Regex English = new Regex(
            @"\b(tap|drag|swipe|pinch|click|w\s?a\s?s\s?d|arrow keys?|keyboard|space ?bar|left stick|right stick|thumbstick|shoulder buttons?|bumpers?|triggers?|d-?pad|gamepad|controller)\b",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        private static readonly Regex Portuguese = new Regex(
            @"(\btoque (em|na|no|nas|nos|aqui|para|duas)\b|\btocar (em|na|no)\b|\barraste\b|\barrastar\b|\bdeslize\b|\bclique\b|\bw\s?a\s?s\s?d\b|\bsetas\b|\bteclado\b|\bteclas?\b|\balavanca\b|\banalógico\b|\bbotões de ombro\b|\bbotão de ombro\b|\bgatilhos?\b|\bcontrole\b)",
            RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

        /// <summary>
        /// Every shared line of <paramref name="table"/> that names one scheme, as "key: text". Empty when the table is
        /// sound. English and Portuguese are checked; other locales by the English list (names of keys stay English).
        /// </summary>
        public static IReadOnlyList<string> Violations(StringTable table)
        {
            var problems = new List<string>();
            if (table == null)
            {
                return problems;
            }

            bool portuguese = table.Locale != null && table.Locale.StartsWith("pt", StringComparison.OrdinalIgnoreCase);
            foreach (var entry in table.Entries)
            {
                if (SchemeKeySet.Contains(entry.Key))
                {
                    continue;
                }

                foreach (string text in entry.Texts)
                {
                    if (string.IsNullOrEmpty(text) || IsSoundCaption(text))
                    {
                        continue;
                    }

                    var match = (portuguese ? Portuguese : English).Match(text);
                    if (!match.Success && portuguese)
                    {
                        match = English.Match(text); // an English word left in a translation
                    }

                    if (match.Success)
                    {
                        problems.Add($"{entry.Key}: \"{text}\" names a control scheme ('{match.Value}')");
                    }
                }
            }

            return problems;
        }

        /// <summary>A caption of a sound, in square brackets ("[Tap. The striped arm swings in the air.]").</summary>
        public static bool IsSoundCaption(string text)
        {
            string t = text.Trim();
            return t.StartsWith("[", StringComparison.Ordinal) && t.EndsWith("]", StringComparison.Ordinal);
        }
    }
}
