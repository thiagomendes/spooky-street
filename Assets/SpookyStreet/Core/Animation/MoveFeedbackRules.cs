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
    }

    /// <summary>
    /// Readability rules for the moveset (S000.6 feel pass): which paper effect each move plays and how much
    /// squash-and-stretch it gets. Negative squash values stretch Pip tall and thin, positive ones squash wide and
    /// flat (<see cref="SquashPulse.ToScale"/>). Chained jumps stretch more at each stage, and a held jump keeps a
    /// small extra stretch while it rises so the hold reads differently from a tap.
    /// </summary>
    public static class MoveFeedbackRules
    {
        /// <summary>Extra stretch held while Pip rises with Jump still held (eased in and out by the avatar).</summary>
        public const float HeldJumpStretch = -0.07f;

        /// <summary>How fast the held stretch eases in and out (units of squash per second).</summary>
        public const float HeldJumpStretchRate = 0.6f;

        /// <summary>Stretch kick when a dive starts (the cape snaps back).</summary>
        public const float DiveSnapStretch = -0.08f;

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
                default: return 0;
            }
        }
    }
}
