using System;
using System.Collections.Generic;
using System.IO;
using SpookyStreet.Core.Case;

namespace SpookyStreet.Core.Save
{
    /// <summary>
    /// The file operations a save needs. The runtime implements it with System.IO (Runtime/Save); tests use an
    /// in-memory version that can crash between any two operations.
    /// </summary>
    public interface ISaveFileSystem
    {
        bool Exists(string path);

        /// <summary>Throws <see cref="IOException"/> when the file cannot be read.</summary>
        string ReadAllText(string path);

        /// <summary>Creates or truncates <paramref name="path"/>, writes UTF-8 and flushes it to the storage device.</summary>
        void WriteAllTextDurable(string path, string contents);

        /// <summary>
        /// Atomically makes <paramref name="source"/> the new <paramref name="destination"/> (which must exist).
        /// The old destination becomes <paramref name="backup"/>, replacing any backup; null discards it.
        /// </summary>
        void Replace(string source, string destination, string backup);

        /// <summary>Renames <paramref name="source"/> to <paramref name="destination"/>, which must not exist.</summary>
        void Move(string source, string destination);

        void Delete(string path);
    }

    /// <summary>File names of one save slot: the latest copy, one previous valid copy, the temp file and a kept file.</summary>
    public sealed class SaveSlotPaths
    {
        public SaveSlotPaths(string directory, string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                throw new ArgumentException("A slot needs a file name.", nameof(name));
            }

            Latest = Path.Combine(directory ?? string.Empty, name + ".json");
            Previous = Path.Combine(directory ?? string.Empty, name + ".prev.json");
            Temp = Path.Combine(directory ?? string.Empty, name + ".tmp");
            Kept = Path.Combine(directory ?? string.Empty, name + ".kept.json");
        }

        public string Latest { get; }

        public string Previous { get; }

        public string Temp { get; }

        /// <summary>Where a file this build cannot read (newer schema) is preserved before a fresh start.</summary>
        public string Kept { get; }

        /// <summary>The case save (spec §7).</summary>
        public static SaveSlotPaths ForCase(string directory) => new SaveSlotPaths(directory, "case-" + CaseDefinition.CaseId);

