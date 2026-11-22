using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Localization;
using UiKeys = SpookyStreet.Core.Localization.LocalizationKeys.CaseUi;

namespace SpookyStreet.Tests.Core
{
    /// <summary>S002 wiring: the pure pieces the world uses (stage, target picking, captions, dialogue gate).</summary>
    public class CaseWiringTests
    {
        [Test]
        public void EveryCaseUiKeyIsInBundledEnglishWithContext()
        {
            var table = StringTable.Parse(LocalizationTests.BundledEnglishJson());
            var contexts = new Dictionary<string, string>();
            foreach (var entry in table.Entries)
            {
                contexts[entry.Key] = entry.Context;
            }

            foreach (string key in UiKeys.All)
            {
                Assert.IsTrue(contexts.ContainsKey(key), "en.json is missing " + key);
                Assert.IsFalse(string.IsNullOrWhiteSpace(contexts[key]), key + " needs translator context");
            }

            foreach (Speaker speaker in System.Enum.GetValues(typeof(Speaker)))
            {
                string name = CaseSpeakers.NameKey(speaker);
                Assert.AreEqual(speaker == Speaker.Caption, name == null, speaker.ToString());
                if (name != null)
                {
                    Assert.IsTrue(table.Contains(name), name);
                }
            }

            foreach (var entry in CaseNotebook.Entries)
            {
                string tag = CaseNotebookTags.TagKey(entry.Id);
                Assert.AreEqual(CaseNotebook.IsQuestion(entry.Id), tag == null, entry.Id);
                if (tag != null)
                {
                    Assert.IsTrue(table.Contains(tag), tag);
                }
            }
        }

        [Test]
        public void ExteriorPointsCoverTheExteriorBeatsWithVerbs()
        {
            var kinds = new HashSet<CaseActionKind>();
            var speakers = new HashSet<Speaker>();
            foreach (var point in CaseStage.ExteriorPoints)
            {
                kinds.Add(point.Action.Kind);
                if (point.Action.Kind == CaseActionKind.Talk)
                {
                    speakers.Add(point.Action.Speaker);
                }

                Assert.IsNotNull(point.VerbKey, point.Id + " needs a Use verb");
                Assert.Greater(point.Reach, 0.5f, point.Id);
            }

            CollectionAssert.IsSupersetOf(kinds, new[]
            {
                CaseActionKind.Talk, CaseActionKind.SitOnBench, CaseActionKind.ExamineStreamer,
                CaseActionKind.TryFrontDoor, CaseActionKind.ExamineCandyBowl,
            });
            CollectionAssert.AreEquivalent(new[] { Speaker.Bea, Speaker.Vale }, speakers);
            Assert.IsFalse(kinds.Contains(CaseActionKind.LiftLatch), "the latch is inside: not offered from the porch");
        }

        [Test]
        public void NothingIsInReachAtTheArrivalSpawn()
        {
            // The Use disc must read "Roll" when a new game starts (S000.6 tests).
            Assert.IsTrue(CaseStage.TryGetAnchor(CaseAnchors.ArrivalPorch, out var spawn));
            Assert.AreEqual(-1, UseTargetPicker.Pick(spawn.X, spawn.Y, spawn.Z, 0f, 1f, CaseStage.ExteriorPoints));
        }

        [Test]
        public void PickerPrefersTargetsInReachAndTowardTheCamera()
        {
            var bea = CaseStage.Find(CaseStage.PointBea);
            var points = CaseStage.ExteriorPoints;
            int index = UseTargetPicker.Pick(bea.Position.X + 1f, 0f, bea.Position.Z, -1f, 0f, points);
            Assert.AreSame(bea, points[index]);
            Assert.AreEqual(-1, UseTargetPicker.Pick(bea.Position.X + 3f, 0f, bea.Position.Z, -1f, 0f, points));
            Assert.AreEqual(-1, UseTargetPicker.Pick(bea.Position.X + 1f, 3f, bea.Position.Z, -1f, 0f, points),
                "a target far below Pip is not offered");

            // Bench seat and Mr. Vale are close together: the one the camera looks at wins at equal distance.
            var bench = CaseStage.Find(CaseStage.PointBench);
            var vale = CaseStage.Find(CaseStage.PointVale);
            float mx = (bench.Position.X + vale.Position.X) * 0.5f;
            float mz = (bench.Position.Z + vale.Position.Z) * 0.5f;
            // Stand 1 m off the line between them, equally far from both, on the plaza side (lower X).
            float sx = vale.Position.X - bench.Position.X, sz = vale.Position.Z - bench.Position.Z;
            float len = (float)System.Math.Sqrt(sx * sx + sz * sz);
            float nx = -sz / len, nz = sx / len;
            if (nx > 0f)
            {
                nx = -nx;
                nz = -nz;
            }

            float px = mx + nx, pz = mz + nz;
            int toBench = UseTargetPicker.Pick(px, 0f, pz, bench.Position.X - px, bench.Position.Z - pz, points);
            int toVale = UseTargetPicker.Pick(px, 0f, pz, vale.Position.X - px, vale.Position.Z - pz, points);
            Assert.AreSame(bench, points[toBench]);
            Assert.AreSame(vale, points[toVale]);
        }

