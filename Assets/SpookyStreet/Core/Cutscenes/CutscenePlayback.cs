using System;
using System.Collections.Generic;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.World;

namespace SpookyStreet.Core.Cutscenes
{
    /// <summary>
    /// Run-time anchors a shot can be framed from. The runtime resolves them from the real models; these defaults keep
    /// the data and its tests engine-free.
    /// </summary>
    public static class CutsceneAnchors
    {
        /// <summary>The rooftop owl puppet over the attic window, facing the street.</summary>
        public const string Owl = "anchor.owl";

        /// <summary>Aunt Kit's framed troupe photo in the hall, facing into the hall.</summary>
        public const string KitPhoto = "anchor.kit_photo";

        public static AnchorFrame Default(string id)
        {
            switch (id)
            {
                case Owl:
                    return new AnchorFrame(new StagePoint(0f, 8.7f, CaseRouteLayout.FrontZ + 0.2f), 180f);
                case KitPhoto:
                    return new AnchorFrame(new StagePoint(2.15f, 1.66f, CaseRouteLayout.InteriorFrontZ + 5.22f), 270f);
                default:
                    return new AnchorFrame(new StagePoint(0f, 0f, 0f), 0f);
            }
        }
    }

    public enum CutscenePhase
    {
        Playing,

        /// <summary>The beats are over (or skipped): the letterbox lifts while the camera returns to play.</summary>
        Outro,

        Done,
    }

    public enum CutsceneEventKind
    {
        BeatStarted,
        Cast,
        Signal,

        /// <summary>The outro starts: apply <see cref="Cutscene.Settle"/> and hand the camera back.</summary>
        OutroStarted,

        Ended,
    }

    /// <summary>Something the runtime must do now.</summary>
    public readonly struct CutsceneEvent
    {
        public CutsceneEvent(CutsceneEventKind kind, int beat, CastCue cast = null, SignalCue signal = null)
        {
            Kind = kind;
            Beat = beat;
            Cast = cast;
            Signal = signal;
        }

        public CutsceneEventKind Kind { get; }
        public int Beat { get; }
        public CastCue Cast { get; }
        public SignalCue Signal { get; }

        public override string ToString() => Kind + " beat " + Beat + (Cast != null ? " " + Cast : "") + (Signal != null ? " " + Signal : "");
    }

    /// <summary>
    /// The engine-free evaluator of one playing cutscene. Advance it with game time (pause freezes it); it returns
    /// what fired in that step, each cue exactly once and in order whatever the frame rate, because it sub-steps at
    /// every beat boundary. The camera pose is a pure function of the beat and its time: blends ease from the last
    /// pose of the previous shot, drifts ease over the beat, and reduced motion cuts every blend, holds the end of
    /// every drift and drops the letterbox slide. A blend between the street and the interior is always a cut (the
    /// lens never travels through the void, D24.1). Skip works at any time: it goes straight to the outro.
    /// </summary>
    public sealed class CutscenePlayback
    {
        /// <summary>The letterbox slides in over this long (not under reduced motion).</summary>
        public const float LetterboxIn = 0.45f;

        /// <summary>A skip hands back to play over this long (0 under reduced motion).</summary>
        public const float SkipExitBlend = 0.35f;

        /// <summary>A page-turn cuts the camera this long into its beat, while the page covers the screen.</summary>
        public const float PageCutDelay = 0.06f;

        /// <summary>The page starts to cover this long before a page-turn beat (the threshold wipe's cover time).</summary>
        public const float PageCoverLead = ThresholdWipe.CoverSeconds;

        /// <summary>Time left below this counts as none, so float steps of any frame rate end a phase on the same frame.</summary>
        private const float Epsilon = 1e-4f;

        private readonly bool _reduced;
        private readonly Func<string, AnchorFrame?> _anchors;
        private readonly HashSet<string> _signals = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<CutsceneEvent> _events = new List<CutsceneEvent>();
        private bool _started;
        private float _waited;
        private float _outroTime;
        private float _outroLength;
        private float _outroFrom = 1f;

