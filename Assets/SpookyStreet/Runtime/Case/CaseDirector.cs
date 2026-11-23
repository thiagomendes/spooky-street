using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using SpookyStreet.App;
using SpookyStreet.Characters;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Save;
using SpookyStreet.Core.World;
using SpookyStreet.Player;
using SpookyStreet.Save;
using SpookyStreet.UI;
using SpookyStreet.World;
using UnityEngine;
using UnityEngine.UI;
using NoteKeys = SpookyStreet.Core.Localization.LocalizationKeys.Notebook;
using SaveKeys = SpookyStreet.Core.Localization.LocalizationKeys.Save;
using UiKeys = SpookyStreet.Core.Localization.LocalizationKeys.CaseUi;

namespace SpookyStreet.Case
{
    /// <summary>
    /// Runs case 01 in the world (S002 wiring, exterior half). Owns the <see cref="CaseState"/>; every change goes
    /// through <see cref="Perform"/>, which applies the pure reducer, autosaves when facts were gained, stages the
    /// world's reaction (knocks, bench view, witnesses talking) and plays the step's lines: captions in the
    /// <see cref="CaptionStrip"/>, everything else on the <see cref="DialogueView"/> card.
    /// On boot it loads the save and rebuilds the world from <see cref="CaseWorldView"/> before any control is
    /// enabled (spec §7); Continue places Pip at the saved safe anchor. Pause freezes captions, the knock beat, the
    /// bench view and dialogue input; opening the notebook or a conversation suspends gameplay input and hides the HUD.
    /// </summary>
    public sealed class CaseDirector : MonoBehaviour
    {
        /// <summary>Interaction is blocked this long after a modal closes, so its last press cannot re-trigger it.</summary>
        public const float InteractCooldown = 0.3f;

        /// <summary>Tests point saves at a temporary folder; null uses Application.persistentDataPath.</summary>
        public static string SaveDirectoryOverride { get; set; }

        private GameBootstrap _game;
        private GameFlow _flow;
        private PipMotor _pip;
        private GameUi _ui;
        private Camera _camera;
        private CanvasGroup _hudGroup;
        private CaseSaveStore _store;
        private HintSession _hints = new HintSession();
        private Witness _talking;
        private bool _modal;
        private int _reenableFrame = -1;
        private float _cooldown;
        private bool _continuing;
        private float _walkedAtStart;
        private bool _onPorch;
        private bool _inAtticEntry;
        private bool _inFoyer;
        private bool _juneWalkPending;
        private bool _finaleLines;
        private float _finaleHold = -1f;
        private float _nookPeek = -1f;
        private float _finaleIntro = -1f;
        private IReadOnlyList<ScriptLine> _finaleIntroLines;
        private readonly List<Renderer> _hiddenPip = new List<Renderer>();
        private int _baseObstacles;
        private int _indoorObstacles;
        private Vector3 _lastPip;
        private bool _hasLastPip;
        private readonly InteriorRescue _rescue = new InteriorRescue();
        private CharacterController _pipController;

        /// <summary>Times the interior safety net set Pip down on a safe anchor (tests, diagnostics).</summary>
        public int Rescues { get; private set; }

        /// <summary>The interior safety net's state (tests, diagnostics).</summary>
        public InteriorRescue Rescue => _rescue;

        /// <summary>Why the safety net last moved Pip.</summary>
        public RescueReason LastRescue { get; private set; }

        /// <summary>Frames the safety net slid Pip off a character's head (June) instead of moving her (tests).</summary>
        public int SlideOffFrames { get; private set; }

        /// <summary>A jump of Pip's position larger than this in one frame is a teleport: the camera cuts.</summary>
        public const float TeleportCutDistance = 6f;
        private CaseWorldView _view = CaseWorldView.From(CaseState.Empty);

        public CaseState State { get; private set; } = CaseState.Empty;

        /// <summary>What the boot load found.</summary>
        public CaseResume Resume { get; private set; }

        /// <summary>A case save exists on disk (Continue is offered on the title).</summary>
        public bool HasSave { get; private set; }

        /// <summary>The last safe anchor written or restored.</summary>
        public string AnchorId { get; private set; } = CaseAnchors.Start;

        /// <summary>The last step applied (tests and debugging).</summary>
        public CaseStep LastStep { get; private set; }

        public string SaveDirectory { get; private set; }

        public CaseUseTargets Targets { get; private set; }
        public CaptionStrip Captions { get; private set; }
        public DialogueView Dialogue { get; private set; }
        public NotebookView Notebook { get; private set; }
        public NoticeCard Notice { get; private set; }
        public InteractionPrompt Prompt { get; private set; }
        public Button NotebookButton { get; private set; }
        public BenchView Bench { get; private set; }
        public KnockBeat Knocks { get; private set; }

        /// <summary>The interior half's set: route and interior models, colliders, interior on or off, door (null without models).</summary>
        public OwlHouseStage House { get; private set; }

        /// <summary>The workshop mechanism and the rooftop owl (null without models).</summary>
        public WorkshopView Workshop { get; private set; }

        /// <summary>The owl house thresholds and their paper page-turn (D24.1; null without models).</summary>
        public ThresholdTravel Travel { get; private set; }

