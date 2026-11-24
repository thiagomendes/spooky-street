using System;
using System.Collections.Generic;
using System.Text;

namespace SpookyStreet.Core.Localization
{
    /// <summary>One localized string plus the translator note that travels with it.</summary>
    public sealed class StringEntry
    {
        private static readonly IReadOnlyDictionary<PluralCategory, string> NoForms =
            new Dictionary<PluralCategory, string>();

        public StringEntry(string key, string value, string context)
            : this(key, value, context, null)
        {
        }

        /// <summary>
        /// A plural entry when <paramref name="plural"/> is not empty: <see cref="Value"/> is then its "other" form
        /// (what older code and schema 1 readers show).
        /// </summary>
        public StringEntry(string key, string value, string context, IDictionary<PluralCategory, string> plural)
        {
            Key = key;
            Context = context ?? string.Empty;
            if (plural != null && plural.Count > 0)
            {
                if (!plural.TryGetValue(PluralCategory.Other, out string other))
                {
                    throw new FormatException($"Plural entry '{key}' needs an 'other' form.");
                }

                Plural = new Dictionary<PluralCategory, string>(plural);
                Value = other;
            }
            else
            {
                Plural = NoForms;
                Value = value;
            }
        }

        public string Key { get; }

        /// <summary>The text, or the "other" form of a plural entry.</summary>
        public string Value { get; }

        /// <summary>Translator context: where the string appears and any length or tone constraints.</summary>
        public string Context { get; }

        /// <summary>CLDR forms of a plural entry; empty for a plain entry.</summary>
        public IReadOnlyDictionary<PluralCategory, string> Plural { get; }

        public bool IsPlural => Plural.Count > 0;

        /// <summary>Every text of the entry: the value, or each plural form.</summary>
        public IEnumerable<string> Texts
        {
            get
            {
                if (!IsPlural)
                {
                    yield return Value;
                    yield break;
                }

                foreach (var form in Plural.Values)
                {
                    yield return form;
                }
            }
        }

        /// <summary>The form for <paramref name="category"/>, or "other" when the entry does not spell it out.</summary>
        public string Form(PluralCategory category) =>
            IsPlural && Plural.TryGetValue(category, out string form) ? form : Value;
    }

    /// <summary>
    /// A locale's strings keyed by stable semantic IDs (e.g. "ui.title.new_game"). This is also the language pack
    /// format (S004 §2.4): a pack is one of these files plus a <see cref="PackManifest"/>.
    /// File format (UTF-8 JSON, no byte order mark):
    /// { "schemaVersion": 2, "locale": "en", "nativeName": "English", "contentVersion": "0.0.1",
    ///   "entries": [ { "key": "...", "value": "...", "context": "..." },
    ///                { "key": "...", "plural": { "one": "{0} bat", "other": "{0} bats" }, "context": "..." } ] }
    /// Schema 2 adds the optional "plural" object (CLDR category → text, "other" required) in place of "value".
    /// Schema 1 files (no plurals) still load.
    /// </summary>
    public sealed class StringTable
    {
        public const int SupportedSchemaVersion = 2;

        private readonly Dictionary<string, StringEntry> _entries;
        private readonly List<StringEntry> _ordered;

        public StringTable(string locale, string nativeName, string contentVersion, IEnumerable<StringEntry> entries)
        {
            if (string.IsNullOrEmpty(locale))
            {
                throw new ArgumentException("Locale is required.", nameof(locale));
            }

            Locale = locale;
            NativeName = string.IsNullOrEmpty(nativeName) ? locale : nativeName;
            ContentVersion = contentVersion ?? string.Empty;
            _entries = new Dictionary<string, StringEntry>(StringComparer.Ordinal);
            _ordered = new List<StringEntry>();
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.Key))
                {
                    throw new FormatException($"String table '{locale}' has an entry with an empty key.");
                }

                if (entry.Value == null)
                {
                    throw new FormatException($"String table '{locale}' has no text for '{entry.Key}'.");
                }

                if (_entries.ContainsKey(entry.Key))
                {
                    throw new FormatException($"String table '{locale}' defines key '{entry.Key}' twice.");
                }

