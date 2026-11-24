using System;
using SpookyStreet.Core.Save;

namespace SpookyStreet.Core.Keepsakes
{
    /// <summary>
    /// JSON form of <see cref="KeepsakeState"/> ("keepsakes.json", beside <c>street.json</c> and the case saves; S004
    /// §2.1): schema version, the found bats, the worn pieces and the remarks already said, all as stable IDs in order.
    /// Pages and pieces earned are derived from the bats and never stored. Unknown IDs are dropped (a later layout can
    /// add bats under new IDs without a schema change); a newer schema is preserved instead of read.
    /// </summary>
    public static class KeepsakeCodec
    {
        public const int CurrentSchemaVersion = 1;

        public static string Serialize(KeepsakeState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            return new SaveJsonWriter()
                .Number("schemaVersion", CurrentSchemaVersion)
                .Strings("foundBats", state.FoundBats)
                .Strings("wornPieces", state.WornPieces)
                .Strings("saidRemarks", state.SaidRemarks)
                .ToString();
        }

        /// <summary>False for a broken, foreign or newer file (<paramref name="newerSchema"/> tells the last apart).</summary>
        public static bool TryRead(string json, out KeepsakeState state, out bool newerSchema)
        {
            state = KeepsakeState.Empty;
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

            if (!SaveJsonReader.TryStrings(root, "foundBats", out var bats))
            {
                return false;
            }

            // The two later lists are optional, so a file holding only bats still loads.
            SaveJsonReader.TryStrings(root, "wornPieces", out var worn);
            SaveJsonReader.TryStrings(root, "saidRemarks", out var said);
            state = KeepsakeState.From(bats, worn, said);
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
    /// The keepsakes file with the atomic write, previous copy and recovery of every save slot (<see cref="SaveSlotStore"/>,
    /// the <c>StreetStore</c> pattern). Calls must be serialized (main thread). Every change writes at once, so a found
    /// bat is never lost to a closed app. A file from a newer build is kept untouched (changes stay in memory) until an
    /// explicit New game sets it aside. The case save never touches this file: "Replay case" keeps every keepsake.
    /// </summary>
    public sealed class KeepsakeStore
    {
        private readonly SaveSlotStore _slot;

        public KeepsakeStore(ISaveFileSystem fs, string directory)
        {
            _slot = new SaveSlotStore(fs, PathsFor(directory), KeepsakeCodec.Check);
        }

        public static SaveSlotPaths PathsFor(string directory) => new SaveSlotPaths(directory, "keepsakes");

        public SaveSlotPaths Paths => _slot.Paths;

        public KeepsakeState State { get; private set; } = KeepsakeState.Empty;

        public SaveLoadStatus LoadStatus { get; private set; } = SaveLoadStatus.Empty;

        public bool WritesBlocked => _slot.WritesBlocked;

        public KeepsakeState Load()
        {
            var load = _slot.Load();
            LoadStatus = load.Status;
            State = load.HasContents && KeepsakeCodec.TryRead(load.Contents, out var read, out _) ? read : KeepsakeState.Empty;
            return State;
        }

        /// <summary>Finds a bat and writes when it is new (see <see cref="KeepsakeState.Find"/>).</summary>
        public BatFindResult Find(string batId)
        {
            var result = State.Find(batId);
            Commit(result.State);
            return result;
        }

        /// <summary>Puts an earned piece on or off; returns true when that changed anything.</summary>
        public bool Wear(string pieceId, bool on) => Commit(State.Wear(pieceId, on));

        /// <summary>Records a remark as said; returns true the first time.</summary>
        public bool Said(string remarkId) => Commit(State.Said(remarkId));

        /// <summary>Confirmed "Replay case": nothing to write, the keepsakes stay.</summary>
        public KeepsakeState Replay()
        {
            State = KeepsakeRules.AfterReplay(State);
            return State;
        }

        /// <summary>Confirmed New game (<see cref="KeepsakeRules.AfterNewGame"/>; a newer file is set aside, never overwritten).</summary>
        public void Reset()
        {
            var next = KeepsakeRules.AfterNewGame(State);
            if (next.Equals(State) && !_slot.WritesBlocked)
            {
                return;
            }

            if (_slot.WritesBlocked)
            {
                _slot.PreserveLatestAndUnblock();
            }

            State = next;
            _slot.Write(KeepsakeCodec.Serialize(State));
        }

        private bool Commit(KeepsakeState next)
        {
            if (next.Equals(State))
            {
                return false;
            }

            State = next;
            if (!_slot.WritesBlocked)
            {
                _slot.Write(KeepsakeCodec.Serialize(State));
            }

            return true;
        }
    }
}
