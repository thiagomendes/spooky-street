using System.Collections.Generic;

namespace SpookyStreet.Core.LivingStreet
{
    /// <summary>
    /// The notebook's street page (living-street §4): a scrapbook of Pip's porch as it is now and the neighbours she has
    /// met. Keepsakes rules (S004 §2.1): it appears only once there is something on it, it shows only what was done (no
    /// empty slot, no silhouette of a neighbour not yet met) and it never says "x of y".
    /// </summary>
    public static class StreetPageRules
    {
        public static bool Shown(NeighbourhoodState state) => state != null && !state.IsEmpty;

        /// <summary>The neighbours to draw, in street order: only those whose bell Pip has rung.</summary>
        public static IReadOnlyList<NeighbourDoor> Faces(NeighbourhoodState state)
        {
            var faces = new List<NeighbourDoor>();
            if (state == null)
            {
                return faces;
            }

            foreach (var door in LivingStreetLayout.Doors)
            {
                if (state.Visits(door.Id) > 0)
                {
                    faces.Add(door);
                }
            }

            return faces;
        }

        /// <summary>Each neighbour's face-disc colour on the page (their front door's paint, 0xRRGGBB).</summary>
        public static int FaceRgb(NeighbourDoor door) =>
            door == null ? 0x5B3A7A
            : door.Id == LivingStreetLayout.Abbotts ? 0xB5602A
            : door.Id == LivingStreetLayout.Pearl ? 0x5B3A7A : 0x7A3E6E;
    }
}
