using System.Collections.Generic;
using SpookyStreet.App;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.World;
using SpookyStreet.UI;
using SpookyStreet.World;
using UnityEngine;

namespace SpookyStreet.Case
{
    /// <summary>
    /// The diegetic clue attractions in the world (S002.5b, D26 §2.3, <see cref="ClueAttractions"/>): while Pip is
    /// near a clue that is not found yet, the thing itself moves a little or catches the light. The silver cloth
    /// flutters and glints on the bridge-head post, the sill thread lifts and glints, dust hops off the hall boards on
    /// each knock, the owl panel rattles and its eyes catch the lamp, the grille's slats of light flicker, the loose
    /// arm swings, the rooftop wind wheel whirs up as a gust starts a knock cycle, the wooden owl's eyes catch the
    /// porch lamp, and the decoy streamer twinkles like every clue. No arrow, no ring, nothing on the HUD.
    /// <para>Motions are small offsets on the real models, applied after their own animation each frame and removed
    /// the moment the attraction stops (<see cref="PoseOffset"/>), so the workshop and ambient motion keep their
    /// poses. Glints are tiny unlit catches of light under the bloom threshold, sized to stay readable at phone size.
    /// The knock attractions fire on the mechanism's own knocks, the frames <c>GameAudio</c> plays the cam tap; the
    /// wheel's whirr on the wind cycle that plays its sound. Game time (the pause freezes everything); reduced motion
    /// keeps the light and shrinks every movement.</para>
    /// </summary>
    public sealed class ClueAttractionView : MonoBehaviour
    {
        public const float GlintSize = 0.26f;
        public const float GlintMinViewFraction = 0.03f;
        public const float FlutterDegrees = 16f;
        public const float RattleDegrees = 2.2f;
        public const float WobbleDegrees = 10f;
        public const float WhirDegreesPerSecond = 540f;
        public const int DustMotes = 10;

        private static readonly Color Lavender = new Color(0.78f, 0.68f, 0.96f, 1f);

        private CaseDirector _director;
        private GameBootstrap _game;
        private readonly Dictionary<string, Slot> _slots = new Dictionary<string, Slot>();
        private readonly SpriteRenderer[] _dust = new SpriteRenderer[DustMotes];
        private SpriteRenderer _slats;
        private float _clock;
        private float _lastKnock = -100f;
        private float _lastGust = -100f;

        /// <summary>Attractions playing this frame (tests).</summary>
        public int PlayingCount { get; private set; }

        /// <summary>Dust motes drawn this frame (tests).</summary>
        public int VisibleDust { get; private set; }

        /// <summary>Seconds of game time since the view started (tests: the pause holds it).</summary>
        public float Clock => _clock;

        /// <summary>Knocks of the mechanism the view has seen (tests).</summary>
        public int KnocksSeen { get; private set; }

        private sealed class Slot
        {
            public ClueAttraction Attraction;
            public PoseOffset Pose;
            public Vector3 Axis;
            public SpriteRenderer Glint;
            public bool Playing;
            public int Starts;
            public float Strength;
            public float MaxStrength;
            public float WhirAngle;
        }

        public static ClueAttractionView Create(CaseDirector director, GameBootstrap game)
        {
            var go = new GameObject("ClueAttractions");
            go.transform.SetParent(director.transform, false);
            var view = go.AddComponent<ClueAttractionView>();
            view._director = director;
            view._game = game;
            view.Build(go.transform);
            return view;
        }

