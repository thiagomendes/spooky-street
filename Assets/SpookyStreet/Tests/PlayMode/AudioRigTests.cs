using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Audio;
using SpookyStreet.Core.Audio;
using SpookyStreet.Core.Save;
using UnityEngine;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// The code-built mixer (S003 foundation) with generated stand-in clips: takes rotate without repeats, bus volumes
    /// follow the exposed parameters and the saved sliders, spatial events play in 3D, loops loop, rapid repeats are
    /// throttled, and a missing clip is skipped quietly. Not wired to gameplay: the sounds await owner approval.
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
                return AudioClip.Create(resourcePath, 4800, 1, 48000, false);
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
