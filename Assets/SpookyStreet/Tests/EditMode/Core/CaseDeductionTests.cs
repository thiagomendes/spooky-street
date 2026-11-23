using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Save;
using Lines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;
using N = SpookyStreet.Core.Case.CaseNotebook;

namespace SpookyStreet.Tests.Core
{
    /// <summary>S002.5a (D26): the notebook asks and waits; the child's stickers answer.</summary>
    public class CaseDeductionTests
    {
        private static CaseQuestion Entry => N.Question(N.QuestionEntry);
        private static CaseQuestion Source => N.Question(N.QuestionSource);
        private static CaseQuestion Moved => N.Question(N.QuestionMoved);

        private static IEnumerable<string> TrayIds()
        {
            foreach (var sticker in N.Stickers)
            {
                if (sticker.InTray)
                {
                    yield return sticker.Id;
                }
            }
        }

        /// <summary>Every ordered placement of tray stickers into a question's slots (with empty slots).</summary>
        private static IEnumerable<string[]> Placements(CaseQuestion question)
        {
            var ids = new List<string>(TrayIds()) { null };
            var slots = new string[question.SlotCount];
            return Fill(ids, slots, 0);
        }

        private static IEnumerable<string[]> Fill(List<string> ids, string[] slots, int at)
        {
            if (at == slots.Length)
            {
                yield return (string[])slots.Clone();
                yield break;
            }

            foreach (string id in ids)
            {
                slots[at] = id;
                foreach (var p in Fill(ids, slots, at + 1))
                {
                    yield return p;
                }
            }
        }

        private static CaseState All()
        {
            var facts = new List<CaseFact>();
            foreach (var d in CaseDefinition.Facts)
            {
                if (d.Role == FactRole.Optional)
                {
                    facts.Add(d.Fact);
                }
            }

            facts.Add(CaseFact.HeardKnocks);
            return CaseState.FromFacts(facts);
        }

        [Test]
        public void StickerAndQuestionIdsAreStableUniqueAndEarnedByOneFact()
        {
            var ids = new HashSet<string>();
            var facts = new HashSet<CaseFact>();
            foreach (var sticker in N.Stickers)
            {
                StringAssert.IsMatch(@"^c01\.sticker\.[a-z_]+$", sticker.Id);
                Assert.IsTrue(ids.Add(sticker.Id), sticker.Id);
                Assert.IsTrue(facts.Add(sticker.Fact), sticker.Id + " shares a fact");
                Assert.IsTrue(CaseAnchors.IsKnown(sticker.PlaceAnchor), sticker.Id);
                Assert.AreNotEqual(FactRole.Required, CaseDefinition.Get(sticker.Fact).Role, "clues never gate the show");
            }

            foreach (var question in N.Questions)
            {
                StringAssert.IsMatch(@"^c01\.note\.q_[a-z]+$", question.Id);
                Assert.IsTrue(N.IsKnownEntry(question.Id));
                Assert.That(question.SlotCount, Is.InRange(2, 3), "2 or 3 slots (§5)");
                foreach (var idea in question.Ideas)
                {
                    foreach (string id in idea)
                    {
                        Assert.IsNotNull(N.Sticker(id), id);
                    }
                }
            }

            Assert.AreEqual(2, Source.SlotCount);
            Assert.AreEqual(3, Entry.SlotCount);
            Assert.AreEqual(2, Moved.SlotCount);
            CollectionAssert.AreEqual(new[] { N.Streamer }, Entry.Decoys, "one honest decoy, on the entry question");
        }

