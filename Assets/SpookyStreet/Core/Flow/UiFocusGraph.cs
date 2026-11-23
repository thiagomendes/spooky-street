using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.Flow
{
    /// <summary>A navigation step on a screen (d-pad, stick, arrow keys). Row 0 is the top of the screen.</summary>
    public enum UiDirection
    {
        Up,
        Down,
        Left,
        Right,
    }

    /// <summary>
    /// The focus map of one screen for gamepad and keyboard (S004 F1, D25, docs/portability.md): every focusable
    /// element sits on a row and column grid in the order it is added (the focus order), and the d-pad moves to the
    /// nearest element in that direction. Left and right wrap inside a row by default; up and down stop at the edges
    /// unless <see cref="WrapColumns"/> is set. A one-way <see cref="Link"/> overrides the grid for odd layouts.
    /// Disabled elements are skipped and never hold focus. Every screen names its <see cref="Exit"/> (the close disc a
    /// touch player taps): Back always does what that element does, whatever has focus, so no screen traps a child.
    /// <see cref="Validate"/> checks the rules (every element reachable, no traps, an exit) and tests call it for each
    /// screen. Pure; the runtime's <c>UiFocusScreen</c> binds the ids to buttons and draws the stitched outline.
    /// </summary>
    public sealed class UiFocusGraph
    {
        private sealed class Node
        {
            public string Id;
            public int Row;
            public int Column;
            public bool Enabled = true;
            public readonly Dictionary<UiDirection, string> Links = new Dictionary<UiDirection, string>();
        }

        private readonly List<Node> _nodes = new List<Node>();
        private readonly Dictionary<string, Node> _byId = new Dictionary<string, Node>(StringComparer.Ordinal);

        public UiFocusGraph(string screenId)
        {
            ScreenId = screenId ?? throw new ArgumentNullException(nameof(screenId));
        }

        /// <summary>The screen's name (messages and tests).</summary>
        public string ScreenId { get; }

        /// <summary>Left past the first element of a row lands on its last, and back (default on).</summary>
        public bool WrapRows { get; set; } = true;

        /// <summary>Up past the top row lands on the bottom row, and back (default off).</summary>
        public bool WrapColumns { get; set; }

        /// <summary>Where focus starts when the screen opens; null means the first enabled element in order.</summary>
        public string DefaultFocus { get; set; }

        /// <summary>The element that closes the screen (Back does what it does). Required by <see cref="Validate"/>.</summary>
        public string Exit { get; set; }

        public int Count => _nodes.Count;

        /// <summary>Every element id in focus order.</summary>
        public IReadOnlyList<string> Order
        {
            get
            {
                var ids = new List<string>(_nodes.Count);
                foreach (var n in _nodes)
                {
                    ids.Add(n.Id);
                }

                return ids;
            }
        }

        /// <summary>Adds an element at a grid cell (row 0 at the top, column 0 at the left).</summary>
        public UiFocusGraph Add(string id, int row, int column)
        {
            if (string.IsNullOrEmpty(id))
            {
                throw new ArgumentException("an element needs an id", nameof(id));
            }

            if (_byId.ContainsKey(id))
            {
                throw new ArgumentException($"{ScreenId}: '{id}' is already on the screen", nameof(id));
            }

            var node = new Node { Id = id, Row = row, Column = column };
            _nodes.Add(node);
            _byId[id] = node;
            return this;
        }

        /// <summary>Adds a row of elements, left to right, in columns 0, 1, 2…</summary>
        public UiFocusGraph AddRow(int row, params string[] ids)
        {
            for (int i = 0; i < ids.Length; i++)
            {
                Add(ids[i], row, i);
            }

            return this;
        }

        /// <summary>One-way override: <paramref name="direction"/> from <paramref name="from"/> goes to <paramref name="to"/>.</summary>
        public UiFocusGraph Link(string from, UiDirection direction, string to)
        {
            NodeOf(from).Links[direction] = to;
            return this;
        }

        public bool Contains(string id) => id != null && _byId.ContainsKey(id);

        public bool IsEnabled(string id) => id != null && _byId.TryGetValue(id, out var n) && n.Enabled;

        /// <summary>A hidden or greyed element: skipped by every move and never focused.</summary>
        public void SetEnabled(string id, bool enabled) => NodeOf(id).Enabled = enabled;

        /// <summary>Where focus starts: <see cref="DefaultFocus"/> when enabled, else the first enabled element; null if none.</summary>
        public string Start()
        {
            if (IsEnabled(DefaultFocus))
            {
                return DefaultFocus;
            }

            foreach (var n in _nodes)
            {
                if (n.Enabled)
                {
                    return n.Id;
                }
            }

            return null;
        }

        /// <summary><paramref name="focus"/> while it is a usable element, else <see cref="Start"/>.</summary>
        public string Resolve(string focus) => IsEnabled(focus) ? focus : Start();

        /// <summary>The element focus moves to from <paramref name="from"/>; the same element when nothing lies that way.</summary>
        public string Move(string from, UiDirection direction)
        {
            string current = Resolve(from);
            if (current == null)
            {
                return null;
            }

            // A link (followed through disabled elements, at most once round the screen) beats the grid.
            var node = _byId[current];
            for (int hops = 0; hops < _nodes.Count && node.Links.TryGetValue(direction, out string to) && Contains(to); hops++)
            {
                node = _byId[to];
                if (node.Enabled)
                {
                    return node.Id;
                }
            }

            var start = _byId[current];
            var next = direction == UiDirection.Left || direction == UiDirection.Right
                ? AlongRow(start, direction == UiDirection.Right ? 1 : -1)
                : AcrossRows(start, direction == UiDirection.Down ? 1 : -1);
            return next != null ? next.Id : current;
        }

        /// <summary>Tab order: the next (or previous) enabled element in focus order, wrapping.</summary>
        public string Step(string from, int step)
        {
            string current = Resolve(from);
            if (current == null || step == 0)
            {
                return current;
            }

            int at = _nodes.IndexOf(_byId[current]);
            int sign = step > 0 ? 1 : -1;
            for (int i = 1; i <= _nodes.Count; i++)
            {
                var n = _nodes[((at + sign * i) % _nodes.Count + _nodes.Count) % _nodes.Count];
                if (n.Enabled)
                {
                    return n.Id;
                }
            }

            return current;
        }

        /// <summary>
        /// The screen's rule breaks, empty when it is sound: an exit is named and usable, every link points at an
        /// element, every enabled element is reachable from the start, and from every one the start is reachable
        /// again (no trap). Run it with the elements enabled as each state of the screen shows them.
        /// </summary>
        public IReadOnlyList<string> Validate()
        {
            var problems = new List<string>();
            string start = Start();
            if (start == null)
            {
                problems.Add($"{ScreenId}: no enabled element to focus");
                return problems;
            }

            if (DefaultFocus != null && !Contains(DefaultFocus))
            {
                problems.Add($"{ScreenId}: default focus '{DefaultFocus}' is not on the screen");
            }

            if (Exit == null)
            {
                problems.Add($"{ScreenId}: no exit (Back must always close the screen)");
            }
            else if (!IsEnabled(Exit))
            {
                problems.Add($"{ScreenId}: exit '{Exit}' is not an enabled element");
            }

            foreach (var n in _nodes)
            {
                foreach (var link in n.Links)
                {
                    if (!Contains(link.Value))
                    {
                        problems.Add($"{ScreenId}: '{n.Id}' {link.Key} links to missing '{link.Value}'");
                    }
                }
            }

            var reached = Reach(start);
            foreach (var n in _nodes)
            {
                if (!n.Enabled)
                {
                    continue;
                }

                if (!reached.Contains(n.Id))
                {
                    problems.Add($"{ScreenId}: '{n.Id}' cannot be reached from '{start}'");
                }
                else if (!Reach(n.Id).Contains(start))
                {
                    problems.Add($"{ScreenId}: '{n.Id}' is a trap (no way back to '{start}')");
                }
            }

            return problems;
        }

        private HashSet<string> Reach(string from)
        {
            var seen = new HashSet<string>(StringComparer.Ordinal) { from };
            var queue = new Queue<string>();
            queue.Enqueue(from);
            while (queue.Count > 0)
            {
                string at = queue.Dequeue();
                for (int d = 0; d < 4; d++)
                {
                    string to = Move(at, (UiDirection)d);
                    if (to != null && seen.Add(to))
                    {
                        queue.Enqueue(to);
                    }
                }
            }

            return seen;
        }

        private Node AlongRow(Node from, int sign)
        {
            Node best = null;
            Node wrap = null;
            foreach (var n in _nodes)
            {
                if (!n.Enabled || n == from || n.Row != from.Row)
                {
                    continue;
                }

                int delta = (n.Column - from.Column) * sign;
                if (delta > 0 && (best == null || delta < (best.Column - from.Column) * sign))
                {
                    best = n;
                }

                // The far end of the other side: the smallest column going right, the largest going left.
                if (delta < 0 && (wrap == null || (n.Column - wrap.Column) * sign < 0))
                {
                    wrap = n;
                }
            }

            return best ?? (WrapRows ? wrap : null);
        }

        private Node AcrossRows(Node from, int sign)
        {
            int? nearest = null;
            int? farthest = null;
            foreach (var n in _nodes)
            {
                if (!n.Enabled || n.Row == from.Row)
                {
                    continue;
                }

                int delta = (n.Row - from.Row) * sign;
                if (delta > 0 && (nearest == null || delta < (nearest.Value - from.Row) * sign))
                {
                    nearest = n.Row;
                }

                if (delta < 0 && (farthest == null || (n.Row - farthest.Value) * sign < 0))
                {
                    farthest = n.Row;
                }
            }

            int? row = nearest ?? (WrapColumns ? farthest : null);
            if (row == null)
            {
                return null;
            }

            // In that row, the element closest to the current column (the left one on a tie).
            Node best = null;
            foreach (var n in _nodes)
            {
                if (!n.Enabled || n.Row != row.Value)
                {
                    continue;
                }

                int gap = Math.Abs(n.Column - from.Column);
                int bestGap = best != null ? Math.Abs(best.Column - from.Column) : int.MaxValue;
                if (gap < bestGap || (gap == bestGap && best != null && n.Column < best.Column))
                {
                    best = n;
                }
            }

            return best;
        }

        private Node NodeOf(string id)
        {
            if (id == null || !_byId.TryGetValue(id, out var node))
            {
                throw new ArgumentException($"{ScreenId}: '{id}' is not on the screen", nameof(id));
            }

            return node;
        }
    }
}
