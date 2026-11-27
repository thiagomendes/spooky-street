using System.Collections.Generic;
using SpookyStreet.App;
using SpookyStreet.Core.LivingStreet;
using SpookyStreet.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpookyStreet.LivingStreet
{
    /// <summary>
    /// One walker's body in the world. The crowd view moves its root along the loop and asks it to animate; the body
    /// decides how (placeholder primitives now, the crowd kit's skinned mesh later). Bodies never collide: walkers step
    /// aside for Pip by rule, the camera boom ignores them, and Pip never gets stuck on one (living-street §3.4 rule 4).
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

        /// <summary>Renderers drawn now (the readout's "drawn" count).</summary>
        public bool Drawn => Detail != WalkerDetail.Hidden;

        public void SetDetail(WalkerDetail detail)
        {
            if (detail == Detail)
            {
                return;
            }

            Detail = detail;
            ApplyDetail(detail);
        }

        /// <summary>Poses the body for <paramref name="time"/> seconds of its own clock.</summary>
        public abstract void Animate(float time, float speed, WalkerGesture gesture, float dt);

        protected abstract void ApplyDetail(WalkerDetail detail);
    }

    /// <summary>
    /// The swap point for the crowd's bodies (living-street §7; the costume kit is built on
    /// <c>feature/living-street-crowd-art</c>, its prefab and part names in docs/living-street-crowd-art.md). A factory
    /// returns null for a look it cannot build, and the view then falls back to the placeholder.
    /// </summary>
    public interface ICrowdBodyFactory
    {
        CrowdBody Create(WalkerLook look, Transform parent, MaterialLibrary materials);
    }

    /// <summary>
    /// The crowd kit's bodies, when committed: <c>Resources/Models/Crowd/Crowd_&lt;Body&gt;_&lt;costume&gt;</c> prefabs
    /// (one merged skinned mesh per walker on the shared crowd atlas, one draw call) with an Animator whose states are the
    /// kit's clips (<see cref="Clips"/>). Until the kit lands every look falls back to the placeholders, so this costs
    /// nothing now. Names live here only, so the art branch's doc and this code meet in one place.
    /// </summary>
    public sealed class CrowdKitBodyFactory : ICrowdBodyFactory
    {
        public const string Folder = "Models/Crowd/";

        /// <summary>The kit's clip (Animator state) names (living-street §7).</summary>
        public static class Clips
        {
            public const string Stroll = "Stroll";
            public const string Idle = "Idle";
            public const string HoldOutPail = "HoldOutPail";
            public const string Cheer = "Cheer";
            public const string Wave = "Wave";
            public const string LookUp = "LookUp";
        }

        /// <summary>Paper standee child shown beyond 18 m (a flat cut-out with a 4-frame bob), if the prefab has one.</summary>
        public const string StandeeChild = "Standee";

        public static string PrefabName(WalkerLook look) =>
            "Crowd_" + (look.Body == CrowdBodyKind.GrownUp ? "GrownUp" : "SmallChild") + "_" + look.Costume;

        public CrowdBody Create(WalkerLook look, Transform parent, MaterialLibrary materials)
        {
            var prefab = Resources.Load<GameObject>(Folder + PrefabName(look));
            if (prefab == null)
            {
                return null;
            }

            var instance = Object.Instantiate(prefab, parent, false);
            instance.name = PrefabName(look);
            return new KitBody(instance.transform);
        }

        private sealed class KitBody : CrowdBody
        {
            private readonly Animator _animator;
            private readonly Renderer[] _renderers;
            private readonly Transform _standee;
            private string _state;

            public KitBody(Transform root) : base(root)
            {
                _animator = root.GetComponentInChildren<Animator>();
                _renderers = root.GetComponentsInChildren<Renderer>(true);
                _standee = ModelKit.FindDeep(root, StandeeChild);
                foreach (var r in _renderers)
                {
                    r.shadowCastingMode = ShadowCastingMode.Off;
                }
            }

            public override void Animate(float time, float speed, WalkerGesture gesture, float dt)
            {
                if (_animator == null)
                {
                    return;
                }

                string state = gesture == WalkerGesture.HoldOut ? Clips.HoldOutPail
                    : gesture == WalkerGesture.Cheer ? Clips.Cheer
                    : gesture == WalkerGesture.Look ? Clips.LookUp
                    : speed > 0.05f ? Clips.Stroll : Clips.Idle;
                if (state != _state)
                {
                    _state = state;
                    _animator.CrossFade(state, 0.2f);
                }

                // The view decides when to animate (15 Hz far away), so the animator runs by hand.
                _animator.Update(dt);
            }

            protected override void ApplyDetail(WalkerDetail detail)
            {
                bool shown = detail != WalkerDetail.Hidden;
                bool standee = detail == WalkerDetail.Standee && _standee != null;
                foreach (var r in _renderers)
                {
                    bool isStandee = _standee != null && r.transform.IsChildOf(_standee);
                    r.enabled = shown && (standee ? isStandee : !isStandee);
                }

                if (_animator != null)
                {
                    // Updated by hand in Animate; culled walkers do no animation work at all.
                    _animator.enabled = false;
                }
            }
        }
    }

    /// <summary>
    /// Placeholder bodies until the crowd kit lands: felt-coloured primitives with the kit's proportions (a small child
    /// about 1.05 m, a grown-up about 1.7 m), a costume read from colour and one silhouette piece (a pointed hat, cat
    /// ears, a crown, a sheet, a box head), a hand prop, and a soft paper disc under the feet instead of a shadow. Walk
    /// swing, bob and the gestures are posed procedurally. Never final art.
    /// </summary>
    public sealed class PlaceholderCrowdBodies : ICrowdBodyFactory
    {
        private static Mesh _cone;

        public CrowdBody Create(WalkerLook look, Transform parent, MaterialLibrary materials) =>
            new PlaceholderBody(look, parent, materials);

        /// <summary>The costume's two colours (main, accent) from the v1.3 Halloween families.</summary>
        public static void Colours(WalkerLook look, out Color main, out Color accent)
        {
            switch (look.Costume)
            {
                case CrowdLooks.GhostSheet: main = Palette.Hex(0xE8DCC2); accent = Palette.InkBlack; break;
                case CrowdLooks.Skeleton: main = Palette.InkBlack; accent = Palette.Hex(0xE6DDC8); break;
                case CrowdLooks.BlackCat: main = Palette.Hex(0x2A2433); accent = Palette.JackOrange; break;
                case CrowdLooks.WitchGreen: main = Palette.Hex(0x4F7A3A); accent = Palette.HauntedPurple; break;
                case CrowdLooks.WitchPlum: main = Palette.Plum; accent = Palette.InkBlack; break;
                case CrowdLooks.Spider: main = Palette.HauntedPurple; accent = Palette.InkBlack; break;
                case CrowdLooks.CandyCorn: main = Palette.CandyCornYellow; accent = Palette.JackOrange; break;
                case CrowdLooks.Robot: main = Palette.Hex(0xB58A5A); accent = Palette.Hex(0x8FA8C8); break;
                case CrowdLooks.Bumblebee: main = Palette.CandyCornYellow; accent = Palette.InkBlack; break;
                case CrowdLooks.FrogPrince: main = Palette.Hex(0x5E9A3A); accent = Palette.CandyCornYellow; break;
                case CrowdLooks.PaperCrown: main = Palette.Hex(0x6B3D7A); accent = Palette.CandyCornYellow; break;
                default: main = Palette.Hex(0x7E6C5F); accent = Palette.Plum; break;
            }

            if (look.Palette % 2 == 1)
            {
                main = Color.Lerp(main, Palette.NightViolet, 0.18f);
            }
        }

        internal static Mesh Cone()
        {
            if (_cone != null)
            {
                return _cone;
            }

            const int sides = 12;
            var vertices = new List<Vector3> { new Vector3(0f, 1f, 0f), Vector3.zero };
            for (int i = 0; i <= sides; i++)
            {
                float a = i * Mathf.PI * 2f / sides;
                vertices.Add(new Vector3(Mathf.Cos(a) * 0.5f, 0f, Mathf.Sin(a) * 0.5f));
            }

            var triangles = new List<int>();
            for (int i = 0; i < sides; i++)
            {
                triangles.AddRange(new[] { 0, 2 + i + 1, 2 + i });
                triangles.AddRange(new[] { 1, 2 + i, 2 + i + 1 });
            }

            _cone = new Mesh { name = "CrowdCone", vertices = vertices.ToArray(), triangles = triangles.ToArray() };
            _cone.RecalculateNormals();
            _cone.RecalculateBounds();
            return _cone;
        }

        private sealed class PlaceholderBody : CrowdBody
        {
            private readonly Transform _legL;
            private readonly Transform _legR;
            private readonly Transform _armL;
            private readonly Transform _armR;
            private readonly Transform _torso;
            private readonly Transform _head;
            private readonly float _height;
            private readonly List<Renderer> _limbs = new List<Renderer>();
            private readonly List<Renderer> _all = new List<Renderer>();

            public PlaceholderBody(WalkerLook look, Transform parent, MaterialLibrary materials)
                : base(new GameObject("Walker_" + look.Costume).transform)
            {
                Root.SetParent(parent, false);
                bool adult = look.Body == CrowdBodyKind.GrownUp;
                _height = adult ? 1.7f : 1.05f;
                float h = _height;
                Colours(look, out var main, out var accent);
                var skin = Palette.PipFace;

                _torso = Pivot("Body", new Vector3(0f, h * 0.36f, 0f));
                Part(PrimitiveType.Capsule, _torso, "Torso", new Vector3(0f, h * 0.17f, 0f),
                    new Vector3(h * 0.3f, h * 0.24f, h * 0.22f), main, materials, false);
                _head = Pivot("Head", new Vector3(0f, h * 0.8f, 0f));
                Part(PrimitiveType.Sphere, _head, "Face", Vector3.zero, Vector3.one * h * (adult ? 0.17f : 0.24f),
                    look.Costume == CrowdLooks.GhostSheet ? main : skin, materials, false);
                _legL = Limb("Leg_L", new Vector3(-h * 0.07f, h * 0.36f, 0f), h * 0.36f, h * 0.08f, accent, materials);
                _legR = Limb("Leg_R", new Vector3(h * 0.07f, h * 0.36f, 0f), h * 0.36f, h * 0.08f, accent, materials);
                _armL = Limb("Arm_L", new Vector3(-h * 0.17f, h * 0.66f, 0f), h * 0.3f, h * 0.065f, main, materials);
                _armR = Limb("Arm_R", new Vector3(h * 0.17f, h * 0.66f, 0f), h * 0.3f, h * 0.065f, main, materials);
                Costume(look, h, main, accent, materials);
                Prop(look, h, materials);

                // A soft paper disc under the feet instead of a real-time shadow (§8).
                var disc = Part(PrimitiveType.Cylinder, Root, "ShadowDisc", new Vector3(0f, 0.012f, 0f),
                    new Vector3(h * 0.42f, 0.004f, h * 0.42f), Palette.NightAmbientGround, materials, false);
                disc.receiveShadows = false;
            }

            private Transform Pivot(string name, Vector3 at)
            {
                var t = new GameObject(name).transform;
                t.SetParent(Root, false);
                t.localPosition = at;
                return t;
            }

            private Transform Limb(string name, Vector3 joint, float length, float thickness, Color color, MaterialLibrary materials)
            {
                var pivot = Pivot(name, joint);
                Part(PrimitiveType.Capsule, pivot, name + "_Mesh", new Vector3(0f, -length * 0.5f, 0f),
                    new Vector3(thickness, length * 0.5f, thickness), color, materials, true);
                return pivot;
            }

            private Renderer Part(PrimitiveType type, Transform parent, string name, Vector3 at, Vector3 scale, Color color,
                MaterialLibrary materials, bool limb)
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
                if (limb)
                {
                    _limbs.Add(renderer);
                }

                return renderer;
            }

            private Renderer ConePart(Transform parent, string name, Vector3 at, Vector3 scale, Color color, MaterialLibrary materials)
            {
                var go = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer));
                go.transform.SetParent(parent, false);
                go.transform.localPosition = at;
                go.transform.localScale = scale;
                go.GetComponent<MeshFilter>().sharedMesh = Cone();
                var renderer = go.GetComponent<MeshRenderer>();
                materials?.Apply(renderer, color);
                renderer.shadowCastingMode = ShadowCastingMode.Off;
                _all.Add(renderer);
                return renderer;
            }

            /// <summary>One silhouette piece per costume, so groups read apart at phone size.</summary>
            private void Costume(WalkerLook look, float h, Color main, Color accent, MaterialLibrary materials)
            {
                float head = h * 0.12f;
                switch (look.Costume)
                {
                    case CrowdLooks.GhostSheet:
                        // A sheet falling from the head: a wide cone over the body, two ink eyes.
                        ConePart(Root, "Sheet", new Vector3(0f, 0.05f, 0f), new Vector3(h * 0.5f, h * 0.86f, h * 0.5f), main, materials);
                        Part(PrimitiveType.Sphere, _head, "Eye_L", new Vector3(-head * 0.35f, head * 0.15f, head * 0.85f),
                            Vector3.one * head * 0.28f, accent, materials, false);
                        Part(PrimitiveType.Sphere, _head, "Eye_R", new Vector3(head * 0.35f, head * 0.15f, head * 0.85f),
                            Vector3.one * head * 0.28f, accent, materials, false);
                        break;
                    case CrowdLooks.BlackCat:
                        ConePart(_head, "Ear_L", new Vector3(-head * 0.55f, head * 0.7f, 0f), new Vector3(head * 0.6f, head * 0.9f, head * 0.35f), main, materials);
                        ConePart(_head, "Ear_R", new Vector3(head * 0.55f, head * 0.7f, 0f), new Vector3(head * 0.6f, head * 0.9f, head * 0.35f), main, materials);
                        break;
                    case CrowdLooks.WitchGreen:
                    case CrowdLooks.WitchPlum:
                        Part(PrimitiveType.Cylinder, _head, "HatBrim", new Vector3(0f, head * 0.7f, 0f), new Vector3(head * 3.2f, 0.01f, head * 3.2f), accent, materials, false);
                        ConePart(_head, "Hat", new Vector3(0f, head * 0.7f, 0f), new Vector3(head * 1.6f, head * 3.2f, head * 1.6f), accent, materials);
                        break;
                    case CrowdLooks.Robot:
                        Part(PrimitiveType.Cube, _head, "BoxHead", new Vector3(0f, head * 0.2f, 0f), Vector3.one * head * 2.3f, main, materials, false);
                        Part(PrimitiveType.Cylinder, _head, "Antenna", new Vector3(0f, head * 1.7f, 0f), new Vector3(head * 0.15f, head * 0.6f, head * 0.15f), accent, materials, false);
                        break;
                    case CrowdLooks.Bumblebee:
                        for (int i = 0; i < 2; i++)
                        {
                            Part(PrimitiveType.Cylinder, _torso, "Stripe" + i, new Vector3(0f, h * (0.12f + i * 0.1f), 0f),
                                new Vector3(h * 0.31f, h * 0.02f, h * 0.23f), accent, materials, false);
                        }

                        Part(PrimitiveType.Sphere, _head, "Antenna_L", new Vector3(-head * 0.5f, head * 1.3f, 0f), Vector3.one * head * 0.3f, accent, materials, false);
                        Part(PrimitiveType.Sphere, _head, "Antenna_R", new Vector3(head * 0.5f, head * 1.3f, 0f), Vector3.one * head * 0.3f, accent, materials, false);
                        break;
                    case CrowdLooks.Skeleton:
                        for (int i = 0; i < 3; i++)
                        {
                            Part(PrimitiveType.Cube, _torso, "Rib" + i, new Vector3(0f, h * (0.12f + i * 0.06f), h * 0.11f),
                                new Vector3(h * 0.2f, h * 0.018f, 0.01f), accent, materials, false);
                        }

                        break;
                    case CrowdLooks.PaperCrown:
                        Part(PrimitiveType.Cylinder, _head, "Crown", new Vector3(0f, head * 0.95f, 0f), new Vector3(head * 1.5f, head * 0.35f, head * 1.5f), accent, materials, false);
                        break;
                    case CrowdLooks.Spider:
                        for (int i = 0; i < 4; i++)
                        {
                            float side = i < 2 ? -1f : 1f;
                            var leg = Part(PrimitiveType.Capsule, _torso, "SpiderLeg" + i,
                                new Vector3(side * h * 0.2f, h * (0.12f + (i % 2) * 0.09f), 0f), new Vector3(h * 0.025f, h * 0.12f, h * 0.025f),
                                accent, materials, false);
                            leg.transform.localRotation = Quaternion.Euler(0f, 0f, side * 60f);
                        }

                        break;
                }
            }

            private void Prop(WalkerLook look, float h, MaterialLibrary materials)
            {
                if (look.Prop == null)
                {
                    return;
                }

                Color color = look.Prop == "pail" ? Palette.JackOrange : look.Prop == "bag" ? Palette.WarmPaper : Palette.CandyCornYellow;
                var prop = Part(look.Prop == "bag" ? PrimitiveType.Cube : PrimitiveType.Cylinder, _armR, "Prop_" + look.Prop,
                    new Vector3(0f, -h * 0.32f, 0f), new Vector3(h * 0.13f, h * 0.07f, h * 0.13f), color, materials, true);
                if (look.Prop == "lantern" && materials != null)
                {
                    prop.sharedMaterial = materials.Glow(Palette.CandyCornYellow, 1.2f);
                }
            }

            public override void Animate(float time, float speed, WalkerGesture gesture, float dt)
            {
                float stride = speed > 0.05f ? Mathf.Sin(time * Mathf.PI * 2f * (1.6f + speed * 0.4f)) : 0f;
                float swing = 28f * Mathf.Clamp01(speed / 1.1f);
                _legL.localRotation = Quaternion.Euler(stride * swing, 0f, 0f);
                _legR.localRotation = Quaternion.Euler(-stride * swing, 0f, 0f);
                float bob = speed > 0.05f ? Mathf.Abs(stride) * 0.03f * _height : Mathf.Sin(time * 1.7f) * 0.006f;
                _torso.localPosition = new Vector3(0f, _height * 0.36f + bob, 0f);
                _head.localPosition = new Vector3(0f, _height * 0.8f + bob, 0f);
                float armL = -stride * swing * 0.8f;
                float armR = stride * swing * 0.8f;
                float headTilt = 0f;
                switch (gesture)
                {
                    case WalkerGesture.HoldOut:
                        armR = -70f;
                        armL = -20f;
                        break;
                    case WalkerGesture.Cheer:
                        float hop = Mathf.Abs(Mathf.Sin(time * 9f));
                        armL = armR = -165f + hop * 15f;
                        _torso.localPosition += new Vector3(0f, hop * 0.05f, 0f);
                        _head.localPosition += new Vector3(0f, hop * 0.05f, 0f);
                        break;
                    case WalkerGesture.Look:
                        headTilt = -12f;
                        break;
                    case WalkerGesture.Wait:
                        headTilt = Mathf.Sin(time * 0.8f) * 6f;
                        break;
                }

                _armL.localRotation = Quaternion.Euler(armL, 0f, 0f);
                _armR.localRotation = Quaternion.Euler(armR, 0f, 0f);
                _head.localRotation = Quaternion.Euler(headTilt, gesture == WalkerGesture.Wait ? Mathf.Sin(time * 0.5f) * 20f : 0f, 0f);
            }

            protected override void ApplyDetail(WalkerDetail detail)
            {
                bool shown = detail != WalkerDetail.Hidden;
                foreach (var r in _all)
                {
                    r.enabled = shown;
                }

                if (detail == WalkerDetail.Standee)
                {
                    // Far away: the limbs go (a flat paper read of the body), and nothing animates.
                    foreach (var r in _limbs)
                    {
                        r.enabled = false;
                    }
                }
            }
        }
    }
}
