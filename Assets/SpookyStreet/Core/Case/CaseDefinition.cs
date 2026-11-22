using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.Case
{
    /// <summary>
    /// Persistent facts of case 01. Code uses this enum; saves use <see cref="FactDefinition.StableId"/> only, so the
    /// enum may be reordered freely. Never key progress off display text.
    /// </summary>
    public enum CaseFact
    {
        HeardKnocks,
        BeaSpoke,
        ValeSpoke,
        CheckedView,
        ExaminedStreamer,
        ExaminedCloth,
        EnteredByWindow,
        AtticFirst,
        ExaminedSill,
        FoundJune,
        FrontOpen,
        SawCam,
        LinkedArm,
        TestedOwl,
        CaseClosed,
        FoundSweet,
    }

    /// <summary>The six beats of the case script (spec §3).</summary>
    public enum CaseBeat
    {
        Arrival,
        ConflictingViews,
        PhysicalEvidence,
        FindJune,
        FindSource,
        Resolve,
    }

    /// <summary>
    /// How a fact matters to the case. Required facts gate the ending; Route facts are on the main line but not
    /// gates; Optional facts only enrich the notebook and never block anything (spec §3 solvability rules).
    /// </summary>
    public enum FactRole
    {
        Required,
        Route,
        Optional,
    }

    /// <summary>One row of the fact table.</summary>
    public sealed class FactDefinition
    {
        internal FactDefinition(CaseFact fact, string stableId, string beatName, CaseBeat beat, FactRole role,
            params CaseFact[] implies)
        {
            Fact = fact;
            StableId = stableId;
            BeatName = beatName;
            Beat = beat;
            Role = role;
            Implies = implies ?? Array.Empty<CaseFact>();
        }

        public CaseFact Fact { get; }

        /// <summary>Saved ID, e.g. "c01.attic.entered". Never change a shipped ID; add a migration instead.</summary>
        public string StableId { get; }

        /// <summary>Name used in the spec's beat table (e.g. "enteredByWindow").</summary>
        public string BeatName { get; }

        public CaseBeat Beat { get; }

        public FactRole Role { get; }

        /// <summary>
        /// Facts that are physically implied (linking the arm means Pip saw the cam; lifting the inside latch means Pip
        /// came in by the window). Gaining this fact also gains these, so early correct actions are accepted.
        /// </summary>
        public IReadOnlyList<CaseFact> Implies { get; }
    }

    /// <summary>
    /// The ONE definition table of case 01 (spec §7: "Map the beat-table names to stable IDs in one definition").
    /// </summary>
    public static class CaseDefinition
    {
        public const string CaseId = "c01";

        public static readonly IReadOnlyList<FactDefinition> Facts = new[]
        {
            new FactDefinition(CaseFact.HeardKnocks, "c01.arrival.knocks_heard", "heardKnocks", CaseBeat.Arrival, FactRole.Route),
            new FactDefinition(CaseFact.BeaSpoke, "c01.plaza.bea_spoke", "beaSpoke", CaseBeat.ConflictingViews, FactRole.Optional),
            new FactDefinition(CaseFact.ValeSpoke, "c01.plaza.vale_spoke", "valeSpoke", CaseBeat.ConflictingViews, FactRole.Optional),
            new FactDefinition(CaseFact.CheckedView, "c01.bench.view_checked", "checkedView", CaseBeat.ConflictingViews, FactRole.Optional),
            new FactDefinition(CaseFact.ExaminedStreamer, "c01.bicycle.streamer_examined", "examinedStreamer", CaseBeat.ConflictingViews, FactRole.Optional),
            new FactDefinition(CaseFact.ExaminedCloth, "c01.treehouse.cloth_examined", "examinedCloth", CaseBeat.PhysicalEvidence, FactRole.Optional),
            new FactDefinition(CaseFact.EnteredByWindow, "c01.attic.entered", "enteredByWindow", CaseBeat.PhysicalEvidence, FactRole.Route),
            new FactDefinition(CaseFact.AtticFirst, "c01.attic.entered_first", "atticFirst", CaseBeat.PhysicalEvidence, FactRole.Optional),
            new FactDefinition(CaseFact.ExaminedSill, "c01.attic.sill_examined", "examinedSill", CaseBeat.PhysicalEvidence, FactRole.Optional),
            new FactDefinition(CaseFact.FoundJune, "c01.attic.june_found", "foundJune", CaseBeat.FindJune, FactRole.Route, CaseFact.EnteredByWindow),
            new FactDefinition(CaseFact.FrontOpen, "c01.foyer.front_open", "frontOpen", CaseBeat.FindJune, FactRole.Required, CaseFact.EnteredByWindow),
            new FactDefinition(CaseFact.SawCam, "c01.workshop.cam_seen", "sawCam", CaseBeat.FindSource, FactRole.Route),
            new FactDefinition(CaseFact.LinkedArm, "c01.workshop.linked", "linkedArm", CaseBeat.FindSource, FactRole.Route, CaseFact.SawCam),
            new FactDefinition(CaseFact.TestedOwl, "c01.workshop.owl_tested", "testedOwl", CaseBeat.FindSource, FactRole.Required, CaseFact.LinkedArm),
            new FactDefinition(CaseFact.CaseClosed, "c01.porch.case_closed", "caseClosed", CaseBeat.Resolve, FactRole.Route),
            new FactDefinition(CaseFact.FoundSweet, "c01.bench.sweet_found", "foundSweet", CaseBeat.Resolve, FactRole.Optional),
        };

        private static readonly Dictionary<string, CaseFact> ByStableId = BuildIndex();

        public static FactDefinition Get(CaseFact fact)
        {
            foreach (var definition in Facts)
            {
                if (definition.Fact == fact)
                {
                    return definition;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(fact), fact, "fact has no definition");
        }

        public static string StableId(CaseFact fact) => Get(fact).StableId;

        public static bool TryParse(string stableId, out CaseFact fact)
        {
            if (stableId != null && ByStableId.TryGetValue(stableId, out fact))
            {
                return true;
            }

            fact = default;
            return false;
        }

        /// <summary>The ending needs these plus arriving at the porch (spec §3).</summary>
        public static readonly IReadOnlyList<CaseFact> EndingRequires = new[] { CaseFact.FrontOpen, CaseFact.TestedOwl };

        private static Dictionary<string, CaseFact> BuildIndex()
        {
            var index = new Dictionary<string, CaseFact>(StringComparer.Ordinal);
            foreach (var definition in Facts)
            {
                index.Add(definition.StableId, definition.Fact);
            }

            return index;
        }
    }

    /// <summary>
    /// Stable safe-anchor IDs (spec §4, §7). Saves store these strings; scenes map them to marker transforms.
    /// </summary>
    public static class CaseAnchors
    {
        public const string ArrivalPorch = "c01.anchor.arrival_porch";
        public const string Plaza = "c01.anchor.plaza";
        public const string BicycleCorner = "c01.anchor.bicycle_corner";
        public const string Bench = "c01.anchor.bench";
        public const string SideYard = "c01.anchor.side_yard";
        public const string Treehouse = "c01.anchor.treehouse";
        public const string AtticSill = "c01.anchor.attic_sill";
        public const string Attic = "c01.anchor.attic";
        public const string Foyer = "c01.anchor.foyer";
        public const string Workshop = "c01.anchor.workshop";
        public const string BluePorch = "c01.anchor.blue_porch";

        /// <summary>Where a new or replayed case starts.</summary>
        public const string Start = ArrivalPorch;

        /// <summary>Fallback for unknown or impossible anchors (spec §7: "Unknown anchor → plaza").</summary>
        public const string Fallback = Plaza;

        public static readonly IReadOnlyList<string> All = new[]
        {
            ArrivalPorch, Plaza, BicycleCorner, Bench, SideYard, Treehouse, AtticSill, Attic, Foyer, Workshop, BluePorch,
        };

        public static bool IsKnown(string anchorId)
        {
            foreach (string id in All)
            {
                if (string.Equals(id, anchorId, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Anchors inside the house. Before the window entry they cannot be legitimately reached.</summary>
        public static bool IsInside(string anchorId) =>
            anchorId == Attic || anchorId == Foyer || anchorId == Workshop;

        /// <summary>
        /// Resolves a saved anchor: unknown IDs, and inside anchors without the window-entry fact, fall back to
        /// the plaza so a restore never places Pip somewhere the facts say Pip has not been.
        /// </summary>
        public static string Resolve(string anchorId, CaseState state)
        {
            if (!IsKnown(anchorId))
            {
                return Fallback;
            }

            if (IsInside(anchorId) && (state == null || !state.Has(CaseFact.EnteredByWindow)))
            {
                return Fallback;
            }

            return anchorId;
        }
    }
}
