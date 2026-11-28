using System;
using System.Collections.Generic;
using SpookyStreet.Core.Crowd;

namespace SpookyStreet.Core.LivingStreet
{
    /// <summary>What a walker is doing with its hands and body (kept for logs and older callers; see <see cref="WalkerPose.Clip"/>).</summary>
    public enum WalkerGesture
    {
        None,

        /// <summary>At a door or Pip's porch: holding out the pail.</summary>
        HoldOut,

        /// <summary>A treat was given: a happy cheer.</summary>
        Cheer,

        /// <summary>Admiring a decoration.</summary>
        Look,

        /// <summary>Waiting its turn (at the heart's edge or a few metres back from a busy door).</summary>
        Wait,
    }

    /// <summary>One walker's pose this frame (world plan metres, Unity yaw).</summary>
    public struct WalkerPose
    {
        public float X;
        public float Z;

        /// <summary>Feet height (0 on the ground).</summary>
        public float Y;

        public float Yaw;

        /// <summary>Walking speed now (0 when stopped), for the walk cycle.</summary>
        public float Speed;

        public WalkerGesture Gesture;

        /// <summary>The one-shot to play (Locomotion: walk or stand by <see cref="Speed"/>), at <see cref="ClipTime"/> seconds.</summary>
        public WalkerClip Clip;

        public float ClipTime;

        /// <summary>Turning on the spot (the body steps round slowly).</summary>
        public bool Turning;
    }

    public enum CrowdEventKind
    {
        /// <summary>A group rang a neighbour's bell (<see cref="CrowdEvent.DoorId"/>, <see cref="CrowdEvent.Ring"/> 1–3).</summary>
        DoorRung,

        /// <summary>A group reached Pip's steps and rang her bell.</summary>
        PorchRung,

        /// <summary>A group left Pip's steps (served or not).</summary>
        PorchLeft,

        /// <summary>A child stands in front of Pip with its pail held out: Pip plays HandTreat now (<see cref="CrowdEvent.Walker"/>).</summary>
        PipHandTreat,

        /// <summary>Pip lets her sweet go over the child's pail (<see cref="CrowdEvent.Walker"/>).</summary>
        PipTreatReleased,
    }

    public readonly struct CrowdEvent
    {
        public CrowdEvent(CrowdEventKind kind, int group, string doorId, int walker = -1, int ring = 0)
        {
            Kind = kind;
            Group = group;
            DoorId = doorId;
            Walker = walker;
            Ring = ring;
        }

        public CrowdEventKind Kind { get; }
        public int Group { get; }
        public string DoorId { get; }

        /// <summary>The walker (index into <see cref="CrowdClock.Poses"/>), or -1.</summary>
        public int Walker { get; }

        public int Ring { get; }

        public override string ToString() => Kind + " #" + Group + (DoorId != null ? " " + DoorId : "") + (Ring > 0 ? " ring " + Ring : "");
    }

    /// <summary>What the world tells the crowd each step.</summary>
    public struct CrowdInputs
    {
        public float PipX;
        public float PipZ;

        /// <summary>False when Pip is indoors (she then never makes anyone step aside or wait).</summary>
        public bool PipOutside;

        /// <summary>Pip's walking direction and whether she is walking (walkers part to let her through).</summary>
        public float PipHeadingX;

        public float PipHeadingZ;

        public bool PipWalking;

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
    /// The slice's crowd clock (living-street §8; owner, 2026-11-28: purposeful walkers, doors rung three times, a treat
    /// handed to each child, no mobs, no bumping, solid bodies that give way). Each group walks its route leg by leg, and
    /// every leg ends at a goal: the next door, Pip's porch once her lantern hangs, a decoration to admire.
    /// <list type="bullet">
    /// <item>Paths follow the sidewalks and garden paths with every bend rounded (<see cref="CrowdPath"/>), so a family
    /// turns together on a curve; a step aside never heads into a hedge (<see cref="StreetObstacles"/>).</item>
    /// <item>A few metres before a door's path a group asks for the door (<see cref="NeighbourDoors.TryHold"/>): it turns
    /// up the path, or waits its turn there if one place in the queue is free, or walks on to the next door. A door never
    /// gathers a mob: one visiting group (at most three walkers) and Pip.</item>
    /// <item>At the door the family gathers in a small arc facing it (<see cref="DoorStoops.WaitSlot"/>), a child rings
    /// three times, and each child in turn steps up, holds out its pail (HoldPail) for its treat, cheers, and steps back to
    /// make room. Then the family turns together and walks on.</item>
    /// <item>Walkers start and stop with WalkStart and WalkStop, the root moved exactly by the clips' travel, so feet stay
    /// planted; they turn while walking, never on a dead stop (a turn in place steps round slowly).</item>
    /// <item>The heart holds at most <see cref="CaseHeartCap"/> walkers while case 01 is open (D34); a group waits at the
    /// heart's edge until it fits. Nobody testifies, and no stop faces a hint area (§3.4).</item>
    /// <item>Groups slow and step aside for Pip about a metre in front; standing walkers keep room around her and part to
    /// let her through (<see cref="GiveWay"/>); no two walkers overlap.</item>
    /// </list>
    /// The clock runs on fixed 1/60 s steps and interpolates the poses, so it is the same at 30 and 60 fps. Nothing is
    /// saved: the crowd starts on its loops at every load. No timer pressures the child: a group at Pip's steps waits a
    /// little and moves on if she is away, and nothing is lost.
    /// </summary>
    public sealed class CrowdClock
    {
        public const int CaseHeartCap = 3;

        /// <summary>After the case closes the heart may hold more (the slice's crowd is smaller than this anyway).</summary>
        public const int OpenHeartCap = 6;

        public const float PorchBaseWait = 4f;
        public const float PorchMaxWait = 20f;

        /// <summary>The cheer after a treat (the art's Cheer clip).</summary>
        public static readonly float CheerSeconds = WalkerClips.Seconds(WalkerClip.Cheer);

        /// <summary>Pip within this in front of a walker (and across its path) slows the group.</summary>
        public const float YieldAhead = 1.25f;
        public const float YieldAcross = 0.55f;

        /// <summary>Pip within this of any member makes the group step aside (away from her).</summary>
        public const float SidestepRadius = 1.6f;
        public const float SidestepMetres = 0.6f;
        public const float SidestepSpeed = 1.2f;

