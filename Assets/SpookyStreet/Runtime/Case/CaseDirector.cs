using System;
using System.Collections.Generic;
using System.IO;
using SpookyStreet.App;
using SpookyStreet.Characters;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Save;
using SpookyStreet.Player;
using SpookyStreet.Save;
using SpookyStreet.UI;
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
            Targets = new CaseUseTargets(CaseStage.ExteriorPoints, point => Perform(point.Action), sightMask);
            _pip.UseTargets = Targets;
            Bench = BenchView.Create(transform, _pip, game.Controls, _camera);
            var anchors = game.Environment != null ? game.Environment.Anchors : default;
            Knocks = KnockBeat.Create(transform, game.Materials, anchors.AtticLight,
                game.Environment != null && game.Environment.UsesModels);

            // Title: Continue resumes the save; New game asks first when a case is in progress.
            _ui.ContinueButton.onClick.AddListener(OnContinue);
            _ui.NewGameButton.onClick.RemoveAllListeners();
            _ui.NewGameButton.onClick.AddListener(OnNewGame);

            LoadSave();
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
            Notebook.RequestHint = () => _hints.Request(State);
            Notebook.ReplayRequested = ConfirmReplay;
            Notebook.Closed += () => SetModal(false);
            var safe = UiFactory.Stretch(UiFactory.Rect(root, "CaptionSafeArea"));
            safe.gameObject.AddComponent<SafeAreaFitter>();
            Captions = CaptionStrip.Create(safe);
            Captions.ViewCamera = _camera;
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
            string anchor = CaseStage.NearestAnchor(safe.x, safe.z);
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
            Knocks.SetHeard(view.PaperBatFallen);
            // Door, June, workshop panel, owl and booth are interior/finale props: wired with the interior pass.
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
        }

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

                _walkedAtStart = _pip.DistanceWalked;
                _onPorch = CaseStage.OnArrivalPorch(_pip.transform.position.x, _pip.transform.position.z);
                _cooldown = InteractCooldown;
            }

            if (current == GameState.Title)
            {
                ResetPresentation();
                _ui.SetHasSave(HasSave && State.Count > 0);
            }

            if (current == GameState.Playing && _modal)
            {
                // GameBootstrap re-enabled gameplay input on resume; a conversation or the notebook still owns it.
                _game.Controls.SetGameplayEnabled(false);
            }
        }

        private void ResetPresentation()
        {
            CloseModals();
            Captions.Clear();
            Bench.End();
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
            LastStep = step;
            if (step.ShouldAutosave)
            {
                Autosave();
            }

            Stage(action, step);
            PlayLines(action, step.Lines);
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
            }
        }

        private void PlayLines(CaseAction action, IReadOnlyList<ScriptLine> lines)
        {
            var spoken = new List<ScriptLine>(lines.Count);
            foreach (var line in lines)
            {
                if (CaseSpeakers.IsCaption(line.Speaker))
                {
                    Captions.ShowKey(line.Key, SourceOf(action));
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
            Dialogue.Play(spoken);
        }

        /// <summary>World position a caption's direction cue points at (the attic window for the knocks).</summary>
        private Func<Vector3?> SourceOf(CaseAction action)
        {
            if (action.Kind == CaseActionKind.HearKnocks && _game.Environment != null)
            {
                Vector3 attic = _game.Environment.Anchors.AtticWindow;
                return () => attic;
            }

            return null;
        }

        private Witness WitnessFor(Speaker speaker)
        {
            var cast = _game.Witnesses;
            if (cast == null)
            {
                return null;
            }

            switch (speaker)
            {
                case Speaker.Bea:
                    return cast.Bea;
                case Speaker.Vale:
                    return cast.Vale;
                default:
                    return null;
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
            if (_talking != null)
            {
                _talking.Conversing = false;
                _talking = null;
            }

            SetModal(false);
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
            Notebook.Cancel();
            Notice.Hide();
            if (_modal)
            {
                SetModal(false);
            }
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
            if (!_modal)
            {
                if (CaseStage.ShouldHearKnocks(State, _pip.DistanceWalked - _walkedAtStart, pip.z))
                {
                    Perform(CaseAction.Of(CaseActionKind.HearKnocks));
                }

                bool onPorch = CaseStage.OnArrivalPorch(pip.x, pip.z);
                if (onPorch && !_onPorch)
                {
                    Perform(CaseAction.Of(CaseActionKind.ArriveAtPorch));
                }

                _onPorch = onPorch;
            }

            Targets.Suspended = _modal || _cooldown > 0f;
            Targets.Tick(pip, _camera.transform.forward);
            var target = Targets.HasTarget ? Targets.Current : null;
            Prompt.Track(_camera, target != null ? CaseUseTargets.MarkerOf(target) : (Vector3?)null,
                CaseUseTargets.IconFor(target));
        }
    }
}
