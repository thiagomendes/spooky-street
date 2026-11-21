using UnityEngine;

namespace SpookyStreet.World
{
    /// <summary>Tiny procedural meshes the primitive set lacks (gable roofs, triangular wall fills).</summary>
    public static class MeshFactory
    {
        /// <summary>
        /// Triangular prism: base width along X, apex height along Y, depth along Z, origin at the centre of the
        /// base. Flat-shaded (duplicated vertices) so faces read as painted planks.
        /// </summary>
        public static Mesh Prism(float width, float height, float depth)
        {
            float w = width * 0.5f;
            float d = depth * 0.5f;
            var a = new Vector3(-w, 0f, -d);
            var b = new Vector3(w, 0f, -d);
            var c = new Vector3(0f, height, -d);
            var a2 = new Vector3(-w, 0f, d);
            var b2 = new Vector3(w, 0f, d);
            var c2 = new Vector3(0f, height, d);

            Vector3[] vertices =
            {
                a, c, b, // front (faces -Z)
                a2, b2, c2, // back
                a, a2, c2, a, c2, c, // left slope
                b, c, c2, b, c2, b2, // right slope
                a, b, b2, a, b2, a2, // bottom
            };
            // Make every face wind outward (Unity front faces: Cross(v1 - v0, v2 - v0) points at the viewer).
            var centroid = new Vector3(0f, height / 3f, 0f);
            for (int i = 0; i < vertices.Length; i += 3)
            {
                Vector3 normal = Vector3.Cross(vertices[i + 1] - vertices[i], vertices[i + 2] - vertices[i]);
                Vector3 centre = (vertices[i] + vertices[i + 1] + vertices[i + 2]) / 3f;
                if (Vector3.Dot(normal, centre - centroid) < 0f)
                {
                    (vertices[i + 1], vertices[i + 2]) = (vertices[i + 2], vertices[i + 1]);
                }
            }

            var triangles = new int[vertices.Length];
            for (int i = 0; i < triangles.Length; i++)
            {
                triangles[i] = i;
            }

            var mesh = new Mesh { name = "Prism", vertices = vertices, triangles = triangles };
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }
    }
}
