using SpookyStreet.Core.Cutscenes;

namespace SpookyStreet.Case
{
    /// <summary>
    /// What a cutscene asks of the audio (S003 voices and music wiring provides it, roughly <c>GameAudio</c> /
    /// <c>AudioCues</c>): play a music cue ("music_street_theme", "music_reveal"), a babble line (speaker and emotion),
    /// stop a cue when the cutscene ends or is skipped, and duck the street while it plays. Kept small so this branch
    /// does not depend on the audio branch: until it lands, <see cref="SilentCutsceneAudio"/> does nothing.
    /// </summary>
    public interface ICutsceneAudio
    {
        void PlayCue(string cueId);

        void Babble(CastActor speaker, BabbleEmotion emotion);

        void Stop(string cueId);

        void Duck(bool ducked);
    }

    /// <summary>The default hook: silent until the audio wiring sets <see cref="CaseCutscenes.Audio"/>.</summary>
    public sealed class SilentCutsceneAudio : ICutsceneAudio
    {
        public static readonly SilentCutsceneAudio Instance = new SilentCutsceneAudio();

        public void PlayCue(string cueId)
        {
        }

        public void Babble(CastActor speaker, BabbleEmotion emotion)
        {
        }

        public void Stop(string cueId)
        {
        }

        public void Duck(bool ducked)
        {
        }
    }
}
