using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.Case
{
    /// <summary>Who a line belongs to. <see cref="Caption"/> is an environmental caption (spec §6 captions).</summary>
    public enum Speaker
    {
        Caption,
        Pip,
        Bea,
        Vale,
        June,
        Kit,
    }

    /// <summary>
    /// Everything the world can report to the case. Interactions come from Use targets; the Enter/Hear/Arrive kinds
    /// come from trigger volumes. The reducer decides what each means; the world never sets facts directly.
    /// </summary>
    public enum CaseActionKind
    {
        /// <summary>Arrival cue trigger (also fired when Pip walks straight past it).</summary>
        HearKnocks,
        Talk,
        SitOnBench,
        ExamineStreamer,
        ExamineCloth,
        EnterAttic,
        ExamineSill,
        /// <summary>Use on the front door from the porch side.</summary>
        TryFrontDoor,
        /// <summary>Use on the wooden latch, from inside.</summary>
        LiftLatch,
        EnterFoyer,
        ExamineGrille,
        LinkArm,
        TurnCrank,
        /// <summary>Pip reaches the arrival porch sightline.</summary>
        ArriveAtPorch,
        ExamineCandyBowl,
    }

    public readonly struct CaseAction : IEquatable<CaseAction>
    {
        private CaseAction(CaseActionKind kind, Speaker speaker)
        {
            Kind = kind;
            Speaker = speaker;
        }

        public CaseActionKind Kind { get; }

        /// <summary>Only meaningful for <see cref="CaseActionKind.Talk"/>.</summary>
        public Speaker Speaker { get; }

        public static CaseAction Of(CaseActionKind kind)
        {
            if (kind == CaseActionKind.Talk)
            {
                throw new ArgumentException("Use CaseAction.Talk(speaker) for conversations.", nameof(kind));
            }

            return new CaseAction(kind, Speaker.Caption);
        }

        public static CaseAction Talk(Speaker speaker)
        {
            if (speaker == Speaker.Caption || speaker == Speaker.Pip)
            {
                throw new ArgumentException("Pip talks to the cast, not to a caption or to Pip.", nameof(speaker));
            }

            return new CaseAction(CaseActionKind.Talk, speaker);
        }

        public bool Equals(CaseAction other) => Kind == other.Kind && Speaker == other.Speaker;

        public override bool Equals(object obj) => obj is CaseAction other && Equals(other);

        public override int GetHashCode() => ((int)Kind * 31) + (int)Speaker;

        public override string ToString() => Kind == CaseActionKind.Talk ? "Talk(" + Speaker + ")" : Kind.ToString();
    }

    /// <summary>What happened, beyond the facts gained, so the world can react with animation and sound.</summary>
    public enum CaseOutcome
    {
        /// <summary>Nothing to react to (e.g. a trigger that no longer matters).</summary>
        None,

        /// <summary>At least one fact was gained.</summary>
        Progressed,

        /// <summary>A repeat: lines may play, but no persistent state changed.</summary>
        Repeated,

        /// <summary>Front door used from outside while still latched: "It's stuck on the inside."</summary>
        DoorStuck,

        /// <summary>Crank turned before the arm is linked: one tap and a visible gap, no fact (spec §3).</summary>
        CrankGap,

        /// <summary>Crank turned after the owl was tested: the puppet show repeats.</summary>
        ShowReplayed,

        /// <summary>The ending's conditions are not met yet; nothing changes.</summary>
        NotYet,

        /// <summary>The character is not present in this state (e.g. Aunt Kit before the reopening).</summary>
        Absent,
    }

    /// <summary>One line to show: a localization key and who says it.</summary>
    public readonly struct ScriptLine : IEquatable<ScriptLine>
    {
        public ScriptLine(Speaker speaker, string key)
        {
            Speaker = speaker;
            Key = key;
        }

        public Speaker Speaker { get; }

        /// <summary>Localization key into the string table.</summary>
        public string Key { get; }

        public bool Equals(ScriptLine other) => Speaker == other.Speaker && Key == other.Key;

        public override bool Equals(object obj) => obj is ScriptLine other && Equals(other);

        public override int GetHashCode() => ((int)Speaker * 397) ^ (Key?.GetHashCode() ?? 0);

        public override string ToString() => Speaker + ": " + Key;
    }

    /// <summary>The result of <see cref="CaseState.Apply"/>.</summary>
    public sealed class CaseStep
    {
        internal CaseStep(CaseState before, CaseState after, IReadOnlyList<CaseFact> gained, CaseOutcome outcome,
            IReadOnlyList<ScriptLine> lines, bool playCutaway)
        {
            Before = before;
            State = after;
            Gained = gained;
            Outcome = outcome;
            Lines = lines;
            PlayCutaway = playCutaway;
        }

        public CaseState Before { get; }

        /// <summary>The new state (the same instance as <see cref="Before"/> when nothing was gained).</summary>
        public CaseState State { get; }

        /// <summary>Facts gained by this action, in the order they were awarded. Empty on any repeat.</summary>
        public IReadOnlyList<CaseFact> Gained { get; }

        public CaseOutcome Outcome { get; }

        /// <summary>Lines to play in order (captions, examine text, dialogue). May be empty.</summary>
        public IReadOnlyList<ScriptLine> Lines { get; }

        /// <summary>Play the cam → taps → bow cutaway (at most four seconds, skippable).</summary>
        public bool PlayCutaway { get; }

        /// <summary>Persistent state changed: autosave now (spec §7 "Autosave on fact changes, closure").</summary>
        public bool ShouldAutosave => Gained.Count > 0;

        /// <summary>The case was closed by this step: stamp "Case closed" on the case page.</summary>
        public bool ClosedCase
        {
            get
            {
                foreach (var fact in Gained)
                {
                    if (fact == CaseFact.CaseClosed)
                    {
                        return true;
                    }
                }

                return false;
            }
        }
    }
}
