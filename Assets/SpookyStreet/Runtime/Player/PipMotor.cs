using System;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Movement;
using SpookyStreet.Controls;
using UnityEngine;

namespace SpookyStreet.Player
{
    /// <summary>
    /// Screen-relative walking, jumping and the moveset (variable and chain jump, bat-cape glide, roll and dive)
    /// through a CharacterController. The motion maths lives in the engine-free MovementModel; this component only
    /// gathers input (Jump, Jump held, Use, interaction in range), applies the displacement and reports each step
    /// through <see cref="Stepped"/> so animation and feel can react.
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

        /// <summary>Bat cape spread (Jump held while falling).</summary>
        public bool Gliding => _state.Gliding;

        public bool Rolling => _state.Rolling;

        public bool Diving => _state.Diving;

        /// <summary>Stage (1–3) of the last jump of the open chain, 0 when none.</summary>
        public int ChainStage => _state.ChainStage;

        /// <summary>A copy of the motor state after the last step (feedback, tips, tests).</summary>
        public MotorState State => _state;

        /// <summary>Rising with Jump held since take-off (the higher, held jump).</summary>
        public bool HoldingJumpRise => _state.HoldingJumpRise;

        /// <summary>Sliding down a marked wall (S003).</summary>
        public bool WallSliding => _state.WallSliding;

        /// <summary>Hanging from a marked ledge (S003).</summary>
        public bool Hanging => _state.Hanging;

        /// <summary>Climbing up from a hang onto the ledge's top (S003).</summary>
        public bool Climbing => _state.Climbing;

        /// <summary>Where Pip last touched a wall at chest height when she kicked off it (feedback anchor).</summary>
        public Vector3 LastKickPoint { get; private set; }

        /// <summary>The edge point Pip's hands caught at the last ledge grab (feedback anchor).</summary>
        public Vector3 LastGrabPoint { get; private set; }

        /// <summary>The wall and ledge measurements of the last step (tests and diagnostics).</summary>
        public WallProbe LastWallProbe { get; private set; }

        public LedgeProbe LastLedgeProbe { get; private set; }

        /// <summary>Use would dive now (in the air, no dive yet this airtime).</summary>
        public bool CanDive => _data != null && _state.CanDive(_data);

        /// <summary>The motor's tuning as plain data (read-only use: animation timing, tests).</summary>
        public MovementTuningData TuningData => _data;

        /// <summary>The step applied this frame (events such as RollStarted are true only for one frame).</summary>
        public MotorStep LastStep { get; private set; }

        /// <summary>Raised after every simulated step (not while paused).</summary>
        public event Action<MotorStep> Stepped;

        /// <summary>
        /// Contextual interactions in range (none exist yet). While it has a target, Use interacts instead of
        /// rolling or diving.
        /// </summary>
        public IUseTargetSource UseTargets { get; set; }

        /// <summary>True while Use would go to an interaction rather than the moveset.</summary>
        public bool InteractionAvailable => UseTargets != null && UseTargets.HasTarget;

        public bool Grounded => _state.Grounded;

        /// <summary>Vertical velocity in m/s (positive while rising; a small negative stick value on the ground).</summary>
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
            Vector3 facing = transform.forward;
            float groundDistance = _state.Grounded ? 0f : GroundDistanceBelow();
            var motorInput = new MotorInput
            {
                MoveX = desired.x,
                MoveZ = desired.z,
                JumpPressed = _input.ConsumeJumpPressed(),
                JumpHeld = _input.ReadJumpHeld(),
                UsePressed = _input.ConsumeUsePressed(),
                InteractionAvailable = InteractionAvailable,
                GroundDistance = groundDistance,
                FacingX = facing.x,
                FacingZ = facing.z,
                FeetY = transform.position.y,
                StickY = move.y,
            };
            ProbeClimbSurfaces(ref motorInput, groundDistance);
            LastWallProbe = motorInput.Wall;
            LastLedgeProbe = motorInput.Ledge;

            Vector3 before = transform.position;
            MotorStep step = MovementModel.Step(ref _state, motorInput, _controller.isGrounded, dt, _data);
            if (step.WallJumped)
            {
                LastKickPoint = before + Vector3.up * ClimbProbes.ChestHeight -
                                new Vector3(_state.KickNormalX, 0f, _state.KickNormalZ) * _data.BodyRadius;
            }

