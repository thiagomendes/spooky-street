using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.Case
{
    /// <summary>
    /// Immutable progress of case 01: the set of acquired facts, kept in acquisition order so the final notebook
    /// card can list the player's actual discoveries. All change goes through <see cref="Apply"/>.
    /// </summary>
    public sealed class CaseState
    {
        public static readonly CaseState Empty = new CaseState(Array.Empty<CaseFact>());

        private readonly CaseFact[] _order;
        private readonly HashSet<CaseFact> _set;

        private CaseState(CaseFact[] order)
        {
            _order = order;
            _set = new HashSet<CaseFact>(order);
        }

        /// <summary>Acquired facts in the order they were gained.</summary>
        public IReadOnlyList<CaseFact> Facts => _order;

        public int Count => _order.Length;

        public bool Has(CaseFact fact) => _set.Contains(fact);

        public bool HasAll(IEnumerable<CaseFact> facts)
        {
            foreach (var fact in facts)
            {
                if (!_set.Contains(fact))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>The pure reducer: the new state, the facts gained and what the world should show.</summary>
        public CaseStep Apply(CaseAction action) => CaseReducer.Apply(this, action);

        /// <summary>
        /// Rebuilds a state from saved facts (restore). Duplicates are ignored and implied facts are added, so a
        /// save written mid-transition can never leave the world inconsistent (e.g. arm linked but cam unseen).
        /// </summary>
        public static CaseState FromFacts(IEnumerable<CaseFact> facts)
        {
            var state = Empty;
            if (facts == null)
            {
                return state;
            }

            foreach (var fact in facts)
            {
                state = state.With(fact, null);
            }

            return state;
        }

        /// <summary>Adds <paramref name="fact"/> and everything it implies (implied facts first).</summary>
        internal CaseState With(CaseFact fact, List<CaseFact> gained)
        {
            if (Has(fact))
            {
                return this;
            }

            var state = this;
            foreach (var implied in CaseDefinition.Get(fact).Implies)
            {
                state = state.With(implied, gained);
            }

            var order = new CaseFact[state._order.Length + 1];
            Array.Copy(state._order, order, state._order.Length);
            order[order.Length - 1] = fact;
            gained?.Add(fact);
            return new CaseState(order);
        }

        public override string ToString() => "CaseState[" + string.Join(", ", _order) + "]";
    }
}