        public CutscenePlayback(Cutscene cutscene, bool reducedMotion, Func<string, AnchorFrame?> anchors = null)
        {
            Cutscene = cutscene ?? throw new ArgumentNullException(nameof(cutscene));
            _reduced = reducedMotion;
            _anchors = anchors;
        }

        public Cutscene Cutscene { get; }

        public bool ReducedMotion => _reduced;

        public CutscenePhase Phase { get; private set; } = CutscenePhase.Playing;

        public bool IsDone => Phase == CutscenePhase.Done;

        /// <summary>True while the beats play (not in the outro).</summary>
        public bool Playing => Phase == CutscenePhase.Playing;

        public bool WasSkipped { get; private set; }

        public int BeatIndex { get; private set; }

        public CutsceneBeat Beat => Phase == CutscenePhase.Playing ? Cutscene.Beats[BeatIndex] : null;

        /// <summary>Seconds into the current beat (holds at its length while it waits).</summary>
        public float BeatTime { get; private set; }

        /// <summary>Game seconds since the start, waits and outro included.</summary>
        public float Elapsed { get; private set; }

        /// <summary>True while the current beat holds at its end for its signal.</summary>
        public bool Waiting => Phase == CutscenePhase.Playing && BeatTime >= Beat.Duration && Beat.WaitFor != null;

        /// <summary>True when the last <see cref="Advance"/> crossed a camera cut (the runtime drops camera damping).</summary>
        public bool CutThisStep { get; private set; }

        /// <summary>0..1 through the nominal length (the skip disc's ring).</summary>
        public float Progress
        {
            get
            {
                if (Phase != CutscenePhase.Playing)
                {
                    return 1f;
                }

                float at = Cutscene.BeatStart(BeatIndex) + Math.Min(BeatTime, Beat.Duration);
                return Cutscene.Duration <= 0f ? 1f : Clamp01(at / Cutscene.Duration);
            }
        }

        /// <summary>The caption key of the current beat (none in the outro).</summary>
        public string CaptionKey => Beat?.CaptionKey;

        /// <summary>0..1: the letterbox bars and the cinematic overlay.</summary>
        public float Letterbox
        {
            get
            {
                switch (Phase)
                {
                    case CutscenePhase.Playing:
                        return _reduced ? 1f : Ease(Elapsed / LetterboxIn);
                    case CutscenePhase.Outro:
                        return _outroLength <= 0f ? 0f : _outroFrom * (1f - Ease(_outroTime / _outroLength));
                    default:
                        return 0f;
                }
            }
        }

        /// <summary>Seconds the outro lasts (the camera's blend back to play).</summary>
        public float OutroLength => _outroLength;

        /// <summary>Seconds until a page-turn beat starts (infinity when none is next): the runtime raises the page.</summary>
        public float SecondsToPageTurn
        {
            get
            {
                if (Phase != CutscenePhase.Playing || BeatIndex + 1 >= Cutscene.Beats.Count ||
                    Cutscene.Beats[BeatIndex + 1].Shot.Transition != ShotTransition.PageTurn)
                {
                    return float.PositiveInfinity;
                }

                return Math.Max(0f, Beat.Duration - BeatTime);
            }
        }

        /// <summary>The camera now.</summary>
        public CamPose Camera => Phase == CutscenePhase.Playing ? PoseAt(BeatIndex, BeatTime) : PoseAt(Cutscene.Beats.Count - 1, float.MaxValue);

        /// <summary>A wait signal arrived (e.g. "walk.pip": Pip reached the porch). Signals are remembered.</summary>
        public void Signal(string id)
        {
            if (id != null)
            {
                _signals.Add(id);
            }
        }

        public bool HasSignal(string id) => id != null && _signals.Contains(id);

