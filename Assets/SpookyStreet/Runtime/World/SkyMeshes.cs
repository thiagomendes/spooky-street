using System.Collections.Generic;
using SpookyStreet.Core.World;
using UnityEngine;

namespace SpookyStreet.World
{
    /// <summary>
    /// Meshes for the handcrafted sky, built once at load from the engine-free shapes in
    /// <see cref="SkyShapes"/>: opaque cut-paper pieces (no alpha overdraw) plus the two soft pieces, the moon's
    /// halo and the shooting star's streak, whose softness is vertex alpha. Vertex colours are sRGB (the sky paper
    /// shader converts them); vertex normals carry the cut edge's rim direction, zero where no rim may show.
    /// Flat pieces face their local −Z (toward the viewer); cloud rings face the dome's axis.
    /// </summary>
    public static class SkyMeshes
    {
        /// <summary>Metres of cloud per repeat of the paper grain tile.</summary>
        public const float GrainMetres = 6f;

        /// <summary>The back sheet of each cloud sits this far behind the front one.</summary>
        public const float SheetGap = 0.5f;

        public const float StreakLength = 9f;
        public const float StreakWidth = 0.5f;
        public const float StreakSparkle = 0.9f;

        /// <summary>A disc in the local XY plane whose UVs map the texture's inscribed circle.</summary>
        public static Mesh Disc(float radius, int segments)
        {
            var vertices = new List<Vector3> { Vector3.zero };
            var uvs = new List<Vector2> { new Vector2(0.5f, 0.5f) };
            var triangles = new List<int>();
            for (int s = 0; s < segments; s++)
            {
                float angle = s * Mathf.PI * 2f / segments;
                var unit = new Vector2(Mathf.Cos(angle), Mathf.Sin(angle));
                vertices.Add(new Vector3(unit.x * radius, unit.y * radius, 0f));
                uvs.Add(new Vector2(0.5f, 0.5f) + unit * 0.495f);
                triangles.Add(0);
                triangles.Add(1 + (s + 1) % segments);
                triangles.Add(1 + s);
            }

            return Build("PaperMoon", vertices, null, Fill(vertices.Count, Color.white), uvs, triangles);
        }

        /// <summary>
        /// The moon's halo: a ring from just inside the disc out to <see cref="SkyModel.HaloRadii"/> moon radii,
        /// fading through vertex alpha.
        /// </summary>
        public static Mesh Halo(float moonRadius, int segments, Color colour)
        {
            float[] radii = { 0.9f, 1.3f, 1.85f, SkyModel.HaloRadii };
            float[] alpha = { SkyModel.HaloOpacity, SkyModel.HaloOpacity * 0.5f, SkyModel.HaloOpacity * 0.17f, 0f };
            var vertices = new List<Vector3>();
            var colours = new List<Color>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            for (int r = 0; r < radii.Length; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    float angle = s * Mathf.PI * 2f / segments;
                    vertices.Add(new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * (radii[r] * moonRadius));
                    colours.Add(new Color(colour.r, colour.g, colour.b, alpha[r]));
                    uvs.Add(new Vector2(0.5f, 0.5f));
                }
            }

            for (int r = 0; r < radii.Length - 1; r++)
            {
                for (int s = 0; s < segments; s++)
                {
                    int a = r * segments + s;
                    int b = r * segments + (s + 1) % segments;
                    int c = a + segments;
                    int d = b + segments;
                    triangles.Add(a);
                    triangles.Add(b);
                    triangles.Add(c);
                    triangles.Add(b);
                    triangles.Add(d);
                    triangles.Add(c);
                }
            }

            return Build("MoonHalo", vertices, null, colours, uvs, triangles);
        }

        /// <summary>A flat cut shape in one colour, in the local XY plane (the paper bats).</summary>
        public static Mesh Flat(PaperShape shape, Color colour, string name)
        {
            var vertices = new List<Vector3>(shape.VertexCount);
            var uvs = new List<Vector2>(shape.VertexCount);
            for (int i = 0; i < shape.VertexCount; i++)
            {
                vertices.Add(new Vector3(shape.X[i], shape.Y[i], 0f));
                uvs.Add(new Vector2(shape.X[i], shape.Y[i]));
            }

            return Build(name, vertices, null, Fill(vertices.Count, colour), uvs, Flipped(shape.Triangles, 0, null));
        }

