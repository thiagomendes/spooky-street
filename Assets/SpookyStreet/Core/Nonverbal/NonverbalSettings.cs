using System;
using SpookyStreet.Core.Save;
using NvKeys = SpookyStreet.Core.Localization.LocalizationKeys.NonverbalUi;

namespace SpookyStreet.Core.Nonverbal
{
    /// <summary>
    /// Non-verbal UX settings (owner decisions D23.1 and D23.4). Settings are not case progress: they live in their
    /// own file and survive a fresh start or replay. A missing or invalid field takes its default.
    /// D23.1 (revised 2026-11-22): one hybrid reading mode for everyone, so every line plays its vignette and shows its
    /// text line; there is no "Pictures only" setting and no first-launch choice. Schema 1 files (which stored
    /// <c>showSpeechText</c> and <c>firstLaunchChoiceMade</c>) still load: those two fields are ignored and dropped on
    /// the next save.
    /// </summary>
    public sealed class NonverbalSettings
    {
        /// <summary>2: the reading-mode fields are gone (D23.1). Schema 1 is read and migrated.</summary>
        public const int CurrentSchemaVersion = 2;

        /// <summary>With <see cref="HelperAutoHints"/> on, the tier-3 "show me" hint may appear after this stall.</summary>
        public const float AutoHintStallSeconds = 180f;

        /// <summary>Opt-in Helper (D23.4). Off: tier-3 help plays only on a radio press, never after a stall.</summary>
        public bool HelperAutoHints { get; set; }

        public static string HelperLabelKey => NvKeys.SettingsHelper;

        public NonverbalSettings Clone() => (NonverbalSettings)MemberwiseClone();

        /// <summary>Whether the tier-3 ghost hand may show without a radio press after <paramref name="stallSeconds"/>.</summary>
        public bool MayAutoShowTier3(float stallSeconds) => HelperAutoHints && stallSeconds >= AutoHintStallSeconds;

        public static string Serialize(NonverbalSettings s)
        {
            if (s == null)
            {
                throw new ArgumentNullException(nameof(s));
            }

            return new SaveJsonWriter()
                .Number("schemaVersion", CurrentSchemaVersion)
                .Bool("helperAutoHints", s.HelperAutoHints)
                .ToString();
        }

        /// <summary>
        /// Reads settings; false (and defaults) when the text is not a usable settings file. Schema 1 loads too; its
        /// retired reading-mode fields are ignored.
        /// </summary>
        public static bool TryRead(string json, out NonverbalSettings settings, out bool newerSchema)
        {
            settings = new NonverbalSettings();
            newerSchema = false;
            var root = SaveJsonReader.ParseObject(json);
            if (root == null || !SaveJsonReader.TryInt(root, "schemaVersion", out int version) || version < 1)
            {
                return false;
            }

            if (version > CurrentSchemaVersion)
            {
                newerSchema = true;
                return false;
            }

            if (SaveJsonReader.TryBool(root, "helperAutoHints", out bool b)) settings.HelperAutoHints = b;
            return true;
        }

        public static SaveCheck Check(string json)
        {
            if (TryRead(json, out _, out bool newer))
            {
                return SaveCheck.Valid;
            }

            return newer ? SaveCheck.Newer : SaveCheck.Invalid;
        }
    }

    /// <summary>
    /// "nonverbal-settings.json": the same atomic write (temp, replace, previous copy) and recovery as every save
    /// slot, separate from both the case save and <see cref="PreferencesStore"/>.
    /// </summary>
    public sealed class NonverbalSettingsStore
    {
        private readonly SaveSlotStore _slot;

        public NonverbalSettingsStore(ISaveFileSystem fs, string directory)
        {
            _slot = new SaveSlotStore(fs, PathsFor(directory), NonverbalSettings.Check);
        }

        public static SaveSlotPaths PathsFor(string directory) => new SaveSlotPaths(directory, "nonverbal-settings");

        public SaveSlotPaths Paths => _slot.Paths;

        /// <summary>The last load's status (Empty means a first launch).</summary>
        public SaveLoadStatus LastLoadStatus { get; private set; } = SaveLoadStatus.Empty;

        /// <summary>
        /// Loads settings, or defaults when none are readable (a first launch, or a lost file: Helper is off again,
        /// which is harmless). A newer-schema file is left untouched.
        /// </summary>
        public NonverbalSettings Load()
        {
            var load = _slot.Load();
            LastLoadStatus = load.Status;
            if (load.HasContents && NonverbalSettings.TryRead(load.Contents, out var settings, out _))
            {
                return settings;
            }

            return new NonverbalSettings();
        }

        /// <summary>Writes settings after a change. A newer-schema file is moved aside first (settings are not progress).</summary>
        public void Save(NonverbalSettings settings)
        {
            if (_slot.WritesBlocked)
            {
                _slot.PreserveLatestAndUnblock();
            }

            _slot.Write(NonverbalSettings.Serialize(settings));
        }
    }
}
