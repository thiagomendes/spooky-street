using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SpookyStreet.Tests.Visual
{
    /// <summary>
    /// TestFlight 23 ("right after entering through the window, I got stuck"): the attic right after a dormer arrival
    /// with the gameplay lens (37_dormer_arrival); the owner's frame, Pip by the trunk with the lens up to the east
    /// looking west, as it rendered before the fix with the sky dome drawn (38_tf23_sky_shown: the dome's sphere cuts
    /// the attic as a flat purple wall) and as it renders now with the sky hidden indoors (39_tf23_sky_hidden).
    /// </summary>
    [Category("Visual")]
    public class DormerArrivalCaptureTests : InputTestFixture
    {
        private const int Width = 1278;
        private const int Height = 590;
        private const float MaxMagenta = 0.005f;
        private const float Ifz = CaseRouteLayout.InteriorFrontZ;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            CaseDirector.UseTemporarySaveDirectory();
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("No graphics device (-nographics); visual captures need rendering.");
            }
        }

        private static IEnumerator Shot(string name)
        {
            var game = GameBootstrap.Instance;
            yield return FrameCapture.Capture(game.CameraRig.OutputCamera, game.Director.UiCanvas, name, Width, Height);
            float magenta = FrameCapture.MagentaFraction(FrameCapture.LastFrame);
            Assert.LessOrEqual(magenta, MaxMagenta, $"{name}: {magenta:P1} of the frame is error-shader magenta");
            Assert.Greater(FrameCapture.MeanLuminance(FrameCapture.LastFrame), 0.02f, $"{name}: frame is black");
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                yield return null;
            }
        }

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator CaptureDormerArrivalFrames()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            var game = GameBootstrap.Instance;
            var director = game.Director;
            if (director.House == null)
            {
                Assert.Ignore("Environment models are not committed: the interior is not staged.");
            }

            game.Ui.NewGameButton.onClick.Invoke();
            yield return Wait(0.3f);
            director.Perform(CaseAction.Of(CaseActionKind.HearKnocks));
            director.Dialogue.Skip();
            yield return Wait(0.3f);

            // 37: in by June's window, the first moment after the page has gone (gameplay lens).
            var travel = director.Travel;
            int before = travel.Crossings;
            game.Pip.Teleport(new Vector3(-4.3f, 2.55f, CaseRouteLayout.ExteriorWindowZ));
            game.Pip.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            director.CutCamera();
            yield return Wait(0.8f);
            game.Pip.Teleport(new Vector3(-3.36f, 2.55f, CaseRouteLayout.ExteriorWindowZ));
            float until = Time.time + 3f;
            while ((travel.Active || travel.Crossings == before) && Time.time < until)
            {
                yield return null;
            }

            Assert.AreEqual(before + 1, travel.Crossings, "37: in through June's window");
            if (director.Dialogue.IsOpen)
            {
                director.Dialogue.Skip();
            }

            yield return Wait(0.15f);
            Vector3 lens = game.CameraRig.OutputCamera.transform.position;
            Debug.Log($"[DormerArrivalCaptureTests] 37_dormer_arrival: {director.Framing.Shot}, lens {lens:F2}, " +
                      $"sky hidden {game.Sky != null && game.Sky.Hidden}");
            Assert.IsTrue(InteriorSafety.InsideShell(lens.x, lens.y, lens.z), "37: the lens is inside the shell");
            yield return Shot("37_dormer_arrival");

            // 38 / 39: the owner's frame. Pip by the trunk at the front wall; the lens up to the east, looking west,
            // inside the dome's sphere (the attic beyond depth ~1.4 is outside it).
            var pip = new Vector3(-1.5f, CaseRouteLayout.LoftY + 0.03f, Ifz + 1.15f);
            game.Pip.Teleport(pip);
            game.Pip.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            director.CutCamera();
            yield return Wait(0.6f);
            var camera = game.CameraRig.OutputCamera;
            var brain = camera.GetComponent("CinemachineBrain") as Behaviour;
            var tf23Lens = new Vector3(0.16f, 6.3f, Ifz + 0.86f);
            Assert.IsTrue(SkyModel.InsideDome(tf23Lens.x, tf23Lens.y, tf23Lens.z), "the owner's lens is inside the dome");
            brain.enabled = false;
            director.enabled = false; // hold the sky as set below while the frame renders
            camera.transform.position = tf23Lens;
            camera.transform.LookAt(pip + Vector3.up * 0.9f);
            if (game.Sky != null)
            {
                game.Sky.Hidden = false;
                yield return Shot("38_tf23_sky_shown");
                game.Sky.Hidden = true;
            }

            yield return Shot("39_tf23_sky_hidden");
            director.enabled = true;
            brain.enabled = true;
            yield return Wait(0.3f);
            Assert.IsTrue(game.Sky == null || game.Sky.Hidden, "the director keeps the sky hidden indoors");
        }
    }
}
