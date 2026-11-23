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

    /// <summary>What happened on the rig (tests and diagnostics: batchmode has no audio device, so tests read this).</summary>
    public enum AudioLogKind
    {
        Play,
        Stop,
        Pause,
        Resume,

        /// <summary>A voice started: music and ambience begin to dip (<see cref="AudioMix.VoiceDuckDb"/>).</summary>
        Duck,

        /// <summary>The last voice ended or stopped: the duck lets go.</summary>
        Unduck,
    }

    /// <summary>One entry of <see cref="AudioRig.Log"/>.</summary>
    public readonly struct AudioLogEntry
    {
        public AudioLogEntry(AudioLogKind kind, string eventId, int take, AudioBus bus, bool spatial, Vector3 position,
            float gameTime, int frame)
        {
            Kind = kind;
            EventId = eventId;
            Take = take;
            Bus = bus;
            Spatial = spatial;
            Position = position;
            GameTime = gameTime;
            Frame = frame;
        }

        public AudioLogKind Kind { get; }

        /// <summary>The event (null for a bus-wide pause or resume).</summary>
        public string EventId { get; }

        public int Take { get; }
        public AudioBus Bus { get; }
        public bool Spatial { get; }
        public Vector3 Position { get; }

        /// <summary><c>Time.time</c> when it happened.</summary>
        public float GameTime { get; }

        public int Frame { get; }

        public override string ToString() => $"{Kind} {EventId} t{Take} @{GameTime:0.000}";
    }

    /// <summary>
    /// Code-built mixer (D20, S003): four buses (Music, SFX, Voice, Ambience) of pooled AudioSources with the exposed
    /// volume parameters <c>MusicVol</c>, <c>SFXVol</c>, <c>VoiceVol</c> and <c>AmbienceVol</c>. Unity cannot author
    /// an AudioMixer asset from code reliably (its group API is editor-internal), so the bus levels, voice ducking,
    /// per-bus shaping (the night bed's indoor trim and low-pass), pause and take rotation live here and in
    /// <see cref="AudioMix"/>. If an AudioMixer and its groups are assigned later, sources route into those groups and
    /// the same parameter names are set on the mixer. Standard AudioSource and AudioLowPassFilter only, no platform
    /// audio APIs (D25). Every play, stop and pause is recorded in <see cref="Log"/>.
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class AudioRig : MonoBehaviour
    {
        /// <summary>Effects overlap the most (steps, glide loop, clue sounds, taps); the other buses need few voices.</summary>
        public static int SourcesFor(AudioBus bus) => bus == AudioBus.Sfx ? 10 : 4;

        public const int LogCapacity = 512;

        [SerializeField] private AudioMixer mixer;
        [SerializeField] private AudioMixerGroup musicGroup;
        [SerializeField] private AudioMixerGroup sfxGroup;
        [SerializeField] private AudioMixerGroup voiceGroup;
        [SerializeField] private AudioMixerGroup ambienceGroup;

        private readonly Dictionary<AudioBus, List<AudioSource>> pools = new Dictionary<AudioBus, List<AudioSource>>();
        private readonly Dictionary<AudioBus, int> nextSource = new Dictionary<AudioBus, int>();
        private readonly Dictionary<AudioSource, AudioEventDef> playing = new Dictionary<AudioSource, AudioEventDef>();
        private readonly HashSet<AudioSource> paused = new HashSet<AudioSource>();
        private readonly Dictionary<AudioSource, float> fading = new Dictionary<AudioSource, float>();
        private readonly Dictionary<AudioSource, float> fadeGain = new Dictionary<AudioSource, float>();
        private readonly Dictionary<AudioSource, float> fadingIn = new Dictionary<AudioSource, float>();
        private readonly Dictionary<AudioSource, double> voiceEnds = new Dictionary<AudioSource, double>();
        private readonly Dictionary<AudioSource, double> voiceHeld = new Dictionary<AudioSource, double>();
        private readonly Dictionary<string, AudioClip> clipCache = new Dictionary<string, AudioClip>();
        private readonly HashSet<string> reportedMissing = new HashSet<string>();
        private readonly List<AudioLogEntry> log = new List<AudioLogEntry>();
        private readonly float[] busDb = new float[4];
        private readonly float[] busTrimDb = new float[4];
        private readonly float[] busLowPassHz = { AudioMix.OpenLowPassHz, AudioMix.OpenLowPassHz, AudioMix.OpenLowPassHz, AudioMix.OpenLowPassHz };

        private AudioCatalog catalog;
        private IAudioClipSource clips;
        private VariantPicker picker;
        private AudioThrottle throttle;
        private bool duckedLastFrame;
        private float duckDb;

        public AudioCatalog Catalog => catalog;

        /// <summary>The most recent plays, stops, pauses and resumes (oldest first, at most <see cref="LogCapacity"/>).</summary>
        public IReadOnlyList<AudioLogEntry> Log => log;

        /// <summary>True while the game is paused: effects, voices and the bed hold (<see cref="AudioMix.PausesWithGame"/>).</summary>
        public bool GamePaused { get; private set; }

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
                for (var i = 0; i < SourcesFor(bus); i++)
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

        /// <summary>Plays an event as 2D sound. Returns the source, or null when throttled, paused or the clip is missing.</summary>
        public AudioSource Play(string eventId) => PlayInternal(eventId, false, Vector3.zero, 0, 0f);

        /// <summary>Plays an event at a world position (3D for spatial events).</summary>
        public AudioSource PlayAt(string eventId, Vector3 position) => PlayInternal(eventId, true, position, 0, 0f);

        /// <summary>
        /// Plays one chosen take (1-based) as 2D sound, fading in over <paramref name="fadeInSeconds"/> (0: at once):
        /// the babble planner's phrase take, a music loop's crossfade.
        /// </summary>
        public AudioSource PlayTake(string eventId, int take, float fadeInSeconds = 0f) =>
            PlayInternal(eventId, false, Vector3.zero, take, fadeInSeconds);

        /// <summary>
        /// True while <paramref name="eventId"/> plays and is not fading out: a loop until stopped, a voice for its
        /// clip's length (held by the pause), anything else while its source plays.
        /// </summary>
        public bool IsPlaying(string eventId)
        {
            double now = Now;
            foreach (var pair in playing)
            {
                if (pair.Value.Id != eventId || fading.ContainsKey(pair.Key)) continue;
                if (pair.Value.Loop || paused.Contains(pair.Key) || pair.Key.isPlaying) return true;
                if (voiceEnds.TryGetValue(pair.Key, out double ends) && ends > now) return true;
            }

            return false;
        }

        /// <summary>The source playing <paramref name="eventId"/> (not fading out), or null.</summary>
        public AudioSource SourceOf(string eventId)
        {
            foreach (var pair in playing)
            {
                if (pair.Value.Id == eventId && !fading.ContainsKey(pair.Key)) return pair.Key;
            }

            return null;
        }

        /// <summary>The current voice duck on music and ambience (dB: 0 open, down to <see cref="AudioMix.VoiceDuckDb"/>).</summary>
        public float DuckDb => duckDb;

        /// <summary>Stops every source playing the event (loops such as the glide flutter or the night bed).</summary>
        public void Stop(string eventId) => Stop(eventId, 0f);

        /// <summary>Stops the event, fading it out over <paramref name="fadeSeconds"/> (no click on a loop).</summary>
        public void Stop(string eventId, float fadeSeconds)
        {
            foreach (var pair in new List<KeyValuePair<AudioSource, AudioEventDef>>(playing))
            {
                if (pair.Value.Id != eventId || fading.ContainsKey(pair.Key)) continue;
                Record(AudioLogKind.Stop, pair.Value, 0, pair.Key.transform.position);
                voiceEnds.Remove(pair.Key);
                voiceHeld.Remove(pair.Key);
                if (fadeSeconds > 0f && (pair.Key.isPlaying || pair.Value.Loop || pair.Value.Bus == AudioBus.Music))
                {
                    fadingIn.Remove(pair.Key);
                    fading[pair.Key] = 1f / fadeSeconds;
                    if (!fadeGain.ContainsKey(pair.Key)) fadeGain[pair.Key] = 1f;
                    continue;
                }

                Release(pair.Key);
            }
        }

        /// <summary>True while a looping event plays (until <see cref="Stop(string)"/>), paused or not.</summary>
        public bool IsLooping(string eventId)
        {
            foreach (var pair in playing)
            {
                if (pair.Value.Loop && pair.Value.Id == eventId && !fading.ContainsKey(pair.Key)) return true;
            }

            return false;
        }

        /// <summary>Plays of <paramref name="eventId"/> still in the log.</summary>
        public int CountPlays(string eventId)
        {
            int n = 0;
            foreach (var e in log)
            {
                if (e.Kind == AudioLogKind.Play && e.EventId == eventId) n++;
            }

            return n;
        }

        public void ClearLog() => log.Clear();

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

        /// <summary>
        /// Extra level and low-pass on one bus on top of the player's slider (the night bed indoors). A cutoff at or
        /// above <see cref="AudioMix.OpenLowPassHz"/> switches the filter off.
        /// </summary>
        public void SetBusShaping(AudioBus bus, float trimDb, float lowPassHz)
        {
            busTrimDb[(int)bus] = Mathf.Clamp(trimDb, AudioMix.MinDb, 0f);
            busLowPassHz[(int)bus] = Mathf.Clamp(lowPassHz, 100f, AudioMix.OpenLowPassHz);
            foreach (var source in pools[bus]) ApplyLowPass(source, busLowPassHz[(int)bus]);
            RefreshVolumes();
        }

        public float BusTrimDb(AudioBus bus) => busTrimDb[(int)bus];

        public float BusLowPassHz(AudioBus bus) => busLowPassHz[(int)bus];

        /// <summary>
        /// Holds the game's sound while paused (spec §5): every effect, voice and the bed pauses in place and resumes
        /// where it was; music and the UI tap carry on (<see cref="AudioMix.PausesWithGame"/>). While paused, new plays
        /// of pausing events are dropped (gameplay is frozen, so none should come).
        /// </summary>
        public void SetGamePaused(bool pause)
        {
            if (pause == GamePaused) return;
            GamePaused = pause;
            foreach (var pair in new List<KeyValuePair<AudioSource, AudioEventDef>>(playing))
            {
                if (!AudioMix.PausesWithGame(pair.Value)) continue;
                // Loops and sounds still sounding hold; a finished one-shot is simply free again.
                bool voiceSounding = voiceEnds.TryGetValue(pair.Key, out double voiceEnd) && voiceEnd > Now;
                if (pause && !pair.Value.Loop && !pair.Key.isPlaying && !voiceSounding) continue;
                if (pause)
                {
                    pair.Key.Pause();
                    paused.Add(pair.Key);
                    if (voiceEnds.TryGetValue(pair.Key, out double ends))
                    {
                        voiceHeld[pair.Key] = System.Math.Max(0.0, ends - Now); // the voice's remaining time holds with it
                        voiceEnds.Remove(pair.Key);
                    }
                }
                else if (paused.Remove(pair.Key))
                {
                    pair.Key.UnPause();
                    if (voiceHeld.TryGetValue(pair.Key, out double left))
                    {
                        voiceEnds[pair.Key] = Now + left;
                        voiceHeld.Remove(pair.Key);
                    }
                }
            }

            if (!pause) paused.Clear();
            foreach (AudioBus bus in System.Enum.GetValues(typeof(AudioBus)))
            {
                if (bus == AudioBus.Music) continue;
                log.Add(new AudioLogEntry(pause ? AudioLogKind.Pause : AudioLogKind.Resume, null, 0, bus, false,
                    Vector3.zero, Time.time, Time.frameCount));
            }

            TrimLog();
        }

        /// <summary>True while <paramref name="bus"/> is held by the pause (Music never is).</summary>
        public bool IsBusPaused(AudioBus bus) => GamePaused && bus != AudioBus.Music;

        /// <summary>True when the source is held by the pause.</summary>
        public bool IsPaused(AudioSource source) => paused.Contains(source);

        /// <summary>True when a source playing <paramref name="eventId"/> is held by the pause.</summary>
        public bool IsHeld(string eventId)
        {
            foreach (var source in paused)
            {
                if (playing.TryGetValue(source, out var def) && def.Id == eventId) return true;
            }

            return false;
        }

        /// <summary>
        /// True while a voice line plays (music and ambience duck under it). It comes from the rig's own bookkeeping
        /// (each voice play lasts its clip's length, holds under the pause and ends on a stop), never from
        /// <c>AudioSource.isPlaying</c>, which depends on the audio device. It used to read every voice source's
        /// <c>isPlaying</c>; see docs/S003-voices-music-wiring.md on the run 113 reading of −6 dB.
        /// </summary>
        public bool VoiceActive
        {
            get
            {
                foreach (var pair in voiceHeld)
                {
                    if (pair.Value > 0.0) return true;
                }

                double now = Now;
                foreach (var pair in voiceEnds)
                {
                    if (pair.Value > now) return true;
                }

                return false;
            }
        }

        private static double Now => Time.unscaledTimeAsDouble;

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            var ducked = VoiceActive;
            if (ducked != duckedLastFrame)
            {
                log.Add(new AudioLogEntry(ducked ? AudioLogKind.Duck : AudioLogKind.Unduck, null, 0, AudioBus.Voice, false,
                    Vector3.zero, Time.time, Time.frameCount));
                TrimLog();
            }

            duckedLastFrame = ducked;
            float duck = AudioMix.StepDuck(duckDb, ducked, dt);
            if (!Mathf.Approximately(duck, duckDb))
            {
                duckDb = duck;
                RefreshVolumes();
            }

            if (fadingIn.Count > 0)
            {
                foreach (var source in new List<AudioSource>(fadingIn.Keys))
                {
                    if (source == null || !playing.TryGetValue(source, out var def))
                    {
                        fadingIn.Remove(source);
                        continue;
                    }

                    float gain = Mathf.Min(1f, fadeGain[source] + fadingIn[source] * dt);
                    fadeGain[source] = gain;
                    if (gain >= 1f)
                    {
                        fadingIn.Remove(source);
                        fadeGain.Remove(source);
                    }

                    source.volume = VolumeFor(def) * gain;
                }
            }

            if (fading.Count == 0) return;
            foreach (var source in new List<AudioSource>(fading.Keys))
            {
                if (source == null)
                {
                    fading.Remove(source);
                    continue;
                }

                if (paused.Contains(source)) continue;
                float gain = fadeGain[source] - fading[source] * dt;
                if (gain <= 0.001f || !source.isPlaying)
                {
                    Release(source);
                    continue;
                }

                fadeGain[source] = gain;
                if (playing.TryGetValue(source, out var def)) source.volume = VolumeFor(def) * gain;
            }
        }

        private AudioSource PlayInternal(string eventId, bool positioned, Vector3 position, int forcedTake, float fadeIn)
        {
            if (catalog == null || !catalog.TryGet(eventId, out var def))
            {
                Debug.LogWarning("AudioRig: unknown audio event " + eventId);
                return null;
            }

            if (GamePaused && AudioMix.PausesWithGame(def)) return null;
            if (!throttle.TryPlay(def, Time.realtimeSinceStartupAsDouble)) return null;
            var take = forcedTake >= 1 && forcedTake <= def.Variants ? forcedTake : picker.Next(def);
            var clip = LoadClip(def.ResourcePath(take));
            if (clip == null) return null;

            var source = NextSource(def.Bus);
            source.Stop();
            fading.Remove(source);
            fadeGain.Remove(source);
            fadingIn.Remove(source);
            voiceEnds.Remove(source);
            voiceHeld.Remove(source);
            source.clip = clip;
            source.loop = def.Loop;
            source.pitch = AudioMix.PitchFor(def, picker.NextUnit());
            bool spatial = positioned && def.Spatial;
            source.spatialBlend = spatial ? 1f : 0f;
            source.rolloffMode = AudioRolloffMode.Logarithmic;
            source.minDistance = def.MinDistance;
            source.maxDistance = def.MaxDistance;
            source.dopplerLevel = 0f;
            source.ignoreListenerPause = !AudioMix.PausesWithGame(def);
            source.transform.position = positioned ? position : transform.position;
            playing[source] = def;
            if (fadeIn > 0f)
            {
                fadingIn[source] = 1f / fadeIn;
                fadeGain[source] = 0f;
            }

            if (def.Bus == AudioBus.Voice)
            {
                voiceEnds[source] = Now + clip.length / Mathf.Max(0.01f, Mathf.Abs(source.pitch));
            }

            source.volume = fadeIn > 0f ? 0f : VolumeFor(def);
            source.Play();
            Record(AudioLogKind.Play, def, take, source.transform.position, spatial);
            return source;
        }

        private void Record(AudioLogKind kind, AudioEventDef def, int take, Vector3 position, bool spatial = false)
        {
            log.Add(new AudioLogEntry(kind, def.Id, take, def.Bus, spatial, position, Time.time, Time.frameCount));
            TrimLog();
        }

        private void TrimLog()
        {
            if (log.Count > LogCapacity) log.RemoveRange(0, log.Count - LogCapacity);
        }

        private void Release(AudioSource source)
        {
            source.Stop();
            playing.Remove(source);
            paused.Remove(source);
            fading.Remove(source);
            fadeGain.Remove(source);
            fadingIn.Remove(source);
            voiceEnds.Remove(source);
            voiceHeld.Remove(source);
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

            // A finished one-shot (never a loop, a paused sound or a fade still running).
            foreach (var s in list)
            {
                if (!s.isPlaying && !paused.Contains(s) && !fading.ContainsKey(s) && playing.TryGetValue(s, out var d) &&
                    !d.Loop)
                {
                    playing.Remove(s);
                    return s;
                }
            }

            // All busy: reuse round-robin, never a loop that is still wanted or a sound held by the pause.
            for (var i = 0; i < list.Count; i++)
            {
                var index = (nextSource[bus] + i) % list.Count;
                var s = list[index];
                if (paused.Contains(s) || (playing.TryGetValue(s, out var def) && def.Loop && !fading.ContainsKey(s))) continue;
                nextSource[bus] = (index + 1) % list.Count;
                Release(s);
                return s;
            }

            var first = list[nextSource[bus]];
            nextSource[bus] = (nextSource[bus] + 1) % list.Count;
            Release(first);
            return first;
        }

        private float VolumeFor(AudioEventDef def)
        {
            float trim = busTrimDb[(int)def.Bus];
            if (mixer != null && GroupFor(def.Bus) != null)
            {
                // The mixer applies the bus level; only the trim, shaping and ducking stay on the source.
                return AudioMix.PlayGain(def, 0f, duckDb, trim);
            }

            return AudioMix.PlayGain(def, busDb[(int)def.Bus], duckDb, trim);
        }

        private void RefreshVolumes()
        {
            var stale = new List<AudioSource>();
            foreach (var pair in playing)
            {
                if (pair.Key == null) stale.Add(pair.Key);
                else
                {
                    float fade = fadeGain.TryGetValue(pair.Key, out var g) ? g : 1f;
                    pair.Key.volume = VolumeFor(pair.Value) * fade;
                }
            }

            foreach (var s in stale) playing.Remove(s);
        }

        private static void ApplyLowPass(AudioSource source, float hz)
        {
            var filter = source.GetComponent<AudioLowPassFilter>();
            bool open = hz >= AudioMix.OpenLowPassHz - 1f;
            if (filter == null)
            {
                if (open) return;
                filter = source.gameObject.AddComponent<AudioLowPassFilter>();
            }

            filter.cutoffFrequency = hz;
            filter.lowpassResonanceQ = 1f;
            filter.enabled = !open;
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