        [Test]
        public void TheAcceptedSetsAreExactlyTheDesignedOnes()
        {
            // Enumerate every placement (order and empty slots included) and compare with the design table (§3).
            var right = new Dictionary<string, HashSet<string>>();
            foreach (var question in N.Questions)
            {
                var sets = new HashSet<string>();
                foreach (var placement in Placements(question))
                {
                    if (CaseDeduction.IsCorrect(question, placement))
                    {
                        var sorted = new List<string>(placement);
                        sorted.Sort(System.StringComparer.Ordinal);
                        sets.Add(string.Join("+", sorted));
                    }
                }

                right[question.Id] = sets;
            }

            CollectionAssert.AreEquivalent(new[] { Key(N.FloorHum, N.WoodenPiece) }, right[N.QuestionSource]);
            CollectionAssert.AreEquivalent(new[] { Key(N.OwlPanel, N.LooseArm), Key(N.OwlBooth, N.LooseArm) }, right[N.QuestionMoved]);

            var entry = new HashSet<string>();
            foreach (string door in new[] { N.ValeDoor, N.StuckDoor })
            {
                foreach (string high in new[] { N.Cloth, N.SillSnag })
                {
                    foreach (string side in new[] { N.BeaFlash, N.BenchView, N.Cloth, N.SillSnag })
                    {
                        if (side != high)
                        {
                            entry.Add(Key(door, high, side));
                        }
                    }
                }
            }

            CollectionAssert.AreEquivalent(entry, right[N.QuestionEntry]);
            Assert.AreEqual(10, entry.Count, "per door: (cloth or sill) × (Bea, bench, the other snag), cloth+sill counted once");
        }

        private static string Key(params string[] ids)
        {
            var list = new List<string>(ids);
            list.Sort(System.StringComparer.Ordinal);
            return string.Join("+", list);
        }

        [Test]
        public void TheDecoyNeverCompletesAnyQuestionAndNoSetRepeatsASticker()
        {
            foreach (var question in N.Questions)
            {
                foreach (var placement in Placements(question))
                {
                    if (!CaseDeduction.IsCorrect(question, placement))
                    {
                        continue;
                    }

                    CollectionAssert.DoesNotContain(placement, N.Streamer, question.Id);
                    CollectionAssert.DoesNotContain(placement, null);
                    CollectionAssert.AllItemsAreUnique(placement);
                }
            }

            Assert.IsFalse(CaseDeduction.IsCorrect(Entry, new[] { N.ValeDoor, N.Cloth, N.Cloth }), "one sticker, one idea");
            Assert.IsFalse(CaseDeduction.IsCorrect(Entry, new[] { N.ValeDoor, N.StuckDoor, N.Cloth }), "two doors, no side");
            Assert.IsFalse(CaseDeduction.IsCorrect(Entry, new[] { N.ValeDoor, N.Cloth }), "too few");
        }

        [Test]
        public void SlotOrderNeverMatters()
        {
            string[][] orders =
            {
                new[] { N.ValeDoor, N.Cloth, N.SillSnag }, new[] { N.SillSnag, N.ValeDoor, N.Cloth },
                new[] { N.Cloth, N.SillSnag, N.ValeDoor }, new[] { N.BeaFlash, N.SillSnag, N.StuckDoor },
            };
            foreach (var order in orders)
            {
                Assert.IsTrue(CaseDeduction.IsCorrect(Entry, order), string.Join(",", order));
                var comic = CaseDeduction.Comic(Entry, order);
                Assert.That(comic[0], Is.EqualTo(N.ValeDoor).Or.EqualTo(N.StuckDoor), "panel 1: the door");
                Assert.That(comic[1], Is.EqualTo(N.Cloth).Or.EqualTo(N.SillSnag), "panel 2: silver up high");
                CollectionAssert.AreEquivalent(order, comic, "the comic is the child's own stickers");
            }
        }

