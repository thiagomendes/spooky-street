using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.LivingStreet
{
    /// <summary>What a walker is doing with its hands and body (the clip the body plays).</summary>
    public enum WalkerGesture
    {
        None,

        /// <summary>At a door or Pip's porch: holding out the pail.</summary>
        HoldOut,

        /// <summary>A treat was given: a happy cheer.</summary>
        Cheer,

        /// <summary>Looking at a porch's decorations.</summary>
        Look,

        /// <summary>Waiting at the heart's edge for room (looking around, never toward the case).</summary>
        Wait,
    }

    /// <summary>One walker's pose this frame (world plan metres, Unity yaw).</summary>
    public struct WalkerPose
    {
        public float X;
        public float Z;
        public float Yaw;

        /// <summary>Walking speed now (0 when stopped), for the walk cycle.</summary>
        public float Speed;

        public WalkerGesture Gesture;
    }

    public enum CrowdEventKind
    {
        /// <summary>A group reached a neighbour's steps and rang (<see cref="CrowdEvent.DoorId"/>).</summary>
        DoorRung,

        /// <summary>A group reached Pip's steps and rang her bell.</summary>
        PorchRung,

        /// <summary>A group left Pip's steps (served or not).</summary>
        PorchLeft,
    }

    public readonly struct CrowdEvent
    {
        public CrowdEvent(CrowdEventKind kind, int group, string doorId)
        {
            Kind = kind;
            Group = group;
            DoorId = doorId;
        }

        public CrowdEventKind Kind { get; }
        public int Group { get; }
        public string DoorId { get; }

        public override string ToString() => Kind + " #" + Group + (DoorId != null ? " " + DoorId : "");
    }

    /// <summary>What the world tells the crowd each step.</summary>
    public struct CrowdInputs
    {
        public float PipX;
        public float PipZ;

        /// <summary>False when Pip is indoors (she then never makes anyone step aside or wait).</summary>
        public bool PipOutside;

        /// <summary>Case 01 is open: the heart holds at most <see cref="CrowdClock.CaseHeartCap"/> walkers (D34).</summary>
        public bool CaseOpen;

        /// <summary>
        /// Groups may visit Pip's porch: her lantern is lit, and the radio's hint area is not her porch (no crowd
        /// stop ever faces the area a hint points to, living-street §3.4 rule 2).
        /// </summary>
        public bool PorchOpen;

        /// <summary>A cutscene holds the world: the crowd stands still (§3.4 rule 7).</summary>
        public bool Frozen;
    }

    /// <summary>
    /// The slice's crowd clock (living-street §8): every group walks its authored loop at its pace, frame-rate
    /// independent and deterministic, with no navigation mesh. Rules:
    /// <list type="bullet">
    /// <item>The heart holds at most <see cref="CaseHeartCap"/> walkers while case 01 is open (D34); a group waits at the
    /// heart's edge until it fits.</item>
    /// <item>Groups slow and step aside for Pip about a metre in front of them, and never stop for good (they pass
    /// beside her; they have no colliders, so she never gets stuck).</item>
    /// <item>At a neighbour's steps a group rings (an event the world answers) and waits; at Pip's steps it rings her
    /// bell and waits a little longer while she is near, until she hands out a treat (<see cref="Serve"/>).</item>
    /// <item>A lap includes Pip's porch only while <see cref="CrowdInputs.PorchOpen"/>.</item>
    /// </list>
    /// Nothing is saved: the crowd starts on its loops at every load.
    /// </summary>
    public sealed class CrowdClock
    {
        public const int CaseHeartCap = 3;

        /// <summary>After the case closes the heart may hold more (the slice's crowd is smaller than this anyway).</summary>
        public const int OpenHeartCap = 6;

        public const float DoorStopSeconds = 5.4f;
        public const float PorchBaseWait = 4f;
        public const float PorchMaxWait = 20f;
        public const float CheerSeconds = 2.2f;

        /// <summary>Pip within this in front of a walker (and across its path) slows the group.</summary>
        public const float YieldAhead = 1.25f;
        public const float YieldAcross = 0.55f;

        /// <summary>Pip within this of any member makes the group step aside (away from her).</summary>
        public const float SidestepRadius = 1.6f;
        public const float SidestepMetres = 0.6f;
        public const float SidestepSpeed = 1.2f;

        private enum Mode
        {
            Walking,
            Stopped,
            AtGate,
            AtPorch,
            Cheering,
        }

        private sealed class Group
        {
            public CrowdRoute Route;
            public RouteNode[] Lap;
            public float[] Cumulative;
            public int Segment;
            public float Along;
            public Mode Mode;
            public float Timer;
            public float Wait;
            public bool InHeart;
            public bool Served;
            public float Sidestep;
            public float SidestepSign;
            public float SpeedNow;
            public float StopYaw = float.NaN;
        }

        private readonly List<Group> _groups = new List<Group>();
        private readonly RouteNode[][] _plain;
        private readonly float[][] _plainCumulative;
        private readonly WalkerPose[] _poses;

        public CrowdClock() : this(CrowdRoutes.All)
        {
        }

        public CrowdClock(IReadOnlyList<CrowdRoute> routes)
        {
            if (routes == null)
            {
                throw new ArgumentNullException(nameof(routes));
            }

            _plain = new RouteNode[routes.Count][];
            _plainCumulative = new float[routes.Count][];
            int walkers = 0;
            for (int i = 0; i < routes.Count; i++)
            {
                var route = routes[i];
                var group = new Group { Route = route };
                _plain[i] = route.Plain;
                _plainCumulative[i] = Cumulate(route.Plain);
                SetLap(group, false);
                Place(group, route.StartMetres);
                _groups.Add(group);
                walkers += route.Size;
            }

            _poses = new WalkerPose[walkers];
            UpdatePoses(default);
        }

        public int GroupCount => _groups.Count;

        public int WalkerCount => _poses.Length;

        /// <summary>Every walker's pose, group by group, members in order.</summary>
        public IReadOnlyList<WalkerPose> Poses => _poses;

        public CrowdRoute RouteOf(int group) => _groups[group].Route;

        /// <summary>Index of the first walker of <paramref name="group"/> in <see cref="Poses"/>.</summary>
        public int FirstWalker(int group)
        {
            int n = 0;
            for (int i = 0; i < group; i++)
            {
                n += _groups[i].Route.Size;
            }

            return n;
        }

        /// <summary>Walkers whose group has passed into the heart (the cap counts these).</summary>
        public int HeartCommitted
        {
            get
            {
                int n = 0;
                foreach (var g in _groups)
                {
                    n += g.InHeart ? g.Route.Size : 0;
                }

                return n;
            }
        }

        /// <summary>Walkers standing in the heart now (by position).</summary>
        public int WalkersInHeart
        {
            get
            {
                int n = 0;
                foreach (var pose in _poses)
                {
                    n += LivingStreetLayout.InHeart(pose.X, pose.Z) ? 1 : 0;
                }

                return n;
            }
        }

        /// <summary>The group waiting at Pip's steps for a treat (-1 when none).</summary>
        public int WaitingAtPorch
        {
            get
            {
                for (int i = 0; i < _groups.Count; i++)
                {
                    if (_groups[i].Mode == Mode.AtPorch && !_groups[i].Served)
                    {
                        return i;
                    }
                }

                return -1;
            }
        }

        public bool IsStopped(int group) => _groups[group].Mode != Mode.Walking;

        /// <summary>Metres walked into the current lap (tests).</summary>
        public float LapMetres(int group)
        {
            var g = _groups[group];
            return g.Cumulative[g.Segment] + g.Along;
        }

        /// <summary>True when the group's current lap goes to Pip's porch.</summary>
        public bool LapVisitsPorch(int group) => _groups[group].Lap != _groups[group].Route.Plain;

        /// <summary>Pip hands the waiting group a treat. Returns false when no group is waiting at her steps.</summary>
        public bool Serve(int group)
        {
            if (group < 0 || group >= _groups.Count)
            {
                return false;
            }

            var g = _groups[group];
            if (g.Mode != Mode.AtPorch || g.Served)
            {
                return false;
            }

            g.Served = true;
            g.Mode = Mode.Cheering;
            g.Timer = 0f;
            return true;
        }

        /// <summary>
        /// Advances the crowd by <paramref name="dt"/> seconds. Events (rings) are appended to <paramref name="events"/>
        /// when given. Long frames are split so a stop or a gate is never skipped.
        /// </summary>
        public void Advance(float dt, CrowdInputs inputs, List<CrowdEvent> events = null)
        {
            if (!(dt > 0f) || inputs.Frozen)
            {
                return;
            }

            int cap = inputs.CaseOpen ? CaseHeartCap : OpenHeartCap;
            for (int i = 0; i < _groups.Count; i++)
            {
                Step(i, _groups[i], dt, inputs, cap, events);
            }

            UpdatePoses(inputs);
        }

        private void Step(int index, Group g, float dt, CrowdInputs inputs, int cap, List<CrowdEvent> events)
        {
            float left = dt;
            int guard = 0;
            while (left > 1e-6f && guard++ < 64)
            {
                switch (g.Mode)
                {
                    case Mode.Stopped:
                    {
                        float take = Math.Min(left, g.Wait - g.Timer);
                        g.Timer += take;
                        left -= take;
                        g.SpeedNow = 0f;
                        if (g.Timer >= g.Wait - 1e-6f)
                        {
                            g.Mode = Mode.Walking;
                            g.StopYaw = float.NaN;
                        }

                        continue;
                    }
                    case Mode.AtGate:
                        g.SpeedNow = 0f;
                        if (HeartCommittedExcept(g) + g.Route.Size <= cap)
                        {
                            g.InHeart = true;
                            g.Mode = Mode.Walking;
                            continue;
                        }

                        left = 0f;
                        continue;
                    case Mode.AtPorch:
                    {
                        g.SpeedNow = 0f;
                        g.Timer += left;
                        left = 0f;
                        bool near = inputs.PipOutside && Near(inputs.PipX, inputs.PipZ, LivingStreetLayout.PorchStandX,
                            LivingStreetLayout.PorchStandZ, LivingStreetLayout.PorchNear);
                        if (g.Timer >= PorchBaseWait && (!near || g.Timer >= PorchMaxWait))
                        {
                            g.Mode = Mode.Walking;
                            g.StopYaw = float.NaN;
                            events?.Add(new CrowdEvent(CrowdEventKind.PorchLeft, index, null));
                        }

                        continue;
                    }
                    case Mode.Cheering:
                    {
                        g.SpeedNow = 0f;
                        float take = Math.Min(left, CheerSeconds - g.Timer);
                        g.Timer += take;
                        left -= take;
                        if (g.Timer >= CheerSeconds - 1e-6f)
                        {
                            g.Mode = Mode.Walking;
                            g.StopYaw = float.NaN;
                            events?.Add(new CrowdEvent(CrowdEventKind.PorchLeft, index, null));
                        }

                        continue;
                    }
                }

                // Walking.
                float factor = YieldFactor(index, g, inputs);
                UpdateSidestep(index, g, inputs, left);
                float speed = g.Route.Speed * factor;
                g.SpeedNow = speed;
                if (speed <= 1e-4f)
                {
                    left = 0f;
                    continue;
                }

                float length = g.Cumulative[g.Segment + 1] - g.Cumulative[g.Segment];
                float room = length - g.Along;
                float move = speed * left;
                if (move < room)
                {
                    g.Along += move;
                    left = 0f;
                    continue;
                }

                left -= room / speed;
                g.Segment++;
                g.Along = 0f;
                Arrive(index, g, inputs, events);
            }
        }

        /// <summary>The group reached node <c>Segment</c> of its lap: wrap, gate, stop or ring.</summary>
        private void Arrive(int index, Group g, CrowdInputs inputs, List<CrowdEvent> events)
        {
            if (g.Segment >= g.Lap.Length - 1)
            {
                // A lap ends at the branch; the next one goes to Pip's porch only while it is open.
                SetLap(g, inputs.PorchOpen && g.Route.PorchExcursion != null);
                g.Segment = 0;
                g.Along = 0f;
            }

            var node = g.Lap[g.Segment];
            switch (node.Kind)
            {
                case RouteNodeKind.HeartGate:
                    g.Mode = Mode.AtGate;
                    g.StopYaw = float.NaN;
                    break;
                case RouteNodeKind.HeartExit:
                    g.InHeart = false;
                    break;
                case RouteNodeKind.Door:
                    g.Mode = Mode.Stopped;
                    g.Timer = 0f;
                    g.Wait = DoorStopSeconds;
                    g.StopYaw = node.FaceYaw;
                    events?.Add(new CrowdEvent(CrowdEventKind.DoorRung, index, node.DoorId));
                    break;
                case RouteNodeKind.Look:
                    g.Mode = Mode.Stopped;
                    g.Timer = 0f;
                    g.Wait = node.Seconds;
                    g.StopYaw = node.FaceYaw;
                    break;
                case RouteNodeKind.PipPorch:
                    g.StopYaw = node.FaceYaw;
                    g.Timer = 0f;
                    g.Served = false;
                    if (inputs.PorchOpen)
                    {
                        g.Mode = Mode.AtPorch;
                        events?.Add(new CrowdEvent(CrowdEventKind.PorchRung, index, null));
                    }
                    else
                    {
                        // The porch closed on the way (a hint now points there): turn back without ringing.
                        g.Mode = Mode.Stopped;
                        g.Wait = 0.5f;
                    }

                    break;
            }

            g.Sidestep = g.Mode == Mode.Walking ? g.Sidestep : 0f;
        }

        private int HeartCommittedExcept(Group except)
        {
            int n = 0;
            foreach (var g in _groups)
            {
                n += g != except && g.InHeart ? g.Route.Size : 0;
            }

            return n;
        }

        /// <summary>How fast the group may walk with Pip where she is (1 free, 0 she stands right in front).</summary>
        private float YieldFactor(int index, Group g, CrowdInputs inputs)
        {
            if (!inputs.PipOutside)
            {
                return 1f;
            }

            float factor = 1f;
            int first = FirstWalker(index);
            for (int m = 0; m < g.Route.Size; m++)
            {
                var pose = _poses[first + m];
                double yaw = pose.Yaw * Math.PI / 180.0;
                float fx = (float)Math.Sin(yaw);
                float fz = (float)Math.Cos(yaw);
                float dx = inputs.PipX - pose.X;
                float dz = inputs.PipZ - pose.Z;
                float ahead = dx * fx + dz * fz;
                float across = Math.Abs(dx * fz - dz * fx);
                if (ahead > -0.1f && ahead < YieldAhead && across < YieldAcross)
                {
                    factor = Math.Min(factor, Clamp01((ahead - 0.35f) / (YieldAhead - 0.35f)));
                }
            }

            // Never a full stop for long: a group creeps past at a fifth of its pace while it steps aside.
            return Math.Max(factor, Math.Abs(g.Sidestep) >= SidestepMetres - 0.05f ? 0.2f : 0f);
        }

        private void UpdateSidestep(int index, Group g, CrowdInputs inputs, float dt)
        {
            float target = 0f;
            if (inputs.PipOutside)
            {
                int first = FirstWalker(index);
                for (int m = 0; m < g.Route.Size; m++)
                {
                    var pose = _poses[first + m];
                    if (!Near(inputs.PipX, inputs.PipZ, pose.X, pose.Z, SidestepRadius))
                    {
                        continue;
                    }

                    if (g.SidestepSign == 0f)
                    {
                        // Step to the side away from Pip (the walker's right is +lateral).
                        double yaw = pose.Yaw * Math.PI / 180.0;
                        float rx = (float)Math.Cos(yaw);
                        float rz = -(float)Math.Sin(yaw);
                        float side = (inputs.PipX - pose.X) * rx + (inputs.PipZ - pose.Z) * rz;
                        g.SidestepSign = side > 0f ? -1f : 1f;
                    }

                    target = g.SidestepSign * SidestepMetres;
                    break;
                }
            }

            if (target == 0f && Math.Abs(g.Sidestep) < 1e-3f)
            {
                g.SidestepSign = 0f;
            }

            float delta = target - g.Sidestep;
            float stepMax = SidestepSpeed * dt;
            g.Sidestep += Math.Abs(delta) <= stepMax ? delta : Math.Sign(delta) * stepMax;
        }

        private void UpdatePoses(CrowdInputs inputs)
        {
            int w = 0;
            for (int i = 0; i < _groups.Count; i++)
            {
                var g = _groups[i];
                float s = g.Cumulative[g.Segment] + g.Along;
                WalkerGesture gesture = g.Mode == Mode.AtPorch ? WalkerGesture.HoldOut
                    : g.Mode == Mode.Cheering ? WalkerGesture.Cheer
                    : g.Mode == Mode.AtGate ? WalkerGesture.Wait
                    : g.Mode == Mode.Stopped && g.Lap[g.Segment].Kind == RouteNodeKind.Door ? WalkerGesture.HoldOut
                    : g.Mode == Mode.Stopped && g.Lap[g.Segment].Kind == RouteNodeKind.Look ? WalkerGesture.Look
                    : WalkerGesture.None;
                foreach (var member in g.Route.Members)
                {
                    PointAt(i, g, s - member.Back, out float x, out float z, out float hx, out float hz);
                    float lateral = member.Lateral + (g.Mode == Mode.Walking ? g.Sidestep : 0f);
                    // Right of a heading (hx, hz) in plan is (hz, -hx).
                    var pose = new WalkerPose
                    {
                        X = x + hz * lateral,
                        Z = z - hx * lateral,
                        Yaw = !float.IsNaN(g.StopYaw) ? g.StopYaw : (float)(Math.Atan2(hx, hz) * 180.0 / Math.PI),
                        Speed = g.SpeedNow,
                        Gesture = gesture,
                    };
                    _poses[w++] = pose;
                }
            }
        }

        /// <summary>Point and heading at <paramref name="s"/> metres into the group's lap (negative: the previous lap's tail).</summary>
        private void PointAt(int index, Group g, float s, out float x, out float z, out float hx, out float hz)
        {
            var nodes = g.Lap;
            var cumulative = g.Cumulative;
            if (s < 0f)
            {
                // Every lap ends with the plain lap's last leg, so the previous lap's tail is the plain lap's.
                nodes = _plain[index];
                cumulative = _plainCumulative[index];
                s += cumulative[cumulative.Length - 1];
                if (s < 0f)
                {
                    s = 0f;
                }
            }

            int seg = 0;
            while (seg < nodes.Length - 2 && cumulative[seg + 1] < s)
            {
                seg++;
            }

            // Skip zero-length legs so a heading always exists.
            var a = nodes[seg];
            var b = nodes[seg + 1];
            float len = cumulative[seg + 1] - cumulative[seg];
            float t = len > 1e-5f ? Clamp01((s - cumulative[seg]) / len) : 1f;
            x = a.X + (b.X - a.X) * t;
            z = a.Z + (b.Z - a.Z) * t;
            Heading(nodes, seg, out hx, out hz);
        }

        private static void Heading(RouteNode[] nodes, int seg, out float hx, out float hz)
        {
            for (int k = seg; k < nodes.Length - 1; k++)
            {
                float dx = nodes[k + 1].X - nodes[k].X;
                float dz = nodes[k + 1].Z - nodes[k].Z;
                float l = (float)Math.Sqrt(dx * dx + dz * dz);
                if (l > 1e-4f)
                {
                    hx = dx / l;
                    hz = dz / l;
                    return;
                }
            }

            hx = 0f;
            hz = 1f;
        }

        private static void SetLap(Group g, bool withPorch)
        {
            g.Lap = g.Route.Lap(withPorch);
            g.Cumulative = Cumulate(g.Lap);
        }

        /// <summary>Puts the group <paramref name="metres"/> into its current lap without stopping on the way.</summary>
        private static void Place(Group g, float metres)
        {
            float total = g.Cumulative[g.Cumulative.Length - 1];
            float s = total > 0f ? metres % total : 0f;
            int seg = 0;
            while (seg < g.Lap.Length - 2 && g.Cumulative[seg + 1] <= s)
            {
                seg++;
            }

            g.Segment = seg;
            g.Along = s - g.Cumulative[seg];
            g.Mode = Mode.Walking;
        }

        private static float[] Cumulate(RouteNode[] nodes)
        {
            var c = new float[nodes.Length];
            for (int i = 1; i < nodes.Length; i++)
            {
                float dx = nodes[i].X - nodes[i - 1].X;
                float dz = nodes[i].Z - nodes[i - 1].Z;
                c[i] = c[i - 1] + (float)Math.Sqrt(dx * dx + dz * dz);
            }

            return c;
        }

        private static bool Near(float ax, float az, float bx, float bz, float r)
        {
            float dx = ax - bx;
            float dz = az - bz;
            return dx * dx + dz * dz <= r * r;
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