        /// <summary>Body colour of a cloud ring, for the flat fallback material.</summary>
        public static Color LayerColour(CloudLayer layer) =>
            Color.Lerp(Color.Lerp(Palette.CloudShade, Palette.CloudBody, 0.5f), Palette.CloudHaze, layer.Mist * 0.7f);

        /// <summary>
        /// One ring of paper clouds wrapped around the dome's axis (the mesh's origin sits on the axis at world
        /// height 0). Each cloud is two sheets: a lighter, taller one behind and a darker one in front. The first
        /// cloud sits at azimuth 0 with its base at <paramref name="firstCloudBase"/>; the others are spread around
        /// the ring at slightly different heights and sizes.
        /// </summary>
        public static Mesh CloudRing(CloudLayer layer, int layerIndex, float firstCloudBase)
        {
            var vertices = new List<Vector3>();
            var normals = new List<Vector3>();
            var colours = new List<Color>();
            var uvs = new List<Vector2>();
            var triangles = new List<int>();
            float haze = layer.Mist * 0.7f;
            Color frontLow = Color.Lerp(Palette.CloudShade, Palette.CloudHaze, haze);
            Color frontHigh = Color.Lerp(Palette.CloudBody, Palette.CloudHaze, haze);
            Color backLow = Color.Lerp(Palette.CloudBackShade, Palette.CloudHaze, haze);
            Color backHigh = Color.Lerp(Palette.CloudBack, Palette.CloudHaze, haze);
            var random = new System.Random(4201 + layerIndex * 97);
            for (int c = 0; c < layer.Clouds; c++)
            {
                float spread = 360f / layer.Clouds;
                float azimuth = c == 0 ? 0f : c * spread + ((float)random.NextDouble() - 0.5f) * spread * 0.35f;
                float baseY = c == 0 ? firstCloudBase : layer.Height + ((float)random.NextDouble() - 0.5f) * layer.CloudHeight;
                float width = layer.CloudWidth * (0.85f + 0.3f * (float)random.NextDouble());
                float height = layer.CloudHeight * (0.9f + 0.2f * (float)random.NextDouble());
                int seed = 100 + layerIndex * 31 + c * 7;
                float lean = ((float)random.NextDouble() - 0.5f) * width * 0.24f;

                PaperShape back = SkyShapes.Cloud(seed + 3, width * 0.78f, height * 1.22f);
                AddSheet(back, layer.Radius + SheetGap, azimuth, lean, baseY + height * 0.1f, backLow, backHigh, seed * 0.37f,
                    vertices, normals, colours, uvs, triangles);
                PaperShape front = SkyShapes.Cloud(seed, width, height);
                AddSheet(front, layer.Radius, azimuth, 0f, baseY, frontLow, frontHigh, seed * 0.61f, vertices, normals,
                    colours, uvs, triangles);
            }

            return Build("PaperClouds_" + layerIndex, vertices, normals, colours, uvs, triangles);
        }

