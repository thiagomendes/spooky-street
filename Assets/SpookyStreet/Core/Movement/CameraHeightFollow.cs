using System;

namespace SpookyStreet.Core.Movement
{
    /// <summary>
    /// Vertical follow for the camera pivot (spec §5 feel): while Pip is airborne the pivot holds still until Pip
    /// leaves a small band around it, so jump arcs and the slow bat-cape glide do not bob the camera; on the ground
    /// it eases back onto Pip. Large jumps in height (a teleport, a safe-ground return) snap.
    /// </summary>
    public sealed class CameraHeightFollow
    {
        /// <summary>Half-height of the airborne band (m) inside which the pivot does not move.</summary>
        public float AirTolerance = 0.3f;

        /// <summary>Exponential catch-up rate on the ground (1/s); 10 settles a 0.3 m offset in about 0.3 s.</summary>
        public float GroundCatchUpRate = 10f;

        /// <summary>Offsets larger than this (m) snap instead of easing.</summary>
        public float SnapDistance = 3f;

        private bool _initialized;

        /// <summary>The followed height (m) to place the pivot from.</summary>
        public float Height { get; private set; }

        public void Reset(float targetY)
        {
            Height = targetY;
            _initialized = true;
        }

        public float Step(float targetY, bool grounded, float deltaTime)
        {
            if (!_initialized || Math.Abs(targetY - Height) > SnapDistance)
            {
                Reset(targetY);
                return Height;
            }

            if (deltaTime <= 0f)
            {
                return Height;
            }

            if (grounded)
            {
                float blend = 1f - (float)Math.Exp(-GroundCatchUpRate * deltaTime);
                Height += (targetY - Height) * blend;
                if (Math.Abs(targetY - Height) < 1e-4f)
                {
                    Height = targetY;
                }
            }
            else if (targetY > Height + AirTolerance)
            {
                Height = targetY - AirTolerance;
            }
            else if (targetY < Height - AirTolerance)
            {
                Height = targetY + AirTolerance;
            }

            return Height;
        }
    }
}
