using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Movement;
using SpookyStreet.Controls;
using SpookyStreet.Player;
using Unity.Cinemachine;
using UnityEngine;

namespace SpookyStreet.CameraSystem
{
    /// <summary>
    /// Third-person camera (spec §5): Cinemachine Third Person Follow behind Pip at ~3.8 m, pivot near the
    /// upper torso, boom shortened against solid walls and eased back out. Orbit input drives the pivot;
    /// manual orbit always wins over auto-recenter. The pivot's height follows Pip through
    /// <see cref="CameraHeightFollow"/>: in the air it only moves once Pip leaves a small band, so jump arcs and
    /// the bat-cape glide do not bob the camera; on the ground it eases back onto Pip.
    /// Runs before Cinemachine's LateUpdate so the camera sees this frame's pivot.
    /// </summary>
    [DefaultExecutionOrder(-200)]
    public sealed class CameraRig : MonoBehaviour
    {
        private PipMotor _pip;
        private PipMovementTuning _tuning;
        private GameInput _input;
        private GameFlow _flow;
        private CameraOrbitModel _orbit;
        private Transform _pivot;
        private readonly CameraHeightFollow _height = new CameraHeightFollow();
        private CinemachineThirdPersonFollow _follow;
        private LayerMask _obstacles;
        private float _pitch;
        private float _yawOffset;
        private bool _indoors;

        /// <summary>Indoor framing eases its pitch and yaw swing at these rates (degrees per second).</summary>
        public const float PitchBlendPerSecond = 90f;
        public const float YawSwingPerSecond = 120f;

        /// <summary>The shoulder eases up and down at this rate (m/s).</summary>
        public const float RiseBlendPerSecond = 1.2f;

        private static readonly Vector3 OutdoorDamping = new Vector3(0.3f, 0.4f, 0.3f);
        private static readonly Vector3 IndoorDamping = new Vector3(0.08f, 0.12f, 0.08f);

        /// <summary>Closer framing per place (spec §5: "Entering the attic blends to a closer shoulder view").</summary>
        public const float DistanceBlendPerSecond = 2.5f;

        /// <summary>
        /// Boom length to ease toward; null keeps the tuned distance. The case sets a shorter boom indoors and the
        /// closer shoulder view in the attic.
        /// </summary>
        public float? DistanceOverride { get; set; }

        /// <summary>
        /// Hard cap on the boom this frame (the interior probe): the camera moves in at once when it drops, and eases back
        /// out at <see cref="DistanceBlendPerSecond"/> when it rises.
        /// </summary>
        public float? DistanceLimit { get; set; }

        /// <summary>Pitch to ease toward instead of the player's orbit pitch (the interior solver), or null.</summary>
        public float? PitchOverride { get; set; }

        /// <summary>Shoulder rise above the pivot to ease toward (lower under the hall's ceiling), or null for the tuning.</summary>
        public float? RiseOverride { get; set; }

        /// <summary>Yaw swing added to the player's orbit (degrees) when the space behind Pip is too tight; eased.</summary>
        public float YawOffsetTarget { get; set; }

        /// <summary>Indoors the follow damping is short, so the lens never trails into a wall behind Pip.</summary>
        public bool Indoors
        {
            get => _indoors;
            set
            {
                if (_indoors == value)
                {
                    return;
                }

                _indoors = value;
                if (_follow != null)
                {
                    _follow.Damping = value ? IndoorDamping : OutdoorDamping;
                }
            }
        }

        /// <summary>The pitch the camera actually uses this frame (degrees, down +).</summary>
        public float EffectivePitch => _pitch;

        /// <summary>The yaw the camera actually uses this frame (orbit plus the eased swing).</summary>
        public float EffectiveYaw => _orbit != null ? CameraOrbitModel.NormalizeAngle(_orbit.Yaw + _yawOffset) : 0f;

        /// <summary>The eased yaw swing currently applied.</summary>
        public float YawOffset => _yawOffset;

        /// <summary>The shoulder rise above the pivot currently applied.</summary>
        public float Rise => _follow != null ? _follow.ShoulderOffset.y : 0f;

        /// <summary>Pivot height above Pip's feet (before the shoulder rise).</summary>
        public float PivotHeight => _tuning != null ? _tuning.pivotHeight : 0f;

        /// <summary>Current boom length (eased toward <see cref="DistanceOverride"/>).</summary>
        public float Distance => _follow != null ? _follow.CameraDistance : 0f;

        /// <summary>
        /// Layers the boom treats as solid. Indoors the case removes the house's floors and walls so the camera can
        /// sit outside the shell and look in through the culled near walls.
        /// </summary>
        public LayerMask ObstacleLayers
        {
            get => _obstacles;
            set
            {
                _obstacles = value;
                if (_follow != null)
                {
                    var avoid = _follow.AvoidObstacles;
                    avoid.CollisionFilter = value;
                    _follow.AvoidObstacles = avoid;
                }
            }
        }

        public Camera OutputCamera { get; private set; }

        public CinemachineCamera VirtualCamera { get; private set; }

        public CameraOrbitModel Orbit => _orbit;

        /// <summary>The camera pivot (Pip's upper torso, with the vertical tolerance applied).</summary>
        public Transform Pivot => _pivot;

