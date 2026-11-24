using System;
using System.Collections.Generic;
using SpookyStreet.Core.Localization;
using SpookyStreet.Core.Save;
using SpookyStreet.Save;
using UnityEngine;

namespace SpookyStreet.Localization
{
    /// <summary>
    /// The device's languages at runtime (S004 stream D1): loads the config and the installed packs at launch, picks
    /// the language (the player's choice, else the device's language when a pack matches, else English), and switches
    /// tables in the <see cref="Localizer"/> catalog. Language and saves stay independent: switching only changes
    /// <see cref="PreferencesData.Locale"/>. Downloads run only from the language screen behind the parent gate.
    /// </summary>
    public sealed class LanguageController : MonoBehaviour
    {
        public const string ConfigResource = "Localization/languages";

        private Func<PreferencesData> _preferences;
        private Action<PreferencesData> _savePreferences;

        /// <summary>Tests: serve packs from a fake or local source instead of the network.</summary>
        public static IPackTransport TransportOverride { get; set; }

        /// <summary>Tests: a catalog URL in place of the config's.</summary>
        public static string CatalogUrlOverride { get; set; }

        /// <summary>Tests: allow http://127.0.0.1 (a local HttpListener).</summary>
        public static bool AllowLoopbackHttp { get; set; }

        /// <summary>Tests: device languages in place of the OS's.</summary>
        public static string[] DeviceLocalesOverride { get; set; }

        public LanguagePacks Packs { get; private set; }

        public LanguageConfig Config { get; private set; }

        public string ActiveLocale => Localizer.ActiveLocale;

        /// <summary>Raised after the active language changed.</summary>
        public event Action LanguageChanged;

        public static void ResetOverridesForTests()
        {
            TransportOverride = null;
            CatalogUrlOverride = null;
            AllowLoopbackHttp = false;
            DeviceLocalesOverride = null;
        }

        /// <param name="saveDirectory">Where packs are installed (the save directory; packs go in its languages folder).</param>
        /// <param name="preferences">The live preferences (language is stored there, apart from case progress).</param>
        /// <param name="savePreferences">Writes changed preferences.</param>
        public static LanguageController Create(Transform parent, string saveDirectory, Func<PreferencesData> preferences,
            Action<PreferencesData> savePreferences)
        {
            var go = new GameObject("Languages");
            go.transform.SetParent(parent, false);
            var controller = go.AddComponent<LanguageController>();
            controller.Initialize(saveDirectory, preferences, savePreferences);
            return controller;
        }

        private void Initialize(string saveDirectory, Func<PreferencesData> preferences, Action<PreferencesData> save)
        {
            _preferences = preferences ?? (() => null);
            _savePreferences = save;
            Config = LoadConfig();
            var english = Localizer.Catalog.Fallback;
            IPackTransport transport = TransportOverride;
            if (transport == null)
            {
                var downloader = gameObject.AddComponent<PackDownloader>();
                downloader.AllowLoopbackHttp = AllowLoopbackHttp;
                transport = downloader;
            }

            var builtIn = new List<(BuiltInLanguage, Func<string>)>();
            foreach (var language in Config.BuiltIn)
            {
                var slot = language;
                builtIn.Add((slot, () => LoadResourceText(slot.Resource)));
            }

            var installer = new PackInstaller(new DiskSaveFileSystem(), saveDirectory ?? Application.persistentDataPath,
                english);
            Packs = new LanguagePacks(english, installer, transport, CatalogUrlOverride ?? Config.CatalogUrl, builtIn,
                Debug.isDebugBuild, AllowLoopbackHttp);
            try
            {
                Packs.LoadInstalled();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Languages] installed packs: " + e.Message);
            }

            var prefs = _preferences();
            string locale = LanguageChoice.Startup(prefs?.Locale, prefs != null && prefs.LocaleChosen, DeviceLocales(),
                Packs.UsableLocales);
            Switch(locale, chosen: false);
            Debug.Log($"[Languages] {locale} at launch; usable: {string.Join(", ", Packs.UsableLocales)}");
        }

        /// <summary>Switches to a usable language now (no gate). Returns false when it is not usable.</summary>
        public bool Use(string locale) => Switch(locale, chosen: true);

        /// <summary>Removes a downloaded pack (call behind the parent gate); the game returns to English if it was on.</summary>
        public PackResult Remove(string locale)
        {
            bool wasActive = string.Equals(ActiveLocale, locale, StringComparison.OrdinalIgnoreCase);
            var result = Packs.Remove(locale);
            if (result.Ok && wasActive)
            {
                Switch(PreferencesData.DefaultLocale, chosen: true);
            }

            return result;
        }

        /// <summary>Downloads and installs a catalog pack (call behind the parent gate). Does not switch to it.</summary>
        public void Download(string locale, Action<PackResult> done = null)
        {
            Packs.Download(locale, result =>
            {
                if (result.Ok && string.Equals(ActiveLocale, locale, StringComparison.OrdinalIgnoreCase))
                {
                    Switch(locale, chosen: false); // an update of the language in use takes effect at once
                }

                if (!result.Ok)
                {
                    Debug.LogWarning($"[Languages] download {locale}: {result}");
                }

                done?.Invoke(result);
            });
        }

        private bool Switch(string locale, bool chosen)
        {
            var table = Packs.TableFor(locale);
            if (table == null)
            {
                return false;
            }

            if (!ReferenceEquals(Localizer.Catalog.Active, table))
            {
                Localizer.Catalog.SetActive(table);
            }

            var prefs = _preferences();
            if (prefs != null && chosen)
            {
                prefs.Locale = table.Locale;
                prefs.LocaleChosen = true;
                _savePreferences?.Invoke(prefs);
            }

            LanguageChanged?.Invoke();
            return true;
        }

        private static LanguageConfig LoadConfig()
        {
            string text = LoadResourceText(ConfigResource);
            if (text != null)
            {
                try
                {
                    return LanguageConfig.Parse(text);
                }
                catch (FormatException e)
                {
                    Debug.LogError("[Languages] bad Resources/" + ConfigResource + ": " + e.Message);
                }
            }

            return new LanguageConfig(null, null);
        }

        private static string LoadResourceText(string resource)
        {
            if (string.IsNullOrEmpty(resource))
            {
                return null;
            }

            var asset = Resources.Load<TextAsset>(resource);
            if (asset == null)
            {
                return null;
            }

            string text = asset.text;
            Resources.UnloadAsset(asset);
            return text;
        }

        /// <summary>The OS language as a BCP-47 tag (Unity reports one language, no region).</summary>
        public static IEnumerable<string> DeviceLocales()
        {
            if (DeviceLocalesOverride != null)
            {
                return DeviceLocalesOverride;
            }

            switch (Application.systemLanguage)
            {
                case SystemLanguage.Portuguese: return new[] { "pt-BR" };
                case SystemLanguage.Spanish: return new[] { "es-419" };
                case SystemLanguage.French: return new[] { "fr" };
                case SystemLanguage.German: return new[] { "de" };
                case SystemLanguage.Italian: return new[] { "it" };
                case SystemLanguage.Dutch: return new[] { "nl" };
                default: return new[] { "en" };
            }
        }
    }
}
