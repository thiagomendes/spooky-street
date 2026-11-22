using SpookyStreet.Core.Animation;
using SpookyStreet.Core.Characters;
using UnityEngine;

namespace SpookyStreet.Characters
{
    /// <summary>
    /// Ambient life of a witness (S000.5; conversations are S002): the rest loop while Pip is away, a wave once
    /// when Pip comes within reach, the talk loop while Pip stays, and back to rest a moment after Pip leaves
    /// (decisions in the engine-free <see cref="WitnessReaction"/>). While attending, the head turns part of the way
    /// toward Pip on top of the clip. Everything runs on game time, so pause freezes the witness.
    /// </summary>
    [RequireComponent(typeof(NpcAvatar))]
    public sealed class Witness : MonoBehaviour
    {
        public const string HeadBone = "Head";

        [SerializeField] private float maxHeadYaw = 35f;
        [Tooltip("Head-turn weight change per second.")]
        [SerializeField] private float headTurnRate = 2.5f;

        private Transform _target;
        private Transform _head;
        private float _headWeight;
        private float _headYaw;
        private bool _hasApplied;
        private Quaternion _baseLocal;
        private Quaternion _appliedLocal;
        private string _gesture;
        private float _gestureYaw;
        private float _restYaw;
        private bool _returning;

        /// <summary>Bea's pointing gesture (D23.6); played over the talk loop when a vignette cues it.</summary>
        public const string PointClip = "Point";

        /// <summary>Body turn speed toward a gesture's aim and back, degrees per second.</summary>
        public const float GestureTurnRate = 240f;

        public NpcAvatar Avatar { get; private set; }
        public WitnessReaction Reaction { get; } = new WitnessReaction();
        public WitnessClipSet Clips { get; private set; }

        /// <summary>
        /// Set by the case while Pip is in a conversation with this witness (S002): the engaged loop (Talk / SitTalk)
        /// and the head turn override the ambient reaction until the conversation ends.
        /// </summary>
        public bool Conversing { get; set; }

        /// <summary>Horizontal distance to Pip at the last step (infinity without a target).</summary>
        public float DistanceToTarget { get; private set; } = float.PositiveInfinity;

        /// <summary>Current head turn toward Pip in degrees (0 at rest).</summary>
        public float HeadYaw => _headYaw * _headWeight;

        public void Configure(WitnessClipSet clips, Transform target)
        {
            Avatar = GetComponent<NpcAvatar>();
            Clips = clips;
            _target = target;
            _head = Avatar.Find(HeadBone);
            Reaction.Reset();
            Reaction.WaveDuration = Avatar.LengthOf(clips.Greeting);
            Avatar.PlayLoop(clips.Rest);
        }

        public void SetTarget(Transform target) => _target = target;

        /// <summary>A tier-1 hint's head turn can go further than the turn toward Pip (degrees).</summary>
        public const float MaxPointLookYaw = 70f;

        private Vector3 _lookPoint;
        private float _lookUntil = -1f;

        /// <summary>
        /// Turns the head toward <paramref name="point"/> for <paramref name="seconds"/> of game time (a tier-1 hint:
        /// "look there"), over the clip and instead of the turn toward Pip; then back.
        /// </summary>
        public void LookAt(Vector3 point, float seconds)
        {
            _lookPoint = point;
            _lookUntil = Time.time + Mathf.Max(0f, seconds);
        }

        /// <summary>True while a <see cref="LookAt"/> turn holds.</summary>
        public bool LookingAtPoint => _lookUntil >= 0f && Time.time < _lookUntil;

        /// <summary>The point of the last <see cref="LookAt"/>.</summary>
        public Vector3 LookPoint => _lookPoint;

        public void StopLooking() => _lookUntil = -1f;

        /// <summary>True while a gesture one-shot plays (the head turn toward Pip is off so the head follows the arm).</summary>
        public bool Gesturing => _gesture != null && Avatar != null && Avatar.OneShot == _gesture;

