using NUnit.Framework;
using SpookyStreet.Core.Flow;

namespace SpookyStreet.Tests.Core
{
    public class GameFlowTests
    {
        [Test]
        public void StartsOnTitleWithSimulationStopped()
        {
            var flow = new GameFlow();
            Assert.AreEqual(GameState.Title, flow.State);
            Assert.IsFalse(flow.SimulationRunning);
        }

        [Test]
        public void TitleToPlayToPauseAndBack()
        {
            var flow = new GameFlow();
            Assert.IsTrue(flow.StartNewGame());
            Assert.IsTrue(flow.SimulationRunning);
            Assert.IsTrue(flow.Pause());
            Assert.AreEqual(GameState.Paused, flow.State);
            Assert.IsFalse(flow.SimulationRunning);
            Assert.IsTrue(flow.Resume());
            Assert.AreEqual(GameState.Playing, flow.State);
        }

        [Test]
        public void RepeatedRequestsDoNotDoubleApply()
        {
            var flow = new GameFlow();
            int changes = 0;
            flow.StateChanged += (_, __) => changes++;
            flow.StartNewGame();
            Assert.IsFalse(flow.StartNewGame());
            flow.Pause();
            Assert.IsFalse(flow.Pause());
            Assert.AreEqual(2, changes);
        }

        [Test]
        public void PauseIsIgnoredOnTitle()
        {
            var flow = new GameFlow();
            Assert.IsFalse(flow.TogglePause());
            Assert.IsFalse(flow.OnApplicationSuspended());
            Assert.AreEqual(GameState.Title, flow.State);
        }

        [Test]
        public void ReturningFromBackgroundOpensPaused()
        {
            var flow = new GameFlow();
            flow.StartNewGame();
            Assert.IsTrue(flow.OnApplicationSuspended());
            Assert.AreEqual(GameState.Paused, flow.State);
            Assert.IsFalse(flow.OnApplicationSuspended(), "a second suspend while paused is a no-op");
        }

        [Test]
        public void QuitToTitleOnlyFromPause()
        {
            var flow = new GameFlow();
            flow.StartNewGame();
            Assert.IsFalse(flow.QuitToTitle());
            flow.Pause();
            Assert.IsTrue(flow.QuitToTitle());
            Assert.AreEqual(GameState.Title, flow.State);
        }
    }
}