        [Test]
        public void QuestionsAppearWhenTheWorldRaisesThem()
        {
            Assert.AreEqual(0, N.Build(CaseState.Empty).Questions.Count, "nothing at the start");
            var knocks = CaseState.Empty.Apply(CaseAction.Of(CaseActionKind.HearKnocks)).State;
            CollectionAssert.AreEqual(new[] { Source }, N.Raised(knocks), "the knocks ask where they come from");
            var bea = knocks.Apply(CaseAction.Talk(Speaker.Bea)).State;
            CollectionAssert.AreEqual(new[] { Source, Entry }, N.Raised(bea));
            var vale = knocks.Apply(CaseAction.Talk(Speaker.Vale)).State;
            CollectionAssert.AreEqual(new[] { Source, Entry }, N.Raised(vale));
            var workshop = vale.Apply(CaseAction.Of(CaseActionKind.ExamineGrille)).State;
            CollectionAssert.AreEqual(new[] { Source, Entry, Moved }, N.Raised(workshop));
            var gap = CaseState.Empty.Apply(CaseAction.Of(CaseActionKind.TurnCrank)).State;
            CollectionAssert.Contains(N.Raised(gap), Moved, "the dangling arm raises it too");

            // A child who climbs straight in meets June first: her "secret way in" raises the entry question.
            var shortcut = CaseState.Empty.Apply(CaseAction.Of(CaseActionKind.EnterAttic)).State;
            CollectionAssert.DoesNotContain(N.Raised(shortcut), Entry);
            CollectionAssert.Contains(N.Raised(shortcut.Apply(CaseAction.Talk(Speaker.June)).State), Entry);
        }

        [Test]
        public void NothingIsPreSolvedByAnyWorldAction()
        {
            // The old notebook answered itself on attic entry; now no world action ever writes a solved fact.
            foreach (var state in CaseReducerTests.ReachableStates())
            {
                foreach (var action in CasePlaythrough.AllActions())
                {
                    if (action.Kind == CaseActionKind.Solve)
                    {
                        continue;
                    }

                    foreach (var fact in state.Apply(action).Gained)
                    {
                        Assert.IsFalse(fact == CaseFact.SolvedEntry || fact == CaseFact.SolvedSource || fact == CaseFact.SolvedMoved,
                            $"{action} answered a question in {state}");
                        if (fact == CaseFact.CaseStamped)
                        {
                            Assert.IsTrue(state.HasAll(new[] { CaseFact.SolvedEntry, CaseFact.SolvedSource, CaseFact.SolvedMoved }),
                                "the stamp needs the child's three deductions");
                        }
                    }
                }
            }

            var page = N.Build(All());
            foreach (var question in page.Questions)
            {
                Assert.IsFalse(question.Solved);
                Assert.IsNull(question.AnswerKey, "no answer words before the child solves");
                Assert.IsNull(question.AnswerStickers, "no comic before the child solves");
            }
        }

        [Test]
        public void TheReducerSolvesOnlyRightSetsOfFoundStickersOnRaisedQuestions()
        {
            var state = All();
            var wrong = state.Apply(CaseAction.Solve(N.QuestionEntry, new[] { N.ValeDoor, N.Cloth, N.Streamer }));
            Assert.AreEqual(CaseOutcome.WrongSet, wrong.Outcome);
            Assert.AreSame(state, wrong.State);

            var unfound = CaseState.FromFacts(new[] { CaseFact.ValeSpoke, CaseFact.ExaminedCloth })
                .Apply(CaseAction.Solve(N.QuestionEntry, new[] { N.ValeDoor, N.Cloth, N.SillSnag }));
            Assert.AreEqual(CaseOutcome.WrongSet, unfound.Outcome, "the sill snag was never found");

            var unraised = CaseState.FromFacts(new[] { CaseFact.ExaminedCloth, CaseFact.ExaminedSill, CaseFact.TriedDoor })
                .Apply(CaseAction.Solve(N.QuestionEntry, new[] { N.StuckDoor, N.Cloth, N.SillSnag }));
            Assert.AreEqual(CaseOutcome.NotYet, unraised.Outcome, "a question nobody asked yet");

            var right = state.Apply(CaseAction.Solve(N.QuestionEntry, new[] { N.StuckDoor, N.SillSnag, N.BenchView }));
            CollectionAssert.AreEqual(new[] { CaseFact.SolvedEntry }, right.Gained);
            Assert.IsTrue(right.ShouldAutosave);
            Assert.AreEqual(CaseOutcome.Repeated, right.State.Apply(CaseAction.Solve(N.QuestionEntry,
                new[] { N.ValeDoor, N.Cloth, N.SillSnag })).Outcome, "solved once");
        }

