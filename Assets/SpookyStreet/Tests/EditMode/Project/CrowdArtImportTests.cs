using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Animation;
using SpookyStreet.Core.Crowd;
using SpookyStreet.EditorTools;
using UnityEditor;
using UnityEngine;

namespace SpookyStreet.Tests.Project
{
    /// <summary>
    /// The crowd costume kit as Unity imports it (docs/living-street-crowd-art.md): two Generic-rig bodies with
    /// readable meshes (merged per walker at spawn), the four clips, every part of <see cref="CrowdKit"/> and its LOD1
    /// as a skinned mesh on one shared skeleton (same bind pose for every bone, so parts merge into one mesh with
    /// one bone array), each walker inside the iPhone budget, and the one shared atlas. Missing files skip.
    /// </summary>
    public class CrowdArtImportTests
    {
        private const string Folder = "Assets/SpookyStreet/Content/Resources/" + CrowdKit.Folder + "/";

        private static GameObject Load(string model)
        {
            var prefab = Resources.Load<GameObject>(CrowdKit.Folder + "/" + model);
            if (prefab == null)
            {
                Assert.Ignore(model + ".fbx is not committed.");
            }

            return prefab;
        }

        private static Dictionary<string, SkinnedMeshRenderer> Skins(GameObject prefab)
        {
            var map = new Dictionary<string, SkinnedMeshRenderer>();
            foreach (var smr in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                map[smr.name] = smr;
            }

            return map;
        }

        private static long Triangles(Mesh mesh)
        {
            long total = 0;
            for (int i = 0; mesh != null && i < mesh.subMeshCount; i++)
            {
                total += mesh.GetIndexCount(i) / 3;
            }

            return total;
        }

        [Test]
        public void CrowdModelsAreReadableCharacters()
        {
            Assert.IsTrue(ModelImportRules.IsCrowdPath(Folder + "CrowdChild.fbx"));
            Assert.IsTrue(ModelImportRules.IsCharacterPath(Folder + "CrowdAdult.fbx"));
            Assert.IsFalse(ModelImportRules.IsCrowdPath(ModelImportRules.ModelsRoot + "Characters/Pip/Pip.fbx"));
            Assert.IsFalse(ModelImportRules.IsStaticBatchedPath(Folder + "CrowdChild.fbx"));
        }

        [TestCase(CrowdKit.ChildModel)]
        [TestCase(CrowdKit.AdultModel)]
        public void ModelImportsAsAReadableGenericRigWithTheFourClips(string model)
        {
            var importer = AssetImporter.GetAtPath(Folder + model + ".fbx") as ModelImporter;
            if (importer == null)
            {
                Assert.Ignore(model + ".fbx is not committed.");
            }

            Assert.AreEqual(ModelImporterAnimationType.Generic, importer.animationType);
            Assert.AreEqual(ModelImporterMaterialImportMode.None, importer.materialImportMode);
            Assert.IsTrue(importer.isReadable, "parts are merged on the CPU at spawn");
            var names = new List<string>();
            foreach (var clip in importer.clipAnimations)
            {
                StringAssert.DoesNotContain("|", clip.name);
                Assert.AreEqual(CharacterClips.IsLooping(clip.name), clip.loopTime, clip.name + " loop flag");
                names.Add(clip.name);
            }

            CollectionAssert.IsSubsetOf(CrowdKit.Clips, names);
            Assert.IsTrue(CharacterClips.IsLooping("Walk") && CharacterClips.IsLooping("Talk"));
            Assert.IsFalse(CharacterClips.IsLooping("Wave"));
        }