        /// <summary>The clock's fixed step.</summary>
        public const float StepSeconds = 1f / 60f;

        /// <summary>A group decides about a door (or Pip's porch) this far before its path (and waits its turn here).</summary>
        public const float Lookahead = 2.8f;

        /// <summary>Metres of walked path kept behind the leader for the members behind it.</summary>
        public const float TailMetres = 4f;

        /// <summary>A child presses the bell again this long after the house answered the last ring.</summary>
        public const float RingPause = 0.45f;

        /// <summary>The next child steps up this long after the last one turned back to its place.</summary>
        public const float NextChildDelay = 0.5f;

        /// <summary>A child admiring a decoration points (waves) at it this far into the stop.</summary>
        public const float PointAt = 0.6f;

        private enum Mode
        {
            Walking,
            Stopping,
            Starting,
            AtGate,
            Queued,
            Gathering,
            Ringing,
            AtDoor,
            AtPorch,
            PorchTreats,
            Looking,
            Departing,
        }

        private enum Treat
        {
            Waiting,
            Approaching,
            Holding,
            Cheering,
            Returning,
            Done,
        }

        private sealed class Member
        {
            public MemberSlot Slot;
            public string Model;
            public bool Child;
            public int ChildIndex = -1;
            public float X, Z, Y, Yaw, Speed, ClipTime;
            public WalkerClip Clip;
            public bool Turning;
            public bool Striding;
            public Stride Stride;
            public float StrideT;
            public Treat Treat;
            public float TreatT;
            public float SlotX, SlotZ, SlotYaw;
        }

        private sealed class Group
        {
            public int Index;
            public CrowdRoute Route;
            public Member[] Members;
            public int Children;
            public CrowdPath Path;
            public float S;
            public int MarkIndex;
            public int Branch;
            public float DecideS;
            public bool Decided;
            public Mode Mode;
            public Mode AfterStop;
            public float Timer;
            public float MoveFrom;
            public float MoveTo;
            public RouteNode StopNode;
            public float StopS;
            public bool InHeart;
            public bool Served;
            public float Sidestep;
            public float SidestepSign;
            public float SpeedNow;
            public string Door;
            public string QueuedDoor;
            public string ReleaseDoor;
            public float RingT;
            public float Walked;
            public int Laps;
            public bool PorchLap;
        }

        private readonly List<Group> _groups = new List<Group>();
        private readonly List<GiveWay.Walker> _giveWay = new List<GiveWay.Walker>();
        private readonly WalkerPose[] _poses;
        private readonly WalkerPose[] _previous;
        private readonly WalkerPose[] _out;
        private readonly float[] _offX;
        private readonly float[] _offZ;
        private readonly Func<float, float, float> _clearance = StreetObstacles.Clearance;
        private double _time;
        private long _steps;
        private List<DoorEvent> _doorEvents;

        public CrowdClock() : this(CrowdRoutes.All)
        {
        }

        public CrowdClock(IReadOnlyList<CrowdRoute> routes, NeighbourDoors doors = null)
        {
            if (routes == null)
            {
                throw new ArgumentNullException(nameof(routes));
            }

            Doors = doors ?? new NeighbourDoors();
            int walkers = 0;
            for (int i = 0; i < routes.Count; i++)
            {
                var route = routes[i];
                var g = new Group { Index = i, Route = route, Members = new Member[route.Size] };
                int child = 0;
                for (int m = 0; m < route.Size; m++)
                {
                    var slot = route.Members[m];
                    bool isChild = slot.Look.Body == CrowdBodyKind.SmallChild;
                    g.Members[m] = new Member { Slot = slot, Model = slot.Look.Model, Child = isChild, ChildIndex = isChild ? child++ : -1 };
                }

                g.Children = child;
                StartOnLoop(g, route.StartMetres);
                _groups.Add(g);
                walkers += route.Size;
            }

            _poses = new WalkerPose[walkers];
            _previous = new WalkerPose[walkers];
            _out = new WalkerPose[walkers];
            _offX = new float[walkers];
            _offZ = new float[walkers];
            for (int i = 0; i < walkers; i++)
            {
                _giveWay.Add(default);
            }

            UpdatePoses(default, 0f);
            Array.Copy(_poses, _previous, walkers);
            Array.Copy(_poses, _out, walkers);
        }

        /// <summary>The three doors the groups ring (the same object the game shows).</summary>
        public NeighbourDoors Doors { get; }

        public int GroupCount => _groups.Count;

        public int WalkerCount => _poses.Length;

        /// <summary>Every walker's pose, group by group, members in order (interpolated between the clock's steps).</summary>
        public IReadOnlyList<WalkerPose> Poses => _out;

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

        /// <summary>The walker who is child <paramref name="turn"/> of <paramref name="group"/> (its treat turn), or -1.</summary>
        public int ChildWalker(int group, int turn)
        {
            if (group < 0 || group >= _groups.Count)
            {
                return -1;
            }

            var g = _groups[group];
            for (int m = 0; m < g.Members.Length; m++)
            {
                if (g.Members[m].ChildIndex == turn)
                {
                    return FirstWalker(group) + m;
                }
            }

            return -1;
        }

        /// <summary>How many children walk in <paramref name="group"/> (one treat each).</summary>
        public int ChildrenOf(int group) => _groups[group].Children;

