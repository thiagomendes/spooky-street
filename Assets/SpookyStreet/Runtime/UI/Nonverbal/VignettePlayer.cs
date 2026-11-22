using System;
using System.Collections.Generic;
using SpookyStreet.Core.Nonverbal;
using UnityEngine;
using UnityEngine.UI;

namespace SpookyStreet.UI.Nonverbal
{
    /// <summary>
    /// Plays one paper-puppet <see cref="Vignette"/> inside a masked stage (docs/nonverbal-ux.md §3, §10): each frame
    /// it samples <see cref="VignetteSampler"/> into a reused <see cref="PartState"/> buffer and moves pooled
    /// <see cref="Image"/>s (one per track, all from the one PuppetAtlas texture) plus up to three overlay images for
    /// the ring, puff and knock cues. Stamp cues already wiggle their target through the sampler. World cues (Bea's
    /// <c>Point</c> gesture, babble syncs) are raised through <see cref="CueStarted"/> once per loop.
    /// Runs on unscaled time gated by <see cref="CanRun"/>, so the game's pause freezes it; reduced motion holds each
    /// beat's end pose (<see cref="Vignette.ReducedMotionTime"/>) and cross-fades between beats; rings stop pulsing.
    /// No per-frame allocations.
    /// </summary>
    public sealed class VignettePlayer : MonoBehaviour
    {
        public const int OverlayCount = 3;

        /// <summary>June's flipbook frame rate while her track moves.</summary>
        public const float FlipbookFps = 12f;

        private const float BeatFadeSeconds = 0.25f;

        private readonly PartState[] _parts = new PartState[Vignette.MaxTracks];
        private readonly Vector2[] _lastPos = new Vector2[Vignette.MaxTracks];
        private readonly Image[] _images = new Image[Vignette.MaxTracks];
        private readonly Image[] _overlays = new Image[OverlayCount];
        private readonly List<CueState> _cues = new List<CueState>();
        private readonly List<VignetteCue> _started = new List<VignetteCue>();

        private RectTransform _stage;
        private CanvasGroup _group;
        private float _prevLocal;
        private float _holdTime = -1f;
        private float _fade = 1f;
        private int _count;

        /// <summary>The vignette on the stage (null when idle).</summary>
        public Vignette Current { get; private set; }

        /// <summary>Seconds played since <see cref="Play"/> (frozen while paused or not running).</summary>
        public float Elapsed { get; private set; }

        /// <summary>Time inside the loop last sampled.</summary>
        public float LocalTime { get; private set; }

        /// <summary>Completed loops (0 while the first pass plays).</summary>
        public int LoopIndex => Current == null || Current.Length <= 0f ? 0 : (int)(Elapsed / Current.Length);

        /// <summary>True while time advances (false for a still or after a finite vignette finished).</summary>
        public bool Running { get; private set; }

        public bool Finished => Current != null && Current.IsFinished(Elapsed);

        /// <summary>Gate for time (pause); null always runs.</summary>
        public Func<bool> CanRun { get; set; }

        /// <summary>
        /// The ring cue is shown by the owner instead of the stitched overlay (the notebook stitches the active
        /// panel orange); <see cref="ActiveRingX"/> says where it is.
        /// </summary>
        public bool SuppressRings { get; set; }

        /// <summary>Stage-normalized x of the running ring cue's target, or -1 when no ring runs.</summary>
        public float ActiveRingX { get; private set; } = -1f;

        /// <summary>Reduced motion: beats become still poses with a cross-fade.</summary>
        public bool ReducedMotion { get; set; }

        /// <summary>World cues (gesture, babble) as they start, with the loop they start in.</summary>
        public event Action<VignetteCue, int> CueStarted;

        /// <summary>Images showing a part right now (tests and captures).</summary>
        public int VisibleParts
        {
            get
            {
                int n = 0;
                for (int i = 0; i < _count; i++)
                {
                    if (_images[i].enabled)
                    {
                        n++;
                    }
                }

                return n;
            }
        }

        public RectTransform Stage => _stage;

