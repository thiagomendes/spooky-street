using System.Collections.Generic;
using SpookyStreet.App;
using SpookyStreet.Characters;
using SpookyStreet.Core.Crowd;
using SpookyStreet.Core.LivingStreet;
using SpookyStreet.World;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;
using UnityEngine.Rendering;

namespace SpookyStreet.LivingStreet
{
    /// <summary>
    /// One walker's body in the world. The crowd view moves its root along the loop and asks it to animate; the body
    /// decides how. Bodies never collide: walkers step aside for Pip by rule, the camera boom ignores them, and Pip never
    /// gets stuck on one (living-street §3.4 rule 4). The root faces its +Z.
    /// </summary>
    public abstract class CrowdBody
    {
        protected CrowdBody(Transform root)
        {
            Root = root;
        }

        public Transform Root { get; }

        /// <summary>The detail the budget gave this walker this frame.</summary>
        public WalkerDetail Detail { get; private set; } = WalkerDetail.Full;

        /// <summary>Drawn now (the readout's "drawn" count).</summary>
        public bool Drawn => Detail != WalkerDetail.Hidden;

        /// <summary>True for the crowd kit's merged bodies (false: a placeholder).</summary>
        public virtual bool IsKit => false;

        /// <summary>Draw calls this body costs while drawn (tests: the kit's merged walker is one).</summary>
        public abstract int DrawCalls { get; }

        public void SetDetail(WalkerDetail detail)
        {
            if (detail == Detail)
            {
                return;
            }

            Detail = detail;
            ApplyDetail(detail);
        }

        /// <summary>
        /// Poses the body from the Core's pose: <paramref name="time"/> is the walker's own clock, the pose's clip and clip
        /// time say which one-shot plays and where (Locomotion: walk or stand by its speed), <paramref name="dt"/> the
        /// animation step (the view passes 1/15 s for walkers animated at 15 Hz). Every change of clip crossfades.
        /// </summary>
        public abstract void Animate(float time, in WalkerPose pose, float dt);

        /// <summary>True when the body's model carries <paramref name="clip"/> (the art's new clips swap in by themselves).</summary>
        public virtual bool HasClip(string clip) => false;

        /// <summary>A bone of the body by name (null for a placeholder or a missing bone).</summary>
        public virtual Transform Bone(string name) => null;

        /// <summary>
        /// Where a sweet lands in this child's pail: the art contract's pail mouth in front of the child while it holds the
        /// pail out (HoldPail), or the prop's own bone when the clip is not in the model yet (the pail then hangs at its side).
        /// </summary>
        public Vector3 PailMouth()
        {
            if (!HasClip(CrowdClipTimings.HoldPail))
            {
                var pail = Bone("Pail");
                if (pail != null)
                {
                    return pail.position + Vector3.down * 0.05f;
                }
            }

            return Root.position + Root.forward * CrowdClipTimings.HoldPailMouthForward + Vector3.up * CrowdClipTimings.HoldPailMouthHeight;
        }

        /// <summary>The knuckles a sweet rides on while it is handed over (the art's bone, else the hand, else in front).</summary>
        public Vector3 TreatHand()
        {
            var hand = Bone(CrowdClipTimings.TreatHandBone) ?? Bone("Hand_L") ?? Bone("Hand_R");
            return hand != null ? hand.position
                : Root.position + Root.forward * CrowdClipTimings.GiveTreatReleaseForward + Vector3.up * CrowdClipTimings.GiveTreatReleaseHeight;
        }

        /// <summary>The walker's soft, solid body: one capsule Pip bumps into and slides round (null for door neighbours).</summary>
        public CapsuleCollider Capsule { get; private set; }

        /// <summary>The capsule's height: taller than any jump, so Pip never lands on a walker's head.</summary>
        public const float CapsuleHeight = 3.5f;

