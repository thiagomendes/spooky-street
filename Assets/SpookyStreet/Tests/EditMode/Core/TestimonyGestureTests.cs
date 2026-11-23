using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpookyStreet.Core.Animation;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Characters;
using SpookyStreet.Core.Nonverbal;
using Lines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;

namespace SpookyStreet.Tests.Core
{
    /// <summary>S003a: which lines play a testimony gesture (<see cref="LineGestures"/>), and Pip's glance on a hint.</summary>
    public class TestimonyGestureTests
    {
        private static readonly Dictionary<string, string> Expected = new Dictionary<string, string>
        {
            { Lines.BeaSawCape, CharacterClips.ShadeEyes },
            { Lines.BeaRepeat, CharacterClips.ShadeEyes },
            { Lines.ValeRepeat, CharacterClips.CheckWatch },
            { Lines.ValeWasShut, CharacterClips.CheckWatch },
            { Lines.JuneGreet, CharacterClips.Listen },
            { Lines.JuneGreetShortcut, CharacterClips.Listen },
            { Lines.JuneListenFloor, CharacterClips.Listen },
            { Lines.KitAsk, CharacterClips.Ponder },
        };

        [Test]
        public void TheTestimonyLinesCueTheirSpeakersGesture()
        {
            CollectionAssert.AreEquivalent(Expected.Keys, LineGestures.LineKeys);
            foreach (var pair in Expected)
            {
                Assert.IsTrue(LineGestures.TryGet(pair.Key, out var gesture), pair.Key);
                Assert.AreEqual(pair.Value, gesture.Clip, pair.Key);
            }

            // Bea on "Something silver flashed up high", Vale on "Not one creak from that door all evening".
            Assert.IsTrue(LineGestures.TryGet(Lines.BeaSawCape, out var bea));
            Assert.AreEqual(CharacterClips.ShadeEyes, bea.Clip);
            Assert.IsTrue(LineGestures.TryGet(Lines.ValeRepeat, out var vale));
            Assert.AreEqual(CharacterClips.CheckWatch, vale.Clip);

            // Everything else stays still: captions, Pip's lines, the invitation, the closed lines, unknown keys.
            foreach (var key in new[]
                     {
                         Lines.ValeDoorShut, Lines.ValeInviteSit, Lines.BeaClosed, Lines.JuneClosed, Lines.KitThanks,
                         Lines.ExamineCloth, Lines.CaptionArrivalKnocks, Lines.BeaRemembers, "c01.line.nobody", null,
                     })
            {
                Assert.IsFalse(LineGestures.TryGet(key, out _), key ?? "null");
            }
        }

        [Test]
        public void EveryGestureBelongsToItsSpeakerAndFitsTheLine()
        {
            foreach (string key in LineGestures.LineKeys)
            {
                Assert.IsNotNull(CaseScript.ConditionFor(key), key + " is a case line");
                var speaker = LineGestures.SpeakerOf(key);
                Assert.IsTrue(speaker.HasValue, key);
                Assert.IsFalse(CaseSpeakers.IsCaption(speaker.Value), key + " is spoken");
                Assert.IsTrue(LineGestures.TryGet(key, out var gesture));
                Assert.AreEqual(CharacterClips.TestimonyGestureOf(speaker.Value.ToString()), gesture.Clip,
                    key + ": the speaker's own testimony gesture");
                Assert.IsFalse(CharacterClips.IsLooping(gesture.Clip), key + ": plays once");
                Assert.That(gesture.Delay, Is.InRange(0f, LineGestures.MaxDelay), key);

                // Testimony, not celebration: nothing once the case is explained (the stamp).
                CollectionAssert.DoesNotContain(CaseScript.ConditionFor(key).Requires, CaseFact.CaseStamped, key);
                CollectionAssert.DoesNotContain(CaseScript.ConditionFor(key).Requires, CaseFact.CaseClosed, key);

                // Only a gesture aimed along the body has a place to turn to; the others keep the speaker's facing.
                Assert.AreEqual(CharacterClips.TurnsToAim(gesture.Clip), gesture.Aim.HasValue, key);
            }

            CollectionAssert.AreEquivalent(CharacterClips.TestimonyGestures,
                LineGestures.LineKeys.Select(k => { LineGestures.TryGet(k, out var g); return g.Clip; }).Distinct(),
                "each of the four gestures is used");
        }

