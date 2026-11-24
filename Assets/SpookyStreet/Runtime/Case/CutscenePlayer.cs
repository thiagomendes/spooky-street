using System;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.Cutscenes;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Core.World;
using SpookyStreet.Localization;
using SpookyStreet.UI;
using SpookyStreet.UI.Nonverbal;
using SpookyStreet.World;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.Cutscenes;

namespace SpookyStreet.Case
{
    /// <summary>What a cutscene asks of the world: the people, the world effects, the anchors and the hand-back to play.</summary>
    public interface ICutsceneStage
    {
        /// <summary>Before the first frame (hold play, hide the HUD, remember what a rewatch must restore).</summary>
        void Begin(Cutscene cutscene, bool rewatch);

        void Cast(CastCue cue);

        void World(string id);

        /// <summary>Every frame while it plays (walks, the interior shown for an indoor lens).</summary>
        void Tick(float dt, CamPose camera, CutscenePlayer player);

        /// <summary>
        /// The outro starts (played through or skipped): final placements; the play camera snaps behind Pip. Returns
        /// where play resumes (Pip), so the camera never blends between the street and the interior.
        /// </summary>
        Vector3 Settle(Cutscene cutscene, bool rewatch);

        /// <summary>It has ended: restore the world from the facts and give control back.</summary>
        void Finish(Cutscene cutscene, bool rewatch, bool skipped);

        /// <summary>The real model's frame for a shot anchor, or null for the data's default.</summary>
        AnchorFrame? Anchor(string id);
    }

    /// <summary>
    /// Plays a <see cref="Cutscene"/> in the world (S002.5b, D28): one Cinemachine camera driven by the engine-free
    /// <see cref="CutscenePlayback"/>, entered and left with the memory replay's camera blend (<see cref="CinematicBlends"/>;
    /// cuts under reduced motion), framed by the replay's letterbox and skip disc (<see cref="CinematicFrame"/>), with
    /// the hybrid text line in the bottom bar (D23.1), the child's own sticker popping in for the reveal's sticker
    /// shots, an optional comic vignette insert (<see cref="VignettePlayer"/>) and the thresholds' paper page-turn
    /// (<see cref="ThresholdWipeView"/>) over cuts between the street and the interior. Skip works on every control
    /// scheme (D25): the disc at once; a tap, Space/Enter/E or gamepad South/East/West asks once and skips on the second
    /// press (<see cref="CutsceneSkipGate"/>). Game time, so pause freezes it. It never records anything about the case.
    /// </summary>
    public sealed class CutscenePlayer : MonoBehaviour
    {
        public const string CameraName = "CutsceneCamera";
        public const float BarHeight = 84f;

        /// <summary>From play into a cutscene (a rewatch, the reveal): the replay's ease, a little longer.</summary>
        public const float EntryBlend = 0.6f;

        public const int LineFontSize = 32;

        private CinemachineBrain _brain;
        private CinemachineBlenderSettings _blends;
        private CinemachineCamera _vcam;
        private CinematicFrame _frame;
        private Text _line;
        private LocalizedText _lineText;
        private RectTransform _stickerHolder;
        private Image _sticker;
        private float _stickerTime;
        private RectTransform _insert;
        private VignettePlayer _vignette;
        private ThresholdWipeView _wipeView;
        private readonly ThresholdWipe _wipe = new ThresholdWipe();
        private readonly CutsceneSkipGate _gate = new CutsceneSkipGate();
        private InputActionMap _map;
        private InputAction _skip;
        private float _clock;

        public ICutsceneStage Stage { get; set; }

        public CutscenePlayback Playback { get; private set; }

        public Cutscene Current => Playback?.Cutscene;

        public bool Active => Playback != null;

        /// <summary>True while the current cutscene is a rewatch from the notebook.</summary>
        public bool Rewatching { get; private set; }

        public bool ReducedMotion => Playback != null && Playback.ReducedMotion;

