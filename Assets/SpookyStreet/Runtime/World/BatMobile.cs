using System.Collections.Generic;
using SpookyStreet.App;
using SpookyStreet.Core.Keepsakes;
using SpookyStreet.Core.World;
using UnityEngine;

namespace SpookyStreet.World
{
    /// <summary>
    /// The bat mobile on Pip's porch (S004 §2.1, stream A2): the diegetic count of the paper bats found. It hangs from a
    /// lantern pole on the arrival porch's east rail, in the title frame's upper right. Each found bat hangs on its hook
    /// (hook <i>n</i> with bat <i>n</i>, so there is never an empty hook and never a number); rings 2 and 3 appear with
    /// their first bat, and at 10, 20 and 30 a ring closes round its little lantern, which lights
    /// (<see cref="BatMobileRules"/>). The model is <c>BatMobile.fbx</c> (atlas <c>Keepsakes</c>); each hanging bat is a
    /// <c>PaperBatKeepsake</c> at <see cref="KeepsakeArt.MobileBatScale"/>, wings nearly open, turned outward, turning
    /// slowly with the mobile (still with reduced motion). The pole is a painted porch post made in code, like the moving boxes.
    /// </summary>
    public sealed class BatMobile : MonoBehaviour
    {
        /// <summary>Wings of a hanging bat: this far (degrees) short of fully open.</summary>
        public const float HangingWingDegrees = 25f;

        /// <summary>The mobile's slow turn (degrees per second) and its sway.</summary>
        public const float TurnDegreesPerSecond = 6f;

        private readonly List<Transform> _hooks = new List<Transform>();
        private readonly List<GameObject> _bats = new List<GameObject>();
        private readonly GameObject[] _rings = new GameObject[KeepsakeArt.MobileRings];
        private readonly GameObject[] _closers = new GameObject[KeepsakeArt.MobileRings];
        private readonly GameObject[] _lanterns = new GameObject[KeepsakeArt.MobileRings];
        private Transform _model;
        private float _turn;

        /// <summary>Bats hanging now (tests: the visible count).</summary>
        public int BatsShown { get; private set; }

        /// <summary>True when the mobile model was found.</summary>
        public bool HasModel => _model != null;

        public Transform Model => _model;

        public bool LanternLit(int ring) => ring >= 1 && ring <= _lanterns.Length && _lanterns[ring - 1] != null &&
                                            _lanterns[ring - 1].activeSelf;

        public bool HookVisible(int hook) => hook >= 1 && hook <= _hooks.Count && _hooks[hook - 1] != null &&
                                             _hooks[hook - 1].gameObject.activeSelf;

        /// <summary>The bat hanging on hook 1..30 (null when hidden or missing).</summary>
        public GameObject BatOn(int hook) => hook >= 1 && hook <= _bats.Count ? _bats[hook - 1] : null;

        public static BatMobile Create(Transform parent, MaterialLibrary materials)
        {
            var go = new GameObject("BatMobile");
            go.transform.SetParent(parent, false);
            var mobile = go.AddComponent<BatMobile>();
            mobile.Build(materials);
            return mobile;
        }

        private void Build(MaterialLibrary materials)
        {
            BuildPole(materials);
            if (materials == null)
            {
                return;
            }

            var kit = new ModelKit(materials, EnvironmentDressing.ModelFolder) { FallbackAtlas = KeepsakeArt.KeepsakesAtlas };
            var hook = new Vector3(KeepsakePorch.MobileX, KeepsakePorch.MobileY, KeepsakePorch.MobileZ);
            var spin = new GameObject("MobileSpin").transform;
            spin.SetParent(transform, false);
            spin.position = hook;
            var model = kit.Spawn(KeepsakeArt.BatMobile, spin, Vector3.zero);
            if (model == null)
            {
                Debug.LogWarning("[BatMobile] Missing model " + KeepsakeArt.BatMobile);
                return;
            }

            _model = spin;
            for (int ring = 1; ring <= KeepsakeArt.MobileRings; ring++)
            {
                _rings[ring - 1] = Find(model.transform, KeepsakeArt.Ring(ring));
                _closers[ring - 1] = Find(model.transform, KeepsakeArt.RingClose(ring));
                _lanterns[ring - 1] = Find(model.transform, KeepsakeArt.RingLantern(ring));
            }

            for (int n = 1; n <= KeepsakeArt.MobileHooks; n++)
            {
                var at = ModelKit.FindDeep(model.transform, KeepsakeArt.Hook(n));
                _hooks.Add(at);
                _bats.Add(at != null ? HangBat(kit, spin, at, n) : null);
            }

            Show(0);
        }

        private static GameObject Find(Transform root, string name)
        {
            var found = ModelKit.FindDeep(root, name);
            return found != null ? found.gameObject : null;
        }

        /// <summary>
        /// A bat under its hook: its ears at the hook's pivot, turned outward from the mobile's axis, wings nearly open.
        /// Parented to the spin root (not the hook: an imported child keeps the builder's axes), so it stays upright.
        /// </summary>
        private static GameObject HangBat(ModelKit kit, Transform spin, Transform hook, int number)
        {
            Vector3 at = spin.InverseTransformPoint(hook.position);
            var holder = new GameObject("MobileBat_" + number.ToString("00")).transform;
            holder.SetParent(spin, false);
            holder.localPosition = at - Vector3.up * (KeepsakeArt.MobileBatHang * KeepsakeArt.MobileBatScale);
            Vector3 outward = new Vector3(at.x, 0f, at.z);
            // The bat's front is its -Z: look along -outward so the front faces out.
            holder.localRotation = outward.sqrMagnitude > 1e-6f
                ? Quaternion.LookRotation(-outward.normalized, Vector3.up)
                : Quaternion.identity;
            var bat = kit.Spawn(KeepsakeArt.PaperBat, holder, Vector3.zero, 0f, KeepsakeArt.MobileBatScale);
            if (bat == null)
            {
                Destroy(holder.gameObject);
                return null;
            }

            foreach (var (name, sign) in new[] { (KeepsakeArt.WingLeft, 1f), (KeepsakeArt.WingRight, -1f) })
            {
                var wing = ModelKit.FindDeep(bat.transform, name);
                if (wing != null)
                {
                    wing.localRotation *= Quaternion.AngleAxis(sign * HangingWingDegrees, Vector3.forward);
                }
            }

            foreach (var renderer in bat.GetComponentsInChildren<Renderer>())
            {
                renderer.shadowCastingMode = UnityEngine.Rendering.ShadowCastingMode.Off;
            }

            return holder.gameObject;
        }

