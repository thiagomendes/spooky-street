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
    /// <item>Voices (D20.2): every spoken line the dialogue shows
    /// (<see cref="SpookyStreet.UI.DialogueView.LineShown"/>, Pip's own examine lines included) plays its speaker's
    /// babble in the line's emotion (<see cref="LineVoices"/>), the phrase take closest to the length the caption asks for (<see cref="BabblePicker"/>), on the Voice bus; music
    /// and the bed dip under it; the next line, Skip or closing stops it.</item>
    /// <item>Music (D20.2, <see cref="MusicPlan"/>): the street theme on the title and the street, then the
    /// exploration loop, crossfaded; exploration only and lower indoors; the case-closed sting when the notebook's
    /// "Case closed" card arrives; the cutscene hooks of <see cref="AudioCues"/> (PlayCue, StopCue, StopMusic,
    /// ResumeMusic, DuckMusic, Babble).</item>
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

        /// <summary>Skip, the next line or closing the conversation fades the babble out this fast (no click).</summary>
        public const float VoiceStopFade = 0.06f;

        /// <summary>How fast the music bus follows the place, the pause and a held duck (dB per second).</summary>
        public const float MusicTrimDbPerSecond = 12f;

        /// <summary>With nothing of the case on screen, the sting waits this long for the "Case closed" card.</summary>
        public const float StingWaitSeconds = 4f;

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
        private readonly BabblePicker _babble = new BabblePicker();
        private readonly MusicPlan _music = new MusicPlan();
        private readonly List<MusicCommand> _musicCommands = new List<MusicCommand>(4);
        private string _voiceEvent;
        private bool _musicHeld;
        private float _musicTrim;
        private float _stingWait = -1f;

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

        /// <summary>The score's state (loop, pass, cue).</summary>
        public MusicPlan Music => _music;

        /// <summary>Where the score thinks Pip is.</summary>
        public MusicPlace MusicPlace { get; private set; } = MusicPlace.Title;

        /// <summary>The last babble played (event, take, lengths, emotion).</summary>
        public BabblePlan LastBabble { get; private set; }

        /// <summary>The voice event of the line showing (null when no babble plays).</summary>
        public string VoiceEvent => _voiceEvent;

        /// <summary>The notebook shows its final "Case closed" card.</summary>
        public bool StampCardShowing
        {
            get
            {
                var notebook = _director != null ? _director.Notebook : null;
                return notebook != null && notebook.IsOpen && notebook.FinalCard != null &&
                       notebook.CurrentPage == notebook.PageCount - 1;
            }
        }

        /// <summary>True while the stamp's sting waits for its card.</summary>
        public bool StingPending => _stingWait >= 0f;

        /// <summary>A cutscene holds the music lower (<see cref="AudioCues.DuckMusic"/>).</summary>
        public bool MusicHeldDown => _musicHeld;

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
            AudioCues.MusicRequested += OnMusicRequest;
            AudioCues.BabbleRequested += OnBabbleRequest;
            if (_director != null)
            {
                if (_director.Dialogue != null)
                {
                    _director.Dialogue.LineShown += OnLineShown;
                    _director.Dialogue.Closed += OnDialogueClosed;
                }

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
            AudioCues.MusicRequested -= OnMusicRequest;
            AudioCues.BabbleRequested -= OnBabbleRequest;
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
                if (_director.Dialogue != null)
                {
                    _director.Dialogue.LineShown -= OnLineShown;
                    _director.Dialogue.Closed -= OnDialogueClosed;
                }

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
                StopVoice();
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

            // The wall moves reuse the jump and land sounds: the kick and the pull-up push like a jump, the hands
            // catching a ledge and the feet reaching the top land like a landing.
            if (step.WallJumped || step.ClimbStarted)
            {
                Rig.Play(AudioEvents.MoveJump);
                _cadence.Reset();
            }

            if (step.Landed || step.LedgeGrabbed || step.ClimbEnded)
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
            if (step.StampedCase)
            {
                // The sting plays as the notebook's "Case closed" card arrives (it slides in after the comic).
                _stingWait = StingWaitSeconds;
            }

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

        // ------------------------------------------------------------------------------------------- voices

        private void OnLineShown(ScriptLine line, string text)
        {
            StopVoice();
            if (CaseSpeakers.IsCaption(line.Speaker))
            {
                return;
            }

            PlayBabble(_babble.Next(line.Speaker, text, LineVoices.For(line.Key)));
        }

        private void OnDialogueClosed() => StopVoice();

        private void OnBabbleRequest(BabbleRequest request)
        {
            StopVoice();
            PlayBabble(_babble.NextFor(request.Speaker, request.Seconds, request.Emotion));
        }

        private void PlayBabble(BabblePlan plan)
        {
            if (!plan.IsValid)
            {
                return;
            }

            LastBabble = plan;
            if (Rig.PlayTake(plan.EventId, plan.Take) != null)
            {
                _voiceEvent = plan.EventId;
            }
        }

        /// <summary>Stops the babble of the line showing (Skip, the next line, closing).</summary>
        public void StopVoice()
        {
            if (_voiceEvent != null)
            {
                Rig.Stop(_voiceEvent, VoiceStopFade);
                _voiceEvent = null;
            }
        }

        // ------------------------------------------------------------------------------------------- music

        private void OnMusicRequest(MusicRequest request)
        {
            _musicCommands.Clear();
            switch (request.Kind)
            {
                case MusicRequestKind.PlayCue:
                    _music.PlayCue(request.CueId, _musicCommands);
                    break;
                case MusicRequestKind.StopCue:
                    _music.StopCue(request.FadeSeconds, _musicCommands);
                    break;
                case MusicRequestKind.StopAll:
                    _music.StopAll(request.FadeSeconds, _musicCommands);
                    break;
                case MusicRequestKind.Resume:
                    _music.Resume();
                    break;
                case MusicRequestKind.Duck:
                    _musicHeld = request.Ducked;
                    break;
            }

            ApplyMusic();
        }

        /// <summary>Plays a music cue now, as <see cref="AudioCues.PlayCue"/> does (tests and the sting).</summary>
        public bool PlayMusicCue(string cueId)
        {
            _musicCommands.Clear();
            bool played = _music.PlayCue(AudioCues.ResolveMusic(cueId), _musicCommands);
            ApplyMusic();
            return played;
        }

        private void ApplyMusic()
        {
            foreach (var command in _musicCommands)
            {
                if (command.Kind == MusicCommandKind.Start)
                {
                    Rig.PlayTake(command.EventId, 1, command.FadeSeconds);
                }
                else
                {
                    Rig.Stop(command.EventId, command.FadeSeconds);
                }
            }

            _musicCommands.Clear();
        }

        private void UpdateMusic(float unscaled)
        {
            MusicPlace = _flow.State == GameState.Title ? MusicPlace.Title
                : Indoors ? MusicPlace.Indoors
                : MusicPlace.Street;
            _musicCommands.Clear();
            _music.Update(unscaled, MusicPlace, _musicCommands);
            ApplyMusic();

            float target = MusicPlan.TrimDb(MusicPlace, _flow.State == GameState.Paused, _musicHeld);
            float trim = AudioMix.Approach(_musicTrim, target, MusicTrimDbPerSecond * unscaled);
            if (!Mathf.Approximately(trim, _musicTrim) || Rig.BusTrimDb(AudioBus.Music) != trim)
            {
                _musicTrim = trim;
                Rig.SetBusShaping(AudioBus.Music, _musicTrim, AudioMix.OpenLowPassHz);
            }

            if (_stingWait >= 0f)
            {
                // The stamp's step happened: wait for its card. The card slides in after the last comic, or opens
                // after the reopening when the show stamps the case; the wait only runs out while nothing of the case
                // is on screen (the child closed the notebook first), and then the sting is dropped, not played late.
                var notebook = _director != null ? _director.Notebook : null;
                if (StampCardShowing)
                {
                    _stingWait = -1f;
                    PlayMusicCue(VoiceMusicEvents.MusicCaseClosed);
                }
                else if (notebook == null ||
                         (!notebook.IsOpen && !_director.FinalePlaying && !_director.Dialogue.IsOpen))
                {
                    _stingWait -= unscaled;
                    if (_stingWait < 0f)
                    {
                        _stingWait = -1f;
                    }
                }
                else
                {
                    _stingWait = StingWaitSeconds;
                }
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

            UpdateMusic(unscaled); // the score runs on unscaled time: it carries on under the pause card

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
