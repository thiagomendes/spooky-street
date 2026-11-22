using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Localization;
using Lines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;

namespace SpookyStreet.Tests.Core
{
    public class CaseScriptTests
    {
        private static StringTable English => StringTable.Parse(LocalizationTests.BundledEnglishJson());

        [Test]
        public void EveryCaseKeyIsInBundledEnglishWithContext()
        {
            var table = English;
            var keys = new List<string>(CaseScript.AllLineKeys);
            keys.AddRange(CaseHints.AllKeys());
            foreach (var sticker in CaseNotebook.Stickers)
            {
                keys.Add(sticker.NoteKey);
            }

            foreach (var question in CaseNotebook.Questions)
            {
                keys.Add(question.QuestionKey);
                keys.Add(question.AnswerKey);
            }

            var all = new HashSet<string>(LocalizationKeys.All);
            foreach (string key in keys)
            {
                Assert.IsTrue(table.Contains(key), "en.json is missing " + key);
                Assert.IsTrue(all.Contains(key), "LocalizationKeys.All is missing " + key);
            }

            foreach (CaseActionKind kind in System.Enum.GetValues(typeof(CaseActionKind)))
            {
                string verb = CaseInteractions.VerbKey(kind);
                if (verb != null)
                {
                    Assert.IsTrue(table.Contains(verb), verb);
                }
            }
        }

        [Test]
        public void EveryScriptLineHasACondition()
        {
            foreach (var field in typeof(LocalizationKeys.CaseLines).GetFields())
            {
                if (field.IsLiteral)
                {
                    Assert.IsNotNull(CaseScript.ConditionFor((string)field.GetValue(null)), field.Name);
                }
            }
        }

        [Test]
        public void SpecLinesAreVerbatim()
        {
            var catalog = new LocalizationCatalog(English);
            Assert.AreEqual("[Three knocks from the blue house]", catalog.Get(Lines.CaptionArrivalKnocks));
            Assert.AreEqual("Something silver flashed up high, by the side of the house!", catalog.Get(Lines.BeaSawCape));
            Assert.AreEqual("That front door? I could see it from my bench. It stayed shut.", catalog.Get(Lines.ValeDoorShut));
            Assert.AreEqual("It was shut when I looked.", catalog.Get(Lines.ValeWasShut));
            Assert.AreEqual("Soft silver cloth, caught on a splinter.", catalog.Get(Lines.ExamineCloth));
            Assert.AreEqual("Pip! You found my secret way in! Did you hear the knocks too?", catalog.Get(Lines.JuneGreet));
            Assert.AreEqual("It sticks. Give me a hand?", catalog.Get(Lines.JuneDoorSticks));
            Assert.AreEqual("Hear that? Three knocks, three bows.", catalog.Get(Lines.JuneResolve));
            Assert.AreEqual("First show of the night. Best seats are on the porch.", catalog.Get(Lines.KitFirstShow));
            Assert.AreEqual("It won't budge.", catalog.Get(Lines.ExamineDoorStuck));
            Assert.AreEqual("So, detective, what happened here?", catalog.Get(Lines.KitAsk));
            Assert.AreEqual("How did June get in?", catalog.Get(LocalizationKeys.Notebook.QuestionEntry));
            Assert.AreEqual("Where are the knocks coming from?", catalog.Get(LocalizationKeys.Notebook.QuestionSource));
            Assert.AreEqual("What should the knocks move?", catalog.Get(LocalizationKeys.Notebook.QuestionMoved));
            Assert.AreEqual("A breeze keeps tugging toward the side yard.", catalog.Get(LocalizationKeys.Hints.FindWayInObserve));
            Assert.AreEqual("Back to the street", catalog.Get(LocalizationKeys.Notebook.BackToStreet));
            Assert.AreEqual("Replay case", catalog.Get(LocalizationKeys.Notebook.Replay));
            Assert.AreEqual("Case closed", catalog.Get(LocalizationKeys.Notebook.CaseClosedStamp));
        }

        [Test]
        public void WitnessesSwitchToPastTenseOnceTheDoorIsOpen()
        {
            var shut = CaseState.FromFacts(new[] { CaseFact.ValeSpoke });
            var open = CaseState.FromFacts(new[] { CaseFact.ValeSpoke, CaseFact.FrontOpen });
            Assert.AreEqual(Lines.ValeRepeat, CaseScript.Conversation(Speaker.Vale, shut)[0].Key);
            Assert.AreEqual(Lines.ValeWasShut, CaseScript.Conversation(Speaker.Vale, open)[0].Key);
            Assert.AreEqual(Lines.ValeWasShut, CaseScript.Conversation(Speaker.Vale, CaseState.FromFacts(new[] { CaseFact.FrontOpen }))[0].Key,
                "a first talk after the door opened is already past tense");
            Assert.AreEqual(Lines.BeaDoorOpen, CaseScript.Conversation(Speaker.Bea, open)[0].Key);
        }

