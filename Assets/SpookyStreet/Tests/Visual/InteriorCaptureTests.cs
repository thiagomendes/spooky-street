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
    /// S002 interior frames: the treehouse route from the side yard (18_treehouse_route), June in the attic from the
    /// closer shoulder view (19_attic_june), the crank cutaway with the owl inset (20_workshop_cutaway), the finale
    /// from the arrival sightline (21_finale), the open front door from the foyer (23_foyer_door), and the stair (24_stair).
    /// </summary>
    [Category("Visual")]
    public class InteriorCaptureTests : InputTestFixture
    {
        private const int Width = 1278;
        private const int Height = 590;
        private const float MaxMagenta = 0.005f;

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

        private static Behaviour Brain(Camera camera) => camera.GetComponent("CinemachineBrain") as Behaviour;

        private static void Face(Vector3 from, Vector3 to)
        {
            var game = GameBootstrap.Instance;
            Vector3 d = to - from;
            d.y = 0f;
            game.Pip.transform.rotation = Quaternion.LookRotation(d.normalized, Vector3.up);
            game.CameraRig.Orbit.RequestRecenter();
        }

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator CaptureInteriorFrames()
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
            director.Perform(CaseAction.Talk(Speaker.Bea));
            director.Dialogue.Skip();
            yield return Wait(0.3f);
            var camera = game.CameraRig.OutputCamera;
            var brain = Brain(camera);

            // 18: the treehouse route from the side yard, Pip on the ramp landing.
            game.Pip.Teleport(new Vector3(-12.45f, CaseRouteLayout.LandingY + 0.05f, 15.6f));
            yield return Wait(1.0f);
            brain.enabled = false;
            camera.transform.position = new Vector3(-16.5f, 6.2f, 3.6f);
            camera.transform.LookAt(new Vector3(-7.6f, 2.2f, 13.2f));
            yield return Shot("18_treehouse_route");
            brain.enabled = true;

            // 19: in by the window, June on her stool, the closer shoulder view (gameplay camera).
            game.Pip.Teleport(new Vector3(-3.6f, 2.55f, CaseRouteLayout.BridgeZ));
            yield return Wait(0.4f);
            var pipAt = new Vector3(-2.35f, 2.55f, 13.85f);
            game.Pip.Teleport(pipAt);
            // Look a little right of June, so she sits left of Pip in the shoulder view.
            var june = CaseStage.JuneAttic.Position;
            Face(pipAt, new Vector3(june.X + 1.1f, june.Y, june.Z - 0.45f));
            yield return Wait(2.2f);
            Assert.IsTrue(director.State.Has(CaseFact.EnteredByWindow), "19: entered by the window");
            Assert.IsTrue(director.House.View.ShowInterior, "19: the interior shows");
            yield return Shot("19_attic_june");

            // 23: the latch lifted and both leaves open, seen from the foyer toward the porch.
            director.Perform(CaseAction.Talk(Speaker.June));
            director.Dialogue.Skip();
            game.Pip.Teleport(new Vector3(0.1f, 0.65f, 13.2f));
            director.Perform(CaseAction.Of(CaseActionKind.LiftLatch));
            director.Dialogue.Skip();
            Face(new Vector3(0.1f, 0.65f, 13.2f), new Vector3(0.05f, 0.65f, 10f));
            yield return Wait(2.0f);
            yield return Shot("23_foyer_door");

            // 24: halfway up the stair, the camera looking down over the balustrade from the stairwell.
            var stairAt = new Vector3(-0.5f, InteriorCamera.StairSurface(-0.5f) + 0.05f,
                (CaseRouteLayout.StairMinZ + CaseRouteLayout.StairMaxZ) * 0.5f);
            game.Pip.Teleport(stairAt);
            Face(stairAt, stairAt + Vector3.right);
            yield return Wait(2.0f);
            Debug.Log("[InteriorCaptureTests] 24_stair: " + director.Framing.Shot);
            yield return Shot("24_stair");

            // 20: the crank cutaway: cam, follower and linkage in the nook, the owl bowing in the inset.
            var crankStand = new Vector3(0.75f, 0.65f, 14.2f);
            game.Pip.Teleport(crankStand);
            Face(crankStand, new Vector3(1.4f, 0.65f, 14.15f));
            director.Perform(CaseAction.Of(CaseActionKind.ExamineGrille));
            director.Perform(CaseAction.Of(CaseActionKind.LinkArm));
            yield return Wait(MechanismMotion.CycleSeconds + 0.3f);
            director.Perform(CaseAction.Of(CaseActionKind.TurnCrank));
            Assert.IsTrue(director.Cutaway.Active, "20: the cutaway plays");
            yield return Wait(MechanismMotion.KnockTimes[1] + 0.12f);
            director.Cutaway.RenderInset();
            yield return Shot("20_workshop_cutaway");
            director.Cutaway.Skip();
            yield return Wait(0.5f);

            // 21: the finale from the arrival sightline: Kit at her booth, June beside her, the reopening lines.
            game.Pip.Teleport(new Vector3(0f, 0.7f, -13.4f));
            Face(new Vector3(0f, 0.7f, -13.4f), new Vector3(0f, 0.7f, 0f));
            yield return Wait(1.5f);
            Assert.IsTrue(director.State.Has(CaseFact.CaseClosed), "21: case closed");
            Assert.IsTrue(director.FinalePlaying, "21: the finale framing holds");
            yield return Shot("21_finale");
            director.Dialogue.Skip();
        }
    }
}
