using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Save;
using SpookyStreet.Core.Street;
using SpookyStreet.Core.World;

namespace SpookyStreet.Tests.Core
{
    /// <summary>S002.5b "the street remembers" (D29): the rules, the view laid over the world, and replay.</summary>
    public class StreetRulesTests
    {
        private const string C01 = CaseDefinition.CaseId;

        private static readonly StreetMemory[] Case01Memories =
        {
            StreetMemory.BoothOpen, StreetMemory.OwlBows, StreetMemory.BeaOwlSticker, StreetMemory.FrontDoorOpen,
            StreetMemory.JuneOutside,
        };

        private static CaseState Stamped => CaseScenarioTests.PlayCanonical().State;

        [Test]
        public void TheStreetLearnsCase01OnlyFromTheStamp()
        {
            var street = StreetState.Empty;
            foreach (var step in CaseScenarioTests.PlayCanonical().Steps)
            {
                street = StreetRules.Observe(street, C01, step.State);
                Assert.AreEqual(step.State.Has(CaseFact.CaseStamped), !street.IsEmpty,
                    "remembers exactly from the stamp: " + step.State);
            }

            CollectionAssert.AreEqual(new[] { C01 }, street.SolvedCases);
            CollectionAssert.AreEqual(Case01Memories, street.Memories, "the five case 01 memories, in table order");
        }

        [Test]
        public void TheReopeningWithoutTheStampTeachesNothing()
        {
            // The show before the deductions: closed, but the notebook still has open questions.
            var play = new CasePlaythrough();
            play.Do(CaseActionKind.HearKnocks);
            play.Do(CaseActionKind.EnterAttic);
            play.Talk(Speaker.June);
            play.Do(CaseActionKind.EnterFoyer);
            play.Do(CaseActionKind.LiftLatch);
            play.Do(CaseActionKind.ExamineGrille);
            play.Do(CaseActionKind.LinkArm);
            play.Do(CaseActionKind.TurnCrank);
            play.Do(CaseActionKind.ArriveAtPorch);
            Assert.IsTrue(play.State.Has(CaseFact.CaseClosed));
            Assert.IsFalse(play.State.Has(CaseFact.CaseStamped));
            Assert.IsTrue(StreetRules.Observe(StreetState.Empty, C01, play.State).IsEmpty);
            Assert.IsFalse(StreetView.From(StreetState.Empty, C01, play.State).Any);
        }

        [Test]
        public void RememberingIsIdempotent()
        {
            var once = StreetState.Empty.Remember(C01);
            var twice = once.Remember(C01);
            Assert.AreSame(once, twice, "nothing new: the same state");
            Assert.AreSame(once, StreetRules.Observe(once, C01, Stamped));
            Assert.AreSame(StreetState.Empty, StreetState.Empty.Remember("c99"), "unknown cases leave nothing");
            var rebuilt = StreetState.From(new[] { C01, C01, "c99" }, new[] { StreetMemory.OwlBows, StreetMemory.OwlBows });
            CollectionAssert.AreEqual(new[] { C01 }, rebuilt.SolvedCases);
            CollectionAssert.AreEqual(new[] { StreetMemory.OwlBows }, rebuilt.Memories);
        }

        [Test]
        public void AfterTheStampTheWorldShowsTheStreetsMemories()
        {
            var state = Stamped;
            var street = StreetRules.Observe(StreetState.Empty, C01, state);
            var view = StreetView.From(street, C01, state);
            foreach (var memory in Case01Memories)
            {
                Assert.IsTrue(view.Shows(memory), memory.ToString());
            }

            Assert.IsFalse(view.Resting);
            var world = view.Apply(CaseWorldView.From(state));
            Assert.AreEqual(JunePlacement.Treehouse, world.June, "June plays outside by her treehouse, not in the attic");
            Assert.IsTrue(world.BoothOpen);
            Assert.IsTrue(world.KitPresent, "Kit at her open booth");
            Assert.AreEqual(DoorState.Open, world.FrontDoor);
            Assert.AreEqual(OwlState.Bowing, world.Owl);
            Assert.AreEqual(world, view.Apply(world), "applying the street twice changes nothing");
        }

