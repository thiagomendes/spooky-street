using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Core.Save;
using Lines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;

namespace SpookyStreet.Tests.Core
{
    /// <summary>Memory replays: when they play (D23.2), their timing and skip, and how "seen" persists.</summary>
    public class MemoryReplayTests
    {
        private static readonly HashSet<string> None = new HashSet<string>();

        // ------------------------------------------------------------------------------- policy

        [Test]
        public void TheFourKeyLinesHaveReplaysAndVignettes()
        {
            CollectionAssert.AreEquivalent(
                new[] { Lines.BeaSawCape, Lines.BeaDoorOpen, Lines.ValeDoorShut, Lines.ValeWasShut },
                new List<MemoryReplayDefinition>(MemoryReplays.All).ConvertAll(r => r.LineKey));
            foreach (var replay in MemoryReplays.All)
            {
                Assert.NotNull(CaseScript.ConditionFor(replay.LineKey), replay.LineKey + " is a script line");
                Assert.IsFalse(C01Vignettes.For(replay.LineKey).UsesV1Strip, replay.LineKey + " needs its vignette");
                Assert.IsTrue(CaseNotebook.IsKnownEntry(replay.StickerEntryId), replay.StickerEntryId);
                StringAssert.StartsWith("mr.", replay.Id);
                Assert.AreSame(replay, MemoryReplays.Get(replay.Id));
            }

            Assert.IsNull(MemoryReplays.ForLine(Lines.JuneResolve), "resolve has a vignette but no memory replay");
        }

        [Test]
        public void NeverAutoPlaysTheFirstHearingOnlyOffersItInTheNotebook()
        {
            // D26 §7: memory replays are on demand only.
            foreach (var replay in MemoryReplays.All)
            {
                var decision = MemoryReplayPolicy.OnLineDismissed(replay.LineKey, None, false, false);
                Assert.AreEqual(ReplayDecisionKind.OnDemandOnly, decision.Kind, replay.Id);
                Assert.IsFalse(decision.Play);
                Assert.IsTrue(decision.MarkSeen);
            }

            Assert.AreEqual(ReplayPlace.TreehouseWindow, MemoryReplays.Get(MemoryReplays.BeaSawCape).Place);
        }

        [Test]
        public void BeasRestagedHazyMemoriesAreOfferedOnDemandFromHerSticker()
        {
            // S002.5b restaged Bea's replays as a silver blur high over the hedge (no June, no route): offered from her
            // sticker once heard, and never auto-played.
            var all = new HashSet<string> { MemoryReplays.BeaSawCape, MemoryReplays.BeaDoorOpen, MemoryReplays.ValeDoorShut };
            var state = CaseState.FromFacts(new[] { CaseFact.BeaSpoke, CaseFact.ValeSpoke });
            CollectionAssert.AreEqual(new[] { MemoryReplays.BeaSawCape, MemoryReplays.BeaDoorOpen },
                new List<MemoryReplayDefinition>(MemoryReplayPolicy.OnDemandFor(CaseNotebook.BeaFlash, state, all)).ConvertAll(r => r.Id));
            Assert.AreEqual(1, MemoryReplayPolicy.OnDemandFor(CaseNotebook.ValeDoor, state, all).Count);
            Assert.AreEqual(0, MemoryReplayPolicy.OnDemandFor(CaseNotebook.BeaFlash, CaseState.Empty, null).Count,
                "nothing before she has spoken");
            foreach (var replay in MemoryReplays.All)
            {
                Assert.IsTrue(replay.Offered, replay.Id);
                Assert.IsFalse(MemoryReplayPolicy.OnLineDismissed(replay.LineKey, null, false, false).Play, replay.Id + " never auto-plays");
            }
        }

