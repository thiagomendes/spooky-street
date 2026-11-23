using System.Collections.Generic;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Movement;
using SpookyStreet.App;
using SpookyStreet.Core.World;
using SpookyStreet.Player;
using SpookyStreet.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpookyStreet.Case
{
    /// <summary>
    /// The interior half's set (S002 interior wiring, house v3): spawns the treehouse-route models, the owl house
    /// interior and the workshop mechanism at <see cref="CaseRouteLayout.Models"/> and builds every authored collider
    /// from <see cref="CaseRouteLayout.Slabs"/> (walk slabs on the SafeGround layer).
    /// <para>D24.1: the exterior <c>OwlHouse</c> keeps street scale, stays whole and solid, and shows lit window cards;
    /// the rooms are the separate, closed <c>OwlHouseInterior</c> up the lane, reached through the thresholds
    /// (<see cref="ThresholdTravel"/>). The interior (with the workshop mechanism and its lamps) is drawn only while Pip
    /// or the camera is in it, or while a threshold crossing needs it; a shadow shroud keeps the moon out of its closed
    /// rooms. Nothing is cut away during play: the attic is always drawn; only the workshop's near wall hides, for the
    /// grille peek and the crank cutaway.</para>
    /// Owns the front door: the exterior leaf (90° at most, art note), the inner leaf (100°) and the latch.
    /// </summary>
    public sealed class OwlHouseStage : MonoBehaviour
    {
        /// <summary>The exterior leaf swings into its shallow view box; past 90° it would pass through the box's side.</summary>
        public const float DoorOpenDegrees = 90f;

        public const float InnerDoorOpenDegrees = InteriorCamera.InnerDoorOpenDegrees;
        public const float LatchLiftDegrees = 38f;
        public const float LatchSeconds = 0.3f;
        public const float DoorSwingSeconds = 0.9f;

        private readonly List<Light> _interiorLights = new List<Light>();
        private GameObject _nookLamp;
        private Transform _body, _recess, _frontDoor, _innerDoor, _latch, _attic, _nookWall, _windowPane, _shroud;
        private Quaternion _frontRest, _innerRest, _latchRest;
        private Vector3 _latchAxis = Vector3.forward;
        private float _doorSign = -1f;
        private float _door, _doorTarget;
        private float _latchLift, _latchTarget;
        private GameObject _doorBlocker;
        private int _safeLayer;
        private bool _nookOverride;
        private bool _interiorShown = true;

        public Transform Interior { get; private set; }
        public Transform Mechanism { get; private set; }
        public Transform ClothSnag { get; private set; }
        public Transform SillSnag { get; private set; }

        /// <summary>True while the interior is drawn this frame.</summary>
        public bool InteriorShown => _interiorShown;

        /// <summary>Keeps the interior drawn whatever Pip and the camera do (captures and tests).</summary>
        public bool ForceInteriorVisible { get; set; }

        public bool DoorOpen => _doorTarget > 0.5f;

        /// <summary>Degrees the exterior front door is open.</summary>
        public float DoorAngle => _door * DoorOpenDegrees;

        /// <summary>Degrees the inner door is open.</summary>
        public float InnerDoorAngle => _door * InnerDoorOpenDegrees;

        public bool LatchLifted => _latchTarget > 0.5f;

        public int ModelCount { get; private set; }

        public int ColliderCount { get; private set; }

        public Transform FrontDoor => _frontDoor;
        public Transform InnerDoor => _innerDoor;
        public Transform Latch => _latch;
        public Transform Attic => _attic;
        public Transform NookWall => _nookWall;
        public Transform Body => _body;
        public Transform RecessCard => _recess;

        /// <summary>The ShadowsOnly box round the interior that keeps the moonlight out of its closed rooms.</summary>
        public Transform Shroud => _shroud;

        /// <summary>Hides the workshop's near wall (the grille look and the crank cutaway).</summary>
        public bool NookWallHidden
        {
            get => _nookOverride;
            set
            {
                _nookOverride = value;
                if (_nookWall != null)
                {
                    _nookWall.gameObject.SetActive(!value);
                }
            }
        }

        public static OwlHouseStage Create(Transform parent, EnvironmentDressing environment, MaterialLibrary materials)
        {
            var go = new GameObject("OwlHouseStage");
            go.transform.SetParent(parent, false);
            var stage = go.AddComponent<OwlHouseStage>();
            stage.Build(environment, materials);
            return stage;
        }

        private void Build(EnvironmentDressing environment, MaterialLibrary materials)
        {
            int safe = LayerMask.NameToLayer(SafeGroundTracker.SafeGroundLayerName);
            _safeLayer = safe >= 0 ? safe : 0;
            float yaw = environment != null ? environment.ModelYawCorrection : 0f;
            if (Mathf.Abs(yaw) > 0.1f)
            {
                Debug.LogWarning("[OwlHouseStage] Environment models were turned by " + yaw +
                                 " degrees; the interior layout assumes the house faces -Z.");
            }

            var kit = new ModelKit(materials, EnvironmentDressing.ModelFolder) { FallbackAtlas = ModelKit.DefaultFallbackAtlas };
            var routes = new ModelKit(materials, EnvironmentDressing.ModelFolder) { FallbackAtlas = CaseRouteLayout.RoutesAtlas };
            foreach (var placement in CaseRouteLayout.Models)
            {
                bool routeModel = placement.Model == CaseRouteLayout.ShedRamp || placement.Model == CaseRouteLayout.SillBridge ||
                                  placement.Model == CaseRouteLayout.TreehouseCabin || placement.Model == CaseRouteLayout.ClothSnag;
                var instance = (routeModel ? routes : kit).Spawn(placement.Model, transform,
                    new Vector3(placement.X, placement.Y, placement.Z), placement.Yaw + yaw, placement.Scale);
                if (instance == null)
                {
                    Debug.LogWarning("[OwlHouseStage] Missing model " + placement.Model);
                    continue;
                }

                ModelCount++;
                // The collision audit decides first (Crate, Awning); the rest keep their placement's kind or are
                // carried by the slabs below.
                if (CollisionRules.TryGet(placement.Model, out var recipe))
                {
                    ModelColliders.Apply(instance, recipe, placement.Yaw + yaw);
                }
                else if (placement.Collider == PlacementCollider.Bounds)
                {
                    ModelKit.AddBoundsCollider(instance);
                }

                switch (placement.Model)
                {
                    case CaseRouteLayout.OwlHouseInterior:
                        Interior = instance.transform;
                        break;
                    case CaseRouteLayout.WorkshopMechanism:
                        Mechanism = instance.transform;
                        break;
                    case CaseRouteLayout.ClothSnag:
                        ClothSnag = instance.transform;
                        break;
                }
            }

            var house = environment != null ? environment.House : null;
            _body = ModelKit.FindDeep(house, CaseRouteLayout.OwlHouseBody);
            _recess = ModelKit.FindDeep(house, CaseRouteLayout.DoorRecessCard);
            _frontDoor = ModelKit.FindDeep(house, EnvironmentLayout.FrontDoor);
            _windowPane = ModelKit.FindDeep(house, CaseRouteLayout.SideWindowPane);
            _innerDoor = ModelKit.FindDeep(Interior, CaseRouteLayout.InnerDoor);
            _latch = ModelKit.FindDeep(Interior, CaseRouteLayout.DoorLatch);
            _attic = ModelKit.FindDeep(Interior, CaseRouteLayout.Attic);
            _nookWall = ModelKit.FindDeep(Interior, CaseRouteLayout.NookWall);
            SillSnag = ModelKit.FindDeep(Interior, CaseRouteLayout.SillSnag);

            // June came in by her window: it stands open from the start, onto its painted attic.
            if (_windowPane != null)
            {
                _windowPane.gameObject.SetActive(false);
            }

            if (_frontDoor != null)
            {
                _frontRest = _frontDoor.rotation;
                // Inward: the leaf's free edge (east of the hinge) swings into the house (+Z). The inner leaf swings
                // into the hall the same way.
                Vector3 leaf = Vector3.right;
                _doorSign = (Quaternion.AngleAxis(45f, Vector3.up) * leaf).z > 0f ? 1f : -1f;
            }

            if (_innerDoor != null)
            {
                _innerRest = _innerDoor.rotation;
            }

            if (_latch != null)
            {
                // The latch is on the inside of the inner door: it rides with the leaf once lifted.
                if (_innerDoor != null)
                {
                    _latch.SetParent(_innerDoor, true);
                }

                _latchRest = _latch.localRotation;
                // Lifting turns the bar's free (east) end up about the door's normal, expressed in its parent's frame.
                _latchAxis = Quaternion.Inverse(_latch.parent.rotation) * Vector3.forward;
            }

            BuildColliders();
            BuildLights();
            BuildNookLamp(materials);
            BuildShroud(materials);
            LogParts();
            ShowInterior(false);
        }

        private void LogParts()
        {
            if (!Debug.isDebugBuild)
            {
                return;
            }

            string Where(Transform t) => t != null ? t.position.ToString("F2") : "missing";
            Debug.Log($"[OwlHouseStage] {ModelCount} models, {ColliderCount} colliders; body {Where(_body)}, " +
                      $"front door {Where(_frontDoor)}, window pane {Where(_windowPane)}, interior {Where(Interior)}, " +
                      $"inner door {Where(_innerDoor)}, latch {Where(_latch)}, attic {Where(_attic)}, " +
                      $"nook wall {Where(_nookWall)}, sill snag {Where(SillSnag)}, mechanism {Where(Mechanism)}, " +
                      $"cloth {Where(ClothSnag)}");
        }

        private void BuildColliders()
        {
            var root = new GameObject("CaseColliders").transform;
            root.SetParent(transform, false);
            foreach (var slab in CaseRouteLayout.Slabs)
            {
                if (slab.Group == SlabGroup.Reference)
                {
                    continue;
                }

                var go = SlabCollider(root, slab);
                go.layer = slab.Kind == SlabKind.Walk ? _safeLayer : 0;
                // S003: the shed's west side is the one marked slab (CollisionRules.TraversalMarks).
                SurfaceMarks marks = CollisionRules.MarksForSlab(slab.Name, out float faceX, out float faceZ);
                ClimbSurface.Mark(go, marks, new Vector3(faceX, 0f, faceZ));
                if (slab.Name == CaseRouteLayout.DoorBlockerName)
                {
                    _doorBlocker = go;
                }

                ColliderCount++;
            }
        }

        /// <summary>A box whose top face runs along the slab's line, <see cref="StageSlab.Depth"/> thick below it.</summary>
        public static GameObject SlabCollider(Transform parent, StageSlab slab)
        {
            var start = new Vector3(slab.StartX, slab.StartY, slab.StartZ);
            var end = new Vector3(slab.EndX, slab.EndY, slab.EndZ);
            Vector3 along = end - start;
            float length = along.magnitude;
            Vector3 forward = along / Mathf.Max(1e-4f, length);
            Vector3 right = Vector3.Cross(Vector3.up, forward).normalized;
            Vector3 up = Vector3.Cross(forward, right).normalized;
            var go = new GameObject(slab.Name, typeof(BoxCollider));
            go.transform.SetParent(parent, false);
            go.transform.SetPositionAndRotation((start + end) * 0.5f - up * (slab.Depth * 0.5f),
                Quaternion.LookRotation(forward, up));
            go.GetComponent<BoxCollider>().size = new Vector3(slab.Width, slab.Depth, length);
            return go;
        }

        private static Vector3 InteriorPoint(float x, float y, float depth) =>
            new Vector3(x, y, CaseRouteLayout.InteriorFrontZ + depth);

        private void BuildLights()
        {
            // The interior's practical lights (builder x and depth): the hall by the door and toward the stair, the stair
            // bay, June's corner, the back of the attic, and the workshop nook.
            AddLight("HallDoorGlow", InteriorPoint(0.2f, CaseRouteLayout.FloorY + 2.2f, 1.6f), 6.5f, 2.0f);
            AddLight("HallStairGlow", InteriorPoint(-0.9f, CaseRouteLayout.FloorY + 2.4f, 4.4f), 6.5f, 1.7f);
            AddLight("StairLamp", InteriorPoint(-2.85f, 3.4f, 6.2f), 5f, 1.5f);
            AddLight("JuneLamp", InteriorPoint(-1.3f, CaseRouteLayout.LoftY + 1.6f, 2.2f), 6f, 2.0f);
            AddLight("AtticBackLamp", InteriorPoint(1.4f, CaseRouteLayout.LoftY + 1.8f, 5.2f), 6f, 1.6f);
            AddLight("NookLamp", InteriorPoint(2.85f, 1.7f, 4.2f), 3.2f, 1.4f);
            // The treehouse cabin's lamp in the lookout bay (it is seamless on the maple: always on), so its closed
            // roof reads as boards from inside rather than as the night.
            CaseRouteLayout.CabinToWorld(CaseRouteLayout.CabinPlatX + 0.6f, CaseRouteLayout.CabinPlatY + 0.9f, out float cx,
                out float cz);
            AddLight("CabinLamp", new Vector3(cx, CaseRouteLayout.TreehouseY + 1.55f, cz), 3.2f, 1.3f, false);
            // The route lantern (exterior: always on).
            AddLight("BridgeLantern", new Vector3(CaseRouteLayout.RoofMaxX - 0.1f, 4.2f, CaseRouteLayout.BridgeZ + 0.05f), 6f,
                1.6f, false);
        }

        /// <summary>
        /// Where the workshop nook's hanging lamp bulb sits: over the nook's open side, in front of the mechanism, above
        /// the 1.6–2.6 m camera band (D24), so it lights the cam, follower and arm from the front and stays out of the
        /// cutaway's frame.
        /// </summary>
        public static Vector3 NookLampBulb => InteriorPoint(1.75f, 3.05f, 4.45f);

        /// <summary>
        /// The workshop nook's warm practical (S002 hints close): a small lamp hanging from the loft over the
        /// mechanism, with its own point light, so the top of the nook (the cam, the follower and the arm) reads in
        /// the crank cutaway. The shade glows at <see cref="GlowLevels.NookLamp"/>, far under the attic window, and the
        /// lamp hides with the interior.
        /// </summary>
        private void BuildNookLamp(MaterialLibrary materials)
        {
            Vector3 bulb = NookLampBulb;
            var lamp = new GameObject("NookHangingLamp").transform;
            lamp.SetParent(transform, false);
            lamp.position = bulb;
            _nookLamp = lamp.gameObject;
            float cordTop = CaseRouteLayout.LoftUnderside;
            float cordLength = Mathf.Max(0.05f, cordTop - (bulb.y + 0.16f));
            LampPart(lamp, PrimitiveType.Cylinder, "Cord", new Vector3(0f, 0.16f + cordLength * 0.5f, 0f),
                new Vector3(0.025f, cordLength * 0.5f, 0.025f), materials.Get(Palette.InkBlack));
            LampPart(lamp, PrimitiveType.Cylinder, "Shade", new Vector3(0f, 0.1f, 0f), new Vector3(0.26f, 0.07f, 0.26f),
                materials.Get(Palette.HauntedPurple));
            LampPart(lamp, PrimitiveType.Sphere, "Bulb", new Vector3(0f, -0.02f, 0f), Vector3.one * 0.15f,
                materials.Glow(Palette.PumpkinAmber, GlowLevels.NookLamp));
            // Just under the shade: it lights the mechanism's top from the front (the cutaway camera's side).
            AddLight("NookTopLamp", bulb + new Vector3(0f, -0.2f, 0f), 4.2f, 2.8f);
        }

        private static void LampPart(Transform parent, PrimitiveType type, string name, Vector3 local, Vector3 scale,
            Material material)
        {
            var part = GameObject.CreatePrimitive(type);
            part.name = name;
            var collider = part.GetComponent<Collider>();
            if (collider != null)
            {
                Destroy(collider);
            }

            part.transform.SetParent(parent, false);
            part.transform.localPosition = local;
            part.transform.localScale = scale;
            var renderer = part.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
        }

        private void AddLight(string name, Vector3 position, float range, float intensity, bool interior = true)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(transform, false);
            light.transform.position = position;
            light.type = LightType.Point;
            light.color = Palette.PumpkinAmber;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
            if (interior)
            {
                _interiorLights.Add(light);
            }
        }

        /// <summary>
        /// A box round the whole interior that only casts shadows: its walls and roof are single inward faces, which
        /// cast none, so without it the moon would light the attic floor through the closed roof.
        /// </summary>
        private void BuildShroud(MaterialLibrary materials)
        {
            if (Interior == null)
            {
                return;
            }

            var shroud = GameObject.CreatePrimitive(PrimitiveType.Cube);
            shroud.name = "InteriorMoonShroud";
            var collider = shroud.GetComponent<Collider>();
            if (collider != null)
            {
                DestroyImmediate(collider);
            }

            shroud.transform.SetParent(transform, false);
            shroud.transform.position = InteriorPoint(0f, 4.3f, 3.0f);
            shroud.transform.rotation = Quaternion.identity;
            shroud.transform.localScale = new Vector3(11.4f, 9.6f, 9.4f);
            var renderer = shroud.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.ShadowsOnly;
            renderer.receiveShadows = false;
            if (materials != null)
            {
                renderer.sharedMaterial = materials.Get(Palette.NightViolet);
            }

            _shroud = shroud.transform;
        }

        /// <summary>Instantly shows the door latched or open (resume, new game, replay).</summary>
        public void SetDoor(bool open)
        {
            _doorTarget = open ? 1f : 0f;
            _door = _doorTarget;
            _latchTarget = _doorTarget;
            _latchLift = _latchTarget;
            ApplyDoor();
        }

        /// <summary>Lift the latch, then swing both leaves open (the inner one into the hall, the outer into its box).</summary>
        public void OpenDoor()
        {
            _latchTarget = 1f;
            _doorTarget = 1f;
        }

        private void ApplyDoor()
        {
            float eased = _door * _door * (3f - 2f * _door);
            if (_frontDoor != null)
            {
                _frontDoor.rotation = Quaternion.AngleAxis(_doorSign * DoorOpenDegrees * eased, Vector3.up) * _frontRest;
            }

            if (_innerDoor != null)
            {
                _innerDoor.rotation = Quaternion.AngleAxis(_doorSign * InnerDoorOpenDegrees * eased, Vector3.up) * _innerRest;
            }

            if (_latch != null)
            {
                _latch.localRotation = Quaternion.AngleAxis(LatchLiftDegrees * _latchLift, _latchAxis) * _latchRest;
            }

            if (_doorBlocker != null)
            {
                _doorBlocker.SetActive(_door < 0.2f);
            }
        }

        /// <summary>Animates the door and shows the interior while Pip or the camera is in it (or a crossing needs it).</summary>
        public void Tick(Vector3 pip, Vector3 camera)
        {
            float dt = Time.deltaTime;
            if (dt > 0f)
            {
                if (Mathf.Abs(_latchLift - _latchTarget) > 1e-4f)
                {
                    _latchLift = Mathf.MoveTowards(_latchLift, _latchTarget, dt / LatchSeconds);
                    ApplyDoor();
                }
                else if (Mathf.Abs(_door - _doorTarget) > 1e-4f)
                {
                    _door = Mathf.MoveTowards(_door, _doorTarget, dt / DoorSwingSeconds);
                    ApplyDoor();
                }
            }

            ShowInterior(ForceInteriorVisible || CaseRouteLayout.Inside(pip.x, pip.z) ||
                         CaseRouteLayout.Inside(camera.x, camera.z));
        }

        private void ShowInterior(bool show)
        {
            if (show == _interiorShown)
            {
                return;
            }

            _interiorShown = show;
            if (Interior != null)
            {
                Interior.gameObject.SetActive(show);
            }

            if (Mechanism != null)
            {
                Mechanism.gameObject.SetActive(show);
            }

            foreach (var light in _interiorLights)
            {
                light.enabled = show;
            }

            if (_nookLamp != null)
            {
                _nookLamp.SetActive(show);
            }

            // D24: nothing is cut away during play. The attic is always drawn; the exterior is always whole.
            if (_attic != null && !_attic.gameObject.activeSelf)
            {
                _attic.gameObject.SetActive(true);
            }

            if (_body != null && !_body.gameObject.activeSelf)
            {
                _body.gameObject.SetActive(true);
            }
        }
    }
}