        /// <summary>June and Aunt Kit (null without models).</summary>
        public CaseCast Cast { get; private set; }

        public CutawayView Cutaway { get; private set; }
        public FinaleView Finale { get; private set; }

        /// <summary>The animated non-verbal UX (D23): replays, bat trail, reading settings (NonverbalLayer).</summary>
        public NonverbalLayer Nonverbal { get; private set; }

        /// <summary>The witness in the current conversation (null otherwise).</summary>
        public Witness Talking => _talking;

        /// <summary>The world view the stage shows (derived from the facts).</summary>
        public CaseWorldView View => _view;

        /// <summary>True while the finale's framing holds (lines, then a beat before the notebook).</summary>
        public bool FinalePlaying => Finale != null && Finale.Active;

        /// <summary>
        /// Spec §5: entering the attic blends to a closer shoulder view. Indoors the camera follows the house's room
        /// profiles and never looks through a solid (<see cref="InteriorFraming"/>).
        /// </summary>
        public InteriorFraming Framing { get; private set; }

        /// <summary>The canvas holding captions, the interaction marker, dialogue and the notebook.</summary>
        public Canvas UiCanvas { get; private set; }

        /// <summary>True while a conversation or the notebook is open.</summary>
        public bool ModalOpen => _modal;

        /// <summary>Raised after every applied step.</summary>
        public event Action<CaseStep> Stepped;

        /// <summary>Points saves at a fresh temporary folder (PlayMode and visual tests; call before loading the scene).</summary>
        public static string UseTemporarySaveDirectory()
        {
            string directory = Path.Combine(Application.temporaryCachePath, "case-saves-" + Guid.NewGuid().ToString("N"));
            Directory.CreateDirectory(directory);
            SaveDirectoryOverride = directory;
            return directory;
        }

        public static CaseDirector Create(GameBootstrap game)
        {
            var go = new GameObject("CaseDirector");
            var director = go.AddComponent<CaseDirector>();
            director.Initialize(game);
            return director;
        }

        private void Initialize(GameBootstrap game)
        {
            _game = game;
            _flow = game.Flow;
            _pip = game.Pip;
            _ui = game.Ui;
            _camera = game.CameraRig.OutputCamera;

            BuildUi();
            int sightMask = Physics.DefaultRaycastLayers & ~(1 << _pip.gameObject.layer);
            bool models = game.Environment != null && game.Environment.UsesModels;
            Targets = new CaseUseTargets(models ? CaseStage.AllPoints : CaseStage.ExteriorPoints, point => Perform(point.Action),
                sightMask) { Filter = IsOffered };
            _pip.UseTargets = Targets;
            Bench = BenchView.Create(transform, _pip, game.Controls, _camera);
            var anchors = game.Environment != null ? game.Environment.Anchors : default;
            Knocks = KnockBeat.Create(transform, game.Materials, anchors.AtticLight, models);
            if (models)
            {
                House = OwlHouseStage.Create(transform, game.Environment, game.Materials);
                Workshop = WorkshopView.Create(transform, House.Mechanism, game.Environment.OwlPuppet);
                Cast = CaseCast.Create(transform, game.Materials, _pip.transform);
                Framing = new InteriorFraming(game.CameraRig);
                Travel = ThresholdTravel.Create(transform, _pip);
                Travel.DoorOpen = () => House.DoorOpen;
                Travel.Started += OnCrossingStarted;
                Travel.Moved += OnCrossingMoved;
                Travel.Finished += OnCrossingFinished;
            }

            Cutaway = CutawayView.Create(transform, _camera, (RectTransform)UiCanvas.transform);
            Finale = FinaleView.Create(transform);
            _baseObstacles = game.CameraRig.ObstacleLayers;
            int safe = LayerMask.NameToLayer(SafeGroundTracker.SafeGroundLayerName);
            int ignore = LayerMask.NameToLayer("Ignore Raycast");
            _indoorObstacles = _baseObstacles & ~(safe >= 0 ? 1 << safe : 0) & ~(ignore >= 0 ? 1 << ignore : 0);

            // Title: Continue resumes the save; New game asks first when a case is in progress.
            _ui.ContinueButton.onClick.AddListener(OnContinue);
            _ui.NewGameButton.onClick.RemoveAllListeners();
            _ui.NewGameButton.onClick.AddListener(OnNewGame);

            LoadSave();
            Nonverbal = NonverbalLayer.Create(this, game);
            _flow.StateChanged += OnStateChanged;
        }

