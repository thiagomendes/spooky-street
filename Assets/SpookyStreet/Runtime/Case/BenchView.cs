using SpookyStreet.Controls;
using SpookyStreet.Core.Case;
using SpookyStreet.Player;
using Unity.Cinemachine;
using UnityEngine;

namespace SpookyStreet.Case
{
    /// <summary>
    /// "Sit and check the view" (spec §3/§4): a fixed Cinemachine camera at a seated child's eye height on Mr. Vale's
    /// bench, framing the front door so the player sees what Vale could see (the door) and what the hedge hides (the
    /// side). It takes over for <see cref="HoldSeconds"/> or until Pip moves, then blends back to the follow camera.
    /// The hold runs on game time, so pause freezes it.
    /// </summary>
    public sealed class BenchView : MonoBehaviour
    {
        public const float HoldSeconds = 3f;
        public const float BlendSeconds = 0.6f;
        public const float FieldOfView = 50f;

        private CinemachineCamera _camera;
        private PipMotor _pip;
        private GameInput _input;
        private Vector3 _pipAtStart;
        private float _left;

        public bool Active { get; private set; }

        public Transform CameraTransform => _camera.transform;

        public static BenchView Create(Transform parent, PipMotor pip, GameInput input, Camera output)
        {
            var go = new GameObject("BenchView");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<BenchView>();
            view._pip = pip;
            view._input = input;

            var cameraObject = new GameObject("BenchViewCamera");
            cameraObject.transform.SetParent(go.transform, false);
            EnvironmentSeat(out Vector3 eye);
            var door = new Vector3(CaseStage.FrontDoor.X, CaseStage.FrontDoor.Y + 1.1f, CaseStage.FrontDoor.Z);
            cameraObject.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(door - eye, Vector3.up));
            cameraObject.SetActive(false);
            var vcam = cameraObject.AddComponent<CinemachineCamera>();
            var lens = vcam.Lens;
            lens.FieldOfView = FieldOfView;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 200f;
            vcam.Lens = lens;
            view._camera = vcam;

            var brain = output != null ? output.GetComponent<CinemachineBrain>() : null;
            if (brain != null)
            {
                brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut, BlendSeconds);
            }

            return view;
        }

        /// <summary>Eye position on the bench seat (the free end, beside Mr. Vale).</summary>
        private static void EnvironmentSeat(out Vector3 eye)
        {
            var seat = CaseStage.Find(CaseStage.PointBench).Position;
            eye = new Vector3(seat.X, CaseStage.BenchEyeHeight, seat.Z);
        }

        public void Begin()
        {
            Active = true;
            _left = HoldSeconds;
            _pipAtStart = _pip.transform.position;
            _camera.gameObject.SetActive(true);
        }

        public void End()
        {
            Active = false;
            _camera.gameObject.SetActive(false);
        }

        private void Update()
        {
            if (!Active)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return; // paused
            }

            _left -= dt;
            Vector3 moved = _pip.transform.position - _pipAtStart;
            moved.y = 0f;
            bool input = _input != null && _input.ReadMove().sqrMagnitude > 0.04f;
            if (_left <= 0f || input || moved.sqrMagnitude > 0.09f)
            {
                End();
            }
        }
    }
}
