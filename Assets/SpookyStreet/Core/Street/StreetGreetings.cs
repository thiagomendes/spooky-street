using System;
using System.Collections.Generic;
using SpookyStreet.Core.Case;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;

namespace SpookyStreet.Core.Street
{
    /// <summary>
    /// The street remembers in words (S003a; narrative bible pillar 5 and checklist 11, "a new greeting"): once the
    /// street remembers case 01, each friend greets Pip with a changed line instead of the line they said the night it
    /// was solved. Same rule as the other memories (D29, <see cref="StreetView"/>): the greetings follow the world view
    /// fixed on load, so they arrive on the next visit after the stamp; while case 01 is open again for a replay they
    /// rest, and the replay hears the old lines; a confirmed New game forgets them with the street. Pure.
    /// </summary>
    public static class StreetGreetings
    {
        private static readonly (Speaker Speaker, string ClosedKey, string GreetingKey, string CaseId)[] Table =
        {
            (Speaker.Bea, Keys.BeaClosed, Keys.BeaRemembers, CaseDefinition.CaseId),
            (Speaker.Vale, Keys.ValeClosed, Keys.ValeRemembers, CaseDefinition.CaseId),
            (Speaker.June, Keys.JuneClosed, Keys.JuneRemembers, CaseDefinition.CaseId),
            (Speaker.Kit, Keys.KitThanks, Keys.KitRemembers, CaseDefinition.CaseId),
        };

        /// <summary>Every greeting line key, in table order.</summary>
        public static IReadOnlyList<string> AllKeys
        {
            get
            {
                var keys = new List<string>(Table.Length);
                foreach (var row in Table)
                {
                    keys.Add(row.GreetingKey);
                }

                return keys;
            }
        }

        /// <summary>The line a greeting replaces (the speaker's line from the night the case was solved), or null.</summary>
        public static string Replaces(string greetingKey)
        {
            foreach (var row in Table)
            {
                if (string.Equals(row.GreetingKey, greetingKey, StringComparison.Ordinal))
                {
                    return row.ClosedKey;
                }
            }

            return null;
        }

        /// <summary>
        /// <paramref name="speaker"/>'s changed greeting with what the world shows of the street, or null when the street
        /// does not show that case (nothing remembered yet, or the case is being replayed).
        /// </summary>
        public static string For(Speaker speaker, StreetView view)
        {
            foreach (var row in Table)
            {
                if (row.Speaker == speaker && view.Remembers(row.CaseId))
                {
                    return row.GreetingKey;
                }
            }

            return null;
        }

        /// <summary>
        /// What <paramref name="speaker"/> says on a talk, given the case script's <paramref name="lines"/>: the line
        /// from the night the case was solved becomes the changed greeting while the street shows that case; every other
        /// line, and every line while the memories rest, stays as written. Returns <paramref name="lines"/> itself when
        /// nothing changes.
        /// </summary>
        public static IReadOnlyList<ScriptLine> Retell(Speaker speaker, IReadOnlyList<ScriptLine> lines, StreetView view)
        {
            if (lines == null || lines.Count == 0)
            {
                return lines;
            }

            string greeting = For(speaker, view);
            string closed = greeting != null ? Replaces(greeting) : null;
            if (closed == null)
            {
                return lines;
            }

            List<ScriptLine> told = null;
            for (int i = 0; i < lines.Count; i++)
            {
                var line = lines[i];
                if (line.Speaker == speaker && string.Equals(line.Key, closed, StringComparison.Ordinal))
                {
                    told ??= new List<ScriptLine>(lines);
                    told[i] = new ScriptLine(speaker, greeting);
                }
            }

            return told ?? lines;
        }
    }
}
