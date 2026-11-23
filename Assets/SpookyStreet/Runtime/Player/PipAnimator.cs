using System.Collections.Generic;
using SpookyStreet.Core.Animation;
using SpookyStreet.Core.Movement;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SpookyStreet.Player
{
    /// <summary>
    /// Drives Pip's imported clips (contract in <see cref="PipClips"/>) from <see cref="PipMotor"/> state with a
    /// small Playables graph, so no editor-authored Animator Controller is needed. The pose logic is the
    /// engine-free <see cref="PipPoseTracker"/>: idle/walk/run blend by ground speed with walk and run phase-locked
    /// so feet never slide between them, jump plays start → air → fall by vertical speed, and touchdown plays Land.
    /// The moveset drives the rest through <see cref="SetGliding"/>, <see cref="SetWallSliding"/>,
    /// <see cref="SetLedgeHanging"/> and <see cref="PlayOneShot"/>. A missing optional clip falls back along
    /// <see cref="PipClips.Fallback"/> and is reported once in development builds; only Idle is required.
    /// Weights ease toward their targets so transitions never pop, and the graph runs on game time, so pause
    /// (timeScale 0) freezes Pip mid-pose. Motor moves drive it directly: the glide pose follows
    /// <see cref="PipMotor.Gliding"/>, a roll (ground roll or dive landing) plays Roll timed to the roll, the third
    /// chain jump tucks into the same clip as a flip, and jumping out of a roll cancels it. S003: the wall slide and
    /// the ledge hang follow <see cref="PipMotor.WallSliding"/> and <see cref="PipMotor.Hanging"/>. A wall kick plays
    /// WallKick at once (the motor has just turned her away from the wall), a catch plays LedgeGrab into LedgeHang
    /// frame 0, and a climb plays LedgeClimb over the motor's climb and ends on her feet with no landing. Each of the
    /// three plays to its last frame, which is the first frame of what follows. Without those clips the kick
    /// restarts JumpStart and the climb plays it as a one-shot, as before they existed.
    /// </summary>
    public sealed class PipAnimator : MonoBehaviour
    {
        private static readonly HashSet<string> s_reported = new HashSet<string>();

        [Tooltip("Weight change per second; 8 gives ~0.12 s cross-fades.")]
        [SerializeField] private float blendRate = 8f;

        [Tooltip("Ground speed the Walk clip was authored at (m/s, from the export report).")]
        [SerializeField] private float walkClipSpeed = 1.6f;

        [Tooltip("Ground speed the Run clip was authored at (m/s, from the export report).")]
        [SerializeField] private float runClipSpeed = 4.5f;

        [Tooltip("Seconds the Roll clip is played over as the third chain jump's flip.")]
        [SerializeField] private float flipDuration = 0.5f;

        [Tooltip("Seconds LedgeGrab takes to fade in over the air pose (the clip is only 0.2 s long).")]
        [SerializeField] private float grabFadeIn = 0.04f;

        private readonly Dictionary<string, int> _slot = new Dictionary<string, int>();
        private readonly PipPoseTracker _tracker = new PipPoseTracker();
        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private AnimationClipPlayable[] _playables;
        private float[] _lengths;
        private float[] _weights;
        private float[] _targets;
        private PipMotor _motor;
        private float _maxSpeed = 4f;
        private bool _gliding;
        private bool _wallSliding;
        private bool _ledgeHanging;
        private bool _kickPending;
        private int _oneShotSlot = -1;
        private float _oneShotRemaining;

        /// <summary>Seconds before the one-shot's end at which the blend back starts (0 plays it to its last frame).</summary>
        private float _oneShotTail;

        /// <summary>Weight change per second while a move's clip fades in (0 uses <see cref="blendRate"/>).</summary>
        private float _oneShotFadeRate;

        /// <summary>The clip restarted when the one-shot ends: the one whose first frame is its last.</summary>
        private string _oneShotNext;
        private float _cyclePhase;
        private int _walkSlot = -1;
        private int _runSlot = -1;

        public bool IsReady => _graph.IsValid();

        public PipPose Pose => _tracker.Pose;

        /// <summary>The one-shot clip currently playing (Roll, Interact, WallKick, LedgeGrab, LedgeClimb), or null.</summary>
        public string OneShot { get; private set; }

        /// <summary>Names of the clips found when the graph was built (for tests and diagnostics).</summary>
        public IReadOnlyCollection<string> ClipNames => _slot.Keys;

        public bool HasClip(string clipName) => _slot.ContainsKey(clipName);

        public float WalkClipSpeed
        {
            get => walkClipSpeed;
            set => walkClipSpeed = Mathf.Max(0.1f, value);
        }

        public float RunClipSpeed
        {
            get => runClipSpeed;
            set => runClipSpeed = Mathf.Max(0.1f, value);
        }

        /// <summary>The clip whose cleaned name is <paramref name="clipName"/> (ignores editor preview clips).</summary>
        public static AnimationClip Find(IList<AnimationClip> clips, string clipName)
        {
            if (clips == null)
            {
                return null;
            }

            foreach (var clip in clips)
            {
                if (clip != null && !clip.name.StartsWith("__preview__", System.StringComparison.Ordinal) &&
                    PipClips.CleanClipName(clip.name) == clipName)
                {
                    return clip;
                }
            }

            return null;
        }

        public static bool CanAnimate(IList<AnimationClip> clips) => Find(clips, PipClips.Required) != null;

        /// <summary>
        /// Builds and starts the graph. Returns false (and builds nothing) when <see cref="PipClips.Required"/> is
        /// missing; the caller then leaves the model in its bind pose.
        /// </summary>
        public bool Build(Animator animator, IList<AnimationClip> clips, PipMotor motor, float maxSpeed)
        {
            if (animator == null || !CanAnimate(clips))
            {
                return false;
            }

            if (_graph.IsValid())
            {
                _graph.Destroy();
            }

            if (_motor != null)
            {
                _motor.Stepped -= OnMotorStep;
            }

            _motor = motor;
            if (_motor != null)
            {
                _motor.Stepped += OnMotorStep;
            }

            _maxSpeed = Mathf.Max(0.1f, maxSpeed);
            _slot.Clear();
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            var found = new List<AnimationClip>();
            foreach (var clipName in PipClips.All)
            {
                var clip = Find(clips, clipName);
                if (clip == null)
                {
                    ReportOnce("missing optional clip " + clipName + "; using " +
                               (PipClips.Fallback(clipName) ?? "nothing") + " instead");
                    continue;
                }

                _slot[clipName] = found.Count;
                found.Add(clip);
            }

            _graph = PlayableGraph.Create("PipAnimation");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            _mixer = AnimationMixerPlayable.Create(_graph, found.Count);
            _playables = new AnimationClipPlayable[found.Count];
            _lengths = new float[found.Count];
            _weights = new float[found.Count];
            _targets = new float[found.Count];
            for (int i = 0; i < found.Count; i++)
            {
                _playables[i] = AnimationClipPlayable.Create(_graph, found[i]);
                _lengths[i] = Mathf.Max(1f / 30f, found[i].length);
                _graph.Connect(_playables[i], 0, _mixer, i);
            }

            _walkSlot = SlotOf(PipClips.Walk);
            _runSlot = SlotOf(PipClips.Run);
            // Walk and run are phase-locked by hand each frame, so they never advance on their own.
            if (_walkSlot >= 0)
            {
                _playables[_walkSlot].SetSpeed(0);
            }

            if (_runSlot >= 0 && _runSlot != _walkSlot)
            {
                _playables[_runSlot].SetSpeed(0);
            }

            _tracker.Reset();
            _tracker.JumpStartDuration = _slot.TryGetValue(PipClips.JumpStart, out int start)
                ? Mathf.Clamp(_lengths[start], 0.1f, 0.4f)
                : 0f;
            _tracker.LandDuration = _slot.TryGetValue(PipClips.Land, out int land)
                ? Mathf.Clamp(_lengths[land], 0.15f, 0.4f)
                : 0f;

            int idle = _slot[PipClips.Idle];
            _weights[idle] = 1f;
            _mixer.SetInputWeight(idle, 1f);
            var output = AnimationPlayableOutput.Create(_graph, "Pip", animator);
            output.SetSourcePlayable(_mixer);
            _graph.Play();
            return true;
        }

        /// <summary>Moveset hook: glide pose while airborne (falls back to Fall without a Glide clip).</summary>
        public void SetGliding(bool gliding) => _gliding = gliding;

        /// <summary>Moveset hook: wall-slide pose while airborne.</summary>
        public void SetWallSliding(bool sliding) => _wallSliding = sliding;

        /// <summary>Moveset hook: ledge-hang pose (overrides grounded and air poses).</summary>
        public void SetLedgeHanging(bool hanging) => _ledgeHanging = hanging;

        /// <summary>
        /// The hint glance (S003a, D26): Pip's head and shoulders turn toward the side <paramref name="point"/> lies
        /// on (<see cref="PipClips.GlanceToward"/>), she peers, and comes back. It shows a direction, never a thing.
        /// Only while she stands on the ground; walking off or leaving the ground ends it. Returns false, without
        /// logging, when the model has no glance clips, she is not standing, or the point is where she stands.
        /// </summary>
        public bool PlayGlance(Vector3 point)
        {
            if (!IsReady || (_motor != null && (!_motor.Grounded || _motor.HorizontalSpeed > GlanceMaxSpeed)))
            {
                return false;
            }

            Transform body = _motor != null ? _motor.transform : transform;
            Vector3 to = point - body.position;
            Vector3 forward = body.forward;
            string clip = PipClips.GlanceToward(forward.x, forward.z, to.x, to.z);
            return clip != null && _slot.ContainsKey(clip) && PlayOneShot(clip);
        }

        /// <summary>Ground speed above which a glance does not start, and at which a playing one ends (m/s).</summary>
        public const float GlanceMaxSpeed = 0.6f;

        /// <summary>
        /// Plays a one-shot clip (e.g. <see cref="PipClips.Roll"/>, <see cref="PipClips.Interact"/>) over every
        /// pose, then blends back. Returns false, without error, when the clip is missing or the graph is not built.
        /// </summary>
        public bool PlayOneShot(string clipName) => PlayOneShot(clipName, 0f);

        /// <summary>
        /// As <see cref="PlayOneShot(string)"/>, retimed so the clip lasts <paramref name="duration"/> seconds
        /// (0 keeps its authored length); used to match Roll to the motor's roll.
        /// </summary>
        public bool PlayOneShot(string clipName, float duration)
        {
            if (!IsReady || clipName == null || !_slot.TryGetValue(clipName, out int slot))
            {
                if (IsReady)
                {
                    ReportOnce("one-shot clip " + clipName + " missing; ignored");
                }

                return false;
            }

            float length = _lengths[slot];
            float playFor = duration > 0f ? Mathf.Clamp(duration, length * 0.4f, length * 2.5f) : length;
            _playables[slot].SetTime(0);
            _playables[slot].SetSpeed(length / playFor);
            _oneShotSlot = slot;
            _oneShotRemaining = playFor;
            // End early by one fade so the blend back overlaps the clip's tail instead of its held last frame.
            _oneShotTail = 1f / Mathf.Max(0.01f, blendRate);
            _oneShotFadeRate = 0f;
            _oneShotNext = null;
            OneShot = clipName;
            return true;
        }

        /// <summary>
        /// Plays a move's own clip (S003: WallKick, LedgeGrab, LedgeClimb) to its last frame, then restarts
        /// <paramref name="next"/>, whose first frame that is, so the hand-over shows no step. It fades in over
        /// <paramref name="fadeIn"/> seconds, or cuts in at once when that is 0. False, with nothing played, when the
        /// clip is missing.
        /// </summary>
        private bool PlayMove(string clipName, float duration, float fadeIn, string next)
        {
            if (!IsReady || !_slot.ContainsKey(clipName) || !PlayOneShot(clipName, duration))
            {
                return false;
            }

            _oneShotTail = 0f;
            _oneShotNext = next;
            if (fadeIn > 0f)
            {
                _oneShotFadeRate = 1f / fadeIn;
            }
            else
            {
                System.Array.Clear(_weights, 0, _weights.Length);
                _weights[_oneShotSlot] = 1f;
            }

            return true;
        }

        public void CancelOneShot()
        {
            _oneShotSlot = -1;
            _oneShotRemaining = 0f;
            _oneShotTail = 0f;
            _oneShotFadeRate = 0f;
            _oneShotNext = null;
            OneShot = null;
        }

        /// <summary>Current normalized mixer weight of <paramref name="clipName"/> (0 when absent).</summary>
        public float WeightOf(string clipName) =>
            IsReady && _slot.TryGetValue(clipName, out int slot) ? _mixer.GetInputWeight(slot) : 0f;

        private void OnMotorStep(MotorStep step)
        {
            if (!IsReady)
            {
                return;
            }

            if (step.WallJumped && !PlayMove(PipClips.WallKick, 0f, 0f, PipClips.JumpAir))
            {
                // Without a WallKick clip the kick restarts the take-off and the air arc follows.
                _kickPending = true;
            }

            if (step.LedgeGrabbed)
            {
                PlayMove(PipClips.LedgeGrab, 0f, grabFadeIn, PipClips.LedgeHang);
            }

            if (step.ClimbStarted)
            {
                float climbTime = _motor != null && _motor.TuningData != null ? _motor.TuningData.LedgeClimbTime : 0f;
                // LedgeClimb starts on the hang pose, so the usual fade is enough. Without that clip the take-off push
                // plays over the climb, then Land on top.
                if (!PlayMove(PipClips.LedgeClimb, climbTime, 1f / Mathf.Max(0.01f, blendRate), null))
                {
                    PlayOneShot(PipClips.Resolve(PipClips.JumpStart, _slot.ContainsKey), climbTime);
                }
            }

            if (step.RollStarted)
            {
                PlayOneShot(PipClips.Roll, _motor != null && _motor.TuningData != null ? _motor.TuningData.RollDuration : 0f);
            }
            else if (step.Jumped)
            {
                if (step.ChainStage >= 3)
                {
                    PlayOneShot(PipClips.Roll, flipDuration);
                }
                else if (OneShot == PipClips.Roll)
                {
                    CancelOneShot();
                }
            }
            else if (step.Landed && OneShot == PipClips.Roll && _motor != null && !_motor.Rolling)
            {
                // A flip that is still tucked at touchdown gives way to the landing.
                CancelOneShot();
            }
        }

        private void OnDestroy()
        {
            if (_motor != null)
            {
                _motor.Stepped -= OnMotorStep;
            }

            if (_graph.IsValid())
            {
                _graph.Destroy();
            }
        }

        private void Update()
        {
            if (!IsReady)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            float speed = _motor != null ? _motor.HorizontalSpeed : 0f;
            var input = new PipPoseInput
            {
                Grounded = _motor == null || _motor.Grounded,
                VerticalSpeed = _motor != null ? _motor.VerticalSpeed : 0f,
                HorizontalSpeed = speed,
                Gliding = _gliding || (_motor != null && _motor.Gliding),
                WallSliding = _wallSliding || (_motor != null && _motor.WallSliding),
                LedgeHanging = _ledgeHanging || (_motor != null && _motor.Hanging),
                Kicked = _kickPending,
                Climbing = _motor != null && _motor.Climbing && _slot.ContainsKey(PipClips.LedgeClimb),
            };
            _kickPending = false;

            // A move that is over before its clip: the kick reached the ground, another wall or a ledge, or the
            // catch was let go.
            if ((OneShot == PipClips.WallKick && (input.Grounded || input.WallSliding || input.LedgeHanging)) ||
                (OneShot == PipClips.LedgeGrab && !input.LedgeHanging))
            {
                CancelOneShot();
            }

            // A glance belongs to standing still: the player walking off, a jump or a fall ends it at once.
            if (PipClips.IsGlance(OneShot) && (!input.Grounded || speed > GlanceMaxSpeed))
            {
                CancelOneShot();
            }

            PipPose pose = _tracker.Step(input, dt);
            if (_tracker.TookOff)
            {
                Restart(PipClips.JumpStart);
            }

            if (_tracker.Landed)
            {
                Restart(PipClips.Land);
            }

            System.Array.Clear(_targets, 0, _targets.Length);
            if (_oneShotSlot >= 0)
            {
                _oneShotRemaining -= dt;
                if (_oneShotRemaining <= _oneShotTail)
                {
                    if (_oneShotNext != null)
                    {
                        Restart(_oneShotNext);
                    }

                    CancelOneShot();
                }
                else
                {
                    _targets[_oneShotSlot] = 1f;
                }
            }

            if (_oneShotSlot < 0 && !TargetPose(pose))
            {
                TargetLocomotion(speed);
            }

            AdvanceLocomotionCycle(speed, dt);

            float total = 0f;
            float step = (_oneShotSlot >= 0 ? Mathf.Max(blendRate, _oneShotFadeRate) : blendRate) * dt;
            for (int i = 0; i < _weights.Length; i++)
            {
                _weights[i] = Mathf.MoveTowards(_weights[i], _targets[i], step);
                total += _weights[i];
            }

            int idle = _slot[PipClips.Idle];
            for (int i = 0; i < _weights.Length; i++)
            {
                _mixer.SetInputWeight(i, total > 1e-4f ? _weights[i] / total : (i == idle ? 1f : 0f));
            }
        }

        /// <summary>Targets the clip for a non-locomotion pose; false when the pose has no clip to show.</summary>
        private bool TargetPose(PipPose pose)
        {
            string clipName;
            switch (pose)
            {
                case PipPose.JumpStart: clipName = PipClips.JumpStart; break;
                case PipPose.JumpAir: clipName = PipClips.JumpAir; break;
                case PipPose.Fall: clipName = PipClips.Fall; break;
                case PipPose.Land: clipName = PipClips.Land; break;
                case PipPose.Glide: clipName = PipClips.Glide; break;
                case PipPose.WallSlide: clipName = PipClips.WallSlide; break;
                case PipPose.LedgeHang: clipName = PipClips.LedgeHang; break;
                default: return false;
            }

            int slot = SlotOf(clipName);
            if (slot < 0)
            {
                return false;
            }

            _targets[slot] += 1f;
            return true;
        }

        private void TargetLocomotion(float speed)
        {
            float fullRun = Mathf.Min(runClipSpeed, _maxSpeed);
            LocomotionWeights w = PipPoseTracker.Locomotion(speed, walkClipSpeed, fullRun);
            AddTarget(PipClips.Idle, w.Idle);
            AddTarget(PipClips.Walk, w.Walk);
            AddTarget(PipClips.Run, w.Run);
        }

        /// <summary>One shared gait phase drives both Walk and Run so their foot contacts line up in the blend.</summary>
        private void AdvanceLocomotionCycle(float speed, float dt)
        {
            if (_walkSlot < 0 && _runSlot < 0)
            {
                return;
            }

            float walkW = _walkSlot >= 0 ? _weights[_walkSlot] : 0f;
            float runW = _runSlot >= 0 && _runSlot != _walkSlot ? _weights[_runSlot] : 0f;
            float walkRate = _walkSlot >= 0
                ? PipPoseTracker.PlaybackRate(speed, walkClipSpeed) / _lengths[_walkSlot]
                : 0f;
            float runRate = _runSlot >= 0
                ? PipPoseTracker.PlaybackRate(speed, runClipSpeed) / _lengths[_runSlot]
                : walkRate;
            float sum = walkW + runW;
            float cyclesPerSecond = sum > 1e-4f ? (walkW * walkRate + runW * runRate) / sum
                : _walkSlot >= 0 ? walkRate : runRate;
            _cyclePhase = Mathf.Repeat(_cyclePhase + cyclesPerSecond * dt, 1f);
            if (_walkSlot >= 0)
            {
                _playables[_walkSlot].SetTime(_cyclePhase * _lengths[_walkSlot]);
            }

            if (_runSlot >= 0 && _runSlot != _walkSlot)
            {
                _playables[_runSlot].SetTime(_cyclePhase * _lengths[_runSlot]);
            }
        }

        private int SlotOf(string clipName)
        {
            string resolved = PipClips.Resolve(clipName, _slot.ContainsKey);
            return resolved != null ? _slot[resolved] : -1;
        }

        private void AddTarget(string clipName, float weight)
        {
            int slot = SlotOf(clipName);
            if (slot >= 0 && weight > 0f)
            {
                _targets[slot] += weight;
            }
        }

        private void Restart(string clipName)
        {
            int slot = SlotOf(clipName);
            if (slot >= 0 && slot != _walkSlot && slot != _runSlot)
            {
                _playables[slot].SetTime(0);
            }
        }

        private static void ReportOnce(string message)
        {
            if (Debug.isDebugBuild && s_reported.Add(message))
            {
                Debug.Log("[PipAnimator] " + message);
            }
        }
    }
}