        /// <summary>
        /// Gives the walker its capsule (living street, owner 2026-11-28: walkers are solid, never ghosts) on the Ignore
        /// Raycast layer, like the street's invisible walls: it stops Pip's CharacterController and lets her slide round,
        /// but the camera boom, the ground and ledge probes and the Use sight lines ignore it. A kinematic body, so moving
        /// it every frame stays cheap. The Core makes walkers give way before Pip has to push (<c>GiveWay</c>).
        /// </summary>
        public void AddCapsule(bool grownUp)
        {
            var go = new GameObject("Body");
            go.transform.SetParent(Root, false);
            int layer = LayerMask.NameToLayer("Ignore Raycast");
            go.layer = layer >= 0 ? layer : 0;
            var capsule = go.AddComponent<CapsuleCollider>();
            capsule.radius = GiveWay.BodyRadius(grownUp);
            capsule.height = CapsuleHeight;
            capsule.center = new Vector3(0f, CapsuleHeight * 0.5f, 0f);
            capsule.direction = 1;
            var body = go.AddComponent<Rigidbody>();
            body.isKinematic = true;
            body.useGravity = false;
            body.interpolation = RigidbodyInterpolation.None;
            Capsule = capsule;
        }

        protected abstract void ApplyDetail(WalkerDetail detail);
    }

    /// <summary>
    /// The swap point for the crowd's bodies (living-street §7). A factory returns null for a look it cannot build, and
    /// the view falls back to the next one (the kit first, the placeholder when the kit's models are not committed).
    /// </summary>
    public interface ICrowdBodyFactory
    {
        CrowdBody Create(WalkerLook look, Transform parent, MaterialLibrary materials);
    }

    /// <summary>
    /// The crowd costume kit's walkers (docs/living-street-crowd-art.md; contract <see cref="CrowdKit"/>): the body's FBX
    /// is instantiated once per walker, the recipe's parts are merged at spawn into one skinned mesh per level of detail
    /// (every part shares the skeleton's bind pose, so the merge is a concatenation remapped onto one bone array: one draw
    /// call), the unused parts are destroyed, and the shared Crowd atlas is applied (its glow variant at 0.9 for lantern
    /// carriers, under the attic window). The kit's clips run on a small Playables graph that the view evaluates by hand,
    /// so culled walkers cost no animation at all and far walkers animate at 15 Hz. The Core says which clip plays and at
    /// what time (<see cref="WalkerPose.Clip"/>): the trick-or-treat clips of the art contract (<c>CrowdClipTimings</c>:
    /// WalkStart, WalkStop, HoldPail, Cheer, GiveTreat, OpenDoorPeek) play as soon as they are in the FBX, and until then
    /// the shipped Idle, Walk, Wave and Talk stand in (<see cref="WalkerClips.Fallback"/>). Every change crossfades. Walk
    /// plays at the walker's speed over the clip's own ground speed, so feet never slide. Merged meshes are cached per
    /// recipe.
    /// </summary>
    public sealed class CrowdKitBodyFactory : ICrowdBodyFactory
    {
        /// <summary>Glow of the paper lanterns some walkers carry (the art doc's level; below the attic window).</summary>
        public const float LanternGlow = 0.9f;

        public const string LanternPart = "Lantern_Paper";

        private sealed class Merged
        {
            public Mesh Mesh;
            public string[] Bones;
            public string RootBone;
        }

        private static readonly Dictionary<string, Merged> Cache = new Dictionary<string, Merged>();

        public CrowdBody Create(WalkerLook look, Transform parent, MaterialLibrary materials)
        {
            var recipe = look.KitRecipe;
            if (recipe == null || materials == null)
            {
                return null;
            }

            var kit = new ModelKit(materials, CrowdKit.Folder + "/") { FallbackAtlas = CrowdKit.Atlas };
            var prefab = kit.Load(recipe.Model);
            if (prefab == null)
            {
                return null;
            }

            var root = new GameObject("Walker_" + recipe.Id).transform;
            root.SetParent(parent, false);
            var model = Object.Instantiate(prefab, root, false);
            model.name = recipe.Model;
            model.transform.localPosition = Vector3.zero;
            model.transform.localRotation = Quaternion.Euler(0f, NpcAvatar.ModelYawOffset, 0f) * prefab.transform.localRotation;

            bool lantern = false;
            foreach (string part in recipe.Parts)
            {
                lantern |= part == LanternPart;
            }

            var material = lantern ? kit.GlowMaterialFor(recipe.Model, LanternGlow) : kit.MaterialFor(recipe.Model);
            var parts = model.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            var lod0 = Build(recipe, 0, parts, model.transform, material);
            var lod1 = Build(recipe, 1, parts, model.transform, material);
            foreach (var smr in parts)
            {
                Object.Destroy(smr.gameObject);
            }

            if (lod0 == null)
            {
                Object.Destroy(root.gameObject);
                return null;
            }

            var animator = model.GetComponentInChildren<Animator>();
            if (animator == null)
            {
                animator = model.AddComponent<Animator>();
            }

            animator.cullingMode = AnimatorCullingMode.AlwaysAnimate;
            var clips = Resources.LoadAll<AnimationClip>(CrowdKit.Folder + "/" + recipe.Model);
            var life = root.gameObject.AddComponent<CrowdWalkerLife>();
            return new KitBody(root, lod0, lod1, animator, clips, CrowdKit.WalkSpeed(recipe.Model), life, recipe.Model);
        }

