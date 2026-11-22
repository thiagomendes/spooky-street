using System;
using SpookyStreet.App;
using SpookyStreet.Characters;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Save;
using SpookyStreet.UI;
using SpookyStreet.UI.Nonverbal;
using SpookyStreet.World;
using UnityEngine;
using UnityEngine.UI;

namespace SpookyStreet.Case
{
    /// <summary>
    /// The animated non-verbal UX in the running case (docs/nonverbal-ux.md §12, D23), kept beside
    /// <see cref="CaseDirector"/> so the director only creates it and forwards hint requests and resets:
    /// settings (<see cref="NonverbalSettingsStore"/>) and the seen-replays set (<see cref="NonverbalProgressStore"/>)
    /// beside the case save; the bubble's words setting, gesture cues to the speaking witness and the speaker tail;
    /// the memory replay trigger (<see cref="MemoryReplayPolicy.OnLineDismissed"/>, seen marked before playing,
    /// played once the conversation closes); the notebook's strips and replay eyes; the radio's tier-2 bat trail and
    /// the opt-in tier-3 helper; the first-launch reading choice and the parent-gated settings toggles.
    /// </summary>
    public sealed class NonverbalLayer : MonoBehaviour
    {
        private CaseDirector _director;
        private GameBootstrap _game;
        private GameFlow _flow;
        private NonverbalSettingsStore _settingsStore;
        private NonverbalProgressStore _progress;
        private MemoryReplayDefinition _pending;
        private CanvasGroup _hud;
        private bool _hudHidden;
        private float _stall;
        private CaseObjective? _autoShownFor;
        private CaseObjective _objective;
        private Image[] _radioPips;

        public NonverbalSettings Settings { get; private set; }

        public NonverbalProgressStore Progress => _progress;

        public MemoryReplayView MemoryReplay { get; private set; }

        public HintGuide Guide { get; private set; }

        /// <summary>The tier-3 "show me" demonstration over the HUD (ghost hand on touch, glyphs otherwise).</summary>
        public HintGhostDemo Demo { get; private set; }

        public NonverbalMenus Menus { get; private set; }

        /// <summary>The HUD radio disc (hint button: three candy pips show the tier).</summary>
        public Button RadioButton { get; private set; }

        /// <summary>The gear on the pause card that opens the gated settings.</summary>
        public Button SettingsButton { get; private set; }

        public static bool ReducedMotion => AmbientMotion.ReducedMotion;

        private const float CaptionDefaultRaise = 300f;

        public static NonverbalLayer Create(CaseDirector director, GameBootstrap game)
        {
            var layer = director.gameObject.AddComponent<NonverbalLayer>();
            layer.Initialize(director, game);
            return layer;
        }

        private void Initialize(CaseDirector director, GameBootstrap game)
        {
            _director = director;
            _game = game;
            _flow = game.Flow;
            var fs = new DiskSaveFileSystem();
            _settingsStore = new NonverbalSettingsStore(fs, director.SaveDirectory);
            _progress = new NonverbalProgressStore(fs, director.SaveDirectory);
            Settings = Try(() => _settingsStore.Load(), new NonverbalSettings());
            Try(() => _progress.Load(director.State), (System.Collections.Generic.IReadOnlyCollection<string>)null);

            var root = (RectTransform)director.UiCanvas.transform;
            MemoryReplay = MemoryReplayView.Create(director.transform, game.Controls, game.CameraRig.OutputCamera, root);
            MemoryReplay.Ended += RestoreHud;
            Guide = HintGuide.Create(director.transform, game.Pip.transform);
            Guide.DoorOpen = () => director.House != null && director.House.DoorOpen;
            Menus = NonverbalMenus.Create(director.transform);
            Menus.Settings = Settings;
            Menus.SettingsChanged = SaveSettings;
            _hud = game.Ui.Hud.GetComponent<CanvasGroup>();

            var dialogue = director.Dialogue;
            dialogue.ViewCamera = game.CameraRig.OutputCamera;
            dialogue.SpeakerAnchor = SpeakerHead;
            dialogue.LineDismissed += OnLineDismissed;
            dialogue.GestureCue += OnGesture;
            dialogue.Finished += OnDialogueFinished; // after the director's own handler: the modal is closed by now

            var notebook = director.Notebook;
            notebook.SeenReplays = () => _progress.Seen;
            notebook.WatchReplay = WatchOnDemand;
            notebook.ShowWords = () => Settings.ShowSpeechText;
            notebook.CanAnimate = () => _flow.State == GameState.Playing;

            BuildRadio();
            Demo = HintGhostDemo.Create(_game.Ui.Hud.transform);
            Guide.Lookers = Lookers;
            BuildSettingsGear();
            ApplySettings();
            director.Stepped += OnStepped;
            _flow.StateChanged += OnStateChanged;
            _objective = CaseHints.ObjectiveFor(director.State);
            if (_flow.State == GameState.Title)
            {
                OfferReadingChoice();
            }
        }

