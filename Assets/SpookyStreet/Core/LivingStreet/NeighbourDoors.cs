using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.LivingStreet
{
    /// <summary>Who rang a neighbour's bell.</summary>
    public enum Ringer
    {
        Pip,
        Group,
    }

    /// <summary>Where a neighbour's door is in its little routine.</summary>
    public enum DoorPhase
    {
        /// <summary>Nobody has rung (or the visit is over).</summary>
        Closed,

        /// <summary>Rung once or twice: the hall light is on, someone is coming; ring again.</summary>
        Called,

        /// <summary>Rung the third time: the neighbour's steps reach the door.</summary>
        Answering,

        /// <summary>The neighbour reaches for the door and it eases open (OpenDoorPeek).</summary>
        Opening,

        /// <summary>The neighbour walks out and down the step to the treat spot.</summary>
        ComingOut,

        /// <summary>A treat for each child in turn (or for Pip).</summary>
        Giving,

        /// <summary>The neighbour walks back in.</summary>
        GoingIn,

        /// <summary>The door eases shut.</summary>
        Closing,
    }

    public enum DoorEventKind
    {
        /// <summary>A ring was accepted (<see cref="DoorEvent.Ring"/> 1, 2 or 3).</summary>
        Rang,

        /// <summary>The second ring: the neighbour calls that they are coming (a caption) and crosses the window.</summary>
        Coming,

        /// <summary>The hand is on the door and the leaf starts to swing.</summary>
        Opened,

        /// <summary>The neighbour stands in the doorway: their line (for Pip) plays now.</summary>
        Arrived,

        /// <summary>A treat is let go over a pail (<see cref="DoorEvent.Recipient"/>: the child's turn, or -1 for Pip).</summary>
        Released,

        /// <summary>Every treat of the visit is given; the neighbour goes back in.</summary>
        Done,

        /// <summary>The door is shut again.</summary>
        Shut,
    }

    public readonly struct DoorEvent
    {
        public DoorEvent(DoorEventKind kind, string doorId, Ringer ringer, int group, int ring, int recipient)
        {
            Kind = kind;
            DoorId = doorId;
            Ringer = ringer;
            Group = group;
            Ring = ring;
            Recipient = recipient;
        }

        public DoorEventKind Kind { get; }
        public string DoorId { get; }
        public Ringer Ringer { get; }

        /// <summary>The ringing group (-1 for Pip).</summary>
        public int Group { get; }

        public int Ring { get; }

        /// <summary>For <see cref="DoorEventKind.Released"/>: the child's turn (0, 1, …) or -1 for Pip.</summary>
        public int Recipient { get; }

        public override string ToString() => Kind + " " + DoorId + " ring " + Ring + " to " + Recipient;
    }

    /// <summary>What a ring brought: the door that answers, and (for Pip) the line and the visit it counts.</summary>
    public sealed class DoorAnswer
    {
        internal DoorAnswer(NeighbourDoor door, Ringer ringer, string lineKey, int visitsBefore, int ring)
        {
            Door = door;
            Ringer = ringer;
            LineKey = lineKey;
            VisitsBefore = visitsBefore;
            Ring = ring;
        }

        public NeighbourDoor Door { get; }
        public Ringer Ringer { get; }

        /// <summary>The neighbour's line to Pip, shown once they stand in the doorway (null for a group).</summary>
        public string LineKey { get; }

        /// <summary>Pip's earlier visits when she rang (0 on the first).</summary>
        public int VisitsBefore { get; }

        /// <summary>Which ring this was (1, 2 or 3).</summary>
        public int Ring { get; }

        /// <summary>The third ring: the door will open.</summary>
        public bool Opens => Ring >= NeighbourDoors.RingsToOpen;

        /// <summary>A treat goes to Pip (into her pail; never a number).</summary>
        public bool TreatForPip => Ringer == Ringer.Pip;
    }

    /// <summary>
    /// The three trick-or-treat doors' routine (living-street §4.1; owner, 2026-11-28: "tocar uma, duas, três, na terceira
    /// a pessoa abre"), pure and frame-rate independent:
    /// <list type="number">
    /// <item>Each press rings the bell. The door opens on the third ring (<see cref="RingsToOpen"/>); between rings the
    /// house shows someone is coming, so a young child knows to ring again: the first ring turns the hall light on
    /// (<see cref="HallLight"/>), the second sends the neighbour's shadow across the lit window
    /// (<see cref="Silhouette"/>) with a "Coming!" caption. A ring is taken once the last one has sounded
    /// (<see cref="RingBeat"/>). Nothing is timed against the child: a door rung once waits as long as it takes, and goes
    /// quiet only when the ringer walks away (<see cref="Abandon"/>).</item>
    /// <item>The neighbour's steps reach the door, they reach for its edge (OpenDoorPeek) and the leaf eases open on a
    /// smooth hinge curve with the house's own gag (Mr. Fogg's springy fling, Nana Pearl's slow ease and warm lamp, the
    /// Abbotts popping into the doorway together).</item>
    /// <item>The neighbour walks out and down the step to the treat spot (<see cref="DoorStoops.TreatOut"/>; toward Pip
    /// when she rang) and hands one treat to each child in turn with GiveTreat: a turn waits until the child stands at
    /// the receive spot (<see cref="RecipientReady"/>), and the sweet is let go over the pail at the clip's release.
    /// Then the neighbour walks back in and the door eases shut.</item>
    /// </list>
    /// A door is held by one visitor at a time (Pip or a group, <see cref="TryHold"/>); one more group may wait its turn a
    /// few metres back (<see cref="TryQueue"/>), any other walks on. Bells, never knocks (§3.4 rule 5).
    /// </summary>
    public sealed class NeighbourDoors
    {
        public const int RingsToOpen = 3;

        /// <summary>A new ring is taken this long after the last one (the ding-dong has sounded and the house answered).</summary>
        public const float RingBeat = 1.7f;

        /// <summary>After the third ring the neighbour's steps reach the door in this long.</summary>
        public const float AnswerSeconds = 0.55f;

        /// <summary>The hall light comes up (and goes down) over this long once switched.</summary>
        public const float HallLightFade = 0.25f;

        /// <summary>The hall light goes off this long after the door is shut.</summary>
        public const float HallLightAfter = 0.8f;

        /// <summary>The shadow starts across the window this long after the second ring and takes <see cref="SilhouetteSeconds"/>.</summary>
        public const float SilhouetteDelay = 0.25f;

        public const float SilhouetteSeconds = 1.9f;

        /// <summary>The door eases shut in this long.</summary>
        public const float CloseSeconds = 0.95f;

        /// <summary>Pip receives once she is this close to the neighbour at the treat spot.</summary>
        public const float PipReach = 1.35f;

        /// <summary>A ringer this far from the door has gone: the house settles (the light goes off) without a word.</summary>
        public const float AbandonDistance = 9f;

        /// <summary>A waiting turn ends without a treat when its child never comes (a safety net, never reached in play).</summary>
        public const float TurnGiveUp = 40f;

        /// <summary>The neighbour turns toward Pip at this rate while waiting (degrees per second).</summary>
        public const float FaceRate = 140f;

        /// <summary>The neighbour may turn this far from facing the street toward Pip.</summary>
        public const float FaceLimit = 70f;

        /// <summary>For older callers and tests: the neighbour stands with the treats at least this long.</summary>
        public const float HoldSeconds = 1.6f;

        private sealed class FigureState
        {
            public float X, Z, Y, Yaw, Speed, ClipTime;
            public WalkerClip Clip;
            public bool Shown, Turning;
            public Stride Stride;
            public float StrideT;
            public bool Striding;
        }

        private sealed class Slot
        {
            public NeighbourDoor Door;
            public DoorPhase Phase;
            public float Time;
            public int Rings;
            public float SinceRing;
            public float HallLight;
            public bool HallOn;
            public float HallOffIn = -1f;
            public float SilhouetteT = -1f;
            public bool Holding;
            public Ringer Ringer;
            public int Group = -1;
            public int Queued = -1;
            public int Recipients;
            public int Turn;
            public bool TurnGiving;
            public float TurnTime;
            public bool Released;
            public string LineKey;
            public int Visits;
            public bool ArrivedSent;
            public bool OpenedSent;
            public FigureState[] Figures;
        }

        private readonly Dictionary<string, Slot> _slots = new Dictionary<string, Slot>(StringComparer.Ordinal);
        private readonly List<DoorEvent> _scratch = new List<DoorEvent>();

        public NeighbourDoors()
        {
            foreach (var door in LivingStreetLayout.Doors)
            {
                var slot = new Slot { Door = door, Figures = new FigureState[Math.Max(1, door.Gag.Figures)] };
                for (int i = 0; i < slot.Figures.Length; i++)
                {
                    slot.Figures[i] = new FigureState();
                }

                _slots[door.Id] = slot;
                Home(slot);
            }
        }

        private Slot Get(string id) => id != null && _slots.TryGetValue(id, out var s) ? s : null;

        public DoorPhase Phase(string doorId) => Get(doorId)?.Phase ?? DoorPhase.Closed;

        /// <summary>True while the door is in a visit (rung, answering, open, closing) or held by a visitor on the way.</summary>
        public bool Busy(string doorId)
        {
            var s = Get(doorId);
            return s != null && (s.Phase != DoorPhase.Closed || s.Holding);
        }

        /// <summary>Who holds the door now (what the doorway answers).</summary>
        public Ringer LastRinger(string doorId) => Get(doorId)?.Ringer ?? Ringer.Group;

        /// <summary>The group holding the door (-1: Pip or nobody).</summary>
        public int Holder(string doorId)
        {
            var s = Get(doorId);
            return s != null && s.Holding && s.Ringer == Ringer.Group ? s.Group : -1;
        }

        /// <summary>True while Pip holds the door (she rang and the visit is not over).</summary>
        public bool HeldByPip(string doorId)
        {
            var s = Get(doorId);
            return s != null && s.Holding && s.Ringer == Ringer.Pip;
        }

        /// <summary>The group waiting its turn a few metres back (-1 none).</summary>
        public int Queued(string doorId) => Get(doorId)?.Queued ?? -1;

        public int Rings(string doorId) => Get(doorId)?.Rings ?? 0;

        /// <summary>How lit the hall is (0 off, 1 on).</summary>
        public float HallLight(string doorId) => Get(doorId)?.HallLight ?? 0f;

        /// <summary>The shadow's way across the hall window (0..1), or -1 when it is not crossing.</summary>
        public float Silhouette(string doorId)
        {
            var s = Get(doorId);
            if (s == null || s.SilhouetteT < SilhouetteDelay)
            {
                return -1f;
            }

            float p = (s.SilhouetteT - SilhouetteDelay) / SilhouetteSeconds;
            return p > 1f ? -1f : p;
        }

        /// <summary>
        /// A group asks for the door before it turns up the path: true when it now holds it (it was free, or already
        /// this group's). A group that held the queue leaves it.
        /// </summary>
        public bool TryHold(string doorId, int group)
        {
            var s = Get(doorId);
            if (s == null)
            {
                return false;
            }

            if (s.Holding)
            {
                return s.Ringer == Ringer.Group && s.Group == group;
            }

            if (s.Phase != DoorPhase.Closed || (s.Queued >= 0 && s.Queued != group))
            {
                return false;
            }

            s.Holding = true;
            s.Ringer = Ringer.Group;
            s.Group = group;
            s.Queued = s.Queued == group ? -1 : s.Queued;
            return true;
        }

        /// <summary>A group waits its turn a few metres back: true when the queue's one place is free (or already its own).</summary>
        public bool TryQueue(string doorId, int group)
        {
            var s = Get(doorId);
            if (s == null || (s.Queued >= 0 && s.Queued != group))
            {
                return false;
            }

            s.Queued = group;
            return true;
        }

        /// <summary>A group gives up its place in the queue (it walks on).</summary>
        public void Unqueue(string doorId, int group)
        {
            var s = Get(doorId);
            if (s != null && s.Queued == group)
            {
                s.Queued = -1;
            }
        }

        /// <summary>The group holding the door lets go of it (its visit is over, or it never rang).</summary>
        public void Release(string doorId, int group)
        {
            var s = Get(doorId);
            if (s != null && s.Holding && s.Ringer == Ringer.Group && s.Group == group && s.Phase == DoorPhase.Closed)
            {
                s.Holding = false;
                s.Group = -1;
            }
        }

        /// <summary>True when <paramref name="ringer"/> may ring now (the door is free or theirs, and the last ring has sounded).</summary>
        public bool CanRing(string doorId, Ringer ringer, int group = -1)
        {
            var s = Get(doorId);
            if (s == null)
            {
                return false;
            }

            bool theirs = !s.Holding || (s.Ringer == ringer && (ringer == Ringer.Pip || s.Group == group));
            if (!theirs)
            {
                return false;
            }

            if (s.Phase == DoorPhase.Closed)
            {
                // A free door: Pip never waits behind a walkers' queue (the queued group simply waits a little longer).
                return true;
            }

            return s.Phase == DoorPhase.Called && s.SinceRing >= RingBeat;
        }

        /// <summary>
        /// A ring at <paramref name="doorId"/>. Returns the answer (which ring it was, and on the third the line Pip hears),
        /// or null when the ring is not taken (another visitor's door, or too soon after the last ring).
        /// <paramref name="pipVisitsBefore"/> picks Pip's line; <paramref name="recipients"/> is how many children wait
        /// for a treat (Pip counts as one).
        /// </summary>
        public DoorAnswer Ring(string doorId, Ringer ringer, int pipVisitsBefore = 0, int recipients = 1, int group = -1,
            List<DoorEvent> events = null)
        {
            var s = Get(doorId);
            if (s == null || !CanRing(doorId, ringer, group))
            {
                return null;
            }

            if (s.Phase == DoorPhase.Closed)
            {
                s.Holding = true;
                s.Ringer = ringer;
                s.Group = ringer == Ringer.Group ? group : -1;
                s.Rings = 0;
                s.SilhouetteT = -1f;
                s.Recipients = Math.Max(1, ringer == Ringer.Pip ? 1 : recipients);
                s.Visits = Math.Max(0, pipVisitsBefore);
                s.LineKey = ringer == Ringer.Pip ? s.Door.LineFor(pipVisitsBefore) : null;
                Home(s);
            }

            s.Rings++;
            s.SinceRing = 0f;
            s.Phase = DoorPhase.Called;
            s.Time = 0f;
            s.HallOn = true;
            s.HallOffIn = -1f;
            if (s.Rings == 2)
            {
                s.SilhouetteT = 0f;
                events?.Add(new DoorEvent(DoorEventKind.Coming, doorId, s.Ringer, s.Group, 2, -1));
            }

            if (s.Rings >= RingsToOpen)
            {
                s.Phase = DoorPhase.Answering;
                s.ArrivedSent = false;
                s.OpenedSent = false;
            }

            events?.Add(new DoorEvent(DoorEventKind.Rang, doorId, s.Ringer, s.Group, s.Rings, -1));
            return new DoorAnswer(s.Door, s.Ringer, s.Rings >= RingsToOpen ? s.LineKey : null, s.Visits, s.Rings);
        }

        /// <summary>
        /// The ringer walked away before the third ring: the house settles (the shadow goes, the light goes off) and the
        /// door is free again. Nothing is lost; ringing again starts over.
        /// </summary>
        public void Abandon(string doorId)
        {
            var s = Get(doorId);
            if (s == null || s.Phase != DoorPhase.Called)
            {
                return;
            }

            s.Phase = DoorPhase.Closed;
            s.Rings = 0;
            s.Holding = false;
            s.Group = -1;
            s.SilhouetteT = -1f;
            s.HallOn = false;
        }

        /// <summary>Whose turn it is at the treat spot (0, 1, …), or -1 when the neighbour is not handing out.</summary>
        public int Turn(string doorId)
        {
            var s = Get(doorId);
            return s != null && s.Phase == DoorPhase.Giving ? s.Turn : -1;
        }

        /// <summary>True while the neighbour waits for the child whose turn it is to step up.</summary>
        public bool WaitingForRecipient(string doorId)
        {
            var s = Get(doorId);
            return s != null && s.Phase == DoorPhase.Giving && !s.TurnGiving;
        }

        /// <summary>The child whose turn it is stands at the receive spot holding out its pail: the neighbour gives.</summary>
        public bool RecipientReady(string doorId)
        {
            var s = Get(doorId);
            if (s == null || s.Phase != DoorPhase.Giving || s.TurnGiving)
            {
                return false;
            }

            s.TurnGiving = true;
            s.TurnTime = 0f;
            s.Released = false;
            return true;
        }

        /// <summary>
        /// Seconds the child should hold out its pail before the neighbour's GiveTreat starts, so the sweet lands in it at
        /// the pail's own drop: <see cref="WalkerClips.PailDrop"/> − <see cref="WalkerClips.GiveRelease"/>.
        /// </summary>
        public static float PailLead => Math.Max(0f, WalkerClips.PailDrop - WalkerClips.GiveRelease);

        /// <summary>The neighbour's receive spot right now (where a child steps up): in front of the giver.</summary>
        public void ReceiveSpot(string doorId, out float x, out float z, out float yaw)
        {
            var s = Get(doorId);
            var f = s.Figures[0];
            double r = f.Yaw * Math.PI / 180.0;
            x = f.X + (float)Math.Sin(r) * WalkerClips.ReceiveDistance;
            z = f.Z + (float)Math.Cos(r) * WalkerClips.ReceiveDistance;
            yaw = (f.Yaw + 180f) % 360f;
        }

        /// <summary>True while a neighbour stands visible in the doorway or on the steps.</summary>
        public bool NeighbourShown(string doorId)
        {
            var s = Get(doorId);
            return s != null && s.Figures[0].Shown;
        }

        /// <summary>The pose of figure <paramref name="figure"/> (0 the giver; the second Abbott is 1), world plan metres.</summary>
        public bool Figure(string doorId, int figure, out float x, out float z, out float y, out float yaw, out float speed,
            out WalkerClip clip, out float clipTime, out bool turning)
        {
            var s = Get(doorId);
            var f = s != null && figure >= 0 && figure < s.Figures.Length ? s.Figures[figure] : null;
            if (f == null)
            {
                x = z = y = yaw = speed = clipTime = 0f;
                clip = WalkerClip.Locomotion;
                turning = false;
                return false;
            }

            x = f.X;
            z = f.Z;
            y = f.Y;
            yaw = f.Yaw;
            speed = f.Speed;
            clip = f.Clip;
            clipTime = f.ClipTime;
            turning = f.Turning;
            return f.Shown;
        }

        /// <summary>The leaf's swing in degrees (0 shut, the gag's angle open): a smooth hinge curve, never a snap.</summary>
        public float SwingDegrees(string doorId)
        {
            var s = Get(doorId);
            if (s == null)
            {
                return 0f;
            }

            var gag = s.Door.Gag;
            switch (s.Phase)
            {
                case DoorPhase.Opening:
                {
                    float t = s.Time - WalkerClips.PeekHandOnDoor;
                    return t <= 0f ? 0f : gag.OpenDegrees * HingeOpen(t / gag.OpenSeconds, gag.Bounce);
                }
                case DoorPhase.ComingOut:
                case DoorPhase.Giving:
                case DoorPhase.GoingIn:
                {
                    // The fling may still be settling when the neighbour sets off.
                    float t = s.Time + OpeningLength(s) - WalkerClips.PeekHandOnDoor;
                    return gag.OpenDegrees * (s.Phase == DoorPhase.ComingOut ? HingeOpen(t / gag.OpenSeconds, gag.Bounce) : 1f);
                }
                case DoorPhase.Closing:
                    return gag.OpenDegrees * (1f - SmootherStep(s.Time / CloseSeconds));
                default:
                    return 0f;
            }
        }

        /// <summary>
        /// The hinge's opening curve over u = 0..1 (and settling after): a gentle start off the latch, a smooth swing and a
        /// soft stop (smootherstep), with the gag's springy overshoot that settles by u = 1.4. Continuous with zero speed at
        /// both ends, so the leaf never snaps.
        /// </summary>
        public static float HingeOpen(float u, float bounce)
        {
            if (u <= 0f)
            {
                return 0f;
            }

            float main = SmootherStep(Math.Min(1f, u));
            if (bounce <= 0f)
            {
                return main;
            }

            // A damped spring that grows in over the swing and dies away by u = 1.4.
            float w = Math.Min(u, 1.4f);
            float env = (float)Math.Sin(Math.PI * Math.Min(1f, w / 1.4f));
            float spring = (float)Math.Sin(Math.PI * 1.6 * w) * env * env;
            return main + bounce * spring;
        }

        /// <summary>Advances every door by <paramref name="dt"/> seconds with Pip far away (tests).</summary>
        public void Advance(float dt) => Advance(dt, 0f, -1000f, null);

        /// <summary>Advances every door by <paramref name="dt"/> seconds (0 while paused); events are appended when given.</summary>
        public void Advance(float dt, float pipX, float pipZ, List<DoorEvent> events)
        {
            if (!(dt > 0f))
            {
                return;
            }

            foreach (var door in LivingStreetLayout.Doors)
            {
                Step(_slots[door.Id], dt, pipX, pipZ, events ?? _scratch);
            }

            _scratch.Clear();
        }

        private void Step(Slot s, float dt, float pipX, float pipZ, List<DoorEvent> events)
        {
            // The hall light and the shadow run on their own.
            float target = s.HallOn ? 1f : 0f;
            float step = dt / HallLightFade;
            s.HallLight = Math.Abs(target - s.HallLight) <= step ? target : s.HallLight + Math.Sign(target - s.HallLight) * step;
            if (s.SilhouetteT >= 0f)
            {
                s.SilhouetteT += dt;
            }

            if (s.HallOffIn >= 0f)
            {
                s.HallOffIn -= dt;
                if (s.HallOffIn <= 0f)
                {
                    s.HallOn = false;
                    s.HallOffIn = -1f;
                }
            }

            float left = dt;
            int guard = 0;
            while (left > 1e-6f && guard++ < 16)
            {
                switch (s.Phase)
                {
                    case DoorPhase.Closed:
                        left = 0f;
                        break;
                    case DoorPhase.Called:
                        s.SinceRing += left;
                        left = 0f;
                        break;
                    case DoorPhase.Answering:
                        left = Run(s, left, AnswerSeconds, () =>
                        {
                            s.Phase = DoorPhase.Opening;
                            foreach (var f in s.Figures)
                            {
                                f.Shown = true;
                                f.Clip = WalkerClip.OpenDoorPeek;
                                f.ClipTime = 0f;
                            }
                        });
                        break;
                    case DoorPhase.Opening:
                    {
                        float length = OpeningLength(s);
                        float take = Math.Min(left, length - s.Time);
                        s.Time += take;
                        left -= take;
                        foreach (var f in s.Figures)
                        {
                            f.ClipTime = s.Time;
                            f.Clip = s.Time < WalkerClips.Seconds(WalkerClip.OpenDoorPeek) ? WalkerClip.OpenDoorPeek : WalkerClip.Locomotion;
                        }

                        if (!s.OpenedSent && s.Time >= WalkerClips.PeekHandOnDoor)
                        {
                            s.OpenedSent = true;
                            events.Add(new DoorEvent(DoorEventKind.Opened, s.Door.Id, s.Ringer, s.Group, s.Rings, -1));
                        }

                        if (s.Time >= length - 1e-6f)
                        {
                            s.Phase = DoorPhase.ComingOut;
                            s.Time = 0f;
                            StartComingOut(s, pipX, pipZ);
                            if (!s.ArrivedSent)
                            {
                                s.ArrivedSent = true;
                                events.Add(new DoorEvent(DoorEventKind.Arrived, s.Door.Id, s.Ringer, s.Group, s.Rings, -1));
                            }
                        }

                        break;
                    }
                    case DoorPhase.ComingOut:
                    {
                        float take = left;
                        s.Time += take;
                        left = 0f;
                        bool moving = false;
                        foreach (var f in s.Figures)
                        {
                            moving |= StepStride(s, f, take);
                        }

                        if (!moving)
                        {
                            s.Phase = DoorPhase.Giving;
                            s.Time = 0f;
                            s.Turn = 0;
                            s.TurnGiving = false;
                            s.TurnTime = 0f;
                        }

                        break;
                    }
                    case DoorPhase.Giving:
                        left = StepGiving(s, left, pipX, pipZ, events);
                        break;
                    case DoorPhase.GoingIn:
                    {
                        s.Time += left;
                        float take = left;
                        left = 0f;
                        bool moving = false;
                        foreach (var f in s.Figures)
                        {
                            moving |= StepStride(s, f, take);
                        }

                        if (!moving)
                        {
                            s.Phase = DoorPhase.Closing;
                            s.Time = 0f;
                        }

                        break;
                    }
                    case DoorPhase.Closing:
                        left = Run(s, left, CloseSeconds, () =>
                        {
                            s.Phase = DoorPhase.Closed;
                            s.Rings = 0;
                            s.SilhouetteT = -1f;
                            s.HallOffIn = HallLightAfter;
                            // A group lets go of the door itself (once its family has gathered to leave); Pip's visit ends here.
                            if (s.Ringer == Ringer.Pip || s.Group < 0)
                            {
                                s.Holding = false;
                            }

                            Home(s);
                            events.Add(new DoorEvent(DoorEventKind.Shut, s.Door.Id, s.Ringer, s.Group, 0, -1));
                        });
                        break;
                }
            }
        }

        private static float Run(Slot s, float left, float length, Action done)
        {
            float take = Math.Min(left, length - s.Time);
            s.Time += take;
            left -= take;
            if (s.Time >= length - 1e-6f)
            {
                s.Time = 0f;
                done();
            }

            return left;
        }

        private static float OpeningLength(Slot s) =>
            Math.Max(WalkerClips.Seconds(WalkerClip.OpenDoorPeek), WalkerClips.PeekHandOnDoor + s.Door.Gag.OpenSeconds);

        /// <summary>Every figure waits just inside, facing the street, hidden.</summary>
        private static void Home(Slot s)
        {
            for (int i = 0; i < s.Figures.Length; i++)
            {
                var f = s.Figures[i];
                DoorStoops.Point(s.Door, DoorStoops.InsideOut, FigureAcross(s, i), out f.X, out f.Z);
                f.Y = DoorStoops.Sill;
                f.Yaw = DoorStoops.FacingStreet(s.Door);
                f.Speed = 0f;
                f.Clip = WalkerClip.Locomotion;
                f.ClipTime = 0f;
                f.Shown = false;
                f.Striding = false;
                f.Turning = false;
            }
        }

        private static float FigureAcross(Slot s, int i) => s.Figures.Length == 1 ? 0f : i == 0 ? -0.18f : 0.3f;

        private void StartComingOut(Slot s, float pipX, float pipZ)
        {
            var door = s.Door;
            float treat = DoorStoops.TreatOut(door);
            float across = 0f;
            float outward = treat;
            float endYaw = DoorStoops.FacingStreet(door);
            if (s.Ringer == Ringer.Pip)
            {
                // Toward Pip: stand a receive distance from her, on the ground in front of the steps.
                ToDoorFrame(door, pipX, pipZ, out float po, out float pa);
                if (po > 0f && po < treat + 6f)
                {
                    float d = (float)Math.Sqrt(po * po + pa * pa);
                    float go = po - po / Math.Max(d, 1e-3f) * WalkerClips.ReceiveDistance;
                    float ga = pa - pa / Math.Max(d, 1e-3f) * WalkerClips.ReceiveDistance;
                    outward = Clamp(go, treat, treat + 0.9f);
                    across = Clamp(ga, -0.3f, 0.9f);
                    DoorStoops.Point(door, outward, across, out float gx, out float gz);
                    endYaw = ClampYaw(Stride.YawTo(gx, gz, pipX, pipZ), DoorStoops.FacingStreet(door), FaceLimit);
                }
            }

            var giver = s.Figures[0];
            DoorStoops.Point(door, outward, across, out float tx, out float tz);
            giver.Stride = new Stride(giver.X, giver.Z, giver.Yaw, tx, tz, endYaw, Crowd.CrowdKit.NeighbourModel, giver.Y, 0f);
            giver.StrideT = 0f;
            giver.Striding = true;
            for (int i = 1; i < s.Figures.Length; i++)
            {
                // The second Abbott steps onto the landing and stays in the doorway, hopping with glee.
                var f = s.Figures[i];
                DoorStoops.Point(door, DoorStoops.DoorwayOut, FigureAcross(s, i) + 0.05f, out float dx, out float dz);
                f.Stride = new Stride(f.X, f.Z, f.Yaw, dx, dz, DoorStoops.FacingStreet(door), Crowd.CrowdKit.NeighbourModel,
                    DoorStoops.Sill, DoorStoops.Sill);
                f.StrideT = 0f;
                f.Striding = true;
            }
        }

        private static void StartGoingIn(Slot s)
        {
            for (int i = 0; i < s.Figures.Length; i++)
            {
                var f = s.Figures[i];
                DoorStoops.Point(s.Door, DoorStoops.InsideOut, FigureAcross(s, i), out float tx, out float tz);
                float heading = Stride.YawTo(f.X, f.Z, tx, tz);
                f.Stride = new Stride(f.X, f.Z, f.Yaw, tx, tz, heading, Crowd.CrowdKit.NeighbourModel, f.Y, DoorStoops.Sill);
                f.StrideT = 0f;
                f.Striding = true;
            }
        }

        /// <summary>Moves a figure along its stride; true while it is still walking.</summary>
        private static bool StepStride(Slot s, FigureState f, float dt)
        {
            if (!f.Striding)
            {
                f.Speed = 0f;
                f.Clip = WalkerClip.Locomotion;
                return false;
            }

            f.StrideT += dt;
            f.Stride.Sample(f.StrideT, out f.X, out f.Z, out _, out f.Yaw, out f.Speed, out f.Clip, out f.ClipTime);
            ToDoorFrame(s.Door, f.X, f.Z, out float outward, out _);
            f.Y = DoorStoops.GroundY(s.Door, outward);
            f.Turning = f.Stride.Turning && f.StrideT < f.Stride.Duration;
            if (f.StrideT >= f.Stride.Duration)
            {
                f.Striding = false;
                f.Speed = 0f;
                f.Clip = WalkerClip.Locomotion;
                f.Turning = false;
                return false;
            }

            return true;
        }

        private float StepGiving(Slot s, float left, float pipX, float pipZ, List<DoorEvent> events)
        {
            var giver = s.Figures[0];
            giver.Speed = 0f;
            giver.Turning = false;
            s.Time += left;
            if (!s.TurnGiving)
            {
                giver.Clip = WalkerClip.Locomotion;
                s.TurnTime += left;
                if (s.Ringer == Ringer.Pip)
                {
                    // Face Pip (within reason) and give once she is close enough; she may take her time.
                    float want = ClampYaw(Stride.YawTo(giver.X, giver.Z, pipX, pipZ), DoorStoops.FacingStreet(s.Door), FaceLimit);
                    float delta = Stride.DeltaYaw(giver.Yaw, want);
                    float turn = FaceRate * left;
                    if (Math.Abs(delta) > 1f)
                    {
                        giver.Yaw += Math.Abs(delta) <= turn ? delta : Math.Sign(delta) * turn;
                        giver.Turning = Math.Abs(delta) > 6f;
                        giver.Speed = giver.Turning ? 0.35f : 0f;
                    }

                    float dx = pipX - giver.X;
                    float dz = pipZ - giver.Z;
                    float d = (float)Math.Sqrt(dx * dx + dz * dz);
                    if (d <= PipReach && Math.Abs(delta) < 12f)
                    {
                        s.TurnGiving = true;
                        s.TurnTime = 0f;
                        s.Released = false;
                    }
                    else if (d > AbandonDistance)
                    {
                        // She went on her way: nothing is lost, the neighbour goes back in.
                        FinishGiving(s, events);
                    }
                }
                else if (s.TurnTime > TurnGiveUp)
                {
                    NextTurn(s, events);
                }

                return 0f;
            }

            float length = WalkerClips.Seconds(WalkerClip.GiveTreat);
            float take = Math.Min(left, length - s.TurnTime);
            s.TurnTime += take;
            giver.Clip = WalkerClip.GiveTreat;
            giver.ClipTime = s.TurnTime;
            if (!s.Released && s.TurnTime >= WalkerClips.GiveRelease)
            {
                s.Released = true;
                events.Add(new DoorEvent(DoorEventKind.Released, s.Door.Id, s.Ringer, s.Group, s.Rings,
                    s.Ringer == Ringer.Pip ? -1 : s.Turn));
            }

            if (s.TurnTime >= length - 1e-6f)
            {
                giver.Clip = WalkerClip.Locomotion;
                NextTurn(s, events);
            }

            return left - take;
        }

        private static void NextTurn(Slot s, List<DoorEvent> events)
        {
            s.Turn++;
            s.TurnGiving = false;
            s.TurnTime = 0f;
            if (s.Turn >= s.Recipients)
            {
                FinishGiving(s, events);
            }
        }

        private static void FinishGiving(Slot s, List<DoorEvent> events)
        {
            s.Phase = DoorPhase.GoingIn;
            s.Time = 0f;
            events.Add(new DoorEvent(DoorEventKind.Done, s.Door.Id, s.Ringer, s.Group, s.Rings, -1));
            StartGoingIn(s);
        }

        /// <summary>The visit is over for the group holding the door once the door is shut; it calls this as it leaves.</summary>
        public bool VisitOver(string doorId) => Phase(doorId) == DoorPhase.Closed || Phase(doorId) == DoorPhase.Closing ||
                                                Phase(doorId) == DoorPhase.GoingIn;

        /// <summary>Shuts every door at once (back to title, a reset).</summary>
        public void Reset()
        {
            foreach (var s in _slots.Values)
            {
                s.Phase = DoorPhase.Closed;
                s.Time = 0f;
                s.Rings = 0;
                s.Holding = false;
                s.Group = -1;
                s.Queued = -1;
                s.HallOn = false;
                s.HallLight = 0f;
                s.HallOffIn = -1f;
                s.SilhouetteT = -1f;
                Home(s);
            }
        }

        /// <summary>World plan point to the door's frame: metres out from the wall and across to the visitor's right.</summary>
        public static void ToDoorFrame(NeighbourDoor door, float x, float z, out float outward, out float across)
        {
            DoorStoops.Outward(door, out float ox, out float oz);
            float dx = x - door.DoorX;
            float dz = z - door.DoorZ;
            outward = dx * ox + dz * oz;
            across = dx * -oz + dz * ox;
        }

        private static float ClampYaw(float yaw, float around, float limit)
        {
            float d = Stride.DeltaYaw(around, yaw);
            return around + Clamp(d, -limit, limit);
        }

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;

        private static float SmootherStep(float u)
        {
            u = u < 0f ? 0f : u > 1f ? 1f : u;
            return u * u * u * (u * (u * 6f - 15f) + 10f);
        }
    }
}
