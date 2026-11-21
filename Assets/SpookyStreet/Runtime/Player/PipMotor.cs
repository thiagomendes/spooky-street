using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Movement;
using SpookyStreet.Controls;
using UnityEngine;

namespace SpookyStreet.Player
{
    /// <summary>
    /// Screen-relative walking and jumping through a CharacterController. The motion maths lives in the
    /// engine-free MovementModel; this component only gathers input and applies the displacement.
    /// </summary>
    [RequireComponent(typeof(CharacterController))]
    [RequireComponent(typeof(SafeGroundTracker))]
    public sealed class PipMotor : MonoBehaviour
    {
        /// <summary>Frames longer than this are clamped so a hitch cannot launch Pip through geometry.</summary>
        public const float MaxSimulationStep = 0.1f;

        private CharacterController _controller;
        private SafeGroundTracker _safeGround;
        private MovementTuningData _data;
        private PipMovementTuning _tuning;
        private GameInput _input;
        private GameFlow _flow;
        private Transform _cameraTransform;
        private MotorState _state = MotorState.Initial;

        public float HorizontalSpeed => _state.HorizontalSpeed;

        public bool Grounded => _state.Grounded;

        /// <summary>Vertical velocity in m/s (positive while rising).</summary>
        public float VerticalSpeed => _state.VelocityY;

        /// <summary>World yaw (degrees) Pip is facing.</summary>
        public float HeadingYaw => transform.eulerAngles.y;

        /// <summary>Total horizontal distance travelled under player control (meters).</summary>
        public float DistanceWalked { get; private set; }

        public int SafeReturns { get; private set; }

        public void Configure(PipMovementTuning tuning, GameInput input, GameFlow flow, Transform cameraTransform)
        {
            _tuning = tuning;
            _data = tuning.ToData();
            _input = input;
            _flow = flow;
            _cameraTransform = cameraTransform;

            _controller = GetComponent<CharacterController>();
            _controller.height = tuning.height;
            _controller.radius = tuning.radius;
            _controller.center = new Vector3(0f, tuning.height * 0.5f, 0f);
            _controller.stepOffset = Mathf.Min(tuning.stepOffset, tuning.height * 0.5f);
            _controller.slopeLimit = tuning.slopeLimit;
            _controller.skinWidth = 0.03f;
            _controller.minMoveDistance = 0f;

            _safeGround = GetComponent<SafeGroundTracker>();
            _safeGround.FallResetDepth = tuning.fallResetDepth;
            _safeGround.MaxFallTime = tuning.maxFallTime;
            _safeGround.SetAnchor(transform.position);
        }

        /// <summary>Re-reads tuning so inspector edits apply during play.</summary>
        private void OnValidate()
        {
            if (_tuning != null)
            {
                _data = _tuning.ToData();
            }
        }

        private void Update()
        {
            if (_flow == null || !_flow.SimulationRunning)
            {
                return;
            }

            float dt = Mathf.Min(Time.deltaTime, MaxSimulationStep);
            if (dt <= 0f)
            {
                return;
            }

            Vector2 move = _input.ReadMove();
            Vector3 forward = Vector3.forward;
            Vector3 right = Vector3.right;
            if (_cameraTransform != null)
            {
                forward = Vector3.ProjectOnPlane(_cameraTransform.forward, Vector3.up);
                if (forward.sqrMagnitude < 1e-4f)
                {
                    forward = Vector3.ProjectOnPlane(_cameraTransform.up, Vector3.up);
                }

                forward.Normalize();
                right = Vector3.Cross(Vector3.up, forward);
            }

            Vector3 desired = right * move.x + forward * move.y;
            var motorInput = new MotorInput
            {
                MoveX = desired.x,
                MoveZ = desired.z,
                JumpPressed = _input.ConsumeJumpPressed(),
            };

            Vector3 before = transform.position;
            MotorStep step = MovementModel.Step(ref _state, motorInput, _controller.isGrounded, dt, _data);
            CollisionFlags flags = _controller.Move(new Vector3(step.DeltaX, step.DeltaY, step.DeltaZ));
            if ((flags & CollisionFlags.Above) != 0)
            {
                MovementModel.OnCeilingHit(ref _state);
            }

            Vector3 travelled = transform.position - before;
            travelled.y = 0f;
            DistanceWalked += travelled.magnitude;

            FaceVelocity(dt);

            if (_safeGround.Tick(_controller.isGrounded, _state.VelocityY, dt))
            {
                ReturnToSafeGround();
            }
        }

        private void FaceVelocity(float dt)
        {
            var velocity = new Vector3(_state.VelocityX, 0f, _state.VelocityZ);
            if (velocity.sqrMagnitude < 0.04f)
            {
                return;
            }

            Quaternion target = Quaternion.LookRotation(velocity, Vector3.up);
            transform.rotation = Quaternion.RotateTowards(transform.rotation, target, _tuning.turnSpeedDegrees * dt);
        }

        public void ReturnToSafeGround()
        {
            Teleport(_safeGround.Anchor);
            _safeGround.OnReturned();
            SafeReturns++;
        }

        public void Teleport(Vector3 position)
        {
            _controller.enabled = false;
            transform.position = position;
            _controller.enabled = true;
            _state = MotorState.Initial;
            Physics.SyncTransforms();
        }
    }
}
