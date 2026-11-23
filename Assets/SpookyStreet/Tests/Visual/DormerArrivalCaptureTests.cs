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
using Lines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;

namespace SpookyStreet.Tests.Visual
{
    /// <summary>
    /// TestFlight 23 ("right after entering through the window, I got stuck") and the S002 attic framing that followed:
    /// the attic right after a dormer arrival with the gameplay lens (37_dormer_arrival: Pip and the room, no wall
    /// filling the frame, and no street knock caption carried in through the window); the owner's frame, Pip by the
    /// trunk with the lens up to the east looking west, as it rendered before the sky fix with the sky dome drawn
    /// (38_tf23_sky_shown: the dome's sphere cuts the attic as a flat purple wall); and Pip in that same place, facing
    /// west, with the gameplay lens as the game frames her now (39_tf23_sky_hidden).
    /// </summary>
    [Category("Visual")]
    public class DormerArrivalCaptureTests : InputTestFixture
    {
        private const int Width = 1278;
        private const int Height = 590;
        private const float MaxMagenta = 0.005f;
        private const float Ifz = CaseRouteLayout.InteriorFrontZ;

        /// <summary>A frame whose walls beside the lens cover more than this is not Pip and the room (D24).</summary>
        private const float MaxFrameCover = 0.15f;

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

        /// <summary>The Core model's share of the frame filled by a wall beside the gameplay lens (logged and bounded).</summary>
        private static float GameplayCover(string name)
        {
            var game = GameBootstrap.Instance;
            var cam = game.CameraRig.OutputCamera.transform;
            Vector3 lens = cam.position;
            float pitch = Mathf.DeltaAngle(0f, cam.eulerAngles.x);
            float cover = InteriorCamera.FrameCover(lens.x, lens.y, lens.z, cam.eulerAngles.y, pitch,
                game.Director.House.DoorOpen);
            Debug.Log($"[DormerArrivalCaptureTests] {name}: {game.Director.Framing.Shot}, lens {lens:F2}, " +
                      $"yaw {cam.eulerAngles.y:F0} pitch {pitch:F0}, frame cover {cover:P0}, " +
                      $"sky hidden {game.Sky != null && game.Sky.Hidden}, caption {game.Director.Captions.CurrentKey ?? "none"}");
            Assert.IsTrue(InteriorSafety.InsideShell(lens.x, lens.y, lens.z), $"{name}: the lens is inside the shell");
            return cover;
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
            Assert.AreNotEqual(Lines.CaptionArrivalKnocks, director.Captions.CurrentKey,
                "37: the street's knocks are not captioned inside the blue house");
            float cover37 = GameplayCover("37_dormer_arrival");
            yield return Shot("37_dormer_arrival");
            Assert.LessOrEqual(cover37, MaxFrameCover, "37: no wall fills the arrival frame");

            // 38: the owner's frame as it rendered before the sky fix. Pip by the trunk at the front wall; the lens up
            // to the east, looking west, inside the dome's sphere (the attic beyond depth ~1.4 is outside it).
            var pip = new Vector3(-1.5f, CaseRouteLayout.LoftY + 0.03f, Ifz + 1.15f);
            game.Pip.Teleport(pip);
            game.Pip.transform.rotation = Quaternion.Euler(0f, -90f, 0f);
            director.CutCamera();
            yield return Wait(0.6f);
            var camera = game.CameraRig.OutputCamera;
            var brain = camera.GetComponent("CinemachineBrain") as Behaviour;
            var tf23Lens = new Vector3(0.16f, 6.3f, Ifz + 0.86f);
            Assert.IsTrue(SkyModel.InsideDome(tf23Lens.x, tf23Lens.y, tf23Lens.z), "the owner's lens is inside the dome");
            if (game.Sky != null)
            {
                brain.enabled = false;
                director.enabled = false; // hold the sky as set below while the frame renders
                camera.transform.position = tf23Lens;
                camera.transform.LookAt(pip + Vector3.up * 0.9f);
                game.Sky.Hidden = false;
                yield return Shot("38_tf23_sky_shown");
                game.Sky.Hidden = true;
                director.enabled = true;
                brain.enabled = true;
            }

            // 39: Pip in the owner's place, facing west, framed by the gameplay lens as the game does now (the sky
            // hidden, the lens north of her: the front gable no longer smeared across the frame).
            director.CutCamera();
            yield return Wait(0.8f);
            Assert.IsTrue(game.Sky == null || game.Sky.Hidden, "the director keeps the sky hidden indoors");
            float cover39 = GameplayCover("39_tf23_sky_hidden");
            yield return Shot("39_tf23_sky_hidden");
            Assert.LessOrEqual(cover39, MaxFrameCover, "39: no wall fills the frame");
        }
    }
}
