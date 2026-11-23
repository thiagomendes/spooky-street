using System;
using System.Collections.Generic;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.Hints;

namespace SpookyStreet.Core.Case
{
    /// <summary>The player's current goal, derived from facts only.</summary>
    public enum CaseObjective
    {
        Arrival,
        CheckView,
        FindWayIn,
        MeetJune,
        OpenDoor,
        FindSource,
        LinkArm,
        TurnCrank,
        ReturnToPorch,
        FreeRoam,
    }

    /// <summary>Spec §5: first observation, then precise location, then direct action.</summary>
    public enum HintTier
    {
        Observation = 0,
        Location = 1,
        Action = 2,
    }

    /// <summary>
    /// Radio hints (spec §5). <see cref="ObjectiveFor"/> picks the current goal; <see cref="HintFor"/> gives the line
    /// for a tier. Neither changes the case: hints are never facts and are not saved.
    /// </summary>
    public static class CaseHints
    {
        private static readonly Dictionary<CaseObjective, string[]> Table = new Dictionary<CaseObjective, string[]>
        {
            { CaseObjective.Arrival, new[] { Keys.ArrivalObserve, Keys.ArrivalLocate, Keys.ArrivalAct } },
            { CaseObjective.CheckView, new[] { Keys.CheckViewObserve, Keys.CheckViewLocate, Keys.CheckViewAct } },
            { CaseObjective.FindWayIn, new[] { Keys.FindWayInObserve, Keys.FindWayInLocate, Keys.FindWayInAct } },
            { CaseObjective.MeetJune, new[] { Keys.MeetJuneObserve, Keys.MeetJuneLocate, Keys.MeetJuneAct } },
            { CaseObjective.OpenDoor, new[] { Keys.OpenDoorObserve, Keys.OpenDoorLocate, Keys.OpenDoorAct } },
            { CaseObjective.FindSource, new[] { Keys.FindSourceObserve, Keys.FindSourceLocate, Keys.FindSourceAct } },
            { CaseObjective.LinkArm, new[] { Keys.LinkArmObserve, Keys.LinkArmLocate, Keys.LinkArmAct } },
            { CaseObjective.TurnCrank, new[] { Keys.TurnCrankObserve, Keys.TurnCrankLocate, Keys.TurnCrankAct } },
            { CaseObjective.ReturnToPorch, new[] { Keys.ReturnObserve, Keys.ReturnLocate, Keys.ReturnAct } },
            { CaseObjective.FreeRoam, new[] { Keys.FreeRoamObserve, Keys.FreeRoamLocate, Keys.FreeRoamAct } },
        };

        /// <summary>
        /// The first unmet step of the canonical route, skipping anything already done in another order. Optional
        /// steps (bench) only lead while the route proof is missing, so they never block the hints either.
        /// </summary>
        public static CaseObjective ObjectiveFor(CaseState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            if (state.Has(CaseFact.CaseClosed))
            {
                return CaseObjective.FreeRoam;
            }

            if (!state.Has(CaseFact.EnteredByWindow))
            {
                return !state.Has(CaseFact.HeardKnocks) ? CaseObjective.Arrival
                    : !state.Has(CaseFact.CheckedView) ? CaseObjective.CheckView
                    : CaseObjective.FindWayIn;
            }

            if (!state.Has(CaseFact.FrontOpen))
            {
                return state.Has(CaseFact.FoundJune) ? CaseObjective.OpenDoor : CaseObjective.MeetJune;
            }

            if (!state.Has(CaseFact.LinkedArm))
            {
                return state.Has(CaseFact.SawCam) ? CaseObjective.LinkArm : CaseObjective.FindSource;
            }

            return state.Has(CaseFact.TestedOwl) ? CaseObjective.ReturnToPorch : CaseObjective.TurnCrank;
        }

        /// <summary>
        /// What the Continue detail and Kit ask about: the first open question the world has raised (D26: titles that
        /// narrated the route, "Find June's way in", "Fix what's loose", were retired). Null when nothing is open.
        /// </summary>
        public static string OpenQuestionKey(CaseState state) => CaseNotebook.FirstOpen(state)?.QuestionKey;

        public static string HintFor(CaseObjective objective, HintTier tier, CaseState state = null)
        {
            if (objective == CaseObjective.FreeRoam && tier == HintTier.Observation && state != null && CaseNotebook.AllFound(state))
            {
                return Keys.FreeRoamAllFound;
            }

            int index = (int)tier;
            if (index < 0 || index > 2)
            {
                throw new ArgumentOutOfRangeException(nameof(tier), tier, "unknown hint tier");
            }

            return Table[objective][index];
        }

        public static IEnumerable<string> AllKeys()
        {
            foreach (var row in Table.Values)
            {
                foreach (string key in row)
                {
                    yield return key;
                }
            }

            yield return Keys.FreeRoamAllFound;
        }
    }

    /// <summary>
    /// Tracks how far the player has escalated hints for the current objective. Each request gives the next tier,
    /// stopping at the action; a new objective starts again from the observation. In-memory only.
    /// </summary>
    public sealed class HintSession
    {
        private CaseObjective? _objective;
        private int _given;

        public string Request(CaseState state)
        {
            var objective = CaseHints.ObjectiveFor(state);
            if (_objective != objective)
            {
                _objective = objective;
                _given = 0;
            }

            var tier = (HintTier)Math.Min(_given, (int)HintTier.Action);
            _given++;
            return CaseHints.HintFor(objective, tier, state);
        }

        /// <summary>Tier the next request would give, for the current objective.</summary>
        public HintTier NextTier(CaseState state) =>
            _objective == CaseHints.ObjectiveFor(state) ? (HintTier)Math.Min(_given, (int)HintTier.Action) : HintTier.Observation;
    }
}
