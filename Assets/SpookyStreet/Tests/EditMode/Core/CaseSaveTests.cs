using System;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Save;

namespace SpookyStreet.Tests.Core
{
    /// <summary>The process was killed: the operation in progress did not finish.</summary>
    public sealed class SimulatedCrash : Exception
    {
    }

    /// <summary>
    /// In-memory <see cref="ISaveFileSystem"/> that can crash after a budget of operations. A crashing write leaves a
    /// truncated file; Replace is modelled as two renames (as on Mono), so the crash can land between them.
    /// </summary>
    public sealed class MemorySaveFileSystem : ISaveFileSystem
    {
        public Dictionary<string, string> Files { get; } = new Dictionary<string, string>(StringComparer.Ordinal);

        /// <summary>Operations allowed before a crash; negative means never crash.</summary>
        public int CrashAfter { get; set; } = -1;

        public int Operations { get; private set; }

        public bool Exists(string path) => Files.ContainsKey(path);

        public string ReadAllText(string path)
        {
            if (!Files.TryGetValue(path, out string text))
            {
                throw new FileNotFoundException(path);
            }

            return text;
        }

        public void WriteAllTextDurable(string path, string contents)
        {
            if (Tick())
            {
                Files[path] = contents.Substring(0, contents.Length / 2);
                throw new SimulatedCrash();
            }

            Files[path] = contents;
        }

        public void Replace(string source, string destination, string backup)
        {
            if (!Files.ContainsKey(source) || !Files.ContainsKey(destination))
            {
                throw new FileNotFoundException(source + " or " + destination);
            }

            Crash();
            string old = Files[destination];
            Files.Remove(destination);
            if (backup != null)
            {
                Files[backup] = old;
            }

            Crash();
            Files[destination] = Files[source];
            Files.Remove(source);
        }

        public void Move(string source, string destination)
        {
            if (!Files.ContainsKey(source) || Files.ContainsKey(destination))
            {
                throw new IOException("bad move " + source + " → " + destination);
            }

            Crash();
            Files[destination] = Files[source];
            Files.Remove(source);
        }

        public void Delete(string path)
        {
            Crash();
            Files.Remove(path);
        }

        public MemorySaveFileSystem Clone()
        {
            var copy = new MemorySaveFileSystem();
            foreach (var pair in Files)
            {
                copy.Files[pair.Key] = pair.Value;
            }

            return copy;
        }

        private void Crash()
        {
            if (Tick())
            {
                throw new SimulatedCrash();
            }
        }

        private bool Tick()
        {
            bool crash = CrashAfter >= 0 && Operations >= CrashAfter;
            Operations++;
            return crash;
        }
    }

    public class CaseSaveCodecTests
    {
        [Test]
        public void RoundTripsEveryContractField()
        {
            var state = CaseScenarioTests.PlayCanonical().State;
            var data = CaseSaveData.FromState(state, CaseAnchors.Workshop);
            string json = CaseSaveCodec.Serialize(data);
            StringAssert.Contains("\"schemaVersion\": 2", json);
            StringAssert.Contains("\"caseId\": \"c01\"", json);
            StringAssert.Contains("\"c01.attic.entered\"", json);
            StringAssert.Contains("\"anchorId\": \"c01.anchor.workshop\"", json);
            StringAssert.Contains("\"c01.note.q_entry\"", json);
            StringAssert.Contains("\"answers\": [", json);

            var read = CaseSaveCodec.Read(json);
            Assert.AreEqual(CaseSaveStatus.Ok, read.Status);
            CollectionAssert.IsEmpty(read.Issues);
            CollectionAssert.AreEqual(data.Facts, read.Data.Facts, "acquisition order survives");
            CollectionAssert.AreEqual(data.NotebookEntries, read.Data.NotebookEntries);
            CollectionAssert.AreEqual(data.Answers, read.Data.Answers);
            Assert.AreEqual(3, read.Data.Answers.Count, "the three solved questions keep their sets");
            Assert.AreEqual(CaseAnchors.Workshop, read.Data.AnchorId);
            CollectionAssert.AreEqual(state.Facts, read.Data.ToState().Facts);
            Assert.AreEqual(json, CaseSaveCodec.Serialize(read.Data), "deterministic");
        }

