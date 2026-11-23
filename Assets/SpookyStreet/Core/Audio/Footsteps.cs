using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.Audio
{
    /// <summary>The three footstep sets (S003): what Pip's soles land on.</summary>
    public enum FootstepSurface
    {
        Wood,
        Grass,
        Stone,
    }

    /// <summary>
    /// The ground under Pip as a footstep surface, from the names of the collider Pip stands on and its parents (the
    /// greybox, street-set and house colliders are all named after what they are: <c>Ground</c>, <c>Path</c>,
    /// <c>Porch_Deck</c>, <c>Collider_Step</c>…). The first name that matches a keyword wins, so a part's own name
    /// beats the model it belongs to. Inside the owl house every floor is wooden boards.
    /// </summary>
    public static class FootstepSurfaces
    {
        // Checked in this order; within a name the earliest table wins (a "StonePorch" would be wood: rare and fine).
        private static readonly string[] WoodWords =
        {
            "porch", "deck", "step", "stair", "plank", "board", "floor", "loft", "landing", "bridge", "ramp", "shed",
            "cabin", "treehouse", "sill", "reveal", "window", "awning", "bench", "crate", "table", "seat", "roof",
            "wood", "timber", "rail",
        };

        private static readonly string[] StoneWords =
        {
            "path", "stone", "sidewalk", "pavement", "paving", "street", "road", "kerb", "curb", "cobble", "rock",
            "chimney",
        };

        private static readonly string[] GrassWords = { "lawn", "grass", "ground", "hedge", "leaf", "leaves", "yard" };

        public static string EventFor(FootstepSurface surface)
        {
            switch (surface)
            {
                case FootstepSurface.Wood: return AudioEvents.StepWood;
                case FootstepSurface.Stone: return AudioEvents.StepStone;
                default: return AudioEvents.StepGrass;
            }
        }

        /// <summary>The surface one name describes, if any keyword matches (case-insensitive).</summary>
        public static bool TryClassify(string name, out FootstepSurface surface)
        {
            surface = FootstepSurface.Grass;
            if (string.IsNullOrEmpty(name)) return false;
            string lower = name.ToLowerInvariant();
            if (Contains(lower, WoodWords))
            {
                surface = FootstepSurface.Wood;
                return true;
            }

            if (Contains(lower, StoneWords))
            {
                surface = FootstepSurface.Stone;
                return true;
            }

            if (Contains(lower, GrassWords))
            {
                surface = FootstepSurface.Grass;
                return true;
            }

            return false;
        }

        /// <summary>
        /// The surface for a collider whose own name comes first in <paramref name="names"/>, then its parents'.
        /// Indoors everything is wood. Outdoors an unnamed surface is the lawn, and the lawn's one big ground collider
        /// also carries the street set's sidewalk slabs, so ground at <paramref name="onSidewalk"/> is stone.
        /// </summary>
        public static FootstepSurface Classify(IEnumerable<string> names, bool indoors, bool onSidewalk = false)
        {
            if (indoors) return FootstepSurface.Wood;
            var result = FootstepSurface.Grass;
            if (names != null)
            {
                foreach (var name in names)
                {
                    if (TryClassify(name, out var surface))
                    {
                        result = surface;
                        break;
                    }
                }
            }

            return result == FootstepSurface.Grass && onSidewalk ? FootstepSurface.Stone : result;
        }

        /// <summary>The street set's sidewalk band (EnvironmentLayout: 2 m deep along X in front of the houses).</summary>
        public static bool OnSidewalk(float x, float z) =>
            Math.Abs(x) <= World.EnvironmentLayout.SidewalkHalfX && z >= World.EnvironmentLayout.SidewalkFrontZ &&
            z <= World.EnvironmentLayout.SidewalkBackZ;

        private static bool Contains(string lower, string[] words)
        {
            foreach (var word in words)
            {
                if (lower.IndexOf(word, StringComparison.Ordinal) >= 0) return true;
            }

            return false;
        }
    }

    /// <summary>
    /// When a foot lands, from the distance Pip covers on the ground: one step per stride, the stride growing from a
    /// walk to a run, so steps follow the walk/run animation's speed (which is locked to ground speed). The first step
    /// comes a quarter stride after Pip starts moving, so a tap on the stick still gives a footfall.
    /// </summary>
    public sealed class FootstepCadence
    {
        public const float WalkStride = 0.55f;
        public const float RunStride = 0.95f;
        public const float WalkSpeed = 1.6f;
        public const float RunSpeed = 4.5f;

        /// <summary>Below this ground speed Pip is standing (or shuffling against a wall): no steps.</summary>
        public const float MinSpeed = 0.3f;

        private float _travelled;
        private bool _moving;

        /// <summary>Steps produced since the last <see cref="Reset"/>.</summary>
        public int Steps { get; private set; }

        /// <summary>Stride length (metres per footfall) at a ground speed.</summary>
        public static float StrideFor(float speed)
        {
            float t = (speed - WalkSpeed) / (RunSpeed - WalkSpeed);
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return WalkStride + (RunStride - WalkStride) * t;
        }

        /// <summary>
        /// One frame: <paramref name="distance"/> metres covered horizontally at <paramref name="speed"/> m/s. True when a
        /// foot lands this frame.
        /// </summary>
        public bool Advance(float distance, float speed, bool grounded)
        {
            if (!grounded || speed < MinSpeed || distance <= 0f || float.IsNaN(distance))
            {
                _moving = false;
                return false;
            }

            float stride = StrideFor(speed);
            if (!_moving)
            {
                _moving = true;
                _travelled = stride * 0.75f;
            }

            _travelled += distance;
            if (_travelled < stride) return false;
            _travelled = Math.Min(_travelled - stride, stride * 0.5f);
            Steps++;
            return true;
        }

        /// <summary>After a landing (the land sound is the footfall) or a teleport.</summary>
        public void Reset()
        {
            _moving = false;
            _travelled = 0f;
        }
    }
}
