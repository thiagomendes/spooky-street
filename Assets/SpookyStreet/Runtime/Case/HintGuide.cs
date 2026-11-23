using SpookyStreet.App;
using SpookyStreet.Characters;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Core.World;
using SpookyStreet.Player;
using SpookyStreet.UI.Nonverbal;
using UnityEngine;

namespace SpookyStreet.Case
{
    /// <summary>
    /// The tier-1 and tier-2 hints in the world, retiered by S002.5b (D26 §6, docs/mystery-first.md §6, mock
    /// <c>mf_hint_tiers</c>): both point to an authored <see cref="HintArea"/>, never to an object.
    /// <para>Tier 1, the nudge: a breeze carries a handful of paper-thin leaves from Pip toward the area, where they
    /// swirl and settle, while a soft warm glow (an unlit pool on the floor plus a dim warm light, under the bloom
    /// threshold) rises over the whole area and fades; the nearest witness in view glances that way for about 2 s, and
    /// Pip, if she stands, glances toward the side the area lies on (<see cref="HintPresentation.PipGlance"/>, S003a).
    /// <see cref="HintPresentation.BreezeSeconds"/> per press, or until the objective changes.</para>
    /// <para>Tier 2, the trail: paper bats launch from Pip, fly to the area's ring and circle the area once before they
    /// fade (<see cref="HintPresentation.TrailPose"/>); there is no goal sticker. Two waves 4 s apart, then silence
    /// until the next press (<see cref="BatTrailSchedule"/>), at most 12 pooled billboards.</para>
    /// An area behind the owl house walls gives way to the threshold area on Pip's side (D24.1). Nothing collides or
    /// blocks the camera. Game time, so the pause freezes it; reduced motion keeps the glow and calms the leaves.
    /// </summary>
    public sealed class HintGuide : MonoBehaviour
    {
        public const float BatSize = 0.75f;
        public const float ArcHeight = 1.6f;

        /// <summary>Far away a bat keeps this share of the view height (read at phone size).</summary>
        public const float BatMinViewFraction = 0.035f;

        public const float LeafSize = 0.3f;
        public const float LeafMinViewFraction = 0.03f;

        /// <summary>The trail's own schedule: six bats a wave, a longer flight so each can circle the area once.</summary>
        public static BatTrailSchedule TrailSchedule() => new BatTrailSchedule(4f, 8f, 6, 0.3f, 3.4f, 0.4f, 0.1f);

        private readonly BatState[] _bats = new BatState[BatTrailSchedule.MaxConcurrentBats];
        private readonly SpriteRenderer[] _pool = new SpriteRenderer[BatTrailSchedule.MaxConcurrentBats];
        private readonly Vector3[] _batAt = new Vector3[BatTrailSchedule.MaxConcurrentBats];
        private readonly bool[] _batArrived = new bool[BatTrailSchedule.MaxConcurrentBats];
        private readonly SpriteRenderer[] _leaves = new SpriteRenderer[HintPresentation.BreezeLeaves];
        private SpriteRenderer _glow;
        private Light _glowLight;
        private Transform _pip;
        private Vector3 _from;
        private float _fromAngle;
        private HintArea _trailArea;

        public BatTrailSchedule Schedule { get; } = TrailSchedule();

        /// <summary>The objective the trail leads toward (null when idle).</summary>
        public CaseObjective? Objective { get; private set; }

        public bool Active => Objective.HasValue && Schedule.IsActive(Time.time);

        /// <summary>The area the trail circles (null when idle).</summary>
        public HintArea TrailArea => Objective.HasValue ? _trailArea : null;

        /// <summary>Bats drawn this frame (tests).</summary>
        public int VisibleBats { get; private set; }

        /// <summary>Bats circling the area this frame (tests).</summary>
        public int CirclingBats { get; private set; }

        /// <summary>A drawn bat's world position this frame, and whether it is circling (tests).</summary>
        public Vector3 BatPosition(int i) => _batAt[i];

        public bool BatCircling(int i) => _batArrived[i];

        /// <summary>The trail's area centre (floor point), for tests and captures.</summary>
        public Vector3 Goal => _trailArea != null ? ToVector(_trailArea.Centre) : Vector3.zero;

        /// <summary>Whether the owl house's front door is open (hints then lead through it; June's window before).</summary>
        public System.Func<bool> DoorOpen { get; set; }

