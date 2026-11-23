using System;
using SpookyStreet.Controls;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Core.World;
using SpookyStreet.UI;
using SpookyStreet.UI.Nonverbal;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.UI;
using NvKeys = SpookyStreet.Core.Localization.LocalizationKeys.NonverbalUi;

namespace SpookyStreet.Case
{
    /// <summary>
    /// Where a memory replay happens: the camera pose, an optional ghost route (empty: no ghost), an optional blur
    /// streak (what a witness glimpsed: a silver smear high up, S002.5b) and the mark the witness points at. Data, so
    /// the places can move (D24: the interior becomes its own space) without touching the view. D26: no key ring and
    /// no ⊘ stamp are ever drawn in a memory.
    /// </summary>
    public readonly struct ReplayStaging
    {
        public ReplayStaging(Vector3 eye, Vector3 lookAt, Vector3[] route, Vector3 mark, Vector3[] streak = null)
        {
            Eye = eye;
            LookAt = lookAt;
            Route = route ?? System.Array.Empty<Vector3>();
            Mark = mark;
            Streak = streak ?? System.Array.Empty<Vector3>();
        }

        public Vector3 Eye { get; }
        public Vector3 LookAt { get; }
        public Vector3[] Route { get; }
        public Vector3 Mark { get; }

        /// <summary>The blur streak's path (empty: none).</summary>
        public Vector3[] Streak { get; }

        public bool HasGhost => Route.Length >= 2;

        public bool HasStreak => Streak.Length >= 2;
    }

    /// <summary>
    /// The in-world memory replay (docs/nonverbal-ux.md §4, D23.2, D23.7, restaged for D26 §7 by S002.5b): a fixed
    /// Cinemachine camera per <see cref="ReplayPlace"/> takes over like <see cref="BenchView"/> (0.35 s blend in,
    /// 1.8 s hold, 0.35 s back, all from <see cref="MemoryReplayPlayback"/>), ink letterbox bars slide in and a lilac
    /// pencil tint with a soft vignette covers the world, both driven by <see cref="MemoryReplayPlayback.Weight"/>.
    /// Memories are hazy and partial, and only on demand from the witness's sticker in the notebook:
    /// <list type="bullet">
    /// <item>Bea's: from her spot on the plaza, through a milky haze, the camera drifts toward the side of the house;
    /// a silver streak smears across high above the hedge and is gone by the roof. No June, no window entry, no ring.</item>
    /// <item>Vale's: the camera holds on the shut front door from his bench. No ⊘ in the world.</item>
    /// </list>
    /// Any tap, Use, Jump or stick push skips (blends back from where it is). It never touches time scale or
    /// gameplay input and never records anything: the line already recorded its clue. Game time, so pause freezes it.
    /// </summary>
    public sealed class MemoryReplayView : MonoBehaviour
    {
        public const float FieldOfView = 45f;

        /// <summary>Input during this long after the start is the press that dismissed the line, not a skip.</summary>
        public const float SkipGraceSeconds = 0.2f;

        public const float BarHeight = 64f;
        public const float GhostHeight = 1.15f;

        public const string TreehouseCameraName = "MemoryReplayTreehouse";
        public const string FrontDoorCameraName = "MemoryReplayFrontDoor";

        /// <summary>Cinemachine's wildcard camera name in custom blends.</summary>
        public const string AnyCameraLabel = "**ANY CAMERA**";

        private static readonly Color Lilac = new Color(0.62f, 0.52f, 0.86f, 1f);
        private static readonly Color GhostTint = new Color(1f, 1f, 1f, 0.9f);

