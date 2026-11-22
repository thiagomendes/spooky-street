using System.Collections.Generic;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.World;
using SpookyStreet.Player;
using SpookyStreet.World;
using UnityEngine;

namespace SpookyStreet.Case
{
    /// <summary>
    /// The interior half's set (S002 interior wiring): spawns the treehouse-route models, the owl house interior and
    /// the workshop mechanism at <see cref="CaseRouteLayout.Models"/>, builds every authored collider from
    /// <see cref="CaseRouteLayout.Slabs"/> (walk slabs on the SafeGround layer), and each frame applies the cut-away
    /// rule (<see cref="InteriorVisibility"/>): inside, or at the open door or June's window, the exterior body and
    /// the door's dark recess card hide and the interior shows; the attic hides from a high camera while Pip is
    /// downstairs. While Pip is inside, the shell's walls move to the Ignore Raycast layer so the camera boom passes
    /// out through them and looks in through the culled near walls. Owns the front door (both leaves and the latch)
    /// and the interior's practical lights.
    /// </summary>
    public sealed class OwlHouseStage : MonoBehaviour
    {
        public const float DoorOpenDegrees = 100f;
        public const float LatchLiftDegrees = 38f;
        public const float LatchSeconds = 0.3f;
        public const float DoorSwingSeconds = 0.9f;

        private readonly List<GameObject> _cameraClearWalls = new List<GameObject>();
        private readonly List<Light> _interiorLights = new List<Light>();
        private Transform _body, _recess, _frontDoor, _innerDoor, _latch, _attic, _nookWall, _windowPane;
        private Quaternion _frontRest, _innerRest, _latchRest;
        private Vector3 _latchAxis = Vector3.forward;
        private float _doorSign = -1f;
        private float _door, _doorTarget;
        private float _latchLift, _latchTarget;
        private GameObject _doorBlocker;
        private int _wallLayer;
        private int _ignoreLayer;
        private int _safeLayer;
        private bool _wallsClear;
        private bool _nookOverride;

        public Transform Interior { get; private set; }
        public Transform Mechanism { get; private set; }
        public Transform ClothSnag { get; private set; }
        public Transform SillSnag { get; private set; }

        /// <summary>The cut-away state applied this frame.</summary>
        public InteriorView View { get; private set; }

        public bool DoorOpen => _doorTarget > 0.5f;

        /// <summary>Degrees the front door is open (both leaves).</summary>
        public float DoorAngle => _door * DoorOpenDegrees;

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

        /// <summary>
        /// Keeps the exterior body drawn even while the interior shows (the crank cutaway: its inset looks at the owl on
        /// the roof from the street, while the nook camera inside sees through the body's outward-facing walls).
        /// </summary>
        public bool ForceBodyVisible { get; set; }

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
            _wallLayer = 0;
            _ignoreLayer = LayerMask.NameToLayer("Ignore Raycast");
            if (_ignoreLayer < 0)
            {
                _ignoreLayer = 2;
            }

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

            // June came in by her window: it stands open from the start.
            if (_windowPane != null)
            {
                _windowPane.gameObject.SetActive(false);
            }

            if (_frontDoor != null)
            {
                _frontRest = _frontDoor.rotation;
                // Inward: the leaf's free edge (east of the hinge) swings into the house (+Z).
                Vector3 leaf = Vector3.right;
                _doorSign = (Quaternion.AngleAxis(45f, Vector3.up) * leaf).z > 0f ? 1f : -1f;
            }

            if (_innerDoor != null)
            {
                _innerRest = _innerDoor.rotation;
            }

            if (_latch != null)
            {
                // The latch is on the inside of the door: it rides with the leaf once lifted.
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
            LogParts();
            SetView(new InteriorView(false, false));
        }

