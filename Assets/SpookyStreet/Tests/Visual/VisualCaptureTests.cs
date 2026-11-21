using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Core.Flow;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SpookyStreet.Tests.Visual
{
    /// <summary>
    /// Renders the key S000 frames at an iPhone landscape aspect so a reviewer (human or agent) can see
    /// what ships. Requires a graphics device; CI runs this assembly without -nographics. The frames are
    /// evidence for review, not a substitute for the on-device check.
    /// </summary>
    [Category("Visual")]
    public class VisualCaptureTests : InputTestFixture
    {
        // iPhone 15/16 landscape is 2556 × 1179; half resolution keeps logs small with the same aspect.
        private const int Width = 1278;
        private const int Height = 590;
        private const float MaxMagenta = 0.005f;

        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("No graphics device (-nographics); visual captures need rendering.");
            }
        }

        private static IEnumerator Shot(string name)
        {
            var game = GameBootstrap.Instance;
            yield return FrameCapture.Capture(game.CameraRig.OutputCamera, game.Ui.GetComponent<Canvas>(), name, Width,
                Height);
            float magenta = FrameCapture.MagentaFraction(FrameCapture.LastFrame);
            Assert.LessOrEqual(magenta, MaxMagenta,
                $"{name}: {magenta:P1} of the frame is error-shader magenta (missing or non-URP material)");
            Assert.Greater(FrameCapture.MeanLuminance(FrameCapture.LastFrame), 0.02f, $"{name}: frame is black");
        }

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator CaptureKeyFrames()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            var game = GameBootstrap.Instance;
            Assert.IsNotNull(game);
            Debug.Log($"[Visual] graphics={SystemInfo.graphicsDeviceType} {SystemInfo.graphicsDeviceName} " +
                      $"pipeline={GraphicsSettings.currentRenderPipeline?.name}");

            yield return Shot("01_title");

            game.Ui.NewGameButton.onClick.Invoke();
            for (int i = 0; i < 30; i++)
            {
                yield return null;
            }

            yield return Shot("02_play_start");

            Press(_keyboard.wKey);
            float until = Time.time + 1.5f;
            while (Time.time < until)
            {
                yield return null;
            }

            Release(_keyboard.wKey);
            yield return null;
            yield return Shot("03_after_walking");

            // Close-up from the front for silhouette review: park the camera brain, frame Pip, then restore.
            var camera = game.CameraRig.OutputCamera;
            var brain = camera.GetComponent("CinemachineBrain") as Behaviour;
            if (brain != null)
            {
                brain.enabled = false;
            }

            var pip = game.Pip.transform;
            Vector3 target = pip.position + Vector3.up * 0.6f;
            camera.transform.position = target + pip.forward * 2.3f + pip.right * 0.6f + Vector3.up * 0.35f;
            camera.transform.LookAt(target);
            game.Ui.Hud.SetActive(false);
            yield return Shot("05_pip_closeup");
            game.Ui.Hud.SetActive(true);
            if (brain != null)
            {
                brain.enabled = true;
            }

            yield return null;

            Press(_keyboard.escapeKey, queueEventOnly: true);
            yield return null;
            Release(_keyboard.escapeKey, queueEventOnly: true);
            yield return null;
            Assert.AreEqual(GameState.Paused, game.Flow.State);
            yield return Shot("04_paused");
        }
    }
}