        /// <summary>Reduced motion (the leaves drift straight and slow; the glow is unchanged).</summary>
        public System.Func<bool> ReducedMotion { get; set; }

        // ------------------------------------------------------------------------------- tier 1: breeze and glow

        private float _breezeStarted = -1f;
        private HintArea _breezeArea;
        private Vector3 _breezeFrom;

        /// <summary>The characters who may glance toward a tier-1 area (witnesses and cast; set by the layer).</summary>
        public System.Func<System.Collections.Generic.IReadOnlyList<Witness>> Lookers { get; set; }

        /// <summary>The objective the breeze points toward (null when idle).</summary>
        public CaseObjective? BreezeObjective { get; private set; }

        public bool BreezeActive => BreezeObjective.HasValue;

        /// <summary>The area the breeze and the glow show (null when idle).</summary>
        public HintArea BreezeArea => BreezeObjective.HasValue ? _breezeArea : null;

        /// <summary>Where the warm glow lies (the area's floor point).</summary>
        public Vector3 GlowCentre => _glow != null ? _glow.transform.position : Vector3.zero;

        /// <summary>The glow is drawn this frame (tests).</summary>
        public bool GlowVisible => _glow != null && _glow.gameObject.activeSelf && _glow.color.a > 0.01f;

        /// <summary>The glow's world radius this frame.</summary>
        public float GlowRadius { get; private set; }

        /// <summary>Leaves drawn this frame (tests).</summary>
        public int VisibleLeaves { get; private set; }

        /// <summary>Leaves settled in the swirl at the area this frame (tests).</summary>
        public int SwirlingLeaves { get; private set; }

        /// <summary>The character who glanced toward the last tier-1 area (null when nobody was in view).</summary>
        public Witness Looker { get; private set; }

        /// <summary>Pip's glance clip for the last tier-1 nudge (null when she did not glance: moving, in the air, reduced
        /// motion, the area ahead of her, or no glance clips).</summary>
        public string PipGlance { get; private set; }

        /// <summary>A tier-1 radio press (or the Helper): the breeze and the glow over the objective's area. False in free roam.</summary>
        public bool StartBreeze(CaseObjective objective)
        {
            Vector3 pip = _pip != null ? _pip.position : Vector3.zero;
            if (!HintPresentation.TryArea(objective, pip.x, pip.z, DoorOpen != null && DoorOpen(), out var area))
            {
                StopBreeze();
                return false;
            }

            BreezeObjective = objective;
            _breezeArea = area;
            _breezeStarted = Time.time;
            _breezeFrom = pip + Vector3.up * 0.9f;
            Looker = ChooseLooker(pip);
            if (Looker != null)
            {
                Looker.LookAt(ToVector(area.Centre) + Vector3.up * HintPresentation.AreaLift, HintPresentation.LookSeconds);
            }

            PipGlance = GlanceToward(area);
            TickBreeze();
            return true;
        }

        private PipAnimator _pipAnimator;

        /// <summary>Pip turns her head toward the side the area lies on, if the rule lets her (standing, area to a side).</summary>
        private string GlanceToward(HintArea area)
        {
            if (_pip == null)
            {
                return null;
            }

            if (_pipAnimator == null)
            {
                _pipAnimator = _pip.GetComponentInChildren<PipAnimator>();
            }

            var motor = _pip.GetComponent<PipMotor>();
            bool grounded = motor == null || motor.Grounded;
            float speed = motor != null ? motor.HorizontalSpeed : 0f;
            Vector3 target = ToVector(HintPresentation.GlanceTarget(area));
            Vector3 forward = _pip.forward;
            Vector3 to = target - _pip.position;
            bool reduced = ReducedMotion != null && ReducedMotion();
            string clip = HintPresentation.PipGlance(true, grounded, speed, reduced, forward.x, forward.z, to.x, to.z);
            return clip != null && _pipAnimator != null && _pipAnimator.PlayGlance(target) ? _pipAnimator.OneShot : null;
        }

        public void StopBreeze()
        {
            BreezeObjective = null;
            _breezeStarted = -1f;
            TickBreeze();
        }

        private readonly System.Collections.Generic.List<LookCandidate> _candidates =
            new System.Collections.Generic.List<LookCandidate>();

