using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.World;
using SpookyStreet.Localization;
using SpookyStreet.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// The original environment models dress the street when they are committed (and the greybox stays when
    /// they are not): owl house with its attic glow, wind wheel and owl, the candy bowl, textured URP materials
    /// everywhere, the attic window as the brightest glow, ambient motion that respects pause, and a mandatory
    /// route from the arrival porch through the plaza and the front gate that is still walkable.
    /// </summary>
    public class EnvironmentDressingTests : InputTestFixture
    {
        private const string SceneName = "LanternLane";
        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            Localizer.ResetForTests();
            CaseDirector.UseTemporarySaveDirectory(); // no save leaks between tests (S002)
        }

        private static IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(GameBootstrap.Instance, "scene did not create GameBootstrap");
        }

        private static bool ModelsCommitted => Resources.Load<GameObject>(EnvironmentDressing.HousePath) != null;

        [UnityTest]
        public IEnumerator EnvironmentMatchesModelAvailability()
        {
            yield return LoadGame();
            var env = GameBootstrap.Instance.Environment;
            Assert.IsNotNull(env);
            Assert.AreEqual(ModelsCommitted, env.UsesModels, "model presence and dressing disagree");
            if (!ModelsCommitted)
            {
                Assert.IsNotNull(GameObject.Find("OwlHouse_Gable"), "greybox owl house missing without the models");
                yield break;
            }

            Assert.IsNull(GameObject.Find("OwlHouse_Gable"), "greybox house must not be drawn under the model");
            Assert.IsNotNull(env.House, "owl house not spawned");
            Assert.IsNotNull(env.AtticWindow, "attic window glow part missing");
            Assert.IsNotNull(env.WindWheel, "wind wheel part missing");
            Assert.IsNotNull(env.OwlPuppet, "owl puppet part missing");
            Assert.IsNotNull(env.CandyBowl, "candy bowl not spawned");
            Assert.Greater(env.SpawnedCount, 30, "expected the street to be dressed");
            Assert.AreEqual(new Vector3(EnvironmentLayout.HouseX, 0f, EnvironmentLayout.HouseZ), env.House.position);
            Assert.Greater(env.CandyBowl.position.y, 0.6f, "candy bowl stands raised on the arrival porch");

            // The attic window sits on the front gable, facing the arrival porch (front at z = 11).
            Bounds attic = env.AtticWindow.GetComponent<Renderer>().bounds;
            Assert.That(attic.center.z, Is.InRange(10.4f, 11.6f), "attic window should be on the front wall (house yaw)");
            Assert.That(attic.center.y, Is.InRange(3.5f, 5.5f));

            var offenders = new List<string>();
            foreach (var renderer in env.Root.GetComponentsInChildren<Renderer>(true))
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    if (material == null || material.shader == null ||
                        !material.shader.name.StartsWith("Universal Render Pipeline/", System.StringComparison.Ordinal))
                    {
                        offenders.Add($"{renderer.name}: not a URP material");
                    }
                    else if (!material.HasProperty("_BaseMap") || material.GetTexture("_BaseMap") == null)
                    {
                        offenders.Add($"{renderer.name}: flat fallback ({material.name}), no baked base map");
                    }
                }
            }

            Assert.IsEmpty(offenders, string.Join("\n", offenders));
        }

        [UnityTest]
        public IEnumerator StreetSetReplacesTheGreyboxGroundEdgesAndSkyline()
        {
            yield return LoadGame();
            var env = GameBootstrap.Instance.Environment;
            var ground = GameObject.Find("Ground");
            var hedge = GameObject.Find("Hedge_North");
            Assert.IsNotNull(ground, "greybox ground collider missing");
            Assert.IsNotNull(hedge, "greybox boundary collider missing");
            Assert.IsTrue(ground.TryGetComponent<Collider>(out _), "Pip walks on the greybox ground collider");
            Assert.IsTrue(hedge.TryGetComponent<Collider>(out _), "the block edge must still block Pip");
            if (!env.StreetSetAvailable)
            {
                Assert.IsFalse(env.UsesStreetSet);
                Assert.IsTrue(ground.TryGetComponent<Renderer>(out _), "greybox ground must draw without the street set");
                Assert.IsNotNull(GameObject.Find("Skyline_House"), "greybox skyline missing without the street set");
                Assert.Ignore("Street-set models are not committed; the greybox ground and skyline stay.");
            }

            Assert.IsTrue(env.UsesStreetSet, "street set committed but not spawned");
            Assert.IsNotNull(env.StreetRoot);
            int expected = EnvironmentLayout.LawnTiles.Length + EnvironmentLayout.PathTiles.Length +
                           EnvironmentLayout.SidewalkTiles.Length + EnvironmentLayout.BoundaryHedges.Length +
                           EnvironmentLayout.NeighbourHouses.Length + EnvironmentLayout.LeafScatters.Length;
            Assert.AreEqual(expected, env.StreetCount, "every street-set placement should spawn");
            Assert.IsFalse(ground.TryGetComponent<Renderer>(out _), "the lawn draws the block; the greybox ground must not");
            Assert.IsFalse(hedge.TryGetComponent<Renderer>(out _), "boundary hedge models draw the block edge");
            var path = GameObject.Find("Path");
            Assert.IsTrue(path != null && path.TryGetComponent<Collider>(out _), "greybox path collider missing");
            Assert.IsFalse(path.TryGetComponent<Renderer>(out _), "path stones replace the greybox path");
            Assert.IsNull(GameObject.Find("Skyline_House"), "neighbour houses replace the skyline boxes");

            // Nothing animated is under the statically batched root.
            foreach (var moving in new[] { env.WindWheel, env.OwlPuppet, env.House })
            {
                Assert.IsFalse(moving != null && moving.IsChildOf(env.StreetRoot), moving + " must not be batched");
            }

            Assert.AreEqual(0, env.StreetRoot.GetComponentsInChildren<AmbientMotion>(true).Length);

            int renderers = 0;
            int batched = 0;
            int neighbourWindows = 0;
            foreach (var renderer in env.StreetRoot.GetComponentsInChildren<Renderer>(true))
            {
                renderers++;
                batched += renderer.isPartOfStaticBatch ? 1 : 0;
                if (renderer.name == "NeighbourWindow_Glow")
                {
                    neighbourWindows++;
                    Assert.AreEqual(GlowLevels.NeighbourWindow, MaterialLibrary.EmissionIntensity(renderer.sharedMaterial),
                        1e-3f, "neighbour windows glow at their own quiet level");
                }

                foreach (var material in renderer.sharedMaterials)
                {
                    Assert.IsTrue(material != null && material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null,
                        $"{renderer.name} has no baked base map");
                }
            }

            Debug.Log($"[EnvironmentDressingTests] street set: {env.StreetCount} instances, {renderers} renderers, " +
                      $"{batched} statically batched, {neighbourWindows} neighbour windows");
            Assert.Greater(renderers, 0);
            Assert.Greater(batched, renderers / 2, "the street set should be statically batched");
            Assert.AreEqual(EnvironmentLayout.NeighbourHouses.Length, neighbourWindows, "one lit window per neighbour");
        }

        [UnityTest]
        public IEnumerator AtticWindowIsTheBrightestGlowOnScreen()
        {
            yield return LoadGame();
            var env = GameBootstrap.Instance.Environment;
            if (!env.UsesModels)
            {
                Assert.Ignore("Environment models are not committed; the greybox keeps its own glow hierarchy.");
            }

            float attic = MaterialLibrary.EmissionIntensity(env.AtticWindow.GetComponent<Renderer>().sharedMaterial);
            Assert.AreEqual(GlowLevels.AtticWindow, attic, 1e-3f, "attic window must be emissive at its HDR level");

            var glowing = 0;
            foreach (var renderer in Object.FindObjectsByType<Renderer>(FindObjectsSortMode.None))
            {
                if (renderer.transform == env.AtticWindow)
                {
                    continue;
                }

                foreach (var material in renderer.sharedMaterials)
                {
                    float emission = MaterialLibrary.EmissionIntensity(material);
                    float unlit = 0f;
                    if (material != null && material.shader != null && material.shader.name.Contains("Unlit") &&
                        material.HasProperty("_BaseColor"))
                    {
                        Color c = material.GetColor("_BaseColor");
                        unlit = Mathf.Max(c.r, Mathf.Max(c.g, c.b));
                    }

                    glowing += emission > 0f ? 1 : 0;
                    Assert.Less(Mathf.Max(emission, unlit), attic, $"{renderer.name} ({material?.name}) outshines the attic");
                }
            }

            Assert.Greater(glowing, 3, "lamps, jack-o'-lantern faces and paper bats should glow too");
        }

        [UnityTest]
        public IEnumerator AmbientMotionRunsInPlayAndFreezesWhenPaused()
        {
            yield return LoadGame();
            var game = GameBootstrap.Instance;
            var env = game.Environment;
            if (!env.UsesModels)
            {
                Assert.Ignore("Environment models are not committed.");
            }

            Quaternion onTitle = env.WindWheel.localRotation;
            yield return null;
            yield return null;
            Assert.AreEqual(onTitle, env.WindWheel.localRotation, "nothing moves on the title screen (timeScale 0)");

            game.Ui.NewGameButton.onClick.Invoke();
            float until = Time.time + 0.5f;
            while (Time.time < until)
            {
                yield return null;
            }

            Assert.Greater(Quaternion.Angle(onTitle, env.WindWheel.localRotation), 5f, "the wind wheel should turn");

            game.Flow.TogglePause();
            yield return null;
            Quaternion paused = env.WindWheel.localRotation;
            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }

            Assert.AreEqual(paused, env.WindWheel.localRotation, "the wind wheel must stop while paused");
        }

        [UnityTest]
        public IEnumerator RouteFromArrivalThroughPlazaAndGateIsWalkable()
        {
            yield return LoadGame();
            var game = GameBootstrap.Instance;
            game.Ui.NewGameButton.onClick.Invoke();
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            float target = EnvironmentLayout.FrontYardZ + 1f;
            bool passedPlaza = false;
            Press(_keyboard.wKey);
            float deadline = Time.time + 9f;
            while (Time.time < deadline && game.Pip.transform.position.z < target)
            {
                passedPlaza |= Mathf.Abs(game.Pip.transform.position.z) < 1f;
                yield return null;
            }

            Release(_keyboard.wKey);
            Vector3 at = game.Pip.transform.position;
            Assert.IsTrue(passedPlaza, $"Pip never reached the plaza (stopped at {at})");
            Assert.GreaterOrEqual(at.z, target, $"Pip could not walk from the arrival porch through the front gate ({at})");
            Assert.Less(Mathf.Abs(at.x), 1f, "walking straight ahead should stay on the path");
        }
    }
}