        [Test]
        public void UnknownAnchorGoesToThePlaza()
        {
            string json = Json(1, "c01", new[] { "c01.arrival.knocks_heard" }, "c01.anchor.rooftop_disco", new string[0]);
            var read = CaseSaveCodec.Read(json);
            Assert.AreEqual(CaseSaveStatus.Ok, read.Status);
            Assert.AreEqual(CaseAnchors.Plaza, read.Data.AnchorId);

            var inside = CaseSaveCodec.Read(Json(1, "c01", new string[0], CaseAnchors.Attic, new string[0]));
            Assert.AreEqual(CaseAnchors.Plaza, inside.Data.AnchorId, "an inside anchor needs the window entry");
        }

        [Test]
        public void ValidationDropsUnknownFactsAndRestoresImpliedOnes()
        {
            string json = Json(2, "c01", new[] { "c01.workshop.owl_tested", "c01.future.thing", "c01.workshop.owl_tested" },
                CaseAnchors.Foyer, new[] { "c01.note.q_moved", "c01.note.made_up" });
            var read = CaseSaveCodec.Read(json);
            Assert.AreEqual(CaseSaveStatus.Ok, read.Status);
            var state = read.Data.ToState();
            Assert.IsTrue(state.Has(CaseFact.LinkedArm));
            Assert.IsTrue(state.Has(CaseFact.SawCam));
            Assert.AreEqual(5, state.Count, "tested ⇒ linked ⇒ cam seen and arm loose; tested ⇒ owl seen");
            Assert.IsFalse(state.Has(CaseFact.SolvedMoved), "a schema-2 save is never helped to an answer");
            CollectionAssert.AreEqual(new[] { CaseNotebook.WoodenPiece, CaseNotebook.LooseArm, CaseNotebook.OwlBooth },
                read.Data.NotebookEntries);
            Assert.AreEqual(CaseAnchors.Plaza, read.Data.AnchorId, "foyer without the window entry");
            Assert.IsTrue(string.Join("; ", read.Issues).Contains("c01.future.thing"), "the dropped fact is reported");
        }

        [Test]
        public void NewerSchemaIsReportedNotRead()
        {
            var read = CaseSaveCodec.Read(Json(3, "c01", new string[0], CaseAnchors.Plaza, new string[0]));
            Assert.AreEqual(CaseSaveStatus.NewerSchema, read.Status);
            Assert.AreEqual(3, read.FoundSchemaVersion);
            Assert.IsNull(read.Data);
        }

        [Test]
        public void GarbageAndForeignSavesAreCorrupt()
        {
            foreach (string bad in new[]
            {
                "", "{", "null", "[]", "{\"schemaVersion\":\"1\"}", "{\"schemaVersion\":0,\"caseId\":\"c01\",\"facts\":[]}",
                "{\"schemaVersion\":1.5,\"caseId\":\"c01\",\"facts\":[]}",
                Json(1, "c02", new string[0], CaseAnchors.Plaza, new string[0]),
                "{\"schemaVersion\":1,\"caseId\":\"c01\",\"facts\":[1,2]}",
                "{\"schemaVersion\":1,\"caseId\":\"c01\"}",
            })
            {
                Assert.AreEqual(CaseSaveStatus.Corrupt, CaseSaveCodec.Read(bad).Status, bad);
            }

            string good = CaseSaveCodec.Serialize(CaseSaveData.Fresh());
            Assert.AreEqual(CaseSaveStatus.Corrupt, CaseSaveCodec.Read(good.Substring(0, good.Length / 2)).Status, "truncated");
        }

        [Test]
        public void MigrationStepsRunInOrder()
        {
            // No shipped schema is older than 1; prove the chain with a temporary step from a pretend schema 0.
            CaseSaveCodec.Migrations[0] = root =>
            {
                root["schemaVersion"] = 1.0;
                if (root.TryGetValue("progress", out object facts))
                {
                    root["facts"] = facts;
                    root.Remove("progress");
                }

                return root;
            };
            try
            {
                var read = CaseSaveCodec.Read(
                    "{\"schemaVersion\":0,\"caseId\":\"c01\",\"progress\":[\"c01.attic.entered\"],\"anchorId\":\"c01.anchor.attic\",\"notebook\":[]}");
                Assert.AreEqual(CaseSaveStatus.Ok, read.Status);
                Assert.IsTrue(read.Data.ToState().Has(CaseFact.EnteredByWindow));
                Assert.AreEqual(CaseAnchors.Attic, read.Data.AnchorId);
            }
            finally
            {
                CaseSaveCodec.Migrations.Remove(0);
            }
        }

