using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Characters;
using SpookyStreet.Core.Animation;
using SpookyStreet.Core.Characters;
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
    /// S000.5: the witnesses of the first case stand in the opening area as animated ambient characters (Bea and
    /// her bicycle west of the plaza, Mr. Vale on the bench), react to Pip coming near (wave once, then talk), and
    /// are absent without their models. No dialogue yet (S002).
    /// </summary>
    public class WitnessTests : InputTestFixture
    {
        private const string SceneName = "LanternLane";

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            Localizer.ResetForTests();
            CaseDirector.UseTemporarySaveDirectory(); // no save leaks between tests (S002)
        }

        private static IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(GameBootstrap.Instance, "scene did not create GameBootstrap");
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                yield return null;
            }
        }

        private static IEnumerator StartPlaying(GameBootstrap game)
        {
            game.Ui.NewGameButton.onClick.Invoke();
            for (int i = 0; i < 5; i++)
            {
                yield return null;
            }

            Assert.AreEqual(1f, Time.timeScale);
        }

        private static bool BeaCommitted => NpcAvatar.Exists(EnvironmentLayout.Bea);

        private static bool ValeCommitted =>
            NpcAvatar.Exists(EnvironmentLayout.Vale) && GameBootstrap.Instance.Environment.UsesModels;

        private static void AssertTextured(Witness witness)
        {
            var renderers = witness.GetComponentsInChildren<Renderer>();
            Assert.IsNotEmpty(renderers, witness.name + " has no renderers");
            foreach (var renderer in renderers)
            {
                foreach (var material in renderer.sharedMaterials)
                {
                    Assert.IsNotNull(material, $"{renderer.name} has no material");
                    Assert.IsTrue(material.HasProperty("_BaseMap") && material.GetTexture("_BaseMap") != null,
                        $"{renderer.name} must use its baked texture ({material.name})");
                }
            }
        }

        [UnityTest]
        public IEnumerator WitnessesSpawnAnimatedAndTextured()
        {
            yield return LoadGame();
            var game = GameBootstrap.Instance;
            var cast = game.Witnesses;
            Assert.IsNotNull(cast);
            Assert.AreEqual(BeaCommitted, cast.Bea != null, "Bea's presence and her model disagree");
            Assert.AreEqual(ValeCommitted, cast.Vale != null, "Mr. Vale's presence and his model disagree");
            bool bikeCommitted = Resources.Load<GameObject>(EnvironmentDressing.ModelFolder + "/" +
                                                            EnvironmentLayout.BeaBicycle) != null;
            Assert.AreEqual(bikeCommitted, cast.Bicycle != null, "bicycle presence and its model disagree");
            if (!BeaCommitted && !ValeCommitted && !bikeCommitted)
            {
                Assert.IsNull(cast.Root, "nothing should be spawned without the models");
                yield break;
            }

            yield return StartPlaying(game);
            yield return Wait(0.5f);

            if (cast.Bea != null)
            {
                var bea = cast.Bea.Avatar;
                Assert.IsTrue(bea.IsAnimated, "Bea's animation graph did not start (Idle clip missing?)");
                Assert.IsTrue(bea.HasClip(CharacterClips.Idle));
                Assert.IsTrue(bea.HasClip(CharacterClips.Talk));
                Assert.IsTrue(bea.HasClip(CharacterClips.Wave));
                Assert.AreEqual(CharacterClips.Idle, bea.Loop);
                Assert.Greater(bea.WeightOf(CharacterClips.Idle), 0.9f);
                Assert.IsNotNull(cast.Bea.GetComponent<CapsuleCollider>(), "Bea needs a capsule collider");
                AssertTextured(cast.Bea);
                Bounds bounds = ModelKit.RendererBounds(cast.Bea.gameObject);
                Assert.That(bounds.size.y, Is.InRange(1.2f, 1.7f), "Bea keeps her native height (about 1.5 m)");
                Assert.That(bounds.min.y, Is.InRange(-0.1f, 0.1f), "Bea's feet are on the ground");
            }

            if (cast.Bicycle != null)
            {
                Assert.IsNotNull(cast.Bicycle.GetComponent<BoxCollider>(), "the bicycle needs a box collider");
                foreach (var renderer in cast.Bicycle.GetComponentsInChildren<Renderer>())
                {
                    Assert.IsNotNull(renderer.sharedMaterial.GetTexture("_BaseMap"), renderer.name);
                }

                Assert.Less(cast.Bicycle.position.x, -5f, "the bicycle is west of the plaza");
            }

            if (cast.Vale != null)
            {
                var vale = cast.Vale.Avatar;
                Assert.IsTrue(vale.IsAnimated, "Mr. Vale's animation graph did not start (SitIdle clip missing?)");
                Assert.IsTrue(vale.HasClip(CharacterClips.SitIdle));
                Assert.IsTrue(vale.HasClip(CharacterClips.SitTalk));
                Assert.AreEqual(CharacterClips.SitIdle, vale.Loop);
                Assert.Greater(vale.WeightOf(CharacterClips.SitIdle), 0.9f);
                AssertTextured(cast.Vale);
            }
        }

        [UnityTest]
        public IEnumerator ValeSitsOnTheBench()
        {
            yield return LoadGame();
            var game = GameBootstrap.Instance;
            var vale = game.Witnesses.Vale;
            if (vale == null)
            {
                Assert.IsFalse(ValeCommitted);
                Assert.Ignore("Mr. Vale (or the bench) is not committed.");
            }

            Transform bench = null;
            foreach (Transform child in game.Environment.Root)
            {
                if (child.name == EnvironmentLayout.Bench)
                {
                    bench = child;
                }
            }

            Assert.IsNotNull(bench, "bench not spawned");
            yield return StartPlaying(game);
            yield return Wait(0.5f);

            // Root on the ground under the seat, offset along the bench and with its facing.
            Vector3 root = vale.transform.position;
            Assert.AreEqual(0f, root.y, 0.02f, "root on the ground");
            Bounds benchBounds = ModelKit.RendererBounds(bench.gameObject);
            Assert.That(root.x, Is.InRange(benchBounds.min.x, benchBounds.max.x), "under the bench (x)");
            Assert.That(root.z, Is.InRange(benchBounds.min.z, benchBounds.max.z), "under the bench (z)");
            Vector3 offset = root - bench.position;
            offset.y = 0f;
            Assert.AreEqual(EnvironmentLayout.ValeBenchOffsetX, offset.magnitude, 0.03f, "offset along the bench");
            float facing = EnvironmentLayout.BenchYaw + EnvironmentLayout.ModelFacingOffset;
            Assert.Less(Mathf.Abs(Mathf.DeltaAngle(facing, vale.transform.eulerAngles.y)), 1f, "faces the bench's way");

            // The seated clip puts his hip joints just above the seat (0.09 m in witness_anims.py); standing they
            // are near 0.85 m.
            var hip = vale.Avatar.Find("UpperLeg_L");
            Assert.IsNotNull(hip, "UpperLeg_L bone missing");
            Debug.Log($"[WitnessTests] Vale hip joint at {hip.position.y:F2} m (seat {EnvironmentLayout.BenchSeatHeight} m), " +
                      $"bench bounds {benchBounds.min} - {benchBounds.max}");
            Assert.That(hip.position.y, Is.InRange(EnvironmentLayout.BenchSeatHeight,
                EnvironmentLayout.BenchSeatHeight + 0.2f), "Mr. Vale should be seated on the bench");
        }

        [UnityTest]
        public IEnumerator BeaWavesWhenPipComesNearThenTalksThenSettles()
        {
            yield return LoadGame();
            var game = GameBootstrap.Instance;
            var bea = game.Witnesses.Bea;
            if (bea == null)
            {
                Assert.Ignore("Bea is not committed.");
            }

            yield return StartPlaying(game);
            Vector3 home = bea.transform.position;
            Vector3 toward = bea.transform.forward; // Pip comes from the plaza, in front of her
            game.Pip.Teleport(home + toward * 8f + Vector3.up * 0.2f);
            yield return Wait(0.5f);
            Assert.AreEqual(WitnessState.Idle, bea.Reaction.State);
            Assert.AreEqual(CharacterClips.Idle, bea.Avatar.Loop);
            Assert.IsNull(bea.Avatar.OneShot);

            // Walk Pip in from 8 m to 2.5 m over a second.
            bool waved = false;
            for (float d = 8f; d >= 2.5f; d -= 0.1f)
            {
                game.Pip.Teleport(home + toward * d + Vector3.up * 0.05f);
                yield return null;
                waved |= bea.Avatar.OneShot == CharacterClips.Wave;
            }

            Assert.IsTrue(waved, "Bea should wave when Pip comes within reach");
            Assert.AreEqual(1, bea.Reaction.WaveCount);

            yield return Wait(bea.Avatar.LengthOf(CharacterClips.Wave) + 0.6f);
            Assert.AreEqual(WitnessState.Talking, bea.Reaction.State);
            Assert.AreEqual(CharacterClips.Talk, bea.Avatar.Loop);
            Assert.IsNull(bea.Avatar.OneShot);
            Assert.Greater(bea.Avatar.WeightOf(CharacterClips.Talk), 0.9f);
            Assert.AreEqual(1, bea.Reaction.WaveCount, "one wave per approach");

            // Pause freezes her.
            game.Flow.TogglePause();
            Assert.AreEqual(0f, Time.timeScale);
            var state = bea.Reaction.State;
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            Assert.AreEqual(state, bea.Reaction.State);
            game.Flow.TogglePause();

            // Leave: back to Idle after the delay.
            game.Pip.Teleport(home + toward * 12f + Vector3.up * 0.2f);
            yield return Wait(bea.Reaction.ReturnDelay + 0.8f);
            Assert.AreEqual(WitnessState.Idle, bea.Reaction.State);
            Assert.AreEqual(CharacterClips.Idle, bea.Avatar.Loop);
            Assert.Greater(bea.Avatar.WeightOf(CharacterClips.Idle), 0.9f);
        }
    }
}
