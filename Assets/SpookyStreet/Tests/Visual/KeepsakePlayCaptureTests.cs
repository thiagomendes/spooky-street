using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.Keepsakes;
using SpookyStreet.Core.World;
using SpookyStreet.Save;
using SpookyStreet.UI;
using SpookyStreet.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SpookyStreet.Tests.Visual
{
    /// <summary>
    /// S004 A2 frames (82–85; 65–68 were taken by S003a and A1, 69–81 are reserved by other S004 streams): the keepsakes
    /// page with page 1 assembling (82b) and assembled beside the bat stack (82), all three pages (82c); the trunk open
    /// with Pip in the star cape and the showman's hat (83); the title with the bat mobile (12 bats) on the porch (84);
    /// a collectible paper bat fluttering open by Pip on the porch, close (85), and the lamp head where it once shared
    /// the air with decorative bats (85b).
    /// </summary>
    [Category("Visual")]
    public class KeepsakePlayCaptureTests : InputTestFixture
    {
        private const int Width = 1278;
        private const int Height = 590;
        private const float MaxMagenta = 0.005f;
        private string _directory;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Gamepad>();
            _directory = CaseDirector.UseTemporarySaveDirectory();
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

        private static CaseDirector Director => GameBootstrap.Instance.Director;

        private void WriteKeepsakes(int count, params CostumePiece[] worn)
        {
            var store = new KeepsakeStore(new DiskSaveFileSystem(), _directory);
            store.Load();
            for (int i = 1; i <= count; i++)
            {
                store.Find(PaperBatLayout.IdFor(i));
            }

            foreach (var piece in worn)
            {
                store.Wear(piece.Id, true);
            }
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
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

            if (!GameBootstrap.Instance.Environment.UsesModels)
            {
                Assert.Ignore("Environment models are not committed.");
            }
        }

        private static IEnumerator Play()
        {
            var game = GameBootstrap.Instance;
            game.Flow.StartNewGame();
            yield return Wait(0.3f);
            if (Director.Cutscenes.Playing)
            {
                Director.Cutscenes.Player.SkipNow();
                yield return Wait(0.2f);
            }

            game.Ui.FirstPrompt.SetActive(false);
            Director.Keepsakes.CanCollect = () => false;
        }

        private static IEnumerator Capture(string name, Canvas canvas)
        {
            var game = GameBootstrap.Instance;
            float scale = Time.timeScale;
            Time.timeScale = 0f;
            yield return FrameCapture.Capture(game.CameraRig.OutputCamera, canvas, name, Width, Height);
            Time.timeScale = scale;
            float magenta = FrameCapture.MagentaFraction(FrameCapture.LastFrame);
            Assert.LessOrEqual(magenta, MaxMagenta, $"{name}: {magenta:P1} of the frame is magenta");
            Assert.Greater(FrameCapture.MeanLuminance(FrameCapture.LastFrame), 0.02f, $"{name}: frame is black");
        }

        private static IEnumerator CaptureFrom(string name, Vector3 cameraAt, Vector3 lookAt)
        {
            var camera = GameBootstrap.Instance.CameraRig.OutputCamera;
            var brain = camera.GetComponent("CinemachineBrain") as Behaviour;
            if (brain != null)
            {
                brain.enabled = false;
            }

            camera.transform.position = cameraAt;
            camera.transform.LookAt(lookAt);
            yield return Capture(name, Director.UiCanvas);
            if (brain != null)
            {
                brain.enabled = true;
            }
        }

        [UnityTest]
        [Timeout(150000)]
        public IEnumerator CaptureKeepsakesPageTrunkAndTitle()
        {
            // 84: the title, with twelve bats on the mobile, the cape and the hat worn.
            WriteKeepsakes(12, CostumePieces.StarCape);
            yield return Load();
            Assert.AreEqual(12, Director.Mobile.BatsShown);
            var camera = GameBootstrap.Instance.CameraRig.OutputCamera;
            var bounds = ModelKit.RendererBounds(Director.Mobile.Model.gameObject);
            Vector3 lo = camera.WorldToViewportPoint(bounds.min);
            Vector3 hi = camera.WorldToViewportPoint(bounds.max);
            Debug.Log($"[KeepsakePlayCapture] 84 mobile viewport {lo:F2}..{hi:F2}");
            // Judged on the frame (upper right, clear of the logo and the buttons); the numbers are for the record.
            yield return Capture("84_title_mobile", GameBootstrap.Instance.Ui.GetComponent<Canvas>());

            // 82b / 82: the tenth bat completes page 1; the notebook opens on it assembling, then whole, beside a stack.
            yield return Play();
            Director.Keepsakes.Collect(PaperBatLayout.IdFor(13));
            yield return Wait(0.2f);
            Director.OpenNotebook();
            yield return Wait(0.3f);
            Director.Notebook.ShowPage(Director.Notebook.KeepsakePageIndex);
            yield return Wait(0.6f);
            Assert.AreEqual(3, Director.Notebook.BatStackCount, "82: page 1 and three bats in the stack");
            yield return Capture("82_keepsakes_page", Director.UiCanvas);
            Director.Notebook.Close();
            yield return Wait(0.4f);

            // A fresh file one bat short of page 1: the 10th find, then the assembly mid-flight.
            WriteKeepsakesFresh(9);
            yield return Load();
            yield return Play();
            Director.Keepsakes.Collect(PaperBatLayout.IdFor(10));
            Director.Keepsakes.Collect(PaperBatLayout.IdFor(11));
            Director.Keepsakes.Collect(PaperBatLayout.IdFor(12));
            yield return Wait(1.2f);
            Director.OpenNotebook();
            yield return Wait(0.55f);
            Assert.IsTrue(Director.Notebook.Assembling, "82b: page 1 assembles");
            yield return Capture("82b_keepsakes_assembling", Director.UiCanvas);
            yield return Wait(NotebookView.AssemblySeconds);
            Director.Notebook.Close();
            yield return Wait(0.4f);

            // 82c and 83: all thirty: the three pages; then the trunk open with Pip in the star cape and the hat.
            WriteKeepsakesFresh(30);
            yield return Load();
            yield return Play();
            Director.OpenNotebook();
            yield return Wait(0.3f);
            Director.Notebook.ShowPage(Director.Notebook.KeepsakePageIndex);
            yield return Wait(0.6f);
            Assert.AreEqual(3, Director.Notebook.SketchImages.Count);
            yield return Capture("82c_keepsakes_three_pages", Director.UiCanvas);
            Director.Notebook.Close();
            yield return Wait(0.5f);

            var spot = KeepsakePorch.DressingSpot;
            GameBootstrap.Instance.Pip.Teleport(new Vector3(spot.X, spot.Y, spot.Z));
            yield return Wait(0.3f);
            Director.UseTrunk();
            yield return Wait(0.3f);
            Director.WearPiece(CostumePieces.StarCape, true);
            Director.WearPiece(CostumePieces.ShowmanHat, true);
            yield return Wait(1.2f);
            Assert.IsTrue(Director.DressUp.IsOpen);
            yield return Capture("83_trunk_dress_up", Director.UiCanvas);
            Director.DressUp.Close();
            yield return Wait(0.8f);

            // 85: a keepsake bat fluttering open by Pip on the porch (bat 01 on the east rail post), close.
            var bat = PaperBatLayout.All[0];
            Vector3 at = new Vector3(bat.X, bat.Y, bat.Z);
            GameBootstrap.Instance.Pip.Teleport(new Vector3(1.2f, 0.62f, -13.4f));
            yield return Wait(0.8f);
            Assert.Greater(Director.Keepsakes.OpenAmount(bat.Id), 0.9f, "85: open by Pip");
            yield return CaptureFrom("85_bat_keepsake_close", at + new Vector3(-0.9f, 0.35f, 0.9f), at);

            // 85b: the lamp head from the path: the keepsake bat folded, the decorative bats moved clear of it.
            var lamp = PaperBatLayout.All[20];
            GameBootstrap.Instance.Pip.Teleport(new Vector3(1.4f, 0.05f, -7.6f));
            yield return Wait(0.6f);
            yield return CaptureFrom("85b_bat_lamp_head", new Vector3(0.2f, 1.9f, -5.6f),
                new Vector3(lamp.X, lamp.Y - 0.6f, lamp.Z));
        }

        private void WriteKeepsakesFresh(int count, params CostumePiece[] worn)
        {
            var store = new KeepsakeStore(new DiskSaveFileSystem(), _directory);
            store.Load();
            store.Reset();
            WriteKeepsakes(count, worn);
        }
    }
}
