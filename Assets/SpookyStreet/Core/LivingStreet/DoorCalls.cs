using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.LivingStreet
{
    /// <summary>
    /// When a group's ring at a neighbour's door is heard (its doorbell and the "Trick or treat!" caption). Groups ring
    /// every door on every lap, so a child who stays by one door used to hear the same call again and again
    /// (docs/living-street-slice.md §6). Now a door calls once while Pip stays near it; it may call again only after she
    /// has walked away from it (past <see cref="HearingDistance"/> plus <see cref="LeaveMargin"/>, so standing on the
    /// edge never makes it flicker) and come back. No timer and no count reaches the child: the doors still open and the
    /// walkers still visit, only the repeated sound and caption are left out.
    /// <para>Pip's own porch is not a neighbour's door and is not held here: a group at her steps is a visitor asking
    /// her for a treat, and its call is what tells the child to hand one out.</para>
    /// </summary>
    public sealed class DoorCalls
    {
        /// <summary>Crowd sounds and calls are heard (and captioned) only this close to Pip (plan metres).</summary>
        public const float HearingDistance = 14f;

        /// <summary>How far past hearing Pip must walk before a door that called may call again.</summary>
        public const float LeaveMargin = 2f;

        private readonly Dictionary<string, (float X, float Z)> _called = new Dictionary<string, (float X, float Z)>();

        /// <summary>
        /// A group rang <paramref name="doorId"/> at (<paramref name="doorX"/>, <paramref name="doorZ"/>) with Pip at
        /// (<paramref name="pipX"/>, <paramref name="pipZ"/>). True when its bell and call should play: Pip can hear it
        /// and this door has not called since she came near.
        /// </summary>
        public bool Ring(string doorId, float doorX, float doorZ, float pipX, float pipZ)
        {
            if (doorId == null)
            {
                throw new ArgumentNullException(nameof(doorId));
            }

            if (Distance(doorX, doorZ, pipX, pipZ) > HearingDistance || _called.ContainsKey(doorId))
            {
                return false;
            }

            _called[doorId] = (doorX, doorZ);
            return true;
        }

        /// <summary>Each frame: the doors Pip has walked away from may call again.</summary>
        public void Update(float pipX, float pipZ)
        {
            if (_called.Count == 0)
            {
                return;
            }

            List<string> away = null;
            foreach (var pair in _called)
            {
                if (Distance(pair.Value.X, pair.Value.Z, pipX, pipZ) > HearingDistance + LeaveMargin)
                {
                    (away ??= new List<string>()).Add(pair.Key);
                }
            }

            if (away != null)
            {
                foreach (var id in away)
                {
                    _called.Remove(id);
                }
            }
        }

        /// <summary>True while <paramref name="doorId"/> has called and Pip has not walked away since.</summary>
        public bool Holding(string doorId) => doorId != null && _called.ContainsKey(doorId);

        /// <summary>A new game, a replay or a reset of the moment: every door may call again.</summary>
        public void Reset() => _called.Clear();

        private static float Distance(float ax, float az, float bx, float bz)
        {
            float dx = ax - bx;
            float dz = az - bz;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }
    }
}
