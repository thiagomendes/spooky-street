using System;
using System.Collections.Generic;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Save;

namespace SpookyStreet.Core.Cutscenes
{
    /// <summary>
    /// The cutscenes already seen in the current case save, in a tiny file of its own beside the replay seen set
    /// (<c>NonverbalProgressData</c> pattern), so the case save schema does not change (spec §7 save contract). It is
    /// tied to "this save" the same way: the file records the case facts (in acquisition order) at its last write;
    /// facts only grow within a save, so a file whose facts are not a prefix of the current save's belongs to an
    /// earlier, replaced save and is cleared.
    /// </summary>
    public sealed class CutsceneProgressData
    {
        public const int CurrentSchemaVersion = 1;

        public CutsceneProgressData(IEnumerable<string> seen, IEnumerable<string> basisFacts)
        {
            Seen = new HashSet<string>(seen ?? Array.Empty<string>(), StringComparer.Ordinal);
            BasisFacts = new List<string>(basisFacts ?? Array.Empty<string>());
        }

        public static CutsceneProgressData Empty => new CutsceneProgressData(null, null);

        /// <summary>Cutscene IDs seen in this save (<see cref="C01Cutscenes.Ids"/>).</summary>
        public HashSet<string> Seen { get; }

        /// <summary>Case fact stable IDs, in acquisition order, at the last write.</summary>
        public List<string> BasisFacts { get; }

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

        public static string Serialize(CutsceneProgressData data)
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
                .Strings("seenCutscenes", seen)
                .Strings("basisFacts", data.BasisFacts)
                .ToString();
        }

        /// <summary>Reads the file; unknown cutscene IDs are dropped. False for a foreign, broken or newer file.</summary>
        public static bool TryRead(string json, out CutsceneProgressData data, out bool newerSchema)
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
                !SaveJsonReader.TryStrings(root, "seenCutscenes", out var seen) ||
                !SaveJsonReader.TryStrings(root, "basisFacts", out var basis))
            {
                return false;
            }

            var known = new List<string>(seen.Count);
            foreach (string id in seen)
            {
                if (C01Cutscenes.IsKnown(id))
                {
                    known.Add(id);
                }
            }

            data = new CutsceneProgressData(known, basis);
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
    /// The seen-cutscenes file ("case-c01-cutscenes.json") with the atomic write, previous copy and recovery of every
    /// save slot (<see cref="SaveSlotStore"/>). Calls must be serialized (main thread). Losing it is harmless: at worst
    /// a cutscene plays once more.
    /// </summary>
    public sealed class CutsceneProgressStore
    {
        private readonly SaveSlotStore _slot;
        private CutsceneProgressData _data = CutsceneProgressData.Empty;

        public CutsceneProgressStore(ISaveFileSystem fs, string directory)
        {
            _slot = new SaveSlotStore(fs, PathsFor(directory), CutsceneProgressData.Check);
        }

        public static SaveSlotPaths PathsFor(string directory) =>
            new SaveSlotPaths(directory, "case-" + CaseDefinition.CaseId + "-cutscenes");

        public SaveSlotPaths Paths => _slot.Paths;

        public IReadOnlyCollection<string> Seen => _data.Seen;

        /// <summary>Loads and reconciles against the case state just restored.</summary>
        public IReadOnlyCollection<string> Load(CaseState state)
        {
            var load = _slot.Load();
            _data = load.HasContents && CutsceneProgressData.TryRead(load.Contents, out var read, out _)
                ? read
                : CutsceneProgressData.Empty;
            Observe(state);
            return Seen;
        }

        /// <summary>Call when the case state changes: clears and rewrites the file when it belongs to another save.</summary>
        public void Observe(CaseState state)
        {
            if (_data.BelongsTo(state))
            {
                return;
            }

            bool hadAny = _data.Seen.Count > 0 || _data.BasisFacts.Count > 0;
            _data = CutsceneProgressData.Empty;
            if (hadAny)
            {
                Write();
            }
        }

        /// <summary>Records <paramref name="id"/> as seen in this save. Write before playing it (a quit must not replay it).</summary>
        public void MarkSeen(string id, CaseState state)
        {
            if (!C01Cutscenes.IsKnown(id))
            {
                throw new ArgumentException("unknown cutscene " + id, nameof(id));
            }

            Observe(state);
            var facts = new List<string>(state != null ? state.Count : 0);
            if (state != null)
            {
                foreach (var fact in state.Facts)
                {
                    facts.Add(CaseDefinition.StableId(fact));
                }
            }

            _data = new CutsceneProgressData(_data.Seen, facts);
            _data.Seen.Add(id);
            Write();
        }

        /// <summary>Explicit reset with the case (new game, confirmed replay).</summary>
        public void Reset()
        {
            _data = CutsceneProgressData.Empty;
            Write();
        }

        private void Write()
        {
            if (_slot.WritesBlocked)
            {
                _slot.PreserveLatestAndUnblock();
            }

            _slot.Write(CutsceneProgressData.Serialize(_data));
        }
    }
}
