using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.Audio
{
    /// <summary>The four mixer buses (D20, spec §6: independent music, effects and dialogue volume).</summary>
    public enum AudioBus
    {
        Music,
        Sfx,
        Voice,
        Ambience,
    }

    /// <summary>
    /// One sound event: the clips it may play (randomised takes rendered by <c>tools/audio</c>), its bus, level and
    /// playback rules. Clips are addressed by resource path, so the catalog stays engine-free and the runtime loads
    /// them however the platform prefers (D25).
    /// </summary>
    public sealed class AudioEventDef
    {
        public const string ResourceRoot = "Audio";

        public AudioEventDef(string id, AudioBus bus, int variants, float volumeDb = 0f, float pitchJitterSemitones = 0f,
            bool loop = false, bool spatial = false, float minIntervalSeconds = 0f, float[] markers = null)
        {
            Id = id;
            Bus = bus;
            Variants = variants;
            VolumeDb = volumeDb;
            PitchJitterSemitones = pitchJitterSemitones;
            Loop = loop;
            Spatial = spatial;
            MinIntervalSeconds = minIntervalSeconds;
            Markers = markers ?? Array.Empty<float>();
        }

        /// <summary>Stable event id; also the file stem of its takes (<c>{Id}_t{take}</c>).</summary>
        public string Id { get; }

        public AudioBus Bus { get; }

        /// <summary>Number of takes (variants) rendered for this event.</summary>
        public int Variants { get; }

        /// <summary>Event trim on top of the category loudness the takes are mastered to.</summary>
        public float VolumeDb { get; }

        /// <summary>Random pitch spread per play (± semitones), on top of the take variation.</summary>
        public float PitchJitterSemitones { get; }

        public bool Loop { get; }

        /// <summary>Played in 3D at a world position (direction and distance come from the scene).</summary>
        public bool Spatial { get; }

        /// <summary>Repeats closer than this are dropped (protects ears from rapid-fire triggers).</summary>
        public float MinIntervalSeconds { get; }

        /// <summary>Sync points in seconds from the start of every take (e.g. the three knock onsets).</summary>
        public IReadOnlyList<float> Markers { get; }

        /// <summary>Resource path of take <paramref name="take"/> (1-based), e.g. <c>Audio/ui_tap/ui_tap_t2</c>.</summary>
        public string ResourcePath(int take)
        {
            if (take < 1 || take > Variants) throw new ArgumentOutOfRangeException(nameof(take));
            return ResourceRoot + "/" + Id + "/" + Id + "_t" + take;
        }

        public override string ToString() => Id;
    }

    /// <summary>Event ids to definitions, with a validation pass that tests and the runtime share.</summary>
    public sealed class AudioCatalog
    {
        private readonly Dictionary<string, AudioEventDef> events = new Dictionary<string, AudioEventDef>(StringComparer.Ordinal);
        private readonly List<AudioEventDef> ordered = new List<AudioEventDef>();

        public IReadOnlyList<AudioEventDef> Events => ordered;

        public int Count => ordered.Count;

        public AudioCatalog Add(AudioEventDef def)
        {
            if (def == null) throw new ArgumentNullException(nameof(def));
            if (events.ContainsKey(def.Id)) throw new ArgumentException("duplicate audio event " + def.Id, nameof(def));
            events.Add(def.Id, def);
            ordered.Add(def);
            return this;
        }

        public bool TryGet(string id, out AudioEventDef def)
        {
            def = null;
            return id != null && events.TryGetValue(id, out def);
        }

        public AudioEventDef Get(string id)
        {
            if (TryGet(id, out var def)) return def;
            throw new KeyNotFoundException("unknown audio event " + id);
        }

        /// <summary>Problems that would make an event unplayable or ambiguous; empty when the catalog is sound.</summary>
        public List<string> Validate()
        {
            var problems = new List<string>();
            foreach (var def in ordered)
            {
                if (string.IsNullOrEmpty(def.Id)) problems.Add("event without id");
                else
                {
                    foreach (var c in def.Id)
                    {
                        if (!(c == '_' || (c >= 'a' && c <= 'z') || (c >= '0' && c <= '9')))
                        {
                            problems.Add(def.Id + ": ids are lower_snake_case");
                            break;
                        }
                    }
                }

                if (def.Variants < 1) problems.Add(def.Id + ": needs at least one take");
                if (def.VolumeDb > 6f || def.VolumeDb < -40f) problems.Add(def.Id + ": trim out of range");
                if (def.PitchJitterSemitones < 0f || def.PitchJitterSemitones > 2f) problems.Add(def.Id + ": pitch jitter out of range");
                if (def.MinIntervalSeconds < 0f) problems.Add(def.Id + ": negative min interval");
                for (var i = 0; i < def.Markers.Count; i++)
                {
                    if (def.Markers[i] < 0f || (i > 0 && def.Markers[i] <= def.Markers[i - 1]))
                        problems.Add(def.Id + ": markers must be ascending and non-negative");
                }
            }

            return problems;
        }
    }
}
