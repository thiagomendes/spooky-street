using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Controls;
using SpookyStreet.Core.Animation;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Lines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// S003a in the running game (docs/S003a-pass.md): talking to a witness plays their testimony gesture on the line
    /// that has one (<see cref="Core.Characters.LineGestures"/>), and a tier-1 hint makes Pip glance toward the side the
    /// area lies on while she stands (<see cref="HintPresentation.PipGlance"/>). Fresh temporary save folder per test.
    /// </summary>
    public class TestimonyGesturePlayTests : InputTestFixture
    {
        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            CaseDirector.UseTemporarySaveDirectory();
            ControlSchemeTracker.Override = null;
        }

        public override void TearDown()
        {
            ControlSchemeTracker.Override = null;
            base.TearDown();
        }

        private static CaseDirector Director => GameBootstrap.Instance.Director;

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
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                yield return null;
            }
        }

        /// <summary>Closes the conversation and any memory replay it started.</summary>
        private static IEnumerator CloseTalk()
        {
            Director.Dialogue.Skip();
            yield return Wait(0.3f);
            if (Director.Nonverbal.MemoryReplay.Active)
            {
                Director.Nonverbal.MemoryReplay.End(false);
            }

            yield return Wait(0.3f);
            Director.Captions.Clear();
        }

        [UnityTest]
        public IEnumerator ATestimonyTalkPlaysTheSpeakersGestureOnItsLine()
        {
            yield return StartGame();
            var game = GameBootstrap.Instance;
            var layer = Director.Nonverbal;
            var bea = game.Witnesses.Bea;
            var vale = game.Witnesses.Vale;
            if (bea == null || vale == null || bea.Avatar == null || !bea.Avatar.HasClip(CharacterClips.ShadeEyes) ||
                vale.Avatar == null || !vale.Avatar.HasClip(CharacterClips.CheckWatch))
            {
                Assert.Ignore("The witness models with the S003a gestures are not committed.");
            }

            Director.Perform(CaseAction.Of(CaseActionKind.HearKnocks));
            Director.Captions.Clear();

            // Bea: "Something silver flashed up high…" and she shades her eyes, turned toward the side of the house.
            float beaYaw = bea.transform.eulerAngles.y;
            Director.Perform(CaseAction.Talk(Speaker.Bea));
            Assert.AreEqual(Lines.BeaSawCape, Director.Dialogue.CurrentLine.Key);
            Assert.IsNull(layer.LastTestimonyGesture, "not before its moment on the line");
            yield return Wait(0.7f);
            Assert.AreEqual(CharacterClips.ShadeEyes, layer.LastTestimonyGesture);
            Assert.AreSame(bea, layer.LastTestimonyGestureBy);
            Assert.IsTrue(bea.Gesturing, "she is shading her eyes");
            Assert.AreEqual(CharacterClips.ShadeEyes, bea.Avatar.OneShot);
            Assert.Greater(Mathf.Abs(Mathf.DeltaAngle(bea.transform.eulerAngles.y, beaYaw)), 20f,
                "she turns toward the side of the house as she looks");
            yield return CloseTalk();
            yield return Wait(2.6f);
            Assert.IsFalse(bea.Gesturing, "the gesture ends");
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(bea.transform.eulerAngles.y, beaYaw)), 1f, "and she turns back");

            // Mr. Vale's first account has no gesture (the bench view is what it is about).
            Director.Perform(CaseAction.Talk(Speaker.Vale));
            Assert.AreEqual(Lines.ValeDoorShut, Director.Dialogue.CurrentLine.Key);
            yield return Wait(0.9f);
            Assert.IsFalse(vale.Gesturing, "no gesture on his first line");
            Assert.AreSame(bea, layer.LastTestimonyGestureBy);
            yield return CloseTalk();

            // "Not one creak from that door all evening.": he checks his watch, seated, without turning on his bench.
            float valeYaw = vale.transform.eulerAngles.y;
            Director.Perform(CaseAction.Talk(Speaker.Vale));
            Assert.AreEqual(Lines.ValeRepeat, Director.Dialogue.CurrentLine.Key);
            yield return Wait(0.9f);
            Assert.AreEqual(CharacterClips.CheckWatch, layer.LastTestimonyGesture);
            Assert.AreSame(vale, layer.LastTestimonyGestureBy);
            Assert.IsTrue(vale.Gesturing, "he checks his watch");
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(vale.transform.eulerAngles.y, valeYaw)), 0.5f, "he stays put on his bench");

            // A line skipped before its moment drops its gesture.
            yield return CloseTalk();
            yield return Wait(2.6f);
            Assert.IsFalse(vale.Gesturing);
            Director.Perform(CaseAction.Talk(Speaker.Vale));
            Assert.AreEqual(Lines.ValeRepeat, Director.Dialogue.CurrentLine.Key);
            Director.Dialogue.Skip();
            yield return Wait(0.9f);
            Assert.IsFalse(vale.Gesturing, "the line was gone before the watch came up");
            yield return CloseTalk();
        }

        [UnityTest]
        public IEnumerator ATierOneHintMakesPipGlanceTowardTheArea()
        {
            yield return StartGame();
            var game = GameBootstrap.Instance;
            var animator = game.Pip.GetComponent<PipAvatar>().Animator;
            if (animator == null || !animator.HasClip(PipClips.GlanceLeft))
            {
                Assert.Ignore("Pip.fbx with the glance clips is not committed.");
            }

            // The bench is found, so the hint leads to the side yard. Pip on the west sidewalk facing east: the side
            // yard lies to her left.
            Director.Perform(CaseAction.Of(CaseActionKind.HearKnocks));
            Director.Perform(CaseAction.Of(CaseActionKind.SitOnBench));
            Director.Bench.End();
            Director.Captions.Clear();
            Assert.AreEqual(CaseObjective.FindWayIn, CaseHints.ObjectiveFor(Director.State));
            var layer = Director.Nonverbal;
            var guide = layer.Guide;
            game.Pip.Teleport(new Vector3(-8.9f, 0.15f, 1.4f));
            game.Pip.transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            yield return Wait(1.2f);
            Assert.IsTrue(game.Pip.Grounded, "Pip stands");

            layer.RadioButton.onClick.Invoke();
            yield return null;
            Assert.IsTrue(guide.BreezeActive, "tier 1 nudges");
            Assert.AreEqual(PipClips.GlanceLeft, guide.PipGlance, "she glances toward the side yard, on her left");
            Assert.AreEqual(PipClips.GlanceLeft, animator.OneShot);
            yield return Wait(1.6f);
            Assert.IsNull(animator.OneShot, "the glance ends; the breeze carries on");
            Assert.IsTrue(guide.BreezeActive);

            // Facing the yard: it is in front of her, so she does not glance (the breeze and the glow show it).
            var yard = HintAreas.For(CaseObjective.FindWayIn);
            Vector3 to = new Vector3(yard.Centre.X, 0f, yard.Centre.Z) - new Vector3(game.Pip.transform.position.x, 0f,
                game.Pip.transform.position.z);
            game.Pip.transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
            yield return Wait(0.3f);
            Assert.IsTrue(guide.StartBreeze(CaseObjective.FindWayIn));
            Assert.IsNull(guide.PipGlance, "the area is ahead of her");
            Assert.IsNull(animator.OneShot);

            // Reduced motion: no glance either.
            game.Pip.transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            yield return Wait(0.3f);
            var reduced = guide.ReducedMotion;
            guide.ReducedMotion = () => true;
            Assert.IsTrue(guide.StartBreeze(CaseObjective.FindWayIn));
            Assert.IsNull(guide.PipGlance, "reduced motion keeps Pip still");
            guide.ReducedMotion = reduced;

            // Standing again with the yard to her left she glances; then the radio's second press is the trail: no glance.
            Assert.IsTrue(guide.StartBreeze(CaseObjective.FindWayIn));
            Assert.AreEqual(PipClips.GlanceLeft, guide.PipGlance, "standing again with the yard to her left");
            yield return Wait(1.6f);
            layer.RadioButton.onClick.Invoke();
            yield return null;
            Assert.IsTrue(guide.Active, "tier 2: the bats");
            Assert.IsNull(animator.OneShot, "the bats lead; Pip does not glance again");
        }
    }
}