        [Test]
        public void AnchorsRestoreOnStageAndFallBackToThePlaza()
        {
            var state = CaseState.Empty;
            var porch = CaseStage.RestorePoint(CaseAnchors.ArrivalPorch, state, out string id);
            Assert.AreEqual(CaseAnchors.ArrivalPorch, id);
            Assert.AreEqual(-13.4f, porch.Z, 0.01f);
            CaseStage.RestorePoint("c01.anchor.nowhere", state, out id);
            Assert.AreEqual(CaseAnchors.Plaza, id, "unknown anchor → plaza");
            CaseStage.RestorePoint(CaseAnchors.Attic, state, out id);
            Assert.AreEqual(CaseAnchors.Plaza, id, "inside without the window entry → plaza");
            var inside = CaseState.FromFacts(new[] { CaseFact.EnteredByWindow });
            CaseStage.RestorePoint(CaseAnchors.Workshop, inside, out id);
            Assert.AreEqual(CaseAnchors.Plaza, id, "rooms that are not built yet restore to the plaza");

            Assert.AreEqual(CaseAnchors.Bench, CaseStage.NearestAnchor(7f, 1f));
            Assert.AreEqual(CaseAnchors.ArrivalPorch, CaseStage.NearestAnchor(0.5f, -12.8f));
            Assert.AreEqual(CaseAnchors.BicycleCorner, CaseStage.NearestAnchor(-6.5f, -1f));
            foreach (string anchor in CaseStage.StagedAnchors)
            {
                Assert.IsTrue(CaseAnchors.IsKnown(anchor), anchor);
                Assert.IsFalse(CaseAnchors.IsInside(anchor), anchor);
            }
        }

        [Test]
        public void ArrivalCuePlaysOnTheFirstStepsOrWhenWalkingPast()
        {
            var empty = CaseState.Empty;
            Assert.IsFalse(CaseStage.ShouldHearKnocks(empty, 0f, -13.4f), "not before Pip moves");
            Assert.IsTrue(CaseStage.ShouldHearKnocks(empty, CaseStage.FirstStepsDistance, -13.4f), "first steps");
            Assert.IsTrue(CaseStage.ShouldHearKnocks(empty, 0f, -5f), "already past the porch (e.g. resumed)");
            var heard = empty.Apply(CaseAction.Of(CaseActionKind.HearKnocks)).State;
            Assert.IsFalse(CaseStage.ShouldHearKnocks(heard, 10f, 0f), "plays once");
            Assert.IsTrue(CaseStage.OnArrivalPorch(0f, -13.4f));
            Assert.IsFalse(CaseStage.OnArrivalPorch(0f, -5f));
        }

        [Test]
        public void CaptionDirectionNamesWhereTheSoundIs()
        {
            Assert.AreEqual(0f, CaptionDirection.SignedAngle(0f, 1f, 0f, 5f), 0.01f);
            Assert.AreEqual(90f, CaptionDirection.SignedAngle(0f, 1f, 3f, 0f), 0.01f);
            Assert.AreEqual(-90f, CaptionDirection.SignedAngle(0f, 1f, -3f, 0f), 0.01f);
            Assert.AreEqual(CaptionBearing.Ahead, CaptionDirection.BearingOf(30f));
            Assert.AreEqual(CaptionBearing.Right, CaptionDirection.BearingOf(90f));
            Assert.AreEqual(CaptionBearing.Left, CaptionDirection.BearingOf(-60f));
            Assert.AreEqual(CaptionBearing.Behind, CaptionDirection.BearingOf(-170f));
            Assert.AreEqual(UiKeys.DirectionBehind, CaptionDirection.WordKey(CaptionBearing.Behind));
            Assert.AreEqual(0f, CaptionDirection.SignedAngle(0f, 0f, 0f, 0f));
        }

        [Test]
        public void HoldingTheButtonNeverAdvancesALine()
        {
            var gate = new DialogueAdvanceGate();
            gate.Begin(held: true); // the Use press that opened the conversation is still down
            for (int i = 0; i < 120; i++)
            {
                Assert.IsFalse(gate.Step(1f / 60f, held: true, pressedThisFrame: i == 0, tapped: false), "frame " + i);
            }

            Assert.IsFalse(gate.Step(1f / 60f, held: false, pressedThisFrame: false, tapped: false), "release");
            Assert.IsTrue(gate.Step(1f / 60f, held: true, pressedThisFrame: true, tapped: false), "a fresh press");

            gate.Begin(held: true);
            Assert.IsFalse(gate.Step(1f, held: true, pressedThisFrame: false, tapped: false), "still held: next line waits");
        }

        [Test]
        public void ALineShowsBrieflyBeforeAnyAdvance()
        {
            var gate = new DialogueAdvanceGate();
            gate.Begin(held: false);
            Assert.IsFalse(gate.Step(0.05f, held: false, pressedThisFrame: false, tapped: true), "too soon after it appeared");
            Assert.IsFalse(gate.Step(0f, held: false, pressedThisFrame: false, tapped: true), "paused time does not count");
            Assert.IsTrue(gate.Step(DialogueAdvanceGate.MinShowSeconds, held: false, pressedThisFrame: false, tapped: true));
            gate.Begin(held: false);
            Assert.IsTrue(gate.Step(1f, held: true, pressedThisFrame: true, tapped: false), "a press after a released start");
        }
    }
}