        private void BuildUi()
        {
            var canvas = UiFactory.CreateCanvas("CaseUi", 5);
            canvas.transform.SetParent(transform, false);
            UiCanvas = canvas;
            var root = (RectTransform)canvas.transform;
            Prompt = InteractionPrompt.Create(root);
            Dialogue = DialogueView.Create(root);
            Dialogue.CanAdvance = () => _flow.State == GameState.Playing;
            Dialogue.Finished += OnDialogueFinished;
            Notebook = NotebookView.Create(root);
            Notebook.State = () => State;
            Notebook.RequestHint = RequestHint;
            Notebook.ReplayRequested = ConfirmReplay;
            Notebook.Closed += () => SetModal(false);
            var safe = UiFactory.Stretch(UiFactory.Rect(root, "CaptionSafeArea"));
            safe.gameObject.AddComponent<SafeAreaFitter>();
            Captions = CaptionStrip.Create(safe);
            Captions.ViewCamera = _camera;
            Captions.Listener = () => _pip.transform.position;
            Notebook.ShowCaption = key => Captions.ShowNow(key);

            var notices = UiFactory.CreateCanvas("CaseNotices", 15);
            notices.transform.SetParent(transform, false);
            Notice = NoticeCard.Create(notices.transform);

            // HUD: the notebook disc joins Pause top-right (left-handed layouts mirror it with the cluster).
            var pause = (RectTransform)_ui.PauseButton.transform;
            bool mirrored = pause.anchorMin.x < 0.5f;
            var disc = UiFactory.FeltDisc(_ui.Hud.transform, "NotebookButton", UiSpriteCatalog.FeltDiscNight,
                Palette.WarmPaper, UiSpriteCatalog.IconNotebook, Palette.WarmPaper, null, UiFactory.SystemDiscDiameter,
                pause.anchorMin, pause.anchoredPosition + new Vector2(mirrored ? 118f : -118f, 0f), UiFactory.HudFeltAlpha);
            var button = disc.gameObject.AddComponent<Button>();
            button.targetGraphic = disc;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(OpenNotebook);
            NotebookButton = button;

            _hudGroup = _ui.Hud.GetComponent<CanvasGroup>();
            if (_hudGroup == null)
            {
                _hudGroup = _ui.Hud.AddComponent<CanvasGroup>();
            }
        }

        private void OnDestroy()
        {
            if (_flow != null)
            {
                _flow.StateChanged -= OnStateChanged;
            }
        }

        // ------------------------------------------------------------------------------------------- save and resume

        private void LoadSave()
        {
            SaveDirectory = SaveDirectoryOverride ?? Application.persistentDataPath;
            _store = new CaseSaveStore(new DiskSaveFileSystem(), SaveDirectory);
            try
            {
                Resume = _store.Load();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[CaseDirector] Save load failed: " + e.Message);
                Resume = null;
            }

            HasSave = Resume != null && Resume.CanContinue;
            State = HasSave ? Resume.State : CaseState.Empty;
            AnchorId = HasSave ? Resume.AnchorId : CaseAnchors.Start;
            // Spec §7: restore props from facts before enabling controls (the title is up; play has not started).
            ApplyWorld();
            _ui.SetHasSave(HasSave && State.Count > 0);
            if (Debug.isDebugBuild)
            {
                Debug.Log($"[CaseDirector] Save {Resume?.Status} in {SaveDirectory}: {State}, anchor {AnchorId}");
            }

            if (Resume != null && Resume.NoticeBodyKey != null)
            {
                if (Resume.OffersFreshStart)
                {
                    Notice.Show(Resume.NoticeHeadingKey, Resume.NoticeBodyKey, SaveKeys.StartFresh, StartFreshAfterNotice,
                        SaveKeys.Back, null);
                }
                else
                {
                    Notice.Show(null, Resume.NoticeBodyKey, SaveKeys.Back, null);
                }
            }
        }

        private void StartFreshAfterNotice()
        {
            TryStore(() => _store.StartFresh());
            ResetCase();
            _ui.SetHasSave(false);
        }

        /// <summary>Writes the case now (fact changes and closure). Returns false when nothing was written.</summary>
        public bool Autosave()
        {
            var tracker = _pip.GetComponent<SafeGroundTracker>();
            Vector3 safe = tracker != null && tracker.HasAnchor ? tracker.Anchor : _pip.transform.position;
            string anchor = CaseStage.NearestAnchor(safe.x, safe.y, safe.z);
            bool written = false;
            TryStore(() => written = _store.Save(State, anchor));
            if (written)
            {
                AnchorId = anchor;
                HasSave = true;
            }

            return written;
        }

        private static void TryStore(Action action)
        {
            try
            {
                action();
            }
            catch (Exception e)
            {
                // A failed write never stops play; the previous copy stays valid (SaveWritePlanner).
                Debug.LogWarning("[CaseDirector] Save failed: " + e.Message);
            }
        }

        /// <summary>Rebuilds every staged prop from facts (resume, new game, replay).</summary>
        private void ApplyWorld()
        {
            var view = CaseWorldView.From(State);
            _view = view;
            Knocks.SetHeard(view.PaperBatFallen);
            _juneWalkPending = false;
            _finaleLines = false;
            _finaleHold = -1f;
            _finaleIntro = -1f;
            _finaleIntroLines = null;
            _nookPeek = -1f;
            Cutaway.End(false);
            Finale.End();
            Travel?.Cancel();
            Travel?.ResetTracker();
            if (House != null)
            {
                House.SetDoor(view.FrontDoor == DoorState.Open);
                House.NookWallHidden = false;
            }

            if (Workshop != null)
            {
                Workshop.SetState(view.ServicePanelOpen, view.ArmLinked, view.Owl == OwlState.Bowing);
                Workshop.WindEnabled = view.PaperBatFallen;
            }

            Cast?.Apply(view);
        }