        [Test]
        public void JuneIsTalkedToWhereTheStreetPutsHer()
        {
            var state = Stamped;
            var closed = CaseWorldView.From(state);
            var remembered = StreetView.From(StreetState.Empty.Remember(C01), C01, state).Apply(closed);
            var point = CaseStage.Find(CaseStage.PointJuneTreehouse);
            Assert.AreEqual(PointSide.Outside, point.Side);
            Assert.IsFalse(CaseRouteLayout.Inside(point.Position.X, point.Position.Z));
            Assert.IsTrue(CaseStage.IsOffered(point, remembered, false, false));
            Assert.IsFalse(CaseStage.IsOffered(CaseStage.Find(CaseStage.PointJunePorch), remembered, false, false));
            Assert.IsFalse(CaseStage.IsOffered(point, closed, false, false), "the reopening keeps her on the porch");
            Assert.IsTrue(CaseStage.IsOffered(CaseStage.Find(CaseStage.PointJunePorch), closed, false, false));

            // Clear of the side-yard corridor to the ramp (route points "side yard" → "ramp foot"): 1.6 m wide.
            var a = new System.Numerics.Vector2(-11.2f, 3.9f);
            var b = new System.Numerics.Vector2(-12.45f, 6.9f);
            var p = new System.Numerics.Vector2(point.Position.X, point.Position.Z);
            float t = System.Math.Clamp(System.Numerics.Vector2.Dot(p - a, b - a) / (b - a).LengthSquared(), 0f, 1f);
            Assert.Greater((a + (b - a) * t - p).Length(), 0.8f + 0.25f, "June never narrows the way to the ramp");
        }

        [Test]
        public void TheStreetViewOpensAFreshWorldForALaterCase()
        {
            // A later case's save (not case 01): case 01's memories show over a world that knows nothing of it.
            var street = StreetState.Empty.Remember(C01);
            var view = StreetView.From(street, "c02", CaseState.Empty);
            Assert.IsTrue(view.Any);
            Assert.IsFalse(view.Resting);
            var world = view.Apply(CaseWorldView.From(CaseState.Empty));
            Assert.AreEqual(DoorState.Open, world.FrontDoor, "the door no longer sticks");
            Assert.AreEqual(OwlState.Bowing, world.Owl);
            Assert.AreEqual(JunePlacement.Treehouse, world.June);
            Assert.IsTrue(world.BoothOpen);
        }

        [Test]
        public void ReplayKeepsTheStreetAndRestsTheReplayedCasesMemoriesUntilItsStamp()
        {
            var street = StreetRules.Observe(StreetState.Empty, C01, Stamped);
            var afterReplay = StreetRules.AfterReplay(street);
            Assert.AreEqual(street, afterReplay, "replaying resets only case data (spec §7)");

            // The replay starts over: every memory of case 01 rests, so the mystery is whole again.
            var replay = new CasePlaythrough();
            var view = StreetView.From(afterReplay, C01, replay.State);
            Assert.IsTrue(view.Resting);
            Assert.IsFalse(view.Any);
            var world = view.Apply(CaseWorldView.From(replay.State));
            Assert.AreEqual(CaseWorldView.From(CaseState.Empty), world, "the street as it was that first evening");

            // Mid-replay, still resting, and replaying never teaches the street anything new.
            foreach (var step in CaseScenarioTests.PlayCanonical().Steps)
            {
                afterReplay = StreetRules.Observe(afterReplay, C01, step.State);
                var mid = StreetView.From(afterReplay, C01, step.State);
                Assert.AreEqual(step.State.Has(CaseFact.CaseStamped), mid.Any, step.State.ToString());
                Assert.AreEqual(street, afterReplay);
            }
        }

        [Test]
        public void ANewGameForgetsTheStreet()
        {
            var street = StreetState.Empty.Remember(C01);
            Assert.IsTrue(StreetRules.AfterNewGame(street).IsEmpty);
        }

        [Test]
        public void StableIdsAreUniqueAndRoundTrip()
        {
            var ids = new HashSet<string>();
            foreach (var memory in StreetMemories.All)
            {
                string id = StreetMemories.StableId(memory);
                StringAssert.StartsWith("street." + C01 + ".", id, "world-and-cases §5: street.* IDs");
                Assert.IsTrue(ids.Add(id), id);
                Assert.IsTrue(StreetMemories.TryParse(id, out var parsed));
                Assert.AreEqual(memory, parsed);
                Assert.AreEqual(C01, StreetMemories.CaseOf(memory));
            }

            Assert.LessOrEqual(StreetMemories.ForCase(C01).Count, 5, "a small set per case (world-and-cases §5)");
            Assert.IsFalse(StreetMemories.TryParse("street.c01.unknown", out _));
        }
    }

