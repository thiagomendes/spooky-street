using SpookyStreet.Core.Art;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.World;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.UI;
using SpookyStreet.UI.Nonverbal;
using UnityEngine;

namespace SpookyStreet.Case
{
    /// <summary>
    /// The tier-2 hint in the world (docs/nonverbal-ux.md §6, D23.7): on a radio press at the "where" tier a paper-bat
    /// trail launches from Pip, one bat every 0.22 s, flies an arc to the objective and fades there, while a floating
    /// goal sticker (the place's puppet part in the orange key ring) pulses over the goal. Timing comes from the
    /// engine-free <see cref="BatTrailSchedule"/> (two waves 4 s apart, about 8 s, then silence until the next press).
    /// At most 12 pooled billboard sprites sharing the atlas; nothing collides or blocks the camera. Game time, so
    /// pause freezes it.
    /// </summary>
    public sealed class HintGuide : MonoBehaviour
    {
        public const float BatSize = 0.75f;
        public const float StickerSize = 1.5f;
        public const float ArcHeight = 1.6f;

        /// <summary>Far away a bat and the sticker grow to keep this share of the view height (read at phone size).</summary>
        public const float BatMinViewFraction = 0.035f;
        public const float StickerMinViewFraction = 0.075f;

        private readonly BatState[] _bats = new BatState[BatTrailSchedule.MaxConcurrentBats];
        private readonly SpriteRenderer[] _pool = new SpriteRenderer[BatTrailSchedule.MaxConcurrentBats];
        private Transform _pip;
        private SpriteRenderer _sticker;
        private SpriteRenderer _stickerRing;
        private SpriteRenderer _glyph;
        private SpriteRenderer _ring;
        private Vector3 _from;
        private Vector3 _goal;

        public BatTrailSchedule Schedule { get; } = new BatTrailSchedule();

        /// <summary>The objective the trail leads to (null when idle).</summary>
        public CaseObjective? Objective { get; private set; }

        public bool Active => Objective.HasValue && Schedule.IsActive(Time.time);

        /// <summary>Bats drawn this frame (tests).</summary>
        public int VisibleBats { get; private set; }

        public bool BeaconVisible => _sticker != null && _sticker.gameObject.activeSelf;

        public Vector3 Goal => _goal;

        /// <summary>Whether the owl house's front door is open (hints then lead through it; June's window before).</summary>
        public System.Func<bool> DoorOpen { get; set; }

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

            guide._stickerRing = Billboard(go.transform, "GoalSticker", UiSprites.TryLoad(UiSpriteCatalog.FeltDiscBone), 61);
            guide._sticker = guide._stickerRing;
            guide._glyph = Billboard(guide._stickerRing.transform, "Glyph", null, 62);
            guide._glyph.gameObject.SetActive(true);
            guide._ring = Billboard(guide._stickerRing.transform, "Ring", PuppetAtlas.Get(PuppetParts.KeyRing), 63);
            guide._ring.gameObject.SetActive(true);
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

        /// <summary>
        /// The world goal and the sticker's part for an objective, seen from where Pip is (D24.1): a goal on the other
        /// side of the owl house walls leads to the threshold on Pip's side instead (the open front door, or June's
        /// window while the door is latched). False when there is no goal (free roam).
        /// </summary>
        public static bool TryGoal(CaseObjective objective, Vector3 pip, bool doorOpen, out Vector3 goal, out string part)
        {
            if (!TryGoal(objective, out goal, out part))
            {
                return false;
            }

            bool pipInside = CaseRouteLayout.Inside(pip.x, pip.z);
            bool goalInside = CaseRouteLayout.Inside(goal.x, goal.z);
            var via = Thresholds.WayTo(pipInside, goalInside, doorOpen);
            if (via != null)
            {
                goal = new Vector3(via.CentreX, via.FloorY + 1.2f, via.CentreZ);
                part = via.Id == ThresholdId.FrontDoor ? "pg.door_open" : "pg.june_window";
            }

            return true;
        }

