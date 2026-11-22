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
    /// S002 interior frames with house v3 (D24.1): the treehouse route (18_treehouse_route), June in the attic of the
    /// separate interior (19_attic_june), the hall and the open door (23_hall_door), the stair (29_stair), the crank
    /// cutaway with the owl inset (20_workshop_cutaway), the page-turn halfway over the hall on the way out
    /// (31_threshold_page), the exterior with its lit window cards (30_exterior_cards), the treehouse cabin from inside
    /// (32_cabin_inside) and the finale from the arrival sightline (21_finale).
    /// </summary>
    [Category("Visual")]
    public class InteriorCaptureTests : InputTestFixture
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

        public override void TearDown()
        {
            Time.timeScale = 1f;
            base.TearDown();
        }

        private static IEnumerator Shot(string name, Canvas canvas = null)
        {
            var game = GameBootstrap.Instance;
            yield return FrameCapture.Capture(game.CameraRig.OutputCamera, canvas != null ? canvas : game.Director.UiCanvas,
                name, Width, Height);
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

        /// <summary>Walks Pip into a threshold's exit zone from just outside it and waits for the page to go.</summary>
        private static IEnumerator Cross(Vector3 approach, Vector3 inZone)
        {
            var game = GameBootstrap.Instance;
            var travel = game.Director.Travel;
            int before = travel.Crossings;
            game.Pip.Teleport(approach);
            yield return Wait(0.8f);
            game.Pip.Teleport(inZone);
            float until = Time.time + 3f;
            while ((travel.Active || travel.Crossings == before) && Time.time < until)
            {
                yield return null;
            }

            Assert.AreEqual(before + 1, travel.Crossings, "a crossing at " + inZone);
            yield return Wait(0.3f);
        }

        private static Vector3 InCabin(float cx, float cy)
        {
            CaseRouteLayout.CabinToWorld(CaseRouteLayout.CabinPlatX + cx, CaseRouteLayout.CabinPlatY + cy, out float x,
                out float z);
            return new Vector3(x, CaseRouteLayout.TreehouseY + 0.05f, z);
        }

        [UnityTest]
        [Timeout(300000)]
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
            game.Pip.Teleport(new Vector3(-12.45f, CaseRouteLayout.RampLandingY + 0.05f, 15.6f));
            yield return Wait(1.0f);
            brain.enabled = false;
            camera.transform.position = new Vector3(-16.5f, 6.2f, 3.6f);
            camera.transform.LookAt(new Vector3(-7.6f, 2.2f, 13.2f));
            yield return Shot("18_treehouse_route");
            brain.enabled = true;

            // 19: in by June's window (the page turns), then near June on her stool (gameplay camera).
            yield return Cross(new Vector3(-4.3f, 2.55f, CaseRouteLayout.ExteriorWindowZ),
                new Vector3(-3.36f, 2.55f, CaseRouteLayout.ExteriorWindowZ));
            Assert.IsTrue(director.State.Has(CaseFact.EnteredByWindow), "19: entered by the window");
            Assert.IsTrue(director.House.InteriorShown, "19: the interior is drawn");
            var june = CaseStage.JuneAttic.Position;
            var pipAt = new Vector3(-0.3f, CaseRouteLayout.LoftY + 0.05f, Ifz + 3.6f);
            game.Pip.Teleport(pipAt);
            // Look a little right of June, so she sits left of Pip in the shoulder view.
            Face(pipAt, new Vector3(june.X + 0.5f, june.Y, june.Z - 0.6f));
            yield return Wait(2.2f);
            Debug.Log("[InteriorCaptureTests] 19_attic_june: " + director.Framing.Shot);
            yield return Shot("19_attic_june");

            // 23: the latch lifted and both leaves open, seen from the hall toward the porch.
            director.Perform(CaseAction.Talk(Speaker.June));
            director.Dialogue.Skip();
            yield return Wait(0.2f);
            var hall = new Vector3(0f, 0.65f, Ifz + 2.4f);
            game.Pip.Teleport(hall);
            director.Perform(CaseAction.Of(CaseActionKind.LiftLatch));
            director.Dialogue.Skip();
            Face(hall, hall + Vector3.back);
            yield return Wait(2.0f);
            Debug.Log("[InteriorCaptureTests] 23_hall_door: " + director.Framing.Shot);
            yield return Shot("23_hall_door");

            // 29: halfway up the lower flight, going up along the west wall.
            float stairZ = Ifz + 4.2f;
            var stairAt = new Vector3(-2.85f, CaseRouteLayout.LowerFlightAt(stairZ) + 0.05f, stairZ);
            game.Pip.Teleport(stairAt);
            Face(stairAt, stairAt + Vector3.forward);
            yield return Wait(2.0f);
            Debug.Log("[InteriorCaptureTests] 29_stair: " + director.Framing.Shot);
            yield return Shot("29_stair");

            // 20: the crank cutaway: cam, follower and linkage in the nook, the owl bowing in the inset.
            var crankStand = new Vector3(CaseRouteLayout.NookX - 0.7f, 0.65f, CaseRouteLayout.MechanismZ + 0.1f);
            game.Pip.Teleport(crankStand);
            Face(crankStand, crankStand + Vector3.right);
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

            // 31: out through the open front door: the page halfway over the hall.
            var travel = director.Travel;
            game.Pip.Teleport(new Vector3(0f, 0.65f, Ifz + 1.2f));
            Face(new Vector3(0f, 0.65f, Ifz + 1.2f), new Vector3(0f, 0.65f, Ifz));
            yield return Wait(1.2f);
            game.Pip.Teleport(new Vector3(0f, 0.65f, Ifz + 0.05f));
            float until = Time.time + 2f;
            while (!(travel.Active && travel.Wipe.Phase == WipePhase.Covering && travel.Wipe.Coverage >= 0.3f) &&
                   Time.time < until)
            {
                yield return null;
            }

            Assert.IsTrue(travel.Active, "31: the page turns at the door");
            Time.timeScale = 0f;
            yield return Shot("31_threshold_page", travel.View.Canvas);
            Time.timeScale = 1f;
            until = Time.time + 2f;
            while (travel.Active && Time.time < until)
            {
                yield return null;
            }

            yield return Wait(0.5f);
            Assert.IsFalse(CaseRouteLayout.Inside(game.Pip.transform.position.x, game.Pip.transform.position.z),
                "31: out on the porch");

            // 30: the exterior from the front yard: same street scale, the window cards lit, the attic window brightest.
            brain.enabled = false;
            camera.transform.position = new Vector3(5.6f, 2.3f, 1.6f);
            camera.transform.LookAt(new Vector3(-0.4f, 3.0f, 12.0f));
            director.Captions.Clear();
            yield return Shot("30_exterior_cards");
            brain.enabled = true;

            // 32: inside the treehouse cabin, looking toward the lookout bay (the short cabin boom).
            var cabinAt = InCabin(-0.45f, 0.5f);
            game.Pip.Teleport(cabinAt);
            Face(cabinAt, InCabin(-0.45f, 1.5f));
            yield return Wait(2.2f);
            Debug.Log("[InteriorCaptureTests] 32_cabin_inside: " + director.Framing.Shot);
            Assert.IsTrue(director.Framing.Active, "32: the cabin camera has the room");
            director.Captions.Clear();
            yield return Shot("32_cabin_inside");

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
