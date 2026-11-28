using System;

namespace SpookyStreet.Core.LivingStreet
{
    /// <summary>
    /// Where the people of a trick-or-treat visit stand at each lane door, measured from the house art
    /// (tools/blender/env/lane_houses.py: a 0.5 m landing at Mr. Fogg's and the Abbotts' doors, Nana Pearl's 1.95 m porch,
    /// one 0.34 m tread down to the ground, the sill at 0.4 m). Distances are "out" from the front wall's face toward the
    /// street along the door's axis, and "across" to the visitor's right.
    /// <list type="bullet">
    /// <item>The neighbour opens the door standing just inside (<see cref="InsideOut"/>), walks out and down the step to
    /// the <see cref="TreatOut"/> spot on the ground, where children (and Pip) can reach him.</item>
    /// <item>A child receives at <see cref="ReceiveOut"/>: the giver's knuckles over the pail's mouth at the release
    /// (<see cref="WalkerClips.ReceiveDistance"/>).</item>
    /// <item>The children of the group visiting wait their turn in a small arc behind the receive spot, facing the door
    /// (<see cref="WaitSlot"/>), never stacked; the grown-up stands behind them (<see cref="GrownUpSlot"/>).</item>
    /// </list>
    /// Everything is in front of the house's footprint and the slice's stand, so the lane's clearance tests cover it.
    /// </summary>
    public static class DoorStoops
    {
        public const float Sill = 0.4f;
        public const float Rise = 0.2f;
        public const float Tread = 0.34f;

        /// <summary>The neighbour waits behind the shut leaf here (inside the lit hall, 0.95 m deep).</summary>
        public const float InsideOut = -0.3f;

        /// <summary>A neighbour who stays in the doorway (the second Abbott) stands here (on the landing).</summary>
        public const float DoorwayOut = LivingStreetLayout.DoorwayOut;

        /// <summary>Radius of the waiting arc around the receive spot and the angle between its places.</summary>
        public const float ArcRadius = 1.15f;
        public const float ArcStepDegrees = 32f;

        /// <summary>The grown-up of a visiting group waits this far behind the receive spot, a little to one side.</summary>
        public const float GrownUpBack = 2.05f;
        public const float GrownUpAcross = 0.55f;

        /// <summary>The landing or porch deck's depth in front of the door.</summary>
        public static float DeckDepth(NeighbourDoor door) => door != null && door.Id == LivingStreetLayout.Pearl ? 1.95f : 0.5f;

        /// <summary>The neighbour's treat spot on the ground, just past the step.</summary>
        public static float TreatOut(NeighbourDoor door) => DeckDepth(door) + Tread + 0.25f;

        public static float ReceiveOut(NeighbourDoor door) => TreatOut(door) + WalkerClips.ReceiveDistance;

        /// <summary>The street's direction from the door (unit, world plan).</summary>
        public static void Outward(NeighbourDoor door, out float ox, out float oz)
        {
            double yaw = door.StandFacingYaw * Math.PI / 180.0;
            ox = -(float)Math.Sin(yaw);
            oz = -(float)Math.Cos(yaw);
        }

        /// <summary>The point <paramref name="outward"/> metres out from the door's centre, <paramref name="across"/> to the right.</summary>
        public static void Point(NeighbourDoor door, float outward, float across, out float x, out float z)
        {
            Outward(door, out float ox, out float oz);
            // Right of a visitor facing the door (heading -outward) is (-oz, ox).
            x = door.DoorX + ox * outward - oz * across;
            z = door.DoorZ + oz * outward + ox * across;
        }

        /// <summary>Feet height on the door's axis at <paramref name="outward"/> metres out: the deck, the tread, the ground.</summary>
        public static float GroundY(NeighbourDoor door, float outward)
        {
            float deck = DeckDepth(door);
            // Short ramps across each riser, so a walk down the step lifts and drops the body smoothly.
            if (outward <= deck - 0.06f)
            {
                return Sill;
            }

            if (outward <= deck + 0.06f)
            {
                return Sill - Rise * ((outward - (deck - 0.06f)) / 0.12f);
            }

            float tread = deck + Tread;
            if (outward <= tread - 0.06f)
            {
                return Sill - Rise;
            }

            if (outward <= tread + 0.06f)
            {
                return (Sill - Rise) * (1f - (outward - (tread - 0.06f)) / 0.12f);
            }

            return 0f;
        }

        /// <summary>Yaw facing the door from the street, and facing the street from the door.</summary>
        public static float FacingDoor(NeighbourDoor door) => door.StandFacingYaw;

        public static float FacingStreet(NeighbourDoor door) => (door.StandFacingYaw + 180f) % 360f;

        /// <summary>Where child <paramref name="index"/> of <paramref name="count"/> waits its turn: an arc behind the receive spot.</summary>
        public static void WaitSlot(NeighbourDoor door, int index, int count, out float x, out float z, out float yaw)
        {
            Arc(ReceiveOut(door), index, count, out float outward, out float across);
            Point(door, outward, across, out x, out z);
            Point(door, ReceiveOut(door) - WalkerClips.ReceiveDistance, 0f, out float fx, out float fz);
            yaw = Stride.YawTo(x, z, fx, fz);
        }

