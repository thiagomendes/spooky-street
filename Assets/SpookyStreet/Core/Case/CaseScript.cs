using System;
using System.Collections.Generic;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;

namespace SpookyStreet.Core.Case
{
    /// <summary>
    /// When a line may play, checked against the case state BEFORE the step that plays it. Tests use these to prove
    /// every line picked in any order of play makes temporal sense (CASE-02).
    /// </summary>
    public sealed class LineCondition
    {
        internal LineCondition(CaseFact[] requires, CaseFact[] forbids)
        {
            Requires = requires ?? Array.Empty<CaseFact>();
            Forbids = forbids ?? Array.Empty<CaseFact>();
        }

        public IReadOnlyList<CaseFact> Requires { get; }

        public IReadOnlyList<CaseFact> Forbids { get; }

        public bool Holds(CaseState state)
        {
            foreach (var fact in Requires)
            {
                if (!state.Has(fact))
                {
                    return false;
                }
            }

            foreach (var fact in Forbids)
            {
                if (state.Has(fact))
                {
                    return false;
                }
            }

            return true;
        }
    }

    /// <summary>
    /// The authored English script of case 01 as localization keys (text lives in en.json), plus the pure function
    /// that picks what a character says for the current state.
    /// </summary>
    public static class CaseScript
    {
        private static readonly CaseFact[] None = Array.Empty<CaseFact>();

        private static readonly Dictionary<string, LineCondition> Conditions = new Dictionary<string, LineCondition>(StringComparer.Ordinal)
        {
            // Captions and examine text.
            { Keys.CaptionArrivalKnocks, When(None, F(CaseFact.HeardKnocks)) },
            { Keys.CaptionFoyerHum, When(None, F(CaseFact.FloorHum)) },
            { Keys.CaptionGrille, When(None, None) },
            { Keys.CaptionArmLinked, When(None, F(CaseFact.LinkedArm)) },
            { Keys.CaptionCrankGap, When(None, F(CaseFact.LinkedArm)) },
            { Keys.CaptionOwlBows, When(F(CaseFact.LinkedArm), None) },
            { Keys.ExamineStreamer, When(None, None) },
            { Keys.ExamineCloth, When(None, None) },
            { Keys.ExamineSill, When(None, None) },
            { Keys.ExamineOwlPanel, When(None, None) },
            { Keys.ExamineOwlBooth, When(None, None) },
            { Keys.ExamineDoorStuck, When(None, F(CaseFact.FrontOpen)) },
            { Keys.ExamineBowl, When(None, F(CaseFact.CaseClosed)) },
            { Keys.ExamineBowlSweet, When(F(CaseFact.CaseClosed), None) },

            // Bea: her account is always true; after the door opens she sets it in the past.
            { Keys.BeaSawCape, When(None, F(CaseFact.BeaSpoke, CaseFact.FrontOpen)) },
            { Keys.BeaRepeat, When(F(CaseFact.BeaSpoke), F(CaseFact.FrontOpen)) },
            { Keys.BeaDoorOpen, When(F(CaseFact.FrontOpen), F(CaseFact.CaseClosed)) },
            { Keys.BeaClosed, When(F(CaseFact.CaseClosed), None) },

            // Mr. Vale: "It stayed shut" only while it is; past tense once opened (spec §3).
            { Keys.ValeDoorShut, When(None, F(CaseFact.ValeSpoke, CaseFact.FrontOpen)) },
            { Keys.ValeInviteSit, When(None, F(CaseFact.CheckedView, CaseFact.FrontOpen)) },
            { Keys.ValeRepeat, When(F(CaseFact.ValeSpoke), F(CaseFact.FrontOpen)) },
            { Keys.ValeWasShut, When(F(CaseFact.FrontOpen), None) },
            { Keys.ValeOpenNow, When(F(CaseFact.FrontOpen), F(CaseFact.CaseClosed)) },
            { Keys.ValeClosed, When(F(CaseFact.CaseClosed), None) },
            { Keys.ValeSavedSweet, When(F(CaseFact.CaseClosed), F(CaseFact.FoundSweet)) },

            // June.
            { Keys.JuneGreet, When(None, F(CaseFact.FoundJune, CaseFact.AtticFirst, CaseFact.CaseClosed)) },
            { Keys.JuneGreetShortcut, When(F(CaseFact.AtticFirst), F(CaseFact.FoundJune, CaseFact.CaseClosed)) },
            { Keys.JuneDoorSticks, When(None, F(CaseFact.FoundJune, CaseFact.FrontOpen)) },
            { Keys.JuneDoorAlreadyOpen, When(F(CaseFact.FrontOpen), F(CaseFact.FoundJune, CaseFact.CaseClosed)) },
            { Keys.JuneLatchReminder, When(F(CaseFact.FoundJune), F(CaseFact.FrontOpen)) },
            { Keys.JuneLatchThanks, When(F(CaseFact.FoundJune), F(CaseFact.FrontOpen)) },
            { Keys.JuneListenFloor, When(F(CaseFact.FoundJune, CaseFact.FrontOpen), F(CaseFact.TestedOwl)) },
            { Keys.JuneGoOutside, When(F(CaseFact.TestedOwl), F(CaseFact.CaseClosed)) },
            { Keys.JuneResolve, When(F(CaseFact.FrontOpen, CaseFact.TestedOwl), F(CaseFact.CaseClosed)) },
            { Keys.JuneClosed, When(F(CaseFact.CaseClosed), None) },

            // Aunt Kit appears only for the reopening.
            { Keys.KitFirstShow, When(F(CaseFact.FrontOpen, CaseFact.TestedOwl), F(CaseFact.CaseClosed)) },
            // After the show, until the child has explained the case in the notebook (D26).
            { Keys.KitAsk, When(F(CaseFact.FrontOpen, CaseFact.TestedOwl), F(CaseFact.CaseStamped)) },
            { Keys.KitThanks, When(F(CaseFact.CaseStamped), None) },

            // The street remembers case 01 (S003a, D29): changed greetings on a later visit. Conversation never picks
            // them; StreetGreetings lays them over the closed lines only while the street shows case 01's memories.
            { Keys.BeaRemembers, When(F(CaseFact.CaseStamped), None) },
            { Keys.ValeRemembers, When(F(CaseFact.CaseStamped), None) },
            { Keys.JuneRemembers, When(F(CaseFact.CaseStamped), None) },
            { Keys.KitRemembers, When(F(CaseFact.CaseStamped), None) },
        };