        /// <summary>One merged skinned mesh of the recipe's parts at a level of detail (null when a part is missing).</summary>
        private static SkinnedMeshRenderer Build(CrowdWalkerRecipe recipe, int lod, SkinnedMeshRenderer[] parts, Transform model,
            Material material)
        {
            string key = recipe.Model + "/" + recipe.Id + "/" + lod;
            if (!Cache.TryGetValue(key, out var merged) || merged.Mesh == null)
            {
                merged = Merge(recipe, lod, parts);
                if (merged == null)
                {
                    return null;
                }

                Cache[key] = merged;
            }

            var bones = new Transform[merged.Bones.Length];
            for (int i = 0; i < bones.Length; i++)
            {
                bones[i] = ModelKit.FindDeep(model, merged.Bones[i]);
                if (bones[i] == null)
                {
                    return null;
                }
            }

            var go = new GameObject("Merged_LOD" + lod);
            go.transform.SetParent(model, false);
            var renderer = go.AddComponent<SkinnedMeshRenderer>();
            renderer.sharedMesh = merged.Mesh;
            renderer.bones = bones;
            renderer.rootBone = ModelKit.FindDeep(model, merged.RootBone) ?? bones[0];
            renderer.sharedMaterial = material;
            renderer.updateWhenOffscreen = true; // the view culls walkers itself; bounds follow the clips
            renderer.shadowCastingMode = ShadowCastingMode.Off; // a paper disc instead of a real-time shadow (§8)
            renderer.skinnedMotionVectors = false;
            return renderer;
        }

        private static Merged Merge(CrowdWalkerRecipe recipe, int lod, SkinnedMeshRenderer[] parts)
        {
            var names = CrowdKit.MeshNames(recipe, lod);
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var tangents = new List<Vector4>();
            var uvs = new List<Vector2>();
            var weights = new List<BoneWeight>();
            var triangles = new List<int>();
            var bones = new List<Transform>();
            var bindposes = new List<Matrix4x4>();
            var index = new Dictionary<Transform, int>();
            bool allTangents = true;
            string rootBone = null;
            foreach (string name in names)
            {
                SkinnedMeshRenderer part = null;
                foreach (var smr in parts)
                {
                    if (smr.name == name)
                    {
                        part = smr;
                        break;
                    }
                }

                var mesh = part != null ? part.sharedMesh : null;
                if (mesh == null || !mesh.isReadable)
                {
                    if (Debug.isDebugBuild)
                    {
                        Debug.LogWarning($"[Crowd] {recipe.Id}: part {name} missing or unreadable at LOD{lod}");
                    }

                    return null;
                }

                rootBone ??= part.rootBone != null ? part.rootBone.name : null;
                var partBones = part.bones;
                var partBind = mesh.bindposes;
                var map = new int[partBones.Length];
                for (int b = 0; b < partBones.Length; b++)
                {
                    if (!index.TryGetValue(partBones[b], out int k))
                    {
                        k = bones.Count;
                        bones.Add(partBones[b]);
                        bindposes.Add(b < partBind.Length ? partBind[b] : Matrix4x4.identity);
                        index[partBones[b]] = k;
                    }

                    map[b] = k;
                }

                int start = vertices.Count;
                int count = mesh.vertexCount;
                vertices.AddRange(mesh.vertices);
                var n = mesh.normals;
                normals.AddRange(n.Length == count ? n : new Vector3[count]);
                var t = mesh.tangents;
                allTangents &= t.Length == count;
                tangents.AddRange(t.Length == count ? t : new Vector4[count]);
                var uv = mesh.uv;
                uvs.AddRange(uv.Length == count ? uv : new Vector2[count]);
                foreach (var w in mesh.boneWeights)
                {
                    weights.Add(new BoneWeight
                    {
                        boneIndex0 = map[w.boneIndex0], weight0 = w.weight0,
                        boneIndex1 = map[w.boneIndex1], weight1 = w.weight1,
                        boneIndex2 = map[w.boneIndex2], weight2 = w.weight2,
                        boneIndex3 = map[w.boneIndex3], weight3 = w.weight3,
                    });
                }

                for (int s = 0; s < mesh.subMeshCount; s++)
                {
                    foreach (int i in mesh.GetTriangles(s))
                    {
                        triangles.Add(start + i);
                    }
                }
            }

            var combined = new Mesh
            {
                name = "Crowd_" + recipe.Id + "_LOD" + lod,
                indexFormat = vertices.Count > 65535 ? IndexFormat.UInt32 : IndexFormat.UInt16,
            };
            combined.SetVertices(vertices);
            combined.SetNormals(normals);
            if (allTangents)
            {
                combined.SetTangents(tangents);
            }

            combined.SetUVs(0, uvs);
            combined.boneWeights = weights.ToArray();
            combined.bindposes = bindposes.ToArray();
            combined.SetTriangles(triangles, 0);
            combined.RecalculateBounds();
            combined.UploadMeshData(true);
            var boneNames = new string[bones.Count];
            for (int i = 0; i < boneNames.Length; i++)
            {
                boneNames[i] = bones[i].name;
            }

            return new Merged { Mesh = combined, Bones = boneNames, RootBone = rootBone };
        }