        private void LogParts()
        {
            if (!Debug.isDebugBuild)
            {
                return;
            }

            string Where(Transform t) => t != null ? t.position.ToString("F2") : "missing";
            Debug.Log($"[OwlHouseStage] {ModelCount} models, {ColliderCount} colliders; body {Where(_body)}, " +
                      $"recess {Where(_recess)}, front door {Where(_frontDoor)}, window pane {Where(_windowPane)}, " +
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
                go.layer = slab.Kind == SlabKind.Walk ? _safeLayer : _wallLayer;
                if (slab.Kind == SlabKind.Wall && (slab.Group == SlabGroup.Shell || slab.Group == SlabGroup.Interior))
                {
                    _cameraClearWalls.Add(go);
                }

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

        private void BuildLights()
        {
            AddLight("JuneLamp", new Vector3(-1.5f, CaseRouteLayout.LoftY + 1.0f, 15.0f), 6f, 2.2f);
            AddLight("FoyerGlow", new Vector3(-0.4f, CaseRouteLayout.FloorY + 1.4f, 12.6f), 6.5f, 1.8f);
            AddLight("WorkshopLamp", new Vector3(1.0f, CaseRouteLayout.FloorY + 1.1f, CaseRouteLayout.MechanismZ), 3.5f, 1.6f);
            AddLight("NookLamp", new Vector3(2.4f, 1.6f, CaseRouteLayout.MechanismZ - 0.4f), 3f, 1.4f);
            // The route lanterns (exterior: always on).
            AddLight("BridgeLantern", new Vector3(CaseRouteLayout.RoofMaxX - 0.1f, 4.2f, CaseRouteLayout.BridgeZ + 0.05f), 6f,
                1.6f, false);
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

        /// <summary>Instantly shows the door latched or open (resume, new game, replay).</summary>
        public void SetDoor(bool open)
        {
            _doorTarget = open ? 1f : 0f;
            _door = _doorTarget;
            _latchTarget = _doorTarget;
            _latchLift = _latchTarget;
            ApplyDoor();
        }

        /// <summary>Lift the latch, then swing both leaves open together about the hinge.</summary>
        public void OpenDoor()
        {
            _latchTarget = 1f;
            _doorTarget = 1f;
        }

        private void ApplyDoor()
        {
            float eased = _door * _door * (3f - 2f * _door);
            var swing = Quaternion.AngleAxis(_doorSign * DoorOpenDegrees * eased, Vector3.up);
            if (_frontDoor != null)
            {
                _frontDoor.rotation = swing * _frontRest;
            }

            if (_innerDoor != null)
            {
                _innerDoor.rotation = swing * _innerRest;
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

        /// <summary>Applies the cut-away rule for Pip and the camera this frame.</summary>
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

            // D24: a lens inside the house never cuts the attic away (the interior camera keeps it out of the loft);
            // only a high camera outside the shell still hides it.
            bool lensInside = CaseRouteLayout.Inside(camera.x, camera.z);
            SetView(InteriorVisibility.Decide(pip.x, pip.y, pip.z, lensInside ? float.MinValue : camera.y, DoorOpen, true));
            bool inside = CaseRouteLayout.Inside(pip.x, pip.z) || View.ShowInterior;
            if (inside != _wallsClear)
            {
                _wallsClear = inside;
                foreach (var wall in _cameraClearWalls)
                {
                    wall.layer = inside ? _ignoreLayer : _wallLayer;
                }
            }
        }

        private void SetView(InteriorView view)
        {
            View = view;
            bool show = view.ShowInterior;
            if (Interior != null && Interior.gameObject.activeSelf != show)
            {
                Interior.gameObject.SetActive(show);
            }

            if (Mechanism != null && Mechanism.gameObject.activeSelf != show)
            {
                Mechanism.gameObject.SetActive(show);
            }

            bool body = !show || ForceBodyVisible;
            if (_body != null && _body.gameObject.activeSelf != body)
            {
                _body.gameObject.SetActive(body);
            }

            if (_recess != null && _recess.gameObject.activeSelf == show)
            {
                _recess.gameObject.SetActive(!show);
            }

            if (_attic != null && _attic.gameObject.activeSelf == view.HideAttic)
            {
                _attic.gameObject.SetActive(!view.HideAttic);
            }

            foreach (var light in _interiorLights)
            {
                light.enabled = show;
            }
        }
    }
}
