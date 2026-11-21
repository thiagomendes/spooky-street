using System;
using System.Collections.Generic;
using System.Globalization;

namespace SpookyStreet.Core.Localization
{
    /// <summary>
    /// Resolves keys against the active table, falling back to the bundled English table (spec §7:
    /// "Missing individual key → bundled English"). Never throws on lookup: a key missing everywhere
    /// renders as a visible marker so it is caught in review instead of crashing a child's session.
    /// </summary>
    public sealed class LocalizationCatalog
    {
        private readonly HashSet<string> _reportedMissing = new HashSet<string>(StringComparer.Ordinal);

        public LocalizationCatalog(StringTable bundledFallback)
        {
            Fallback = bundledFallback ?? throw new ArgumentNullException(nameof(bundledFallback));
            Active = bundledFallback;
        }

        public StringTable Fallback { get; }

        public StringTable Active { get; private set; }

        /// <summary>Raised after the active table changes so visible text can refresh.</summary>
        public event Action Changed;

        /// <summary>Raised once per key that is missing from both the active and fallback tables.</summary>
        public event Action<string> MissingKey;

        public void SetActive(StringTable table)
        {
            Active = table ?? Fallback;
            Changed?.Invoke();
        }

        /// <summary>Reverts to bundled English (e.g. when an active pack is removed).</summary>
        public void ResetToFallback() => SetActive(Fallback);

        public string Get(string key)
        {
            if (Active.TryGet(key, out string value) || Fallback.TryGet(key, out value))
            {
                return value;
            }

            if (key != null && _reportedMissing.Add(key))
            {
                MissingKey?.Invoke(key);
            }

            return MissingMarker(key);
        }

        public string Format(string key, params object[] args)
        {
            string pattern = Get(key);
            try
            {
                return string.Format(CultureInfo.InvariantCulture, pattern, args);
            }
            catch (FormatException)
            {
                // A translator broke a placeholder: show the raw pattern rather than throwing.
                return pattern;
            }
        }

        public static string MissingMarker(string key) => "⟦" + (key ?? "null") + "⟧";
    }
}