        /// <summary>The world goal and the sticker's part for an objective, wherever Pip is; false in free roam.</summary>
        public static bool TryGoal(CaseObjective objective, out Vector3 goal, out string part)
        {
            string anchor = null;
            string point = null;
            switch (objective)
            {
                case CaseObjective.Arrival:
                    anchor = CaseAnchors.BluePorch;
                    part = "pg.house";
                    break;
                case CaseObjective.CheckView:
                    point = CaseStage.PointBench;
                    part = "pg.bench";
                    break;
                case CaseObjective.FindWayIn:
                    anchor = CaseAnchors.Treehouse;
                    part = "pg.treehouse";
                    break;
                case CaseObjective.MeetJune:
                    anchor = CaseAnchors.Attic;
                    part = "pg.june";
                    break;
                case CaseObjective.OpenDoor:
                    point = CaseStage.PointLatch;
                    part = "pg.latch";
                    break;
                case CaseObjective.FindSource:
                    point = CaseStage.PointGrille;
                    part = "pg.grille";
                    break;
                case CaseObjective.LinkArm:
                    point = CaseStage.PointLinkArm;
                    part = "pg.arm";
                    break;
                case CaseObjective.TurnCrank:
                    point = CaseStage.PointCrank;
                    part = "pg.crank";
                    break;
                case CaseObjective.ReturnToPorch:
                    anchor = CaseAnchors.ArrivalPorch;
                    part = "pg.porch";
                    break;
                default:
                    goal = Vector3.zero;
                    part = null;
                    return false;
            }

            StagePoint p;
            if (anchor != null)
            {
                if (!CaseStage.TryGetAnchor(anchor, out p))
                {
                    goal = Vector3.zero;
                    return false;
                }
            }
            else
            {
                var found = CaseStage.Find(point);
                if (found == null)
                {
                    goal = Vector3.zero;
                    return false;
                }

                p = found.Position;
            }

            goal = new Vector3(p.X, p.Y + 1.2f, p.Z);
            return true;
        }

        /// <summary>A tier-2 radio press: (re)starts the trail toward <paramref name="objective"/>'s goal.</summary>
        public bool StartTrail(CaseObjective objective)
        {
            Vector3 pip = _pip != null ? _pip.position : Vector3.zero;
            if (!TryGoal(objective, pip, DoorOpen != null && DoorOpen(), out var goal, out string part))
            {
                Stop();
                return false;
            }

            Objective = objective;
            _goal = goal;
            _from = _pip != null ? _pip.position + Vector3.up * 0.9f : goal;
            _glyph.sprite = PuppetAtlas.Get(part);
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

        private void LateUpdate() => Tick();

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
            for (int i = 0; i < _pool.Length; i++)
            {
                var renderer = _pool[i];
                if (i >= count)
                {
                    renderer.gameObject.SetActive(false);
                    continue;
                }

                var bat = _bats[i];
                float u = bat.RouteProgress;
                // A gentle arc from Pip to the goal; each bat weaves a little off the line so the trail reads as a flock.
                Vector3 at = Vector3.Lerp(_from, _goal, u) + Vector3.up * (ArcHeight * 4f * u * (1f - u));
                float weave = Mathf.Sin((u * 3f + bat.Index * 0.37f) * Mathf.PI) * 0.18f;
                at += (cam != null ? cam.transform.right : Vector3.right) * weave;
                renderer.gameObject.SetActive(true);
                float flap = 0.75f + 0.25f * Mathf.Abs(Mathf.Sin(now * 14f + bat.Index));
                renderer.transform.SetPositionAndRotation(at, face);
                SetSize(renderer, Readable(BatSize, at, cam, BatMinViewFraction), flap);
                renderer.color = new Color(1f, 1f, 1f, bat.Alpha);
            }

            VisibleBats = count;
            bool beacon = Objective.HasValue && Schedule.BeaconVisible(now);
            _sticker.gameObject.SetActive(beacon);
            if (beacon)
            {
                float pulse = 1f + 0.08f * Mathf.Sin(now * 2f * Mathf.PI * 1.2f);
                _sticker.transform.SetPositionAndRotation(_goal + Vector3.up * (1.1f + 0.08f * Mathf.Sin(now * 2.2f)), face);
                SetSize(_sticker, Readable(StickerSize, _sticker.transform.position, cam, StickerMinViewFraction) * pulse, 1f);
                FitChild(_glyph, 0.8f);
                FitChild(_ring, 1.25f);
            }
        }

        /// <summary>Sizes a sticker layer relative to the sticker disc (k = its height in disc heights).</summary>
        private void FitChild(SpriteRenderer child, float k)
        {
            if (child.sprite == null)
            {
                return;
            }

            float parent = _sticker.sprite != null ? _sticker.sprite.bounds.size.y : 1f;
            var t = child.transform;
            t.localScale = Vector3.one * (k * parent / Mathf.Max(0.01f, child.sprite.bounds.size.y));
            t.localPosition = new Vector3(0f, 0f, -0.01f);
            t.localRotation = Quaternion.identity;
        }

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
    }
}