        [Test]
        public void PreferencesRoundTripAndFallBackToDefaults()
        {
            var p = new PreferencesData { Locale = "pt-BR", InvertY = true, CameraSensitivity = 1.5f, ReducedMotion = true, Haptics = false, MusicVolume = 0.25f };
            Assert.IsTrue(PreferencesData.TryRead(PreferencesData.Serialize(p), out var back, out _));
            Assert.AreEqual("pt-BR", back.Locale);
            Assert.IsTrue(back.InvertY);
            Assert.AreEqual(1.5f, back.CameraSensitivity);
            Assert.IsTrue(back.ReducedMotion);
            Assert.IsFalse(back.Haptics);
            Assert.AreEqual(0.25f, back.MusicVolume);

            Assert.IsFalse(PreferencesData.TryRead("garbage", out var defaults, out bool newer));
            Assert.IsFalse(newer);
            Assert.AreEqual("en", defaults.Locale);
            Assert.IsTrue(defaults.Haptics);
            Assert.IsTrue(PreferencesData.TryRead("{\"schemaVersion\":1,\"locale\":\"<script>\",\"musicVolume\":9}", out var clamped, out _));
            Assert.AreEqual("en", clamped.Locale);
            Assert.AreEqual(1f, clamped.MusicVolume);
            Assert.AreEqual(SaveCheck.Newer, PreferencesData.Check("{\"schemaVersion\":7}"));
        }

        internal static string Json(int version, string caseId, string[] facts, string anchor, string[] notebook) =>
            new SaveJsonWriter().Number("schemaVersion", version).String("caseId", caseId).Strings("facts", facts)
                .String("anchorId", anchor).Strings("notebook", notebook).ToString();
    }

    public class SaveStoreTests
    {
        private const string Dir = "saves";

        [Test]
        public void PlannerWritesTempThenReplacesKeepingOnePreviousCopy()
        {
            var paths = SaveSlotPaths.ForCase(Dir);
            var first = SaveWritePlanner.PlanWrite(paths, "x", latestExists: false, latestValid: false);
            Assert.AreEqual(2, first.Count);
            Assert.AreEqual(SaveWriteStepKind.WriteFile, first[0].Kind);
            Assert.AreEqual(paths.Temp, first[0].Path);
            Assert.AreEqual(SaveWriteStepKind.Move, first[1].Kind);
            Assert.AreEqual(paths.Latest, first[1].Destination);

            var next = SaveWritePlanner.PlanWrite(paths, "y", latestExists: true, latestValid: true);
            Assert.AreEqual(SaveWriteStepKind.Replace, next[1].Kind);
            Assert.AreEqual(paths.Previous, next[1].Backup);

            var overCorrupt = SaveWritePlanner.PlanWrite(paths, "z", latestExists: true, latestValid: false);
            Assert.IsNull(overCorrupt[1].Backup, "a corrupt latest never replaces the good previous copy");
        }

        [Test]
        public void SecondWriteKeepsThePreviousValidCopy()
        {
            var fs = new MemorySaveFileSystem();
            var store = new CaseSaveStore(fs, Dir);
            Assert.AreEqual(SaveLoadStatus.Empty, store.Load().Status);
            var a = CaseState.Empty.Apply(CaseAction.Of(CaseActionKind.HearKnocks)).State;
            var b = a.Apply(CaseAction.Of(CaseActionKind.EnterAttic)).State;
            store.Save(a, CaseAnchors.Plaza);
            store.Save(b, CaseAnchors.Attic);
            Assert.IsFalse(fs.Exists(store.Paths.Temp));
            Assert.AreEqual(b.Count, CaseSaveCodec.Read(fs.Files[store.Paths.Latest]).Data.ToState().Count);
            Assert.AreEqual(a.Count, CaseSaveCodec.Read(fs.Files[store.Paths.Previous]).Data.ToState().Count);
        }