    /// <summary>The street file: codec, atomic writes, recovery and the migration from no file.</summary>
    public class StreetStoreTests
    {
        private const string Dir = "saves";
        private const string C01 = CaseDefinition.CaseId;

        private static CaseState Stamped => CaseScenarioTests.PlayCanonical().State;

        [Test]
        public void CodecRoundTripsAndDropsUnknownIds()
        {
            var state = StreetState.Empty.Remember(C01);
            string json = StreetCodec.Serialize(state);
            Assert.IsTrue(StreetCodec.TryRead(json, out var read, out bool newer));
            Assert.IsFalse(newer);
            Assert.AreEqual(state, read);
            Assert.AreEqual(json, StreetCodec.Serialize(read), "deterministic output");

            string extra = json.Replace("\"street.c01.owl_bows\"", "\"street.c01.owl_bows\",\n    \"street.c09.mystery\"");
            Assert.IsTrue(StreetCodec.TryRead(extra, out var dropped, out _));
            Assert.AreEqual(state, dropped);

            Assert.AreEqual(SaveCheck.Newer, StreetCodec.Check("{\"schemaVersion\": 2, \"solvedCases\": [], \"memories\": []}"));
            Assert.AreEqual(SaveCheck.Invalid, StreetCodec.Check("{\"schemaVersion\": 1, \"solvedCases\": ["));
            Assert.AreEqual(SaveCheck.Invalid, StreetCodec.Check("{\"schemaVersion\": 1}"));
            Assert.AreEqual(SaveCheck.Invalid, StreetCodec.Check("not json"));
        }

        [Test]
        public void MigrationFromNoFileRemembersAStampedSave()
        {
            var fs = new MemorySaveFileSystem();
            var store = new StreetStore(fs, Dir);
            Assert.IsTrue(store.Load().IsEmpty);
            Assert.AreEqual(SaveLoadStatus.Empty, store.LoadStatus);

            // An unstamped case writes nothing: no file appears for a case in progress.
            Assert.IsFalse(store.Observe(C01, CaseScenarioTests.PlayCanonical().Steps[3].State));
            Assert.IsFalse(fs.Exists(store.Paths.Latest));

            // A save stamped before the street file existed teaches the street on its first load.
            Assert.IsTrue(store.Observe(C01, Stamped));
            Assert.IsTrue(fs.Exists(store.Paths.Latest));
            var reloaded = new StreetStore(fs, Dir);
            Assert.IsTrue(reloaded.Load().HasSolved(C01));
            Assert.AreEqual(SaveLoadStatus.Latest, reloaded.LoadStatus);
            Assert.IsFalse(reloaded.Observe(C01, Stamped), "observing again changes and writes nothing");
        }

        [Test]
        public void AnInterruptionAtAnyStepLeavesTheOldOrTheNewStreet()
        {
            for (int warmups = 0; warmups < 3; warmups++)
            {
                var baseline = new MemorySaveFileSystem();
                var setup = new StreetStore(baseline, Dir);
                setup.Load();
                for (int i = 0; i < warmups; i++)
                {
                    setup.Reset(); // an empty street on disk (and its previous copy)
                }

                for (int crashAt = 0; crashAt < 6; crashAt++)
                {
                    var fs = baseline.Clone();
                    fs.CrashAfter = crashAt;
                    var store = new StreetStore(fs, Dir);
                    store.Load();
                    bool crashed = false;
                    try
                    {
                        store.Observe(C01, Stamped);
                    }
                    catch (SimulatedCrash)
                    {
                        crashed = true;
                    }

                    fs.CrashAfter = -1;
                    var resume = new StreetStore(fs, Dir);
                    var state = resume.Load();
                    string where = $"warmups {warmups}, crash at {crashAt}";
                    Assert.AreNotEqual(SaveLoadStatus.Unreadable, resume.LoadStatus, where);
                    Assert.IsTrue(state.IsEmpty || state.HasSolved(C01), where);
                    if (!crashed)
                    {
                        Assert.IsTrue(state.HasSolved(C01), where);
                    }

                    // Whatever survived, the stamped case save heals the street on this load.
                    resume.Observe(C01, Stamped);
                    Assert.IsTrue(new StreetStore(fs, Dir).Load().HasSolved(C01), where + " (healed)");
                }
            }
        }

