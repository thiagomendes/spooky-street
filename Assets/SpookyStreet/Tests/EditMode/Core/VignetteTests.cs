using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Nonverbal;
using Lines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;

namespace SpookyStreet.Tests.Core
{
    /// <summary>Non-verbal UX v2: the vignette format, its evaluator and the case 01 data (decision D23).</summary>
    public class VignetteTests
    {
        private static Vignette Simple(params VignetteCue[] cues) => new Vignette("vg.test", 2f, 0,
            new[] { new VignetteBeat(0f, 2f) },
            new[]
            {
                new VignetteTrack("a", "pg.june",
                    new VignetteKey(0.5f, new PuppetPose(0f, 0f, 1f, 0f, 1f)),
                    new VignetteKey(1.5f, new PuppetPose(1f, 0.5f, 2f, 90f, 0f), Ease.Linear)),
            },
            cues);

        // ------------------------------------------------------------------------------- evaluator

        [Test]
        public void SampleInterpolatesBetweenKeysWithTheArrivingKeysEase()
        {
            var parts = VignetteSampler.Sample(Simple(), 1.0f);
            Assert.AreEqual("a", parts[0].TrackId);
            Assert.AreEqual("pg.june", parts[0].Part);
            Assert.AreEqual(0.5f, parts[0].Pose.X, 1e-5f);
            Assert.AreEqual(0.25f, parts[0].Pose.Y, 1e-5f);
            Assert.AreEqual(1.5f, parts[0].Pose.Scale, 1e-5f);
            Assert.AreEqual(45f, parts[0].Pose.Rotation, 1e-4f);
            Assert.AreEqual(0.5f, parts[0].Pose.Alpha, 1e-5f);
            Assert.IsTrue(parts[0].Visible);
        }

        [Test]
        public void TrackIsHiddenBeforeItsFirstKeyAndHoldsItsLastPose()
        {
            var before = VignetteSampler.Sample(Simple(), 0.2f)[0];
            Assert.IsFalse(before.Visible);
            Assert.AreEqual(0f, before.Pose.X, 1e-5f);

            var after = VignetteSampler.Sample(Simple(), 1.9f)[0];
            Assert.AreEqual(1f, after.Pose.X, 1e-5f);
            Assert.IsFalse(after.Visible, "faded to alpha 0");
        }

        [Test]
        public void EasesStartAtZeroEndAtOneAndBehave()
        {
            foreach (Ease ease in System.Enum.GetValues(typeof(Ease)))
            {
                Assert.AreEqual(0f, VignetteSampler.Apply(ease, 0f), 1e-5f, ease.ToString());
                Assert.AreEqual(1f, VignetteSampler.Apply(ease, 1f), 1e-5f, ease.ToString());
                Assert.AreEqual(0f, VignetteSampler.Apply(ease, -1f), 1e-5f, "clamped below");
                Assert.AreEqual(1f, VignetteSampler.Apply(ease, 2f), 1e-5f, "clamped above");
            }

            Assert.AreEqual(0.5f, VignetteSampler.Apply(Ease.InOut, 0.5f), 1e-5f);
            Assert.Greater(VignetteSampler.Apply(Ease.Out, 0.5f), 0.5f, "out is fast first");
            Assert.AreEqual(0f, VignetteSampler.Apply(Ease.Step, 0.99f), 1e-5f, "step holds until the key");
            float maxBack = 0f;
            for (int i = 0; i <= 100; i++)
            {
                maxBack = System.Math.Max(maxBack, VignetteSampler.Apply(Ease.Back, i / 100f));
            }

            Assert.Greater(maxBack, 1f, "back overshoots");
        }

