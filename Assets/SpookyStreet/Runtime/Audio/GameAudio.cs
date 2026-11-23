using System;
using System.Collections.Generic;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.Audio;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Movement;
using SpookyStreet.Core.Save;
using SpookyStreet.Core.World;
using SpookyStreet.Player;
using SpookyStreet.Save;
using UnityEngine;
using UnityEngine.UI;
using CaseLines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;

namespace SpookyStreet.Audio
{
    /// <summary>
    /// The approved sound effects in play (S003, D20.1), wired from the outside through events so the player, camera
    /// and case code stay as they are:
    /// <list type="bullet">
    /// <item>Pip's motor (<see cref="PipMotor.Stepped"/>): footsteps per stride on the surface under Pip (wood, grass,
    /// stone; <see cref="FootstepSurfaces"/>), jump, land, and the glide's cape snap followed by the flutter loop.</item>
    /// <item>The case: the arrival knocks as a 3D sound at the attic window, starting on <see cref="KnockBeat.Started"/>
    /// so the light pulses land on the knocks; the floor hum when the hall's hum caption plays; a cam tap on every
    /// knock contact of the workshop mechanism (<see cref="WorkshopView.Knocked"/>) while it can be heard (inside the
    /// house or in the crank cutaway); the wind wheel's whirr at the rooftop wheel on each wind-driven cycle while
    /// Pip is outdoors; the paper page-turn on every threshold crossing.</item>
    /// <item>The UI tap on every uGUI button (<see cref="UiTapSound"/>) and the notebook hooks (<see cref="AudioCues"/>).</item>
    /// <item>The night bed outdoors, quieter and muffled inside the house; the music, sounds and voices sliders from
    /// the saved preferences; the pause holding the game's sound (<see cref="AudioRig.SetGamePaused"/>).</item>
    /// </list>
    /// Reduced motion changes nothing here: it is a visual setting.
    /// </summary>
    public sealed class GameAudio : MonoBehaviour
    {
        /// <summary>The flutter loop takes over this long after the cape snaps open.</summary>
        public const float GlideLoopDelay = 0.35f;

        public const float GlideLoopFade = 0.15f;

        /// <summary>A jump of Pip's position larger than this in one frame is a teleport, not a stride.</summary>
        public const float TeleportDistance = 1.5f;

        /// <summary>The volume sliders save once they have rested this long (unscaled seconds).</summary>
        public const float SaveDelay = 0.5f;

        private const float ButtonScanSeconds = 0.5f;
        private const float GroundProbeLength = 1.2f;

        private GameBootstrap _game;
        private GameFlow _flow;
        private PipMotor _pip;
        private CaseDirector _director;
        private PreferencesStore _store;
        private readonly FootstepCadence _cadence = new FootstepCadence();
        private readonly HashSet<Button> _hooked = new HashSet<Button>();
        private readonly List<string> _names = new List<string>(8);
        private Vector3 _lastPip;
        private bool _hasLastPip;
        private float _glideLoopAt = -1f;
        private float _scanClock;
        private float _saveClock = -1f;
        private float _ambienceTrim;
        private float _ambienceCutoff = AudioMix.OpenLowPassHz;
        private int _groundMask;

        public AudioRig Rig { get; private set; }

        /// <summary>The player's preferences (volume sliders); saved through <see cref="PreferencesStore"/>.</summary>
        public PreferencesData Preferences { get; private set; }

        /// <summary>Pip is inside the owl house (the bed is muffled).</summary>
        public bool Indoors { get; private set; }

        /// <summary>The surface of the last footstep.</summary>
        public FootstepSurface LastSurface { get; private set; } = FootstepSurface.Grass;

        /// <summary>The collider the last footstep probed (null when the ray found nothing).</summary>
        public Collider LastGround { get; private set; }

        /// <summary>Footsteps played since the scene loaded (tests).</summary>
        public int Footsteps { get; private set; }

