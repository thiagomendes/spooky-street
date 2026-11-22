using System;

namespace SpookyStreet.Core.World
{
    /// <summary>
    /// Ambient life for the environment (spec §6 "density and life"): the owl house wind wheel turns slowly, the
    /// rooftop owl puppet sways a little, paper bats bob. Driven by game time, so pausing (timeScale 0) freezes
    /// it. Reduced motion (spec §5/§6: "removes shake and decorative sway") stops the sway and bob and leaves only
    /// the slow wheel, which reads as wind rather than decoration.
    /// </summary>
    public static class AmbientMotionModel
    {
        public const float WindWheelDegreesPerSecond = 36f;

        public const float OwlSwayHz = 0.21f;
        public const float OwlSwayDegrees = 2f;
        public const float OwlNodHz = 0.13f;
        public const float OwlNodDegrees = 1.2f;

        public const float BatBobHz = 0.45f;
        public const float BatBobMetres = 0.07f;
        public const float BatTurnDegrees = 6f;

        /// <summary>Wheel angle in [0, 360) after <paramref name="time"/> seconds of game time.</summary>
        public static float SpinDegrees(double time, float degreesPerSecond)
        {
            double angle = time * degreesPerSecond % 360.0;
            if (angle < 0.0)
            {
                angle += 360.0;
            }

            return (float)angle;
        }

        /// <summary>A sine oscillation; <paramref name="phase"/> is in cycles so neighbours can be desynchronised.</summary>
        public static float Oscillate(double time, float hz, float amplitude, float phase = 0f) =>
            amplitude * (float)Math.Sin((time * hz + phase) * 2.0 * Math.PI);

        /// <summary>Decorative sway and bob amplitude scale: zero in reduced-motion mode.</summary>
        public static float DecorativeScale(bool reducedMotion) => reducedMotion ? 0f : 1f;

        /// <summary>Wheel speed: the wheel keeps turning slowly even in reduced-motion mode (half speed).</summary>
        public static float WheelSpeed(bool reducedMotion) =>
            reducedMotion ? WindWheelDegreesPerSecond * 0.5f : WindWheelDegreesPerSecond;

        /// <summary>Stable per-instance phase in [0, 1) so a row of bats never bobs in lockstep.</summary>
        public static float PhaseFor(int index) => (float)((index * 0.618033988749895) % 1.0);
    }
}