        [Test]
        public void WrongTriesWobbleHomeThenFittingStickersStayThenTheDecoyIsSetAside()
        {
            var state = All();
            var found = N.FoundIds(state);
            var board = new DeductionBoard(Entry);
            DeductionCheck Try(params string[] ids)
            {
                for (int i = 0; i < ids.Length; i++)
                {
                    Assert.IsTrue(board.Place(i, ids[i], found), ids[i]);
                }

                return board.Check(found);
            }

            board.Place(0, N.ValeDoor, found);
            Assert.AreEqual(DeductionVerdict.NotFull, board.Check(found).Verdict, "nothing checks until every slot is full");
            Assert.AreEqual(0, board.Tries);
            board.Clear();

            // Tries 1 and 2: everything wobbles home, even the stickers that fit (all-or-nothing).
            for (int t = 1; t <= 2; t++)
            {
                var check = Try(N.ValeDoor, N.Cloth, N.Streamer);
                Assert.AreEqual(DeductionVerdict.Wrong, check.Verdict);
                CollectionAssert.AreEqual(new[] { 0, 1, 2 }, check.ReturnedSlots);
                Assert.IsEmpty(check.KeptSlots);
                Assert.IsTrue(board.IsEmpty);
            }

            // Try 3: the stickers that fit stay with a tick; only the streamer goes home.
            var third = Try(N.ValeDoor, N.Cloth, N.Streamer);
            CollectionAssert.AreEqual(new[] { 2 }, third.ReturnedSlots);
            CollectionAssert.AreEqual(new[] { 0, 1 }, third.KeptSlots);
            Assert.IsTrue(board.IsKept(0) && board.IsKept(1));
            Assert.AreEqual(N.ValeDoor, board.Slots[0]);
            Assert.IsFalse(third.DecoySetAside);

            // Two doors fit the same idea: only the first stays.
            board.Clear();
            var doors = Try(N.ValeDoor, N.StuckDoor, N.Streamer);
            CollectionAssert.AreEqual(new[] { 0 }, doors.KeptSlots);

            // Try 5 with every needed clue found: the decoy is set aside and can no longer be placed here.
            board.Clear();
            var fifth = Try(N.Streamer, N.Cloth, N.ValeDoor);
            Assert.AreEqual(5, fifth.Tries);
            Assert.IsTrue(fifth.DecoySetAside);
            Assert.IsTrue(board.DecoyAside);
            Assert.IsFalse(board.Place(0, N.Streamer, found), "set aside: not this time");
            Assert.IsTrue(new DeductionBoard(Source).Place(0, N.Streamer, found), "other questions are untouched");

            // The right set: the comic, the stickers go home, tries reset.
            board.Clear();
            var right = Try(N.SillSnag, N.ValeDoor, N.BeaFlash);
            Assert.AreEqual(DeductionVerdict.Right, right.Verdict);
            CollectionAssert.AreEqual(new[] { N.ValeDoor, N.SillSnag, N.BeaFlash }, right.Comic);
            Assert.IsTrue(board.IsEmpty);
            Assert.AreEqual(0, board.Tries);
        }

        [Test]
        public void TheDecoyStaysInPlayWhileANeededClueIsMissing()
        {
            // Without the door clues the question cannot be solved, so the scaffold never removes the decoy.
            var state = CaseState.FromFacts(new[] { CaseFact.BeaSpoke, CaseFact.ExaminedCloth, CaseFact.ExaminedSill, CaseFact.ExaminedStreamer });
            var found = N.FoundIds(state);
            var board = new DeductionBoard(Entry);
            for (int t = 0; t < 8; t++)
            {
                board.Place(0, N.Streamer, found);
                board.Place(1, N.Cloth, found);
                board.Place(2, N.SillSnag, found);
                Assert.IsFalse(board.Check(found).DecoySetAside);
                board.Clear();
            }

            Assert.IsFalse(board.DecoyAside);
        }