        /// <summary>Whether a Use target exists in the world right now (June's mark, the panel, the latch...).</summary>
        private bool IsOffered(InteractionPoint point)
        {
            Vector3 pip = _pip.transform.position;
            return CaseStage.IsOffered(point, _view, CaseRouteLayout.Inside(pip.x, pip.z), Cast != null && Cast.JuneWalking);
        }

        private void ResetCase()
        {
            State = CaseState.Empty;
            AnchorId = CaseAnchors.Start;
            _hints = new HintSession();
            CloseModals();
            Captions.Clear();
            Bench.End();
            ApplyWorld();
            Nonverbal?.OnCaseReset();
        }

        /// <summary>The radio (HUD disc or notebook): the next hint for the current objective (returns its key).</summary>
        public string RequestHint()
        {
            var tier = _hints.NextTier(State);
            string key = _hints.Request(State);
            Nonverbal?.OnHintGiven(tier, State);
            return key;
        }

        /// <summary>The tier the radio gives next.</summary>
        public HintTier NextHintTier => _hints.NextTier(State);

        // ------------------------------------------------------------------------------------------------- title

        private void OnContinue()
        {
            _continuing = true;
            _flow.StartNewGame();
        }

        private void OnNewGame()
        {
            if (State.Count > 0)
            {
                Notice.Show(null, UiKeys.NewGameConfirm, UiKeys.NewGameConfirmYes, StartNewCase, UiKeys.NewGameConfirmNo,
                    null);
                return;
            }

            StartNewCase();
        }

        private void StartNewCase()
        {
            if (HasSave)
            {
                TryStore(() => _store.StartFresh());
            }

            ResetCase();
            _continuing = false;
            _flow.StartNewGame();
        }

        private void OnStateChanged(GameState previous, GameState current)
        {
            if (previous == GameState.Title && current == GameState.Playing)
            {
                // GameBootstrap has already placed Pip on the arrival porch; Continue moves Pip to the saved anchor.
                if (_continuing)
                {
                    var point = CaseStage.RestorePoint(AnchorId, State, out string resolved);
                    AnchorId = resolved;
                    _pip.Teleport(new Vector3(point.X, point.Y, point.Z));
                    _continuing = false;
                }

                // A resume in the interior starts there, with the camera already in the room.
                CutCamera();
                Travel?.ResetTracker();
                _walkedAtStart = _pip.DistanceWalked;
                _onPorch = CaseStage.OnArrivalPorch(_pip.transform.position.x, _pip.transform.position.z);
                _cooldown = InteractCooldown;
            }

            if (current == GameState.Title)
            {
                ResetPresentation();
                _ui.SetHasSave(HasSave && State.Count > 0);
            }

            if (current == GameState.Playing && (_modal || (Travel != null && Travel.Active)))
            {
                // GameBootstrap re-enabled gameplay input on resume; a conversation, the notebook or a page-turn still
                // owns it (a crossing gives it back itself when the page has gone).
                _game.Controls.SetGameplayEnabled(false);
            }
        }

        private void ResetPresentation()
        {
            Travel?.Cancel();
            Framing?.Release();
            CloseModals();
            Captions.Clear();
            Bench.End();
            Finale.End();
            _finaleLines = false;
            _finaleHold = -1f;
            _finaleIntro = -1f;
            _finaleIntroLines = null;
        }

        /// <summary>The character Pip is talking to, or the one the Use prompt offers to talk to (kept in the camera's view).</summary>
        private Vector3? FocusPoint()
        {
            var witness = _talking;
            if (witness == null && Targets.HasTarget && Targets.Current.Action.Kind == CaseActionKind.Talk)
            {
                witness = WitnessFor(Targets.Current.Action.Speaker);
            }

            return witness != null && witness.gameObject.activeInHierarchy ? witness.transform.position : (Vector3?)null;
        }

        /// <summary>
        /// Puts the camera straight behind Pip in Pip's place (a threshold crossing, a resume, a replay, any teleport):
        /// the room's framing indoors, the follow camera outside, with nothing eased across the cut, so the lens never
        /// travels through the void between the street and the interior.
        /// </summary>
        public void CutCamera()
        {
            var rig = _game.CameraRig;
            if (rig == null)
            {
                return;
            }

            Vector3 pip = _pip.transform.position;
            float yaw = _pip.transform.eulerAngles.y;
            bool inside = CaseRouteLayout.Inside(pip.x, pip.z);
            rig.ObstacleLayers = inside ? _indoorObstacles : _baseObstacles;
            House?.Tick(pip, pip);
            HideSkyIndoors(pip, pip);
            rig.Orbit.Snap(yaw);
            if (Framing != null)
            {
                Framing.Snap(pip, House != null && House.DoorOpen);
            }

            rig.Cut(yaw);
            _lastPip = pip;
            _hasLastPip = true;
        }

