using System;
using SpookyStreet.Core.World;
using SpookyStreet.Player;
using UnityEngine;

namespace SpookyStreet.World
{
    /// <summary>
    /// Moves Pip between the street-scale owl house and its roomier interior (D24.1). Each frame the engine-free
    /// <see cref="ThresholdTracker"/> watches Pip's feet; walking into a usable exit zone (June's window always, the
    /// front door once the latch is lifted) starts the paper page-turn (<see cref="ThresholdWipe"/>,
    /// <see cref="ThresholdWipeView"/>). While the page covers the screen Pip is placed at the paired arrival point,
    /// facing into the new place, and <see cref="Moved"/> lets the owner cut the camera; then the page uncovers it.
    /// Gameplay input is ignored for the whole crossing (the owner disables it on <see cref="Started"/> and gives it
    /// back on <see cref="Finished"/>). Runs on game time, so a pause holds the page where it is.
    /// </summary>
    public sealed class ThresholdTravel : MonoBehaviour
    {
        private PipMotor _pip;

        public ThresholdTracker Tracker { get; } = new ThresholdTracker();

        public ThresholdWipe Wipe { get; } = new ThresholdWipe();

        public ThresholdWipeView View { get; private set; }

        /// <summary>Whether the front door is open (the latch lifted): the door works only then.</summary>
        public Func<bool> DoorOpen { get; set; }

        /// <summary>Raised when the page starts to turn, with the end Pip is leaving through.</summary>
        public event Action<ThresholdEnd> Started;

        /// <summary>Raised once, while the screen is covered, after Pip has been placed: (leaving, arriving).</summary>
        public event Action<ThresholdEnd, ThresholdEnd> Moved;

        /// <summary>Raised when the page has gone, with the end Pip arrived through.</summary>
        public event Action<ThresholdEnd> Finished;

        /// <summary>True from the first frame of the page-turn until the page has gone.</summary>
        public bool Active => Wipe.Active;

        /// <summary>The end being left during a crossing (null otherwise).</summary>
        public ThresholdEnd Leaving { get; private set; }

        /// <summary>The end arrived through by the last crossing.</summary>
        public ThresholdEnd LastArrival { get; private set; }

        /// <summary>Crossings since the scene loaded (tests).</summary>
        public int Crossings { get; private set; }

        public static ThresholdTravel Create(Transform parent, PipMotor pip)
        {
            var go = new GameObject("ThresholdTravel");
            go.transform.SetParent(parent, false);
            var travel = go.AddComponent<ThresholdTravel>();
            travel._pip = pip;
            travel.View = ThresholdWipeView.Create(go.transform);
            return travel;
        }

        /// <summary>Pip was placed somewhere (new game, resume, replay): no crossing until Pip steps clear of the zones.</summary>
        public void ResetTracker() => Tracker.Reset();

        /// <summary>
        /// One frame. <paramref name="canStart"/> is false while a conversation, the notebook or a cutaway owns the
        /// player (a running crossing always finishes).
        /// </summary>
        public void Tick(float dt, bool canStart)
        {
            if (_pip == null)
            {
                return;
            }

            if (Wipe.Active)
            {
                bool move = Wipe.Step(Mathf.Min(dt, 0.1f));
                if (move)
                {
                    Arrive();
                }

                View.Apply(Wipe);
                if (!Wipe.Active)
                {
                    Finish();
                }

                return;
            }

            if (!canStart)
            {
                return;
            }

            Vector3 p = _pip.transform.position;
            var exit = Tracker.Step(dt, p.x, p.y, p.z, DoorOpen != null && DoorOpen());
            if (exit != null)
            {
                Begin(exit);
            }
        }

        /// <summary>Starts a crossing through <paramref name="exit"/> (also used when Pip walks into its zone).</summary>
        public bool Begin(ThresholdEnd exit)
        {
            if (exit == null || Wipe.Active)
            {
                return false;
            }

            Leaving = exit;
            Wipe.Start(AmbientMotion.ReducedMotion);
            View.Show(exit.Id);
            View.Apply(Wipe);
            Started?.Invoke(exit);
            if (Debug.isDebugBuild)
            {
                Debug.Log($"[ThresholdTravel] Leaving through {exit} (reduced motion {Wipe.ReducedMotion})");
            }

            return true;
        }

        private void Arrive()
        {
            var arrive = Thresholds.Pair(Leaving);
            _pip.Teleport(new Vector3(arrive.ArriveX, arrive.ArriveY + 0.03f, arrive.ArriveZ));
            _pip.transform.rotation = Quaternion.Euler(0f, arrive.ArriveYaw, 0f);
            Tracker.Arrived();
            LastArrival = arrive;
            Crossings++;
            Moved?.Invoke(Leaving, arrive);
        }

        private void Finish()
        {
            View.Hide();
            var arrived = LastArrival;
            Leaving = null;
            Finished?.Invoke(arrived);
        }

        /// <summary>Drops a crossing at once (a reset to the title or a new game); Pip stays where it is.</summary>
        public void Cancel()
        {
            if (!Wipe.Active)
            {
                return;
            }

            Wipe.Cancel();
            View.Hide();
            var leaving = Leaving;
            Leaving = null;
            Finished?.Invoke(leaving);
        }
    }
}