        /// <summary>True for a grown-up walker.</summary>
        public bool IsGrownUp(int walker)
        {
            int w = 0;
            foreach (var g in _groups)
            {
                foreach (var m in g.Members)
                {
                    if (w++ == walker)
                    {
                        return !m.Child;
                    }
                }
            }

            return false;
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

        /// <summary>The group waiting at Pip's steps for her to hand out treats (-1 when none).</summary>
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

        /// <summary>True while a group is handed treats at Pip's porch (she is busy giving).</summary>
        public bool PorchTreatsGoing
        {
            get
            {
                foreach (var g in _groups)
                {
                    if (g.Mode == Mode.PorchTreats)
                    {
                        return true;
                    }
                }

                return false;
            }
        }

        public bool IsStopped(int group) => _groups[group].Mode != Mode.Walking;

        /// <summary>Metres the group has walked (an odometer; tests).</summary>
        public float LapMetres(int group) => _groups[group].Walked;

        /// <summary>Whole laps the group has finished.</summary>
        public int Laps(int group) => _groups[group].Laps;

        /// <summary>True when the group's current lap goes to Pip's porch.</summary>
        public bool LapVisitsPorch(int group) => _groups[group].PorchLap;

        /// <summary>The door the group is visiting or waiting for (null when none).</summary>
        public string DoorOf(int group) => _groups[group].Door ?? _groups[group].QueuedDoor;

        /// <summary>True while the group waits its turn a few metres back from a busy door.</summary>
        public bool IsQueued(int group) =>
            _groups[group].Mode == Mode.Queued || (_groups[group].Mode == Mode.Stopping && _groups[group].AfterStop == Mode.Queued);

        /// <summary>
        /// Pip hands treats to the group waiting at her steps: each child in turn steps up to her and gets one. Returns
        /// false when no group is waiting there.
        /// </summary>
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
            g.Mode = Mode.PorchTreats;
            g.Timer = 0f;
            foreach (var m in g.Members)
            {
                m.Treat = Treat.Waiting;
                m.TreatT = 0f;
            }

            return true;
        }

        /// <summary>
        /// Advances the crowd (and the doors it rings) by <paramref name="dt"/> seconds in fixed steps. Crowd events and
        /// door events are appended when lists are given.
        /// </summary>
        public void Advance(float dt, CrowdInputs inputs, List<CrowdEvent> events = null, List<DoorEvent> doorEvents = null)
        {
            if (!(dt > 0f) || inputs.Frozen)
            {
                return;
            }

            _time += dt;
            long target = (long)Math.Floor(_time / StepSeconds + 1e-6);
            int cap = inputs.CaseOpen ? CaseHeartCap : OpenHeartCap;
            _doorEvents = doorEvents;
            int guard = 0;
            while (_steps < target && guard++ < 240)
            {
                Array.Copy(_poses, _previous, _poses.Length);
                for (int i = 0; i < _groups.Count; i++)
                {
                    Step(_groups[i], StepSeconds, inputs, cap, events);
                }

                Doors.Advance(StepSeconds, inputs.PipX, inputs.PipZ, doorEvents);
                UpdatePoses(inputs, StepSeconds);
                _steps++;
            }

            if (_steps < target)
            {
                _steps = target; // a very long frame: skip ahead rather than stall
            }

            float alpha = (float)((_time - _steps * (double)StepSeconds) / StepSeconds);
            alpha = alpha < 0f ? 0f : alpha > 1f ? 1f : alpha;
            for (int i = 0; i < _poses.Length; i++)
            {
                var a = _previous[i];
                var b = _poses[i];
                var p = b;
                p.X = a.X + (b.X - a.X) * alpha;
                p.Z = a.Z + (b.Z - a.Z) * alpha;
                p.Y = a.Y + (b.Y - a.Y) * alpha;
                p.Yaw = Stride.LerpYaw(a.Yaw, b.Yaw, alpha);
                _out[i] = p;
            }
        }

        // ----------------------------------------------------------------------------------------------- legs

        private static int Unique(CrowdRoute r) => r.Plain.Length - 1;

        /// <summary>The next node after <paramref name="from"/> where something may happen: the lap's first node or a trip's branch.</summary>
        private static int NextBranch(CrowdRoute r, int from)
        {
            int n = Unique(r);
            int i = from;
            for (int k = 0; k < n; k++)
            {
                i = (i + 1) % n;
                if (i == 0 || r.DetourAt(i) != null)
                {
                    return i;
                }
            }

            return 0;
        }

        /// <summary>Places the group <paramref name="metres"/> along its plain lap, walking, with a path to its next branch.</summary>
        private void StartOnLoop(Group g, float metres)
        {
            var r = g.Route;
            int n = Unique(r);
            float total = 0f;
            for (int i = 0; i < n; i++)
            {
                total += Dist(r.Plain[i], r.Plain[i + 1]);
            }

            float s = total > 0f ? metres % total : 0f;
            int seg = 0;
            float acc = 0f;
            while (seg < n - 1 && acc + Dist(r.Plain[seg], r.Plain[seg + 1]) <= s)
            {
                acc += Dist(r.Plain[seg], r.Plain[seg + 1]);
                seg++;
            }

            // Nodes from two before the start (a tail for the members behind) to the branch after it, and one more.
            var nodes = new List<RouteNode>();
            int prev = (seg - 2 + n) % n;
            nodes.Add(r.Plain[prev]);
            nodes.Add(r.Plain[(seg - 1 + n) % n]);
            nodes.Add(r.Plain[seg]);
            int branch = NextBranch(r, seg);
            for (int i = (seg + 1) % n; ; i = (i + 1) % n)
            {
                nodes.Add(r.Plain[i]);
                if (i == branch)
                {
                    break;
                }
            }

            nodes.Add(r.Plain[(branch + 1) % n]);
            g.Path = CrowdPath.Build(null, nodes);
            var start = r.Plain[seg];
            var next = r.Plain[(seg + 1) % n];
            float into = s - acc;
            float len = Dist(start, next);
            float px = start.X + (next.X - start.X) * (len > 0f ? into / len : 0f);
            float pz = start.Z + (next.Z - start.Z) * (len > 0f ? into / len : 0f);
            g.S = Closest(g.Path, px, pz, g.Path.NodeS[3] + 0.1f);
            g.MarkIndex = FirstMarkAfter(g.Path, g.S);
            g.Branch = branch;
            g.DecideS = g.Path.NodeS[nodes.Count - 2] - Lookahead;
            g.Decided = false;
            g.Mode = Mode.Walking;
        }