        private GameInput _input;
        private CinemachineBrain _brain;
        private CinemachineBlenderSettings _blends;
        private CinemachineCamera _treehouseCamera;
        private CinemachineCamera _doorCamera;
        private readonly System.Collections.Generic.Dictionary<ReplayPlace, ReplayStaging> _staging =
            new System.Collections.Generic.Dictionary<ReplayPlace, ReplayStaging>();
        private ReplayStaging _current;
        private CinemachineCamera _active;
        private RectTransform _overlay;
        private Image _tint;
        private Image _vignette;
        private RectTransform _topBar;
        private RectTransform _bottomBar;
        private Image _skipRing;
        private SpriteRenderer _ghost;
        private readonly SpriteRenderer[] _streak = new SpriteRenderer[StreakEchoes];
        private Image _haze;
        private Vector3[] _route;
        private Vector3[] _streakPath;
        private bool _reduced;
        private float _started;
        private float _ghostClock;

        public MemoryReplayPlayback Playback { get; private set; }

        public bool Active => Playback != null && !Playback.IsDone;

        /// <summary>The replay that played last (tests).</summary>
        public MemoryReplayDefinition Last { get; private set; }

        /// <summary>Replays started since the scene loaded (tests).</summary>
        public int PlayCount { get; private set; }

        public float Weight => Active ? Playback.Weight : 0f;

        public Button SkipButton { get; private set; }

        /// <summary>The skip disc's small caption ("Skip memory", <c>ui.memory.skip</c>) in the letterbox.</summary>
        public Text SkipCaption { get; private set; }

        public SpriteRenderer Ghost => _ghost;

        /// <summary>The silver blur streak of a hazy memory (Bea's), and its fading echoes.</summary>
        public SpriteRenderer Streak => _streak[0];

        /// <summary>The streak is drawn this frame (tests).</summary>
        public bool StreakVisible => _streak[0] != null && _streak[0].gameObject.activeSelf;

        /// <summary>The streak has been drawn at some point of the current or last replay (tests).</summary>
        public bool StreakShown { get; private set; }

        /// <summary>The milky haze over a hazy memory (0 clear, up to <see cref="HazeAlpha"/>).</summary>
        public float Haze => _haze != null ? _haze.color.a : 0f;

        public const float HazeAlpha = 0.24f;
        public const int StreakEchoes = 4;

        public CinemachineCamera ActiveCamera => _active;

        public event Action Ended;

        public static Vector3[] TreehouseRoute() => new[]
        {
            new Vector3(-6.6f, CaseRouteLayout.TreehouseY + 0.05f, 13.4f),
            new Vector3(CaseRouteLayout.BridgeStartX, CaseRouteLayout.TreehouseY + 0.05f, CaseRouteLayout.BridgeZ),
            new Vector3(CaseRouteLayout.BridgeEndX, CaseRouteLayout.ExteriorSillY, CaseRouteLayout.BridgeZ),
            new Vector3(CaseRouteLayout.ExteriorWindowX - 0.15f, CaseRouteLayout.ExteriorSillY + 0.35f, CaseRouteLayout.ExteriorWindowZ),
        };

        /// <summary>
        /// Bea's glimpse (D26 §7, mf_bea_testimony): a silver smear high above the hedge, by the side of the house,
        /// from the treehouse side toward the roof, where it is gone. It never reaches the window.
        /// </summary>
        public static Vector3[] BeaStreak() => new[]
        {
            new Vector3(-6.6f, 3.55f, 13.9f),
            new Vector3(-5.2f, 3.95f, 14.0f),
            new Vector3(-3.9f, 4.25f, 14.1f),
        };

        public static Vector3 SideWindowCentre =>
            new Vector3(CaseRouteLayout.ExteriorWindowX - 0.06f, CaseRouteLayout.ExteriorSillY + 0.85f, CaseRouteLayout.ExteriorWindowZ);

        public static Vector3 FrontDoorCentre =>
            new Vector3(CaseStage.FrontDoor.X, CaseStage.FrontDoor.Y + 1.05f, CaseStage.FrontDoor.Z - 0.12f);