        /// <summary>The interior half each frame: doors, interior on or off, camera indoors, the nook peek, the finale beat.</summary>
        private void TickStage(Vector3 pip, float dt)
        {
            if (House != null)
            {
                if (_hasLastPip && (pip - _lastPip).sqrMagnitude > TeleportCutDistance * TeleportCutDistance)
                {
                    // Pip was moved far in one frame (a teleport): cut, never ease across.
                    CutCamera();
                }

                _lastPip = pip;
                _hasLastPip = true;
                House.Tick(pip, _camera.transform.position);
                HideSkyIndoors(pip, _camera.transform.position);
                Cast?.ShowJuneIndoors(House.InteriorShown);
                bool inside = CaseRouteLayout.Inside(pip.x, pip.z);
                var rig = _game.CameraRig;
                rig.ObstacleLayers = inside ? _indoorObstacles : _baseObstacles;
                Framing.Tick(pip, House.DoorOpen, FocusPoint(), dt);
                if (_nookPeek >= 0f && dt > 0f)
                {
                    _nookPeek -= dt;
                    if (_nookPeek < 0f && !Cutaway.Active)
                    {
                        House.NookWallHidden = false;
                    }
                }
            }

            if (_finaleIntro >= 0f && dt > 0f)
            {
                _finaleIntro -= dt;
                if (_finaleIntro < 0f)
                {
                    var lines = _finaleIntroLines;
                    _finaleIntroLines = null;
                    if (lines != null && lines.Count > 0)
                    {
                        PlayLines(CaseAction.Of(CaseActionKind.ArriveAtPorch), lines);
                    }
                    else
                    {
                        SetModal(false);
                        _finaleLines = false;
                        _finaleHold = FinaleView.HoldAfterLines;
                    }
                }
            }

            if (_finaleHold >= 0f && dt > 0f && !_modal)
            {
                _finaleHold -= dt;
                if (_finaleHold < 0f)
                {
                    Finale.End();
                    OpenNotebook();
                }
            }
        }

        // ----------------------------------------------------------------------------------------------- the case

        /// <summary>
        /// Applies <paramref name="action"/> to the case: the one way the world changes it. Autosaves on gained facts,
        /// stages the reaction and plays the lines. Returns the step.
        /// </summary>
        public CaseStep Perform(CaseAction action)
        {
            var step = State.Apply(action);
            State = step.State;
            _view = CaseWorldView.From(State);
            LastStep = step;
            if (step.ShouldAutosave)
            {
                Autosave();
            }

            Stage(action, step);
            if (step.ClosedCase && _finaleIntro >= 0f)
            {
                // The reopening first plays without the card; its lines follow (TickStage).
                _finaleIntroLines = step.Lines;
            }
            else
            {
                PlayLines(action, step.Lines);
            }
            Stepped?.Invoke(step);
            return step;
        }

        private void Stage(CaseAction action, CaseStep step)
        {
            switch (action.Kind)
            {
                case CaseActionKind.HearKnocks:
                    if (step.Gained.Count > 0)
                    {
                        Knocks.Play();
                    }

                    break;
                case CaseActionKind.SitOnBench:
                    Bench.Begin();
                    break;
                case CaseActionKind.Talk:
                    if (action.Speaker == Speaker.June && step.Gained.Contains(CaseFact.FoundJune))
                    {
                        // She walks down to her mark once her lines are done.
                        _juneWalkPending = true;
                    }

                    break;
                case CaseActionKind.LiftLatch:
                    if (step.Gained.Contains(CaseFact.FrontOpen))
                    {
                        House?.OpenDoor();
                    }

                    break;
                case CaseActionKind.ExamineGrille:
                    // Through the grille the follower visibly lifts three times; seeing it opens the service panel.
                    if (Workshop != null)
                    {
                        Workshop.OpenPanel();
                        if (!Workshop.Playing)
                        {
                            Workshop.PlayCycle();
                        }

                        PeekIntoNook(MechanismMotion.CycleSeconds + 0.4f);
                    }

                    break;
                case CaseActionKind.LinkArm:
                    if (step.Gained.Contains(CaseFact.LinkedArm))
                    {
                        Workshop?.LinkArm();
                        PeekIntoNook(1.2f);
                    }

                    break;
                case CaseActionKind.TurnCrank:
                    StageCrank(step);
                    break;
                case CaseActionKind.ArriveAtPorch:
                    if (step.ClosedCase)
                    {
                        StartFinale();
                    }

                    break;
            }

            if (step.Gained.Contains(CaseFact.HeardKnocks) && Workshop != null)
            {
                Workshop.WindEnabled = true;
            }
        }

        private void StageCrank(CaseStep step)
        {
            if (Workshop == null)
            {
                return;
            }

            if (step.Outcome == CaseOutcome.CrankGap)
            {
                // One tap and a visible gap where the arm should connect; no fact.
                Workshop.PlayGap();
                PeekIntoNook(MechanismMotion.GapCycleSeconds + 0.6f);
                return;
            }

            Workshop.PlayCycle();
            if (step.Gained.Contains(CaseFact.TestedOwl))
            {
                Workshop.SetBowing();
            }

            if (step.PlayCutaway)
            {
                PlayCutaway();
            }
            else
            {
                PeekIntoNook(MechanismMotion.CycleSeconds + 0.4f);
            }
        }

