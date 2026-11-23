using System;
using System.Collections.Generic;
using SpookyStreet.Core.Case;

namespace SpookyStreet.Core.Street
{
    /// <summary>
    /// One permanent change a solved case leaves on the street (spec v1.4 §7, world-and-cases §5: "the street
    /// remembers"). Only changes that reuse existing art: no memory needs a new model.
    /// </summary>
    public enum StreetMemory
    {
        /// <summary>Aunt Kit's puppet booth stays open and lit, with Kit at it.</summary>
        BoothOpen,

        /// <summary>The rooftop owl bows now and then (the wind keeps turning its cam).</summary>
        OwlBows,

        /// <summary>Bea's bicycle carries an owl sticker ("I'm putting an owl on my bike.").</summary>
        BeaOwlSticker,

        /// <summary>The owl house's front door opens from outside (it no longer sticks).</summary>
        FrontDoorOpen,

        /// <summary>June plays outside by her treehouse way instead of up in the attic.</summary>
        JuneOutside,
    }

    /// <summary>The stable IDs of the memories and which case leaves each one (<c>street.*</c>, world-and-cases §5).</summary>
    public static class StreetMemories
    {
        private static readonly (StreetMemory Memory, string Id, string CaseId)[] Table =
        {
            (StreetMemory.BoothOpen, "street.c01.booth_open", CaseDefinition.CaseId),
            (StreetMemory.OwlBows, "street.c01.owl_bows", CaseDefinition.CaseId),
            (StreetMemory.BeaOwlSticker, "street.c01.bea_owl_sticker", CaseDefinition.CaseId),
            (StreetMemory.FrontDoorOpen, "street.c01.front_door_open", CaseDefinition.CaseId),
            (StreetMemory.JuneOutside, "street.c01.june_outside", CaseDefinition.CaseId),
        };

        /// <summary>Every memory, in table order.</summary>
        public static IReadOnlyList<StreetMemory> All
        {
            get
            {
                var all = new List<StreetMemory>(Table.Length);
                foreach (var row in Table)
                {
                    all.Add(row.Memory);
                }

                return all;
            }
        }

        /// <summary>Cases that leave memories (only case 01 so far).</summary>
        public static bool IsKnownCase(string caseId) => ForCase(caseId).Count > 0;

        public static string StableId(StreetMemory memory)
        {
            foreach (var row in Table)
            {
                if (row.Memory == memory)
                {
                    return row.Id;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(memory), memory, "unknown street memory");
        }

        public static bool TryParse(string id, out StreetMemory memory)
        {
            foreach (var row in Table)
            {
                if (string.Equals(row.Id, id, StringComparison.Ordinal))
                {
                    memory = row.Memory;
                    return true;
                }
            }

            memory = default;
            return false;
        }

        /// <summary>The case whose solving leaves <paramref name="memory"/>.</summary>
        public static string CaseOf(StreetMemory memory)
        {
            foreach (var row in Table)
            {
                if (row.Memory == memory)
                {
                    return row.CaseId;
                }
            }

            throw new ArgumentOutOfRangeException(nameof(memory), memory, "unknown street memory");
        }

        /// <summary>What the street remembers once <paramref name="caseId"/> is solved (empty for an unknown case).</summary>
        public static IReadOnlyList<StreetMemory> ForCase(string caseId)
        {
            var list = new List<StreetMemory>();
            foreach (var row in Table)
            {
                if (string.Equals(row.CaseId, caseId, StringComparison.Ordinal))
                {
                    list.Add(row.Memory);
                }
            }

            return list;
        }
    }

    /// <summary>
    /// What the street remembers across cases: the solved cases and the memories they left, in the order they were
    /// gained. Immutable; it only ever grows, except for an explicit New game (D29).
    /// </summary>
    public sealed class StreetState : IEquatable<StreetState>
    {
        public static readonly StreetState Empty = new StreetState(Array.Empty<string>(), Array.Empty<StreetMemory>());

        private readonly string[] _cases;
        private readonly StreetMemory[] _memories;

