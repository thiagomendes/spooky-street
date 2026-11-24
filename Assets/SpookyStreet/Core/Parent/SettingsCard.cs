using System;
using System.Collections.Generic;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Save;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.Parent;
using NvKeys = SpookyStreet.Core.Localization.LocalizationKeys.NonverbalUi;

namespace SpookyStreet.Core.Parent
{
    /// <summary>A settings row: a switch over one <see cref="PreferencesData"/> field, or a slider.</summary>
    public sealed class SettingRow
    {
        internal SettingRow(string id, string labelKey, string picture, int column, int row,
            Func<PreferencesData, bool> get, Action<PreferencesData, bool> set)
        {
            Id = id;
            LabelKey = labelKey;
            Picture = picture;
            Column = column;
            Row = row;
            Get = get;
            Set = set;
        }

        internal SettingRow(string id, string labelKey, string picture, int column, int row,
            Func<PreferencesData, float> getValue, Action<PreferencesData, float> setValue)
        {
            Id = id;
            LabelKey = labelKey;
            Picture = picture;
            Column = column;
            Row = row;
            GetValue = getValue;
            SetValue = setValue;
        }

        public string Id { get; }

        public string LabelKey { get; }

        /// <summary>The row's small puppet picture.</summary>
        public string Picture { get; }

        /// <summary>Card column (0 sound and help, 1 camera and touch, 2 reading and motion) and row (1 at the top).</summary>
        public int Column { get; }

        public int Row { get; }

        public bool IsSlider => GetValue != null;

        /// <summary>A switch's value (null for a slider).</summary>
        public Func<PreferencesData, bool> Get { get; }

        public Action<PreferencesData, bool> Set { get; }

        /// <summary>A slider's position, 0–1 (null for a switch).</summary>
        public Func<PreferencesData, float> GetValue { get; }

        public Action<PreferencesData, float> SetValue { get; }

        public override string ToString() => Id;
    }

    /// <summary>
    /// The settings card behind the parent gate (S004 stream E): what each row changes, where it sits, and the card's
    /// focus graph. Every row but the Helper switch (which lives in the non-verbal settings file) is a field of
    /// <see cref="PreferencesData"/>, so it is saved with the language and volumes in <c>preferences.json</c> and
    /// survives Replay and New game. Below the rows: Language, For grown-ups and Credits.
    /// </summary>
    public static class SettingsCard
    {
        public const string Close = "close";
        public const string Helper = "helper";
        public const string Music = "music";
        public const string Sounds = "sounds";
        public const string Voices = "voices";
        public const string CameraSpeed = "camera_speed";
        public const string InvertY = "invert_y";
        public const string LeftHanded = "left_handed";
        public const string Haptics = "haptics";
        public const string ReducedMotion = "reduced_motion";
        public const string LargeCaptions = "large_captions";
        public const string OpaqueCaptions = "opaque_captions";
        public const string Language = "language";
        public const string GrownUps = "grown_ups";
        public const string Credits = "credits";

        public const int Columns = 3;
        public const int Rows = 4;

        /// <summary>The bottom row (Language, For grown-ups, Credits).</summary>
        public const int EntryRow = Rows + 1;

        /// <summary>One gamepad or key step on a slider.</summary>
        public const float SliderStep = 0.1f;

        public static readonly IReadOnlyList<string> ColumnHeadingKeys = new[] { Keys.SettingsSound, Keys.SettingsCamera, Keys.SettingsReading };

        /// <summary>The Helper switch (non-verbal settings), shown in the first row of the sound column.</summary>
        public static readonly (string Id, string LabelKey, string Picture, int Column, int Row) HelperRow =
            (Helper, NvKeys.SettingsHelper, "pg.hands", 0, 1);

