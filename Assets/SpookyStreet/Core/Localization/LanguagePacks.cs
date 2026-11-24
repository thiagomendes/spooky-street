using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.Localization
{
    /// <summary>A language that ships inside the app beside English (Resources), e.g. pt-BR (S004 open decision 1).</summary>
    public sealed class BuiltInLanguage
    {
        public BuiltInLanguage(string locale, string nativeName, string resource)
        {
            if (!PackManifest.IsLocaleCode(locale))
            {
                throw new FormatException($"'{locale}' is not a locale code.");
            }

            Locale = locale;
            NativeName = string.IsNullOrEmpty(nativeName) ? locale : nativeName;
            Resource = resource ?? string.Empty;
        }

        public string Locale { get; }

        public string NativeName { get; }

        /// <summary>Resources path of its string table, e.g. "Localization/pt-BR".</summary>
        public string Resource { get; }
    }

    /// <summary>
    /// Language settings shipped in the app (Resources/Localization/languages.json):
    /// { "configVersion": 1, "catalogUrl": "https://…/catalog.json",
    ///   "builtIn": [ { "locale": "pt-BR", "nativeName": "Português (Brasil)", "resource": "Localization/pt-BR" } ] }.
    /// A built-in slot whose table is not in the build is simply not offered, so D2 adds pt-BR by adding its table.
    /// To ship pt-BR as a download instead (open decision 1, option b), remove its slot and publish it to the host.
    /// </summary>
    public sealed class LanguageConfig
    {
        public LanguageConfig(string catalogUrl, IEnumerable<BuiltInLanguage> builtIn)
        {
            CatalogUrl = catalogUrl ?? string.Empty;
            BuiltIn = new List<BuiltInLanguage>(builtIn ?? new BuiltInLanguage[0]);
        }

        /// <summary>The download catalog; empty means "no downloads" (the language screen hides them).</summary>
        public string CatalogUrl { get; }

        public IReadOnlyList<BuiltInLanguage> BuiltIn { get; }

        public static LanguageConfig Parse(string json)
        {
            if (!(MiniJson.Parse(json ?? string.Empty) is Dictionary<string, object> root))
            {
                throw new FormatException("Language config must be a JSON object.");
            }

            var builtIn = new List<BuiltInLanguage>();
            if (root.TryGetValue("builtIn", out object raw) && raw is List<object> list)
            {
                foreach (object item in list)
                {
                    if (item is Dictionary<string, object> obj)
                    {
                        builtIn.Add(new BuiltInLanguage(PackManifest.Str(obj, "locale"), PackManifest.Str(obj, "nativeName"),
                            PackManifest.Str(obj, "resource")));
                    }
                }
            }

            return new LanguageConfig(PackManifest.Str(root, "catalogUrl"), builtIn);
        }
    }

    /// <summary>The bytes of one GET, or why there are none.</summary>
    public sealed class PackFetch
    {
        private PackFetch(byte[] bytes, PackError error, string detail)
        {
            Bytes = bytes;
            Error = error;
            Detail = detail ?? string.Empty;
        }

        public byte[] Bytes { get; }

        public PackError Error { get; }

        public string Detail { get; }

        public bool Ok => Error == PackError.None;

        public static PackFetch Success(byte[] bytes) => new PackFetch(bytes ?? new byte[0], PackError.None, null);

        /// <summary>A failed fetch; <paramref name="partial"/> is what arrived before it stopped, if anything.</summary>
        public static PackFetch Failure(PackError error, string detail, byte[] partial = null) =>
            new PackFetch(partial, error, detail);
    }

    /// <summary>
    /// One HTTP GET with no body, cookies or identifiers (engagement §2.6: nothing about the child is sent). The
    /// runtime uses UnityWebRequest; tests use a local or in-memory source. <paramref name="done"/> is called exactly
    /// once, possibly synchronously.
    /// </summary>
    public interface IPackTransport
    {
        void Get(string url, long maxBytes, Action<float> progress, Action<PackFetch> done);
    }

    /// <summary>URL rules: HTTPS only, and pack files only on the catalog's own host.</summary>
    public static class PackUrls
    {
        public static bool IsAllowed(string url, bool allowLoopbackHttp = false)
        {
            if (!Uri.TryCreate(url, UriKind.Absolute, out var uri))
            {
                return false;
            }

            if (uri.Scheme == Uri.UriSchemeHttps)
            {
                return true;
            }

            // Test fixtures only (a local HttpListener); never configured in a build.
            return allowLoopbackHttp && uri.Scheme == Uri.UriSchemeHttp && uri.IsLoopback;
        }

        /// <summary>The pack file's URL next to the catalog, or null when it would leave the catalog's host.</summary>
        public static string Resolve(string catalogUrl, string file, bool allowLoopbackHttp = false)
        {
            if (!IsAllowed(catalogUrl, allowLoopbackHttp) || string.IsNullOrEmpty(file) ||
                !Uri.TryCreate(new Uri(catalogUrl), file, out var resolved))
            {
                return null;
            }

            var catalog = new Uri(catalogUrl);
            if (!string.Equals(resolved.Host, catalog.Host, StringComparison.OrdinalIgnoreCase) ||
                resolved.Port != catalog.Port || resolved.Scheme != catalog.Scheme)
            {
                return null;
            }

            return resolved.AbsoluteUri;
        }
    }

    /// <summary>Picks the language at launch (S004 §2.4: first launch follows the device; language and saves are separate).</summary>
    public static class LanguageChoice
    {
        /// <summary>
        /// A language the player chose is kept while it is available (else English). Before any choice, the first
        /// device language with an available pack wins (exact tag, then the same language: "pt-PT" finds "pt-BR"),
        /// else English. The pseudo-language is never picked from the device.
        /// </summary>
        public static string Startup(string saved, bool chosen, IEnumerable<string> deviceLocales,
            IEnumerable<string> available)
        {
            var locales = new List<string>(available ?? new string[0]);
            if (chosen && !string.IsNullOrEmpty(saved))
            {
                return Match(locales, saved, exactOnly: true) ?? PreferencesDefault;
            }

            foreach (string device in deviceLocales ?? new string[0])
            {
                if (string.IsNullOrEmpty(device) || PluralRules.Language(device) == "qps")
                {
                    continue;
                }

                string match = Match(locales, device, exactOnly: false);
                if (match != null && PluralRules.Language(match) != "qps")
                {
                    return match;
                }
            }

            return PreferencesDefault;
        }

        private const string PreferencesDefault = "en";

        private static string Match(List<string> available, string wanted, bool exactOnly)
        {
            string normalized = wanted.Replace('_', '-');
            foreach (string locale in available)
            {
                if (string.Equals(locale, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    return locale;
                }
            }

            if (exactOnly)
            {
                return null;
            }

            string language = PluralRules.Language(normalized);
            foreach (string locale in available)
            {
                if (PluralRules.Language(locale) == language)
                {
                    return locale;
                }
            }

            return null;
        }
    }

    public enum LanguageSource
    {
        /// <summary>Bundled English, always present.</summary>
        English,

        /// <summary>Ships inside the app (Resources).</summary>
        BuiltIn,

        /// <summary>Downloaded and validated on this device.</summary>
        Installed,

        /// <summary>In the host's catalog, not on the device.</summary>
        Available,
    }

    /// <summary>One row of the language screen.</summary>
    public sealed class LanguageOption
    {
        internal LanguageOption(string locale, string nativeName, LanguageSource source, long size, float progress,
            PackError lastError, bool outdated)
        {
            Locale = locale;
            NativeName = nativeName;
            Source = source;
            Size = size;
            Progress = progress;
            LastError = lastError;
            Outdated = outdated;
        }

        public string Locale { get; }

        public string NativeName { get; }

        public LanguageSource Source { get; }

        /// <summary>Bytes on disk or to download (0 for languages inside the app).</summary>
        public long Size { get; }

        /// <summary>0..1 while downloading, else −1.</summary>
        public float Progress { get; }

        public bool Downloading => Progress >= 0f;

        /// <summary>Why the last download or remove of this language failed (None when it did not).</summary>
        public PackError LastError { get; }

        /// <summary>The installed copy is older than the catalog's (a download updates it).</summary>
        public bool Outdated { get; }

        /// <summary>Can be switched on now, offline included.</summary>
        public bool Usable => Source != LanguageSource.Available;

        /// <summary>Downloaded packs can be removed behind the parent gate; built-in languages cannot.</summary>
        public bool Removable => Source == LanguageSource.Installed;
    }

    /// <summary>
    /// The language packs of one device (S004 §2.4): bundled English, built-in languages, installed packs and, once
    /// a grown-up opened the download list, the host's catalog. Downloads go transport → validator → installer, one at
    /// a time; nothing is fetched unless <see cref="FetchCatalog"/> or <see cref="Download"/> is called. Main thread.
    /// </summary>
    public sealed class LanguagePacks
    {
        private readonly StringTable _english;
        private readonly PackInstaller _installer;
        private readonly IPackTransport _transport;
        private readonly bool _development;
        private readonly bool _allowLoopbackHttp;
        private readonly List<(BuiltInLanguage language, Func<string> load)> _builtIn = new List<(BuiltInLanguage, Func<string>)>();
        private readonly Dictionary<string, StringTable> _builtInTables = new Dictionary<string, StringTable>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, PackError> _errors = new Dictionary<string, PackError>(StringComparer.OrdinalIgnoreCase);
        private List<InstalledPack> _installed = new List<InstalledPack>();
        private string _downloading;
        private float _progress = -1f;

        /// <param name="builtIn">Built-in languages with a loader that returns the table's JSON, or null when absent.</param>
        public LanguagePacks(StringTable english, PackInstaller installer, IPackTransport transport, string catalogUrl,
            IEnumerable<(BuiltInLanguage language, Func<string> load)> builtIn, bool developmentBuild,
            bool allowLoopbackHttp = false)
        {
            _english = english ?? throw new ArgumentNullException(nameof(english));
            _installer = installer ?? throw new ArgumentNullException(nameof(installer));
            _transport = transport;
            CatalogUrl = catalogUrl ?? string.Empty;
            _development = developmentBuild;
            _allowLoopbackHttp = allowLoopbackHttp;
            if (builtIn != null)
            {
                _builtIn.AddRange(builtIn);
            }
        }

        /// <summary>Raised when the options change (install, remove, catalog, download progress).</summary>
        public event Action Changed;

        public string CatalogUrl { get; }

        /// <summary>Downloads are possible in this build (a host is configured and a transport exists).</summary>
        public bool DownloadsConfigured => _transport != null && PackUrls.IsAllowed(CatalogUrl, _allowLoopbackHttp);

        public PackCatalog Catalog { get; private set; }

        public bool CatalogBusy { get; private set; }

        public PackError CatalogError { get; private set; }

        public bool Busy => _downloading != null;

        public IReadOnlyList<InstalledPack> Installed => _installed;

        /// <summary>Reads the installed packs from disk (launch; works offline).</summary>
        public void LoadInstalled()
        {
            _installed = new List<InstalledPack>(_installer.Load());
            Changed?.Invoke();
        }

        public IReadOnlyList<LanguageOption> Options
        {
            get
            {
                var options = new List<LanguageOption>
                {
                    new LanguageOption(_english.Locale, _english.NativeName, LanguageSource.English, 0, -1f, PackError.None, false),
                };
                var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase) { _english.Locale };
                foreach (var (language, _) in _builtIn)
                {
                    if (!seen.Contains(language.Locale) && TableFor(language.Locale) != null)
                    {
                        seen.Add(language.Locale);
                        options.Add(new LanguageOption(language.Locale, language.NativeName, LanguageSource.BuiltIn, 0,
                            -1f, PackError.None, false));
                    }
                }

                foreach (var pack in _installed)
                {
                    if (seen.Add(pack.Locale) && (_development || !pack.Manifest.Development))
                    {
                        var listed = Catalog?.Find(pack.Locale);
                        bool outdated = listed != null && listed.Sha256 != pack.Manifest.Sha256;
                        options.Add(new LanguageOption(pack.Locale, pack.Manifest.NativeName, LanguageSource.Installed,
                            pack.Manifest.Size, ProgressOf(pack.Locale), ErrorOf(pack.Locale), outdated));
                    }
                }

                if (Catalog != null)
                {
                    foreach (var manifest in Catalog.Visible(_development))
                    {
                        if (seen.Add(manifest.Locale))
                        {
                            options.Add(new LanguageOption(manifest.Locale, manifest.NativeName, LanguageSource.Available,
                                manifest.Size, ProgressOf(manifest.Locale), ErrorOf(manifest.Locale), false));
                        }
                    }
                }

                return options;
            }
        }

        public LanguageOption Option(string locale)
        {
            foreach (var option in Options)
            {
                if (string.Equals(option.Locale, locale, StringComparison.OrdinalIgnoreCase))
                {
                    return option;
                }
            }

            return null;
        }

        /// <summary>Locales that can be switched on now (English, built-in, installed).</summary>
        public IEnumerable<string> UsableLocales
        {
            get
            {
                foreach (var option in Options)
                {
                    if (option.Usable)
                    {
                        yield return option.Locale;
                    }
                }
            }
        }

        /// <summary>The table to switch on for a usable locale, else null.</summary>
        public StringTable TableFor(string locale)
        {
            if (string.IsNullOrEmpty(locale))
            {
                return null;
            }

            if (string.Equals(locale, _english.Locale, StringComparison.OrdinalIgnoreCase))
            {
                return _english;
            }

            foreach (var pack in _installed)
            {
                if (string.Equals(pack.Locale, locale, StringComparison.OrdinalIgnoreCase) &&
                    (_development || !pack.Manifest.Development))
                {
                    return pack.Table;
                }
            }

            if (_builtInTables.TryGetValue(locale, out var cached))
            {
                return cached;
            }

            foreach (var (language, load) in _builtIn)
            {
                if (string.Equals(language.Locale, locale, StringComparison.OrdinalIgnoreCase))
                {
                    StringTable table = null;
                    try
                    {
                        string json = load?.Invoke();
                        if (json != null)
                        {
                            table = StringTable.Parse(json);
                        }
                    }
                    catch (FormatException)
                    {
                        table = null; // a broken built-in table is not offered (tests guard the shipped ones)
                    }

                    _builtInTables[language.Locale] = table;
                    return table;
                }
            }

            return null;
        }

        /// <summary>Fetches the host's catalog (call only after the parent gate). Installed packs are untouched.</summary>
        public void FetchCatalog(Action<PackError> done = null)
        {
            if (!DownloadsConfigured)
            {
                CatalogError = PackError.NotHttps;
                done?.Invoke(CatalogError);
                return;
            }

            if (CatalogBusy)
            {
                done?.Invoke(PackError.Busy);
                return;
            }

            CatalogBusy = true;
            CatalogError = PackError.None;
            Changed?.Invoke();
            _transport.Get(CatalogUrl, 256 * 1024, null, fetch =>
            {
                CatalogBusy = false;
                if (!fetch.Ok)
                {
                    CatalogError = fetch.Error;
                }
                else
                {
                    string text;
                    try
                    {
                        text = PackHash.Utf8.GetString(fetch.Bytes);
                    }
                    catch (ArgumentException)
                    {
                        text = null;
                    }

                    CatalogError = PackCatalog.TryParse(text, out var catalog);
                    if (CatalogError == PackError.None)
                    {
                        Catalog = catalog;
                    }
                }

                Changed?.Invoke();
                done?.Invoke(CatalogError);
            });
        }

        /// <summary>Downloads, validates and installs a catalog pack (call only after the parent gate).</summary>
        public void Download(string locale, Action<PackResult> done = null)
        {
            var manifest = Catalog?.Find(locale);
            if (manifest == null || (!_development && manifest.Development))
            {
                done?.Invoke(new PackResult(PackError.UnknownPack, locale));
                return;
            }

            if (Busy)
            {
                done?.Invoke(new PackResult(PackError.Busy, _downloading));
                return;
            }

            string url = PackUrls.Resolve(CatalogUrl, manifest.File, _allowLoopbackHttp);
            if (url == null || _transport == null)
            {
                Finish(manifest.Locale, new PackResult(PackError.NotHttps, manifest.File), done);
                return;
            }

            _downloading = manifest.Locale;
            _progress = 0f;
            _errors.Remove(manifest.Locale);
            Changed?.Invoke();
            _transport.Get(url, manifest.Size, p =>
            {
                _progress = Math.Max(0f, Math.Min(1f, p));
                Changed?.Invoke();
            }, fetch =>
            {
                PackResult result;
                if (!fetch.Ok)
                {
                    result = new PackResult(fetch.Error, fetch.Detail);
                }
                else
                {
                    result = _installer.Install(manifest, fetch.Bytes);
                    if (result.Ok)
                    {
                        _installed.RemoveAll(p => string.Equals(p.Locale, manifest.Locale, StringComparison.OrdinalIgnoreCase));
                        _installed.Add(result.Pack);
                    }
                }

                Finish(manifest.Locale, result, done);
            });
        }

        /// <summary>Removes a downloaded pack (behind the parent gate). The caller reverts to English if it was on.</summary>
        public PackResult Remove(string locale)
        {
            var option = Option(locale);
            if (option != null && (option.Source == LanguageSource.BuiltIn || option.Source == LanguageSource.English))
            {
                return new PackResult(PackError.BuiltIn, locale);
            }

            var result = _installer.Remove(locale);
            if (result.Ok)
            {
                _installed.RemoveAll(p => string.Equals(p.Locale, locale, StringComparison.OrdinalIgnoreCase));
                _errors.Remove(locale);
            }
            else
            {
                _errors[locale] = result.Error;
            }

            Changed?.Invoke();
            return result;
        }

        private void Finish(string locale, PackResult result, Action<PackResult> done)
        {
            _downloading = null;
            _progress = -1f;
            if (result.Ok)
            {
                _errors.Remove(locale);
            }
            else
            {
                _errors[locale] = result.Error;
            }

            Changed?.Invoke();
            done?.Invoke(result);
        }

        private float ProgressOf(string locale) =>
            _downloading != null && string.Equals(_downloading, locale, StringComparison.OrdinalIgnoreCase) ? _progress : -1f;

        private PackError ErrorOf(string locale) => _errors.TryGetValue(locale, out var error) ? error : PackError.None;
    }
}