        [Test]
        public void PlacingMovesStickersAndRefusesUnfoundOnes()
        {
            var found = N.FoundIds(CaseState.FromFacts(new[] { CaseFact.FloorHum, CaseFact.SawCam }));
            var board = new DeductionBoard(Source);
            Assert.IsFalse(board.Place(0, N.Cloth, found), "not in the tray");
            Assert.IsTrue(board.Place(0, N.FloorHum, found));
            Assert.IsTrue(board.Place(1, N.FloorHum, found), "dragging a placed sticker moves it");
            Assert.IsNull(board.Slots[0]);
            Assert.AreEqual(1, board.SlotOf(N.FloorHum));
            Assert.AreEqual(0, board.FirstEmpty());
            Assert.IsTrue(board.Place(1, N.WoodenPiece, found), "a full slot swaps its sticker home");
            Assert.AreEqual(-1, board.SlotOf(N.FloorHum));
            Assert.AreEqual(N.WoodenPiece, board.Remove(1));
            Assert.IsTrue(board.IsEmpty);
        }

        [Test]
        public void TheStampNeedsTheShowAndAllThreeDeductionsWhileTheShowNeedsOnlyTheWorld()
        {
            var play = new CasePlaythrough(All());
            play.Do(CaseActionKind.LiftLatch);
            play.Do(CaseActionKind.LinkArm);
            play.Do(CaseActionKind.TurnCrank);
            play.Do(CaseActionKind.ArriveAtPorch);
            Assert.IsTrue(play.State.Has(CaseFact.CaseClosed), "the owl show is tied to world actions (D26)");
            Assert.IsFalse(play.State.Has(CaseFact.CaseStamped));
            Assert.IsNull(N.Build(play.State).FinalCard, "no Case closed card yet");
            Assert.AreEqual(Lines.KitAsk, play.Steps[play.Steps.Count - 1].Lines[2].Key, "So, detective?");
            Assert.AreEqual(Lines.KitAsk, CaseScript.Conversation(Speaker.Kit, play.State)[0].Key);

            play.Do(CaseAction.Solve(N.QuestionSource, new[] { N.FloorHum, N.WoodenPiece }));
            play.Do(CaseAction.Solve(N.QuestionMoved, new[] { N.OwlPanel, N.LooseArm }));
            Assert.IsFalse(play.State.Has(CaseFact.CaseStamped), "two of three");
            var last = play.Do(CaseAction.Solve(N.QuestionEntry, new[] { N.ValeDoor, N.Cloth, N.SillSnag }));
            Assert.IsTrue(last.StampedCase);
            Assert.IsNotNull(N.Build(play.State).FinalCard);
            Assert.AreEqual(Lines.KitThanks, CaseScript.Conversation(Speaker.Kit, play.State)[0].Key);

            // Solved before the show: the porch stamps at once, and Kit does not ask.
            var early = new CasePlaythrough(All());
            early.SolveAll();
            Assert.IsFalse(early.State.Has(CaseFact.CaseStamped), "no stamp without the show");
            early.Do(CaseActionKind.LiftLatch);
            early.Do(CaseActionKind.LinkArm);
            early.Do(CaseActionKind.TurnCrank);
            var porch = early.Do(CaseActionKind.ArriveAtPorch);
            Assert.IsTrue(porch.ClosedCase && porch.StampedCase);
            CollectionAssert.DoesNotContain(early.LinesOf(Speaker.Kit), Lines.KitAsk);
        }

        [Test]
        public void EveryReachableStateCanStillBeStamped()
        {
            // No order of play deadlocks the deductions: from anywhere, finding the clues still out there, then the
            // world route, then the right sets, stamps the case (spec §3 solvability, CASE-04).
            CaseAction[] finish =
            {
                CaseAction.Talk(Speaker.Vale), CaseAction.Talk(Speaker.Bea), CaseAction.Of(CaseActionKind.SitOnBench),
                CaseAction.Of(CaseActionKind.HearKnocks), CaseAction.Of(CaseActionKind.ExamineCloth),
                CaseAction.Of(CaseActionKind.ExamineSill), CaseAction.Of(CaseActionKind.EnterAttic),
                CaseAction.Of(CaseActionKind.EnterFoyer), CaseAction.Of(CaseActionKind.LiftLatch),
                CaseAction.Of(CaseActionKind.ExamineGrille), CaseAction.Of(CaseActionKind.LinkArm),
                CaseAction.Of(CaseActionKind.TurnCrank), CaseAction.Of(CaseActionKind.ArriveAtPorch),
            };
            int count = 0;
            foreach (var start in CaseReducerTests.ReachableStates())
            {
                var play = new CasePlaythrough(start);
                foreach (var action in finish)
                {
                    play.Do(action);
                }

                play.SolveAll();
                Assert.IsTrue(play.State.Has(CaseFact.CaseStamped), "deduction deadlock from " + start);
                count++;
            }

            Assert.Greater(count, 100);
        }

