using System.Collections.Generic;
using SpookyStreet.Core.Animation;
using SpookyStreet.World;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

namespace SpookyStreet.Characters
{
    /// <summary>
    /// A non-player character built from an original rigged model in <c>Resources/Models/Characters/&lt;Name&gt;</c>
    /// (same pipeline and texture convention as Pip: <c>&lt;Name&gt;_BaseColor</c> and <c>_Normal</c> on every part,
    /// <c>&lt;Name&gt;_Face</c> on the <c>*_Face</c> mesh), kept at its native scale with its root on the ground.
    /// Animation is a small Playables graph on game time (pause freezes it) holding every clip of the model: one
    /// loop at a time with cross-fades (<see cref="PlayLoop"/>) and one-shots over it (<see cref="PlayOneShot"/>)
    /// that blend back to the loop. The root GameObject faces its +Z; the model under it is turned by
    /// <see cref="ModelYawOffset"/> because the exports face -Z (learned with Pip).
    /// </summary>
    public sealed class NpcAvatar : MonoBehaviour
    {
        public const string CharactersFolder = "Models/Characters";
        public const string FaceSuffix = "_Face";

        /// <summary>The exported characters face -Z in their own frame; this turns them to face the root's +Z.</summary>
        public const float ModelYawOffset = 180f;

        [Tooltip("Weight change per second; 4 gives ~0.25 s cross-fades.")]
        [SerializeField] private float blendRate = 4f;

        private readonly Dictionary<string, int> _slot = new Dictionary<string, int>();
        private PlayableGraph _graph;
        private AnimationMixerPlayable _mixer;
        private AnimationClipPlayable[] _playables;
        private float[] _lengths;
        private bool[] _loops;
        private float[] _weights;
        private float[] _targets;
        private int _loopSlot = -1;
        private int _oneShotSlot = -1;
        private float _oneShotRemaining;

        /// <summary>Model name (the FBX and texture prefix).</summary>
        public string ModelName { get; private set; }

        /// <summary>The instantiated model under this root.</summary>
        public Transform Model { get; private set; }

        public bool IsAnimated => _graph.IsValid();

        /// <summary>The loop currently shown (or being faded to); null without a graph.</summary>
        public string Loop { get; private set; }

        /// <summary>The one-shot playing over the loop, or null.</summary>
        public string OneShot { get; private set; }

        /// <summary>Clip names found in the model (cleaned of exporter prefixes).</summary>
        public IReadOnlyCollection<string> ClipNames => _slot.Keys;

        public static string PathFor(string model) => CharactersFolder + "/" + model + "/" + model;

        /// <summary>True when the character's model is committed.</summary>
        public static bool Exists(string model) => Resources.Load<GameObject>(PathFor(model)) != null;

        /// <summary>
        /// Spawns <paramref name="model"/> under <paramref name="parent"/> at <paramref name="position"/>, its root
        /// facing the Unity yaw <paramref name="facingYaw"/>, playing <paramref name="startLoop"/>. Returns null, and
        /// logs nothing, when the model is not committed. Without <paramref name="startLoop"/> the character shows its
        /// bind pose.
        /// </summary>
        public static NpcAvatar Spawn(MaterialLibrary materials, string model, Transform parent, Vector3 position,
            float facingYaw, string startLoop)
        {
            string folder = CharactersFolder + "/" + model;
            var kit = new ModelKit(materials, folder) { FallbackAtlas = null };
            var prefab = kit.Load(model);
            if (prefab == null)
            {
                return null;
            }

            var root = new GameObject(model);
            if (parent != null)
            {
                root.transform.SetParent(parent, false);
                root.layer = parent.gameObject.layer;
            }

            root.transform.localPosition = position;
            root.transform.localRotation = Quaternion.Euler(0f, facingYaw, 0f);

            var instance = Instantiate(prefab, root.transform, false);
            instance.name = model + "Model";
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.Euler(0f, ModelYawOffset, 0f) * prefab.transform.localRotation;
            ModelKit.SetLayer(instance.transform, root.layer);

            Material skin = kit.MaterialFor(model);
            var faceTexture = kit.Texture(model + FaceSuffix);
            Material face = faceTexture != null ? materials.Textured(faceTexture, null) : skin;
            foreach (var renderer in instance.GetComponentsInChildren<Renderer>(true))
            {
                bool isFace = renderer.gameObject.name.EndsWith(FaceSuffix, System.StringComparison.Ordinal);
                MaterialLibrary.ApplyAll(renderer, isFace ? face : skin);
                // Skinned bounds follow the bind pose; a seated clip moves the body well away from it.
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    skinned.updateWhenOffscreen = true;
                }
            }

            var avatar = root.AddComponent<NpcAvatar>();
            avatar.ModelName = model;
            avatar.Model = instance.transform;
            var animator = instance.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                animator = instance.AddComponent<Animator>();
            }