        [Test]
        public void StepKeysSnapOnTheKeyTime()
        {
            var vale = C01Vignettes.Get(C01Vignettes.ValeDoorShut);
            var hand = vale.FindTrack("clock_hand");
            int index = IndexOf(vale, "clock_hand");
            Assert.NotNull(hand);
            Assert.AreEqual(0f, VignetteSampler.Sample(vale, 0.49f)[index].Pose.Rotation, 1e-4f);
            Assert.AreEqual(-60f, VignetteSampler.Sample(vale, 0.5f)[index].Pose.Rotation, 1e-4f);
            Assert.AreEqual(-60f, VignetteSampler.Sample(vale, 0.99f)[index].Pose.Rotation, 1e-4f);
            Assert.AreEqual(-300f, VignetteSampler.Sample(vale, 3.0f)[index].Pose.Rotation, 1e-4f);
        }

        [Test]
        public void KeysSwitchTheSpriteForBlinks()
        {
            var vale = C01Vignettes.Get(C01Vignettes.ValeDoorShut);
            int eye = IndexOf(vale, "eye");
            Assert.AreEqual("pg.eye", VignetteSampler.Sample(vale, 1.0f)[eye].Part);
            Assert.AreEqual(PuppetParts.EyeShut, VignetteSampler.Sample(vale, 1.4f)[eye].Part);
            Assert.AreEqual("pg.eye", VignetteSampler.Sample(vale, 1.6f)[eye].Part);
        }

        [Test]
        public void StampCueWigglesItsTargetToAStop()
        {
            var vale = C01Vignettes.Get(C01Vignettes.ValeDoorShut);
            int not = IndexOf(vale, "not");
            Assert.AreNotEqual(0f, VignetteSampler.Sample(vale, 2.7f)[not].Pose.Rotation);
            Assert.AreEqual(0f, VignetteSampler.Sample(vale, 3.5f)[not].Pose.Rotation, 1e-4f, "settled");
            Assert.AreEqual(0f, VignetteSampler.Wiggle(0f), 1e-5f);
            Assert.AreEqual(0f, VignetteSampler.Wiggle(1f), 1e-5f);
        }

        [Test]
        public void ActiveCuesReportProgressAndStartedCuesFireOnce()
        {
            var vale = C01Vignettes.Get(C01Vignettes.ValeDoorShut);
            var active = new List<CueState>();
            VignetteSampler.ActiveCues(vale, 3.0f, active);
            Assert.AreEqual(1, active.Count);
            Assert.AreEqual(CueKind.Stamp, active[0].Cue.Kind);
            Assert.AreEqual(0.5f, active[0].Progress, 1e-3f);

            var started = new List<VignetteCue>();
            VignetteSampler.CuesStarted(vale, 2.5f, 2.7f, started);
            Assert.AreEqual(1, started.Count);
            Assert.AreEqual(CueKind.Stamp, started[0].Kind);
            VignetteSampler.CuesStarted(vale, 2.7f, 2.9f, started);
            Assert.AreEqual(0, started.Count);
        }

        [Test]
        public void SampleIntoAReusedBufferMatchesAndRejectsSmallBuffers()
        {
            var bea = C01Vignettes.Get(C01Vignettes.BeaSawCape);
            var buffer = new PartState[Vignette.MaxTracks];
            int n = VignetteSampler.Sample(bea, 1.0f, buffer);
            Assert.AreEqual(bea.Tracks.Count, n);
            Assert.AreEqual(VignetteSampler.Sample(bea, 1.0f)[IndexOf(bea, "flash")].Pose.X, buffer[IndexOf(bea, "flash")].Pose.X);
            Assert.Throws<System.ArgumentException>(() => VignetteSampler.Sample(bea, 1f, new PartState[1]));
        }

        [Test]
        public void LoopTimeLoopsSpeechForever()
        {
            var speech = C01Vignettes.Get(C01Vignettes.BeaSawCape);
            Assert.AreEqual(0, speech.LoopCount);
            Assert.AreEqual(1.0f, speech.LocalTime(speech.Length * 5 + 1.0f), 1e-3f);
            Assert.IsFalse(speech.IsFinished(1000f));
        }

