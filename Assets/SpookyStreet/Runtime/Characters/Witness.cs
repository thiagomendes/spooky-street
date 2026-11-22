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

        public NpcAvatar Avatar { get; private set; }
        public WitnessReaction Reaction { get; } = new WitnessReaction();
        public WitnessClipSet Clips { get; private set; }

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

            Avatar.PlayLoop(Reaction.WantsEngagedLoop ? Clips.Engaged : Clips.Rest);
            _headWeight = Mathf.MoveTowards(_headWeight, Reaction.Attending && _target != null ? 1f : 0f,
                headTurnRate * dt);
            if (_target != null)
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