            avatar.Build(animator, Resources.LoadAll<AnimationClip>(PathFor(model)), startLoop);
            return avatar;
        }

        /// <summary>Depth-first search for a bone or part of the model by exact name.</summary>
        public Transform Find(string part) => ModelKit.FindDeep(Model, part);

        public bool HasClip(string clipName) => clipName != null && _slot.ContainsKey(clipName);

        /// <summary>Clip length in seconds (0 when absent).</summary>
        public float LengthOf(string clipName) => HasClip(clipName) ? _lengths[_slot[clipName]] : 0f;

        /// <summary>Current normalized mixer weight of <paramref name="clipName"/> (0 when absent).</summary>
        public float WeightOf(string clipName) =>
            IsAnimated && HasClip(clipName) ? _mixer.GetInputWeight(_slot[clipName]) : 0f;

        /// <summary>Cross-fades to the loop <paramref name="clipName"/>; false (no change) when it is missing.</summary>
        public bool PlayLoop(string clipName)
        {
            if (!IsAnimated || !HasClip(clipName))
            {
                return false;
            }

            _loopSlot = _slot[clipName];
            Loop = clipName;
            return true;
        }

        /// <summary>
        /// Plays <paramref name="clipName"/> once over the loop from its first frame, then blends back to the loop.
        /// False (no change) when the clip is missing.
        /// </summary>
        public bool PlayOneShot(string clipName)
        {
            if (!IsAnimated || !HasClip(clipName))
            {
                return false;
            }

            int slot = _slot[clipName];
            _playables[slot].SetTime(0);
            _playables[slot].SetSpeed(1);
            _oneShotSlot = slot;
            _oneShotRemaining = _lengths[slot];
            OneShot = clipName;
            return true;
        }

        private bool Build(Animator animator, IList<AnimationClip> clips, string startLoop)
        {
            if (animator == null || clips == null)
            {
                return false;
            }

            var found = new List<AnimationClip>();
            foreach (var clip in clips)
            {
                if (clip == null || clip.name.StartsWith("__preview__", System.StringComparison.Ordinal))
                {
                    continue;
                }

                string name = PipClips.CleanClipName(clip.name);
                if (!_slot.ContainsKey(name))
                {
                    _slot[name] = found.Count;
                    found.Add(clip);
                }
            }

            if (startLoop == null || !_slot.ContainsKey(startLoop))
            {
                _slot.Clear();
                if (Debug.isDebugBuild)
                {
                    Debug.Log($"[NpcAvatar] {ModelName} has no {startLoop} clip ({found.Count} clips); bind pose.");
                }

                return false;
            }

            animator.applyRootMotion = false;
            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            _graph = PlayableGraph.Create(ModelName + "Animation");
            _graph.SetTimeUpdateMode(DirectorUpdateMode.GameTime);
            _mixer = AnimationMixerPlayable.Create(_graph, found.Count);
            _playables = new AnimationClipPlayable[found.Count];
            _lengths = new float[found.Count];
            _loops = new bool[found.Count];
            _weights = new float[found.Count];
            _targets = new float[found.Count];
            // Different characters start their loops at different phases so they never breathe in step.
            int hash = 0;
            foreach (char c in ModelName)
            {
                hash = hash * 31 + c;
            }

            float phase = (hash & 0xffff) / 65536f;
            for (int i = 0; i < found.Count; i++)
            {
                _playables[i] = AnimationClipPlayable.Create(_graph, found[i]);
                _lengths[i] = Mathf.Max(1f / 30f, found[i].length);
                _loops[i] = CharacterClips.IsLooping(found[i].name);
                if (_loops[i])
                {
                    _playables[i].SetTime(phase * _lengths[i]);
                }

                _graph.Connect(_playables[i], 0, _mixer, i);
            }

            _loopSlot = _slot[startLoop];
            Loop = startLoop;
            _weights[_loopSlot] = 1f;
            _mixer.SetInputWeight(_loopSlot, 1f);
            var output = AnimationPlayableOutput.Create(_graph, ModelName, animator);
            output.SetSourcePlayable(_mixer);
            _graph.Play();
            return true;
        }

        private void Update()
        {
            if (!IsAnimated)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            System.Array.Clear(_targets, 0, _targets.Length);
            if (_oneShotSlot >= 0)
            {
                _oneShotRemaining -= dt;
                // End one fade early so the blend back overlaps the clip's tail, not a held last frame.
                if (_oneShotRemaining <= 1f / Mathf.Max(0.01f, blendRate))
                {
                    _oneShotSlot = -1;
                    OneShot = null;
                }
                else
                {
                    _targets[_oneShotSlot] = 1f;
                }
            }

            if (_oneShotSlot < 0)
            {
                _targets[_loopSlot] = 1f;
            }

            float total = 0f;
            float step = blendRate * dt;
            for (int i = 0; i < _weights.Length; i++)
            {
                _weights[i] = Mathf.MoveTowards(_weights[i], _targets[i], step);
                total += _weights[i];
                // Keep loop times small and wrapping even if a clip was imported without its loop flag.
                if (_loops[i])
                {
                    double time = _playables[i].GetTime();
                    if (time > _lengths[i])
                    {
                        _playables[i].SetTime(time % _lengths[i]);
                    }
                }
            }

            for (int i = 0; i < _weights.Length; i++)
            {
                _mixer.SetInputWeight(i, total > 1e-4f ? _weights[i] / total : (i == _loopSlot ? 1f : 0f));
            }
        }

        private void OnDestroy()
        {
            if (_graph.IsValid())
            {
                _graph.Destroy();
            }
        }
    }
}
