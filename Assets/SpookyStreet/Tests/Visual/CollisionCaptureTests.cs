using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.World;
using SpookyStreet.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SpookyStreet.Tests.Visual
{
    /// <summary>
    /// Collision pass frame: Pip standing on the treehouse deck at 2.4 m (22_treehouse_deck), seen from the deck's
    /// open side so the floor, the rails and the trunk through the deck read together.
    /// </summary>
    [Category("Visual")]
    public class CollisionCaptureTests : InputTestFixture
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

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                yield return null;
            }
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator CaptureTreehouseDeck()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            var game = GameBootstrap.Instance;
            if (!game.Environment.UsesModels)
            {
                Assert.Ignore("Environment models are not committed.");
            }

            game.Ui.NewGameButton.onClick.Invoke();
            yield return Wait(0.3f);

            Transform deck = null;
            foreach (var p in EnvironmentLayout.Street)
            {
                if (p.Model != EnvironmentLayout.TreehousePlatform)
                {
                    continue;
                }

                foreach (var t in game.Environment.Root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == ModelColliders.PartPrefix + "Deck" &&
                        Vector3.Distance(t.parent.position, new Vector3(p.X, p.Y, p.Z)) < 0.1f)
                    {
                        deck = t;
                    }
                }
            }

            Assert.IsNotNull(deck, "22_treehouse_deck: the treehouse deck collider is missing");
            float top = deck.GetComponent<Collider>().bounds.max.y;
            Vector3 stand = deck.TransformPoint(new Vector3(0.75f, 0f, 0.6f));
            game.Pip.Teleport(new Vector3(stand.x, top + 0.15f, stand.z));
            Vector3 open = deck.TransformDirection(Vector3.left); // the deck's open edge (toward the shed roof)
            game.Pip.transform.rotation = Quaternion.LookRotation(open, Vector3.up);
            yield return Wait(1.0f);
            Assert.IsTrue(game.Pip.Grounded, "22_treehouse_deck: Pip should stand on the deck");
            Assert.AreEqual(top, game.Pip.transform.position.y, 0.08f, "22_treehouse_deck: on the deck floor");

            var camera = game.CameraRig.OutputCamera;
            var brain = camera.GetComponent("CinemachineBrain") as Behaviour;
            if (brain != null)
            {
                brain.enabled = false;
            }

            Vector3 pip = game.Pip.transform.position;
            Vector3 side = Vector3.Cross(Vector3.up, open).normalized;
            camera.transform.position = pip + open * 3.6f + side * 1.2f + Vector3.up * 0.9f;
            camera.transform.LookAt(pip + Vector3.up * 0.35f);
            Time.timeScale = 0f;
            yield return FrameCapture.Capture(camera, game.Ui.GetComponent<Canvas>(), "22_treehouse_deck", Width, Height);
            Time.timeScale = 1f;
            if (brain != null)
            {
                brain.enabled = true;
            }

            float magenta = FrameCapture.MagentaFraction(FrameCapture.LastFrame);
            Assert.LessOrEqual(magenta, MaxMagenta, $"22_treehouse_deck: {magenta:P1} of the frame is magenta");
            Assert.Greater(FrameCapture.MeanLuminance(FrameCapture.LastFrame), 0.02f, "22_treehouse_deck: frame is black");
        }
    }
}