        private void Build(Transform root)
        {
            var house = _director.House;
            var workshop = _director.Workshop;
            var environment = _game.Environment;
            var star = UiSprites.TryLoad(UiSpriteCatalog.HintStar) ?? SpookyStreet.UI.Nonverbal.PuppetAtlas.Get("pg.star");
            foreach (var attraction in ClueAttractions.All)
            {
                var slot = new Slot { Attraction = attraction };
                Transform model = null;
                switch (attraction.Id)
                {
                    case ClueAttractions.Cloth:
                        model = house != null ? house.ClothSnag : null;
                        slot.Axis = Vector3.forward; // it hangs off the post toward the bridge: lifts about the street axis
                        break;
                    case ClueAttractions.Sill:
                        model = house != null ? house.SillSnag : null;
                        slot.Axis = Vector3.forward;
                        break;
                    case ClueAttractions.OwlPanel:
                        model = workshop != null ? workshop.Panel : null;
                        slot.Axis = Vector3.up;
                        break;
                    case ClueAttractions.LooseArm:
                        model = workshop != null ? workshop.ArmSlipped : null;
                        slot.Axis = Vector3.right;
                        break;
                    case ClueAttractions.WindWheel:
                        model = environment != null ? environment.WindWheel : null;
                        slot.Axis = EnvironmentDressing.WindWheelAxis;
                        break;
                    case ClueAttractions.Streamer:
                        model = _game.Witnesses != null ? _game.Witnesses.Bicycle : null;
                        break;
                    case ClueAttractions.WoodenOwl:
                        model = environment != null ? environment.OwlPuppet : null;
                        break;
                }

                bool moves = attraction.Has(AttractionKind.Flutter) || attraction.Has(AttractionKind.Rattle) ||
                             attraction.Has(AttractionKind.Wobble) || attraction.Has(AttractionKind.Whir);
                if (model != null && moves && attraction.Id != ClueAttractions.Grille)
                {
                    slot.Pose = new PoseOffset(model);
                }

                if (attraction.Has(AttractionKind.Glint) || attraction.Has(AttractionKind.Twinkle))
                {
                    slot.Glint = Sprite(root, "Glint_" + attraction.Id, star, 66);
                }

                _slots[attraction.Id] = slot;
            }

            var mote = SoftDot();
            for (int i = 0; i < _dust.Length; i++)
            {
                _dust[i] = Sprite(root, "DustMote" + i, mote, 65);
            }

            _slats = Sprite(root, "GrilleSlats", mote, 64);
            if (workshop != null)
            {
                workshop.Knocked += _ => OnKnock();
                workshop.CycleStarted += wind =>
                {
                    if (wind)
                    {
                        _lastGust = _clock;
                    }
                };
            }
        }

        private void OnKnock()
        {
            _lastKnock = _clock;
            KnocksSeen++;
        }

        /// <summary>Tests: a knock as the mechanism makes it (the dust, rattle and wobble ring out from now).</summary>
        public void KnockForTests() => OnKnock();

        /// <summary>Tests: a wind gust starting a knock cycle (the wheel whirs up from now).</summary>
        public void GustForTests() => _lastGust = _clock;

        /// <summary>True while <paramref name="id"/> is playing (Pip near, clue not found).</summary>
        public bool IsPlaying(string id) => _slots.TryGetValue(id, out var slot) && slot.Playing;

        /// <summary>Times <paramref name="id"/> started playing (tests).</summary>
        public int Starts(string id) => _slots.TryGetValue(id, out var slot) ? slot.Starts : 0;

        /// <summary>The strongest pulse (0..1) <paramref name="id"/> has shown since it last started (tests).</summary>
        public float PeakStrength(string id) => _slots.TryGetValue(id, out var slot) ? slot.MaxStrength : 0f;

        /// <summary>The glint of <paramref name="id"/> is drawn this frame (tests).</summary>
        public bool GlintVisible(string id) =>
            _slots.TryGetValue(id, out var slot) && slot.Glint != null && slot.Glint.gameObject.activeSelf;

        /// <summary>The model <paramref name="id"/> moves is off its own pose this frame (tests).</summary>
        public bool Offset(string id) => _slots.TryGetValue(id, out var slot) && slot.Pose != null && slot.Pose.Applied;

        /// <summary>The model <paramref name="id"/> moves (null when it has none or the model is missing).</summary>
        public Transform Model(string id) => _slots.TryGetValue(id, out var slot) && slot.Pose != null ? slot.Pose.Target : null;

