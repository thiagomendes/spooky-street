using NUnit.Framework;
using SpookyStreet.Core.World;
using SpookyStreet.World;
using UnityEditor;
using UnityEngine;

namespace SpookyStreet.Tests.Project
{
    /// <summary>
    /// The night sky surrounds the block from every angle and is visible from inside; the paper moon, its halo and
    /// the cloud rings sit between the neighbour houses and the dome; the sky shaders ship with the game.
    /// </summary>
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
                             new Vector3(-21.5f, 14f, 23f), Atmosphere.MoonPosition,
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

        [Test]
        public void MoonAndHaloSitJustInsideTheDome()
        {
            Vector3 centre = GreyboxBuilder.SkyDomeCentre;
            float moonRadius = Atmosphere.MoonDiameter * 0.5f;
            Assert.Greater(Atmosphere.MoonDiameter, 4.2f, "a big full moon (the greybox disc was 4.2 m)");
            Quaternion facing = Quaternion.LookRotation(Atmosphere.MoonPosition - Atmosphere.MoonFacing);
            for (int i = 0; i < 32; i++)
            {
                float angle = i * Mathf.PI / 16f;
                Vector3 rim = Atmosphere.MoonPosition +
                              facing * new Vector3(Mathf.Cos(angle), Mathf.Sin(angle), 0f) * (moonRadius * SkyModel.HaloRadii);
                Assert.Less((rim - centre).magnitude, GreyboxBuilder.SkyDomeRadius - 1f, "the halo card must not cut the dome");
                Assert.Greater(rim.y, 0f, "the halo stays above the ground");
            }

            // Shooting stars travel between the moon and the dome.
            Assert.Less(MagicSky.ShootingStarRadius, GreyboxBuilder.SkyDomeRadius - 1f);
            Assert.Greater(MagicSky.ShootingStarRadius, (Atmosphere.MoonPosition - centre).magnitude);
        }

        [Test]
        public void CloudRingsFaceInwardBetweenTheHousesAndTheDome()
        {
            Vector3 centre = GreyboxBuilder.SkyDomeCentre;
            var axis = new Vector3(centre.x, 0f, centre.z);
            for (int layerIndex = 0; layerIndex < SkyModel.Layers.Length; layerIndex++)
            {
                CloudLayer layer = SkyModel.Layers[layerIndex];
                Mesh mesh = SkyMeshes.CloudRing(layer, layerIndex, layer.Height);
                try
                {
                    Vector3[] vertices = mesh.vertices;
                    Vector3[] normals = mesh.normals;
                    int[] triangles = mesh.triangles;
                    Assert.AreEqual(vertices.Length, mesh.colors.Length, "clouds are shaded by vertex colour");
                    Assert.Less(triangles.Length / 3, 6000, "a ring stays a few thousand triangles");
                    for (int i = 0; i < vertices.Length; i++)
                    {
                        Vector3 world = axis + vertices[i];
                        Assert.Less((world - centre).magnitude, GreyboxBuilder.SkyDomeRadius - 1f,
                            $"ring {layerIndex}: a cloud pokes through the dome");
                        float fromAxis = new Vector2(vertices[i].x, vertices[i].z).magnitude;
                        Assert.Greater(fromAxis, 32f, $"ring {layerIndex}: clouds stay beyond the neighbour houses");
                        Assert.Greater(world.y, 4f, $"ring {layerIndex}: clouds stay off the ground");
                        Assert.LessOrEqual(normals[i].magnitude, 1.001f, "rim directions are at most unit length");
                    }

                    for (int t = 0; t < triangles.Length; t += 3)
                    {
                        Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                        Vector3 normal = Vector3.Cross(b - a, c - a);
                        if (normal.sqrMagnitude < 1e-8f)
                        {
                            continue; // zero-height slivers at a cloud's pointed ends
                        }

                        Vector3 middle = (a + b + c) / 3f;
                        Assert.Less(Vector3.Dot(normal, new Vector3(middle.x, 0f, middle.z)), 0f,
                            $"ring {layerIndex}: every cloud triangle must face the viewer inside the ring");
                    }
                }
                finally
                {
                    Object.DestroyImmediate(mesh);
                }
            }
        }

