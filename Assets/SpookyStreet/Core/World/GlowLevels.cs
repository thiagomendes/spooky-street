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
        public const float Lamp = 1.3f; // amber, never clipping to white next to the attic (CI run 27)
        public const float JackFace = 1.8f;
        public const float PaperBatEdge = 1.2f;
        public const float Default = 1.5f;

        /// <summary>
        /// Lit windows on the neighbour houses: lived-in but quiet, under the bloom threshold so the neighbours never
        /// compete with the owl house (and far below the attic window).
        /// </summary>
        public const float NeighbourWindow = 0.9f;

        /// <summary>
        /// The owl house's lit window cards outside (house v3: painted rooms behind the ground-floor windows and the
        /// open thresholds): warm and lived-in, under the attic window by far so it stays the brightest point outside.
        /// </summary>
        public const float RoomCard = 1.2f;

        /// <summary>
        /// The painted night views behind the interior's windows and its two thresholds: a night street, so they glow
        /// only softly (under the bloom threshold) and never outshine the rooms' lamps.
        /// </summary>
        public const float StreetView = 0.85f;

        /// <summary>
        /// The S002 tier-1 star flecks (retired from the world by S002.5b; the star stays the tier-1 badge picture).
        /// </summary>
        public const float HintSparkle = 0.95f;

        /// <summary>
        /// The tier-1 area glow (S002.5b, D26): a soft warm pool on the area's floor, well under the bloom threshold, so
        /// it reads as lamplight on a place and never as a glowing "this matters" mark.
        /// </summary>
        public const float HintAreaGlow = 0.55f;

        /// <summary>A clue's glint (S002.5b attractions): a brief catch of light, under the bloom threshold.</summary>
        public const float ClueGlint = 0.95f;

        /// <summary>
        /// The workshop nook's hanging lamp shade (house v3 cutaway): a warm practical, well under the attic window.
        /// </summary>
        public const float NookLamp = 1.2f;

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

            if (objectName.StartsWith("NeighbourWindow", StringComparison.Ordinal))
            {
                return NeighbourWindow;
            }

            if (objectName.StartsWith("RoomCard", StringComparison.Ordinal))
            {
                return RoomCard;
            }

            if (objectName.StartsWith("StreetView", StringComparison.Ordinal))
            {
                return StreetView;
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
            Math.Max(Math.Max(Math.Max(SideWindow, Lamp), Math.Max(JackFace, PaperBatEdge)),
                Math.Max(Math.Max(Default, Moon), Math.Max(RoomCard, StreetView)));
    }
}
