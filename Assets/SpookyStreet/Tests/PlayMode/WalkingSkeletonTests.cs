using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Localization;
using SpookyStreet.Core.Animation;
using SpookyStreet.Localization;
using SpookyStreet.Player;
using SpookyStreet.World;
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
            CaseDirector.UseTemporarySaveDirectory(); // no save leaks between tests (S002)
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

            // The game name is the baked stitched logo (product name, not localized, docs/ui-language.md); the case
            // name under it is the localized title text.
            Assert.IsNotNull(game.Ui.Logo, "baked logo sprite missing from Resources/UI");
            Assert.IsNotNull(game.Ui.Logo.sprite);
            Assert.IsTrue(game.Ui.Logo.gameObject.activeInHierarchy);
            var caseName = game.Ui.CaseName.GetComponent<LocalizedText>();
            Assert.IsNotNull(caseName, "case name must be bound to a localization key");
            Assert.AreEqual(LocalizationKeys.TitleCaseName, caseName.Key);
            Assert.AreEqual("Three Knocks on Lantern Lane", game.Ui.CaseName.text);
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
            game.Pip.Teleport(new Vector3(40f, 2f, 40f)); // outside the walls and the lawn ring: no ground
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

        /// <summary>URP's own shaders, or the project's two URP sky shaders (written for URP, see MagicSky).</summary>
        private static bool IsUrpShader(string shaderName) =>
            shaderName.StartsWith("Universal Render Pipeline/", System.StringComparison.Ordinal) ||
            shaderName == SpookyStreet.World.MagicSky.DomeShaderName ||
            shaderName == SpookyStreet.World.MagicSky.PaperShaderName;

        /// <summary>
        /// Machine check from the visual floor: no renderer may use Unity's built-in Default-Material or a
        /// non-URP shader. Under URP those render as the magenta error shader on device (TestFlight 3.1).
        /// </summary>
        [UnityTest]
        public IEnumerator EveryRendererUsesAUrpMaterial()
        {
            yield return LoadGame();
            GameBootstrap.Instance.Ui.NewGameButton.onClick.Invoke();
            yield return null;

            var offenders = new System.Collections.Generic.List<string>();
            var renderers = Object.FindObjectsByType<Renderer>(FindObjectsInactive.Include, FindObjectsSortMode.None);
            Assert.Greater(renderers.Length, 5, "expected the greybox world and Pip to exist");
            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    string problem = null;
                    if (material == null)
                    {
                        problem = "missing material";
                    }
                    else if (material.name.StartsWith("Default-Material", System.StringComparison.Ordinal))
                    {
                        problem = "built-in Default-Material";
                    }
                    else if (material.shader == null || !IsUrpShader(material.shader.name))
                    {
                        problem = "non-URP shader " + (material.shader == null ? "null" : material.shader.name);
                    }

                    if (problem != null)
                    {
                        offenders.Add(renderer.name + ": " + problem);
                    }
                }
            }

            Assert.IsEmpty(offenders, "Renderers that would draw magenta on device:\n" + string.Join("\n", offenders));
        }

        [UnityTest]
        public IEnumerator PseudoLanguageSwapsVisibleTitle()
        {
            yield return LoadGame();
            var game = GameBootstrap.Instance;
            Localizer.TogglePseudo();
            yield return null;
            Text title = game.Ui.CaseName;
            Assert.IsNotNull(title);
            StringAssert.StartsWith("[", title.text);
            Localizer.TogglePseudo();
            Assert.AreEqual("Three Knocks on Lantern Lane", title.text);
        }

        /// <summary>
        /// Pip is the original rigged model when it is committed (animated, child-sized, feet on the ground) and the
        /// primitive avatar otherwise. Holds in both states so the model can land without changing this test.
        /// </summary>
        [UnityTest]
        public IEnumerator PipAvatarMatchesModelAvailability()
        {
            yield return LoadGame();
            var game = GameBootstrap.Instance;
            var avatar = game.Pip.GetComponent<PipAvatar>();
            bool modelCommitted = Resources.Load<GameObject>(PipAvatar.ModelPath) != null;
            Assert.AreEqual(modelCommitted, avatar.UsesModel, "model presence and avatar choice disagree");

            Bounds bounds = ModelKit.RendererBounds(avatar.Body.gameObject);
            Assert.That(bounds.size.y, Is.InRange(0.9f, 1.8f), "Pip's height is off (antennae included)");
            Assert.That(bounds.min.y - game.Pip.transform.position.y, Is.InRange(-0.1f, 0.1f),
                "Pip's feet must sit on the capsule base");

            if (!modelCommitted)
            {
                Assert.IsNull(avatar.Animator);
                Assert.IsNotNull(avatar.Body.Find("Hood"), "primitive avatar missing");
                yield break;
            }

            Assert.IsNull(avatar.Body.Find("Hood"), "primitive avatar must not be built under the model");
            Assert.IsNotNull(avatar.Animator, "Pip model needs at least its Idle clip");
            Assert.IsTrue(avatar.Animator.IsReady, "Pip's animation graph did not start");
            Assert.IsTrue(avatar.Animator.HasClip(PipClips.Idle));
            foreach (var renderer in avatar.Body.GetComponentsInChildren<Renderer>())
            {
                var material = renderer.sharedMaterial;
                Assert.IsNotNull(material, $"{renderer.name} has no material");
                Assert.IsTrue(material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null,
                    $"{renderer.name} must use Pip's baked texture, not a flat colour ({material.name})");
            }

            // Walk: locomotion pose with the walk/run clips carrying weight. Then jump: an air pose.
            game.Ui.NewGameButton.onClick.Invoke();
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            Press(_keyboard.wKey);
            float until = Time.time + 0.6f;
            while (Time.time < until)
            {
                yield return null;
            }

            Assert.AreEqual(PipPose.Locomotion, avatar.Animator.Pose);
            Assert.Less(avatar.Animator.WeightOf(PipClips.Idle), 0.5f, "moving Pip should not look idle");

            Press(_keyboard.spaceKey);
            yield return null;
            Release(_keyboard.spaceKey);
            until = Time.time + 0.25f;
            while (Time.time < until)
            {
                yield return null;
            }

            Release(_keyboard.wKey);
            Assert.That(avatar.Animator.Pose,
                Is.EqualTo(PipPose.JumpStart).Or.EqualTo(PipPose.JumpAir).Or.EqualTo(PipPose.Fall),
                "a jump should show an air pose");
            Assert.IsFalse(game.Pip.Grounded);
        }
    }
}