        private static T Try<T>(Func<T> read, T fallback)
        {
            try
            {
                return read();
            }
            catch (Exception e)
            {
                Debug.LogWarning("[NonverbalLayer] " + e.Message);
                return fallback;
            }
        }

        private void OnDestroy()
        {
            if (_flow != null)
            {
                _flow.StateChanged -= OnStateChanged;
            }
        }

        // ------------------------------------------------------------------------------------- settings

        private void ApplySettings()
        {
            _director.Dialogue.ShowSpeechText = Settings.ShowSpeechText;
            _director.Dialogue.ReducedMotion = ReducedMotion;
            _director.Notebook.ReducedMotion = ReducedMotion;
        }

        private void SaveSettings(NonverbalSettings settings)
        {
            Settings = settings;
            ApplySettings();
            Try(() =>
            {
                _settingsStore.Save(settings);
                return true;
            }, false);
        }

        /// <summary>First launch (D23.1): two picture cards over the title; one tap chooses, Settings can change it.</summary>
        private void OfferReadingChoice()
        {
            if (Settings.NeedsFirstLaunchChoice)
            {
                Menus.ShowReadingChoice(ChooseReadingMode);
            }
        }

        /// <summary>The reading choice (true = Pictures only); saved at once.</summary>
        public void ChooseReadingMode(bool picturesOnly)
        {
            Settings.ChooseReadingMode(picturesOnly);
            Menus.HideReadingChoice();
            SaveSettings(Settings);
        }

        private void BuildSettingsGear()
        {
            var card = _game.Ui.PauseScreen.transform.Find("Content/PauseCard");
            if (card == null)
            {
                return;
            }

            var gear = UiFactory.FeltDisc(card, "SettingsButton", UiSpriteCatalog.FeltDiscNight, Palette.WarmPaper,
                UiSpriteCatalog.IconGear, Palette.WarmPaper, null, UiFactory.SystemDiscDiameter, Vector2.one,
                new Vector2(-30f, -30f), 1f);
            SettingsButton = gear.gameObject.AddComponent<Button>();
            SettingsButton.targetGraphic = gear;
            SettingsButton.transition = Selectable.Transition.None;
            SettingsButton.onClick.AddListener(OpenSettings);
        }

        /// <summary>The gear: a fresh parent gate, then the settings card with the two toggles.</summary>
        public void OpenSettings()
        {
            Menus.Settings = Settings;
            Menus.RequireParent(Menus.OpenSettings);
        }

        // ------------------------------------------------------------------------------------- dialogue

        private Vector3? SpeakerHead()
        {
            var witness = _director.Talking;
            if (witness == null)
            {
                return null;
            }

            var head = witness.Avatar != null ? witness.Avatar.Find(Witness.HeadBone) : null;
            return head != null ? head.position + Vector3.up * 0.15f : witness.transform.position + Vector3.up * 1.4f;
        }

        private void OnGesture(ScriptLine line, string gesture)
        {
            var witness = _director.Talking;
            if (witness == null || string.IsNullOrEmpty(gesture))
            {
                return;
            }

            // Bea points at the side window June went in by; a missing clip does nothing (no error).
            Vector3? aim = line.Speaker == Speaker.Bea ? MemoryReplay.StagingFor(ReplayPlace.TreehouseWindow).Mark : (Vector3?)null;
            witness.PlayGesture(gesture, aim);
        }

        private void OnLineDismissed(ScriptLine line)
        {
            var decision = MemoryReplayPolicy.OnLineDismissed(line.Key, _progress.Seen, !_game.Pip.Grounded, false);
            if (decision.MarkSeen)
            {
                // Before playing: a skip, crash or quit must not replay it (§12 step 4).
                Try(() =>
                {
                    _progress.MarkSeen(decision.Replay.Id, _director.State);
                    return true;
                }, false);
            }

            if (decision.Play && _pending == null)
            {
                _pending = decision.Replay;
            }
        }

        private void OnDialogueFinished()
        {
            var replay = _pending;
            _pending = null;
            if (replay != null && _flow.State == GameState.Playing && !_director.ModalOpen && _game.Pip.Grounded)
            {
                PlayReplay(replay);
            }
        }

