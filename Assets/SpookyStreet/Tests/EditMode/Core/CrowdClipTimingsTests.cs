using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Animation;
using SpookyStreet.Core.Crowd;

namespace SpookyStreet.Tests.Core
{
    /// <summary>The trick-or-treat clip contract (CrowdClipTimings) the game sequences on; the import tests check the
    /// committed FBX files against the same numbers.</summary>
    public class CrowdClipTimingsTests
    {
        [Test]
        public void TheBriefsNamesLengthsAndMarkers()
        {
            AssertClip(CrowdClipTimings.GiveTreat, 48, 0.55f);
            AssertClip(CrowdClipTimings.OpenDoorPeek, 36, 0.25f);
            AssertClip(CrowdClipTimings.HoldPail, 48, 0.6f);
            AssertClip(CrowdClipTimings.Cheer, 27, 0.45f);
            AssertClip(CrowdClipTimings.WalkStart, 12, -1f);
            AssertClip(CrowdClipTimings.WalkStop, 12, -1f);
            AssertClip(CrowdClipTimings.HandTreat, 48, 0.55f);
            Assert.AreEqual("GiveTreat", CrowdClipTimings.GiveTreat);
            Assert.AreEqual("OpenDoorPeek", CrowdClipTimings.OpenDoorPeek);
            Assert.AreEqual("HoldPail", CrowdClipTimings.HoldPail);
            Assert.AreEqual("Cheer", CrowdClipTimings.Cheer);
            Assert.AreEqual("WalkStart", CrowdClipTimings.WalkStart);
            Assert.AreEqual("WalkStop", CrowdClipTimings.WalkStop);
            Assert.AreEqual(PipClips.HandTreat, CrowdClipTimings.HandTreat);
            Assert.That(CrowdClipTimings.Of("GiveTreat").Value.Seconds, Is.EqualTo(1.6f).Within(1e-4f));
            Assert.That(CrowdClipTimings.Of("GiveTreat").Value.MarkerSeconds, Is.EqualTo(0.88f).Within(1e-4f));
            Assert.IsNull(CrowdClipTimings.Of("Idle"));
        }

        private static void AssertClip(string name, int frames, float marker)
        {
            var t = CrowdClipTimings.Of(name);
            Assert.IsTrue(t.HasValue, name);
            Assert.AreEqual(frames, t.Value.Frames, name + " frames");
            Assert.AreEqual(marker, t.Value.Marker, 1e-5f, name + " marker");
            Assert.IsFalse(CharacterClips.IsLooping(name), name + " plays once");
        }

        [Test]
        public void EachBodyHasItsClipsAndTheNeighboursShareTheGrownUps()
        {
            var child = Names(CrowdClipTimings.For(CrowdKit.ChildModel));
            var adult = Names(CrowdClipTimings.For(CrowdKit.AdultModel));
            CollectionAssert.AreEquivalent(new[] { "HoldPail", "Cheer", "WalkStart", "WalkStop" }, child);
            CollectionAssert.AreEquivalent(new[] { "GiveTreat", "OpenDoorPeek", "WalkStart", "WalkStop" }, adult);
            CollectionAssert.AreEquivalent(adult, Names(CrowdClipTimings.For(CrowdKit.NeighbourModel)));
            Assert.AreEqual(0, CrowdClipTimings.For("Pip").Count);
            foreach (var name in child)
            {
                CollectionAssert.DoesNotContain(CrowdKit.Clips, name, "the shipped clips stay as they are");
            }
        }

        private static List<string> Names(IReadOnlyList<CrowdClipTiming> set)
        {
            var names = new List<string>();
            foreach (var t in set)
            {
                names.Add(t.Name);
            }

            return names;
        }

        [Test]
        public void TheSweetGoesFromTheGiversHandIntoTheChildsPail()
        {
            // the release sits a little above the held-out pail's mouth, and the treat lands as the child bobs
            float drop = CrowdClipTimings.GiveTreatReleaseHeight - CrowdClipTimings.HoldPailMouthHeight;
            Assert.That(drop, Is.InRange(0.02f, 0.1f));
            Assert.That(CrowdClipTimings.HandTreatReleaseHeight - CrowdClipTimings.HoldPailMouthHeight, Is.InRange(0.02f, 0.1f));
            float release = CrowdClipTimings.GiveTreatRelease * CrowdClipTimings.GiveTreatFrames / CrowdClipTimings.Fps;
            float landed = CrowdClipTimings.HoldPailDrop * CrowdClipTimings.HoldPailFrames / CrowdClipTimings.Fps;
            Assert.That(landed - release, Is.InRange(0.03f, 0.15f), "started together, the bob follows the release");
            Assert.Less(CrowdClipTimings.GiveTreatPickUp, CrowdClipTimings.GiveTreatRelease);
            Assert.Less(CrowdClipTimings.HandTreatPickUp, CrowdClipTimings.HandTreatRelease);
            Assert.That(CrowdClipTimings.GiveTreatReleaseHeight, Is.InRange(0.55f, 0.7f), "a child's pail height");
            Assert.That(CrowdClipTimings.GiveTreatReleaseForward, Is.EqualTo(0.45f).Within(1e-4f));
            Assert.Less(CrowdClipTimings.OpenDoorPeekHandOnDoor, CrowdClipTimings.OpenDoorPeekLetGo);
        }

        [TestCase(CrowdKit.ChildModel, 1.167f)]
        [TestCase(CrowdKit.AdultModel, 1.173f)]
        [TestCase(CrowdKit.NeighbourModel, 1.173f)]
        public void WalkTransitionsTravelFromStandingToWalkSpeedAndBack(string model, float speed)
        {
            Assert.AreEqual(speed, CrowdKit.WalkSpeed(model), 1e-4f);
            foreach (var clip in new[] { CrowdClipTimings.WalkStart, CrowdClipTimings.WalkStop })
            {
                float d = CrowdClipTimings.WalkTransitionDistance(clip, model);
                Assert.That(d, Is.InRange(0.1f, 0.4f), clip);
                Assert.AreEqual(0f, CrowdClipTimings.RootTravel(clip, model, 0f), 1e-6f);
                Assert.AreEqual(d, CrowdClipTimings.RootTravel(clip, model, 1f), 1e-5f);
                float seconds = CrowdClipTimings.WalkStartFrames / CrowdClipTimings.Fps;
                float prev = 0f;
                for (int i = 1; i <= 100; i++)
                {
                    float x = CrowdClipTimings.RootTravel(clip, model, i / 100f);
                    Assert.GreaterOrEqual(x, prev - 1e-6f, clip + ": never back");
                    prev = x;
                }

                // speed (m/s) at each end: standing at one, Walk speed at the other
                const float h = 0.002f;
                float v0 = CrowdClipTimings.RootTravel(clip, model, h) / h / seconds;
                float v1 = (d - CrowdClipTimings.RootTravel(clip, model, 1f - h)) / h / seconds;
                float atRest = clip == CrowdClipTimings.WalkStart ? v0 : v1;
                float walking = clip == CrowdClipTimings.WalkStart ? v1 : v0;
                Assert.Less(atRest, 0.15f, clip + " standing end");
                Assert.AreEqual(speed, walking, 0.03f, clip + " walking end");
            }

            float phase = CrowdClipTimings.WalkStartEndPhase(model);
            float frames = model == CrowdKit.ChildModel ? 24f : 41f;
            Assert.AreEqual(System.Math.Round(phase * frames), phase * frames, 1e-4, "a whole Walk frame");
            Assert.AreEqual(0f, CrowdClipTimings.RootTravel("Wave", model, 0.5f));
        }
    }
}
