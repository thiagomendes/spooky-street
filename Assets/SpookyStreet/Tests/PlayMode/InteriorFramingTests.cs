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
    /// The indoor camera in the real house (S002 interior polish): at sample places in the hall, the attic and on the
    /// stair, nothing solid lies between the lens and Pip's head and chest. "Solid" is every collider in the scene and
    /// every drawn mesh of the interior and the workshop mechanism (given temporary mesh colliders for the check, so the
    /// joists, rails and dressing that have no collider count too). Pip fills a reasonable share of the frame, and the
    /// character Pip is about to talk to stays in clear view as well.
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
            foreach (var root in new[] { house.Interior, house.Mechanism })
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

        /// <summary>Pip's height on screen as a fraction of the frame (0 when Pip is not in front of the lens).</summary>
        private static float ScreenShare(Camera camera, out bool headInFrame)
        {
            var renderers = Game.Pip.GetComponentsInChildren<Renderer>().Where(r => r.enabled).ToList();
            Vector3 pip = Game.Pip.transform.position;
            Vector3 head = camera.WorldToViewportPoint(pip + Vector3.up * InteriorCamera.HeadHeight);
            headInFrame = head.z > 0f && head.x > 0f && head.x < 1f && head.y > 0f && head.y < 1f;
            if (renderers.Count == 0)
            {
                return 0f;
            }

            var bounds = renderers[0].bounds;
            foreach (var r in renderers)
            {
                bounds.Encapsulate(r.bounds);
            }

            float min = float.MaxValue, max = float.MinValue;
            for (int i = 0; i < 8; i++)
            {
                var corner = new Vector3((i & 1) == 0 ? bounds.min.x : bounds.max.x, (i & 2) == 0 ? bounds.min.y : bounds.max.y,
                    (i & 4) == 0 ? bounds.min.z : bounds.max.z);
                var v = camera.WorldToViewportPoint(corner);
                if (v.z <= 0f)
                {
                    return 0f;
                }

                min = Mathf.Min(min, v.y);
                max = Mathf.Max(max, v.y);
            }

            return Mathf.Clamp01(max) - Mathf.Clamp01(min);
        }

        private static float YawToward(Vector3 from, Vector3 to) =>
            Mathf.Atan2(to.x - from.x, to.z - from.z) * Mathf.Rad2Deg;

        private static Vector3 OnStair(float x) =>
            new Vector3(x, InteriorCamera.StairSurface(x), (CaseRouteLayout.StairMinZ + CaseRouteLayout.StairMaxZ) * 0.5f);

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator NothingSolidBetweenTheLensAndPip()
        {
            yield return StartGame();
            var director = Game.Director;
            const float floor = CaseRouteLayout.FloorY;
            const float loft = CaseRouteLayout.LoftY;
            var june = new Vector3(CaseStage.JuneAttic.Position.X, CaseStage.JuneAttic.Position.Y, CaseStage.JuneAttic.Position.Z);
            var samples = new List<(string Name, Vector3 At, float Yaw)>
            {
                ("hall, facing the front door (23_foyer_door)", new Vector3(0.1f, floor, 13.2f), 180f),
                ("hall, by the bench facing the workshop", new Vector3(-2.0f, floor, 13.6f), 90f),
                ("hall, at the crank", new Vector3(0.75f, floor, 14.2f), 90f),
                ("hall, just inside the door facing in", new Vector3(0.05f, floor, 11.7f), 0f),
                ("hall, under the loft facing the stair", new Vector3(-0.6f, floor, 14.6f), 0f),
                ("attic, by June (19_attic_june)", new Vector3(-2.35f, loft, 13.85f),
                    YawToward(new Vector3(-2.35f, loft, 13.85f), june + new Vector3(1.1f, 0f, -0.45f))),
                ("attic, middle facing the window", new Vector3(0.2f, loft, 14.3f), 270f),
                ("attic, at the loft rail facing the hall", new Vector3(-0.4f, loft, 12.95f), 180f),
                ("attic, at the stairwell", new Vector3(-0.6f, loft, 15.3f), 90f),
                ("attic, in the alcove facing in", new Vector3(-2.5f, loft, CaseRouteLayout.WindowZ), 90f),
                ("stair, bottom going up", OnStair(-1.4f), 90f),
                ("stair, middle going up", OnStair(-0.5f), 90f),
                ("stair, top going up", OnStair(0.45f), 90f),
                ("stair, middle going down", OnStair(-0.2f), 270f),
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

                    float share = ScreenShare(camera, out bool headInFrame);
                    if (!headInFrame || share < MinScreenShare || share > MaxScreenShare)
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
            try
            {
                var camera = Game.CameraRig.OutputCamera;
                Vector3 june = cast.June.transform.position;
                foreach (var stand in new[] { new Vector3(-2.35f, CaseRouteLayout.LoftY, 13.85f), new Vector3(-1.6f, CaseRouteLayout.LoftY, 14.2f) })
                {
                    yield return StandAt(stand, YawToward(stand, june));
                    Assert.IsTrue(director.Targets.HasTarget && director.Targets.Current.Id == CaseStage.PointJuneAttic,
                        $"June is offered from {stand}");
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

            Assert.IsEmpty(failures, string.Join("\n", failures));
        }

        [UnityTest]
        public IEnumerator OutsideTheHouseTheCameraIsHandedBack()
        {
            yield return StartGame();
            yield return StandAt(new Vector3(0.1f, CaseRouteLayout.FloorY, 13.2f), 180f);
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
