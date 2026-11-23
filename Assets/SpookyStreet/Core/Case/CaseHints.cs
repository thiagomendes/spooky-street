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
        // Tier 1 (a sensory line about the area) and tier 2 (the area trail line). Tier 3 has no per-objective line any
        // more (D26 §6): it only demonstrates a move, with that move's line (HintMoves.LineKey).
        private static readonly Dictionary<CaseObjective, string[]> Table = new Dictionary<CaseObjective, string[]>
        {
            { CaseObjective.Arrival, new[] { Keys.ArrivalObserve, Keys.ArrivalLocate } },
            { CaseObjective.CheckView, new[] { Keys.CheckViewObserve, Keys.CheckViewLocate } },
            { CaseObjective.FindWayIn, new[] { Keys.FindWayInObserve, Keys.FindWayInLocate } },
            { CaseObjective.MeetJune, new[] { Keys.MeetJuneObserve, Keys.MeetJuneLocate } },
            { CaseObjective.OpenDoor, new[] { Keys.OpenDoorObserve, Keys.OpenDoorLocate } },
            { CaseObjective.FindSource, new[] { Keys.FindSourceObserve, Keys.FindSourceLocate } },
            { CaseObjective.LinkArm, new[] { Keys.LinkArmObserve, Keys.LinkArmLocate } },
            { CaseObjective.TurnCrank, new[] { Keys.TurnCrankObserve, Keys.TurnCrankLocate } },
            { CaseObjective.ReturnToPorch, new[] { Keys.ReturnObserve, Keys.ReturnLocate } },
            { CaseObjective.FreeRoam, new[] { Keys.FreeRoamObserve, Keys.FreeRoamLocate } },
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

        /// <summary>
        /// The line for a tier. Tier 3 names a move (<paramref name="move"/>, D26 §6); without one there is no tier 3,
        /// so the area trail line stands (the radio stops at two pips where no move blocks the way).
        /// </summary>
        public static string HintFor(CaseObjective objective, HintTier tier, CaseState state = null, HintMove move = HintMove.None)
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

            if (tier == HintTier.Action)
            {
                return HintMoves.LineKey(move) ?? Table[objective][(int)HintTier.Location];
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
            foreach (var move in HintMoves.Demonstrable)
            {
                yield return HintMoves.LineKey(move);
            }
        }
    }

    /// <summary>
    /// Tracks how far the player has escalated hints for the current objective. Each request gives the next tier; a
    /// new objective starts again from the observation. Tier 3 exists only where a move blocks the way
    /// (<see cref="MoveAt"/>, D26 §6): elsewhere the radio stays on tier 2. In-memory only.
    /// </summary>
    public sealed class HintSession
    {
        private CaseObjective? _objective;
        private int _given;

        /// <summary>The move that blocks Pip where she stands now (None: no tier 3). Unset means no move anywhere.</summary>
        public Func<CaseObjective, HintMove> MoveAt { get; set; }

        public string Request(CaseState state)
        {
            var objective = CaseHints.ObjectiveFor(state);
            if (_objective != objective)
            {
                _objective = objective;
                _given = 0;
            }

            var move = Move(objective);
            var tier = Cap(_given, move);
            _given = Math.Min(_given + 1, (int)HintTier.Action);
            return CaseHints.HintFor(objective, tier, state, move);
        }

        /// <summary>Tier the next request would give, for the current objective and where Pip is now.</summary>
        public HintTier NextTier(CaseState state)
        {
            var objective = CaseHints.ObjectiveFor(state);
            return _objective == objective ? Cap(_given, Move(objective)) : HintTier.Observation;
        }

        /// <summary>The move a tier-3 request would demonstrate now (None when there is no tier 3 here).</summary>
        public HintMove Move(CaseObjective objective) => MoveAt != null ? MoveAt(objective) : HintMove.None;

        private static HintTier Cap(int given, HintMove move) =>
            (HintTier)Math.Min(given, move == HintMove.None ? (int)HintTier.Location : (int)HintTier.Action);
    }
}
