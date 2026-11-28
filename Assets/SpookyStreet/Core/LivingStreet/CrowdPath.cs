using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.LivingStreet
{
    /// <summary>
    /// A walker group's path for its next leg: the authored nodes with every bend rounded into a smooth curve (a
    /// quadratic curve between points <see cref="CornerReach"/> before and after the bend, shorter on short legs), so a
    /// family turns together along a curve instead of pivoting on a corner, and a member walking beside the line never
    /// jumps sideways. Stops (a door, Pip's porch, a look at a decoration) stay exact points on the path. Markers (the
    /// heart's gate and exit) keep their place at the nearest point of the curve. Engine-free; sampled by arc length.
    /// </summary>
    public sealed class CrowdPath
    {
        /// <summary>A bend rounds off over at most this much of each leg either side of it.</summary>
        public const float CornerReach = 1.3f;

        /// <summary>Bends turning less than this stay as they are.</summary>
        public const float MinTurnDegrees = 3f;

        private const int CornerSegments = 10;

        private readonly List<float> _x = new List<float>();
        private readonly List<float> _z = new List<float>();
        private readonly List<float> _tx = new List<float>();
        private readonly List<float> _tz = new List<float>();
        private readonly List<float> _s = new List<float>();
        private readonly List<PathMark> _marks = new List<PathMark>();

        /// <summary>A node of the route (a stop or a marker) at its arc length on the path.</summary>
        public readonly struct PathMark
        {
            public PathMark(float s, RouteNode node)
            {
                S = s;
                Node = node;
            }

            public float S { get; }
            public RouteNode Node { get; }
        }

        private float[] _nodeS = Array.Empty<float>();

        public float Length => _s.Count > 0 ? _s[_s.Count - 1] : 0f;

        /// <summary>Arc length of each node passed to <see cref="Build"/> (a rounded bend: the middle of its curve).</summary>
        public IReadOnlyList<float> NodeS => _nodeS;

        /// <summary>Stops and markers in order along the path.</summary>
        public IReadOnlyList<PathMark> Marks => _marks;

        /// <summary>
        /// Builds a path: <paramref name="lead"/> points first (already smooth: the tail the group walked, so members
        /// behind the leader still have a line), then <paramref name="nodes"/> with their bends rounded.
        /// </summary>
        public static CrowdPath Build(IReadOnlyList<(float X, float Z)> lead, IReadOnlyList<RouteNode> nodes)
        {
            var path = new CrowdPath();
            var pts = new List<RouteNode>();
            if (lead != null)
            {
                foreach (var p in lead)
                {
                    pts.Add(new RouteNode(p.X, p.Z));
                }
            }

            int firstNode = pts.Count;
            var map = new int[nodes.Count];
            int j = -1;
            foreach (var n in nodes)
            {
                j++;
                // Drop repeated points (a detour comes back to its branch), keeping the one that carries a meaning.
                if (pts.Count > 0 && Same(pts[pts.Count - 1], n))
                {
                    if (n.Kind != RouteNodeKind.Walk && pts[pts.Count - 1].Kind == RouteNodeKind.Walk)
                    {
                        pts[pts.Count - 1] = n;
                    }

                    map[j] = pts.Count - 1;
                    continue;
                }

                map[j] = pts.Count;
                pts.Add(n);
            }

            var ptsS = new float[pts.Count];

            // Points that lie straight on the line between their neighbours (a marker such as the heart's gate) are not
            // corners: they are laid onto the finished path afterwards, so they never shorten a nearby bend's curve.
            var onLine = new bool[pts.Count];
            for (int i = Math.Max(1, firstNode); i < pts.Count - 1; i++)
            {
                onLine[i] = !pts[i].IsStop && Turn(pts[i - 1], pts[i], pts[i + 1]) < MinTurnDegrees;
            }

            int Prev(int i)
            {
                int k = i - 1;
                while (k > 0 && onLine[k])
                {
                    k--;
                }

                return k;
            }

            int Next(int i)
            {
                int k = i + 1;
                while (k < pts.Count - 1 && onLine[k])
                {
                    k++;
                }

                return k;
            }

            for (int i = 0; i < pts.Count; i++)
            {
                if (onLine[i])
                {
                    continue;
                }

                var n = pts[i];
                // The lead is already smooth; the route's own bends round off (the first one joins the lead).
                bool corner = i >= Math.Max(1, firstNode) && i < pts.Count - 1 && !n.IsStop;
                if (!corner || !path.Round(pts[Prev(i)], n, pts[Next(i)], out var mark))
                {
                    path.Add(n.X, n.Z);
                    ptsS[i] = path.Length;
                    if (n.Kind != RouteNodeKind.Walk && i >= firstNode)
                    {
                        path._marks.Add(new PathMark(path.Length, n));
                    }

                    continue;
                }

                ptsS[i] = mark;
                if (n.Kind != RouteNodeKind.Walk && i >= firstNode)
                {
                    path._marks.Add(new PathMark(mark, n));
                }
            }

            for (int i = 0; i < pts.Count; i++)
            {
                if (!onLine[i])
                {
                    continue;
                }

                int a = Prev(i), b = Next(i);
                ptsS[i] = path.Project(pts[i].X, pts[i].Z, ptsS[a], ptsS[b]);
                if (pts[i].Kind != RouteNodeKind.Walk && i >= firstNode)
                {
                    path._marks.Add(new PathMark(ptsS[i], pts[i]));
                }
            }

            path._marks.Sort((x, y) => x.S.CompareTo(y.S));

            path._nodeS = new float[nodes.Count];
            for (int k = 0; k < nodes.Count; k++)
            {
                path._nodeS[k] = ptsS[map[k]];
            }

            path.FinishTangents();
            return path;
        }

        private static float Turn(RouteNode a, RouteNode b, RouteNode c)
        {
            float ix = b.X - a.X, iz = b.Z - a.Z, ox = c.X - b.X, oz = c.Z - b.Z;
            float li = (float)Math.Sqrt(ix * ix + iz * iz);
            float lo = (float)Math.Sqrt(ox * ox + oz * oz);
            if (li < 1e-4f || lo < 1e-4f)
            {
                return 180f;
            }

            float dot = Math.Max(-1f, Math.Min(1f, (ix * ox + iz * oz) / (li * lo)));
            return (float)(Math.Acos(dot) * 180.0 / Math.PI);
        }

        /// <summary>Arc length of the point of the path nearest (x, z) between <paramref name="from"/> and <paramref name="to"/>.</summary>
        private float Project(float x, float z, float from, float to)
        {
            float best = float.MaxValue, at = from;
            for (int i = 0; i < _x.Count - 1; i++)
            {
                if (_s[i + 1] < from - 1e-4f || _s[i] > to + 1e-4f)
                {
                    continue;
                }

                float ax = _x[i], az = _z[i];
                float dx = _x[i + 1] - ax, dz = _z[i + 1] - az;
                float len2 = dx * dx + dz * dz;
                float t = len2 > 1e-10f ? ((x - ax) * dx + (z - az) * dz) / len2 : 0f;
                t = t < 0f ? 0f : t > 1f ? 1f : t;
                float px = ax + dx * t - x, pz = az + dz * t - z;
                float d = px * px + pz * pz;
                if (d < best)
                {
                    best = d;
                    at = _s[i] + (_s[i + 1] - _s[i]) * t;
                }
            }

            return at;
        }

        /// <summary>Rounds the bend at <paramref name="b"/>; false when it is too slight (then it stays a point).</summary>
        private bool Round(RouteNode a, RouteNode b, RouteNode c, out float markS)
        {
            markS = 0f;
            float ix = b.X - a.X, iz = b.Z - a.Z, ox = c.X - b.X, oz = c.Z - b.Z;
            float li = (float)Math.Sqrt(ix * ix + iz * iz);
            float lo = (float)Math.Sqrt(ox * ox + oz * oz);
            if (li < 1e-4f || lo < 1e-4f)
            {
                return false;
            }

            ix /= li;
            iz /= li;
            ox /= lo;
            oz /= lo;
            float dot = Math.Max(-1f, Math.Min(1f, ix * ox + iz * oz));
            float turn = (float)(Math.Acos(dot) * 180.0 / Math.PI);
            if (turn < MinTurnDegrees)
            {
                return false;
            }

            float reach = Math.Min(CornerReach, 0.45f * Math.Min(li, lo));
            float p1x = b.X - ix * reach, p1z = b.Z - iz * reach;
            float p2x = b.X + ox * reach, p2z = b.Z + oz * reach;
            Add(p1x, p1z);
            for (int k = 1; k <= CornerSegments; k++)
            {
                float u = k / (float)CornerSegments;
                float w0 = (1f - u) * (1f - u), w1 = 2f * u * (1f - u), w2 = u * u;
                Add(w0 * p1x + w1 * b.X + w2 * p2x, w0 * p1z + w1 * b.Z + w2 * p2z);
                if (k == CornerSegments / 2)
                {
                    markS = Length;
                }
            }

            return true;
        }

        private void Add(float x, float z)
        {
            if (_x.Count > 0)
            {
                float dx = x - _x[_x.Count - 1];
                float dz = z - _z[_z.Count - 1];
                float d = (float)Math.Sqrt(dx * dx + dz * dz);
                if (d < 1e-4f)
                {
                    return;
                }

                _s.Add(_s[_s.Count - 1] + d);
            }
            else
            {
                _s.Add(0f);
            }

            _x.Add(x);
            _z.Add(z);
        }

        private void FinishTangents()
        {
            int n = _x.Count;
            for (int i = 0; i < n; i++)
            {
                // A vertex's tangent averages its two segments (curves turn smoothly); a stop's point keeps each side's.
                float ax = 0f, az = 0f;
                if (i > 0)
                {
                    Dir(i - 1, i, out float dx, out float dz);
                    ax += dx;
                    az += dz;
                }

                if (i < n - 1)
                {
                    Dir(i, i + 1, out float dx, out float dz);
                    ax += dx;
                    az += dz;
                }

                float l = (float)Math.Sqrt(ax * ax + az * az);
                if (l < 1e-4f)
                {
                    // A turn back on itself (a stop at the end of a detour): take the incoming direction.
                    if (i > 0)
                    {
                        Dir(i - 1, i, out ax, out az);
                    }
                    else
                    {
                        ax = 0f;
                        az = 1f;
                    }

                    l = 1f;
                }

                _tx.Add(ax / l);
                _tz.Add(az / l);
            }
        }

        private void Dir(int a, int b, out float dx, out float dz)
        {
            dx = _x[b] - _x[a];
            dz = _z[b] - _z[a];
            float l = (float)Math.Sqrt(dx * dx + dz * dz);
            dx /= l;
            dz /= l;
        }

        /// <summary>Point and unit heading at arc length <paramref name="s"/> (clamped to the path).</summary>
        public void At(float s, out float x, out float z, out float hx, out float hz)
        {
            int n = _x.Count;
            if (n == 0)
            {
                x = z = hx = 0f;
                hz = 1f;
                return;
            }

            if (n == 1 || s <= 0f)
            {
                x = _x[0];
                z = _z[0];
                hx = _tx[0];
                hz = _tz[0];
                return;
            }

            if (s >= Length)
            {
                x = _x[n - 1];
                z = _z[n - 1];
                hx = _tx[n - 1];
                hz = _tz[n - 1];
                return;
            }

            int lo = 0, hi = n - 1;
            while (hi - lo > 1)
            {
                int mid = (lo + hi) >> 1;
                if (_s[mid] <= s)
                {
                    lo = mid;
                }
                else
                {
                    hi = mid;
                }
            }

            float len = _s[hi] - _s[lo];
            float t = len > 1e-6f ? (s - _s[lo]) / len : 0f;
            x = _x[lo] + (_x[hi] - _x[lo]) * t;
            z = _z[lo] + (_z[hi] - _z[lo]) * t;
            // Within a segment the heading is the segment's own; it eases toward the next one over the last part, so a
            // walker's facing on a curve changes smoothly.
            Dir(lo, hi, out float sx, out float sz);
            float bx, bz;
            if (t < 0.5f)
            {
                float w = t + 0.5f;
                bx = _tx[lo] * (1f - w) + sx * w;
                bz = _tz[lo] * (1f - w) + sz * w;
                if (t < 1e-6f || IsCusp(lo))
                {
                    bx = sx;
                    bz = sz;
                }
            }
            else
            {
                float w = t - 0.5f;
                bx = sx * (1f - w) + _tx[hi] * w;
                bz = sz * (1f - w) + _tz[hi] * w;
                if (IsCusp(hi))
                {
                    bx = sx;
                    bz = sz;
                }
            }

            float l = (float)Math.Sqrt(bx * bx + bz * bz);
            hx = l > 1e-5f ? bx / l : sx;
            hz = l > 1e-5f ? bz / l : sz;
        }

        /// <summary>A vertex where the path turns back sharply (a stop at the end of a detour): no blending across it.</summary>
        private bool IsCusp(int i)
        {
            if (i <= 0 || i >= _x.Count - 1)
            {
                return false;
            }

            Dir(i - 1, i, out float ax, out float az);
            Dir(i, i + 1, out float bx, out float bz);
            return ax * bx + az * bz < 0.5f;
        }

        /// <summary>Points every <paramref name="step"/> metres from <paramref name="from"/> to <paramref name="to"/> (a tail for the next leg).</summary>
        public List<(float X, float Z)> Resample(float from, float to, float step)
        {
            var list = new List<(float X, float Z)>();
            from = Math.Max(0f, from);
            to = Math.Min(Length, to);
            for (float s = from; s < to - step * 0.5f; s += step)
            {
                At(s, out float x, out float z, out _, out _);
                list.Add((x, z));
            }

            At(to, out float ex, out float ez, out _, out _);
            list.Add((ex, ez));
            return list;
        }

        private static bool Same(RouteNode a, RouteNode b) => Math.Abs(a.X - b.X) < 1e-4f && Math.Abs(a.Z - b.Z) < 1e-4f;
    }
}
