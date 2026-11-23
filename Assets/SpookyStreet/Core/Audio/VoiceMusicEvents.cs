using System;
using SpookyStreet.Core.Case;

namespace SpookyStreet.Core.Audio
{
    /// <summary>The five babble emotions every character was recorded in (tools/audio/spookyaudio/babble_spec.py).</summary>
    public enum BabbleEmotion
    {
        Neutral,
        Happy,
        Surprised,
        Thinking,
        Whisper,
    }

    /// <summary>
    /// The owner-approved v2 voices and music (D20.2), imported by <c>tools/audio/import_unity.py</c>:
    /// <list type="bullet">
    /// <item>one voice event per character and emotion, <c>voice_&lt;char&gt;_&lt;emotion&gt;</c>, whose six takes are the
    /// phrase takes p1–p6 (shortest first, 0.6–3 s; lengths in <see cref="VoiceMusicData.PhraseSeconds"/>);</item>
    /// <item>the street theme (seed 101) and exploration (seed 111) loops, the case-closed sting (seed 121) and the
    /// reveal (seed 131), one take each.</item>
    /// </list>
    /// The rejected S003 formant voices (<see cref="AudioEvents.VoicePip"/>…) and the plucked motif stay in the S003
    /// catalog for the pipeline sync, but are never imported.
    /// </summary>
    public static class VoiceMusicEvents
    {
        public const int PhrasesPerEmotion = 6;

        public const string MusicStreetTheme = "music_street_theme";
        public const string MusicExploration = "music_exploration";
        public const string MusicCaseClosed = "music_case_closed";
        public const string MusicReveal = "music_reveal";

        /// <summary>Emotion ids in <see cref="BabbleEmotion"/> order (babble_spec.EMOTIONS).</summary>
        public static readonly string[] EmotionIds = { "neutral", "happy", "surprised", "thinking", "whisper" };

        /// <summary>The music events, loops first.</summary>
        public static readonly string[] Music = { MusicStreetTheme, MusicExploration, MusicCaseClosed, MusicReveal };

        /// <summary>
        /// Music sits a little under the effects and voices (it is mastered 1 dB below the voices already): the clue
        /// sounds and the babble must stay on top on a phone speaker.
        /// </summary>
        public const float MusicTrimDb = -3f;

        /// <summary>Every voice event id, character by character (cast order), emotion by emotion.</summary>
        public static readonly string[] VoiceIds = BuildVoiceIds();

        private static string[] BuildVoiceIds()
        {
            var ids = new string[VoiceMusicData.Characters.Length * EmotionIds.Length];
            int i = 0;
            foreach (var character in VoiceMusicData.Characters)
            {
                foreach (var emotion in EmotionIds)
                {
                    ids[i++] = "voice_" + character + "_" + emotion;
                }
            }

            return ids;
        }

        /// <summary>The cast id of a speaker (<c>pip</c>, <c>bea</c>…); null for captions.</summary>
        public static string CharacterId(Speaker speaker)
        {
            switch (speaker)
            {
                case Speaker.Pip: return "pip";
                case Speaker.Bea: return "bea";
                case Speaker.Vale: return "vale";
                case Speaker.June: return "june";
                case Speaker.Kit: return "kit";
                default: return null;
            }
        }

        /// <summary>Index of a cast id in <see cref="VoiceMusicData.Characters"/>, or -1.</summary>
        public static int CharacterIndex(string characterId) =>
            characterId == null ? -1 : Array.IndexOf(VoiceMusicData.Characters, characterId);

        /// <summary>The voice event of a speaker in an emotion (null for captions).</summary>
        public static string VoiceId(Speaker speaker, BabbleEmotion emotion)
        {
            string character = CharacterId(speaker);
            return character == null ? null : "voice_" + character + "_" + EmotionIds[(int)emotion];
        }

        public static bool IsVoice(string id) => id != null && Array.IndexOf(VoiceIds, id) >= 0;

        public static bool IsMusic(string id) => id != null && Array.IndexOf(Music, id) >= 0;

        /// <summary>True for the two music loops.</summary>
        public static bool IsMusicLoop(string id) => id == MusicStreetTheme || id == MusicExploration;

        /// <summary>Exact length of a music cue in seconds (its sample count at 48 kHz); 0 for anything else.</summary>
        public static double MusicSeconds(string id)
        {
            switch (id)
            {
                case MusicStreetTheme: return VoiceMusicData.StreetThemeSamples / (double)AudioImportPlan.SampleRate;
                case MusicExploration: return VoiceMusicData.ExplorationSamples / (double)AudioImportPlan.SampleRate;
                case MusicCaseClosed: return VoiceMusicData.CaseClosedSamples / (double)AudioImportPlan.SampleRate;
                case MusicReveal: return VoiceMusicData.RevealSamples / (double)AudioImportPlan.SampleRate;
                default: return 0.0;
            }
        }

        /// <summary>Adds the v2 voices and music to a catalog (<see cref="AudioEvents.CreateCatalog"/> calls it).</summary>
        public static void AddTo(AudioCatalog catalog)
        {
            foreach (var id in VoiceIds)
            {
                catalog.Add(new AudioEventDef(id, AudioBus.Voice, PhrasesPerEmotion));
            }

            catalog.Add(new AudioEventDef(MusicStreetTheme, AudioBus.Music, 1, volumeDb: MusicTrimDb, loop: true));
            catalog.Add(new AudioEventDef(MusicExploration, AudioBus.Music, 1, volumeDb: MusicTrimDb, loop: true));
            catalog.Add(new AudioEventDef(MusicCaseClosed, AudioBus.Music, 1, volumeDb: MusicTrimDb));
            catalog.Add(new AudioEventDef(MusicReveal, AudioBus.Music, 1, volumeDb: MusicTrimDb));
        }
    }
}
