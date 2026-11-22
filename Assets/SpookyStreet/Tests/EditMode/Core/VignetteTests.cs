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
            var bea = C01Vignettes.Get(C01Vignettes.BeaSawCape);
            var active = new List<CueState>();
            VignetteSampler.ActiveCues(bea, 3.05f, active);
            Assert.AreEqual(1, active.Count);
            Assert.AreEqual(CueKind.Ring, active[0].Cue.Kind);
            Assert.AreEqual(0.5f, active[0].Progress, 1e-3f);

            var started = new List<VignetteCue>();
            VignetteSampler.CuesStarted(bea, 2.1f, 2.2f, started);
            Assert.AreEqual(1, started.Count);
            Assert.AreEqual(CueKind.Gesture, started[0].Kind);
            Assert.AreEqual("Point", started[0].Arg, "Bea plays Point in the world (decision D23.6)");
            VignetteSampler.CuesStarted(bea, 2.2f, 2.3f, started);
            Assert.AreEqual(0, started.Count);
        }

        [Test]
        public void SampleIntoAReusedBufferMatchesAndRejectsSmallBuffers()
        {
            var bea = C01Vignettes.Get(C01Vignettes.BeaSawCape);
            var buffer = new PartState[Vignette.MaxTracks];
            int n = VignetteSampler.Sample(bea, 1.0f, buffer);
            Assert.AreEqual(bea.Tracks.Count, n);
            Assert.AreEqual(VignetteSampler.Sample(bea, 1.0f)[IndexOf(bea, "june")].Pose.X, buffer[IndexOf(bea, "june")].Pose.X);
            Assert.Throws<System.ArgumentException>(() => VignetteSampler.Sample(bea, 1f, new PartState[1]));
        }

        [Test]
        public void LoopTimeLoopsSpeechForeverAndNotebookStripsTwice()
        {
            var speech = C01Vignettes.Get(C01Vignettes.BeaSawCape);
            Assert.AreEqual(0, speech.LoopCount);
            Assert.AreEqual(1.0f, speech.LocalTime(3.6f * 5 + 1.0f), 1e-3f);
            Assert.IsFalse(speech.IsFinished(1000f));

            var strip = C01Vignettes.AnswerStripFor(CaseNotebook.QuestionEntry);
            Assert.AreEqual(C01Vignettes.NotebookLoops, strip.LoopCount);
            Assert.AreEqual(0.5f, strip.LocalTime(strip.Length + 0.5f), 1e-3f);
            Assert.IsTrue(strip.IsFinished(strip.Length * 2));
            Assert.AreEqual(strip.Length, strip.LocalTime(strip.Length * 2 + 3f), 1e-5f, "holds the end pose");
        }

        [Test]
        public void ReducedMotionHoldsEachBeatsEndPose()
        {
            var bea = C01Vignettes.Get(C01Vignettes.BeaSawCape);
            Assert.AreEqual(1.7f, bea.ReducedMotionTime(0f), 1e-5f);
            Assert.AreEqual(1.7f, bea.ReducedMotionTime(1.0f), 1e-5f);
            Assert.AreEqual(2.1f, bea.ReducedMotionTime(1.8f), 1e-5f);
            Assert.AreEqual(3.6f, bea.ReducedMotionTime(2.5f), 1e-5f);
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
            Assert.AreEqual(8, C01Vignettes.All.Count);
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
        public void EveryScriptLineHasAnEntryAndScopedLinesHaveAVignette()
        {
            var scriptKeys = new List<string>(CaseScript.AllLineKeys);
            Assert.AreEqual(scriptKeys.Count, C01Vignettes.LineTable.Count, "one table row per CaseScript line");
            var scoped = new HashSet<string>(C01Vignettes.ScopedLineKeys);
            foreach (string key in scriptKeys)
            {
                var visual = C01Vignettes.For(key);
                Assert.NotNull(visual, key);
                Assert.AreEqual(key, visual.LineKey);
                if (scoped.Contains(key))
                {
                    Assert.IsFalse(visual.UsesV1Strip, key + " is in scope and needs a vignette");
                    Assert.NotNull(C01Vignettes.Get(visual.VignetteId), key + " → " + visual.VignetteId);
                }
                else
                {
                    Assert.IsTrue(visual.UsesV1Strip, key + " keeps the v1 strip until its vignette is made");
                    Assert.IsNull(visual.VignetteId);
                }
            }

            foreach (string key in scoped)
            {
                CollectionAssert.Contains(scriptKeys, key, "scoped key is a real CaseScript line");
            }

            Assert.IsNull(C01Vignettes.For("c01.line.nope"));
            Assert.IsNull(C01Vignettes.For(null));
        }

        [Test]
        public void ScopeMatchesTheOwnerDecision()
        {
            CollectionAssert.AreEquivalent(new[]
            {
                Lines.BeaSawCape, Lines.BeaDoorOpen, Lines.ValeDoorShut, Lines.ValeWasShut, Lines.JuneResolve,
            }, C01Vignettes.ScopedLineKeys);
            Assert.AreEqual(C01Vignettes.BeaSawCape, C01Vignettes.For(Lines.BeaSawCape).VignetteId);
            Assert.AreEqual(C01Vignettes.ValeDoorShut, C01Vignettes.For(Lines.ValeDoorShut).VignetteId);
            Assert.AreEqual(C01Vignettes.JuneResolve, C01Vignettes.For(Lines.JuneResolve).VignetteId);
        }

        [Test]
        public void EveryNotebookQuestionHasAnAnswerStrip()
        {
            var strips = new HashSet<string>();
            foreach (var question in CaseNotebook.Questions)
            {
                var strip = C01Vignettes.AnswerStripFor(question.Id);
                Assert.NotNull(strip, question.Id);
                Assert.Greater(strip.LoopCount, 0, "strips stop after playing (no constant motion on the page)");
                Assert.IsTrue(strips.Add(strip.Id));
                Assert.NotNull(strip.FindTrack("check"), "each strip ends on the green check stamp");
            }

            Assert.IsNull(C01Vignettes.AnswerStripFor("c01.note.nope"));
        }

        [Test]
        public void BeaVignetteFollowsTheApprovedChoreography()
        {
            var bea = C01Vignettes.Get(C01Vignettes.BeaSawCape);
            Assert.AreEqual(3.6f, bea.Length, 1e-5f);
            int june = IndexOf(bea, "june");
            int lit = IndexOf(bea, "window_lit");
            int hand = IndexOf(bea, "hand");

            var early = VignetteSampler.Sample(bea, 1.0f);
            Assert.IsTrue(early[june].Visible, "June runs across the bridge");
            Assert.IsFalse(early[lit].Visible);
            Assert.IsFalse(early[hand].Visible);

            var inside = VignetteSampler.Sample(bea, 2.15f);
            Assert.IsFalse(inside[june].Visible, "June popped into the window");
            Assert.IsTrue(inside[lit].Visible, "the window lit with June inside");

            var point = VignetteSampler.Sample(bea, 3.0f);
            Assert.IsTrue(point[hand].Visible, "Bea's hand points");
            var cues = new List<CueState>();
            VignetteSampler.ActiveCues(bea, 3.0f, cues);
            Assert.IsTrue(cues.Exists(c => c.Cue.Kind == CueKind.Ring && c.Cue.Target == "window_lit"), "key ring pulses on the window");
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
