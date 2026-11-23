using System;
using System.Collections.Generic;
using SpookyStreet.Core.Case;

namespace SpookyStreet.Core.Save
{
    /// <summary>
    /// The case save contract (spec §7): schema version, case ID, acquired facts (stable IDs, in acquisition order),
    /// last safe anchor ID, discovered notebook entries and (schema 2, D26) the child's own right sticker sets per
    /// solved question. Wrong tries and half-filled slots are memory only. Preferences and language live in
    /// <see cref="PreferencesData"/>, in a separate file, so replaying the case never touches them.
    /// </summary>
    public sealed class CaseSaveData
    {
        /// <summary>2: the deduction notebook (S002.5a) adds solved facts and the "answers" field.</summary>
        public const int CurrentSchemaVersion = 2;

        public CaseSaveData(string caseId, IReadOnlyList<string> facts, string anchorId, IReadOnlyList<string> notebookEntries,
            IReadOnlyList<string> answers = null)
        {
            CaseId = caseId;
            Facts = facts ?? Array.Empty<string>();
            AnchorId = anchorId;
            NotebookEntries = notebookEntries ?? Array.Empty<string>();
            Answers = answers ?? Array.Empty<string>();
        }

        public int SchemaVersion => CurrentSchemaVersion;

        public string CaseId { get; }

        public IReadOnlyList<string> Facts { get; }

        public string AnchorId { get; }

        public IReadOnlyList<string> NotebookEntries { get; }

        /// <summary>The child's right sets, "questionId=sticker+sticker" (<see cref="CaseAnswers.ToSaveStrings"/>).</summary>
        public IReadOnlyList<string> Answers { get; }

        /// <summary>A new or replayed case: no facts, at the arrival porch.</summary>
        public static CaseSaveData Fresh() => FromState(CaseState.Empty, CaseAnchors.Start);

        public static CaseSaveData FromState(CaseState state, string anchorId) => FromState(state, anchorId, null);

        public static CaseSaveData FromState(CaseState state, string anchorId, CaseAnswers answers)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var facts = new List<string>(state.Count);
            foreach (var fact in state.Facts)
            {
                facts.Add(CaseDefinition.StableId(fact));
            }

            // Only right sets for solved questions are written; a solved question always has one.
            var checkedAnswers = CaseAnswers.FromSaveStrings(answers?.ToSaveStrings(), state, null);
            return new CaseSaveData(CaseDefinition.CaseId, facts, CaseAnchors.Resolve(anchorId, state),
                CaseNotebook.DiscoveredEntryIds(state), checkedAnswers.ToSaveStrings());
        }

        /// <summary>The validated answers for <see cref="ToState"/>.</summary>
        public CaseAnswers ToAnswers() => CaseAnswers.FromSaveStrings(Answers, ToState(), null);

