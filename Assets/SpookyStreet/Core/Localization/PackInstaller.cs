using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using SpookyStreet.Core.Save;

namespace SpookyStreet.Core.Localization
{
    /// <summary>An installed, validated pack ready to switch on.</summary>
    public sealed class InstalledPack
    {
        public InstalledPack(PackManifest manifest, StringTable table, float coverage)
        {
            Manifest = manifest;
            Table = table;
            Coverage = coverage;
        }

        public PackManifest Manifest { get; }

        public StringTable Table { get; }

        public float Coverage { get; }

        public string Locale => Manifest.Locale;
    }

    /// <summary>The result of an install or remove.</summary>
    public sealed class PackResult
    {
        public PackResult(PackError error, string detail = null, InstalledPack pack = null)
        {
            Error = error;
            Detail = detail ?? string.Empty;
            Pack = pack;
        }

        public PackError Error { get; }

        public bool Ok => Error == PackError.None;

        public string Detail { get; }

        public InstalledPack Pack { get; }

        public override string ToString() => Ok ? "ok" : Error + " (" + Detail + ")";
    }

    /// <summary>
    /// Installs downloaded packs on the device (S004 §2.4 pipeline) over the save file system abstraction:
    /// validate the bytes, write them to a staging file, read the staged copy back and check its hash, move it to
    /// its own name (<c>locale-hash.json</c>), then switch the index file (<c>installed.json</c>, written like a save:
    /// temp, atomic replace, one previous copy) to it. Only then is the old file of that locale deleted. An
    /// interruption at any step leaves the previous index pointing at files that still exist, so the old pack (or
    /// English) is always what loads. Writes must be serialized by the caller (main thread).
    /// </summary>
    public sealed class PackInstaller
    {
        public const int IndexSchemaVersion = 1;
        public const string FolderName = "languages";

        private readonly ISaveFileSystem _fs;
        private readonly SaveSlotStore _index;
        private readonly StringTable _english;
        private readonly Func<int, bool> _fontCovers;
        private readonly Func<long> _freeBytes;
        private List<PackManifest> _entries = new List<PackManifest>();

        /// <param name="directory">The save directory; packs live in its <see cref="FolderName"/> folder.</param>
        /// <param name="freeBytes">Free space on the device, or null when unknown (then only write errors count).</param>
        public PackInstaller(ISaveFileSystem fs, string directory, StringTable english, Func<int, bool> fontCovers = null,
            Func<long> freeBytes = null)
        {
            _fs = fs ?? throw new ArgumentNullException(nameof(fs));
            _english = english ?? throw new ArgumentNullException(nameof(english));
            Directory = Path.Combine(directory ?? string.Empty, FolderName);
            _index = new SaveSlotStore(fs, new SaveSlotPaths(Directory, "installed"), CheckIndex);
            _fontCovers = fontCovers;
            _freeBytes = freeBytes;
        }

        public string Directory { get; }

        public SaveSlotPaths IndexPaths => _index.Paths;

        public string StagingPath(string locale) => Path.Combine(Directory, locale + ".staging");

        public string PackPath(PackManifest manifest) =>
            Path.Combine(Directory, manifest.Locale + "-" + manifest.Sha256.Substring(0, 16) + ".json");

        /// <summary>
        /// Reads the index and every pack it lists, re-validating each (launch, offline). A pack whose file is missing
        /// or no longer valid is left out; it is never half-loaded.
        /// </summary>
        public IReadOnlyList<InstalledPack> Load()
        {
            _entries = new List<PackManifest>();
            var load = _index.Load();
            if (load.HasContents)
            {
                _entries = ParseIndex(load.Contents) ?? new List<PackManifest>();
            }

            var packs = new List<InstalledPack>();
            foreach (var manifest in _entries)
            {
                var pack = TryLoad(manifest);
                if (pack != null)
                {
                    packs.Add(pack);
                }
            }

            return packs;
        }

        /// <summary>Validates and installs a downloaded pack; on any failure the installed packs are unchanged.</summary>
        public PackResult Install(PackManifest manifest, byte[] bytes)
        {
            if (manifest == null)
            {
                return new PackResult(PackError.UnknownPack, "no manifest");
            }

            var validation = PackValidator.Validate(manifest, bytes, _english, _fontCovers);
            if (!validation.Ok)
            {
                return new PackResult(validation.Error, validation.Detail);
            }

            if (_freeBytes != null)
            {
                long free = _freeBytes();
                if (free >= 0 && free < manifest.Size * 3)
                {
                    return new PackResult(PackError.InsufficientStorage, $"{free} bytes free");
                }
            }

            string staging = StagingPath(manifest.Locale);
            string target = PackPath(manifest);
            try
            {
                if (_fs.Exists(staging))
                {
                    _fs.Delete(staging);
                }

                _fs.WriteAllTextDurable(staging, validation.Text);
                string staged = _fs.ReadAllText(staging);
                if (PackHash.Sha256Hex(PackHash.Utf8.GetBytes(staged)) != manifest.Sha256)
                {
                    TryDelete(staging);
                    return new PackResult(PackError.InsufficientStorage, "the staged copy does not read back");
                }

                if (_fs.Exists(target))
                {
                    // Same locale and hash (a reinstall): keep a good copy; replace a damaged one atomically.
                    if (SameHash(target, manifest.Sha256))
                    {
                        _fs.Delete(staging);
                    }
                    else
                    {
                        _fs.Replace(staging, target, null);
                    }
                }
                else
                {
                    _fs.Move(staging, target);
                }
                var previous = Find(manifest.Locale);
                var entries = new List<PackManifest>();
                foreach (var entry in _entries)
                {
                    if (!string.Equals(entry.Locale, manifest.Locale, StringComparison.OrdinalIgnoreCase))
                    {
                        entries.Add(entry);
                    }
                }

                entries.Add(new PackManifest(manifest.Locale, manifest.NativeName, manifest.ContentVersion,
                    manifest.TableSchema, manifest.Size, manifest.Sha256, Path.GetFileName(target), manifest.Development));
                WriteIndex(entries);
                if (previous != null && PackPath(previous) != target)
                {
                    TryDelete(PackPath(previous));
                }
            }
            catch (IOException e)
            {
                TryDelete(staging);
                return new PackResult(PackError.InsufficientStorage, e.Message);
            }
            catch (UnauthorizedAccessException e)
            {
                TryDelete(staging);
                return new PackResult(PackError.InsufficientStorage, e.Message);
            }

            return new PackResult(PackError.None, null,
                new InstalledPack(Find(manifest.Locale), validation.Table, validation.Coverage));
        }