        public CinemachineCamera VirtualCamera => _vcam;

        public CinematicFrame Frame => _frame;

        /// <summary>The text line in the bottom bar.</summary>
        public Text Line => _line;

        /// <summary>The child's sticker shown on a reveal sticker shot (null otherwise).</summary>
        public Image Sticker => _sticker != null && _sticker.gameObject.activeSelf ? _sticker : null;

        public VignettePlayer Insert => _vignette;

        public ThresholdWipeView PageView => _wipeView;

        public bool PageTurning => _wipe.Active;

        public CutsceneSkipGate Gate => _gate;

        /// <summary>Captures and tests: freezes the cutscene where it is (its clock, camera and cues).</summary>
        public bool Hold { get; set; }

        /// <summary>Cutscenes started since the scene loaded (tests).</summary>
        public int PlayCount { get; private set; }

        public string LastId { get; private set; }

        public bool LastSkipped { get; private set; }

        /// <summary>A cutscene started.</summary>
        public event Action<Cutscene> Started;

        /// <summary>A cutscene ended (played through, or skipped when the flag is set), after the stage gave control back.</summary>
        public event Action<Cutscene, bool> Ended;

        /// <summary>A babble or music cue (placeholders until S003 audio).</summary>
        public event Action<SignalCue> CueFired;

        /// <summary>
        /// True when another cutscene follows this one at once (S004: the seed after the reveal). It then ends without
        /// the blend back to play, and its <see cref="Ended"/> handler starts the next one with <c>chained</c>.
        /// </summary>
        public Func<Cutscene, bool, bool> ChainsAfter { get; set; }

        public static CutscenePlayer Create(Transform parent, Camera output, RectTransform canvasRoot)
        {
            var go = new GameObject("CutscenePlayer");
            go.transform.SetParent(parent, false);
            var player = go.AddComponent<CutscenePlayer>();
            player.Build(output, canvasRoot);
            return player;
        }

        private void Build(Camera output, RectTransform canvasRoot)
        {
            _brain = output != null ? output.GetComponent<CinemachineBrain>() : null;
            var cameraObject = new GameObject(CameraName);
            cameraObject.transform.SetParent(transform, false);
            cameraObject.SetActive(false);
            _vcam = cameraObject.AddComponent<CinemachineCamera>();
            var lens = _vcam.Lens;
            lens.FieldOfView = 40f;
            lens.NearClipPlane = 0.05f;
            lens.FarClipPlane = 200f;
            _vcam.Lens = lens;

            // The replay's frame: letterbox, skip disc and ring; no pencil tint, a light vignette for depth.
            _frame = CinematicFrame.Create(canvasRoot, "CutsceneOverlay", BarHeight, Keys.Skip, Color.white, 0f, 0.35f);
            _frame.Tapped += () => Press(SkipPress.Any);
            _frame.SkipPressed += () => Press(SkipPress.Disc);

            // The hybrid text line, centred in the bottom bar.
            _line = UiFactory.Label(_frame.Overlay, "TextLine", null, LineFontSize, TextAnchor.MiddleCenter, Palette.WarmPaper,
                UiFactory.BoldFont);
            _line.resizeTextMinSize = 26;
            var rect = _line.rectTransform;
            rect.anchorMin = new Vector2(0.12f, 0f);
            rect.anchorMax = new Vector2(0.88f, 0f);
            rect.pivot = new Vector2(0.5f, 0f);
            rect.anchoredPosition = new Vector2(0f, 4f);
            rect.sizeDelta = new Vector2(0f, BarHeight - 8f);
            _lineText = _line.gameObject.AddComponent<LocalizedText>();
            _line.gameObject.SetActive(false);

            // The child's sticker pops in at the lower left, over the picture.
            _stickerHolder = UiFactory.Rect(_frame.SafeArea, "StickerHolder");
            UiFactory.Place(_stickerHolder, new Vector2(0f, 0f), new Vector2(128f, BarHeight + 92f), new Vector2(190f, 164f));

            // A comic vignette insert at the upper left (optional per beat).
            var panel = UiFactory.PaperCard(_frame.SafeArea, "VignetteInsert", new Vector2(0f, 1f), new Vector2(230f, -BarHeight - 92f),
                new Vector2(380f, 150f), -1.5f, Palette.HauntedPurple);
            panel.raycastTarget = false;
            _insert = panel.rectTransform;
            _vignette = VignettePlayer.Create(_insert, "Vignette");
            _vignette.CanRun = () => Time.deltaTime > 0f;
            _insert.gameObject.SetActive(false);

            _wipeView = ThresholdWipeView.Create(transform);

            _map = new InputActionMap("Cutscene");
            _skip = _map.AddAction("Skip", InputActionType.Button, "<Keyboard>/space");
            _skip.AddBinding("<Keyboard>/enter");
            _skip.AddBinding("<Keyboard>/e");
            _skip.AddBinding("<Gamepad>/buttonSouth");
            _skip.AddBinding("<Gamepad>/buttonEast");
            _skip.AddBinding("<Gamepad>/buttonWest");
        }

