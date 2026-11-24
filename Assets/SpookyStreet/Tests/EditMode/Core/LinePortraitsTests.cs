using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Audio;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Core.Street;
using Lines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;

namespace SpookyStreet.Tests.Core
{
    /// <summary>Every spoken line shows its speaker's portrait in the line's babble emotion; captions show none (S004 §2.6).</summary>
    public class LinePortraitsTests
    {
        /// <summary>Who says a case line, from its stable key: examine lines are Pip's, c01.line.&lt;speaker&gt;.*.</summary>
        private static Speaker SpeakerOf(string key)
        {
            if (key.StartsWith("c01.caption.", StringComparison.Ordinal))
            {
                return Speaker.Caption;
            }

            if (key.StartsWith("c01.examine.", StringComparison.Ordinal))
            {
                return Speaker.Pip;
            }

            Assert.IsTrue(key.StartsWith("c01.line.", StringComparison.Ordinal), key + " is not a case line key");
            string name = key.Substring("c01.line.".Length).Split('.')[0];
            Assert.IsTrue(Enum.TryParse(name, true, out Speaker speaker), key + ": unknown speaker " + name);
            return speaker;
        }

        private static IEnumerable<ScriptLine> SpokenCaseLines()
        {
            foreach (string key in CaseScript.AllLineKeys)
            {
                var speaker = SpeakerOf(key);
                if (!CaseSpeakers.IsCaption(speaker))
                {
                    yield return new ScriptLine(speaker, key);
                }
            }
        }

        [Test]
        public void EverySpokenCaseLineResolvesToItsSpeakersPortraitInItsEmotion()
        {
            int spoken = 0;
            foreach (var line in SpokenCaseLines())
            {
                Assert.IsTrue(LineVoices.Has(line.Key), line.Key + " needs a LineVoices row");
                Assert.IsTrue(LinePortraits.TryGet(line, out var rect), line.Key + " has no portrait");
                Assert.AreEqual(line.Speaker, rect.Speaker, line.Key);
                Assert.AreEqual(LineVoices.For(line.Key), rect.Emotion, line.Key);
                Assert.AreEqual(rect.Id, LinePortraits.SpriteName(line));
                spoken++;
            }

            Assert.Greater(spoken, 30, "the case has its spoken lines");
            foreach (string key in LineVoices.LineKeys)
            {
                Assert.IsTrue(LinePortraits.TryGet(new ScriptLine(SpeakerOf(key), key), out _), key);
            }
        }

        [Test]
        public void ConversationsAndStreetGreetingsShowAPortraitOnEveryLine()
        {
            var stamped = CaseScenarioTests.PlayCanonical().State;
            var states = new[] { CaseState.Empty, stamped };
            foreach (var speaker in new[] { Speaker.Bea, Speaker.Vale, Speaker.June, Speaker.Kit })
            {
                foreach (var state in states)
                {
                    foreach (var line in CaseScript.Conversation(speaker, state))
                    {
                        Assert.IsTrue(LinePortraits.TryGet(line, out var rect), line.Key);
                        Assert.AreEqual(speaker, rect.Speaker, line.Key);
                    }
                }
            }

            foreach (string key in StreetGreetings.AllKeys)
            {
                var line = new ScriptLine(SpeakerOf(key), key);
                Assert.IsTrue(LinePortraits.TryGet(line, out var rect), key);
                Assert.AreEqual(LineVoices.For(key), rect.Emotion, key);
            }
        }

        [Test]
        public void TheEmotionPicksTheFaceAndCaptionsHaveNone()
        {
            Assert.AreEqual("portrait.bea.surprised", LinePortraits.SpriteName(new ScriptLine(Speaker.Bea, Lines.BeaSawCape)));
            Assert.AreEqual("portrait.june.whisper", LinePortraits.SpriteName(new ScriptLine(Speaker.June, Lines.JuneListenFloor)));
            Assert.AreEqual("portrait.pip.thinking", LinePortraits.SpriteName(new ScriptLine(Speaker.Pip, Lines.ExamineCloth)));
            Assert.AreEqual("portrait.kit.neutral", LinePortraits.SpriteName(new ScriptLine(Speaker.Kit, "c01.line.nobody")),
                "a line without a voice row shows the neutral face");
            Assert.IsNull(LinePortraits.SpriteName(new ScriptLine(Speaker.Caption, Lines.CaptionGrille)));
            Assert.IsFalse(LinePortraits.TryGet(Speaker.Caption, Lines.BeaSawCape, out _), "captions never get a face");
        }
    }
}