        public static GameAudio Create(GameBootstrap game, IAudioClipSource clips = null)
        {
            var go = new GameObject("GameAudio");
            var audio = go.AddComponent<GameAudio>();
            audio.Initialize(game, clips);
            return audio;
        }

        private void Initialize(GameBootstrap game, IAudioClipSource clips)
        {
            _game = game;
            _flow = game.Flow;
            _pip = game.Pip;
            _director = game.Director;
            _groundMask = Physics.DefaultRaycastLayers & ~(1 << _pip.gameObject.layer);

            Rig = AudioRig.Create(AudioEvents.CreateCatalog(), clips ?? new ResourcesClipSource());
            Rig.transform.SetParent(transform, false);

            string directory = _director != null ? _director.SaveDirectory : null;
            Preferences = new PreferencesData();
            if (!string.IsNullOrEmpty(directory))
            {
                try
                {
                    _store = new PreferencesStore(new DiskSaveFileSystem(), directory);
                    Preferences = _store.Load();
                }
                catch (Exception e)
                {
                    Debug.LogWarning("[GameAudio] preferences: " + e.Message);
                }
            }

            Rig.ApplyPreferences(Preferences);

            _pip.Stepped += OnMotorStep;
            _flow.StateChanged += OnStateChanged;
            AudioCues.Requested += OnCue;
            if (_director != null)
            {
                _director.Stepped += OnCaseStep;
                _director.Knocks.Started += OnKnocks;
                if (_director.Workshop != null)
                {
                    _director.Workshop.Knocked += OnMechanismKnock;
                    _director.Workshop.CycleStarted += OnMechanismCycle;
                }

                if (_director.Travel != null)
                {
                    _director.Travel.Started += OnThresholdStarted;
                }

                var menus = _director.Nonverbal != null ? _director.Nonverbal.Menus : null;
                if (menus != null)
                {
                    menus.Preferences = Preferences;
                    menus.PreferencesChanged = OnPreferencesChanged;
                }
            }

            HookButtons();
            Rig.SetGamePaused(_flow.State != GameState.Playing);
            if (_flow.State == GameState.Playing)
            {
                StartAmbience();
            }
        }

        private void OnDestroy()
        {
            if (_saveClock >= 0f)
            {
                SavePreferences();
            }

            AudioCues.Requested -= OnCue;
            if (_pip != null)
            {
                _pip.Stepped -= OnMotorStep;
            }

            if (_flow != null)
            {
                _flow.StateChanged -= OnStateChanged;
            }

            if (_director != null)
            {
                _director.Stepped -= OnCaseStep;
                if (_director.Knocks != null)
                {
                    _director.Knocks.Started -= OnKnocks;
                }

                if (_director.Workshop != null)
                {
                    _director.Workshop.Knocked -= OnMechanismKnock;
                    _director.Workshop.CycleStarted -= OnMechanismCycle;
                }

                if (_director.Travel != null)
                {
                    _director.Travel.Started -= OnThresholdStarted;
                }
            }
        }

        // ------------------------------------------------------------------------------------------- game state

        private void OnStateChanged(GameState previous, GameState current)
        {
            bool playing = current == GameState.Playing;
            Rig.SetGamePaused(!playing);
            if (playing)
            {
                StartAmbience();
            }

            if (current == GameState.Title)
            {
                Rig.Stop(AudioEvents.MoveGlideLoop);
                _glideLoopAt = -1f;
            }

            _hasLastPip = false;
            _cadence.Reset();
        }

        private void StartAmbience()
        {
            if (!Rig.IsLooping(AudioEvents.AmbNight))
            {
                UpdateIndoors();
                _ambienceTrim = AudioMix.AmbienceTrimDb(Indoors);
                _ambienceCutoff = AudioMix.AmbienceLowPassHz(Indoors);
                Rig.SetBusShaping(AudioBus.Ambience, _ambienceTrim, _ambienceCutoff);
                Rig.Play(AudioEvents.AmbNight);
            }
        }