        /// <summary>
        /// Advances by <paramref name="dt"/> seconds (0 while paused). The first call, even with 0, starts the first
        /// beat. Returns the events of this step in order (the list is reused by the next call).
        /// </summary>
        public IReadOnlyList<CutsceneEvent> Advance(float dt)
        {
            _events.Clear();
            CutThisStep = false;
            if (!_started)
            {
                _started = true;
                EnterBeat(0);
            }

            if (dt <= 0f || float.IsNaN(dt))
            {
                return _events;
            }

            int guard = 0;
            while (dt > 0f && Phase != CutscenePhase.Done && guard++ < 10000)
            {
                if (Phase == CutscenePhase.Outro)
                {
                    float step = Math.Min(dt, _outroLength - _outroTime);
                    _outroTime += step;
                    Elapsed += step;
                    dt -= step;
                    if (_outroTime >= _outroLength - Epsilon)
                    {
                        Finish();
                    }

                    continue;
                }

                var beat = Beat;
                float left = beat.Duration - BeatTime;
                if (left <= Epsilon && left > 0f)
                {
                    FireBetween(beat, BeatTime, beat.Duration);
                    BeatTime = beat.Duration;
                    left = 0f;
                }

                if (left > 0f)
                {
                    float step = Math.Min(dt, left);
                    float from = BeatTime;
                    float to = dt >= left ? beat.Duration : BeatTime + step;
                    FireBetween(beat, from, to);
                    if (beat.Shot.Transition == ShotTransition.PageTurn && BeatIndex > 0 && from < PageCutDelay && to >= PageCutDelay)
                    {
                        CutThisStep = true;
                    }

                    BeatTime = to;
                    Elapsed += step;
                    dt -= step;
                    continue;
                }

                if (beat.WaitFor != null && !HasSignal(beat.WaitFor) && _waited < beat.MaxWait - Epsilon)
                {
                    float step = Math.Min(dt, beat.MaxWait - _waited);
                    _waited += step;
                    Elapsed += step;
                    dt -= step;
                    continue;
                }

                if (BeatIndex + 1 < Cutscene.Beats.Count)
                {
                    EnterBeat(BeatIndex + 1);
                }
                else
                {
                    StartOutro(_reduced ? 0f : Cutscene.ExitBlend);
                }
            }

            // A beat that ended exactly on this step moves on now, so the next frame starts in the right beat.
            if (Phase == CutscenePhase.Playing && BeatTime >= Beat.Duration - Epsilon &&
                (Beat.WaitFor == null || HasSignal(Beat.WaitFor) || _waited >= Beat.MaxWait - Epsilon))
            {
                FireBetween(Beat, BeatTime, Beat.Duration);
                BeatTime = Beat.Duration;
                if (BeatIndex + 1 < Cutscene.Beats.Count)
                {
                    EnterBeat(BeatIndex + 1);
                }
                else
                {
                    StartOutro(_reduced ? 0f : Cutscene.ExitBlend);
                }
            }

            return _events;
        }

        /// <summary>
        /// Skip at any point (any control scheme): straight to the outro (a short blend back, a cut under reduced
        /// motion). Returns the events (the outro start, and the end when there is no blend). No-op once in the outro.
        /// </summary>
        public IReadOnlyList<CutsceneEvent> Skip()
        {
            _events.Clear();
            CutThisStep = false;
            if (Phase != CutscenePhase.Playing)
            {
                return _events;
            }

            _started = true;
            WasSkipped = true;
            StartOutro(_reduced ? 0f : SkipExitBlend);
            return _events;
        }

        private void EnterBeat(int index)
        {
            bool first = index == 0;
            BeatIndex = index;
            BeatTime = 0f;
            _waited = 0f;
            var beat = Cutscene.Beats[index];
            if (!first && !BlendsFromPrevious(index) && beat.Shot.Transition != ShotTransition.PageTurn)
            {
                CutThisStep = true;
            }

            _events.Add(new CutsceneEvent(CutsceneEventKind.BeatStarted, index));
            FireBetween(beat, float.NegativeInfinity, 0f);
        }

