using System.Collections;
using System.Linq;
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
    /// The hint pictures in the running game (retiered by S002.5b, D26 §6, D25): a tier-1 press breezes leaves toward
    /// the area and warms it while a witness glances there; tier 2 circles the area with bats; neither sits on the clue
    /// object; tier 3 only demonstrates a move where one blocks (the ghost hand on touch, the glyph otherwise); nothing
    /// shows without a press while the Helper is off. Also the per-scheme parent gate line, the dialogue's next and
    /// skip glyphs and the memory replay's skip name. Fresh temporary save folder per test.
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

        /// <summary>The bench and the way in are found: the hint leads up the side yard; Pip stands by the crate.</summary>
        private static IEnumerator StandByTheCrate()
        {
            var game = GameBootstrap.Instance;
            Director.Perform(CaseAction.Of(CaseActionKind.HearKnocks));
            Director.Perform(CaseAction.Of(CaseActionKind.SitOnBench));
            Director.Bench.End();
            yield return Wait(0.2f);
            Director.Captions.Clear();
            Assert.AreEqual(CaseObjective.FindWayIn, CaseHints.ObjectiveFor(Director.State));
            var yard = SpookyStreet.Core.World.CaseRouteLayout.SkilledAscent[0];
            game.Pip.Teleport(new Vector3(yard.X, 0.1f, yard.Z));
            game.CameraRig.Orbit.RequestRecenter();
            yield return Wait(0.8f);
        }

        /// <summary>Every Use target's marker point (what a hint must never sit on).</summary>
        private static float NearestTarget(Vector3 at, out string id)
        {
            float best = float.MaxValue;
            id = null;
            foreach (var point in CaseStage.AllPoints)
            {
                var marker = new Vector3(point.Position.X, point.Position.Y + point.MarkerHeight, point.Position.Z);
                float d = Vector3.Distance(marker, at);
                if (d < best)
                {
                    best = d;
                    id = point.Id;
                }
            }

            return best;
        }

        [UnityTest]
        public IEnumerator ATierOnePressBreezesTowardTheAreaAndAWitnessGlancesThere()
        {
            yield return StartGame();
            yield return FaceVale();
            var layer = Director.Nonverbal;
            Assert.AreEqual(HintTier.Observation, Director.NextHintTier);
            layer.RadioButton.onClick.Invoke();
            yield return Frames(3);
            var guide = layer.Guide;
            Assert.IsTrue(guide.BreezeActive, "tier 1 nudges");
            Assert.AreEqual(CaseObjective.CheckView, guide.BreezeObjective);
            var area = HintAreas.For(CaseObjective.CheckView);
            Assert.AreSame(area, guide.BreezeArea, "the breeze points to the bench's area");
            yield return Wait(0.9f);
            Assert.Greater(guide.VisibleLeaves, 0, "leaves ride the breeze");
            Assert.LessOrEqual(guide.VisibleLeaves, HintPresentation.BreezeLeaves);
            Assert.IsTrue(guide.GlowVisible, "a warm glow rises over the area");
            var centre = new Vector3(area.Centre.X, area.Centre.Y, area.Centre.Z);
            Assert.Less(Vector3.Distance(new Vector3(guide.GlowCentre.x, centre.y, guide.GlowCentre.z), centre), 0.01f,
                "the glow lies over the area's centre");
            Assert.AreEqual(area.Radius, guide.GlowRadius, 1e-3f, "and covers the whole area");
            Assert.IsNotNull(layer.HintBadge, "the hint line carries a picture");
            Assert.IsTrue(layer.HintBadge.gameObject.activeInHierarchy, "the badge shows while the hint line is up");
            Assert.AreEqual(SpookyStreet.Core.Art.UiSpriteCatalog.HintStar, layer.HintBadgePicture.sprite.name,
                "tier 1's badge picture is still the star");
            Assert.IsFalse(guide.Active, "tier 1 has no bat trail");
            Assert.IsFalse(layer.Demo.Active, "tier 1 has no demo");

            Assert.IsNotNull(guide.Looker, "a witness in view glances toward the area");
            Assert.IsTrue(guide.Looker.LookingAtPoint);
            Assert.Less(Vector3.Distance(guide.Looker.LookPoint, centre + Vector3.up * HintPresentation.AreaLift), 0.01f,
                "toward the area, not at the bench");
            yield return Wait(HintPresentation.LookSeconds + 0.4f);
            Assert.IsFalse(guide.Looker.LookingAtPoint, "the glance lasts about 2 s");

            // Meeting the objective stops the nudge.
            Director.Perform(CaseAction.Of(CaseActionKind.SitOnBench));
            yield return Frames(2);
            Assert.IsFalse(guide.BreezeActive, "the nudge stops when the objective changes");
            Assert.AreEqual(0, guide.VisibleLeaves);
            Assert.IsFalse(guide.GlowVisible);
            Director.Bench.End();
        }

        [UnityTest]
        public IEnumerator TheTierOneAndTwoVisualsNeverSitOnTheClueObject()
        {
            yield return StartGame();
            yield return FaceVale();
            var layer = Director.Nonverbal;
            var guide = layer.Guide;
            layer.RadioButton.onClick.Invoke(); // tier 1: breeze and glow
            float until = Time.time + HintPresentation.BreezeSeconds - 0.5f;
            int swirlFrames = 0;
            while (Time.time < until)
            {
                yield return null;
                Assert.GreaterOrEqual(NearestTarget(guide.GlowCentre, out string id), HintAreas.MinClearance - 0.1f,
                    "the glow's centre is clear of " + id);
                if (guide.SwirlingLeaves > 0)
                {
                    swirlFrames++;
                }
            }

            Assert.Greater(swirlFrames, 0, "the leaves settle into their swirl over the area");

            layer.RadioButton.onClick.Invoke(); // tier 2: the bats circle the area
            Assert.IsTrue(guide.Active);
            Assert.IsFalse(guide.BreezeActive, "the nudge gives way to the trail");
            int circling = 0;
            until = Time.time + 6f;
            while (Time.time < until)
            {
                yield return null;
                for (int i = 0; i < guide.VisibleBats; i++)
                {
                    if (!guide.BatCircling(i))
                    {
                        continue;
                    }

                    circling++;
                    var at = guide.BatPosition(i);
                    Assert.GreaterOrEqual(NearestTarget(at, out string id), HintPresentation.MinMarkerClearance - 0.15f,
                        $"a circling bat at {at} sits on the {id} target");
                    var goal = guide.Goal;
                    float plan = new Vector2(at.x - goal.x, at.z - goal.z).magnitude;
                    Assert.AreEqual(guide.TrailArea.Radius * HintPresentation.TrailRingFactor, plan, 0.05f,
                        "circling bats keep to the area's ring");
                }
            }

            Assert.Greater(circling, 0, "the bats circled the area");
            Assert.AreEqual(0, GameObject.FindObjectsByType<SpriteRenderer>(FindObjectsSortMode.None)
                .Count(r => r.gameObject.activeInHierarchy && r.name == "GoalSticker"), "no goal sticker names the object");
        }

        [UnityTest]
        public IEnumerator TierThreeOnlyDemonstratesAMoveWhereOneBlocks()
        {
            yield return StartGame();
            ControlSchemeTracker.Override = ControlScheme.Touch;
            yield return FaceVale();
            var layer = Director.Nonverbal;
            layer.RadioButton.onClick.Invoke();
            layer.RadioButton.onClick.Invoke();
            Assert.AreEqual(HintTier.Location, Director.NextHintTier, "no move blocks the bench: two pips");
            layer.RadioButton.onClick.Invoke();
            Assert.IsFalse(layer.Demo.Active, "never a Use demo on the bench");
            Assert.IsTrue(layer.Guide.Active, "the third press flies the trail again");
            Assert.AreEqual(Director.Captions.CurrentKey, CaseHints.HintFor(CaseObjective.CheckView, HintTier.Location));

            // By the crate on the side-yard climb, the third tier demonstrates the hop.
            yield return StandByTheCrate();
            layer.RadioButton.onClick.Invoke();
            layer.RadioButton.onClick.Invoke();
            Assert.AreEqual(HintTier.Action, Director.NextHintTier, "a move blocks here: the third pip lights");
            layer.RadioButton.onClick.Invoke();
            Assert.IsTrue(layer.Demo.Active, "tier 3 demonstrates the move");
            Assert.AreEqual(HintMove.Jump, layer.Demo.Demo);
            Assert.AreEqual(Director.Captions.CurrentKey, SpookyStreet.Core.Localization.LocalizationKeys.Hints.MoveJump);
            Assert.IsFalse(layer.Guide.BreezeActive);
            Assert.IsFalse(layer.Guide.Active);
            yield return Wait(0.6f);
            Assert.AreEqual(ControlScheme.Touch, layer.Demo.Scheme);
            Assert.IsTrue(layer.Demo.HandVisible, "the translucent hand taps the Jump disc");
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
            yield return StandByTheCrate();
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

            // The radio stays on tier 3: a press with a gamepad shows the lit Jump button, never a hand.
            ControlSchemeTracker.Override = ControlScheme.Gamepad;
            layer.RadioButton.onClick.Invoke();
            yield return Wait(0.4f);
            Assert.IsTrue(layer.Demo.GlyphVisible);
            Assert.IsFalse(layer.Demo.HandVisible);
            Assert.AreEqual(GlyphShape.PadFace, layer.Demo.Glyph.Glyph.Shape);
        }

        [UnityTest]
        public IEnumerator NothingShowsWithoutAPressWhileHelperIsOffAndTheHelperOnlyNudges()
        {
            yield return StartGame();
            var layer = Director.Nonverbal;
            Assert.IsFalse(layer.Settings.HelperAutoHints, "Helper is off by default");
            layer.AddStallForTests(NonverbalSettings.AutoHintStallSeconds * 3f);
            yield return Wait(0.5f);
            Assert.IsFalse(layer.Guide.BreezeActive, "no nudge without a press");
            Assert.IsFalse(layer.Demo.Active, "no demo without a press");
            Assert.IsFalse(layer.Guide.Active, "no bat trail without a press");

            // With the Helper on, a long stall may offer the tier-1 nudge once, never a demo (D26 §6).
            layer.Settings.HelperAutoHints = true;
            layer.AddStallForTests(NonverbalSettings.AutoHintStallSeconds);
            yield return Frames(3);
            Assert.IsTrue(layer.Guide.BreezeActive, "the Helper offers the nudge after a stall");
            Assert.IsFalse(layer.Demo.Active, "and never a demonstration");
            Assert.IsFalse(layer.Guide.Active, "or the trail");
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
