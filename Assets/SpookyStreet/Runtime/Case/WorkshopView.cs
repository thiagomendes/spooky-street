using System;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.World;
using SpookyStreet.World;
using UnityEngine;

namespace SpookyStreet.Case
{
    /// <summary>
    /// The workshop mechanism behind the grille and the rooftop owl it drives (spec §2 "what actually happened"): the
    /// service panel swings open, the slipped link arm is reseated, and each knock cycle turns the three-lobed cam 120
    /// degrees per knock while the follower lifts and drops on every knock ("short, short, pause, last") and, once the
    /// arm is linked, the owl puppet bows with it. Every pose is one function of cycle time
    /// (<see cref="MechanismMotion"/>), so cam, follower, crank and owl stay in step frame for frame.
    /// Rotations are about world axes through each part's exported pivot; the sign of each hinge is chosen at runtime
    /// from the part's own geometry (which way is "up", "toward the foyer", "toward the street"), so it does not
    /// depend on how the importer converted Blender's axes. Runs on game time.
    /// </summary>
    public sealed class WorkshopView : MonoBehaviour
    {
        public const float PanelOpenDegrees = 112f;
        public const float PanelSwingSeconds = 0.7f;

        /// <summary>Wind-driven cycles: the knocks keep coming every so often (the owl bows once it is linked).</summary>
        public const float WindIntervalSeconds = 9f;

        private Transform _cam, _follower, _crank, _panel, _armSeated, _armSlipped, _owl;
        private Quaternion _camRest, _followerRest, _crankRest, _panelRest;
        private Vector3 _armRest;
        private Vector3 _followerAxis, _crankAxis, _owlAxis;
        private float _panelSign = 1f;
        private float _panelAngle, _panelTarget;
        private float[] _knocks;
        private float _cycleLength;
        private float _time = -1f;
        private bool _bowThisCycle;
        private float _camBase;
        private float _windClock;

        /// <summary>The mechanism model (null without it).</summary>
        public Transform Root { get; private set; }

        public bool PanelOpen => _panelTarget > 0.5f;

        /// <summary>How far the service panel has swung about its hinge (0 closed, 112 open).</summary>
        public float PanelDegrees => PanelOpenDegrees * _panelAngle * _panelAngle * (3f - 2f * _panelAngle);
        public bool ArmLinked { get; private set; }

        /// <summary>The owl bows with every knock (tested).</summary>
        public bool OwlBowing { get; private set; }

        /// <summary>True while a knock cycle plays.</summary>
        public bool Playing => _time >= 0f;

        /// <summary>Knocks heard in the current (or last) cycle.</summary>
        public int KnocksThisCycle { get; private set; }

        /// <summary>Bows the owl made in the current (or last) cycle.</summary>
        public int BowsThisCycle { get; private set; }

        /// <summary>Highest follower lift (0..1) seen this cycle (tests: the follower visibly lifted).</summary>
        public float FollowerLift { get; private set; }

        public float OwlBow { get; private set; }

        public float CamAngle { get; private set; }

        /// <summary>Wind-driven cycles run (they never start before the arrival knocks were heard).</summary>
        public bool WindEnabled { get; set; }

        /// <summary>True when the current (or last) cycle was started by the wind rather than the crank.</summary>
        public bool WindCycle { get; private set; }

        /// <summary>Raised when a knock cycle starts; true for a wind-driven one (the rooftop wheel's whirr, S003).</summary>
        public event Action<bool> CycleStarted;

        /// <summary>Raised on each knock contact (0-based), the frame the follower drops onto the cam (the cam tap).</summary>
        public event Action<int> Knocked;

        public Transform Owl => _owl;
        public Transform Cam => _cam;
        public Transform Follower => _follower;
        public Transform Panel => _panel;
        public Transform ArmSeated => _armSeated;
        public Transform ArmSlipped => _armSlipped;

        public static WorkshopView Create(Transform parent, Transform mechanism, Transform owl)
        {
            var go = new GameObject("WorkshopView");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<WorkshopView>();
            view.Bind(mechanism, owl);
            return view;
        }

