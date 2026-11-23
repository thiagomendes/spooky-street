using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SpookyStreet.Core.Localization
{
    /// <summary>Why a language pack step failed (S004 §2.4, spec §7). The old pack is kept on every failure.</summary>
    public enum PackError
    {
        None,

        /// <summary>No connection, or the host answered with an error.</summary>
        Offline,

        /// <summary>The download stopped before the whole file arrived.</summary>
        Interrupted,

        /// <summary>The file is bigger than its manifest says (or than a pack may be).</summary>
        TooLarge,

        /// <summary>The bytes do not match the manifest's SHA-256 (corrupt file).</summary>
        HashMismatch,

        /// <summary>Not UTF-8 JSON in the string table format.</summary>
        Unreadable,

        /// <summary>Made for a newer app (table, manifest or catalog schema this build cannot read).</summary>
        NewerSchema,

        /// <summary>The file's locale is not the manifest's.</summary>
        WrongLocale,

        /// <summary>Too many keys missing for the game's current content.</summary>
        Incomplete,

        /// <summary>A translation's {0} placeholders or rich-text tags differ from English, or break formatting.</summary>
        PlaceholderMismatch,

        /// <summary>Plural forms missing or present where English has none.</summary>
        PluralMismatch,

        /// <summary>A character the bundled fonts cannot draw.</summary>
        MissingGlyph,

        /// <summary>Writing the pack failed (normally a full device).</summary>
        InsufficientStorage,

        /// <summary>Only HTTPS hosts are allowed.</summary>
        NotHttps,

        /// <summary>No such pack in the catalog or on the device.</summary>
        UnknownPack,

        /// <summary>Built-in languages cannot be removed.</summary>
        BuiltIn,

        /// <summary>Another download is running.</summary>
        Busy,
    }

    /// <summary>
    /// What the catalog says about one downloadable pack (spec §7: locale, content version, supported game-content
    /// schema, size, bundle reference and SHA-256). JSON object fields: "locale", "nativeName", "contentVersion",
    /// "tableSchema", "size", "sha256", "file" (relative to the catalog URL) and the optional "development" flag
    /// (pseudo pack: listed only in development builds).
    /// </summary>
    public sealed class PackManifest
    {
        /// <summary>No pack may be bigger than this (the English table is about 60 KB).</summary>
        public const long MaxPackBytes = 2 * 1024 * 1024;

        public PackManifest(string locale, string nativeName, string contentVersion, int tableSchema, long size,
            string sha256, string file, bool development = false)
        {
            if (!IsLocaleCode(locale))
            {
                throw new FormatException($"'{locale}' is not a locale code.");
            }

            if (size <= 0 || size > MaxPackBytes)
            {
                throw new FormatException($"Pack '{locale}' has a bad size {size}.");
            }

            if (!IsSha256(sha256))
            {
                throw new FormatException($"Pack '{locale}' needs a 64-hex-digit sha256.");
            }

            if (string.IsNullOrEmpty(file))
            {
                throw new FormatException($"Pack '{locale}' needs a file.");
            }

            Locale = locale;
            NativeName = string.IsNullOrEmpty(nativeName) ? locale : nativeName;
            ContentVersion = contentVersion ?? string.Empty;
            TableSchema = tableSchema;
            Size = size;
            Sha256 = sha256.ToLowerInvariant();
            File = file;
            Development = development;
        }

        public string Locale { get; }

        public string NativeName { get; }

        public string ContentVersion { get; }

        /// <summary>The <see cref="StringTable"/> schema the file uses.</summary>
        public int TableSchema { get; }

        public long Size { get; }

        public string Sha256 { get; }

        public string File { get; }

        public bool Development { get; }

        /// <summary>Builds a manifest for a pack file (pack builder, tests and local fixtures).</summary>
        public static PackManifest For(StringTable table, string file, bool development = false)
        {
            byte[] bytes = PackHash.Utf8.GetBytes(table.ToJson());
            int schema = (int)SchemaOf(table);
            return new PackManifest(table.Locale, table.NativeName, table.ContentVersion, schema, bytes.Length,
                PackHash.Sha256Hex(bytes), file, development);
        }

        public static PackManifest FromJson(Dictionary<string, object> obj)
        {
            if (obj == null)
            {
                throw new FormatException("A pack entry must be an object.");
            }

            return new PackManifest(Str(obj, "locale"), Str(obj, "nativeName"), Str(obj, "contentVersion"),
                (int)Num(obj, "tableSchema", 1), (long)Num(obj, "size", 0), Str(obj, "sha256"), Str(obj, "file"),
                obj.TryGetValue("development", out object dev) && dev is bool b && b);
        }

        public void WriteJson(StringBuilder sb, string indent)
        {
            sb.Append(indent).Append("{ \"locale\": ");
            MiniJson.Quote(sb, Locale).Append(", \"nativeName\": ");
            MiniJson.Quote(sb, NativeName).Append(", \"contentVersion\": ");
            MiniJson.Quote(sb, ContentVersion).Append(", \"tableSchema\": ").Append(TableSchema)
                .Append(", \"size\": ").Append(Size.ToString(CultureInfo.InvariantCulture)).Append(", \"sha256\": ");
            MiniJson.Quote(sb, Sha256).Append(", \"file\": ");
            MiniJson.Quote(sb, File);
            if (Development)
            {
                sb.Append(", \"development\": true");
            }

            sb.Append(" }");
        }

        /// <summary>Letters, digits and hyphens, 2–35 characters ("en", "pt-BR", "qps-ploc"); safe as a file name.</summary>
        public static bool IsLocaleCode(string s)
        {
            if (string.IsNullOrEmpty(s) || s.Length < 2 || s.Length > 35 || s[0] == '-' || s[s.Length - 1] == '-')
            {
                return false;
            }

            foreach (char c in s)
            {
                if (!(c == '-' || (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9')))
                {
                    return false;
                }
            }

            return true;
        }

        private static bool IsSha256(string s)
        {
            if (s == null || s.Length != 64)
            {
                return false;
            }

            foreach (char c in s)
            {
                if (!((c >= '0' && c <= '9') || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F')))
                {
                    return false;
                }
            }

            return true;
        }

        private static double SchemaOf(StringTable table)
        {
            foreach (var entry in table.Entries)
            {
                if (entry.IsPlural)
                {
                    return 2;
                }
            }

            return 1;
        }

        internal static string Str(Dictionary<string, object> obj, string name) =>
            obj.TryGetValue(name, out object v) && v is string s ? s : null;

        internal static double Num(Dictionary<string, object> obj, string name, double fallback) =>
            obj.TryGetValue(name, out object v) && v is double d ? d : fallback;
    }

    /// <summary>
    /// The host's list of downloadable packs: { "catalogVersion": 1, "packs": [ manifest, ... ] }. Fetched only when a
    /// grown-up opens the download list behind the parent gate; nothing is sent with the request.
    /// </summary>
    public sealed class PackCatalog
    {
        public const int SupportedCatalogVersion = 1;

        public PackCatalog(IEnumerable<PackManifest> packs)
        {
            var list = new List<PackManifest>();
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var pack in packs)
            {
                if (seen.Add(pack.Locale))
                {
                    list.Add(pack);
                }
            }

            Packs = list;
        }

        public IReadOnlyList<PackManifest> Packs { get; }

        /// <summary>The packs a build may offer: development packs (the pseudo-language) only in development builds.</summary>
        public IEnumerable<PackManifest> Visible(bool developmentBuild)
        {
            foreach (var pack in Packs)
            {
                if (developmentBuild || !pack.Development)
                {
                    yield return pack;
                }
            }
        }

        public PackManifest Find(string locale)
        {
            foreach (var pack in Packs)
            {
                if (string.Equals(pack.Locale, locale, StringComparison.OrdinalIgnoreCase))
                {
                    return pack;
                }
            }

            return null;
        }

        /// <summary>Parses a catalog; a bad entry is skipped, a newer catalog version is refused as a whole.</summary>
        public static PackError TryParse(string json, out PackCatalog catalog)
        {
            catalog = null;
            Dictionary<string, object> root;
            try
            {
                root = MiniJson.Parse(json ?? string.Empty) as Dictionary<string, object>;
            }
            catch (FormatException)
            {
                return PackError.Unreadable;
            }

            if (root == null)
            {
                return PackError.Unreadable;
            }

            double version = PackManifest.Num(root, "catalogVersion", 0);
            if (version < 1)
            {
                return PackError.Unreadable;
            }

            if (version > SupportedCatalogVersion)
            {
                return PackError.NewerSchema;
            }

            var packs = new List<PackManifest>();
            if (root.TryGetValue("packs", out object raw) && raw is List<object> list)
            {
                foreach (object item in list)
                {
                    try
                    {
                        packs.Add(PackManifest.FromJson(item as Dictionary<string, object>));
                    }
                    catch (FormatException)
                    {
                        // One broken entry must not hide the others.
                    }
                }
            }

            catalog = new PackCatalog(packs);
            return PackError.None;
        }

        public string ToJson()
        {
            var sb = new StringBuilder();
            sb.Append("{\n  \"catalogVersion\": ").Append(SupportedCatalogVersion).Append(",\n  \"packs\": [");
            for (int i = 0; i < Packs.Count; i++)
            {
                sb.Append(i == 0 ? "\n" : ",\n");
                Packs[i].WriteJson(sb, "    ");
            }

            sb.Append("\n  ]\n}\n");
            return sb.ToString();
        }
    }

    /// <summary>SHA-256 and strict UTF-8 for pack files.</summary>
    public static class PackHash
    {
        /// <summary>UTF-8 without a byte order mark, throwing on invalid bytes.</summary>
        public static readonly UTF8Encoding Utf8 = new UTF8Encoding(false, true);

        public static string Sha256Hex(byte[] bytes)
        {
            using (var sha = System.Security.Cryptography.SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(bytes ?? new byte[0]);
                var sb = new StringBuilder(64);
                foreach (byte b in hash)
                {
                    sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
                }

                return sb.ToString();
            }
        }
    }
}
