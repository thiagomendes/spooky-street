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
        Closed,
        Opening,
        Open,
        Closing,
    }

    /// <summary>What a ring brought: the door that answers, its gag, and (for Pip) the line and the visit it counts.</summary>
    public sealed class DoorAnswer
    {
        internal DoorAnswer(NeighbourDoor door, Ringer ringer, string lineKey, int visitsBefore)
        {
            Door = door;
            Ringer = ringer;
            LineKey = lineKey;
            VisitsBefore = visitsBefore;
        }

        public NeighbourDoor Door { get; }
        public Ringer Ringer { get; }

        /// <summary>The neighbour's line to Pip (null for a group: they hear the bell and the cheer, no subtitle).</summary>
        public string LineKey { get; }

        /// <summary>Pip's earlier visits when she rang (0 on the first).</summary>
        public int VisitsBefore { get; }

        /// <summary>A treat goes to Pip (to the pail; never a number).</summary>
        public bool TreatForPip => Ringer == Ringer.Pip;
    }

    /// <summary>
    /// The three trick-or-treat doors' routine (living-street §4.1), pure and frame-rate independent: a ring opens a
    /// closed door with that house's gag (<see cref="DoorGag"/>), the neighbour stays in the doorway for
    /// <see cref="HoldSeconds"/>, then the door swings shut. A door already answering ignores another ring (a group and
    /// Pip never get two answers at once). Pip's visits pick the neighbour's line: first, again (by name), and a
    /// third gag for every later visit. Bells, never knocks (§3.4 rule 5).
    /// </summary>
    public sealed class NeighbourDoors
    {
        /// <summary>The neighbour stays in the open doorway this long.</summary>
        public const float HoldSeconds = 3.6f;

        /// <summary>The door swings shut in this long.</summary>
        public const float CloseSeconds = 0.6f;

        private sealed class Slot
        {
            public DoorPhase Phase;
            public float Time;
            public Ringer LastRinger;
        }

        private readonly Dictionary<string, Slot> _slots = new Dictionary<string, Slot>(StringComparer.Ordinal);

        public NeighbourDoors()
        {
            foreach (var door in LivingStreetLayout.Doors)
            {
                _slots[door.Id] = new Slot();
            }
        }

        public DoorPhase Phase(string doorId) => _slots.TryGetValue(doorId ?? string.Empty, out var s) ? s.Phase : DoorPhase.Closed;

        /// <summary>True while the door is answering (anything but closed).</summary>
        public bool Busy(string doorId) => Phase(doorId) != DoorPhase.Closed;

        /// <summary>Who rang last (what the doorway shows: the treat goes to Pip or to the group).</summary>
        public Ringer LastRinger(string doorId) => _slots.TryGetValue(doorId ?? string.Empty, out var s) ? s.LastRinger : Ringer.Group;

        /// <summary>
        /// A ring at <paramref name="doorId"/>. Returns the answer, or null when the door is already answering or
        /// unknown. <paramref name="pipVisitsBefore"/> picks Pip's line (ignored for a group).
        /// </summary>
        public DoorAnswer Ring(string doorId, Ringer ringer, int pipVisitsBefore = 0)
        {
            var door = LivingStreetLayout.Door(doorId);
            if (door == null || !_slots.TryGetValue(doorId, out var slot) || slot.Phase != DoorPhase.Closed)
            {
                return null;
            }

            slot.Phase = DoorPhase.Opening;
            slot.Time = 0f;
            slot.LastRinger = ringer;
            string line = ringer == Ringer.Pip ? door.LineFor(pipVisitsBefore) : null;
            return new DoorAnswer(door, ringer, line, ringer == Ringer.Pip ? Math.Max(0, pipVisitsBefore) : 0);
        }

        /// <summary>Advances every door by <paramref name="dt"/> seconds (0 while paused).</summary>
        public void Advance(float dt)
        {
            if (!(dt > 0f))
            {
                return;
            }

            foreach (var door in LivingStreetLayout.Doors)
            {
                var slot = _slots[door.Id];
                float left = dt;
                while (left > 0f && slot.Phase != DoorPhase.Closed)
                {
                    float length = slot.Phase == DoorPhase.Opening ? door.Gag.OpenSeconds
                        : slot.Phase == DoorPhase.Open ? HoldSeconds : CloseSeconds;
                    float step = Math.Min(left, length - slot.Time);
                    slot.Time += step;
                    left -= step;
                    if (slot.Time >= length - 1e-5f)
                    {
                        slot.Time = 0f;
                        slot.Phase = slot.Phase == DoorPhase.Opening ? DoorPhase.Open
                            : slot.Phase == DoorPhase.Open ? DoorPhase.Closing : DoorPhase.Closed;
                    }
                }
            }
        }

        /// <summary>
        /// The leaf's swing in degrees (0 shut, the gag's angle open), with the gag's overshoot on the way out: what the
        /// hinged door rig shows.
        /// </summary>
        public float SwingDegrees(string doorId)
        {
            var door = LivingStreetLayout.Door(doorId);
            if (door == null || !_slots.TryGetValue(doorId, out var slot))
            {
                return 0f;
            }

            float open = door.Gag.OpenDegrees;
            switch (slot.Phase)
            {
                case DoorPhase.Opening:
                {
                    float t = door.Gag.OpenSeconds > 0f ? slot.Time / door.Gag.OpenSeconds : 1f;
                    float eased = 1f - (1f - t) * (1f - t);
                    // A springy fling peaks past the angle at about 70 % and settles at the end.
                    float over = door.Gag.Bounce * (float)Math.Sin(Math.PI * t);
                    return open * (eased + over);
                }
                case DoorPhase.Open:
                    return open;
                case DoorPhase.Closing:
                {
                    float t = slot.Time / CloseSeconds;
                    return open * (1f - t * t);
                }
                default:
                    return 0f;
            }
        }

        /// <summary>True while the neighbour stands in the doorway (the door is wide enough open).</summary>
        public bool NeighbourShown(string doorId)
        {
            var phase = Phase(doorId);
            if (phase == DoorPhase.Open)
            {
                return true;
            }

            var door = LivingStreetLayout.Door(doorId);
            return phase == DoorPhase.Opening && door != null && SwingDegrees(doorId) >= door.Gag.OpenDegrees * 0.6f;
        }

        /// <summary>Shuts every door at once (back to title, a reset).</summary>
        public void Reset()
        {
            foreach (var slot in _slots.Values)
            {
                slot.Phase = DoorPhase.Closed;
                slot.Time = 0f;
            }
        }
    }
}
