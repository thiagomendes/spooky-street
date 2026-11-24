using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.Case;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SpookyStreet.Tests.Visual
{
    /// <summary>
    /// Non-verbal UX v2 frames (D23, docs/nonverbal-ux.md, mocks in docs/evidence/s002/nonverbal_v2): the tier-2 bat
    /// trail from the arrival porch (27) and Bea's vignette bubble mid-animation (24: since D26, only the silver flash she
    /// saw). The S002 hint frames (33 sparkle, 34 and 36 Use demos) moved to ClueHintCaptureTests with the S002.5b
    /// retiering (34 and 36 now show the move demo at the crate; 51 and 52 the area breeze and bats). The memory replay (25) and notebook page (26) frames were retired by S002.5a. The first-launch picture cards (28) were retired with D23.1
    /// (one hybrid reading mode). 79 (S004 stream G) is Bea's surprised portrait on the same bubble, at the phone
    /// aspect and at iPad 4:3.
    /// </summary>
    [Category("Visual")]
    public class NonverbalCaptureTests : InputTestFixture
    {
        private const int Width = 1278;
        private const int Height = 590;
        private const float MaxMagenta = 0.005f;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            CaseDirector.UseTemporarySaveDirectory();
            SpookyStreet.Controls.ControlSchemeTracker.Override = null;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("No graphics device (-nographics); visual captures need rendering.");
            }
        }

        private static IEnumerator Shot(string name, Canvas canvas = null, int width = Width, int height = Height)
        {
            var game = GameBootstrap.Instance;
            yield return FrameCapture.Capture(game.CameraRig.OutputCamera, canvas != null ? canvas : game.Director.UiCanvas,
                name, width, height);
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

        private static IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }
        }

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator CaptureNonverbalFrames()
        {
            yield return Load();
            var game = GameBootstrap.Instance;
            var director = game.Director;
            var layer = director.Nonverbal;
            game.Ui.NewGameButton.onClick.Invoke();
            game.CameraRig.Orbit.RequestRecenter();
            yield return Wait(1.5f);

            // 27: the knocks are heard, so the hint area is the plaza corner by Mr. Vale's bench (off to the right): the
            // radio at tier 2 flies the paper-bat trail there from the arrival porch; the first bats circle the area
            // (S002.5b: no goal sticker).
            director.Perform(CaseAction.Of(CaseActionKind.HearKnocks));
            yield return Wait(0.2f);
            director.Captions.Clear();
            layer.RadioButton.onClick.Invoke();
            layer.RadioButton.onClick.Invoke();
            director.Captions.Clear();
            Assert.IsTrue(layer.Guide.Active, "27_bat_trail: tier 2 should start the trail");
            Assert.AreEqual(CaseObjective.CheckView, layer.Guide.Objective, "27_bat_trail: the bench is the goal");
            yield return Wait(1.85f);
            Assert.Greater(layer.Guide.VisibleBats, 0, "27_bat_trail: bats should be flying");
            Assert.Greater(layer.Guide.CirclingBats, 0, "27_bat_trail: the first bats should be circling the area");
            yield return Shot("27_bat_trail");
            layer.Guide.Stop();
            director.Captions.Clear();

            // 24: Bea's bubble, framed like 14_dialogue, on the beat where her hand points and the ring pulses.
            var bea = CaseStage.Find(CaseStage.PointBea).Position;
            var bike = CaseStage.Find(CaseStage.PointBicycle).Position;
            var beaAt = new Vector3(bea.X, 0f, bea.Z);
            Vector3 away = beaAt - new Vector3(bike.X, 0f, bike.Z);
            away = (away.normalized + Vector3.right).normalized;
            game.Pip.Teleport(beaAt + away * 2.4f + Vector3.up * 0.1f);
            game.Pip.transform.rotation = Quaternion.LookRotation(-away, Vector3.up);
            game.CameraRig.Orbit.RequestRecenter();
            yield return Wait(1.5f);

            director.Perform(CaseAction.Talk(Speaker.Bea));
            yield return null;
            Assert.IsTrue(director.Dialogue.ShowsVignette, "24_bea_vignette: Bea's key line should play its vignette");
            var camera = game.CameraRig.OutputCamera;
            var brain = camera.GetComponent("CinemachineBrain") as Behaviour;
            if (brain != null)
            {
                brain.enabled = false;
            }

            var pip = game.Pip.transform;
            camera.transform.position = pip.position + pip.right * 0.9f + Vector3.up * 1.3f - pip.forward * 1.4f;
            camera.transform.LookAt(beaAt + Vector3.up * 1.05f);
            float timeout = Time.realtimeSinceStartup + 20f;
            while (director.Dialogue.Vignette.LocalTime < 2.95f && Time.realtimeSinceStartup < timeout)
            {
                yield return null;
            }

            yield return Shot("24_bea_vignette");

            // 79 (S004 stream G): the same beat shows Bea's surprised portrait in the bubble's face disc, her pumpkin
            // stalk rising over the felt; again at an iPad 4:3 aspect to check the layout and safe area.
            Assert.AreEqual("portrait.bea.surprised", director.Dialogue.PortraitId, "79: BeaSawCape is a surprised line");
            Assert.IsTrue(director.Dialogue.PortraitImage.enabled, "79: the portrait badge shows");
            yield return Shot("79_bea_portrait");
            yield return Shot("79b_bea_portrait_4x3", null, 1024, 768);
            if (brain != null)
            {
                brain.enabled = true;
            }

            // 25 and 26 were retired with D26 (S002.5a): memory replays no longer auto-play, and the notebook page is
            // captured by DeductionCaptureTests (the tray, a wrong-try wobble, the solved comic, the case card).
            director.Dialogue.Advance();
            yield return null;
            Assert.IsFalse(layer.MemoryReplay.Active, "D26: no replay auto-plays after the line");
        }
    }
}
