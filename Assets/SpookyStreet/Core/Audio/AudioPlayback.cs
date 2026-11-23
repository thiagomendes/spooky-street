using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.Audio
{
    /// <summary>
    /// Chooses takes like a shuffle bag: every take plays once before any repeats, and the same take never plays twice
    /// in a row (also across bag refills), so footsteps and taps never sound mechanical. Seeded and deterministic.
    /// </summary>
    public sealed class VariantPicker
    {
        private readonly Dictionary<string, Bag> bags = new Dictionary<string, Bag>(StringComparer.Ordinal);
        private uint state;

        public VariantPicker(uint seed = 0x5EED5u)
        {
            state = seed == 0 ? 0x9E3779B9u : seed;
        }

        /// <summary>Next 1-based take for <paramref name="def"/>.</summary>
        public int Next(AudioEventDef def)
        {
            if (def.Variants <= 1) return 1;
            if (!bags.TryGetValue(def.Id, out var bag))
            {
                bag = new Bag();
                bags.Add(def.Id, bag);
            }

            if (bag.Remaining.Count == 0) Refill(bag, def.Variants);
            var take = bag.Remaining[bag.Remaining.Count - 1];
            bag.Remaining.RemoveAt(bag.Remaining.Count - 1);
            bag.Last = take;
            return take;
        }

        /// <summary>A uniform float in [0, 1) from the same deterministic stream (for pitch jitter).</summary>
        public float NextUnit()
        {
            // xorshift32: identical on every runtime, unlike System.Random across .NET and Mono versions.
            state ^= state << 13;
            state ^= state >> 17;
            state ^= state << 5;
            return (state >> 8) / 16777216f;
        }

        private void Refill(Bag bag, int variants)
        {
            for (var i = 1; i <= variants; i++) bag.Remaining.Add(i);
            for (var i = bag.Remaining.Count - 1; i > 0; i--)
            {
                var j = (int)(NextUnit() * (i + 1));
                var tmp = bag.Remaining[i];
                bag.Remaining[i] = bag.Remaining[j];
                bag.Remaining[j] = tmp;
            }

            // The next pick is the last element: keep it different from the take that just played.
            var lastIndex = bag.Remaining.Count - 1;
            if (bag.Remaining[lastIndex] == bag.Last)
            {
                var tmp = bag.Remaining[0];
                bag.Remaining[0] = bag.Remaining[lastIndex];
                bag.Remaining[lastIndex] = tmp;
            }
        }

        private sealed class Bag
        {
            public readonly List<int> Remaining = new List<int>();
            public int Last;
        }
    }

    /// <summary>Drops repeats of an event that come faster than its <see cref="AudioEventDef.MinIntervalSeconds"/>.</summary>
    public sealed class AudioThrottle
    {
        private readonly Dictionary<string, double> lastPlayed = new Dictionary<string, double>(StringComparer.Ordinal);

        public bool TryPlay(AudioEventDef def, double now)
        {
            if (def.MinIntervalSeconds > 0f && lastPlayed.TryGetValue(def.Id, out var last) && now - last < def.MinIntervalSeconds)
                return false;
            lastPlayed[def.Id] = now;
            return true;
        }
    }

    /// <summary>
    /// The mix as numbers: exposed parameter names, the player's 0–1 volume sliders as decibels, and per-play gain and
    /// pitch. A Unity AudioMixer (if one is assigned) receives the same parameter names.
    /// </summary>
    public static class AudioMix
    {
        public const string MusicVol = "MusicVol";
        public const string SfxVol = "SFXVol";
        public const string VoiceVol = "VoiceVol";
        public const string AmbienceVol = "AmbienceVol";

        /// <summary>Silence floor, as Unity's mixer uses.</summary>
        public const float MinDb = -80f;

        /// <summary>Music and ambience dip by this much while a voice line plays, so babble stays clear.</summary>
        public const float VoiceDuckDb = -6f;

        public static string Parameter(AudioBus bus)
        {
            switch (bus)
            {
                case AudioBus.Music: return MusicVol;
                case AudioBus.Sfx: return SfxVol;
                case AudioBus.Voice: return VoiceVol;
                case AudioBus.Ambience: return AmbienceVol;
                default: throw new ArgumentOutOfRangeException(nameof(bus));
            }
        }

        /// <summary>
        /// A 0–1 slider to decibels on a perceptual (squared) taper: 1 → 0 dB, 0.5 → −12 dB, 0 → silence.
        /// </summary>
        public static float SliderToDb(float slider)
        {
            if (float.IsNaN(slider) || slider <= 0f) return MinDb;
            if (slider >= 1f) return 0f;
            var db = 40f * (float)Math.Log10(slider);
            return db < MinDb ? MinDb : db;
        }

        public static float DbToLinear(float db) => db <= MinDb ? 0f : (float)Math.Pow(10.0, db / 20.0);

        /// <summary>
        /// Bus levels from the saved preferences (spec §6: independent music, effects and dialogue volume). Ambience
        /// follows the effects slider.
        /// </summary>
        public static float BusDb(AudioBus bus, float music, float effects, float dialogue)
        {
            switch (bus)
            {
                case AudioBus.Music: return SliderToDb(music);
                case AudioBus.Voice: return SliderToDb(dialogue);
                default: return SliderToDb(effects);
            }
        }

        /// <summary>Linear gain for one play: bus level, event trim and voice ducking.</summary>
        public static float PlayGain(AudioEventDef def, float busDb, bool voiceActive)
        {
            var db = busDb + def.VolumeDb;
            if (voiceActive && (def.Bus == AudioBus.Music || def.Bus == AudioBus.Ambience)) db += VoiceDuckDb;
            return busDb <= MinDb ? 0f : DbToLinear(db);
        }

        /// <summary>Pitch multiplier for a unit random value in [0, 1): within ± the event's jitter.</summary>
        public static float PitchFor(AudioEventDef def, float unit)
        {
            if (def.PitchJitterSemitones <= 0f) return 1f;
            var semitones = (unit * 2f - 1f) * def.PitchJitterSemitones;
            return (float)Math.Pow(2.0, semitones / 12.0);
        }
    }
}