        [Test]
        public void NeverAutoPlaysAgainOnceSeen()
        {
            var seen = new HashSet<string> { MemoryReplays.ValeWasShut };
            var decision = MemoryReplayPolicy.OnLineDismissed(Lines.ValeWasShut, seen, false, false);
            Assert.AreEqual(ReplayDecisionKind.AlreadySeen, decision.Kind);
            Assert.IsFalse(decision.Play);
            Assert.IsFalse(decision.MarkSeen);
        }

        [Test]
        public void OtherLinesHaveNoReplay()
        {
            var decision = MemoryReplayPolicy.OnLineDismissed(Lines.BeaRepeat, None, false, false);
            Assert.AreEqual(ReplayDecisionKind.NotKeyLine, decision.Kind);
            Assert.IsFalse(decision.MarkSeen);
            Assert.AreEqual(ReplayDecisionKind.NotKeyLine, MemoryReplayPolicy.OnLineDismissed(null, None, false, false).Kind);
        }

        [Test]
        public void DroppedWhenPipIsAirborneOrInAHazardButStillCountsAsOffered()
        {
            foreach (var (air, hazard) in new[] { (true, false), (false, true), (true, true) })
            {
                var decision = MemoryReplayPolicy.OnLineDismissed(Lines.BeaDoorOpen, None, air, hazard);
                Assert.AreEqual(ReplayDecisionKind.DroppedUnsafe, decision.Kind);
                Assert.IsFalse(decision.Play);
                Assert.IsTrue(decision.MarkSeen, "the first-hearing chance has passed; the notebook can play it");
            }

            Assert.IsTrue(MemoryReplayPolicy.AvailableOnDemand(MemoryReplays.Get(MemoryReplays.ValeWasShut),
                CaseState.Empty, new HashSet<string> { MemoryReplays.ValeWasShut }));
        }

        [Test]
        public void NotebookOffersSeenReplaysAndFactProvenOnesPerSticker()
        {
            var state = CaseState.FromFacts(new[] { CaseFact.ValeSpoke });
            var vale = MemoryReplayPolicy.OnDemandFor(CaseNotebook.ValeDoor, state, None);
            Assert.AreEqual(1, vale.Count, "a save from before this feature still offers Vale's replay");
            Assert.AreEqual(MemoryReplays.ValeDoorShut, vale[0].Id);
            Assert.AreEqual(0, MemoryReplayPolicy.OnDemandFor(CaseNotebook.BeaFlash, state, None).Count, "Bea has not spoken");

            var seen = new HashSet<string> { MemoryReplays.ValeWasShut };
            CollectionAssert.AreEqual(new[] { MemoryReplays.ValeDoorShut, MemoryReplays.ValeWasShut },
                new List<MemoryReplayDefinition>(MemoryReplayPolicy.OnDemandFor(CaseNotebook.ValeDoor, state, seen)).ConvertAll(r => r.Id));
            Assert.IsFalse(MemoryReplayPolicy.AvailableOnDemand(null, state, seen));
        }

        // ------------------------------------------------------------------------------- playback