        [Test]
        public void ReducedMotionHoldsEachBeatsEndPose()
        {
            var bea = C01Vignettes.Get(C01Vignettes.BeaSawCape);
            Assert.AreEqual(1.5f, bea.ReducedMotionTime(0f), 1e-5f);
            Assert.AreEqual(1.5f, bea.ReducedMotionTime(1.0f), 1e-5f);
            Assert.AreEqual(2.4f, bea.ReducedMotionTime(2.2f), 1e-5f);
            Assert.AreEqual(3.4f, bea.ReducedMotionTime(3.0f), 1e-5f);
        }

        // ------------------------------------------------------------------------------- validator

        [Test]
        public void ValidatorCatchesUnknownPartsAndTargets()
        {
            var bad = new Vignette("vg.bad", 2f, 0, new[] { new VignetteBeat(0f, 1f) },
                new[] { new VignetteTrack("a", "pg.no_such_part", new VignetteKey(0f, new PuppetPose(0.5f, 0.5f))) },
                new[] { new VignetteCue(0.5f, CueKind.Ring, "missing", 0.5f) });
            var errors = VignetteValidator.Validate(bad);
            Assert.IsTrue(errors.Exists(e => e.Contains("unknown part")), string.Join("\n", errors));
            Assert.IsTrue(errors.Exists(e => e.Contains("unknown track")), string.Join("\n", errors));
        }

        [Test]
        public void ValidatorCountsTracksAndOverlaysTowardTheTwoMovingPartLimit()
        {
            Assert.AreEqual(1, Simple().MaxMovingAtOnce());
            Assert.AreEqual(2, Simple(new VignetteCue(1.0f, CueKind.Ring, "a", 0.3f)).MaxMovingAtOnce());
            Assert.AreEqual(1, Simple(new VignetteCue(1.0f, CueKind.Stamp, "a", 0.3f)).MaxMovingAtOnce(),
                "a stamp moves its own target: still one part");
            Assert.AreEqual(1, Simple(new VignetteCue(1.0f, CueKind.Gesture, null, 0.3f, "Point")).MaxMovingAtOnce(),
                "world-only cues are not on the stage");

            var three = Simple(new VignetteCue(1.0f, CueKind.Ring, "a", 0.3f), new VignetteCue(1.1f, CueKind.Puff, "a", 0.3f));
            Assert.AreEqual(3, three.MaxMovingAtOnce());
            var errors = VignetteValidator.Validate(three);
            Assert.IsTrue(errors.Exists(e => e.Contains("parts move at once")), string.Join("\n", errors));

            var touching = Simple(new VignetteCue(1.5f, CueKind.Ring, "a", 0.3f), new VignetteCue(1.0f, CueKind.Puff, "a", 0.5f));
            Assert.AreEqual(2, touching.MaxMovingAtOnce(), "intervals are half-open: one ending as another starts is not overlap");
        }

        [Test]
        public void ValidatorCatchesBadTimingBeatsAndKeyOrder()
        {
            var bad = new Vignette("bad", 5f, -1,
                new[] { new VignetteBeat(0f, 1f), new VignetteBeat(1f, 2f), new VignetteBeat(2f, 3f), new VignetteBeat(3f, 4f) },
                new[]
                {
                    new VignetteTrack("a", "pg.june",
                        new VignetteKey(1f, new PuppetPose(0.5f, 0.5f)), new VignetteKey(0.5f, new PuppetPose(2f, 0.5f))),
                },
                new[] { new VignetteCue(4.8f, CueKind.Gesture, null, 0.5f) });
            var errors = VignetteValidator.Validate(bad);
            string all = string.Join("\n", errors);
            StringAssert.Contains("vg.", all);
            StringAssert.Contains("length", all);
            StringAssert.Contains("loop count", all);
            StringAssert.Contains("4 beats", all);
            StringAssert.Contains("out of order", all);
            StringAssert.Contains("off the stage", all);
            StringAssert.Contains("outside the loop", all);
            StringAssert.Contains("clip name", all);
        }

        // ------------------------------------------------------------------------------- data