        /// <summary>The default staging of case 01's places in the street layout.</summary>
        public static ReplayStaging DefaultStaging(ReplayPlace place)
        {
            if (place == ReplayPlace.FrontDoor)
            {
                var seat = CaseStage.Find(CaseStage.PointBench).Position;
                var benchEye = new Vector3(seat.X, CaseStage.BenchEyeHeight + 0.1f, seat.Z);
                return new ReplayStaging(benchEye, FrontDoorCentre, null, FrontDoorCentre + Vector3.right * 0.2f);
            }

            // From Bea's own spot on the plaza, at her eye height, toward the side of the house above the hedge.
            var bea = CaseStage.Find(CaseStage.PointBea).Position;
            var streak = BeaStreak();
            return new ReplayStaging(new Vector3(bea.X + 0.3f, 1.45f, bea.Z + 0.2f), new Vector3(-4.9f, 3.6f, 13.8f), null,
                streak[streak.Length - 1], streak);
        }

        /// <summary>The staging used for <paramref name="place"/>.</summary>
        public ReplayStaging StagingFor(ReplayPlace place) =>
            _staging.TryGetValue(place, out var staging) ? staging : DefaultStaging(place);

        /// <summary>Moves a place (camera, route, mark); takes effect from the next replay.</summary>
        public void SetStaging(ReplayPlace place, ReplayStaging staging) => _staging[place] = staging;

        public static MemoryReplayView Create(Transform parent, GameInput input, Camera output, RectTransform canvasRoot)
        {
            var go = new GameObject("MemoryReplay");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<MemoryReplayView>();
            view._input = input;
            view._brain = output != null ? output.GetComponent<CinemachineBrain>() : null;

            view._treehouseCamera = MakeCamera(go.transform, TreehouseCameraName);
            view._doorCamera = MakeCamera(go.transform, FrontDoorCameraName);

            view.BuildOverlay(canvasRoot);
            view.BuildHaze();
            view.BuildWorld(go.transform);
            view.SetLook(0f);
            return view;
        }

        private static CinemachineCamera MakeCamera(Transform parent, string name)
        {
            var cameraObject = new GameObject(name);
            cameraObject.transform.SetParent(parent, false);
            cameraObject.SetActive(false);
            var vcam = cameraObject.AddComponent<CinemachineCamera>();
            var lens = vcam.Lens;
            lens.FieldOfView = FieldOfView;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 200f;
            vcam.Lens = lens;
            return vcam;
        }

        private void BuildOverlay(RectTransform canvasRoot)
        {
            _overlay = UiFactory.Stretch(UiFactory.Rect(canvasRoot, "MemoryReplayOverlay"));
            _tint = UiFactory.Panel(_overlay, "PencilTint", UiFactory.WithAlpha(Lilac, 0f), raycast: false);
            _vignette = UiFactory.Panel(_overlay, "Vignette", UiFactory.WithAlpha(Palette.NightViolet, 0f), raycast: false);
            _vignette.sprite = EdgeFade();
            _topBar = Bar("TopBar", new Vector2(0f, 1f));
            _bottomBar = Bar("BottomBar", new Vector2(0f, 0f));

            // Tap anywhere skips: a clear catcher over the world (the HUD is hidden while the replay runs).
            var catcher = UiFactory.Panel(_overlay, "TapToSkip", new Color(0f, 0f, 0f, 0f));
            var catcherButton = catcher.gameObject.AddComponent<Button>();
            catcherButton.transition = Selectable.Transition.None;
            catcherButton.onClick.AddListener(() => Skip());

            var safe = UiFactory.Stretch(UiFactory.Rect(_overlay, "SafeArea"));
            safe.gameObject.AddComponent<SafeAreaFitter>();
            var disc = UiFactory.FeltDisc(safe, "SkipDisc", UiSpriteCatalog.FeltDiscNight, Palette.WarmPaper,
                UiSpriteCatalog.IconSkip, Palette.WarmPaper, null, UiFactory.SystemDiscDiameter, Vector2.one,
                new Vector2(-62f, -60f - BarHeight * 0.5f), 0.92f);
            var button = disc.gameObject.AddComponent<Button>();
            button.targetGraphic = disc;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => Skip());
            SkipButton = button;
            // Its name, as the accessibility name and as a small caption in the letterbox beside the disc.
            SpookyStreet.Localization.AccessibleName.Set(disc.gameObject, NvKeys.MemorySkip);
            var caption = UiFactory.Label(disc.transform, "Caption", NvKeys.MemorySkip, 26, TextAnchor.MiddleRight,
                UiFactory.WithAlpha(Palette.WarmPaper, 0.85f), UiFactory.BoldFont);
            UiFactory.Place(caption.rectTransform, new Vector2(0f, 0.5f), new Vector2(-18f, 0f), new Vector2(300f, 40f),
                new Vector2(1f, 0.5f));
            SkipCaption = caption;
            // The candy progress ring fills over the replay (how long is left, without numbers).
            _skipRing = UiFactory.Sprite(disc.transform, "Progress", UiFactory.Baked(UiSpriteCatalog.StitchRing),
                Palette.CandyCornYellow, new Vector2(0.5f, 0.5f), Vector2.zero,
                Vector2.one * (UiFactory.SystemDiscDiameter + 16f));
            _skipRing.type = Image.Type.Filled;
            _skipRing.fillMethod = Image.FillMethod.Radial360;
            _skipRing.fillOrigin = (int)Image.Origin360.Top;
            _skipRing.fillClockwise = true;
            _overlay.gameObject.SetActive(false);
        }

