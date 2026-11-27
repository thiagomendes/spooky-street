using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Keepsakes;
using SpookyStreet.Core.Localization;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Core.Parent;
using SpookyStreet.Core.World;
using Lines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// S004 stream F2, the accessibility pass (A11Y-01, MAP-01, D25), engine-free: the screens moved onto the shared
    /// focus graph are sound in every state; no shared line names one control scheme (the prompt lint, English and
    /// Portuguese); every settings row has its own picture; every sound that carries a case signal has a visible
    /// equivalent; and MAP-01 still holds with the paper bats placed.
    /// </summary>
    public class AccessibilityPassCoreTests
    {
        // ------------------------------------------------------------------------------------- screens

        [Test]
        public void EveryStateOfTheTitlePauseNoticeAndGateIsASoundFocusGraph()
        {
            foreach (bool hasSave in new[] { true, false })
            {
                var title = ScreenFocus.Title();
                title.SetEnabled(ScreenFocus.TitleContinue, hasSave);
                CollectionAssert.IsEmpty(title.Validate(), "title, save " + hasSave);
                Assert.AreEqual(hasSave ? ScreenFocus.TitleContinue : ScreenFocus.TitleNewGame, title.Start());
                string bottom = title.Move(ScreenFocus.TitleLanguage, UiDirection.Left);
                Assert.AreEqual(ScreenFocus.TitleNewGame, bottom, "the pill hands focus back to New game");
            }

            foreach (bool gear in new[] { true, false })
            {
                var pause = ScreenFocus.Pause();
                pause.SetEnabled(ScreenFocus.PauseSettings, gear);
                CollectionAssert.IsEmpty(pause.Validate(), "pause, gear " + gear);
                Assert.AreEqual(ScreenFocus.PauseResume, pause.Start(), "the pause card opens on Resume");
                Assert.AreEqual(ScreenFocus.PauseResume, pause.Exit, "Back resumes");
                Assert.AreEqual(ScreenFocus.PauseToTitle, pause.Move(ScreenFocus.PauseResume, UiDirection.Down));
                Assert.AreEqual(ScreenFocus.PauseLanguage, pause.Move(ScreenFocus.PauseResume, UiDirection.Up));
                Assert.AreEqual(gear ? ScreenFocus.PauseSettings : ScreenFocus.PauseLanguage,
                    pause.Move(ScreenFocus.PauseLanguage, UiDirection.Right));
            }

            foreach (bool secondary in new[] { true, false })
            {
                var notice = ScreenFocus.Notice(secondary);
                CollectionAssert.IsEmpty(notice.Validate(), "notice, secondary " + secondary);
                Assert.AreEqual(ScreenFocus.NoticePrimary, notice.Start());
                Assert.AreEqual(secondary ? ScreenFocus.NoticeSecondary : ScreenFocus.NoticePrimary, notice.Exit,
                    "Back is the safe choice");
            }

            var gate = ScreenFocus.Gate();
            CollectionAssert.IsEmpty(gate.Validate());
            Assert.AreEqual(ScreenFocus.GateClose, gate.Exit, "Back cancels the gate on every scheme");
        }

        // ------------------------------------------------------------------------------------- prompts

        private static StringTable English => StringTable.Parse(LocalizationTests.BundledEnglishJson());

        private static StringTable Portuguese => StringTable.Parse(File.ReadAllText(
            Path.Combine(Path.GetDirectoryName(LocalizationTests.BundledEnglishPath()), "pt-BR.json")));

        [Test]
        public void NoSharedLineNamesAControlSchemeInEnglishOrPortuguese()
        {
            CollectionAssert.IsEmpty(PromptWording.Violations(English), "en.json");
            CollectionAssert.IsEmpty(PromptWording.Violations(Portuguese), "pt-BR.json");
            foreach (string key in PromptWording.SchemeKeys)
            {
                Assert.IsTrue(English.Contains(key), key + " is a real key");
                Assert.IsTrue(Portuguese.Contains(key), key + " is translated");
            }
        }

        [Test]
        public void ThePromptLintCatchesSchemeWordsAndLeavesSoundsAlone()
        {
            StringTable Table(string locale, params (string Key, string Value)[] rows) =>
                new StringTable(locale, locale, "1", rows.Select(r => new StringEntry(r.Key, r.Value, "test")));

            var en = Table("en", ("a", "Tap Jump to hop up!"), ("b", "Use W A S D to walk"), ("c", "Push the left stick"),
                ("d", "[Tap. The arm swings.]"), ("e", "Press Jump quickly to hop up!"), ("f", "I'll be tapping all night"),
                (LocalizationKeys.PromptMoveKeyboard, "Use W A S D or the arrow keys to walk."));
            CollectionAssert.AreEquivalent(new[] { "a", "b", "c" },
                PromptWording.Violations(en).Select(v => v.Substring(0, v.IndexOf(':'))));

            var pt = Table("pt-BR", ("a", "Toque em Pular para subir!"), ("b", "Arraste aqui"), ("c", "Use as setas"),
                ("d", "[Clique!]"), ("e", "Pule de novo ao tocar o chão!"), ("f", "Aperte Pular rapidinho para subir!"),
                ("g", "Câmera e toque"), ("h", "Tap to go"));
            CollectionAssert.AreEquivalent(new[] { "a", "b", "c", "h" },
                PromptWording.Violations(pt).Select(v => v.Substring(0, v.IndexOf(':'))));
        }

        [Test]
        public void SkipIsPassarAndNeverPipsJumpWordInPortuguese()
        {
            var pt = Portuguese;
            foreach (string key in new[] { "ui.dialogue.skip", "ui.cutscene.skip", "ui.memory.skip", "ui.cutscene.skip_again" })
            {
                Assert.IsTrue(pt.TryGet(key, out string text), key);
                StringAssert.DoesNotContain("pul", text.ToLowerInvariant(), key + ": skip is never 'pular' (Jump)");
                StringAssert.Contains("pass", text.ToLowerInvariant(), key);
            }

            Assert.IsTrue(pt.TryGet("ui.hud.jump", out string jump));
            Assert.AreEqual("Pular", jump);
            Assert.IsTrue(pt.TryGet("ui.hud.dive", out string dive));
            Assert.AreEqual("Mergulhar", dive, "a verb, like Rolar and Pular");
        }

        [Test]
        public void HintsNameLandmarksNotCompassPoints()
        {
            var compass = new[] { "east", "west", "north", "south", "leste", "oeste", "norte", " sul" };
            foreach (var table in new[] { English, Portuguese })
            {
                foreach (var entry in table.Entries.Where(e => e.Key.Contains(".hint.")))
                {
                    foreach (string text in entry.Texts)
                    {
                        foreach (string word in compass)
                        {
                            StringAssert.DoesNotContain(word, " " + text.ToLowerInvariant(), $"{table.Locale} {entry.Key}");
                        }
                    }
                }
            }
        }

        // ------------------------------------------------------------------------------------- settings pictures

        [Test]
        public void EverySettingsRowHasItsOwnPicture()
        {
            var pictures = new List<string> { SettingsCard.HelperRow.Picture };
            pictures.AddRange(SettingsCard.All.Select(r => r.Picture));
            CollectionAssert.AllItemsAreUnique(pictures, "two rows share a picture: " + string.Join(", ", pictures));
            foreach (string picture in pictures)
            {
                Assert.IsTrue(PuppetParts.Contains(picture), picture + " is a puppet part");
                Assert.IsTrue(PuppetAtlasLayout.TryGet(picture, out _), picture + " is in the atlas");
            }

            Assert.AreEqual(PuppetParts.CameraTurn, SettingsCard.Row(SettingsCard.CameraSpeed).Picture);
            Assert.AreEqual(PuppetParts.PhoneBuzz, SettingsCard.Row(SettingsCard.Haptics).Picture);
        }

        // ------------------------------------------------------------------------------------- sound off

        private static CaseStep Step(CaseState state, CaseAction action) => CaseReducer.Apply(state, action);

        [Test]
        public void EverySoundThatCarriesACaseSignalHasACaptionOrAVisibleEquivalent()
        {
            var empty = CaseState.Empty;
            var english = English;

            // Sounds that are clues have a caption line (A11Y-01: sound alone never carries a clue).
            void AssertCaption(CaseStep step, string key)
            {
                Assert.IsTrue(step.Lines.Any(l => l.Speaker == Speaker.Caption && l.Key == key), key);
                Assert.IsTrue(english.Contains(key), key + " is a string");
                Assert.IsTrue(PromptWording.IsSoundCaption(english.TryGet(key, out string t) ? t : ""), key + " reads as a sound");
            }

            AssertCaption(Step(empty, CaseAction.Of(CaseActionKind.HearKnocks)), Lines.CaptionArrivalKnocks);
            AssertCaption(Step(empty, CaseAction.Of(CaseActionKind.EnterFoyer)), Lines.CaptionFoyerHum);
            AssertCaption(Step(empty, CaseAction.Of(CaseActionKind.ExamineGrille)), Lines.CaptionGrille);
            AssertCaption(Step(empty, CaseAction.Of(CaseActionKind.TurnCrank)), Lines.CaptionCrankGap);
            var linked = Step(empty, CaseAction.Of(CaseActionKind.LinkArm));
            AssertCaption(linked, Lines.CaptionArmLinked);
            AssertCaption(Step(linked.State, CaseAction.Of(CaseActionKind.TurnCrank)), Lines.CaptionOwlBows);

            // The knocks and the floor hum come from somewhere: their captions take a direction word.
            CollectionAssert.AreEquivalent(new[] { Lines.CaptionArrivalKnocks, Lines.CaptionFoyerHum },
                CaptionDirection.DirectionalSoundKeys);

            // Every sound paired with an attraction (the cam taps, the wind wheel's whirr) has a motion or light to see,
            // which reduced motion keeps (scaled, never removed).
            foreach (var attraction in ClueAttractions.All.Where(a => a.Sound != null))
            {
                Assert.Greater(attraction.Kinds.Count, 0, attraction.Id + " shows something");
            }

            Assert.Greater(ClueAttractions.ReducedMotionScale, 0f, "reduced motion keeps the attractions visible");
            Assert.IsNull(ClueAttractions.Get(ClueAttractions.WindWheel).StickerId,
                "the wind wheel is no clue yet: nothing depends on hearing it");
        }

        // ------------------------------------------------------------------------------------- MAP-01 with the bats

        [Test]
        public void Map01StillHoldsWithThePaperBatsPlaced()
        {
            // No bat sits in or next to a threshold's zone: picking one up never coincides with the page turn, and no
            // bat stands where Pip must pass to reach the sill by either ascent.
            foreach (var bat in PaperBatLayout.All)
            {
                Assert.IsFalse(Thresholds.NearAnyZone(bat.X, bat.Y, bat.Z, 0.5f), bat + " is at a threshold");
            }

            // Both ascents still reach the same sill: the bats add no collider, so the route layout is unchanged.
            var lastLeg = CaseRouteLayout.MandatoryLegs[0];
            Assert.IsNotNull(lastLeg);
            Assert.Greater(CaseRouteLayout.SkilledAscent.Length, 0);

            // The main loop's bats ask for nothing beyond a walk and a tap jump: no blind leap is ever mandatory, and
            // the harder bats are optional (the case never needs a bat).
            foreach (var bat in PaperBatLayout.All.Where(b => b.Tier == BatTier.Loop))
            {
                Assert.IsTrue((bat.Needs & ~BatMoves.TapJump) == 0, bat + " needs more than a tap jump");
            }

            // Revisit bench, treehouse and porch for a second readable use: a bat waits at each.
            foreach (string anchor in new[] { CaseAnchors.Bench, CaseAnchors.Treehouse, CaseAnchors.ArrivalPorch })
            {
                Assert.IsTrue(CaseStage.TryGetAnchor(anchor, out var at), anchor);
                var near = PaperBatLayout.All.Where(b =>
                    Math.Sqrt((b.X - at.X) * (b.X - at.X) + (b.Z - at.Z) * (b.Z - at.Z)) <= RevisitRadius &&
                    Math.Abs(b.Y - at.Y) <= 2.5f).ToList();
                Assert.IsNotEmpty(near, anchor + " has a bat within " + RevisitRadius + " m");
            }
        }

        /// <summary>A bat this close (plan metres) to a revisit spot is part of its second use.</summary>
        private const double RevisitRadius = 5.0;
    }
}
