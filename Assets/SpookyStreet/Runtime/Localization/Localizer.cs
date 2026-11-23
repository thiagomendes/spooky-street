using SpookyStreet.Core.Localization;
using UnityEngine;

namespace SpookyStreet.Localization
{
    /// <summary>
    /// Process-wide access to the localization catalog. The bundled English table ships inside the app
    /// (Resources/Localization/en.json) so a fresh offline install always has every string.
    /// </summary>
    public static class Localizer
    {
        public const string BundledEnglishResource = "Localization/en";

        private static LocalizationCatalog s_catalog;

        public static LocalizationCatalog Catalog
        {
            get
            {
                if (s_catalog == null)
                {
                    Initialize();
                }

                return s_catalog;
            }
        }

        public static string Get(string key) => Catalog.Get(key);

        public static string Format(string key, params object[] args) => Catalog.Format(key, args);

        /// <summary>The plural form for <paramref name="count"/> (schema 2), formatted with <paramref name="args"/>.</summary>
        public static string Plural(string key, long count, params object[] args) => Catalog.Plural(key, count, args);

        /// <summary>The active language's locale, e.g. "en" or "qps-ploc".</summary>
        public static string ActiveLocale => Catalog.Active.Locale;

        public static void Initialize()
        {
            var asset = Resources.Load<TextAsset>(BundledEnglishResource);
            StringTable english;
            if (asset == null)
            {
                Debug.LogError("[Localization] Bundled English table missing at Resources/" + BundledEnglishResource);
                english = new StringTable("en", "English", "missing", new StringEntry[0]);
            }
            else
            {
                english = StringTable.Parse(asset.text);
                Resources.UnloadAsset(asset);
            }

            s_catalog = new LocalizationCatalog(english);
            s_catalog.MissingKey += key => Debug.LogWarning("[Localization] Missing key: " + key);
        }

        /// <summary>
        /// Development and test helper: cycles English ↔ an in-memory pseudo-language to check +35% layouts. The
        /// language screen (<see cref="LanguageController"/>) is the player's path; it switches to the downloaded
        /// pseudo pack instead.
        /// </summary>
        public static void TogglePseudo()
        {
            var catalog = Catalog;
            if (catalog.Active.Locale == PseudoLocalizer.Locale)
            {
                catalog.ResetToFallback();
            }
            else
            {
                catalog.SetActive(PseudoLocalizer.CreateTable(catalog.Fallback));
            }
        }

        /// <summary>Test hook: forget the catalog so the next access reloads the bundled table.</summary>
        public static void ResetForTests() => s_catalog = null;
    }
}