        /// <summary>Hides the workshop's near wall for a moment so the mechanism reads from where Pip stands.</summary>
        private void PeekIntoNook(float seconds)
        {
            if (House == null)
            {
                return;
            }

            House.NookWallHidden = true;
            _nookPeek = Mathf.Max(_nookPeek, seconds);
        }

        /// <summary>The crank cutaway's eye and look-at (house v3 §3.4, interior builder (0.30, 4.90, 2.10) → (2.90, 4.50, 1.50)).</summary>
        public static Vector3 CutawayEye => new Vector3(0.3f, 2.1f, CaseRouteLayout.InteriorFrontZ + 4.9f);

        public static Vector3 CutawayLookAt => new Vector3(2.9f, 1.5f, CaseRouteLayout.InteriorFrontZ + 4.5f);

        /// <summary>The 4 s skippable cutaway: cam, taps and linkage in the nook, the owl bowing in the inset.</summary>
        private void PlayCutaway()
        {
            if (House == null || Workshop == null)
            {
                return;
            }

            // House v3 §3.4: from the hall, over the partition into the nook with its near wall hidden.
            Vector3 eye = CutawayEye;
            Vector3 mechanism = CutawayLookAt;
            // The inset still looks at the rooftop owl on the street: the interior is a separate place (D24.1).
            Vector3 owl = Workshop.Owl != null ? ModelKit.RendererBounds(Workshop.Owl.gameObject).center
                : new Vector3(0f, 7f, CaseRouteLayout.FrontZ);
            Vector3 owlEye = owl + new Vector3(1.8f, -0.6f, -4.2f);
            House.NookWallHidden = true;
            _nookPeek = -1f;
            Bench.End();
            SetModal(true);
            SetPipHidden(true);
            Cutaway.Play(eye, mechanism, owlEye, owl, OnCutawayEnded);
        }

        private void SetPipHidden(bool hidden)
        {
            if (hidden)
            {
                foreach (var renderer in _pip.GetComponentsInChildren<Renderer>())
                {
                    if (renderer.enabled)
                    {
                        renderer.enabled = false;
                        _hiddenPip.Add(renderer);
                    }
                }

                return;
            }

            foreach (var renderer in _hiddenPip)
            {
                if (renderer != null)
                {
                    renderer.enabled = true;
                }
            }

            _hiddenPip.Clear();
        }

        private void OnCutawayEnded()
        {
            SetPipHidden(false);
            if (House != null)
            {
                House.NookWallHidden = false;
            }

            SetModal(false);
        }

        /// <summary>The reopening (spec §3 Resolve): Kit at her booth, June beside her, the owl bowing, the arrival view.</summary>
        private void StartFinale()
        {
            // The reopening starts clean: no caption from an earlier beat carries over into the finale shot.
            Captions.Clear();
            Cutaway.End();
            Cast?.StageReopening();
            Workshop?.PlayCycle();
            Bench.End();
            Finale.Begin();
            _finaleLines = true;
            _finaleHold = -1f;
            _finaleIntro = FinaleView.IntroSeconds;
            SetModal(true);
        }

        private void PlayLines(CaseAction action, IReadOnlyList<ScriptLine> lines)
        {
            var spoken = new List<ScriptLine>(lines.Count);
            foreach (var line in lines)
            {
                if (CaseSpeakers.IsCaption(line.Speaker))
                {
                    // Only a sound from somewhere (the knocks, the hum) gets a direction cue; what Pip sees does not.
                    // Captions describe what Pip perceives where she is (D26): indoors the knocks come up through the
                    // floor from the workshop, never "from the blue house" off to one side.
                    Vector3 at = _pip.transform.position;
                    bool indoors = CaptionPerception.Indoors(at.x, at.z);
                    string key = CaptionPerception.KeyFor(line.Key, indoors);
                    Captions.ShowKey(key, CaptionDirection.IsDirectionalSound(key) ? SourceOf(action, indoors) : null);
                }
                else
                {
                    spoken.Add(line);
                }
            }

            if (spoken.Count == 0)
            {
                return;
            }

            _talking = action.Kind == CaseActionKind.Talk ? WitnessFor(action.Speaker) : null;
            if (_talking != null)
            {
                _talking.Conversing = true;
                FacePip(_talking.transform.position);
            }

            Bench.End();
            SetModal(true);
            Captions.Raised = true;
            Dialogue.Play(spoken);
        }

        /// <summary>World position a caption's direction cue points at (the attic window for the knocks on the street,
        /// the workshop under the floor for the knocks heard indoors).</summary>
        private Func<Vector3?> SourceOf(CaseAction action, bool indoors)
        {
            if (action.Kind == CaseActionKind.HearKnocks && !indoors && _game.Environment != null)
            {
                Vector3 attic = _game.Environment.Anchors.AtticWindow;
                return () => attic;
            }

            var workshop = new Vector3(CaseRouteLayout.MechanismX + 0.5f, 0.9f, CaseRouteLayout.MechanismZ);
            switch (action.Kind)
            {
                case CaseActionKind.HearKnocks:
                case CaseActionKind.EnterFoyer:
                case CaseActionKind.ExamineGrille:
                case CaseActionKind.LinkArm:
                    return () => workshop;
                case CaseActionKind.TurnCrank:
                    var owl = Workshop != null ? Workshop.Owl : null;
                    return () => owl != null ? owl.position : workshop;
                default:
                    return null;
            }
        }