        /// <summary>Preferences and language, separate from case progress.</summary>
        public static SaveSlotPaths ForPreferences(string directory) => new SaveSlotPaths(directory, "preferences");
    }

    public enum SaveWriteStepKind
    {
        WriteFile,
        Move,
        Replace,
        Delete,
    }

    /// <summary>One file operation of a write plan.</summary>
    public readonly struct SaveWriteStep
    {
        private SaveWriteStep(SaveWriteStepKind kind, string path, string destination, string backup, string contents)
        {
            Kind = kind;
            Path = path;
            Destination = destination;
            Backup = backup;
            Contents = contents;
        }

        public SaveWriteStepKind Kind { get; }

        /// <summary>The file written or deleted, or the source of a move or replace.</summary>
        public string Path { get; }

        public string Destination { get; }

        public string Backup { get; }

        public string Contents { get; }

        public static SaveWriteStep Write(string path, string contents) => new SaveWriteStep(SaveWriteStepKind.WriteFile, path, null, null, contents);

        public static SaveWriteStep MoveTo(string source, string destination) => new SaveWriteStep(SaveWriteStepKind.Move, source, destination, null, null);

        public static SaveWriteStep ReplaceWith(string source, string destination, string backup) =>
            new SaveWriteStep(SaveWriteStepKind.Replace, source, destination, backup, null);

        public static SaveWriteStep DeleteFile(string path) => new SaveWriteStep(SaveWriteStepKind.Delete, path, null, null, null);

        public override string ToString() => Kind + " " + Path + (Destination != null ? " → " + Destination : "") +
                                             (Backup != null ? " (backup " + Backup + ")" : "");
    }

    /// <summary>
    /// Pure planner for "write temp, then atomically replace, keep one previous valid copy" (spec §7). An
    /// interruption between any two steps leaves either the old or the new save readable (see the loader's
    /// candidate order in <see cref="SaveSlotStore.Load"/>).
    /// </summary>
    public static class SaveWritePlanner
    {
        public static IReadOnlyList<SaveWriteStep> PlanWrite(SaveSlotPaths paths, string contents, bool latestExists, bool latestValid)
        {
            var steps = new List<SaveWriteStep>(2) { SaveWriteStep.Write(paths.Temp, contents) };
            if (!latestExists)
            {
                steps.Add(SaveWriteStep.MoveTo(paths.Temp, paths.Latest));
            }
            else
            {
                // A valid latest becomes the previous copy; an unreadable one is discarded so it can never push
                // out the last good previous copy.
                steps.Add(SaveWriteStep.ReplaceWith(paths.Temp, paths.Latest, latestValid ? paths.Previous : null));
            }

            return steps;
        }

        /// <summary>Moves the latest file aside, untouched, so a fresh start cannot overwrite it.</summary>
        public static IReadOnlyList<SaveWriteStep> PlanPreserveLatest(SaveSlotPaths paths, bool keptExists)
        {
            var steps = new List<SaveWriteStep>(2);
            if (keptExists)
            {
                steps.Add(SaveWriteStep.DeleteFile(paths.Kept));
            }

            steps.Add(SaveWriteStep.MoveTo(paths.Latest, paths.Kept));
            return steps;
        }

        public static void Execute(ISaveFileSystem fs, IEnumerable<SaveWriteStep> steps)
        {
            foreach (var step in steps)
            {
                switch (step.Kind)
                {
                    case SaveWriteStepKind.WriteFile:
                        fs.WriteAllTextDurable(step.Path, step.Contents);
                        break;
                    case SaveWriteStepKind.Move:
                        fs.Move(step.Path, step.Destination);
                        break;
                    case SaveWriteStepKind.Replace:
                        fs.Replace(step.Path, step.Destination, step.Backup);
                        break;
                    case SaveWriteStepKind.Delete:
                        fs.Delete(step.Path);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(steps), step.Kind, "unknown step");
                }
            }
        }
    }

    /// <summary>How a classifier judges one file's text.</summary>
    public enum SaveCheck
    {
        Valid,
        Newer,
        Invalid,
    }

    public enum SaveLoadStatus
    {
        /// <summary>No save files: start a new game.</summary>
        Empty,

        /// <summary>The latest copy was read.</summary>
        Latest,

        /// <summary>The latest copy was unreadable or missing; an interrupted temp or the previous copy was used.</summary>
        Recovered,

        /// <summary>Written by a newer build: explain, keep the file, block writes until an explicit fresh start.</summary>
        Newer,

        /// <summary>Nothing readable: offer an explicit fresh start.</summary>
        Unreadable,
    }

    public sealed class SaveSlotLoad
    {
        internal SaveSlotLoad(SaveLoadStatus status, string contents, string sourcePath)
        {
            Status = status;
            Contents = contents;
            SourcePath = sourcePath;
        }

        public SaveLoadStatus Status { get; }

        /// <summary>Text of the copy to use (Latest/Recovered), else null.</summary>
        public string Contents { get; }

        /// <summary>The file it came from (or the newer file for <see cref="SaveLoadStatus.Newer"/>).</summary>
        public string SourcePath { get; }

        public bool HasContents => Contents != null;
    }

    /// <summary>
    /// One save slot: loads with recovery and writes with the planner. Writes must be serialized by the caller
    /// (one at a time, e.g. on the main thread); each write is complete when <see cref="Write"/> returns.
    /// </summary>
    public sealed class SaveSlotStore
    {
        private readonly ISaveFileSystem _fs;
        private readonly Func<string, SaveCheck> _check;
        private bool _latestValid;

        public SaveSlotStore(ISaveFileSystem fs, SaveSlotPaths paths, Func<string, SaveCheck> check)
        {
            _fs = fs ?? throw new ArgumentNullException(nameof(fs));
            Paths = paths ?? throw new ArgumentNullException(nameof(paths));
            _check = check ?? throw new ArgumentNullException(nameof(check));
        }

        public SaveSlotPaths Paths { get; }

        /// <summary>True after loading a newer-schema file, until <see cref="PreserveLatestAndUnblock"/>.</summary>
        public bool WritesBlocked { get; private set; }

        /// <summary>Candidates in order: latest, an interrupted temp (complete and valid), then the previous copy.</summary>
        public SaveSlotLoad Load()
        {
            WritesBlocked = false;
            _latestValid = false;
            bool anyFile = false;
            string newerPath = null;
            foreach (string path in new[] { Paths.Latest, Paths.Temp, Paths.Previous })
            {
                if (!_fs.Exists(path))
                {
                    continue;
                }

                // A leftover temp file is never authoritative: if it is unreadable it does not count as a save.
                anyFile |= path != Paths.Temp;
                string text = TryRead(path);
                var check = text == null ? SaveCheck.Invalid : _check(text);
                if (check == SaveCheck.Newer)
                {
                    newerPath = newerPath ?? path;
                    if (path == Paths.Latest)
                    {
                        break;
                    }

                    continue;
                }

                if (check == SaveCheck.Valid)
                {
                    if (path == Paths.Latest)
                    {
                        _latestValid = true;
                        return new SaveSlotLoad(SaveLoadStatus.Latest, text, path);
                    }

                    return new SaveSlotLoad(SaveLoadStatus.Recovered, text, path);
                }
            }

            if (newerPath != null)
            {
                WritesBlocked = true;
                return new SaveSlotLoad(SaveLoadStatus.Newer, null, newerPath);
            }

            return new SaveSlotLoad(anyFile ? SaveLoadStatus.Unreadable : SaveLoadStatus.Empty, null, null);
        }

        /// <summary>Writes <paramref name="contents"/> as the new latest copy (temp, then atomic replace).</summary>
        public void Write(string contents)
        {
            if (WritesBlocked)
            {
                throw new InvalidOperationException(
                    "The save on disk is from a newer build; preserve it (explicit fresh start) before writing.");
            }

            bool latestExists = _fs.Exists(Paths.Latest);
            SaveWritePlanner.Execute(_fs, SaveWritePlanner.PlanWrite(Paths, contents, latestExists, latestExists && _latestValid));
            _latestValid = true;
        }

        /// <summary>The player chose a fresh start over a newer-schema save: keep that file aside, then allow writes.</summary>
        public void PreserveLatestAndUnblock()
        {
            if (_fs.Exists(Paths.Latest))
            {
                SaveWritePlanner.Execute(_fs, SaveWritePlanner.PlanPreserveLatest(Paths, _fs.Exists(Paths.Kept)));
            }

            _latestValid = false;
            WritesBlocked = false;
        }

        private string TryRead(string path)
        {
            try
            {
                return _fs.ReadAllText(path);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }
    }
}
