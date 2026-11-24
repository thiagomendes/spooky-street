using System;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Keepsakes;
using SpookyStreet.Core.Localization;
using SpookyStreet.Core.Save;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// Keepsake rules (S004 §2.1): a page and a piece at 10, 20 and 30 exactly once, never respawn, idempotent, Replay
    /// keeps, New game per decision 4; pieces and remarks only when earned; no child-facing total.
    /// </summary>
    public class KeepsakeRulesTests
    {
        private static string Bat(int n) => PaperBatLayout.IdFor(n);

        private static KeepsakeState Found(int count)
        {
            var state = KeepsakeState.Empty;
            for (int i = 1; i <= count; i++)
            {
                state = state.Find(Bat(i)).State;
            }

            return state;
        }

        [Test]
        public void PagesCompleteAtTenTwentyAndThirtyExactlyOnce()
        {
            var state = KeepsakeState.Empty;
            var completed = new System.Collections.Generic.List<int>();
            // Any order: the count decides, never which bat.
            foreach (int n in Enumerable.Range(1, 30).Reverse())
            {
                var result = state.Find(Bat(n));
                Assert.AreEqual(BatFind.Found, result.Outcome);
                if (result.CompletedPage > 0)
                {
                    completed.Add(result.State.BatCount);
                    Assert.AreEqual(result.State.BatCount / 10, result.CompletedPage);
                }

                state = result.State;
            }

            CollectionAssert.AreEqual(new[] { 10, 20, 30 }, completed);
            Assert.AreEqual(3, state.PagesCompleted);
            CollectionAssert.AreEqual(SketchPages.All, state.Pages);
            CollectionAssert.AreEqual(CostumePieces.All, state.EarnedPieces);
            Assert.AreEqual(0, state.LooseBats);
        }

        [Test]
        public void OnlyEarnedPagesAndPiecesAreListedNeverEmptySlots()
        {
            Assert.AreEqual(0, Found(9).Pages.Count);
            Assert.AreEqual(0, Found(9).EarnedPieces.Count);
            Assert.AreEqual(9, Found(9).LooseBats);
            Assert.AreEqual(1, Found(10).Pages.Count);
            Assert.AreEqual(SketchPages.Lanterns, Found(10).Pages[0]);
            Assert.AreEqual(CostumePieces.StarCape, Found(17).EarnedPieces.Single());
            Assert.AreEqual(7, Found(17).LooseBats);
            Assert.AreEqual(2, Found(29).Pages.Count);
        }

        [Test]
        public void ABatIsFoundOnceAndNeverRespawns()
        {
            var once = KeepsakeState.Empty.Find(Bat(10));
            Assert.AreEqual(BatFind.Found, once.Outcome);
            var again = once.State.Find(Bat(10));
            Assert.AreEqual(BatFind.AlreadyFound, again.Outcome);
            Assert.AreSame(once.State, again.State, "idempotent");
            Assert.AreEqual(0, again.CompletedPage);

            // The tenth find completes the page; finding it again completes nothing.
            var nine = Found(9);
            var tenth = nine.Find(Bat(10));
            Assert.AreEqual(1, tenth.CompletedPage);
            Assert.AreEqual(0, tenth.State.Find(Bat(10)).CompletedPage);
            Assert.AreEqual(0, tenth.State.Find(Bat(3)).CompletedPage);

            var unknown = KeepsakeState.Empty.Find("bat.lantern_lane.99");
            Assert.AreEqual(BatFind.Unknown, unknown.Outcome);
            Assert.IsTrue(unknown.State.IsEmpty);
            Assert.AreEqual(BatFind.Unknown, KeepsakeState.Empty.Find("Paper bat").Outcome, "never display text");
        }

        [Test]
        public void ReplayKeepsAndNewGameFollowsDecisionFour()
        {
            var state = Found(12).Wear(CostumePieces.StarCape.Id, true);
            Assert.AreSame(state, KeepsakeRules.AfterReplay(state));
            var fresh = KeepsakeRules.AfterNewGame(state);
            bool clears = KeepsakeRules.NewGameClearsKeepsakes;
            if (clears)
            {
                Assert.IsTrue(fresh.IsEmpty, "decision 4 (a): a New game clears bats, pages and pieces");
            }
            else
            {
                Assert.AreSame(state, fresh, "decision 4 (b): keepsakes survive a New game");
            }

            Assert.IsTrue(KeepsakeRules.AfterReplay(null).IsEmpty);
        }

        [Test]
        public void OnlyEarnedPiecesCanBeWorn()
        {
            var nine = Found(9);
            Assert.AreSame(nine, nine.Wear(CostumePieces.StarCape.Id, true), "not earned yet");
            var ten = Found(10);
            var caped = ten.Wear(CostumePieces.StarCape.Id, true);
            Assert.IsTrue(caped.IsWearing(CostumePieces.StarCape.Id));
            Assert.AreSame(caped, caped.Wear(CostumePieces.StarCape.Id, true));
            Assert.AreSame(caped, caped.Wear(CostumePieces.LanternPail.Id, true), "the pail comes at 20");
            Assert.AreSame(caped, caped.Wear("keepsake.costume.crown", true));
            Assert.IsFalse(caped.Wear(CostumePieces.StarCape.Id, false).IsWearing(CostumePieces.StarCape.Id));

            var all = Found(30);
            foreach (var piece in CostumePieces.All)
            {
                all = all.Wear(piece.Id, true);
            }

            Assert.AreEqual(3, all.WornPieces.Count, "three slots: all three together");
            Assert.AreEqual(3, CostumePieces.All.Select(p => p.Slot).Distinct().Count());
        }

        [Test]
        public void ARemarkIsSaidOnceAndKitWaitsForTheStamp()
        {
            var state = Found(30);
            Assert.IsNull(CostumeRemarks.Due(state, Speaker.June, false), "nothing worn");
            foreach (var piece in CostumePieces.All)
            {
                state = state.Wear(piece.Id, true);
            }

            Assert.AreSame(CostumeRemarks.JuneCape, CostumeRemarks.Due(state, Speaker.June, false));
            Assert.AreSame(CostumeRemarks.ValePail, CostumeRemarks.Due(state, Speaker.Vale, false));
            Assert.IsNull(CostumeRemarks.Due(state, Speaker.Kit, false), "Kit is at her booth only after the stamp");
            Assert.AreSame(CostumeRemarks.KitHat, CostumeRemarks.Due(state, Speaker.Kit, true));
            Assert.IsNull(CostumeRemarks.Due(state, Speaker.Bea, true));

            var said = state.Said(CostumeRemarks.JuneCape.Id);
            Assert.IsNull(CostumeRemarks.Due(said, Speaker.June, false), "once ever");
            Assert.AreSame(said, said.Said(CostumeRemarks.JuneCape.Id));
            Assert.AreSame(said, said.Said("keepsake.remark.nobody"));
        }

        [Test]
        public void FromSavedIdsDropsUnknownsDuplicatesAndUnearnedPieces()
        {
            var state = KeepsakeState.From(
                new[] { Bat(4), Bat(4), "bat.lantern_lane.77", Bat(1) },
                new[] { CostumePieces.StarCape.Id, "keepsake.costume.crown" },
                new[] { CostumeRemarks.ValePail.Id, CostumeRemarks.ValePail.Id, "x" });
            CollectionAssert.AreEqual(new[] { Bat(4), Bat(1) }, state.FoundBats);
            Assert.AreEqual(0, state.WornPieces.Count, "two bats earn no piece");
            CollectionAssert.AreEqual(new[] { CostumeRemarks.ValePail.Id }, state.SaidRemarks);
            Assert.IsTrue(KeepsakeState.From(null, null, null).IsEmpty);
        }

        [Test]
        public void StableIdsAreUniqueAndNeverDisplayText()
        {
            var ids = PaperBatLayout.All.Select(b => b.Id)
                .Concat(SketchPages.All.Select(p => p.Id))
                .Concat(CostumePieces.All.Select(p => p.Id))
                .Concat(CostumeRemarks.All.Select(r => r.Id)).ToList();
            Assert.AreEqual(ids.Count, ids.Distinct().Count());
            foreach (string id in ids)
            {
                Assert.IsTrue(Regex.IsMatch(id, "^[a-z0-9_.]+$"), id + " is an ID, not display text");
            }

            foreach (var page in SketchPages.All)
            {
                CollectionAssert.Contains(LocalizationKeys.Keepsakes.All, page.CaptionKey);
            }
        }

        [Test]
        public void NoChildFacingStringStatesATotal()
        {
            // Engagement §2.3: no "x / 30" or "x of 30" for keepsakes in the child's flow; the parent page alone shows it.
            var table = StringTable.Parse(LocalizationTests.BundledEnglishJson());
            var total = new Regex(@"\{\d\}\s*(/|of|out of)\s*(\{\d\}|\d+)|\b\d+\s*/\s*\d+\b", RegexOptions.IgnoreCase);
            foreach (var entry in table.Entries)
            {
                // Keepsake strings and anything that counts paper bats (the parent page's own keys are exempt).
                bool keepsake = entry.Key.StartsWith("keepsake.", StringComparison.Ordinal) ||
                                entry.Value.IndexOf("bat", StringComparison.OrdinalIgnoreCase) >= 0;
                if (!keepsake || entry.Key.StartsWith("ui.parent.", StringComparison.Ordinal))
                {
                    continue;
                }

                Assert.IsFalse(total.IsMatch(entry.Value), entry.Key + " shows a total: " + entry.Value);
            }

            foreach (string key in LocalizationKeys.Keepsakes.All)
            {
                Assert.IsTrue(table.TryGet(key, out string value), key);
                StringAssert.DoesNotContain("30", value);
            }
        }
    }

    /// <summary>The keepsakes file (keepsakes.json): codec, interruption at every step, recovery, a newer file kept.</summary>
    public class KeepsakeStoreTests
    {
        private const string Dir = "saves";

        private static string Bat(int n) => PaperBatLayout.IdFor(n);

        [Test]
        public void CodecRoundTripsAndDropsUnknownIds()
        {
            var state = KeepsakeState.Empty;
            for (int i = 1; i <= 11; i++)
            {
                state = state.Find(Bat(i)).State;
            }

            state = state.Wear(CostumePieces.StarCape.Id, true).Said(CostumeRemarks.JuneCape.Id);
            string json = KeepsakeCodec.Serialize(state);
            StringAssert.Contains("\"schemaVersion\": 1", json);
            Assert.IsTrue(KeepsakeCodec.TryRead(json, out var read, out bool newer));
            Assert.IsFalse(newer);
            Assert.AreEqual(state, read);
            Assert.AreEqual(json, KeepsakeCodec.Serialize(read), "deterministic output");

            string extra = json.Replace("\"" + Bat(1) + "\"", "\"" + Bat(1) + "\",\n    \"bat.bramble_row.01\"");
            Assert.IsTrue(KeepsakeCodec.TryRead(extra, out var dropped, out _));
            Assert.AreEqual(state, dropped, "a later street's bat is ignored, not fatal");

            Assert.IsTrue(KeepsakeCodec.TryRead("{\"schemaVersion\": 1, \"foundBats\": [\"" + Bat(2) + "\"]}", out var bare, out _),
                "a file with only bats loads");
            Assert.AreEqual(1, bare.BatCount);

            Assert.AreEqual(SaveCheck.Newer, KeepsakeCodec.Check("{\"schemaVersion\": 2, \"foundBats\": []}"));
            Assert.AreEqual(SaveCheck.Invalid, KeepsakeCodec.Check("{\"schemaVersion\": 1, \"foundBats\": ["));
            Assert.AreEqual(SaveCheck.Invalid, KeepsakeCodec.Check("{\"schemaVersion\": 1}"));
            Assert.AreEqual(SaveCheck.Invalid, KeepsakeCodec.Check("not json"));
        }

        [Test]
        public void EveryFindWritesAtOnceAndReloads()
        {
            var fs = new MemorySaveFileSystem();
            var store = new KeepsakeStore(fs, Dir);
            Assert.IsTrue(store.Load().IsEmpty);
            Assert.AreEqual(SaveLoadStatus.Empty, store.LoadStatus);
            Assert.IsFalse(fs.Exists(store.Paths.Latest), "nothing is written before the first find");

            Assert.AreEqual(BatFind.Found, store.Find(Bat(7)).Outcome);
            Assert.IsTrue(fs.Exists(store.Paths.Latest));
            Assert.IsTrue(new KeepsakeStore(fs, Dir).Load().HasFound(Bat(7)));

            string before = fs.Files[store.Paths.Latest];
            Assert.AreEqual(BatFind.AlreadyFound, store.Find(Bat(7)).Outcome);
            Assert.AreEqual(before, fs.Files[store.Paths.Latest], "a repeat writes nothing");
            Assert.AreNotEqual(StreetPaths().Latest, store.Paths.Latest, "its own file beside street.json");
            StringAssert.EndsWith("keepsakes.json", store.Paths.Latest);
        }

        private static SaveSlotPaths StreetPaths() => SpookyStreet.Core.Street.StreetStore.PathsFor(Dir);

        [Test]
        public void AnInterruptionAtAnyStepLeavesTheOldOrTheNewKeepsakes()
        {
            for (int warmups = 0; warmups < 3; warmups++)
            {
                var baseline = new MemorySaveFileSystem();
                var setup = new KeepsakeStore(baseline, Dir);
                setup.Load();
                for (int i = 1; i <= warmups; i++)
                {
                    setup.Find(Bat(i)); // a file and its previous copy on disk
                }

                for (int crashAt = 0; crashAt < 6; crashAt++)
                {
                    var fs = baseline.Clone();
                    fs.CrashAfter = crashAt;
                    var store = new KeepsakeStore(fs, Dir);
                    store.Load();
                    bool crashed = false;
                    try
                    {
                        store.Find(Bat(20));
                    }
                    catch (SimulatedCrash)
                    {
                        crashed = true;
                    }

                    fs.CrashAfter = -1;
                    var resume = new KeepsakeStore(fs, Dir);
                    var state = resume.Load();
                    string where = $"warmups {warmups}, crash at {crashAt}";
                    Assert.AreNotEqual(SaveLoadStatus.Unreadable, resume.LoadStatus, where);
                    Assert.AreEqual(warmups, state.FoundBats.Count(id => id != Bat(20)), where + ": no earlier bat is lost");
                    if (!crashed)
                    {
                        Assert.IsTrue(state.HasFound(Bat(20)), where);
                    }
                }
            }
        }

        [Test]
        public void ABrokenLatestRecoversThePreviousCopy()
        {
            var fs = new MemorySaveFileSystem();
            var store = new KeepsakeStore(fs, Dir);
            store.Load();
            store.Find(Bat(1));
            store.Find(Bat(2)); // latest: two bats, previous: one
            fs.Files[store.Paths.Latest] = "{\"schemaVersion\": 1, \"foundB";
            var recovered = new KeepsakeStore(fs, Dir);
            recovered.Load();
            Assert.AreEqual(SaveLoadStatus.Recovered, recovered.LoadStatus);
            Assert.AreEqual(1, recovered.State.BatCount, "the previous valid copy");

            fs.Files[store.Paths.Latest] = "garbage";
            fs.Files.Remove(store.Paths.Previous);
            var unreadable = new KeepsakeStore(fs, Dir);
            Assert.IsTrue(unreadable.Load().IsEmpty);
            Assert.AreEqual(SaveLoadStatus.Unreadable, unreadable.LoadStatus);
            unreadable.Find(Bat(3));
            Assert.AreEqual(1, new KeepsakeStore(fs, Dir).Load().BatCount, "play goes on writing");
        }

        [Test]
        public void ANewerKeepsakesFileIsKeptUntilAnExplicitNewGame()
        {
            var fs = new MemorySaveFileSystem();
            var paths = KeepsakeStore.PathsFor(Dir);
            const string newer = "{\"schemaVersion\": 9, \"foundBats\": [\"bat.lantern_lane.01\"], \"gems\": 4}";
            fs.Files[paths.Latest] = newer;
            var store = new KeepsakeStore(fs, Dir);
            store.Load();
            Assert.AreEqual(SaveLoadStatus.Newer, store.LoadStatus);
            Assert.IsTrue(store.WritesBlocked);
            Assert.AreEqual(BatFind.Found, store.Find(Bat(5)).Outcome, "found for this session");
            Assert.AreEqual(newer, fs.Files[paths.Latest], "never written over");

            store.Reset();
            Assert.AreEqual(newer, fs.Files[paths.Kept], "set aside untouched");
            var after = new KeepsakeStore(fs, Dir).Load();
            Assert.AreEqual(KeepsakeRules.NewGameClearsKeepsakes ? 0 : 1, after.BatCount);
        }

        [Test]
        public void ReplayAndTheCaseSaveLeaveTheKeepsakesFileAlone()
        {
            var fs = new MemorySaveFileSystem();
            var keepsakes = new KeepsakeStore(fs, Dir);
            keepsakes.Load();
            keepsakes.Find(Bat(9));
            string before = fs.Files[keepsakes.Paths.Latest];

            var cases = new CaseSaveStore(fs, Dir);
            cases.Load();
            cases.Save(CaseScenarioTests.PlayCanonical().State, CaseAnchors.ArrivalPorch);
            cases.Replay();
            Assert.AreEqual(keepsakes.State, keepsakes.Replay());
            Assert.AreEqual(before, fs.Files[keepsakes.Paths.Latest], "replaying never touches the keepsakes file");
            Assert.IsTrue(new KeepsakeStore(fs, Dir).Load().HasFound(Bat(9)));
        }

        [Test]
        public void ANewGameClearsTheFileWhenDecisionFourSaysSo()
        {
            var fs = new MemorySaveFileSystem();
            var store = new KeepsakeStore(fs, Dir);
            store.Load();
            store.Find(Bat(1));
            store.Reset();
            var reloaded = new KeepsakeStore(fs, Dir).Load();
            Assert.AreEqual(KeepsakeRules.NewGameClearsKeepsakes, reloaded.IsEmpty);
        }
    }
}