        public static CameraRig Create(PipMotor pip, PipMovementTuning tuning, GameInput input, GameFlow flow,
            LayerMask obstacleLayers)
        {
            var root = new GameObject("CameraRig");
            var rig = root.AddComponent<CameraRig>();
            rig._pip = pip;
            rig._tuning = tuning;
            rig._input = input;
            rig._flow = flow;
            rig._orbit = new CameraOrbitModel(new CameraOrbitTuning(), pip.transform.eulerAngles.y);
            rig._pitch = rig._orbit.Pitch;

            rig._pivot = new GameObject("PipCameraPivot").transform;
            rig._pivot.SetParent(root.transform, false);
            rig._height.Reset(pip.transform.position.y);
            rig.SnapPivot();

            var cameraObject = new GameObject("Main Camera") { tag = "MainCamera" };
            cameraObject.transform.SetParent(root.transform, false);
            var outputCamera = cameraObject.AddComponent<Camera>();
            outputCamera.clearFlags = CameraClearFlags.SolidColor;
            outputCamera.backgroundColor = Palette.NightViolet;
            outputCamera.nearClipPlane = 0.1f;
            outputCamera.farClipPlane = 200f;
            World.Atmosphere.ConfigureCamera(outputCamera);
            cameraObject.AddComponent<AudioListener>();
            var brain = cameraObject.AddComponent<CinemachineBrain>();
            brain.UpdateMethod = CinemachineBrain.UpdateMethods.LateUpdate;
            rig.OutputCamera = outputCamera;

            var vcamObject = new GameObject("PipCamera");
            vcamObject.transform.SetParent(root.transform, false);
            var vcam = vcamObject.AddComponent<CinemachineCamera>();
            var lens = vcam.Lens;
            lens.FieldOfView = tuning.fieldOfView;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 200f;
            vcam.Lens = lens;
            vcam.Follow = rig._pivot;

            var follow = vcamObject.AddComponent<CinemachineThirdPersonFollow>();
            follow.CameraDistance = tuning.cameraDistance;
            follow.ShoulderOffset = new Vector3(tuning.shoulderSide, tuning.shoulderRise, 0f);
            follow.VerticalArmLength = 0f;
            follow.CameraSide = 0.5f;
            follow.Damping = OutdoorDamping;
            follow.AvoidObstacles = new CinemachineThirdPersonFollow.ObstacleSettings
            {
                Enabled = true,
                CollisionFilter = obstacleLayers,
                IgnoreTag = "Player",
                CameraRadius = 0.2f,
                DampingIntoCollision = 0f,
                DampingFromCollision = 0.6f,
            };
            rig.VirtualCamera = vcam;
            rig._follow = follow;
            rig._obstacles = obstacleLayers;
            return rig;
        }

        private void LateUpdate()
        {
            if (_pip == null)
            {
                return;
            }

            if (_flow != null && _flow.SimulationRunning)
            {
                float dt = Time.deltaTime;
                Vector2 orbit = Vector2.zero;
                if (_input.Touch != null)
                {
                    orbit += _input.Touch.ConsumeOrbitDelta() * _tuning.touchDegreesPerPixel;
                }

                orbit += _input.ReadMouseOrbitPixels() * _tuning.mouseDegreesPerPixel;
                orbit += _input.ReadStickOrbit() * (_tuning.stickDegreesPerSecond * dt);
                float pitchSign = _tuning.invertY ? 1f : -1f;
                _orbit.ApplyManual(orbit.x, orbit.y * pitchSign);

                if (_input.ConsumeRecenterPressed())
                {
                    _orbit.RequestRecenter();
                }

                _orbit.Tick(dt, _pip.HorizontalSpeed, _pip.HeadingYaw);
                _height.Step(_pip.transform.position.y, _pip.Grounded, dt);
                StepFraming(dt);
            }
            else if (_input.Touch != null)
            {
                _input.Touch.ConsumeOrbitDelta();
            }

            SnapPivot();
        }

        /// <summary>Eases pitch, yaw swing, shoulder and boom toward this frame's framing (interior overrides or tuning).</summary>
        private void StepFraming(float dt)
        {
            // The player's own pitch follows input at once; an override (and the hand-back from one) eases.
            float pitchTarget = PitchOverride ?? _orbit.Pitch;
            float pitchRate = PitchOverride.HasValue ? PitchBlendPerSecond : 4f * PitchBlendPerSecond;
            _pitch = Mathf.MoveTowards(_pitch, pitchTarget, pitchRate * dt);
            _yawOffset = Mathf.MoveTowardsAngle(_yawOffset, YawOffsetTarget, YawSwingPerSecond * dt);
            if (_follow == null)
            {
                return;
            }

            float riseTarget = RiseOverride ?? _tuning.shoulderRise;
            var shoulder = _follow.ShoulderOffset;
            if (Mathf.Abs(shoulder.y - riseTarget) > 1e-4f)
            {
                shoulder.y = Mathf.MoveTowards(shoulder.y, riseTarget, RiseBlendPerSecond * dt);
                _follow.ShoulderOffset = shoulder;
            }

            float wanted = DistanceOverride ?? _tuning.cameraDistance;
            if (DistanceLimit.HasValue)
            {
                wanted = Mathf.Min(wanted, DistanceLimit.Value);
            }

            float current = _follow.CameraDistance;
            if (wanted < current)
            {
                // In at once against something solid; a plain profile change still eases.
                bool limited = DistanceLimit.HasValue && DistanceLimit.Value < current;
                current = limited ? wanted : Mathf.MoveTowards(current, wanted, DistanceBlendPerSecond * dt);
            }
            else if (wanted > current)
            {
                current = Mathf.MoveTowards(current, wanted, DistanceBlendPerSecond * dt);
            }

            _follow.CameraDistance = current;
        }

        private void SnapPivot()
        {
            Vector3 pip = _pip.transform.position;
            // A teleport (new game, safe-ground return) snaps straight away, even before the next simulated step.
            float height = Mathf.Abs(pip.y - _height.Height) > _height.SnapDistance ? pip.y : _height.Height;
            _pivot.SetPositionAndRotation(new Vector3(pip.x, height + _tuning.pivotHeight, pip.z),
                Quaternion.Euler(_pitch, _orbit.Yaw + _yawOffset, 0f));
        }
    }
}
