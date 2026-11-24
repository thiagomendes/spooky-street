using System;
using System.Collections.Generic;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.Case;
using SpookyStreet.Player;
using UnityEngine;

namespace SpookyStreet.Case
{
    /// <summary>
    /// The case's <see cref="IUseTargetSource"/>: each frame the director ticks it with Pip's position and the camera
    /// heading, and it selects the authored <see cref="InteractionPoint"/> in reach (spec §5: ~1.6 m, in sight,
    /// weighted toward the camera centre). Use then reports that point's <see cref="CaseAction"/>; the Use disc shows
    /// its verb (<see cref="CaseInteractions.VerbKey"/>). Suspended while a modal is open, so selection clears on
    /// menus and dialogue.
    /// </summary>
    public sealed class CaseUseTargets : IUseTargetSource
    {
        private readonly List<InteractionPoint> _points;
        private readonly List<InteractionPoint> _offered = new List<InteractionPoint>();
        private readonly Action<InteractionPoint> _interact;
        private readonly int _sightMask;

        public CaseUseTargets(IEnumerable<InteractionPoint> points, Action<InteractionPoint> interact, int sightMask)
        {
            _points = new List<InteractionPoint>(points);
            _interact = interact;
            _sightMask = sightMask;
        }

        /// <summary>The selected target (null when none is in range).</summary>
        public InteractionPoint Current { get; private set; }

        /// <summary>True while a modal screen or a cool-down blocks interaction.</summary>
        public bool Suspended { get; set; }

        public IReadOnlyList<InteractionPoint> Points => _points;

        /// <summary>Which points exist right now (the world's state: June's mark, the panel, the latch). Null: all.</summary>
        public Func<InteractionPoint, bool> Filter { get; set; }

        public bool HasTarget => !Suspended && Current != null;

        public string VerbKey => HasTarget ? VerbFor?.Invoke(Current) ?? Current.VerbKey : null;

        /// <summary>A verb the world's state gives a point instead of its own (null: the point's; S004 the booth's knock).</summary>
        public Func<InteractionPoint, string> VerbFor { get; set; }

        public string IconName => HasTarget ? IconFor(Current) : null;

        public static string IconFor(InteractionPoint point) =>
            point != null && point.Action.Kind == CaseActionKind.Talk ? UiSpriteCatalog.IconTalk : UiSpriteCatalog.IconExamine;

        /// <summary>World position of a point's floating marker.</summary>
        public static Vector3 MarkerOf(InteractionPoint point) =>
            new Vector3(point.Position.X, point.Position.Y + point.MarkerHeight, point.Position.Z);

        public void Interact()
        {
            if (HasTarget)
            {
                _interact?.Invoke(Current);
            }
        }

        public void Tick(Vector3 pip, Vector3 cameraForward)
        {
            _offered.Clear();
            foreach (var point in _points)
            {
                if (Filter == null || Filter(point))
                {
                    _offered.Add(point);
                }
            }

            int index = UseTargetPicker.Pick(pip.x, pip.y, pip.z, cameraForward.x, cameraForward.z, _offered);
            var picked = index >= 0 ? _offered[index] : null;
            if (picked != null && !InSight(pip, picked))
            {
                picked = null;
            }

            Current = picked;
        }

        /// <summary>Nothing solid between Pip's chest and the target's marker (ignores triggers and Pip).</summary>
        private bool InSight(Vector3 pip, InteractionPoint point)
        {
            Vector3 from = pip + Vector3.up * 0.9f;
            Vector3 to = MarkerOf(point);
            Vector3 delta = to - from;
            float distance = delta.magnitude;
            if (distance < 0.05f)
            {
                return true;
            }

            // Stop short of the target itself: its own collider (Bea, the bench, the door) is not an obstacle.
            return !Physics.Raycast(from, delta / distance, Mathf.Max(0f, distance - 0.9f), _sightMask,
                QueryTriggerInteraction.Ignore);
        }
    }
}