        /// <summary>
        /// The group decided at its branch: a new path from where it walks (its tail kept) through the branch (and the
        /// trip, when <paramref name="trip"/>) to the next branch.
        /// </summary>
        private void Rebuild(Group g, RouteDetour trip)
        {
            var r = g.Route;
            int n = Unique(r);
            var tail = g.Path.Resample(g.S - TailMetres, g.S, 0.25f);
            var nodes = new List<RouteNode> { r.Plain[g.Branch] };
            if (trip != null)
            {
                nodes.AddRange(trip.Nodes);
            }

            int next = NextBranch(r, g.Branch);
            for (int i = (g.Branch + 1) % n; ; i = (i + 1) % n)
            {
                nodes.Add(r.Plain[i]);
                if (i == next)
                {
                    break;
                }
            }

            nodes.Add(r.Plain[(next + 1) % n]);
            int lead = tail.Count;
            // The current point is the tail's last; the branch follows it.
            g.Path = CrowdPath.Build(tail, nodes);
            g.S = Closest(g.Path, tail[lead - 1].X, tail[lead - 1].Z, TailMetres + 0.6f);
            g.MarkIndex = FirstMarkAfter(g.Path, g.S - 1e-3f);
            g.Branch = next;
            g.DecideS = g.Path.NodeS[nodes.Count - 2] - Lookahead;
            g.Decided = false;
        }

        /// <summary>A few metres before a branch: take its trip, wait for the door, or walk on.</summary>
        private void Decide(Group g, CrowdInputs inputs)
        {
            g.Decided = true;
            var trip = g.Route.DetourAt(g.Branch);
            if (g.Branch == 0)
            {
                g.Laps++;
            }

            if (trip == null)
            {
                Rebuild(g, null);
                return;
            }

            if (trip.Goal == RouteGoal.PipPorch)
            {
                g.PorchLap = inputs.PorchOpen;
                Rebuild(g, inputs.PorchOpen ? trip : null);
                return;
            }

            if (Doors.TryHold(trip.DoorId, g.Index))
            {
                g.Door = trip.DoorId;
                Rebuild(g, trip);
                return;
            }

            if (Doors.TryQueue(trip.DoorId, g.Index))
            {
                // Wait its turn here, a few metres back on the sidewalk.
                g.QueuedDoor = trip.DoorId;
                BeginStop(g, g.S + Travel(WalkerClip.WalkStop), Mode.Queued);
                return;
            }

            // Someone is already at the door and another family waits: walk on to the next door.
            Rebuild(g, null);
        }

        // ----------------------------------------------------------------------------------------------- step

        private void Step(Group g, float dt, CrowdInputs inputs, int cap, List<CrowdEvent> events)
        {
            if (g.ReleaseDoor != null && Doors.Phase(g.ReleaseDoor) == DoorPhase.Closed)
            {
                Doors.Release(g.ReleaseDoor, g.Index);
                g.ReleaseDoor = null;
            }

            switch (g.Mode)
            {
                case Mode.Walking:
                    Walk(g, dt, inputs, cap);
                    break;
                case Mode.Stopping:
                case Mode.Starting:
                {
                    bool stopping = g.Mode == Mode.Stopping;
                    var clip = stopping ? WalkerClip.WalkStop : WalkerClip.WalkStart;
                    float length = WalkerClips.Seconds(clip);
                    g.Timer = Math.Min(length, g.Timer + dt);
                    float u = g.Timer / length;
                    float travel = Travel(clip);
                    float f = travel > 0f ? CrowdClipTimings.RootTravel(stopping ? CrowdClipTimings.WalkStop : CrowdClipTimings.WalkStart,
                        CrowdKit.ChildModel, u) / travel : 1f;
                    float before = g.S;
                    g.S = g.MoveFrom + (g.MoveTo - g.MoveFrom) * f;
                    g.Walked += Math.Max(0f, g.S - before);
                    g.SpeedNow = (g.S - before) / dt;
                    if (g.Timer >= length - 1e-6f)
                    {
                        g.S = g.MoveTo;
                        g.SpeedNow = stopping ? 0f : g.Route.Speed;
                        if (stopping)
                        {
                            Arrive(g, inputs, events);
                        }
                        else
                        {
                            g.Mode = Mode.Walking;
                        }
                    }

                    break;
                }
                case Mode.AtGate:
                    g.SpeedNow = 0f;
                    if (HeartCommittedExcept(g) + g.Route.Size <= cap)
                    {
                        g.InHeart = true;
                        g.MarkIndex++;
                        BeginStart(g);
                    }

                    break;
                case Mode.Queued:
                    g.SpeedNow = 0f;
                    if (Doors.TryHold(g.QueuedDoor, g.Index))
                    {
                        g.Door = g.QueuedDoor;
                        g.QueuedDoor = null;
                        Rebuild(g, g.Route.DetourAt(g.Branch));
                        BeginStart(g);
                    }
                    else if (Doors.Queued(g.QueuedDoor) != g.Index)
                    {
                        g.QueuedDoor = null;
                        Rebuild(g, null);
                        BeginStart(g);
                    }

                    break;
                case Mode.Gathering:
                    if (StepStrides(g, dt))
                    {
                        if (g.StopNode.Kind == RouteNodeKind.Door)
                        {
                            g.Mode = Mode.Ringing;
                            g.RingT = 0f;
                        }
                        else
                        {
                            g.Mode = Mode.AtPorch;
                            g.Timer = 0f;
                            g.Served = false;
                            events?.Add(new CrowdEvent(CrowdEventKind.PorchRung, g.Index, null));
                        }
                    }

                    break;
                case Mode.Ringing:
                {
                    g.RingT += dt;
                    StandAll(g, dt);
                    if (g.RingT >= RingPause && Doors.CanRing(g.Door, Ringer.Group, g.Index))
                    {
                        var answer = Doors.Ring(g.Door, Ringer.Group, 0, Math.Max(1, g.Children), g.Index, _doorEvents);
                        if (answer != null)
                        {
                            g.RingT = 0f;
                            events?.Add(new CrowdEvent(CrowdEventKind.DoorRung, g.Index, g.Door, -1, answer.Ring));
                            if (answer.Opens)
                            {
                                g.Mode = Mode.AtDoor;
                                foreach (var m in g.Members)
                                {
                                    m.Treat = m.Child ? Treat.Waiting : Treat.Done;
                                }
                            }
                        }
                    }

                    break;
                }
                case Mode.AtDoor:
                    StepDoorTreats(g, dt);
                    break;
                case Mode.AtPorch:
                {
                    StandAll(g, dt);
                    g.Timer += dt;
                    bool near = inputs.PipOutside && Near(inputs.PipX, inputs.PipZ, LivingStreetLayout.PorchStandX,
                        LivingStreetLayout.PorchStandZ, LivingStreetLayout.PorchNear);
                    if (g.Timer >= PorchBaseWait && (!near || g.Timer >= PorchMaxWait))
                    {
                        events?.Add(new CrowdEvent(CrowdEventKind.PorchLeft, g.Index, null));
                        BeginDepart(g);
                    }

                    break;
                }
                case Mode.PorchTreats:
                    StepPorchTreats(g, dt, inputs, events);
                    break;
                case Mode.Looking:
                    StepLooking(g, dt);
                    break;
                case Mode.Departing:
                    if (StepStrides(g, dt))
                    {
                        g.S = g.MoveTo;
                        g.MarkIndex = FirstMarkAfter(g.Path, g.S);
                        if (g.Door != null)
                        {
                            g.ReleaseDoor = g.Door;
                            g.Door = null;
                        }

                        BeginStart(g);
                    }

                    break;
            }
        }

