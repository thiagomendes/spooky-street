using System.Collections.Generic;
using System.Diagnostics;
using SpookyStreet.Core.LivingStreet;
using SpookyStreet.World;
using UnityEngine;

namespace SpookyStreet.LivingStreet
{
    /// <summary>
    /// The crowd in the world (living-street §8): runs the Core <see cref="CrowdClock"/> clock, puts each walker's body on its
    /// pose, and applies the tier's budget every frame (off-screen walkers are hidden and not animated; beyond 12 m they
    /// animate at 15 Hz; beyond 18 m or past the animated cap they are standees). Its own CPU time per frame is measured
    /// with a stopwatch for the device readout. Bodies come from <see cref="ICrowdBodyFactory"/> (the kit when its prefabs
    /// are committed, placeholders otherwise).
    /// </summary>
    public sealed class CrowdView : MonoBehaviour
    {
        private readonly List<CrowdBody> _bodies = new List<CrowdBody>();
        private readonly List<float> _distances = new List<float>();
        private readonly List<bool> _inView = new List<bool>();
        private readonly Plane[] _planes = new Plane[6];
        private WalkerDetail[] _detail;
        private int[] _order;
        private float[] _clock;
        private double _time;
        private readonly Stopwatch _watch = new Stopwatch();

        public CrowdClock Clock { get; private set; }

        public CrowdBudget Budget { get; set; } = CrowdBudget.For(CrowdTier.High);

        public IReadOnlyList<CrowdBody> Bodies => _bodies;

        /// <summary>Milliseconds the crowd took last frame (logic, culling and posing).</summary>
        public float LastMs { get; private set; }

        /// <summary>Walkers drawn last frame.</summary>
        public int Drawn { get; private set; }

        /// <summary>The camera the budget culls against (null: everything counts as in view).</summary>
        public Camera View { get; set; }

        /// <summary>Hidden walkers inside the heart while a cutscene plays (they hold out of shot, §3.4 rule 7).</summary>
        public bool HideHeart { get; set; }

        public static CrowdView Create(Transform parent, MaterialLibrary materials, params ICrowdBodyFactory[] factories)
        {
            var go = new GameObject("Crowd");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<CrowdView>();
            view.Build(materials, factories);
            return view;
        }

        private void Build(MaterialLibrary materials, ICrowdBodyFactory[] factories)
        {
            Clock = new CrowdClock();
            var fallback = new PlaceholderCrowdBodies();
            foreach (var route in CrowdRoutes.All)
            {
                foreach (var member in route.Members)
                {
                    CrowdBody body = null;
                    foreach (var factory in factories)
                    {
                        body = factory?.Create(member.Look, transform, materials);
                        if (body != null)
                        {
                            break;
                        }
                    }

                    body ??= fallback.Create(member.Look, transform, materials);
                    _bodies.Add(body);
                    _distances.Add(0f);
                    _inView.Add(true);
                }
            }

            _detail = new WalkerDetail[_bodies.Count];
            _order = new int[_bodies.Count];
            _clock = new float[_bodies.Count];
            for (int i = 0; i < _clock.Length; i++)
            {
                _clock[i] = i * 0.37f; // walkers never step in unison
            }

            Place(0f, true);
        }

        /// <summary>Advances the crowd and poses it. Called by the stage once per frame (dt 0 while paused).</summary>
        public void Tick(float dt, CrowdInputs inputs, List<CrowdEvent> events)
        {
            _watch.Restart();
            Clock.Advance(dt, inputs, events);
            Place(dt, false);
            _watch.Stop();
            LastMs = (float)_watch.Elapsed.TotalMilliseconds;
        }

        private void Place(float dt, bool all)
        {
            var poses = Clock.Poses;
            Vector3 eye = View != null ? View.transform.position : Vector3.zero;
            if (View != null)
            {
                GeometryUtility.CalculateFrustumPlanes(View, _planes);
            }

            for (int i = 0; i < _bodies.Count; i++)
            {
                var pose = poses[i];
                var root = _bodies[i].Root;
                root.SetPositionAndRotation(new Vector3(pose.X, 0f, pose.Z), Quaternion.Euler(0f, pose.Yaw, 0f));
                var centre = root.position + Vector3.up * 0.8f;
                _distances[i] = View != null ? Vector3.Distance(eye, centre) : 0f;
                bool visible = View == null || GeometryUtility.TestPlanesAABB(_planes, new Bounds(centre, new Vector3(1.2f, 1.9f, 1.2f)));
                if (HideHeart && LivingStreetLayout.InHeart(pose.X, pose.Z))
                {
                    visible = false;
                }

                _inView[i] = visible;
            }

            Budget.Decide(_distances, _inView, _detail, _order);
            double previous = _time;
            _time += dt;
            int drawn = 0;
            for (int i = 0; i < _bodies.Count; i++)
            {
                var body = _bodies[i];
                body.SetDetail(_detail[i]);
                drawn += body.Drawn ? 1 : 0;
                _clock[i] += dt;
                switch (_detail[i])
                {
                    case WalkerDetail.Full:
                        body.Animate(_clock[i], poses[i].Speed, poses[i].Gesture, dt);
                        break;
                    case WalkerDetail.Reduced:
                        if (all || CrowdBudget.ReducedTick(_time, previous, i))
                        {
                            body.Animate(_clock[i], poses[i].Speed, poses[i].Gesture, 1f / CrowdBudget.ReducedRateHz);
                        }

                        break;
                    default:
                        if (all)
                        {
                            body.Animate(_clock[i], poses[i].Speed, poses[i].Gesture, dt);
                        }

                        break;
                }
            }

            Drawn = drawn;
        }
    }
}
