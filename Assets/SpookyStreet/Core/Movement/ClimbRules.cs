using System;

namespace SpookyStreet.Core.Movement
{
    /// <summary>
    /// What a surface lets Pip do (S003). Only surfaces marked in <c>CollisionRules.TraversalMarks</c> (or by a test
    /// rig) carry a mark; everything else, the house siding included, is <see cref="None"/> and never starts a wall
    /// slide, a wall jump or a ledge grab.
    /// </summary>
    [Flags]
    public enum SurfaceMarks
    {
        None = 0,

        /// <summary>A climbable wall: Pip slides down it and kicks off it.</summary>
        WallJump = 1,

        /// <summary>A grabbable ledge: Pip catches its top edge, hangs and climbs up.</summary>
        Ledge = 2,
    }

    /// <summary>
    /// The nearest marked wall beside Pip this frame (a probe at chest height; filled by the runtime, or by a test
    /// world). Pure data in Pip's own terms, so the rules need no engine.
    /// </summary>
    public struct WallProbe
    {
        /// <summary>A wall face spans Pip's chest height within the probe's reach.</summary>
        public bool Hit;

        /// <summary>The marks of the surface that was hit (None for any unmarked collider).</summary>
        public SurfaceMarks Marks;

        /// <summary>Unit horizontal normal of the face, pointing out of the wall toward Pip.</summary>
        public float NormalX;

        public float NormalZ;

        /// <summary>Horizontal metres from Pip's centre line to the face.</summary>
        public float Distance;

        /// <summary>Metres of air under Pip's feet (a large number when no ground is in reach).</summary>
        public float FeetClearance;
    }

    /// <summary>
    /// The nearest marked ledge in front of Pip this frame: its top edge relative to Pip's feet and the room around it.
    /// The runtime fills the room checks with capsule tests; a test world computes them from boxes.
    /// </summary>
    public struct LedgeProbe
    {
        public bool Hit;

        public SurfaceMarks Marks;

        /// <summary>Unit horizontal normal of the face under the edge, pointing out of the ledge toward Pip.</summary>
        public float NormalX;

        public float NormalZ;

        /// <summary>Horizontal metres from Pip's centre line to the edge (the face's plane).</summary>
        public float EdgeDistance;

        /// <summary>Height of the ledge's flat top above Pip's feet (metres).</summary>
        public float TopAbove;

        /// <summary>Pip fits standing on the top at the climb-up spot (<see cref="ClimbRules.ClimbTarget"/>).</summary>
        public bool RoomToClimb;

        /// <summary>Pip's body fits at the hang spot below the edge (<see cref="ClimbRules.HangSnap"/>).</summary>
        public bool RoomToHang;

        /// <summary>Metres of air under Pip's feet at the hang spot (a large number when no ground is in reach).</summary>
        public float HangClearance;
    }

    /// <summary>
    /// The engine-free rules of the wall slide, the wall jump and the ledge grab (S003): which probe results count,
    /// where Pip hangs, where the climb puts her and how hard a kick throws her. <see cref="MovementModel"/> applies
    /// them each step; the runtime only measures the probes. Audience 6–9 (D27): every window is generous.
    /// </summary>
    public static class ClimbRules
    {
        /// <summary>A marked wall Pip touches now (any approach), clear of the ground and not the wall she last kicked
        /// off while still above that kick (no climbing a single wall).</summary>
        public static bool WallUsable(MotorState state, MotorInput input, MovementTuningData tuning)
        {
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            WallProbe wall = input.Wall;
            if (!wall.Hit || (wall.Marks & SurfaceMarks.WallJump) == 0)
            {
                return false;
            }

            if (wall.Distance > tuning.BodyRadius + tuning.WallContactSlack ||
                wall.FeetClearance < tuning.WallSlideMinFeetHeight)
            {
                return false;
            }

            return !SameWallAboveKick(state, wall.NormalX, wall.NormalZ, input.FeetY);
        }

        /// <summary>
        /// True for the wall Pip last kicked off (same facing) while her feet are still above that kick: kicking or
        /// sliding there again would let her walk up one wall. Landing, or dropping below the kick, clears it.
        /// </summary>
        public static bool SameWallAboveKick(MotorState state, float normalX, float normalZ, float feetY) =>
            state.KickedWall && normalX * state.KickNormalX + normalZ * state.KickNormalZ > 0.7f &&
            feetY > state.KickFeetY - 0.05f;

        /// <summary>
        /// Pip starts sliding: airborne past coyote time, falling, on a usable wall and heading into it (the stick or
        /// her motion points at the face). A child who lets the stick go keeps sliding once started.
        /// </summary>
        public static bool CanStartWallSlide(MotorState state, MotorInput input, MovementTuningData tuning)
        {
            if (state.Grounded || state.WallSliding || state.Hanging || state.Climbing || state.Rolling ||
                state.TimeSinceGrounded <= tuning.CoyoteTime || state.VelocityY > 0f)
            {
                return false;
            }

            if (!WallUsable(state, input, tuning))
            {
                return false;
            }

            float intoX = -input.Wall.NormalX;
            float intoZ = -input.Wall.NormalZ;
            float stickInto = input.MoveX * intoX + input.MoveZ * intoZ;
            float moveInto = state.VelocityX * intoX + state.VelocityZ * intoZ;
            return stickInto >= tuning.WallSlideIntoMin || moveInto >= tuning.WallSlideIntoMin;
        }

        /// <summary>Downward speed limit of the slide <paramref name="slideTime"/> seconds in: a short grip, then the slide.</summary>
        public static float SlideSpeedLimit(float slideTime, MovementTuningData tuning) =>
            slideTime < tuning.WallSlideGripTime ? tuning.WallSlideGripSpeed : tuning.WallSlideSpeed;

