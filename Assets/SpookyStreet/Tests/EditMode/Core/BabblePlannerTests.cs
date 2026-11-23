using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Audio;
using SpookyStreet.Core.Case;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// The babble runtime planner (D20.2), ported from <c>tools/audio/spookyaudio/babble.py</c>: syllable counts (the
    /// golden captions below are checked against the Python reference by tools/audio/tests/test_import.py), the length
    /// rule and its clamps, the closest phrase take of the line's emotion, variety, the emotion of every case 01 line and
    /// the cutscene cue ids.
    /// </summary>
    public class BabblePlannerTests
    {
        // Golden syllable counts: babble.caption_syllables gives the same (tools/audio/tests/test_import.py).
        [TestCase("Something silver flashed up high, by the side of the house!", 15)]
        [TestCase("It won't budge.", 3)]
        [TestCase("Sit down and see for yourself.", 7)]
        [TestCase("Pip! You found my secret way in, and nobody showed you! Did you hear the knocks too?", 21)]
        [TestCase("It's open now, though. Well, well.", 7)]
        [TestCase("Knock, knock… knock. I'll be tapping that all night!", 10)]
        [TestCase("So, detective, what happened here?", 9)]
        [TestCase("One sweet, saved in a tiny knight helmet.", 11)]
        [TestCase("A painted owl, wings folded.", 7)]
        [TestCase("Crinkly, shiny foil.", 5)]
        [TestCase("Hmm", 1)]
        [TestCase("Olá, avó! Está tudo bem?", 9)]
        [TestCase("The door's open now? That silver flash was long before.", 13)]
        [TestCase("...", 1)]
        public void SyllablesMatchThePythonReference(string caption, int syllables)
        {
            Assert.AreEqual(syllables, BabblePlanner.CaptionSyllables(caption));
        }

        [Test]
        public void LengthIsSixTenthsOfTheSyllablesOverTheRateClamped()
        {
            const string bea = "Something silver flashed up high, by the side of the house!"; // 15 syllables
            Assert.AreEqual(0.6f * 15f / 10.5f, BabblePlanner.TargetSeconds(bea, 10.5f), 1e-5f);
            Assert.AreEqual(0.6f * 15f / 5.5f, BabblePlanner.TargetSeconds(bea, 5.5f), 1e-5f);
            Assert.AreEqual(BabblePlanner.MinSeconds, BabblePlanner.TargetSeconds("It won't budge.", 8.5f), "short lines clamp up");
            Assert.AreEqual(BabblePlanner.MinSeconds, BabblePlanner.TargetSeconds(string.Empty, 8.5f));
            string longLine = string.Concat(System.Linq.Enumerable.Repeat("banana ", 20)); // 60 syllables
            Assert.AreEqual(BabblePlanner.MaxSeconds, BabblePlanner.TargetSeconds(longLine, 5.5f), "long lines clamp down");
            Assert.Throws<System.ArgumentOutOfRangeException>(() => BabblePlanner.TargetSeconds("x", 0f));
        }

        [Test]
        public void TheClosestPhraseTakeOfTheEmotionPlays()
        {
            var takes = new List<float> { 0.6f, 0.9f, 1.3f, 1.8f, 2.4f, 3.0f };
            Assert.AreEqual(1, BabblePlanner.ClosestTake(takes, 0.6f));
            Assert.AreEqual(3, BabblePlanner.ClosestTake(takes, 1.2f));
            Assert.AreEqual(4, BabblePlanner.ClosestTake(takes, 1.6f));
            Assert.AreEqual(6, BabblePlanner.ClosestTake(takes, 3.0f));

            // Vale's 15-syllable line asks for 1.64 s: his neutral p4 (1.82 s) is closest.
            var plan = BabblePlanner.Plan(Speaker.Vale, "Something silver flashed up high, by the side of the house!",
                BabbleEmotion.Neutral);
            Assert.AreEqual("voice_vale_neutral", plan.EventId);
            Assert.AreEqual(0.6f * 15f / 5.5f, plan.TargetSeconds, 1e-5f);
            Assert.AreEqual(4, plan.Take);
            Assert.AreEqual(VoiceMusicData.PhraseSeconds[2][0][3], plan.TakeSeconds);

            // Bea surprised, same line: 0.86 s, her surprised p2 is 0.985 s and p1 0.602 s.
            plan = BabblePlanner.Plan(Speaker.Bea, "Something silver flashed up high, by the side of the house!",
                BabbleEmotion.Surprised);
            Assert.AreEqual("voice_bea_surprised", plan.EventId);
            Assert.AreEqual(2, plan.Take);

            // Every character, emotion and length picks the true nearest take.
            foreach (var speaker in new[] { Speaker.Pip, Speaker.Bea, Speaker.Vale, Speaker.June, Speaker.Kit })
            {
                for (int e = 0; e < 5; e++)
                {
                    var emotion = (BabbleEmotion)e;
                    var lengths = BabblePlanner.PhraseSeconds(VoiceMusicEvents.CharacterId(speaker), emotion);
                    Assert.AreEqual(VoiceMusicEvents.PhrasesPerEmotion, lengths.Count);
                    for (float target = 0.6f; target <= 3f; target += 0.1f)
                    {
                        var p = BabblePlanner.PlanFor(speaker, target, emotion, 0);
                        foreach (float other in lengths)
                        {
                            Assert.LessOrEqual(System.Math.Abs(p.TakeSeconds - target), System.Math.Abs(other - target) + 1e-6f);
                        }
                    }
                }
            }

            Assert.IsFalse(BabblePlanner.Plan(Speaker.Caption, "[Click!]", BabbleEmotion.Neutral).IsValid, "captions are silent");
        }

        [Test]
        public void VarietyAvoidsRepeatingATakeWhenAnotherIsNearlyAsClose()
        {
            var takes = new List<float> { 0.6f, 0.9f, 1.3f, 1.8f, 2.4f, 3.0f };
            Assert.AreEqual(2, BabblePlanner.ClosestTake(takes, 0.8f, avoid: 1), "0.9 s is closest anyway");
            Assert.AreEqual(1, BabblePlanner.ClosestTake(takes, 0.8f, avoid: 2), "0.6 s is within the tolerance");
            Assert.AreEqual(6, BabblePlanner.ClosestTake(takes, 3.0f, avoid: 6), "2.4 s is 0.6 s further: too far");
            Assert.AreEqual(1, BabblePlanner.ClosestTake(new List<float> { 0.6f, 2.0f }, 0.6f, avoid: 1),
                "too far: the closest plays again");

            var picker = new BabblePicker();
            const string line = "So, detective, what happened here?";
            var first = picker.Next(Speaker.Kit, line, BabbleEmotion.Thinking);
            var second = picker.Next(Speaker.Kit, line, BabbleEmotion.Thinking);
            Assert.AreEqual(first.EventId, second.EventId);
            Assert.AreNotEqual(first.Take, second.Take, "the same line twice gets another nearby take");
            Assert.LessOrEqual(System.Math.Abs(second.TakeSeconds - second.TargetSeconds),
                System.Math.Abs(first.TakeSeconds - first.TargetSeconds) + BabblePlanner.VarietyToleranceSeconds + 1e-5f);
            var cue = picker.NextFor(Speaker.June, 9f, BabbleEmotion.Whisper);
            Assert.AreEqual(BabblePlanner.MaxSeconds, cue.TargetSeconds, "cue lengths clamp too");
        }

        [Test]
        public void EverySpokenCaseLineHasAnEmotionAndCaptionsHaveNone()
        {
            var spoken = new HashSet<string>();
            foreach (string key in CaseScript.AllLineKeys)
            {
                if (key.StartsWith("c01.caption.")) continue;
                spoken.Add(key);
                Assert.IsTrue(LineVoices.Has(key), key + " needs a babble emotion in LineVoices");
            }

            foreach (string key in LineVoices.LineKeys)
            {
                Assert.IsTrue(spoken.Contains(key), key + " is not a spoken case line");
            }

            Assert.AreEqual(BabbleEmotion.Surprised, LineVoices.For(Keys.BeaSawCape));
            Assert.AreEqual(BabbleEmotion.Whisper, LineVoices.For(Keys.JuneListenFloor));
            Assert.AreEqual(BabbleEmotion.Thinking, LineVoices.For(Keys.ExamineCloth), "Pip's examine lines use Pip's voice");
            Assert.AreEqual(BabbleEmotion.Neutral, LineVoices.For("c01.line.nobody"));
            Assert.AreEqual("voice_pip_thinking", VoiceMusicEvents.VoiceId(Speaker.Pip, LineVoices.For(Keys.ExamineCloth)));
        }

        [Test]
        public void CatalogHasEveryVoiceAndMusicEvent()
        {
            var catalog = AudioEvents.CreateCatalog();
            Assert.IsEmpty(catalog.Validate());
            Assert.AreEqual(25, VoiceMusicEvents.VoiceIds.Length);
            foreach (string id in VoiceMusicEvents.VoiceIds)
            {
                var def = catalog.Get(id);
                Assert.AreEqual(AudioBus.Voice, def.Bus, id);
                Assert.AreEqual(6, def.Variants, id);
                Assert.IsTrue(AudioImportPlan.IsApproved(id), id);
                Assert.AreEqual(".ogg", AudioImportPlan.FileExtension(id));
                var plan = AudioImportPlan.For(def);
                Assert.AreEqual(AudioCategory.Voice, plan.Category);
                Assert.AreEqual(AudioLoad.CompressedInMemory, plan.Load, "150 phrases stay compressed in memory");
                Assert.IsFalse(plan.ForceMono, "already mono");
                Assert.IsTrue(AudioMix.PausesWithGame(def), "voices hold with the game");
            }

            foreach (string id in VoiceMusicEvents.Music)
            {
                var def = catalog.Get(id);
                Assert.AreEqual(AudioBus.Music, def.Bus, id);
                Assert.AreEqual(1, def.Variants, id);
                Assert.AreEqual(VoiceMusicEvents.IsMusicLoop(id), def.Loop, id);
                Assert.AreEqual(AudioLoad.Streaming, AudioImportPlan.For(def).Load, id);
                Assert.IsFalse(AudioMix.PausesWithGame(def), "music carries on under the pause card");
                Assert.Greater(VoiceMusicEvents.MusicSeconds(id), 5.0, id);
            }

            Assert.IsFalse(AudioImportPlan.IsApproved(AudioEvents.VoicePip), "the S003 formant voices stay rejected");
            Assert.IsFalse(AudioImportPlan.IsApproved(AudioEvents.MusicMotifPluck), "the plucked motif stays rejected");
        }

        [Test]
        public void TheCutsceneEmotionsMapOneToOneOntoTheBabbleEmotions()
        {
            // GameCutsceneAudio casts the cutscenes' BabbleEmotion onto the audio one by value.
            CollectionAssert.AreEqual(System.Enum.GetNames(typeof(SpookyStreet.Core.Cutscenes.BabbleEmotion)),
                System.Enum.GetNames(typeof(BabbleEmotion)));
            Assert.AreEqual(VoiceMusicEvents.MusicStreetTheme, SpookyStreet.Core.Cutscenes.CutsceneAudio.MusicStreetTheme);
            Assert.AreEqual(VoiceMusicEvents.MusicReveal, SpookyStreet.Core.Cutscenes.CutsceneAudio.MusicReveal);
        }

        [Test]
        public void CutsceneCueIdsResolve()
        {
            Assert.AreEqual("music_reveal", AudioCues.ResolveMusic("music_reveal"));
            Assert.AreEqual("music_reveal", AudioCues.ResolveMusic("music.c01.reveal"));
            Assert.AreEqual("music_street_theme", AudioCues.ResolveMusic("music.c01.opening"));
            Assert.AreEqual("music_case_closed", AudioCues.ResolveMusic("music.c01.case_closed"));
            Assert.IsNull(AudioCues.ResolveMusic("music.c01.nope"));
            Assert.IsNull(AudioCues.ResolveMusic("ui_tap"));

            Assert.IsTrue(AudioCues.TryResolveBabble("babble.c01.bea.hello", out var speaker, out var emotion));
            Assert.AreEqual(Speaker.Bea, speaker);
            Assert.AreEqual(BabbleEmotion.Happy, emotion);
            Assert.IsTrue(AudioCues.TryResolveBabble("babble.c01.june.cheer", out speaker, out emotion));
            Assert.AreEqual(Speaker.June, speaker);
            Assert.IsTrue(AudioCues.TryResolveBabble("babble.c01.vale.mumble", out speaker, out emotion));
            Assert.AreEqual(BabbleEmotion.Neutral, emotion);
            Assert.IsFalse(AudioCues.TryResolveBabble("babble.c01.owl.hoot", out _, out _));

            var music = new List<MusicRequest>();
            var babble = new List<BabbleRequest>();
            System.Action<MusicRequest> onMusic = music.Add;
            System.Action<BabbleRequest> onBabble = babble.Add;
            AudioCues.MusicRequested += onMusic;
            AudioCues.BabbleRequested += onBabble;
            try
            {
                Assert.IsTrue(AudioCues.PlayCue("music.c01.reveal"));
                Assert.IsTrue(AudioCues.PlayCue("babble.c01.kit.cheer"));
                Assert.IsFalse(AudioCues.PlayCue("nothing"));
                AudioCues.StopCue();
                AudioCues.DuckMusic(true);
                AudioCues.StopMusic();
                AudioCues.ResumeMusic();
                Assert.AreEqual(5, music.Count);
                Assert.AreEqual(MusicRequestKind.PlayCue, music[0].Kind);
                Assert.AreEqual("music_reveal", music[0].CueId);
                Assert.AreEqual(MusicRequestKind.Duck, music[2].Kind);
                Assert.IsTrue(music[2].Ducked);
                Assert.AreEqual(1, babble.Count);
                Assert.AreEqual(Speaker.Kit, babble[0].Speaker);
                Assert.AreEqual(BabbleEmotion.Happy, babble[0].Emotion);
            }
            finally
            {
                AudioCues.MusicRequested -= onMusic;
                AudioCues.BabbleRequested -= onBabble;
            }
        }
    }
}