        private void Walk(Group g, float dt, CrowdInputs inputs, int cap)
        {
            float factor = YieldFactor(g, inputs);
            UpdateSidestep(g, inputs, dt);
            float speed = g.Route.Speed * factor;
            g.SpeedNow = speed;
            float stop = Travel(WalkerClip.WalkStop);
            float move = speed * dt;

            // The next mark: a gate to check, an exit, a stop to slow into.
            while (g.MarkIndex < g.Path.Marks.Count)
            {
                var mark = g.Path.Marks[g.MarkIndex];
                var kind = mark.Node.Kind;
                if (kind == RouteNodeKind.HeartExit)
                {
                    if (g.S + move >= mark.S)
                    {
                        g.InHeart = false;
                        g.MarkIndex++;
                        continue;
                    }

                    break;
                }

                if (kind == RouteNodeKind.HeartGate)
                {
                    if (g.S + move + stop < mark.S)
                    {
                        break;
                    }

                    if (g.InHeart || HeartCommittedExcept(g) + g.Route.Size <= cap)
                    {
                        g.InHeart = true;
                        g.MarkIndex++;
                        continue;
                    }

                    // Full: slow to a stop at the heart's edge and wait for room.
                    BeginStop(g, Math.Max(g.S, mark.S - 0.01f), Mode.AtGate);
                    return;
                }

                // A stop (a door's gathering point, Pip's porch, a decoration).
                if (g.S + move + stop >= mark.S)
                {
                    g.StopNode = mark.Node;
                    g.StopS = mark.S;
                    g.MarkIndex++;
                    BeginStop(g, Math.Max(g.S, mark.S), StopMode(mark.Node));
                    return;
                }

                break;
            }

            if (!g.Decided && g.S + move >= g.DecideS)
            {
                g.S = Math.Max(g.S, Math.Min(g.DecideS, g.S + move));
                Decide(g, inputs);
                if (g.Mode != Mode.Walking)
                {
                    return;
                }

                return;
            }

            if (speed <= 1e-4f)
            {
                return;
            }

            g.S += move;
            g.Walked += move;
            if (g.S >= g.Path.Length - 0.05f && !g.Decided)
            {
                Decide(g, inputs);
            }
        }

        private static Mode StopMode(RouteNode node) =>
            node.Kind == RouteNodeKind.Look ? Mode.Looking : Mode.Gathering;

        private static float Travel(WalkerClip clip) =>
            CrowdClipTimings.WalkTransitionDistance(clip == WalkerClip.WalkStop ? CrowdClipTimings.WalkStop : CrowdClipTimings.WalkStart,
                CrowdKit.ChildModel);

        private static void BeginStop(Group g, float at, Mode after)
        {
            g.Mode = Mode.Stopping;
            g.AfterStop = after;
            g.Timer = 0f;
            g.MoveFrom = g.S;
            g.MoveTo = Math.Max(g.S, at);
        }

        private static void BeginStart(Group g)
        {
            g.Mode = Mode.Starting;
            g.Timer = 0f;
            g.MoveFrom = g.S;
            g.MoveTo = Math.Min(g.Path.Length, g.S + Travel(WalkerClip.WalkStart));
            g.StopNode = default;
        }

        /// <summary>The group has come to a stand: gather for a door or Pip's porch, admire, or wait.</summary>
        private void Arrive(Group g, CrowdInputs inputs, List<CrowdEvent> events)
        {
            g.SpeedNow = 0f;
            g.Mode = g.AfterStop;
            g.Timer = 0f;
            CaptureFormation(g);
            switch (g.Mode)
            {
                case Mode.Gathering:
                    if (g.StopNode.Kind == RouteNodeKind.PipPorch && !inputs.PorchOpen)
                    {
                        // The porch closed on the way (a hint now points there): turn back without ringing.
                        BeginDepart(g);
                        return;
                    }

                    GatherSlots(g);
                    break;
                case Mode.Looking:
                    foreach (var m in g.Members)
                    {
                        StrideTo(m, m.X, m.Z, g.StopNode.FaceYaw);
                    }

                    break;
            }
        }

        /// <summary>Each member's pose now becomes its own (it leaves the formation's line).</summary>
        private void CaptureFormation(Group g)
        {
            int first = FirstWalker(g.Index);
            for (int i = 0; i < g.Members.Length; i++)
            {
                var m = g.Members[i];
                var p = _poses[first + i];
                m.X = p.X - _offX[first + i];
                m.Z = p.Z - _offZ[first + i];
                m.Y = 0f;
                m.Yaw = p.Yaw;
                m.Speed = 0f;
                m.Striding = false;
                m.Clip = WalkerClip.Locomotion;
                m.ClipTime = 0f;
                m.Turning = false;
            }
        }

        private void GatherSlots(Group g)
        {
            bool door = g.StopNode.Kind == RouteNodeKind.Door;
            var nd = door ? LivingStreetLayout.Door(g.Door ?? g.StopNode.DoorId) : null;
            int adults = 0;
            foreach (var m in g.Members)
            {
                float x, z, yaw;
                if (m.Child)
                {
                    if (door)
                    {
                        DoorStoops.WaitSlot(nd, m.ChildIndex, g.Children, out x, out z, out yaw);
                    }
                    else
                    {
                        PorchSlot(m.ChildIndex, g.Children, out x, out z, out yaw);
                    }
                }
                else if (door)
                {
                    DoorStoops.GrownUpSlot(nd, out x, out z, out yaw);
                    x += adults * 0.6f;
                    adults++;
                }
                else
                {
                    x = LivingStreetLayout.PorchStandX + 0.55f + adults * 0.6f;
                    z = LivingStreetLayout.PorchStandZ + 1.25f;
                    yaw = LivingStreetLayout.PorchStandYaw;
                    adults++;
                }

                m.SlotX = x;
                m.SlotZ = z;
                m.SlotYaw = yaw;
                StrideTo(m, x, z, yaw);
            }
        }

