using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Case;

namespace SpookyStreet.Tests.Core
{
    /// <summary>Plays action sequences through the reducer and checks every line it plays is valid at that moment.</summary>
    public sealed class CasePlaythrough
    {
        public CaseState State { get; private set; } = CaseState.Empty;

        public List<CaseStep> Steps { get; } = new List<CaseStep>();

        public List<CaseFact> Gained { get; } = new List<CaseFact>();

        public CasePlaythrough(CaseState start = null)
        {
            State = start ?? CaseState.Empty;
        }

        public CaseStep Do(CaseAction action)
        {
            var step = State.Apply(action);
            foreach (var line in step.Lines)
            {
                var condition = CaseScript.ConditionFor(line.Key);
                Assert.IsNotNull(condition, "line without a condition: " + line);
                Assert.IsTrue(condition.Holds(step.Before), $"{line} played out of time after {action} in {step.Before}");
            }

            Steps.Add(step);
            Gained.AddRange(step.Gained);
            State = step.State;
            return step;
        }

        public CaseStep Do(CaseActionKind kind) => Do(CaseAction.Of(kind));

        public CaseStep Talk(Speaker speaker) => Do(CaseAction.Talk(speaker));

        public List<string> LinesOf(Speaker speaker)
        {
            var keys = new List<string>();
            foreach (var step in Steps)
            {
                foreach (var line in step.Lines)
                {
                    if (line.Speaker == speaker)
                    {
                        keys.Add(line.Key);
                    }
                }
            }

            return keys;
        }

        /// <summary>One right set per question, from the stickers of the canonical play (the notebook sends these).</summary>
        public static IEnumerable<CaseAction> RightSets()
        {
            yield return CaseAction.Solve(CaseNotebook.QuestionSource, new[] { CaseNotebook.FloorHum, CaseNotebook.WoodenPiece });
            yield return CaseAction.Solve(CaseNotebook.QuestionEntry,
                new[] { CaseNotebook.ValeDoor, CaseNotebook.Cloth, CaseNotebook.SillSnag });
            yield return CaseAction.Solve(CaseNotebook.QuestionMoved, new[] { CaseNotebook.OwlBooth, CaseNotebook.LooseArm });
        }

        /// <summary>Solves every raised question whose right set the child has found (the canonical deductions).</summary>
        public void SolveAll()
        {
            foreach (var solve in RightSets())
            {
                Do(solve);
            }
        }

        /// <summary>Every action the world can send, including each conversation partner and the notebook's deductions.</summary>
        public static IEnumerable<CaseAction> AllActions()
        {
            foreach (CaseActionKind kind in System.Enum.GetValues(typeof(CaseActionKind)))
            {
                if (kind == CaseActionKind.Talk)
                {
                    foreach (var speaker in new[] { Speaker.Bea, Speaker.Vale, Speaker.June, Speaker.Kit })
                    {
                        yield return CaseAction.Talk(speaker);
                    }
                }
                else if (kind == CaseActionKind.Solve)
                {
                    foreach (var solve in RightSets())
                    {
                        yield return solve;
                    }
                }
                else
                {
                    yield return CaseAction.Of(kind);
                }
            }
        }
    }
}
