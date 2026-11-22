using NUnit.Framework;
using SpookyStreet.World;
using UnityEngine;

namespace SpookyStreet.Tests.Project
{
    /// <summary>The night sky surrounds the block from every angle and is visible from inside.</summary>
    public class SkyDomeTests
    {
        [Test]
        public void DomeFacesInwardAndEnclosesTheStreet()
        {
            var mesh = GreyboxBuilder.BuildSkyDomeMesh(GreyboxBuilder.SkyDomeRadius);
            try
            {
                var vertices = mesh.vertices;
                var triangles = mesh.triangles;
                int inward = 0;
                for (int i = 0; i < triangles.Length; i += 3)
                {
                    Vector3 a = vertices[triangles[i]], b = vertices[triangles[i + 1]], c = vertices[triangles[i + 2]];
                    Vector3 normal = Vector3.Cross(b - a, c - a);
                    Vector3 centre = (a + b + c) / 3f;
                    if (normal.sqrMagnitude < 1e-8f)
                    {
                        inward++; // degenerate pole triangle, culled anyway
                        continue;
                    }

                    if (Vector3.Dot(normal, centre) < 0f)
                    {
                        inward++;
                    }
                }

                Assert.AreEqual(triangles.Length / 3, inward, "every sky triangle must face the camera inside the dome");

                // Block corners, neighbour houses and the moon all sit inside the dome.
                Vector3 centreWorld = GreyboxBuilder.SkyDomeCentre;
                foreach (var p in new[]
                         {
                             new Vector3(-30f, 14f, -18f), new Vector3(30f, 14f, 34f), new Vector3(29f, 14f, 3f),
                             new Vector3(-21.5f, 14f, 23f), new Vector3(-8f, 13f, 33f),
                         })
                {
                    Assert.Less((p - centreWorld).magnitude, GreyboxBuilder.SkyDomeRadius, $"{p} is outside the sky");
                }
            }
            finally
            {
                Object.DestroyImmediate(mesh);
            }
        }
    }
}
