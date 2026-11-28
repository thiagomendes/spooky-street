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

        public bool HasTarget => !Suspended && (Current != null || ExtraActive);

        public string VerbKey => !HasTarget ? null
            : Current != null ? VerbFor?.Invoke(Current) ?? Current.VerbKey
            : _selectedExtra.VerbKey;

        private readonly List<ExtraUseTarget> _extras = new List<ExtraUseTarget>();
        private ExtraUseTarget _extraPrimary;

        /// <summary>
        /// One Use target that is not a case point (S004: the dress-up trunk on Pip's porch). Offered only when no case
        /// point is, so it never takes a clue's Use; null for none. Further free-world targets join with
        /// <see cref="AddExtra"/> (living street: doorbells, the lantern table, treats at Pip's porch).
        /// </summary>
        public ExtraUseTarget Extra
        {
            get => ExtraActive ? _selectedExtra : _extraPrimary;
            set
            {
                if (_extraPrimary != null)
                {
                    _extras.Remove(_extraPrimary);
                }

                _extraPrimary = value;
                if (value != null)
                {
                    _extras.Insert(0, value);
                }
            }
        }

        private ExtraUseTarget _selectedExtra;

        /// <summary>Adds a free-world Use target (never offered while a case point is in range).</summary>
        public void AddExtra(ExtraUseTarget target)
        {
            if (target != null && !_extras.Contains(target))
            {
                _extras.Add(target);
            }
        }

        /// <summary>True while one of the extra targets is the selected target.</summary>
        public bool ExtraActive { get; private set; }

        /// <summary>The selected extra target (null when a case point or nothing is selected).</summary>
        public ExtraUseTarget SelectedExtra => ExtraActive ? _selectedExtra : null;

        /// <summary>Where the selected target's floating marker goes (null when none).</summary>
        public Vector3? CurrentMarker => !HasTarget ? (Vector3?)null : Current != null ? MarkerOf(Current) : _selectedExtra.Marker;

        /// <summary>The selected target's icon (null when none).</summary>
        public string CurrentIcon => !HasTarget ? null : Current != null ? IconFor(Current) : _selectedExtra.IconName;

        /// <summary>A verb the world's state gives a point instead of its own (null: the point's; S004 the booth's knock).</summary>
        public Func<InteractionPoint, string> VerbFor { get; set; }

        public string IconName => CurrentIcon;

        public static string IconFor(InteractionPoint point) =>
            point != null && point.Action.Kind == CaseActionKind.Talk ? UiSpriteCatalog.IconTalk : UiSpriteCatalog.IconExamine;

        /// <summary>World position of a point's floating marker.</summary>
        public static Vector3 MarkerOf(InteractionPoint point) =>
            new Vector3(point.Position.X, point.Position.Y + point.MarkerHeight, point.Position.Z);

        public void Interact()
        {
            if (!HasTarget)
            {
                return;
            }

            if (Current != null)
            {
                _interact?.Invoke(Current);
            }
            else
            {
                _selectedExtra.Interact?.Invoke();
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
            _selectedExtra = null;
            if (picked == null)
            {
                float best = float.MaxValue;
                foreach (var extra in _extras)
                {
                    if (extra.Offered == null || !extra.Offered(pip))
                    {
                        continue;
                    }

                    float d = (extra.Marker - pip).sqrMagnitude;
                    if (d < best)
                    {
                        best = d;
                        _selectedExtra = extra;
                    }
                }
            }

            ExtraActive = _selectedExtra != null;
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

    /// <summary>A Use target outside the case's points (S004: the dress-up trunk).</summary>
    public sealed class ExtraUseTarget
    {
        /// <summary>Whether Pip (feet position) can use it now.</summary>
        public Func<Vector3, bool> Offered { get; set; }

        public string VerbKey { get; set; }

        public string IconName { get; set; } = UiSpriteCatalog.IconExamine;

        public Vector3 Marker { get; set; }

        public Action Interact { get; set; }
    }
}
