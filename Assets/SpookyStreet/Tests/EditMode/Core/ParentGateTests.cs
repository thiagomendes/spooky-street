using NUnit.Framework;
using SpookyStreet.Core.Nonverbal;

namespace SpookyStreet.Tests.Core
{
    /// <summary>The parent gate (D23.5): hold two marked spots together for 3 s, simulated touches.</summary>
    public class ParentGateTests
    {
        private static readonly float AX = ParentGate.DefaultZoneA.CenterX, AY = ParentGate.DefaultZoneA.CenterY;
        private static readonly float BX = ParentGate.DefaultZoneB.CenterX, BY = ParentGate.DefaultZoneB.CenterY;

        private static void Run(ParentGate gate, float seconds, float step = 1f / 60f)
        {
            for (float t = 0f; t < seconds - 1e-5f; t += step)
            {
                gate.Update(step);
            }
        }

        [Test]
        public void PassesAfterHoldingBothSpotsForThreeSeconds()
        {
            var gate = new ParentGate();
            Assert.AreEqual(3.0f, gate.HoldSeconds);
            gate.TouchDown(1, AX, AY);
            Assert.AreEqual(ParentGateState.Waiting, gate.State);
            Assert.IsTrue(gate.IsZoneHeld(0));
            gate.TouchDown(2, BX, BY);
            Assert.AreEqual(ParentGateState.Holding, gate.State);
            Run(gate, 1.5f);
            Assert.AreEqual(0.5f, gate.Progress, 0.02f);
            Run(gate, 1.4f);
            Assert.AreEqual(ParentGateState.Holding, gate.State);
            Run(gate, 0.2f);
            Assert.AreEqual(ParentGateState.Passed, gate.State);
            Assert.AreEqual(1f, gate.Progress);
            Assert.IsTrue(gate.IsFinished);
        }

        [Test]
        public void OneSpotAloneNeverPasses()
        {
            var gate = new ParentGate();
            gate.TouchDown(1, AX, AY);
            gate.TouchDown(2, AX + 0.01f, AY);
            Run(gate, 5f);
            Assert.AreEqual(ParentGateState.Waiting, gate.State);
            Assert.AreEqual(0f, gate.Progress);
        }

        [Test]
        public void ReleasingEitherSpotResetsTheFill()
        {
            foreach (int release in new[] { 1, 2 })
            {
                var gate = new ParentGate();
                gate.TouchDown(1, AX, AY);
                gate.TouchDown(2, BX, BY);
                Run(gate, 2.5f);
                gate.TouchUp(release);
                Assert.AreEqual(ParentGateState.Waiting, gate.State);
                Assert.AreEqual(0f, gate.Progress, "released " + release);
                gate.TouchDown(release, release == 1 ? AX : BX, release == 1 ? AY : BY);
                Run(gate, 2.5f);
                Assert.AreEqual(ParentGateState.Holding, gate.State, "starts again from zero");
                Run(gate, 0.6f);
                Assert.AreEqual(ParentGateState.Passed, gate.State);
            }
        }

        [Test]
        public void SlidingOffASpotCountsAsReleaseAndSlidingOnHolds()
        {
            var gate = new ParentGate();
            gate.TouchDown(1, AX, AY);
            gate.TouchDown(2, 0.5f, 0.5f);
            Assert.AreEqual(ParentGateState.Waiting, gate.State);
            gate.TouchMove(2, BX, BY);
            Assert.AreEqual(ParentGateState.Holding, gate.State);
            Run(gate, 1f);
            gate.TouchMove(1, 0.5f, 0.1f);
            Assert.AreEqual(ParentGateState.Waiting, gate.State);
            Assert.AreEqual(0f, gate.Progress);
        }

        [Test]
        public void ASecondFingerOnTheSameSpotKeepsItHeld()
        {
            var gate = new ParentGate();
            gate.TouchDown(1, AX, AY);
            gate.TouchDown(3, AX, AY + 0.02f);
            gate.TouchDown(2, BX, BY);
            Run(gate, 1f);
            gate.TouchUp(1);
            Assert.AreEqual(ParentGateState.Holding, gate.State, "finger 3 still holds spot A");
            Run(gate, 2.1f);
            Assert.AreEqual(ParentGateState.Passed, gate.State);
        }

        [Test]
        public void SystemTouchCancelReleasesEverything()
        {
            var gate = new ParentGate();
            gate.TouchDown(1, AX, AY);
            gate.TouchDown(2, BX, BY);
            Run(gate, 2f);
            gate.ReleaseAll();
            Assert.AreEqual(ParentGateState.Waiting, gate.State);
            Assert.AreEqual(0f, gate.Progress);
        }

        [Test]
        public void TimesOutWhenNobodyPasses()
        {
            var gate = new ParentGate();
            gate.TouchDown(1, AX, AY);
            Run(gate, ParentGate.DefaultTimeoutSeconds + 0.1f, 0.5f);
            Assert.AreEqual(ParentGateState.TimedOut, gate.State);
            gate.TouchDown(2, BX, BY);
            Run(gate, 5f);
            Assert.AreEqual(ParentGateState.TimedOut, gate.State, "finished gates ignore input");
        }

        [Test]
        public void CancelEndsTheGate()
        {
            var gate = new ParentGate();
            gate.TouchDown(1, AX, AY);
            gate.TouchDown(2, BX, BY);
            Run(gate, 2.9f);
            gate.Cancel();
            Assert.AreEqual(ParentGateState.Cancelled, gate.State);
            Run(gate, 1f);
            Assert.AreEqual(ParentGateState.Cancelled, gate.State);
            gate.Cancel();
            gate.TouchUp(1);
            Assert.AreEqual(ParentGateState.Cancelled, gate.State);
        }

        [Test]
        public void ConfigurationIsValidated()
        {
            var a = new GateZone(0f, 0f, 0.5f, 0.5f);
            Assert.Throws<System.ArgumentException>(() => new ParentGate(a, new GateZone(0.4f, 0.4f, 0.9f, 0.9f), 3f, 30f));
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new ParentGate(a, new GateZone(0.6f, 0.6f, 0.9f, 0.9f), 3f, 2f));
            Assert.Throws<System.ArgumentException>(() => new GateZone(0.5f, 0f, 0.5f, 1f));
            Assert.IsFalse(ParentGate.DefaultZoneA.Overlaps(ParentGate.DefaultZoneB));
        }
    }
}