        private StreetState(string[] cases, StreetMemory[] memories)
        {
            _cases = cases;
            _memories = memories;
        }

        /// <summary>Builds a state, dropping duplicates and unknown cases (order kept).</summary>
        public static StreetState From(IEnumerable<string> solvedCases, IEnumerable<StreetMemory> memories)
        {
            var cases = new List<string>();
            foreach (string id in solvedCases ?? Array.Empty<string>())
            {
                if (StreetMemories.IsKnownCase(id) && !cases.Contains(id))
                {
                    cases.Add(id);
                }
            }

            var list = new List<StreetMemory>();
            foreach (var memory in memories ?? Array.Empty<StreetMemory>())
            {
                if (!list.Contains(memory))
                {
                    list.Add(memory);
                }
            }

            return new StreetState(cases.ToArray(), list.ToArray());
        }

        public IReadOnlyList<string> SolvedCases => _cases;

        public IReadOnlyList<StreetMemory> Memories => _memories;

        public bool IsEmpty => _cases.Length == 0 && _memories.Length == 0;

        public bool HasSolved(string caseId) => Array.IndexOf(_cases, caseId) >= 0;

        public bool Remembers(StreetMemory memory) => Array.IndexOf(_memories, memory) >= 0;

        /// <summary>
        /// The street after <paramref name="caseId"/> is solved: the case and every memory it leaves are added once.
        /// Idempotent: remembering a case again returns an equal state.
        /// </summary>
        public StreetState Remember(string caseId)
        {
            if (!StreetMemories.IsKnownCase(caseId))
            {
                return this;
            }

            var cases = new List<string>(_cases);
            if (!cases.Contains(caseId))
            {
                cases.Add(caseId);
            }

            var memories = new List<StreetMemory>(_memories);
            foreach (var memory in StreetMemories.ForCase(caseId))
            {
                if (!memories.Contains(memory))
                {
                    memories.Add(memory);
                }
            }

            var next = new StreetState(cases.ToArray(), memories.ToArray());
            return next.Equals(this) ? this : next;
        }

