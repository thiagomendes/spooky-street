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

namespace SpookyStreet.Case
{
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
        public const string AnyCameraLabel = "**ANY CAMERA**";

        private static readonly Color Lilac = new Color(0.62f, 0.52f, 0.86f, 1f);
        private static readonly Color GhostTint = new Color(0.93f, 0.95f, 1f, 0.72f);

        private GameInput _input;
        private CinemachineBrain _brain;
        private CinemachineBlenderSettings _blends;
        private CinemachineCamera _treehouseCamera;
        private CinemachineCamera _doorCamera;
        private CinemachineCamera _active;
        private RectTransform _overlay;
        private Image _tint;
        private Image _vignette;
        private RectTransform _topBar;
        private RectTransform _bottomBar;
        private Image _skipRing;
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

        public Button SkipButton { get; private set; }

        public SpriteRenderer Ghost => _ghost;

        public CinemachineCamera ActiveCamera => _active;

        public event Action Ended;

        public static Vector3[] TreehouseRoute() => new[]
        {
            new Vector3(-6.6f, CaseRouteLayout.TreehouseY + 0.05f, 13.4f),
            new Vector3(CaseRouteLayout.BridgeStartX, CaseRouteLayout.TreehouseY + 0.05f, CaseRouteLayout.BridgeZ),
            new Vector3(CaseRouteLayout.BridgeEndX, CaseRouteLayout.SillY, CaseRouteLayout.BridgeZ),
            new Vector3(CaseRouteLayout.WindowX - 0.15f, CaseRouteLayout.SillY + 0.35f, CaseRouteLayout.WindowZ),
        };

        public static Vector3 SideWindowCentre =>
            new Vector3(CaseRouteLayout.WindowX - 0.06f, CaseRouteLayout.SillY + 0.85f, CaseRouteLayout.WindowZ);

        public static Vector3 FrontDoorCentre =>
            new Vector3(CaseStage.FrontDoor.X, CaseStage.FrontDoor.Y + 1.05f, CaseStage.FrontDoor.Z - 0.12f);

        public static MemoryReplayView Create(Transform parent, GameInput input, Camera output, RectTransform canvasRoot)
        {
            var go = new GameObject("MemoryReplay");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<MemoryReplayView>();
            view._input = input;
            view._brain = output != null ? output.GetComponent<CinemachineBrain>() : null;

            var balconyEye = new Vector3(-8.4f, 3.7f, 5.6f);
            var balconyAt = new Vector3(-4.9f, 2.9f, 13.7f);
            view._treehouseCamera = MakeCamera(go.transform, TreehouseCameraName, balconyEye, balconyAt);
            var seat = CaseStage.Find(CaseStage.PointBench).Position;
            var benchEye = new Vector3(seat.X, CaseStage.BenchEyeHeight + 0.1f, seat.Z);
            view._doorCamera = MakeCamera(go.transform, FrontDoorCameraName, benchEye, FrontDoorCentre);

            view.BuildOverlay(canvasRoot);
            view.BuildWorld(go.transform);
            view.SetLook(0f);
            return view;
        }

        private static CinemachineCamera MakeCamera(Transform parent, string name, Vector3 eye, Vector3 at)
        {
            var cameraObject = new GameObject(name);
            cameraObject.transform.SetParent(parent, false);
            cameraObject.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(at - eye, Vector3.up));
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
            _ghost = WorldSprite(parent, "JuneMemoryGhost", PuppetParts.JuneRun, GhostTint);
            _ring = WorldSprite(parent, "MemoryKeyRing", PuppetParts.KeyRing, Color.white);
            _stamp = WorldSprite(parent, "MemoryNeverStamp", "pg.not", Color.white);
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
            _active = replay.Place == ReplayPlace.FrontDoor ? _doorCamera : _treehouseCamera;
            _active.gameObject.SetActive(true);
            _route = replay.Place == ReplayPlace.TreehouseWindow ? TreehouseRoute() : null;
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
            _ring.gameObject.SetActive(false);
            _stamp.gameObject.SetActive(false);
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
            _tint.color = UiFactory.WithAlpha(Lilac, 0.26f * w);
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
            if (place == ReplayPlace.TreehouseWindow && _route != null)
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
                _ghost.sprite = PuppetAtlas.Get(PuppetAtlas.FrameOf(PuppetParts.JuneRun, frame));
                _ghost.color = UiFactory.WithAlpha(GhostTint, GhostTint.a * weight * (1f - into));
                _ghost.flipX = cam != null && Vector3.Dot(cam.transform.right, _route[_route.Length - 1] - _route[0]) < 0f;

                bool ring = progress >= 0.8f && weight > 0.05f;
                _ring.gameObject.SetActive(ring);
                if (ring)
                {
                    float pulse = 1f + 0.08f * Mathf.Sin(_ghostClock * 2f * Mathf.PI * 1.6f);
                    float pop = VignetteSampler.Apply(Ease.Back, Mathf.Clamp01((progress - 0.8f) * 6f));
                    _ring.transform.SetPositionAndRotation(SideWindowCentre, face);
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
                _stamp.transform.SetPositionAndRotation(FrontDoorCentre + Vector3.right * 0.2f,
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
