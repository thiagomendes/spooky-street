using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
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
            CaseDirector.UseTemporarySaveDirectory(); // no save leaks between tests (S002)
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

            // Close-up from Pip's front-right for model and silhouette review (primitive or original model):
            // park the camera brain, frame Pip, then restore. Pip faces its +Z after walking forward.
            var camera = game.CameraRig.OutputCamera;
            var brain = camera.GetComponent("CinemachineBrain") as Behaviour;
            if (brain != null)
            {
                brain.enabled = false;
            }

            var pip = game.Pip.transform;
            Vector3 target = pip.position + Vector3.up * 0.65f;
            camera.transform.position = target + pip.forward * 2.2f + pip.right * 0.7f + Vector3.up * 0.3f;
            camera.transform.LookAt(target);
            game.Ui.Hud.SetActive(false);
            yield return Shot("05_pip_closeup");

            // Key frame camera (docs/evidence/s000.2/env_keyframe_v2.png): a child's eye height (1.05 m) on the
            // arrival porch, looking up the path past the candy bowl (foreground left) to the owl house (right of
            // centre), as in the key frame. Pip is a few metres up the path after the walk above.
            float fieldOfView = camera.fieldOfView;
            camera.fieldOfView = 40f;
            camera.transform.position = new Vector3(-0.4f, 0.6f + 1.05f, -15f);
            camera.transform.LookAt(new Vector3(-2.5f, 3.4f, 8f));
            yield return Shot("06_keyframe_view");

            // Owl house from the plaza, front-left: house facing, glows, owl, wind wheel, gate and fence.
            camera.fieldOfView = 50f;
            camera.transform.position = new Vector3(-3.2f, 1.8f, -0.5f);
            camera.transform.LookAt(new Vector3(0f, 3f, 11f));
            yield return Shot("07_owl_house");

            // Witnesses (S000.5): Bea with her bicycle west of the plaza, from her front-right (her bike side), and
            // Mr. Vale on the bench from the front. Skipped without their models.
            var cast = game.Witnesses;
            if (cast != null && cast.Bea != null)
            {
                var bea = cast.Bea.transform;
                camera.fieldOfView = 45f;
                camera.transform.position = bea.position + bea.forward * 2.6f + bea.right * 1.5f + Vector3.up * 1.25f;
                camera.transform.LookAt(bea.position + bea.right * 0.4f - bea.forward * 0.1f + Vector3.up * 0.7f);
                yield return Shot("08_bea");
            }

            if (cast != null && cast.Vale != null)
            {
                var vale = cast.Vale.transform;
                camera.fieldOfView = 45f;
                camera.transform.position = vale.position + vale.forward * 2.7f + vale.right * 0.9f + Vector3.up * 1.2f;
                camera.transform.LookAt(vale.position + vale.forward * 0.2f + Vector3.up * 0.65f);
                yield return Shot("09_vale");
            }

            // Bat-cape glide (moveset, spec §5 v1.2): Pip dropped into open sky above the plaza, Jump held so the
            // cape spreads, then frozen mid-glide (time scale 0) and seen from behind-left toward the owl house.
            Vector3 parkedAt = pip.position;
            Quaternion parkedRotation = pip.rotation;
            game.Pip.Teleport(new Vector3(0f, 5.5f, -0.5f));
            pip.rotation = Quaternion.identity;
            until = Time.time + 0.25f;
            while (Time.time < until)
            {
                yield return null;
            }

            Press(_keyboard.spaceKey, queueEventOnly: true);
            until = Time.time + 0.5f;
            while (Time.time < until)
            {
                yield return null;
            }

            Assert.IsTrue(game.Pip.Gliding, "10_glide: Pip should be gliding");
            Time.timeScale = 0f;
            Vector3 body = pip.position + Vector3.up * 0.6f;
            camera.fieldOfView = 45f;
            camera.transform.position = body - pip.forward * 2.6f - pip.right * 1.5f - Vector3.up * 0.35f;
            camera.transform.LookAt(body + pip.forward * 0.4f);
            yield return Shot("10_glide");
            Time.timeScale = 1f;
            Release(_keyboard.spaceKey, queueEventOnly: true);
            yield return null;
            game.Pip.Teleport(parkedAt);
            pip.rotation = parkedRotation;

            camera.fieldOfView = fieldOfView;
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