        [Test]
        public void TimingIsBlendHoldBlendWithinBudget()
        {
            var t = MemoryReplayTiming.Default;
            Assert.AreEqual(0.35f, t.BlendIn, 1e-6f);
            Assert.AreEqual(0.35f, t.BlendOut, 1e-6f);
            Assert.That(t.Hold, Is.InRange(1.5f, 2.0f));
            Assert.LessOrEqual(t.Total, 2.7f + 1e-5f);
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new MemoryReplayTiming(0.35f, 1.4f, 0.35f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new MemoryReplayTiming(0.35f, 2.1f, 0.35f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new MemoryReplayTiming(-0.1f, 1.8f, 0.35f));
            Assert.DoesNotThrow(() => new MemoryReplayTiming(0.35f, 1.5f, 0.35f));
            Assert.DoesNotThrow(() => new MemoryReplayTiming(0.35f, 2.0f, 0.35f));
        }

        [Test]
        public void PlaysThroughBlendInHoldAndBlendOut()
        {
            var p = Playback(false);
            Assert.AreEqual(ReplayPhase.BlendIn, p.Phase);
            Assert.AreEqual(0f, p.Weight, 1e-5f);
            p.Advance(0.175f);
            Assert.AreEqual(0.5f, p.Weight, 1e-4f);
            p.Advance(0.175f);
            Assert.AreEqual(ReplayPhase.Hold, p.Phase);
            Assert.AreEqual(1f, p.Weight);
            p.Advance(0.9f);
            Assert.AreEqual(0.5f, p.GhostProgress, 1e-4f);
            p.Advance(0.9f);
            Assert.AreEqual(ReplayPhase.BlendOut, p.Phase);
            Assert.AreEqual(1f, p.GhostProgress, 1e-5f);
            p.Advance(0.35f);
            Assert.IsTrue(p.IsDone);
            Assert.AreEqual(0f, p.Weight);
            Assert.IsFalse(p.WasSkipped);
        }

        [Test]
        public void OneLargeStepCrossesPhases()
        {
            var p = Playback(false);
            p.Advance(10f);
            Assert.IsTrue(p.IsDone);
        }

        [Test]
        public void SkipBlendsBackAtOnceFromWhereItIs()
        {
            var p = Playback(false);
            p.Advance(1.0f);
            Assert.AreEqual(ReplayPhase.Hold, p.Phase);
            p.Skip();
            Assert.IsTrue(p.WasSkipped);
            Assert.AreEqual(ReplayPhase.BlendOut, p.Phase);
            Assert.AreEqual(1f, p.Weight, 1e-5f);
            p.Advance(0.35f);
            Assert.IsTrue(p.IsDone);

            var early = Playback(false);
            early.Advance(0.175f);
            float w = early.Weight;
            early.Skip();
            Assert.AreEqual(w, early.Weight, 1e-5f, "no jump");
            early.Advance(0.35f * w);
            Assert.IsTrue(early.IsDone, "shorter blend back from a partial blend");

            early.Skip();
            Assert.IsTrue(early.IsDone, "skip after the end is harmless");
        }

        [Test]
        public void ReducedMotionCutsAndShowsTheGhostAtItsEndPose()
        {
            var p = Playback(true);
            Assert.AreEqual(ReplayPhase.Hold, p.Phase);
            Assert.AreEqual(1f, p.Weight);
            Assert.AreEqual(1f, p.GhostProgress);
            p.Advance(1.8f);
            Assert.IsTrue(p.IsDone, "cut back, no blend");

            var s = Playback(true);
            s.Skip();
            Assert.IsTrue(s.IsDone);
        }

        [Test]
        public void SkippingKeepsTheReplayMarkedSeenAndNeverTouchesFacts()
        {
            // The line records its clue when it plays; the replay decision is taken after, from the seen set only.
            var store = new NonverbalProgressStore(new MemorySaveFileSystem(), "saves");
            var state = CaseState.FromFacts(new[] { CaseFact.BeaSpoke });
            store.Load(state);
            var decision = MemoryReplayPolicy.OnLineDismissed(Lines.BeaSawCape, store.Seen, false, false);
            Assert.IsTrue(decision.MarkSeen);
            store.MarkSeen(decision.Replay.Id, state);
            var playback = new MemoryReplayPlayback(decision.Replay, MemoryReplayTiming.Default, false);
            playback.Skip();
            Assert.IsTrue(state.Has(CaseFact.BeaSpoke), "the clue stands");
            CollectionAssert.Contains(store.Seen, MemoryReplays.BeaSawCape);
            Assert.AreEqual(ReplayDecisionKind.AlreadySeen,
                MemoryReplayPolicy.OnLineDismissed(Lines.BeaSawCape, store.Seen, false, false).Kind);
        }

        // ------------------------------------------------------------------------------- persistence

        [Test]
        public void ProgressRoundTripsAndDropsUnknownIds()
        {
            var state = CaseState.FromFacts(new[] { CaseFact.HeardKnocks, CaseFact.ValeSpoke });
            var data = new NonverbalProgressData(new[] { MemoryReplays.ValeDoorShut, "mr.nope" }, NonverbalProgressData.FactIds(state));
            string json = NonverbalProgressData.Serialize(data);
            Assert.IsTrue(NonverbalProgressData.TryRead(json, out var read, out bool newer));
            Assert.IsFalse(newer);
            CollectionAssert.AreEquivalent(new[] { MemoryReplays.ValeDoorShut }, read.Seen);
            CollectionAssert.AreEqual(data.BasisFacts, read.BasisFacts);
            Assert.IsTrue(read.BelongsTo(state));
            StringAssert.DoesNotContain("I saw", json, "never display text");
        }

        [Test]
        public void ProgressRejectsGarbageForeignAndNewerFiles()
        {
            Assert.AreEqual(SaveCheck.Invalid, NonverbalProgressData.Check("{"));
            Assert.AreEqual(SaveCheck.Invalid, NonverbalProgressData.Check(
                "{\"schemaVersion\":1,\"caseId\":\"c99\",\"seenReplays\":[],\"basisFacts\":[]}"));
            Assert.AreEqual(SaveCheck.Newer, NonverbalProgressData.Check(
                "{\"schemaVersion\":9,\"caseId\":\"c01\",\"seenReplays\":[],\"basisFacts\":[]}"));
            Assert.AreEqual(SaveCheck.Valid, NonverbalProgressData.Check(
                "{\"schemaVersion\":1,\"caseId\":\"c01\",\"seenReplays\":[],\"basisFacts\":[]}"));
        }

        [Test]
        public void BelongsToTheSaveWhoseFactsExtendTheBasis()
        {
            var early = CaseState.FromFacts(new[] { CaseFact.HeardKnocks, CaseFact.BeaSpoke });
            var data = new NonverbalProgressData(new[] { MemoryReplays.BeaSawCape }, NonverbalProgressData.FactIds(early));
            Assert.IsTrue(data.BelongsTo(early));
            Assert.IsTrue(data.BelongsTo(CaseState.FromFacts(new[] { CaseFact.HeardKnocks, CaseFact.BeaSpoke, CaseFact.ValeSpoke })),
                "facts only grow within a save");
            Assert.IsFalse(data.BelongsTo(CaseState.Empty), "fresh start or replay");
            Assert.IsFalse(data.BelongsTo(CaseState.FromFacts(new[] { CaseFact.BeaSpoke, CaseFact.HeardKnocks })),
                "a different order is a different playthrough");
            Assert.AreEqual(0, NonverbalProgressData.Reconcile(data, CaseState.Empty).Seen.Count);
            Assert.AreSame(data, NonverbalProgressData.Reconcile(data, early));
            Assert.AreEqual(0, NonverbalProgressData.Reconcile(null, early).Seen.Count);
        }

        [Test]
        public void StorePersistsSeenReplaysAcrossLaunches()
        {
            var fs = new MemorySaveFileSystem();
            var state = CaseState.FromFacts(new[] { CaseFact.HeardKnocks, CaseFact.BeaSpoke });
            var store = new NonverbalProgressStore(fs, "saves");
            CollectionAssert.IsEmpty(store.Load(state));
            store.MarkSeen(MemoryReplays.BeaSawCape, state);

            var later = CaseState.FromFacts(new[] { CaseFact.HeardKnocks, CaseFact.BeaSpoke, CaseFact.ValeSpoke });
            var relaunched = new NonverbalProgressStore(fs, "saves");
            CollectionAssert.AreEquivalent(new[] { MemoryReplays.BeaSawCape }, relaunched.Load(later));
            relaunched.MarkSeen(MemoryReplays.ValeDoorShut, later);
            CollectionAssert.AreEquivalent(new[] { MemoryReplays.BeaSawCape, MemoryReplays.ValeDoorShut },
                new NonverbalProgressStore(fs, "saves").Load(later));
            Assert.IsTrue(fs.Files.ContainsKey(NonverbalProgressStore.PathsFor("saves").Latest));
            Assert.AreNotEqual(SaveSlotPaths.ForCase("saves").Latest, NonverbalProgressStore.PathsFor("saves").Latest);
            Assert.Throws<System.ArgumentException>(() => relaunched.MarkSeen("mr.nope", later));
        }

        [Test]
        public void FreshStartOrReplayClearsSeenReplaysEvenAfterACrash()
        {
            var fs = new MemorySaveFileSystem();
            var state = CaseState.FromFacts(new[] { CaseFact.HeardKnocks, CaseFact.BeaSpoke });
            var store = new NonverbalProgressStore(fs, "saves");
            store.Load(state);
            store.MarkSeen(MemoryReplays.BeaSawCape, state);

            // The case save was replaced by a fresh one and the app died before anything told this store.
            var relaunched = new NonverbalProgressStore(fs, "saves");
            CollectionAssert.IsEmpty(relaunched.Load(CaseState.Empty));
            var replayed = CaseState.FromFacts(new[] { CaseFact.HeardKnocks, CaseFact.BeaSpoke });
            CollectionAssert.IsEmpty(new NonverbalProgressStore(fs, "saves").Load(replayed), "the clear was written");

            // Observe on a state change clears too, and explicit Reset always does.
            store = new NonverbalProgressStore(fs, "saves");
            store.Load(state);
            store.MarkSeen(MemoryReplays.BeaSawCape, state);
            store.Observe(CaseState.Empty);
            CollectionAssert.IsEmpty(store.Seen);
            store.MarkSeen(MemoryReplays.BeaSawCape, state);
            store.Reset();
            CollectionAssert.IsEmpty(new NonverbalProgressStore(fs, "saves").Load(state));
        }

        [Test]
        public void ObserveDoesNotWriteWhileTheSaveIsTheSame()
        {
            var fs = new MemorySaveFileSystem();
            var state = CaseState.FromFacts(new[] { CaseFact.HeardKnocks });
            var store = new NonverbalProgressStore(fs, "saves");
            store.Load(state);
            store.MarkSeen(MemoryReplays.ValeDoorShut, state);
            int ops = fs.Operations;
            store.Observe(CaseState.FromFacts(new[] { CaseFact.HeardKnocks, CaseFact.ValeSpoke }));
            Assert.AreEqual(ops, fs.Operations);
        }

        [Test]
        public void ACrashAtAnyStepOfAWriteLeavesTheOldOrNewSet()
        {
            var state = CaseState.FromFacts(new[] { CaseFact.HeardKnocks, CaseFact.BeaSpoke, CaseFact.ValeSpoke });
            var baseFs = new MemorySaveFileSystem();
            var setup = new NonverbalProgressStore(baseFs, "saves");
            setup.Load(state);
            setup.MarkSeen(MemoryReplays.BeaSawCape, state);

            for (int crashAfter = 0; crashAfter < 6; crashAfter++)
            {
                var fs = baseFs.Clone();
                var store = new NonverbalProgressStore(fs, "saves");
                store.Load(state);
                fs.CrashAfter = fs.Operations + crashAfter;
                try
                {
                    store.MarkSeen(MemoryReplays.ValeDoorShut, state);
                }
                catch (SimulatedCrash)
                {
                }

                fs.CrashAfter = -1;
                var seen = new List<string>(new NonverbalProgressStore(fs, "saves").Load(state));
                CollectionAssert.Contains(seen, MemoryReplays.BeaSawCape, "crash after " + crashAfter);
            }
        }

        private static MemoryReplayPlayback Playback(bool reducedMotion) =>
            new MemoryReplayPlayback(MemoryReplays.Get(MemoryReplays.BeaSawCape), MemoryReplayTiming.Default, reducedMotion);
    }
}
