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
    /// The paper lantern table's art in the game's light (docs/living-street-lantern-art.md), before the living street
    /// wires it: this test places the pieces itself, through <see cref="PaperLanternArt"/> as the slice will.
    /// 95_lantern_table_porch: the gameplay camera behind Pip on the path, looking at her porch with the hook and her
    /// lantern lit, and the lantern table standing beside the path (a stand-in spot: its home is the Green, which this
    /// branch's scene does not have). 95b_lantern_table: Pip at the table, the gameplay camera behind her.
    /// 95c_lantern_colours: the four shapes in the four colours with a crowd child's paper lantern beside them, in front
    /// of the owl house, so their glow is judged against the crowd's and under the attic window. 95d_lantern_arrival:
    /// the arrival camera on Pip's porch with her lantern on its hook.
    /// </summary>
    [Category("Visual")]
    public class LanternArtCaptureTests : InputTestFixture
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
            game.Director.Captions.Clear();
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

        /// <summary>The hook on Pip's porch with a lit lantern hung from its eye, as the slice will place them.</summary>
        private static GameObject PorchLantern(ModelKit kit, Transform root, string shape, string colour)
        {
            var hook = kit.Spawn(LanternArt.Hook, root,
                new Vector3(LanternArt.PorchHookX, LanternArt.PorchHookY, LanternArt.PorchHookZ), LanternArt.PorchHookYaw);
            Assert.IsNotNull(hook, LanternArt.Hook + " is not loadable");
            var hang = PaperLanternArt.HangPoint(hook);
            Assert.IsTrue(hang.HasValue, "the hook has its hang point");
            var lantern = PaperLanternArt.SpawnLantern(kit, shape, colour, root, hang.Value, LanternArt.PorchHookYaw);
            Assert.IsNotNull(lantern, LanternArt.Model(shape) + " is not loadable");
            return lantern;
        }

        private static GameObject Table(ModelKit kit, Transform root, Vector3 at, float yaw)
        {
            var table = kit.Spawn(LanternArt.Table, root, at, yaw);
            Assert.IsNotNull(table, LanternArt.Table + " is not loadable");
            return table;
        }

        /// <summary>A crowd child carrying her paper lantern (the crowd kit's glow material at 0.9), Idle frame 0.</summary>
        private static GameObject CrowdLanternChild(Vector3 at, float yaw)
        {
            var game = GameBootstrap.Instance;
            CrowdWalkerRecipe recipe = null;
            foreach (var w in CrowdKit.Walkers)
            {
                if (w.Id == "QueenKid")
                {
                    recipe = w;
                }
            }

            var kit = new ModelKit(game.Materials, CrowdKit.Folder) { FallbackAtlas = CrowdKit.Atlas };
            var prefab = recipe != null ? kit.Load(recipe.Model) : null;
            if (prefab == null)
            {
                return null;
            }

            var root = new GameObject("Crowd_" + recipe.Id);
            root.transform.SetPositionAndRotation(at, Quaternion.Euler(0f, yaw, 0f));
            var model = Object.Instantiate(prefab, root.transform, false);
            model.transform.localRotation = Quaternion.Euler(0f, NpcAvatar.ModelYawOffset, 0f) * prefab.transform.localRotation;
            var keep = new HashSet<string>(CrowdKit.MeshNames(recipe, 0));
            var material = kit.GlowMaterialFor(recipe.Model, GlowLevels.PaperLantern);
            foreach (var smr in model.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                smr.gameObject.SetActive(keep.Contains(smr.name));
                smr.updateWhenOffscreen = true;
                MaterialLibrary.ApplyAll(smr, material);
            }

            foreach (var c in Resources.LoadAll<AnimationClip>(CrowdKit.Folder + "/" + recipe.Model))
            {
                if (c.name == "Idle")
                {
                    c.SampleAnimation(model, 0f);
                }
            }

            return root;
        }

        private static void Face(Vector3 direction)
        {
            var game = GameBootstrap.Instance;
            game.Pip.transform.rotation = Quaternion.LookRotation(direction, Vector3.up);
            game.CameraRig.Orbit.RequestRecenter();
        }

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator CaptureTheLanternArt()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            var game = GameBootstrap.Instance;
            var kit = PaperLanternArt.Kit(game.Materials);
            if (!game.Environment.UsesModels || !kit.Has(LanternArt.Hook))
            {
                Assert.Ignore("Environment or lantern models are not committed.");
            }

            game.Ui.NewGameButton.onClick.Invoke();
            yield return Wait(0.3f);
            if (game.Director.Cutscenes.Playing)
            {
                game.Director.Cutscenes.Player.SkipNow();
                yield return Wait(0.2f);
            }

            game.Ui.FirstPrompt.SetActive(false);
            var root = new GameObject("LanternArtCapture").transform;

            // 95: from the path, Pip turned toward her porch: the hook and her lantern lit, the table beside the path.
            PorchLantern(kit, root, LanternArt.Cat, LanternArt.Orange);
            var tableAt = new Vector3(4.3f, 0f, -10.4f);
            tableAt.y = Ground(tableAt.x, tableAt.z);
            const float tableYaw = 160f; // its front (the model's -Z) toward the path
            Table(kit, root, tableAt, tableYaw);
            game.Pip.Teleport(new Vector3(0.3f, 0.15f, -7.6f));
            Face(Vector3.back);
            yield return Wait(2.0f);
            yield return Shot("95_lantern_table_porch");

            // 95b: Pip at the table's front, the gameplay camera behind her.
            Vector3 front = Quaternion.Euler(0f, tableYaw, 0f) * Vector3.back;
            game.Pip.Teleport(tableAt + front * 0.9f + Vector3.up * 0.1f);
            Face(-front);
            yield return Wait(2.0f);
            yield return Shot("95b_lantern_table");

            // 95c: every shape in every colour (shapes across, colours down) and a crowd child's lantern, under the attic.
            var grid = new GameObject("LanternGrid").transform;
            grid.SetParent(root, false);
            for (int s = 0; s < LanternArt.Shapes.Count; s++)
            {
                for (int c = 0; c < LanternArt.Colours.Count; c++)
                {
                    var at = new Vector3(-1.125f + 0.75f * s, 2.75f - 0.5f * c, 2.2f);
                    PaperLanternArt.SpawnLantern(kit, LanternArt.Shapes[s], LanternArt.Colours[c], grid, at, 180f);
                }
            }

            var child = CrowdLanternChild(new Vector3(2.35f, Ground(2.35f, 2.2f), 2.2f), 180f);
            game.Pip.Teleport(new Vector3(-2.6f, Ground(-2.6f, 2.0f) + 0.05f, 2.0f));
            game.Pip.transform.rotation = Quaternion.LookRotation(Vector3.back, Vector3.up);
            yield return Wait(1.0f);
            var camera = game.CameraRig.OutputCamera;
            var brain = camera.GetComponent("CinemachineBrain") as Behaviour;
            if (brain != null)
            {
                brain.enabled = false;
            }

            camera.transform.position = new Vector3(0f, 1.7f, -3.4f);
            camera.transform.LookAt(new Vector3(0f, 2.9f, 6f));
            yield return Shot("95c_lantern_colours");
            if (brain != null)
            {
                brain.enabled = true;
            }

            Object.Destroy(grid.gameObject);
            if (child != null)
            {
                Object.Destroy(child);
            }

            // 95d: the arrival camera on Pip's porch (the opening's view), her lantern on its hook at the front corner.
            game.Pip.Teleport(new Vector3(0f, 0.75f, -14.2f));
            Face(Vector3.forward);
            yield return Wait(2.0f);
            yield return Shot("95d_lantern_arrival");
            Object.Destroy(root.gameObject);
        }
    }
}
