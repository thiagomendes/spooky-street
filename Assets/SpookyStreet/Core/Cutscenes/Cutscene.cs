using System;
using System.Collections.Generic;
using SpookyStreet.Core.Case;

namespace SpookyStreet.Core.Cutscenes
{
    /// <summary>A camera pose: the eye, the point it looks at and the vertical field of view (degrees).</summary>
    public readonly struct CamPose
    {
        public CamPose(StagePoint eye, StagePoint aim, float fov)
        {
            Eye = eye;
            Aim = aim;
            Fov = fov;
        }

        public StagePoint Eye { get; }
        public StagePoint Aim { get; }
        public float Fov { get; }

        public static CamPose Lerp(CamPose a, CamPose b, float t) =>
            new CamPose(Lerp(a.Eye, b.Eye, t), Lerp(a.Aim, b.Aim, t), a.Fov + (b.Fov - a.Fov) * t);

        public static StagePoint Lerp(StagePoint a, StagePoint b, float t) =>
            new StagePoint(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);

        public override string ToString() => $"eye {Eye} aim {Aim} fov {Fov:F1}";
    }

    /// <summary>How a shot follows the one before it.</summary>
    public enum ShotTransition
    {
        /// <summary>Eased camera move from the last pose (only inside one space: the street or the interior).</summary>
        Blend,

        /// <summary>A hard cut (always used under reduced motion and between spaces).</summary>
        Cut,

        /// <summary>The paper page-turn of the thresholds (D24.1) covers a cut between places.</summary>
        PageTurn,
    }

    /// <summary>
    /// A place a shot can be framed from at run time (a model part whose exact position the runtime knows better
    /// than the data). Offsets are in the anchor's own frame: x to its right, y up, z out of its face.
    /// </summary>
    public readonly struct AnchorFrame
    {
        public AnchorFrame(StagePoint position, float facingYaw)
        {
            Position = position;
            FacingYaw = facingYaw;
        }

        public StagePoint Position { get; }

        /// <summary>The direction the anchor faces (Unity yaw, degrees: 0 = +Z, 90 = +X).</summary>
        public float FacingYaw { get; }

        public StagePoint Apply(StagePoint offset)
        {
            double r = FacingYaw * Math.PI / 180.0;
            float fx = (float)Math.Sin(r);
            float fz = (float)Math.Cos(r);
            // Right of a facing (fx, fz) seen from behind it is (fz, -fx).
            return new StagePoint(Position.X + offset.X * fz + offset.Z * fx, Position.Y + offset.Y,
                Position.Z - offset.X * fx + offset.Z * fz);
        }
    }

    /// <summary>
    /// One camera shot: where it starts (eye, aim, field of view), optionally where it drifts to over the beat (a slow
    /// push or pan; reduced motion holds the end framing), and how it follows the previous shot.
    /// </summary>
    public sealed class CameraShot
    {
        public CameraShot(StagePoint eye, StagePoint aim, float fov, ShotTransition transition = ShotTransition.Cut,
            float blendSeconds = 0f)
        {
            if (fov <= 1f || fov >= 120f)
            {
                throw new ArgumentOutOfRangeException(nameof(fov), fov, "field of view must be 1–120 degrees");
            }

            if (blendSeconds < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(blendSeconds), blendSeconds, "blend cannot be negative");
            }

            Start = new CamPose(eye, aim, fov);
            End = Start;
            Transition = transition;
            BlendSeconds = transition == ShotTransition.Blend ? blendSeconds : 0f;
        }

        public CamPose Start { get; private set; }

        public CamPose End { get; private set; }

        public bool Moves => !Equals(Start.Eye, End.Eye) || !Equals(Start.Aim, End.Aim) || Math.Abs(Start.Fov - End.Fov) > 1e-4f;

        public ShotTransition Transition { get; }

        public float BlendSeconds { get; }

        /// <summary>When set, the eyes and aims are offsets in this anchor's frame (<see cref="CutsceneAnchors"/>).</summary>
        public string Anchor { get; private set; }

        /// <summary>The shot drifts to this framing over its beat (eased). Returns this shot.</summary>
        public CameraShot DriftTo(StagePoint eye, StagePoint aim, float fov)
        {
            End = new CamPose(eye, aim, fov);
            return this;
        }