        private void WatchOnDemand(MemoryReplayDefinition replay)
        {
            _director.Notebook.Close();
            PlayReplay(replay);
        }

        private void PlayReplay(MemoryReplayDefinition replay)
        {
            MemoryReplay.Play(replay, ReducedMotion);
            if (_hud != null)
            {
                _hud.alpha = 0f;
                _hud.blocksRaycasts = false;
                _hudHidden = true;
            }
        }

        private void RestoreHud()
        {
            if (!_hudHidden || _hud == null)
            {
                return;
            }

            _hudHidden = false;
            bool modal = _director.ModalOpen;
            _hud.alpha = modal ? 0f : 1f;
            _hud.blocksRaycasts = !modal;
        }

        // ------------------------------------------------------------------------------------- hints

        private void BuildRadio()
        {
            var pause = (RectTransform)_game.Ui.PauseButton.transform;
            bool mirrored = pause.anchorMin.x < 0.5f;
            var disc = UiFactory.FeltDisc(_game.Ui.Hud.transform, "RadioButton", UiSpriteCatalog.FeltDiscOrange,
                Palette.CandyCornYellow, UiSpriteCatalog.IconRadio, Palette.InkBlack, null, UiFactory.SystemDiscDiameter,
                pause.anchorMin, pause.anchoredPosition + new Vector2(mirrored ? 236f : -236f, 0f), UiFactory.HudFeltAlpha);
            RadioButton = disc.gameObject.AddComponent<Button>();
            RadioButton.targetGraphic = disc;
            RadioButton.transition = Selectable.Transition.None;
            RadioButton.onClick.AddListener(PressRadio);
            _radioPips = new Image[3];
            for (int i = 0; i < 3; i++)
            {
                _radioPips[i] = UiFactory.Sprite(disc.transform, "TierPip" + i, IconFactory.Disc, Palette.CandyCornYellow,
                    new Vector2(0.5f, 0f), new Vector2((i - 1) * 18f, -10f), new Vector2(12f, 12f));
            }

            RefreshPips();
        }

        /// <summary>The radio on the HUD: the next hint tier in the caption strip (tier 2 also flies the bat trail).</summary>
        public void PressRadio()
        {
            string key = _director.RequestHint();
            if (key != null)
            {
                _director.Captions.ShowNow(key);
            }
        }

        /// <summary>The director tells the layer a hint of <paramref name="tier"/> was just given (HUD radio or notebook).</summary>
        public void OnHintGiven(HintTier tier, CaseState state)
        {
            ShowHint(HintPresentation.For(HintTrigger.RadioPress, tier, Settings), CaseHints.ObjectiveFor(state));
            _stall = 0f;
            RefreshPips();
        }

        /// <summary>Plays a hint's pictures (docs/nonverbal-ux.md §6); every other hint picture stops.</summary>
        private void ShowHint(HintVisuals visuals, CaseObjective objective)
        {
            if (visuals.BatTrail)
            {
                Guide.StartTrail(objective);
            }
            else
            {
                Guide.Stop();
            }

            if (visuals.Sparkle)
            {
                Guide.StartSparkle(objective);
            }
            else
            {
                Guide.StopSparkle();
            }

            if (visuals.GhostDemo)
            {
                PlayDemo(objective);
            }
            else
            {
                Demo.Stop();
            }
        }

        /// <summary>Seconds since the last fact or hint (the Helper's stall clock).</summary>
        public float StallSeconds => _stall;

        /// <summary>Tests: advance the stall clock without waiting three minutes.</summary>
        public void AddStallForTests(float seconds) => _stall += seconds;

        private void StopHintPictures()
        {
            Guide.Stop();
            Guide.StopSparkle();
            Demo.Stop();
        }

        /// <summary>The tier-3 demonstration for the objective, from where Pip is.</summary>
        private void PlayDemo(CaseObjective objective)
        {
            Vector3 pip = _game.Pip.transform.position;
            bool door = _director.House != null && _director.House.DoorOpen;
            if (!HintPresentation.TryTarget(objective, pip.x, pip.z, door, out var target, out _))
            {
                Demo.Stop();
                return;
            }

            string pointId = HintPresentation.PointIdFor(objective);
            var current = _director.Targets != null && _director.Targets.HasTarget ? _director.Targets.Current : null;
            bool inRange = pointId != null && current != null && current.Id == pointId;
            var demo = HintPresentation.DemoFor(pip.x, pip.y, pip.z, target, inRange);
            Demo.Play(demo, ScreenDirection(new Vector3(target.X, target.Y, target.Z) - pip));
        }