        [Test]
        public void CorruptLatestRecoversThePreviousCopy()
        {
            var fs = new MemorySaveFileSystem();
            var store = new CaseSaveStore(fs, Dir);
            var a = CaseState.FromFacts(new[] { CaseFact.HeardKnocks });
            store.Save(a, CaseAnchors.Plaza);
            store.Save(CaseState.FromFacts(new[] { CaseFact.HeardKnocks, CaseFact.BeaSpoke }), CaseAnchors.Plaza);
            fs.Files[store.Paths.Latest] = "{\"schemaVersion\":1,\"caseId\":\"c0";

            var reopened = new CaseSaveStore(fs, Dir);
            var resume = reopened.Load();
            Assert.AreEqual(SaveLoadStatus.Recovered, resume.Status);
            Assert.AreEqual(SpookyStreet.Core.Localization.LocalizationKeys.Save.Recovered, resume.NoticeBodyKey);
            Assert.AreEqual(1, resume.State.Count);

            // The next autosave replaces the corrupt latest without pushing out the good previous copy.
            reopened.Save(resume.State, resume.AnchorId);
            Assert.AreEqual(CaseSaveStatus.Ok, CaseSaveCodec.Read(fs.Files[reopened.Paths.Previous]).Status);
            Assert.AreEqual(CaseSaveStatus.Ok, CaseSaveCodec.Read(fs.Files[reopened.Paths.Latest]).Status);
        }

        [Test]
        public void NothingReadableOffersAnExplicitFreshStart()
        {
            var fs = new MemorySaveFileSystem();
            var paths = SaveSlotPaths.ForCase(Dir);
            fs.Files[paths.Latest] = "garbage";
            fs.Files[paths.Previous] = "{}";
            var store = new CaseSaveStore(fs, Dir);
            var resume = store.Load();
            Assert.AreEqual(SaveLoadStatus.Unreadable, resume.Status);
            Assert.IsTrue(resume.OffersFreshStart);
            Assert.IsFalse(resume.CanContinue);
            Assert.AreEqual("garbage", fs.Files[paths.Latest], "nothing is overwritten before the player chooses");

            store.StartFresh();
            Assert.AreEqual(SaveLoadStatus.Latest, new CaseSaveStore(fs, Dir).Load().Status);
        }

        [Test]
        public void NewerSchemaIsExplainedAndItsFilePreserved()
        {
            var fs = new MemorySaveFileSystem();
            var paths = SaveSlotPaths.ForCase(Dir);
            string newer = CaseSaveCodecTests.Json(9, "c01", new[] { "c01.attic.entered" }, CaseAnchors.Attic, new string[0]);
            fs.Files[paths.Latest] = newer;
            fs.Files[paths.Previous] = CaseSaveCodec.Serialize(CaseSaveData.Fresh());

            var store = new CaseSaveStore(fs, Dir);
            var resume = store.Load();
            Assert.AreEqual(SaveLoadStatus.Newer, resume.Status, "never silently falls back past a newer save");
            Assert.AreEqual(SpookyStreet.Core.Localization.LocalizationKeys.Save.NewerHeading, resume.NoticeHeadingKey);
            Assert.IsTrue(store.WritesBlocked);
            Assert.IsFalse(store.Save(CaseState.Empty, CaseAnchors.Plaza), "autosave cannot overwrite it");
            Assert.AreEqual(newer, fs.Files[paths.Latest]);

            store.StartFresh();
            Assert.AreEqual(newer, fs.Files[paths.Kept], "kept byte for byte");
            Assert.AreEqual(SaveLoadStatus.Latest, new CaseSaveStore(fs, Dir).Load().Status);
        }