        private Witness ChooseLooker(Vector3 pip)
        {
            var lookers = Lookers?.Invoke();
            if (lookers == null)
            {
                return null;
            }

            var cam = Camera.main;
            _candidates.Clear();
            foreach (var w in lookers)
            {
                bool available = w != null && w.isActiveAndEnabled && !w.Conversing && !w.Gesturing;
                Vector3 at = w != null ? w.transform.position : Vector3.zero;
                bool inView = false;
                if (available && cam != null)
                {
                    Vector3 v = cam.WorldToViewportPoint(at + Vector3.up * 1.2f);
                    inView = v.z > 0f && v.x > 0.02f && v.x < 0.98f && v.y > 0.02f && v.y < 0.98f;
                }

                _candidates.Add(new LookCandidate(at.x, at.y, at.z, inView, available));
            }

            int index = HintPresentation.ChooseLooker(_candidates, pip.x, pip.z);
            return index >= 0 ? lookers[index] : null;
        }

        /// <summary>Leaf timing inside the breeze: launch stagger, flight to the area, then the swirl and the fade.</summary>
        private const float LeafStagger = 0.18f;
        private const float LeafFlight = 2.4f;

        private void TickBreeze()
        {
            float now = Time.time;
            if (BreezeObjective.HasValue && now - _breezeStarted > HintPresentation.BreezeSeconds)
            {
                BreezeObjective = null;
            }

            bool on = BreezeObjective.HasValue && _breezeArea != null;
            float age = on ? now - _breezeStarted : 0f;
            var cam = Camera.main;
            bool reduced = ReducedMotion != null && ReducedMotion();

            // The warm glow over the whole area: rises over 0.8 s, breathes slowly, fades over the last 1.2 s.
            if (_glow != null)
            {
                _glow.gameObject.SetActive(on);
                if (_glowLight != null)
                {
                    _glowLight.enabled = on;
                }

                if (on)
                {
                    Vector3 centre = ToVector(_breezeArea.Centre) + Vector3.up * 0.04f;
                    float envelope = Mathf.Clamp01(age / 0.8f) * Mathf.Clamp01((HintPresentation.BreezeSeconds - age) / 1.2f);
                    float breathe = 0.85f + 0.15f * Mathf.Sin(age * 2f * Mathf.PI * 0.35f);
                    GlowRadius = _breezeArea.Radius;
                    _glow.transform.SetPositionAndRotation(centre, Quaternion.Euler(90f, 0f, 0f));
                    float native = _glow.sprite != null ? _glow.sprite.bounds.size.x : 1f;
                    float k = 2f * GlowRadius / Mathf.Max(0.01f, native);
                    _glow.transform.localScale = new Vector3(k, k, 1f);
                    Color c = Palette.PumpkinAmber * GlowLevels.HintAreaGlow;
                    c.a = 0.5f * envelope * breathe;
                    _glow.color = c;
                    if (_glowLight != null)
                    {
                        _glowLight.transform.position = centre + Vector3.up * 2.2f;
                        _glowLight.range = GlowRadius * 2.2f;
                        _glowLight.intensity = 1.6f * envelope * breathe;
                    }
                }
            }

            // The leaves: launched one by one from Pip, carried on a wavy arc to the area, then a small swirl at its
            // centre (never on a thing at its edge), tumbling as they go.
            int visible = 0, swirling = 0;
            Quaternion face = cam != null ? cam.transform.rotation : Quaternion.identity;
            Vector3 right = cam != null ? cam.transform.right : Vector3.right;
            for (int i = 0; i < _leaves.Length; i++)
            {
                var leaf = _leaves[i];
                if (leaf == null)
                {
                    continue;
                }

                float t = age - 0.1f - i * LeafStagger;
                float life = HintPresentation.BreezeSeconds - 0.1f - i * LeafStagger;
                if (!on || t < 0f || t >= life)
                {
                    leaf.gameObject.SetActive(false);
                    continue;
                }

                Vector3 centre = ToVector(_breezeArea.Centre);
                float phase = i * 0.785f;
                Vector3 swirl = centre + Vector3.up * HintPresentation.SwirlLift + new Vector3(
                    Mathf.Cos(phase + t * 1.8f) * HintPresentation.SwirlRadius, 0.15f * Mathf.Sin(t * 2.3f + phase),
                    Mathf.Sin(phase + t * 1.8f) * HintPresentation.SwirlRadius);
                Vector3 at;
                float u = Mathf.Clamp01(t / LeafFlight);
                if (u < 1f)
                {
                    float e = u * u * (3f - 2f * u);
                    at = Vector3.Lerp(_breezeFrom, swirl, e) + Vector3.up * (1.1f * 4f * e * (1f - e));
                    float wave = reduced ? 0f : Mathf.Sin((u * 2.5f + i * 0.31f) * Mathf.PI) * 0.35f * (1f - e);
                    at += right * wave;
                }
                else
                {
                    at = swirl;
                    swirling++;
                }

                float fade = Mathf.Clamp01(t / 0.25f) * Mathf.Clamp01((life - t) / 0.6f);
                float spin = reduced ? 0f : (t * (140f + 25f * i)) * (i % 2 == 0 ? 1f : -1f);
                float flutter = reduced ? 1f : 0.55f + 0.45f * Mathf.Abs(Mathf.Cos(t * 5.5f + phase));
                leaf.gameObject.SetActive(true);
                leaf.transform.SetPositionAndRotation(at, face * Quaternion.Euler(0f, 0f, spin + i * 47f));
                float size = Readable(LeafSize, at, cam, LeafMinViewFraction);
                float native = leaf.sprite != null ? leaf.sprite.bounds.size.y : 1f;
                float k = size / Mathf.Max(0.01f, native);
                leaf.transform.localScale = new Vector3(k * flutter, k, k);
                Color tint = i % 3 == 0 ? Palette.JackOrange : i % 3 == 1 ? Palette.PumpkinAmber : Palette.CandyCornYellow;
                tint *= 0.85f;
                tint.a = fade;
                leaf.color = tint;
                visible++;
            }

            VisibleLeaves = visible;
            SwirlingLeaves = swirling;
        }