        /// <summary>Frames the shot from <paramref name="anchorId"/> (offsets in its frame). Returns this shot.</summary>
        public CameraShot From(string anchorId)
        {
            Anchor = anchorId;
            return this;
        }

        private static bool Equals(StagePoint a, StagePoint b) =>
            Math.Abs(a.X - b.X) < 1e-4f && Math.Abs(a.Y - b.Y) < 1e-4f && Math.Abs(a.Z - b.Z) < 1e-4f;
    }

    /// <summary>The people a cutscene can stage (the owl and the machine move through world cues).</summary>
    public enum CastActor
    {
        Pip,
        June,
        Kit,
        Bea,
        Vale,
    }

    public enum CastCueKind
    {
        /// <summary>Stand at a point facing a yaw (degrees).</summary>
        Place,

        /// <summary>Walk a path (the first point is where the walk starts) at a pace in m/s, with the walk clip.</summary>
        WalkTo,

        /// <summary>Hold a looping clip.</summary>
        Loop,

        /// <summary>Play a clip once over the loop.</summary>
        OneShot,

        /// <summary>A gesture clip, turning toward a point while it plays (<c>Witness.PlayGesture</c>).</summary>
        Gesture,

        /// <summary>Turn the head toward a point for some seconds (<c>Witness.LookAt</c>).</summary>
        LookAt,

        Hide,
        Show,
    }

    /// <summary>A character cue at a time inside its beat. Only existing clips are used (no hand-keyed animation).</summary>
    public sealed class CastCue
    {
        private CastCue(float at, CastActor actor, CastCueKind kind)
        {
            if (at < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(at), at, "a cue cannot start before its beat");
            }

            At = at;
            Actor = actor;
            Kind = kind;
            Path = Array.Empty<StagePoint>();
        }

        public float At { get; }
        public CastActor Actor { get; }
        public CastCueKind Kind { get; }

        /// <summary>Place: where; Gesture and LookAt: the point to turn to.</summary>
        public StagePoint Point { get; private set; }

        /// <summary>Place: the facing yaw (degrees).</summary>
        public float Yaw { get; private set; }

        public string Clip { get; private set; }

        /// <summary>WalkTo: the path, start first.</summary>
        public IReadOnlyList<StagePoint> Path { get; private set; }

        /// <summary>WalkTo: metres per second.</summary>
        public float Speed { get; private set; }

        /// <summary>LookAt: how long the head turn holds.</summary>
        public float Seconds { get; private set; }

        /// <summary>WalkTo: the walk's length in metres.</summary>
        public float PathLength
        {
            get
            {
                float total = 0f;
                for (int i = 0; i + 1 < Path.Count; i++)
                {
                    float dx = Path[i + 1].X - Path[i].X;
                    float dy = Path[i + 1].Y - Path[i].Y;
                    float dz = Path[i + 1].Z - Path[i].Z;
                    total += (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
                }

                return total;
            }
        }

        /// <summary>WalkTo: seconds the walk takes.</summary>
        public float WalkSeconds => Speed > 0f ? PathLength / Speed : 0f;

        /// <summary>Where the actor stands once this cue has finished (Place and WalkTo), or null.</summary>
        public StagePoint? FinalPoint =>
            Kind == CastCueKind.Place ? Point : Kind == CastCueKind.WalkTo && Path.Count > 0 ? Path[Path.Count - 1] : (StagePoint?)null;

        public static CastCue Place(float at, CastActor actor, StagePoint point, float yaw) =>
            new CastCue(at, actor, CastCueKind.Place) { Point = point, Yaw = yaw };

        public static CastCue WalkTo(float at, CastActor actor, float speed, params StagePoint[] path)
        {
            if (path == null || path.Length < 2)
            {
                throw new ArgumentException("a walk needs a start and an end", nameof(path));
            }

            if (speed <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(speed), speed, "a walk needs a pace");
            }

            return new CastCue(at, actor, CastCueKind.WalkTo) { Path = path, Speed = speed };
        }

        public static CastCue Loop(float at, CastActor actor, string clip) =>
            new CastCue(at, actor, CastCueKind.Loop) { Clip = clip ?? throw new ArgumentNullException(nameof(clip)) };

        public static CastCue OneShot(float at, CastActor actor, string clip) =>
            new CastCue(at, actor, CastCueKind.OneShot) { Clip = clip ?? throw new ArgumentNullException(nameof(clip)) };

        public static CastCue Gesture(float at, CastActor actor, string clip, StagePoint aim) =>
            new CastCue(at, actor, CastCueKind.Gesture) { Clip = clip ?? throw new ArgumentNullException(nameof(clip)), Point = aim };

        public static CastCue LookAt(float at, CastActor actor, StagePoint point, float seconds) =>
            new CastCue(at, actor, CastCueKind.LookAt) { Point = point, Seconds = seconds };

        public static CastCue Hide(float at, CastActor actor) => new CastCue(at, actor, CastCueKind.Hide);

        public static CastCue Show(float at, CastActor actor) => new CastCue(at, actor, CastCueKind.Show);

        public override string ToString() => $"{At:F2}s {Actor} {Kind} {Clip}";
    }

    /// <summary>What a signal cue drives: a babble voice and the music (placeholders until S003 audio) or a world effect.</summary>
    public enum CueChannel
    {
        Babble,
        Music,
        World,
    }

    /// <summary>A babble, music or world cue at a time inside its beat (IDs are stable data; audio arrives with S003).</summary>
    public sealed class SignalCue
    {
        public SignalCue(float at, CueChannel channel, string id)
        {
            if (at < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(at), at, "a cue cannot start before its beat");
            }

            At = at;
            Channel = channel;
            Id = string.IsNullOrEmpty(id) ? throw new ArgumentException("a cue needs an ID", nameof(id)) : id;
        }

        public float At { get; }
        public CueChannel Channel { get; }
        public string Id { get; }

        public override string ToString() => $"{At:F2}s {Channel} {Id}";
    }

    /// <summary>
    /// One beat of a cutscene: one idea, a few seconds (narrative bible §5). It holds the shot, the caption key (the
    /// hybrid text line, D23.1), an optional comic vignette insert, an optional sticker the child placed (the reveal),
    /// the cast and signal cues, and an optional wait: at its end the beat holds until a signal arrives (or a
    /// maximum wait passes).
    /// </summary>
    public sealed class CutsceneBeat
    {
        private readonly List<CastCue> _cast = new List<CastCue>();
        private readonly List<SignalCue> _signals = new List<SignalCue>();

        public CutsceneBeat(string id, float duration, CameraShot shot)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("a beat needs an ID", nameof(id));
            }

            if (duration <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(duration), duration, "a beat needs a length");
            }

            Id = id;
            Duration = duration;
            Shot = shot ?? throw new ArgumentNullException(nameof(shot));
        }

        public string Id { get; }

        public float Duration { get; }

        public CameraShot Shot { get; }

        /// <summary>The text line under the picture (a localization key), or null.</summary>
        public string CaptionKey { get; private set; }

        /// <summary>A comic vignette played as an insert (<c>C01Vignettes</c> ID), or null.</summary>
        public string VignetteId { get; private set; }

        /// <summary>The child's own clue sticker this shot is about (the reveal), or null.</summary>
        public string StickerId { get; private set; }

        public IReadOnlyList<CastCue> Cast => _cast;

        public IReadOnlyList<SignalCue> Signals => _signals;

        /// <summary>A signal the beat waits for at its end (e.g. a walk arriving), or null.</summary>
        public string WaitFor { get; private set; }

        /// <summary>The longest the beat waits for <see cref="WaitFor"/> past its length.</summary>
        public float MaxWait { get; private set; }

        public CutsceneBeat Caption(string key)
        {
            CaptionKey = key;
            return this;
        }

        public CutsceneBeat Vignette(string vignetteId)
        {
            VignetteId = vignetteId;
            return this;
        }

        public CutsceneBeat Sticker(string stickerId)
        {
            StickerId = stickerId;
            return this;
        }

        public CutsceneBeat Cue(CastCue cue)
        {
            _cast.Add(cue ?? throw new ArgumentNullException(nameof(cue)));
            return this;
        }

        public CutsceneBeat Cue(SignalCue cue)
        {
            _signals.Add(cue ?? throw new ArgumentNullException(nameof(cue)));
            return this;
        }

        public CutsceneBeat Babble(float at, string id) => Cue(new SignalCue(at, CueChannel.Babble, id));

        public CutsceneBeat Music(float at, string id) => Cue(new SignalCue(at, CueChannel.Music, id));

        public CutsceneBeat World(float at, string id) => Cue(new SignalCue(at, CueChannel.World, id));

        public CutsceneBeat Wait(string signal, float maxWait)
        {
            if (maxWait <= 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(maxWait), maxWait, "a wait needs a limit");
            }

            WaitFor = signal ?? throw new ArgumentNullException(nameof(signal));
            MaxWait = maxWait;
            return this;
        }

        public override string ToString() => $"{Id} ({Duration:F1}s)";
    }

    /// <summary>
    /// A short authored story moment (D28, narrative bible §5): beats played in order, then an outro that hands the
    /// camera back to play. <see cref="Settle"/> holds the placements that must be true when it ends, however it ends
    /// (played through or skipped), so a skip never leaves anyone halfway.
    /// </summary>
    public sealed class Cutscene
    {
        public const float MinSeconds = 10f;
        public const float MaxSeconds = 40f;

        public Cutscene(string id, IReadOnlyList<CutsceneBeat> beats, float exitBlend, IReadOnlyList<CastCue> settle = null,
            bool hidePip = false)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("a cutscene needs a stable ID", nameof(id));
            }

            if (beats == null || beats.Count == 0)
            {
                throw new ArgumentException("a cutscene needs beats", nameof(beats));
            }

            if (exitBlend < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(exitBlend), exitBlend, "blend cannot be negative");
            }

            for (int i = 0; i < beats.Count; i++)
            {
                var beat = beats[i] ?? throw new ArgumentException("null beat", nameof(beats));
                foreach (var cue in beat.Cast)
                {
                    if (cue.At > beat.Duration)
                    {
                        throw new ArgumentException($"{beat}: cue {cue} starts after the beat");
                    }
                }

                foreach (var cue in beat.Signals)
                {
                    if (cue.At > beat.Duration)
                    {
                        throw new ArgumentException($"{beat}: cue {cue} starts after the beat");
                    }
                }

                if (beat.WaitFor != null && i + 1 < beats.Count && beats[i + 1].Shot.Transition == ShotTransition.PageTurn)
                {
                    // The page starts to cover before the boundary; a wait would move the boundary under it.
                    throw new ArgumentException($"{beat}: a waiting beat cannot lead into a page-turn");
                }
            }

            Id = id;
            Beats = beats;
            ExitBlend = exitBlend;
            Settle = settle ?? Array.Empty<CastCue>();
            HidePip = hidePip;
            float total = 0f;
            _starts = new float[beats.Count];
            for (int i = 0; i < beats.Count; i++)
            {
                _starts[i] = total;
                total += beats[i].Duration;
            }

            Duration = total;
        }

        private readonly float[] _starts;

        /// <summary>Stable saved ID (the seen set), e.g. "cs.c01.opening". Never display text.</summary>
        public string Id { get; }

        public IReadOnlyList<CutsceneBeat> Beats { get; }

        /// <summary>Seconds the camera takes to blend back to play after the last beat (0 = cut).</summary>
        public float ExitBlend { get; }

        /// <summary>Placements applied when it ends, played through or skipped (Place cues; their times are ignored).</summary>
        public IReadOnlyList<CastCue> Settle { get; }

        /// <summary>Pip stays out of frame while it plays (the reveal: Pip may stand anywhere).</summary>
        public bool HidePip { get; }

        /// <summary>Nominal length: the beats without waits or the outro.</summary>
        public float Duration { get; }

        public float BeatStart(int index) => _starts[index];

        /// <summary>The beats whose sticker the child placed, in order.</summary>
        public IReadOnlyList<string> Stickers
        {
            get
            {
                var list = new List<string>();
                foreach (var beat in Beats)
                {
                    if (beat.StickerId != null)
                    {
                        list.Add(beat.StickerId);
                    }
                }

                return list;
            }
        }

        /// <summary>Every caption key, in beat order.</summary>
        public IReadOnlyList<string> CaptionKeys
        {
            get
            {
                var list = new List<string>();
                foreach (var beat in Beats)
                {
                    if (beat.CaptionKey != null)
                    {
                        list.Add(beat.CaptionKey);
                    }
                }

                return list;
            }
        }

        public override string ToString() => $"{Id} ({Beats.Count} beats, {Duration:F1}s)";
    }
}
