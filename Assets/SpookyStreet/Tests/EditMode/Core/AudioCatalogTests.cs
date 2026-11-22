using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Audio;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// The S003 audio catalog (D20): every event is valid and addressable, the clue knocks keep the short, short,
    /// pause, last rhythm, takes rotate without repeats, throttling and the volume model behave.
    /// </summary>
    public class AudioCatalogTests
    {
        [Test]
        public void TheFirstSetIsValid()
        {
            var catalog = AudioEvents.CreateCatalog();
            Assert.AreEqual(25, catalog.Count);
            CollectionAssert.IsEmpty(catalog.Validate());
            foreach (var def in catalog.Events)
            {
                Assert.GreaterOrEqual(def.Variants, 3, def + " needs 3–5 takes");
                Assert.LessOrEqual(def.Variants, 5, def.Id);
                Assert.AreEqual("Audio/" + def.Id + "/" + def.Id + "_t1", def.ResourcePath(1));
            }
        }

        [Test]
        public void EveryConstantResolves()
        {
            var catalog = AudioEvents.CreateCatalog();
            foreach (var field in typeof(AudioEvents).GetFields())
            {
                if (field.FieldType != typeof(string)) continue;
                var id = (string)field.GetValue(null);
                Assert.IsTrue(catalog.TryGet(id, out _), field.Name);
            }
        }

        [Test]
        public void BusesMatchTheirJobs()
        {
            var c = AudioEvents.CreateCatalog();
            Assert.AreEqual(AudioBus.Voice, c.Get(AudioEvents.VoiceVale).Bus);
            Assert.AreEqual(AudioBus.Ambience, c.Get(AudioEvents.AmbNight).Bus);
            Assert.AreEqual(AudioBus.Music, c.Get(AudioEvents.MusicMotifWood).Bus);
            Assert.AreEqual(AudioBus.Sfx, c.Get(AudioEvents.ClueKnocksDistant).Bus);
            Assert.IsTrue(c.Get(AudioEvents.AmbNight).Loop);
            Assert.IsTrue(c.Get(AudioEvents.MoveGlideLoop).Loop);
            Assert.IsTrue(c.Get(AudioEvents.ClueKnocksDistant).Spatial, "the knocks come from the house");
        }

        [Test]
        public void KnocksAreShortShortPauseLast()
        {
            foreach (var onsets in new[] { AudioEvents.KnockOnsets, AudioEvents.HumKnockOnsets })
            {
                Assert.AreEqual(3, onsets.Length);
                var first = onsets[1] - onsets[0];
                var second = onsets[2] - onsets[1];
                Assert.That(first, Is.InRange(0.18f, 0.35f), "short");
                Assert.Greater(second, first * 2f, "pause before the last knock");
            }

            var c = AudioEvents.CreateCatalog();
            CollectionAssert.AreEqual(AudioEvents.KnockOnsets, c.Get(AudioEvents.ClueKnocksDistant).Markers);
        }

        [Test]
        public void ValidationCatchesBadEvents()
        {
            var c = new AudioCatalog()
                .Add(new AudioEventDef("Bad Id", AudioBus.Sfx, 1))
                .Add(new AudioEventDef("no_takes", AudioBus.Sfx, 0))
                .Add(new AudioEventDef("markers", AudioBus.Sfx, 1, markers: new[] { 0.5f, 0.2f }))
                .Add(new AudioEventDef("loud", AudioBus.Sfx, 1, volumeDb: 12f));
            Assert.AreEqual(4, c.Validate().Count);
            Assert.Throws<ArgumentException>(() => c.Add(new AudioEventDef("loud", AudioBus.Sfx, 1)));
            Assert.Throws<KeyNotFoundException>(() => c.Get("missing"));
            Assert.Throws<ArgumentOutOfRangeException>(() => c.Get("loud").ResourcePath(2));
        }

        [Test]
        public void TakesRotateWithoutImmediateRepeats()
        {
            var def = new AudioEventDef("step_test", AudioBus.Sfx, 5);
            var picker = new VariantPicker(42);
            var last = 0;
            var counts = new int[6];
            for (var round = 0; round < 40; round++)
            {
                var seen = new HashSet<int>();
                for (var i = 0; i < 5; i++)
                {
                    var take = picker.Next(def);
                    Assert.That(take, Is.InRange(1, 5));
                    Assert.AreNotEqual(last, take, "no take twice in a row");
                    Assert.IsTrue(seen.Add(take), "each take once per bag");
                    counts[take]++;
                    last = take;
                }
            }

            for (var t = 1; t <= 5; t++) Assert.AreEqual(40, counts[t]);
        }

        [Test]
        public void PickerIsDeterministicPerSeed()
        {
            var def = new AudioEventDef("tap_test", AudioBus.Sfx, 4);
            var a = new VariantPicker(7);
            var b = new VariantPicker(7);
            var c = new VariantPicker(8);
            var same = true;
            for (var i = 0; i < 32; i++)
            {
                var x = a.Next(def);
                Assert.AreEqual(x, b.Next(def));
                same &= x == c.Next(def);
            }

            Assert.IsFalse(same, "a different seed gives a different order");
            Assert.AreEqual(1, a.Next(new AudioEventDef("single", AudioBus.Sfx, 1)));
            for (var i = 0; i < 1000; i++) Assert.That(a.NextUnit(), Is.InRange(0f, 0.99999994f));
        }

        [Test]
        public void ThrottleDropsRapidRepeats()
        {
            var step = new AudioEventDef("step_test", AudioBus.Sfx, 3, minIntervalSeconds: 0.08f);
            var throttle = new AudioThrottle();
            Assert.IsTrue(throttle.TryPlay(step, 1.0));
            Assert.IsFalse(throttle.TryPlay(step, 1.05));
            Assert.IsTrue(throttle.TryPlay(step, 1.09));
            var free = new AudioEventDef("free", AudioBus.Sfx, 1);
            Assert.IsTrue(throttle.TryPlay(free, 2.0));
            Assert.IsTrue(throttle.TryPlay(free, 2.0));
        }

        [Test]
        public void VolumeSlidersMapToDecibels()
        {
            Assert.AreEqual(0f, AudioMix.SliderToDb(1f));
            Assert.AreEqual(-12.04f, AudioMix.SliderToDb(0.5f), 0.01f);
            Assert.AreEqual(AudioMix.MinDb, AudioMix.SliderToDb(0f));
            Assert.AreEqual(AudioMix.MinDb, AudioMix.SliderToDb(float.NaN));
            Assert.AreEqual(1f, AudioMix.DbToLinear(0f), 1e-6f);
            Assert.AreEqual(0f, AudioMix.DbToLinear(AudioMix.MinDb));
            Assert.AreEqual("MusicVol", AudioMix.Parameter(AudioBus.Music));
            Assert.AreEqual("SFXVol", AudioMix.Parameter(AudioBus.Sfx));
            Assert.AreEqual(AudioMix.SliderToDb(0.3f), AudioMix.BusDb(AudioBus.Ambience, 1f, 0.3f, 1f));
            Assert.AreEqual(AudioMix.SliderToDb(0.2f), AudioMix.BusDb(AudioBus.Voice, 1f, 1f, 0.2f));
        }

        [Test]
        public void VoiceDucksMusicAndAmbienceOnly()
        {
            var music = new AudioEventDef("m", AudioBus.Music, 1);
            var sfx = new AudioEventDef("s", AudioBus.Sfx, 1, volumeDb: -6f);
            Assert.AreEqual(AudioMix.DbToLinear(AudioMix.VoiceDuckDb), AudioMix.PlayGain(music, 0f, true), 1e-6f);
            Assert.AreEqual(1f, AudioMix.PlayGain(music, 0f, false), 1e-6f);
            Assert.AreEqual(AudioMix.DbToLinear(-6f), AudioMix.PlayGain(sfx, 0f, true), 1e-6f);
            Assert.AreEqual(0f, AudioMix.PlayGain(sfx, AudioMix.MinDb, false));
        }

        [Test]
        public void PitchJitterStaysInRange()
        {
            var def = new AudioEventDef("p", AudioBus.Sfx, 1, pitchJitterSemitones: 0.6f);
            Assert.AreEqual(1f, AudioMix.PitchFor(new AudioEventDef("q", AudioBus.Sfx, 1), 0.9f));
            Assert.AreEqual(1f, AudioMix.PitchFor(def, 0.5f), 1e-6f);
            Assert.AreEqual(Math.Pow(2, -0.6 / 12), AudioMix.PitchFor(def, 0f), 1e-5);
            Assert.Less(AudioMix.PitchFor(def, 0.9999f), Math.Pow(2, 0.6 / 12) + 1e-5);
        }
    }
}