        public static HintGuide Create(Transform parent, Transform pip)
        {
            var go = new GameObject("HintGuide");
            go.transform.SetParent(parent, false);
            var guide = go.AddComponent<HintGuide>();
            guide._pip = pip;
            var bat = PuppetAtlas.Get(PuppetAtlasLayout.PaperBat) ?? PuppetAtlas.Get("pg.bat");
            for (int i = 0; i < guide._pool.Length; i++)
            {
                guide._pool[i] = Billboard(go.transform, "PaperBat" + i, bat, 60);
            }

            var leaf = LeafSprite();
            for (int i = 0; i < guide._leaves.Length; i++)
            {
                guide._leaves[i] = Billboard(go.transform, "BreezeLeaf" + i, leaf, 64);
            }

            guide._glow = Billboard(go.transform, "AreaGlow", GlowSprite(), 20);
            var lightObject = new GameObject("AreaGlowLight");
            lightObject.transform.SetParent(go.transform, false);
            guide._glowLight = lightObject.AddComponent<Light>();
            guide._glowLight.type = LightType.Point;
            guide._glowLight.color = Palette.PumpkinAmber;
            guide._glowLight.shadows = LightShadows.None;
            guide._glowLight.enabled = false;
            return guide;
        }

        private static SpriteRenderer Billboard(Transform parent, string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            go.SetActive(false);
            return renderer;
        }

        /// <summary>A tier-2 radio press: (re)starts the trail toward <paramref name="objective"/>'s area.</summary>
        public bool StartTrail(CaseObjective objective)
        {
            Vector3 pip = _pip != null ? _pip.position : Vector3.zero;
            if (!HintPresentation.TryArea(objective, pip.x, pip.z, DoorOpen != null && DoorOpen(), out var area))
            {
                Stop();
                return false;
            }

            Objective = objective;
            _trailArea = area;
            _from = pip + Vector3.up * 0.9f;
            // The bats meet the ring on Pip's side of the area, then circle it.
            _fromAngle = Mathf.Atan2(pip.z - area.Centre.Z, pip.x - area.Centre.X);
            Schedule.Press(Time.time);
            Tick();
            return true;
        }

        public void Stop()
        {
            Schedule.Stop();
            Objective = null;
            Tick();
        }

        private void LateUpdate()
        {
            Tick();
            TickBreeze();
        }