        [Test]
        public void EveryCaseVignetteIsValid()
        {
            Assert.AreEqual(35, C01Vignettes.All.Count, "5 key lines and 30 for the other lines (the notebook comic is drawn from stickers)");
            foreach (var v in C01Vignettes.All)
            {
                var errors = VignetteValidator.Validate(v);
                Assert.IsEmpty(errors, string.Join("\n", errors));
                Assert.LessOrEqual(v.MaxMovingAtOnce(), Vignette.MaxMovingParts, v.Id);
            }
        }

        [Test]
        public void EveryPartUsedExistsInThePartsLibrary()
        {
            foreach (var v in C01Vignettes.All)
            {
                foreach (var track in v.Tracks)
                {
                    Assert.IsTrue(PuppetParts.Contains(track.Part), v.Id + "/" + track.Id + " uses " + track.Part);
                    foreach (var key in track.Keys)
                    {
                        Assert.IsTrue(key.Part == null || PuppetParts.Contains(key.Part), v.Id + "/" + track.Id + " → " + key.Part);
                    }
                }
            }
        }

        [Test]
        public void PartsLibraryIdsAreUniqueAndPrefixed()
        {
            var seen = new HashSet<string>();
            foreach (string id in PuppetParts.All)
            {
                StringAssert.StartsWith(PuppetParts.Prefix, id);
                Assert.IsTrue(seen.Add(id), "duplicate part " + id);
            }

            Assert.IsFalse(PuppetParts.Contains(null));
            Assert.IsFalse(PuppetParts.Contains("window"), "IDs carry the prefix");
        }

        [Test]
        public void VignetteIdsAreUnique()
        {
            var ids = new HashSet<string>();
            foreach (var v in C01Vignettes.All)
            {
                Assert.IsTrue(ids.Add(v.Id), v.Id);
                Assert.AreSame(v, C01Vignettes.Get(v.Id));
            }

            Assert.IsNull(C01Vignettes.Get("vg.nope"));
        }

        [Test]
        public void EveryScriptLineHasAVignetteAndNoneKeepsTheV1Card()
        {
            var scriptKeys = new List<string>(CaseScript.AllLineKeys);
            Assert.AreEqual(37, scriptKeys.Count, "case 01 has 37 lines (captions, examines, dialogue; S002.5a adds the owl panel and Kit's question)");
            Assert.AreEqual(scriptKeys.Count, C01Vignettes.LineTable.Count, "one table row per CaseScript line");
            CollectionAssert.AreEquivalent(scriptKeys, C01Vignettes.ScopedLineKeys, "every line is in scope");
            foreach (string key in scriptKeys)
            {
                var visual = C01Vignettes.For(key);
                Assert.NotNull(visual, key);
                Assert.AreEqual(key, visual.LineKey);
                Assert.IsFalse(visual.UsesV1Strip, key + " needs a vignette (no v1 fallback in case 01)");
                var vignette = C01Vignettes.Get(visual.VignetteId);
                Assert.NotNull(vignette, key + " → " + visual.VignetteId);
                Assert.AreEqual(0, vignette.LoopCount, key + ": speech vignettes loop until next");
                Assert.IsEmpty(VignetteValidator.Validate(vignette), key);
            }

            foreach (var row in C01Vignettes.LineTable)
            {
                Assert.IsFalse(row.UsesV1Strip, row.LineKey);
                Assert.AreEqual(C01Vignettes.For(row.LineKey).VignetteId, row.VignetteId, row.LineKey);
            }

            Assert.IsNull(C01Vignettes.For("c01.line.nope"));
            Assert.IsNull(C01Vignettes.For(null));
            Assert.IsNull(C01Vignettes.For("c01.hint.arrival.observe"), "hints are not case lines");
        }

        [Test]
        public void EveryVignetteIsUsedByALine()
        {
            var used = new HashSet<string>();
            foreach (var row in C01Vignettes.LineTable)
            {
                used.Add(row.VignetteId);
            }

            foreach (var v in C01Vignettes.All)
            {
                Assert.IsTrue(used.Contains(v.Id), v.Id + " is not used by any line");
            }
        }