        /// <summary>Where child <paramref name="index"/> waits at the foot of Pip's steps (a short row facing her porch).</summary>
        public static void PorchSlot(int index, int count, out float x, out float z, out float yaw)
        {
            x = LivingStreetLayout.PorchStandX + (index - (count - 1) * 0.5f) * 0.7f;
            z = LivingStreetLayout.PorchStandZ;
            yaw = LivingStreetLayout.PorchStandYaw;
        }

        /// <summary>
        /// Where a child receives from Pip: a hand-out distance in front of her, toward the children, on the path in front of
        /// her steps (never on the steps, never near a clue).
        /// </summary>
        public static void PorchReceive(float pipX, float pipZ, out float x, out float z, out float yaw)
        {
            float dx = LivingStreetLayout.PorchStandX - pipX;
            float dz = LivingStreetLayout.PorchStandZ - pipZ;
            float d = (float)Math.Sqrt(dx * dx + dz * dz);
            if (d < 1e-3f)
            {
                dx = 0f;
                dz = 1f;
                d = 1f;
            }

            x = pipX + dx / d * WalkerClips.PipReceiveDistance;
            z = pipZ + dz / d * WalkerClips.PipReceiveDistance;
            // Keep it on the path: within reach of the stand, in front of the steps.
            float ox = x - LivingStreetLayout.PorchStandX;
            float oz = z - LivingStreetLayout.PorchStandZ;
            float o = (float)Math.Sqrt(ox * ox + oz * oz);
            if (o > PorchReceiveRadius)
            {
                x = LivingStreetLayout.PorchStandX + ox / o * PorchReceiveRadius;
                z = LivingStreetLayout.PorchStandZ + oz / o * PorchReceiveRadius;
            }

            z = Math.Max(z, PorchReceiveMinZ);
            yaw = Stride.YawTo(x, z, pipX, pipZ);
        }

        /// <summary>A child receiving at Pip's porch stays within this of the group's stand, and north of the steps' foot.</summary>
        public const float PorchReceiveRadius = 1.2f;

        public const float PorchReceiveMinZ = -12.3f;

        private static void StrideTo(Member m, float x, float z, float yaw)
        {
            m.Stride = new Stride(m.X, m.Z, m.Yaw, x, z, yaw, m.Model, m.Y, 0f);
            m.StrideT = 0f;
            m.Striding = true;
        }

        /// <summary>Moves every striding member; true once none is.</summary>
        private static bool StepStrides(Group g, float dt)
        {
            bool all = true;
            foreach (var m in g.Members)
            {
                all &= !StepMember(m, dt);
            }

            return all;
        }

        /// <summary>One member's stride; true while it is still walking.</summary>
        private static bool StepMember(Member m, float dt)
        {
            if (!m.Striding)
            {
                m.Speed = 0f;
                m.Turning = false;
                if (m.Clip == WalkerClip.WalkStop || m.Clip == WalkerClip.WalkStart)
                {
                    m.Clip = WalkerClip.Locomotion;
                }

                return false;
            }

            m.StrideT += dt;
            m.Stride.Sample(m.StrideT, out m.X, out m.Z, out m.Y, out m.Yaw, out m.Speed, out m.Clip, out m.ClipTime);
            m.Turning = m.Stride.Turning && m.StrideT < m.Stride.Duration;
            if (m.StrideT >= m.Stride.Duration)
            {
                m.Striding = false;
                m.Speed = 0f;
                m.Turning = false;
                m.Clip = WalkerClip.Locomotion;
                return false;
            }

            return true;
        }

        private static void StandAll(Group g, float dt)
        {
            foreach (var m in g.Members)
            {
                StepMember(m, dt);
            }
        }

        private void StepDoorTreats(Group g, float dt)
        {
            string door = g.Door;
            int turn = Doors.Turn(door);
            foreach (var m in g.Members)
            {
                if (!m.Child)
                {
                    StepMember(m, dt);
                    continue;
                }

                switch (m.Treat)
                {
                    case Treat.Waiting:
                        StepMember(m, dt);
                        if (m.ChildIndex == turn && Doors.WaitingForRecipient(door) && Ready(PreviousChild(g, m)))
                        {
                            Doors.ReceiveSpot(door, out float rx, out float rz, out float ryaw);
                            StrideTo(m, rx, rz, ryaw);
                            m.Treat = Treat.Approaching;
                        }

                        break;
                    case Treat.Approaching:
                        if (!StepMember(m, dt))
                        {
                            Doors.RecipientReady(door);
                            m.Treat = Treat.Holding;
                            m.TreatT = 0f;
                        }

                        break;
                    default:
                        StepTreat(m, dt);
                        break;
                }
            }

            bool over = Doors.VisitOver(door);
            bool settled = true;
            foreach (var m in g.Members)
            {
                settled &= !m.Striding && (m.Treat == Treat.Done || (m.Treat == Treat.Waiting && over));
            }

            if (settled && over)
            {
                BeginDepart(g);
            }
        }

        /// <summary>The child whose turn came just before <paramref name="m"/>'s, or null for the first.</summary>
        private static Member PreviousChild(Group g, Member m)
        {
            foreach (var other in g.Members)
            {
                if (other.Child && other.ChildIndex == m.ChildIndex - 1)
                {
                    return other;
                }
            }

            return null;
        }

        /// <summary>The previous child has turned back to its place (or there was none): the next one may step up.</summary>
        private static bool Ready(Member previous) =>
            previous == null || previous.Treat == Treat.Done || (previous.Treat == Treat.Returning && previous.StrideT >= NextChildDelay);