            if (step.LedgeGrabbed)
            {
                var ledge = motorInput.Ledge;
                LastGrabPoint = before + new Vector3(-ledge.NormalX * ledge.EdgeDistance, ledge.TopAbove,
                    -ledge.NormalZ * ledge.EdgeDistance);
            }

            if (step.Interacted && UseTargets != null)
            {
                UseTargets.Interact();
            }

            CollisionFlags flags = _controller.Move(new Vector3(step.DeltaX, step.DeltaY, step.DeltaZ));
            if ((flags & CollisionFlags.Above) != 0)
            {
                MovementModel.OnCeilingHit(ref _state);
            }

            Vector3 travelled = transform.position - before;
            travelled.y = 0f;
            DistanceWalked += travelled.magnitude;

            FaceVelocity(dt);
            LastStep = step;
            Stepped?.Invoke(step);

            // A glide or a wall slide with ground below is a controlled descent; over the void it still counts as falling.
            bool glidingOverGround = (_state.Gliding || _state.WallSliding) && GroundBelow();
            if (_safeGround.Tick(_controller.isGrounded, _state.VelocityY, dt, glidingOverGround))
            {
                ReturnToSafeGround();
            }
        }

        /// <summary>Solid ground somewhere below Pip within the fall-reset depth (glide safety).</summary>
        private bool GroundBelow()
        {
            int mask = Physics.DefaultRaycastLayers & ~(1 << gameObject.layer);
            float reach = _safeGround.FallResetDepth + Mathf.Max(0f, transform.position.y - _safeGround.Anchor.y);
            return Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, reach + 0.1f, mask,
                QueryTriggerInteraction.Ignore);
        }

        /// <summary>Probe reach: more than the jump buffer at terminal fall speed (0.15 s × 20 m/s).</summary>
        private const float GroundProbeReach = 3.5f;

        /// <summary>Air below the feet while airborne (0 when nothing is in reach): keeps a late press a jump.</summary>
        private float GroundDistanceBelow()
        {
            int mask = Physics.DefaultRaycastLayers & ~(1 << gameObject.layer);
            if (Physics.Raycast(transform.position + Vector3.up * 0.1f, Vector3.down, out RaycastHit hit,
                    GroundProbeReach + 0.1f, mask, QueryTriggerInteraction.Ignore))
            {
                return Mathf.Max(1e-3f, hit.distance - 0.1f);
            }

            return 0f;
        }

        /// <summary>
        /// Fills the wall and ledge probes (S003) while airborne and free; nothing on the ground, while held by a ledge
        /// (the hang keeps its own ledge) or while rising too fast to catch one.
        /// </summary>
        private void ProbeClimbSurfaces(ref MotorInput motorInput, float groundDistance)
        {
            if (_state.Grounded || _state.Hanging || _state.Climbing)
            {
                return;
            }

            int mask = Physics.DefaultRaycastLayers & ~(1 << gameObject.layer);
            Vector3 feet = transform.position;
            float clearance = groundDistance > 0f ? groundDistance : 100f;
            motorInput.Wall = ClimbProbes.Wall(feet, _data, clearance);
            if (_state.VelocityY <= _data.LedgeGrabMaxRiseSpeed && _state.LedgeRegrabLeft <= 0f)
            {
                motorInput.Ledge = ClimbProbes.Ledge(feet, _data, mask, _controller.height, _controller.radius);
            }
        }

        /// <summary>Turn rate (degrees per second) toward a wall or ledge Pip is held by.</summary>
        private const float FaceWallDegreesPerSecond = 1080f;

        private void FaceVelocity(float dt)
        {
            if (_state.FacingWall)
            {
                var into = new Vector3(-_state.FacedNormalX, 0f, -_state.FacedNormalZ);
                if (into.sqrMagnitude > 1e-4f)
                {
                    transform.rotation = Quaternion.RotateTowards(transform.rotation,
                        Quaternion.LookRotation(into, Vector3.up), FaceWallDegreesPerSecond * dt);
                }

                return;
            }

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