        private void StartOutro(float length)
        {
            _outroFrom = Letterbox;
            Phase = CutscenePhase.Outro;
            _outroTime = 0f;
            _outroLength = length;
            _events.Add(new CutsceneEvent(CutsceneEventKind.OutroStarted, BeatIndex));
            if (length <= 0f)
            {
                Finish();
            }
        }

        private void Finish()
        {
            Phase = CutscenePhase.Done;
            _events.Add(new CutsceneEvent(CutsceneEventKind.Ended, BeatIndex));
        }

        /// <summary>Fires the cues with from &lt; At ≤ to (cast before signals at the same time; data order within each).</summary>
        private void FireBetween(CutsceneBeat beat, float from, float to)
        {
            foreach (var cue in beat.Cast)
            {
                if (cue.At > from && cue.At <= to)
                {
                    _events.Add(new CutsceneEvent(CutsceneEventKind.Cast, BeatIndex, cast: cue));
                }
            }

            foreach (var cue in beat.Signals)
            {
                if (cue.At > from && cue.At <= to)
                {
                    _events.Add(new CutsceneEvent(CutsceneEventKind.Signal, BeatIndex, signal: cue));
                }
            }
        }

        /// <summary>True when beat <paramref name="index"/> eases from the previous shot (not a cut, same space, motion on).</summary>
        public bool BlendsFromPrevious(int index)
        {
            if (index <= 0 || index >= Cutscene.Beats.Count || _reduced)
            {
                return false;
            }

            var shot = Cutscene.Beats[index].Shot;
            if (shot.Transition != ShotTransition.Blend || shot.BlendSeconds <= 0f)
            {
                return false;
            }

            var from = PoseAt(index - 1, float.MaxValue).Eye;
            var to = TargetPose(index, 0f).Eye;
            return CaseRouteLayout.Inside(from.X, from.Z) == CaseRouteLayout.Inside(to.X, to.Z);
        }

        /// <summary>The camera at <paramref name="time"/> seconds into beat <paramref name="index"/> (clamped to the beat).</summary>
        public CamPose PoseAt(int index, float time)
        {
            var beat = Cutscene.Beats[index];
            float t = Math.Max(0f, Math.Min(time, beat.Duration));
            var target = TargetPose(index, t);
            if (index == 0)
            {
                return target;
            }

            switch (beat.Shot.Transition)
            {
                case ShotTransition.PageTurn:
                    return t < PageCutDelay ? PoseAt(index - 1, float.MaxValue) : target;
                case ShotTransition.Blend:
                    if (BlendsFromPrevious(index) && t < beat.Shot.BlendSeconds)
                    {
                        return CamPose.Lerp(PoseAt(index - 1, float.MaxValue), target, Ease(t / beat.Shot.BlendSeconds));
                    }

                    return target;
                default:
                    return target;
            }
        }

        /// <summary>The shot's own framing at time <paramref name="t"/> (its drift), before any blend from the last shot.</summary>
        private CamPose TargetPose(int index, float t)
        {
            var beat = Cutscene.Beats[index];
            var shot = beat.Shot;
            var pose = !shot.Moves ? shot.Start
                : _reduced ? shot.End
                : CamPose.Lerp(shot.Start, shot.End, Ease(t / beat.Duration));
            if (shot.Anchor == null)
            {
                return pose;
            }

            var frame = _anchors?.Invoke(shot.Anchor) ?? CutsceneAnchors.Default(shot.Anchor);
            return new CamPose(frame.Apply(pose.Eye), frame.Apply(pose.Aim), pose.Fov);
        }

        /// <summary>Smoothstep ease in and out on 0..1.</summary>
        public static float Ease(float u)
        {
            u = Clamp01(u);
            return u * u * (3f - 2f * u);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
