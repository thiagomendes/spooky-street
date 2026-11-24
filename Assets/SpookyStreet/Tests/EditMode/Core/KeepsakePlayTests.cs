using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpookyStreet.Core.Audio;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Cutscenes;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Keepsakes;
using SpookyStreet.Core.Localization;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Core.World;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// Keepsakes in play (S004 A2): the bat mobile shows exactly the found bats, the trunk and the mobile sit on Pip's
    /// porch clear of the route, the bats and the clue, the trunk opens only on earned pieces, its screen is sound in
    /// every state, a page assembles once, the Continue line counts only, and each costume remark is a real line.
    /// </summary>
    public class KeepsakePlayTests
    {
        private static KeepsakeState Found(int count)
        {
            var state = KeepsakeState.Empty;
            for (int i = 1; i <= count; i++)
            {
                state = state.Find(PaperBatLayout.IdFor(i)).State;
            }

            return state;
        }

        [Test]
        public void TheMobileHangsOneHookPerFoundBatAndLightsAtTenTwentyThirty()
        {
            for (int count = 0; count <= 30; count++)
            {
                int hooks = Enumerable.Range(1, KeepsakeArt.MobileHooks).Count(h => BatMobileRules.HookShown(h, count));
                Assert.AreEqual(count, hooks, count + " bats: one hook each, never an empty hook");
                for (int ring = 1; ring <= KeepsakeArt.MobileRings; ring++)
                {
                    Assert.AreEqual(count >= ring * 10, BatMobileRules.RingClosed(ring, count), count + " ring " + ring);
                    Assert.AreEqual(count >= ring * 10, BatMobileRules.LanternLit(ring, count), count + " lantern " + ring);
                    bool hasBat = count > (ring - 1) * 10;
                    Assert.AreEqual(ring == 1 || hasBat, BatMobileRules.RingShown(ring, count), count + " hoop " + ring);
                }
            }

            Assert.AreEqual(30, BatMobileRules.HooksShown(99));
            Assert.AreEqual(0, BatMobileRules.HooksShown(-1));
            Assert.IsFalse(BatMobileRules.HookShown(0, 5));
        }

        [Test]
        public void TheTrunkAndTheMobileStandOnPipsPorchClearOfEverything()
        {
            // The trunk lies on the deck, inside its back half, clear of the porch rails and the candy-bowl crate.
            float half = KeepsakeArt.TrunkSize * 0.5f;
            Assert.GreaterOrEqual(KeepsakePorch.TrunkX - half, KeepsakePorch.DeckMinX + 0.3f);
            Assert.LessOrEqual(KeepsakePorch.TrunkX + half, KeepsakePorch.DeckMaxX - 0.3f);
            Assert.GreaterOrEqual(KeepsakePorch.TrunkZ - half, KeepsakePorch.DeckMinZ);
            Assert.Less(KeepsakePorch.TrunkFrontZ, KeepsakePorch.DeckMaxZ - 1f, "the porch front stays open for the steps");
            var crate = EnvironmentLayout.Street.First(p => p.Model == EnvironmentLayout.Crate);
            Assert.Greater(Math.Abs(crate.X - KeepsakePorch.TrunkX), crate.HalfX + half + 0.6f, "Pip passes between them");

            // Pip's opening walk starts beside it and the spawn never offers it (the Use disc stays Roll at the start).
            var back = C01Cutscenes.PorchBack;
            Assert.Greater(Math.Abs(back.X - KeepsakePorch.TrunkX) - half, 0.28f + 0.2f, "the opening walk is clear");
            var spawn = C01Cutscenes.PorchSpawn;
            Assert.IsFalse(KeepsakePorch.TrunkInReach(spawn.X, spawn.Y, spawn.Z), "not offered where play starts");
            var spot = KeepsakePorch.DressingSpot;
            Assert.IsTrue(KeepsakePorch.TrunkInReach(spot.X, spot.Y, spot.Z), "offered where Pip dresses");
            Assert.IsFalse(KeepsakePorch.TrunkInReach(spot.X, spot.Y + 1.5f, spot.Z), "not from the rail tops");
            Assert.IsFalse(KeepsakePorch.TrunkInReach(KeepsakePorch.TrunkX, KeepsakePorch.DeckY, KeepsakePorch.TrunkZ - 0.6f),
                "not from behind the porch");

            // Clear of every keepsake bat (a box with a bat on it would read as a marker) and of every case target.
            foreach (var bat in PaperBatLayout.All)
            {
                float dx = bat.X - KeepsakePorch.TrunkX, dz = bat.Z - KeepsakePorch.TrunkZ;
                Assert.Greater(Math.Sqrt(dx * dx + dz * dz), 1.0, bat + " by the trunk");
            }

            foreach (var point in CaseStage.AllPoints)
            {
                float dx = point.Position.X - KeepsakePorch.TrunkX, dz = point.Position.Z - KeepsakePorch.TrunkFrontZ;
                Assert.Greater(Math.Sqrt(dx * dx + dz * dz), KeepsakePorch.TrunkReach + 0.5,
                    point.Id + " shares its Use reach with the trunk (the owl booth's knock included)");
            }

            // The mobile hangs over the deck from the east rail's pole, above Pip's head (1.2 m tall on the deck).
            Assert.That(KeepsakePorch.MobileX, Is.InRange(KeepsakePorch.DeckMinX, KeepsakePorch.DeckMaxX));
            Assert.That(KeepsakePorch.MobileZ, Is.InRange(KeepsakePorch.DeckMinZ, KeepsakePorch.DeckMaxZ));
            Assert.Greater(KeepsakePorch.MobileBottom, KeepsakePorch.DeckY + 1.5f, "Pip walks under it");
            Assert.Greater(KeepsakePorch.PoleTop, KeepsakePorch.MobileY);
            Assert.Less(Math.Abs(KeepsakePorch.MobileX - KeepsakePorch.PoleX), 0.5f, "a short arm");
            foreach (var bat in PaperBatLayout.All)
            {
                float dx = bat.X - KeepsakePorch.MobileX, dz = bat.Z - KeepsakePorch.MobileZ;
                bool under = Math.Sqrt(dx * dx + dz * dz) < KeepsakeArt.MobileRadius + 0.3f;
                Assert.IsFalse(under && bat.Y > KeepsakePorch.MobileBottom - 0.5f, bat + " hangs among the mobile's bats");
            }

            // The dressing camera looks at Pip from the street side, the trunk behind her.
            Assert.Greater(KeepsakePorch.DressingCamera.Z, KeepsakePorch.DressingSpot.Z + 1.5f);
            Assert.Less(KeepsakePorch.DressingLook.Z, KeepsakePorch.DressingSpot.Z);
        }

        [Test]
        public void TheTrunkOpensOnlyOnEarnedPiecesAndItsScreenIsSoundInEveryState()
        {
            Assert.IsFalse(KeepsakePorch.TrunkOffered(KeepsakeState.Empty));
            Assert.IsFalse(KeepsakePorch.TrunkOffered(Found(9)), "no empty trunk");
            Assert.IsTrue(KeepsakePorch.TrunkOffered(Found(10)));
            foreach (int count in new[] { 10, 15, 20, 29, 30 })
            {
                var state = Found(count);
                var graph = DressUpFocus.Build(state);
                CollectionAssert.IsEmpty(graph.Validate(), count + " bats");
                Assert.AreEqual(DressUpFocus.Close, graph.Exit);
                var shown = CostumePieces.All.Where(p => graph.IsEnabled(DressUpFocus.Piece(p))).ToList();
                CollectionAssert.AreEqual(state.EarnedPieces, shown, count + ": only earned pieces, never a locked card");
                Assert.AreEqual(DressUpFocus.Piece(CostumePieces.StarCape), graph.Start(), "focus starts on the first piece");
                // Down walks the pieces and comes back up to close.
                string at = graph.Start();
                var seen = new List<string> { at };
                for (int i = 0; i < 4; i++)
                {
                    at = graph.Move(at, UiDirection.Down);
                    seen.Add(at);
                }

                foreach (var piece in shown)
                {
                    CollectionAssert.Contains(seen, DressUpFocus.Piece(piece));
                }

                Assert.AreEqual(DressUpFocus.Close, graph.Move(graph.Start(), UiDirection.Up));
            }

            var names = CostumePieces.All.Select(DressUpFocus.NameKey).ToList();
            CollectionAssert.AllItemsAreUnique(names);
            var table = StringTable.Parse(LocalizationTests.BundledEnglishJson());
            foreach (string key in names.Append(LocalizationKeys.Verbs.DressUp).Append(LocalizationKeys.Keepsakes.TrunkClose))
            {
                Assert.IsTrue(table.Contains(key), key);
                CollectionAssert.Contains(LocalizationKeys.All, key);
            }
        }

        [Test]
        public void APageAssemblesOnceTheNextTimeTheNotebookOpens()
        {
            var notebook = new KeepsakeNotebook();
            CollectionAssert.IsEmpty(notebook.TakePending());
            notebook.Completed(0);
            notebook.Completed(1);
            notebook.Completed(1);
            CollectionAssert.AreEqual(new[] { 1 }, notebook.TakePending());
            CollectionAssert.IsEmpty(notebook.TakePending(), "once");
            notebook.Completed(2);
            notebook.Completed(3);
            notebook.Completed(4);
            notebook.Reset();
            CollectionAssert.IsEmpty(notebook.TakePending(), "a New game forgets");

            Assert.IsFalse(KeepsakeNotebook.HasPage(KeepsakeState.Empty), "no keepsakes page before a bat");
            Assert.IsTrue(KeepsakeNotebook.HasPage(Found(1)));
            Assert.IsTrue(KeepsakeNotebook.ShowsStack(Found(7)));
            Assert.IsFalse(KeepsakeNotebook.ShowsStack(Found(10)), "the stack went into the page");
            Assert.IsTrue(KeepsakeNotebook.ShowsStack(Found(12)));
            Assert.IsFalse(KeepsakeNotebook.ShowsStack(Found(30)));
        }

        [Test]
        public void TheContinueLineCountsBatsOnlyOnceOneIsFound()
        {
            Assert.IsFalse(KeepsakeTitle.ShowsBatCount(KeepsakeState.Empty), "never '0 paper bats'");
            Assert.IsFalse(KeepsakeTitle.ShowsBatCount(null));
            Assert.IsTrue(KeepsakeTitle.ShowsBatCount(Found(1)));
            var table = StringTable.Parse(LocalizationTests.BundledEnglishJson());
            var catalog = new LocalizationCatalog(table);
            string line = catalog.Plural(LocalizationKeys.TitleContinueDetail, 12, "How?", 12);
            StringAssert.Contains("12", line);
            StringAssert.DoesNotContain("30", line, "a count, never a total");
            string stack = catalog.Plural(LocalizationKeys.Keepsakes.NotebookBatStack, 3, 3);
            Assert.AreEqual("3 paper bats", stack);
        }

        [Test]
        public void EachRemarkIsASpokenCaseLineWithItsVignetteAndVoice()
        {
            var table = StringTable.Parse(LocalizationTests.BundledEnglishJson());
            foreach (var remark in CostumeRemarks.All)
            {
                string key = remark.LineKey;
                Assert.IsNotNull(CaseScript.ConditionFor(key), key + " is a case line");
                StringAssert.StartsWith("c01.line." + remark.Speaker.ToString().ToLowerInvariant() + ".", key, "said by its friend");
                Assert.IsTrue(table.Contains(key), key);
                Assert.IsTrue(LineVoices.Has(key), key + " has a voice");
                Assert.IsTrue(LinePortraits.TryGet(remark.Line, out var portrait), key + " has a portrait");
                Assert.AreEqual(remark.Speaker, portrait.Speaker);
                var visual = C01Vignettes.For(key);
                Assert.IsNotNull(visual, key);
                Assert.IsFalse(visual.UsesV1Strip, key + " has its own vignette");
                CollectionAssert.IsEmpty(VignetteValidator.Validate(C01Vignettes.Get(visual.VignetteId)), key);
                Assert.AreEqual(remark.AfterStamp, CaseScript.ConditionFor(key).Requires.Contains(CaseFact.CaseStamped), key);
                // Conversation never picks a remark: the director adds it once when Pip wears the piece.
                var stamped = CaseScenarioTests.PlayCanonical().State;
                foreach (var state in new[] { CaseState.Empty, stamped })
                {
                    foreach (var speaker in new[] { Speaker.Bea, Speaker.Vale, Speaker.June, Speaker.Kit })
                    {
                        Assert.IsFalse(CaseScript.Conversation(speaker, state).Any(l => l.Key == key), key);
                    }
                }
            }

            CollectionAssert.AllItemsAreUnique(CostumeRemarks.All.Select(r => r.Speaker), "a different friend for each piece");
        }
    }
}