        public static VignettePlayer Create(Transform parent, string name)
        {
            var rect = UiFactory.Stretch(UiFactory.Rect(parent, name));
            rect.gameObject.AddComponent<RectMask2D>();
            var player = rect.gameObject.AddComponent<VignettePlayer>();
            player._stage = rect;
            player._group = rect.gameObject.AddComponent<CanvasGroup>();
            player._group.blocksRaycasts = false;
            player._group.interactable = false;
            for (int i = 0; i < player._images.Length; i++)
            {
                player._images[i] = player.NewImage("Part" + i);
            }

            for (int i = 0; i < OverlayCount; i++)
            {
                player._overlays[i] = player.NewImage("Overlay" + i);
            }

            return player;
        }

        private Image NewImage(string name)
        {
            var image = UiFactory.Sprite(_stage, name, null, Color.white, Vector2.zero, Vector2.zero, Vector2.one * 10f);
            image.preserveAspect = false;
            image.enabled = false;
            return image;
        }

        /// <summary>Starts <paramref name="vignette"/> from its beginning (null clears the stage).</summary>
        public void Play(Vignette vignette)
        {
            Load(vignette);
            Running = vignette != null;
        }

        /// <summary>Shows <paramref name="vignette"/> frozen at local time <paramref name="time"/> (the notebook's resting strip).</summary>
        public void ShowStill(Vignette vignette, float time)
        {
            Load(vignette);
            Running = false;
            Elapsed = time;
            Render(time);
        }

        public void Stop()
        {
            Load(null);
            Running = false;
        }

        private void Load(Vignette vignette)
        {
            Current = vignette;
            Elapsed = 0f;
            LocalTime = 0f;
            _prevLocal = -1f;
            _holdTime = -1f;
            _fade = 1f;
            _count = vignette != null ? Math.Min(vignette.Tracks.Count, _images.Length) : 0;
            for (int i = 0; i < _images.Length; i++)
            {
                _images[i].enabled = false;
                _lastPos[i] = new Vector2(float.NaN, float.NaN);
            }

            foreach (var overlay in _overlays)
            {
                overlay.enabled = false;
            }

            if (_group != null)
            {
                _group.alpha = 1f;
            }

            if (vignette != null)
            {
                Render(0f);
            }
        }

        private void Update()
        {
            if (Current == null)
            {
                return;
            }

            bool can = Running && (CanRun == null || CanRun());
            float dt = can ? Time.unscaledDeltaTime : 0f;
            if (dt > 0f)
            {
                if (Current.IsFinished(Elapsed))
                {
                    Running = false;
                }
                else
                {
                    int loop = LoopIndex;
                    Elapsed += dt;
                    float local = Current.LocalTime(Elapsed);
                    FireCues(_prevLocal, local, loop);
                    _prevLocal = local;
                }
            }

            if (ReducedMotion && _fade < 1f)
            {
                _fade = Mathf.Min(1f, _fade + Time.unscaledDeltaTime / BeatFadeSeconds);
                _group.alpha = 0.35f + 0.65f * _fade;
            }

            Render(Current.LocalTime(Elapsed));
        }

        private void FireCues(float from, float to, int loop)
        {
            if (CueStarted == null)
            {
                return;
            }

            if (to >= from)
            {
                Raise(from, to, loop);
            }
            else
            {
                // Wrapped into the next loop.
                Raise(from, Current.Length, loop);
                Raise(-1f, to, loop + 1);
            }
        }

        private void Raise(float from, float to, int loop)
        {
            VignetteSampler.CuesStarted(Current, from, to, _started);
            foreach (var cue in _started)
            {
                if (!cue.IsVisual)
                {
                    CueStarted?.Invoke(cue, loop);
                }
            }
        }

