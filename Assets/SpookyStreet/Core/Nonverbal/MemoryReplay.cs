using System;
using System.Collections.Generic;
using SpookyStreet.Core.Case;
using Lines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;

namespace SpookyStreet.Core.Nonverbal
{
    /// <summary>The real place a memory replay frames (camera pose data lives in the runtime, per place).</summary>
    public enum ReplayPlace
    {
        /// <summary>Treehouse balcony → side window, from the plaza side (Bea's account).</summary>
        TreehouseWindow,

        /// <summary>The front door, from Vale's bench.</summary>
        FrontDoor,
    }

    /// <summary>One in-world memory replay (docs/nonverbal-ux.md §4).</summary>
    public sealed class MemoryReplayDefinition
    {
        internal MemoryReplayDefinition(string id, string lineKey, ReplayPlace place, string stickerEntryId, CaseFact? heardFact)
        {
            Id = id;
            LineKey = lineKey;
            Place = place;
            StickerEntryId = stickerEntryId;
            HeardFact = heardFact;
        }

        /// <summary>Stable saved ID, e.g. "mr.bea.saw_cape". Never display text.</summary>
        public string Id { get; }

        /// <summary>The key evidence line that triggers it.</summary>
        public string LineKey { get; }

        public ReplayPlace Place { get; }

        /// <summary>The notebook clue sticker that offers it on demand (the eye icon).</summary>
        public string StickerEntryId { get; }

        /// <summary>
        /// A case fact that proves the line was heard, when one exists (the first-hearing lines gain one). Lets saves
        /// from before this feature still offer the replay in the notebook. Null for lines that repeat.
        /// </summary>
        public CaseFact? HeardFact { get; }
    }

    /// <summary>The four key evidence replays of case 01 (owner decision D23.2).</summary>
    public static class MemoryReplays
    {
        public const string BeaSawCape = "mr.bea.saw_cape";
        public const string BeaDoorOpen = "mr.bea.door_open";
        public const string ValeDoorShut = "mr.vale.door_shut";
        public const string ValeWasShut = "mr.vale.was_shut";

        public static readonly IReadOnlyList<MemoryReplayDefinition> All = new[]
        {
            new MemoryReplayDefinition(BeaSawCape, Lines.BeaSawCape, ReplayPlace.TreehouseWindow, "c01.note.bea", CaseFact.BeaSpoke),
            new MemoryReplayDefinition(BeaDoorOpen, Lines.BeaDoorOpen, ReplayPlace.TreehouseWindow, "c01.note.bea", null),
            new MemoryReplayDefinition(ValeDoorShut, Lines.ValeDoorShut, ReplayPlace.FrontDoor, "c01.note.vale", CaseFact.ValeSpoke),
            new MemoryReplayDefinition(ValeWasShut, Lines.ValeWasShut, ReplayPlace.FrontDoor, "c01.note.vale", null),
        };

        public static MemoryReplayDefinition ForLine(string lineKey)
        {
            foreach (var replay in All)
            {
                if (string.Equals(replay.LineKey, lineKey, StringComparison.Ordinal))
                {
                    return replay;
                }
            }

            return null;
        }

        public static MemoryReplayDefinition Get(string id)
        {
            foreach (var replay in All)
            {
                if (string.Equals(replay.Id, id, StringComparison.Ordinal))
                {
                    return replay;
                }
            }

            return null;
        }

        public static bool IsKnown(string id) => Get(id) != null;
    }

    public enum ReplayDecisionKind
    {
        /// <summary>Play it now (first hearing in this save).</summary>
        Play,

        /// <summary>The line has no memory replay.</summary>
        NotKeyLine,

        /// <summary>Already offered in this save: notebook only.</summary>
        AlreadySeen,

        /// <summary>Pip is airborne or in a hazard: dropped (counts as offered; the notebook can play it).</summary>
        DroppedUnsafe,
    }