        /// <summary>Every line key the case can play.</summary>
        public static IEnumerable<string> AllLineKeys => Conditions.Keys;

        /// <summary>When <paramref name="key"/> may play; null for keys that are not case lines.</summary>
        public static LineCondition ConditionFor(string key) =>
            key != null && Conditions.TryGetValue(key, out var condition) ? condition : null;

        /// <summary>
        /// What <paramref name="speaker"/> says when Pip talks to them in <paramref name="state"/>, in order.
        /// Empty when the character is not present (Aunt Kit before the reopening). Pure.
        /// </summary>
        public static IReadOnlyList<ScriptLine> Conversation(Speaker speaker, CaseState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var lines = new List<ScriptLine>(3);
            bool open = state.Has(CaseFact.FrontOpen);
            bool closed = state.Has(CaseFact.CaseClosed);
            switch (speaker)
            {
                case Speaker.Bea:
                    lines.Add(Line(Speaker.Bea, closed ? Keys.BeaClosed
                        : open ? Keys.BeaDoorOpen
                        : state.Has(CaseFact.BeaSpoke) ? Keys.BeaRepeat
                        : Keys.BeaSawCape));
                    break;

                case Speaker.Vale:
                    if (closed)
                    {
                        lines.Add(Line(Speaker.Vale, Keys.ValeClosed));
                    }
                    else if (open)
                    {
                        lines.Add(Line(Speaker.Vale, Keys.ValeWasShut));
                        lines.Add(Line(Speaker.Vale, Keys.ValeOpenNow));
                    }
                    else
                    {
                        lines.Add(Line(Speaker.Vale, state.Has(CaseFact.ValeSpoke) ? Keys.ValeRepeat : Keys.ValeDoorShut));
                        if (!state.Has(CaseFact.CheckedView))
                        {
                            lines.Add(Line(Speaker.Vale, Keys.ValeInviteSit));
                        }
                    }

                    break;

                case Speaker.June:
                    if (closed)
                    {
                        lines.Add(Line(Speaker.June, Keys.JuneClosed));
                    }
                    else if (!state.Has(CaseFact.FoundJune))
                    {
                        lines.Add(Line(Speaker.June, state.Has(CaseFact.AtticFirst) ? Keys.JuneGreetShortcut : Keys.JuneGreet));
                        lines.Add(Line(Speaker.June, open ? Keys.JuneDoorAlreadyOpen : Keys.JuneDoorSticks));
                        if (state.Has(CaseFact.TestedOwl))
                        {
                            lines.Add(Line(Speaker.June, Keys.JuneGoOutside));
                        }
                    }
                    else if (!open)
                    {
                        lines.Add(Line(Speaker.June, Keys.JuneLatchReminder));
                    }
                    else
                    {
                        lines.Add(Line(Speaker.June, state.Has(CaseFact.TestedOwl) ? Keys.JuneGoOutside : Keys.JuneListenFloor));
                    }

                    break;

                case Speaker.Kit:
                    if (closed)
                    {
                        lines.Add(Line(Speaker.Kit, state.Has(CaseFact.CaseStamped) ? Keys.KitThanks : Keys.KitAsk));
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(speaker), speaker, "not a conversation partner");
            }

            return lines;
        }

        internal static ScriptLine Line(Speaker speaker, string key) => new ScriptLine(speaker, key);

        private static LineCondition When(CaseFact[] requires, CaseFact[] forbids) => new LineCondition(requires, forbids);

        private static CaseFact[] F(params CaseFact[] facts) => facts;
    }
}