        [Test]
        public void SimilarLinesShareOneVignette()
        {
            Assert.AreEqual(C01Vignettes.JuneGreet, C01Vignettes.For(Lines.JuneGreet).VignetteId);
            Assert.AreEqual(C01Vignettes.JuneGreet, C01Vignettes.For(Lines.JuneGreetShortcut).VignetteId, "both hellos");
            Assert.AreEqual(C01Vignettes.JuneResolve, C01Vignettes.For(Lines.CaptionOwlBows).VignetteId,
                "the owl-bow caption is the resolve: knock, knock … knock, three bows");
        }

        [Test]
        public void ClueLinesShowTheActualClue()
        {
            AssertShows(Lines.ExamineCloth, "pg.cloth", PuppetParts.Sparkle);
            AssertShows(Lines.ExamineSill, PuppetParts.SillSnag);
            AssertShows(Lines.ExamineStreamer, "pg.streamer");
            AssertShows(Lines.ExamineBowlSweet, "pg.sweet");
            AssertShows(Lines.ExamineDoorStuck, "pg.door_shut");
            AssertShows(Lines.ExamineOwlPanel, "pg.owl");
            AssertShows(Lines.BeaSawCape, "pg.hedge", PuppetParts.Sparkle, "pg.house_side");
            AssertShows(Lines.BeaRepeat, "pg.hedge", PuppetParts.Sparkle);
            AssertShows(Lines.CaptionGrille, "pg.grille");
            AssertShows(Lines.CaptionArmLinked, "pg.arm", "pg.cam");
            AssertShows(Lines.CaptionCrankGap, "pg.crank", "pg.arm");
            AssertShows(Lines.CaptionFoyerHum, "pg.floor_hum");
            AssertShows(Lines.JuneLatchReminder, PuppetParts.LatchBar, "pg.hands");
            AssertShows(Lines.JuneGreet, PuppetParts.WaveHand, "pg.june");
            AssertShows(Lines.KitAsk, "pg.kit", "pg.question");
        }

        /// <summary>
        /// D26 spoiler rules for case content: no "this matters" ring, no check stamp and no key ring in any case line's
        /// vignette; clue lines never set June's cape beside the clue; Bea's lines show only a flash (no June, no lit
        /// window); the decoy and the crank gap get no ⊘ (only Mr. Vale's own claim keeps it).
        /// </summary>
        [Test]
        public void NoCaseVignetteNamesAConclusion()
        {
            foreach (var row in C01Vignettes.LineTable)
            {
                var v = C01Vignettes.Get(row.VignetteId);
                foreach (var cue in v.Cues)
                {
                    Assert.AreNotEqual(CueKind.Ring, cue.Kind, row.LineKey + ": the ring cue left case content");
                }

                AssertHides(row.LineKey, "pg.check", PuppetParts.KeyRing);
            }

            foreach (string key in new[] { Lines.BeaSawCape, Lines.BeaRepeat, Lines.BeaDoorOpen })
            {
                AssertHides(key, "pg.june", PuppetParts.JuneRun, "pg.june_window", "pg.cape", "pg.window", "pg.pointer", "pg.feet", "pg.not");
            }

            foreach (string key in new[] { Lines.ExamineCloth, Lines.ExamineSill, Lines.ExamineStreamer })
            {
                AssertHides(key, "pg.cape", "pg.not");
            }

            AssertHides(Lines.CaptionCrankGap, "pg.not", "pg.gap");
            AssertHides(Lines.ExamineDoorStuck, "pg.lock");
            AssertHides(Lines.JuneListenFloor, "pg.floor_hum");
        }

        private static void AssertHides(string lineKey, params string[] parts)
        {
            var v = C01Vignettes.Get(C01Vignettes.For(lineKey).VignetteId);
            foreach (var track in v.Tracks)
            {
                foreach (string part in parts)
                {
                    Assert.AreNotEqual(part, track.Part, lineKey + " (" + v.Id + ") must not show " + part);
                    foreach (var key in track.Keys)
                    {
                        Assert.AreNotEqual(part, key.Part, lineKey + " (" + v.Id + ") must not show " + part);
                    }
                }
            }
        }

