using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Characters;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.World;
using SpookyStreet.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// The indoor camera in the real rooms (S002 interior polish, house v3): at sample places in the hall, the attic,
    /// the alcove and on the stair of the separate interior, and in the treehouse cabin, nothing solid lies between the
    /// lens and Pip's head and chest. "Solid" is every collider in the scene and every drawn mesh of the interior, the
    /// workshop mechanism and the cabin (given temporary mesh colliders for the check, so the beams, rails and dressing
    /// that have no collider count too). Pip fills a reasonable share of the frame, the character Pip is about to talk
    /// to stays in clear view as well, and the lens never leaves the room (so no frame shows the void). No drawn surface
    /// sits against the lens (S002 attic framing: a screen grid of rays finds the art within 0.4 m over at most 5 %).
    /// </summary>
    public class InteriorFramingTests : InputTestFixture
    {
        private const string SceneName = "LanternLane";

        /// <summary>Pip's height in the frame: big enough to read on a phone, never cropped by a lens in Pip's face.</summary>
        private const float MinScreenShare = 0.2f;

        private const float MaxScreenShare = 0.95f;

        private readonly List<MeshCollider> _probes = new List<MeshCollider>();

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            Localizer.ResetForTests();
            CaseDirector.UseTemporarySaveDirectory();
        }

        private static GameBootstrap Game => GameBootstrap.Instance;

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                yield return null;
            }
        }

        private static IEnumerator StartGame()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(GameBootstrap.Instance, "scene did not create GameBootstrap");
            if (Game.Environment == null || !Game.Environment.UsesModels || Game.Director.House == null)
            {
                Assert.Ignore("Environment models are not committed: the interior is not staged.");
            }

            Game.Ui.NewGameButton.onClick.Invoke();
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            Assert.AreEqual(GameState.Playing, Game.Flow.State);
        }

        /// <summary>Stand Pip at <paramref name="at"/> facing <paramref name="yaw"/> with the camera recentred behind.</summary>
        private static IEnumerator StandAt(Vector3 at, float yaw)
        {
            Game.Pip.Teleport(at + Vector3.up * 0.05f);
            Game.Pip.transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            Game.CameraRig.Orbit.RequestRecenter();
            // The yaw swing is at most 180° at 120°/s; then the short indoor damping settles.
            yield return Wait(2.0f);
        }

        /// <summary>Temporary mesh colliders on every drawn interior mesh, so sightlines see what the player sees.</summary>
        private void AddMeshProbes()
        {
            var house = Game.Director.House;
            Transform cabin = null;
            foreach (Transform child in house.transform)
            {
                if (child.name == CaseRouteLayout.TreehouseCabin)
                {
                    cabin = child;
                }
            }

            foreach (var root in new[] { house.Interior, house.Mechanism, cabin })
            {
                if (root == null)
                {
                    continue;
                }

                foreach (var filter in root.GetComponentsInChildren<MeshFilter>(true))
                {
                    if (filter.sharedMesh == null || filter.GetComponent<Collider>() != null)
                    {
                        continue;
                    }

                    var probe = filter.gameObject.AddComponent<MeshCollider>();
                    probe.sharedMesh = filter.sharedMesh;
                    probe.enabled = false;
                    _probes.Add(probe);
                }
            }

            Assert.Greater(_probes.Count, 0, "the interior has drawn meshes to probe");
        }

        private void RemoveMeshProbes()
        {
            foreach (var probe in _probes)
            {
                if (probe != null)
                {
                    Object.Destroy(probe);
                }
            }

            _probes.Clear();
        }

        /// <summary>Everything solid between <paramref name="from"/> and <paramref name="to"/> (characters excluded).</summary>
        private List<string> Between(Vector3 from, Vector3 to, Transform ignore)
        {
            foreach (var probe in _probes)
            {
                // Only drawn meshes count: a part the cut-away hides (the attic from a high camera) is not in the way.
                probe.enabled = probe.GetComponent<Renderer>() is Renderer r && r.enabled && r.gameObject.activeInHierarchy;
            }

            Physics.SyncTransforms();
            int mask = ~(1 << Game.Pip.gameObject.layer);
            Vector3 d = to - from;
            var hits = Physics.RaycastAll(from, d.normalized, d.magnitude, mask, QueryTriggerInteraction.Ignore);
            var names = new List<string>();
            foreach (var hit in hits)
            {
                var t = hit.collider.transform;
                if (t.IsChildOf(Game.Pip.transform) || (ignore != null && t.IsChildOf(ignore)) ||
                    t.GetComponentInParent<Witness>() != null)
                {
                    continue;
                }

                names.Add($"{t.name} at {hit.point:F2}");
            }

            foreach (var probe in _probes)
            {
                probe.enabled = false;
            }

            return names;
        }

        /// <summary>
        /// Pip's height on screen as a fraction of the frame: feet to the top of the head (1.1 m) projected through the
        /// lens (0 when Pip is behind it). Points, not renderer bounds, so a skinned mesh's loose bounds cannot skew it.
        /// </summary>
        private static float ScreenShare(Camera camera, out bool headInFrame)
        {
            Vector3 pip = Game.Pip.transform.position;
            Vector3 head = camera.WorldToViewportPoint(pip + Vector3.up * InteriorCamera.HeadHeight);
            Vector3 top = camera.WorldToViewportPoint(pip + Vector3.up * CaseRouteLayout.PipHeight);
            Vector3 feet = camera.WorldToViewportPoint(pip);
            headInFrame = head.z > 0f && head.x > 0f && head.x < 1f && head.y > 0f && head.y < 1f;
            if (top.z <= 0f || feet.z <= 0f)
            {
                return 0f;
            }

            return Mathf.Clamp01(top.y) - Mathf.Clamp01(feet.y);
        }

        /// <summary>Indoors, drawn surfaces within 0.4 m of the lens may cover at most this share of the screen.</summary>
        private const float MaxNearShare = 0.05f;

        /// <summary>The stair is a 1.5 m wide flight between walls: a little more of them may be close.</summary>
        private const float MaxNearShareStair = 0.15f;

        /// <summary>
        /// The share of a 24 x 12 grid of screen rays whose first drawn surface or collider (not Pip, not a character)
        /// is closer to the lens than <paramref name="within"/>.
        /// </summary>
        private float NearScreenShare(Camera camera, float within)
        {
            foreach (var probe in _probes)
            {
                probe.enabled = probe.GetComponent<Renderer>() is Renderer r && r.enabled && r.gameObject.activeInHierarchy;
            }

            Physics.SyncTransforms();
            int mask = ~(1 << Game.Pip.gameObject.layer);
            const int columns = 24;
            const int rows = 12;
            int covered = 0;
            for (int i = 0; i < columns; i++)
            {
                for (int k = 0; k < rows; k++)
                {
                    var ray = camera.ViewportPointToRay(new Vector3((i + 0.5f) / columns, (k + 0.5f) / rows, 0f));
                    foreach (var hit in Physics.RaycastAll(ray, within, mask, QueryTriggerInteraction.Ignore))
                    {
                        var t = hit.collider.transform;
                        if (t.IsChildOf(Game.Pip.transform) || t.GetComponentInParent<Witness>() != null)
                        {
                            continue;
                        }

                        covered++;
                        break;
                    }
                }
            }

            foreach (var probe in _probes)
            {
                probe.enabled = false;
            }

            return covered / (float)(columns * rows);
        }

        private static float YawToward(Vector3 from, Vector3 to) =>
            Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;

        private const float Ifz = CaseRouteLayout.InteriorFrontZ;

        private static Vector3 Hall(float x, float depth) => new Vector3(x, CaseRouteLayout.FloorY, Ifz + depth);

        private static Vector3 Attic(float x, float depth) => new Vector3(x, CaseRouteLayout.LoftY, Ifz + depth);

        private static Vector3 OnLowerFlight(float depth) =>
            new Vector3(-2.85f, CaseRouteLayout.LowerFlightAt(Ifz + depth), Ifz + depth);

        private static Vector3 OnUpperFlight(float x) =>
            new Vector3(x, CaseRouteLayout.UpperFlightAt(x), CaseRouteLayout.UpperFlightZ);

        private static Vector3 InCabin(float cx, float cy)
        {
            CaseRouteLayout.CabinToWorld(CaseRouteLayout.CabinPlatX + cx, CaseRouteLayout.CabinPlatY + cy, out float x,
                out float z);
            return new Vector3(x, CaseRouteLayout.TreehouseY, z);
        }

        /// <summary>The cabin's door faces the shed roof: world yaw of the cabin frame's +x and +y.</summary>
        private static float CabinYaw(float cx, float cy)
        {
            Vector3 a = InCabin(0f, 0f);
            Vector3 b = InCabin(cx, cy);
            return YawToward(a, b);
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator NothingSolidBetweenTheLensAndPip()
        {
            yield return StartGame();
            var director = Game.Director;
            const float floor = CaseRouteLayout.FloorY;
            var june = new Vector3(CaseStage.JuneAttic.Position.X, CaseStage.JuneAttic.Position.Y, CaseStage.JuneAttic.Position.Z);
            var samples = new List<(string Name, Vector3 At, float Yaw)>
            {
                ("hall, facing the front door (hall and door)", Hall(0f, 2.4f), 180f),
                ("hall, by the blanket chest facing the workshop", Hall(-2.4f, 1.6f), 90f),
                ("hall, at the crank", new Vector3(CaseRouteLayout.NookX - 0.7f, floor, CaseRouteLayout.MechanismZ + 0.1f), 90f),
                ("hall, at the door arrival facing in", Hall(Thresholds.DoorInside.ArriveX, 2.0f), 0f),
                ("hall, facing the stair foot", Hall(-1.2f, 2.0f), 315f),
                ("hall, coat alcove", Hall(1.2f, 6.0f), 0f),
                ("attic, by June (attic with June)", Attic(-0.4f, 2.4f),
                    YawToward(Attic(-0.4f, 2.4f), june)),
                ("attic, the dormer arrival facing in", Attic(-1.7f, 1.5f), 90f),
                ("attic, middle facing the window", Attic(0.6f, 3.2f), 270f),
                ("attic, at the stair rail", Attic(-1.2f, 4.4f), 270f),
                ("attic, at the stair top", Attic(0.6f, 6.3f), 180f),
                ("attic, in the alcove facing in", Attic(-3.0f, 1.5f), 90f),
                ("attic, by the trunk facing the window (TestFlight 23 frame)", Attic(-1.5f, 1.15f), 270f),
                ("stair, lower flight going up", OnLowerFlight(3.8f), 0f),
                ("stair, lower flight going down", OnLowerFlight(4.8f), 180f),
                ("stair, landing", new Vector3(-2.85f, CaseRouteLayout.LandingY, Ifz + 6.3f), 90f),
                ("stair, upper flight going up", OnUpperFlight(-1.2f), 90f),
                ("stair, upper flight going down", OnUpperFlight(-0.6f), 270f),
                ("cabin, by the door", InCabin(-0.75f, -0.57f), CabinYaw(1f, 0f)),
                ("cabin, in the bay", InCabin(0f, 1.35f), CabinYaw(0f, 1f)),
                ("cabin, behind the trunk", InCabin(0.6f, -0.6f), CabinYaw(-1f, 0f)),
            };

            AddMeshProbes();
            var failures = new List<string>();
            try
            {
                var camera = Game.CameraRig.OutputCamera;
                foreach (var sample in samples)
                {
                    yield return StandAt(sample.At, sample.Yaw);
                    Vector3 pip = Game.Pip.transform.position;
                    Vector3 lens = camera.transform.position;
                    string where = $"{sample.Name}: lens {lens:F2}, {director.Framing.Shot}";
                    Debug.Log("[InteriorFramingTests] " + where);
                    Assert.IsTrue(director.Framing.Active, $"{sample.Name}: the interior camera is in charge");
                    bool cabin = sample.Name.StartsWith("cabin", System.StringComparison.Ordinal);
                    if (!InteriorCamera.InAir(lens.x, lens.y, lens.z))
                    {
                        failures.Add($"{where}: the lens is outside the room's air ({(cabin ? "cabin" : "interior")})");
                    }

                    foreach (float h in new[] { InteriorCamera.HeadHeight, InteriorCamera.ChestHeight })
                    {
                        var hits = Between(lens, pip + Vector3.up * h, null);
                        if (hits.Count > 0)
                        {
                            failures.Add($"{where}: between the lens and Pip's {(h > 0.8f ? "head" : "chest")}: " +
                                         string.Join(", ", hits));
                        }
                    }

                    if (Physics.CheckSphere(lens, 0.1f, ~(1 << Game.Pip.gameObject.layer), QueryTriggerInteraction.Ignore))
                    {
                        failures.Add($"{where}: the lens is inside a collider");
                    }

                    // S002 attic framing: no drawn surface against the lens. Rays through a 24 x 12 grid of screen
                    // points find the art within 0.4 m over no more than a post's width (within 1.0 m is logged).
                    float near = NearScreenShare(camera, 0.4f);
                    Debug.Log($"[InteriorFramingTests] {sample.Name}: art within 0.4 m {near:P0}, within 1.0 m " +
                              $"{NearScreenShare(camera, 1.0f):P0} of the screen");
                    bool stair = sample.Name.StartsWith("stair", System.StringComparison.Ordinal);
                    if (!cabin && near > (stair ? MaxNearShareStair : MaxNearShare))
                    {
                        failures.Add($"{where}: the art is within 0.4 m of the lens over {near:P0} of the screen");
                    }

                    float share = ScreenShare(camera, out bool headInFrame);
                    // In the child-sized cabin the 1.0–1.2 m boom frames Pip nearly full height; the head stays in frame.
                    float most = cabin ? 1.01f : MaxScreenShare;
                    if (!headInFrame || share < MinScreenShare || share > most)
                    {
                        failures.Add($"{where}: Pip fills {share:P0} of the frame height (head in frame: {headInFrame})");
                    }
                }
            }
            finally
            {
                RemoveMeshProbes();
            }

            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator JuneStaysInViewWhenPipCanTalkToHer()
        {
            yield return StartGame();
            var director = Game.Director;
            var cast = director.Cast;
            if (cast == null || cast.June == null)
            {
                Assert.Ignore("June's model is not committed.");
            }

            AddMeshProbes();
            var failures = new List<string>();
            int checkedStands = 0;
            try
            {
                var camera = Game.CameraRig.OutputCamera;
                Vector3 june = cast.June.transform.position;
                foreach (var stand in new[] { Attic(-0.4f, 2.4f), Attic(-0.4f, 3.4f), Attic(-1.7f, 1.6f) })
                {
                    yield return StandAt(stand, YawToward(stand, june));
                    if (!director.Targets.HasTarget || director.Targets.Current.Id != CaseStage.PointJuneAttic)
                    {
                        // From near the window the sill snag can be the nearer target; then June is not the focus.
                        Debug.Log($"[InteriorFramingTests] June not offered from {stand}: {director.Targets.Current?.Id}");
                        continue;
                    }

                    checkedStands++;
                    Vector3 lens = camera.transform.position;
                    var hits = Between(lens, june + Vector3.up * InteriorCamera.FocusHeight, cast.June.transform);
                    if (hits.Count > 0)
                    {
                        failures.Add($"from {stand}, lens {lens:F2} ({director.Framing.Shot}): June hidden by {string.Join(", ", hits)}");
                    }

                    Vector3 v = camera.WorldToViewportPoint(june + Vector3.up * InteriorCamera.FocusHeight);
                    if (v.z <= 0f || v.x < 0f || v.x > 1f || v.y < 0f || v.y > 1f)
                    {
                        failures.Add($"from {stand}: June is out of frame ({v:F2})");
                    }
                }
            }
            finally
            {
                RemoveMeshProbes();
            }

            Assert.Greater(checkedStands, 0, "June is offered from at least one stand");
            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [UnityTest]
        public IEnumerator OutsideTheHouseTheCameraIsHandedBack()
        {
            yield return StartGame();
            yield return StandAt(Hall(0f, 2.4f), 180f);
            Assert.IsTrue(Game.Director.Framing.Active);
            Assert.IsTrue(Game.CameraRig.Indoors);
            Game.Pip.Teleport(new Vector3(0f, 0.7f, -6f));
            yield return Wait(0.5f);
            Assert.IsFalse(Game.Director.Framing.Active, "outside, the follow camera is the player's again");
            Assert.IsFalse(Game.CameraRig.Indoors);
            Assert.IsNull(Game.CameraRig.PitchOverride);
            Assert.IsNull(Game.CameraRig.DistanceLimit);
        }
    }
}