        private void Bind(Transform mechanism, Transform owl)
        {
            Root = mechanism;
            _owl = owl;
            if (mechanism != null)
            {
                _cam = ModelKit.FindDeep(mechanism, CaseRouteLayout.Cam);
                _follower = ModelKit.FindDeep(mechanism, CaseRouteLayout.Follower);
                _crank = ModelKit.FindDeep(mechanism, CaseRouteLayout.Crank);
                _panel = ModelKit.FindDeep(mechanism, CaseRouteLayout.ServicePanel);
                _armSeated = ModelKit.FindDeep(mechanism, CaseRouteLayout.LinkArmSeated);
                _armSlipped = ModelKit.FindDeep(mechanism, CaseRouteLayout.LinkArmSlipped);
            }

            if (_cam != null)
            {
                _camRest = _cam.rotation;
            }

            if (_follower != null)
            {
                _followerRest = _follower.rotation;
                // The follower runs from its hinge toward the cam shaft; it lifts about the horizontal axis across it.
                Vector3 arm = (_cam != null ? _cam.position : CentreOf(_follower)) - _follower.position;
                arm.y = 0f;
                if (arm.sqrMagnitude < 1e-4f)
                {
                    arm = Vector3.back;
                }

                _followerAxis = Vector3.Cross(Vector3.up, arm.normalized);
                if ((Quaternion.AngleAxis(10f, _followerAxis) * arm).y < 0f)
                {
                    _followerAxis = -_followerAxis;
                }
            }

            if (_crank != null)
            {
                _crankRest = _crank.rotation;
                // The crank's axle runs front to back: from the foyer (-X) into the nook.
                _crankAxis = Vector3.right;
            }

            if (_panel != null)
            {
                _panelRest = _panel.rotation;
                // Swing so the free edge comes out toward the foyer (-X).
                Vector3 leaf = CentreOf(_panel) - _panel.position;
                leaf.y = 0f;
                _panelSign = (Quaternion.AngleAxis(PanelOpenDegrees * 0.5f, Vector3.up) * leaf).x < leaf.x ? 1f : -1f;
            }

            if (_armSeated != null)
            {
                _armRest = _armSeated.localPosition;
            }

            if (_owl != null)
            {
                // Bow toward the street (-Z): tip the owl's top toward -Z about the world X axis.
                _owlAxis = (Quaternion.AngleAxis(10f, Vector3.right) * Vector3.up).z < 0f ? Vector3.right : Vector3.left;
            }

            SetState(false, false, false);
        }

        private static Vector3 CentreOf(Transform part)
        {
            var renderer = part != null ? part.GetComponentInChildren<Renderer>() : null;
            return renderer != null ? renderer.bounds.center : part != null ? part.position : Vector3.zero;
        }

        /// <summary>Instantly shows a state rebuilt from facts (resume, new game, replay).</summary>
        public void SetState(bool panelOpen, bool armLinked, bool owlBowing)
        {
            _panelTarget = panelOpen ? 1f : 0f;
            _panelAngle = _panelTarget;
            ArmLinked = armLinked;
            OwlBowing = owlBowing;
            _time = -1f;
            _windClock = 0f;
            ApplyArm();
            Pose(0f, 0f, 0f);
            ApplyPanel();
        }

        /// <summary>Swings the service panel open (+112 degrees about its hinge).</summary>
        public void OpenPanel() => _panelTarget = 1f;

        /// <summary>Reseats the link arm (LinkArm_Slipped hides, LinkArm_Seated shows).</summary>
        public void LinkArm()
        {
            ArmLinked = true;
            ApplyArm();
        }

        /// <summary>One knock cycle from the crank or the wind. The owl bows only when the arm is linked.</summary>
        public void PlayCycle()
        {
            Begin(MechanismMotion.KnockTimes, MechanismMotion.CycleSeconds, ArmLinked, false);
        }

        /// <summary>The crank turned before the arm: a single tap, the cam a third of a turn, no bow (spec §3).</summary>
        public void PlayGap()
        {
            Begin(MechanismMotion.GapKnockTimes, MechanismMotion.GapCycleSeconds, false, false);
        }