        /// <summary>Hold the pail for the sweet, cheer, step back to its place.</summary>
        private void StepTreat(Member m, float dt)
        {
            switch (m.Treat)
            {
                case Treat.Holding:
                {
                    m.TreatT += dt;
                    m.Speed = 0f;
                    m.Clip = WalkerClip.HoldPail;
                    m.ClipTime = NeighbourDoors.PailLead + m.TreatT;
                    if (m.ClipTime >= WalkerClips.Seconds(WalkerClip.HoldPail))
                    {
                        m.Treat = Treat.Cheering;
                        m.TreatT = 0f;
                    }

                    break;
                }
                case Treat.Cheering:
                    m.TreatT += dt;
                    m.Clip = WalkerClip.Cheer;
                    m.ClipTime = m.TreatT;
                    if (m.TreatT >= CheerSeconds)
                    {
                        m.Clip = WalkerClip.Locomotion;
                        m.Treat = Treat.Returning;
                        StrideTo(m, m.SlotX, m.SlotZ, m.SlotYaw);
                    }

                    break;
                case Treat.Returning:
                    if (!StepMember(m, dt))
                    {
                        m.Treat = Treat.Done;
                    }

                    break;
                default:
                    StepMember(m, dt);
                    break;
            }
        }

        private void StepPorchTreats(Group g, float dt, CrowdInputs inputs, List<CrowdEvent> events)
        {
            g.Timer += dt;
            bool pipThere = inputs.PipOutside && LivingStreetLayout.TreatInReach(inputs.PipX, inputs.PipZ);
            bool pipGone = !inputs.PipOutside || !Near(inputs.PipX, inputs.PipZ, LivingStreetLayout.PorchStandX,
                LivingStreetLayout.PorchStandZ, LivingStreetLayout.PorchNear);
            int first = FirstWalker(g.Index);
            var next = FirstWaiting(g);
            for (int i = 0; i < g.Members.Length; i++)
            {
                var m = g.Members[i];
                if (!m.Child)
                {
                    StepMember(m, dt);
                    continue;
                }

                switch (m.Treat)
                {
                    case Treat.Waiting:
                        StepMember(m, dt);
                        if (pipGone)
                        {
                            m.Treat = Treat.Done; // she went on her way; nothing is lost
                        }
                        else if (pipThere && m == next && Ready(PreviousChild(g, m)))
                        {
                            PorchReceive(inputs.PipX, inputs.PipZ, out float rx, out float rz, out float ryaw);
                            StrideTo(m, rx, rz, ryaw);
                            m.Treat = Treat.Approaching;
                        }

                        break;
                    case Treat.Approaching:
                        if (!StepMember(m, dt))
                        {
                            m.Treat = Treat.Holding;
                            m.TreatT = 0f;
                            events?.Add(new CrowdEvent(CrowdEventKind.PipHandTreat, g.Index, null, first + i));
                        }

                        break;
                    case Treat.Holding:
                    {
                        float before = m.TreatT;
                        StepTreat(m, dt);
                        float release = WalkerClips.HandRelease;
                        if (before < release && (m.TreatT >= release || m.Treat != Treat.Holding))
                        {
                            events?.Add(new CrowdEvent(CrowdEventKind.PipTreatReleased, g.Index, null, first + i));
                        }

                        break;
                    }
                    default:
                        StepTreat(m, dt);
                        break;
                }
            }

            bool done = true;
            foreach (var m in g.Members)
            {
                done &= !m.Striding && (!m.Child || m.Treat == Treat.Done);
            }

            if (done)
            {
                events?.Add(new CrowdEvent(CrowdEventKind.PorchLeft, g.Index, null));
                BeginDepart(g);
            }
        }

        private static Member FirstWaiting(Group g)
        {
            Member best = null;
            foreach (var m in g.Members)
            {
                if (m.Child && m.Treat == Treat.Waiting && (best == null || m.ChildIndex < best.ChildIndex))
                {
                    best = m;
                }
            }

            return best;
        }

        private void StepLooking(Group g, float dt)
        {
            g.Timer += dt;
            bool pointed = false;
            foreach (var m in g.Members)
            {
                if (StepMember(m, dt))
                {
                    continue;
                }

                if (!m.Child)
                {
                    // The grown-up chats about the decoration.
                    m.Clip = WalkerClip.Talk;
                    m.ClipTime = g.Timer;
                }
                else if (!pointed && g.Timer >= PointAt)
                {
                    // One child points at it (the kit's wave, once).
                    pointed = true;
                    m.Clip = WalkerClip.Wave;
                    m.ClipTime = g.Timer - PointAt;
                }
            }

            if (g.Timer >= Math.Max(g.StopNode.Seconds, 1f))
            {
                BeginDepart(g);
            }
        }

        /// <summary>The visit is over: the family turns together and walks to its places on the way on.</summary>
        private void BeginDepart(Group g)
        {
            float back = 0f;
            foreach (var m in g.Members)
            {
                back = Math.Max(back, m.Slot.Back);
            }

            bool look = g.StopNode.Kind == RouteNodeKind.Look;
            float sDep = Math.Min(g.Path.Length, g.StopS + (look ? 0.45f : back + 0.6f));
            foreach (var m in g.Members)
            {
                Formation(g, m, sDep, 0f, out float x, out float z, out float yaw);
                m.Clip = WalkerClip.Locomotion;
                StrideTo(m, x, z, yaw);
            }

            g.MoveTo = sDep;
            g.Sidestep = 0f;
            g.SidestepSign = 0f;
            g.Mode = Mode.Departing;
            g.Timer = 0f;
        }

        private static void Formation(Group g, Member m, float s, float sidestep, out float x, out float z, out float yaw)
        {
            g.Path.At(s - m.Slot.Back, out float px, out float pz, out float hx, out float hz);
            float lateral = m.Slot.Lateral + sidestep;
            x = px + hz * lateral;
            z = pz - hx * lateral;
            yaw = (float)(Math.Atan2(hx, hz) * 180.0 / Math.PI);
        }