        /// <summary>
        /// The shooting star: a tapering trail behind a four-point sparkle, in the local XY plane, travelling
        /// toward +X with the head at the origin. Vertex 0 is the tail's end; move it to change the trail length
        /// (<paramref name="vertices"/> is the array to edit and hand back with <c>SetVertices</c>).
        /// </summary>
        public static Mesh Streak(Color colour, out Vector3[] vertices)
        {
            float half = StreakWidth * 0.5f;
            float tip = StreakSparkle;
            float waist = StreakSparkle * 0.13f;
            vertices = new[]
            {
                new Vector3(-StreakLength, 0f, 0f), new Vector3(-0.2f, half, 0f), new Vector3(-0.2f, -half, 0f),
                new Vector3(0.25f, 0f, 0f),
                Vector3.zero,
                new Vector3(tip, 0f, 0f), new Vector3(0f, tip, 0f), new Vector3(-tip, 0f, 0f), new Vector3(0f, -tip, 0f),
                new Vector3(waist, waist, 0f), new Vector3(-waist, waist, 0f), new Vector3(-waist, -waist, 0f),
                new Vector3(waist, -waist, 0f),
            };
            float[] alpha = { 0f, 0.8f, 0.8f, 1f, 1f, 0f, 0f, 0f, 0f, 0.9f, 0.9f, 0.9f, 0.9f };
            var colours = new List<Color>(vertices.Length);
            var uvs = new List<Vector2>(vertices.Length);
            for (int i = 0; i < vertices.Length; i++)
            {
                colours.Add(new Color(colour.r, colour.g, colour.b, alpha[i]));
                uvs.Add(new Vector2(0.5f, 0.5f));
            }

            var triangles = new List<int> { 0, 1, 2, 1, 3, 2 };
            for (int arm = 0; arm < 4; arm++)
            {
                // Each arm: centre, the waist point before it, its tip, the waist point after it.
                int point = 5 + arm;
                int before = 9 + (arm + 3) % 4;
                int after = 9 + arm;
                triangles.Add(4);
                triangles.Add(before);
                triangles.Add(point);
                triangles.Add(4);
                triangles.Add(point);
                triangles.Add(after);
            }

            Mesh mesh = Build("ShootingStar", new List<Vector3>(vertices), null, colours, uvs, triangles);
            mesh.MarkDynamic();
            return mesh;
        }

        private static void AddSheet(PaperShape shape, float radius, float azimuthDegrees, float shift, float baseY,
            Color low, Color high, float grainOffset, List<Vector3> vertices, List<Vector3> normals, List<Color> colours,
            List<Vector2> uvs, List<int> triangles)
        {
            int first = vertices.Count;
            float centre = azimuthDegrees * Mathf.Deg2Rad;
            for (int i = 0; i < shape.VertexCount; i++)
            {
                float azimuth = centre + (shape.X[i] + shift) / radius;
                float sin = Mathf.Sin(azimuth);
                float cos = Mathf.Cos(azimuth);
                vertices.Add(new Vector3(sin * radius, baseY + shape.Y[i], cos * radius));
                // Rim direction: along the ring's tangent and straight up.
                normals.Add(new Vector3(cos * shape.RimX[i], shape.RimY[i], -sin * shape.RimX[i]));
                colours.Add(Color.Lerp(low, high, shape.Shade[i]));
                uvs.Add(new Vector2(shape.X[i] / GrainMetres + grainOffset, shape.Y[i] / GrainMetres));
            }

            Flipped(shape.Triangles, first, triangles);
        }

        /// <summary>
        /// The shapes wind counter-clockwise seen from their +Z; the sky looks at them from the other side, so
        /// every triangle is reversed (Unity front faces: Cross(v1 − v0, v2 − v0) points at the viewer).
        /// </summary>
        private static List<int> Flipped(int[] source, int offset, List<int> target)
        {
            target ??= new List<int>(source.Length);
            for (int i = 0; i < source.Length; i += 3)
            {
                target.Add(offset + source[i]);
                target.Add(offset + source[i + 2]);
                target.Add(offset + source[i + 1]);
            }

            return target;
        }

        private static List<Color> Fill(int count, Color colour)
        {
            var colours = new List<Color>(count);
            for (int i = 0; i < count; i++)
            {
                colours.Add(colour);
            }

            return colours;
        }

        private static Mesh Build(string name, List<Vector3> vertices, List<Vector3> normals, List<Color> colours,
            List<Vector2> uvs, List<int> triangles)
        {
            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            if (normals == null)
            {
                normals = new List<Vector3>(vertices.Count);
                for (int i = 0; i < vertices.Count; i++)
                {
                    normals.Add(Vector3.zero); // no rim light
                }
            }

            mesh.SetNormals(normals);
            mesh.SetColors(colours);
            mesh.SetUVs(0, uvs);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
