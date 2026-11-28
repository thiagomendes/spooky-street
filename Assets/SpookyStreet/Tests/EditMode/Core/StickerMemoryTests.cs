using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Cutscenes;
using SpookyStreet.Core.Localization;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Core.World;
using MemoryKeys = SpookyStreet.Core.Localization.LocalizationKeys.StickerMemory;
using N = SpookyStreet.Core.Case.CaseNotebook;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// D32 (owner, 2026-11-24): every notebook sticker opens a short memory of where the child found it, with one
    /// descriptive caption. It recalls what and where, never what it means (D26), and inspecting is free: it never
    /// changes the case, places a sticker or counts a try.
    /// </summary>
    public class StickerMemoryTests
    {
        private static readonly CaseFact[] EveryClue =
        {
            CaseFact.HeardKnocks, CaseFact.BeaSpoke, CaseFact.ValeSpoke, CaseFact.CheckedView, CaseFact.TriedDoor,
            CaseFact.ExaminedStreamer, CaseFact.ExaminedCloth, CaseFact.ExaminedSill, CaseFact.FloorHum, CaseFact.SawCam,
            CaseFact.OwlPanel, CaseFact.OwlBooth, CaseFact.LooseArm,
        };

        private static StringTable English => StringTable.Parse(LocalizationTests.BundledEnglishJson());

        private static StringTable Portuguese => StringTable.Parse(File.ReadAllText(
            Path.Combine(Path.GetDirectoryName(LocalizationTests.BundledEnglishPath()), "pt-BR.json")));

        private static List<string> Words(string text) =>
            Regex.Matches(text.ToLowerInvariant(), @"[\p{L}']+").Cast<Match>().Select(m => m.Value).ToList();

        [Test]
        public void EveryTrayStickerOfCase01HasOneMemoryAndACaptionTheDecoyIncluded()
        {
            var tray = N.Stickers.Where(s => s.InTray).Select(s => s.Id).ToList();
            CollectionAssert.AreEquivalent(tray, StickerMemories.All.Select(m => m.StickerId), "one memory per tray sticker");
            Assert.AreEqual(12, StickerMemories.All.Count);
            CollectionAssert.Contains(StickerMemories.All.Select(m => m.StickerId).ToList(), N.Streamer,
                "the honest decoy gets a memory like every clue (D26 §2.7)");
            CollectionAssert.AllItemsAreUnique(StickerMemories.All.Select(m => m.CaptionKey));
            CollectionAssert.AreEquivalent(MemoryKeys.Captions, StickerMemories.All.Select(m => m.CaptionKey));
            Assert.IsNull(StickerMemories.For(N.Sweet), "the saved sweet is a keepsake, never in the tray");
            Assert.IsNull(StickerMemories.For("c01.sticker.nope"));

            var english = English;
            var portuguese = Portuguese;
            foreach (string key in MemoryKeys.All)
            {
                CollectionAssert.Contains(LocalizationKeys.All, key);
                Assert.IsTrue(english.TryGetEntry(key, out var en), "en.json is missing " + key);
                Assert.IsTrue(portuguese.TryGetEntry(key, out var pt), "pt-BR.json is missing " + key);
                Assert.IsNotEmpty(en.Value, key);
                Assert.IsNotEmpty(pt.Value, key);
                Assert.That(en.Context, Is.Not.Null.And.Length.GreaterThan(40), key + " needs translator context");
                Assert.AreEqual(en.Context, pt.Context, key + ": identical translator context");
                Assert.AreNotEqual(en.Value, pt.Value, key + " is translated");
            }
        }

        [Test]
        public void CaptionsDescribeWhatAndWhereAndNeverConclude()
        {
            var englishBanned = new[]
            {
                "because", "so june", "that's why", "that is why", "the answer", "solved", "got in", "it was", "means",
                "must be", "must have", "climbed", "sneaked", "june", "cape", "way in", "goes with", "belongs",
            };
            var portugueseBanned = new[]
            {
                "porque", "por isso", "a resposta", "resolvid", "entrou", "foi a june", "foi ela", "june", "capa",
                "significa", "quer dizer", "deve ser", "com certeza", "subiu", "combina",
            };
            var fear = new[]
            {
                "scary", "scared", "afraid", "fear", "monster", "danger", "dangerous", "dead", "death", "die", "ghost",
                "blood", "skull", "kill", "nightmare", "scream", "terror", "perigo", "perigoso", "perigosa", "monstro",
                "morto", "morta", "morte", "morrer", "medo", "assustador", "assustadora", "assusta", "terror", "sangue",
                "caveira", "matar", "fantasma", "pesadelo", "grito",
            };
            // D31: visual landmarks, never compass points.
            var compass = new[] { "north", "south", "east", "west", "norte", "sul", "leste", "oeste", "nordeste", "noroeste", "sudeste", "sudoeste" };
            var english = English;
            var portuguese = Portuguese;
            foreach (string key in MemoryKeys.Captions)
            {
                english.TryGet(key, out string en);
                portuguese.TryGet(key, out string pt);
                foreach (string phrase in englishBanned)
                {
                    StringAssert.DoesNotContain(phrase, en.ToLowerInvariant(), key + ": describes, never concludes");
                }

                foreach (string phrase in portugueseBanned)
                {
                    StringAssert.DoesNotContain(phrase, pt.ToLowerInvariant(), key + ": descreve, nunca conclui");
                }

                foreach (string word in Words(en).Concat(Words(pt)))
                {
                    CollectionAssert.DoesNotContain(fear, word, key + ": no fear words");
                    CollectionAssert.DoesNotContain(compass, word, key + ": no compass points (D31)");
                }

                Assert.LessOrEqual(en.Length, 56, key + ": one short line under the picture");
                foreach (string control in new[] { "tap", "drag", "click", "press", "button", "toque", "arraste", "aperte", "botão" })
                {
                    CollectionAssert.DoesNotContain(Words(en).Concat(Words(pt)).ToList(), control, key + ": a memory names no control");
                }
            }
        }

        [Test]
        public void EachShotFramesThePlaceWhereTheStickerIsFound()
        {
            foreach (var memory in StickerMemories.All)
            {
                var sticker = N.Sticker(memory.StickerId);
                string id = memory.StickerId;
                Assert.IsTrue(CaseStage.TryGetAnchor(sticker.PlaceAnchor, out var place), id);
                bool inside = CaseRouteLayout.Inside(place.X, place.Z);
                Assert.AreEqual(inside, memory.Indoors, id + ": the shot stands on the side of the house walls where the clue is found");
                foreach (var pose in new[] { memory.From, memory.To })
                {
                    Assert.AreEqual(inside, CaseRouteLayout.Inside(pose.Eye.X, pose.Eye.Z), id + ": eye side");
                    Assert.AreEqual(inside, CaseRouteLayout.Inside(pose.Aim.X, pose.Aim.Z), id + ": aim side");
                    float d = Distance(pose.Eye, pose.Aim);
                    Assert.That(d, Is.InRange(1.0f, 22f), id + ": the camera looks at something near");
                    Assert.That(pose.Fov, Is.InRange(20f, 60f), id);
                    Assert.That(pose.Eye.Y, Is.InRange(0.5f, 5f), id + ": a child's or a witness's eye, never a bird's");
                }

                float drift = Distance(memory.From.Eye, memory.To.Eye);
                Assert.Greater(drift + Math.Abs(memory.From.Fov - memory.To.Fov), 0.05f, id + ": the memory moves a little");
                Assert.Less(drift, 0.6f, id + ": a gentle drift, never a fly-through");
            }

            // The streamer's own shot looks at the bicycle the decoy hangs on.
            var streamer = StickerMemories.For(N.Streamer);
            var bike = ClueAttractions.Get(ClueAttractions.Streamer).At;
            Assert.Less(Math.Abs(streamer.To.Aim.X - bike.X) + Math.Abs(streamer.To.Aim.Z - bike.Z), 0.5f);

            // The door memories show the door shut as it was; the workshop ones look through the near wall like the grille.
            Assert.AreEqual(MemoryLook.DoorShut, StickerMemories.For(N.StuckDoor).Look);
            Assert.AreEqual(MemoryLook.DoorShut, StickerMemories.For(N.ValeDoor).Look);
            Assert.AreEqual(MemoryLook.NookOpen, StickerMemories.For(N.LooseArm).Look);
            Assert.AreEqual(MemoryLook.None, StickerMemories.For(N.Cloth).Look);
        }

        [Test]
        public void SmallThingsAreFramedCloseAndTheDecoyIsNeverFramedTighterThanAClue()
        {
            // S004 polish (frames 90 and 92): the silver scrap (about 0.3 m) and the foil moons on the streamer were too
            // small to read on a phone. Span: how many metres of the place the settled picture shows, top to bottom.
            float Span(StickerMemory m) => 2f * Distance(m.To.Eye, m.To.Aim) * (float)Math.Tan(m.To.Fov * Math.PI / 360.0);

            var cloth = StickerMemories.For(N.Cloth);
            var snag = new StagePoint(CaseRouteLayout.ClothX, CaseRouteLayout.ClothY, CaseRouteLayout.ClothZ);
            Assert.Less(Distance(cloth.To.Aim, snag), 0.25f, "the cloth memory looks at the scrap on the post");
            Assert.Less(Span(cloth), 0.7f, "the scrap fills about half the picture's height");
            var reveal = C01Cutscenes.StickerShot(N.Cloth, ShotTransition.Cut);
            Assert.Less(Span(cloth), 2f * Distance(reveal.End.Eye, reveal.End.Aim) * (float)Math.Tan(reveal.End.Fov * Math.PI / 360.0),
                "closer than the reveal's shot of the same place");

            var streamer = StickerMemories.For(N.Streamer);
            Assert.Less(Span(streamer), 0.75f, "the strings under the handlebar read");
            Assert.Less(streamer.To.Eye.Y, 1f, "at a child's eye height");
            Assert.Less(streamer.To.Aim.Y, streamer.To.Eye.Y, "looking at the handlebar's strings, not at Bea");

            // D26 §2.7: the decoy gets what a clue gets and no more, so its picture is never tighter than the tightest
            // clue memory (and nothing else differs: same card, caption, look and timing).
            float tightestClue = StickerMemories.All.Where(m => m.StickerId != N.Streamer).Min(m => Span(m));
            Assert.GreaterOrEqual(Span(streamer), tightestClue - 1e-3f, "the decoy is never singled out by a closer framing");
            Assert.AreEqual(MemoryLook.None, streamer.Look);
        }

        [Test]
        public void TheCardPlaysAShortShotHoldsThenFadesAndReducedMotionShowsAStillFrame()
        {
            Assert.That(StickerMemories.ShotSeconds, Is.InRange(2f, 3f), "D32: a 2–3 second memory");
            Assert.That(StickerMemories.CardSeconds, Is.LessThanOrEqualTo(4.5f), "the card never slows the notebook down for long");
            var memory = StickerMemories.For(N.Cloth);

            var p = new StickerMemoryPlayback(memory, false);
            Assert.AreEqual(0f, p.Progress, 1e-5f);
            Assert.IsTrue(p.Moving);
            AssertPose(memory.From, p.Pose);
            p.Advance(StickerMemories.ShotSeconds * 0.5f);
            Assert.AreEqual(0.5f, p.Progress, 1e-4f);
            p.Advance(0f);
            Assert.AreEqual(0.5f, p.Progress, 1e-4f, "the pause (no time) holds the frame");
            p.Advance(StickerMemories.ShotSeconds * 0.5f);
            Assert.AreEqual(1f, p.Progress, 1e-5f);
            Assert.IsFalse(p.Moving);
            AssertPose(memory.To, p.Pose);
            Assert.AreEqual(1f, p.Alpha);
            p.Advance(StickerMemories.HoldSeconds + StickerMemories.FadeSeconds * 0.5f);
            Assert.AreEqual(0.5f, p.Alpha, 1e-3f, "it fades away by itself");
            Assert.IsFalse(p.IsDone);
            p.Advance(10f);
            Assert.IsTrue(p.IsDone);
            Assert.AreEqual(0f, p.Alpha);

            var still = new StickerMemoryPlayback(memory, true);
            Assert.IsFalse(still.Moving, "reduced motion: a still frame");
            Assert.AreEqual(1f, still.Progress);
            AssertPose(memory.To, still.Pose);
            still.Advance(StickerMemories.ShotSeconds);
            AssertPose(memory.To, still.Pose);
            Assert.AreEqual(1f, still.Alpha);
            still.Advance(StickerMemories.HoldSeconds + 0.01f);
            Assert.AreEqual(0f, still.Alpha, "it cuts away rather than fading");
            Assert.Throws<ArgumentNullException>(() => new StickerMemoryPlayback(null, false));
        }

        [Test]
        public void InspectingNeverChangesTheCaseNeverPlacesAndNeverCountsATry()
        {
            var state = CaseState.FromFacts(EveryClue);
            var factsBefore = state.Facts.ToList();
            var found = N.FoundIds(state);
            var question = N.Question(N.QuestionEntry);
            var board = new DeductionBoard(question);

            // One wrong try (the decoy) so there is a count to protect, then a sticker left in a slot.
            board.Place(0, N.Streamer, found);
            board.Place(1, N.Cloth, found);
            board.Place(2, N.BeaFlash, found);
            Assert.AreEqual(DeductionVerdict.Wrong, board.Check(found).Verdict);
            Assert.AreEqual(1, board.Tries);
            board.Place(1, N.SillSnag, found);
            var slotsBefore = board.Slots.ToList();

            var tray = N.Found(state, true).Select(s => s.Id).Where(id => board.SlotOf(id) < 0).ToList();
            var cursor = new DeductionCursor();
            cursor.Set(CursorRow.Tray, tray.IndexOf(N.ValeDoor));
            Assert.AreEqual(CursorResult.Picked, cursor.Use(board, tray, found));
            string held = cursor.Held;

            foreach (var sticker in N.Stickers.Where(s => s.InTray))
            {
                var memory = StickerMemories.Open(sticker.Id, state);
                Assert.IsNotNull(memory, sticker.Id + " is found, so it opens");
                Assert.AreEqual(sticker.Id, memory.StickerId);
            }

            // Inspect at every cursor position: the target is read, nothing else moves.
            for (int i = 0; i <= tray.Count; i++)
            {
                cursor.Set(CursorRow.Tray, i);
                string target = cursor.InspectTarget(board, tray);
                Assert.AreEqual(i < tray.Count ? tray[i] : null, target);
                Assert.AreEqual(i, cursor.Index);
            }

            for (int k = 0; k < board.SlotCount; k++)
            {
                cursor.Set(CursorRow.Slots, k);
                Assert.AreEqual(board.Slots[k], cursor.InspectTarget(board, tray), "a sticker sitting in a slot inspects too");
            }

            Assert.IsNull(new DeductionCursor().InspectTarget(null, new string[0]), "an empty tray");
            Assert.AreEqual(held, cursor.Held, "inspecting never drops or picks");
            CollectionAssert.AreEqual(slotsBefore, board.Slots, "inspecting never places or sends home");
            Assert.AreEqual(1, board.Tries, "inspecting never counts a try");
            CollectionAssert.AreEqual(factsBefore, state.Facts, "inspecting never records a fact");

            // Placement still works right after: the held sticker goes into the empty slot.
            cursor.Set(CursorRow.Slots, board.FirstEmpty());
            Assert.AreEqual(CursorResult.Placed, cursor.Use(board, tray, found));
            Assert.AreEqual(N.ValeDoor, board.Slots[0]);

            // Before it is found there is nothing to recall.
            Assert.IsNull(StickerMemories.Open(N.Cloth, CaseState.FromFacts(new[] { CaseFact.HeardKnocks })));
            Assert.IsNull(StickerMemories.Open(N.Sweet, CaseState.FromFacts(new[] { CaseFact.FoundSweet })));
        }

        [Test]
        public void InspectHasItsOwnGlyphOnEveryScheme()
        {
            var pad = ControlGlyphs.For(ControlScheme.Gamepad, ControlPrompt.Inspect);
            Assert.AreEqual(GlyphShape.PadFace, pad.Shape);
            Assert.AreEqual(PadButton.West, pad.Pad);
            Assert.AreEqual(ControlGlyphs.PadInspect, pad.BindingPath);
            var key = ControlGlyphs.For(ControlScheme.KeyboardMouse, ControlPrompt.Inspect);
            Assert.AreEqual(GlyphShape.Key, key.Shape);
            Assert.AreEqual("<Keyboard>/f", key.BindingPath);
            Assert.IsTrue(ControlGlyphs.For(ControlScheme.Touch, ControlPrompt.Inspect).IsTouch, "touch presses the sticker itself");

            // Never the same button or key as another notebook control.
            foreach (var other in new[] { ControlPrompt.Submit, ControlPrompt.Back, ControlPrompt.PageLeft, ControlPrompt.PageRight, ControlPrompt.Rewatch })
            {
                Assert.AreNotEqual(ControlGlyphs.For(ControlScheme.Gamepad, other).BindingPath, pad.BindingPath, other.ToString());
                Assert.AreNotEqual(ControlGlyphs.For(ControlScheme.KeyboardMouse, other).BindingPath, key.BindingPath, other.ToString());
            }
        }

        private static float Distance(StagePoint a, StagePoint b)
        {
            float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }

        private static void AssertPose(SpookyStreet.Core.Cutscenes.CamPose expected, SpookyStreet.Core.Cutscenes.CamPose actual)
        {
            Assert.AreEqual(expected.Eye.X, actual.Eye.X, 1e-4f);
            Assert.AreEqual(expected.Eye.Y, actual.Eye.Y, 1e-4f);
            Assert.AreEqual(expected.Eye.Z, actual.Eye.Z, 1e-4f);
            Assert.AreEqual(expected.Aim.X, actual.Aim.X, 1e-4f);
            Assert.AreEqual(expected.Fov, actual.Fov, 1e-4f);
        }
    }
}