        private Witness WitnessFor(Speaker speaker)
        {
            var cast = _game.Witnesses;
            if (cast == null)
            {
                return Cast != null ? Cast.For(speaker) : null;
            }

            switch (speaker)
            {
                case Speaker.Bea:
                    return cast.Bea;
                case Speaker.Vale:
                    return cast.Vale;
                default:
                    return Cast != null ? Cast.For(speaker) : null;
            }
        }

        private void FacePip(Vector3 target)
        {
            Vector3 to = target - _pip.transform.position;
            to.y = 0f;
            if (to.sqrMagnitude > 0.01f)
            {
                _pip.transform.rotation = Quaternion.LookRotation(to, Vector3.up);
            }
        }

        private void OnDialogueFinished()
        {
            Captions.Raised = false;
            if (_talking != null)
            {
                _talking.Conversing = false;
                _talking = null;
            }

            SetModal(false);
            if (_juneWalkPending)
            {
                _juneWalkPending = false;
                Cast?.WalkJuneToFoyer();
            }

            if (_finaleLines)
            {
                // The reopening lines are done: hold the restored view a beat, then the notebook's final card.
                _finaleLines = false;
                _finaleHold = FinaleView.HoldAfterLines;
            }
        }

        // ------------------------------------------------------------------------------------------ thresholds

        /// <summary>The page starts to turn: input is ignored and the HUD steps aside until it has gone.</summary>
        private void OnCrossingStarted(ThresholdEnd leaving)
        {
            Bench.End();
            _game.Controls.SetGameplayEnabled(false);
            _reenableFrame = -1;
            _hudGroup.alpha = 0f;
            _hudGroup.blocksRaycasts = false;
            _hudGroup.interactable = false;
        }

        /// <summary>Under the page: Pip is already at the arrival point; the camera cuts into the new place.</summary>
        private void OnCrossingMoved(ThresholdEnd leaving, ThresholdEnd arriving)
        {
            // A sound still captioned belongs to the place Pip left (D26): the street's knocks are not heard indoors.
            Captions.DropSounds();
            CutCamera();
            if (Debug.isDebugBuild)
            {
                Debug.Log($"[CaseDirector] Crossed {leaving} -> {arriving} at {_pip.transform.position:F2}, " +
                          $"camera {(Framing != null && Framing.Active ? Framing.Shot.ToString() : "outdoor")}");
            }
        }

        /// <summary>The page has gone: the player has control again (the arrival zones report on the next frame).</summary>
        private void OnCrossingFinished(ThresholdEnd arrived)
        {
            if (_modal)
            {
                return;
            }

            _reenableFrame = Time.frameCount + 1;
            _hudGroup.alpha = 1f;
            _hudGroup.blocksRaycasts = true;
            _hudGroup.interactable = true;
        }

        // -------------------------------------------------------------------------------------------- notebook

        public void OpenNotebook()
        {
            if (_flow.State != GameState.Playing || _modal)
            {
                return;
            }

            Bench.End();
            SetModal(true);
            Notebook.Open();
        }

        private void ConfirmReplay()
        {
            Notice.Show(null, NoteKeys.ReplayConfirm, NoteKeys.ReplayConfirmYes, Replay, NoteKeys.ReplayConfirmNo, null);
        }

        /// <summary>Confirmed "Replay case": resets only case data and starts again on the arrival porch.</summary>
        public void Replay()
        {
            TryStore(() => _store.Replay());
            HasSave = true;
            ResetCase();
            CaseStage.TryGetAnchor(CaseAnchors.ArrivalPorch, out var porch);
            _pip.Teleport(new Vector3(porch.X, porch.Y, porch.Z));
            _pip.transform.rotation = Quaternion.identity;
            CutCamera();
            Travel?.ResetTracker();
            _walkedAtStart = _pip.DistanceWalked;
            _onPorch = true;
        }

        // ------------------------------------------------------------------------------------------------ modal

        private void SetModal(bool open)
        {
            _modal = open;
            if (open)
            {
                _game.Controls.SetGameplayEnabled(false);
                _reenableFrame = -1;
                _hudGroup.alpha = 0f;
                _hudGroup.blocksRaycasts = false;
                _hudGroup.interactable = false;
            }
            else
            {
                // Re-enable on the next frame so the press that closed the card never reaches the moveset.
                _reenableFrame = Time.frameCount + 1;
                _cooldown = InteractCooldown;
                _hudGroup.alpha = 1f;
                _hudGroup.blocksRaycasts = true;
                _hudGroup.interactable = true;
            }
        }

        private void CloseModals()
        {
            if (_talking != null)
            {
                _talking.Conversing = false;
                _talking = null;
            }

            Dialogue.Cancel();
            Captions.Raised = false;
            Notebook.Cancel();
            Notice.Hide();
            Cutaway.End(false);
            SetPipHidden(false);
            if (House != null)
            {
                House.NookWallHidden = false;
            }

            if (_modal)
            {
                SetModal(false);
            }
        }

        // ------------------------------------------------------------------------------------- interior safety

