using System;
using System.Collections.Generic;
using SpookyStreet.Core.Movement;

namespace SpookyStreet.Core.Tips
{
    /// <summary>First-time moveset tips (S000.6 feel pass). Each one is shown at most once ever.</summary>
    public enum MoveTip
    {
        None,

        /// <summary>"Hold Jump to jump higher": the first short (released early) jump.</summary>
        HoldJump,

        /// <summary>"Jump again as you land!": the first landing while moving fast enough to chain.</summary>
        ChainJump,

        /// <summary>"Hold Jump while falling to glide": the first fall past a jump's apex without the cape.</summary>
        Glide,

        /// <summary>"Use in the air to dive": the first time Pip is airborne after having rolled.</summary>
        Dive,
    }

    /// <summary>Where "already shown" is remembered. Ids are stable strings, never display text.</summary>
    public interface ITipStore
    {
        bool HasShown(string tipId);

        void MarkShown(string tipId);
    }

    /// <summary>In-memory store (tests, and a fallback when no persistent store exists).</summary>
    public sealed class MemoryTipStore : ITipStore
    {
        private readonly HashSet<string> _shown = new HashSet<string>();

        public bool HasShown(string tipId) => _shown.Contains(tipId);

        public void MarkShown(string tipId) => _shown.Add(tipId);
    }

    public static class MoveTipIds
    {
        /// <summary>Stable persistence id of a tip (saved in preferences; never rename).</summary>
        public static string Id(MoveTip tip)
        {
            switch (tip)
            {
                case MoveTip.HoldJump: return "move.hold_jump";
                case MoveTip.ChainJump: return "move.chain_jump";
                case MoveTip.Glide: return "move.glide";
                case MoveTip.Dive: return "move.dive";
                default: return null;
            }
        }
    }

    /// <summary>
    /// Shows one tip at a time: a tip runs for <see cref="DisplaySeconds"/> or until its move is performed, then
    /// no tip may start for <see cref="CooldownSeconds"/>. Triggers that arrive while a tip is up, during the
    /// cooldown or while <see cref="Blocked"/> are dropped, not queued (a tip shown later would be out of context);
    /// the condition simply has to happen again. A tip is marked shown when it appears, and a move performed before
    /// its tip ever appeared marks it learned, so it never appears. Runs on game time: no ticks while paused.
    /// </summary>
    public sealed class TipScheduler
    {
        private readonly ITipStore _store;
        private float _cooldownLeft;

        public TipScheduler(ITipStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        /// <summary>Seconds a tip stays up when its move is not performed.</summary>
        public float DisplaySeconds = 3f;

        /// <summary>Quiet seconds after a tip ends before the next may start.</summary>
        public float CooldownSeconds = 2f;

        /// <summary>The tip on screen, or <see cref="MoveTip.None"/>.</summary>
        public MoveTip Current { get; private set; }

        /// <summary>Seconds the current tip has been up.</summary>
        public float CurrentAge { get; private set; }

        /// <summary>The tip ended because its move was performed (false when it timed out or none ended).</summary>
        public bool LastEndedByPerforming { get; private set; }

        /// <summary>While true (another prompt is on screen) no tip starts.</summary>
        public bool Blocked { get; set; }

        public bool HasShown(MoveTip tip) => tip != MoveTip.None && _store.HasShown(MoveTipIds.Id(tip));

        /// <summary>Starts <paramref name="tip"/> if nothing else is up, the cooldown is over and it was never shown.</summary>
        public bool TryShow(MoveTip tip)
        {
            if (tip == MoveTip.None || Blocked || Current != MoveTip.None || _cooldownLeft > 0f || HasShown(tip))
            {
                return false;
            }

            _store.MarkShown(MoveTipIds.Id(tip));
            Current = tip;
            CurrentAge = 0f;
            LastEndedByPerforming = false;
            return true;
        }

        /// <summary>The player did the move: its tip ends now, or will never be needed.</summary>
        public void Performed(MoveTip tip)
        {
            if (tip == MoveTip.None)
            {
                return;
            }

            if (Current == tip)
            {
                End(true);
                return;
            }

            if (!HasShown(tip))
            {
                _store.MarkShown(MoveTipIds.Id(tip));
            }
        }

        public void Tick(float deltaTime)
        {
            if (deltaTime <= 0f)
            {
                return;
            }

            if (Current != MoveTip.None)
            {
                CurrentAge += deltaTime;
                if (CurrentAge >= DisplaySeconds)
                {
                    End(false);
                }

                return;
            }

            _cooldownLeft = Math.Max(0f, _cooldownLeft - deltaTime);
        }

        /// <summary>Hides the current tip at once (e.g. leaving play); it stays marked as shown.</summary>
        public void Dismiss()
        {
            if (Current != MoveTip.None)
            {
                End(false);
            }
        }

        private void End(bool performed)
        {
            Current = MoveTip.None;
            CurrentAge = 0f;
            LastEndedByPerforming = performed;
            _cooldownLeft = CooldownSeconds;
        }
    }