        /// <summary>The visiting group's grown-up waits behind the children.</summary>
        public static void GrownUpSlot(NeighbourDoor door, out float x, out float z, out float yaw)
        {
            Point(door, ReceiveOut(door) + GrownUpBack, GrownUpAcross, out x, out z);
            yaw = FacingDoor(door);
        }

        /// <summary>
        /// The arc's place <paramref name="index"/> of <paramref name="count"/> (0 the middle-most), as out and across
        /// from a centre at <paramref name="centreOut"/>: symmetric, <see cref="ArcStepDegrees"/> apart.
        /// </summary>
        public static void Arc(float centreOut, int index, int count, out float outward, out float across)
        {
            float first = -(count - 1) * 0.5f * ArcStepDegrees;
            double a = (first + index * ArcStepDegrees) * Math.PI / 180.0;
            outward = centreOut + ArcRadius * (float)Math.Cos(a);
            across = ArcRadius * (float)Math.Sin(a);
        }

        // --- the lit window beside the door: the hall light comes on, the neighbour's shadow crosses it -----------

        /// <summary>
        /// The lit ground-floor window nearest each door (house art: its room card is <c>RoomCard_Glow</c>): its centre
        /// across in the house's own frame (Unity x), its sill and top over the ground, its width, and the face of the
        /// wall it is in (house z). Mr. Fogg's front-room window with the paper ghost, Nana Pearl's right-hand window, the
        /// Abbotts' little window beside their door. <paramref name="towardDoor"/> is +1 when the door lies at larger x.
        /// </summary>
        public static void HallWindow(NeighbourDoor door, out float centre, out float bottom, out float top, out float width,
            out float wall, out float towardDoor)
        {
            switch (door.Id)
            {
                case LivingStreetLayout.Fogg:
                    centre = -2.15f;
                    bottom = Sill + 0.7f;
                    top = bottom + 1.35f;
                    width = 0.85f;
                    wall = -2.75f;
                    break;
                case LivingStreetLayout.Pearl:
                    centre = 2.55f;
                    bottom = Sill + 0.65f;
                    top = bottom + 1.3f;
                    width = 1.0f;
                    wall = -1.9f;
                    break;
                default:
                    centre = -2.98f;
                    bottom = Sill + 0.75f;
                    top = bottom + 1.1f;
                    width = 0.56f;
                    wall = -3.0f;
                    break;
            }

            towardDoor = door.DoorLocalX > centre ? 1f : -1f;
        }

        /// <summary>
        /// The neighbour's shadow behind the window's glass: a head and shoulders (0.46 m wide, from 1.35 m to the top of
        /// a 1.7 m grown-up standing on the floor at the sill) that crosses the window toward the door. Returns, for
        /// horizontal band <paramref name="band"/> of <see cref="SilhouetteBands"/>, its left and right edge in house x
        /// clipped to the window, and its bottom and top; false when the band shows nothing at <paramref name="progress"/>
        /// (0 entering at the far edge, 1 gone past the near edge).
        /// </summary>
        public static bool SilhouetteBand(NeighbourDoor door, float progress, int band, out float left, out float right,
            out float bottom, out float top)
        {
            HallWindow(door, out float centre, out float winBottom, out float winTop, out float width, out _, out float toward);
            const float shoulders = 0.46f;
            float floor = Sill;
            float headTop = floor + 1.72f;
            float neck = floor + 1.45f;
            float low = floor + 1.0f;
            float span = width + shoulders;
            float cx = centre - toward * span * 0.5f + toward * span * Clamp01(progress);
            float h0 = low + (headTop - low) * band / SilhouetteBands;
            float h1 = low + (headTop - low) * (band + 1) / SilhouetteBands;
            float mid = (h0 + h1) * 0.5f;
            float half;
            if (mid < neck - 0.08f)
            {
                // the shoulders round off toward the neck
                float u = (neck - 0.08f - mid) / (neck - 0.08f - low);
                half = shoulders * 0.5f * (0.82f + 0.18f * Math.Min(1f, u * 3f));
            }
            else if (mid < neck)
            {
                half = 0.07f;
            }
            else
            {
                // a round head, 0.24 m across
                float r = 0.12f;
                float cy = neck + r + 0.02f;
                float dy = Math.Abs(mid - cy);
                half = dy < r ? (float)Math.Sqrt(r * r - dy * dy) : 0f;
            }

            left = Math.Max(cx - half, centre - width * 0.5f);
            right = Math.Min(cx + half, centre + width * 0.5f);
            bottom = Math.Max(h0, winBottom);
            top = Math.Min(h1, winTop);
            return half > 0f && right > left && top > bottom;
        }

        public const int SilhouetteBands = 14;

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