        /// <summary>Triangles of a merged walker at LOD0 and LOD1 (tests and the log).</summary>
        public static int Triangles(CrowdBody body, int lod) =>
            body is KitBody kit ? kit.Triangles(lod) : 0;

        private sealed class KitBody : CrowdBody
        {
            /// <summary>Weight change per second of a crossfade (about 0.18 s).</summary>
            private const float BlendRate = 5.5f;
            private readonly SkinnedMeshRenderer _lod0;
            private readonly SkinnedMeshRenderer _lod1;
            private readonly AnimationClipPlayable[] _clips;
            private readonly float[] _weights;
            private readonly Dictionary<string, int> _slot = new Dictionary<string, int>();
            private readonly float _walkSpeed;
            private readonly CrowdWalkerLife _life;
            private AnimationMixerPlayable _mixer;
            private int _target = -1;
            private float _fastBlend;
            private readonly string[] _names;
            private readonly string _model;
            private readonly float[] _lengths;

            public KitBody(Transform root, SkinnedMeshRenderer lod0, SkinnedMeshRenderer lod1, Animator animator,
                AnimationClip[] clips, float walkSpeed, CrowdWalkerLife life, string model) : base(root)
            {
                _model = model;
                _lod0 = lod0;
                _lod1 = lod1;
                _walkSpeed = walkSpeed;
                _life = life;
                var graph = PlayableGraph.Create("Crowd_" + root.name);
                graph.SetTimeUpdateMode(DirectorUpdateMode.Manual);
                life.Graph = graph;
                var output = AnimationPlayableOutput.Create(graph, "Crowd", animator);
                _mixer = AnimationMixerPlayable.Create(graph, clips.Length);
                output.SetSourcePlayable(_mixer);
                _clips = new AnimationClipPlayable[clips.Length];
                _weights = new float[clips.Length];
                _names = new string[clips.Length];
                _lengths = new float[clips.Length];
                for (int i = 0; i < clips.Length; i++)
                {
                    _clips[i] = AnimationClipPlayable.Create(graph, clips[i]);
                    graph.Connect(_clips[i], 0, _mixer, i);
                    _mixer.SetInputWeight(i, 0f);
                    _names[i] = SpookyStreet.Core.Animation.PipClips.CleanClipName(clips[i].name);
                    _lengths[i] = clips[i].length;
                    if (!clips[i].name.StartsWith("__preview__", System.StringComparison.Ordinal))
                    {
                        _slot[_names[i]] = i;
                    }
                }

                graph.Play();
                int idle = Slot("Idle");
                if (idle >= 0)
                {
                    _target = idle;
                    _weights[idle] = 1f;
                    _mixer.SetInputWeight(idle, 1f);
                }

                graph.Evaluate(0f);
                ApplyDetail(WalkerDetail.Full);
            }

