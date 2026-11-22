using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Localization;
using SpookyStreet.Core.Nonverbal;
using NvKeys = SpookyStreet.Core.Localization.LocalizationKeys.NonverbalUi;
using PromptKeys = SpookyStreet.Core.Localization.LocalizationKeys.ControlPrompts;

namespace SpookyStreet.Tests.Core
{
    /// <summary>Glyphs and lines per control scheme (D25): touch controls, gamepad shapes, key caps.</summary>
    public class ControlGlyphsTests
    {
        [Test]
        public void TouchPromptsUseTheOnScreenControls()
        {
            foreach (ControlPrompt prompt in Enum.GetValues(typeof(ControlPrompt)))
            {
                var g = ControlGlyphs.For(ControlScheme.Touch, prompt);
                Assert.IsTrue(g.IsTouch, prompt + " on touch");
                Assert.IsNull(g.BindingPath, prompt + " names no key or button on touch");
            }

            Assert.AreEqual(GlyphShape.TouchStick, ControlGlyphs.For(ControlScheme.Touch, ControlPrompt.Move).Shape);
            Assert.IsTrue(ControlGlyphs.For(ControlScheme.Touch, ControlPrompt.HoldJump).Hold);
            Assert.IsFalse(ControlGlyphs.For(ControlScheme.Touch, ControlPrompt.Use).Hold);
        }

        [Test]
        public void GamepadPromptsLightTheBoundFaceButton()
        {
            var use = ControlGlyphs.For(ControlScheme.Gamepad, ControlPrompt.Use);
            Assert.AreEqual(GlyphShape.PadFace, use.Shape);
            Assert.AreEqual(PadButton.West, use.Pad);
            Assert.AreEqual("<Gamepad>/buttonWest", use.BindingPath);
            var jump = ControlGlyphs.For(ControlScheme.Gamepad, ControlPrompt.HoldJump);
            Assert.AreEqual(PadButton.South, jump.Pad);
            Assert.IsTrue(jump.Hold);
            Assert.AreEqual(GlyphShape.PadStick, ControlGlyphs.For(ControlScheme.Gamepad, ControlPrompt.Move).Shape);
            Assert.AreEqual(PadButton.South, ControlGlyphs.For(ControlScheme.Gamepad, ControlPrompt.Next).Pad);
            Assert.AreEqual(PadButton.East, ControlGlyphs.For(ControlScheme.Gamepad, ControlPrompt.Skip).Pad);
            Assert.AreEqual(GlyphShape.PadShoulder, ControlGlyphs.For(ControlScheme.Gamepad, ControlPrompt.GateA).Shape);
            Assert.AreEqual(PadButton.LeftShoulder, ControlGlyphs.For(ControlScheme.Gamepad, ControlPrompt.GateA).Pad);
            Assert.AreEqual(PadButton.RightShoulder, ControlGlyphs.For(ControlScheme.Gamepad, ControlPrompt.GateB).Pad);
        }

        [Test]
        public void KeyboardPromptsShowTheBoundKey()
        {
            var expected = new Dictionary<ControlPrompt, string>
            {
                { ControlPrompt.Use, "<Keyboard>/e" },
                { ControlPrompt.Jump, "<Keyboard>/space" },
                { ControlPrompt.HoldJump, "<Keyboard>/space" },
                { ControlPrompt.Move, "<Keyboard>/w" },
                { ControlPrompt.Next, "<Keyboard>/e" },
                { ControlPrompt.Skip, "<Keyboard>/tab" },
                { ControlPrompt.GateA, "<Keyboard>/q" },
                { ControlPrompt.GateB, "<Keyboard>/p" },
            };
            foreach (var pair in expected)
            {
                var g = ControlGlyphs.For(ControlScheme.KeyboardMouse, pair.Key);
                Assert.AreEqual(GlyphShape.Key, g.Shape, pair.Key.ToString());
                Assert.AreEqual(pair.Value, g.BindingPath, pair.Key.ToString());
            }
        }

        [Test]
        public void TheMoveKeyFollowsTheGoalsScreenDirection()
        {
            Assert.AreEqual("<Keyboard>/w", ControlGlyphs.MoveKey(0.1f, 0.9f).BindingPath);
            Assert.AreEqual("<Keyboard>/s", ControlGlyphs.MoveKey(-0.1f, -0.9f).BindingPath);
            Assert.AreEqual("<Keyboard>/d", ControlGlyphs.MoveKey(0.9f, 0.3f).BindingPath);
            Assert.AreEqual("<Keyboard>/a", ControlGlyphs.MoveKey(-0.9f, -0.3f).BindingPath);
            Assert.IsTrue(ControlGlyphs.MoveKey(1f, 0f).Hold);
        }

        [Test]
        public void TheParentGateAndContinueLinesFollowTheScheme()
        {
            Assert.AreEqual(NvKeys.ParentGateHint, ControlGlyphs.ParentGateHintKey(ControlScheme.Touch));
            Assert.AreEqual(PromptKeys.ParentGateHintGamepad, ControlGlyphs.ParentGateHintKey(ControlScheme.Gamepad));
            Assert.AreEqual(PromptKeys.ParentGateHintKeyboard, ControlGlyphs.ParentGateHintKey(ControlScheme.KeyboardMouse));
            Assert.AreEqual(PromptKeys.DialogueContinueTouch, ControlGlyphs.ContinueKey(ControlScheme.Touch));
            Assert.AreEqual(PromptKeys.DialogueContinueButton, ControlGlyphs.ContinueKey(ControlScheme.Gamepad));
            Assert.AreEqual(PromptKeys.DialogueContinueButton, ControlGlyphs.ContinueKey(ControlScheme.KeyboardMouse));
        }

        [Test]
        public void EveryControlPromptLineIsInBundledEnglishWithContext()
        {
            var table = StringTable.Parse(LocalizationTests.BundledEnglishJson());
            var catalog = new LocalizationCatalog(table);
            foreach (string key in PromptKeys.All)
            {
                Assert.IsTrue(table.Contains(key), "en.json is missing " + key);
            }

            foreach (var entry in table.Entries)
            {
                if (Array.IndexOf(PromptKeys.All, entry.Key) >= 0)
                {
                    Assert.IsNotEmpty(entry.Context, entry.Key);
                }
            }

            // Only the touch line may say "tap"; the others name the buttons or keys.
            StringAssert.DoesNotContain("tap", catalog.Get(PromptKeys.DialogueContinueButton).ToLowerInvariant());
            StringAssert.Contains("Q", catalog.Get(PromptKeys.ParentGateHintKeyboard));
            StringAssert.Contains("shoulder", catalog.Get(PromptKeys.ParentGateHintGamepad));
        }
    }
}