        /// <summary>A world offset as a direction on screen (x right, y forward/up), from the gameplay camera.</summary>
        private Vector2 ScreenDirection(Vector3 offset)
        {
            var cam = _game.CameraRig.OutputCamera;
            if (cam == null)
            {
                return Vector2.up;
            }

            Vector3 right = cam.transform.right;
            Vector3 forward = Vector3.ProjectOnPlane(cam.transform.forward, Vector3.up).normalized;
            return new Vector2(Vector3.Dot(offset, right), Vector3.Dot(offset, forward));
        }

        private readonly System.Collections.Generic.List<Witness> _lookers = new System.Collections.Generic.List<Witness>();

        /// <summary>The witnesses and the cast who may turn to look at a tier-1 target.</summary>
        private System.Collections.Generic.IReadOnlyList<Witness> Lookers()
        {
            _lookers.Clear();
            var witnesses = _game.Witnesses;
            if (witnesses != null)
            {
                Add(witnesses.Bea);
                Add(witnesses.Vale);
            }

            var cast = _director.Cast;
            if (cast != null)
            {
                Add(cast.June);
                Add(cast.Kit);
            }

            return _lookers;

            void Add(Witness w)
            {
                if (w != null)
                {
                    _lookers.Add(w);
                }
            }
        }

        private void RefreshPips()
        {
            if (_radioPips == null)
            {
                return;
            }

            int next = (int)_director.NextHintTier;
            for (int i = 0; i < _radioPips.Length; i++)
            {
                _radioPips[i].color = i < next ? Palette.CandyCornYellow : UiFactory.WithAlpha(Palette.WarmPaper, 0.35f);
            }
        }

        private void OnStepped(CaseStep step)
        {
            Try(() =>
            {
                _progress.Observe(step.State);
                return true;
            }, false);
            if (step.Gained.Count > 0)
            {
                _stall = 0f;
            }

            var objective = CaseHints.ObjectiveFor(step.State);
            if (objective != _objective)
            {
                // The objective is met: the trail stops and the radio starts again from the first tier.
                _objective = objective;
                StopHintPictures();
                _autoShownFor = null;
            }

            RefreshPips();
        }

        /// <summary>Fresh start or a confirmed case replay: the seen set and the hints start over.</summary>
        public void OnCaseReset()
        {
            Try(() =>
            {
                _progress.Reset();
                return true;
            }, false);
            _pending = null;
            MemoryReplay.End(false);
            RestoreHud();
            StopHintPictures();
            _director.Notebook.ResetStickers();
            _objective = CaseHints.ObjectiveFor(_director.State);
            _autoShownFor = null;
            _stall = 0f;
            RefreshPips();
        }

        private void OnStateChanged(GameState previous, GameState current)
        {
            if (current == GameState.Title)
            {
                _pending = null;
                MemoryReplay.End(false);
                RestoreHud();
                StopHintPictures();
                Menus.CloseAll();
                OfferReadingChoice();
            }
            else
            {
                Menus.HideReadingChoice();
            }

            if (current == GameState.Playing)
            {
                Menus.CloseAll();
                ApplySettings();
            }
        }

        private void Update()
        {
            // Raised captions sit just above the speech bubble (it is taller than the v1 card).
            var dialogue = _director.Dialogue;
            var captions = _director.Captions;
            float raised = dialogue.IsOpen && dialogue.ShowsVignette ? dialogue.BubbleTop + 44f : CaptionDefaultRaise;
            if (!Mathf.Approximately(captions.RaisedHeight, raised))
            {
                captions.RaisedHeight = raised;
                captions.Raised = captions.Raised; // re-apply
            }

            if (_flow.State != GameState.Playing || _director.ModalOpen || MemoryReplay.Active)
            {
                return;
            }

            // Helper (opt-in, off by default): "look here" and "show me" may show by themselves after a long stall,
            // once per objective. With the Helper off nothing shows without a radio press.
            _stall += Time.deltaTime;
            var objective = CaseHints.ObjectiveFor(_director.State);
            var visuals = HintPresentation.For(HintTrigger.HelperStall, HintTier.Action, Settings, _stall);
            if (visuals.Any && _autoShownFor != objective)
            {
                _autoShownFor = objective;
                _stall = 0f;
                _director.Captions.ShowNow(CaseHints.HintFor(objective, HintTier.Action, _director.State));
                ShowHint(visuals, objective);
            }
        }
    }
}