        [Test]
        public void ConversationIsPureAndRecordsTheRightFact()
        {
            var state = CaseState.Empty;
            var first = CaseScript.Conversation(Speaker.Vale, state);
            var again = CaseScript.Conversation(Speaker.Vale, state);
            CollectionAssert.AreEqual(first, again);
            Assert.AreEqual(0, state.Count);
            var step = state.Apply(CaseAction.Talk(Speaker.Vale));
            CollectionAssert.AreEqual(first, step.Lines);
            CollectionAssert.AreEqual(new[] { CaseFact.ValeSpoke }, step.Gained);
        }
    }

    public class CaseHintTests
    {
        [Test]
        public void TiersEscalateAndRestartWithANewObjective()
        {
            var session = new HintSession();
            var state = CaseState.FromFacts(new[] { CaseFact.HeardKnocks });
            Assert.AreEqual(CaseObjective.CheckView, CaseHints.ObjectiveFor(state));
            Assert.AreEqual(LocalizationKeys.Hints.CheckViewObserve, session.Request(state));
            Assert.AreEqual(LocalizationKeys.Hints.CheckViewLocate, session.Request(state));
            Assert.AreEqual(LocalizationKeys.Hints.CheckViewAct, session.Request(state));
            Assert.AreEqual(LocalizationKeys.Hints.CheckViewAct, session.Request(state), "stays on the action");

            state = state.Apply(CaseAction.Of(CaseActionKind.SitOnBench)).State;
            Assert.AreEqual(HintTier.Observation, session.NextTier(state));
            Assert.AreEqual(LocalizationKeys.Hints.FindWayInObserve, session.Request(state));
        }

        /// <summary>For each objective, the action its third tier names actually moves the case on.</summary>
        [Test]
        public void EveryObjectivesActionHintMakesProgressFromAnyReachableState()
        {
            var actionFor = new Dictionary<CaseObjective, CaseAction>
            {
                { CaseObjective.Arrival, CaseAction.Of(CaseActionKind.HearKnocks) },
                { CaseObjective.CheckView, CaseAction.Of(CaseActionKind.SitOnBench) },
                { CaseObjective.FindWayIn, CaseAction.Of(CaseActionKind.EnterAttic) },
                { CaseObjective.MeetJune, CaseAction.Talk(Speaker.June) },
                { CaseObjective.OpenDoor, CaseAction.Of(CaseActionKind.LiftLatch) },
                { CaseObjective.FindSource, CaseAction.Of(CaseActionKind.ExamineGrille) },
                { CaseObjective.LinkArm, CaseAction.Of(CaseActionKind.LinkArm) },
                { CaseObjective.TurnCrank, CaseAction.Of(CaseActionKind.TurnCrank) },
                { CaseObjective.ReturnToPorch, CaseAction.Of(CaseActionKind.ArriveAtPorch) },
            };
            foreach (var state in CaseReducerTests.ReachableStates())
            {
                var objective = CaseHints.ObjectiveFor(state);
                if (objective == CaseObjective.FreeRoam)
                {
                    Assert.IsTrue(state.Has(CaseFact.CaseClosed));
                    continue;
                }

                var step = state.Apply(actionFor[objective]);
                Assert.Greater(step.Gained.Count, 0, $"{objective} hint leads nowhere from {state}");
                Assert.AreNotEqual(objective, CaseHints.ObjectiveFor(step.State));
            }
        }

        [Test]
        public void FreeRoamObservationNoticesWhenEverythingIsFound()
        {
            var all = new List<CaseFact>();
            foreach (var definition in CaseDefinition.Facts)
            {
                all.Add(definition.Fact);
            }

            var done = CaseState.FromFacts(all);
            Assert.AreEqual(LocalizationKeys.Hints.FreeRoamAllFound, CaseHints.HintFor(CaseObjective.FreeRoam, HintTier.Observation, done));
            var closed = CaseState.FromFacts(new[] { CaseFact.FrontOpen, CaseFact.TestedOwl, CaseFact.CaseClosed });
            Assert.AreEqual(LocalizationKeys.Hints.FreeRoamObserve, CaseHints.HintFor(CaseObjective.FreeRoam, HintTier.Observation, closed));
        }
    }
}
