using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Controls;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Localization;
using SpookyStreet.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Lines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;
using NvKeys = SpookyStreet.Core.Localization.LocalizationKeys.NonverbalUi;
using PromptKeys = SpookyStreet.Core.Localization.LocalizationKeys.ControlPrompts;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// The hint pictures in the running game (S002 hints close, docs/nonverbal-ux.md §6, D23.4, D25): a tier-1 press
    /// sparkles the target and a witness looks at it; a tier-3 press shows the ghost hand on touch and the key glyph
    /// on a keyboard; neither shows without a press while the Helper is off. Also the per-scheme parent gate line,
    /// the dialogue's next and skip glyphs and the memory replay's skip name. Fresh temporary save folder per test.
    /// </summary>
    public class HintPicturesTests : InputTestFixture
    {
        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            Localizer.ResetForTests();
            CaseDirector.UseTemporarySaveDirectory();
            ControlSchemeTracker.Override = null;
        }

        public override void TearDown()
        {
            ControlSchemeTracker.Override = null;
            base.TearDown();
        }

        private static IEnumerator StartGame()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(GameBootstrap.Instance, "scene did not create GameBootstrap");
            GameBootstrap.Instance.Ui.NewGameButton.onClick.Invoke();
            yield return Wait(0.6f);
            Assert.AreEqual(GameState.Playing, GameBootstrap.Instance.Flow.State);
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
            }
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private static CaseDirector Director => GameBootstrap.Instance.Director;

        /// <summary>The knocks are heard (goal: Mr. Vale's bench) and Pip stands a few steps from Vale, facing him.</summary>
        private static IEnumerator FaceVale()
        {
            var game = GameBootstrap.Instance;
            Director.Perform(CaseAction.Of(CaseActionKind.HearKnocks));
            yield return Wait(0.2f);
            Director.Captions.Clear();
            var vale = game.Witnesses.Vale.transform.position;
            var pip = game.Pip.transform.position;
            Vector3 away = new Vector3(pip.x - vale.x, 0f, pip.z - vale.z).normalized;
            if (away.sqrMagnitude < 0.01f)
            {
                away = Vector3.back;
            }

            game.Pip.Teleport(new Vector3(vale.x, 0f, vale.z) + away * 4f + Vector3.up * 0.1f);
            game.Pip.transform.rotation = Quaternion.LookRotation(-away, Vector3.up);
            game.CameraRig.Orbit.RequestRecenter();
            yield return Wait(1.2f);
        }

        [UnityTest]
        public IEnumerator ATierOnePressSparklesTheTargetAndAWitnessLooksAtIt()
        {
            yield return StartGame();
            yield return FaceVale();
            var layer = Director.Nonverbal;
            Assert.AreEqual(HintTier.Observation, Director.NextHintTier);
            layer.RadioButton.onClick.Invoke();
            yield return Frames(3);
            var guide = layer.Guide;
            Assert.IsTrue(guide.SparkleActive, "tier 1 sparkles");
            Assert.AreEqual(CaseObjective.CheckView, guide.SparkleObjective);
            Assert.Greater(guide.VisibleFlecks, 0, "star flecks are drawn");
            Assert.LessOrEqual(guide.VisibleFlecks, HintPresentation.SparkleFlecks);
            Assert.IsTrue(HintPresentation.TryTarget(CaseObjective.CheckView, out var bench, out _));
            Assert.Less(Vector3.Distance(guide.SparkleTarget, new Vector3(bench.X, bench.Y, bench.Z)), 0.01f,
                "the sparkle sits on the bench");
            Assert.IsNotNull(layer.HintBadge, "the hint line carries a picture");
            Assert.IsTrue(layer.HintBadge.gameObject.activeInHierarchy, "the badge shows while the hint line is up");
            Assert.AreEqual(SpookyStreet.Core.Art.UiSpriteCatalog.HintStar, layer.HintBadgePicture.sprite.name,
                "tier 1's picture is the star");
            Assert.IsFalse(guide.Active, "tier 1 has no bat trail");
            Assert.IsFalse(layer.Demo.Active, "tier 1 has no ghost demo");

            Assert.IsNotNull(guide.Looker, "a witness in view turns to look");
            Assert.IsTrue(guide.Looker.LookingAtPoint);
            Assert.Less(Vector3.Distance(guide.Looker.LookPoint, guide.SparkleTarget), 0.01f);
            yield return Wait(HintPresentation.LookSeconds + 0.4f);
            Assert.IsFalse(guide.Looker.LookingAtPoint, "the look lasts about 2 s");

            // Meeting the objective stops the sparkle.
            Director.Perform(CaseAction.Of(CaseActionKind.SitOnBench));
            yield return Frames(2);
            Assert.IsFalse(guide.SparkleActive, "the sparkle stops when the objective changes");
            Assert.AreEqual(0, guide.VisibleFlecks);
            Director.Bench.End();
        }

        [UnityTest]
        public IEnumerator ATierThreePressShowsTheGhostHandOnTouch()
        {
            yield return StartGame();
            ControlSchemeTracker.Override = ControlScheme.Touch;
            var layer = Director.Nonverbal;
            layer.RadioButton.onClick.Invoke();
            layer.RadioButton.onClick.Invoke();
            Assert.IsFalse(layer.Demo.Active, "tiers 1 and 2 have no ghost demo");
            layer.RadioButton.onClick.Invoke(); // tier 3: show me
            Assert.IsTrue(layer.Demo.Active, "tier 3 starts the demonstration");
            Assert.IsFalse(layer.Guide.SparkleActive);
            Assert.IsFalse(layer.Guide.Active);
            yield return Wait(0.6f);
            Assert.AreEqual(ControlScheme.Touch, layer.Demo.Scheme);
            Assert.IsTrue(layer.Demo.HandVisible, "the translucent hand demonstrates on the real control");
            Assert.Less(layer.Demo.Hand.color.a, 1f, "the hand is translucent");
            Assert.IsFalse(layer.Demo.GlyphVisible, "no button glyph on touch");
            yield return Wait(HintPresentation.DemoLoops * HintPresentation.DemoLoopSeconds);
            Assert.IsFalse(layer.Demo.Active, "the demonstration ends after its loops");
        }

        [UnityTest]
        public IEnumerator ATierThreePressShowsTheKeyGlyphOnAKeyboardAndTheButtonOnAGamepad()
        {
            yield return StartGame();
            ControlSchemeTracker.Override = ControlScheme.KeyboardMouse;
            var layer = Director.Nonverbal;
            for (int i = 0; i < 3; i++)
            {
                layer.RadioButton.onClick.Invoke();
            }

            yield return Wait(0.4f);
            Assert.IsTrue(layer.Demo.Active);
            Assert.IsFalse(layer.Demo.HandVisible, "no hand with a keyboard");
            Assert.IsTrue(layer.Demo.GlyphVisible, "the key glyph pulses instead");
            Assert.AreEqual(GlyphShape.Key, layer.Demo.Glyph.Glyph.Shape);
            Assert.IsFalse(string.IsNullOrEmpty(layer.Demo.Glyph.KeyLabel), "the key cap names the key");

            // The radio stays on tier 3: a press with a gamepad shows a lit button or the stick, never a hand.
            ControlSchemeTracker.Override = ControlScheme.Gamepad;
            layer.RadioButton.onClick.Invoke();
            yield return Wait(0.4f);
            Assert.IsTrue(layer.Demo.GlyphVisible);
            Assert.IsFalse(layer.Demo.HandVisible);
            var shape = layer.Demo.Glyph.Glyph.Shape;
            Assert.IsTrue(shape == GlyphShape.PadFace || shape == GlyphShape.PadStick, shape.ToString());
        }

        [UnityTest]
        public IEnumerator NeitherShowsWithoutAPressWhileHelperIsOff()
        {
            yield return StartGame();
            var layer = Director.Nonverbal;
            Assert.IsFalse(layer.Settings.HelperAutoHints, "Helper is off by default");
            layer.AddStallForTests(NonverbalSettings.AutoHintStallSeconds * 3f);
            yield return Wait(0.5f);
            Assert.IsFalse(layer.Guide.SparkleActive, "no sparkle without a press");
            Assert.IsFalse(layer.Demo.Active, "no ghost demo without a press");
            Assert.IsFalse(layer.Guide.Active, "no bat trail without a press");

            // With the Helper on, a long stall may show "look here" and "show me" once.
            layer.Settings.HelperAutoHints = true;
            layer.AddStallForTests(NonverbalSettings.AutoHintStallSeconds);
            yield return Frames(3);
            Assert.IsTrue(layer.Demo.Active, "the Helper shows the demonstration after a stall");
            Assert.IsTrue(layer.Guide.SparkleActive, "and the sparkle");
            layer.Settings.HelperAutoHints = false;
        }

        [UnityTest]
        public IEnumerator TheParentGateLineAndSpotsFollowTheControlScheme()
        {
            yield return StartGame();
            var game = GameBootstrap.Instance;
            var menus = game.Director.Nonverbal.Menus;
            game.Flow.Pause();
            yield return Frames(2);
            ControlSchemeTracker.Override = ControlScheme.Touch;
            game.Director.Nonverbal.SettingsButton.onClick.Invoke();
            yield return Frames(2);
            Assert.AreEqual(NvKeys.ParentGateHint, menus.GateHintKey, "touch: the two spots");
            Assert.IsFalse(menus.GateGlyph(0).HasGlyph);

            ControlSchemeTracker.Override = ControlScheme.Gamepad;
            yield return Frames(2);
            Assert.AreEqual(PromptKeys.ParentGateHintGamepad, menus.GateHintKey, "gamepad: both shoulder buttons");
            Assert.AreEqual(PadButton.LeftShoulder, menus.GateGlyph(0).LitButton);
            Assert.AreEqual(PadButton.RightShoulder, menus.GateGlyph(1).LitButton);

            ControlSchemeTracker.Override = ControlScheme.KeyboardMouse;
            yield return Frames(2);
            Assert.AreEqual(PromptKeys.ParentGateHintKeyboard, menus.GateHintKey, "keyboard: Q and P");
            Assert.AreEqual("Q", menus.GateGlyph(0).KeyLabel);
            Assert.AreEqual("P", menus.GateGlyph(1).KeyLabel);
            menus.CancelGate();
            game.Flow.Resume();
        }

        [UnityTest]
        public IEnumerator TheDialogueNextAndSkipGlyphsFollowTheControlScheme()
        {
            yield return StartGame();
            var dialogue = Director.Dialogue;
            var glyphs = dialogue.GetComponent<DialogueGlyphs>();
            Assert.IsNotNull(glyphs);
            ControlSchemeTracker.Override = ControlScheme.Touch;
            dialogue.Play(new[] { new ScriptLine(Speaker.Bea, Lines.BeaSawCape), new ScriptLine(Speaker.Bea, Lines.BeaRepeat) });
            yield return Frames(2);
            Assert.IsTrue(glyphs.NextShowsArrow, "touch: the next disc shows its arrow");
            Assert.IsFalse(glyphs.SkipGlyph.HasGlyph);

            ControlSchemeTracker.Override = ControlScheme.KeyboardMouse;
            yield return Frames(2);
            Assert.IsFalse(glyphs.NextShowsArrow, "keyboard: the arrow gives way to the key");
            Assert.AreEqual("E", glyphs.NextGlyph(1).KeyLabel);
            Assert.AreEqual(GlyphShape.Key, glyphs.SkipGlyph.Glyph.Shape);
            Assert.IsFalse(string.IsNullOrEmpty(glyphs.SkipGlyph.KeyLabel));

            ControlSchemeTracker.Override = ControlScheme.Gamepad;
            yield return Frames(2);
            Assert.AreEqual(PadButton.South, glyphs.NextGlyph(0).LitButton);
            Assert.AreEqual(PadButton.East, glyphs.SkipGlyph.LitButton);
            dialogue.Skip();
        }

        [UnityTest]
        public IEnumerator TheMemoryControlsCarryTheirNames()
        {
            yield return StartGame();
            var replay = Director.Nonverbal.MemoryReplay;
            Assert.IsNotNull(replay.SkipCaption, "the skip disc has its small caption");
            Assert.AreEqual(NvKeys.MemorySkip, replay.SkipCaption.GetComponent<LocalizedText>().Key);
            var name = replay.SkipButton.GetComponent<AccessibleName>();
            Assert.IsNotNull(name);
            Assert.AreEqual(Localizer.Get(NvKeys.MemorySkip), name.Name);
        }
    }
}
