using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.Nonverbal
{
    /// <summary>A marked felt spot of the parent gate, in normalized panel coordinates (0..1, origin bottom-left).</summary>
    public readonly struct GateZone
    {
        public GateZone(float minX, float minY, float maxX, float maxY)
        {
            if (maxX <= minX || maxY <= minY)
            {
                throw new ArgumentException("a zone needs a positive size");
            }

            MinX = minX;
            MinY = minY;
            MaxX = maxX;
            MaxY = maxY;
        }

        public float MinX { get; }

        public float MinY { get; }

        public float MaxX { get; }

        public float MaxY { get; }

        public float CenterX => (MinX + MaxX) * 0.5f;

        public float CenterY => (MinY + MaxY) * 0.5f;

        public bool Contains(float x, float y) => x >= MinX && x <= MaxX && y >= MinY && y <= MaxY;

        public bool Overlaps(GateZone o) => MinX < o.MaxX && o.MinX < MaxX && MinY < o.MaxY && o.MinY < MaxY;
    }

    public enum ParentGateState
    {
        /// <summary>Waiting for both spots to be held.</summary>
        Waiting,

        /// <summary>Both spots are held; the candy ring is filling.</summary>
        Holding,

        /// <summary>Held long enough: open the guarded thing (language download, credits link, external link).</summary>
        Passed,

        /// <summary>Nobody passed in time: close the gate, nothing happens.</summary>
        TimedOut,

        /// <summary>The player backed out.</summary>
        Cancelled,
    }

    /// <summary>
    /// The parent gate (owner decision D23.5): press and hold two marked spots at the same time for 3 s. Releasing
    /// either (or sliding off it) resets the fill. No reading, no maths. Pure: feed touches and time.
    /// </summary>
    public sealed class ParentGate
    {
        public const float DefaultHoldSeconds = 3.0f;
        public const float DefaultTimeoutSeconds = 30f;

        /// <summary>Diagonal spots (bottom-left, top-right): one small hand cannot cover both.</summary>
        public static readonly GateZone DefaultZoneA = new GateZone(0.08f, 0.15f, 0.30f, 0.45f);
        public static readonly GateZone DefaultZoneB = new GateZone(0.70f, 0.55f, 0.92f, 0.85f);

        private readonly Dictionary<int, int> _touchZone = new Dictionary<int, int>();
        private readonly GateZone[] _zones;

        public ParentGate()
            : this(DefaultZoneA, DefaultZoneB, DefaultHoldSeconds, DefaultTimeoutSeconds)
        {
        }

        public ParentGate(GateZone a, GateZone b, float holdSeconds, float timeoutSeconds)
        {
            if (a.Overlaps(b))
            {
                throw new ArgumentException("gate zones must not overlap");
            }

            if (holdSeconds <= 0f || timeoutSeconds <= holdSeconds)
            {
                throw new ArgumentOutOfRangeException(nameof(timeoutSeconds), "need 0 < hold < timeout");
            }

            _zones = new[] { a, b };
            HoldSeconds = holdSeconds;
            TimeoutSeconds = timeoutSeconds;
        }

        public float HoldSeconds { get; }

        public float TimeoutSeconds { get; }

        public GateZone ZoneA => _zones[0];

        public GateZone ZoneB => _zones[1];

        public ParentGateState State { get; private set; } = ParentGateState.Waiting;

        public bool IsFinished => State == ParentGateState.Passed || State == ParentGateState.TimedOut || State == ParentGateState.Cancelled;

        /// <summary>Seconds both spots have been held continuously.</summary>
        public float HeldSeconds { get; private set; }

        /// <summary>0..1 for the candy ring.</summary>
        public float Progress => Math.Min(1f, HeldSeconds / HoldSeconds);

        public float OpenSeconds { get; private set; }

        /// <summary>Spot <paramref name="zone"/> (0 or 1) has a finger on it: light it.</summary>
        public bool IsZoneHeld(int zone)
        {
            foreach (var pair in _touchZone)
            {
                if (pair.Value == zone)
                {
                    return true;
                }
            }

            return false;
        }

        public void TouchDown(int touchId, float x, float y) => Track(touchId, x, y);

        /// <summary>A finger that slides off its spot counts as released; sliding onto a spot holds it.</summary>
        public void TouchMove(int touchId, float x, float y) => Track(touchId, x, y);

        public void TouchUp(int touchId)
        {
            if (IsFinished)
            {
                return;
            }

            _touchZone.Remove(touchId);
            Refresh();
        }

        /// <summary>The OS cancelled all touches (app backgrounded, gesture taken): everything is released.</summary>
        public void ReleaseAll()
        {
            if (IsFinished)
            {
                return;
            }

            _touchZone.Clear();
            Refresh();
        }

        public void Cancel()
        {
            if (IsFinished)
            {
                return;
            }

            _touchZone.Clear();
            HeldSeconds = 0f;
            State = ParentGateState.Cancelled;
        }

        public void Update(float dt)
        {
            if (IsFinished || dt <= 0f)
            {
                return;
            }

            OpenSeconds += dt;
            if (State == ParentGateState.Holding)
            {
                HeldSeconds += dt;
                if (HeldSeconds >= HoldSeconds)
                {
                    HeldSeconds = HoldSeconds;
                    State = ParentGateState.Passed;
                    return;
                }
            }

            if (OpenSeconds >= TimeoutSeconds)
            {
                _touchZone.Clear();
                HeldSeconds = 0f;
                State = ParentGateState.TimedOut;
            }
        }

        private void Track(int touchId, float x, float y)
        {
            if (IsFinished)
            {
                return;
            }

            int zone = _zones[0].Contains(x, y) ? 0 : _zones[1].Contains(x, y) ? 1 : -1;
            if (zone < 0)
            {
                _touchZone.Remove(touchId);
            }
            else
            {
                _touchZone[touchId] = zone;
            }

            Refresh();
        }

        private void Refresh()
        {
            bool both = IsZoneHeld(0) && IsZoneHeld(1);
            if (!both)
            {
                HeldSeconds = 0f;
            }

            State = both ? ParentGateState.Holding : ParentGateState.Waiting;
        }
    }
}