        private void Render(float local)
        {
            LocalTime = local;
            float t = local;
            if (ReducedMotion)
            {
                t = Current.ReducedMotionTime(local);
                if (_holdTime >= 0f && Math.Abs(t - _holdTime) > 1e-4f)
                {
                    _fade = 0f;
                }

                _holdTime = t;
            }

            var size = _stage.rect.size;
            if (size.x <= 1f || size.y <= 1f)
            {
                return;
            }

            VignetteSampler.Sample(Current, t, _parts);
            int flip = (int)(Elapsed * FlipbookFps);
            for (int i = 0; i < _count; i++)
            {
                var state = _parts[i];
                var image = _images[i];
                if (!state.Visible)
                {
                    image.enabled = false;
                    continue;
                }

                var pose = state.Pose;
                var pos = new Vector2(pose.X * size.x, pose.Y * size.y);
                bool moving = !ReducedMotion && Running && !float.IsNaN(_lastPos[i].x) &&
                              (pos - _lastPos[i]).sqrMagnitude > 0.01f;
                _lastPos[i] = pos;
                string part = moving ? PuppetAtlas.FrameOf(state.Part, flip) : state.Part;
                var sprite = PuppetAtlas.Get(part);
                if (sprite == null)
                {
                    image.enabled = false;
                    continue;
                }

                image.enabled = true;
                image.sprite = sprite;
                var rect = image.rectTransform;
                rect.anchoredPosition = pos;
                rect.sizeDelta = PuppetAtlas.NominalSize(state.Part, size.y) * pose.Scale;
                rect.localRotation = Quaternion.Euler(0f, 0f, pose.Rotation);
                image.color = new Color(1f, 1f, 1f, pose.Alpha);
            }

            RenderOverlays(t, size);
        }

        private void RenderOverlays(float t, Vector2 size)
        {
            VignetteSampler.ActiveCues(Current, t, _cues);
            int used = 0;
            ActiveRingX = -1f;
            foreach (var cue in _cues)
            {
                if (cue.Cue.Kind == CueKind.Ring && SuppressRings)
                {
                    if (TryTarget(cue.Cue.Target, size, out var ringAt, out _))
                    {
                        ActiveRingX = ringAt.x / size.x;
                    }

                    continue;
                }

                if (used >= OverlayCount)
                {
                    break;
                }

                var kind = cue.Cue.Kind;
                if (kind != CueKind.Ring && kind != CueKind.Puff && kind != CueKind.Knock)
                {
                    continue;
                }

                if (!TryTarget(cue.Cue.Target, size, out var at, out var targetSize))
                {
                    continue;
                }

                var image = _overlays[used++];
                float p = Mathf.Clamp01(cue.Progress);
                float d = Mathf.Max(targetSize.x, targetSize.y);
                string sprite;
                float scale;
                float alpha = 1f;
                switch (kind)
                {
                    case CueKind.Ring:
                        sprite = PuppetParts.KeyRing;
                        float pop = ReducedMotion ? 1f : VignetteSampler.Apply(Ease.Back, Mathf.Min(1f, p * 4f));
                        float pulse = ReducedMotion ? 1f : 1f + 0.08f * Mathf.Sin(cue.Elapsed * 2f * Mathf.PI * 1.6f);
                        scale = 1.15f * pop * pulse;
                        break;
                    case CueKind.Puff:
                        sprite = PuppetParts.Puff;
                        scale = ReducedMotion ? 1f : 0.6f + 0.8f * p;
                        alpha = 1f - p;
                        break;
                    default:
                        sprite = "pg.knock_ring";
                        scale = ReducedMotion ? 0.9f : 0.5f + 0.9f * p;
                        alpha = 1f - p * p;
                        at += new Vector2(d * 0.25f, d * 0.1f);
                        break;
                }

                image.sprite = PuppetAtlas.Get(sprite);
                image.enabled = image.sprite != null;
                var rect = image.rectTransform;
                rect.anchoredPosition = at;
                rect.sizeDelta = new Vector2(d, d) * scale;
                rect.localRotation = Quaternion.identity;
                image.color = new Color(1f, 1f, 1f, alpha);
            }

            for (int i = used; i < OverlayCount; i++)
            {
                _overlays[i].enabled = false;
            }
        }

        private bool TryTarget(string trackId, Vector2 size, out Vector2 at, out Vector2 targetSize)
        {
            for (int i = 0; i < _count; i++)
            {
                if (string.Equals(_parts[i].TrackId, trackId, StringComparison.Ordinal))
                {
                    var pose = _parts[i].Pose;
                    at = new Vector2(pose.X * size.x, pose.Y * size.y);
                    targetSize = PuppetAtlas.NominalSize(_parts[i].Part, size.y) * Mathf.Max(0.3f, pose.Scale);
                    return true;
                }
            }

            at = Vector2.zero;
            targetSize = Vector2.zero;
            return false;
        }
    }
}