        [Test]
        public void AnInterruptionAtAnyStepLeavesTheOldOrTheNewSave()
        {
            var old = CaseState.FromFacts(new[] { CaseFact.HeardKnocks, CaseFact.EnteredByWindow });
            var fresh = old.Apply(CaseAction.Of(CaseActionKind.LiftLatch)).State;
            for (int warmups = 0; warmups < 3; warmups++)
            {
                // warmups: how many earlier saves exist (0: first save ever; 1: no previous copy; 2: full rotation).
                var baseline = new MemorySaveFileSystem();
                var setup = new CaseSaveStore(baseline, Dir);
                setup.Load();
                for (int i = 0; i < warmups; i++)
                {
                    setup.Save(old, CaseAnchors.Attic);
                }

                for (int crashAt = 0; crashAt < 6; crashAt++)
                {
                    var fs = baseline.Clone();
                    fs.CrashAfter = crashAt;
                    var store = new CaseSaveStore(fs, Dir);
                    store.Load();
                    bool crashed = false;
                    try
                    {
                        store.Save(fresh, CaseAnchors.Foyer);
                    }
                    catch (SimulatedCrash)
                    {
                        crashed = true;
                    }

                    fs.CrashAfter = -1;
                    var resume = new CaseSaveStore(fs, Dir).Load();
                    string where = $"warmups {warmups}, crash at {crashAt}";
                    Assert.AreNotEqual(SaveLoadStatus.Unreadable, resume.Status, where);
                    if (warmups == 0 && crashed && resume.Status == SaveLoadStatus.Empty)
                    {
                        continue; // the very first save was interrupted before it existed: a new game, not an error
                    }

                    Assert.IsTrue(resume.CanContinue, where + " → " + resume.Status);
                    int count = resume.State.Count;
                    Assert.IsTrue(count == old.Count || count == fresh.Count, where);
                    if (!crashed)
                    {
                        Assert.AreEqual(fresh.Count, count, where);
                    }
                }
            }
        }

        [Test]
        public void ReplayResetsOnlyCaseData()
        {
            var fs = new MemorySaveFileSystem();
            var prefs = new PreferencesStore(fs, Dir);
            prefs.Save(new PreferencesData { Locale = "qps-ploc", LeftHanded = true });
            string prefsBefore = fs.Files[prefs.Paths.Latest];

            var store = new CaseSaveStore(fs, Dir);
            store.Load();
            store.Save(CaseScenarioTests.PlayCanonical().State, CaseAnchors.ArrivalPorch);
            var replay = store.Replay();
            Assert.AreEqual(0, replay.Facts.Count);
            Assert.AreEqual(0, replay.NotebookEntries.Count);
            Assert.AreEqual(CaseAnchors.Start, replay.AnchorId);
            Assert.AreEqual(0, new CaseSaveStore(fs, Dir).Load().State.Count);
            Assert.AreEqual(prefsBefore, fs.Files[prefs.Paths.Latest], "preferences file untouched");
            var loaded = new PreferencesStore(fs, Dir).Load();
            Assert.AreEqual("qps-ploc", loaded.Locale);
            Assert.IsTrue(loaded.LeftHanded);
            Assert.AreNotEqual(store.Paths.Latest, prefs.Paths.Latest);
        }

        [Test]
        public void NewerPreferencesAreKeptAsideWhenThePlayerChangesASetting()
        {
            var fs = new MemorySaveFileSystem();
            var paths = SaveSlotPaths.ForPreferences(Dir);
            fs.Files[paths.Latest] = "{\"schemaVersion\":5,\"locale\":\"fr\"}";
            var prefs = new PreferencesStore(fs, Dir);
            Assert.AreEqual("en", prefs.Load().Locale, "defaults in memory");
            Assert.AreEqual("{\"schemaVersion\":5,\"locale\":\"fr\"}", fs.Files[paths.Latest], "untouched on load");
            prefs.Save(new PreferencesData { InvertY = true });
            Assert.AreEqual("{\"schemaVersion\":5,\"locale\":\"fr\"}", fs.Files[paths.Kept]);
            Assert.IsTrue(new PreferencesStore(fs, Dir).Load().InvertY);
        }
    }

    /// <summary>SAVE-01 (Core half): force close at each major transition and restore from saved facts.</summary>
    public class Save01Tests
    {
        private static readonly (CaseAction action, string anchor)[] Route =
        {
            (CaseAction.Of(CaseActionKind.HearKnocks), CaseAnchors.ArrivalPorch),
            (CaseAction.Talk(Speaker.Bea), CaseAnchors.BicycleCorner),
            (CaseAction.Talk(Speaker.Vale), CaseAnchors.Bench),
            (CaseAction.Of(CaseActionKind.SitOnBench), CaseAnchors.Bench),
            (CaseAction.Of(CaseActionKind.ExamineCloth), CaseAnchors.Treehouse),
            (CaseAction.Of(CaseActionKind.EnterAttic), CaseAnchors.Attic),        // window entry
            (CaseAction.Talk(Speaker.June), CaseAnchors.Attic),
            (CaseAction.Of(CaseActionKind.LiftLatch), CaseAnchors.Foyer),         // latch opening
            (CaseAction.Of(CaseActionKind.ExamineGrille), CaseAnchors.Workshop),
            (CaseAction.Of(CaseActionKind.LinkArm), CaseAnchors.Workshop),
            (CaseAction.Of(CaseActionKind.TurnCrank), CaseAnchors.Workshop),      // immediately after the crank
            (CaseAction.Of(CaseActionKind.ArriveAtPorch), CaseAnchors.ArrivalPorch),
        };