    /// <summary>What to do when a key line's bubble is dismissed.</summary>
    public readonly struct ReplayDecision
    {
        internal ReplayDecision(ReplayDecisionKind kind, MemoryReplayDefinition replay)
        {
            Kind = kind;
            Replay = replay;
        }

        public ReplayDecisionKind Kind { get; }

        public MemoryReplayDefinition Replay { get; }

        public bool Play => Kind == ReplayDecisionKind.Play;

        /// <summary>Record the replay ID as seen now (before playing: a skip, crash or quit must not replay it).</summary>
        public bool MarkSeen => Kind == ReplayDecisionKind.Play || Kind == ReplayDecisionKind.DroppedUnsafe;
    }

    /// <summary>
    /// When memory replays play (owner decision D23.2): automatically the first time each key line is heard in a
    /// save, never again after that except on demand from the notebook. Pure over a set of seen replay IDs. The
    /// replay never gates progress: the clue (fact) is recorded by the line itself, whatever happens to the replay.
    /// </summary>
    public static class MemoryReplayPolicy
    {
        /// <summary>
        /// Call after the key line's vignette has played once and the player tapped next (never mid-line).
        /// </summary>
        public static ReplayDecision OnLineDismissed(string lineKey, IReadOnlyCollection<string> seen, bool pipAirborne, bool pipInHazard)
        {
            var replay = MemoryReplays.ForLine(lineKey);
            if (replay == null)
            {
                return new ReplayDecision(ReplayDecisionKind.NotKeyLine, null);
            }

            if (seen != null && Contains(seen, replay.Id))
            {
                return new ReplayDecision(ReplayDecisionKind.AlreadySeen, replay);
            }

            if (pipAirborne || pipInHazard)
            {
                return new ReplayDecision(ReplayDecisionKind.DroppedUnsafe, replay);
            }

            return new ReplayDecision(ReplayDecisionKind.Play, replay);
        }

        /// <summary>The notebook can replay it: it was offered in this save, or its heard-fact proves the line played.</summary>
        public static bool AvailableOnDemand(MemoryReplayDefinition replay, CaseState state, IReadOnlyCollection<string> seen)
        {
            if (replay == null)
            {
                return false;
            }

            if (seen != null && Contains(seen, replay.Id))
            {
                return true;
            }

            return replay.HeardFact.HasValue && state != null && state.Has(replay.HeardFact.Value);
        }

        /// <summary>Replays the sticker <paramref name="stickerEntryId"/> offers right now, in script order.</summary>
        public static IReadOnlyList<MemoryReplayDefinition> OnDemandFor(string stickerEntryId, CaseState state, IReadOnlyCollection<string> seen)
        {
            var list = new List<MemoryReplayDefinition>(2);
            foreach (var replay in MemoryReplays.All)
            {
                if (string.Equals(replay.StickerEntryId, stickerEntryId, StringComparison.Ordinal) &&
                    AvailableOnDemand(replay, state, seen))
                {
                    list.Add(replay);
                }
            }

            return list;
        }

