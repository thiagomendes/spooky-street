using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace SpookyStreet.Core.Localization
{
    /// <summary>The outcome of checking one pack file before it is switched on.</summary>
    public sealed class PackValidation
    {
        internal PackValidation(PackError error, string detail, StringTable table, string text, float coverage,
            IReadOnlyList<string> missingKeys)
        {
            Error = error;
            Detail = detail ?? string.Empty;
            Table = table;
            Text = text;
            Coverage = coverage;
            MissingKeys = missingKeys ?? new string[0];
        }

        public PackError Error { get; }

        public bool Ok => Error == PackError.None;

        /// <summary>For logs and tests: which key or character failed. Never shown to the child.</summary>
        public string Detail { get; }

        /// <summary>The parsed table when <see cref="Ok"/>.</summary>
        public StringTable Table { get; }

        /// <summary>The decoded file text when <see cref="Ok"/>.</summary>
        public string Text { get; }

        /// <summary>Share of bundled English keys the pack translates (0..1). Missing keys show English.</summary>
        public float Coverage { get; }

        public IReadOnlyList<string> MissingKeys { get; }

        internal static PackValidation Fail(PackError error, string detail) =>
            new PackValidation(error, detail, null, null, 0f, null);
    }

    /// <summary>
    /// Checks a pack file against its manifest and the bundled English table before activation (S004 §2.4): size and
    /// SHA-256, strict UTF-8, schema, locale, key coverage, {n} placeholders and rich-text tags per key, plural forms
    /// per the pack's CLDR rules, and that the bundled fonts draw every character. Pure: bytes in, verdict out.
    /// </summary>
    public static class PackValidator
    {
        /// <summary>A pack must translate at least this share of the current English keys (an older pack may lag).</summary>
        public const float DefaultMinCoverage = 0.8f;

        private static readonly Regex Placeholder = new Regex(@"\{(\d+)\}", RegexOptions.CultureInvariant);
        private static readonly Regex Tag = new Regex(@"</?[a-zA-Z]+(?:=[^>]*)?>", RegexOptions.CultureInvariant);

        public static PackValidation Validate(PackManifest manifest, byte[] bytes, StringTable english,
            Func<int, bool> fontCovers = null, float minCoverage = DefaultMinCoverage)
        {
            if (manifest == null)
            {
                throw new ArgumentNullException(nameof(manifest));
            }

            if (english == null)
            {
                throw new ArgumentNullException(nameof(english));
            }

            fontCovers = fontCovers ?? FontCoverage.Covers;
            if (bytes == null || bytes.Length < manifest.Size)
            {
                return PackValidation.Fail(PackError.Interrupted,
                    $"{bytes?.Length ?? 0} of {manifest.Size} bytes arrived");
            }

            if (bytes.Length > manifest.Size)
            {
                return PackValidation.Fail(PackError.TooLarge, $"{bytes.Length} bytes, manifest says {manifest.Size}");
            }

            if (PackHash.Sha256Hex(bytes) != manifest.Sha256)
            {
                return PackValidation.Fail(PackError.HashMismatch, "sha256 differs from the manifest");
            }

            if (bytes.Length >= 3 && bytes[0] == 0xEF && bytes[1] == 0xBB && bytes[2] == 0xBF)
            {
                return PackValidation.Fail(PackError.Unreadable, "byte order mark");
            }

            string text;
            try
            {
                text = PackHash.Utf8.GetString(bytes);
            }
            catch (ArgumentException)
            {
                return PackValidation.Fail(PackError.Unreadable, "not UTF-8");
            }

            return ValidateText(manifest, text, english, fontCovers, minCoverage);
        }

        /// <summary>The checks after integrity (also re-run on launch over the installed copy).</summary>
        public static PackValidation ValidateText(PackManifest manifest, string text, StringTable english,
            Func<int, bool> fontCovers = null, float minCoverage = DefaultMinCoverage)
        {
            fontCovers = fontCovers ?? FontCoverage.Covers;
            if (manifest.TableSchema > StringTable.SupportedSchemaVersion)
            {
                return PackValidation.Fail(PackError.NewerSchema, $"table schema {manifest.TableSchema}");
            }

            StringTable table;
            try
            {
                table = StringTable.Parse(text);
            }
            catch (FormatException e)
            {
                bool newer = e.Message.Contains("schema") && e.Message.Contains("not supported");
                return PackValidation.Fail(newer ? PackError.NewerSchema : PackError.Unreadable, e.Message);
            }

            if (!string.Equals(table.Locale, manifest.Locale, StringComparison.Ordinal))
            {
                return PackValidation.Fail(PackError.WrongLocale, $"file says '{table.Locale}'");
            }

            string glyph = MissingGlyph(table.NativeName, fontCovers);
            if (glyph != null)
            {
                return PackValidation.Fail(PackError.MissingGlyph, "nativeName needs " + glyph);
            }

            var missing = new List<string>();
            foreach (var source in english.Entries)
            {
                if (!table.TryGetEntry(source.Key, out var entry))
                {
                    missing.Add(source.Key);
                    continue;
                }

                var problem = CheckEntry(source, entry, table.Locale, fontCovers);
                if (problem.Error != PackError.None)
                {
                    return PackValidation.Fail(problem.Error, source.Key + ": " + problem.Detail);
                }
            }

            // Keys English does not have (a pack made for newer content) are never shown; their text is still checked
            // for glyphs so nothing undrawable is stored.
            foreach (var entry in table.Entries)
            {
                if (!english.Contains(entry.Key))
                {
                    foreach (string t in entry.Texts)
                    {
                        glyph = MissingGlyph(t, fontCovers);
                        if (glyph != null)
                        {
                            return PackValidation.Fail(PackError.MissingGlyph, entry.Key + " needs " + glyph);
                        }
                    }
                }
            }

            float coverage = english.Count == 0 ? 1f : (english.Count - missing.Count) / (float)english.Count;
            if (coverage < minCoverage)
            {
                return new PackValidation(PackError.Incomplete,
                    $"{english.Count - missing.Count} of {english.Count} keys", null, null, coverage, missing);
            }

            return new PackValidation(PackError.None, null, table, text, coverage, missing);
        }

        private readonly struct Problem
        {
            public Problem(PackError error, string detail)
            {
                Error = error;
                Detail = detail;
            }

            public PackError Error { get; }

            public string Detail { get; }
        }

        private static Problem CheckEntry(StringEntry source, StringEntry entry, string locale, Func<int, bool> covers)
        {
            if (source.IsPlural != entry.IsPlural)
            {
                return new Problem(PackError.PluralMismatch,
                    source.IsPlural ? "English has plural forms" : "English has no plural forms");
            }

            if (entry.IsPlural)
            {
                foreach (var category in PluralRules.RequiredFor(locale))
                {
                    if (!entry.Plural.ContainsKey(category))
                    {
                        return new Problem(PackError.PluralMismatch, "needs the '" + PluralRules.Name(category) + "' form");
                    }
                }
            }

            var sourcePlaceholders = Placeholders(source.Value);
            var allowed = new HashSet<int>();
            foreach (string t in source.Texts)
            {
                allowed.UnionWith(Placeholders(t));
            }

            string sourceTags = Tags(source.Value);
            foreach (string t in entry.Texts)
            {
                string glyph = MissingGlyph(t, covers);
                if (glyph != null)
                {
                    return new Problem(PackError.MissingGlyph, "needs " + glyph);
                }

                if (!Formats(t))
                {
                    return new Problem(PackError.PlaceholderMismatch, "braces do not format");
                }

                var placeholders = Placeholders(t);
                if (!allowed.IsSupersetOf(placeholders))
                {
                    return new Problem(PackError.PlaceholderMismatch, "unknown placeholder");
                }

                if (Tags(t) != sourceTags)
                {
                    return new Problem(PackError.PlaceholderMismatch, "rich-text tags differ");
                }
            }

            // The general form keeps every placeholder English shows (a "one" form may spell out its count).
            if (!Placeholders(entry.Value).SetEquals(sourcePlaceholders))
            {
                return new Problem(PackError.PlaceholderMismatch, "placeholders differ from English");
            }

            return new Problem(PackError.None, null);
        }

        private static HashSet<int> Placeholders(string text)
        {
            var set = new HashSet<int>();
            foreach (Match m in Placeholder.Matches(text ?? string.Empty))
            {
                if (int.TryParse(m.Groups[1].Value, NumberStyles.None, CultureInfo.InvariantCulture, out int n))
                {
                    set.Add(n);
                }
            }

            return set;
        }

        private static string Tags(string text)
        {
            var tags = new List<string>();
            foreach (Match m in Tag.Matches(text ?? string.Empty))
            {
                tags.Add(m.Value);
            }

            tags.Sort(StringComparer.Ordinal);
            return string.Join("|", tags);
        }

        /// <summary>True when <see cref="string.Format(string, object[])"/> accepts the text with enough arguments.</summary>
        private static bool Formats(string text)
        {
            int max = -1;
            foreach (int n in Placeholders(text))
            {
                max = Math.Max(max, n);
            }

            if (max > 9)
            {
                return false;
            }

            var args = new object[max + 1];
            for (int i = 0; i < args.Length; i++)
            {
                args[i] = "x";
            }

            try
            {
                string.Format(CultureInfo.InvariantCulture, text, args);
                return true;
            }
            catch (FormatException)
            {
                return false;
            }
        }

        /// <summary>"U+0416 'Ж'" for the first character the fonts lack, else null.</summary>
        public static string MissingGlyph(string text, Func<int, bool> covers)
        {
            if (string.IsNullOrEmpty(text))
            {
                return null;
            }

            for (int i = 0; i < text.Length; i++)
            {
                int cp;
                if (char.IsHighSurrogate(text[i]) && i + 1 < text.Length && char.IsLowSurrogate(text[i + 1]))
                {
                    cp = char.ConvertToUtf32(text[i], text[i + 1]);
                    i++;
                }
                else
                {
                    cp = text[i];
                }

                if (!covers(cp))
                {
                    var sb = new StringBuilder("U+");
                    sb.Append(cp.ToString("X4", CultureInfo.InvariantCulture));
                    return sb.ToString();
                }
            }

            return null;
        }
    }
}
