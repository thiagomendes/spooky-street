using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.Keepsakes;
using SpookyStreet.Core.World;
using SpookyStreet.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SpookyStreet.Tests.Visual
{
    /// <summary>
    /// S004 A1 frames (stream A's reserved number 65; 66–68 stay for A2): a paper bat fluttering open by Pip inside the
    /// treehouse cabin (65_bat_treehouse_deck), the folded bat on the street lamp's head seen from the path
    /// (65b_bat_lamp_head), the side-yard ramp bat open beside Pip (65c_bat_side_yard_ramp) and the arrival porch at the
    /// start in the play camera, the rail bat opening (65d_bat_porch_start). Greybox bat art until stream B2's model.
    /// Pickup is switched off so Pip can stand close.
    /// </summary>
    [Category("Visual")]
    public class KeepsakeCaptureTests : InputTestFixture
    {
        private const int Width = 1278;
        private const int Height = 590;
        private const float MaxMagenta = 0.005f;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Gamepad>();
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

        private static PaperBatCollectibles Bats => GameBootstrap.Instance.Director.Keepsakes;

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
            if (game.Director.Cutscenes.Playing)
            {
                game.Director.Cutscenes.Player.SkipNow();
                yield return Wait(0.2f);
            }

            game.Ui.FirstPrompt.SetActive(false);
            Bats.CanCollect = () => false;
        }

        private static Vector3 Cabin(float cx, float cy, float y)
        {
            CaseRouteLayout.CabinToWorld(CaseRouteLayout.CabinPlatX + cx, CaseRouteLayout.CabinPlatY + cy, out float x, out float z);
            return new Vector3(x, y, z);
        }

        private static Vector3 At(int number)
        {
            var spot = PaperBatLayout.All[number - 1];
            return new Vector3(spot.X, spot.Y, spot.Z);
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
            yield return PlainCapture(name);
            if (brain != null)
            {
                brain.enabled = true;
            }
        }

        private static IEnumerator PlainCapture(string name)
        {
            var game = GameBootstrap.Instance;
            Time.timeScale = 0f;
            yield return FrameCapture.Capture(game.CameraRig.OutputCamera, game.Ui.GetComponent<Canvas>(), name, Width, Height);
            Time.timeScale = 1f;
            float magenta = FrameCapture.MagentaFraction(FrameCapture.LastFrame);
            Assert.LessOrEqual(magenta, MaxMagenta, $"{name}: {magenta:P1} of the frame is magenta");
            Assert.Greater(FrameCapture.MeanLuminance(FrameCapture.LastFrame), 0.02f, $"{name}: frame is black");
        }

        private static IEnumerator Place(Vector3 feet, Vector3 face)
        {
            var pip = GameBootstrap.Instance.Pip;
            pip.Teleport(feet);
            face.y = 0f;
            pip.transform.rotation = Quaternion.LookRotation(face.normalized, Vector3.up);
            yield return Wait(0.8f);
            Debug.Log($"[Keepsakes] Pip at {pip.transform.position:F2}, grounded {pip.Grounded}");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator CaptureBatSpots()
        {
            yield return StartGame();

            // 65d first, in the play camera at the start: the rail bat by the spawn opens; the box bat stays folded.
            yield return Wait(0.8f);
            Assert.Greater(Bats.OpenAmount(PaperBatLayout.IdFor(1)), 0.9f, "65d: the rail bat opens as Pip arrives");
            yield return PlainCapture("65d_bat_porch_start");

            // 65: inside the treehouse cabin, the bat under the roof beam beside the door flutters open by Pip.
            Vector3 bat8 = At(8);
            // The maple's trunk stands in the cabin's middle: Pip by the door, the lens in the far corner of the bat's
            // wall, so the trunk stays out of the line.
            yield return Place(Cabin(-0.55f, -0.25f, CaseRouteLayout.TreehouseY + 0.05f), bat8 - Cabin(-0.55f, -0.25f, 0f));
            Assert.Greater(Bats.OpenAmount(PaperBatLayout.IdFor(8)), 0.9f, "65: the cabin bat should be open");
            Vector3 pip = GameBootstrap.Instance.Pip.transform.position;
            yield return Capture("65_bat_treehouse_deck", Cabin(0.8f, 0.9f, CaseRouteLayout.TreehouseY + 1.5f),
                Vector3.Lerp(pip + Vector3.up * 0.7f, bat8, 0.6f));

            // 65b: the bat on the street lamp's head, folded, seen from the path below.
            Vector3 bat21 = At(21);
            yield return Place(new Vector3(1.4f, 0.05f, -7.6f), bat21 - new Vector3(1.4f, 0f, -7.6f));
            Assert.Less(Bats.OpenAmount(PaperBatLayout.IdFor(21)), 0.1f, "65b: out of reach it stays folded");
            yield return Capture("65b_bat_lamp_head", new Vector3(0.2f, 1.9f, -5.6f), bat21 + Vector3.down * 0.6f);

            // 65c: the ramp bat in the side yard, open beside Pip on the ramp, seen from the corridor.
            Vector3 bat7 = At(7);
            yield return Place(new Vector3(-12.45f, 0.4f, 8.7f), bat7 - new Vector3(-12.45f, 0f, 8.7f));
            Assert.Greater(Bats.OpenAmount(PaperBatLayout.IdFor(7)), 0.9f, "65c: the ramp bat should be open");
            // From the ramp's east side, so Pip (facing the bat) does not hide it.
            yield return Capture("65c_bat_side_yard_ramp", new Vector3(-11.0f, 2.1f, 10.0f), bat7 + new Vector3(0.3f, 0f, -0.3f));
            Assert.AreEqual(KeepsakeRules.TotalBats, Bats.Remaining, "captures take nothing");
        }
    }
}
