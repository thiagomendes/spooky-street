using System;

namespace SpookyStreet.Core.Flow
{
    /// <summary>Top-level application states for the walking skeleton.</summary>
    public enum GameState
    {
        Title,
        Playing,
        Paused,
    }

    /// <summary>
    /// Explicit, engine-free state machine for title → play → pause.
    /// Every transition is idempotent: an invalid request returns false and changes nothing,
    /// so repeated button presses or lifecycle events can never double-apply.
    /// </summary>
    public sealed class GameFlow
    {
        public GameState State { get; private set; } = GameState.Title;

        /// <summary>Raised after a state change with (previous, current).</summary>
        public event Action<GameState, GameState> StateChanged;

        /// <summary>Gameplay input and simulation run only while playing.</summary>
        public bool SimulationRunning => State == GameState.Playing;

        public bool StartNewGame() => TryTransition(GameState.Title, GameState.Playing);

        public bool Pause() => TryTransition(GameState.Playing, GameState.Paused);

        public bool Resume() => TryTransition(GameState.Paused, GameState.Playing);

        public bool QuitToTitle() => TryTransition(GameState.Paused, GameState.Title);

        /// <summary>Pause toggles between playing and paused; ignored on the title screen.</summary>
        public bool TogglePause() => State == GameState.Playing ? Pause() : Resume();

        /// <summary>
        /// The app lost focus or went to the background. Spec §5: returning from background opens paused.
        /// </summary>
        public bool OnApplicationSuspended() => Pause();

        private bool TryTransition(GameState from, GameState to)
        {
            if (State != from)
            {
                return false;
            }

            State = to;
            StateChanged?.Invoke(from, to);
            return true;
        }
    }
}