        /// <summary>
        /// The sky's dome is an opaque sphere that cuts through the south of the interior (TestFlight 23: a lens there
        /// saw a flat purple wall where the attic should be). Nothing of the sky can be seen from inside the closed
        /// shell, so it is not drawn while Pip or the lens is indoors.
        /// </summary>
        private void HideSkyIndoors(Vector3 pip, Vector3 lens)
        {
            if (_game.Sky != null)
            {
                _game.Sky.Hidden = InteriorSafety.SkyHidden(pip.x, pip.z, lens.x, lens.z);
            }
        }

        /// <summary>
        /// The interior safety net: off the walkable area, or pushed for several seconds without getting anywhere,
        /// Pip is set down on the nearest safe anchor with a small paper puff. Returns true when Pip was moved.
        /// </summary>
        private bool TickRescue(Vector3 pip, float dt, bool crossing)
        {
            if (House == null || dt <= 0f)
            {
                return false;
            }

            bool controllable = !_modal && !crossing && _game.Controls.GameplayEnabled &&
                                (Cutaway == null || !Cutaway.Active) && !FinalePlaying;
            _rescue.DoorOpen = House.DoorOpen;
            _rescue.ClearCharacters();
            var june = Cast != null ? Cast.June : null;
            if (june != null && june.isActiveAndEnabled)
            {
                Vector3 j = june.transform.position;
                _rescue.AddCharacter(j.x, j.y, j.z);
            }

            var reason = _rescue.Step(dt, pip.x, pip.y, pip.z, _pip.Grounded, _game.Controls.ReadMove().magnitude,
                controllable);
            if (reason == RescueReason.SlideOff)
            {
                // On June's head: slid gently off her (Pip drops to the floor beside June), never teleported.
                if (_pipController == null)
                {
                    _pipController = _pip.GetComponent<CharacterController>();
                }

                _pipController.Move(new Vector3(_rescue.SlideX, 0f, _rescue.SlideZ) * (InteriorRescue.SlideSpeed * dt));
                SlideOffFrames++;
                return false;
            }

            if (reason == RescueReason.None)
            {
                return false;
            }

            var anchor = InteriorSafety.Nearest(pip.x, pip.y, pip.z, reason == RescueReason.Stuck);
            _game.Feedback?.Puff(pip);
            _pip.Teleport(new Vector3(anchor.X, anchor.Y + 0.03f, anchor.Z));
            _pip.transform.rotation = Quaternion.Euler(0f, anchor.Yaw, 0f);
            CutCamera();
            Travel?.ResetTracker();
            _game.Feedback?.Puff(_pip.transform.position);
            Rescues++;
            LastRescue = reason;
            Debug.Log($"[CaseDirector] Safety net ({reason}): Pip at {pip:F2} set down at {anchor}");
            return true;
        }

        // ----------------------------------------------------------------------------------------------- frame

        private void Update()
        {
            bool playing = _flow.State == GameState.Playing;
            if (_reenableFrame >= 0 && Time.frameCount >= _reenableFrame)
            {
                _reenableFrame = -1;
                if (playing && !_modal)
                {
                    _game.Controls.SetGameplayEnabled(true);
                }
            }

            if (!playing)
            {
                Prompt.Track(_camera, null, null);
                return;
            }

            float dt = Time.deltaTime;
            if (_cooldown > 0f)
            {
                _cooldown -= dt;
            }

            Vector3 pip = _pip.transform.position;
            if (Travel != null)
            {
                Travel.Tick(dt, !_modal);
                pip = _pip.transform.position;
            }

            bool crossing = Travel != null && Travel.Active;
            TickStage(pip, dt);
            if (TickRescue(pip, dt, crossing))
            {
                pip = _pip.transform.position;
            }

            if (!_modal && !crossing)
            {
                if (CaseStage.ShouldHearKnocks(State, _pip.DistanceWalked - _walkedAtStart, pip.z))
                {
                    Perform(CaseAction.Of(CaseActionKind.HearKnocks));
                }

                if (House != null)
                {
                    bool attic = CaseStage.InAtticEntry(pip.x, pip.y, pip.z);
                    if (attic && !_inAtticEntry)
                    {
                        Perform(CaseAction.Of(CaseActionKind.EnterAttic));
                    }

                    _inAtticEntry = attic;
                    bool foyer = CaseStage.InFoyer(pip.x, pip.y, pip.z);
                    if (foyer && !_inFoyer)
                    {
                        Perform(CaseAction.Of(CaseActionKind.EnterFoyer));
                    }

                    _inFoyer = foyer;
                }

                bool onPorch = CaseStage.OnArrivalPorch(pip.x, pip.z);
                if (onPorch && !_onPorch)
                {
                    Perform(CaseAction.Of(CaseActionKind.ArriveAtPorch));
                }

                _onPorch = onPorch;
            }

            Targets.Suspended = _modal || _cooldown > 0f || crossing;
            Targets.Tick(pip, _camera.transform.forward);
            var target = Targets.HasTarget ? Targets.Current : null;
            Prompt.Track(_camera, target != null ? CaseUseTargets.MarkerOf(target) : (Vector3?)null,
                CaseUseTargets.IconFor(target));
        }
    }
}