        private void Tick()
        {
            float now = Time.time;
            int count = Objective.HasValue ? Schedule.Sample(now, _bats) : 0;
            if (Objective.HasValue && !Schedule.IsActive(now))
            {
                Objective = null;
            }

            var cam = Camera.main;
            Quaternion face = cam != null ? cam.transform.rotation : Quaternion.identity;
            int circling = 0;
            for (int i = 0; i < _pool.Length; i++)
            {
                var renderer = _pool[i];
                if (i >= count || _trailArea == null)
                {
                    renderer.gameObject.SetActive(false);
                    _batArrived[i] = false;
                    continue;
                }

                var bat = _bats[i];
                float ring = _trailArea.Radius * HintPresentation.TrailRingFactor;
                // Each bat meets the ring a little further round, so the flock spreads out over the circle.
                float start = _fromAngle + (bat.Index - 2.5f) * 0.32f;
                HintPresentation.TrailPose(bat.RouteProgress, start, ring, out float ox, out float oz, out float approach);
                Vector3 onRing = ToVector(_trailArea.Centre) + new Vector3(ox, HintPresentation.TrailLift, oz);
                Vector3 at;
                if (approach < 1f)
                {
                    at = Vector3.Lerp(_from, onRing, approach) + Vector3.up * (ArcHeight * 4f * approach * (1f - approach));
                    float weave = Mathf.Sin((approach * 3f + bat.Index * 0.37f) * Mathf.PI) * 0.18f;
                    at += (cam != null ? cam.transform.right : Vector3.right) * weave;
                    _batArrived[i] = false;
                }
                else
                {
                    at = onRing + Vector3.up * (0.12f * Mathf.Sin(now * 3f + bat.Index));
                    _batArrived[i] = true;
                    circling++;
                }

                _batAt[i] = at;
                renderer.gameObject.SetActive(true);
                float flap = 0.75f + 0.25f * Mathf.Abs(Mathf.Sin(now * 14f + bat.Index));
                renderer.transform.SetPositionAndRotation(at, face);
                SetSize(renderer, Readable(BatSize, at, cam, BatMinViewFraction), flap);
                renderer.color = new Color(1f, 1f, 1f, bat.Alpha);
            }

            VisibleBats = _trailArea != null ? count : 0;
            CirclingBats = circling;
        }

        private static Vector3 ToVector(StagePoint p) => new Vector3(p.X, p.Y, p.Z);

        /// <summary>World height that keeps at least <paramref name="fraction"/> of the view height at this distance.</summary>
        private static float Readable(float height, Vector3 at, Camera cam, float fraction)
        {
            if (cam == null)
            {
                return height;
            }

            float distance = Vector3.Distance(cam.transform.position, at);
            float view = 2f * distance * Mathf.Tan(cam.fieldOfView * 0.5f * Mathf.Deg2Rad);
            return Mathf.Max(height, view * fraction);
        }

        private static void SetSize(SpriteRenderer renderer, float height, float squashY)
        {
            float native = renderer.sprite != null ? renderer.sprite.bounds.size.y : 1f;
            float k = height / Mathf.Max(0.01f, native);
            renderer.transform.localScale = new Vector3(k, k * squashY, k);
        }

        private static Sprite s_leaf;
        private static Sprite s_glow;

        /// <summary>A small paper leaf drawn in code (an almond with a pale midrib), tinted per leaf.</summary>
        private static Sprite LeafSprite()
        {
            if (s_leaf != null)
            {
                return s_leaf;
            }

            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "BreezeLeaf" };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float v = (y + 0.5f) / size; // 0 stem .. 1 tip
                    float h = (x + 0.5f) / size * 2f - 1f;
                    float half = 0.62f * Mathf.Pow(Mathf.Sin(Mathf.Clamp01((v - 0.08f) / 0.9f) * Mathf.PI), 0.85f);
                    float edge = half - Mathf.Abs(h);
                    float alpha = Mathf.Clamp01(edge * size * 0.25f);
                    // The stem below the blade.
                    if (v < 0.12f && Mathf.Abs(h) < 0.05f)
                    {
                        alpha = 1f;
                    }

                    float rib = Mathf.Abs(h) < 0.04f ? 0.72f : 1f;
                    float shade = 0.82f + 0.18f * v;
                    texture.SetPixel(x, y, new Color(shade * rib, shade * rib, shade * rib, alpha));
                }
            }

            texture.Apply();
            s_leaf = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            return s_leaf;
        }

        /// <summary>A soft round pool of light (opaque centre fading to clear), laid flat on the area's floor.</summary>
        private static Sprite GlowSprite()
        {
            if (s_glow != null)
            {
                return s_glow;
            }

            const int size = 128;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "AreaGlow" };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float r = Mathf.Sqrt(dx * dx + dy * dy);
                    float a = Mathf.Clamp01(1f - r);
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a * (3f - 2f * a)));
                }
            }

            texture.Apply();
            s_glow = Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            return s_glow;
        }
    }
}
