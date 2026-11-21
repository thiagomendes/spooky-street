using System;

namespace SpookyStreet.Core.Movement
{
    /// <summary>Floating virtual-stick math, shared by touch controls and tests.</summary>
    public static class StickMath
    {
        /// <summary>
        /// Converts a finger offset from the stick origin into a stick value with magnitude ≤ 1.
        /// Inside the dead zone the value is zero; outside it the range is rescaled so movement
        /// starts smoothly instead of jumping to the dead-zone magnitude.
        /// </summary>
        public static void Evaluate(float offsetX, float offsetY, float radius, float deadZone,
            out float valueX, out float valueY)
        {
            valueX = 0f;
            valueY = 0f;
            if (radius <= 0f)
            {
                return;
            }

            float x = offsetX / radius;
            float y = offsetY / radius;
            float magnitude = (float)Math.Sqrt(x * x + y * y);
            if (magnitude <= deadZone || magnitude < 1e-6f)
            {
                return;
            }

            float clamped = Math.Min(magnitude, 1f);
            float rescaled = deadZone >= 1f ? 1f : (clamped - deadZone) / (1f - deadZone);
            valueX = x / magnitude * rescaled;
            valueY = y / magnitude * rescaled;
        }

        /// <summary>
        /// Returns where a floating stick's origin should move so the finger stays within radius
        /// (the origin trails the finger instead of the value saturating forever).
        /// </summary>
        public static void FollowOrigin(float originX, float originY, float fingerX, float fingerY, float radius,
            out float newOriginX, out float newOriginY)
        {
            float dx = fingerX - originX;
            float dy = fingerY - originY;
            float distance = (float)Math.Sqrt(dx * dx + dy * dy);
            if (distance <= radius || distance < 1e-6f)
            {
                newOriginX = originX;
                newOriginY = originY;
                return;
            }

            float excess = distance - radius;
            newOriginX = originX + dx / distance * excess;
            newOriginY = originY + dy / distance * excess;
        }
    }
}
