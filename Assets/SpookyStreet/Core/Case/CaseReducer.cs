using System;
using System.Collections.Generic;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;

namespace SpookyStreet.Core.Case
{
    /// <summary>
    /// The small explicit reducer of case 01 (spec §7). Rules (spec §3):
    /// <list type="bullet">
    /// <item>No action is gated on conversations: the world accepts correct actions early.</item>
    /// <item>Gaining a fact also gains what it physically implies (see <see cref="FactDefinition.Implies"/>).</item>
    /// <item>Entering by the window before either witness speaks also records <see cref="CaseFact.AtticFirst"/>,
    /// which June acknowledges.</item>
    /// <item>The ending needs frontOpen and testedOwl plus arriving at the porch; nothing else.</item>
    /// <item>Turning the crank before the arm is linked is a tap and a gap: no fact.</item>
    /// <item>Facts are a set: repeating any action gains nothing and returns the same state instance.</item>
    /// </list>
    /// </summary>
    public static class CaseReducer
    {
        private static readonly IReadOnlyList<ScriptLine> NoLines = Array.Empty<ScriptLine>();

        public static CaseStep Apply(CaseState state, CaseAction action)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var gained = new List<CaseFact>(3);
            var lines = new List<ScriptLine>(3);
            var next = state;
            var special = CaseOutcome.None;
            bool cutaway = false;

            switch (action.Kind)
            {
                case CaseActionKind.HearKnocks:
                    next = Gain(next, CaseFact.HeardKnocks, gained);
                    if (gained.Count > 0)
                    {
                        lines.Add(Caption(Keys.CaptionArrivalKnocks));
                    }

                    break;

                case CaseActionKind.Talk:
                    var conversation = CaseScript.Conversation(action.Speaker, state);
                    if (conversation.Count == 0)
                    {
                        special = CaseOutcome.Absent;
                        break;
                    }

                    lines.AddRange(conversation);
                    if (TalkFact(action.Speaker, out var talkFact))
                    {
                        next = Gain(next, talkFact, gained);
                    }

                    break;

                case CaseActionKind.SitOnBench:
                    next = Gain(next, CaseFact.CheckedView, gained);
                    break;

                case CaseActionKind.ExamineStreamer:
                    next = Gain(next, CaseFact.ExaminedStreamer, gained);
                    lines.Add(Pip(Keys.ExamineStreamer));
                    break;

                case CaseActionKind.ExamineCloth:
                    next = Gain(next, CaseFact.ExaminedCloth, gained);
                    lines.Add(Pip(Keys.ExamineCloth));
                    break;

                case CaseActionKind.EnterAttic:
                    next = Gain(next, CaseFact.EnteredByWindow, gained);
                    break;

                case CaseActionKind.ExamineSill:
                    next = Gain(next, CaseFact.ExaminedSill, gained);
                    lines.Add(Pip(Keys.ExamineSill));
                    break;

                case CaseActionKind.TryFrontDoor:
                    if (!state.Has(CaseFact.FrontOpen))
                    {
                        special = CaseOutcome.DoorStuck;
                        lines.Add(Pip(Keys.ExamineDoorStuck));
                    }

                    break;

                case CaseActionKind.LiftLatch:
                    next = Gain(next, CaseFact.FrontOpen, gained);
                    if (gained.Contains(CaseFact.FrontOpen) && state.Has(CaseFact.FoundJune))
                    {
                        lines.Add(CaseScript.Line(Speaker.June, Keys.JuneLatchThanks));
                    }

                    break;

                case CaseActionKind.EnterFoyer:
                    // June's mark is derived from foundJune (CaseWorldView), so entering only cues the hum.
                    if (!state.Has(CaseFact.SawCam))
                    {
                        lines.Add(Caption(Keys.CaptionFoyerHum));
                    }

                    break;

                case CaseActionKind.ExamineGrille:
                    next = Gain(next, CaseFact.SawCam, gained);
                    lines.Add(Caption(Keys.CaptionGrille));
                    break;

                case CaseActionKind.LinkArm:
                    next = Gain(next, CaseFact.LinkedArm, gained);
                    if (gained.Contains(CaseFact.LinkedArm))
                    {
                        lines.Add(Caption(Keys.CaptionArmLinked));
                    }

                    break;

                case CaseActionKind.TurnCrank:
                    if (!state.Has(CaseFact.LinkedArm))
                    {
                        special = CaseOutcome.CrankGap;
                        lines.Add(Caption(Keys.CaptionCrankGap));
                    }
                    else
                    {
                        next = Gain(next, CaseFact.TestedOwl, gained);
                        cutaway = gained.Count > 0;
                        special = cutaway ? CaseOutcome.None : CaseOutcome.ShowReplayed;
                        lines.Add(Caption(Keys.CaptionOwlBows));
                    }

                    break;

                case CaseActionKind.ArriveAtPorch:
                    if (state.Has(CaseFact.CaseClosed))
                    {
                        break;
                    }

                    if (!state.HasAll(CaseDefinition.EndingRequires))
                    {
                        special = CaseOutcome.NotYet;
                        break;
                    }

                    next = Gain(next, CaseFact.CaseClosed, gained);
                    lines.Add(CaseScript.Line(Speaker.June, Keys.JuneResolve));
                    lines.Add(CaseScript.Line(Speaker.Kit, Keys.KitFirstShow));
                    break;

                case CaseActionKind.ExamineCandyBowl:
                    if (!state.Has(CaseFact.CaseClosed))
                    {
                        lines.Add(Pip(Keys.ExamineBowl));
                        break;
                    }

                    next = Gain(next, CaseFact.FoundSweet, gained);
                    lines.Add(Pip(Keys.ExamineBowlSweet));
                    if (gained.Count > 0)
                    {
                        lines.Add(CaseScript.Line(Speaker.Vale, Keys.ValeSavedSweet));
                    }

                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action.Kind, "unknown case action");
            }

            // Route proof before any interview: June acknowledges the shortcut (spec §3).
            if (gained.Contains(CaseFact.EnteredByWindow) && !state.Has(CaseFact.BeaSpoke) && !state.Has(CaseFact.ValeSpoke))
            {
                next = Gain(next, CaseFact.AtticFirst, gained);
            }

            var outcome = gained.Count > 0 ? CaseOutcome.Progressed
                : special != CaseOutcome.None ? special
                : lines.Count > 0 ? CaseOutcome.Repeated
                : CaseOutcome.None;
            return new CaseStep(state, next, gained, outcome, lines.Count > 0 ? lines : NoLines, cutaway);
        }

        /// <summary>Which fact a conversation records (Aunt Kit's records none).</summary>
        public static bool TalkFact(Speaker speaker, out CaseFact fact)
        {
            switch (speaker)
            {
                case Speaker.Bea:
                    fact = CaseFact.BeaSpoke;
                    return true;
                case Speaker.Vale:
                    fact = CaseFact.ValeSpoke;
                    return true;
                case Speaker.June:
                    fact = CaseFact.FoundJune;
                    return true;
                default:
                    fact = default;
                    return false;
            }
        }

        private static CaseState Gain(CaseState state, CaseFact fact, List<CaseFact> gained) => state.With(fact, gained);

        private static ScriptLine Caption(string key) => CaseScript.Line(Speaker.Caption, key);

        private static ScriptLine Pip(string key) => CaseScript.Line(Speaker.Pip, key);
    }
}
