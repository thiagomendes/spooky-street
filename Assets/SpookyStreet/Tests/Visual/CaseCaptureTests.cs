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
    /// S002 wiring frames: Bea talking on the speech card (14_dialogue) and the notebook spread open with a few notes
    /// found (15_notebook). Same capture path as the S000 frames, with the case UI canvas in the frame.
    /// </summary>
    [Category("Visual")]
    public class CaseCaptureTests : InputTestFixture
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

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator CaptureCaseFrames()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            var game = GameBootstrap.Instance;
            var director = game.Director;
            game.Ui.NewGameButton.onClick.Invoke();
            yield return Wait(0.3f);

            // Pip a couple of metres from Bea on the plaza side, facing her, camera recentred behind Pip.
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
            yield return Wait(0.4f);
            Assert.IsTrue(director.Dialogue.IsOpen, "14_dialogue: Bea's line should be on the card");
            yield return Shot("14_dialogue");
            director.Dialogue.Skip();

            // A few notes found, then the notebook.
            director.Perform(CaseAction.Talk(Speaker.Vale));
            director.Dialogue.Skip();
            director.Perform(CaseAction.Of(CaseActionKind.ExamineStreamer));
            director.Dialogue.Skip();
            yield return Wait(0.5f);
            director.OpenNotebook();
            yield return Wait(0.3f);
            Assert.IsTrue(director.Notebook.IsOpen, "15_notebook: the notebook should be open");
            yield return Shot("15_notebook");
            director.Notebook.Close();
        }
    }
}