        public bool Equals(StreetState other)
        {
            if (other == null || other._cases.Length != _cases.Length || other._memories.Length != _memories.Length)
            {
                return false;
            }

            for (int i = 0; i < _cases.Length; i++)
            {
                if (!string.Equals(_cases[i], other._cases[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            for (int i = 0; i < _memories.Length; i++)
            {
                if (_memories[i] != other._memories[i])
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj) => Equals(obj as StreetState);

        public override int GetHashCode() => _cases.Length * 31 + _memories.Length;

        public override string ToString() =>
            "StreetState[" + string.Join(", ", _cases) + " | " + string.Join(", ", _memories) + "]";
    }

    /// <summary>
    /// The rules of the street state (D29). The street learns a case only from its "Case closed" stamp; a confirmed
    /// "Replay case" never changes it (spec §7: replaying resets only case data); only a confirmed New game, which
    /// starts Pip over as the new kid, clears it.
    /// </summary>
    public static class StreetRules
    {
        /// <summary>A case counts as solved for the street once its notebook carries the stamp.</summary>
        public static bool Solved(CaseState caseState) => caseState != null && caseState.Has(CaseFact.CaseStamped);

        /// <summary>
        /// The street after observing <paramref name="caseState"/> of <paramref name="caseId"/>: remembers the case once
        /// it is stamped, otherwise unchanged. Pure and idempotent, so it doubles as the migration for saves that were
        /// stamped before the street file existed.
        /// </summary>
        public static StreetState Observe(StreetState street, string caseId, CaseState caseState)
        {
            street = street ?? StreetState.Empty;
            return Solved(caseState) ? street.Remember(caseId) : street;
        }

        /// <summary>Confirmed "Replay case": the street keeps every memory (only the case save resets).</summary>
        public static StreetState AfterReplay(StreetState street) => street ?? StreetState.Empty;

        /// <summary>Confirmed New game: Pip arrives as the new kid again, so the street forgets everything.</summary>
        public static StreetState AfterNewGame(StreetState street) => StreetState.Empty;
    }

    /// <summary>
    /// What the world shows of the street's memories with the current case save (D29). A case's memories show when the
    /// street remembers them and that case is not being replayed: while a replayed case is open again (its save not yet
    /// stamped), its memories rest, so the replay finds the street as it was that first evening (the door sticks, June
    /// is in the attic, the booth is shut, Bea's bike is plain), and they come back with the stamp. Memories of other
    /// cases always show.
    /// </summary>
    public readonly struct StreetView : IEquatable<StreetView>
    {
        private readonly bool[] _shown;

        private StreetView(bool[] shown, bool resting)
        {
            _shown = shown;
            Resting = resting;
        }

        public static StreetView None => new StreetView(new bool[Enum.GetValues(typeof(StreetMemory)).Length], false);

        /// <summary>The street remembers memories that the replay of the current case is holding back.</summary>
        public bool Resting { get; }

        public bool Shows(StreetMemory memory) => _shown != null && _shown[(int)memory];

        public bool BoothOpen => Shows(StreetMemory.BoothOpen);

        public bool OwlBows => Shows(StreetMemory.OwlBows);

        public bool BeaOwlSticker => Shows(StreetMemory.BeaOwlSticker);

        public bool FrontDoorOpen => Shows(StreetMemory.FrontDoorOpen);

        public bool JuneOutside => Shows(StreetMemory.JuneOutside);

        /// <summary>
        /// True when the world shows what <paramref name="caseId"/> left on the street (any of its memories): the street
        /// remembers that case and it is not open again for a replay. Its friends' changed greetings follow this.
        /// </summary>
        public bool Remembers(string caseId)
        {
            foreach (var memory in StreetMemories.ForCase(caseId))
            {
                if (Shows(memory))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>True when any memory shows.</summary>
        public bool Any
        {
            get
            {
                if (_shown == null)
                {
                    return false;
                }

                foreach (bool shown in _shown)
                {
                    if (shown)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public static StreetView From(StreetState street, string currentCaseId, CaseState current)
        {
            street = street ?? StreetState.Empty;
            var shown = new bool[Enum.GetValues(typeof(StreetMemory)).Length];
            bool replaying = !StreetRules.Solved(current);
            bool resting = false;
            foreach (var memory in street.Memories)
            {
                bool held = replaying && string.Equals(StreetMemories.CaseOf(memory), currentCaseId, StringComparison.Ordinal);
                shown[(int)memory] = !held;
                resting |= held;
            }

            return new StreetView(shown, resting);
        }

        /// <summary>
        /// The world view with the street's memories laid over the case's own (pure; applying twice changes nothing):
        /// the booth open, the door open, the owl bowing, June outside by her treehouse.
        /// </summary>
        public CaseWorldView Apply(CaseWorldView view) =>
            view.WithStreet(BoothOpen, FrontDoorOpen, OwlBows, JuneOutside);

        public bool Equals(StreetView other)
        {
            foreach (StreetMemory memory in Enum.GetValues(typeof(StreetMemory)))
            {
                if (Shows(memory) != other.Shows(memory))
                {
                    return false;
                }
            }

            return Resting == other.Resting;
        }

        public override bool Equals(object obj) => obj is StreetView other && Equals(other);

        public override int GetHashCode()
        {
            int hash = Resting ? 1 : 0;
            foreach (StreetMemory memory in Enum.GetValues(typeof(StreetMemory)))
            {
                hash = hash * 2 + (Shows(memory) ? 1 : 0);
            }

            return hash;
        }

        public override string ToString()
        {
            var shown = new List<string>();
            foreach (StreetMemory memory in Enum.GetValues(typeof(StreetMemory)))
            {
                if (Shows(memory))
                {
                    shown.Add(memory.ToString());
                }
            }

            return "StreetView[" + string.Join(", ", shown) + (Resting ? " (resting)" : "") + "]";
        }
    }
}
