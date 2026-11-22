using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.World;
using SpookyStreet.Localization;
using SpookyStreet.Player;
using SpookyStreet.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// Collision audit in the real scene (owner feedback, TestFlight 13.1): every placed model the audit lists as
    /// solid has colliders where its meshes are; Pip, driven with the gamepad stick, is stopped by a sample of
    /// them (never ends up inside one) or stands on top; the treehouse deck carries her at 2.4 m and bumps her
    /// from below; and running, chain-jumping and gliding at every side of the block (and off the owl house roof)
    /// keeps her inside the boundary walls, over ground, with no safe-ground return.
    /// </summary>
    public class CollisionTests : InputTestFixture
    {
        private const string SceneName = "LanternLane";

        private Keyboard _keyboard;
        private Gamepad _pad;
        private bool _jumpDown;
        private float _jumpPressedAt;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _pad = InputSystem.AddDevice<Gamepad>();
            Localizer.ResetForTests();
            CaseDirector.UseTemporarySaveDirectory(); // no save leaks between tests (S002)
            _jumpDown = false;
        }

        private static IEnumerator StartGame()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            var game = GameBootstrap.Instance;
            Assert.IsNotNull(game, "scene did not create GameBootstrap");
            game.Ui.NewGameButton.onClick.Invoke();
            for (int i = 0; i < 10; i++)
            {
                yield return null;
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

        /// <summary>Pushes the left stick so Pip runs along world <paramref name="direction"/> (camera-relative input).</summary>
        private void Steer(Vector3 direction)
        {
            var camera = GameBootstrap.Instance.CameraRig.OutputCamera.transform;
            Vector3 forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            direction.y = 0f;
            direction.Normalize();
            var stick = new Vector2(Vector3.Dot(direction, right), Vector3.Dot(direction, forward));
            Set(_pad.leftStick, stick.sqrMagnitude > 1e-4f ? stick.normalized : Vector2.zero);
        }

        private void StopSteering() => Set(_pad.leftStick, Vector2.zero);

        /// <summary>Skips any case line a probe happens to trigger, so the stick keeps driving Pip.</summary>
        private static void KeepPlaying()
        {
            var dialogue = GameBootstrap.Instance.Director.Dialogue;
            if (dialogue.IsOpen)
            {
                dialogue.Skip();
            }
        }

        /// <summary>
        /// Held jumps chained on every landing (2.1 → 2.4 → 2.8 m), the cape spread while falling (Jump stays held).
        /// </summary>
        private void JumpEveryLanding(PipMotor pip)
        {
            if (pip.Grounded && !_jumpDown)
            {
                Press(_keyboard.spaceKey, queueEventOnly: true);
                _jumpDown = true;
                _jumpPressedAt = Time.time;
            }
            else if (pip.Grounded && _jumpDown && Time.time - _jumpPressedAt > 0.2f)
            {
                Release(_keyboard.spaceKey, queueEventOnly: true);
                _jumpDown = false;
            }
        }

        private void ReleaseJump()
        {
            if (_jumpDown)
            {
                Release(_keyboard.spaceKey, queueEventOnly: true);
                _jumpDown = false;
            }
        }

        private static Transform FindInstance(Transform root, string model, Vector3 at)
        {
            Transform best = null;
            float bestDistance = 0.1f;
            foreach (var t in root.GetComponentsInChildren<Transform>(true))
            {
                if (t.name != model)
                {
                    continue;
                }

                float d = Vector3.Distance(t.position, at);
                if (d < bestDistance)
                {
                    best = t;
                    bestDistance = d;
                }
            }

            return best;
        }

        private static List<Collider> SolidColliders(Transform instance)
        {
            var list = new List<Collider>();
            foreach (var c in instance.GetComponentsInChildren<Collider>(true))
            {
                if (!c.isTrigger && c.enabled)
                {
                    list.Add(c);
                }
            }

            return list;
        }

        [UnityTest]
        public IEnumerator EverySolidPlacedModelHasCollidersOnItsMeshes()
        {
            yield return StartGame();
            var env = GameBootstrap.Instance.Environment;
            if (!env.UsesModels)
            {
                Assert.Ignore("Environment models are not committed; the greybox keeps its own colliders.");
            }

            var streetSet = new HashSet<string>(EnvironmentLayout.StreetSetModels);
            var offenders = new List<string>();
            int checkedCount = 0;
            foreach (var p in CollisionRules.PlacedModels())
            {
                if (streetSet.Contains(p.Model) && !env.UsesStreetSet)
                {
                    continue;
                }

                CollisionRules.TryGet(p.Model, out var recipe);
                if (!CollisionRules.IsSolid(p))
                {
                    continue;
                }

                if (recipe != null && !string.IsNullOrEmpty(recipe.CoveredBy))
                {
                    var cover = GameObject.Find(recipe.CoveredBy);
                    if (cover == null || !cover.TryGetComponent<Collider>(out _))
                    {
                        offenders.Add($"{p.Model}: covering collider {recipe.CoveredBy} missing");
                    }

                    continue;
                }

                var instance = FindInstance(env.Root, p.Model, new Vector3(p.X, p.Y, p.Z));
                if (instance == null)
                {
                    offenders.Add($"{p.Model} at ({p.X}, {p.Z}) not spawned");
                    continue;
                }

                var colliders = SolidColliders(instance);
                int expected = recipe != null ? recipe.Parts.Length : 1;
                if (colliders.Count < expected)
                {
                    offenders.Add($"{p.Model} at ({p.X}, {p.Z}): {colliders.Count} colliders, expected {expected}");
                    continue;
                }

                Bounds drawn = ModelKit.RendererBounds(instance.gameObject);
                drawn.Expand(0.6f);
                foreach (var c in colliders)
                {
                    Bounds b = c.bounds;
                    if (b.size.x < 0.05f || b.size.y < 0.05f || b.size.z < 0.05f)
                    {
                        offenders.Add($"{p.Model}/{c.name}: degenerate collider {b.size}");
                    }
                    else if (!drawn.Contains(b.center))
                    {
                        offenders.Add($"{p.Model}/{c.name}: collider at {b.center} is away from the model {drawn}");
                    }
                }

                checkedCount++;
            }

            Debug.Log($"[CollisionTests] {checkedCount} solid placements checked");
            Assert.IsEmpty(offenders, string.Join("\n", offenders));
            Assert.Greater(checkedCount, 30);
        }

        /// <summary>A solid placement to drive Pip into: which instance, where Pip starts and which way she runs.</summary>
        private readonly struct Probe
        {
            public Probe(string model, float x, float z, Vector3 start, Vector3 direction, float seconds = 1.6f)
            {
                Model = model;
                At = new Vector2(x, z);
                Start = start;
                Direction = direction;
                Seconds = seconds;
            }

            public string Model { get; }
            public Vector2 At { get; }
            public Vector3 Start { get; }
            public Vector3 Direction { get; }
            public float Seconds { get; }
        }

        private static readonly Probe[] Probes =
        {
            // Porch probes first: Pip starts on the arrival porch, so staying on it never replays the arrival beat.
            new Probe(EnvironmentLayout.KitRail, -1.95f, -14f, new Vector3(-0.6f, 0.7f, -14.4f), Vector3.left),
            new Probe(EnvironmentLayout.Crate, -1.4f, -13.45f, new Vector3(0.1f, 0.7f, -13.45f), Vector3.left),
            new Probe(EnvironmentLayout.Mailbox, -1.45f, -10.6f, new Vector3(0.15f, 0.1f, -10.6f), Vector3.left),
            new Probe(EnvironmentLayout.StreetLamp, 2.2f, -9.6f, new Vector3(0.6f, 0.1f, -9.65f), Vector3.right),
            new Probe(EnvironmentLayout.JackOLantern, -1.5f, -8.2f, new Vector3(0.1f, 0.1f, -8.2f), Vector3.left),
            new Probe(EnvironmentLayout.Pumpkin, 1.45f, -6.9f, new Vector3(-0.15f, 0.1f, -6.9f), Vector3.right),
            new Probe(EnvironmentLayout.PartyTable, 4.6f, -2.6f, new Vector3(4.6f, 0.1f, -4.6f), Vector3.forward),
            new Probe(EnvironmentLayout.Bench, EnvironmentLayout.BenchX, EnvironmentLayout.BenchZ,
                new Vector3(EnvironmentLayout.BenchX, 0.1f, EnvironmentLayout.BenchZ - 2f), Vector3.forward),
            new Probe(EnvironmentLayout.Maple, -4.5f, 2f, new Vector3(-4.45f, 0.1f, 0.2f), Vector3.forward),
            new Probe(EnvironmentLayout.Maple, -9.5f, 12f, new Vector3(-9.45f, 0.1f, 9.8f), Vector3.forward, 2f),
            new Probe(EnvironmentLayout.FenceSection, 3.8f, EnvironmentLayout.FrontYardZ, new Vector3(3.8f, 0.1f, 3.2f),
                Vector3.forward),
            new Probe(EnvironmentLayout.FenceGate, -0.1f, EnvironmentLayout.FrontYardZ, new Vector3(0.45f, 0.1f, 3.3f),
                Vector3.forward),
            new Probe(EnvironmentLayout.Hedge, -1.97f, EnvironmentLayout.FrontYardZ, new Vector3(-2.2f, 0.1f, 3.2f),
                Vector3.forward),
        };

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator PipIsStoppedBySolidProps()
        {
            yield return StartGame();
            var game = GameBootstrap.Instance;
            var env = game.Environment;
            if (!env.UsesModels)
            {
                Assert.Ignore("Environment models are not committed.");
            }

            var pip = game.Pip;
            var controller = pip.GetComponent<CharacterController>();
            var failures = new List<string>();
            foreach (var probe in Probes)
            {
                Transform instance = null;
                foreach (var p in CollisionRules.PlacedModels())
                {
                    if (p.Model == probe.Model && Mathf.Abs(p.X - probe.At.x) < 0.05f && Mathf.Abs(p.Z - probe.At.y) < 0.05f)
                    {
                        instance = FindInstance(env.Root, p.Model, new Vector3(p.X, p.Y, p.Z));
                    }
                }

                Assert.IsNotNull(instance, $"{probe.Model} at {probe.At} not found");
                var colliders = SolidColliders(instance);
                Assert.IsNotEmpty(colliders, probe.Model + " has no collider");
                int returns = pip.SafeReturns;
                pip.Teleport(probe.Start);
                pip.transform.rotation = Quaternion.LookRotation(probe.Direction, Vector3.up);
                yield return Wait(0.3f);

                float closest = float.MaxValue;
                string inside = null;
                float until = Time.time + probe.Seconds;
                while (Time.time < until)
                {
                    KeepPlaying();
                    Steer(probe.Direction);
                    yield return null;
                    Measure(pip, controller, colliders, ref closest, ref inside);
                }

                StopSteering();
                yield return Wait(0.2f);
                string what = $"{probe.Model} at {probe.At}";
                if (inside != null)
                {
                    failures.Add($"{what}: Pip passed into {inside}");
                }
                else if (closest > controller.radius + 0.15f)
                {
                    failures.Add($"{what}: Pip never reached it (closest {closest:F2} m, at {pip.transform.position})");
                }

                if (pip.SafeReturns != returns)
                {
                    failures.Add($"{what}: safe-ground return");
                }

                Debug.Log($"[CollisionTests] {what}: closest {closest:F2} m, stopped at {pip.transform.position}");
            }

            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        /// <summary>Closest approach of Pip's capsule axis to the colliders, and the first collider it got inside.</summary>
        private static void Measure(PipMotor pip, CharacterController controller, List<Collider> colliders,
            ref float closest, ref string inside)
        {
            Vector3 feet = pip.transform.position;
            float r = controller.radius;
            var axis = new[] { feet + Vector3.up * r, feet + Vector3.up * (controller.height * 0.5f),
                feet + Vector3.up * (controller.height - r) };
            foreach (var c in colliders)
            {
                foreach (var p in axis)
                {
                    Vector3 q = c.ClosestPoint(p);
                    float d = Vector3.Distance(p, q);
                    if (d < 1e-4f && inside == null)
                    {
                        inside = c.name;
                    }

                    closest = Mathf.Min(closest, d);
                }
            }
        }

        private static Collider Part(Transform instance, string part)
        {
            var t = ModelKit.FindDeep(instance, ModelColliders.PartPrefix + part);
            Assert.IsNotNull(t, $"{instance.name} has no collider part {part}");
            return t.GetComponent<Collider>();
        }

        private static Transform Treehouse()
        {
            var env = GameBootstrap.Instance.Environment;
            foreach (var p in EnvironmentLayout.Street)
            {
                if (p.Model == EnvironmentLayout.TreehousePlatform)
                {
                    return FindInstance(env.Root, p.Model, new Vector3(p.X, p.Y, p.Z));
                }
            }

            return null;
        }

        /// <summary>A point on (or under) the deck, given in the deck's own frame from its centre.</summary>
        public static Vector3 OnDeck(Collider deck, float localX, float localZ, float y)
        {
            Vector3 p = deck.transform.TransformPoint(new Vector3(localX, 0f, localZ));
            return new Vector3(p.x, y, p.z);
        }

        [UnityTest]
        public IEnumerator TreehouseDeckCarriesPipAndBumpsHerFromBelow()
        {
            yield return StartGame();
            var game = GameBootstrap.Instance;
            if (!game.Environment.UsesModels)
            {
                Assert.Ignore("Environment models are not committed.");
            }

            var pip = game.Pip;
            var controller = pip.GetComponent<CharacterController>();
            var treehouse = Treehouse();
            Assert.IsNotNull(treehouse, "treehouse platform not spawned");
            var deck = Part(treehouse, "Deck");
            var rail = Part(treehouse, "RailFront");
            float top = deck.bounds.max.y;
            Assert.AreEqual(CollisionRules.DeckTop, top, 0.05f, "the deck floor stands at 2.4 m");

            // Standing on the deck (inside where the cabin goes, clear of the trunk and its walls).
            pip.Teleport(OnDeck(deck, 0.75f, 0.6f, top + 0.15f));
            yield return Wait(0.6f);
            Assert.IsTrue(pip.Grounded, $"Pip should stand on the deck ({pip.transform.position})");
            Assert.AreEqual(top, pip.transform.position.y, 0.08f, "standing on the deck floor");

            // Walking into the front rail keeps her on the deck.
            Vector3 towardRail = deck.transform.TransformDirection(Vector3.back);
            var railOnly = new List<Collider> { rail };
            float closest = float.MaxValue;
            string inside = null;
            float lowest = float.MaxValue;
            float until = Time.time + 1.5f;
            while (Time.time < until)
            {
                KeepPlaying();
                Steer(towardRail);
                yield return null;
                Measure(pip, controller, railOnly, ref closest, ref inside);
                lowest = Mathf.Min(lowest, pip.transform.position.y);
            }

            StopSteering();
            Assert.IsNull(inside, "Pip passed into the treehouse rail");
            Assert.Greater(lowest, top - 0.1f, "Pip fell off the deck through the rail");
            Assert.AreEqual(0, pip.SafeReturns);

            // Under the deck, a held jump bumps the deck's underside instead of passing up through it.
            pip.Teleport(OnDeck(deck, 0.95f, 0f, 0.1f));
            yield return Wait(0.4f);
            Assert.IsTrue(pip.Grounded, $"Pip should stand under the deck ({pip.transform.position})");
            var deckOnly = new List<Collider> { deck };
            inside = null;
            float highestHead = 0f;
            Press(_keyboard.spaceKey, queueEventOnly: true);
            until = Time.time + 1.2f;
            while (Time.time < until)
            {
                yield return null;
                Measure(pip, controller, deckOnly, ref closest, ref inside);
                highestHead = Mathf.Max(highestHead, pip.transform.position.y + controller.height);
            }

            Release(_keyboard.spaceKey, queueEventOnly: true);
            yield return Wait(1f);
            Assert.IsNull(inside, "Pip jumped up into the treehouse deck");
            Assert.LessOrEqual(highestHead, deck.bounds.min.y + 0.05f, "Pip's head passed the deck's underside");
            Assert.Greater(highestHead, deck.bounds.min.y - 0.3f, "the held jump should reach the deck");
            Assert.Less(pip.transform.position.y, 0.3f, "Pip lands back on the ground under the deck");
            Assert.AreEqual(0, pip.SafeReturns);
        }

        /// <summary>From inside the block toward each side: start point and running direction.</summary>
        private static readonly (string Name, Vector3 Start, Vector3 Direction)[] Sides =
        {
            ("north", new Vector3(8f, 0.1f, 13.4f), Vector3.forward),
            ("south", new Vector3(8f, 0.1f, -13.4f), Vector3.back),
            ("east", new Vector3(17.4f, 0.1f, -6f), Vector3.right),
            ("west", new Vector3(-17.4f, 0.1f, -6f), Vector3.left),
            ("north corner", new Vector3(17.6f, 0.1f, 13.6f), new Vector3(1f, 0f, 1f)),
            ("owl house roof", new Vector3(0f, 6.7f, 16f), Vector3.forward),
        };

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator BoundaryHoldsAgainstRunningChainJumpsAndGlides()
        {
            yield return StartGame();
            var game = GameBootstrap.Instance;
            var pip = game.Pip;
            var controller = pip.GetComponent<CharacterController>();
            int probeMask = Physics.DefaultRaycastLayers & ~(1 << pip.gameObject.layer);
            float limitX = CollisionRules.WallInnerHalfX - controller.radius + 0.05f;
            float limitZ = CollisionRules.WallInnerHalfZ - controller.radius + 0.05f;
            var failures = new List<string>();
            foreach (var side in Sides)
            {
                pip.Teleport(side.Start);
                pip.transform.rotation = Quaternion.LookRotation(side.Direction, Vector3.up);
                yield return Wait(0.4f);
                int returns = pip.SafeReturns;
                bool glided = false;
                float highest = 0f;
                string escape = null;
                float until = Time.time + 6f;
                while (Time.time < until)
                {
                    KeepPlaying();
                    Steer(side.Direction);
                    JumpEveryLanding(pip);
                    yield return null;
                    Vector3 at = pip.transform.position;
                    glided |= pip.Gliding;
                    highest = Mathf.Max(highest, at.y);
                    if (escape == null && (Mathf.Abs(at.x) > limitX || Mathf.Abs(at.z) > limitZ))
                    {
                        escape = $"left the block at {at}";
                    }

                    if (escape == null && !Physics.Raycast(at + Vector3.up * 0.1f, Vector3.down, 60f, probeMask,
                            QueryTriggerInteraction.Ignore))
                    {
                        escape = $"no ground under {at}";
                    }
                }

                StopSteering();
                ReleaseJump();
                float landBy = Time.time + 6f;
                while (!pip.Grounded && Time.time < landBy)
                {
                    yield return null;
                }

                Vector3 end = pip.transform.position;
                Debug.Log($"[CollisionTests] boundary {side.Name}: highest {highest:F2} m, glided {glided}, end {end}, " +
                          $"returns {pip.SafeReturns - returns}");
                if (escape != null)
                {
                    failures.Add($"{side.Name}: {escape}");
                }

                if (pip.SafeReturns != returns)
                {
                    failures.Add($"{side.Name}: triggered a safe-ground return");
                }

                if (!pip.Grounded)
                {
                    failures.Add($"{side.Name}: never landed ({end})");
                }

                if (!glided)
                {
                    failures.Add($"{side.Name}: the run never spread the cape (the test lost its glide)");
                }
            }

            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [UnityTest]
        public IEnumerator BoundaryHedgeTopsAreStandableFromInside()
        {
            yield return StartGame();
            var game = GameBootstrap.Instance;
            var pip = game.Pip;
            foreach (var at in new[] { new Vector3(6f, 2.2f, EnvironmentLayout.HedgeLineZ),
                         new Vector3(EnvironmentLayout.HedgeLineX, 2.2f, -6f) })
            {
                pip.Teleport(at);
                yield return Wait(0.8f);
                Assert.IsTrue(pip.Grounded, $"Pip should stand on the hedge top at {pip.transform.position}");
                Assert.That(pip.transform.position.y, Is.InRange(1.15f, 1.45f), "on the hedge top, not inside it");
            }

            Assert.AreEqual(0, pip.SafeReturns);
        }
    }
}
