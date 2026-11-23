using System;

namespace SpookyStreet.Core.Cutscenes
{
    /// <summary>The babble emotions the S003 voice library offers (owner-approved cartoon babble, D20.1).</summary>
    public enum BabbleEmotion
    {
        Neutral,
        Happy,
        Surprised,
        Thinking,
        Whisper,
    }

    /// <summary>
    /// The audio vocabulary of cutscene cues: music cue IDs of the imported score (<c>VoiceMusicEvents</c>), and babble
    /// cues as "speaker.emotion" (e.g. "bea.happy") in the voice library's emotions. Data only; the runtime plays them
    /// through <c>ICutsceneAudio</c> (<c>GameCutsceneAudio</c>).
    /// </summary>
    public static class CutsceneAudio
    {
        public const string MusicStreetTheme = "music_street_theme";
        public const string MusicReveal = "music_reveal";

        public static string Babble(CastActor speaker, BabbleEmotion emotion) =>
            speaker.ToString().ToLowerInvariant() + "." + emotion.ToString().ToLowerInvariant();

        /// <summary>
        /// A babble in the emotion the speaker's line <paramref name="lineKey"/> has in play (<see cref="Audio.LineVoices"/>),
        /// so a character sounds the same in a cutscene as when the child talks to them.
        /// </summary>
        public static string BabbleAsIn(CastActor speaker, string lineKey) =>
            Babble(speaker, EmotionOf(Audio.LineVoices.For(lineKey)));

        /// <summary>The cutscene emotion of a voice emotion (the two enums share their names).</summary>
        public static BabbleEmotion EmotionOf(Audio.BabbleEmotion emotion) =>
            (BabbleEmotion)Enum.Parse(typeof(BabbleEmotion), emotion.ToString());

        /// <summary>Length of a music cue (seconds, from the imported masters), 0 for an unknown id.</summary>
        public static float MusicSeconds(string cueId) => (float)Audio.VoiceMusicEvents.MusicSeconds(cueId);

        /// <summary>Splits a babble cue ID into its speaker and emotion; false when it is not "speaker.emotion".</summary>
        public static bool TryParseBabble(string id, out CastActor speaker, out BabbleEmotion emotion)
        {
            speaker = CastActor.Pip;
            emotion = BabbleEmotion.Neutral;
            if (string.IsNullOrEmpty(id))
            {
                return false;
            }

            int dot = id.IndexOf('.');
            return dot > 0 && dot == id.LastIndexOf('.') &&
                   Enum.TryParse(id.Substring(0, dot), true, out speaker) && Enum.IsDefined(typeof(CastActor), speaker) &&
                   Enum.TryParse(id.Substring(dot + 1), true, out emotion) && Enum.IsDefined(typeof(BabbleEmotion), emotion);
        }
    }
}
