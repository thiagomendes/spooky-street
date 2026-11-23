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
    /// The audio vocabulary of cutscene cues (S003 wiring provides the sounds): music cue IDs as the audio branch names
    /// them, and babble cues as "speaker.emotion" (e.g. "bea.happy"). Data only; the runtime plays them through a hook
    /// that does nothing until the audio wiring lands.
    /// </summary>
    public static class CutsceneAudio
    {
        public const string MusicStreetTheme = "music_street_theme";
        public const string MusicReveal = "music_reveal";

        public static string Babble(CastActor speaker, BabbleEmotion emotion) =>
            speaker.ToString().ToLowerInvariant() + "." + emotion.ToString().ToLowerInvariant();

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