    /// <summary>
    /// Turns motor steps into tip triggers and "performed" signals for a <see cref="TipScheduler"/>. Feed it every
    /// simulated step with the motor state after that step.
    /// <list type="bullet">
    /// <item>Hold Jump: shown when a jump is released while still rising (a short jump); performed by a jump held
    /// to its apex.</item>
    /// <item>Chain: shown on a landing at chain speed (not a dive landing, not already chaining); performed by a
    /// second or third chained jump.</item>
    /// <item>Glide: shown once a jump is falling at <see cref="GlideHintFallSpeed"/> past its apex without the
    /// cape; performed by spreading the cape.</item>
    /// <item>Dive: shown when Pip is airborne (past coyote time, no dive yet) after at least one roll; performed
    /// by a dive.</item>
    /// </list>
    /// </summary>
    public sealed class MoveTipCoach
    {
        /// <summary>Downward speed (m/s) past the apex at which the glide tip triggers (≈ 0.1 s of falling).</summary>
        public const float GlideHintFallSpeed = 2.5f;

        private bool _inJumpAir;
        private bool _releasedEarly;
        private bool _apexPassed;
        private bool _glidedThisAir;
        private bool _hasRolled;

        public MoveTipCoach(TipScheduler scheduler)
        {
            Scheduler = scheduler ?? throw new ArgumentNullException(nameof(scheduler));
        }

        public TipScheduler Scheduler { get; }

        public void Observe(MotorStep step, MotorState state, MovementTuningData tuning)
        {
            if (tuning == null)
            {
                throw new ArgumentNullException(nameof(tuning));
            }

            if (step.Jumped)
            {
                _inJumpAir = true;
                _releasedEarly = false;
                _apexPassed = false;
                _glidedThisAir = false;
                if (step.ChainStage >= 2)
                {
                    Scheduler.Performed(MoveTip.ChainJump);
                }
            }

            if (step.GlideStarted)
            {
                _glidedThisAir = true;
                Scheduler.Performed(MoveTip.Glide);
            }

            if (step.DiveStarted)
            {
                Scheduler.Performed(MoveTip.Dive);
            }

            if (step.RollStarted)
            {
                _hasRolled = true;
            }

            if (state.Grounded)
            {
                bool moving = state.HorizontalSpeed >= tuning.ChainJumpMinSpeedFraction * tuning.MaxSpeed - 1e-4f;
                if (step.Landed && !step.Jumped && !step.DiveLanded && !state.Rolling && moving)
                {
                    Scheduler.TryShow(MoveTip.ChainJump);
                }

                if (!step.Jumped)
                {
                    _inJumpAir = false;
                }

                return;
            }

            if (_inJumpAir && !state.Diving)
            {
                if (!_releasedEarly && !_apexPassed && state.InJump && state.JumpReleased && state.VelocityY > 0f)
                {
                    _releasedEarly = true;
                    Scheduler.TryShow(MoveTip.HoldJump);
                }

                if (!_apexPassed && state.VelocityY <= 0f)
                {
                    _apexPassed = true;
                    if (!_releasedEarly)
                    {
                        Scheduler.Performed(MoveTip.HoldJump);
                    }
                }

                if (_apexPassed && !_glidedThisAir && !state.Gliding && state.VelocityY <= -GlideHintFallSpeed)
                {
                    Scheduler.TryShow(MoveTip.Glide);
                }
            }

            if (_hasRolled && state.CanDive(tuning))
            {
                Scheduler.TryShow(MoveTip.Dive);
            }
        }
    }
}
