using System;
using System.Collections.Generic;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Save;

namespace SpookyStreet.Core.Nonverbal
{
    /// <summary>
    /// The memory replays already offered in the current case save, kept in a tiny file of its own so the case save
    /// schema (<see cref="CaseSaveData"/>) does not change. It cannot be derived from facts alone: Bea's
    /// <c>door_open</c> and Vale's <c>was_shut</c> lines repeat in every conversation after the door opens without
    /// gaining a fact. To stay tied to "this save" without a shared save ID, the file records the case facts (in
    /// acquisition order) at its last write; facts only grow within a save, so if they are not a prefix of the
    /// current save's facts the file belongs to an earlier, replaced save and is cleared (see <see cref="Reconcile"/>).
    /// </summary>
    public sealed class NonverbalProgressData
    {
        public const int CurrentSchemaVersion = 1;

        public NonverbalProgressData(IEnumerable<string> seenReplays, IEnumerable<string> basisFacts)
        {
            Seen = new HashSet<string>(seenReplays ?? Array.Empty<string>(), StringComparer.Ordinal);
            BasisFacts = new List<string>(basisFacts ?? Array.Empty<string>());
        }

        public static NonverbalProgressData Empty => new NonverbalProgressData(null, null);

        /// <summary>Replay IDs offered in this save (<see cref="MemoryReplays"/>).</summary>
        public HashSet<string> Seen { get; }

        /// <summary>Case fact stable IDs, in acquisition order, at the last write.</summary>
        public List<string> BasisFacts { get; }

        /// <summary>True when the file belongs to the save whose state is <paramref name="state"/>.</summary>
        public bool BelongsTo(CaseState state)
        {
            var facts = state != null ? state.Facts : Array.Empty<CaseFact>();
            if (BasisFacts.Count > facts.Count)
            {
                return false;
            }

            for (int i = 0; i < BasisFacts.Count; i++)
            {
                if (!string.Equals(BasisFacts[i], CaseDefinition.StableId(facts[i]), StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The data to use with <paramref name="state"/>: this one, or empty if it is from another save.</summary>
        public static NonverbalProgressData Reconcile(NonverbalProgressData data, CaseState state) =>
            data != null && data.BelongsTo(state) ? data : Empty;

        public static List<string> FactIds(CaseState state)
        {
            var ids = new List<string>(state != null ? state.Count : 0);
            if (state != null)
            {
                foreach (var fact in state.Facts)
                {
                    ids.Add(CaseDefinition.StableId(fact));
                }
            }

            return ids;
        }

        public static string Serialize(NonverbalProgressData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            var seen = new List<string>(data.Seen);
            seen.Sort(StringComparer.Ordinal);
            return new SaveJsonWriter()
                .Number("schemaVersion", CurrentSchemaVersion)
                .String("caseId", CaseDefinition.CaseId)
                .Strings("seenReplays", seen)
                .Strings("basisFacts", data.BasisFacts)
                .ToString();
        }

        /// <summary>Reads the file; unknown replay IDs are dropped. False for a foreign, broken or newer file.</summary>
        public static bool TryRead(string json, out NonverbalProgressData data, out bool newerSchema)
        {
            data = Empty;
            newerSchema = false;
            var root = SaveJsonReader.ParseObject(json);
            if (root == null || !SaveJsonReader.TryInt(root, "schemaVersion", out int version) || version < 1)
            {
                return false;
            }

            if (version > CurrentSchemaVersion)
            {
                newerSchema = true;
                return false;
            }

            if (!SaveJsonReader.TryString(root, "caseId", out string caseId) || caseId != CaseDefinition.CaseId ||
                !SaveJsonReader.TryStrings(root, "seenReplays", out var seen) ||
                !SaveJsonReader.TryStrings(root, "basisFacts", out var basis))
            {
                return false;
            }

            var known = new List<string>(seen.Count);
            foreach (string id in seen)
            {
                if (MemoryReplays.IsKnown(id))
                {
                    known.Add(id);
                }
            }

            data = new NonverbalProgressData(known, basis);
            return true;
        }

        public static SaveCheck Check(string json)
        {
            if (TryRead(json, out _, out bool newer))
            {
                return SaveCheck.Valid;
            }

            return newer ? SaveCheck.Newer : SaveCheck.Invalid;
        }
    }

    /// <summary>
    /// The seen-replays file ("case-c01-nonverbal.json") with the same atomic write, previous copy and recovery as
    /// every save slot (<see cref="SaveSlotStore"/>). Calls must be serialized (main thread). Losing this file is
    /// harmless: at worst a key line's replay plays once more.
    /// </summary>
    public sealed class NonverbalProgressStore
    {
        private readonly SaveSlotStore _slot;
        private NonverbalProgressData _data = NonverbalProgressData.Empty;

        public NonverbalProgressStore(ISaveFileSystem fs, string directory)
        {
            _slot = new SaveSlotStore(fs, PathsFor(directory), NonverbalProgressData.Check);
        }

        public static SaveSlotPaths PathsFor(string directory) =>
            new SaveSlotPaths(directory, "case-" + CaseDefinition.CaseId + "-nonverbal");

        public SaveSlotPaths Paths => _slot.Paths;

        /// <summary>The seen replay IDs for the state last loaded or observed (pass to <see cref="MemoryReplayPolicy"/>).</summary>
        public IReadOnlyCollection<string> Seen => _data.Seen;

        /// <summary>Loads and reconciles against the case state just restored (after <c>CaseSaveStore.Load</c>).</summary>
        public IReadOnlyCollection<string> Load(CaseState state)
        {
            var load = _slot.Load();
            _data = load.HasContents && NonverbalProgressData.TryRead(load.Contents, out var read, out _)
                ? read
                : NonverbalProgressData.Empty;
            Observe(state);
            return Seen;
        }

        /// <summary>
        /// Call whenever the case state changes (facts gained, fresh start, replay). Clears and rewrites the file when
        /// it no longer belongs to the current save; otherwise does nothing (no write per fact).
        /// </summary>
        public void Observe(CaseState state)
        {
            if (_data.BelongsTo(state))
            {
                return;
            }

            bool hadAny = _data.Seen.Count > 0 || _data.BasisFacts.Count > 0;
            _data = NonverbalProgressData.Empty;
            if (hadAny)
            {
                Write();
            }
        }

        /// <summary>Records <paramref name="replayId"/> as offered in this save. Write before playing it.</summary>
        public void MarkSeen(string replayId, CaseState state)
        {
            if (!MemoryReplays.IsKnown(replayId))
            {
                throw new ArgumentException("unknown memory replay " + replayId, nameof(replayId));
            }

            Observe(state);
            _data = new NonverbalProgressData(_data.Seen, NonverbalProgressData.FactIds(state));
            _data.Seen.Add(replayId);
            Write();
        }

        /// <summary>Explicit reset with the case (fresh start, confirmed replay).</summary>
        public void Reset()
        {
            _data = NonverbalProgressData.Empty;
            Write();
        }

        private void Write()
        {
            if (_slot.WritesBlocked)
            {
                // A newer build's file holds nothing that is progress; keep it aside and continue.
                _slot.PreserveLatestAndUnblock();
            }

            _slot.Write(NonverbalProgressData.Serialize(_data));
        }
    }
}