        [Test]
        public void TheMiddleRingsFirstCloudPassesBetweenTheArrivalPorchAndTheMoon()
        {
            CloudLayer layer = SkyModel.Layers[SkyModel.MoonLayer];
            Assert.IsTrue(MagicSky.CrossingPoint(MagicSky.ReferenceView, Atmosphere.MoonPosition, layer.Radius,
                out float azimuth, out float height), "the moon must lie beyond the middle cloud ring");
            Vector3 centre = GreyboxBuilder.SkyDomeCentre;
            float radians = azimuth * Mathf.Deg2Rad;
            var crossing = new Vector3(centre.x + Mathf.Sin(radians) * layer.Radius, height,
                centre.z + Mathf.Cos(radians) * layer.Radius);
            Vector3 toMoon = Atmosphere.MoonPosition - MagicSky.ReferenceView;
            Vector3 toCrossing = crossing - MagicSky.ReferenceView;
            Assert.Less(Vector3.Angle(toMoon, toCrossing), 0.1f, "the crossing point is on the line of sight to the moon");
            Assert.Less(toCrossing.magnitude, toMoon.magnitude - 2f, "and clearly in front of it");
            Assert.That(height, Is.InRange(12f, 24f), "above the neighbours' roofs");
        }

        [Test]
        public void FlatSkyPiecesFaceTheViewer()
        {
            Mesh[] meshes =
            {
                SkyMeshes.Disc(4f, 32), SkyMeshes.Halo(4f, 32, Color.white),
                SkyMeshes.Flat(SkyShapes.Bat(2f), Color.black, "Bat"),
            };
            try
            {
                foreach (Mesh mesh in meshes)
                {
                    Vector3[] vertices = mesh.vertices;
                    int[] triangles = mesh.triangles;
                    Assert.Greater(triangles.Length, 0, mesh.name);
                    for (int t = 0; t < triangles.Length; t += 3)
                    {
                        Vector3 a = vertices[triangles[t]], b = vertices[triangles[t + 1]], c = vertices[triangles[t + 2]];
                        Vector3 normal = Vector3.Cross(b - a, c - a);
                        Assert.LessOrEqual(normal.z, 1e-6f, mesh.name + ": pieces face local −Z, toward the block");
                    }
                }

                Mesh streak = SkyMeshes.Streak(Color.white, out Vector3[] streakVertices);
                Assert.AreEqual(streak.vertexCount, streakVertices.Length);
                Assert.AreEqual(-SkyMeshes.StreakLength, streakVertices[0].x, 1e-5f, "vertex 0 is the end of the tail");
                Assert.AreEqual(0f, streak.colors[0].a, "the tail fades to nothing");
                Object.DestroyImmediate(streak);
            }
            finally
            {
                foreach (Mesh mesh in meshes)
                {
                    Object.DestroyImmediate(mesh);
                }
            }
        }

        [Test]
        public void SkyShadersShipThroughResourcesMaterials()
        {
            foreach (var (path, shaderName) in new[]
                     {
                         (MagicSky.DomeMaterialPath, MagicSky.DomeShaderName),
                         (MagicSky.PaperMaterialPath, MagicSky.PaperShaderName),
                     })
            {
                var material = Resources.Load<Material>(path);
                Assert.IsNotNull(material, $"Resources/{path}.mat keeps {shaderName} in player builds");
                Assert.IsNotNull(material.shader, path);
                Assert.AreEqual(shaderName, material.shader.name, $"{path} must reference the sky shader");
                Assert.AreEqual(material.shader, Shader.Find(shaderName));
                Assert.IsFalse(ShaderUtil.ShaderHasError(material.shader), $"{shaderName} has compile errors");
                Assert.AreEqual(1, material.shader.passCount, $"{shaderName}: one pass, one draw");
                Assert.IsEmpty(material.shaderKeywords, $"{shaderName}: no keywords, so no variants to strip");
                Assert.IsTrue(material.HasProperty("_MaxValue"), "sky shaders clamp under the bloom threshold");
                Assert.Less(material.GetFloat("_MaxValue"), 1f);
            }
        }
    }
}
