using System;
using NUnit.Framework;
using SpookyStreet.Core.Animation;
using SpookyStreet.Core.Movement;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// S000.6 feel pass after the owner's TestFlight 11.1 feedback ("the other moves I couldn't do"): a longer chain
    /// window, a chain press accepted just before landing, a 25 % speed gate so a jog chains, a dive from any jump
    /// including a glide, and the readability rules (stretch, bursts). Flat ground at y = 0, as in MovesetTests.
    /// </summary>
    public class MovesetFeelTests
    {
        private sealed class Sim
        {
            public MotorState State = MotorState.Initial;
            public readonly MovementTuningData Tuning = new MovementTuningData();
            public readonly float Dt;
            public float Y;
            public bool Grounded = true;

            public Sim(float fps) => Dt = 1f / fps;

            public MotorStep Frame(MotorInput input)
            {
                input.GroundDistance = Grounded ? 0f : Math.Max(1e-3f, Y); // the motor's downward probe
                var step = MovementModel.Step(ref State, input, Grounded, Dt, Tuning);
                Y += step.DeltaY;
                if (Y <= 0f)
                {
                    Y = 0f;
                    Grounded = true;
                }
                else
                {
                    Grounded = false;
                }

                return step;
            }

            public void Run(float seconds, MotorInput input)
            {
                int frames = (int)Math.Round(seconds / Dt);
                for (int i = 0; i < frames; i++)
                {
                    Frame(input);
                }
            }

            /// <summary>Seconds until touchdown under plain gravity from the current height and fall speed.</summary>
            public float TimeToLand()
            {
                float down = -State.VelocityY;
                float g = Tuning.Gravity;
                return (-down + (float)Math.Sqrt(down * down + 2f * g * Y)) / g;
            }

            /// <summary>Jumps holding to the apex, then releases and falls until <see cref="TimeToLand"/> ≤ lead.</summary>
            public void JumpAndFallUntil(float moveX, float lead)
            {
                Frame(new MotorInput { MoveX = moveX, JumpPressed = true, JumpHeld = true });
                for (int i = 0; i < 600 && !(State.VelocityY < 0f && TimeToLand() <= lead); i++)
                {
                    Frame(new MotorInput { MoveX = moveX, JumpHeld = State.VelocityY > 0f });
                }
            }
        }

        private struct EarlyChain
        {
            public bool Jumped;
            public int Stage;
            public float Lead;
            public float Apex;
            public bool GlidedBeforeLanding;
        }

        /// <summary>
        /// Jogs at <paramref name="moveX"/>, jumps (held), presses Jump again <paramref name="lead"/> seconds before
        /// touchdown and keeps it held through the next rise, like a thumb on the Jump disc.
        /// </summary>
        private static EarlyChain PressBeforeLanding(float moveX, float lead, float fps = 60f)
        {
            var sim = new Sim(fps);
            sim.Run(0.5f, new MotorInput { MoveX = moveX });
            sim.JumpAndFallUntil(moveX, lead);
            var result = new EarlyChain();
            float sincePress = 0f;
            bool first = true;
            for (int i = 0; i < 600; i++)
            {
                var step = sim.Frame(new MotorInput { MoveX = moveX, JumpPressed = first, JumpHeld = true });
                first = false;
                sincePress += sim.Dt;
                if (step.Jumped)
                {
                    result.Jumped = true;
                    result.Stage = step.ChainStage;
                    result.Lead = sincePress - sim.Dt;
                    break;
                }

                result.GlidedBeforeLanding |= sim.State.Gliding;
                if (step.Landed && i > 0 && sim.State.TimeSinceLanded > sim.Tuning.ChainJumpWindow)
                {
                    break;
                }

                if (sim.Grounded && sincePress > lead + 0.5f)
                {
                    break;
                }
            }

            if (result.Jumped)
            {
                for (int i = 0; i < 600 && sim.State.VelocityY > 0f; i++)
                {
                    sim.Frame(new MotorInput { MoveX = moveX, JumpHeld = true });
                    result.Apex = Math.Max(result.Apex, sim.Y);
                }
            }

            return result;
        }

        // --- Tuning --------------------------------------------------------------------------------

        [Test]
        public void FeelPassTuningValues()
        {
            var t = new MovementTuningData();
            Assert.AreEqual(0.35f, t.ChainJumpWindow, 1e-6f, "chain window after landing");
            Assert.AreEqual(0.15f, t.JumpBufferTime, 1e-6f, "a press this early before landing still counts");
            Assert.AreEqual(0.25f, t.ChainJumpMinSpeedFraction, 1e-6f, "a light jog keeps the chain");
            Assert.AreEqual(1f, t.JumpApexHeight, 1e-6f, "tap height unchanged");
            Assert.AreEqual(2.1f, t.JumpHoldApexHeight, 1e-6f, "hold height raised after TestFlight 12.1");
        }

        // --- Buffered chain before landing --------------------------------------------------------

        [Test]
        public void ChainPressedJustBeforeLandingJumpsOnTouchdownAndChains([Values(30f, 60f)] float fps)
        {
            // A 1.6 m/s jog: 40 % of max speed, which the old 50 % gate refused.
            var early = PressBeforeLanding(0.4f, 0.1f, fps);
            Assert.IsTrue(early.Jumped, "the early press jumps on touchdown");
            Assert.AreEqual(2, early.Stage, "and continues the chain");
            Assert.That(early.Lead, Is.InRange(0.05f, 0.15f + 1e-3f), "pressed about 0.1 s before landing");
            Assert.That(early.Apex, Is.EqualTo(2.4f).Within(fps < 45f ? 0.05f : 0.02f), "second chain jump, held: 2.4 m");
            Assert.IsFalse(early.GlidedBeforeLanding, "a press about to become a jump never spreads the cape");
        }

        [Test]
        public void ChainPressUpToTheBufferBeforeLandingCountsButEarlierDoesNot()
        {
            // The motor sees the ground one frame after contact, so 0.13 s before contact is ~0.15 s of buffer.
            var edge = PressBeforeLanding(0.4f, 0.13f);
            Assert.IsTrue(edge.Jumped);
            Assert.AreEqual(2, edge.Stage, "0.13 s early still chains");

            var tooEarly = PressBeforeLanding(0.4f, 0.3f);
            Assert.IsFalse(tooEarly.Jumped, "a press 0.3 s early is not remembered until touchdown");
            Assert.IsTrue(tooEarly.GlidedBeforeLanding, "held that long in the air, it glides instead");
        }

        [Test]
        public void AFreshPressHighUpGlidesAtOnceAndKeepsGliding()
        {
            var sim = new Sim(60f);
            sim.Y = 6f;
            sim.Grounded = false;
            sim.State.Grounded = false;
            sim.State.TimeSinceGrounded = 1f;
            sim.State.VelocityY = -6f;
            var first = sim.Frame(new MotorInput { JumpPressed = true, JumpHeld = true });
            Assert.IsTrue(first.GlideStarted, "far from the ground a fresh hold spreads the cape at once");
            for (int i = 0; i < 90; i++)
            {
                sim.Frame(new MotorInput { JumpHeld = true });
                Assert.IsTrue(sim.State.Gliding, "a genuine hold keeps gliding past the buffer window");
            }
        }

        [Test]
        public void AFreshPressJustAboveTheGroundStaysAJumpNotAGlide()
        {
            var sim = new Sim(60f);
            sim.Y = 0.6f; // ≈ 0.08 s above the ground at 7 m/s
            sim.Grounded = false;
            sim.State.Grounded = false;
            sim.State.TimeSinceGrounded = 1f;
            sim.State.VelocityY = -7f;
            bool jumped = false;
            for (int i = 0; i < 20 && !jumped; i++)
            {
                var step = sim.Frame(new MotorInput { JumpPressed = i == 0, JumpHeld = true });
                Assert.IsFalse(sim.State.Gliding, "about to land: the press is a buffered jump");
                jumped = step.Jumped;
            }

            Assert.IsTrue(jumped, "and it jumps on touchdown");
            Assert.AreEqual(float.PositiveInfinity, MovementModel.TimeToGround(-5f, 0f, 25f), "unknown ground: far");
        }

        // --- Chain window and speed gate ----------------------------------------------------------

        private static int SecondStage(float moveX, float waitAfterLanding)
        {
            var sim = new Sim(60f);
            sim.Run(0.5f, new MotorInput { MoveX = moveX });
            sim.JumpAndFallUntil(moveX, 0f);
            for (int i = 0; i < 120 && !sim.Grounded; i++)
            {
                sim.Frame(new MotorInput { MoveX = moveX });
            }

            sim.Run(waitAfterLanding, new MotorInput { MoveX = moveX });
            var step = sim.Frame(new MotorInput { MoveX = moveX, JumpPressed = true, JumpHeld = true });
            Assert.IsTrue(step.Jumped);
            return step.ChainStage;
        }

        [Test]
        public void ChainWindowIsAboutAThirdOfASecond()
        {
            Assert.AreEqual(2, SecondStage(1f, 0.3f), "0.3 s after landing still chains");
            Assert.AreEqual(1, SecondStage(1f, 0.42f), "0.42 s after landing starts over");
        }

        [Test]
        public void AQuarterOfMaxSpeedChains()
        {
            Assert.AreEqual(2, SecondStage(0.3f, 0f), "1.2 m/s (30 %) chains");
            Assert.AreEqual(2, SecondStage(0.26f, 0f), "just over 25 % chains");
            Assert.AreEqual(1, SecondStage(0.2f, 0f), "0.8 m/s (20 %) does not");
        }

        // --- Dive from any jump, including a glide --------------------------------------------------

        [Test]
        public void UseWhileGlidingFoldsTheCapeIntoADive()
        {
            var sim = new Sim(60f);
            sim.Run(0.5f, new MotorInput { MoveX = 1f });
            sim.Frame(new MotorInput { MoveX = 1f, JumpPressed = true, JumpHeld = true });
            for (int i = 0; i < 120 && !sim.State.Gliding; i++)
            {
                sim.Frame(new MotorInput { MoveX = 1f, JumpHeld = true });
            }

            Assert.IsTrue(sim.State.Gliding, "held through the apex: gliding");
            Assert.AreEqual(UseAction.Dive, MovementModel.ResolveUse(sim.State, default, sim.Tuning));
            Assert.IsTrue(sim.State.CanDive(sim.Tuning), "the Use disc would read Dive");
            var dive = sim.Frame(new MotorInput { MoveX = 1f, JumpHeld = true, UsePressed = true });
            Assert.IsTrue(dive.DiveStarted, "Use mid-glide dives");
            Assert.IsTrue(dive.GlideEnded, "and folds the cape the same frame");
            Assert.IsFalse(sim.State.Gliding);
            Assert.IsTrue(sim.State.Diving);
            Assert.That(sim.State.HorizontalSpeed, Is.GreaterThanOrEqualTo(sim.Tuning.DiveSpeed - 1e-3f));

            bool landedInRoll = false;
            for (int i = 0; i < 300; i++)
            {
                var step = sim.Frame(new MotorInput { MoveX = 1f, JumpHeld = true });
                Assert.IsFalse(step.GlideStarted, "still holding Jump, the dive is not turned back into a glide");
                if (step.DiveLanded)
                {
                    landedInRoll = step.RollStarted;
                    break;
                }
            }

            Assert.IsTrue(landedInRoll, "the dive lands in a roll");
        }

        [Test]
        public void DiveWorksFromATapAHoldAChainJumpAndAFall([Values(0, 1, 2, 3)] int kind)
        {
            var sim = new Sim(60f);
            sim.Run(0.5f, new MotorInput { MoveX = 1f });
            switch (kind)
            {
                case 0: // tapped jump
                    sim.Frame(new MotorInput { MoveX = 1f, JumpPressed = true });
                    break;
                case 1: // held jump
                    sim.Frame(new MotorInput { MoveX = 1f, JumpPressed = true, JumpHeld = true });
                    sim.Run(0.3f, new MotorInput { MoveX = 1f, JumpHeld = true });
                    break;
                case 2: // second chain jump
                    sim.JumpAndFallUntil(1f, 0.05f);
                    bool chained = sim.Frame(new MotorInput { MoveX = 1f, JumpPressed = true, JumpHeld = true }).Jumped;
                    for (int i = 0; i < 30 && !chained; i++)
                    {
                        chained = sim.Frame(new MotorInput { MoveX = 1f, JumpHeld = true }).Jumped;
                    }

                    Assert.IsTrue(chained);
                    Assert.AreEqual(2, sim.State.ChainStage);
                    break;
                default: // walked off a ledge
                    sim.Y = 2f;
                    sim.Grounded = false;
                    break;
            }

            sim.Run(0.15f, new MotorInput { MoveX = 1f });
            Assert.IsFalse(sim.State.Grounded);
            var dive = sim.Frame(new MotorInput { MoveX = 1f, UsePressed = true });
            Assert.IsTrue(dive.DiveStarted);
        }

        // --- Readability ---------------------------------------------------------------------------

        [Test]
        public void HeldJumpRiseIsFlaggedOnlyWhileHeldAndRising()
        {
            var sim = new Sim(60f);
            sim.Frame(new MotorInput { JumpPressed = true, JumpHeld = true });
            Assert.IsTrue(sim.State.HoldingJumpRise);
            sim.Run(0.1f, new MotorInput { JumpHeld = true });
            Assert.IsTrue(sim.State.HoldingJumpRise);
            sim.Frame(default);
            Assert.IsFalse(sim.State.HoldingJumpRise, "released: a tap-height jump, no hold stretch");

            var tap = new Sim(60f);
            tap.Frame(new MotorInput { JumpPressed = true, JumpHeld = false });
            Assert.IsFalse(tap.State.HoldingJumpRise);
            Assert.Less(MoveFeedbackRules.HeldJumpStretch, 0f, "the held jump stretches Pip");
        }

        [Test]
        public void ChainStretchAndSquashGrowWithEachStage()
        {
            Assert.Less(MoveFeedbackRules.TakeOffStretch(2), MoveFeedbackRules.TakeOffStretch(1));
            Assert.Less(MoveFeedbackRules.TakeOffStretch(3), MoveFeedbackRules.TakeOffStretch(2));
            Assert.Less(MoveFeedbackRules.TakeOffStretch(1), 0f);
            Assert.Greater(MoveFeedbackRules.LandingSquash(2), MoveFeedbackRules.LandingSquash(1));
            Assert.Greater(MoveFeedbackRules.LandingSquash(3), MoveFeedbackRules.LandingSquash(2));
            SquashPulse.ToScale(MoveFeedbackRules.TakeOffStretch(3), out float wide, out float tall);
            Assert.Less(wide, 1f);
            Assert.Greater(tall, 1f);
            Assert.Less(tall, 1.35f, "a stretch, not a distortion");
        }

        [Test]
        public void BurstsFollowTheMoveAndReducedMotionSkipsThem()
        {
            Assert.AreEqual(MoveBursts.None, MoveFeedbackRules.BurstsFor(new MotorStep { Jumped = true, ChainStage = 1 }, false));
            Assert.AreEqual(MoveBursts.ChainSecond,
                MoveFeedbackRules.BurstsFor(new MotorStep { Jumped = true, ChainStage = 2 }, false));
            Assert.AreEqual(MoveBursts.ChainThird,
                MoveFeedbackRules.BurstsFor(new MotorStep { Jumped = true, ChainStage = 3 }, false));
            Assert.AreEqual(MoveBursts.DiveStreak, MoveFeedbackRules.BurstsFor(new MotorStep { DiveStarted = true }, false));
            Assert.AreEqual(MoveBursts.DiveDust,
                MoveFeedbackRules.BurstsFor(new MotorStep { DiveLanded = true, RollStarted = true, Landed = true }, false));
            Assert.AreEqual(MoveBursts.None, MoveFeedbackRules.BurstsFor(new MotorStep { RollStarted = true }, false),
                "a plain ground roll has its own squash, no burst");
            Assert.AreEqual(MoveBursts.None,
                MoveFeedbackRules.BurstsFor(new MotorStep { Jumped = true, ChainStage = 3, DiveStarted = true }, true),
                "reduced motion: no bursts");
            Assert.LessOrEqual(MoveFeedbackRules.PieceCount(MoveBursts.ChainThird), 16, "cheap on mobile");
            Assert.Greater(MoveFeedbackRules.PieceCount(MoveBursts.ChainThird),
                MoveFeedbackRules.PieceCount(MoveBursts.ChainSecond), "the third jump bursts bigger");
        }
    }
}
