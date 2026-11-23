using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.World;
using SpookyStreet.Player;
using SpookyStreet.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SpookyStreet.Tests.Visual
{
    /// <summary>
    /// S003 frames: Pip sliding down the tall shed's west side under the treehouse (45_wall_slide) and hanging from the
    /// street lamp's arm (46_ledge_hang), each reached by the real moves (the gamepad stick pushes her into the wall or
    /// at the arm) and frozen mid-move.
    /// </summary>
    [Category("Visual")]
    public class WallLedgeCaptureTests : InputTestFixture
    {
        private const int Width = 1278;
        private const int Height = 590;
        private const float MaxMagenta = 0.005f;

        private Gamepad _pad;

        public override void Setup()
        {
            base.Setup();
            _pad = InputSystem.AddDevice<Gamepad>();
            CaseDirector.UseTemporarySaveDirectory();
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("No graphics device (-nographics); visual captures need rendering.");
            }
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            base.TearDown();
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                yield return null;
            }
        }

        private static IEnumerator StartGame()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            var game = GameBootstrap.Instance;
            if (!game.Environment.UsesModels)
            {
                Assert.Ignore("Environment models are not committed.");
            }

            game.Ui.NewGameButton.onClick.Invoke();
            yield return Wait(0.3f);
            game.Ui.FirstPrompt.SetActive(false);
        }

        private void Steer(Vector3 direction)
        {
            var camera = GameBootstrap.Instance.CameraRig.OutputCamera.transform;
            Vector3 forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            direction.y = 0f;
            direction.Normalize();
            Set(_pad.leftStick, new Vector2(Vector3.Dot(direction, right), Vector3.Dot(direction, forward)));
        }

        private static IEnumerator Capture(string name, Vector3 cameraAt, Vector3 lookAt)
        {
            var game = GameBootstrap.Instance;
            var camera = game.CameraRig.OutputCamera;
            var brain = camera.GetComponent("CinemachineBrain") as Behaviour;
            if (brain != null)
            {
                brain.enabled = false;
            }

            camera.transform.position = cameraAt;
            camera.transform.LookAt(lookAt);
            Time.timeScale = 0f;
            yield return FrameCapture.Capture(camera, game.Ui.GetComponent<Canvas>(), name, Width, Height);
            Time.timeScale = 1f;
            if (brain != null)
            {
                brain.enabled = true;
            }

            float magenta = FrameCapture.MagentaFraction(FrameCapture.LastFrame);
            Assert.LessOrEqual(magenta, MaxMagenta, $"{name}: {magenta:P1} of the frame is magenta");
            Assert.Greater(FrameCapture.MeanLuminance(FrameCapture.LastFrame), 0.02f, $"{name}: frame is black");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator CaptureWallSlide()
        {
            yield return StartGame();
            var pip = GameBootstrap.Instance.Pip;
            // In the air against the shed's west face (x -7.95) in the yard pocket north of the treehouse deck.
            pip.Teleport(new Vector3(CaseRouteLayout.RoofMinX - 0.33f, 1.8f, 14.1f));
            pip.transform.rotation = Quaternion.LookRotation(Vector3.right, Vector3.up);
            float deadline = Time.time + 1f;
            while (!pip.WallSliding && Time.time < deadline)
            {
                Steer(Vector3.right);
                yield return null;
            }

            Assert.IsTrue(pip.WallSliding, "45_wall_slide: Pip should slide down the shed wall");
            yield return Wait(0.35f);
            var probe = pip.LastWallProbe;
            Debug.Log($"[S003] 45_wall_slide: feet {pip.transform.position:F2}, sliding {pip.WallSliding}, " +
                      $"vy {pip.VerticalSpeed:F2}, wall hit {probe.Hit} d {probe.Distance:F2} clearance {probe.FeetClearance:F2}");
            Assert.IsTrue(pip.WallSliding, "45_wall_slide: still sliding");
            Vector3 at = pip.transform.position;
            yield return Capture("45_wall_slide", at + new Vector3(-2.0f, 0.45f, -1.1f), at + new Vector3(0.15f, 0.65f, 0f));
            Set(_pad.leftStick, Vector2.zero);
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator CaptureLedgeHang()
        {
            yield return StartGame();
            var game = GameBootstrap.Instance;
            var pip = game.Pip;
            BoxCollider head = null;
            foreach (var t in game.Environment.Root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == ModelColliders.PartPrefix + "Head" && t.GetComponent<ClimbSurface>() != null)
                {
                    head = t.GetComponent<BoxCollider>();
                }
            }

            Assert.IsNotNull(head, "46_ledge_hang: the street lamp's marked arm");
            Vector3 n = head.transform.forward;
            n.y = 0f;
            n.Normalize();
            Bounds b = head.bounds;
            // Falling past the arm's long side, just inside the catch window.
            Vector3 start = b.center + n * (head.size.z * 0.5f + 0.4f);
            start.y = b.max.y - 1.2f;
            pip.Teleport(start);
            pip.transform.rotation = Quaternion.LookRotation(-n, Vector3.up);
            float deadline = Time.time + 1f;
            while (!pip.Hanging && Time.time < deadline)
            {
                Steer(-n);
                yield return null;
            }

            Set(_pad.leftStick, Vector2.zero);
            Assert.IsTrue(pip.Hanging, "46_ledge_hang: Pip should catch the lamp arm");
            yield return Wait(0.75f);
            Assert.IsTrue(pip.Hanging, "46_ledge_hang: still hanging");
            Vector3 at = pip.transform.position;
            Vector3 side = Vector3.Cross(Vector3.up, n);
            yield return Capture("46_ledge_hang", at + n * 2.4f + side * 1.3f + Vector3.up * 0.9f,
                at + Vector3.up * 1.0f - n * 0.2f);
        }
    }
}
