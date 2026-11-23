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
    /// Where a memory replay happens: the camera pose, the ghost's route (empty: no ghost) and the mark that gets the
    /// key ring (with a route) or the ⊘ stamp (without). Data, so the places can move (D24: the interior becomes its
    /// own space) without touching the view.
    /// </summary>
    public readonly struct ReplayStaging
    {
        public ReplayStaging(Vector3 eye, Vector3 lookAt, Vector3[] route, Vector3 mark)
        {
            Eye = eye;
            LookAt = lookAt;
            Route = route ?? System.Array.Empty<Vector3>();
            Mark = mark;
        }

        public Vector3 Eye { get; }
        public Vector3 LookAt { get; }
        public Vector3[] Route { get; }
        public Vector3 Mark { get; }
        public bool HasGhost => Route.Length >= 2;
    }

    /// <summary>
    /// The in-world memory replay (docs/nonverbal-ux.md §4, D23.2, D23.7): a fixed Cinemachine camera per
    /// <see cref="ReplayPlace"/> takes over like <see cref="BenchView"/> (0.35 s blend in, 1.8 s hold, 0.35 s back,
    /// all from <see cref="MemoryReplayPlayback"/>), ink letterbox bars slide in and a lilac pencil tint with a soft
    /// vignette covers the world, both driven by <see cref="MemoryReplayPlayback.Weight"/>. June's memory ghost is
    /// the flat paper cut-out path of D23.7 (her running puppet from the PuppetAtlas, pale translucent, billboarded
    /// and flipbooked) moving along the real route from the treehouse balcony over the bridge into the side window,
    /// which then gets the pulsing orange key ring; at the front door the ⊘ stamp thumps onto the real door.
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
        public const string AnyCameraLabel = CinematicBlends.AnyCamera;

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
        private CinematicFrame _frame;
        private SpriteRenderer _ghost;
        private SpriteRenderer _ring;
        private SpriteRenderer _stamp;
        private Vector3[] _route;
        private float _started;
        private float _ghostClock;

        public MemoryReplayPlayback Playback { get; private set; }

        public bool Active => Playback != null && !Playback.IsDone;

        /// <summary>The replay that played last (tests).</summary>
        public MemoryReplayDefinition Last { get; private set; }

        /// <summary>Replays started since the scene loaded (tests).</summary>
        public int PlayCount { get; private set; }

        public float Weight => Active ? Playback.Weight : 0f;

        public Button SkipButton => _frame.SkipButton;

        /// <summary>The skip disc's small caption ("Skip memory", <c>ui.memory.skip</c>) in the letterbox.</summary>
        public Text SkipCaption => _frame.SkipCaption;

        /// <summary>The letterbox, tint, vignette and skip disc (shared with the cutscenes, <see cref="CinematicFrame"/>).</summary>
        public CinematicFrame Frame => _frame;

        public SpriteRenderer Ghost => _ghost;

        public CinemachineCamera ActiveCamera => _active;

        public event Action Ended;

        public static Vector3[] TreehouseRoute() => new[]
        {
            new Vector3(-6.6f, CaseRouteLayout.TreehouseY + 0.05f, 13.4f),
            new Vector3(CaseRouteLayout.BridgeStartX, CaseRouteLayout.TreehouseY + 0.05f, CaseRouteLayout.BridgeZ),
            new Vector3(CaseRouteLayout.BridgeEndX, CaseRouteLayout.ExteriorSillY, CaseRouteLayout.BridgeZ),
            new Vector3(CaseRouteLayout.ExteriorWindowX - 0.15f, CaseRouteLayout.ExteriorSillY + 0.35f, CaseRouteLayout.ExteriorWindowZ),
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

            return new ReplayStaging(new Vector3(-8.4f, 3.7f, 5.6f), new Vector3(-4.9f, 2.9f, 13.7f), TreehouseRoute(),
                SideWindowCentre);
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
            // Tap anywhere or the disc skips (the HUD is hidden while the replay runs).
            _frame = CinematicFrame.Create(canvasRoot, "MemoryReplayOverlay", BarHeight, NvKeys.MemorySkip, Lilac, 0.26f, 0.7f);
            _frame.Tapped += Skip;
            _frame.SkipPressed += Skip;
        }

        private void BuildWorld(Transform parent)
        {
            _ghost = WorldSprite(parent, "JuneMemoryGhost", GhostFrame(0), GhostTint);
            _ring = WorldSprite(parent, "MemoryKeyRing", PuppetParts.KeyRing, Color.white);
            _stamp = WorldSprite(parent, "MemoryNeverStamp", "pg.not", Color.white);
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
            _frame.Show();
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

            _frame.Hide();
            _ghost.gameObject.SetActive(false);
            _ring.gameObject.SetActive(false);
            _stamp.gameObject.SetActive(false);
            if (was && notify)
            {
                Ended?.Invoke();
            }
        }

        private void ConfigureBlends(bool cut) => CinematicBlends.Configure(_brain, ref _blends, "MemoryReplayBlends", cut,
            MemoryReplayTiming.DefaultBlend, TreehouseCameraName, FrontDoorCameraName);

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
            _frame.Progress = Playback.GhostProgress;
            StageGhost(Playback.Replay.Place, Playback.GhostProgress, Playback.Weight);
        }

        private void SetLook(float w) => _frame.SetLook(w);

        private void StageGhost(ReplayPlace place, float progress, float weight)
        {
            var cam = Camera.main;
            Quaternion face = cam != null ? cam.transform.rotation : Quaternion.identity;
            if (_route != null)
            {
                // Run 0..0.8 of the hold along the route, then pop into the window; the ring pulses on it.
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

                bool ring = progress >= 0.8f && weight > 0.05f;
                _ring.gameObject.SetActive(ring);
                if (ring)
                {
                    float pulse = 1f + 0.08f * Mathf.Sin(_ghostClock * 2f * Mathf.PI * 1.6f);
                    float pop = VignetteSampler.Apply(Ease.Back, Mathf.Clamp01((progress - 0.8f) * 6f));
                    _ring.transform.SetPositionAndRotation(_current.Mark, face);
                    SetWorldSize(_ring, 1.5f * pop * pulse);
                    _ring.color = UiFactory.WithAlpha(Color.white, weight);
                }

                _stamp.gameObject.SetActive(false);
                return;
            }

            // Front door: the door stays shut; late in the hold ⊘ thumps onto it and wiggles to a stop.
            _ghost.gameObject.SetActive(false);
            _ring.gameObject.SetActive(false);
            float p = Mathf.Clamp01((progress - 0.45f) / 0.45f);
            bool stamp = progress >= 0.45f && weight > 0.05f;
            _stamp.gameObject.SetActive(stamp);
            if (stamp)
            {
                float size = Mathf.Lerp(1.6f, 1f, VignetteSampler.Apply(Ease.Out, Mathf.Min(1f, p * 3f)));
                _stamp.transform.SetPositionAndRotation(_current.Mark,
                    face * Quaternion.Euler(0f, 0f, VignetteSampler.Wiggle(p)));
                SetWorldSize(_stamp, 0.95f * size);
                _stamp.color = UiFactory.WithAlpha(Color.white, weight * Mathf.Min(1f, p * 4f));
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
    }
}
