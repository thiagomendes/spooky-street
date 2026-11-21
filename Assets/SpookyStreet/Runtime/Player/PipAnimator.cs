using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SpookyStreet.Player
{
    /// <summary>
    /// Drives Pip's imported clips from <see cref="PipMotor"/> state with a small Playables graph, so no
    /// editor-authored Animator Controller is needed: idle/walk/run blend by speed, and the jump plays
    /// start → air loop → land. Weights ease toward their targets so transitions never pop. The graph uses
    /// game time, so pause (timeScale 0) freezes Pip mid-pose.
    /// </summary>
    public sealed class PipAnimator : MonoBehaviour
    {
        public const string Idle = "Idle";
        public const string Walk = "Walking_A";
        public const string Run = "Running_A";
        public const string JumpStart = "Jump_Start";
        public const string JumpAir = "Jump_Idle";
        public const string Land = "Jump_Land";

        private static readonly string[] Required = { Idle, Walk, Run, JumpStart, JumpAir, Land };

        [SerializeField] private float blendRate = 9f;
        [SerializeField] private float walkClipSpeed = 1.6f;
        [SerializeField] private float runClipSpeed = 4.5f;

        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private readonly Dictionary<string, int> _index = new Dictionary<string, int>();
        private AnimationClipPlayable[] _playables;
        private float[] _weights;
        private PipMotor _motor;
        private float _maxSpeed = 4f;
        private bool _wasGrounded = true;
        private float _startTimer;
        private float _landTimer;

        public bool IsReady => _graph.IsValid();

        /// <summary>Names of the clips found when the graph was built (for tests and diagnostics).</summary>
        public IReadOnlyCollection<string> ClipNames => _index.Keys;

        public static bool HasRequiredClips(IList<AnimationClip> clips)
        {
            foreach (var name in Required)
            {
                if (Find(clips, name) == null)
                {
                    return false;
                }
            }

            return true;
        }

        public void Build(Animator animator, IList<AnimationClip> clips, PipMotor motor, float maxSpeed)
        {
            _motor = motor;
            _maxSpeed = Mathf.Max(0.1f, maxSpeed);
            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;

            _graph = PlayableGraph.Create("PipAnimation");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            _mixer = AnimationMixerPlayable.Create(_graph, Required.Length);
            _playables = new AnimationClipPlayable[Required.Length];
            _weights = new float[Required.Length];
            for (int i = 0; i < Required.Length; i++)
            {
                var clip = Find(clips, Required[i]);
                if (clip == null)
                {
                    Debug.LogWarning($"[PipAnimator] Clip {Required[i]} missing.");
                    continue;
                }

                _index[Required[i]] = i;
                _playables[i] = AnimationClipPlayable.Create(_graph, clip);
                _graph.Connect(_playables[i], 0, _mixer, i);
            }

            _weights[0] = 1f;
            _mixer.SetInputWeight(0, 1f);
            var output = AnimationPlayableOutput.Create(_graph, "Pip", animator);
            output.SetSourcePlayable(_mixer);
            _graph.Play();
        }

        private static AnimationClip Find(IList<AnimationClip> clips, string name)
        {
            foreach (var clip in clips)
            {
                if (clip != null && (clip.name == name || clip.name.EndsWith("|" + name)))
                {
                    return clip;
                }
            }

            return null;
        }

        private void OnDestroy()
        {
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }
        }

        private void Update()
        {
            if (!IsReady || _motor == null)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            bool grounded = _motor.Grounded;
            if (_wasGrounded && !grounded && _motor.VerticalSpeed > 0.5f)
            {
                Restart(JumpStart);
                _startTimer = 0.22f;
            }
            else if (!_wasGrounded && grounded)
            {
                Restart(Land);
                _landTimer = 0.28f;
            }

            _wasGrounded = grounded;
            _startTimer -= dt;
            _landTimer -= dt;

            float speedN = Mathf.Clamp01(_motor.HorizontalSpeed / _maxSpeed);
            System.Array.Clear(_targets, 0, _targets.Length);
            if (!grounded)
            {
                Target(_startTimer > 0f ? JumpStart : JumpAir, 1f);
            }
            else if (_landTimer > 0f && speedN < 0.35f)
            {
                Target(Land, 1f);
            }
            else
            {
                // Idle → walk by 0.45 → run at full speed.
                float walk = speedN < 0.45f ? Mathf.InverseLerp(0.05f, 0.45f, speedN) : 1f - Mathf.InverseLerp(0.45f, 0.95f, speedN);
                float run = Mathf.InverseLerp(0.45f, 0.95f, speedN);
                Target(Idle, Mathf.Clamp01(1f - walk - run));
                Target(Walk, walk);
                Target(Run, run);
            }

            // Match foot speed to ground speed.
            SetSpeed(Walk, Mathf.Max(0.6f, _motor.HorizontalSpeed / walkClipSpeed));
            SetSpeed(Run, Mathf.Max(0.8f, _motor.HorizontalSpeed / runClipSpeed));

            float total = 0f;
            for (int i = 0; i < _weights.Length; i++)
            {
                _weights[i] = Mathf.MoveTowards(_weights[i], _targets[i], blendRate * dt);
                total += _weights[i];
            }

            for (int i = 0; i < _weights.Length; i++)
            {
                _mixer.SetInputWeight(i, total > 0f ? _weights[i] / total : (i == 0 ? 1f : 0f));
            }
        }

        private readonly float[] _targets = new float[6];

        private void Target(string name, float weight)
        {
            if (_index.TryGetValue(name, out int i))
            {
                _targets[i] = weight;
            }
        }

        private void Restart(string name)
        {
            if (_index.TryGetValue(name, out int i))
            {
                _playables[i].SetTime(0);
            }
        }

        private void SetSpeed(string name, float speed)
        {
            if (_index.TryGetValue(name, out int i))
            {
                _playables[i].SetSpeed(speed);
            }
        }
    }
}