            public override bool IsKit => true;

            public override int DrawCalls => 1;

            public int Triangles(int lod)
            {
                var r = lod == 0 ? _lod0 : _lod1;
                return r != null && r.sharedMesh != null ? (int)(r.sharedMesh.GetIndexCount(0) / 3) : 0;
            }

            public override bool HasClip(string clip) => clip != null && _slot.ContainsKey(clip);

            public override Transform Bone(string name)
            {
                if (!_bones.TryGetValue(name, out var bone))
                {
                    bone = ModelKit.FindDeep(Root, name);
                    _bones[name] = bone;
                }

                return bone;
            }

            private readonly Dictionary<string, Transform> _bones = new Dictionary<string, Transform>();

            /// <summary>Clips that play once and are set to the Core's time each frame (they never advance on their own).</summary>
            private static bool Driven(string clip) =>
                clip == CrowdClipTimings.WalkStart || clip == CrowdClipTimings.WalkStop || clip == CrowdClipTimings.HoldPail ||
                clip == CrowdClipTimings.Cheer || clip == CrowdClipTimings.GiveTreat || clip == CrowdClipTimings.OpenDoorPeek ||
                clip == "Wave";

            private int Slot(string clip) => clip != null && _slot.TryGetValue(clip, out int slot) ? slot : -1;

            public override void Animate(float time, in WalkerPose pose, float dt)
            {
                if (!_life.Graph.IsValid())
                {
                    return;
                }

                // The Core's clip, or the shipped stand-in while the art's one is not in the model (WalkerClips.Fallback).
                string want = WalkerClips.Name(pose.Clip);
                float clipTime = pose.ClipTime;
                if (want != null && !HasClip(want))
                {
                    want = WalkerClips.Fallback(pose.Clip);
                }

                float speed = pose.Speed;
                if (want == null || want == "Walk")
                {
                    want = speed > 0.05f || pose.Turning ? "Walk" : "Idle";
                }

                int target = Slot(want);
                if (target < 0)
                {
                    target = Slot("Idle");
                    want = "Idle";
                }

                if (target < 0)
                {
                    return;
                }

                bool fromStart = _target >= 0 && _names[_target] == CrowdClipTimings.WalkStart;
                if (target != _target)
                {
                    // WalkStart ends on the Walk cycle at the art contract's phase: the walk picks up exactly there.
                    if (fromStart && want == "Walk")
                    {
                        _clips[target].SetTime(CrowdClipTimings.WalkStartEndPhase(_model) * _lengths[target]);
                        _fastBlend = 0.08f;
                    }
                    else if (want == "Wave" || Driven(want))
                    {
                        _fastBlend = 0f;
                    }

                    _target = target;
                }

                for (int i = 0; i < _clips.Length; i++)
                {
                    string name = _names[i];
                    if (Driven(name))
                    {
                        // One-shots are a pure function of the Core's clock (deterministic at any frame rate).
                        _clips[i].SetSpeed(0.0);
                        if (i == target)
                        {
                            _clips[i].SetTime(Mathf.Clamp(clipTime, 0f, _lengths[i]));
                        }
                    }
                    else if (name == "Walk")
                    {
                        float pace = pose.Turning && speed < 0.05f ? 0.35f : speed;
                        _clips[i].SetSpeed(pace > 0.05f ? Mathf.Clamp(pace / _walkSpeed, 0.2f, 1.5f) : 1f);
                    }
                }

                // Crossfade every change (no pops): about a fifth of a second, a quick hand-over out of WalkStart.
                float rate = _fastBlend > 0f ? 1f / _fastBlend : BlendRate;
                for (int i = 0; i < _weights.Length; i++)
                {
                    float goal = i == _target ? 1f : 0f;
                    _weights[i] = Mathf.MoveTowards(_weights[i], goal, rate * dt);
                    _mixer.SetInputWeight(i, _weights[i]);
                }

                if (_weights[_target] >= 0.999f)
                {
                    _fastBlend = 0f;
                }

                _life.Graph.Evaluate(dt);
            }

