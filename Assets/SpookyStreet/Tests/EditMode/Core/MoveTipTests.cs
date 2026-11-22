using System;
using NUnit.Framework;
using SpookyStreet.Core.Localization;
using SpookyStreet.Core.Movement;
using SpookyStreet.Core.Tips;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// First-time moveset tips (S000.6 feel pass): the scheduler shows each tip once ever, one at a time, with a
    /// cooldown, ending after ~3 s or when the move is performed; the coach turns motor steps into the triggers.
    /// </summary>
    public class MoveTipTests
    {
        // --- Scheduler -------------------------------------------------------------------------------

        [Test]
        public void EachTipShowsOnceEverAcrossSessions()
        {
            var store = new MemoryTipStore();
            var first = new TipScheduler(store);
            Assert.IsTrue(first.TryShow(MoveTip.Glide));
            Assert.AreEqual(MoveTip.Glide, first.Current);
            first.Tick(first.DisplaySeconds + 0.01f);
            first.Tick(first.CooldownSeconds + 0.01f);
            Assert.IsFalse(first.TryShow(MoveTip.Glide), "never twice");

            var nextLaunch = new TipScheduler(store);
            Assert.IsFalse(nextLaunch.TryShow(MoveTip.Glide), "remembered by the store, not the scheduler");
            Assert.IsTrue(nextLaunch.TryShow(MoveTip.Dive));
            Assert.IsTrue(store.HasShown(MoveTipIds.Id(MoveTip.Glide)));
        }

        [Test]
        public void OneTipAtATimeAndTriggersWhileBusyAreDropped()
        {
            var scheduler = new TipScheduler(new MemoryTipStore());
            Assert.IsTrue(scheduler.TryShow(MoveTip.HoldJump));
            Assert.IsFalse(scheduler.TryShow(MoveTip.Glide), "another tip is up");
            Assert.AreEqual(MoveTip.HoldJump, scheduler.Current);
            Assert.IsFalse(scheduler.HasShown(MoveTip.Glide), "a dropped trigger is not marked shown");
        }

        [Test]
        public void TipFadesAfterAboutThreeSecondsThenACooldown()
        {
            var scheduler = new TipScheduler(new MemoryTipStore());
            Assert.That(scheduler.DisplaySeconds, Is.InRange(2.5f, 3.5f));
            Assert.That(scheduler.CooldownSeconds, Is.InRange(1f, 4f));
            scheduler.TryShow(MoveTip.ChainJump);
            scheduler.Tick(2.9f);
            Assert.AreEqual(MoveTip.ChainJump, scheduler.Current, "still up just before 3 s");
            scheduler.Tick(0.2f);
            Assert.AreEqual(MoveTip.None, scheduler.Current, "gone after 3 s");
            Assert.IsFalse(scheduler.LastEndedByPerforming);

            Assert.IsFalse(scheduler.TryShow(MoveTip.Glide), "cooldown");
            scheduler.Tick(scheduler.CooldownSeconds - 0.1f);
            Assert.IsFalse(scheduler.TryShow(MoveTip.Glide), "still cooling down");
            scheduler.Tick(0.2f);
            Assert.IsTrue(scheduler.TryShow(MoveTip.Glide), "after the cooldown the next tip may show");
        }

        [Test]
        public void PerformingTheMoveEndsItsTipAndStartsTheCooldown()
        {
            var scheduler = new TipScheduler(new MemoryTipStore());
            scheduler.TryShow(MoveTip.Glide);
            scheduler.Performed(MoveTip.Dive);
            Assert.AreEqual(MoveTip.Glide, scheduler.Current, "another move does not end it");
            scheduler.Tick(0.5f);
            scheduler.Performed(MoveTip.Glide);
            Assert.AreEqual(MoveTip.None, scheduler.Current);
            Assert.IsTrue(scheduler.LastEndedByPerforming);
            Assert.IsFalse(scheduler.TryShow(MoveTip.HoldJump), "the cooldown follows a performed tip too");
        }

        [Test]
        public void AMovePerformedBeforeItsTipMarksItLearned()
        {
            var scheduler = new TipScheduler(new MemoryTipStore());
            scheduler.Performed(MoveTip.Dive);
            Assert.IsTrue(scheduler.HasShown(MoveTip.Dive));
            Assert.IsFalse(scheduler.TryShow(MoveTip.Dive), "already knows it: never shown");
        }

        [Test]
        public void BlockedSchedulerShowsNothingAndPauseFreezesIt()
        {
            var scheduler = new TipScheduler(new MemoryTipStore()) { Blocked = true };
            Assert.IsFalse(scheduler.TryShow(MoveTip.HoldJump), "another prompt is up");
            scheduler.Blocked = false;
            Assert.IsTrue(scheduler.TryShow(MoveTip.HoldJump));
            scheduler.Tick(0f);
            scheduler.Tick(-1f);
            Assert.AreEqual(0f, scheduler.CurrentAge, "no game time passes while paused");
        }

        [Test]
        public void TipIdsAreStableAndEveryTipHasALocalizedLine()
        {
            Assert.AreEqual("move.hold_jump", MoveTipIds.Id(MoveTip.HoldJump));
            Assert.AreEqual("move.chain_jump", MoveTipIds.Id(MoveTip.ChainJump));
            Assert.AreEqual("move.glide", MoveTipIds.Id(MoveTip.Glide));
            Assert.AreEqual("move.dive", MoveTipIds.Id(MoveTip.Dive));
            Assert.IsNull(MoveTipIds.Id(MoveTip.None));

            var english = StringTable.Parse(LocalizationTests.BundledEnglishJson());
            foreach (string key in new[]
                     {
                         LocalizationKeys.TipHoldJump, LocalizationKeys.TipChainJump, LocalizationKeys.TipGlide,
                         LocalizationKeys.TipDive, LocalizationKeys.HudDive,
                     })
            {
                Assert.IsTrue(english.TryGet(key, out string value), key);
                Assert.That(value.Length, Is.InRange(3, 36), key);
                CollectionAssert.Contains(LocalizationKeys.All, key);
            }

            Assert.IsTrue(english.TryGet(LocalizationKeys.TipHoldJump, out string hold));
            Assert.AreEqual("Keep holding Jump to go much higher", hold);
        }

        // --- Coach: motor steps → triggers ----------------------------------------------------------

        private sealed class Sim
        {
            public MotorState State = MotorState.Initial;
            public readonly MovementTuningData Tuning = new MovementTuningData();
            public readonly MoveTipCoach Coach = new MoveTipCoach(new TipScheduler(new MemoryTipStore()));
            public float Y;
            public bool Grounded = true;
            private const float Dt = 1f / 60f;

            public TipScheduler Tips => Coach.Scheduler;

            public MotorStep Frame(MotorInput input)
            {
                input.GroundDistance = Grounded ? 0f : Math.Max(1e-3f, Y); // the motor's downward probe
                var step = MovementModel.Step(ref State, input, Grounded, Dt, Tuning);
                Y += step.DeltaY;
                Grounded = Y <= 0f;
                if (Grounded)
                {
                    Y = 0f;
                }

                Coach.Observe(step, State, Tuning);
                Tips.Tick(Dt);
                return step;
            }

            public void Run(float seconds, MotorInput input, Func<bool> until = null)
            {
                for (int i = 0; i < (int)Math.Round(seconds * 60f); i++)
                {
                    Frame(input);
                    if (until != null && until())
                    {
                        return;
                    }
                }
            }
        }

        [Test]
        public void AShortJumpShowsHoldJump()
        {
            var sim = new Sim();
            sim.Frame(new MotorInput { JumpPressed = true, JumpHeld = true });
            Assert.AreEqual(MoveTip.None, sim.Tips.Current, "nothing while still held");
            sim.Frame(default); // released while rising
            Assert.AreEqual(MoveTip.HoldJump, sim.Tips.Current);
        }

        [Test]
        public void AFullHeldJumpCountsAsKnowingHoldJump()
        {
            var sim = new Sim();
            sim.Frame(new MotorInput { JumpPressed = true, JumpHeld = true });
            sim.Run(0.5f, new MotorInput { JumpHeld = true }, () => sim.State.VelocityY <= 0f);
            Assert.IsTrue(sim.Tips.HasShown(MoveTip.HoldJump), "held to the apex: learned, never shown");
            Assert.AreNotEqual(MoveTip.HoldJump, sim.Tips.Current);
        }

        [Test]
        public void FallingPastTheApexWithoutTheCapeShowsGlideAndGlidingEndsIt()
        {
            var sim = new Sim();
            sim.Frame(new MotorInput { JumpPressed = true, JumpHeld = true });
            sim.Run(0.5f, new MotorInput { JumpHeld = true }, () => sim.State.VelocityY <= 0f);
            // Let go at the apex: Pip falls without the cape.
            sim.Run(0.4f, default, () => sim.Tips.Current != MoveTip.None);
            Assert.AreEqual(MoveTip.Glide, sim.Tips.Current);
            Assert.Less(sim.State.VelocityY, -MoveTipCoach.GlideHintFallSpeed + 0.5f);
            // Holding again (no fresh press) spreads the cape once the apex hang has passed (TestFlight 12.1).
            sim.Run(0.1f, new MotorInput { JumpHeld = true }, () => sim.State.Gliding);
            Assert.IsTrue(sim.State.Gliding, "holding again spreads the cape");
            Assert.AreEqual(MoveTip.None, sim.Tips.Current, "performed: the tip goes");
            Assert.IsTrue(sim.Tips.LastEndedByPerforming);
        }

        [Test]
        public void GlidingBeforeTheTipNeverShowsIt()
        {
            var sim = new Sim();
            sim.Frame(new MotorInput { JumpPressed = true, JumpHeld = true });
            sim.Run(2.5f, new MotorInput { JumpHeld = true }, () => sim.Grounded);
            Assert.IsTrue(sim.Tips.HasShown(MoveTip.Glide), "glided on its own: learned");
            Assert.AreEqual(MoveTip.None, sim.Tips.Current);
        }

        [Test]
        public void LandingWhileMovingShowsChainJumpAndChainingEndsIt()
        {
            var sim = new Sim();
            var jog = new MotorInput { MoveX = 0.5f };
            sim.Run(0.5f, jog);
            sim.Frame(new MotorInput { MoveX = 0.5f, JumpPressed = true, JumpHeld = true });
            bool landed = false;
            for (int i = 0; i < 180 && !landed; i++)
            {
                // Held all the way (a glide), so the glide tip is learned and not the one on screen at touchdown.
                landed = sim.Frame(new MotorInput { MoveX = 0.5f, JumpHeld = true }).Landed;
            }

            Assert.IsTrue(landed);
            Assert.AreEqual(MoveTip.ChainJump, sim.Tips.Current, "landed at a jog");
            var chain = sim.Frame(new MotorInput { MoveX = 0.5f, JumpPressed = true, JumpHeld = true });
            Assert.AreEqual(2, chain.ChainStage);
            Assert.AreEqual(MoveTip.None, sim.Tips.Current, "chained: the tip goes");
        }

        [Test]
        public void LandingStandingStillDoesNotShowChainJump()
        {
            var sim = new Sim();
            sim.Frame(new MotorInput { JumpPressed = true, JumpHeld = true });
            sim.Run(2.5f, new MotorInput { JumpHeld = true }, () => sim.Grounded);
            Assert.AreNotEqual(MoveTip.ChainJump, sim.Tips.Current);
            Assert.IsFalse(sim.Tips.HasShown(MoveTip.ChainJump));
        }

        [Test]
        public void AirborneAfterARollShowsDiveAndDivingEndsIt()
        {
            var sim = new Sim();
            var run = new MotorInput { MoveX = 1f };
            sim.Run(0.5f, run);
            sim.Frame(new MotorInput { MoveX = 1f, JumpPressed = true, JumpHeld = true });
            sim.Run(2.5f, new MotorInput { MoveX = 1f, JumpHeld = true }, () => sim.Grounded);
            sim.Run(0.1f, run);
            Assert.AreNotEqual(MoveTip.Dive, sim.Tips.Current, "no roll yet: no dive tip");

            sim.Frame(new MotorInput { MoveX = 1f, UsePressed = true });
            Assert.IsTrue(sim.State.Rolling);
            sim.Run(1f, run, () => !sim.State.Rolling);
            sim.Run(3f, run); // let any earlier tip and its cooldown pass
            sim.Tips.Dismiss();
            sim.Tips.Tick(sim.Tips.CooldownSeconds + 0.1f);

            sim.Frame(new MotorInput { MoveX = 1f, JumpPressed = true });
            sim.Run(0.3f, run, () => sim.Tips.Current == MoveTip.Dive);
            Assert.AreEqual(MoveTip.Dive, sim.Tips.Current, "airborne after having rolled");
            Assert.IsTrue(sim.Frame(new MotorInput { MoveX = 1f, UsePressed = true }).DiveStarted);
            Assert.AreEqual(MoveTip.None, sim.Tips.Current, "dived: the tip goes");
        }
    }
}