        /// <summary>The kick off a wall whose outward normal is (<paramref name="normalX"/>, <paramref name="normalZ"/>):
        /// away from the wall at <see cref="MovementTuningData.WallJumpAwaySpeed"/>, up to
        /// <see cref="MovementTuningData.WallJumpHeight"/> above the kick under plain gravity.</summary>
        public static void KickVelocity(float normalX, float normalZ, MovementTuningData tuning, out float vx,
            out float vy, out float vz)
        {
            float length = (float)Math.Sqrt(normalX * normalX + normalZ * normalZ);
            if (length < 1e-4f)
            {
                normalX = 0f;
                normalZ = -1f;
                length = 1f;
            }

            vx = normalX / length * tuning.WallJumpAwaySpeed;
            vz = normalZ / length * tuning.WallJumpAwaySpeed;
            vy = (float)Math.Sqrt(2.0 * tuning.Gravity * Math.Max(0.01f, tuning.WallJumpHeight));
        }

        /// <summary>
        /// The ledge's top lies in the hands' catch window: from <see cref="MovementTuningData.LedgeGrabReachBelow"/>
        /// under the hanging hand height to <see cref="MovementTuningData.LedgeGrabReachAbove"/> over it.
        /// </summary>
        public static bool TopInReach(float topAbove, MovementTuningData tuning) =>
            topAbove >= tuning.LedgeHangHandHeight - tuning.LedgeGrabReachBelow &&
            topAbove <= tuning.LedgeHangHandHeight + tuning.LedgeGrabReachAbove;

        /// <summary>
        /// Pip catches the ledge: airborne past coyote time, not rising fast, a marked ledge whose top is in the catch
        /// window and whose edge is close in front (she faces it, steers or moves toward it), with room to hang under
        /// it, air under her feet there and room to stand on top.
        /// </summary>
        public static bool CanGrabLedge(MotorState state, MotorInput input, MovementTuningData tuning)
        {
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            if (state.Grounded || state.Hanging || state.Climbing || state.Rolling || state.LedgeRegrabLeft > 0f ||
                state.TimeSinceGrounded <= tuning.CoyoteTime || state.VelocityY > tuning.LedgeGrabMaxRiseSpeed)
            {
                return false;
            }

            LedgeProbe ledge = input.Ledge;
            if (!ledge.Hit || (ledge.Marks & SurfaceMarks.Ledge) == 0 || !ledge.RoomToClimb || !ledge.RoomToHang ||
                ledge.HangClearance < tuning.LedgeMinHangClearance)
            {
                return false;
            }

            if (!TopInReach(ledge.TopAbove, tuning) || ledge.EdgeDistance < 0f ||
                ledge.EdgeDistance > tuning.BodyRadius + tuning.LedgeGrabReachForward)
            {
                return false;
            }

            float intoX = -ledge.NormalX;
            float intoZ = -ledge.NormalZ;
            float facing = input.FacingX * intoX + input.FacingZ * intoZ;
            float stick = input.MoveX * intoX + input.MoveZ * intoZ;
            float motion = state.VelocityX * intoX + state.VelocityZ * intoZ;
            return facing >= 0.2f || stick >= 0.3f || motion >= 0.3f;
        }

        /// <summary>
        /// Displacement from where Pip is to the hang spot: <see cref="MovementTuningData.LedgeHangWallOffset"/> out from
        /// the face, hands at the top (feet <see cref="MovementTuningData.LedgeHangHandHeight"/> below it).
        /// </summary>
        public static void HangSnap(LedgeProbe ledge, MovementTuningData tuning, out float dx, out float dy, out float dz)
        {
            float toward = ledge.EdgeDistance - tuning.LedgeHangWallOffset;
            dx = -ledge.NormalX * toward;
            dz = -ledge.NormalZ * toward;
            dy = ledge.TopAbove - tuning.LedgeHangHandHeight;
        }

        /// <summary>
        /// Where the climb puts Pip's feet, from the hang spot: up by the hand height plus a small lift, then over the
        /// edge until her centre is <see cref="MovementTuningData.LedgeClimbInset"/> in from it.
        /// </summary>
        public static void ClimbTarget(float normalX, float normalZ, MovementTuningData tuning, out float dx,
            out float dy, out float dz)
        {
            float forward = tuning.LedgeHangWallOffset + tuning.LedgeClimbInset;
            dx = -normalX * forward;
            dz = -normalZ * forward;
            dy = tuning.LedgeHangHandHeight + tuning.LedgeClimbLift;
        }

        /// <summary>
        /// Progress of the climb at <paramref name="elapsed"/> seconds as (rise, forward) fractions: the rise eases over
        /// the first <see cref="MovementTuningData.LedgeClimbRiseFraction"/> of the climb, then Pip steps over.
        /// </summary>
        public static void ClimbProgress(float elapsed, MovementTuningData tuning, out float rise, out float forward)
        {
            float total = Math.Max(0.01f, tuning.LedgeClimbTime);
            float riseEnd = total * Math.Max(0.05f, Math.Min(0.95f, tuning.LedgeClimbRiseFraction));
            rise = Smooth(elapsed / riseEnd);
            forward = Smooth((elapsed - riseEnd) / (total - riseEnd));
        }

        /// <summary>The hang ends with a drop: the stick pulled down or pointed away from the wall.</summary>
        public static bool WantsDrop(MotorInput input, float normalX, float normalZ) =>
            input.StickY <= -0.5f || input.MoveX * normalX + input.MoveZ * normalZ >= 0.5f;

        private static float Smooth(float t)
        {
            t = t < 0f ? 0f : t > 1f ? 1f : t;
            return t * t * (3f - 2f * t);
        }
    }
}
