using System;
using System.Collections.Generic;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Save;

namespace SpookyStreet.Core.Street
{
    /// <summary>
    /// JSON form of <see cref="StreetState"/> ("street.json", beside the case saves; spec v1.4 §7): schema version,
    /// the solved cases and the memories they left, both as stable IDs in the order they were gained. Unknown IDs are
    /// dropped (a newer build bumps the schema, which this build preserves instead of reading).
    /// </summary>
    public static class StreetCodec
    {
        public const int CurrentSchemaVersion = 1;

        public static string Serialize(StreetState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var memories = new List<string>(state.Memories.Count);
            foreach (var memory in state.Memories)
            {
                memories.Add(StreetMemories.StableId(memory));
            }

            return new SaveJsonWriter()
                .Number("schemaVersion", CurrentSchemaVersion)
                .Strings("solvedCases", state.SolvedCases)
                .Strings("memories", memories)
                .ToString();
        }

        /// <summary>False for a broken, foreign or newer file (<paramref name="newerSchema"/> tells the last apart).</summary>
        public static bool TryRead(string json, out StreetState state, out bool newerSchema)
        {
            state = StreetState.Empty;
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

            if (!SaveJsonReader.TryStrings(root, "solvedCases", out var cases) ||
                !SaveJsonReader.TryStrings(root, "memories", out var ids))
            {
                return false;
            }

            var memories = new List<StreetMemory>(ids.Count);
            foreach (string id in ids)
            {
                if (StreetMemories.TryParse(id, out var memory))
                {
                    memories.Add(memory);
                }
            }

            state = StreetState.From(cases, memories);
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
    /// The street file with the atomic write, previous copy and recovery of every save slot (<see cref="SaveSlotStore"/>).
    /// Calls must be serialized (main thread). Every solved case can rebuild its memories from its own stamped save
    /// (<see cref="Observe"/>), so a lost or broken file heals on the next load; a file from a newer build is kept
    /// untouched (no autosave over it) until an explicit New game sets it aside.
    /// </summary>
    public sealed class StreetStore
    {
        private readonly SaveSlotStore _slot;

        public StreetStore(ISaveFileSystem fs, string directory)
        {
            _slot = new SaveSlotStore(fs, PathsFor(directory), StreetCodec.Check);
        }

        public static SaveSlotPaths PathsFor(string directory) => new SaveSlotPaths(directory, "street");

        public SaveSlotPaths Paths => _slot.Paths;

        /// <summary>What the street remembers (as last loaded or written).</summary>
        public StreetState State { get; private set; } = StreetState.Empty;

        /// <summary>What the last <see cref="Load"/> found.</summary>
        public SaveLoadStatus LoadStatus { get; private set; } = SaveLoadStatus.Empty;

        /// <summary>True while a newer build's street file is being preserved (nothing is written over it).</summary>
        public bool WritesBlocked => _slot.WritesBlocked;

        /// <summary>Loads the street (empty when there is no readable file; a newer file stays on disk untouched).</summary>
        public StreetState Load()
        {
            var load = _slot.Load();
            LoadStatus = load.Status;
            State = load.HasContents && StreetCodec.TryRead(load.Contents, out var read, out _) ? read : StreetState.Empty;
            return State;
        }

        /// <summary>
        /// Records what <paramref name="caseState"/> of <paramref name="caseId"/> teaches the street (after the stamp; the
        /// migration for stamped saves without a street file too). Writes only when something was gained and writes are
        /// allowed. Returns true when the street changed.
        /// </summary>
        public bool Observe(string caseId, CaseState caseState)
        {
            var next = StreetRules.Observe(State, caseId, caseState);
            if (next.Equals(State))
            {
                return false;
            }

            State = next;
            if (!_slot.WritesBlocked)
            {
                _slot.Write(StreetCodec.Serialize(State));
            }

            return true;
        }

        /// <summary>Confirmed "Replay case": nothing to write, the street keeps its memories (D29).</summary>
        public StreetState Replay()
        {
            State = StreetRules.AfterReplay(State);
            return State;
        }

        /// <summary>Confirmed New game: the street forgets (a newer build's file is set aside first, never overwritten).</summary>
        public void Reset()
        {
            if (_slot.WritesBlocked)
            {
                _slot.PreserveLatestAndUnblock();
            }

            State = StreetRules.AfterNewGame(State);
            _slot.Write(StreetCodec.Serialize(State));
        }
    }
}