        [Test]
        public void StuckMusingPointsToAPlaceNeverToAPairing()
        {
            // Nothing found for the source question: the musing points at the hall (where the floor hums).
            var knocks = CaseState.FromFacts(new[] { CaseFact.HeardKnocks });
            var musing = CaseDeduction.Musing(Source, knocks);
            CollectionAssert.AreEqual(new[] { "pg.ear", "pg.eye" }, musing.Parts, "listen, then look");
            Assert.AreEqual(N.FloorHum, musing.MissingStickerId);
            Assert.AreEqual(CaseAnchors.Foyer, musing.MissingPlaceAnchor);

            // The hum is found: the missing one is the wooden piece, in the workshop.
            var hum = CaseState.FromFacts(new[] { CaseFact.HeardKnocks, CaseFact.FloorHum });
            Assert.AreEqual(CaseAnchors.Workshop, CaseDeduction.Musing(Source, hum).MissingPlaceAnchor);

            // Every needed clue in the tray: Pip only muses, no place lights.
            var all = CaseDeduction.Musing(Entry, All());
            Assert.IsFalse(all.PointsToPlace);
            Assert.IsNull(all.MissingStickerId);

            // Entry with the door clue found: the place is where silver hangs up high (the treehouse), never "Vale + cloth".
            var vale = CaseState.FromFacts(new[] { CaseFact.ValeSpoke });
            var entry = CaseDeduction.Musing(Entry, vale);
            Assert.AreEqual(N.Cloth, entry.MissingStickerId);
            Assert.AreEqual(CaseAnchors.Treehouse, entry.MissingPlaceAnchor);

            // A lost clue is never pointed at: once the door is open the stuck door cannot be found.
            var open = CaseState.FromFacts(new[] { CaseFact.BeaSpoke, CaseFact.FrontOpen });
            Assert.AreNotEqual(N.StuckDoor, CaseDeduction.Musing(Entry, open).MissingStickerId);
            Assert.AreEqual(N.ValeDoor, CaseDeduction.Musing(Entry, open).MissingStickerId);
        }

        [Test]
        public void TheTrayIsSharedAndCountsOnlyCluesStillOutThere()
        {
            var empty = N.Build(CaseState.Empty);
            Assert.AreEqual(0, empty.Tray.Count);
            Assert.AreEqual(12, empty.OutstandingCount, "one ? per clue, never which");

            var some = N.Build(CaseState.FromFacts(new[] { CaseFact.HeardKnocks, CaseFact.ValeSpoke, CaseFact.ExaminedStreamer }));
            CollectionAssert.AreEqual(new[] { N.ValeDoor, N.Streamer }, Ids(some.Tray), "in the order found");
            Assert.AreEqual(10, some.OutstandingCount);

            var open = N.Build(CaseState.FromFacts(new[] { CaseFact.FrontOpen }));
            Assert.AreEqual(11, open.OutstandingCount, "door opened: the stuck door can no longer be found");

            var sweet = N.Build(CaseState.FromFacts(new[] { CaseFact.CaseClosed, CaseFact.FoundSweet }));
            CollectionAssert.DoesNotContain(Ids(sweet.Tray), N.Sweet, "the keepsake never goes in the tray");
        }

        private static List<string> Ids(IEnumerable<ClueSticker> stickers)
        {
            var ids = new List<string>();
            foreach (var s in stickers)
            {
                ids.Add(s.Id);
            }

            return ids;
        }