        /// <summary>Rebuilds the case state; unknown IDs are ignored (they were reported when the save was read).</summary>
        public CaseState ToState()
        {
            var facts = new List<CaseFact>(Facts.Count);
            foreach (string id in Facts)
            {
                if (CaseDefinition.TryParse(id, out var fact))
                {
                    facts.Add(fact);
                }
            }

            return CaseState.FromFacts(facts);
        }
    }

    public enum CaseSaveStatus
    {
        /// <summary>Read, migrated if needed, and validated. <see cref="CaseSaveRead.Data"/> is usable.</summary>
        Ok,

        /// <summary>Written by a newer build. Explain to the player and preserve the file untouched.</summary>
        NewerSchema,

        /// <summary>Not a readable case save (truncated, garbage, wrong case, missing fields).</summary>
        Corrupt,
    }

    public sealed class CaseSaveRead
    {
        internal CaseSaveRead(CaseSaveStatus status, CaseSaveData data, int foundSchema, IReadOnlyList<string> issues)
        {
            Status = status;
            Data = data;
            FoundSchemaVersion = foundSchema;
            Issues = issues;
        }

        public CaseSaveStatus Status { get; }

        public CaseSaveData Data { get; }

        /// <summary>The schema version in the file (0 when it had none).</summary>
        public int FoundSchemaVersion { get; }

        /// <summary>Repairs made while validating (for logs): dropped IDs, anchor fallback, notebook rebuilt.</summary>
        public IReadOnlyList<string> Issues { get; }
    }

    /// <summary>JSON form of <see cref="CaseSaveData"/>, with migration and validation rules.</summary>
    public static class CaseSaveCodec
    {
        /// <summary>
        /// Migration steps keyed by the version they upgrade FROM (v → v + 1), each over the parsed JSON object.
        /// Schema 1 is the first shipped schema; 1 → 2 is the deduction notebook (<see cref="MigrateDeductions"/>).
        /// </summary>
        public static readonly Dictionary<int, Func<Dictionary<string, object>, Dictionary<string, object>>> Migrations =
            new Dictionary<int, Func<Dictionary<string, object>, Dictionary<string, object>>>
            {
                { 1, MigrateDeductions },
            };

        /// <summary>
        /// Schema 1 → 2 (S002.5a, D26). In schema 1 the notebook answered a question by physical proof, so a player
        /// who saw an answer keeps it: entering the attic solved "How did June get in?", seeing the cam "Where are the
        /// knocks from?", the owl's bow "What should the knocks move?", and a closed case keeps its stamp. Runs once
        /// (only schema-1 files reach it); the answers field starts empty and validation derives each solved
        /// question's comic from the stickers that save had found. New saves never derive anything.
        /// </summary>
        public static Dictionary<string, object> MigrateDeductions(Dictionary<string, object> root)
        {
            if (!SaveJsonReader.TryStrings(root, "facts", out var facts))
            {
                return root; // left for validation to reject as corrupt
            }

            var list = new List<object>(facts.Count + 4);
            foreach (string id in facts)
            {
                list.Add(id);
            }

            void Derive(CaseFact proof, CaseFact solved)
            {
                if (facts.Contains(CaseDefinition.StableId(proof)) && !facts.Contains(CaseDefinition.StableId(solved)))
                {
                    list.Add(CaseDefinition.StableId(solved));
                }
            }

            Derive(CaseFact.EnteredByWindow, CaseFact.SolvedEntry);
            Derive(CaseFact.FoundJune, CaseFact.SolvedEntry);
            Derive(CaseFact.FrontOpen, CaseFact.SolvedEntry);
            Derive(CaseFact.SawCam, CaseFact.SolvedSource);
            Derive(CaseFact.LinkedArm, CaseFact.SolvedSource);
            Derive(CaseFact.TestedOwl, CaseFact.SolvedSource);
            Derive(CaseFact.TestedOwl, CaseFact.SolvedMoved);
            Derive(CaseFact.CaseClosed, CaseFact.SolvedEntry);
            Derive(CaseFact.CaseClosed, CaseFact.SolvedSource);
            Derive(CaseFact.CaseClosed, CaseFact.SolvedMoved);
            Derive(CaseFact.CaseClosed, CaseFact.CaseStamped);
            root["facts"] = Distinct(list);
            root["answers"] = new List<object>();
            root["schemaVersion"] = 2.0;
            return root;
        }

        private static List<object> Distinct(List<object> list)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal);
            var result = new List<object>(list.Count);
            foreach (object o in list)
            {
                if (o is string s && seen.Add(s))
                {
                    result.Add(s);
                }
            }

            return result;
        }

        public static string Serialize(CaseSaveData data)
        {
            if (data == null)
            {
                throw new ArgumentNullException(nameof(data));
            }

            return new SaveJsonWriter()
                .Number("schemaVersion", data.SchemaVersion)
                .String("caseId", data.CaseId)
                .Strings("facts", data.Facts)
                .String("anchorId", data.AnchorId)
                .Strings("notebook", data.NotebookEntries)
                .Strings("answers", data.Answers)
                .ToString();
        }

        public static CaseSaveRead Read(string json)
        {
            var issues = new List<string>();
            var root = SaveJsonReader.ParseObject(json);
            if (root == null)
            {
                return Corrupt(0, "not a JSON object");
            }

            if (!SaveJsonReader.TryInt(root, "schemaVersion", out int version) || version < 0)
            {
                return Corrupt(0, "missing or invalid schemaVersion");
            }

            if (version > CaseSaveData.CurrentSchemaVersion)
            {
                return new CaseSaveRead(CaseSaveStatus.NewerSchema, null, version, issues);
            }

            for (int v = version; v < CaseSaveData.CurrentSchemaVersion; v++)
            {
                if (!Migrations.TryGetValue(v, out var step))
                {
                    return Corrupt(version, "no migration from schema " + v);
                }

                root = step(root);
                if (root == null)
                {
                    return Corrupt(version, "migration from schema " + v + " failed");
                }

                issues.Add("migrated schema " + v + " to " + (v + 1));
            }

            if (!SaveJsonReader.TryString(root, "caseId", out string caseId) || caseId != CaseDefinition.CaseId)
            {
                return Corrupt(version, "missing or foreign caseId");
            }

            if (!SaveJsonReader.TryStrings(root, "facts", out var rawFacts))
            {
                return Corrupt(version, "missing facts");
            }

            if (!SaveJsonReader.TryString(root, "anchorId", out string anchor))
            {
                anchor = null;
                issues.Add("missing anchorId");
            }

            if (!SaveJsonReader.TryStrings(root, "notebook", out var rawNotebook))
            {
                rawNotebook = new List<string>();
                issues.Add("missing notebook");
            }

            // Facts: drop unknown IDs and duplicates; FromFacts restores implied facts (e.g. linked ⇒ cam seen).
            var facts = new List<CaseFact>(rawFacts.Count);
            foreach (string id in rawFacts)
            {
                if (CaseDefinition.TryParse(id, out var fact))
                {
                    facts.Add(fact);
                }
                else
                {
                    issues.Add("dropped unknown fact " + id);
                }
            }

            var state = CaseState.FromFacts(facts);
            if (state.Count != rawFacts.Count)
            {
                issues.Add("fact set normalised");
            }

            // Anchor: unknown, or inside the house without the window entry → plaza.
            string resolved = CaseAnchors.Resolve(anchor, state);
            if (resolved != anchor)
            {
                issues.Add("anchor " + (anchor ?? "null") + " → " + resolved);
            }

            // Notebook: entries are written only by proof, so the facts decide; report any disagreement.
            var notebook = CaseNotebook.DiscoveredEntryIds(state);
            if (!SameSet(notebook, rawNotebook))
            {
                issues.Add("notebook rebuilt from facts");
            }

            // Answers (schema 2): only right sets of solved questions; a solved question without one gets a comic
            // from the stickers it had (a migrated save).
            if (!SaveJsonReader.TryStrings(root, "answers", out var rawAnswers))
            {
                rawAnswers = new List<string>();
                issues.Add("missing answers");
            }

            var answers = CaseAnswers.FromSaveStrings(rawAnswers, state, issues);
            var data = CaseSaveData.FromState(state, resolved, answers);
            return new CaseSaveRead(CaseSaveStatus.Ok, data, version, issues);
        }

        /// <summary>Classifier for <see cref="SaveSlotStore"/>.</summary>
        public static SaveCheck Check(string json)
        {
            switch (Read(json).Status)
            {
                case CaseSaveStatus.Ok:
                    return SaveCheck.Valid;
                case CaseSaveStatus.NewerSchema:
                    return SaveCheck.Newer;
                default:
                    return SaveCheck.Invalid;
            }
        }

        private static CaseSaveRead Corrupt(int version, string why) =>
            new CaseSaveRead(CaseSaveStatus.Corrupt, null, version, new[] { why });

        private static bool SameSet(IReadOnlyList<string> a, IReadOnlyList<string> b)
        {
            var set = new HashSet<string>(a, StringComparer.Ordinal);
            var other = new HashSet<string>(b, StringComparer.Ordinal);
            return set.SetEquals(other);
        }
    }
}
