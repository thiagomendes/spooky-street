using System;
using SpookyStreet.Core.Movement;

namespace SpookyStreet.Core.Animation
{
    /// <summary>Small paper effects a motor step asks for (several can fire in one frame).</summary>
    [Flags]
    public enum MoveBursts
    {
        None = 0,

        /// <summary>Second chained jump: a small burst of paper confetti and stars at Pip's feet.</summary>
        ChainSecond = 1,

        /// <summary>Third chained jump: a bigger burst with more candy-corn stars.</summary>
        ChainThird = 2,

        /// <summary>Dive start: short paper speed streaks trailing behind Pip.</summary>
        DiveStreak = 4,

        /// <summary>A dive touching down into its roll: a dust puff.</summary>
        DiveDust = 8,

        /// <summary>
        /// The hold boost of a high jump engages: a small upward puff of paper stars and confetti under Pip's feet
        /// and a brief vertical speed line.
        /// </summary>
        BoostPuff = 16,

        /// <summary>A kick off a marked wall: a puff of paper confetti and stars bursting from the wall (S003).</summary>
        WallKickPuff = 32,

        /// <summary>A ledge caught: a small felt dust puff where the hands land on the edge (S003).</summary>
        LedgeGrabDust = 64,
    }

    /// <summary>
    /// Readability rules for the moveset (S000.6 feel pass): which paper effect each move plays and how much
    /// squash-and-stretch it gets. Negative squash values stretch Pip tall and thin, positive ones squash wide and
    /// flat (<see cref="SquashPulse.ToScale"/>). Chained jumps stretch more at each stage, and a held jump keeps an
    /// extra stretch while it rises and kicks a cape flare when its boost engages, so the hold reads differently from
    /// a tap (TestFlight 12.1: "holding or tapping, the jump is the same").
    /// </summary>
    public static class MoveFeedbackRules
    {
        /// <summary>Extra stretch held while Pip rises with Jump still held (eased in and out by the avatar).</summary>
        public const float HeldJumpStretch = -0.1f;

        /// <summary>How fast the held stretch eases in and out (units of squash per second).</summary>
        public const float HeldJumpStretchRate = 0.9f;

        /// <summary>Stretch kick when the hold boost engages: the cape flares up as Pip shoots up.</summary>
        public const float BoostFlareStretch = -0.12f;

        /// <summary>Most stretch kicks and the held stretch may add up to (tall about 1.42x: a stretch, not a distortion).</summary>
        public const float MaxStretch = -0.16f;

        /// <summary>A kick plus the held stretch, never stretched past <see cref="MaxStretch"/>.</summary>
        public static float CombinedSquash(float pulse, float held) => Math.Max(MaxStretch, pulse + held);

        /// <summary>Stretch kick when a dive starts (the cape snaps back).</summary>
        public const float DiveSnapStretch = -0.08f;

        /// <summary>Stretch kick when Pip kicks off a wall (S003): like a second chain jump.</summary>
        public const float WallKickStretch = -0.09f;

        /// <summary>Squash kick when Pip catches a ledge: her arms take the weight.</summary>
        public const float LedgeCatchSquash = 0.06f;

        /// <summary>Squash kick when a wall slide starts: the grip.</summary>
        public const float WallGripSquash = 0.04f;

        /// <summary>
        /// The hanging body leans this far toward the wall (degrees, about the feet), so the LedgeHang clip's raised
        /// hand meets the edge while her chest stays clear of the face (measured on the clip in Blender).
        /// </summary>
        public const float HangLeanDegrees = 10f;

        /// <summary>And slides this far toward the wall (metres) from the motor's hang spot.</summary>
        public const float HangShift = 0.1f;

        /// <summary>How fast the hang lean eases in and out (fraction per second).</summary>
        public const float HangLeanRate = 8f;

        /// <summary>Take-off stretch kick for chain stage 1, 2 or 3: it grows with the chain.</summary>
        public static float TakeOffStretch(int stage) => stage >= 3 ? -0.13f : stage == 2 ? -0.09f : -0.04f;

        /// <summary>Landing squash kick after a jump of chain stage 1, 2 or 3: it grows with the chain.</summary>
        public static float LandingSquash(int stage) => stage >= 3 ? 0.11f : stage == 2 ? 0.08f : 0.045f;

        /// <summary>
        /// Paper effects for this step. With <paramref name="reducedMotion"/> no bursts play (squash stays: it is
        /// part of the character's own motion, not decoration).
        /// </summary>
        public static MoveBursts BurstsFor(MotorStep step, bool reducedMotion)
        {
            if (reducedMotion)
            {
                return MoveBursts.None;
            }

            var bursts = MoveBursts.None;
            if (step.Jumped && step.ChainStage >= 3)
            {
                bursts |= MoveBursts.ChainThird;
            }
            else if (step.Jumped && step.ChainStage == 2)
            {
                bursts |= MoveBursts.ChainSecond;
            }

            if (step.DiveStarted)
            {
                bursts |= MoveBursts.DiveStreak;
            }

            if (step.DiveLanded)
            {
                bursts |= MoveBursts.DiveDust;
            }

            if (step.BoostStarted)
            {
                bursts |= MoveBursts.BoostPuff;
            }

            if (step.WallJumped)
            {
                bursts |= MoveBursts.WallKickPuff;
            }

            if (step.LedgeGrabbed)
            {
                bursts |= MoveBursts.LedgeGrabDust;
            }

            return bursts;
        }

        /// <summary>Pieces in one burst (kept small for mobile: at most 16 quads per effect).</summary>
        public static int PieceCount(MoveBursts burst)
        {
            switch (burst)
            {
                case MoveBursts.ChainSecond: return 10;
                case MoveBursts.ChainThird: return 16;
                case MoveBursts.DiveStreak: return 4;
                case MoveBursts.DiveDust: return 7;
                case MoveBursts.BoostPuff: return 8;
                case MoveBursts.WallKickPuff: return 9;
                case MoveBursts.LedgeGrabDust: return 4;
                default: return 0;
            }
        }
    }
}