        private static bool Contains(IReadOnlyCollection<string> seen, string id)
        {
            if (seen is ICollection<string> collection)
            {
                return collection.Contains(id);
            }

            foreach (string s in seen)
            {
                if (string.Equals(s, id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Camera timing of a replay (§4): blend in, hold, blend back. Total at most 2.7 s.</summary>
    public readonly struct MemoryReplayTiming
    {
        public const float DefaultBlend = 0.35f;
        public const float MinHold = 1.5f;
        public const float MaxHold = 2.0f;
        public const float DefaultHold = 1.8f;

        public MemoryReplayTiming(float blendIn, float hold, float blendOut)
        {
            if (blendIn < 0f || blendOut < 0f)
            {
                throw new ArgumentOutOfRangeException(nameof(blendIn), "blends cannot be negative");
            }

            if (hold < MinHold || hold > MaxHold)
            {
                throw new ArgumentOutOfRangeException(nameof(hold), hold, "hold must be 1.5–2 s");
            }

            BlendIn = blendIn;
            Hold = hold;
            BlendOut = blendOut;
        }

        public static MemoryReplayTiming Default => new MemoryReplayTiming(DefaultBlend, DefaultHold, DefaultBlend);

        public float BlendIn { get; }

        public float Hold { get; }

        public float BlendOut { get; }

        public float Total => BlendIn + Hold + BlendOut;
    }

    public enum ReplayPhase
    {
        BlendIn,
        Hold,
        BlendOut,
        Done,
    }

    /// <summary>
    /// One playing replay: phase, blend weight (0 = gameplay camera, 1 = memory camera and full memory look) and
    /// ghost progress. Skippable at any time: it blends back at once from wherever it is. Reduced motion cuts
    /// instead of blending. Time scale is never touched.
    /// </summary>
    public sealed class MemoryReplayPlayback
    {
        private readonly bool _reducedMotion;
        private float _outFrom = 1f;
        private float _outDuration;
        private float _phaseTime;
        private float _holdTime;

        public MemoryReplayPlayback(MemoryReplayDefinition replay, MemoryReplayTiming timing, bool reducedMotion)
        {
            Replay = replay ?? throw new ArgumentNullException(nameof(replay));
            Timing = timing;
            _reducedMotion = reducedMotion;
            _outDuration = timing.BlendOut;
            Phase = reducedMotion || timing.BlendIn <= 0f ? ReplayPhase.Hold : ReplayPhase.BlendIn;
        }

        public MemoryReplayDefinition Replay { get; }

        public MemoryReplayTiming Timing { get; }

        public ReplayPhase Phase { get; private set; }

        public bool IsDone => Phase == ReplayPhase.Done;

        public bool WasSkipped { get; private set; }

        /// <summary>Eased 0..1: drives the camera blend, letterbox bars and paper tint.</summary>
        public float Weight
        {
            get
            {
                switch (Phase)
                {
                    case ReplayPhase.BlendIn:
                        return VignetteSampler.Apply(Ease.InOut, _phaseTime / Timing.BlendIn);
                    case ReplayPhase.Hold:
                        return 1f;
                    case ReplayPhase.BlendOut:
                        return _outDuration <= 0f ? 0f : _outFrom * (1f - VignetteSampler.Apply(Ease.InOut, _phaseTime / _outDuration));
                    default:
                        return 0f;
                }
            }
        }

        /// <summary>0..1 through the hold: the ghost's re-enactment. Reduced motion shows the end pose.</summary>
        public float GhostProgress => _reducedMotion ? 1f : Math.Min(1f, _holdTime / Timing.Hold);

        public void Advance(float dt)
        {
            if (dt <= 0f)
            {
                return;
            }

            while (dt > 0f && Phase != ReplayPhase.Done)
            {
                float length = PhaseLength();
                float left = length - _phaseTime;
                float step = Math.Min(dt, left);
                _phaseTime = dt >= left ? length : _phaseTime + step;
                if (Phase == ReplayPhase.Hold)
                {
                    _holdTime += step;
                }

                dt -= step;
                if (_phaseTime >= length)
                {
                    NextPhase();
                }
            }
        }

        /// <summary>Tap anywhere or the skip disc: blend back now. The clue was already recorded by the line.</summary>
        public void Skip()
        {
            if (Phase == ReplayPhase.Done || (Phase == ReplayPhase.BlendOut && WasSkipped))
            {
                return;
            }

            WasSkipped = true;
            float from = Weight;
            Phase = ReplayPhase.BlendOut;
            _phaseTime = 0f;
            _outFrom = from;
            _outDuration = _reducedMotion ? 0f : Timing.BlendOut * from;
            if (_outDuration <= 0f)
            {
                Phase = ReplayPhase.Done;
            }
        }

        private float PhaseLength()
        {
            switch (Phase)
            {
                case ReplayPhase.BlendIn:
                    return Timing.BlendIn;
                case ReplayPhase.Hold:
                    return Timing.Hold;
                case ReplayPhase.BlendOut:
                    return _outDuration;
                default:
                    return 0f;
            }
        }

        private void NextPhase()
        {
            _phaseTime = 0f;
            switch (Phase)
            {
                case ReplayPhase.BlendIn:
                    Phase = ReplayPhase.Hold;
                    break;
                case ReplayPhase.Hold:
                    _outFrom = 1f;
                    _outDuration = _reducedMotion ? 0f : Timing.BlendOut;
                    Phase = _outDuration > 0f ? ReplayPhase.BlendOut : ReplayPhase.Done;
                    break;
                default:
                    Phase = ReplayPhase.Done;
                    break;
            }
        }
    }
}