        /// <summary>The preference rows, column by column.</summary>
        public static readonly IReadOnlyList<SettingRow> All = new[]
        {
            new SettingRow(Music, NvKeys.SettingsMusic, "pg.emo_tune", 0, 2, p => p.MusicVolume, (p, v) => p.MusicVolume = Clamp01(v)),
            new SettingRow(Sounds, NvKeys.SettingsSounds, "pg.knock_ring", 0, 3, p => p.EffectsVolume, (p, v) => p.EffectsVolume = Clamp01(v)),
            new SettingRow(Voices, NvKeys.SettingsVoices, "pg.emo_wow", 0, 4, p => p.DialogueVolume, (p, v) => p.DialogueVolume = Clamp01(v)),
            new SettingRow(CameraSpeed, Keys.SettingCameraSpeed, "pg.eye", 1, 1,
                p => SliderFromSensitivity(p.CameraSensitivity), (p, v) => p.CameraSensitivity = SensitivityFromSlider(v)),
            new SettingRow(InvertY, Keys.SettingInvertY, "pg.pointer", 1, 2, p => p.InvertY, (p, b) => p.InvertY = b),
            new SettingRow(LeftHanded, Keys.SettingLeftHanded, "pg.wave_hand", 1, 3, p => p.LeftHanded, (p, b) => p.LeftHanded = b),
            new SettingRow(Haptics, Keys.SettingHaptics, "pg.knock_ring", 1, 4, p => p.Haptics, (p, b) => p.Haptics = b),
            new SettingRow(ReducedMotion, Keys.SettingReducedMotion, "pg.quiet", 2, 1, p => p.ReducedMotion, (p, b) => p.ReducedMotion = b),
            new SettingRow(LargeCaptions, Keys.SettingLargeCaptions, "pg.eye", 2, 2, p => p.LargeCaptions, (p, b) => p.LargeCaptions = b),
            new SettingRow(OpaqueCaptions, Keys.SettingOpaqueCaptions, "pg.lamp", 2, 3, p => p.OpaqueCaptions, (p, b) => p.OpaqueCaptions = b),
        };

        public static readonly IReadOnlyList<(string Id, string LabelKey)> Entries = new[]
        {
            (Language, Keys.SettingsLanguage), (GrownUps, Keys.SettingsGrownUps), (Credits, Keys.SettingsCredits),
        };

        public static SettingRow Row(string id)
        {
            foreach (var row in All)
            {
                if (row.Id == id)
                {
                    return row;
                }
            }

            return null;
        }

        /// <summary>Flips a switch row on <paramref name="preferences"/>; returns the new value.</summary>
        public static bool Toggle(PreferencesData preferences, string id)
        {
            var row = Row(id);
            if (row == null || row.IsSlider || preferences == null)
            {
                throw new ArgumentException("not a switch: " + id, nameof(id));
            }

            bool next = !row.Get(preferences);
            row.Set(preferences, next);
            return next;
        }

        /// <summary>Moves a slider row one step (gamepad, keys); returns the new position.</summary>
        public static float Step(PreferencesData preferences, string id, int direction)
        {
            var row = Row(id);
            if (row == null || !row.IsSlider || preferences == null)
            {
                throw new ArgumentException("not a slider: " + id, nameof(id));
            }

            float next = Clamp01((float)Math.Round(row.GetValue(preferences) + Math.Sign(direction) * SliderStep, 2));
            row.SetValue(preferences, next);
            return row.GetValue(preferences);
        }

        /// <summary>Camera speed slider: the left half spans 0.25–1×, the right half 1–3×, so 1× sits in the middle.</summary>
        public static float SensitivityFromSlider(float slider)
        {
            slider = Clamp01(slider);
            return slider <= 0.5f ? 0.25f + slider / 0.5f * 0.75f : 1f + (slider - 0.5f) / 0.5f * 2f;
        }

        public static float SliderFromSensitivity(float sensitivity)
        {
            sensitivity = Math.Max(0.25f, Math.Min(3f, sensitivity));
            return sensitivity <= 1f ? (sensitivity - 0.25f) / 0.75f * 0.5f : 0.5f + (sensitivity - 1f) / 2f * 0.5f;
        }

        /// <summary>
        /// The card's focus graph: the close disc on top (the exit), the three columns of rows, then the entries. Left
        /// and right on a slider move the slider (the screen takes them), so the switches carry focus across columns.
        /// </summary>
        public static UiFocusGraph BuildFocus()
        {
            var graph = new UiFocusGraph("settings");
            graph.Add(HelperRow.Id, HelperRow.Row, HelperRow.Column);
            foreach (var row in All)
            {
                graph.Add(row.Id, row.Row, row.Column);
            }

            for (int i = 0; i < Entries.Count; i++)
            {
                graph.Add(Entries[i].Id, EntryRow, i);
            }

            graph.Add(Close, 0, Columns - 1);
            graph.Exit = Close;
            graph.DefaultFocus = HelperRow.Id;
            return graph;
        }

        private static float Clamp01(float v) => float.IsNaN(v) ? 0f : Math.Max(0f, Math.Min(1f, v));
    }

    /// <summary>The focus graph of a reading sheet (the grown-ups' page, credits): close, then the sheet that scrolls.</summary>
    public static class ReadingSheetFocus
    {
        public const string Close = "close";
        public const string Sheet = "sheet";

        public static UiFocusGraph Build(string screenId)
        {
            var graph = new UiFocusGraph(screenId);
            graph.Add(Sheet, 1, 0);
            graph.Add(Close, 0, 0);
            graph.Exit = Close;
            graph.DefaultFocus = Sheet;
            return graph;
        }
    }
}