        private void LateUpdate()
        {
            var flow = _game.Flow;
            bool playing = flow.State == GameState.Playing || flow.State == GameState.Paused;
            float dt = Time.deltaTime;
            _clock += dt;
            var pip = _game.Pip.transform.position;
            var state = _director.State;
            bool reduced = AmbientMotion.ReducedMotion;
            float motion = ClueAttractions.MotionScale(reduced);
            var cam = Camera.main;
            int count = 0;
            float sinceKnock = _clock - _lastKnock;
            float knock = ClueAttractions.KnockRing(sinceKnock);
            foreach (var slot in _slots.Values)
            {
                var a = slot.Attraction;
                bool on = playing && ClueAttractions.ShouldPlay(a, state, pip.x, pip.y, pip.z);
                if (on && !slot.Playing)
                {
                    slot.Starts++;
                    slot.MaxStrength = 0f;
                }

                slot.Playing = on;
                if (!on)
                {
                    slot.Pose?.Clear();
                    if (slot.Glint != null)
                    {
                        slot.Glint.gameObject.SetActive(false);
                    }

                    slot.Strength = 0f;
                    continue;
                }

                count++;
                float strength = 0f;
                Quaternion offset = Quaternion.identity;
                switch (a.Trigger)
                {
                    case AttractionTrigger.Idle:
                        float gust = ClueAttractions.Gust(_clock, a.Period);
                        if (a.Has(AttractionKind.Flutter))
                        {
                            float flap = Mathf.Sin(_clock * 9f) * 0.6f + Mathf.Sin(_clock * 15.5f) * 0.4f;
                            offset = Quaternion.AngleAxis(FlutterDegrees * motion * gust * (0.55f + 0.45f * flap), slot.Axis);
                        }

                        // The glint catches the light as the gust turns the cloth (two quick flashes per loop).
                        strength = Mathf.Max(gust * (a.Has(AttractionKind.Flutter) ? 0.3f : 0f), ClueAttractions.Glint(_clock, a.Period));
                        break;
                    case AttractionTrigger.Knock:
                        strength = knock;
                        if (a.Has(AttractionKind.Rattle))
                        {
                            offset = Quaternion.AngleAxis(RattleDegrees * motion * knock * Mathf.Sin(sinceKnock * 70f), slot.Axis);
                        }
                        else if (a.Has(AttractionKind.Wobble))
                        {
                            offset = Quaternion.AngleAxis(WobbleDegrees * motion * knock * Mathf.Sin(sinceKnock * 13f), slot.Axis);
                        }

                        break;
                    case AttractionTrigger.WindCycle:
                        float whir = ClueAttractions.Whir(_clock - _lastGust);
                        slot.WhirAngle = (slot.WhirAngle + whir * WhirDegreesPerSecond * motion * dt) % 360f;
                        offset = Quaternion.AngleAxis(slot.WhirAngle, slot.Axis);
                        strength = whir;
                        break;
                }

                slot.Strength = strength;
                slot.MaxStrength = Mathf.Max(slot.MaxStrength, strength);
                if (slot.Pose != null)
                {
                    slot.Pose.Apply(offset);
                }

                if (slot.Glint != null)
                {
                    TickGlint(slot, a, cam);
                }
            }

            PlayingCount = count;
            TickDust(state, pip, cam, knock, motion, playing);
        }

        private void TickGlint(Slot slot, ClueAttraction a, Camera cam)
        {
            float g = a.Trigger == AttractionTrigger.Knock
                ? ClueAttractions.KnockRing(_clock - _lastKnock)
                : ClueAttractions.Glint(_clock + a.Period * 0.37f, a.Period);
            var glint = slot.Glint;
            glint.gameObject.SetActive(g > 0.02f);
            if (g <= 0.02f)
            {
                return;
            }

            Vector3 at = slot.Pose != null && slot.Pose.Target != null ? Centre(slot.Pose.Target) : At(a);
            if (a.Id == ClueAttractions.Streamer && _game.Witnesses != null && _game.Witnesses.Bicycle != null)
            {
                at = At(a);
            }

            // A catch of light just in front of the thing, toward the camera, so the model never hides it.
            if (cam != null)
            {
                at += (cam.transform.position - at).normalized * 0.12f;
            }

            Quaternion face = cam != null ? cam.transform.rotation : Quaternion.identity;
            glint.transform.SetPositionAndRotation(at, face * Quaternion.Euler(0f, 0f, 20f + 25f * g));
            float size = Readable(GlintSize, at, cam, GlintMinViewFraction) * (0.45f + 0.55f * g);
            float native = glint.sprite != null ? glint.sprite.bounds.size.y : 1f;
            glint.transform.localScale = Vector3.one * (size / Mathf.Max(0.01f, native));
            Color c = (a.Has(AttractionKind.Twinkle) ? Lavender : Color.white) * GlowLevels.ClueGlint;
            c.a = g;
            glint.color = c;
        }

