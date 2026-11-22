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
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// The handcrafted sky in the running game: every part exists and draws with a valid URP or sky shader (never
    /// the magenta error shader), nothing can out-shine the attic window, the sky holds still on the title and
    /// pause screens, and reduced motion removes the shooting star and the bats and slows the clouds. The timing
    /// maths is covered by the Core SkyTests.
    /// </summary>
    public class MagicSkyTests : InputTestFixture
    {
        private const string SceneName = "LanternLane";

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            Localizer.ResetForTests();
            CaseDirector.UseTemporarySaveDirectory(); // no save leaks between tests (S002)
            AmbientMotion.ReducedMotion = false;
        }

        public override void TearDown()
        {
            AmbientMotion.ReducedMotion = false;
            Time.timeScale = 1f;
            base.TearDown();
        }

        private static IEnumerator LoadGame(bool start)
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            var game = GameBootstrap.Instance;
            Assert.IsNotNull(game, "scene did not create GameBootstrap");
            Assert.IsNotNull(game.Sky, "the handcrafted sky was not built");
            if (start)
            {
                game.Ui.NewGameButton.onClick.Invoke();
                for (int i = 0; i < 5; i++)
                {
                    yield return null;
                }
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
        public IEnumerator SkyPartsExistAndNothingDrawsMagenta()
        {
            yield return LoadGame(start: false);
            MagicSky sky = GameBootstrap.Instance.Sky;
            GameObject dome = GameObject.Find(GreyboxBuilder.SkyDomeName);
            Assert.IsNotNull(dome, "sky dome missing");
            Debug.Log($"[MagicSky] crafted={sky.Crafted} graphics={SystemInfo.graphicsDeviceType}");

            var renderers = new List<Renderer> { dome.GetComponent<Renderer>() };
            renderers.AddRange(sky.GetComponentsInChildren<Renderer>(true));
            var offenders = new List<string>();
            foreach (Renderer renderer in renderers)
            {
                Material material = renderer.sharedMaterial;
                if (material == null || material.shader == null)
                {
                    offenders.Add($"{renderer.name}: no material or shader");
                    continue;
                }

                string shader = material.shader.name;
                bool skyShader = shader == MagicSky.DomeShaderName || shader == MagicSky.PaperShaderName;
                bool urp = shader.StartsWith("Universal Render Pipeline/", System.StringComparison.Ordinal);
                if (!skyShader && !urp)
                {
                    offenders.Add($"{renderer.name}: {shader} is neither a sky shader nor URP (would draw magenta)");
                }

                if (skyShader != sky.Crafted && renderer.gameObject != dome)
                {
                    offenders.Add($"{renderer.name}: {shader} does not match the sky's {(sky.Crafted ? "crafted" : "fallback")} path");
                }

                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null && !material.shader.isSupported)
                {
                    offenders.Add($"{renderer.name}: {shader} is not supported on this device");
                }

                if (material.HasProperty("_MaxValue") && material.GetFloat("_MaxValue") >= 1f)
                {
                    offenders.Add($"{renderer.name}: may reach the bloom threshold");
                }

                if (renderer.shadowCastingMode != ShadowCastingMode.Off)
                {
                    offenders.Add($"{renderer.name}: sky pieces must not cast shadows");
                }

                var filter = renderer.GetComponent<MeshFilter>();
                if (filter == null || filter.sharedMesh == null || filter.sharedMesh.vertexCount == 0)
                {
                    offenders.Add($"{renderer.name}: no mesh");
                }
            }

            Assert.IsEmpty(offenders, string.Join("\n", offenders));

            // The parts.
            Assert.IsNotNull(sky.Moon);
            Assert.IsNotNull(sky.MoonDisc);
            Assert.IsNotNull(sky.MoonDisc.sharedMaterial.GetTexture("_BaseMap"), "the moon is a painted paper disc");
            Assert.Less((sky.Moon.position - Atmosphere.MoonPosition).magnitude, 0.01f);
            Assert.AreEqual(SkyModel.Layers.Length, sky.CloudLayers.Count);
            Assert.AreEqual(SkyModel.BatMaxCount, sky.Bats.Count);
            foreach (Transform bat in sky.Bats)
            {
                Assert.IsFalse(bat.gameObject.activeSelf, "bats only appear for their flights");
            }

            Assert.IsNull(GameObject.Find("MoonCrater"), "the old primitive moon is gone");
            Assert.IsNotNull(dome.GetComponent<Renderer>().sharedMaterial.GetTexture("_BaseMap"), "the dome is painted");
            if (sky.Crafted)
            {
                Assert.AreEqual(MagicSky.DomeShaderName, dome.GetComponent<Renderer>().sharedMaterial.shader.name);
                Assert.IsNotNull(dome.GetComponent<Renderer>().sharedMaterial.GetTexture("_StarMap"), "stars twinkle from a mask");
                Assert.IsNotNull(sky.MoonHalo, "the moon has a soft halo card");
                Assert.GreaterOrEqual(sky.MoonHalo.sharedMaterial.renderQueue, (int)RenderQueue.Transparent);
                Assert.Less(sky.MoonDisc.sharedMaterial.renderQueue, (int)RenderQueue.AlphaTest, "cut pieces are opaque");
                Assert.IsNotNull(sky.ShootingStar);
                Assert.IsFalse(sky.ShootingStar.activeSelf, "the shooting star waits in its pool");
            }
            else
            {
                Assert.IsNull(sky.MoonHalo, "no soft pieces without the sky shader");
                Assert.IsNull(sky.ShootingStar);
            }

            // Draw budget: one call per piece. The old moon was four discs; the sky adds at most a handful.
            int pieces = sky.GetComponentsInChildren<Renderer>(true).Length;
            int idle = sky.GetComponentsInChildren<Renderer>(false).Length;
            Assert.LessOrEqual(pieces, 9, "moon, halo, three cloud rings, three bats, one shooting star");
            Assert.LessOrEqual(idle, 5, "between flights only the moon, halo and cloud rings draw");

            // Everything sits inside the dome.
            foreach (Renderer renderer in sky.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.gameObject.activeInHierarchy)
                {
                    Bounds bounds = renderer.bounds;
                    float reach = (bounds.center - GreyboxBuilder.SkyDomeCentre).magnitude;
                    Assert.Less(reach, GreyboxBuilder.SkyDomeRadius, renderer.name + " is centred outside the dome");
                }
            }
        }

        [UnityTest]
        public IEnumerator SkyHoldsStillWhilePausedAndDriftsInPlay()
        {
            yield return LoadGame(start: false);
            MagicSky sky = GameBootstrap.Instance.Sky;
            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }

            // The frame in which the scene loads may still carry the previous scene's time step; after that the
            // title screen runs at time scale 0.
            double atTitle = sky.Schedule.CloudClock;
            Assert.Less(atTitle, 0.5, "the sky has barely started on the title screen");
            Quaternion atStart = sky.CloudLayers[SkyModel.MoonLayer].localRotation;
            yield return new WaitForSecondsRealtime(0.3f);
            Assert.AreEqual(atTitle, sky.Schedule.CloudClock, "the title screen runs at time scale 0: the sky holds still");
            Assert.AreEqual(atStart, sky.CloudLayers[SkyModel.MoonLayer].localRotation);

            GameBootstrap.Instance.Ui.NewGameButton.onClick.Invoke();
            yield return Wait(0.6f);
            Assert.Greater(sky.Schedule.CloudClock, atTitle + 0.3, "clouds drift in play");
            Assert.Greater(sky.Schedule.TwinkleClock, 0.3, "stars twinkle in play");
            Assert.Greater(Quaternion.Angle(atStart, sky.CloudLayers[SkyModel.MoonLayer].localRotation), 0.1f);

            sky.ShowBats(0.45f);
            Assert.IsTrue(sky.Bats[0].gameObject.activeSelf);
            GameBootstrap.Instance.Flow.Pause();
            yield return null;
            double clock = sky.Schedule.CloudClock;
            Quaternion paused = sky.CloudLayers[SkyModel.MoonLayer].localRotation;
            Vector3 bat = sky.Bats[0].localPosition;
            yield return new WaitForSecondsRealtime(0.4f);
            Assert.AreEqual(clock, sky.Schedule.CloudClock, "paused: the clouds hold still");
            Assert.AreEqual(paused, sky.CloudLayers[SkyModel.MoonLayer].localRotation);
            Assert.AreEqual(bat, sky.Bats[0].localPosition, "paused: a bat hangs mid-flight");
            Assert.IsTrue(sky.Schedule.BatsActive);

            GameBootstrap.Instance.Flow.Resume();
            yield return Wait(0.3f);
            Assert.Greater(sky.Schedule.CloudClock, clock);
            Assert.AreNotEqual(bat, sky.Bats[0].localPosition, "resumed: the bat flies on");
        }

        [UnityTest]
        public IEnumerator ReducedMotionRemovesShootingStarsAndBats()
        {
            yield return LoadGame(start: true);
            MagicSky sky = GameBootstrap.Instance.Sky;

            // A flight half-way: the lead bat is in front of the moon disc.
            sky.ShowBats(0.5f);
            Assert.IsTrue(sky.Bats[0].gameObject.activeSelf);
            Vector3 offset = sky.Bats[0].position - sky.Moon.position;
            Assert.Less(offset.magnitude, Atmosphere.MoonDiameter * 0.5f, "the lead bat crosses the moon");
            Vector3 toViewer = GreyboxBuilder.SkyDomeCentre - sky.Moon.position;
            Assert.Greater(Vector3.Dot(offset, toViewer), 0f, "bats fly in front of the moon, not behind it");

            Vector3 head = sky.ShowShootingStar(0.2f, 0f);
            Assert.IsTrue(sky.Schedule.ShootingStarActive);
            Assert.Greater(head.y, 15f, "shooting stars cross the upper sky");
            Assert.Less((head - GreyboxBuilder.SkyDomeCentre).magnitude, GreyboxBuilder.SkyDomeRadius);
            if (sky.Crafted)
            {
                Assert.IsTrue(sky.ShootingStar.activeSelf, "the streak shows while it falls");
                Assert.Less((sky.ShootingStar.transform.position - head).magnitude, 0.01f);
            }

            AmbientMotion.ReducedMotion = true;
            yield return null;
            yield return null;
            Assert.IsFalse(sky.Schedule.ShootingStarActive, "reduced motion: no shooting star");
            Assert.IsFalse(sky.Schedule.BatsActive, "reduced motion: no bats");
            foreach (Transform bat in sky.Bats)
            {
                Assert.IsFalse(bat.gameObject.activeSelf);
            }

            if (sky.Crafted)
            {
                Assert.IsFalse(sky.ShootingStar.activeSelf);
            }

            double clock = sky.Schedule.CloudClock;
            float began = Time.time;
            yield return Wait(0.8f);
            double drift = sky.Schedule.CloudClock - clock;
            float passed = Time.time - began;
            Assert.Greater(drift, 0.0, "reduced motion: the clouds still drift");
            Assert.Less(drift, passed * 0.5, "reduced motion: but slowly");
            Assert.AreEqual(0, CountActive(sky.Bats));
        }

        private static int CountActive(IReadOnlyList<Transform> items)
        {
            int count = 0;
            foreach (Transform item in items)
            {
                if (item.gameObject.activeSelf)
                {
                    count++;
                }
            }

            return count;
        }
    }
}