        /// <summary>
        /// Plays <paramref name="clip"/> once over the loop and, while it plays, turns the body toward
        /// <paramref name="aimAt"/> (the clip's own aim is accepted; no IK), then turns back. Returns false and does
        /// nothing, without logging, when the model has no such clip yet.
        /// </summary>
        public bool PlayGesture(string clip, Vector3? aimAt)
        {
            if (Avatar == null || !Avatar.HasClip(clip) || !Avatar.PlayOneShot(clip))
            {
                return false;
            }

            if (_gesture == null && !_returning)
            {
                _restYaw = transform.eulerAngles.y;
            }

            _gesture = clip;
            _returning = false;
            _gestureYaw = _restYaw;
            if (aimAt.HasValue)
            {
                Vector3 to = aimAt.Value - transform.position;
                if (new Vector2(to.x, to.z).sqrMagnitude > 0.01f)
                {
                    _gestureYaw = WitnessReaction.YawOf(to.x, to.z);
                }
            }

            return true;
        }

        private void StepGesture(float dt)
        {
            if (_gesture == null && !_returning)
            {
                return;
            }

            if (_gesture != null && !Gesturing)
            {
                _gesture = null;
                _returning = true;
            }

            float want = _gesture != null ? _gestureYaw : _restYaw;
            float yaw = Mathf.MoveTowardsAngle(transform.eulerAngles.y, want, GestureTurnRate * dt);
            transform.rotation = Quaternion.Euler(0f, yaw, 0f);
            if (_returning && Mathf.Abs(Mathf.DeltaAngle(yaw, _restYaw)) < 0.1f)
            {
                _returning = false;
            }
        }

        private void Update()
        {
            if (Avatar == null || !Avatar.IsAnimated)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            DistanceToTarget = HorizontalDistance();
            Reaction.Step(DistanceToTarget, dt);
            if (Reaction.WaveStarted)
            {
                Avatar.PlayOneShot(Clips.Greeting);
            }

            Avatar.PlayLoop(Reaction.WantsEngagedLoop || Conversing ? Clips.Engaged : Clips.Rest);
            StepGesture(dt);
            bool point = LookingAtPoint && !Gesturing;
            bool look = point || ((Reaction.Attending || Conversing) && _target != null && !Gesturing);
            _headWeight = Mathf.MoveTowards(_headWeight, look ? 1f : 0f, (Gesturing ? 8f : headTurnRate) * dt);
            if (point)
            {
                Vector3 to = _lookPoint - transform.position;
                float targetYaw = WitnessReaction.YawOf(to.x, to.z);
                _headYaw = WitnessReaction.LookYaw(transform.eulerAngles.y, targetYaw, MaxPointLookYaw);
            }
            else if (_target != null)
            {
                Vector3 to = _target.position - transform.position;
                float targetYaw = WitnessReaction.YawOf(to.x, to.z);
                _headYaw = WitnessReaction.LookYaw(transform.eulerAngles.y, targetYaw, maxHeadYaw);
            }
        }

        private void LateUpdate()
        {
            if (_head == null)
            {
                return;
            }

            // The clip rewrites the head every evaluated frame; when it did not (paused graph), undo our last turn
            // first so the turn never accumulates.
            Quaternion current = _head.localRotation;
            bool untouched = _hasApplied && Quaternion.Angle(current, _appliedLocal) < 0.01f;
            _baseLocal = untouched ? _baseLocal : current;
            _head.localRotation = _baseLocal;
            float yaw = HeadYaw;
            if (Mathf.Abs(yaw) > 0.01f)
            {
                _head.rotation = Quaternion.AngleAxis(yaw, Vector3.up) * _head.rotation;
            }

            _appliedLocal = _head.localRotation;
            _hasApplied = true;
        }

        private float HorizontalDistance()
        {
            if (_target == null)
            {
                return float.PositiveInfinity;
            }

            Vector3 d = _target.position - transform.position;
            d.y = 0f;
            return d.magnitude;
        }
    }
}
