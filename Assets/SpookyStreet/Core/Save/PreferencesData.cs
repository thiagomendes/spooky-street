using System;

namespace SpookyStreet.Core.Save
{
    /// <summary>
    /// Player preferences and language (spec §5, §7), stored in their own file so replaying or losing the case
    /// save never resets them. A missing or invalid field takes its default; nothing here is progress.
    /// </summary>
    public sealed class PreferencesData
    {
        public const int CurrentSchemaVersion = 1;
        public const string DefaultLocale = "en";

        /// <summary>Active language pack locale, e.g. "en". Never keyed off display text.</summary>
        public string Locale { get; set; } = DefaultLocale;

        /// <summary>
        /// The player picked <see cref="Locale"/> on the language screen. Until then each launch follows the device's
        /// language when a matching pack is in the app or installed (S004 §2.4).
        /// </summary>
        public bool LocaleChosen { get; set; }

        public bool InvertY { get; set; }

        /// <summary>Camera orbit sensitivity multiplier, 0.25–3.</summary>
        public float CameraSensitivity { get; set; } = 1f;

        public bool ReducedMotion { get; set; }

        public bool Haptics { get; set; } = true;

        public bool LargeCaptions { get; set; }

        public bool OpaqueCaptions { get; set; }

        public bool LeftHanded { get; set; }

        public float MusicVolume { get; set; } = 1f;

        public float EffectsVolume { get; set; } = 1f;

        public float DialogueVolume { get; set; } = 1f;

        public PreferencesData Clone() => (PreferencesData)MemberwiseClone();

        public static string Serialize(PreferencesData p)
        {
            if (p == null)
            {
                throw new ArgumentNullException(nameof(p));
            }

            return new SaveJsonWriter()
                .Number("schemaVersion", CurrentSchemaVersion)
                .String("locale", p.Locale)
                .Bool("localeChosen", p.LocaleChosen)
                .Bool("invertY", p.InvertY)
                .Number("cameraSensitivity", p.CameraSensitivity)
                .Bool("reducedMotion", p.ReducedMotion)
                .Bool("haptics", p.Haptics)
                .Bool("largeCaptions", p.LargeCaptions)
                .Bool("opaqueCaptions", p.OpaqueCaptions)
                .Bool("leftHanded", p.LeftHanded)
                .Number("musicVolume", p.MusicVolume)
                .Number("effectsVolume", p.EffectsVolume)
                .Number("dialogueVolume", p.DialogueVolume)
                .ToString();
        }

        /// <summary>Reads preferences; returns false (and defaults) when the text is not a usable preferences file.</summary>
        public static bool TryRead(string json, out PreferencesData preferences, out bool newerSchema)
        {
            preferences = new PreferencesData();
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

            if (SaveJsonReader.TryString(root, "locale", out string locale) && IsLocaleCode(locale))
            {
                preferences.Locale = locale;
            }

            if (SaveJsonReader.TryBool(root, "localeChosen", out bool b)) preferences.LocaleChosen = b;
            if (SaveJsonReader.TryBool(root, "invertY", out b)) preferences.InvertY = b;
            if (SaveJsonReader.TryBool(root, "reducedMotion", out b)) preferences.ReducedMotion = b;
            if (SaveJsonReader.TryBool(root, "haptics", out b)) preferences.Haptics = b;
            if (SaveJsonReader.TryBool(root, "largeCaptions", out b)) preferences.LargeCaptions = b;
            if (SaveJsonReader.TryBool(root, "opaqueCaptions", out b)) preferences.OpaqueCaptions = b;
            if (SaveJsonReader.TryBool(root, "leftHanded", out b)) preferences.LeftHanded = b;
            if (SaveJsonReader.TryNumber(root, "cameraSensitivity", out double d)) preferences.CameraSensitivity = Clamp(d, 0.25, 3);
            if (SaveJsonReader.TryNumber(root, "musicVolume", out d)) preferences.MusicVolume = Clamp(d, 0, 1);
            if (SaveJsonReader.TryNumber(root, "effectsVolume", out d)) preferences.EffectsVolume = Clamp(d, 0, 1);
            if (SaveJsonReader.TryNumber(root, "dialogueVolume", out d)) preferences.DialogueVolume = Clamp(d, 0, 1);
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

        /// <summary>BCP-47-like: letters, digits and hyphens, 2–35 characters (e.g. "en", "pt-BR", "qps-ploc").</summary>
        private static bool IsLocaleCode(string s)
        {
            if (s.Length < 2 || s.Length > 35)
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

        private static float Clamp(double v, double min, double max) => (float)Math.Max(min, Math.Min(max, v));
    }
}
