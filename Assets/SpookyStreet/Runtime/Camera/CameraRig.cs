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
    /// manual orbit always wins over auto-recenter.
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

        public Camera OutputCamera { get; private set; }

        public CinemachineCamera VirtualCamera { get; private set; }

        public CameraOrbitModel Orbit => _orbit;

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

            rig._pivot = new GameObject("PipCameraPivot").transform;
            rig._pivot.SetParent(root.transform, false);
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
            follow.Damping = new Vector3(0.3f, 0.4f, 0.3f);
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
            }
            else if (_input.Touch != null)
            {
                _input.Touch.ConsumeOrbitDelta();
            }

            SnapPivot();
        }

        private void SnapPivot()
        {
            _pivot.SetPositionAndRotation(_pip.transform.position + Vector3.up * _tuning.pivotHeight,
                Quaternion.Euler(_orbit.Pitch, _orbit.Yaw, 0f));
        }
    }
}