        [Test]
        public void ABrokenLatestRecoversThePreviousCopy()
        {
            var fs = new MemorySaveFileSystem();
            var store = new StreetStore(fs, Dir);
            store.Load();
            store.Observe(C01, Stamped);
            store.Reset(); // latest: empty street, previous: case 01 remembered
            store.Observe(C01, Stamped); // latest: case 01, previous: empty
            fs.Files[store.Paths.Latest] = "{\"schemaVersion\": 1, \"solvedC";
            var recovered = new StreetStore(fs, Dir);
            recovered.Load();
            Assert.AreEqual(SaveLoadStatus.Recovered, recovered.LoadStatus);
            Assert.IsTrue(recovered.State.IsEmpty, "the previous valid copy");
            Assert.IsTrue(recovered.Observe(C01, Stamped), "and the stamped case save heals it");

            fs.Files[store.Paths.Latest] = "garbage";
            fs.Files.Remove(store.Paths.Previous);
            var unreadable = new StreetStore(fs, Dir);
            Assert.IsTrue(unreadable.Load().IsEmpty);
            Assert.AreEqual(SaveLoadStatus.Unreadable, unreadable.LoadStatus);
            Assert.IsTrue(unreadable.Observe(C01, Stamped));
            Assert.IsTrue(new StreetStore(fs, Dir).Load().HasSolved(C01));
        }

        [Test]
        public void ANewerStreetFileIsKeptUntilAnExplicitNewGame()
        {
            var fs = new MemorySaveFileSystem();
            var paths = StreetStore.PathsFor(Dir);
            const string newer = "{\"schemaVersion\": 9, \"solvedCases\": [\"c01\", \"c02\"], \"memories\": []}";
            fs.Files[paths.Latest] = newer;
            var store = new StreetStore(fs, Dir);
            store.Load();
            Assert.AreEqual(SaveLoadStatus.Newer, store.LoadStatus);
            Assert.IsTrue(store.WritesBlocked);
            Assert.IsTrue(store.Observe(C01, Stamped), "remembered for this session");
            Assert.AreEqual(newer, fs.Files[paths.Latest], "never written over");

            store.Reset();
            Assert.AreEqual(newer, fs.Files[paths.Kept], "set aside untouched");
            Assert.IsTrue(new StreetStore(fs, Dir).Load().IsEmpty);
        }

        [Test]
        public void ReplayAndTheCaseSaveLeaveTheStreetFileAlone()
        {
            var fs = new MemorySaveFileSystem();
            var street = new StreetStore(fs, Dir);
            street.Load();
            street.Observe(C01, Stamped);
            string before = fs.Files[street.Paths.Latest];

            var cases = new CaseSaveStore(fs, Dir);
            cases.Load();
            cases.Save(Stamped, CaseAnchors.ArrivalPorch);
            var replay = cases.Replay();
            Assert.AreEqual(0, replay.Facts.Count);
            Assert.AreEqual(street.State, street.Replay());
            Assert.AreEqual(before, fs.Files[street.Paths.Latest], "replaying never touches the street file");
            Assert.AreNotEqual(cases.Paths.Latest, street.Paths.Latest);
        }
    }

    /// <summary>The Continue picture recap: where Pip was, the open question's chips, the stickers; never an answer.</summary>
    public class ContinueRecapTests
    {
        private static IEnumerable<CaseState> EveryStateOfTwoPlays()
        {
            yield return CaseState.Empty;
            foreach (var step in CaseScenarioTests.PlayCanonical().Steps)
            {
                yield return step.State;
            }

            // Show first, deductions after: closed but open questions, then the stamp from the notebook.
            var play = new CasePlaythrough();
            foreach (var kind in new[]
                     {
                         CaseActionKind.HearKnocks, CaseActionKind.ExamineStreamer, CaseActionKind.EnterAttic,
                         CaseActionKind.EnterFoyer, CaseActionKind.LiftLatch, CaseActionKind.ExamineGrille,
                         CaseActionKind.LinkArm, CaseActionKind.TurnCrank, CaseActionKind.ArriveAtPorch,
                     })
            {
                yield return play.Do(kind).State;
            }

            play.Talk(Speaker.Bea);
            play.Talk(Speaker.Vale);
            play.Do(CaseActionKind.ExamineSill);
            yield return play.State;
            play.SolveAll();
            yield return play.State;
        }

