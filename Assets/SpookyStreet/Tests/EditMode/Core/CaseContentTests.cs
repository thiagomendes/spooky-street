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
            foreach (var entry in CaseNotebook.Entries)
            {
                keys.Add(entry.TextKey);
            }

            foreach (var question in CaseNotebook.Questions)
            {
                keys.Add(question.QuestionKey);
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
            Assert.AreEqual("I saw June's silver cape go in. Right there!", catalog.Get(Lines.BeaSawCape));
            Assert.AreEqual("That front door? I could see it from my bench. It stayed shut.", catalog.Get(Lines.ValeDoorShut));
            Assert.AreEqual("It was shut when I looked.", catalog.Get(Lines.ValeWasShut));
            Assert.AreEqual("Silver cloth. One corner's torn.", catalog.Get(Lines.ExamineCloth));
            Assert.AreEqual("You came my way! I heard the knocks before I climbed in.", catalog.Get(Lines.JuneGreet));
            Assert.AreEqual("It sticks. Give me a hand?", catalog.Get(Lines.JuneDoorSticks));
            Assert.AreEqual("Hear that? Three knocks, three bows.", catalog.Get(Lines.JuneResolve));
            Assert.AreEqual("First show of the night. Best seats are on the porch.", catalog.Get(Lines.KitFirstShow));
            Assert.AreEqual("It's stuck on the inside.", catalog.Get(Lines.ExamineDoorStuck));
            Assert.AreEqual("How did June get inside?", catalog.Get(LocalizationKeys.Notebook.QuestionEntry));
            Assert.AreEqual("Where are the knocks coming from?", catalog.Get(LocalizationKeys.Notebook.QuestionSource));
            Assert.AreEqual("What did the knocking move?", catalog.Get(LocalizationKeys.Notebook.QuestionMoved));
            Assert.AreEqual("Try Mr. Vale's seat. What does the hedge hide?", catalog.Get(LocalizationKeys.Hints.CheckViewObserve));
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

    public class CaseNotebookTests
    {
        [Test]
        public void QuestionsAreAnsweredOnlyByPhysicalProof()
        {
            var talkedOnly = CaseState.FromFacts(new[] { CaseFact.BeaSpoke, CaseFact.ValeSpoke, CaseFact.CheckedView, CaseFact.ExaminedCloth });
            var page = CaseNotebook.Build(talkedOnly);
            foreach (var question in page.Questions)
            {
                Assert.IsFalse(question.Answered, "testimony and optional clues never answer " + question.QuestionKey);
                Assert.IsNull(question.AnswerKey);
            }

            Assert.IsNull(page.FinalCard);
            var bea = page.Questions[0].Annotations[0];
            Assert.IsTrue(bea.Found);
            Assert.AreEqual(LocalizationKeys.Notebook.NoteBea, bea.CaptionKey);

            var entered = CaseNotebook.Build(CaseState.FromFacts(new[] { CaseFact.EnteredByWindow }));
            Assert.IsTrue(entered.Questions[0].Answered);
            Assert.AreEqual(LocalizationKeys.Notebook.AnswerEntry, entered.Questions[0].AnswerKey);
            Assert.IsFalse(entered.Questions[1].Answered);
            Assert.IsFalse(entered.Questions[2].Answered);

            var tested = CaseNotebook.Build(CaseState.FromFacts(new[] { CaseFact.TestedOwl }));
            Assert.IsTrue(tested.Questions[1].Answered, "testing implies the cam was seen");
            Assert.IsTrue(tested.Questions[2].Answered);
        }

        [Test]
        public void UnknownEntriesHaveNoText()
        {
            var page = CaseNotebook.Build(CaseState.Empty);
            foreach (var question in page.Questions)
            {
                Assert.IsNull(question.AnswerKey);
                foreach (var note in question.Annotations)
                {
                    Assert.IsNull(note.CaptionKey);
                }
            }

            Assert.AreEqual(1, page.Extras.Count);
            Assert.IsFalse(page.Extras[0].Found);
        }

        [Test]
        public void EntryIdsAreStableAndUnique()
        {
            var ids = new HashSet<string>();
            foreach (var entry in CaseNotebook.Entries)
            {
                StringAssert.IsMatch(@"^c01\.note\.[a-z_]+$", entry.Id);
                Assert.IsTrue(ids.Add(entry.Id));
                Assert.IsTrue(CaseNotebook.IsKnownEntry(entry.Id));
            }

            foreach (var question in CaseNotebook.Questions)
            {
                Assert.IsTrue(CaseNotebook.IsKnownEntry(question.Id), question.Id);
            }
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
            Assert.AreEqual(LocalizationKeys.Hints.FreeRoamTitle, CaseHints.TitleKey(CaseObjective.FreeRoam));
        }
    }
}
