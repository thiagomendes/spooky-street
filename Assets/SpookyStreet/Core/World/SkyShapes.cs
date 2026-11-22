using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.World
{
    /// <summary>
    /// A flat cut-paper shape as a triangle list: 2D positions (x right, y up, metres), a rim direction per vertex
    /// (zero inside the sheet, the outward edge direction on the cut edge, scaled by how much moonlight that edge
    /// may catch) and a 0..1 shade (0 at the underside, 1 at the top) the renderer turns into colour.
    /// </summary>
    public sealed class PaperShape
    {
        public PaperShape(float[] x, float[] y, float[] rimX, float[] rimY, float[] shade, int[] triangles)
        {
            X = x;
            Y = y;
            RimX = rimX;
            RimY = rimY;
            Shade = shade;
            Triangles = triangles;
        }

        public float[] X { get; }

        public float[] Y { get; }

        public float[] RimX { get; }

        public float[] RimY { get; }

        public float[] Shade { get; }

        /// <summary>Counter-clockwise in the shape's own plane (x right, y up, seen from +z).</summary>
        public int[] Triangles { get; }

        public int VertexCount => X.Length;
    }

    /// <summary>
    /// Original procedural cut-paper silhouettes for the sky (spec v1.3: all art is authored as reviewable code):
    /// scalloped clouds with a deckled edge and a tiny bat. Shapes are built as columns between a lower and an
    /// upper outline, so they are opaque cut meshes with no alpha overdraw. Engine-free and deterministic.
    /// </summary>
    public static class SkyShapes
    {
        public const int CloudColumns = 56;

        /// <summary>How much of the moon's rim light the underside of a cloud may catch (the top catches all).</summary>
        public const float UndersideRim = 0.45f;

        // Right half of the bat, unit wingspan: upper outline (ear, shoulder, wrist, wing tip) and lower outline
        // (tail, then the membrane's finger points and arches).
        private static readonly float[][] BatTop =
        {
            new[] { 0f, 0.095f }, new[] { 0.018f, 0.105f }, new[] { 0.04f, 0.185f }, new[] { 0.062f, 0.1f },
            new[] { 0.11f, 0.105f }, new[] { 0.19f, 0.175f }, new[] { 0.27f, 0.2f }, new[] { 0.39f, 0.165f },
            new[] { 0.5f, 0.085f },
        };

        private static readonly float[][] BatBottom =
        {
            new[] { 0f, -0.17f }, new[] { 0.04f, -0.13f }, new[] { 0.075f, -0.07f }, new[] { 0.115f, -0.015f },
            new[] { 0.16f, -0.03f }, new[] { 0.2f, -0.095f }, new[] { 0.235f, -0.02f }, new[] { 0.29f, 0.012f },
            new[] { 0.335f, -0.055f }, new[] { 0.37f, 0.03f }, new[] { 0.43f, 0.062f }, new[] { 0.5f, 0.085f },
        };

        /// <summary>
        /// A paper cloud about <paramref name="width"/> × <paramref name="height"/> metres, origin at the middle of
        /// its flat base: a union of round bumps on top, a nearly flat belly, pointed ends and a torn (deckled)
        /// edge. Each column has an inner pair of vertices so the rim light stays a narrow band at the cut.
        /// </summary>
        public static PaperShape Cloud(int seed, float width, float height)
        {
            var random = new Random(seed);
            int bumps = 4 + random.Next(3);
            var cx = new float[bumps];
            var cy = new float[bumps];
            var radius = new float[bumps];
            for (int i = 0; i < bumps; i++)
            {
                float t = i / (bumps - 1f);
                // Tallest bumps near the middle, small ones at the ends, like a hand-cut cumulus. Each bump overlaps
                // the one before it, so the sheet is always one piece.
                float arch = (float)Math.Sin(Math.PI * (0.1f + 0.8f * t));
                radius[i] = 0.26f + 0.3f * arch * (0.65f + 0.35f * (float)random.NextDouble());
                cy[i] = (1f - radius[i]) * arch * arch * (0.55f + 0.45f * (float)random.NextDouble());
                cx[i] = i == 0 ? radius[i] : cx[i - 1] + (radius[i - 1] + radius[i]) * (0.55f + 0.14f * (float)random.NextDouble());
            }

            // Fit the row of bumps to the requested size.
            float left = cx[0] - radius[0];
            float right = cx[bumps - 1] + radius[bumps - 1];
            float peak = 0f;
            for (int i = 0; i < bumps; i++)
            {
                peak = Math.Max(peak, cy[i] + radius[i]);
            }

            float scaleX = width / (right - left);
            float scaleY = height / peak;
            float half = width * 0.5f;
            int columns = CloudColumns;
            var xs = new float[columns + 1];
            var bottom = new float[columns + 1];
            var top = new float[columns + 1];
            for (int c = 0; c <= columns; c++)
            {
                // Cosine spacing: more columns where the end bumps turn down steeply.
                float along = 0.5f - 0.5f * (float)Math.Cos(Math.PI * c / columns);
                float source = left + (right - left) * along;
                float y = 0f;
                for (int i = 0; i < bumps; i++)
                {
                    float dx = source - cx[i];
                    float inside = radius[i] * radius[i] - dx * dx;
                    if (inside > 0f)
                    {
                        y = Math.Max(y, cy[i] + (float)Math.Sqrt(inside));
                    }
                }

                float ends = (float)Math.Sin(Math.PI * c / columns);
                float taper = Math.Min(1f, ends * 7f);
                float deckleTop = ((float)random.NextDouble() - 0.5f) * height * 0.03f;
                float deckleBottom = ((float)random.NextDouble() - 0.5f) * height * 0.025f;
                float belly = -height * 0.06f * ends * (0.6f + 0.4f * (float)Math.Sin(c * 0.7f + seed));
                xs[c] = -half + width * along;
                bottom[c] = (belly + deckleBottom) * taper;
                top[c] = Math.Max(bottom[c], (y * scaleY + deckleTop) * taper);
            }

            return Columns(xs, bottom, top, Math.Min(height * 0.09f, 0.34f));
        }

        /// <summary>A paper bat silhouette, <paramref name="wingspan"/> metres tip to tip, origin at the body.</summary>
        public static PaperShape Bat(float wingspan)
        {
            var breaks = new SortedSet<float>();
            foreach (var point in BatTop)
            {
                breaks.Add(point[0]);
            }

            foreach (var point in BatBottom)
            {
                breaks.Add(point[0]);
            }

            var half = new List<float>(breaks);
            int n = half.Count * 2 - 1;
            var xs = new float[n];
            var bottom = new float[n];
            var top = new float[n];
            for (int i = 0; i < n; i++)
            {
                int k = i - (half.Count - 1);
                float ax = half[Math.Abs(k)];
                xs[i] = (k < 0 ? -ax : ax) * wingspan;
                bottom[i] = Sample(BatBottom, ax) * wingspan;
                top[i] = Sample(BatTop, ax) * wingspan;
            }

            return Columns(xs, bottom, top, 0f);
        }

        /// <summary>
        /// Triangulates the area between two outlines sampled at the same x positions. With an
        /// <paramref name="inset"/> each column gets four vertices (edge, inner, inner, edge), otherwise two.
        /// </summary>
        public static PaperShape Columns(float[] xs, float[] bottom, float[] top, float inset)
        {
            int columns = xs.Length;
            int per = inset > 0f ? 4 : 2;
            int count = columns * per;
            var x = new float[count];
            var y = new float[count];
            var rimX = new float[count];
            var rimY = new float[count];
            var shade = new float[count];
            float lowest = float.MaxValue;
            float highest = float.MinValue;
            for (int c = 0; c < columns; c++)
            {
                lowest = Math.Min(lowest, bottom[c]);
                highest = Math.Max(highest, top[c]);
            }

            float span = Math.Max(highest - lowest, 1e-4f);
            for (int c = 0; c < columns; c++)
            {
                float thickness = Math.Max(top[c] - bottom[c], 0f);
                float step = Math.Min(inset, thickness / 3f);
                int before = Math.Max(c - 1, 0);
                int after = Math.Min(c + 1, columns - 1);
                float run = Math.Max(xs[after] - xs[before], 1e-5f);
                float slopeTop = (top[after] - top[before]) / run;
                float length = (float)Math.Sqrt(slopeTop * slopeTop + 1f);
                int v = c * per;
                for (int k = 0; k < per; k++)
                {
                    x[v + k] = xs[c];
                }

                if (per == 4)
                {
                    y[v] = bottom[c];
                    y[v + 1] = bottom[c] + step;
                    y[v + 2] = top[c] - step;
                    y[v + 3] = top[c];
                    rimY[v] = -UndersideRim;
                    rimX[v + 3] = -slopeTop / length;
                    rimY[v + 3] = 1f / length;
                }
                else
                {
                    y[v] = bottom[c];
                    y[v + 1] = top[c];
                }

                for (int k = 0; k < per; k++)
                {
                    shade[v + k] = (y[v + k] - lowest) / span;
                }
            }

            var triangles = new int[(columns - 1) * (per - 1) * 6];
            int t = 0;
            for (int c = 0; c < columns - 1; c++)
            {
                for (int k = 0; k < per - 1; k++)
                {
                    int a = c * per + k;
                    int b = a + per;
                    triangles[t++] = a;
                    triangles[t++] = b;
                    triangles[t++] = a + 1;
                    triangles[t++] = a + 1;
                    triangles[t++] = b;
                    triangles[t++] = b + 1;
                }
            }

            return new PaperShape(x, y, rimX, rimY, shade, triangles);
        }

        private static float Sample(float[][] outline, float x)
        {
            for (int i = 0; i < outline.Length - 1; i++)
            {
                float x0 = outline[i][0];
                float x1 = outline[i + 1][0];
                if (x <= x1)
                {
                    float t = x1 > x0 ? (x - x0) / (x1 - x0) : 0f;
                    return outline[i][1] + (outline[i + 1][1] - outline[i][1]) * t;
                }
            }

            return outline[outline.Length - 1][1];
        }
    }
}