        [Test]
        public void AnswersRoundTripAndOnlyKeepRightSetsOfSolvedQuestions()
        {
            var state = All().Apply(CaseAction.Solve(N.QuestionEntry, new[] { N.BeaFlash, N.SillSnag, N.StuckDoor })).State;
            var answers = CaseAnswers.Empty.With(N.QuestionEntry, new[] { N.BeaFlash, N.SillSnag, N.StuckDoor });
            var data = CaseSaveData.FromState(state, CaseAnchors.Plaza, answers);
            var read = CaseSaveCodec.Read(CaseSaveCodec.Serialize(data));
            Assert.AreEqual(CaseSaveStatus.Ok, read.Status);
            Assert.AreEqual(CaseSaveData.CurrentSchemaVersion, read.FoundSchemaVersion);
            CollectionAssert.AreEqual(new[] { N.BeaFlash, N.SillSnag, N.StuckDoor }, read.Data.ToAnswers().Get(N.QuestionEntry));
            Assert.IsEmpty(read.Issues);

            var page = N.Build(read.Data.ToState(), read.Data.ToAnswers());
            CollectionAssert.AreEqual(new[] { N.StuckDoor, N.SillSnag, N.BeaFlash }, page.Questions[1].AnswerStickers,
                "the comic is drawn from the child's own set, in idea order");

            // A wrong set, an unsolved question or garbage never survive a read.
            var json = CaseSaveCodec.Serialize(CaseSaveData.FromState(state, CaseAnchors.Plaza)).Replace(
                "\"answers\": [", "\"answers\": [\"c01.note.q_source=c01.sticker.floor_hum+c01.sticker.wooden_piece\", " +
                "\"c01.note.q_entry=c01.sticker.streamer+c01.sticker.cloth+c01.sticker.vale_door\", \"junk\", ");
            var repaired = CaseSaveCodec.Read(json);
            Assert.AreEqual(CaseSaveStatus.Ok, repaired.Status);
            Assert.IsNull(repaired.Data.ToAnswers().Get(N.QuestionSource), "the source question is not solved");
            Assert.IsTrue(CaseDeduction.IsCorrect(Entry, repaired.Data.ToAnswers().Get(N.QuestionEntry)), "derived again");
        }

        [Test]
        public void Schema1SavesMigrateOnceAndKeepWhatTheyHadSeen()
        {
            // Schema 1: the notebook had answered the entry and source questions by proof.
            string v1 = "{\"schemaVersion\":1,\"caseId\":\"c01\",\"facts\":[\"c01.arrival.knocks_heard\",\"c01.plaza.bea_spoke\"," +
                        "\"c01.treehouse.cloth_examined\",\"c01.attic.entered\",\"c01.workshop.cam_seen\"],\"anchorId\":\"c01.anchor.workshop\"," +
                        "\"notebook\":[\"c01.note.knocks\",\"c01.note.q_entry\"]}";
            var read = CaseSaveCodec.Read(v1);
            Assert.AreEqual(CaseSaveStatus.Ok, read.Status);
            Assert.AreEqual(1, read.FoundSchemaVersion);
            CollectionAssert.Contains(read.Issues, "migrated schema 1 to 2");
            var state = read.Data.ToState();
            Assert.IsTrue(state.Has(CaseFact.SolvedEntry), "attic entry had answered it");
            Assert.IsTrue(state.Has(CaseFact.SolvedSource), "seeing the cam had answered it");
            Assert.IsFalse(state.Has(CaseFact.SolvedMoved));
            Assert.AreEqual(CaseAnchors.Workshop, read.Data.AnchorId);
            Assert.IsTrue(CaseDeduction.IsCorrect(Entry, read.Data.ToAnswers().Get(N.QuestionEntry)), "a comic was derived");
            Assert.AreEqual(N.Cloth, read.Data.ToAnswers().Get(N.QuestionEntry)[1], "from the stickers that save had found first");

            // Written back, it is schema 2 and reading it again changes nothing (the migration ran once).
            string v2 = CaseSaveCodec.Serialize(read.Data);
            StringAssert.Contains("\"schemaVersion\": 2", v2);
            var again = CaseSaveCodec.Read(v2);
            Assert.IsEmpty(again.Issues);
            Assert.AreEqual(v2, CaseSaveCodec.Serialize(again.Data));

            // A closed schema-1 case keeps its stamp; an untouched one gains nothing.
            string closed = "{\"schemaVersion\":1,\"caseId\":\"c01\",\"facts\":[\"c01.foyer.front_open\",\"c01.workshop.owl_tested\"," +
                            "\"c01.porch.case_closed\"],\"anchorId\":\"c01.anchor.plaza\",\"notebook\":[]}";
            var stamped = CaseSaveCodec.Read(closed).Data.ToState();
            Assert.IsTrue(stamped.HasAll(CaseDefinition.StampRequires));
            Assert.IsTrue(stamped.Has(CaseFact.CaseStamped));
            Assert.IsNotNull(N.Build(stamped).FinalCard);

            string fresh = "{\"schemaVersion\":1,\"caseId\":\"c01\",\"facts\":[\"c01.arrival.knocks_heard\"],\"anchorId\":\"c01.anchor.plaza\",\"notebook\":[]}";
            Assert.AreEqual(1, CaseSaveCodec.Read(fresh).Data.ToState().Count);

            // A schema-2 save with the same proofs is never "helped": its questions stay open.
            string modern = CaseSaveCodec.Serialize(CaseSaveData.FromState(
                CaseState.FromFacts(new[] { CaseFact.EnteredByWindow, CaseFact.SawCam }), CaseAnchors.Plaza));
            var kept = CaseSaveCodec.Read(modern).Data.ToState();
            Assert.IsFalse(kept.Has(CaseFact.SolvedEntry));
            Assert.IsFalse(kept.Has(CaseFact.SolvedSource));
        }

