using System;
using System.Linq;
using NUnit.Framework;
using SpookyStreet.Core.Audio;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Nonverbal;

namespace SpookyStreet.Tests.Core
{
    /// <summary>The dialogue portrait atlas has one badge per speaker and babble emotion (S004 §2.6).</summary>
    public class PortraitAtlasLayoutTests
    {
        [Test]
        public void EverySpeakerHasEveryEmotion()
        {
            var speakers = ((Speaker[])Enum.GetValues(typeof(Speaker))).Where(s => s != Speaker.Caption).ToArray();
            var emotions = (BabbleEmotion[])Enum.GetValues(typeof(BabbleEmotion));
            CollectionAssert.AreEquivalent(speakers, PortraitAtlasLayout.Speakers);
            CollectionAssert.AreEquivalent(emotions, PortraitAtlasLayout.Emotions);
            Assert.AreEqual(speakers.Length * emotions.Length, PortraitAtlasLayout.All.Count);
            foreach (var speaker in speakers)
            {
                foreach (var emotion in emotions)
                {
                    Assert.IsTrue(PortraitAtlasLayout.TryGet(speaker, emotion, out var rect), speaker + " " + emotion);
                    Assert.AreEqual(speaker, rect.Speaker);
                    Assert.AreEqual(emotion, rect.Emotion);
                    string id = "portrait." + speaker.ToString().ToLowerInvariant() + "." +
                                emotion.ToString().ToLowerInvariant();
                    Assert.AreEqual(id, rect.Id);
                    Assert.AreEqual(id, PortraitAtlasLayout.SpriteName(speaker, emotion));
                    Assert.IsTrue(PortraitAtlasLayout.TryGet(id, out var byId));
                    Assert.AreEqual(rect.X, byId.X);
                    Assert.AreEqual(rect.Y, byId.Y);
                }
            }

            Assert.AreEqual(PortraitAtlasLayout.All.Count,
                PortraitAtlasLayout.All.Select(r => r.Id).Distinct(StringComparer.Ordinal).Count(), "no duplicate names");
        }

        [Test]
        public void CaptionsHaveNoPortrait()
        {
            Assert.IsNull(PortraitAtlasLayout.SpriteName(Speaker.Caption, BabbleEmotion.Neutral));
            Assert.IsFalse(PortraitAtlasLayout.TryGet(Speaker.Caption, BabbleEmotion.Happy, out _));
            Assert.IsFalse(PortraitAtlasLayout.TryGet(null, out _));
            Assert.IsFalse(PortraitAtlasLayout.TryGet("portrait.owl.happy", out _));
        }

        [Test]
        public void CellsFitTheAtlasWithoutOverlapping()
        {
            var all = PortraitAtlasLayout.All;
            for (int i = 0; i < all.Count; i++)
            {
                var a = all[i];
                Assert.AreEqual(PortraitAtlasLayout.CellSize, a.Width, a.Id);
                Assert.AreEqual(PortraitAtlasLayout.CellSize, a.Height, a.Id);
                Assert.That(a.X >= 0 && a.Y >= 0 && a.X + a.Width <= PortraitAtlasLayout.AtlasSize &&
                            a.Y + a.Height <= PortraitAtlasLayout.AtlasSize, a.Id + " outside the atlas");
                for (int j = i + 1; j < all.Count; j++)
                {
                    var b = all[j];
                    bool overlap = a.X < b.X + b.Width && b.X < a.X + a.Width && a.Y < b.Y + b.Height &&
                                   b.Y < a.Y + a.Height;
                    Assert.IsFalse(overlap, a.Id + " overlaps " + b.Id);
                }
            }
        }

        [Test]
        public void TheDiscLiesInsideTheCellWithRoomAboveForHats()
        {
            float r = PortraitAtlasLayout.DiscRadius;
            Assert.That(PortraitAtlasLayout.DiscCenterX - r >= 0f && PortraitAtlasLayout.DiscCenterX + r <= 1f);
            Assert.That(PortraitAtlasLayout.DiscCenterY - r >= 0f);
            Assert.Greater(1f - (PortraitAtlasLayout.DiscCenterY + r), 0.1f, "the head and hat rise above the disc");
        }

        [Test]
        public void EverySpokenLineResolvesToAPortraitOfItsEmotion()
        {
            foreach (string key in LineVoices.LineKeys)
            {
                var emotion = LineVoices.For(key);
                foreach (var speaker in PortraitAtlasLayout.Speakers)
                {
                    Assert.IsTrue(PortraitAtlasLayout.TryGet(speaker, emotion, out var rect), key + " " + speaker);
                    Assert.AreEqual(emotion, rect.Emotion, key);
                }
            }
        }
    }
}
