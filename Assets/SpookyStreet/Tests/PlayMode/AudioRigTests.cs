using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Audio;
using SpookyStreet.Core.Audio;
using SpookyStreet.Core.Save;
using UnityEngine;
using UnityEngine.TestTools;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// The code-built mixer (S003 foundation) with generated stand-in clips: takes rotate without repeats, bus volumes
    /// follow the exposed parameters and the saved sliders, spatial events play in 3D, loops loop, rapid repeats are
    /// throttled, and a missing clip is skipped quietly; the pause holds game sound but not the UI tap, the bus
    /// shaping trims and filters the bed, loops fade out, and every play is logged (batchmode has no audio device).
    /// </summary>
    public class AudioRigTests
    {
        private AudioRig rig;
        private FakeClips clips;

        private sealed class FakeClips : IAudioClipSource
        {
            public readonly List<string> Loaded = new List<string>();
            public readonly HashSet<string> Missing = new HashSet<string>();

            public AudioClip Load(string resourcePath)
            {
                if (Missing.Contains(resourcePath)) return null;
                Loaded.Add(resourcePath);
                // Voices last 2 s, so a duck can be read while one plays; everything else 0.1 s.
                int samples = resourcePath.Contains("/voice_") ? 96000 : 4800;
                return AudioClip.Create(resourcePath, samples, 1, 48000, false);
            }
        }

        [SetUp]
        public void SetUp()
        {
            clips = new FakeClips();
            rig = AudioRig.Create(AudioEvents.CreateCatalog(), clips, 11);
        }

        [TearDown]
        public void TearDown()
        {
            if (rig != null) Object.DestroyImmediate(rig.gameObject);
        }

        [Test]
        public void TakesRotateWithoutRepeats()
        {
            string last = null;
            var seen = new HashSet<string>();
            for (var i = 0; i < 10; i++)
            {
                var source = rig.Play(AudioEvents.UiPageTurn);
                Assert.IsNotNull(source);
                StringAssert.StartsWith("Audio/ui_page_turn/ui_page_turn_t", source.clip.name);
                Assert.AreNotEqual(last, source.clip.name);
                last = source.clip.name;
                seen.Add(source.clip.name);
                Assert.That(source.pitch, Is.InRange(0.98f, 1.02f));
            }

            Assert.AreEqual(4, seen.Count, "all four takes get used");
            Assert.AreEqual(4, clips.Loaded.Count, "clips are cached");
        }

        [Test]
        public void BusVolumesFollowParametersAndPreferences()
        {
            Assert.AreEqual(1f, rig.Play(AudioEvents.NoteStickerWobble).volume, 1e-5f);
            rig.SetVolume(AudioMix.SfxVol, -12f);
            Assert.AreEqual(-12f, rig.GetVolume("SFXVol"));
            Assert.AreEqual(AudioMix.DbToLinear(-12f), rig.Play(AudioEvents.ClueCamTap).volume, 1e-4f);

            rig.ApplyPreferences(new PreferencesData { MusicVolume = 0f, EffectsVolume = 0.5f, DialogueVolume = 1f });
            Assert.AreEqual(0f, rig.Play(AudioEvents.MusicMotifPluck).volume);
            Assert.AreEqual(AudioMix.DbToLinear(AudioMix.SliderToDb(0.5f)), rig.Play(AudioEvents.MoveJump).volume, 1e-4f);
            Assert.AreEqual(AudioMix.SliderToDb(0.5f), rig.GetVolume(AudioMix.AmbienceVol), 1e-4f);
            Assert.AreEqual(1f, rig.Play(AudioEvents.VoiceBea).volume, 1e-5f);
        }

        [Test]
        public void SpatialEventsPlayIn3DAndLoopsLoop()
        {
            var knocks = rig.PlayAt(AudioEvents.ClueKnocksDistant, new Vector3(4f, 2f, 9f));
            Assert.AreEqual(1f, knocks.spatialBlend);
            Assert.AreEqual(new Vector3(4f, 2f, 9f), knocks.transform.position);
            Assert.IsFalse(knocks.loop);

            var tap = rig.PlayAt(AudioEvents.UiTap, new Vector3(1f, 0f, 0f));
            Assert.AreEqual(0f, tap.spatialBlend, "UI is never spatial");

            var bed = rig.Play(AudioEvents.AmbNight);
            Assert.IsTrue(bed.loop);
            Assert.AreEqual(0f, bed.spatialBlend);
            rig.Stop(AudioEvents.AmbNight);
            Assert.IsFalse(bed.isPlaying);
        }

        [Test]
        public void RapidRepeatsAreThrottledAndMissingClipsSkipped()
        {
            Assert.IsNotNull(rig.Play(AudioEvents.StepWood));
            Assert.IsNull(rig.Play(AudioEvents.StepWood), "a second step within 80 ms is dropped");

            for (var take = 1; take <= 5; take++) clips.Missing.Add("Audio/step_grass/step_grass_t" + take);
            Assert.IsNull(rig.Play(AudioEvents.StepGrass));
            Assert.IsNull(rig.Play("no_such_event"));
        }

        [Test]
        public void PauseHoldsGameSoundButTheUiTapStillPlays()
        {
            var loop = rig.Play(AudioEvents.MoveGlideLoop);
            var bed = rig.Play(AudioEvents.AmbNight);
            Assert.IsFalse(rig.IsBusPaused(AudioBus.Sfx));
            rig.SetGamePaused(true);
            Assert.IsTrue(rig.GamePaused);
            Assert.IsTrue(rig.IsBusPaused(AudioBus.Sfx));
            Assert.IsTrue(rig.IsBusPaused(AudioBus.Ambience));
            Assert.IsTrue(rig.IsBusPaused(AudioBus.Voice));
            Assert.IsFalse(rig.IsBusPaused(AudioBus.Music), "music carries on under the pause card");
            Assert.IsTrue(rig.IsPaused(loop), "the glide loop holds");
            Assert.IsTrue(rig.IsPaused(bed), "the bed holds");
            Assert.IsTrue(rig.IsLooping(AudioEvents.MoveGlideLoop), "held, not stopped");
            Assert.IsNull(rig.Play(AudioEvents.StepGrass), "no game sound starts while paused");
            var tap = rig.Play(AudioEvents.UiTap);
            Assert.IsNotNull(tap, "the pause menu's buttons still tap");
            Assert.IsTrue(tap.ignoreListenerPause);
            Assert.AreNotSame(loop, tap, "a held loop is never stolen");
            Assert.IsFalse(loop.ignoreListenerPause);

            rig.SetGamePaused(false);
            Assert.IsFalse(rig.IsPaused(loop));
            Assert.IsFalse(rig.IsBusPaused(AudioBus.Sfx));
            Assert.IsNotNull(rig.Play(AudioEvents.StepGrass));
            int pauses = 0, resumes = 0;
            foreach (var entry in rig.Log)
            {
                if (entry.Kind == AudioLogKind.Pause && entry.Bus == AudioBus.Sfx) pauses++;
                if (entry.Kind == AudioLogKind.Resume && entry.Bus == AudioBus.Sfx) resumes++;
            }

            Assert.AreEqual(1, pauses);
            Assert.AreEqual(1, resumes);
        }

        [Test]
        public void BusShapingTrimsAndFiltersOneBus()
        {
            var bed = rig.Play(AudioEvents.AmbNight);
            float open = bed.volume;
            Assert.IsNull(bed.GetComponent<AudioLowPassFilter>(), "no filter until one is needed");
            rig.SetBusShaping(AudioBus.Ambience, AudioMix.IndoorAmbienceDb, AudioMix.IndoorAmbienceLowPassHz);
            Assert.AreEqual(AudioMix.IndoorAmbienceDb, rig.BusTrimDb(AudioBus.Ambience));
            // Relative to the open level: the trim is the only change (run 113 read an extra -6 dB here, see the doc).
            Assert.AreEqual(open * AudioMix.DbToLinear(AudioMix.IndoorAmbienceDb), bed.volume, 1e-4f,
                $"open {open}, voice active {rig.VoiceActive}");
            var filter = bed.GetComponent<AudioLowPassFilter>();
            Assert.IsNotNull(filter);
            Assert.IsTrue(filter.enabled);
            Assert.AreEqual(AudioMix.IndoorAmbienceLowPassHz, rig.BusLowPassHz(AudioBus.Ambience), 1f);
            Assert.AreEqual(1f, rig.Play(AudioEvents.NoteStickerWobble).volume, 1e-5f, "other buses untouched");

            rig.SetBusShaping(AudioBus.Ambience, 0f, AudioMix.OpenLowPassHz);
            Assert.AreEqual(open, bed.volume, 1e-5f);
            Assert.IsFalse(filter.enabled, "an open filter is switched off");
        }

        [Test]
        public void PlaysAreLoggedWithTakeAndPlace()
        {
            rig.ClearLog();
            rig.PlayAt(AudioEvents.ClueKnocksDistant, new Vector3(1f, 4f, 9f));
            rig.Play(AudioEvents.UiTap);
            rig.Play(AudioEvents.MoveGlideLoop);
            rig.Stop(AudioEvents.MoveGlideLoop, 0.2f);
            Assert.IsFalse(rig.IsLooping(AudioEvents.MoveGlideLoop), "stopping (fading) loops no longer count");
            Assert.AreEqual(4, rig.Log.Count);
            var knock = rig.Log[0];
            Assert.AreEqual(AudioLogKind.Play, knock.Kind);
            Assert.AreEqual(AudioEvents.ClueKnocksDistant, knock.EventId);
            Assert.IsTrue(knock.Spatial);
            Assert.AreEqual(new Vector3(1f, 4f, 9f), knock.Position);
            Assert.That(knock.Take, Is.InRange(1, 4));
            Assert.AreEqual(Time.frameCount, knock.Frame);
            Assert.IsFalse(rig.Log[1].Spatial);
            Assert.AreEqual(AudioLogKind.Stop, rig.Log[3].Kind);
            Assert.AreEqual(1, rig.CountPlays(AudioEvents.UiTap));
            var knocks = AudioEvents.CreateCatalog().Get(AudioEvents.ClueKnocksDistant);
            var source = rig.PlayAt(AudioEvents.ClueKnocksDistant, Vector3.zero);
            Assert.AreEqual(knocks.MinDistance, source.minDistance);
            Assert.AreEqual(knocks.MaxDistance, source.maxDistance);
        }

        [UnityTest]
        public IEnumerator VoiceDucksMusicAndTheBedSmoothlyAndAStopOrTheEndReleasesIt()
        {
            rig.ClearLog();
            var music = rig.PlayTake(VoiceMusicEvents.MusicStreetTheme, 1, 0.2f);
            Assert.IsTrue(music.loop);
            Assert.AreEqual(0f, music.volume, "a loop crossfading in starts silent");
            yield return new WaitForSecondsRealtime(0.4f);
            float open = AudioMix.DbToLinear(VoiceMusicEvents.MusicTrimDb);
            Assert.AreEqual(open, music.volume, 1e-3f, "faded in to its level");
            var bed = rig.Play(AudioEvents.AmbNight);
            Assert.AreEqual(1f, bed.volume, 1e-4f);
            Assert.IsFalse(rig.VoiceActive, "no voice, no duck (the run 113 reading cannot come from here)");

            var voice = rig.PlayTake("voice_pip_happy", 3);
            Assert.AreEqual("Audio/voice_pip_happy/voice_pip_happy_t3", voice.clip.name, "the chosen take plays");
            Assert.AreEqual(3, rig.Log[rig.Log.Count - 1].Take);
            Assert.IsTrue(rig.VoiceActive);
            Assert.IsTrue(rig.IsPlaying("voice_pip_happy"));
            yield return null;
            Assert.Less(rig.DuckDb, 0f, "the duck starts");
            Assert.Greater(rig.DuckDb, AudioMix.VoiceDuckDb, "and ramps in, no step");
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(AudioMix.VoiceDuckDb, rig.DuckDb, 1e-3f);
            Assert.AreEqual(open * AudioMix.DbToLinear(AudioMix.VoiceDuckDb), music.volume, 1e-3f, "music dips");
            Assert.AreEqual(AudioMix.DbToLinear(AudioMix.VoiceDuckDb), bed.volume, 1e-3f, "the bed dips");
            Assert.AreEqual(1f, rig.Play(AudioEvents.NoteCorrectGlow).volume, 1e-4f, "effects never duck");

            // The pause holds the voice and its duck; the resume carries on.
            rig.SetGamePaused(true);
            Assert.IsTrue(rig.IsHeld("voice_pip_happy"));
            yield return new WaitForSecondsRealtime(0.2f);
            Assert.IsTrue(rig.VoiceActive, "a held voice still ducks");
            rig.SetGamePaused(false);
            Assert.IsTrue(rig.VoiceActive);

            rig.Stop("voice_pip_happy", 0.05f);
            Assert.IsFalse(rig.VoiceActive, "a stopped voice no longer ducks");
            Assert.IsFalse(rig.IsPlaying("voice_pip_happy"));
            yield return new WaitForSecondsRealtime(1f);
            Assert.AreEqual(0f, rig.DuckDb, 1e-3f, "the duck lets go");
            Assert.AreEqual(open, music.volume, 1e-3f);
            int ducks = 0, unducks = 0;
            foreach (var e in rig.Log)
            {
                if (e.Kind == AudioLogKind.Duck) ducks++;
                if (e.Kind == AudioLogKind.Unduck) unducks++;
            }

            Assert.AreEqual(1, ducks);
            Assert.AreEqual(1, unducks);

            // A voice that plays to its end releases the duck too (fake voices last 2 s).
            rig.PlayTake("voice_vale_neutral", 1);
            yield return null;
            Assert.IsTrue(rig.VoiceActive);
            yield return new WaitForSecondsRealtime(2.2f);
            Assert.IsFalse(rig.VoiceActive, "the duck ends with the clip");

            // Music stops fade out from where a fade-in left them.
            rig.Stop(VoiceMusicEvents.MusicStreetTheme, 0.2f);
            Assert.IsFalse(rig.IsLooping(VoiceMusicEvents.MusicStreetTheme));
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.IsNull(rig.SourceOf(VoiceMusicEvents.MusicStreetTheme));
        }

        [Test]
        public void EveryBusHasItsOwnSources()
        {
            var music = rig.Play(AudioEvents.MusicMotifWood);
            var voice = rig.Play(AudioEvents.VoiceKit);
            var amb = rig.Play(AudioEvents.AmbNight);
            var sfx = rig.Play(AudioEvents.NoteCorrectGlow);
            var parents = new HashSet<Transform> { music.transform.parent, voice.transform.parent, amb.transform.parent, sfx.transform.parent };
            Assert.AreEqual(4, parents.Count);
            Assert.IsNull(sfx.outputAudioMixerGroup, "no mixer asset: the code-built buses apply the levels");
        }
    }
}