        private RectTransform Bar(string name, Vector2 anchor)
        {
            var bar = UiFactory.Panel(_overlay, name, Palette.InkBlack, raycast: false).rectTransform;
            bar.anchorMin = anchor;
            bar.anchorMax = new Vector2(1f, anchor.y);
            bar.pivot = new Vector2(0.5f, anchor.y);
            bar.sizeDelta = new Vector2(0f, BarHeight);
            return bar;
        }

        private void BuildWorld(Transform parent)
        {
            _ghost = WorldSprite(parent, "JuneMemoryGhost", GhostFrame(0), GhostTint);
            var smear = StreakSprite();
            for (int i = 0; i < _streak.Length; i++)
            {
                _streak[i] = WorldSprite(parent, i == 0 ? "MemoryBlurStreak" : "MemoryBlurEcho" + i, null, Color.white);
                _streak[i].sprite = smear;
                _streak[i].sortingOrder = 50 - i;
            }
        }

        /// <summary>A pale milky veil over the whole view for a hazy memory, under the letterbox.</summary>
        private void BuildHaze()
        {
            _haze = UiFactory.Panel(_overlay, "MemoryHaze", UiFactory.WithAlpha(Palette.WarmPaper, 0f), raycast: false);
            _haze.rectTransform.SetSiblingIndex(1);
        }

        /// <summary>The pale paper ghost frame (falls back to June's run puppet when the extras are missing).</summary>
        private static string GhostFrame(int frame)
        {
            var frames = PuppetAtlasLayout.JuneGhostFrames;
            string id = frames[((frame % frames.Count) + frames.Count) % frames.Count];
            return PuppetAtlasLayout.TryGet(id, out _) ? id : PuppetAtlas.FrameOf(PuppetParts.JuneRun, frame);
        }