        /// <summary>Forgets an installed pack and deletes its file (the caller reverts to English if it was active).</summary>
        public PackResult Remove(string locale)
        {
            var manifest = Find(locale);
            if (manifest == null)
            {
                return new PackResult(PackError.UnknownPack, locale);
            }

            var entries = new List<PackManifest>();
            foreach (var entry in _entries)
            {
                if (entry != manifest)
                {
                    entries.Add(entry);
                }
            }

            try
            {
                WriteIndex(entries);
            }
            catch (IOException e)
            {
                return new PackResult(PackError.InsufficientStorage, e.Message);
            }

            TryDelete(PackPath(manifest));
            return new PackResult(PackError.None);
        }

        public PackManifest Find(string locale)
        {
            foreach (var entry in _entries)
            {
                if (string.Equals(entry.Locale, locale, StringComparison.OrdinalIgnoreCase))
                {
                    return entry;
                }
            }

            return null;
        }

        private InstalledPack TryLoad(PackManifest manifest)
        {
            string path = PackPath(manifest);
            try
            {
                if (!_fs.Exists(path))
                {
                    return null;
                }

                string text = _fs.ReadAllText(path);
                byte[] bytes = PackHash.Utf8.GetBytes(text);
                var validation = PackValidator.Validate(manifest, bytes, _english, _fontCovers);
                return validation.Ok ? new InstalledPack(manifest, validation.Table, validation.Coverage) : null;
            }
            catch (IOException)
            {
                return null;
            }
            catch (ArgumentException)
            {
                return null; // text that is not valid Unicode
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        private void WriteIndex(List<PackManifest> entries)
        {
            var sb = new StringBuilder();
            sb.Append("{\n  \"schemaVersion\": ").Append(IndexSchemaVersion).Append(",\n  \"packs\": [");
            for (int i = 0; i < entries.Count; i++)
            {
                sb.Append(i == 0 ? "\n" : ",\n");
                entries[i].WriteJson(sb, "    ");
            }

            sb.Append("\n  ]\n}\n");
            if (_index.WritesBlocked)
            {
                // An index from a newer build lists packs this build may not read; it holds no progress.
                _index.PreserveLatestAndUnblock();
            }

            _index.Write(sb.ToString());
            _entries = entries;
        }

        private bool SameHash(string path, string sha256)
        {
            try
            {
                return PackHash.Sha256Hex(PackHash.Utf8.GetBytes(_fs.ReadAllText(path))) == sha256;
            }
            catch (IOException)
            {
                return false;
            }
            catch (ArgumentException)
            {
                return false;
            }
        }

        private void TryDelete(string path)
        {
            try
            {
                if (_fs.Exists(path))
                {
                    _fs.Delete(path);
                }
            }
            catch (IOException)
            {
                // A leftover file is harmless: the index no longer lists it.
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private static List<PackManifest> ParseIndex(string json)
        {
            Dictionary<string, object> root;
            try
            {
                root = MiniJson.Parse(json) as Dictionary<string, object>;
            }
            catch (FormatException)
            {
                return null;
            }

            if (root == null || !(root.TryGetValue("packs", out object raw) && raw is List<object> list))
            {
                return null;
            }

            var entries = new List<PackManifest>();
            foreach (object item in list)
            {
                try
                {
                    entries.Add(PackManifest.FromJson(item as Dictionary<string, object>));
                }
                catch (FormatException)
                {
                    // Skip one bad row; the others still load.
                }
            }

            return entries;
        }

        private static SaveCheck CheckIndex(string json)
        {
            try
            {
                if (!(MiniJson.Parse(json) is Dictionary<string, object> root) ||
                    !(root.TryGetValue("schemaVersion", out object v) && v is double version) || version < 1 ||
                    !(root.TryGetValue("packs", out object packs) && packs is List<object>))
                {
                    return SaveCheck.Invalid;
                }

                return version > IndexSchemaVersion ? SaveCheck.Newer : SaveCheck.Valid;
            }
            catch (FormatException)
            {
                return SaveCheck.Invalid;
            }
        }
    }
}