        [Test]
        public void GesturesPlayOnlyWhereTheyAddMeaning()
        {
            foreach (var speaker in new[] { Speaker.Bea, Speaker.Vale, Speaker.June, Speaker.Kit })
            {
                var lines = CaseScript.AllLineKeys.Where(k => LineGestures.SpeakerOf(k) == speaker).ToList();
                int gestured = lines.Count(k => LineGestures.TryGet(k, out _));
                Assert.Greater(gestured, 0, speaker + " gestures somewhere");
                Assert.Less(gestured * 2, lines.Count, speaker + ": not on every line (fewer than half)");
            }

            // In any one conversation at most one line gestures, so a talk never turns into a dance.
            var states = new[] { CaseState.Empty }.ToList();
            foreach (var action in CanonicalRoute())
            {
                states.Add(states[states.Count - 1].Apply(action).State);
            }

            foreach (var state in states)
            {
                foreach (var speaker in new[] { Speaker.Bea, Speaker.Vale, Speaker.June })
                {
                    var talk = CaseScript.Conversation(speaker, state);
                    Assert.LessOrEqual(talk.Count(l => LineGestures.TryGet(l.Key, out _)), 1, speaker + " in one talk");
                }
            }
        }

        [Test]
        public void BeaTurnsToThePlaceHerWordsNameNeverToAThing()
        {
            Assert.IsTrue(LineGestures.TryGet(Lines.BeaSawCape, out var gesture));
            var aim = gesture.Aim.Value;
            var yard = HintAreas.Get(HintAreas.SideYard);
            Assert.AreEqual(yard.Centre.X, aim.X, 1e-4f, "over the side yard (behind the hedge, by the side of the house)");
            Assert.AreEqual(yard.Centre.Z, aim.Z, 1e-4f);
            Assert.Greater(aim.Y, 2.5f, "up high");
            foreach (var point in CaseStage.AllPoints)
            {
                float dx = point.Position.X - aim.X, dy = point.Position.Y - aim.Y, dz = point.Position.Z - aim.Z;
                Assert.GreaterOrEqual((float)Math.Sqrt(dx * dx + dy * dy + dz * dz), HintAreas.MinClearance,
                    "clear of " + point.Id);
            }
        }

        [Test]
        public void SpeakerOfReadsTheKey()
        {
            Assert.AreEqual(Speaker.Bea, LineGestures.SpeakerOf(Lines.BeaRepeat));
            Assert.AreEqual(Speaker.Vale, LineGestures.SpeakerOf(Lines.ValeWasShut));
            Assert.AreEqual(Speaker.June, LineGestures.SpeakerOf(Lines.JuneListenFloor));
            Assert.AreEqual(Speaker.Kit, LineGestures.SpeakerOf(Lines.KitAsk));
            Assert.IsNull(LineGestures.SpeakerOf("c01.caption.knocks"));
            Assert.IsNull(LineGestures.SpeakerOf(null));
            Assert.IsNull(LineGestures.SpeakerOf("c01.line."));
        }

        // -------------------------------------------------------------------------------------- hint glance

