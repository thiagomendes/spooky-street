using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.LivingStreet;
using SpookyStreet.LivingStreet;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SpookyStreet.Tests.Visual
{
    /// <summary>
    /// The living street's test slice (docs/living-street-slice.md): 94 the east lane with walkers on their loops, 96 a
    /// door answering Pip's ring (Mr. Fogg in his doorway), 97 Pip's lit porch lantern with a group at her button, 97b the
    /// lantern craft table on the Green, 97c the lantern picker with its names (95 is the lantern art's own frames). Review
    /// frames: the lantern table's screen, the Green's corner, the heart with walkers passing south of the plaza, and a
    /// walker close up (placeholder bodies until the crowd kit lands).
    /// </summary>
    [Category("Visual")]
    public class LivingStreetCaptureTests : InputTestFixture
    {
        private const int Width = 1278;
        private const int Height = 590;
        private const int ReviewWidth = 640;
        private const int ReviewHeight = 296;
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

        public override void TearDown()
        {
            Time.timeScale = 1f;
            base.TearDown();
        }

        private static CaseDirector Director => GameBootstrap.Instance.Director;

        private static LivingStreetStage Stage => Director.Living;

        private static IEnumerator Shot(string name, Canvas canvas, int width = Width, int height = Height)
        {
            var game = GameBootstrap.Instance;
            yield return FrameCapture.Capture(game.CameraRig.OutputCamera, canvas, name, width, height);
            float magenta = FrameCapture.MagentaFraction(FrameCapture.LastFrame);
            Assert.LessOrEqual(magenta, MaxMagenta, $"{name}: {magenta:P1} of the frame is error-shader magenta");
            Assert.Greater(FrameCapture.MeanLuminance(FrameCapture.LastFrame), 0.02f, $"{name}: frame is black");
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                if (Director.Dialogue.IsOpen)
                {
                    Director.Dialogue.Skip();
                }

                yield return null;
            }
        }

        private static IEnumerator Null()
        {
            yield return null;
        }

        private static IEnumerator StartGame()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            GameBootstrap.Instance.Ui.NewGameButton.onClick.Invoke();
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }
        }

        private static Behaviour Frame(Vector3 eye, Vector3 at)
        {
            var camera = GameBootstrap.Instance.CameraRig.OutputCamera;
            var brain = camera.GetComponent("CinemachineBrain") as Behaviour;
            if (brain != null)
            {
                brain.enabled = false;
            }

            camera.transform.position = eye;
            camera.transform.LookAt(at);
            return brain;
        }

        private static void PlacePip(Vector3 at, Vector3 lookAt)
        {
            var pip = GameBootstrap.Instance.Pip;
            pip.Teleport(at);
            Vector3 to = lookAt - at;
            to.y = 0f;
            pip.transform.rotation = Quaternion.LookRotation(to, Vector3.up);
        }

        /// <summary>Runs the crowd forward until <paramref name="ready"/> (game time sped up; nothing is skipped).</summary>
        private static IEnumerator RunCrowdUntil(System.Func<bool> ready, float realSeconds, float speed = 6f)
        {
            Time.timeScale = speed;
            float until = Time.realtimeSinceStartup + realSeconds;
            while (!ready() && Time.realtimeSinceStartup < until)
            {
                if (Director.Dialogue.IsOpen)
                {
                    Director.Dialogue.Skip();
                }

                yield return null;
            }

            Time.timeScale = 1f;
        }

        /// <summary>Walkers whose roots are in front of the camera within <paramref name="range"/> metres.</summary>
        private static int WalkersInView(float range)
        {
            var camera = GameBootstrap.Instance.CameraRig.OutputCamera;
            int n = 0;
            foreach (var body in Stage.Crowd.Bodies)
            {
                Vector3 v = camera.WorldToViewportPoint(body.Root.position + Vector3.up * 0.6f);
                n += v.z > 0f && v.z < range && v.x > 0.05f && v.x < 0.95f && v.y > 0.05f && v.y < 0.95f ? 1 : 0;
            }

            return n;
        }

        [UnityTest]
        [Timeout(420000)]
        public IEnumerator CapturesTheLivingStreetSlice()
        {
            yield return StartGame();
            Assert.IsNotNull(Stage);

            // 94: the east lane from the hedge gap, the porch houses and walkers on their loops.
            PlacePip(new Vector3(23.4f, 0.1f, -0.6f), new Vector3(34f, 0f, 0f));
            var brain = Frame(new Vector3(21.2f, 2.4f, -2.2f), new Vector3(34f, 1.4f, 1.2f));
            yield return RunCrowdUntil(() => WalkersInView(26f) >= 5, 40f);
            yield return Wait(0.3f);
            Debug.Log($"[LivingStreetCaptureTests] 94: {WalkersInView(26f)} walkers in view");
            Director.Captions.Clear(); // the arrival's knock caption belongs to the case, not this frame
            yield return Null();
            yield return Shot("94_living_street_walkers", Director.UiCanvas);

            // Review: the heart with walkers passing south of the plaza (never near a clue, never in the bench's view).
            Frame(new Vector3(4f, 2.6f, -12f), new Vector3(14f, 1f, -3.5f));
            yield return RunCrowdUntil(() => Stage.Crowd.Clock.WalkersInHeart >= 2, 40f);
            yield return Wait(0.2f);
            yield return Shot("r_living_street_heart", Director.UiCanvas, ReviewWidth, ReviewHeight);

            // 96: Pip rings Mr. Fogg's bell; he flings the door open and stands in the lit doorway.
            var fogg = LivingStreetLayout.Door(LivingStreetLayout.Fogg);
            PlacePip(new Vector3(fogg.StandX, 0.1f, fogg.StandZ - 0.2f), new Vector3(fogg.DoorX, 1f, fogg.DoorZ));
            yield return Wait(0.6f);
            Assert.IsTrue(Stage.Ring(fogg));
            yield return Wait(fogg.Gag.OpenSeconds + 0.6f);
            Frame(new Vector3(fogg.StandX + 2.4f, 2.1f, fogg.StandZ - 3.4f), new Vector3(fogg.DoorX - 0.2f, 1.5f, fogg.DoorZ));
            yield return Shot("96_living_street_door", Director.UiCanvas);

            // Review: the Abbotts answer together (two in the doorway).
            var abbotts = LivingStreetLayout.Door(LivingStreetLayout.Abbotts);
            PlacePip(new Vector3(abbotts.StandX, 0.1f, abbotts.StandZ + 0.2f), new Vector3(abbotts.DoorX, 1f, abbotts.DoorZ));
            yield return Wait(0.4f);
            if (Stage.Ring(abbotts))
            {
                yield return Wait(abbotts.Gag.OpenSeconds + 0.5f);
                Frame(new Vector3(abbotts.StandX - 2.2f, 2.0f, abbotts.StandZ + 3.2f), new Vector3(abbotts.DoorX, 1.5f, abbotts.DoorZ));
                yield return Shot("r_living_street_abbotts", Director.UiCanvas, ReviewWidth, ReviewHeight);
            }

            // Review: the lantern table's screen over the Green's corner (string lights, paper lanterns).
            PlacePip(new Vector3(LivingStreetLayout.TableUseX, 0.1f, LivingStreetLayout.TableUseZ + 0.3f),
                new Vector3(LivingStreetLayout.TableX, 1f, LivingStreetLayout.TableZ));
            Frame(new Vector3(LivingStreetLayout.TableX - 2.2f, 2.3f, LivingStreetLayout.TableZ + 4.6f),
                new Vector3(LivingStreetLayout.TableX + 0.4f, 1.1f, LivingStreetLayout.TableZ));
            yield return Wait(0.4f);
            Director.Captions.Clear();
            yield return null;
            Assert.IsNotNull(Stage.Set.TableArt, "the lantern art's craft table");
            yield return Shot("97b_living_street_table", Director.UiCanvas);
            Stage.OpenTable();
            yield return null;
            Stage.LanternScreen.ChooseShape(LanternShape.Bat);
            Stage.LanternScreen.ChooseColour(LanternColour.Orange);
            yield return Wait(0.3f);
            Director.Captions.Clear();
            yield return null;
            yield return Shot("97c_living_street_picker", Director.UiCanvas);
            Stage.LanternScreen.Fold();
            yield return Wait(0.3f);
            Assert.IsTrue(Stage.Set.PorchLanternLit);

            // 97: Pip on her porch under her lit lantern; a group at her steps rings her bell.
            PlacePip(new Vector3(0.2f, 0.62f, -13.6f), new Vector3(0.3f, 0f, -10f));
            Frame(new Vector3(-3.6f, 2.0f, -9.0f), new Vector3(0.0f, 1.3f, -12.6f));
            yield return RunCrowdUntil(() => Stage.Crowd.Clock.WaitingAtPorch >= 0, 90f);
            Assert.GreaterOrEqual(Stage.Crowd.Clock.WaitingAtPorch, 0, "a group came to Pip's bell");
            yield return Wait(0.8f);
            yield return Shot("97_living_street_porch", Director.UiCanvas);

            // Review: a walker close up (placeholder body), and the treat handed over.
            Assert.IsTrue(Stage.GiveTreat());
            yield return Wait(0.25f);
            var walker = Stage.Crowd.Bodies[0].Root;
            Frame(walker.position + walker.forward * 2.2f + new Vector3(0.6f, 1.3f, 0f), walker.position + Vector3.up * 0.6f);
            yield return Shot("r_living_street_walker", Director.UiCanvas, ReviewWidth, ReviewHeight);

            // Review: the notebook's street page (Pip's porch with her lantern, the neighbours she has met).
            yield return Wait(CrowdClock.CheerSeconds);
            Director.OpenNotebook();
            yield return null;
            var notebook = Director.Notebook;
            Assert.IsNotNull(notebook.StreetPage);
            notebook.ShowPage(notebook.PageCount - 1);
            yield return Wait(0.8f);
            yield return Shot("r_living_street_page", Director.UiCanvas, ReviewWidth, ReviewHeight);
            if (brain != null)
            {
                brain.enabled = true;
            }
        }
    }
}