        private void UpdateIndoors()
        {
            Vector3 p = _pip.transform.position;
            Indoors = CaseRouteLayout.Inside(p.x, p.z);
        }

        // ------------------------------------------------------------------------------------------- Pip

        private void OnMotorStep(MotorStep step)
        {
            Vector3 position = _pip.transform.position;
            float distance = 0f;
            if (_hasLastPip)
            {
                Vector3 moved = position - _lastPip;
                moved.y = 0f;
                distance = moved.magnitude;
                if (distance > TeleportDistance)
                {
                    distance = 0f;
                    _cadence.Reset();
                }
            }

            _lastPip = position;
            _hasLastPip = true;

            if (step.Jumped)
            {
                Rig.Play(AudioEvents.MoveJump);
                _cadence.Reset();
            }

            if (step.Landed)
            {
                Rig.Play(AudioEvents.MoveLand);
                _cadence.Reset();
            }

            if (step.GlideStarted)
            {
                Rig.Play(AudioEvents.MoveGlideOpen);
                _glideLoopAt = Time.time + GlideLoopDelay;
            }

            if (step.GlideEnded || (!_pip.Gliding && (_glideLoopAt >= 0f || Rig.IsLooping(AudioEvents.MoveGlideLoop))))
            {
                _glideLoopAt = -1f;
                Rig.Stop(AudioEvents.MoveGlideLoop, GlideLoopFade);
            }

            bool onFeet = _pip.Grounded && !_pip.Rolling && !step.Landed && !step.Jumped;
            if (_cadence.Advance(onFeet ? distance : 0f, _pip.HorizontalSpeed, onFeet))
            {
                PlayFootstep(position);
            }
        }

        private void PlayFootstep(Vector3 feet)
        {
            LastSurface = ProbeSurface(feet);
            if (Rig.Play(FootstepSurfaces.EventFor(LastSurface)) != null)
            {
                Footsteps++;
            }
        }

        /// <summary>The surface under <paramref name="feet"/>: the collider straight below, named after what it is.</summary>
        public FootstepSurface ProbeSurface(Vector3 feet)
        {
            _names.Clear();
            LastGround = null;
            if (Physics.Raycast(feet + Vector3.up * 0.3f, Vector3.down, out RaycastHit hit, GroundProbeLength, _groundMask,
                    QueryTriggerInteraction.Ignore))
            {
                LastGround = hit.collider;
                for (Transform t = hit.collider.transform; t != null && _names.Count < 6; t = t.parent)
                {
                    _names.Add(t.name);
                }
            }

            bool indoors = CaseRouteLayout.Inside(feet.x, feet.z);
            bool sidewalk = _game.Environment != null && _game.Environment.StreetSetAvailable &&
                            FootstepSurfaces.OnSidewalk(feet.x, feet.z);
            return FootstepSurfaces.Classify(_names, indoors, sidewalk);
        }

        // ------------------------------------------------------------------------------------------- the case

        private void OnKnocks()
        {
            Rig.PlayAt(AudioEvents.ClueKnocksDistant, _director.Knocks.SoundPosition);
        }

        private void OnCaseStep(CaseStep step)
        {
            foreach (var line in step.Lines)
            {
                if (line.Key == CaseLines.CaptionFoyerHum)
                {
                    // The hum comes up through the hall floor all around Pip, so it is not placed.
                    Rig.Play(AudioEvents.ClueFloorHum);
                    break;
                }
            }
        }

        /// <summary>The mechanism can be heard from inside the house, or in the crank cutaway that looks into the nook.</summary>
        private bool MechanismAudible => Indoors || (_director.Cutaway != null && _director.Cutaway.Active);

        private void OnMechanismKnock(int knock)
        {
            var workshop = _director.Workshop;
            if (!MechanismAudible || workshop == null)
            {
                return;
            }

            Vector3 at = workshop.Cam != null ? workshop.Cam.position
                : new Vector3(CaseRouteLayout.MechanismX, 0.9f, CaseRouteLayout.MechanismZ);
            Rig.PlayAt(AudioEvents.ClueCamTap, at);
        }