        /// <summary>Owl bows for good from now on (the test crank worked).</summary>
        public void SetBowing()
        {
            OwlBowing = true;
        }

        private void Begin(float[] knocks, float length, bool bow, bool wind)
        {
            if (Playing)
            {
                _camBase += MechanismMotion.CamAngle(_cycleLength, _knocks);
            }

            _knocks = knocks;
            _cycleLength = length;
            _bowThisCycle = bow;
            _time = 0f;
            _windClock = 0f;
            KnocksThisCycle = 0;
            BowsThisCycle = 0;
            FollowerLift = 0f;
            WindCycle = wind;
            CycleStarted?.Invoke(wind);
        }

        private void ApplyArm()
        {
            if (_armSeated != null)
            {
                _armSeated.gameObject.SetActive(ArmLinked);
            }

            if (_armSlipped != null)
            {
                _armSlipped.gameObject.SetActive(!ArmLinked);
            }
        }

        private void ApplyPanel()
        {
            if (_panel != null)
            {
                float eased = _panelAngle * _panelAngle * (3f - 2f * _panelAngle);
                _panel.rotation = Quaternion.AngleAxis(_panelSign * PanelOpenDegrees * eased, Vector3.up) * _panelRest;
            }
        }

        private void Pose(float camAngle, float lift, float bow)
        {
            CamAngle = camAngle;
            OwlBow = bow;
            if (_cam != null)
            {
                _cam.rotation = Quaternion.AngleAxis(camAngle, Vector3.up) * _camRest;
            }

            if (_follower != null)
            {
                _follower.rotation = Quaternion.AngleAxis(lift * MechanismMotion.FollowerLiftDegrees, _followerAxis) *
                                     _followerRest;
            }

            if (_crank != null)
            {
                _crank.rotation = Quaternion.AngleAxis(camAngle, _crankAxis) * _crankRest;
            }

            if (_armSeated != null && ArmLinked)
            {
                _armSeated.localPosition = _armRest;
                _armSeated.position += Vector3.up * (0.05f * lift);
            }
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            if (Mathf.Abs(_panelAngle - _panelTarget) > 1e-4f)
            {
                _panelAngle = Mathf.MoveTowards(_panelAngle, _panelTarget, dt / PanelSwingSeconds);
                ApplyPanel();
            }

            if (_time < 0f)
            {
                if (WindEnabled)
                {
                    _windClock += dt;
                    if (_windClock >= WindIntervalSeconds)
                    {
                        Begin(MechanismMotion.KnockTimes, MechanismMotion.CycleSeconds, OwlBowing, true);
                    }
                }

                return;
            }

            _time += dt;
            float t = Mathf.Min(_time, _cycleLength);
            float lift = MechanismMotion.FollowerLift(t, _knocks);
            float bow = _bowThisCycle ? MechanismMotion.OwlBow(t, _knocks) : 0f;
            FollowerLift = Mathf.Max(FollowerLift, lift);
            int knocks = MechanismMotion.KnocksBy(t, _knocks);
            if (knocks > KnocksThisCycle)
            {
                int first = KnocksThisCycle;
                KnocksThisCycle = knocks;
                if (_bowThisCycle)
                {
                    BowsThisCycle = knocks;
                }

                for (int k = first; k < knocks; k++)
                {
                    Knocked?.Invoke(k);
                }
            }

            Pose(_camBase + MechanismMotion.CamAngle(t, _knocks), lift, bow);
            if (_time >= _cycleLength)
            {
                _camBase = (_camBase + MechanismMotion.CamAngle(_cycleLength, _knocks)) % 360f;
                _time = -1f;
            }
        }

        private void LateUpdate()
        {
            // AmbientMotion rewrites the owl's sway in Update; the bow tips it further toward the street on top.
            if (_owl != null && OwlBow > 1e-3f)
            {
                _owl.rotation = Quaternion.AngleAxis(OwlBow * MechanismMotion.OwlBowDegrees, _owlAxis) * _owl.rotation;
            }
        }
    }
}
