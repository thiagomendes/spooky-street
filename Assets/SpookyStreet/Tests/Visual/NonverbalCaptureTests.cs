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
    /// trail from the arrival porch (27), Bea's vignette bubble mid-animation (24), her memory replay mid-ghost (25),
    /// the notebook page with its comic playing (26). The first-launch picture cards (28) were retired with D23.1
    /// (one hybrid reading mode).
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
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("No graphics device (-nographics); visual captures need rendering.");
            }
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

            // 27: the knocks are heard, so the goal is Mr. Vale's bench (off to the right): the radio at tier 2 flies
            // the paper-bat trail there from the arrival porch, mid-wave, with the goal sticker up.
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
            Assert.IsTrue(layer.Guide.BeaconVisible, "27_bat_trail: the goal sticker should be up");
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
            if (brain != null)
            {
                brain.enabled = true;
            }

            // 25: dismiss the line: the first hearing replays her memory in the world (letterbox, lilac, June's ghost).
            director.Dialogue.Advance();
            yield return null;
            Assert.IsTrue(layer.MemoryReplay.Active, "25_memory_replay: the replay should start");
            yield return Wait(1.1f);
            Assert.IsTrue(layer.MemoryReplay.Ghost.gameObject.activeSelf, "25_memory_replay: the ghost should be out");
            yield return Shot("25_memory_replay");
            yield return Wait(2f);

            // 26: a notebook page with a proven answer: the comic plays (June on the bridge, the ring on her panel).
            director.Perform(CaseAction.Of(CaseActionKind.HearKnocks));
            director.Perform(CaseAction.Talk(Speaker.Vale));
            director.Dialogue.Skip();
            director.Perform(CaseAction.Of(CaseActionKind.ExamineStreamer));
            director.Dialogue.Skip();
            director.Perform(CaseAction.Of(CaseActionKind.EnterAttic));
            director.Dialogue.Skip();
            director.Captions.Clear();
            yield return Wait(0.3f);
            layer.MemoryReplay.End(false);
            director.OpenNotebook();
            yield return Wait(1.2f); // stickers wiggle in
            Assert.IsTrue(director.Notebook.PlayStrip(0), "26_notebook_page: the entry comic should be playable");
            yield return Wait(1.15f);
            yield return Shot("26_notebook_page");
            director.Notebook.Close();
        }
    }
}
