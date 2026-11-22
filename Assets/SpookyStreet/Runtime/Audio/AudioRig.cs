using System.Collections.Generic;
using SpookyStreet.Core.Audio;
using SpookyStreet.Core.Save;
using UnityEngine;
using UnityEngine.Audio;

namespace SpookyStreet.Audio
{
    /// <summary>Where clips come from. The default loads them from Resources; tests inject generated clips.</summary>
    public interface IAudioClipSource
    {
        AudioClip Load(string resourcePath);
    }

    public sealed class ResourcesClipSource : IAudioClipSource
    {
        public AudioClip Load(string resourcePath) => Resources.Load<AudioClip>(resourcePath);
    }

    /// <summary>
    /// Code-built mixer (D20, S003 foundation): four buses (Music, SFX, Voice, Ambience) of pooled AudioSources with
    /// the exposed volume parameters <c>MusicVol</c>, <c>SFXVol</c>, <c>VoiceVol</c> and <c>AmbienceVol</c>. Unity
    /// cannot author an AudioMixer asset from code reliably (its group API is editor-internal), so the bus levels,
    /// voice ducking and take rotation live here and in <see cref="AudioMix"/>. If an AudioMixer and its groups are
    /// assigned later, sources route into those groups and the same parameter names are set on the mixer.
    /// Standard AudioSource only, no platform audio APIs (D25). Not wired to gameplay yet: the owner approves the
    /// sounds first.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioRig : MonoBehaviour
    {
        private const int SourcesPerBus = 6;

        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioMixerGroup musicGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;
        [SerializeField] private AudioMixerGroup voiceGroup;
        [SerializeField] private AudioMixerGroup ambienceGroup;

        private readonly Dictionary<AudioBus, List<AudioSource>> pools = new Dictionary<AudioBus, List<AudioSource>>();
        private readonly Dictionary<AudioBus, int> nextSource = new Dictionary<AudioBus, int>();
        private readonly Dictionary<AudioSource, AudioEventDef> playing = new Dictionary<AudioSource, AudioEventDef>();
        private readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();
        private readonly HashSet<string> reportedMissing = new HashSet<string>();
        private readonly float[] busDb = new float[4];

        private AudioCatalog catalog;
        private IAudioClipSource clips;
        private VariantPicker picker;
        private AudioThrottle throttle;
        private bool duckedLastFrame;

        public AudioCatalog Catalog => catalog;

        /// <summary>A rig on a new GameObject, ready to play the S003 catalog (or the given one).</summary>
        public static AudioRig Create(AudioCatalog catalog = null, IAudioClipSource clips = null, uint seed = 0x5EED5u)
        {
            var go = new GameObject("AudioRig");
            var rig = go.AddComponent<AudioRig>();
            rig.Initialize(catalog ?? AudioEvents.CreateCatalog(), clips ?? new ResourcesClipSource(), seed);
            return rig;
        }

        public void Initialize(AudioCatalog audioCatalog, IAudioClipSource clipSource, uint seed = 0x5EED5u)
        {
            catalog = audioCatalog;
            clips = clipSource;
            picker = new VariantPicker(seed);
            throttle = new AudioThrottle();
            if (pools.Count > 0) return;
            foreach (AudioBus bus in System.Enum.GetValues(typeof(AudioBus)))
            {
                var holder = new GameObject(bus + "Bus").transform;
                holder.SetParent(transform, false);
                var list = new List<AudioSource>();
                for (var i = 0; i < SourcesPerBus; i++)
                {
                    var go = new GameObject(bus + "Source" + i);
                    go.transform.SetParent(holder, false);
                    var source = go.AddComponent<AudioSource>();
                    source.playOnAwake = false;
                    source.outputAudioMixerGroup = GroupFor(bus);
                    list.Add(source);
                }

                pools[bus] = list;
                nextSource[bus] = 0;
            }
        }

        /// <summary>Plays an event as 2D sound. Returns the source, or null when throttled or the clip is missing.</summary>
        public AudioSource Play(string eventId) => PlayInternal(eventId, false, Vector3.zero);

        /// <summary>Plays an event at a world position (3D for spatial events).</summary>
        public AudioSource PlayAt(string eventId, Vector3 position) => PlayInternal(eventId, true, position);

        /// <summary>Stops every source playing the event (loops such as the glide flutter or the night bed).</summary>
        public void Stop(string eventId)
        {
            foreach (var pair in new List<KeyValuePair<AudioSource, AudioEventDef>>(playing))
            {
                if (pair.Value.Id != eventId) continue;
                pair.Key.Stop();
                playing.Remove(pair.Key);
            }
        }

        /// <summary>Sets an exposed parameter (e.g. <c>"SFXVol"</c>) in decibels.</summary>
        public void SetVolume(string parameter, float db)
        {
            for (var i = 0; i < busDb.Length; i++)
            {
                var bus = (AudioBus)i;
                if (AudioMix.Parameter(bus) != parameter) continue;
                busDb[i] = Mathf.Clamp(db, AudioMix.MinDb, 0f);
                if (mixer != null && GroupFor(bus) != null) mixer.SetFloat(parameter, busDb[i]);
                RefreshVolumes();
                return;
            }

            Debug.LogWarning("AudioRig: unknown volume parameter " + parameter);
        }

        public float GetVolume(string parameter)
        {
            for (var i = 0; i < busDb.Length; i++)
            {
                if (AudioMix.Parameter((AudioBus)i) == parameter) return busDb[i];
            }

            return AudioMix.MinDb;
        }

        /// <summary>Applies the saved music, effects and dialogue sliders (ambience follows effects).</summary>
        public void ApplyPreferences(PreferencesData preferences)
        {
            foreach (AudioBus bus in System.Enum.GetValues(typeof(AudioBus)))
            {
                SetVolume(AudioMix.Parameter(bus),
                    AudioMix.BusDb(bus, preferences.MusicVolume, preferences.EffectsVolume, preferences.DialogueVolume));
            }
        }

        /// <summary>True while a voice line plays (music and ambience duck under it).</summary>
        public bool VoiceActive
        {
            get
            {
                if (!pools.TryGetValue(AudioBus.Voice, out var voices)) return false;
                foreach (var s in voices)
                {
                    if (s.isPlaying) return true;
                }

                return false;
            }
        }

        private void Update()
        {
            var ducked = VoiceActive;
            if (ducked != duckedLastFrame) RefreshVolumes();
            duckedLastFrame = ducked;
        }

        private AudioSource PlayInternal(string eventId, bool positioned, Vector3 position)
        {
            if (catalog == null || !catalog.TryGet(eventId, out var def))
            {
                Debug.LogWarning("AudioRig: unknown audio event " + eventId);
                return null;
            }

            if (!throttle.TryPlay(def, Time.realtimeSinceStartupAsDouble)) return null;
            var take = picker.Next(def);
            var clip = LoadClip(def.ResourcePath(take));
            if (clip == null) return null;

            var source = NextSource(def.Bus);
            source.Stop();
            source.clip = clip;
            source.loop = def.Loop;
            source.pitch = AudioMix.PitchFor(def, picker.NextUnit());
            source.spatialBlend = positioned && def.Spatial ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = 3f;
            source.maxDistance = 40f;
            source.transform.position = positioned ? position : transform.position;
            playing[source] = def;
            source.volume = VolumeFor(def);
            source.Play();
            return source;
        }

        private AudioClip LoadClip(string path)
        {
            if (clipCache.TryGetValue(path, out var cached)) return cached;
            var clip = clips.Load(path);
            if (clip == null)
            {
                if (reportedMissing.Add(path)) Debug.LogWarning("AudioRig: no clip at Resources/" + path);
                return null;
            }

            clipCache[path] = clip;
            return clip;
        }

        private AudioSource NextSource(AudioBus bus)
        {
            var list = pools[bus];
            foreach (var s in list)
            {
                if (!s.isPlaying && !playing.ContainsKey(s)) return s;
            }

            // All busy: reuse round-robin, never a loop that is still wanted.
            for (var i = 0; i < list.Count; i++)
            {
                var index = (nextSource[bus] + i) % list.Count;
                var s = list[index];
                if (playing.TryGetValue(s, out var def) && def.Loop) continue;
                nextSource[bus] = (index + 1) % list.Count;
                playing.Remove(s);
                return s;
            }

            var first = list[nextSource[bus]];
            nextSource[bus] = (nextSource[bus] + 1) % list.Count;
            playing.Remove(first);
            return first;
        }

        private float VolumeFor(AudioEventDef def)
        {
            if (mixer != null && GroupFor(def.Bus) != null)
            {
                // The mixer applies the bus level; only the trim and ducking stay on the source.
                return AudioMix.PlayGain(def, 0f, VoiceActive);
            }

            return AudioMix.PlayGain(def, busDb[(int)def.Bus], VoiceActive);
        }

        private void RefreshVolumes()
        {
            var stale = new List<AudioSource>();
            foreach (var pair in playing)
            {
                if (pair.Key == null || (!pair.Key.isPlaying && !pair.Value.Loop)) stale.Add(pair.Key);
                else pair.Key.volume = VolumeFor(pair.Value);
            }

            foreach (var s in stale) playing.Remove(s);
        }

        private AudioMixerGroup GroupFor(AudioBus bus)
        {
            switch (bus)
            {
                case AudioBus.Music: return musicGroup;
                case AudioBus.Sfx: return sfxGroup;
                case AudioBus.Voice: return voiceGroup;
                default: return ambienceGroup;
            }
        }
    }
}
