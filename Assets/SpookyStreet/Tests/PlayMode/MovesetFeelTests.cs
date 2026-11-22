using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Controls;
using SpookyStreet.Core.Animation;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Localization;
using SpookyStreet.Core.Tips;
using SpookyStreet.Localization;
using SpookyStreet.Player;
using SpookyStreet.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// S000.6 feel pass in the real scene: an early chain press while jogging, the first-time tip tag (never in the
    /// way of input), sliding a thumb from Jump onto Use to dive out of a glide, and the paper bursts (pause freezes
    /// them, reduced motion skips them). The maths is covered by the Core MovesetFeelTests and MoveTipTests.
    /// </summary>
    public class MovesetFeelTests : InputTestFixture
    {
        private const string SceneName = "LanternLane";

        /// <summary>On the arrival path heading north: flat, clear of the pumpkins and lamps for several metres.</summary>
        private static readonly Vector3 OnPath = new Vector3(0f, 0.3f, -10.5f);

        private static readonly Vector3 AbovePlaza = new Vector3(0f, 5.5f, -0.5f);

        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            Localizer.ResetForTests();
            AmbientMotion.ReducedMotion = false;
        }

        public override void TearDown()
        {
            AmbientMotion.ReducedMotion = false;
            Time.timeScale = 1f;
            base.TearDown();
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

            // Tests never depend on (or write) the player's real "tips already shown" preferences.
            game.Ui.Tips.UseStore(new MemoryTipStore());
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                yield return null;
            }
        }

        private IEnumerator Tap(KeyControl key)
        {
            Press(key, queueEventOnly: true);
            yield return null;
            Release(key, queueEventOnly: true);
            yield return null;
        }

        /// <summary>Seconds until Pip reaches <paramref name="groundY"/> under plain gravity.</summary>
        private static float TimeToLand(PipMotor pip, float groundY)
        {
            float height = Mathf.Max(0f, pip.transform.position.y - groundY);
            float down = Mathf.Max(0f, -pip.VerticalSpeed);
            float g = pip.TuningData.Gravity;
            return (-down + Mathf.Sqrt(down * down + 2f * g * height)) / g;
        }

        [UnityTest]
        public IEnumerator ChainJumpPressedJustBeforeLandingWhileJoggingReachesTwoPointFourMetres()
        {
            yield return StartGame();
            var game = GameBootstrap.Instance;
            var pip = game.Pip;
            var avatar = pip.GetComponent<PipAvatar>();
            var pad = InputSystem.AddDevice<Gamepad>();
            pip.Teleport(OnPath);
            pip.transform.rotation = Quaternion.identity;
            yield return Wait(0.3f);
            Assert.IsTrue(pip.Grounded, "standing on the path");

            // A light jog: the stick a little under half way (after the stick dead zone ≈ 40 % of max speed).
            Set(pad.leftStick, new Vector2(0f, 0.45f));
            yield return Wait(0.6f);
            float max = pip.TuningData.MaxSpeed;
            Assert.That(pip.HorizontalSpeed, Is.InRange(0.27f * max, 0.48f * max),
                "jogging between the new 25 % gate and the old 50 % one");
            float ground = pip.transform.position.y;

            // First jump, held to the apex.
            Press(pad.buttonSouth, queueEventOnly: true);
            for (int i = 0; i < 20 && pip.Grounded; i++)
            {
                yield return null;
            }

            Assert.IsFalse(pip.Grounded, "first jump took off");
            Assert.AreEqual(1, pip.ChainStage);
            float minSquash = 0f;
            while (pip.VerticalSpeed > 0f)
            {
                minSquash = Mathf.Min(minSquash, avatar.SquashValue);
                yield return null;
            }

            Assert.Less(minSquash, -0.03f, "the held jump stretches Pip");
            Release(pad.buttonSouth, queueEventOnly: true);

            // Press again about 0.1 s before touchdown (the press is read on the next frame).
            float deadline = Time.time + 2f;
            while (TimeToLand(pip, ground) > 0.1f + Time.deltaTime && !pip.Grounded && Time.time < deadline)
            {
                yield return null;
            }

            Assert.IsFalse(pip.Grounded, "still in the air when pressing early");
            Press(pad.buttonSouth, queueEventOnly: true);
            yield return null;
            float pressedAt = Time.time;
            int burstsBefore = game.Feedback.BurstsPlayed;
            float jumpedAt = -1f;
            deadline = Time.time + 1f;
            while (Time.time < deadline)
            {
                Assert.IsFalse(pip.Gliding, "an early chain press never spreads the cape");
                if (pip.LastStep.Jumped)
                {
                    jumpedAt = Time.time;
                    break;
                }

                yield return null;
            }

            Assert.Greater(jumpedAt, 0f, "the early press jumped on touchdown");
            Assert.AreEqual(2, pip.ChainStage, "and continued the chain");
            float lead = jumpedAt - pressedAt;
            Assert.That(lead, Is.InRange(0.03f, pip.TuningData.JumpBufferTime + 0.034f),
                $"pressed {lead:F3} s before touchdown");
            Assert.AreEqual(burstsBefore + 1, game.Feedback.BurstsPlayed, "the 2nd chained jump plays its paper burst");
            Assert.IsTrue((game.Feedback.Played & MoveBursts.ChainSecond) != 0);
            Assert.Greater(game.Feedback.ActivePieces, 0, "the burst's pieces are alive");

            // Hold through the rise: the second chain jump reaches 2.4 m.
            float apex = 0f;
            deadline = Time.time + 1.5f;
            while ((pip.VerticalSpeed > 0f || pip.LastStep.Jumped) && Time.time < deadline)
            {
                apex = Mathf.Max(apex, pip.transform.position.y - ground);
                yield return null;
            }

            apex = Mathf.Max(apex, pip.transform.position.y - ground);
            Release(pad.buttonSouth, queueEventOnly: true);
            Set(pad.leftStick, Vector2.zero);
            Debug.Log($"[MovesetFeel] early chain: lead {lead:F3} s, apex {apex:F3} m, jog {0.45f} stick");
            Assert.That(apex, Is.EqualTo(2.4f).Within(0.12f), "second chained jump, held: 2.4 m");
            Assert.AreEqual(0, pip.SafeReturns);
        }

        [UnityTest]
        public IEnumerator HoldingJumpShootsPipAboveTwoMetresBeforeTheCapeOpens()
        {
            // TestFlight 12.1: "holding or tapping quickly, the jump is the same; when I hold, she comes down gliding".
            yield return StartGame();
            var game = GameBootstrap.Instance;
            var pip = game.Pip;
            var avatar = pip.GetComponent<PipAvatar>();
            pip.Teleport(OnPath);
            pip.transform.rotation = Quaternion.identity;
            yield return Wait(0.3f);
            Assert.IsTrue(pip.Grounded, "standing on the path");
            float ground = pip.transform.position.y;
            int burstsBefore = game.Feedback.BurstsPlayed;

            Press(_keyboard.spaceKey, queueEventOnly: true);
            float apex = 0f;
            float minSquash = 0f;
            bool boosted = false;
            float deadline = Time.time + 3f;
            while (!pip.Gliding && Time.time < deadline)
            {
                yield return null;
                boosted |= pip.LastStep.BoostStarted;
                minSquash = Mathf.Min(minSquash, avatar.SquashValue);
                if (!pip.Gliding)
                {
                    apex = Mathf.Max(apex, pip.transform.position.y - ground);
                }
            }

            Debug.Log($"[MovesetFeel] high jump: apex {apex:F3} m before the cape, min squash {minSquash:F3}");
            Assert.IsTrue(pip.Gliding, "held on past the apex and its hang, the cape opens");
            Assert.GreaterOrEqual(apex, 1.95f, "the held jump reaches about 2.1 m before any glide starts");
            Assert.IsTrue(boosted, "the hold boost engaged");
            Assert.IsTrue((game.Feedback.Played & MoveBursts.BoostPuff) != 0, "the boost puffs paper under Pip's feet");
            Assert.Greater(game.Feedback.BurstsPlayed, burstsBefore);
            Assert.Less(minSquash, MoveFeedbackRules.HeldJumpStretch + 0.01f, "the cape flares: Pip stretches tall");

            Release(_keyboard.spaceKey, queueEventOnly: true);
            deadline = Time.time + 3f;
            while (!pip.Grounded && Time.time < deadline)
            {
                yield return null;
            }

            Assert.IsTrue(pip.Grounded, "lands again");
            Assert.AreEqual(0, pip.SafeReturns);

            // A tap stays the routes' 1.0 m jump, with no boost.
            yield return Wait(0.3f);
            ground = pip.transform.position.y;
            yield return Tap(_keyboard.spaceKey);
            float tapApex = 0f;
            deadline = Time.time + 2f;
            do
            {
                tapApex = Mathf.Max(tapApex, pip.transform.position.y - ground);
                yield return null;
            }
            while (!pip.Grounded && Time.time < deadline);

            Debug.Log($"[MovesetFeel] tap: apex {tapApex:F3} m");
            Assert.That(tapApex, Is.EqualTo(1.0f).Within(0.1f), "a tap still peaks at 1.0 m");
        }

        [UnityTest]
        public IEnumerator AShortJumpShowsTheHoldTipWhichNeverBlocksInputAndFades()
        {
            yield return StartGame();
            var game = GameBootstrap.Instance;
            var pip = game.Pip;
            var tips = game.Ui.Tips;
            game.Ui.FirstPrompt.SetActive(false); // one prompt at a time: the walk prompt would hold tips back
            pip.Teleport(OnPath);
            pip.transform.rotation = Quaternion.identity;
            yield return Wait(0.3f);

            yield return Tap(_keyboard.spaceKey);
            float deadline = Time.time + 0.6f;
            while (tips.Shown == MoveTip.None && Time.time < deadline)
            {
                yield return null;
            }

            Assert.AreEqual(MoveTip.HoldJump, tips.Shown, "a short jump teaches the hold");
            Assert.IsTrue(tips.Tag.activeInHierarchy);
            Assert.AreEqual(LocalizationKeys.TipHoldJump, tips.Tag.GetComponentInChildren<LocalizedText>(true).Key);
            foreach (var graphic in tips.Tag.GetComponentsInChildren<Graphic>(true))
            {
                Assert.IsFalse(graphic.raycastTarget, graphic.name + " would catch touches");
            }

            Assert.IsFalse(tips.Tag.GetComponent<CanvasGroup>().blocksRaycasts);

            // Input keeps working while the tip is up.
            yield return Wait(0.6f);
            Press(_keyboard.wKey);
            yield return Wait(0.5f);
            Assert.Greater(pip.HorizontalSpeed, 2f, "walking with the tip on screen");
            Release(_keyboard.wKey);
            Assert.AreEqual(MoveTip.HoldJump, tips.Shown, "still up within 3 s");

            yield return Wait(2.4f);
            Assert.AreEqual(MoveTip.None, tips.Shown, "faded after about 3 s");
            Assert.IsFalse(tips.Tag.activeSelf);
            Assert.IsTrue(tips.Scheduler.HasShown(MoveTip.HoldJump), "and never again");
        }

        [UnityTest]
        public IEnumerator SlidingTheThumbFromJumpOntoUseDivesOutOfAGlide()
        {
            yield return StartGame();
            var game = GameBootstrap.Instance;
            var pip = game.Pip;
            var touch = game.Ui.Hud.GetComponent<TouchControls>();
            pip.Teleport(AbovePlaza);
            pip.transform.rotation = Quaternion.Euler(0f, 180f, 0f); // dive back down the clear path
            yield return Wait(0.25f);

            var thumb = new PointerEventData(EventSystem.current) { pointerId = 9 };
            touch.JumpButton.OnPointerDown(thumb);
            thumb.rawPointerPress = touch.JumpButton.gameObject;
            thumb.pointerPress = touch.JumpButton.gameObject;
            yield return Wait(0.45f);
            Assert.IsTrue(pip.Gliding, "holding the Jump disc in the air glides");
            Assert.AreEqual(LocalizationKeys.HudDive, game.Ui.UseVerbKey, "in the air the Use disc reads Dive");

            // A hover (no press) never presses Use.
            var hover = new PointerEventData(EventSystem.current) { pointerId = 4 };
            touch.UseButton.OnPointerEnter(hover);
            yield return null;
            yield return null;
            Assert.IsFalse(pip.Diving);

            int burstsBefore = game.Feedback.BurstsPlayed;
            touch.JumpButton.OnPointerExit(thumb);
            touch.UseButton.OnPointerEnter(thumb);
            for (int i = 0; i < 5 && !pip.Diving; i++)
            {
                yield return null;
            }

            Assert.IsTrue(pip.Diving, "sliding onto Use mid-glide dives");
            Assert.IsFalse(pip.Gliding, "the cape folds into the dive");
            Assert.IsTrue((game.Feedback.Played & MoveBursts.DiveStreak) != 0, "the dive plays its speed streaks");
            touch.UseButton.OnPointerExit(thumb);

            float deadline = Time.time + 3f;
            while (!pip.Grounded && Time.time < deadline)
            {
                yield return null;
            }

            Assert.IsTrue(pip.Grounded, "the dive lands");
            yield return null;
            Assert.IsTrue((game.Feedback.Played & MoveBursts.DiveDust) != 0, "landing the dive puffs dust");
            Assert.GreaterOrEqual(game.Feedback.BurstsPlayed, burstsBefore + 2);
            Assert.AreEqual(LocalizationKeys.HudRoll, game.Ui.UseVerbKey, "back on the ground the disc reads Roll");
            Assert.AreEqual(0, pip.SafeReturns);
        }

        [UnityTest]
        public IEnumerator BurstsFreezeWhilePausedAndReducedMotionSkipsThem()
        {
            yield return StartGame();
            var game = GameBootstrap.Instance;
            var feedback = game.Feedback;

            feedback.Play(MoveBursts.ChainThird);
            Assert.Greater(feedback.ActivePieces, 0);
            yield return null;
            game.Flow.Pause();
            yield return null;
            int alive = feedback.ActivePieces;
            yield return new WaitForSecondsRealtime(1f); // longer than any piece's life
            Assert.AreEqual(alive, feedback.ActivePieces, "paused: pieces hold still and do not expire");
            game.Flow.Resume();
            yield return Wait(1f);
            Assert.AreEqual(0, feedback.ActivePieces, "after resuming they finish and return to the pool");
            Assert.AreEqual(GameState.Playing, game.Flow.State);

            AmbientMotion.ReducedMotion = true;
            int played = feedback.BurstsPlayed;
            feedback.Play(MoveBursts.ChainThird | MoveBursts.DiveDust | MoveBursts.BoostPuff);
            Assert.AreEqual(played, feedback.BurstsPlayed, "reduced motion skips the bursts");
            Assert.AreEqual(0, feedback.ActivePieces);
        }
    }
}