        /// <summary>
        /// Starts <paramref name="cutscene"/>. <paramref name="cutIn"/> cuts straight in (the opening, from the title);
        /// otherwise the camera blends in from play (a cut under reduced motion).
        /// </summary>
        public void Play(Cutscene cutscene, bool reducedMotion, bool rewatch, bool cutIn, bool chained = false)
        {
            if (cutscene == null)
            {
                return;
            }

            End(false);
            Playback = new CutscenePlayback(cutscene, reducedMotion, id => Stage?.Anchor(id)) { StartLetterboxed = chained };
            Rewatching = rewatch;
            PlayCount++;
            LastId = cutscene.Id;
            LastSkipped = false;
            _clock = 0f;
            _gate.Reset();
            _wipe.Cancel();
            Stage?.Begin(cutscene, rewatch);
            var events = Playback.Advance(0f);
            var first = Playback.Camera;
            // Never blend between the street and the interior (D24.1): the lens would cross the void.
            bool crossing = _brain != null && CaseRouteLayout.Inside(_brain.transform.position.x, _brain.transform.position.z) !=
                CaseRouteLayout.Inside(first.Eye.X, first.Eye.Z);
            CinematicBlends.Configure(_brain, ref _blends, "CutsceneBlends", reducedMotion || cutIn || crossing, EntryBlend,
                CameraName);
            ApplyCamera(first);
            _vcam.PreviousStateIsValid = false;
            _vcam.gameObject.SetActive(true);
            _frame.Show();
            _map.Enable();
            Started?.Invoke(cutscene);
            Process(events);
            Present(0f);
        }

        /// <summary>A skip press from a control (touch tap, key, button, or the disc).</summary>
        public SkipGateResult Press(SkipPress press)
        {
            // Paused: the press belongs to the pause menu.
            if (Playback == null || !Playback.Playing || Time.timeScale <= 0f)
            {
                return SkipGateResult.Ignored;
            }

            var result = _gate.Press(press, _clock);
            if (result == SkipGateResult.Skip)
            {
                SkipNow();
            }

            return result;
        }

        /// <summary>Skips at once (to the outro): what the gate does on its second press.</summary>
        public void SkipNow()
        {
            if (Playback == null)
            {
                return;
            }

            LastSkipped = true;
            Process(Playback.Skip());
            if (Playback != null)
            {
                Present(0f);
            }
        }

        /// <summary>A wait signal (Pip reached the porch).</summary>
        public void Signal(string id) => Playback?.Signal(id);

