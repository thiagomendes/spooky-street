using SpookyStreet.Case;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Cutscenes;
using AudioEmotion = SpookyStreet.Core.Audio.BabbleEmotion;
using CutsceneEmotion = SpookyStreet.Core.Cutscenes.BabbleEmotion;

namespace SpookyStreet.Audio
{
    /// <summary>
    /// The cutscenes' audio hook (<see cref="ICutsceneAudio"/> on <see cref="CaseCutscenes.Audio"/>, S002.5b) played
    /// through <see cref="GameAudio"/> (docs/S003-voices-music-wiring.md §"Hooks for cutscenes"):
    /// <list type="bullet">
    /// <item><c>PlayCue</c>: <c>music_street_theme</c> restarts the street theme from its start (the opening);
    /// <c>music_reveal</c> plays the reveal over a faded loop.</item>
    /// <item><c>Babble</c>: the speaker's babble in that emotion, a short phrase take, on the Voice bus.</item>
    /// <item><c>Stop</c>: ends the reveal early (the loop comes back). Stopping a loop does nothing: the theme the
    /// opening started carries on into the game, as the score's rules say.</item>
    /// <item><c>Duck</c>: the street (the night bed) steps back while a cutscene plays; the cutscene's own music is
    /// not lowered.</item>
    /// </list>
    /// </summary>
    public sealed class GameCutsceneAudio : ICutsceneAudio
    {
        private readonly GameAudio _audio;

        public GameCutsceneAudio(GameAudio audio)
        {
            _audio = audio;
        }

        public void PlayCue(string cueId) => _audio.PlayCutsceneCue(cueId);

        public void Babble(CastActor speaker, CutsceneEmotion emotion) =>
            _audio.PlayCutsceneBabble(SpeakerOf(speaker), (AudioEmotion)(int)emotion);

        public void Stop(string cueId) => _audio.StopCutsceneCue(cueId);

        public void Duck(bool ducked) => _audio.SetCutsceneDuck(ducked);

        /// <summary>The case speaker of a cutscene actor.</summary>
        public static Speaker SpeakerOf(CastActor actor)
        {
            switch (actor)
            {
                case CastActor.June: return Speaker.June;
                case CastActor.Kit: return Speaker.Kit;
                case CastActor.Bea: return Speaker.Bea;
                case CastActor.Vale: return Speaker.Vale;
                default: return Speaker.Pip;
            }
        }
    }
}