        [Test]
        public void PipGlancesOnlyForTheTierOneNudgeWhileStanding()
        {
            var settings = new NonverbalSettings();
            bool nudge = HintPresentation.For(HintTrigger.RadioPress, HintTier.Observation, settings).Breeze;
            bool trail = HintPresentation.For(HintTrigger.RadioPress, HintTier.Location, settings).Breeze;
            Assert.IsTrue(nudge);
            Assert.IsFalse(trail);

            // Facing +Z, the area 5 m to her left or right.
            Assert.AreEqual(PipClips.GlanceLeft, HintPresentation.PipGlance(nudge, true, 0f, false, 0f, 1f, -5f, 1f));
            Assert.AreEqual(PipClips.GlanceRight, HintPresentation.PipGlance(nudge, true, 0f, false, 0f, 1f, 5f, 1f));
            Assert.AreEqual(PipClips.GlanceRight, HintPresentation.PipGlance(nudge, true, 0.5f, false, 0f, 1f, 5f, -2f),
                "behind and to the right: the side she would turn to");

            Assert.IsNull(HintPresentation.PipGlance(trail, true, 0f, false, 0f, 1f, -5f, 1f), "the bats lead; no glance");
            Assert.IsNull(HintPresentation.PipGlance(nudge, false, 0f, false, 0f, 1f, -5f, 1f), "in the air");
            Assert.IsNull(HintPresentation.PipGlance(nudge, true, PipClips.GlanceMaxSpeed + 0.1f, false, 0f, 1f, -5f, 1f),
                "walking");
            Assert.IsNull(HintPresentation.PipGlance(nudge, true, 0f, true, 0f, 1f, -5f, 1f), "reduced motion");
            Assert.IsNull(HintPresentation.PipGlance(nudge, true, 0f, false, 0f, 1f, 1f, 5f), "the area is ahead of her");
            Assert.IsNull(HintPresentation.PipGlance(nudge, true, 0f, false, 0f, 1f, 0f, 0f), "no direction");
            Assert.AreEqual(PipClips.GlanceRight,
                HintPresentation.PipGlance(nudge, true, 0f, false, 0f, 1f, 5f * (float)Math.Sin(0.6), 5f * (float)Math.Cos(0.6)),
                "34° to the right is to one side");
        }

        [Test]
        public void PipGlancesTowardTheAreaNeverAnObject()
        {
            foreach (var area in HintAreas.All)
            {
                var target = HintPresentation.GlanceTarget(area);
                Assert.AreEqual(area.Centre.X, target.X);
                Assert.AreEqual(area.Centre.Z, target.Z);
                Assert.GreaterOrEqual(HintAreas.Clearance(area, out _), HintAreas.MinClearance, area.Id);
            }

            Assert.Throws<ArgumentNullException>(() => HintPresentation.GlanceTarget(null));

            // The same nudge from the same spot picks the side the area lies on, whichever objective it serves.
            var yard = HintAreas.For(CaseObjective.FindWayIn);
            float pipX = -8.9f, pipZ = 1.4f;
            var t = HintPresentation.GlanceTarget(yard);
            Assert.AreEqual(PipClips.GlanceLeft, HintPresentation.PipGlance(true, true, 0f, false, 1f, 0f, t.X - pipX, t.Z - pipZ),
                "facing east on the west sidewalk, the side yard is to her left");
        }

        private static CaseAction[] CanonicalRoute() => new[]
        {
            CaseAction.Of(CaseActionKind.HearKnocks), CaseAction.Talk(Speaker.Bea), CaseAction.Talk(Speaker.Bea),
            CaseAction.Talk(Speaker.Vale), CaseAction.Talk(Speaker.Vale), CaseAction.Of(CaseActionKind.SitOnBench),
            CaseAction.Of(CaseActionKind.ExamineCloth), CaseAction.Of(CaseActionKind.EnterAttic),
            CaseAction.Of(CaseActionKind.ExamineSill), CaseAction.Talk(Speaker.June), CaseAction.Talk(Speaker.June),
            CaseAction.Of(CaseActionKind.EnterFoyer), CaseAction.Of(CaseActionKind.LiftLatch), CaseAction.Talk(Speaker.June),
            CaseAction.Talk(Speaker.Vale), CaseAction.Of(CaseActionKind.ExamineGrille), CaseAction.Of(CaseActionKind.LinkArm),
            CaseAction.Of(CaseActionKind.TurnCrank), CaseAction.Talk(Speaker.June),
        };
    }
}
