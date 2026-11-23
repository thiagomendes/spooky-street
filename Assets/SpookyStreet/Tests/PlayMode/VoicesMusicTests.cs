using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Audio;
using SpookyStreet.Case;
using SpookyStreet.Core.Audio;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Save;
using SpookyStreet.Core.World;
using SpookyStreet.Localization;
using SpookyStreet.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using CaseLines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// The v2 voices and music in the real scene (D20.2, docs/S003-voices-music-wiring.md), read through the rig's
    /// event log and source state (batchmode has no audio device): a line plays its speaker's babble in the line's
    /// emotion and the planner's take, music and the bed duck under it, the next line and Skip stop it, Pip's examine
    /// lines use Pip's voice; the theme starts on the title and the street, holds under the pause, follows the music
    /// slider and switches to the lower exploration loop indoors; the sting plays when the "Case closed" card shows;
    /// the cutscene hooks (PlayCue, DuckMusic, StopCue, StopMusic, ResumeMusic, babble ids) work. Every test runs
    /// against a fresh save folder.
    /// </summary>
    public class VoicesMusicTests : InputTestFixture
    {
        private const string SceneName = "LanternLane";

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            Localizer.ResetForTests();
            CaseDirector.UseTemporarySaveDirectory();
            AmbientMotion.ReducedMotion = false;
        }

        public override void TearDown()
        {
            AmbientMotion.ReducedMotion = false;
            base.TearDown();
        }

        private static GameBootstrap Game => GameBootstrap.Instance;
        private static CaseDirector Director => GameBootstrap.Instance.Director;
        private static GameAudio Audio => GameBootstrap.Instance.Audio;
        private static AudioRig Rig => GameBootstrap.Instance.Audio.Rig;

        private static IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(GameBootstrap.Instance, "scene did not create GameBootstrap");
            Assert.IsNotNull(GameBootstrap.Instance.Audio, "the game builds its audio");
        }

        private static IEnumerator StartGame()
        {
            yield return LoadGame();
            Game.Ui.NewGameButton.onClick.Invoke();
            yield return Frames(10);
            Assert.AreEqual(GameState.Playing, Game.Flow.State);
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until)
            {
                yield return null;
            }
        }

        private static int Plays(string id) => Rig.CountPlays(id);

        private static int Stops(string id)
        {
            int n = 0;
            foreach (var e in Rig.Log)
            {
                if (e.Kind == AudioLogKind.Stop && e.EventId == id) n++;
            }

            return n;
        }

        private static AudioLogEntry? LastPlay(string id)
        {
            var log = Rig.Log;
            for (int i = log.Count - 1; i >= 0; i--)
            {
                if (log[i].Kind == AudioLogKind.Play && log[i].EventId == id) return log[i];
            }

            return null;
        }

        private static int Count(AudioLogKind kind)
        {
            int n = 0;
            foreach (var e in Rig.Log)
            {
                if (e.Kind == kind) n++;
            }

            return n;
        }

        [UnityTest]
        public IEnumerator ALinePlaysItsSpeakersBabbleOfTheRightEmotionAndStopsOnSkip()
        {
            yield return StartGame();
            yield return Wait(MusicPlan.FirstFadeInSeconds + 0.3f); // the theme has faded in
            var theme = Rig.SourceOf(VoiceMusicEvents.MusicStreetTheme);
            Assert.IsNotNull(theme, "the theme plays on the street");
            float open = theme.volume;
            Assert.Greater(open, 0f);
            Rig.ClearLog();

            // Bea's first line: surprised, the phrase take the planner picks for its caption.
            Director.Perform(CaseAction.Talk(Speaker.Bea));
            yield return null;
            Assert.IsTrue(Director.Dialogue.IsOpen);
            Assert.AreEqual(CaseLines.BeaSawCape, Director.Dialogue.CurrentLine.Key);
            var expected = BabblePlanner.Plan(Speaker.Bea, Localizer.Get(CaseLines.BeaSawCape), BabbleEmotion.Surprised);
            var play = LastPlay("voice_bea_surprised");
            Assert.IsTrue(play.HasValue, "Bea's surprised babble plays with her line");
            Assert.AreEqual(AudioBus.Voice, play.Value.Bus);
            Assert.AreEqual(expected.Take, play.Value.Take, "the phrase take closest to the caption's length");
            Assert.IsFalse(play.Value.Spatial);
            Assert.AreEqual("voice_bea_surprised", Audio.VoiceEvent);
            Assert.IsTrue(Rig.VoiceActive);

            // Music and the bed dip under the voice.
            yield return Wait(0.3f);
            Assert.AreEqual(1, Count(AudioLogKind.Duck));
            Assert.AreEqual(AudioMix.VoiceDuckDb, Rig.DuckDb, 0.01f);
            Assert.AreEqual(open * AudioMix.DbToLinear(AudioMix.VoiceDuckDb), theme.volume, 1e-3f, "the theme ducks");
            var bed = Rig.SourceOf(AudioEvents.AmbNight);
            Assert.IsNotNull(bed);
            Assert.AreEqual(AudioMix.DbToLinear(AudioMix.VoiceDuckDb), bed.volume, 1e-3f, "the bed ducks too");

            // Skip stops the babble and the duck lets go.
            Director.Dialogue.Skip();
            yield return null;
            Assert.IsFalse(Director.Dialogue.IsOpen);
            Assert.AreEqual(1, Stops("voice_bea_surprised"), "skipping the line stops its babble");
            Assert.IsFalse(Rig.IsPlaying("voice_bea_surprised"));
            Assert.IsFalse(Rig.VoiceActive);
            Assert.IsNull(Audio.VoiceEvent);
            yield return Wait(1.0f);
            Assert.AreEqual(1, Count(AudioLogKind.Unduck));
            Assert.AreEqual(0f, Rig.DuckDb, 1e-3f);
            Assert.AreEqual(open, theme.volume, 1e-3f, "the theme comes back up");

            // Vale's two lines: the next line stops the first babble and plays its own.
            Rig.ClearLog();
            Director.Perform(CaseAction.Talk(Speaker.Vale));
            yield return null;
            Assert.AreEqual(CaseLines.ValeDoorShut, Director.Dialogue.CurrentLine.Key);
            Assert.AreEqual(1, Plays("voice_vale_neutral"));
            Director.Dialogue.Advance();
            yield return null;
            Assert.AreEqual(CaseLines.ValeInviteSit, Director.Dialogue.CurrentLine.Key);
            Assert.AreEqual(2, Plays("voice_vale_neutral"), "the next line plays its own babble");
            Assert.AreEqual(1, Stops("voice_vale_neutral"), "after stopping the last one");
            Director.Dialogue.Advance();
            yield return null;
            Assert.IsFalse(Director.Dialogue.IsOpen);
            Assert.AreEqual(2, Stops("voice_vale_neutral"), "closing stops it");

            // Pip's own examine line uses Pip's voice; a caption has none.
            Rig.ClearLog();
            Director.Perform(CaseAction.Of(CaseActionKind.ExamineCloth));
            yield return null;
            Assert.AreEqual(1, Plays("voice_pip_thinking"), "Pip musing over the silver cloth");
            Director.Dialogue.Skip();
            yield return null;
            int voices = 0;
            foreach (var e in Rig.Log)
            {
                if (e.Kind == AudioLogKind.Play && e.Bus == AudioBus.Voice) voices++;
            }

            Assert.AreEqual(1, voices, "one babble per spoken line, none for captions");
        }

        [UnityTest]
        public IEnumerator MusicStartsOnTheStreetHoldsThroughPauseAndSwitchesIndoors()
        {
            yield return LoadGame();
            yield return Frames(3);
            Assert.AreEqual(MusicPlace.Title, Audio.MusicPlace);
            Assert.AreEqual(1, Plays(VoiceMusicEvents.MusicStreetTheme), "the street theme plays on the title");
            Assert.AreEqual(VoiceMusicEvents.MusicStreetTheme, Audio.Music.Loop);
            var entry = LastPlay(VoiceMusicEvents.MusicStreetTheme).Value;
            Assert.AreEqual(AudioBus.Music, entry.Bus);
            Assert.IsTrue(Rig.IsLooping(VoiceMusicEvents.MusicStreetTheme));

            Game.Ui.NewGameButton.onClick.Invoke();
            yield return Frames(10);
            Assert.AreEqual(MusicPlace.Street, Audio.MusicPlace);
            Assert.AreEqual(1, Plays(VoiceMusicEvents.MusicStreetTheme), "the theme carries on into the game");
            yield return Wait(MusicPlan.FirstFadeInSeconds + 0.2f);
            var theme = Rig.SourceOf(VoiceMusicEvents.MusicStreetTheme);
            float open = theme.volume;

            // The pause keeps the music playing, a little lower.
            Game.Flow.Pause();
            yield return Wait(0.8f);
            Assert.IsFalse(Rig.IsBusPaused(AudioBus.Music));
            Assert.IsFalse(Rig.IsPaused(theme), "music carries on under the pause card");
            Assert.AreEqual(MusicPlan.PausedTrimDb, Rig.BusTrimDb(AudioBus.Music), 0.01f);
            Game.Flow.Resume();
            yield return Wait(0.8f);
            Assert.AreEqual(0f, Rig.BusTrimDb(AudioBus.Music), 0.01f);
            Assert.AreEqual(open, theme.volume, 1e-3f);

            // The music slider drives the real music now.
            var prefs = new PreferencesData { MusicVolume = 0.5f, EffectsVolume = 1f, DialogueVolume = 1f };
            Audio.SetPreferences(prefs);
            Assert.AreEqual(open * AudioMix.DbToLinear(AudioMix.SliderToDb(0.5f)), theme.volume, 1e-3f);
            Audio.SetPreferences(new PreferencesData());
            Assert.AreEqual(open, theme.volume, 1e-3f);

            if (Game.Environment == null || !Game.Environment.UsesModels || Director.Travel == null)
            {
                Assert.Ignore("Environment models are not committed: the house is not staged.");
            }

            // In by June's window (GameAudioTests): the theme crosses into the lower exploration loop.
            var travel = Director.Travel;
            Game.Pip.Teleport(new Vector3(-4.3f, 2.55f, CaseRouteLayout.ExteriorWindowZ));
            yield return Wait(0.8f);
            Rig.ClearLog();
            Game.Pip.Teleport(new Vector3(-3.36f, 2.55f, CaseRouteLayout.ExteriorWindowZ));
            float until = Time.time + 1f;
            while (!travel.Active && Time.time < until) yield return null;
            until = Time.time + 2.5f;
            while (travel.Active && Time.time < until) yield return null;
            yield return Wait(0.6f);
            Assert.IsTrue(Audio.Indoors);
            Assert.AreEqual(MusicPlace.Indoors, Audio.MusicPlace);
            Assert.AreEqual(VoiceMusicEvents.MusicExploration, Audio.Music.Loop, "exploration indoors");
            Assert.AreEqual(1, Plays(VoiceMusicEvents.MusicExploration));
            Assert.AreEqual(1, Stops(VoiceMusicEvents.MusicStreetTheme), "the theme fades out");
            Assert.AreEqual(MusicPlan.IndoorTrimDb, Rig.BusTrimDb(AudioBus.Music), 0.01f, "and the music sits lower");
            Assert.IsTrue(Rig.IsLooping(VoiceMusicEvents.MusicExploration));
            Assert.IsFalse(Rig.IsLooping(VoiceMusicEvents.MusicStreetTheme));
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator TheStingPlaysWhenTheCaseClosedCardShows()
        {
            yield return StartGame();
            foreach (var action in new[]
                     {
                         CaseAction.Of(CaseActionKind.HearKnocks), CaseAction.Talk(Speaker.Vale),
                         CaseAction.Of(CaseActionKind.ExamineCloth),
                         CaseAction.Of(CaseActionKind.EnterAttic), CaseAction.Of(CaseActionKind.ExamineSill),
                         CaseAction.Talk(Speaker.June), CaseAction.Of(CaseActionKind.EnterFoyer),
                         CaseAction.Of(CaseActionKind.LiftLatch), CaseAction.Of(CaseActionKind.ExamineGrille),
                         CaseAction.Of(CaseActionKind.LinkArm), CaseAction.Of(CaseActionKind.TurnCrank),
                         CaseAction.Of(CaseActionKind.ArriveAtPorch),
                     })
            {
                Director.Perform(action);
                Director.Dialogue.Skip();
            }

            yield return Wait(FinaleView.IntroSeconds + 0.3f);
            Director.Dialogue.Skip();
            yield return Wait(FinaleView.HoldAfterLines + 0.4f);
            var notebook = Director.Notebook;
            Assert.IsTrue(notebook.IsOpen, "Kit asks; the notebook opens on an open question");
            notebook.Close();
            yield return Frames(2);
            Rig.ClearLog();

            Assert.IsNotNull(Director.SolveQuestion(CaseNotebook.QuestionSource, new[] { CaseNotebook.FloorHum, CaseNotebook.WoodenPiece }));
            Assert.IsNotNull(Director.SolveQuestion(CaseNotebook.QuestionEntry,
                new[] { CaseNotebook.ValeDoor, CaseNotebook.Cloth, CaseNotebook.SillSnag }));
            Assert.IsFalse(Audio.StingPending, "no stamp yet");

            // The last deduction is made in the notebook (the reveal waits for the notebook to close).
            Director.OpenNotebook();
            yield return Frames(2);
            Assert.IsTrue(notebook.IsOpen);
            Assert.IsNotNull(Director.SolveQuestion(CaseNotebook.QuestionMoved, new[] { CaseNotebook.LooseArm, CaseNotebook.OwlBooth }));
            Assert.IsTrue(Director.State.Has(CaseFact.CaseStamped));
            Assert.IsTrue(Audio.StingPending, "the stamp's sting waits for its card");
            notebook.Rebuild();
            yield return Frames(2);
            Assert.IsNotNull(notebook.FinalCard);
            notebook.ShowPage(notebook.PageCount - 1);
            yield return Frames(2);
            Assert.IsTrue(Audio.StampCardShowing);
            Assert.AreEqual(1, Plays(VoiceMusicEvents.MusicCaseClosed), "the sting plays at the Case closed card");
            Assert.AreEqual(AudioBus.Music, LastPlay(VoiceMusicEvents.MusicCaseClosed).Value.Bus);
            Assert.AreEqual(VoiceMusicEvents.MusicCaseClosed, Audio.Music.Cue);
            Assert.IsNull(Audio.Music.Loop, "the loop fades out under it");
            Assert.IsFalse(Audio.StingPending);

            // Turning back to the card plays nothing more.
            notebook.ShowPage(0);
            yield return Frames(2);
            notebook.ShowPage(notebook.PageCount - 1);
            yield return Frames(2);
            Assert.AreEqual(1, Plays(VoiceMusicEvents.MusicCaseClosed), "once per stamp");

            // Closing the notebook frees the screen: the reveal cutscene plays the reveal cue over the sting, with
            // Kit's and June's babble, holds the street back, and the theme comes back after it.
            notebook.Close();
            float until = Time.unscaledTime + 5f;
            while (!Director.Cutscenes.Playing && Time.unscaledTime < until) yield return null;
            Assert.IsTrue(Director.Cutscenes.Playing, "the reveal plays once the notebook closes");
            yield return Frames(2);
            Assert.AreEqual(1, Plays(VoiceMusicEvents.MusicReveal), "the cutscene's reveal cue");
            Assert.AreEqual(1, Stops(VoiceMusicEvents.MusicCaseClosed), "it replaces the sting");
            Assert.AreEqual(VoiceMusicEvents.MusicReveal, Audio.Music.Cue);
            Assert.IsTrue(Audio.CutsceneDucked);
            until = Time.unscaledTime + 60f;
            while (Director.Cutscenes.Playing && Time.unscaledTime < until) yield return null;
            Assert.IsFalse(Director.Cutscenes.Playing);
            Assert.AreEqual(1, Plays("voice_kit_happy"), "kit.happy at the reopening");
            Assert.AreEqual(1, Plays("voice_june_happy"), "june.happy at the reopening");
            yield return Frames(3);
            Assert.IsFalse(Audio.CutsceneDucked, "the street comes back");
            Assert.IsNull(Audio.Music.Cue);
            Assert.AreEqual(VoiceMusicEvents.MusicStreetTheme, Audio.Music.Loop, "the theme comes back after the reveal");
            Assert.IsTrue(Rig.IsLooping(VoiceMusicEvents.MusicStreetTheme));
            Assert.AreEqual(1, Plays(VoiceMusicEvents.MusicCaseClosed), "the sting never comes back late");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator TheOpeningPlaysTheStreetThemeAndBeasBabble()
        {
            CaseDirector.UseTemporarySaveDirectory(playOpening: true);
            yield return LoadGame();
            yield return Frames(3);
            Assert.AreEqual(1, Plays(VoiceMusicEvents.MusicStreetTheme), "the theme on the title");
            Assert.IsInstanceOf<GameCutsceneAudio>(Director.Cutscenes.Audio, "the cutscenes play through the game's audio");
            Game.Ui.NewGameButton.onClick.Invoke();
            yield return Frames(3);
            Assert.IsTrue(Director.Cutscenes.Playing, "the opening plays");
            Assert.AreEqual(2, Plays(VoiceMusicEvents.MusicStreetTheme), "the opening restarts the theme from its start");
            Assert.AreEqual(1, Stops(VoiceMusicEvents.MusicStreetTheme));
            Assert.IsTrue(Audio.CutsceneDucked);
            yield return Wait(1f);
            Assert.AreEqual(AudioMix.AmbienceTrimDb(false) + GameAudio.CutsceneBedDuckDb, Rig.BusTrimDb(AudioBus.Ambience), 0.01f,
                "the street steps back");
            Assert.AreEqual(0f, Rig.BusTrimDb(AudioBus.Music), 0.01f, "the cutscene's own music is not lowered");

            float until = Time.unscaledTime + 90f;
            while (Director.Cutscenes.Playing && Time.unscaledTime < until) yield return null;
            Assert.IsFalse(Director.Cutscenes.Playing);
            Assert.AreEqual(1, Plays("voice_bea_happy"), "bea.happy in the opening");
            yield return Wait(1f);
            Assert.IsFalse(Audio.CutsceneDucked);
            Assert.AreEqual(0f, Rig.BusTrimDb(AudioBus.Ambience), 0.01f);
            Assert.AreEqual(2, Plays(VoiceMusicEvents.MusicStreetTheme), "the theme carries on into play");
            Assert.IsTrue(Rig.IsLooping(VoiceMusicEvents.MusicStreetTheme));
        }

        [UnityTest]
        public IEnumerator TheRevealCueApiWorks()
        {
            yield return StartGame();
            yield return Wait(0.5f);
            Rig.ClearLog();

            // The cutscene placeholder id plays the reveal over a faded loop.
            Assert.IsTrue(AudioCues.PlayCue("music.c01.reveal"));
            yield return null;
            Assert.AreEqual(1, Plays(VoiceMusicEvents.MusicReveal));
            Assert.AreEqual(VoiceMusicEvents.MusicReveal, Audio.Music.Cue);
            Assert.AreEqual(1, Stops(VoiceMusicEvents.MusicStreetTheme), "the theme fades under the reveal");
            Assert.IsNotNull(Rig.SourceOf(VoiceMusicEvents.MusicReveal));

            // A held duck lowers the music bus; letting go brings it back.
            AudioCues.DuckMusic(true);
            yield return Wait(1.2f);
            Assert.IsTrue(Audio.MusicHeldDown);
            Assert.AreEqual(MusicPlan.HeldDuckDb, Rig.BusTrimDb(AudioBus.Music), 0.01f);
            AudioCues.DuckMusic(false);
            yield return Wait(1.2f);
            Assert.AreEqual(0f, Rig.BusTrimDb(AudioBus.Music), 0.01f);

            // Stop ends the reveal and the loop comes back.
            AudioCues.StopCue();
            yield return null;
            Assert.AreEqual(1, Stops(VoiceMusicEvents.MusicReveal));
            Assert.IsNull(Audio.Music.Cue);
            Assert.AreEqual(1, Plays(VoiceMusicEvents.MusicStreetTheme), "the street loop comes back in");

            // Silence holds until resumed.
            AudioCues.StopMusic(0.2f);
            yield return Wait(0.5f);
            Assert.IsTrue(Audio.Music.Silenced);
            Assert.IsFalse(Rig.IsLooping(VoiceMusicEvents.MusicStreetTheme));
            Assert.IsFalse(Rig.IsLooping(VoiceMusicEvents.MusicExploration));
            AudioCues.ResumeMusic();
            yield return Frames(2);
            Assert.IsFalse(Audio.Music.Silenced);
            Assert.IsTrue(Rig.IsLooping(VoiceMusicEvents.MusicStreetTheme));

            // A babble placeholder plays the speaker's babble.
            Assert.IsTrue(AudioCues.PlayCue("babble.c01.bea.hello"));
            yield return null;
            Assert.AreEqual(1, Plays("voice_bea_happy"));
            Assert.AreEqual(BabbleEmotion.Happy, Audio.LastBabble.Emotion);
            Assert.IsFalse(AudioCues.PlayCue("music.c01.unknown"));
        }
    }
}