        [Test]
        public void KnockLinesKnockInTheMachinesRhythm()
        {
            foreach (string key in new[] { Lines.CaptionArrivalKnocks, Lines.CaptionFoyerHum, Lines.JuneClosed })
            {
                var v = C01Vignettes.Get(C01Vignettes.For(key).VignetteId);
                var knocks = new List<float>();
                foreach (var cue in v.Cues)
                {
                    if (cue.Kind == CueKind.Knock)
                    {
                        knocks.Add(cue.T);
                    }
                }

                Assert.AreEqual(3, knocks.Count, key);
                Assert.Greater(knocks[2] - knocks[1], 1.5f * (knocks[1] - knocks[0]), key + ": knock, knock … knock");
            }
        }

        private static void AssertShows(string lineKey, params string[] parts)
        {
            var v = C01Vignettes.Get(C01Vignettes.For(lineKey).VignetteId);
            var present = new HashSet<string>();
            foreach (var track in v.Tracks)
            {
                present.Add(track.Part);
                foreach (var key in track.Keys)
                {
                    if (key.Part != null)
                    {
                        present.Add(key.Part);
                    }
                }
            }

            foreach (string part in parts)
            {
                Assert.IsTrue(present.Contains(part), lineKey + " (" + v.Id + ") should show " + part);
            }
        }

        [Test]
        public void ScopeMatchesTheOwnerDecision()
        {
            CollectionAssert.AreEquivalent(new[]
            {
                Lines.BeaSawCape, Lines.BeaDoorOpen, Lines.ValeDoorShut, Lines.ValeWasShut, Lines.JuneResolve,
            }, C01Vignettes.KeyLineKeys);
            Assert.AreEqual(C01Vignettes.BeaSawCape, C01Vignettes.For(Lines.BeaSawCape).VignetteId);
            Assert.AreEqual(C01Vignettes.ValeDoorShut, C01Vignettes.For(Lines.ValeDoorShut).VignetteId);
            Assert.AreEqual(C01Vignettes.JuneResolve, C01Vignettes.For(Lines.JuneResolve).VignetteId);
        }

        [Test]
        public void BeaVignetteShowsOnlyTheFlashSheSaw()
        {
            var bea = C01Vignettes.Get(C01Vignettes.BeaSawCape);
            int flash = IndexOf(bea, "flash");
            int eye = IndexOf(bea, "eye");
            int what = IndexOf(bea, "what");

            var early = VignetteSampler.Sample(bea, 0.9f);
            Assert.IsTrue(early[flash].Visible, "a silver flash streaks over the hedge");
            Assert.Greater(early[flash].Pose.Y, 0.6f, "up high");
            Assert.IsFalse(early[eye].Visible);

            var gone = VignetteSampler.Sample(bea, 1.7f);
            Assert.IsFalse(gone[flash].Visible, "then it was gone");

            var after = VignetteSampler.Sample(bea, 3.5f);
            Assert.IsTrue(after[eye].Visible, "Bea's eye: she saw it");
            Assert.IsTrue(after[what].Visible, "and she does not know what it was");
        }

        [Test]
        public void ValeVignetteKeepsTheDoorStillAndStampsNever()
        {
            var vale = C01Vignettes.Get(C01Vignettes.ValeDoorShut);
            int door = IndexOf(vale, "door");
            Assert.AreEqual(1, vale.Tracks[door].Keys.Count, "shut ≠ stuck: the door never moves");
            Assert.AreEqual("pg.door_shut", vale.Tracks[door].Part);
            Assert.IsTrue(VignetteSampler.Sample(vale, 3.5f)[IndexOf(vale, "not")].Visible);
        }

        private static int IndexOf(Vignette v, string trackId)
        {
            for (int i = 0; i < v.Tracks.Count; i++)
            {
                if (v.Tracks[i].Id == trackId)
                {
                    return i;
                }
            }

            Assert.Fail(v.Id + " has no track " + trackId);
            return -1;
        }
    }
}