        /// <summary>
        /// A painted porch post on the rail (cream, like the porch's trim) with a round orange finial, a short bracket
        /// arm with a brace and an orange knob, and a paper cord down to the mobile: a plant-hanger bracket. CI run 180:
        /// a dark pole with a bare arm and a black cord read as a gibbet from the title camera.
        /// </summary>
        private void BuildPole(MaterialLibrary materials)
        {
            float bottom = KeepsakePorch.DeckY;
            float height = KeepsakePorch.PoleTop - bottom;
            Bar("MobilePole", new Vector3(KeepsakePorch.PoleX, bottom + height * 0.5f, KeepsakePorch.PoleZ),
                new Vector3(0.08f, height, 0.08f), Quaternion.identity, materials, Palette.WarmPaper);
            Part(PrimitiveType.Sphere, "MobileFinial", new Vector3(KeepsakePorch.PoleX, KeepsakePorch.PoleTop + 0.05f,
                KeepsakePorch.PoleZ), Vector3.one * 0.13f, Quaternion.identity, materials, Palette.JackOrange);
            float armLength = Mathf.Abs(KeepsakePorch.PoleX - KeepsakePorch.MobileX) + 0.08f;
            float armX = (KeepsakePorch.PoleX + KeepsakePorch.MobileX) * 0.5f;
            Bar("MobileArm", new Vector3(armX, KeepsakePorch.PoleTop - 0.08f, KeepsakePorch.PoleZ),
                new Vector3(armLength, 0.05f, 0.05f), Quaternion.identity, materials, Palette.WarmPaper);
            Bar("MobileBrace", new Vector3(KeepsakePorch.PoleX - 0.11f, KeepsakePorch.PoleTop - 0.2f, KeepsakePorch.PoleZ),
                new Vector3(0.035f, 0.3f, 0.035f), Quaternion.Euler(0f, 0f, -45f), materials, Palette.WarmPaper);
            Part(PrimitiveType.Sphere, "MobileArmKnob", new Vector3(KeepsakePorch.MobileX - 0.05f,
                KeepsakePorch.PoleTop - 0.08f, KeepsakePorch.PoleZ), Vector3.one * 0.07f, Quaternion.identity, materials,
                Palette.JackOrange);
            float cordTop = KeepsakePorch.PoleTop - 0.1f;
            Bar("MobileCord", new Vector3(KeepsakePorch.MobileX, (cordTop + KeepsakePorch.MobileY) * 0.5f, KeepsakePorch.MobileZ),
                new Vector3(0.012f, cordTop - KeepsakePorch.MobileY, 0.012f), Quaternion.identity, materials, Palette.WarmPaper);
        }

        private void Bar(string name, Vector3 centre, Vector3 size, Quaternion rotation, MaterialLibrary materials,
            Color color) => Part(PrimitiveType.Cube, name, centre, size, rotation, materials, color);

        private void Part(PrimitiveType shape, string name, Vector3 centre, Vector3 size, Quaternion rotation,
            MaterialLibrary materials, Color color)
        {
            var bar = GameObject.CreatePrimitive(shape);
            bar.name = name;
            Destroy(bar.GetComponent<Collider>());
            bar.transform.SetParent(transform, false);
            bar.transform.SetPositionAndRotation(centre, rotation);
            bar.transform.localScale = size;
            if (materials != null)
            {
                materials.Apply(bar.GetComponent<Renderer>(), color);
            }
        }

        /// <summary>Shows the mobile for <paramref name="batCount"/> found bats.</summary>
        public void Show(int batCount)
        {
            int shown = 0;
            for (int n = 1; n <= _hooks.Count; n++)
            {
                bool on = BatMobileRules.HookShown(n, batCount);
                if (_hooks[n - 1] != null)
                {
                    _hooks[n - 1].gameObject.SetActive(on);
                }

                if (_bats[n - 1] != null)
                {
                    _bats[n - 1].SetActive(on);
                    shown += on ? 1 : 0;
                }
            }

            BatsShown = shown;
            for (int ring = 1; ring <= KeepsakeArt.MobileRings; ring++)
            {
                SetActive(_rings[ring - 1], BatMobileRules.RingShown(ring, batCount));
                SetActive(_closers[ring - 1], BatMobileRules.RingClosed(ring, batCount));
                SetActive(_lanterns[ring - 1], BatMobileRules.LanternLit(ring, batCount));
            }
        }

        private static void SetActive(GameObject go, bool on)
        {
            if (go != null)
            {
                go.SetActive(on);
            }
        }

        private void Update()
        {
            if (_model == null || AmbientMotion.ReducedMotion)
            {
                return;
            }

            _turn = (_turn + Time.deltaTime * TurnDegreesPerSecond) % 360f;
            float sway = Mathf.Sin(Time.time * 0.7f) * 1.5f;
            _model.localRotation = Quaternion.Euler(sway, _turn, 0f);
        }
    }
}