        [Test]
        public void TheRecapNeverCarriesAnAnswer()
        {
            var answerKeys = new HashSet<string>();
            foreach (var question in CaseNotebook.Questions)
            {
                answerKeys.Add(question.AnswerKey);
            }

            int states = 0;
            foreach (var state in EveryStateOfTwoPlays())
            {
                var recap = ContinueRecap.Build(state, CaseAnchors.Treehouse);
                string where = state.ToString();
                Assert.IsFalse(answerKeys.Contains(recap.LineKey ?? ""), where);
                Assert.IsFalse(answerKeys.Contains(recap.QuestionKey ?? ""), where);
                Assert.AreNotEqual(recap.HeadingKey, recap.LineKey);
                var open = CaseNotebook.FirstOpen(state);
                if (state.Has(CaseFact.CaseStamped))
                {
                    Assert.IsNull(recap.OpenQuestionId, where);
                    Assert.IsEmpty(recap.Chips, where);
                    Assert.AreEqual(CaseNotebook.Build(state).FinalCard.StampKey, recap.LineKey, where);
                }
                else if (open != null)
                {
                    Assert.AreEqual(open.Id, recap.OpenQuestionId, where);
                    Assert.AreEqual(open.QuestionKey, recap.LineKey, "the question's own words, never its answer");
                    CollectionAssert.AreEqual(ContinueRecap.QuestionChips(open.Id), recap.Chips, where);
                    CollectionAssert.DoesNotContain(recap.Chips, "pg.check", "no chip marks an answer");
                }
                else
                {
                    Assert.IsNull(recap.LineKey, where);
                    Assert.IsEmpty(recap.Chips, where);
                }

                // Stickers: what was found, in the order found; never grouped by the question they answer.
                var found = new List<string>();
                foreach (var sticker in CaseNotebook.Found(state, true))
                {
                    found.Add(sticker.Id);
                }

                CollectionAssert.AreEqual(found, recap.Stickers, where);
                states++;
            }

            Assert.Greater(states, 20);
            foreach (var property in typeof(ContinueRecap).GetProperties())
            {
                StringAssert.DoesNotContain("Answer", property.Name, "the recap has no answer field");
                StringAssert.DoesNotContain("Comic", property.Name);
                StringAssert.DoesNotContain("Hint", property.Name, "no hint or route either");
            }
        }

        [Test]
        public void ChaptersFollowTheSolvedQuestionsAsSoftStops()
        {
            var chapters = new List<int>();
            foreach (var step in CaseScenarioTests.PlayCanonical().Steps)
            {
                int chapter = ContinueRecap.Build(step.State, CaseAnchors.Plaza).Chapter;
                if (chapters.Count == 0 || chapters[chapters.Count - 1] != chapter)
                {
                    chapters.Add(chapter);
                }
            }

            CollectionAssert.AreEqual(new[] { 1, 2, 3, 4 }, chapters, "the witnesses, then one stop per solved question");
            Assert.AreEqual(ContinueRecap.ChapterCount, chapters[chapters.Count - 1]);
        }

        [Test]
        public void ThePlaceIsTheValidatedResumeAnchor()
        {
            var midCase = CaseScenarioTests.PlayCanonical().Steps[6].State; // the attic was entered
            Assert.AreEqual(CaseAnchors.Attic, ContinueRecap.Build(midCase, CaseAnchors.Attic).PlaceAnchor);
            Assert.AreEqual(CaseAnchors.Plaza, ContinueRecap.Build(midCase, "c01.anchor.nowhere").PlaceAnchor,
                "unknown anchor → plaza, as Continue does");
            Assert.AreEqual(CaseAnchors.Resolve(CaseAnchors.Foyer, CaseState.Empty),
                ContinueRecap.Build(CaseState.Empty, CaseAnchors.Foyer).PlaceAnchor, "inside without the entry → plaza");
        }

        [Test]
        public void TheStickerCountAndTotalReadAsPictures()
        {
            var state = CaseScenarioTests.PlayCanonical().State;
            var recap = ContinueRecap.Build(state, CaseAnchors.ArrivalPorch);
            Assert.AreEqual(recap.Stickers.Count, recap.StickerCount);
            Assert.Greater(recap.StickerCount, ContinueRecap.MaxStickerIcons, "more stickers than icons: the count carries them");
            Assert.LessOrEqual(recap.StickerCount, recap.TotalStickers);
            Assert.IsTrue(recap.CaseClosed);
            Assert.AreEqual(4, recap.Chapter);
        }
    }
}