        [TestCase(CrowdKit.ChildModel, 0.7f, 0.95f)]
        [TestCase(CrowdKit.AdultModel, 1.3f, 1.55f)]
        public void EveryPartIsSkinnedToOneSharedSkeleton(string model, float headMin, float headMax)
        {
            var prefab = Load(model);
            var bones = new Dictionary<string, Transform>();
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
            {
                bones[t.name] = t;
            }

            foreach (var b in CrowdKit.CostumeBones)
            {
                Assert.IsTrue(bones.ContainsKey(b), model + " has no costume bone " + b);
            }

            Assert.IsTrue(bones.ContainsKey("Head"), model + " has no Head bone");
            Assert.That(bones["Head"].position.y, Is.InRange(headMin, headMax), model + " head height");
            var skins = Skins(prefab);
            var bindposes = new Dictionary<string, Matrix4x4>();
            foreach (var part in CrowdKit.PartsOf(model))
            {
                foreach (var name in new[] { part.Name, part.Name + CrowdKit.Lod1Suffix })
                {
                    Assert.IsTrue(skins.TryGetValue(name, out var smr), $"{model} has no skinned mesh {name}");
                    Assert.IsNotNull(smr.sharedMesh, name);
                    Assert.Greater(smr.sharedMesh.vertexCount, 0, name);
                    Assert.AreEqual(smr.bones.Length, smr.sharedMesh.bindposes.Length, name);
                    Assert.AreEqual(1, smr.sharedMesh.subMeshCount, name + ": one material (the shared atlas)");
                    for (int i = 0; i < smr.bones.Length; i++)
                    {
                        Assert.IsNotNull(smr.bones[i], name + " bone " + i);
                        Assert.IsTrue(bones.ContainsKey(smr.bones[i].name), name + ": foreign bone " + smr.bones[i].name);
                        var bp = smr.sharedMesh.bindposes[i];
                        if (bindposes.TryGetValue(smr.bones[i].name, out var seen))
                        {
                            for (int k = 0; k < 16; k++)
                            {
                                Assert.AreEqual(seen[k], bp[k], 1e-4f, $"{name}: bind pose of {smr.bones[i].name}");
                            }
                        }
                        else
                        {
                            bindposes[smr.bones[i].name] = bp;
                        }
                    }
                }
            }
        }

        [Test]
        public void EveryWalkerFitsTheIphoneBudget()
        {
            var prefabs = new Dictionary<string, Dictionary<string, SkinnedMeshRenderer>>
            {
                [CrowdKit.ChildModel] = Skins(Load(CrowdKit.ChildModel)),
                [CrowdKit.AdultModel] = Skins(Load(CrowdKit.AdultModel)),
            };
            foreach (var w in CrowdKit.Walkers)
            {
                var skins = prefabs[w.Model];
                for (int lod = 0; lod < 2; lod++)
                {
                    long tris = 0;
                    foreach (var name in CrowdKit.MeshNames(w, lod))
                    {
                        Assert.IsTrue(skins.ContainsKey(name), $"{w.Id}: {name} missing");
                        tris += Triangles(skins[name].sharedMesh);
                    }

                    int max = lod == 0 ? CrowdKit.MaxLod0Triangles : CrowdKit.MaxLod1Triangles;
                    Assert.LessOrEqual(tris, max, $"{w.Id} LOD{lod} triangles");
                    Assert.Greater(tris, max / 4, $"{w.Id} LOD{lod} looks empty");
                }
            }
        }

        [Test]
        public void TheSharedAtlasImportsWithItsRoles()
        {
            var colour = AssetImporter.GetAtPath(Folder + CrowdKit.Atlas + "_BaseColor.png") as TextureImporter;
            if (colour == null)
            {
                Assert.Ignore("the crowd atlas is not committed.");
            }

            Assert.IsTrue(colour.sRGBTexture);
            Assert.AreEqual(TextureImporterType.Default, colour.textureType);
            var normal = AssetImporter.GetAtPath(Folder + CrowdKit.Atlas + "_Normal.png") as TextureImporter;
            Assert.IsNotNull(normal);
            Assert.AreEqual(TextureImporterType.NormalMap, normal.textureType);
            Assert.IsNotNull(AssetImporter.GetAtPath(Folder + CrowdKit.Atlas + "_Emission.png") as TextureImporter);
            var tex = Resources.Load<Texture2D>(CrowdKit.Folder + "/" + CrowdKit.Atlas + "_BaseColor");
            Assert.IsNotNull(tex);
            Assert.AreEqual(CrowdKit.AtlasSize, tex.width);
            // ModelKit's fallback atlas: the bodies have no texture set of their own, so FallbackAtlas = "Crowd" applies
            Assert.IsNull(Resources.Load<Texture2D>(CrowdKit.Folder + "/" + CrowdKit.ChildModel + "_BaseColor"));
        }
    }
}
