using System;
using System.Collections.Generic;
using SpookyStreet.Core.Case;

namespace SpookyStreet.Core.Cutscenes
{
    /// <summary>
    /// When a cutscene plays (D28, narrative bible §5): each plays once per save on its own and is rewatchable from
    /// the notebook's case page. The opening plays on a new game before control; the reveal plays when the step that
    /// stamps the case (the three deductions plus the reopening, D26) has happened in this session, so it is never
    /// ahead of the child and never doubles the reopening (which the world action plays). Continue and a reload never
    /// start a cutscene.
    /// </summary>
    public static class CutscenePolicy
    {
        /// <summary>New game: the opening, unless this save has already seen it.</summary>
        public static string OnNewGame(IReadOnlyCollection<string> seen) =>
            Contains(seen, C01Cutscenes.OpeningId) ? null : C01Cutscenes.OpeningId;

        /// <summary>Continue (or a reload): nothing plays by itself.</summary>
        public static string OnContinue() => null;

        /// <summary>After a case step: the reveal when this step stamped the case and the save has not seen it.</summary>
        public static string AfterStep(CaseStep step, IReadOnlyCollection<string> seen) =>
            step != null && step.StampedCase && step.State.Has(CaseFact.CaseStamped) && !Contains(seen, C01Cutscenes.RevealId)
                ? C01Cutscenes.RevealId
                : null;

        /// <summary>The notebook's case page offers it: the opening always, the reveal once the case is stamped.</summary>
        public static bool CanRewatch(string id, CaseState state)
        {
            switch (id)
            {
                case C01Cutscenes.OpeningId:
                    return true;
                case C01Cutscenes.RevealId:
                    return state != null && state.Has(CaseFact.CaseStamped);
                default:
                    return false;
            }
        }

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
