using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Characters;
using SpookyStreet.Core.Crowd;
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
    /// The crowd costume kit in the game's light (docs/living-street-crowd-art.md), before the walkers are wired:
    /// every walker of <see cref="CrowdKit.Walkers"/> is posed by this test (its recipe's parts shown, Walk or Idle
    /// sampled) around Pip on the sidewalk. 93_crowd_street is the gameplay camera behind Pip with walkers crossing
    /// in front of her house; 93b_crowd_lineup the walkers and Pip side by side, close; 93c_crowd_lineup_back the
    /// same with everyone turned to face the house. The kit's art is checked here, not the crowd's behaviour (that is the slice's wiring).
    /// </summary>
    [Category("Visual")]
    public class CrowdArtCaptureTests : InputTestFixture
    {
        private const int Width = 1278;
        private const int Height = 590;
        private const float MaxMagenta = 0.005f;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Gamepad>();
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

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                yield return null;
            }
        }

        private static IEnumerator Shot(string name)
        {
            var game = GameBootstrap.Instance;
            Time.timeScale = 0f;
            yield return FrameCapture.Capture(game.CameraRig.OutputCamera, game.Director.UiCanvas, name, Width, Height);
            Time.timeScale = 1f;
            float magenta = FrameCapture.MagentaFraction(FrameCapture.LastFrame);
            Assert.LessOrEqual(magenta, MaxMagenta, $"{name}: {magenta:P1} of the frame is error-shader magenta");
            Assert.Greater(FrameCapture.MeanLuminance(FrameCapture.LastFrame), 0.02f, $"{name}: frame is black");
        }

        private static float Ground(float x, float z)
        {
            return Physics.Raycast(new Vector3(x, 4f, z), Vector3.down, out var hit, 8f, Physics.DefaultRaycastLayers,
                QueryTriggerInteraction.Ignore) ? hit.point.y : EnvironmentLayout.SidewalkTop;
        }

        /// <summary>A walker as the slice will show it: its recipe's LOD0 parts only, the shared atlas (with the
        /// lantern glow), facing <paramref name="yaw"/>, posed at <paramref name="clip"/> time <paramref name="t"/>.</summary>
        private static GameObject Walker(CrowdWalkerRecipe recipe, Vector3 at, float yaw, string clip, float t)
        {
            var game = GameBootstrap.Instance;
            var kit = new ModelKit(game.Materials, CrowdKit.Folder) { FallbackAtlas = CrowdKit.Atlas };
            var prefab = kit.Load(recipe.Model);
            Assert.IsNotNull(prefab, recipe.Model + " is not loadable");
            var root = new GameObject("Crowd_" + recipe.Id);
            root.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            var model = Object.Instantiate(prefab, root.transform, false);
            model.transform.localRotation = Quaternion.Euler(0f, NpcAvatar.ModelYawOffset, 0f) * prefab.transform.localRotation;
            var keep = new HashSet<string>(CrowdKit.MeshNames(recipe, 0));
            bool lantern = keep.Contains("Lantern_Paper");
            Material material = lantern ? kit.GlowMaterialFor(recipe.Model, 0.9f) : kit.MaterialFor(recipe.Model);
            int shown = 0;
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                bool on = keep.Contains(smr.name);
                smr.gameObject.SetActive(on);
                smr.updateWhenOffscreen = true;
                MaterialLibrary.ApplyAll(smr, material);
                shown += on ? 1 : 0;
            }

            Assert.AreEqual(keep.Count, shown, recipe.Id + ": every part of the recipe is in the model");
            AnimationClip sample = null;
            foreach (var c in Resources.LoadAll<AnimationClip>(CrowdKit.Folder + "/" + recipe.Model))
            {
                if (c.name == clip)
                {
                    sample = c;
                }
            }

            Assert.IsNotNull(sample, recipe.Model + " has no " + clip);
            sample.SampleAnimation(model, t * sample.length);
            return root;
        }

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator CaptureTheCrowdKit()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            var game = GameBootstrap.Instance;
            if (!game.Environment.UsesModels || Resources.Load<GameObject>(CrowdKit.Folder + "/" + CrowdKit.ChildModel) == null)
            {
                Assert.Ignore("Environment or crowd models are not committed.");
            }

            game.Ui.NewGameButton.onClick.Invoke();
            yield return Wait(0.3f);
            if (game.Director.Cutscenes.Playing)
            {
                game.Director.Cutscenes.Player.SkipNow();
                yield return Wait(0.2f);
            }

            game.Ui.FirstPrompt.SetActive(false);

            // 92: the gameplay camera behind Pip on the plaza, the walkers crossing on the sidewalk and the plaza.
            var pipAt = new Vector3(0f, 0.15f, 0.6f);
            game.Pip.Teleport(pipAt);
            game.Pip.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            game.CameraRig.Orbit.RequestRecenter();
            var spawned = new List<GameObject>();
            float[] xs = { -3.6f, -2.5f, -1.3f, 0.9f, 2.1f, 3.3f, -1.9f, 1.6f, 4.3f };
            float[] zs = { 3.3f, 3.6f, 3.2f, 3.5f, 3.3f, 3.6f, 2.0f, 2.1f, 2.4f };
            for (int i = 0; i < CrowdKit.Walkers.Length; i++)
            {
                float x = xs[i % xs.Length], z = zs[i % zs.Length];
                float yaw = i % 2 == 0 ? 90f : -90f;
                spawned.Add(Walker(CrowdKit.Walkers[i], new Vector3(x, Ground(x, z), z), yaw, "Walk", 0.13f * i));
            }

            yield return Wait(2.0f);
            game.Director.Captions.Clear();
            yield return Shot("93_crowd_street");

            // 93b / 93c: the lineup with Pip in the middle, close, front and back, Idle frame 0.
            foreach (var go in spawned)
            {
                Object.Destroy(go);
            }

            yield return null;
            spawned.Clear();
            const float lineZ = 3.4f;
            int n = CrowdKit.Walkers.Length + 1;
            int pipSlot = n / 2;
            for (int i = 0, w = 0; i < n; i++)
            {
                float x = (i - (n - 1) * 0.5f) * 0.78f;
                if (i == pipSlot)
                {
                    game.Pip.Teleport(new Vector3(x, Ground(x, lineZ) + 0.05f, lineZ));
                    game.Pip.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
                    continue;
                }

                spawned.Add(Walker(CrowdKit.Walkers[w++], new Vector3(x, Ground(x, lineZ), lineZ), 180f, "Idle", 0f));
            }

            yield return Wait(1.0f);
            game.Director.Captions.Clear();
            var camera = game.CameraRig.OutputCamera;
            var brain = camera.GetComponent("CinemachineBrain") as Behaviour;
            if (brain != null)
            {
                brain.enabled = false;
            }

            camera.transform.position = new Vector3(0f, 1.25f, lineZ - 5.6f);
            camera.transform.LookAt(new Vector3(0f, 0.8f, lineZ));
            yield return Shot("93b_crowd_lineup");
            // from behind: everyone turns to face the house, the camera stays on the street side
            foreach (var go in spawned)
            {
                go.transform.rotation = Quaternion.Euler(0f, 0f, 0f);
            }

            game.Pip.transform.rotation = Quaternion.LookRotation(Vector3.forward, Vector3.up);
            yield return null;
            yield return Shot("93c_crowd_lineup_back");
            if (brain != null)
            {
                brain.enabled = true;
            }
        }
    }
}