        private static SpriteRenderer WorldSprite(Transform parent, string name, string part, Color tint)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = PuppetAtlas.Get(part);
            renderer.color = tint;
            renderer.sortingOrder = 50;
            go.SetActive(false);
            return renderer;
        }

        /// <summary>
        /// Starts <paramref name="replay"/>. On-demand replays (the notebook eye) use the same path; the caller marks
        /// first hearings as seen before calling (§12 step 4).
        /// </summary>
        public void Play(MemoryReplayDefinition replay, bool reducedMotion)
        {
            if (replay == null)
            {
                return;
            }

            End(false);
            Playback = new MemoryReplayPlayback(replay, MemoryReplayTiming.Default, reducedMotion);
            Last = replay;
            PlayCount++;
            _started = Time.unscaledTime;
            _ghostClock = 0f;
            ConfigureBlends(reducedMotion);
            _current = StagingFor(replay.Place);
            _active = replay.Place == ReplayPlace.FrontDoor ? _doorCamera : _treehouseCamera;
            _active.transform.SetPositionAndRotation(_current.Eye,
                Quaternion.LookRotation(_current.LookAt - _current.Eye, Vector3.up));
            _active.gameObject.SetActive(true);
            _route = _current.HasGhost ? _current.Route : null;
            _streakPath = _current.HasStreak ? _current.Streak : null;
            StreakShown = false;
            _reduced = reducedMotion;
            _overlay.gameObject.SetActive(true);
            _overlay.SetAsLastSibling();
            Step(0f);
        }

        /// <summary>Tap anywhere or the skip disc: blend back now.</summary>
        public void Skip()
        {
            if (Active)
            {
                Playback.Skip();
                _active?.gameObject.SetActive(false);
                Step(0f);
            }
        }

        /// <summary>Ends at once (back to title, new case); <paramref name="notify"/> raises <see cref="Ended"/>.</summary>
        public void End(bool notify = true)
        {
            bool was = Playback != null;
            Playback = null;
            if (_active != null)
            {
                _active.gameObject.SetActive(false);
                _active = null;
            }

            SetLook(0f);
            _overlay.gameObject.SetActive(false);
            _ghost.gameObject.SetActive(false);
            foreach (var s in _streak)
            {
                s.gameObject.SetActive(false);
            }

            if (_haze != null)
            {
                _haze.color = UiFactory.WithAlpha(Palette.WarmPaper, 0f);
            }

            if (was && notify)
            {
                Ended?.Invoke();
            }
        }

        private void ConfigureBlends(bool cut)
        {
            if (_brain == null)
            {
                return;
            }

            if (_blends == null)
            {
                _blends = ScriptableObject.CreateInstance<CinemachineBlenderSettings>();
                _blends.name = "MemoryReplayBlends";
            }

            var style = cut ? CinemachineBlendDefinition.Styles.Cut : CinemachineBlendDefinition.Styles.EaseInOut;
            var blend = new CinemachineBlendDefinition(style, MemoryReplayTiming.DefaultBlend);
            const string any = AnyCameraLabel;
            _blends.CustomBlends = new[]
            {
                new CinemachineBlenderSettings.CustomBlend { From = any, To = TreehouseCameraName, Blend = blend },
                new CinemachineBlenderSettings.CustomBlend { From = TreehouseCameraName, To = any, Blend = blend },
                new CinemachineBlenderSettings.CustomBlend { From = any, To = FrontDoorCameraName, Blend = blend },
                new CinemachineBlenderSettings.CustomBlend { From = FrontDoorCameraName, To = any, Blend = blend },
            };
            _brain.CustomBlends = _blends;
        }

        private void Update()
        {
            if (Playback == null)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (dt > 0f && Time.unscaledTime - _started > SkipGraceSeconds && SkipPressed())
            {
                Skip();
            }

            Step(dt);
        }

        private bool SkipPressed()
        {
            if (_input == null)
            {
                return false;
            }

            return _input.Jump.WasPressedThisFrame() || _input.Use.WasPressedThisFrame() ||
                   _input.ReadMove().sqrMagnitude > 0.25f;
        }

        private void Step(float dt)
        {
            if (Playback == null)
            {
                return;
            }

            var phase = Playback.Phase;
            Playback.Advance(dt);
            _ghostClock += dt;
            if (Playback.Phase == ReplayPhase.BlendOut && phase != ReplayPhase.BlendOut)
            {
                // The hold ended: hand the view back (Cinemachine blends out over the same 0.35 s).
                _active?.gameObject.SetActive(false);
            }

            if (Playback.IsDone)
            {
                End();
                return;
            }

            SetLook(Playback.Weight);
            _skipRing.fillAmount = Mathf.Clamp01(Playback.GhostProgress);
            StageGhost(Playback.Replay.Place, Playback.GhostProgress, Playback.Weight);
        }

        private void SetLook(float w)
        {
            bool hazy = Playback != null && _streakPath != null;
            _tint.color = UiFactory.WithAlpha(Lilac, (hazy ? 0.34f : 0.26f) * w);
            if (_haze != null)
            {
                _haze.color = UiFactory.WithAlpha(Palette.WarmPaper, hazy ? HazeAlpha * w : 0f);
            }

            _vignette.color = UiFactory.WithAlpha(Palette.NightViolet, 0.7f * w);
            _topBar.sizeDelta = new Vector2(0f, BarHeight * w);
            _bottomBar.sizeDelta = new Vector2(0f, BarHeight * w);
            if (SkipButton != null)
            {
                var group = SkipButton.transform as RectTransform;
                group.localScale = Vector3.one * Mathf.Lerp(0.6f, 1f, w);
            }
        }

        private void StageGhost(ReplayPlace place, float progress, float weight)
        {
            var cam = Camera.main;
            Quaternion face = cam != null ? cam.transform.rotation : Quaternion.identity;
            StageStreak(progress, weight, cam);
            if (_active != null && _streakPath != null && !_reduced)
            {
                // The memory is hazy: the view drifts a little toward the house, as if remembering, never settling.
                Vector3 drift = new Vector3(0.18f * progress, 0.06f * Mathf.Sin(_ghostClock * 1.3f), 0.12f * progress);
                _active.transform.SetPositionAndRotation(_current.Eye + drift,
                    Quaternion.LookRotation(_current.LookAt - (_current.Eye + drift), Vector3.up));
            }

            if (_route == null)
            {
                // The front door from the bench (Vale): the door simply stays shut; nothing is stamped on it (D26).
                _ghost.gameObject.SetActive(false);
                return;
            }

            // A staged ghost route (none of case 01's memories use one since S002.5b): the paper cut-out runs it and
            // fades; no ring marks where it went.
            float run = Mathf.Clamp01(progress / 0.8f);
            float into = Mathf.Clamp01((progress - 0.8f) / 0.2f);
            Vector3 at = PathAt(_route, run);
            at.y += GhostHeight * 0.5f + 0.06f * Mathf.Abs(Mathf.Sin(_ghostClock * 14f)) * (1f - into);
            _ghost.gameObject.SetActive(weight > 0.05f && into < 1f);
            _ghost.transform.SetPositionAndRotation(at, face);
            SetWorldSize(_ghost, GhostHeight * Mathf.Lerp(1f, 0.55f, into));
            int frame = run < 1f && progress < 1f ? (int)(_ghostClock * VignettePlayer.FlipbookFps) : 0;
            _ghost.sprite = PuppetAtlas.Get(GhostFrame(frame));
            _ghost.color = UiFactory.WithAlpha(GhostTint, GhostTint.a * weight * (1f - into));
            _ghost.flipX = cam != null && Vector3.Dot(cam.transform.right, _route[_route.Length - 1] - _route[0]) < 0f;
        }

        /// <summary>When in the hold the streak crosses (the rest of the memory is only the haze and the house).</summary>
        public const float StreakFrom = 0.3f;

        public const float StreakTo = 0.62f;

        /// <summary>
        /// Bea's glimpse: a soft silver smear rushes along the streak path, trailing fading echoes like a motion blur,
        /// and is gone. Reduced motion shows one still smear, faint, at the middle of the path.
        /// </summary>
        private void StageStreak(float progress, float weight, Camera cam)
        {
            bool on = _streakPath != null && weight > 0.05f;
            float u = (progress - StreakFrom) / (StreakTo - StreakFrom);
            if (_reduced)
            {
                // Reduced motion has no ghost clock (progress holds at its end): one still, faint smear for the hold.
                u = 0.5f;
            }

            on &= u >= 0f && u <= 1f;
            for (int i = 0; i < _streak.Length; i++)
            {
                var s = _streak[i];
                float ui = _reduced ? u : u - i * 0.07f;
                bool show = on && ui >= 0f && ui <= 1f && (!_reduced || i == 0);
                s.gameObject.SetActive(show);
                if (!show)
                {
                    continue;
                }

                StreakShown = true;
                float e = ui * ui * (3f - 2f * ui);
                Vector3 at = PathAt(_streakPath, e);
                Vector3 ahead = PathAt(_streakPath, Mathf.Min(1f, e + 0.05f)) - PathAt(_streakPath, Mathf.Max(0f, e - 0.05f));
                // Face the camera and lie along the motion on screen.
                Quaternion face = cam != null ? cam.transform.rotation : Quaternion.identity;
                float roll = 0f;
                if (cam != null)
                {
                    Vector3 local = cam.transform.InverseTransformDirection(ahead);
                    roll = Mathf.Atan2(local.y, local.x) * Mathf.Rad2Deg;
                }

                s.transform.SetPositionAndRotation(at, face * Quaternion.Euler(0f, 0f, roll));
                float swell = Mathf.Sin(Mathf.Clamp01(ui) * Mathf.PI);
                float length = (_reduced ? 0.9f : 1.5f) * (1f - 0.12f * i);
                float native = s.sprite != null ? s.sprite.bounds.size.x : 1f;
                float k = length / Mathf.Max(0.01f, native);
                s.transform.localScale = new Vector3(k, k * (0.75f + 0.25f * swell), 1f);
                float alpha = weight * swell * (_reduced ? 0.55f : 0.9f) * (1f - 0.22f * i);
                Color c = Palette.ClothSilver;
                c.a = alpha;
                s.color = c;
            }
        }

        private static void SetWorldSize(SpriteRenderer renderer, float height)
        {
            var sprite = renderer.sprite;
            float native = sprite != null ? sprite.bounds.size.y : 1f;
            renderer.transform.localScale = Vector3.one * (height / Mathf.Max(0.01f, native));
        }

        public static Vector3 PathAt(Vector3[] points, float t)
        {
            float total = 0f;
            for (int i = 0; i + 1 < points.Length; i++)
            {
                total += Vector3.Distance(points[i], points[i + 1]);
            }

            float d = Mathf.Clamp01(t) * total;
            for (int i = 0; i + 1 < points.Length; i++)
            {
                float len = Vector3.Distance(points[i], points[i + 1]);
                if (d <= len || i + 2 == points.Length)
                {
                    return Vector3.Lerp(points[i], points[i + 1], len > 0f ? Mathf.Clamp01(d / len) : 1f);
                }

                d -= len;
            }

            return points[points.Length - 1];
        }

        private static Sprite s_streak;

        /// <summary>A soft horizontal smear: a bright rounded head on the right, a tail that thins and fades left.</summary>
        private static Sprite StreakSprite()
        {
            if (s_streak != null)
            {
                return s_streak;
            }

            const int w = 128, h = 32;
            var texture = new Texture2D(w, h, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "BlurStreak" };
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    float u = (x + 0.5f) / w;
                    float v = (y + 0.5f) / h * 2f - 1f;
                    float thickness = Mathf.Lerp(0.12f, 0.85f, Mathf.Pow(u, 1.6f)) * (u > 0.88f ? Mathf.Sqrt(Mathf.Clamp01((1f - u) / 0.12f)) : 1f);
                    float across = Mathf.Clamp01(1f - Mathf.Abs(v) / Mathf.Max(0.01f, thickness));
                    float a = across * across * Mathf.Pow(u, 1.2f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a));
                }
            }

            texture.Apply();
            s_streak = Sprite.Create(texture, new Rect(0f, 0f, w, h), new Vector2(0.5f, 0.5f), w);
            return s_streak;
        }

        private static Sprite s_edge;

        /// <summary>Clear in the middle, opaque at the edges (the memory vignette).</summary>
        private static Sprite EdgeFade()
        {
            if (s_edge != null)
            {
                return s_edge;
            }

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float d = Mathf.Clamp01((Mathf.Sqrt(dx * dx * 0.7f + dy * dy) - 0.55f) / 0.6f);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, d * d));
                }
            }

            texture.Apply();
            s_edge = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f));
            return s_edge;
        }
    }
}
