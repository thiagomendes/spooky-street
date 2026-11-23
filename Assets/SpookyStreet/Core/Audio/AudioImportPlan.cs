using System;

namespace SpookyStreet.Core.Audio
{
    /// <summary>Mastering category of a sound (tools/audio/spookyaudio/master.py), which sets its import settings.</summary>
    public enum AudioCategory
    {
        Clue,
        Movement,
        Footstep,
        Notebook,
        Ui,
        Voice,
        Ambience,
        Music,
    }

    /// <summary>How a clip loads at runtime.</summary>
    public enum AudioLoad
    {
        /// <summary>Short sounds: decoded to PCM once, so playback costs no CPU and starts on the exact frame.</summary>
        DecompressOnLoad,

        /// <summary>Long beds: decoded from disk while playing, a few kilobytes of memory.</summary>
        Streaming,
    }

    /// <summary>Import settings of one clip, as plain data (applied by <c>Editor/AudioImportRules.cs</c>).</summary>
    public readonly struct AudioImportSettings
    {
        public AudioImportSettings(AudioCategory category, float vorbisQuality, AudioLoad load, bool forceMono,
            bool loadInBackground)
        {
            Category = category;
            VorbisQuality = vorbisQuality;
            Load = load;
            ForceMono = forceMono;
            LoadInBackground = loadInBackground;
        }

        public AudioCategory Category { get; }

        /// <summary>Vorbis quality, 0–1 (Unity's importer slider is 1–100 %).</summary>
        public float VorbisQuality { get; }

        public AudioLoad Load { get; }

        /// <summary>3D one-shots are summed to mono: a spatialised source plays one channel anyway.</summary>
        public bool ForceMono { get; }

        public bool LoadInBackground { get; }

        /// <summary>
        /// Every master is rendered at 48 kHz, the iPhone's hardware rate, so clips keep their rate: no runtime
        /// resampling and no second resampling pass at import.
        /// </summary>
        public int SampleRate => AudioImportPlan.SampleRate;
    }

    /// <summary>
    /// Which sounds are in the game (D20.1: the approved effects; voices and music are being replaced) and how each
    /// imports: Vorbis quality per category, mono for 3D one-shots, decompress-on-load for short sounds and streaming
    /// for the night bed. Engine-free so the rules are unit tested; the editor postprocessor only applies them.
    /// </summary>
    public static class AudioImportPlan
    {
        public const string AssetFolder = "Assets/SpookyStreet/Content/Resources/Audio/";
        public const int SampleRate = 48000;

        /// <summary>The owner-approved effects imported under <see cref="AssetFolder"/> (tools/audio/import_unity.py).</summary>
        public static readonly string[] Approved =
        {
            AudioEvents.ClueKnocksDistant, AudioEvents.ClueFloorHum, AudioEvents.ClueCamTap, AudioEvents.ClueWindWheel,
            AudioEvents.StepWood, AudioEvents.StepGrass, AudioEvents.StepStone,
            AudioEvents.MoveJump, AudioEvents.MoveLand, AudioEvents.MoveGlideOpen, AudioEvents.MoveGlideLoop,
            AudioEvents.UiPageTurn, AudioEvents.NoteStickerPick, AudioEvents.NoteStickerDrop,
            AudioEvents.NoteStickerWobble, AudioEvents.NoteCorrectGlow, AudioEvents.UiTap, AudioEvents.AmbNight,
        };

        public static bool IsApproved(string id) => Array.IndexOf(Approved, id) >= 0;

        /// <summary>File extension of the stored takes: the night bed is Ogg Vorbis, the rest are WAV masters.</summary>
        public static string FileExtension(string id) => id == AudioEvents.AmbNight ? ".ogg" : ".wav";

        /// <summary>The category the sound was mastered in (the page turn is a notebook sound in master.py).</summary>
        public static AudioCategory CategoryOf(string id)
        {
            if (id == null) throw new ArgumentNullException(nameof(id));
            if (id == AudioEvents.UiPageTurn) return AudioCategory.Notebook;
            if (id.StartsWith("clue_", StringComparison.Ordinal)) return AudioCategory.Clue;
            if (id.StartsWith("move_", StringComparison.Ordinal)) return AudioCategory.Movement;
            if (id.StartsWith("step_", StringComparison.Ordinal)) return AudioCategory.Footstep;
            if (id.StartsWith("note_", StringComparison.Ordinal)) return AudioCategory.Notebook;
            if (id.StartsWith("ui_", StringComparison.Ordinal)) return AudioCategory.Ui;
            if (id.StartsWith("voice_", StringComparison.Ordinal)) return AudioCategory.Voice;
            if (id.StartsWith("amb_", StringComparison.Ordinal)) return AudioCategory.Ambience;
            if (id.StartsWith("music_", StringComparison.Ordinal)) return AudioCategory.Music;
            throw new ArgumentException("no category for audio id " + id, nameof(id));
        }

        /// <summary>
        /// Vorbis quality per category: the clue sounds carry the case, so they get the most bits; footsteps repeat
        /// constantly under everything else and get the fewest. The bed streams at a moderate quality (it is noise).
        /// </summary>
        public static float QualityFor(AudioCategory category)
        {
            switch (category)
            {
                case AudioCategory.Clue: return 0.8f;
                case AudioCategory.Notebook: return 0.7f;
                case AudioCategory.Voice: return 0.7f;
                case AudioCategory.Music: return 0.7f;
                case AudioCategory.Movement: return 0.6f;
                case AudioCategory.Ui: return 0.6f;
                case AudioCategory.Ambience: return 0.5f;
                default: return 0.5f; // Footstep
            }
        }

        /// <summary>Settings for every take of <paramref name="def"/>.</summary>
        public static AudioImportSettings For(AudioEventDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            var category = CategoryOf(def.Id);
            bool stream = category == AudioCategory.Ambience || category == AudioCategory.Music;
            return new AudioImportSettings(category, QualityFor(category),
                stream ? AudioLoad.Streaming : AudioLoad.DecompressOnLoad,
                forceMono: def.Spatial && !def.Loop,
                loadInBackground: stream);
        }

        /// <summary>
        /// Splits an asset path such as <c>Assets/…/Audio/ui_tap/ui_tap_t2.wav</c> into its event id and take; false
        /// for anything that is not a take file in its own event folder.
        /// </summary>
        public static bool TryParse(string assetPath, out string id, out int take)
        {
            id = null;
            take = 0;
            if (assetPath == null || !assetPath.StartsWith(AssetFolder, StringComparison.Ordinal)) return false;
            string rest = assetPath.Substring(AssetFolder.Length);
            int slash = rest.IndexOf('/');
            if (slash <= 0) return false;
            string folder = rest.Substring(0, slash);
            string file = rest.Substring(slash + 1);
            int dot = file.LastIndexOf('.');
            if (dot <= 0 || file.IndexOf('/') >= 0) return false;
            string stem = file.Substring(0, dot);
            string prefix = folder + "_t";
            if (!stem.StartsWith(prefix, StringComparison.Ordinal)) return false;
            if (!int.TryParse(stem.Substring(prefix.Length), System.Globalization.NumberStyles.None,
                    System.Globalization.CultureInfo.InvariantCulture, out take) || take < 1)
            {
                take = 0;
                return false;
            }

            id = folder;
            return true;
        }
    }
}
