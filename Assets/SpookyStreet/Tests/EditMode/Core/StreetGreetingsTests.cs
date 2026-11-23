using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Audio;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Localization;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Core.Street;
using Lines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// S003a "the street remembers in words": each friend's changed greeting once the street remembers case 01, under
    /// the same rules as the other memories (D29): only with the stamp remembered and case 01 not open for a replay;
    /// a replay rests them; a New game clears them.
    /// </summary>
    public class StreetGreetingsTests
    {
        private const string C01 = CaseDefinition.CaseId;

        private static readonly Speaker[] Friends = { Speaker.Bea, Speaker.Vale, Speaker.June, Speaker.Kit };

        private static readonly (Speaker Speaker, string Closed, string Greeting)[] Expected =
        {
            (Speaker.Bea, Lines.BeaClosed, Lines.BeaRemembers),
            (Speaker.Vale, Lines.ValeClosed, Lines.ValeRemembers),
            (Speaker.June, Lines.JuneClosed, Lines.JuneRemembers),
            (Speaker.Kit, Lines.KitThanks, Lines.KitRemembers),
        };

        private static CaseState Stamped => CaseScenarioTests.PlayCanonical().State;

        private static StreetState Remembered => StreetRules.Observe(StreetState.Empty, C01, Stamped);

        private static IReadOnlyList<ScriptLine> Talk(Speaker speaker, CaseState state, StreetView view) =>
            StreetGreetings.Retell(speaker, CaseScript.Conversation(speaker, state), view);

        private static List<string> KeysOf(IReadOnlyList<ScriptLine> lines)
        {
            var keys = new List<string>();
            foreach (var line in lines)
            {
                keys.Add(line.Key);
            }

            return keys;
        }

        [Test]
        public void EachFriendGreetsPipDifferentlyOnceTheStreetRemembersCase01()
        {
            var state = Stamped;
            var view = StreetView.From(Remembered, C01, state);
            Assert.IsTrue(view.Remembers(C01));
            foreach (var (speaker, closed, greeting) in Expected)
            {
                CollectionAssert.AreEqual(new[] { closed }, KeysOf(CaseScript.Conversation(speaker, state)),
                    speaker + ": the case script still says the closed line");
                var told = Talk(speaker, state, view);
                CollectionAssert.AreEqual(new[] { greeting }, KeysOf(told), speaker + " greets Pip differently");
                Assert.AreEqual(speaker, told[0].Speaker);
                Assert.AreEqual(greeting, StreetGreetings.For(speaker, view));
                Assert.AreEqual(closed, StreetGreetings.Replaces(greeting));
            }
        }

        [Test]
        public void TheNightOfTheStampKeepsTheClosedLines()
        {
            // The world view is fixed on load: before the stamp the street remembers nothing, so the night the case is
            // solved every friend says the closed line, and the greeting waits for the next visit.
            var state = Stamped;
            var view = StreetView.From(StreetState.Empty, C01, state);
            Assert.IsFalse(view.Remembers(C01));
            foreach (var (speaker, closed, _) in Expected)
            {
                CollectionAssert.AreEqual(new[] { closed }, KeysOf(Talk(speaker, state, view)), speaker.ToString());
                Assert.IsNull(StreetGreetings.For(speaker, view));
            }
        }

        [Test]
        public void AReplayRestsTheGreetingsUntilItsStampAndTheNextLoad()
        {
            var street = StreetRules.AfterReplay(Remembered);
            var replay = new CasePlaythrough();
            var resting = StreetView.From(street, C01, replay.State);
            Assert.IsTrue(resting.Resting);
            Assert.IsFalse(resting.Remembers(C01), "case 01 is open again: its memories rest");
            foreach (var friend in Friends)
            {
                Assert.IsNull(StreetGreetings.For(friend, resting), friend + " rests");
            }

            // Every talk of a whole replay plays the case's own lines, never a greeting.
            foreach (var step in CaseScenarioTests.PlayCanonical().Steps)
            {
                var view = StreetView.From(street, C01, step.State);
                if (StreetRules.Solved(step.State))
                {
                    break;
                }

                foreach (var friend in Friends)
                {
                    var script = CaseScript.Conversation(friend, step.State);
                    Assert.AreSame(script, StreetGreetings.Retell(friend, script, view), friend + " in " + step.State);
                    foreach (string key in StreetGreetings.AllKeys)
                    {
                        CollectionAssert.DoesNotContain(KeysOf(script), key);
                    }
                }
            }

            // With the replay's stamp and the next load the greetings come back.
            var again = StreetView.From(StreetRules.Observe(street, C01, Stamped), C01, Stamped);
            Assert.AreEqual(Lines.KitRemembers, StreetGreetings.For(Speaker.Kit, again));
        }

        [Test]
        public void ANewGameClearsTheGreetings()
        {
            var street = StreetRules.AfterNewGame(Remembered);
            var view = StreetView.From(street, C01, CaseState.Empty);
            foreach (var friend in Friends)
            {
                Assert.IsNull(StreetGreetings.For(friend, view));
            }

            Assert.IsFalse(StreetView.None.Remembers(C01));
        }

        [Test]
        public void OnlyTheClosedLineChanges()
        {
            var view = StreetView.From(Remembered, C01, Stamped);
            var lines = new[]
            {
                new ScriptLine(Speaker.Vale, Lines.ValeSavedSweet), new ScriptLine(Speaker.Vale, Lines.ValeClosed),
            };
            CollectionAssert.AreEqual(new[] { Lines.ValeSavedSweet, Lines.ValeRemembers },
                KeysOf(StreetGreetings.Retell(Speaker.Vale, lines, view)));
            Assert.AreSame(lines, StreetGreetings.Retell(Speaker.Bea, lines, view), "another speaker's lines stay");
            Assert.IsNull(StreetGreetings.For(Speaker.Pip, view), "Pip has no greeting");
            Assert.IsEmpty(StreetGreetings.Retell(Speaker.Kit, new ScriptLine[0], view));
            Assert.IsNull(StreetGreetings.Replaces(Lines.BeaClosed));
        }

        [Test]
        public void EveryGreetingIsAScriptLineWithWordsPicturesAndAVoice()
        {
            var english = StringTable.Parse(LocalizationTests.BundledEnglishJson());
            var all = new HashSet<string>(LocalizationKeys.All);
            CollectionAssert.AreEqual(new[] { Lines.BeaRemembers, Lines.ValeRemembers, Lines.JuneRemembers, Lines.KitRemembers },
                StreetGreetings.AllKeys);
            foreach (string key in StreetGreetings.AllKeys)
            {
                Assert.IsTrue(english.Contains(key), key);
                Assert.IsTrue(all.Contains(key), key);
                var condition = CaseScript.ConditionFor(key);
                Assert.NotNull(condition, key);
                Assert.IsTrue(condition.Holds(Stamped), key + " plays only after the stamp");
                Assert.IsFalse(condition.Holds(CaseState.Empty), key);
                Assert.IsTrue(LineVoices.Has(key), key + " has a babble emotion");
                var visual = C01Vignettes.For(key);
                Assert.NotNull(visual, key);
                Assert.IsFalse(visual.UsesV1Strip, key + " has its own vignette");
                Assert.IsEmpty(VignetteValidator.Validate(C01Vignettes.Get(visual.VignetteId)), key);

                // Short for 6–9 (narrative bible §5: 14 words at most).
                Assert.IsTrue(english.TryGet(key, out string text), key);
                Assert.LessOrEqual(text.Split(' ').Length, 14, key + ": " + text);
            }
        }
    }
}