        [Test]
        public void MigratedSavesSurviveACrashAtAnyStepOfTheFirstSchema2Write()
        {
            // The schema-1 file is the latest copy; the first autosave after loading writes schema 2 through the
            // atomic slot store. A crash at any file operation leaves a readable save that still holds the derived
            // deduction (either the old schema-1 copy, migrated again, or the new schema-2 one).
            const string dir = "saves";
            var baseline = new MemorySaveFileSystem();
            baseline.Files[SaveSlotPaths.ForCase(dir).Latest] = "{\"schemaVersion\":1,\"caseId\":\"c01\"," +
                "\"facts\":[\"c01.attic.entered\",\"c01.attic.sill_examined\"],\"anchorId\":\"c01.anchor.attic\",\"notebook\":[]}";
            var first = new CaseSaveStore(baseline.Clone(), dir).Load();
            Assert.IsTrue(first.CanContinue);
            Assert.IsTrue(first.State.Has(CaseFact.SolvedEntry));
            Assert.IsTrue(CaseDeduction.IsCorrect(Entry, first.Answers.Get(N.QuestionEntry)));

            for (int crashAt = 0; crashAt < 6; crashAt++)
            {
                var fs = baseline.Clone();
                var store = new CaseSaveStore(fs, dir);
                var resume = store.Load();
                fs.CrashAfter = fs.Operations + crashAt;
                try
                {
                    store.Save(resume.State, resume.AnchorId, resume.Answers);
                }
                catch (SimulatedCrash)
                {
                }

                fs.CrashAfter = -1;
                var after = new CaseSaveStore(fs, dir).Load();
                Assert.IsTrue(after.CanContinue, "crash at " + crashAt + " -> " + after.Status);
                Assert.IsTrue(after.State.Has(CaseFact.SolvedEntry), "crash at " + crashAt);
                Assert.IsTrue(after.State.Has(CaseFact.ExaminedSill));
                Assert.IsTrue(CaseDeduction.IsCorrect(Entry, after.Answers.Get(N.QuestionEntry)));
            }

            var done = baseline.Clone();
            var writer = new CaseSaveStore(done, dir);
            var loaded = writer.Load();
            Assert.IsTrue(writer.Save(loaded.State, loaded.AnchorId, loaded.Answers));
            StringAssert.Contains("\"schemaVersion\": 2", done.Files[writer.Paths.Latest]);
        }
    }
}