        private void OnMechanismCycle(bool wind)
        {
            UpdateIndoors();
            if (!wind || Indoors)
            {
                return;
            }

            var wheel = _game.Environment != null ? _game.Environment.WindWheel : null;
            if (wheel != null)
            {
                Rig.PlayAt(AudioEvents.ClueWindWheel, wheel.position);
            }
        }

        private void OnThresholdStarted(ThresholdEnd leaving)
        {
            Rig.Play(AudioEvents.UiPageTurn);
        }

        private void OnCue(string eventId)
        {
            if (Rig != null)
            {
                Rig.Play(eventId);
            }
        }

        // ------------------------------------------------------------------------------------------- UI and settings

        /// <summary>Gives every uGUI button in the scene the UI tap (new buttons are picked up within half a second).</summary>
        public void HookButtons()
        {
            foreach (var button in FindObjectsByType<Button>(FindObjectsInactive.Include, FindObjectsSortMode.None))
            {
                if (button == null || !_hooked.Add(button) || button.GetComponent<UiTapSound>() != null)
                {
                    continue;
                }

                UiTapSound.Attach(button, () => Rig.Play(AudioEvents.UiTap));
            }

            _hooked.RemoveWhere(b => b == null);
        }

        private void OnPreferencesChanged(PreferencesData preferences)
        {
            Preferences = preferences;
            Rig.ApplyPreferences(preferences);
            _saveClock = SaveDelay;
        }

        private void SavePreferences()
        {
            _saveClock = -1f;
            if (_store == null)
            {
                return;
            }

            try
            {
                _store.Save(Preferences);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[GameAudio] could not save preferences: " + e.Message);
            }
        }

        /// <summary>Applies and saves new volume preferences now (settings code and tests).</summary>
        public void SetPreferences(PreferencesData preferences)
        {
            OnPreferencesChanged(preferences);
            SavePreferences();
        }

        private void Update()
        {
            float unscaled = Time.unscaledDeltaTime;
            _scanClock += unscaled;
            if (_scanClock >= ButtonScanSeconds)
            {
                _scanClock = 0f;
                HookButtons();
            }

            if (_saveClock >= 0f)
            {
                _saveClock -= unscaled;
                if (_saveClock < 0f)
                {
                    SavePreferences();
                }
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return; // paused: the bed and the glide hold where they are
            }

            UpdateIndoors();
            float trim = AudioMix.Approach(_ambienceTrim, AudioMix.AmbienceTrimDb(Indoors), AudioMix.AmbienceFadeDbPerSecond * dt);
            // The cutoff moves on a log scale, in step with the level (one fade of the trim covers the whole sweep).
            float targetCutoff = AudioMix.AmbienceLowPassHz(Indoors);
            float logNow = Mathf.Log(_ambienceCutoff), logTarget = Mathf.Log(targetCutoff);
            float logRate = Mathf.Abs(Mathf.Log(AudioMix.OpenLowPassHz) - Mathf.Log(AudioMix.IndoorAmbienceLowPassHz)) *
                            AudioMix.AmbienceFadeDbPerSecond / Mathf.Abs(AudioMix.IndoorAmbienceDb) * dt;
            float cutoff = Mathf.Exp(AudioMix.Approach(logNow, logTarget, logRate));
            if (!Mathf.Approximately(trim, _ambienceTrim) || !Mathf.Approximately(cutoff, _ambienceCutoff))
            {
                _ambienceTrim = trim;
                _ambienceCutoff = cutoff;
                Rig.SetBusShaping(AudioBus.Ambience, _ambienceTrim, _ambienceCutoff);
            }

            if (_glideLoopAt >= 0f && Time.time >= _glideLoopAt)
            {
                _glideLoopAt = -1f;
                if (_pip.Gliding && !Rig.IsLooping(AudioEvents.MoveGlideLoop))
                {
                    Rig.Play(AudioEvents.MoveGlideLoop);
                }
            }
        }
    }
}