        private void TickDust(CaseState state, Vector3 pip, Camera cam, float knock, float motion, bool playing)
        {
            var hum = ClueAttractions.Get(ClueAttractions.FloorHum);
            var grille = ClueAttractions.Get(ClueAttractions.Grille);
            bool dust = playing && ClueAttractions.ShouldPlay(hum, state, pip.x, pip.y, pip.z);
            bool slats = playing && ClueAttractions.ShouldPlay(grille, state, pip.x, pip.y, pip.z);
            Quaternion face = cam != null ? cam.transform.rotation : Quaternion.identity;
            int visible = 0;
            var from = At(hum);
            var to = At(grille);
            for (int i = 0; i < _dust.Length; i++)
            {
                var mote = _dust[i];
                if (!dust)
                {
                    mote.gameObject.SetActive(false);
                    continue;
                }

                // Motes along the boards toward the grille: a faint idle shimmer, and a hop on every knock.
                float u = (i + 0.5f) / _dust.Length;
                float jitter = Mathf.Sin(i * 12.9898f) * 0.5f;
                Vector3 at = Vector3.Lerp(from, new Vector3(to.x - 0.35f, from.y, to.z), u) +
                             new Vector3(jitter * 0.5f, 0.03f, Mathf.Cos(i * 7.31f) * 0.4f);
                float hop = knock * (0.5f + 0.5f * Mathf.Abs(Mathf.Sin(i * 3.7f))) * motion;
                float idle = 0.5f + 0.5f * Mathf.Sin(_clock * 2.1f + i * 1.7f);
                at.y += 0.04f + 0.35f * hop + 0.05f * idle * motion;
                float alpha = Mathf.Clamp01(0.25f * idle + 0.85f * knock);
                mote.gameObject.SetActive(alpha > 0.03f);
                if (alpha <= 0.03f)
                {
                    continue;
                }

                mote.transform.SetPositionAndRotation(at, face);
                float size = Readable(0.07f, at, cam, 0.008f) * (0.8f + 0.4f * idle);
                float native = mote.sprite != null ? mote.sprite.bounds.size.y : 1f;
                mote.transform.localScale = Vector3.one * (size / Mathf.Max(0.01f, native));
                mote.color = new Color(0.96f, 0.9f, 0.78f, alpha * 0.85f);
                visible++;
            }

            VisibleDust = visible;

            // The grille's three slats of light, flickering in the knock's rhythm (a soft warm glow behind it).
            _slats.gameObject.SetActive(slats && knock > 0.02f);
            if (slats && knock > 0.02f)
            {
                Vector3 at = to + Vector3.left * 0.06f;
                _slats.transform.SetPositionAndRotation(at, face);
                float flicker = 0.7f + 0.3f * Mathf.Sin((_clock - _lastKnock) * 40f);
                float native = _slats.sprite != null ? _slats.sprite.bounds.size.y : 1f;
                _slats.transform.localScale = new Vector3(0.9f, 0.45f, 1f) * (Readable(0.5f, at, cam, 0.03f) / Mathf.Max(0.01f, native));
                Color c = Palette.PumpkinAmber * GlowLevels.ClueGlint;
                c.a = 0.55f * knock * flicker;
                _slats.color = c;
            }
        }

        private static Vector3 At(ClueAttraction a) => new Vector3(a.At.X, a.At.Y, a.At.Z);

        private static Vector3 Centre(Transform t)
        {
            var renderer = t.GetComponentInChildren<Renderer>();
            return renderer != null ? renderer.bounds.center : t.position;
        }

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

        private static SpriteRenderer Sprite(Transform parent, string name, Sprite sprite, int order)
        {
            var go = new GameObject(name);
            go.transform.SetParent(parent, false);
            var renderer = go.AddComponent<SpriteRenderer>();
            renderer.sprite = sprite;
            renderer.sortingOrder = order;
            go.SetActive(false);
            return renderer;
        }

        private static Sprite s_dot;

        /// <summary>A soft round mote (dust in the lamplight, the grille's glow).</summary>
        private static Sprite SoftDot()
        {
            if (s_dot != null)
            {
                return s_dot;
            }

            const int size = 32;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp, name = "SoftDot" };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = (x + 0.5f) / size * 2f - 1f;
                    float dy = (y + 0.5f) / size * 2f - 1f;
                    float a = Mathf.Clamp01(1f - Mathf.Sqrt(dx * dx + dy * dy));
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, a * a));
                }
            }

            texture.Apply();
            s_dot = UnityEngine.Sprite.Create(texture, new Rect(0f, 0f, size, size), new Vector2(0.5f, 0.5f), size);
            return s_dot;
        }
    }

    /// <summary>
    /// A small rotation laid over a model's own pose each frame and taken off again, without fighting whoever animates
    /// the model: if someone else wrote the rotation since the last frame (ambient spin, the workshop swinging the
    /// panel), that becomes the new base; otherwise the base is kept. Clearing restores the base only when nobody else
    /// has moved the model since.
    /// </summary>
    public sealed class PoseOffset
    {
        private Quaternion _base;
        private Quaternion _written;

        public PoseOffset(Transform target)
        {
            Target = target;
        }

        public Transform Target { get; }

        public bool Applied { get; private set; }

        public void Apply(Quaternion worldOffset)
        {
            if (Target == null)
            {
                return;
            }

            var now = Target.rotation;
            if (!Applied || !Same(now, _written))
            {
                _base = now;
            }

            Target.rotation = worldOffset * _base;
            _written = Target.rotation;
            Applied = true;
        }

        public void Clear()
        {
            if (!Applied || Target == null)
            {
                Applied = false;
                return;
            }

            if (Same(Target.rotation, _written))
            {
                Target.rotation = _base;
            }

            Applied = false;
        }

        private static bool Same(Quaternion a, Quaternion b) =>
            Mathf.Abs(a.x - b.x) < 1e-6f && Mathf.Abs(a.y - b.y) < 1e-6f && Mathf.Abs(a.z - b.z) < 1e-6f &&
            Mathf.Abs(a.w - b.w) < 1e-6f;
    }
}