        [Test]
        public void RestoreAtEachTransitionRebuildsTheWorldWithoutDuplicatedRewards()
        {
            var state = CaseState.Empty;
            var fs = new MemorySaveFileSystem();
            var store = new CaseSaveStore(fs, "saves");
            store.Load();
            var rewards = new List<CaseFact>();
            foreach (var (action, anchor) in Route)
            {
                var step = state.Apply(action);
                rewards.AddRange(step.Gained);
                state = step.State;
                Assert.IsTrue(step.ShouldAutosave, action + " changes facts");
                store.Save(state, anchor);

                // Force close: a new process reads the files back.
                var resume = new CaseSaveStore(fs, "saves").Load();
                Assert.AreEqual(SaveLoadStatus.Latest, resume.Status);
                var restored = resume.State;
                Assert.AreEqual(CaseWorldView.From(state), CaseWorldView.From(restored), "world after " + action);
                Assert.AreEqual(anchor, resume.AnchorId);
                CollectionAssert.AreEqual(state.Facts, restored.Facts);

                // Retrying the interrupted interaction awards nothing again (the reveal can replay, rewards cannot).
                var retry = restored.Apply(action);
                Assert.AreEqual(0, retry.Gained.Count, "duplicated reward on retry of " + action);
                Assert.IsFalse(retry.PlayCutaway);
            }

            var unique = new HashSet<CaseFact>(rewards);
            Assert.AreEqual(rewards.Count, unique.Count, "every fact awarded once");
        }

        [Test]
        public void KeyTransitionsRestoreDoorJuneAndOwl()
        {
            var afterWindow = CaseWorldView.From(Restore(CaseState.Empty.Apply(CaseAction.Of(CaseActionKind.EnterAttic)).State));
            Assert.AreEqual(DoorState.Latched, afterWindow.FrontDoor);
            Assert.AreEqual(JunePlacement.Attic, afterWindow.June);
            Assert.AreEqual(OwlState.Slipped, afterWindow.Owl);

            var metJune = CaseState.Empty.Apply(CaseAction.Of(CaseActionKind.EnterAttic)).State.Apply(CaseAction.Talk(Speaker.June)).State;
            var afterTalk = CaseWorldView.From(Restore(metJune));
            Assert.AreEqual(JunePlacement.Foyer, afterTalk.June, "June is at her mark, not stranded on the stairs");

            var afterLatch = CaseWorldView.From(Restore(metJune.Apply(CaseAction.Of(CaseActionKind.LiftLatch)).State));
            Assert.AreEqual(DoorState.Open, afterLatch.FrontDoor);
            Assert.AreEqual(JunePlacement.Foyer, afterLatch.June);

            var linked = metJune.Apply(CaseAction.Of(CaseActionKind.LiftLatch)).State.Apply(CaseAction.Of(CaseActionKind.LinkArm)).State;
            Assert.AreEqual(OwlState.Linked, CaseWorldView.From(Restore(linked)).Owl);
            Assert.IsTrue(CaseWorldView.From(Restore(linked)).ServicePanelOpen);
            var cranked = linked.Apply(CaseAction.Of(CaseActionKind.TurnCrank)).State;
            Assert.AreEqual(OwlState.Bowing, CaseWorldView.From(Restore(cranked)).Owl);
            Assert.AreEqual(DoorState.Open, CaseWorldView.From(Restore(cranked)).FrontDoor, "a retry never resets the door");
        }

        private static CaseState Restore(CaseState state)
        {
            string json = CaseSaveCodec.Serialize(CaseSaveData.FromState(state, CaseAnchors.Plaza));
            return CaseSaveCodec.Read(json).Data.ToState();
        }
    }
}
