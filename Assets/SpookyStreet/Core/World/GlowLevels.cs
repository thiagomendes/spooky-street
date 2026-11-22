using System;

namespace SpookyStreet.Core.World
{
    /// <summary>
    /// HDR emission intensity for the environment's light-emitting parts (objects whose name ends in
    /// <see cref="Suffix"/>). Art bible §3 / spec §6: the attic window holds the brightest sustained value on
    /// screen; lamps and jack-o'-lantern faces stay warm amber and clearly below it. Bloom starts at 1, so values
    /// above it glow; the moon stays under it.
    /// </summary>
    public static class GlowLevels
    {
        public const string Suffix = "_Glow";

        public const float AtticWindow = 4f;
        public const float SideWindow = 1.6f;
        public const float Lamp = 2.2f;
        public const float JackFace = 1.8f;
        public const float PaperBatEdge = 1.2f;
        public const float Default = 1.5f;

        /// <summary>Unlit moon disc colour scale: under the bloom threshold so it never competes with the attic.</summary>
        public const float Moon = 0.92f;

        public static bool IsGlowPart(string objectName) =>
            objectName != null && objectName.EndsWith(Suffix, StringComparison.Ordinal);

        /// <summary>Emission intensity for a glow part by its object name (e.g. "AtticWindow_Glow").</summary>
        public static float IntensityFor(string objectName)
        {
            if (string.IsNullOrEmpty(objectName))
            {
                return Default;
            }

            if (objectName.StartsWith("AtticWindow", StringComparison.Ordinal))
            {
                return AtticWindow;
            }

            if (objectName.StartsWith("SideWindow", StringComparison.Ordinal))
            {
                return SideWindow;
            }

            if (objectName.StartsWith("Lamp", StringComparison.Ordinal))
            {
                return Lamp;
            }

            if (objectName.StartsWith("Face", StringComparison.Ordinal))
            {
                return JackFace;
            }

            if (objectName.StartsWith("PaperBat", StringComparison.Ordinal))
            {
                return PaperBatEdge;
            }

            return Default;
        }

        /// <summary>The brightest level any non-attic glow part can get (for the value-hierarchy check).</summary>
        public static float BrightestPractical =>
            Math.Max(Math.Max(Math.Max(SideWindow, Lamp), Math.Max(JackFace, PaperBatEdge)), Math.Max(Default, Moon));
    }
}
