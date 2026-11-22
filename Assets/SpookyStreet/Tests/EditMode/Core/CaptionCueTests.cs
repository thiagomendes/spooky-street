using NUnit.Framework;
using SpookyStreet.Core.Case;
using Lines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;
using UiKeys = SpookyStreet.Core.Localization.LocalizationKeys.CaseUi;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// Caption direction cues (S002 interior polish): only sounds heard from somewhere get the arrow and the direction
    /// word, only when their source is away from Pip, and the word matches where the source is on screen.
    /// </summary>
    public class CaptionCueTests
    {
        [Test]
        public void OnlyTheKnocksAndTheHumAreDirectionalSounds()
        {
            CollectionAssert.AreEquivalent(new[] { Lines.CaptionArrivalKnocks, Lines.CaptionFoyerHum },
                CaptionDirection.DirectionalSoundKeys);
            foreach (string key in new[] { Lines.CaptionGrille, Lines.CaptionArmLinked, Lines.CaptionCrankGap, Lines.CaptionOwlBows })
            {
                Assert.IsFalse(CaptionDirection.IsDirectionalSound(key), key);
                Assert.IsFalse(CaptionDirection.ShouldCue(key, true, 20f), key + " never takes a cue");
            }

            Assert.IsFalse(CaptionDirection.IsDirectionalSound(null));
        }

        [Test]
        public void ADirectionalSoundNeedsASourceAwayFromPip()
        {
            Assert.IsTrue(CaptionDirection.ShouldCue(Lines.CaptionArrivalKnocks, true, 25f));
            Assert.IsTrue(CaptionDirection.ShouldCue(Lines.CaptionFoyerHum, true, CaptionDirection.MinSourceDistance));
            Assert.IsFalse(CaptionDirection.ShouldCue(Lines.CaptionFoyerHum, true, CaptionDirection.MinSourceDistance - 0.1f),
                "the hum right by Pip has no direction");
            Assert.IsFalse(CaptionDirection.ShouldCue(Lines.CaptionArrivalKnocks, false, 25f), "no source, no cue");
        }

        [TestCase(0f, 1f, 0f, 10f, CaptionBearing.Ahead)]
        [TestCase(0f, 1f, 6f, 6.5f, CaptionBearing.Ahead)]
        [TestCase(0f, 1f, 10f, 0f, CaptionBearing.Right)]
        [TestCase(0f, 1f, -10f, 2f, CaptionBearing.Left)]
        [TestCase(0f, 1f, 1f, -10f, CaptionBearing.Behind)]
        [TestCase(0f, -1f, 10f, 0f, CaptionBearing.Left)] // facing south, east is on the left
        [TestCase(1f, 0f, 0f, -10f, CaptionBearing.Right)] // facing east, south is on the right
        public void TheWordMatchesTheScreen(float fx, float fz, float tx, float tz, CaptionBearing expected)
        {
            float angle = CaptionDirection.SignedAngle(fx, fz, tx, tz);
            Assert.AreEqual(expected, CaptionDirection.BearingOf(angle), $"angle {angle}");
        }

        [Test]
        public void EachBearingHasItsWord()
        {
            Assert.AreEqual(UiKeys.DirectionAhead, CaptionDirection.WordKey(CaptionBearing.Ahead));
            Assert.AreEqual(UiKeys.DirectionLeft, CaptionDirection.WordKey(CaptionBearing.Left));
            Assert.AreEqual(UiKeys.DirectionRight, CaptionDirection.WordKey(CaptionBearing.Right));
            Assert.AreEqual(UiKeys.DirectionBehind, CaptionDirection.WordKey(CaptionBearing.Behind));
        }
    }
}