                _entries.Add(entry.Key, entry);
                _ordered.Add(entry);
            }
        }

        public string Locale { get; }

        public string NativeName { get; }

        public string ContentVersion { get; }

        public int Count => _entries.Count;

        /// <summary>Entries in file order.</summary>
        public IEnumerable<StringEntry> Entries => _ordered;

        public bool Contains(string key) => key != null && _entries.ContainsKey(key);

        public bool TryGet(string key, out string value)
        {
            if (key != null && _entries.TryGetValue(key, out var entry))
            {
                value = entry.Value;
                return true;
            }

            value = null;
            return false;
        }

        public bool TryGetEntry(string key, out StringEntry entry)
        {
            entry = null;
            return key != null && _entries.TryGetValue(key, out entry);
        }

        /// <summary>The form for <paramref name="count"/> under this table's locale rules (plain entries ignore it).</summary>
        public bool TryGetPlural(string key, long count, out string value)
        {
            if (TryGetEntry(key, out var entry))
            {
                value = entry.Form(PluralRules.Select(Locale, count));
                return true;
            }

            value = null;
            return false;
        }

        public static StringTable Parse(string json)
        {
            if (json == null)
            {
                throw new FormatException("String table text is missing.");
            }

            if (!(MiniJson.Parse(json) is Dictionary<string, object> root))
            {
                throw new FormatException("String table root must be a JSON object.");
            }

            int schema = (int)RequireNumber(root, "schemaVersion");
            if (schema > SupportedSchemaVersion || schema < 1)
            {
                throw new FormatException(
                    $"String table schema {schema} is not supported (this build reads ≤ {SupportedSchemaVersion}).");
            }

            string locale = RequireString(root, "locale");
            string nativeName = OptionalString(root, "nativeName");
            string contentVersion = OptionalString(root, "contentVersion");
            if (!root.TryGetValue("entries", out object rawEntries) || !(rawEntries is List<object> list))
            {
                throw new FormatException("String table needs an 'entries' array.");
            }

            var entries = new List<StringEntry>(list.Count);
            foreach (object item in list)
            {
                if (!(item is Dictionary<string, object> obj))
                {
                    throw new FormatException("Each string table entry must be an object.");
                }

                string key = RequireString(obj, "key");
                string context = OptionalString(obj, "context");
                if (obj.TryGetValue("plural", out object rawPlural))
                {
                    if (schema < 2)
                    {
                        throw new FormatException($"'{key}': plural forms need schemaVersion 2.");
                    }

                    entries.Add(new StringEntry(key, null, context, ParsePlural(key, rawPlural)));
                }
                else
                {
                    entries.Add(new StringEntry(key, RequireString(obj, "value"), context));
                }
            }

            return new StringTable(locale, nativeName, contentVersion, entries);
        }

        /// <summary>Writes the table in the schema 2 format (schema 1 when it has no plural entry).</summary>
        public string ToJson()
        {
            bool anyPlural = false;
            foreach (var entry in _ordered)
            {
                anyPlural |= entry.IsPlural;
            }

            var sb = new StringBuilder(64 * (_ordered.Count + 2));
            sb.Append("{\n  \"schemaVersion\": ").Append(anyPlural ? 2 : 1).Append(",\n  \"locale\": ");
            MiniJson.Quote(sb, Locale).Append(",\n  \"nativeName\": ");
            MiniJson.Quote(sb, NativeName).Append(",\n  \"contentVersion\": ");
            MiniJson.Quote(sb, ContentVersion).Append(",\n  \"entries\": [");
            for (int i = 0; i < _ordered.Count; i++)
            {
                var entry = _ordered[i];
                sb.Append(i == 0 ? "\n    { \"key\": " : ",\n    { \"key\": ");
                MiniJson.Quote(sb, entry.Key);
                if (entry.IsPlural)
                {
                    sb.Append(", \"plural\": {");
                    bool first = true;
                    foreach (PluralCategory category in Enum.GetValues(typeof(PluralCategory)))
                    {
                        if (entry.Plural.TryGetValue(category, out string form))
                        {
                            sb.Append(first ? " " : ", ");
                            MiniJson.Quote(sb, PluralRules.Name(category)).Append(": ");
                            MiniJson.Quote(sb, form);
                            first = false;
                        }
                    }

                    sb.Append(" }");
                }
                else
                {
                    sb.Append(", \"value\": ");
                    MiniJson.Quote(sb, entry.Value);
                }

                sb.Append(", \"context\": ");
                MiniJson.Quote(sb, entry.Context).Append(" }");
            }

            sb.Append("\n  ]\n}\n");
            return sb.ToString();
        }

        private static Dictionary<PluralCategory, string> ParsePlural(string key, object raw)
        {
            if (!(raw is Dictionary<string, object> forms) || forms.Count == 0)
            {
                throw new FormatException($"'{key}': 'plural' must be an object of CLDR forms.");
            }

            var result = new Dictionary<PluralCategory, string>();
            foreach (var pair in forms)
            {
                if (!PluralRules.TryParse(pair.Key, out var category))
                {
                    throw new FormatException($"'{key}': '{pair.Key}' is not a CLDR plural category.");
                }

                if (!(pair.Value is string text))
                {
                    throw new FormatException($"'{key}': plural form '{pair.Key}' must be a string.");
                }

                result[category] = text;
            }

            if (!result.ContainsKey(PluralCategory.Other))
            {
                throw new FormatException($"Plural entry '{key}' needs an 'other' form.");
            }

            return result;
        }

        private static string RequireString(Dictionary<string, object> obj, string name)
        {
            if (obj.TryGetValue(name, out object value) && value is string s)
            {
                return s;
            }

            throw new FormatException($"Missing string field '{name}'.");
        }

        private static string OptionalString(Dictionary<string, object> obj, string name) =>
            obj.TryGetValue(name, out object value) && value is string s ? s : null;

        private static double RequireNumber(Dictionary<string, object> obj, string name)
        {
            if (obj.TryGetValue(name, out object value) && value is double d)
            {
                return d;
            }

            throw new FormatException($"Missing number field '{name}'.");
        }
    }
}
