using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.Localization
{
    /// <summary>One localized string plus the translator note that travels with it.</summary>
    public sealed class StringEntry
    {
        public StringEntry(string key, string value, string context)
        {
            Key = key;
            Value = value;
            Context = context ?? string.Empty;
        }

        public string Key { get; }

        public string Value { get; }

        /// <summary>Translator context: where the string appears and any length or tone constraints.</summary>
        public string Context { get; }
    }

    /// <summary>
    /// A locale's strings keyed by stable semantic IDs (e.g. "ui.title.new_game").
    /// File format (UTF-8 JSON):
    /// { "schemaVersion": 1, "locale": "en", "nativeName": "English", "contentVersion": "0.0.1",
    ///   "entries": [ { "key": "...", "value": "...", "context": "..." } ] }
    /// </summary>
    public sealed class StringTable
    {
        public const int SupportedSchemaVersion = 1;

        private readonly Dictionary<string, StringEntry> _entries;

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
            foreach (var entry in entries)
            {
                if (string.IsNullOrEmpty(entry.Key))
                {
                    throw new FormatException($"String table '{locale}' has an entry with an empty key.");
                }

                if (_entries.ContainsKey(entry.Key))
                {
                    throw new FormatException($"String table '{locale}' defines key '{entry.Key}' twice.");
                }

                _entries.Add(entry.Key, entry);
            }
        }

        public string Locale { get; }

        public string NativeName { get; }

        public string ContentVersion { get; }

        public int Count => _entries.Count;

        public IEnumerable<StringEntry> Entries => _entries.Values;

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

        public static StringTable Parse(string json)
        {
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

                entries.Add(new StringEntry(RequireString(obj, "key"), RequireString(obj, "value"),
                    OptionalString(obj, "context")));
            }

            return new StringTable(locale, nativeName, contentVersion, entries);
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
