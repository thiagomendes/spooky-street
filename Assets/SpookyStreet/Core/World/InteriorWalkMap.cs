using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.World
{
    /// <summary>How Pip gets from one standing place to the next in <see cref="InteriorWalkMap"/>.</summary>
    public enum WalkMove
    {
        /// <summary>A step (up or down at most the controller's step offset): both ways.</summary>
        Walk,

        /// <summary>Stepping off an edge and falling to the surface below: one way.</summary>
        Drop,

        /// <summary>A jump up onto a surface close by: one way.</summary>
        Jump,
    }

    /// <summary>
    /// Where Pip can stand in the owl house interior and how the standing places connect (TestFlight 23 safety net):
    /// a flood fill over the authored collider layout (<see cref="CaseRouteLayout.Slabs"/>, the interior group) at Pip's
    /// capsule size (<see cref="CaseRouteLayout.PipRadius"/>, <see cref="CaseRouteLayout.PipHeight"/>), on a
    /// <see cref="Cell"/> grid over the interior's plan. A node is a slab top under the capsule's centre where the
    /// capsule above the step height touches no slab. Nodes connect by walking (a step of at most
    /// <see cref="CaseRouteLayout.StepOffset"/>, both ways), by stepping off an edge and falling onto a surface below within <see cref="JumpReach"/> (one way) and by
    /// jumping up at most <see cref="JumpUp"/> onto a surface within <see cref="JumpReach"/> (one way). The main area is
    /// the walking component of the two arrival points (the attic and the hall, joined by the stair). Pure.
    /// </summary>
    public sealed class InteriorWalkMap
    {
        /// <summary>Grid step (metres).</summary>
        public const float Cell = 0.1f;

        /// <summary>Highest climb a jump is credited with (a held jump peaks higher; this stays on the safe side).</summary>
        public const float JumpUp = 1.25f;

        /// <summary>Furthest a jump is credited to carry Pip sideways onto a higher surface (and a step off an edge
        /// before the fall: walking off a chest carries Pip past the reach of its sides).</summary>
        public const float JumpReach = 0.6f;

        /// <summary>Two slab tops this close at one place are the same surface.</summary>
        private const float SameSurface = 0.03f;

        public static readonly float MinX = CaseRouteLayout.InnerMinX - 0.45f;
        public static readonly float MaxX = CaseRouteLayout.InnerMaxX + 0.3f;
        public static readonly float MinZ = CaseRouteLayout.InteriorFrontZ - 0.45f;
        public static readonly float MaxZ = CaseRouteLayout.InnerMaxZ + 0.3f;

        private readonly StageSlab[] _slabs;
        private readonly int _nx;
        private readonly int _nz;
        private readonly List<int>[] _cells;
        private readonly List<float> _x = new List<float>();
        private readonly List<float> _y = new List<float>();
        private readonly List<float> _z = new List<float>();
        private readonly List<bool> _floor = new List<bool>();
        private List<int>[] _out;
        private List<int>[] _in;
        private List<WalkMove>[] _outMoves;

        public InteriorWalkMap(bool doorOpen)
        {
            DoorOpen = doorOpen;
            _slabs = Solids(doorOpen);
            _nx = (int)Math.Ceiling((MaxX - MinX) / Cell);
            _nz = (int)Math.Ceiling((MaxZ - MinZ) / Cell);
            _cells = new List<int>[_nx * _nz];
            for (int i = 0; i < _nx; i++)
            {
                for (int k = 0; k < _nz; k++)
                {
                    float x = MinX + (i + 0.5f) * Cell;
                    float z = MinZ + (k + 0.5f) * Cell;
                    var list = new List<int>(2);
                    foreach (var (y, floor) in Tops(_slabs, x, z))
                    {
                        if (!Blocked(_slabs, x, z, y + CaseRouteLayout.StepOffset + 0.02f, y + CaseRouteLayout.PipHeight))
                        {
                            list.Add(_x.Count);
                            _x.Add(x);
                            _y.Add(y);
                            _z.Add(z);
                            _floor.Add(floor);
                        }
                    }

                    _cells[i * _nz + k] = list;
                }
            }

            BuildEdges();
            Main = Component(Arrivals());
            ReachableFromMain = Search(Main, _out);
            CanReachMain = Search(Main, _in);
        }

        public bool DoorOpen { get; }

        public int Count => _x.Count;

        /// <summary>Nodes in the walking component of the arrival points.</summary>
        public bool[] Main { get; }

        /// <summary>Nodes Pip can get to from the main area by any move.</summary>
        public bool[] ReachableFromMain { get; }

        /// <summary>Nodes from which Pip can get back to the main area by any move.</summary>
        public bool[] CanReachMain { get; }

        public float X(int node) => _x[node];
        public float Y(int node) => _y[node];
        public float Z(int node) => _z[node];

        /// <summary>True when the node stands on a floor (a walk slab), not on furniture or a wall top.</summary>
        public bool OnFloor(int node) => _floor[node];

        public IReadOnlyList<int> Out(int node) => _out[node];

        /// <summary>True when <paramref name="b"/> can be walked to from <paramref name="a"/> (steps only, no drop or jump).</summary>
        public bool WalkConnected(int a, int b) => Component(new[] { a })[b];

        public WalkMove MoveTo(int node, int index) => _outMoves[node][index];

        /// <summary>Nodes reachable from the main area that cannot get back to it (pockets Pip could be trapped in).</summary>
        public List<int> Traps()
        {
            var traps = new List<int>();
            for (int n = 0; n < Count; n++)
            {
                if (ReachableFromMain[n] && !CanReachMain[n])
                {
                    traps.Add(n);
                }
            }

            return traps;
        }

        /// <summary>The standing node nearest (x, y, z) in plan within one cell and at most <paramref name="dy"/> in height, or −1.</summary>
        public int NodeAt(float x, float y, float z, float dy = 0.3f)
        {
            int best = -1;
            float bestD = float.MaxValue;
            int ci = (int)Math.Floor((x - MinX) / Cell);
            int ck = (int)Math.Floor((z - MinZ) / Cell);
            for (int i = ci - 1; i <= ci + 1; i++)
            {
                for (int k = ck - 1; k <= ck + 1; k++)
                {
                    if (i < 0 || k < 0 || i >= _nx || k >= _nz)
                    {
                        continue;
                    }

                    foreach (int n in _cells[i * _nz + k])
                    {
                        float h = Math.Abs(_y[n] - y);
                        if (h > dy)
                        {
                            continue;
                        }

                        float d = (_x[n] - x) * (_x[n] - x) + (_z[n] - z) * (_z[n] - z) + h * h;
                        if (d < bestD)
                        {
                            bestD = d;
                            best = n;
                        }
                    }
                }
            }

            return best;
        }

        /// <summary>
        /// True when a standing Pip at (x, y, z) has a slab top under the capsule's centre within
        /// <paramref name="tolerance"/> of the feet and the capsule above the step touches no interior slab.
        /// </summary>
        public static bool CanStand(float x, float y, float z, bool doorOpen, float tolerance = 0.25f)
        {
            var slabs = Solids(doorOpen);
            foreach (float top in Surfaces(slabs, x, z))
            {
                if (Math.Abs(top - y) <= tolerance &&
                    !Blocked(slabs, x, z, top + CaseRouteLayout.StepOffset + 0.02f, top + CaseRouteLayout.PipHeight))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// How far a standing Pip at (x, y, z) can walk straight along (dirX, dirZ) on standing places, without a
        /// jump and without entering any threshold's exit zone, up to <paramref name="max"/> metres.
        /// </summary>
        public static float FreeRun(float x, float y, float z, float dirX, float dirZ, float max, bool doorOpen)
        {
            var slabs = Solids(doorOpen);
            float len = (float)Math.Sqrt(dirX * dirX + dirZ * dirZ);
            dirX /= len;
            dirZ /= len;
            const float step = 0.02f;
            float h = y;
            float run = 0f;
            while (run < max)
            {
                float d = run + step;
                float px = x + dirX * d;
                float pz = z + dirZ * d;
                bool found = false;
                foreach (float top in Surfaces(slabs, px, pz))
                {
                    if (Math.Abs(top - h) <= CaseRouteLayout.StepOffset &&
                        !Blocked(slabs, px, pz, top + CaseRouteLayout.StepOffset + 0.02f, top + CaseRouteLayout.PipHeight))
                    {
                        h = top;
                        found = true;
                        break;
                    }
                }

                if (!found || Thresholds.NearAnyZone(px, h, pz, 0f))
                {
                    break;
                }

                run = d;
            }

            return Math.Min(run, max);
        }

        /// <summary>The interior's solid slabs (the inner door leaf only while latched).</summary>
        public static StageSlab[] Solids(bool doorOpen)
        {
            var list = new List<StageSlab>();
            foreach (var slab in CaseRouteLayout.Slabs)
            {
                if (slab.Group != SlabGroup.Interior || (doorOpen && slab.Name == CaseRouteLayout.DoorBlockerName))
                {
                    continue;
                }

                list.Add(slab);
            }

            return list.ToArray();
        }

        /// <summary>Slab tops (walk or wall) over (x, z), highest first, near-equal ones merged.</summary>
        private static List<float> Surfaces(StageSlab[] slabs, float x, float z)
        {
            var tops = new List<float>(4);
            foreach (var (y, _) in Tops(slabs, x, z))
            {
                tops.Add(y);
            }

            return tops;
        }

        /// <summary>Slab tops over (x, z), highest first, near-equal ones merged; floor when any merged top is a walk slab.</summary>
        private static List<(float Y, bool Floor)> Tops(StageSlab[] slabs, float x, float z)
        {
            var tops = new List<(float Y, bool Floor)>(4);
            foreach (var slab in slabs)
            {
                if (slab.TryTopAt(x, z, 0f, out float y))
                {
                    tops.Add((y, slab.Kind == SlabKind.Walk));
                }
            }

            tops.Sort((a, b) => b.Y.CompareTo(a.Y));
            for (int i = tops.Count - 1; i > 0; i--)
            {
                if (tops[i - 1].Y - tops[i].Y < SameSurface)
                {
                    tops[i - 1] = (tops[i - 1].Y, tops[i - 1].Floor || tops[i].Floor);
                    tops.RemoveAt(i);
                }
            }

            return tops;
        }

        /// <summary>True when Pip's capsule at (x, z) spanning heights y0…y1 touches any of <paramref name="slabs"/>.</summary>
        private static bool Blocked(StageSlab[] slabs, float x, float z, float y0, float y1)
        {
            foreach (var slab in slabs)
            {
                if (slab.Blocks(x, z, y0, y1, CaseRouteLayout.PipRadius))
                {
                    return true;
                }
            }

            return false;
        }

        private IEnumerable<int> Arrivals()
        {
            foreach (var end in Thresholds.Ends)
            {
                if (!end.Inside)
                {
                    continue;
                }

                int n = NodeAt(end.ArriveX, end.ArriveY, end.ArriveZ);
                if (n >= 0)
                {
                    yield return n;
                }
            }
        }

        private void BuildEdges()
        {
            int count = Count;
            _out = new List<int>[count];
            _outMoves = new List<WalkMove>[count];
            _in = new List<int>[count];
            for (int n = 0; n < count; n++)
            {
                _out[n] = new List<int>(8);
                _outMoves[n] = new List<WalkMove>(8);
                _in[n] = new List<int>(8);
            }

            int reach = (int)Math.Ceiling(JumpReach / Cell);
            for (int i = 0; i < _nx; i++)
            {
                for (int k = 0; k < _nz; k++)
                {
                    foreach (int n in _cells[i * _nz + k])
                    {
                        for (int di = -reach; di <= reach; di++)
                        {
                            for (int dk = -reach; dk <= reach; dk++)
                            {
                                int a = i + di;
                                int b = k + dk;
                                if (a < 0 || b < 0 || a >= _nx || b >= _nz)
                                {
                                    continue;
                                }

                                bool adjacent = Math.Abs(di) <= 1 && Math.Abs(dk) <= 1;
                                float plan = (float)Math.Sqrt(di * di + dk * dk) * Cell;
                                foreach (int m in _cells[a * _nz + b])
                                {
                                    if (m == n)
                                    {
                                        continue;
                                    }

                                    float rise = _y[m] - _y[n];
                                    WalkMove move;
                                    if (adjacent && Math.Abs(rise) <= CaseRouteLayout.StepOffset + 0.01f)
                                    {
                                        move = WalkMove.Walk;
                                    }
                                    else if (rise < 0f && plan <= JumpReach &&
                                             !Blocked(_slabs, _x[m], _z[m], _y[m] + CaseRouteLayout.StepOffset + 0.02f,
                                                 _y[n] + CaseRouteLayout.PipHeight))
                                    {
                                        move = WalkMove.Drop;
                                    }
                                    else if (rise > CaseRouteLayout.StepOffset && rise <= JumpUp && plan <= JumpReach &&
                                             !Blocked(_slabs, _x[n], _z[n], _y[n] + CaseRouteLayout.StepOffset + 0.02f,
                                                 _y[m] + CaseRouteLayout.PipHeight))
                                    {
                                        move = WalkMove.Jump;
                                    }
                                    else
                                    {
                                        continue;
                                    }

                                    _out[n].Add(m);
                                    _outMoves[n].Add(move);
                                    _in[m].Add(n);
                                }
                            }
                        }
                    }
                }
            }
        }

        /// <summary>Nodes connected to <paramref name="seeds"/> by walking (steps both ways).</summary>
        private bool[] Component(IEnumerable<int> seeds)
        {
            var seen = new bool[Count];
            var queue = new Queue<int>();
            foreach (int s in seeds)
            {
                if (!seen[s])
                {
                    seen[s] = true;
                    queue.Enqueue(s);
                }
            }

            while (queue.Count > 0)
            {
                int n = queue.Dequeue();
                for (int e = 0; e < _out[n].Count; e++)
                {
                    int m = _out[n][e];
                    if (_outMoves[n][e] == WalkMove.Walk && !seen[m])
                    {
                        seen[m] = true;
                        queue.Enqueue(m);
                    }
                }
            }

            return seen;
        }

        private bool[] Search(bool[] from, List<int>[] edges)
        {
            var seen = (bool[])from.Clone();
            var queue = new Queue<int>();
            for (int n = 0; n < Count; n++)
            {
                if (seen[n])
                {
                    queue.Enqueue(n);
                }
            }

            while (queue.Count > 0)
            {
                int n = queue.Dequeue();
                foreach (int m in edges[n])
                {
                    if (!seen[m])
                    {
                        seen[m] = true;
                        queue.Enqueue(m);
                    }
                }
            }

            return seen;
        }
    }
}
