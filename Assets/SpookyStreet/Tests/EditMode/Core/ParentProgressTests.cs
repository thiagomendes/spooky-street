using System;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Keepsakes;
using SpookyStreet.Core.Parent;
using SpookyStreet.Core.Street;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.Parent;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// S004 stream E: the "For grown-ups" page model. Its numbers are computed from the saves every time (nothing is
    /// stored), no field ever carries the answer of an unsolved question, and the keepsake totals live only here.
    /// </summary>
    public class ParentProgressTests
    {
        private static readonly DateTime Now = new DateTime(2026, 11, 24, 18, 0, 0);

        private static ParentProgressPage Build(CaseState state, CaseAnswers answers = null, StreetState street = null,
            KeepsakeState keepsakes = null, PlaytimeLedger playtime = null) =>
            ParentProgress.Build(state, answers ?? CaseAnswers.Empty, street, keepsakes, playtime, Now);

        [Test]
        public void ANewGameShowsNothingStartedAndNoTotalsButTheBats()
        {
            var page = Build(CaseState.Empty);
            Assert.AreEqual(ParentCaseStatus.NotStarted, page.CaseStatus);
            Assert.AreEqual(Keys.CaseNotStarted, page.CaseStatusKey);
            Assert.IsEmpty(page.Solved);
            Assert.IsEmpty(page.Places);
            Assert.IsEmpty(page.Friends);
            Assert.AreEqual(0, page.BatsFound);
            Assert.AreEqual(KeepsakeRules.TotalBats, page.BatsTotal);
            Assert.AreEqual(3, page.PagesTotal);
            Assert.AreEqual(3, page.PiecesTotal);
            Assert.AreEqual(Keys.TimeNone, page.Week.TotalKey);
            Assert.AreEqual(3, page.PractiseKeys.Count);
            Assert.AreEqual(3, page.PrivacyKeys.Count);
        }

        [Test]
        public void MidCaseShowsPlacesFriendsAndOnlyTheSolvedQuestionsComic()
        {
            var play = new CasePlaythrough();
            play.Do(CaseActionKind.HearKnocks);
            play.Talk(Speaker.Bea);
            play.Talk(Speaker.Vale);
            play.Do(CaseActionKind.SitOnBench);
            play.Do(CaseActionKind.ExamineCloth);
            var page = Build(play.State);
            Assert.AreEqual(ParentCaseStatus.InProgress, page.CaseStatus);
            CollectionAssert.AreEqual(new[] { ParentProgress.PlaceLane, ParentProgress.PlaceTreehouse },
                page.Places.Select(p => p.Id).ToArray());
            CollectionAssert.AreEqual(new[] { "friend.bea", "friend.vale" }, page.Friends.Select(f => f.Id).ToArray());
            Assert.AreEqual("portrait.bea.happy", page.Friends[0].Picture);
            Assert.IsEmpty(page.Solved, "nothing solved yet, so no answer anywhere");
        }

        [Test]
        public void TheChildsOwnOrderIsTheComic()
        {
            var play = new CasePlaythrough();
            foreach (var kind in new[]
                     {
                         CaseActionKind.HearKnocks, CaseActionKind.EnterAttic, CaseActionKind.LiftLatch,
                         CaseActionKind.EnterFoyer, CaseActionKind.ExamineGrille,
                     })
            {
                play.Do(kind);
            }

            var solve = CasePlaythrough.RightSets().First();
            play.Do(solve);
            if (!play.State.Has(CaseFact.SolvedSource))
            {
                Assert.Inconclusive("the source question needs other clues on this route: " + play.State);
            }

            var answers = CaseAnswers.Empty.With(CaseNotebook.QuestionSource, solve.Stickers.Reverse().ToArray());
            var page = Build(play.State, answers);
            var source = page.Solved.Single();
            Assert.AreEqual(CaseNotebook.QuestionSource, source.QuestionId);
            Assert.IsNotNull(source.AnswerKey);
            CollectionAssert.AreEquivalent(solve.Stickers, source.ComicStickers);
        }

        [Test]
        public void NoReachableStateLeaksAnUnsolvedAnswer()
        {
            int checkedStates = 0;
            foreach (var state in CaseReducerTests.ReachableStates())
            {
                var page = Build(state, CaseAnswers.Empty);
                foreach (var question in CaseNotebook.Questions)
                {
                    bool listed = page.Solved.Any(s => s.QuestionId == question.Id);
                    Assert.AreEqual(question.IsSolved(state), listed, $"{question.Id} in {state}");
                }

                foreach (var solved in page.Solved)
                {
                    Assert.IsTrue(CaseNotebook.Question(solved.QuestionId).IsSolved(state));
                    Assert.IsNotEmpty(solved.ComicStickers);
                    Assert.IsTrue(CaseDeduction.IsCorrect(CaseNotebook.Question(solved.QuestionId), solved.ComicStickers),
                        "the comic is a right set the child had");
                }

                Assert.AreEqual(state.Count > 0, page.Places.Any(p => p.Id == ParentProgress.PlaceLane));
                Assert.AreEqual(StreetRules.Solved(state), page.CaseStatus == ParentCaseStatus.Solved);
                Assert.LessOrEqual(page.Friends.Count, 4);
                checkedStates++;
            }

            Assert.Greater(checkedStates, 10);
        }

        [Test]
        public void ACaseTheStreetRemembersStaysSolvedAfterReplay()
        {
            var street = StreetState.Empty.Remember(CaseDefinition.CaseId);
            var page = Build(CaseState.Empty, street: street);
            Assert.AreEqual(ParentCaseStatus.Solved, page.CaseStatus);
            Assert.AreEqual(Keys.CaseSolved, page.CaseStatusKey);
            Assert.IsEmpty(page.Solved, "the replayed notebook starts fresh");
        }

        [Test]
        public void KeepsakeTotalsAreComputedFromTheFoundBats()
        {
            var keepsakes = KeepsakeState.Empty;
            foreach (var spot in PaperBatLayout.All.Take(17))
            {
                keepsakes = keepsakes.Find(spot.Id).State;
            }

            var page = Build(CaseState.Empty, keepsakes: keepsakes);
            Assert.AreEqual(17, page.BatsFound);
            Assert.AreEqual(1, page.PagesEarned);
            Assert.AreEqual(1, page.PiecesEarned);
        }

        [Test]
        public void TheWeekIsComputedFromTheLedger()
        {
            var ledger = new PlaytimeLedger();
            for (int day = 2; day >= 0; day--) // oldest first, as a real clock runs
            {
                for (int i = 0; i < 3200; i++)
                {
                    ledger.Tick(0.5, true, Now.AddDays(-day));
                }
            }

            var page = Build(CaseState.Empty, playtime: ledger);
            Assert.AreEqual(Keys.TimeHoursMinutes, page.Week.TotalKey);
            CollectionAssert.AreEqual(new object[] { 1, 20 }, page.Week.TotalArgs);
        }

        /// <summary>Nothing in Core/Parent names a network, web or socket type: the page is computed on the device.</summary>
        [Test]
        public void CoreParentReferencesNoNetworkType()
        {
            string en = LocalizationTests.BundledEnglishPath();
            string assets = Path.GetFullPath(Path.Combine(Path.GetDirectoryName(en), "..", "..", ".."));
            string folder = Path.Combine(assets, "Core", "Parent");
            var files = Directory.GetFiles(folder, "*.cs");
            Assert.IsNotEmpty(files);
            var banned = new Regex(@"System\.Net|HttpClient|WebRequest|WebClient|Socket|\bUri\b|Dns|TcpClient|UdpClient|IPackTransport|PackDownloader");
            foreach (string file in files)
            {
                string text = File.ReadAllText(file);
                Assert.IsFalse(banned.IsMatch(text), Path.GetFileName(file) + " must not reference networking");
            }

            foreach (var type in typeof(ParentProgress).Assembly.GetTypes().Where(t => t.Namespace == "SpookyStreet.Core.Parent"))
            {
                foreach (var member in type.GetMembers())
                {
                    string signature = member.ToString() ?? "";
                    Assert.IsFalse(signature.Contains("System.Net"), type.Name + "." + member.Name);
                }
            }
        }
    }
}