            protected override void ApplyDetail(WalkerDetail detail)
            {
                bool near = detail == WalkerDetail.Full || _lod1 == null;
                bool shown = detail != WalkerDetail.Hidden;
                _lod0.enabled = shown && near;
                if (_lod1 != null)
                {
                    _lod1.enabled = shown && !near;
                }
            }
        }
    }

    /// <summary>Destroys a kit walker's Playables graph with it (graphs are not garbage collected).</summary>
    public sealed class CrowdWalkerLife : MonoBehaviour
    {
        public PlayableGraph Graph { get; set; }

        private void OnDestroy()
        {
            if (Graph.IsValid())
            {
                Graph.Destroy();
            }
        }
    }

    /// <summary>
    /// The fallback when the crowd kit's models are not committed (a stripped checkout): felt-coloured primitives at the
    /// kit's heights with a procedural walk swing. Never shipped art.
    /// </summary>
    public sealed class PlaceholderCrowdBodies : ICrowdBodyFactory
    {
        public CrowdBody Create(WalkerLook look, Transform parent, MaterialLibrary materials) =>
            new PlaceholderBody(look, parent, materials);

        private sealed class PlaceholderBody : CrowdBody
        {
            private readonly Transform _legL;
            private readonly Transform _legR;
            private readonly float _height;
            private readonly List<Renderer> _all = new List<Renderer>();

            public PlaceholderBody(WalkerLook look, Transform parent, MaterialLibrary materials)
                : base(new GameObject("Walker_" + look.Recipe).transform)
            {
                Root.SetParent(parent, false);
                _height = look.Body == CrowdBodyKind.GrownUp ? CrowdKit.AdultHeight : CrowdKit.ChildHeight;
                float h = _height;
                int hash = (look.Recipe ?? string.Empty).GetHashCode() & 0x7fffffff;
                var colours = new[] { Palette.HauntedPurple, Palette.Plum, Palette.NightViolet, Palette.InkBlack };
                var main = colours[hash % colours.Length];
                Part(PrimitiveType.Capsule, Root, "Torso", new Vector3(0f, h * 0.53f, 0f), new Vector3(h * 0.3f, h * 0.24f, h * 0.22f), main, materials);
                Part(PrimitiveType.Sphere, Root, "Head", new Vector3(0f, h * 0.82f, 0f), Vector3.one * h * 0.22f, Palette.PipFace, materials);
                _legL = Limb(new Vector3(-h * 0.07f, h * 0.36f, 0f), h, main, materials);
                _legR = Limb(new Vector3(h * 0.07f, h * 0.36f, 0f), h, main, materials);
            }

            public override int DrawCalls => _all.Count;

            private Transform Limb(Vector3 hip, float h, Color color, MaterialLibrary materials)
            {
                var pivot = new GameObject("Leg").transform;
                pivot.SetParent(Root, false);
                pivot.localPosition = hip;
                Part(PrimitiveType.Capsule, pivot, "LegMesh", new Vector3(0f, -h * 0.18f, 0f), new Vector3(h * 0.08f, h * 0.18f, h * 0.08f), color, materials);
                return pivot;
            }

            private void Part(PrimitiveType type, Transform parent, string name, Vector3 at, Vector3 scale, Color color, MaterialLibrary materials)
            {
                var go = GameObject.CreatePrimitive(type);
                go.name = name;
                Object.Destroy(go.GetComponent<Collider>());
                go.transform.SetParent(parent, false);
                go.transform.localPosition = at;
                go.transform.localScale = scale;
                var renderer = go.GetComponent<Renderer>();
                materials?.Apply(renderer, color);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                _all.Add(renderer);
            }

            public override void Animate(float time, in WalkerPose pose, float dt)
            {
                float stride = pose.Speed > 0.05f || pose.Turning ? Mathf.Sin(time * Mathf.PI * 2f * 1.8f) * 28f : 0f;
                _legL.localRotation = Quaternion.Euler(stride, 0f, 0f);
                _legR.localRotation = Quaternion.Euler(-stride, 0f, 0f);
            }

            protected override void ApplyDetail(WalkerDetail detail)
            {
                foreach (var r in _all)
                {
                    r.enabled = detail != WalkerDetail.Hidden;
                }
            }
        }
    }
}
