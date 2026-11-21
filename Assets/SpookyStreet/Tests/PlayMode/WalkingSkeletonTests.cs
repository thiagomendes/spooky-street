using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Localization;
using SpookyStreet.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// S000 acceptance through the normal entry point: load the gameplay scene, see the localized title,
    /// start, walk with simulated keyboard input, pause and resume. Editor-hosted; a device pass is separate.
    /// </summary>
    public class WalkingSkeletonTests : InputTestFixture
    {
        private const string SceneName = "LanternLane";
        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            Localizer.ResetForTests();
        }

        private static IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(GameBootstrap.Instance, "scene did not create GameBootstrap");
        }

        [UnityTest]
        public IEnumerator BootsToLocalizedTitle()
        {
            yield return LoadGame();
            var game = GameBootstrap.Instance;
            Assert.AreEqual(GameState.Title, game.Flow.State);
            Assert.IsTrue(game.Ui.TitleScreen.activeInHierarchy);
            Assert.IsFalse(game.Ui.Hud.activeInHierarchy);

            var labels = game.Ui.TitleScreen.GetComponentsInChildren<LocalizedText>(true);
            bool foundTitle = false;
            foreach (var label in labels)
            {
                if (label.Key == LocalizationKeys.TitleGameName)
                {
                    Assert.AreEqual("Spooky Street", label.GetComponent<Text>().text);
                    foundTitle = true;
                }
            }

            Assert.IsTrue(foundTitle, "title text must be bound to a localization key");
            Assert.AreEqual(0f, Time.timeScale, "simulation is suspended on the title screen");
        }

        [UnityTest]
        public IEnumerator TitleThenWalkThenPause()
        {
            yield return LoadGame();
            var game = GameBootstrap.Instance;
            game.Ui.NewGameButton.onClick.Invoke();
            Assert.AreEqual(GameState.Playing, game.Flow.State);
            Assert.IsTrue(game.Ui.FirstPrompt.activeSelf, "first prompt appears when play starts");

            // Let Pip settle on the porch, then walk forward for one second.
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            Vector3 start = game.Pip.transform.position;
            Press(_keyboard.wKey);
            float until = Time.time + 1f;
            while (Time.time < until)
            {
                yield return null;
            }

            Release(_keyboard.wKey);
            yield return null;
            Vector3 moved = game.Pip.transform.position - start;
            moved.y = 0f;
            Assert.Greater(moved.magnitude, 2f, "holding forward for 1 s should walk well over 2 m");
            Assert.LessOrEqual(moved.magnitude, 4.2f, "never faster than max speed (4 m/s) plus a frame");
            Assert.IsFalse(game.Ui.FirstPrompt.activeSelf, "first prompt dismisses after walking");

            // Queue only, so the press is processed in the next frame's input update before Update reads it.
            Press(_keyboard.escapeKey, queueEventOnly: true);
            yield return null;
            Release(_keyboard.escapeKey, queueEventOnly: true);
            yield return null;
            Assert.AreEqual(GameState.Paused, game.Flow.State);
            Assert.AreEqual(0f, Time.timeScale);
            Assert.IsTrue(game.Ui.PauseScreen.activeInHierarchy);

            Vector3 pausedAt = game.Pip.transform.position;
            Press(_keyboard.wKey);
            yield return null;
            yield return null;
            Release(_keyboard.wKey);
            Assert.AreEqual(pausedAt, game.Pip.transform.position, "no movement while paused");

            game.Ui.ResumeButton.onClick.Invoke();
            Assert.AreEqual(GameState.Playing, game.Flow.State);
            Assert.AreEqual(1f, Time.timeScale);
        }

        [UnityTest]
        public IEnumerator FallingOffReturnsToSafeGroundWithinTwoSeconds()
        {
            yield return LoadGame();
            var game = GameBootstrap.Instance;
            game.Ui.NewGameButton.onClick.Invoke();
            for (int i = 0; i < 20; i++)
            {
                yield return null;
            }

            Vector3 safe = game.Pip.transform.position;
            game.Pip.Teleport(new Vector3(30f, 2f, 30f)); // outside the floor
            float deadline = Time.time + 2f;
            while (game.Pip.SafeReturns == 0 && Time.time < deadline)
            {
                yield return null;
            }

            Assert.AreEqual(1, game.Pip.SafeReturns, "Pip must return to the last safe anchor within 2 s");
            Assert.Less(Vector3.Distance(safe, game.Pip.transform.position), 0.5f);
        }

        [UnityTest]
        public IEnumerator SuspendingTheAppOpensPaused()
        {
            yield return LoadGame();
            var game = GameBootstrap.Instance;
            game.Ui.NewGameButton.onClick.Invoke();
            yield return null;
            game.SendMessage("OnApplicationPause", true);
            Assert.AreEqual(GameState.Paused, game.Flow.State);
            game.SendMessage("OnApplicationPause", false);
            Assert.AreEqual(GameState.Paused, game.Flow.State, "resuming the app stays paused until the player chooses");
        }

        [UnityTest]
        public IEnumerator PseudoLanguageSwapsVisibleTitle()
        {
            yield return LoadGame();
            var game = GameBootstrap.Instance;
            Localizer.TogglePseudo();
            yield return null;
            Text title = null;
            foreach (var label in game.Ui.TitleScreen.GetComponentsInChildren<LocalizedText>(true))
            {
                if (label.Key == LocalizationKeys.TitleGameName)
                {
                    title = label.GetComponent<Text>();
                }
            }

            Assert.IsNotNull(title);
            StringAssert.StartsWith("[", title.text);
            Localizer.TogglePseudo();
            Assert.AreEqual("Spooky Street", title.text);
        }
    }
}
