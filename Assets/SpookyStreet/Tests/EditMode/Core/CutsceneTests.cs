using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpookyStreet.Core.Animation;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Cutscenes;
using SpookyStreet.Core.Localization;
using SpookyStreet.Core.Save;
using SpookyStreet.Core.World;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.Cutscenes;
using N = SpookyStreet.Core.Case.CaseNotebook;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// S002.5b cutscene core (D28, narrative bible §5): the evaluator at 30 and 60 fps, skip at any point, reduced
    /// motion, waits, the seen set, case 01's opening and the reveal built from the child's own stickers, and the rule
    /// that a cutscene never runs ahead of the child's deduction.
    /// </summary>
    public class CutsceneTests
    {
        // ------------------------------------------------------------------------------------------ helpers

        /// <summary>Every case 01 clue found, the show over, nothing solved yet.</summary>
        private static CaseState AllFoundAfterShow()
        {
            var play = new CasePlaythrough();
            play.Do(CaseActionKind.HearKnocks);
            play.Talk(Speaker.Bea);
            play.Talk(Speaker.Vale);
            play.Do(CaseActionKind.SitOnBench);
            play.Do(CaseActionKind.ExamineStreamer);
            play.Do(CaseActionKind.ExamineCloth);
            play.Do(CaseActionKind.TryFrontDoor);
            play.Do(CaseActionKind.EnterAttic);
            play.Do(CaseActionKind.ExamineSill);
            play.Talk(Speaker.June);
            play.Do(CaseActionKind.EnterFoyer);
            play.Do(CaseActionKind.LiftLatch);
            play.Do(CaseActionKind.ExamineGrille);
            play.Do(CaseActionKind.ExamineOwlPanel);
            play.Do(CaseActionKind.TurnCrank);
            play.Do(CaseActionKind.LinkArm);
            play.Do(CaseActionKind.TurnCrank);
            play.Do(CaseActionKind.ArriveAtPorch);
            Assert.IsTrue(play.State.Has(CaseFact.CaseClosed));
            return play.State;
        }

        /// <summary>Every right set of a question (slot order canonical), from every tray sticker.</summary>
        private static List<string[]> RightSets(string questionId)
        {
            var question = N.Question(questionId);
            var ids = N.Stickers.Where(s => s.InTray).Select(s => s.Id).ToArray();
            var sets = new List<string[]>();
            Combine(ids, question.SlotCount, 0, new List<string>(), sets, question);
            return sets;
        }

        private static void Combine(string[] ids, int k, int start, List<string> pick, List<string[]> output, CaseQuestion question)
        {
            if (pick.Count == k)
            {
                var set = pick.ToArray();
                if (CaseDeduction.IsCorrect(question, set))
                {
                    output.Add(set);
                }

                return;
            }

            for (int i = start; i < ids.Length; i++)
            {
                pick.Add(ids[i]);
                Combine(ids, k, i + 1, pick, output, question);
                pick.RemoveAt(pick.Count - 1);
            }
        }

        private static (CaseState state, CaseAnswers answers) Stamped(string[] entry, string[] source, string[] moved)
        {
            var state = AllFoundAfterShow();
            var answers = CaseAnswers.Empty;
            foreach (var (q, set) in new[] { (N.QuestionEntry, entry), (N.QuestionSource, source), (N.QuestionMoved, moved) })
            {
                var step = state.Apply(CaseAction.Solve(q, set));
                Assert.AreEqual(CaseOutcome.Progressed, step.Outcome, q + " " + string.Join("+", set));
                state = step.State;
                answers = answers.With(q, set);
            }

            Assert.IsTrue(state.Has(CaseFact.CaseStamped));
            return (state, answers);
        }

        private static (CaseState state, CaseAnswers answers) Canonical() => Stamped(
            new[] { N.ValeDoor, N.Cloth, N.SillSnag }, new[] { N.FloorHum, N.WoodenPiece }, new[] { N.OwlBooth, N.LooseArm });

        /// <summary>Plays to the end at a fixed frame rate, recording every event with the playback time it fired at.</summary>
        private static List<(float time, CutsceneEvent e)> Run(Cutscene cutscene, float fps, bool reduced = false,
            Func<CutscenePlayback, float, bool> each = null, float maxSeconds = 120f)
        {
            var playback = new CutscenePlayback(cutscene, reduced);
            var log = new List<(float, CutsceneEvent)>();
            foreach (var e in playback.Advance(0f))
            {
                log.Add((0f, e));
            }

            float dt = 1f / fps;
            float t = 0f;
            while (!playback.IsDone && t < maxSeconds)
            {
                t += dt;
                foreach (var e in playback.Advance(dt))
                {
                    log.Add((playback.Elapsed, e));
                }

                if (each != null && !each(playback, t))
                {
                    break;
                }
            }

            Assert.IsTrue(playback.IsDone || each != null, "it ends");
            return log;
        }

        private static string Describe(CutsceneEvent e) =>
            e.Kind + ":" + e.Beat + ":" + (e.Cast != null ? e.Cast.ToString() : "") + (e.Signal != null ? e.Signal.ToString() : "");

        private static void AssertClose(CamPose a, CamPose b, float tolerance, string message)
        {
            Assert.AreEqual(a.Eye.X, b.Eye.X, tolerance, message + " eye x");
            Assert.AreEqual(a.Eye.Y, b.Eye.Y, tolerance, message + " eye y");
            Assert.AreEqual(a.Eye.Z, b.Eye.Z, tolerance, message + " eye z");
            Assert.AreEqual(a.Aim.X, b.Aim.X, tolerance, message + " aim x");
            Assert.AreEqual(a.Aim.Y, b.Aim.Y, tolerance, message + " aim y");
            Assert.AreEqual(a.Aim.Z, b.Aim.Z, tolerance, message + " aim z");
            Assert.AreEqual(a.Fov, b.Fov, tolerance, message + " fov");
        }

        private static StagePoint P(float x, float y, float z) => new StagePoint(x, y, z);

        /// <summary>A small cutscene exercising every kind of transition, cue and a wait.</summary>
        private static Cutscene Sample()
        {
            var beats = new List<CutsceneBeat>
            {
                new CutsceneBeat("a", 2f, new CameraShot(P(0, 2, -10), P(0, 1, 0), 40f).DriftTo(P(0, 2, -8), P(0, 1, 0), 36f))
                    .Caption("k.a")
                    .Cue(CastCue.Place(0f, CastActor.Pip, P(0, 0, -5), 0f))
                    .Cue(CastCue.WalkTo(0.5f, CastActor.Pip, 1f, P(0, 0, -5), P(0, 0, -4)))
                    .Music(0f, CutsceneAudio.MusicStreetTheme)
                    .Wait("walk.pip", 1f),
                new CutsceneBeat("b", 1.5f, new CameraShot(P(3, 2, -6), P(0, 1, 0), 30f, ShotTransition.Blend, 0.6f))
                    .Babble(0.75f, CutsceneAudio.Babble(CastActor.Bea, BabbleEmotion.Surprised))
                    .Cue(CastCue.Gesture(1.5f, CastActor.Bea, CharacterClips.Wave, P(0, 1, 0))),
                new CutsceneBeat("c", 1f, new CameraShot(P(0, 1.5f, CaseRouteLayout.InteriorFrontZ + 1f), P(1, 1, CaseRouteLayout.InteriorFrontZ + 3f), 45f,
                        ShotTransition.PageTurn))
                    .World(0.33f, "world.test"),
                new CutsceneBeat("d", 1.2f, new CameraShot(P(0, 1.5f, CaseRouteLayout.InteriorFrontZ + 2f), P(1, 1, CaseRouteLayout.InteriorFrontZ + 3f), 45f,
                    ShotTransition.Cut)),
            };
            return new Cutscene("cs.test", beats, 0.8f, new[] { CastCue.Place(0f, CastActor.Pip, P(0, 0, -4), 0f) });
        }

        // ------------------------------------------------------------------------------------------ evaluator

        [Test]
        public void EveryCueFiresOnceInOrderAt30And60Fps()
        {
            var (state, answers) = Canonical();
            foreach (var cutscene in new[] { Sample(), C01Cutscenes.Opening, C01Cutscenes.Reveal(state, answers) })
            {
                var at30 = Run(cutscene, 30f);
                var at60 = Run(cutscene, 60f);
                var at24Jitter = new List<string>();
                var playback = new CutscenePlayback(cutscene, false);
                var rng = new Random(7);
                at24Jitter.AddRange(playback.Advance(0f).Select(Describe));
                while (!playback.IsDone)
                {
                    at24Jitter.AddRange(playback.Advance((float)(1.0 / 24 + rng.NextDouble() * 0.05)).Select(Describe));
                }

                CollectionAssert.AreEqual(at30.Select(x => Describe(x.e)), at60.Select(x => Describe(x.e)), cutscene.Id);
                CollectionAssert.AreEqual(at30.Select(x => Describe(x.e)), at24Jitter, cutscene.Id + " with a jittery frame rate");

                int cues = cutscene.Beats.Sum(b => b.Cast.Count + b.Signals.Count);
                Assert.AreEqual(cues, at60.Count(x => x.e.Kind == CutsceneEventKind.Cast || x.e.Kind == CutsceneEventKind.Signal),
                    cutscene.Id + ": every cue exactly once");
                Assert.AreEqual(cutscene.Beats.Count, at60.Count(x => x.e.Kind == CutsceneEventKind.BeatStarted));
                Assert.AreEqual(1, at60.Count(x => x.e.Kind == CutsceneEventKind.OutroStarted));
                Assert.AreEqual(CutsceneEventKind.Ended, at60.Last().e.Kind);

                // A cue fires on the frame its time is reached: never early, at most one frame late.
                foreach (var (time, e) in at30.Where(x => x.e.Kind == CutsceneEventKind.Cast || x.e.Kind == CutsceneEventKind.Signal))
                {
                    float at = e.Cast?.At ?? e.Signal.At;
                    float nominal = cutscene.BeatStart(e.Beat) + at;
                    Assert.GreaterOrEqual(time + 1e-3f, nominal, cutscene.Id + " " + Describe(e) + " early");
                }
            }
        }

        [Test]
        public void TheCameraIsTheSameAt30And60Fps()
        {
            var (state, answers) = Canonical();
            foreach (var cutscene in new[] { Sample(), C01Cutscenes.Opening, C01Cutscenes.Reveal(state, answers) })
            {
                var a = new CutscenePlayback(cutscene, false);
                var b = new CutscenePlayback(cutscene, false);
                a.Advance(0f);
                b.Advance(0f);
                a.Signal(CutsceneWorld.PipArrived);
                b.Signal(CutsceneWorld.PipArrived);
                for (int frame = 0; frame < 30 * 40 && !a.IsDone; frame++)
                {
                    a.Advance(1f / 30f);
                    b.Advance(1f / 60f);
                    b.Advance(1f / 60f);
                    if (a.IsDone || b.IsDone)
                    {
                        Assert.AreEqual(a.IsDone, b.IsDone, cutscene.Id + " ends on the same frame");
                        break;
                    }

                    Assert.AreEqual(a.BeatIndex, b.BeatIndex, cutscene.Id + " frame " + frame);
                    AssertClose(a.Camera, b.Camera, 2e-3f, cutscene.Id + " frame " + frame);
                    Assert.AreEqual(a.Letterbox, b.Letterbox, 1e-3f);
                    Assert.AreEqual(a.CaptionKey, b.CaptionKey);
                }
            }
        }

        [Test]
        public void BlendsStartWhereTheLastShotEndedAndNeverCrossBetweenStreetAndInterior()
        {
            var cutscene = Sample();
            var playback = new CutscenePlayback(cutscene, false);
            Assert.IsTrue(playback.BlendsFromPrevious(1));
            AssertClose(playback.PoseAt(0, 99f), playback.PoseAt(1, 0f), 1e-4f, "the blend starts on the last frame");
            AssertClose(new CamPose(P(3, 2, -6), P(0, 1, 0), 30f), playback.PoseAt(1, 0.6f), 1e-4f, "and lands on the shot");
            Assert.IsFalse(playback.BlendsFromPrevious(2), "a page-turn cuts");
            AssertClose(playback.PoseAt(1, 99f), playback.PoseAt(2, 0.01f), 1e-4f, "it holds under the covering page");
            AssertClose(cutscene.Beats[2].Shot.Start, playback.PoseAt(2, CutscenePlayback.PageCutDelay), 1e-4f, "then cuts");
            Assert.IsFalse(playback.BlendsFromPrevious(3));

            // The reveal crosses between the street and the interior: every crossing turns the page, never blends.
            var (state, answers) = Canonical();
            foreach (var reveal in new[] { C01Cutscenes.Reveal(state, answers) })
            {
                var p = new CutscenePlayback(reveal, false);
                for (int i = 1; i < reveal.Beats.Count; i++)
                {
                    var from = p.PoseAt(i - 1, 99f).Eye;
                    var to = p.PoseAt(i, 99f).Eye;
                    bool crosses = CaseRouteLayout.Inside(from.X, from.Z) != CaseRouteLayout.Inside(to.X, to.Z);
                    if (crosses)
                    {
                        Assert.AreEqual(ShotTransition.PageTurn, reveal.Beats[i].Shot.Transition, reveal.Beats[i].Id);
                        Assert.IsFalse(p.BlendsFromPrevious(i));
                    }
                }
            }
        }

        [Test]
        public void ACutIsReportedOnTheFrameItHappens()
        {
            var playback = new CutscenePlayback(Sample(), false);
            playback.Advance(0f);
            playback.Signal("walk.pip");
            var cuts = new List<int>();
            while (!playback.IsDone)
            {
                playback.Advance(1f / 60f);
                if (playback.CutThisStep)
                {
                    cuts.Add(playback.BeatIndex);
                }
            }

            CollectionAssert.AreEqual(new[] { 2, 3 }, cuts, "the page-turn and the cut; the blend is no cut");
        }

        [Test]
        public void SkipWorksAtAnyPointAndNothingFiresAfterIt()
        {
            var cutscene = C01Cutscenes.Opening;
            for (float at = 0f; at < cutscene.Duration + 2f; at += 0.37f)
            {
                var playback = new CutscenePlayback(cutscene, false);
                playback.Advance(0f);
                float t = 0f;
                while (t < at && playback.Playing)
                {
                    playback.Advance(1f / 60f);
                    t += 1f / 60f;
                }

                bool wasPlaying = playback.Playing;
                var skipped = playback.Skip();
                if (!wasPlaying)
                {
                    CollectionAssert.IsEmpty(skipped, "skip in the outro does nothing");
                    continue;
                }

                Assert.IsTrue(playback.WasSkipped);
                Assert.AreEqual(CutsceneEventKind.OutroStarted, skipped[0].Kind);
                Assert.AreEqual(CutscenePhase.Outro, playback.Phase);
                Assert.IsNull(playback.CaptionKey, "the caption goes with the skip");
                Assert.AreEqual(CutscenePlayback.SkipExitBlend, playback.OutroLength, 1e-5f);
                var after = new List<CutsceneEvent>();
                while (!playback.IsDone)
                {
                    after.AddRange(playback.Advance(1f / 30f));
                }

                CollectionAssert.AreEqual(new[] { CutsceneEventKind.Ended }, after.Select(e => e.Kind), "no cue after a skip");
                CollectionAssert.IsEmpty(playback.Skip(), "a second skip does nothing");
            }

            var before = new CutscenePlayback(cutscene, false);
            Assert.AreEqual(CutsceneEventKind.OutroStarted, before.Skip()[0].Kind, "skip before the first frame");
        }

        [Test]
        public void ReducedMotionCutsEveryBlendHoldsEveryFrameAndSkipsAtOnce()
        {
            var (state, answers) = Canonical();
            foreach (var cutscene in new[] { Sample(), C01Cutscenes.Opening, C01Cutscenes.Reveal(state, answers) })
            {
                var playback = new CutscenePlayback(cutscene, true);
                for (int i = 0; i < cutscene.Beats.Count; i++)
                {
                    Assert.IsFalse(playback.BlendsFromPrevious(i), cutscene.Id + " beat " + i);
                    var shot = cutscene.Beats[i].Shot;
                    if (shot.Anchor == null && (shot.Transition != ShotTransition.PageTurn || i == 0))
                    {
                        AssertClose(shot.End, playback.PoseAt(i, 0f), 1e-4f, cutscene.Id + " " + cutscene.Beats[i].Id + " holds");
                        AssertClose(shot.End, playback.PoseAt(i, cutscene.Beats[i].Duration * 0.5f), 1e-4f, "no drift");
                    }
                }

                playback.Advance(0f);
                Assert.AreEqual(1f, playback.Letterbox, "the bars are simply there");
                var skip = playback.Skip();
                Assert.IsTrue(playback.IsDone, "reduced motion skips without a blend back");
                CollectionAssert.AreEqual(new[] { CutsceneEventKind.OutroStarted, CutsceneEventKind.Ended }, skip.Select(e => e.Kind));
            }

            var played = Run(C01Cutscenes.Opening, 30f, reduced: true);
            Assert.AreEqual(CutsceneEventKind.Ended, played.Last().e.Kind);
            Assert.AreEqual(played.First(x => x.e.Kind == CutsceneEventKind.OutroStarted).time, played.Last().time, 1e-4f,
                "no blend back under reduced motion");
        }

        [Test]
        public void PauseFreezesEverything()
        {
            var playback = new CutscenePlayback(C01Cutscenes.Opening, false);
            playback.Advance(0f);
            playback.Advance(1.3f);
            var pose = playback.Camera;
            float elapsed = playback.Elapsed;
            for (int i = 0; i < 100; i++)
            {
                CollectionAssert.IsEmpty(playback.Advance(0f));
            }

            AssertClose(pose, playback.Camera, 0f, "paused");
            Assert.AreEqual(elapsed, playback.Elapsed);
        }

        [Test]
        public void AWaitPointHoldsForItsSignalOrItsLimit()
        {
            var cutscene = C01Cutscenes.Opening;
            int boxes = cutscene.Beats.ToList().FindIndex(b => b.Id == "boxes");
            Assert.AreEqual(CutsceneWorld.PipArrived, cutscene.Beats[boxes].WaitFor);
            float end = cutscene.BeatStart(boxes) + cutscene.Beats[boxes].Duration;

            // No signal: it holds for the full limit, then moves on.
            float moved = -1f;
            Run(cutscene, 60f, each: (p, t) =>
            {
                if (moved < 0f && p.BeatIndex > boxes)
                {
                    moved = p.Elapsed;
                }

                return true;
            });
            Assert.AreEqual(end + cutscene.Beats[boxes].MaxWait, moved, 0.02f);

            // The signal before the end: no hold.
            moved = -1f;
            Run(cutscene, 60f, each: (p, t) =>
            {
                if (p.BeatIndex == boxes && p.BeatTime > 1.5f)
                {
                    p.Signal(CutsceneWorld.PipArrived);
                }

                if (moved < 0f && p.BeatIndex > boxes)
                {
                    moved = p.Elapsed;
                }

                return true;
            });
            Assert.AreEqual(end, moved, 0.02f);

            // The signal during the hold releases it on that frame.
            var playback = new CutscenePlayback(cutscene, false);
            playback.Advance(0f);
            while (!playback.Waiting)
            {
                playback.Advance(1f / 30f);
            }

            Assert.AreEqual(boxes, playback.BeatIndex);
            playback.Advance(0.5f);
            Assert.IsTrue(playback.Waiting, "still holding");
            playback.Signal(CutsceneWorld.PipArrived);
            playback.Advance(1f / 30f);
            Assert.AreEqual(boxes + 1, playback.BeatIndex);
        }

        [Test]
        public void TheLetterboxSlidesInAndOut()
        {
            var playback = new CutscenePlayback(Sample(), false);
            playback.Advance(0f);
            Assert.AreEqual(0f, playback.Letterbox);
            playback.Advance(CutscenePlayback.LetterboxIn * 0.5f);
            Assert.AreEqual(0.5f, playback.Letterbox, 1e-3f);
            playback.Advance(CutscenePlayback.LetterboxIn);
            Assert.AreEqual(1f, playback.Letterbox);
            Assert.Greater(playback.Progress, 0f);
            playback.Skip();
            Assert.AreEqual(1f, playback.Letterbox, 1e-3f, "the outro starts full");
            playback.Advance(CutscenePlayback.SkipExitBlend * 0.5f);
            Assert.AreEqual(0.5f, playback.Letterbox, 1e-3f);
            playback.Advance(1f);
            Assert.AreEqual(0f, playback.Letterbox);
            Assert.IsTrue(playback.IsDone);
        }

        [Test]
        public void SecondsToPageTurnLeadsThePage()
        {
            var playback = new CutscenePlayback(Sample(), false);
            playback.Advance(0f);
            Assert.AreEqual(float.PositiveInfinity, playback.SecondsToPageTurn);
            playback.Signal("walk.pip");
            while (playback.BeatIndex < 1)
            {
                playback.Advance(1f / 60f);
            }

            float left = playback.SecondsToPageTurn;
            Assert.AreEqual(1.5f, left, 0.02f);
            playback.Advance(left - CutscenePlayback.PageCoverLead);
            Assert.AreEqual(CutscenePlayback.PageCoverLead, playback.SecondsToPageTurn, 1e-3f);
        }

        [Test]
        public void AudioCuesUseTheAudioBranchVocabulary()
        {
            var (state, answers) = Canonical();
            foreach (var cutscene in new[] { C01Cutscenes.Opening, C01Cutscenes.Reveal(state, answers) })
            {
                foreach (var cue in cutscene.Beats.SelectMany(b => b.Signals))
                {
                    if (cue.Channel == CueChannel.Babble)
                    {
                        Assert.IsTrue(CutsceneAudio.TryParseBabble(cue.Id, out _, out _), cue.Id + " is speaker.emotion");
                    }
                    else if (cue.Channel == CueChannel.Music)
                    {
                        CollectionAssert.Contains(new[] { CutsceneAudio.MusicStreetTheme, CutsceneAudio.MusicReveal }, cue.Id);
                    }
                }
            }

            Assert.AreEqual(CutsceneAudio.MusicStreetTheme, C01Cutscenes.Opening.Beats[0].Signals.Single(s => s.Channel == CueChannel.Music).Id);
            Assert.AreEqual(CutsceneAudio.MusicReveal,
                C01Cutscenes.Reveal(state, answers).Beats[0].Signals.Single(s => s.Channel == CueChannel.Music).Id);
            Assert.IsTrue(CutsceneAudio.TryParseBabble("kit.whisper", out var who, out var how));
            Assert.AreEqual(CastActor.Kit, who);
            Assert.AreEqual(BabbleEmotion.Whisper, how);
            Assert.IsFalse(CutsceneAudio.TryParseBabble("kit", out _, out _));
            Assert.IsFalse(CutsceneAudio.TryParseBabble("kit.grumpy", out _, out _));
            Assert.IsFalse(CutsceneAudio.TryParseBabble("babble.kit.happy", out _, out _));
        }

        [Test]
        public void DataRulesAreEnforced()
        {
            var shot = new CameraShot(P(0, 1, 0), P(0, 1, 1), 40f);
            Assert.Throws<ArgumentOutOfRangeException>(() => new CutsceneBeat("x", 0f, shot));
            Assert.Throws<ArgumentOutOfRangeException>(() => new CameraShot(P(0, 1, 0), P(0, 1, 1), 0.5f));
            Assert.Throws<ArgumentException>(() => CastCue.WalkTo(0f, CastActor.Pip, 1f, P(0, 0, 0)));
            Assert.Throws<ArgumentException>(() => new Cutscene("cs.x", new[] { new CutsceneBeat("x", 1f, shot).Babble(2f, "b") }, 0f));
            Assert.Throws<ArgumentException>(() => new Cutscene("cs.x", new[]
            {
                new CutsceneBeat("x", 1f, shot).Wait("s", 1f),
                new CutsceneBeat("y", 1f, new CameraShot(P(0, 1, 0), P(0, 1, 1), 40f, ShotTransition.PageTurn)),
            }, 0f), "a wait would move the page-turn's boundary");
        }

        [Test]
        public void AnchorFramesPlaceOffsetsInTheirOwnFrame()
        {
            var facingSouth = new AnchorFrame(P(0, 8, 11), 180f);
            var eye = facingSouth.Apply(P(1f, -1f, 6f));
            Assert.AreEqual(-1f, eye.X, 1e-4f, "its right is world -x when it faces -z");
            Assert.AreEqual(7f, eye.Y, 1e-4f);
            Assert.AreEqual(5f, eye.Z, 1e-4f, "out of its face is toward the street");
            var facingEast = new AnchorFrame(P(0, 0, 0), 90f).Apply(P(0f, 0f, 2f));
            Assert.AreEqual(2f, facingEast.X, 1e-4f);

            // A resolver moves the anchored shot with the model.
            var (state, answers) = Canonical();
            var reveal = C01Cutscenes.Reveal(state, answers);
            int owl = reveal.Beats.ToList().FindIndex(b => b.Id == "story:moved");
            var moved = new CutscenePlayback(reveal, false, id => id == CutsceneAnchors.Owl ? new AnchorFrame(P(0, 10, 11), 180f) : (AnchorFrame?)null);
            Assert.AreEqual(10f - 0.4f, moved.PoseAt(owl, 99f).Aim.Y, 1e-4f);
        }

        // ------------------------------------------------------------------------------------------ skip gate

        [Test]
        public void SkipWorksOnEveryControlSchemeWithAGentleSecondPress()
        {
            var gate = new CutsceneSkipGate();
            Assert.AreEqual(SkipGateResult.Ignored, gate.Press(SkipPress.Any, 0.1f), "the press that started it");
            Assert.AreEqual(SkipGateResult.Skip, gate.Press(SkipPress.Disc, 0.3f), "the disc skips at once (touch)");

            gate.Reset();
            Assert.AreEqual(SkipGateResult.Armed, gate.Press(SkipPress.Any, 1f), "a key, button or stray tap asks first");
            Assert.IsTrue(gate.Armed);
            Assert.AreEqual(SkipGateResult.Skip, gate.Press(SkipPress.Any, 2f), "the second press skips");

            gate.Reset();
            gate.Press(SkipPress.Any, 1f);
            gate.Tick(1f + CutsceneSkipGate.ArmSeconds + 0.1f);
            Assert.IsFalse(gate.Armed, "the question goes away");
            Assert.AreEqual(SkipGateResult.Armed, gate.Press(SkipPress.Any, 5f));
        }

        // ------------------------------------------------------------------------------------------ seen set

        [Test]
        public void SeenSetRoundTripsInItsOwnFileAndDropsUnknownIds()
        {
            var state = CaseState.FromFacts(new[] { CaseFact.HeardKnocks });
            var data = new CutsceneProgressData(new[] { C01Cutscenes.OpeningId, "cs.nope" }, new[] { CaseDefinition.StableId(CaseFact.HeardKnocks) });
            string json = CutsceneProgressData.Serialize(data);
            Assert.IsTrue(CutsceneProgressData.TryRead(json, out var read, out bool newer));
            Assert.IsFalse(newer);
            CollectionAssert.AreEquivalent(new[] { C01Cutscenes.OpeningId }, read.Seen);
            Assert.IsTrue(read.BelongsTo(state));
            StringAssert.DoesNotContain("Candlebrook", json, "never display text");
            Assert.AreEqual(SaveCheck.Newer, CutsceneProgressData.Check(
                "{\"schemaVersion\":9,\"caseId\":\"c01\",\"seenCutscenes\":[],\"basisFacts\":[]}"));
            Assert.AreEqual(SaveCheck.Invalid, CutsceneProgressData.Check("{"));
            Assert.AreNotEqual(SaveSlotPaths.ForCase("saves").Latest, CutsceneProgressStore.PathsFor("saves").Latest,
                "outside the case save");
            Assert.AreNotEqual(SpookyStreet.Core.Nonverbal.NonverbalProgressStore.PathsFor("saves").Latest,
                CutsceneProgressStore.PathsFor("saves").Latest, "beside the replay seen set, not in it");
        }

        [Test]
        public void ContinueAndReloadNeverReplayASeenCutscene()
        {
            var fs = new MemorySaveFileSystem();
            var store = new CutsceneProgressStore(fs, "saves");
            store.Load(CaseState.Empty);
            Assert.AreEqual(C01Cutscenes.OpeningId, CutscenePolicy.OnNewGame(store.Seen), "a new game plays the opening");
            store.MarkSeen(C01Cutscenes.OpeningId, CaseState.Empty);

            // Play on, quit, relaunch: Continue starts nothing, and the opening stays seen for this save.
            var later = CaseState.FromFacts(new[] { CaseFact.HeardKnocks, CaseFact.BeaSpoke });
            var relaunched = new CutsceneProgressStore(fs, "saves");
            CollectionAssert.AreEquivalent(new[] { C01Cutscenes.OpeningId }, relaunched.Load(later));
            Assert.IsNull(CutscenePolicy.OnContinue());
            Assert.IsNull(CutscenePolicy.OnNewGame(relaunched.Seen));

            // The reveal: marked before it plays; a reload of the stamped save never starts it again.
            var (stamped, _) = Canonical();
            relaunched.Load(stamped);
            relaunched.MarkSeen(C01Cutscenes.RevealId, stamped);
            var again = new CutsceneProgressStore(fs, "saves");
            CollectionAssert.Contains(again.Load(stamped), C01Cutscenes.RevealId);

            // A new game (explicit reset) or a replayed case (facts no longer extend the basis) clears it.
            again.Reset();
            CollectionAssert.IsEmpty(new CutsceneProgressStore(fs, "saves").Load(CaseState.Empty));
            relaunched = new CutsceneProgressStore(fs, "saves");
            relaunched.Load(stamped);
            relaunched.MarkSeen(C01Cutscenes.RevealId, stamped);
            relaunched.Observe(CaseState.Empty);
            CollectionAssert.IsEmpty(relaunched.Seen);
            CollectionAssert.IsEmpty(new CutsceneProgressStore(fs, "saves").Load(CaseState.Empty), "the clear was written");
            Assert.Throws<ArgumentException>(() => relaunched.MarkSeen("cs.nope", stamped));
        }

        // ------------------------------------------------------------------------------------------ policy

        [Test]
        public void TheRevealNeverRunsAheadOfTheDeduction()
        {
            var answers = CaseAnswers.Empty;
            foreach (var (q, set) in new[]
                     {
                         (N.QuestionEntry, new[] { N.ValeDoor, N.Cloth, N.SillSnag }),
                         (N.QuestionSource, new[] { N.FloorHum, N.WoodenPiece }),
                         (N.QuestionMoved, new[] { N.OwlBooth, N.LooseArm }),
                     })
            {
                answers = answers.With(q, set);
            }

            int stamped = 0;
            foreach (var state in CaseReducerTests.ReachableStates())
            {
                var reveal = C01Cutscenes.Reveal(state, answers);
                bool done = state.Has(CaseFact.CaseStamped);
                Assert.AreEqual(done, reveal != null, state.ToString());
                Assert.AreEqual(done, CutscenePolicy.Build(C01Cutscenes.RevealId, state, answers) != null);
                Assert.AreEqual(done, CutscenePolicy.CanRewatch(C01Cutscenes.RevealId, state), "the case page offers it only when stamped");
                Assert.IsTrue(CutscenePolicy.CanRewatch(C01Cutscenes.OpeningId, state), "the opening shows nothing to solve");
                Assert.IsNotNull(CutscenePolicy.Build(C01Cutscenes.OpeningId, state, answers));
                if (done)
                {
                    stamped++;
                    foreach (var question in N.Questions)
                    {
                        Assert.IsTrue(question.IsSolved(state), "every staged answer was deduced by the child");
                    }
                }

                foreach (var action in CasePlaythrough.AllActions())
                {
                    var step = state.Apply(action);
                    string auto = CutscenePolicy.AfterStep(step, Array.Empty<string>());
                    Assert.AreEqual(step.StampedCase ? C01Cutscenes.RevealId : null, auto, action + " from " + state);
                    Assert.IsNull(CutscenePolicy.AfterStep(step, new[] { C01Cutscenes.RevealId }), "seen: never again by itself");
                }
            }

            Assert.Greater(stamped, 0);
            Assert.IsNull(C01Cutscenes.Reveal(null, answers));
        }

        [Test]
        public void TheRevealIsBuiltFromTheChildsOwnStickers()
        {
            var entrySets = RightSets(N.QuestionEntry);
            var sourceSets = RightSets(N.QuestionSource);
            var movedSets = RightSets(N.QuestionMoved);
            Assert.Greater(entrySets.Count, 4);
            Assert.AreEqual(1, sourceSets.Count);
            Assert.AreEqual(2, movedSets.Count);
            int built = 0;
            foreach (var entry in entrySets)
            {
                foreach (var moved in movedSets)
                {
                    // The child's slot order is shuffled: the reveal still follows the comic's idea order.
                    var shuffled = entry.Reverse().ToArray();
                    var (state, answers) = Stamped(shuffled, sourceSets[0], moved);
                    var reveal = C01Cutscenes.Reveal(state, answers);
                    var expected = new List<string>();
                    expected.AddRange(CaseDeduction.Comic(N.Question(N.QuestionEntry), shuffled));
                    expected.AddRange(CaseDeduction.Comic(N.Question(N.QuestionSource), sourceSets[0]));
                    expected.AddRange(CaseDeduction.Comic(N.Question(N.QuestionMoved), moved));
                    CollectionAssert.AreEqual(expected, reveal.Stickers, "one shot per sticker the child placed");
                    CollectionAssert.DoesNotContain(reveal.Stickers, N.Streamer, "the decoy is never staged");

                    // Each sticker shot carries the child's sticker and its perception note; a story beat follows each question.
                    var ids = reveal.Beats.Select(b => b.Id).ToList();
                    int entryStory = ids.IndexOf("story:entry");
                    int sourceStory = ids.IndexOf("story:source");
                    int movedStory = ids.IndexOf("story:moved");
                    Assert.AreEqual(entry.Length, entryStory);
                    Assert.AreEqual(entryStory + 1 + sourceSets[0].Length, sourceStory);
                    Assert.AreEqual(sourceStory + 1 + moved.Length, movedStory);
                    CollectionAssert.AreEqual(new[] { "reopen", "seed" }, ids.Skip(movedStory + 1));
                    foreach (var beat in reveal.Beats.Where(b => b.StickerId != null))
                    {
                        Assert.AreEqual(N.Sticker(beat.StickerId).NoteKey, beat.CaptionKey);
                    }

                    Assert.GreaterOrEqual(reveal.Duration, 20f, "about 20–30 s");
                    Assert.LessOrEqual(reveal.Duration, 30f, "about 20–30 s");
                    built++;
                }
            }

            Assert.AreEqual(entrySets.Count * movedSets.Count, built);
        }

        [Test]
        public void TheRevealDiffersWhenTheChildChoseDifferentClues()
        {
            var a = Stamped(new[] { N.ValeDoor, N.Cloth, N.SillSnag }, new[] { N.FloorHum, N.WoodenPiece }, new[] { N.OwlBooth, N.LooseArm });
            var b = Stamped(new[] { N.StuckDoor, N.SillSnag, N.BeaFlash }, new[] { N.FloorHum, N.WoodenPiece }, new[] { N.OwlPanel, N.LooseArm });
            var revealA = C01Cutscenes.Reveal(a.state, a.answers);
            var revealB = C01Cutscenes.Reveal(b.state, b.answers);
            CollectionAssert.AreNotEqual(revealA.Stickers, revealB.Stickers);
            CollectionAssert.Contains(revealB.Stickers, N.BeaFlash);
            CollectionAssert.DoesNotContain(revealA.Stickers, N.BeaFlash);
            var bea = revealB.Beats.First(x => x.StickerId == N.BeaFlash);
            Assert.IsTrue(bea.Cast.Any(c => c.Actor == CastActor.Bea && c.Clip == CharacterClips.Point), "Bea points up high");
        }

        [Test]
        public void CutscenesAreShortAndUseOnlyExistingClips()
        {
            var allowed = new[]
            {
                CharacterClips.Walk, CharacterClips.Talk, CharacterClips.Point, CharacterClips.SitIdle, CharacterClips.SitTalk,
                CharacterClips.Wave, CharacterClips.OpenBooth, CharacterClips.KneeTap, CharacterClips.Idle,
            };
            var (state, answers) = Canonical();
            foreach (var cutscene in new[] { C01Cutscenes.Opening, C01Cutscenes.Reveal(state, answers) })
            {
                Assert.GreaterOrEqual(cutscene.Duration, Cutscene.MinSeconds);
                Assert.LessOrEqual(cutscene.Duration + cutscene.Beats.Sum(b => b.MaxWait) + cutscene.ExitBlend, Cutscene.MaxSeconds);
                foreach (var beat in cutscene.Beats)
                {
                    Assert.LessOrEqual(beat.Duration, 6f, beat.Id + ": one idea, a few seconds");
                    foreach (var cue in beat.Cast.Where(c => c.Clip != null))
                    {
                        CollectionAssert.Contains(allowed, cue.Clip, beat.Id + " uses only existing clips");
                    }
                }
            }

            float opening = C01Cutscenes.Opening.Duration;
            Assert.GreaterOrEqual(opening, 20f, "the opening is about 20–35 s");
            Assert.LessOrEqual(opening + 1.5f, 35f);
        }

        [Test]
        public void TheOpeningEndsOnTheArrivalPorchComposition()
        {
            var opening = C01Cutscenes.Opening;
            var last = opening.Beats.Last();
            AssertClose(C01Cutscenes.PorchPlayView, last.Shot.End, 1e-4f, "it settles into the play camera");
            var settle = opening.Settle.Single();
            Assert.AreEqual(CastActor.Pip, settle.Actor);
            Assert.AreEqual(CastCueKind.Place, settle.Kind);
            Assert.AreEqual(C01Cutscenes.PorchSpawn.Z, settle.Point.Z, 1e-4f, "played or skipped, play starts on the porch");
            Assert.IsTrue(CaseStage.OnArrivalPorch(settle.Point.X, settle.Point.Z));
            Assert.AreEqual(0f, settle.Yaw, "facing the owl house");
            var walk = opening.Beats.SelectMany(b => b.Cast).Single(c => c.Kind == CastCueKind.WalkTo);
            Assert.IsTrue(CaseStage.OnArrivalPorch(walk.Path[0].X, walk.Path[0].Z), "Pip steps out on the porch");
            Assert.IsFalse(CaseStage.ShouldHearKnocks(CaseState.Empty, 0f, walk.Path.Last().Z),
                "the walk stays behind the knock line: the three knocks happen in play");
            CollectionAssert.DoesNotContain(opening.Beats.SelectMany(b => b.Signals).Select(s => s.Id), CutsceneWorld.MechanismCycle,
                "no knocks in the opening (spec v1.4 §5)");
        }

        [Test]
        public void CaptionsAreKeysWithContextAndStateNoConclusion()
        {
            var table = StringTable.Parse(LocalizationTests.BundledEnglishJson());
            foreach (string key in Keys.All)
            {
                Assert.IsTrue(table.Contains(key), key);
                Assert.IsTrue(table.TryGet(key, out _));
            }

            CollectionAssert.IsSubsetOf(Keys.All, LocalizationKeys.All);
            var (state, answers) = Canonical();
            var forbidden = new[] { "because", "so june", "that's why", "that is why", "the answer", "solved", "got in by", "it was" };
            foreach (var cutscene in new[] { C01Cutscenes.Opening, C01Cutscenes.Reveal(state, answers) })
            {
                foreach (string key in cutscene.CaptionKeys)
                {
                    Assert.IsTrue(table.TryGet(key, out string text), key);
                    foreach (string word in forbidden)
                    {
                        StringAssert.DoesNotContain(word, text.ToLowerInvariant(), key + ": perception only, never a conclusion");
                    }
                }
            }

            foreach (string key in C01Cutscenes.Opening.CaptionKeys)
            {
                table.TryGet(key, out string text);
                foreach (string clue in new[] { "knock", "june", "silver", "owl", "flash" })
                {
                    StringAssert.DoesNotContain(clue, text.ToLowerInvariant(), key + ": the opening shows nothing the child could find");
                }
            }
        }
    }
}