        /// <summary>Ends at once. <paramref name="notify"/> false is a silent cancel (back to title, a case reset).</summary>
        public void End(bool notify = true)
        {
            if (Playback == null)
            {
                return;
            }

            var cutscene = Playback.Cutscene;
            bool skipped = Playback.WasSkipped;
            bool settled = !Playback.Playing;
            bool rewatch = Rewatching;
            Playback = null;
            _map.Disable();
            _vcam.gameObject.SetActive(false);
            _frame.Hide();
            _line.gameObject.SetActive(false);
            HideSticker();
            _vignette.Stop();
            _insert.gameObject.SetActive(false);
            _wipe.Cancel();
            _wipeView.Hide();
            if (!settled)
            {
                Stage?.Settle(cutscene, rewatch);
                CinematicBlends.Configure(_brain, ref _blends, "CutsceneBlends", true, 0f, CameraName);
            }

            Stage?.Finish(cutscene, rewatch, skipped);
            Rewatching = false;
            if (notify)
            {
                Ended?.Invoke(cutscene, skipped);
            }
        }

        private void Update()
        {
            if (Playback == null || Hold)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                _clock += dt;
                _gate.Tick(_clock);
                if (_skip.WasPressedThisFrame())
                {
                    Press(SkipPress.Any);
                    if (Playback == null)
                    {
                        return;
                    }
                }
            }

            Process(Playback.Advance(dt));
            if (Playback == null)
            {
                return;
            }

            Present(dt);
        }

        private void Process(System.Collections.Generic.IReadOnlyList<CutsceneEvent> events)
        {
            // Copy first: a stage callback may end the cutscene and the playback reuses its list.
            var copy = new CutsceneEvent[events.Count];
            for (int i = 0; i < copy.Length; i++)
            {
                copy[i] = events[i];
            }

            foreach (var e in copy)
            {
                if (Playback == null)
                {
                    return;
                }

                switch (e.Kind)
                {
                    case CutsceneEventKind.BeatStarted:
                        OnBeat(e.Beat);
                        break;
                    case CutsceneEventKind.Cast:
                        Stage?.Cast(e.Cast);
                        break;
                    case CutsceneEventKind.Signal:
                        if (e.Signal.Channel == CueChannel.World)
                        {
                            Stage?.World(e.Signal.Id);
                        }
                        else
                        {
                            CueFired?.Invoke(e.Signal);
                        }

                        break;
                    case CutsceneEventKind.OutroStarted:
                        if (ChainsAfter != null && ChainsAfter(Playback.Cutscene, Rewatching))
                        {
                            // Another cutscene follows at once (the seed after the reveal): no blend back to play and
                            // the bars stay down; the next one starts from the Ended handler.
                            Stage?.Settle(Playback.Cutscene, Rewatching);
                            End();
                            return;
                        }

                        OnOutro();
                        break;
                    case CutsceneEventKind.Ended:
                        End();
                        return;
                }
            }
        }

        private void OnBeat(int index)
        {
            var beat = Playback.Cutscene.Beats[index];
            if (beat.StickerId != null)
            {
                ShowSticker(beat.StickerId, index);
            }
            else
            {
                HideSticker();
            }

            var vignette = beat.VignetteId != null ? C01Vignettes.Get(beat.VignetteId) : null;
            _insert.gameObject.SetActive(vignette != null);
            if (vignette != null)
            {
                _vignette.ReducedMotion = Playback.ReducedMotion;
                _vignette.Play(vignette);
            }
            else
            {
                _vignette.Stop();
            }
        }

        private void OnOutro()
        {
            var cutscene = Playback.Cutscene;
            HideSticker();
            _vignette.Stop();
            _insert.gameObject.SetActive(false);
            _wipe.Cancel();
            _wipeView.Hide();
            // Final placements first, so the play camera is already behind Pip when the blend back starts.
            var last = Playback.Camera;
            Vector3 play = Stage != null ? Stage.Settle(cutscene, Rewatching) : new Vector3(last.Eye.X, last.Eye.Y, last.Eye.Z);
            bool crossing = CaseRouteLayout.Inside(last.Eye.X, last.Eye.Z) != CaseRouteLayout.Inside(play.x, play.z);
            CinematicBlends.Configure(_brain, ref _blends, "CutsceneBlends", Playback.OutroLength <= 0f || crossing,
                Playback.OutroLength, CameraName);
            _vcam.gameObject.SetActive(false);
        }

