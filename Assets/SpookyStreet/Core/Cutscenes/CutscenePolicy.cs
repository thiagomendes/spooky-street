using System;
using System.Collections.Generic;
using SpookyStreet.Core.Case;

namespace SpookyStreet.Core.Cutscenes
{
    /// <summary>
    /// When a cutscene plays (D28, narrative bible §5): each plays once per save on its own and is rewatchable from
    /// the notebook's case page. The opening plays on a new game before control; the reveal plays when the step that
    /// stamps the case (the three deductions plus the reopening, D26) has happened in this session, so it is never
    /// ahead of the child and never doubles the reopening (which the world action plays). S004: the seed follows the
    /// reveal's first play; "the street remembers" plays on the first load after the stamp (not in the run where the
    /// stamp landed, never while a replay rests the memories). Otherwise Continue and a reload start nothing.
    /// </summary>
    public static class CutscenePolicy
    {
        /// <summary>New game: the opening, unless this save has already seen it.</summary>
        public static string OnNewGame(IReadOnlyCollection<string> seen) =>
            Contains(seen, C01Cutscenes.OpeningId) ? null : C01Cutscenes.OpeningId;

        /// <summary>
        /// Continue (the first load of a save in this run of the game): "the street remembers" once, when the case is
        /// stamped, the street lays its memories over the world (<paramref name="memoriesShown"/>: false while a
        /// replayed case rests them, D29), the stamp did not land in this run (<paramref name="stampedThisSession"/>), and
        /// this save has not seen it. Nothing else plays by itself on a Continue.
        /// </summary>
        public static string OnContinue(IReadOnlyCollection<string> seen, CaseState state, bool memoriesShown,
            bool stampedThisSession) =>
            Stamped(state) && memoriesShown && !stampedThisSession && !Contains(seen, C01Cutscenes.StreetRemembersId)
                ? C01Cutscenes.StreetRemembersId
                : null;

        /// <summary>After a case step: the reveal when this step stamped the case and the save has not seen it.</summary>
        public static string AfterStep(CaseStep step, IReadOnlyCollection<string> seen) =>
            step != null && step.StampedCase && step.State.Has(CaseFact.CaseStamped) && !Contains(seen, C01Cutscenes.RevealId)
                ? C01Cutscenes.RevealId
                : null;

        /// <summary>
        /// When a cutscene ends: the seed follows the reveal's first play (played through or skipped: the night always
        /// ends on the seed), once per save and only once stamped. A rewatch never chains.
        /// </summary>
        public static string AfterCutscene(string endedId, bool rewatch, IReadOnlyCollection<string> seen, CaseState state) =>
            endedId == C01Cutscenes.RevealId && !rewatch && Stamped(state) && !Contains(seen, C01Cutscenes.SeedId)
                ? C01Cutscenes.SeedId
                : null;

        /// <summary>
        /// The notebook offers it: the opening always; the reveal, the seed and "the street remembers" once the case is
        /// stamped (so never while a replay is open: its facts start over).
        /// </summary>
        public static bool CanRewatch(string id, CaseState state)
        {
            switch (id)
            {
                case C01Cutscenes.OpeningId:
                    return true;
                case C01Cutscenes.RevealId:
                case C01Cutscenes.SeedId:
                case C01Cutscenes.StreetRemembersId:
                    return Stamped(state);
                default:
                    return false;
            }
        }

        private static bool Stamped(CaseState state) => state != null && state.Has(CaseFact.CaseStamped);

        /// <summary>The cutscenes the case page offers now, in story order.</summary>
        public static IReadOnlyList<string> Rewatchable(CaseState state)
        {
            var list = new List<string>(2);
            foreach (string id in C01Cutscenes.Ids)
            {
                if (CanRewatch(id, state))
                {
                    list.Add(id);
                }
            }

            return list;
        }

        /// <summary>The cutscene to play for <paramref name="id"/> now, or null when it may not play (never ahead of the child).</summary>
        public static Cutscene Build(string id, CaseState state, CaseAnswers answers)
        {
            switch (id)
            {
                case C01Cutscenes.OpeningId:
                    return C01Cutscenes.Opening;
                case C01Cutscenes.RevealId:
                    return C01Cutscenes.Reveal(state, answers);
                case C01Cutscenes.SeedId:
                    return Stamped(state) ? C01Cutscenes.Seed : null;
                case C01Cutscenes.StreetRemembersId:
                    return Stamped(state) ? C01Cutscenes.StreetRemembers : null;
                default:
                    return null;
            }
        }

        private static bool Contains(IReadOnlyCollection<string> seen, string id)
        {
            if (seen == null)
            {
                return false;
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

    public enum SkipPress
    {
        /// <summary>The skip disc itself: skips at once.</summary>
        Disc,

        /// <summary>A tap anywhere else, or a key or button (keyboard Space/Enter/E, gamepad South/East/West).</summary>
        Any,
    }

    public enum SkipGateResult
    {
        Ignored,

        /// <summary>The first press: the skip disc grows and says "Press again to skip".</summary>
        Armed,

        Skip,
    }

    /// <summary>
    /// Skip on every control scheme (D25), kind to small hands: the skip disc skips at once; any other tap, key or
    /// button first arms the disc (it grows and asks again) and a second press within <see cref="ArmSeconds"/> skips.
    /// Presses in the first <see cref="GraceSeconds"/> are the press that started the cutscene and are ignored.
    /// </summary>
    public sealed class CutsceneSkipGate
    {
        public const float ArmSeconds = 2.5f;
        public const float GraceSeconds = 0.25f;

        private float _armedAt = float.NegativeInfinity;

        public bool Armed { get; private set; }

        /// <summary>A press at <paramref name="time"/> seconds since the cutscene started.</summary>
        public SkipGateResult Press(SkipPress press, float time)
        {
            if (time < GraceSeconds)
            {
                return SkipGateResult.Ignored;
            }

            Tick(time);
            if (press == SkipPress.Disc || Armed)
            {
                Armed = false;
                return SkipGateResult.Skip;
            }

            Armed = true;
            _armedAt = time;
            return SkipGateResult.Armed;
        }

        /// <summary>The arm lapses <see cref="ArmSeconds"/> after the first press.</summary>
        public void Tick(float time)
        {
            if (Armed && time - _armedAt > ArmSeconds)
            {
                Armed = false;
            }
        }

        public void Reset()
        {
            Armed = false;
            _armedAt = float.NegativeInfinity;
        }
    }
}