        // ------------------------------------------------------------------------------------------ yield to Pip

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
        private float YieldFactor(Group g, CrowdInputs inputs)
        {
            if (!inputs.PipOutside)
            {
                return 1f;
            }

            float factor = 1f;
            int first = FirstWalker(g.Index);
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

        private void UpdateSidestep(Group g, CrowdInputs inputs, float dt)
        {
            float target = 0f;
            if (inputs.PipOutside)
            {
                int first = FirstWalker(g.Index);
                for (int m = 0; m < g.Route.Size; m++)
                {
                    var pose = _poses[first + m];
                    if (!Near(inputs.PipX, inputs.PipZ, pose.X, pose.Z, SidestepRadius))
                    {
                        continue;
                    }

                    if (g.SidestepSign == 0f)
                    {
                        // Step to the side away from Pip (the walker's right is +lateral), unless that side is a hedge,
                        // a fence or a wall: then the other side, or less far (planned ahead, never a bump).
                        double yaw = pose.Yaw * Math.PI / 180.0;
                        float rx = (float)Math.Cos(yaw);
                        float rz = -(float)Math.Sin(yaw);
                        float side = (inputs.PipX - pose.X) * rx + (inputs.PipZ - pose.Z) * rz;
                        float away = side > 0f ? -1f : 1f;
                        g.SidestepSign = SideRoom(g, away) >= SideRoom(g, -away) * 0.6f ? away : -away;
                    }

                    target = g.SidestepSign * Math.Min(SidestepMetres, SideRoom(g, g.SidestepSign));
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

        /// <summary>How far (up to the full step) every member can step to <paramref name="sign"/> and keep clear of obstacles.</summary>
        private float SideRoom(Group g, float sign)
        {
            float best = SidestepMetres;
            foreach (var m in g.Members)
            {
                for (float step = SidestepMetres; step > 0f; step -= 0.1f)
                {
                    Formation(g, m, g.S, sign * step, out float x, out float z, out _);
                    if (StreetObstacles.Clearance(x, z) >= StreetObstacles.WalkerClearance)
                    {
                        best = Math.Min(best, step);
                        break;
                    }

                    if (step <= 0.11f)
                    {
                        best = 0f;
                    }
                }
            }

            return best;
        }

        // ---------------------------------------------------------------------------------------------- poses

        private void UpdatePoses(CrowdInputs inputs, float dt)
        {
            int w = 0;
            for (int i = 0; i < _groups.Count; i++)
            {
                var g = _groups[i];
                bool formation = g.Mode == Mode.Walking || g.Mode == Mode.Stopping || g.Mode == Mode.Starting ||
                                 g.Mode == Mode.AtGate || g.Mode == Mode.Queued;
                foreach (var m in g.Members)
                {
                    var pose = new WalkerPose();
                    if (formation)
                    {
                        Formation(g, m, g.S, g.Sidestep, out pose.X, out pose.Z, out pose.Yaw);
                        pose.Speed = g.SpeedNow;
                        pose.Clip = g.Mode == Mode.Stopping ? WalkerClip.WalkStop : g.Mode == Mode.Starting ? WalkerClip.WalkStart : WalkerClip.Locomotion;
                        pose.ClipTime = g.Timer;
                        pose.Gesture = g.Mode == Mode.AtGate || g.Mode == Mode.Queued ? WalkerGesture.Wait : WalkerGesture.None;
                    }
                    else
                    {
                        pose.X = m.X;
                        pose.Z = m.Z;
                        pose.Y = m.Y;
                        pose.Yaw = m.Yaw;
                        pose.Speed = m.Speed;
                        pose.Clip = m.Clip;
                        pose.ClipTime = m.ClipTime;
                        pose.Turning = m.Turning;
                        pose.Gesture = m.Treat == Treat.Holding ? WalkerGesture.HoldOut
                            : m.Treat == Treat.Cheering ? WalkerGesture.Cheer
                            : g.Mode == Mode.Looking ? WalkerGesture.Look
                            : WalkerGesture.None;
                    }

                    // A child stepping up for its treat (to Pip or a neighbour) is busy with the giver: it keeps only its body
                    // clear of Pip, not the room a standing walker leaves her.
                    bool engaged = (g.Mode == Mode.AtDoor || g.Mode == Mode.PorchTreats) &&
                                   (m.Treat == Treat.Approaching || m.Treat == Treat.Holding || m.Treat == Treat.Cheering);
                    bool still = !formation && !m.Striding && !engaged;
                    _giveWay[w] = new GiveWay.Walker { X = pose.X, Z = pose.Z, Still = still || g.Mode == Mode.AtGate || g.Mode == Mode.Queued, GrownUp = !m.Child };
                    _poses[w++] = pose;
                }
            }

            if (dt <= 0f)
            {
                return;
            }

            // Make room: standing walkers keep their distance from Pip and part for her, nobody overlaps anybody.
            float hl = (float)Math.Sqrt(inputs.PipHeadingX * inputs.PipHeadingX + inputs.PipHeadingZ * inputs.PipHeadingZ);
            GiveWay.Solve(_giveWay, _offX, _offZ, inputs.PipX, inputs.PipZ, inputs.PipHeadingX, inputs.PipHeadingZ,
                inputs.PipWalking && hl > 1e-3f, inputs.PipOutside, dt, _clearance);
            for (int i = 0; i < _poses.Length; i++)
            {
                var p = _poses[i];
                float bx = p.X, bz = p.Z;
                p.X += _offX[i];
                p.Z += _offZ[i];
                float moved = (float)Math.Sqrt((p.X - _previous[i].X) * (p.X - _previous[i].X) + (p.Z - _previous[i].Z) * (p.Z - _previous[i].Z)) / dt;
                if (p.Speed < 0.05f && p.Clip == WalkerClip.Locomotion && moved > 0.15f && (bx != p.X || bz != p.Z))
                {
                    // Stepping aside: the legs walk it (never a slide).
                    p.Speed = Math.Min(moved, 1.2f);
                }

                _poses[i] = p;
            }
        }

        // ---------------------------------------------------------------------------------------------- helpers

        private static float Closest(CrowdPath path, float x, float z, float maxS)
        {
            float best = float.MaxValue;
            float at = 0f;
            float end = Math.Min(path.Length, maxS);
            for (float s = 0f; s <= end; s += 0.05f)
            {
                path.At(s, out float px, out float pz, out _, out _);
                float d = (px - x) * (px - x) + (pz - z) * (pz - z);
                if (d < best)
                {
                    best = d;
                    at = s;
                }
            }

            return at;
        }

        private static int FirstMarkAfter(CrowdPath path, float s)
        {
            int i = 0;
            while (i < path.Marks.Count && path.Marks[i].S <= s + 1e-4f)
            {
                i++;
            }

            return i;
        }

        private static float Dist(RouteNode a, RouteNode b)
        {
            float dx = b.X - a.X;
            float dz = b.Z - a.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
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