        /// <summary>Camera, page-turn, stage tick and the frame for this frame.</summary>
        private void Present(float dt)
        {
            if (Playback.Playing)
            {
                var pose = Playback.Camera;
                ApplyCamera(pose);
                if (Playback.CutThisStep)
                {
                    _vcam.PreviousStateIsValid = false;
                }

                float lead = Playback.ReducedMotion ? ThresholdWipe.FadeSeconds : CutscenePlayback.PageCoverLead;
                if (!_wipe.Active && Playback.SecondsToPageTurn <= lead + 1e-3f)
                {
                    _wipe.Start(Playback.ReducedMotion);
                    _wipeView.Show(ThresholdId.FrontDoor);
                    var glyph = PuppetAtlas.Get("pg.house");
                    if (glyph != null && _wipeView.Glyph != null)
                    {
                        _wipeView.Glyph.sprite = glyph;
                    }
                }

                Stage?.Tick(dt, pose, this);
            }

            if (_wipe.Active)
            {
                _wipe.Step(dt);
                _wipeView.Apply(_wipe);
            }

            float w = Playback.Letterbox;
            _frame.SetLook(w);
            _frame.Progress = Playback.Progress;
            _frame.SkipEmphasis = _gate.Armed ? 1.25f : 1f;
            _frame.SetSkipCaption(_gate.Armed ? Keys.SkipAgain : Keys.Skip);
            string key = Playback.CaptionKey;
            bool showLine = key != null && w > 0.05f;
            if (_line.gameObject.activeSelf != showLine)
            {
                _line.gameObject.SetActive(showLine);
            }

            if (showLine && _lineText.Key != key)
            {
                _lineText.SetKey(key);
            }

            _line.color = UiFactory.WithAlpha(Palette.WarmPaper, Mathf.Clamp01(w));
            AnimateSticker(dt);
        }

        private void ApplyCamera(CamPose pose)
        {
            var eye = new Vector3(pose.Eye.X, pose.Eye.Y, pose.Eye.Z);
            var aim = new Vector3(pose.Aim.X, pose.Aim.Y, pose.Aim.Z);
            var forward = aim - eye;
            _vcam.transform.SetPositionAndRotation(eye,
                forward.sqrMagnitude > 1e-6f ? Quaternion.LookRotation(forward, Vector3.up) : _vcam.transform.rotation);
            var lens = _vcam.Lens;
            lens.FieldOfView = pose.Fov;
            _vcam.Lens = lens;
        }

        private void ShowSticker(string stickerId, int beat)
        {
            HideSticker();
            float tilt = beat % 2 == 0 ? -6f : 5f;
            _sticker = NotebookView.StickerCard(_stickerHolder, stickerId, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(176f, 150f), tilt);
            _sticker.raycastTarget = false;
            UiFactory.InkShadow(_sticker, new Vector2(4f, -6f), 0.5f);
            _stickerTime = 0f;
            AnimateSticker(0f);
        }

        private void HideSticker()
        {
            if (_sticker != null)
            {
                Destroy(_sticker.gameObject);
                _sticker = null;
            }
        }

        private void AnimateSticker(float dt)
        {
            if (_sticker == null)
            {
                return;
            }

            _stickerTime += dt;
            float pop = Playback != null && Playback.ReducedMotion ? 1f : VignetteSampler.Apply(Ease.Back, Mathf.Clamp01(_stickerTime / 0.35f));
            _sticker.rectTransform.localScale = Vector3.one * Mathf.Lerp(0.55f, 1f, pop);
        }

        private void OnDestroy()
        {
            _map?.Dispose();
        }
    }
}
