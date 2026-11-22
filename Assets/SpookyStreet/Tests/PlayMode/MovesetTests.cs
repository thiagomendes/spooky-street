using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Controls;
using SpookyStreet.Core.Animation;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Localization;
using SpookyStreet.Localization;
using SpookyStreet.Player;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// The moveset in the real scene (spec §5 v1.2): holding Jump in the air spreads the bat cape (slow fall, Glide
    /// pose), a glide over the void still returns Pip to safe ground, Use while running rolls, an interaction in
    /// range wins over the roll, and pause freezes all of it. The maths is covered by the Core MovesetTests.
    /// </summary>
    public class MovesetTests : InputTestFixture
    {
        private const string SceneName = "LanternLane";

        /// <summary>Open sky above the plaza path, clear of the maple, the pumpkins and the table.</summary>
        private static readonly Vector3 AbovePlaza = new Vector3(0f, 5.5f, -0.5f);

        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            Localizer.ResetForTests();
        }

        private static IEnumerator StartGame()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            var game = GameBootstrap.Instance;
            Assert.IsNotNull(game, "scene did not create GameBootstrap");
            game.Ui.NewGameButton.onClick.Invoke();
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                yield return null;
            }
        }

        /// <summary>Press edge processed in the next frame's input update, before gameplay reads it.</summary>
        private IEnumerator Tap(KeyControl key)
        {
            Press(key, queueEventOnly: true);
            yield return null;
            Release(key, queueEventOnly: true);
            yield return null;
        }

        [UnityTest]
        public IEnumerator HoldingJumpInTheAirGlides()
        {
            yield return StartGame();
            var game = GameBootstrap.Instance;
            var pip = game.Pip;
            var animator = pip.GetComponent<PipAvatar>().Animator;
            pip.Teleport(AbovePlaza);
            pip.transform.rotation = Quaternion.identity;
            yield return Wait(0.25f); // falling freely, past coyote time

            Press(_keyboard.spaceKey, queueEventOnly: true);
            yield return Wait(0.45f);
            Assert.IsFalse(pip.Grounded, "still in the air");
            Assert.IsTrue(pip.Gliding, "holding Jump while falling spreads the cape");
            Assert.GreaterOrEqual(pip.VerticalSpeed, -2f, "the cape clamps the fall to about 2 m/s");
            if (animator != null)
            {
                Assert.AreEqual(PipPose.Glide, animator.Pose);
            }

            // A long glide over solid ground is not a fall: past the 1.4 s fall limit Pip is still gliding.
            yield return Wait(1.1f);
            Assert.IsTrue(pip.Gliding, "the glide continues");
            Assert.AreEqual(0, pip.SafeReturns, "gliding over the plaza must not trigger the fall return");
            Assert.GreaterOrEqual(pip.VerticalSpeed, -2f);

            // Pause freezes Pip mid-glide.
            game.Flow.Pause();
            yield return null;
            Vector3 paused = pip.transform.position;
            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }

            Assert.AreEqual(paused, pip.transform.position, "no movement while paused");
            game.Flow.Resume();
            Release(_keyboard.spaceKey, queueEventOnly: true);
            yield return Wait(0.2f);
            Assert.IsFalse(pip.Gliding, "releasing Jump folds the cape");

            float deadline = Time.time + 2f;
            while (!pip.Grounded && Time.time < deadline)
            {
                yield return null;
            }

            Assert.IsTrue(pip.Grounded, "Pip lands on the plaza");
            Assert.AreEqual(0, pip.SafeReturns);
        }

        [UnityTest]
        public IEnumerator GlidingOverTheVoidStillReturnsToSafeGroundWithinTwoSeconds()
        {
            yield return StartGame();
            var game = GameBootstrap.Instance;
            var pip = game.Pip;
            Vector3 safe = pip.transform.position;
            pip.Teleport(new Vector3(30f, 2f, 30f)); // outside the floor
            yield return Wait(0.2f); // past coyote time

            // Hold the on-screen Jump disc (touch path).
            var touch = game.Ui.Hud.GetComponent<TouchControls>();
            Assert.IsNotNull(touch, "the HUD hosts the touch controls");
            var jump = touch.JumpButton;
            var finger = new PointerEventData(EventSystem.current) { pointerId = 7 };
            jump.OnPointerDown(finger);
            bool glided = false;
            float deadline = Time.time + 2f;
            while (pip.SafeReturns == 0 && Time.time < deadline)
            {
                glided |= pip.Gliding;
                yield return null;
            }

            jump.OnPointerUp(finger);
            Assert.IsTrue(glided, "holding the touch Jump disc in the air glides");
            Assert.AreEqual(1, pip.SafeReturns, "a glide into the void must still return Pip within 2 s");
            Assert.Less(Vector3.Distance(safe, pip.transform.position), 0.5f);
        }

        [UnityTest]
        public IEnumerator UseWhileRunningRolls()
        {
            yield return StartGame();
            var game = GameBootstrap.Instance;
            var pip = game.Pip;
            var animator = pip.GetComponent<PipAvatar>().Animator;
            Assert.AreEqual(LocalizationKeys.HudRoll, game.Ui.UseVerbKey, "Use reads Roll with nothing to interact with");

            Press(_keyboard.wKey);
            yield return Wait(1.0f); // past the porch steps, running on the flat path
            Assert.Greater(pip.HorizontalSpeed, 0.6f * 4f, "running fast enough to roll");

            yield return Tap(_keyboard.eKey);
            for (int i = 0; i < 5 && !pip.Rolling; i++)
            {
                yield return null;
            }

            Assert.IsTrue(pip.Rolling, "Use while running rolls");
            Assert.Greater(pip.HorizontalSpeed, 4.5f, "the roll is a burst above running speed");
            if (animator != null && animator.HasClip(PipClips.Roll))
            {
                Assert.AreEqual(PipClips.Roll, animator.OneShot, "the Roll clip plays");
            }

            yield return Wait(0.7f);
            Release(_keyboard.wKey);
            Assert.IsFalse(pip.Rolling, "the roll is short");
            Assert.AreEqual(0, pip.SafeReturns);
        }

        private sealed class FakeTarget : IUseTargetSource
        {
            public int Interactions;
            public bool HasTarget => true;
            public string VerbKey => LocalizationKeys.HudUse;
            public string IconName => null;
            public void Interact() => Interactions++;
        }

        [UnityTest]
        public IEnumerator InteractionInRangeWinsOverTheRoll()
        {
            yield return StartGame();
            var game = GameBootstrap.Instance;
            var pip = game.Pip;
            var target = new FakeTarget();
            pip.UseTargets = target;
            yield return null;
            Assert.AreEqual(LocalizationKeys.HudUse, game.Ui.UseVerbKey, "the Use disc shows the interaction's verb");

            Press(_keyboard.wKey);
            yield return Wait(1.0f); // past the porch steps, running on the flat path
            yield return Tap(_keyboard.eKey);
            yield return null;
            Assert.AreEqual(1, target.Interactions, "Use goes to the interaction");
            Assert.IsFalse(pip.Rolling, "and not to the roll");
            Release(_keyboard.wKey);

            pip.UseTargets = null;
            yield return null;
            Assert.AreEqual(LocalizationKeys.HudRoll, game.Ui.UseVerbKey);
            Assert.AreEqual(GameState.Playing, game.Flow.State);
        }
    }
}
