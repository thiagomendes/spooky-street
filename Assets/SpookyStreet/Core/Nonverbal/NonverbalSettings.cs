using System;
using SpookyStreet.Core.Save;
using NvKeys = SpookyStreet.Core.Localization.LocalizationKeys.NonverbalUi;

namespace SpookyStreet.Core.Nonverbal
{
    /// <summary>
    /// Non-verbal UX settings (owner decisions D23.1 and D23.4). Settings are not case progress: they live in their
    /// own file and survive a fresh start or replay. A missing or invalid field takes its default.
    /// </summary>
    public sealed class NonverbalSettings
    {
        public const int CurrentSchemaVersion = 1;

        /// <summary>With <see cref="HelperAutoHints"/> on, the tier-3 "show me" hint may appear after this stall.</summary>
        public const float AutoHintStallSeconds = 180f;

        /// <summary>
        /// The small text line under the vignette stage. On by default; "Pictures only" turns it off. Animations
        /// always play either way (reading mode, D23.1).
        /// </summary>
        public bool ShowSpeechText { get; set; } = true;

        /// <summary>Opt-in Helper (D23.4). Off: tier-3 help plays only on a radio press, never after a stall.</summary>
        public bool HelperAutoHints { get; set; }

        /// <summary>The first-launch "Words and pictures / Pictures only" choice was made.</summary>
        public bool FirstLaunchChoiceMade { get; set; }

        /// <summary>Show the two-card first-launch choice before the title screen.</summary>
        public bool NeedsFirstLaunchChoice => !FirstLaunchChoiceMade;

        public static string ShowSpeechTextLabelKey => NvKeys.SettingsShowSpeechText;

        public static string HelperLabelKey => NvKeys.SettingsHelper;

        public NonverbalSettings Clone() => (NonverbalSettings)MemberwiseClone();

        /// <summary>One tap at first launch: <paramref name="picturesOnly"/> hides the text line.</summary>
        public void ChooseReadingMode(bool picturesOnly)
        {
            ShowSpeechText = !picturesOnly;
            FirstLaunchChoiceMade = true;
        }

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
                .Bool("showSpeechText", s.ShowSpeechText)
                .Bool("helperAutoHints", s.HelperAutoHints)
                .Bool("firstLaunchChoiceMade", s.FirstLaunchChoiceMade)
                .ToString();
        }

        /// <summary>Reads settings; false (and defaults) when the text is not a usable settings file.</summary>
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

            if (SaveJsonReader.TryBool(root, "showSpeechText", out bool b)) settings.ShowSpeechText = b;
            if (SaveJsonReader.TryBool(root, "helperAutoHints", out b)) settings.HelperAutoHints = b;
            if (SaveJsonReader.TryBool(root, "firstLaunchChoiceMade", out b)) settings.FirstLaunchChoiceMade = b;
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
        /// Loads settings, or defaults when none are readable (a first launch, or a lost file: the player is offered
        /// the reading choice again, which is harmless). A newer-schema file is left untouched.
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
