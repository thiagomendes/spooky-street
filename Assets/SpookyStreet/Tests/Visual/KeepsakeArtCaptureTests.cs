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
    /// S004 stream B frames: the booth's owl crest from the plaza at the gameplay camera (70_booth_crest) and Aunt
    /// Kit's troupe photo in the hall at the gameplay camera (69_troupe_photo). Review frames (same numbers, an "r"):
    /// the crest from the gate and the photo close, as the seed cutscene will frame it.
    /// </summary>
    [Category("Visual")]
    public class KeepsakeArtCaptureTests : InputTestFixture
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

        private static bool OnScreen(Camera camera, Vector3 world)
        {
            Vector3 v = camera.WorldToViewportPoint(world);
            return v.z > 0f && v.x > 0.02f && v.x < 0.98f && v.y > 0.02f && v.y < 0.98f;
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator CaptureSeedArtFrames()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            var game = GameBootstrap.Instance;
            var director = game.Director;
            if (director.House == null || game.Environment.Crest == null || director.House.TroupePhoto == null)
            {
                Assert.Ignore("Environment models are not committed: the crest and the photo are not staged.");
            }

            game.Ui.NewGameButton.onClick.Invoke();
            yield return Wait(0.3f);
            director.Perform(CaseAction.Of(CaseActionKind.HearKnocks));
            director.Perform(CaseAction.Talk(Speaker.Bea));
            director.Dialogue.Skip();
            yield return Wait(0.3f);
            var camera = game.CameraRig.OutputCamera;
            var brain = Brain(camera);
            var crest = game.Environment.Crest;
            var crestBounds = SpookyStreet.World.ModelKit.RendererBounds(crest.gameObject);
            Assert.That(crestBounds.size.x, Is.InRange(0.3f, 0.45f), "the crest sign is 0.35 m across");
            Assert.That(crestBounds.center.y, Is.InRange(2.3f, 2.7f), "it hangs under the porch beam");
            Assert.That(crestBounds.center.x, Is.EqualTo(EnvironmentLayout.HouseX).Within(0.05f), "over the door");
            Assert.Less(crestBounds.center.z, EnvironmentLayout.HouseZ - 3.9f, "in front of the porch");

            // 70: the booth's crest from the plaza, at the gameplay camera.
            var plaza = new Vector3(0f, 0.15f, 1.5f);
            game.Pip.Teleport(plaza);
            Face(plaza, crestBounds.center);
            yield return Wait(2.2f);
            director.Captions.Clear();
            Assert.IsTrue(OnScreen(camera, crestBounds.center), "70: the crest is in the plaza view");
            yield return Shot("70_booth_crest");

            // 70r: from the gate, as the seed's match cut will see it.
            brain.enabled = false;
            camera.transform.position = new Vector3(-0.5f, 2.0f, EnvironmentLayout.FrontYardZ + 0.5f);
            camera.transform.LookAt(crestBounds.center);
            yield return Shot("70r_booth_crest_gate");
            brain.enabled = true;

            // 69: in by June's window, down to the hall: the troupe photo on the partition, at the gameplay camera.
            var travel = director.Travel;
            int before = travel.Crossings;
            game.Pip.Teleport(new Vector3(-4.3f, 2.55f, CaseRouteLayout.ExteriorWindowZ));
            yield return Wait(0.8f);
            game.Pip.Teleport(new Vector3(-3.36f, 2.55f, CaseRouteLayout.ExteriorWindowZ));
            float until = Time.time + 3f;
            while ((travel.Active || travel.Crossings == before) && Time.time < until)
            {
                yield return null;
            }

            Assert.AreEqual(before + 1, travel.Crossings, "69: in by the window");
            yield return Wait(0.3f);
            Assert.IsTrue(director.House.InteriorShown, "69: the interior is drawn");
            var photo = director.House.TroupePhoto;
            Assert.AreEqual(KeepsakeArt.KitPhoto, photo.name, "the cutscenes find it by this name");
            Assert.AreSame(director.House.Mechanism, photo.parent, "it is shown and hidden with the mechanism");
            Assert.IsTrue(photo.gameObject.activeInHierarchy, "69: the photo is drawn in the hall");
            var photoBounds = SpookyStreet.World.ModelKit.RendererBounds(photo.gameObject);
            Assert.That(photoBounds.center.x, Is.EqualTo(KeepsakeArt.PhotoWorldX).Within(0.05f), "on the partition");
            Assert.That(photoBounds.center.y, Is.EqualTo(KeepsakeArt.PhotoWorldY).Within(0.08f), "between the rails");
            Assert.That(photoBounds.center.z, Is.EqualTo(KeepsakeArt.PhotoWorldZ).Within(0.05f), "north of the service panel");
            Assert.That(photoBounds.size.z, Is.InRange(0.66f, 0.74f), "0.7 m wide along the wall");
            Assert.Less(photoBounds.size.x, 0.1f, "flat on the wall");
            foreach (string puppet in KeepsakeArt.Puppets)
            {
                Assert.IsNotNull(SpookyStreet.World.ModelKit.FindDeep(photo, puppet), "69: " + puppet);
            }

            var hall = new Vector3(0.2f, 0.65f, KeepsakeArt.PhotoWorldZ - 0.3f);
            game.Pip.Teleport(hall);
            Face(hall, new Vector3(KeepsakeArt.PhotoWorldX, 0.65f, KeepsakeArt.PhotoWorldZ));
            yield return Wait(2.2f);
            director.Captions.Clear();
            Debug.Log("[KeepsakeArtCaptureTests] 69_troupe_photo: " + director.Framing.Shot + ", photo on screen " +
                      OnScreen(camera, photoBounds.center));
            yield return Shot("69_troupe_photo");

            // 69r: close, square on, as the seed cutscene will push in on it.
            brain.enabled = false;
            camera.transform.position = photoBounds.center + Vector3.left * 0.95f;
            camera.transform.LookAt(photoBounds.center);
            yield return Shot("69r_troupe_photo_close");
            brain.enabled = true;
        }
    }
}
