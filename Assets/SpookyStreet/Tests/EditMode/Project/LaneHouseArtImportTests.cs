using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Animation;
using SpookyStreet.Core.Crowd;
using SpookyStreet.Core.World;
using SpookyStreet.EditorTools;
using UnityEditor;
using UnityEngine;

namespace SpookyStreet.Tests.Project
{
    /// <summary>
    /// The east lane's porch houses as Unity imports them (docs/living-street-houses-art.md, <see cref="LaneHouseArt"/>):
    /// each house a near and a far mesh in one LOD group with its lit parts beside them, inside its footprint and clear of
    /// the visitor's stand, its door where the slice's door is, in the heart's houses' triangle range; each leaf hinged on
    /// its edge; each lamp lit under the attic window; one atlas per house shared with its leaf and its lamp; and the door
    /// neighbours as crowd kit parts on the grown-up body (same skeleton and bind pose as the walkers' grown-up, so they
    /// merge into one mesh and play the kit's clips). Missing files skip, like the other model import tests.
    /// <para>Measures are taken in the prefab's world frame (Unity axes, Y up, front toward -Z).</para>
    /// </summary>
    public class LaneHouseArtImportTests
    {
        private const string Folder = ModelImportRules.ModelsRoot + "Environment/";
        private const string CrowdFolder = "Assets/SpookyStreet/Content/Resources/" + CrowdKit.Folder + "/";

        private static GameObject Load(string folder, string model)
        {
            var prefab = Resources.Load<GameObject>(folder + "/" + model);
            if (prefab == null)
            {
                Assert.Ignore(model + ".fbx is not committed.");
            }

            return prefab;
        }

        private static Dictionary<string, Transform> Children(GameObject prefab)
        {
            var map = new Dictionary<string, Transform>();
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
            {
                map[t.name] = t;
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

        private static Bounds Bounds(IEnumerable<MeshFilter> filters)
        {
            bool any = false;
            var box = new Bounds();
            foreach (var filter in filters)
            {
                if (filter.sharedMesh == null)
                {
                    continue;
                }

                var local = filter.sharedMesh.bounds;
                for (int i = 0; i < 8; i++)
                {
                    var corner = local.center + Vector3.Scale(local.extents,
                        new Vector3((i & 1) == 0 ? -1f : 1f, (i & 2) == 0 ? -1f : 1f, (i & 4) == 0 ? -1f : 1f));
                    var p = filter.transform.TransformPoint(corner);
                    if (!any)
                    {
                        box = new Bounds(p, Vector3.zero);
                        any = true;
                    }
                    else
                    {
                        box.Encapsulate(p);
                    }
                }
            }

            return box;
        }

        private static Bounds ModelBounds(GameObject prefab) => Bounds(prefab.GetComponentsInChildren<MeshFilter>(true));

        private static IEnumerable<string> Models()
        {
            foreach (string id in LaneHouseArt.Houses)
            {
                yield return LaneHouseArt.House(id);
                yield return LaneHouseArt.DoorLeaf(id);
                yield return LaneHouseArt.PorchLamp(id);
            }
        }

        [Test]
        public void ModelsImportAsPropsWithoutImportedMaterials()
        {
            foreach (string model in Models())
            {
                string path = Folder + model + ".fbx";
                var importer = AssetImporter.GetAtPath(path) as ModelImporter;
                if (importer == null)
                {
                    Assert.Ignore(path + " is not committed.");
                }

                Assert.IsTrue(ModelImportRules.IsGameModelPath(path), path);
                Assert.IsFalse(ModelImportRules.IsCharacterPath(path), path + ": a prop, not a rig");
                Assert.IsFalse(ModelImportRules.IsStaticBatchedPath(path), path + ": not a street-set model");
                Assert.AreEqual(model.StartsWith("LaneHouse"), ModelImportRules.IsLaneHousePath(path), path);
                Assert.AreEqual(ModelImporterAnimationType.None, importer.animationType, path);
                Assert.AreEqual(ModelImporterMaterialImportMode.None, importer.materialImportMode, path);
                Assert.IsTrue(importer.bakeAxisConversion, path);
            }
        }

        [Test]
        public void EachHouseIsANearAndAFarMeshInOneLodGroupInTheHeartsRange()
        {
            foreach (string id in LaneHouseArt.Houses)
            {
                string model = LaneHouseArt.House(id);
                var prefab = Load(LaneHouseArt.Folder, model);
                var parts = Children(prefab);
                Assert.IsTrue(parts.ContainsKey(model + LaneHouseArt.Lod0Suffix), model + ": near mesh");
                Assert.IsTrue(parts.ContainsKey(model + LaneHouseArt.Lod1Suffix), model + ": far mesh");
                Assert.IsTrue(parts.ContainsKey(LaneHouseArt.RoomGlow), model + ": lit windows and hall");
                var near = parts[model + LaneHouseArt.Lod0Suffix].GetComponent<MeshFilter>().sharedMesh;
                var far = parts[model + LaneHouseArt.Lod1Suffix].GetComponent<MeshFilter>().sharedMesh;
                Assert.That(Triangles(near), Is.InRange(5000L, (long)LaneHouseArt.MaxLod0Triangles),
                    model + " LOD0: between the skyline houses (5k) and the owl house (26k)");
                Assert.That(Triangles(far), Is.InRange(1500L, (long)LaneHouseArt.MaxLod1Triangles), model + " LOD1");
                Assert.Less(Triangles(far) * 3, Triangles(near), model + ": the far mesh is far lighter");
                var group = prefab.GetComponentInChildren<LODGroup>(true);
                Assert.IsNotNull(group, model + ": LOD group");
                var lods = group.GetLODs();
                Assert.AreEqual(2, lods.Length, model);
                Assert.AreEqual(LaneHouseArt.Lod0ScreenHeight, lods[0].screenRelativeTransitionHeight, 1e-3f, model);
                Assert.AreEqual(model + LaneHouseArt.Lod0Suffix, lods[0].renderers[0].name);
                Assert.AreEqual(model + LaneHouseArt.Lod1Suffix, lods[1].renderers[0].name);
                foreach (var name in parts.Keys)
                {
                    if (GlowLevels.IsGlowPart(name))
                    {
                        Assert.Less(GlowLevels.IntensityFor(name), GlowLevels.AtticWindow * 0.5f, model + "/" + name + ": under the attic window");
                    }
                }
            }
        }

        [Test]
        public void EachHouseFitsItsFootprintDoorAndStand()
        {
            foreach (string id in LaneHouseArt.Houses)
            {
                string model = LaneHouseArt.House(id);
                var prefab = Load(LaneHouseArt.Folder, model);
                LaneHouseArt.Footprint(id, out float width, out float depth);
                LaneHouseArt.Door(id, out float across, out float wall);
                foreach (string level in new[] { LaneHouseArt.Lod0Suffix, LaneHouseArt.Lod1Suffix })
                {
                    var mesh = Children(prefab)[model + level].GetComponent<MeshFilter>();
                    var box = Bounds(new[] { mesh });
                    Assert.That(box.min.y, Is.InRange(-0.05f, 0.02f), model + level + ": stands on the ground");
                    Assert.That(box.size.y, Is.InRange(5f, 13f), model + level + ": height");
                    Assert.GreaterOrEqual(box.min.x, -width / 2f - 0.05f, model + level + ": footprint (x)");
                    Assert.LessOrEqual(box.max.x, width / 2f + 0.05f, model + level + ": footprint (x)");
                    Assert.LessOrEqual(box.max.z, depth / 2f + 0.05f, model + level + ": footprint (back)");
                    Assert.Greater(box.min.z, LaneHouseArt.Stand(id) + 0.3f, model + level + ": the visitor's stand is clear");
                }

                // the door: a lit hall behind the opening, centred on the slice's door, behind the wall's face
                var hall = Bounds(new[] { Children(prefab)[LaneHouseArt.RoomGlow].GetComponent<MeshFilter>() });
                Assert.Greater(hall.max.z, wall + 0.5f, model + ": the hall reaches into the house");
            }
        }

        [Test]
        public void EachLeafHangsOnItsHingeEdge()
        {
            foreach (string id in LaneHouseArt.Houses)
            {
                var prefab = Load(LaneHouseArt.Folder, LaneHouseArt.DoorLeaf(id));
                var box = ModelBounds(prefab);
                Assert.That(box.min.x, Is.InRange(-0.02f, 0.03f), id + ": the hinge edge at the origin");
                Assert.That(box.max.x, Is.InRange(LaneHouseArt.LeafWidth - 0.05f, LaneHouseArt.LeafWidth + 0.02f), id + ": closes along +X");
                Assert.That(box.min.y, Is.InRange(-0.02f, 0.03f), id + ": from the sill");
                Assert.That(box.max.y, Is.InRange(LaneHouseArt.LeafHeight - 0.05f, LaneHouseArt.LeafHeight + 0.02f), id);
                Assert.That(box.min.z, Is.InRange(-0.08f, 0f), id + ": its face toward the street (-Z)");
                Assert.That(box.max.z, Is.InRange(0.02f, LaneHouseArt.HingeInset), id + ": stays in front of the wall");
                long tris = 0;
                foreach (var f in prefab.GetComponentsInChildren<MeshFilter>(true))
                {
                    tris += Triangles(f.sharedMesh);
                }

                Assert.That(tris, Is.InRange(400L, 2500L), id + ": a prop");
            }
        }

        [Test]
        public void EachLampGlowsFromItsOriginUnderTheAtticWindow()
        {
            foreach (string id in LaneHouseArt.Houses)
            {
                var prefab = Load(LaneHouseArt.Folder, LaneHouseArt.PorchLamp(id));
                var parts = Children(prefab);
                Assert.IsTrue(parts.ContainsKey(LaneHouseArt.LampGlow), id + ": the glass");
                var glass = Bounds(new[] { parts[LaneHouseArt.LampGlow].GetComponent<MeshFilter>() });
                Assert.Less(glass.center.magnitude, 0.08f, id + ": the origin is in the glass");
                var box = ModelBounds(prefab);
                Assert.That(box.max.z, Is.InRange(LaneHouseArt.LampOut - 0.02f, LaneHouseArt.LampOut + 0.02f), id + ": the bracket meets the wall");
                Assert.Less(GlowLevels.IntensityFor(LaneHouseArt.LampGlow), GlowLevels.AtticWindow * 0.5f);
            }
        }

        [Test]
        public void EachHouseHasItsOwnAtlasSharedWithItsLeafAndLamp()
        {
            foreach (string id in LaneHouseArt.Houses)
            {
                string basePath = Folder + LaneHouseArt.Atlas(id);
                var colour = AssetImporter.GetAtPath(basePath + "_BaseColor.png") as TextureImporter;
                if (colour == null)
                {
                    Assert.Ignore(basePath + " textures are not committed.");
                }

                var normal = AssetImporter.GetAtPath(basePath + "_Normal.png") as TextureImporter;
                Assert.IsNotNull(normal, basePath + "_Normal.png");
                Assert.IsNotNull(AssetImporter.GetAtPath(basePath + "_Emission.png") as TextureImporter, basePath + "_Emission.png");
                Assert.IsTrue(colour.sRGBTexture);
                Assert.AreEqual(TextureImporterType.NormalMap, normal.textureType);
                var tex = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath + "_BaseColor.png");
                Assert.AreEqual(LaneHouseArt.AtlasSize, tex.width);
                Assert.IsNotNull(Resources.Load<Texture2D>(LaneHouseArt.Folder + "/" + LaneHouseArt.House(id) + "_BaseColor"),
                    "the house finds its atlas by its own name");
                Assert.IsNull(Resources.Load<Texture2D>(LaneHouseArt.Folder + "/" + LaneHouseArt.DoorLeaf(id) + "_BaseColor"),
                    "the leaf shares the house's atlas (ModelKit.FallbackAtlas)");
                Assert.IsNull(Resources.Load<Texture2D>(LaneHouseArt.Folder + "/" + LaneHouseArt.PorchLamp(id) + "_BaseColor"));
            }
        }

        [Test]
        public void NeighboursAreKitPartsOnTheGrownUpBody()
        {
            string path = CrowdFolder + CrowdKit.NeighbourModel + ".fbx";
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Assert.Ignore(path + " is not committed.");
            }

            Assert.IsTrue(ModelImportRules.IsCrowdPath(path));
            Assert.IsTrue(importer.isReadable, "parts are merged on the CPU at spawn");
            Assert.AreEqual(ModelImporterAnimationType.Generic, importer.animationType);
            var clips = new List<string>();
            foreach (var clip in importer.clipAnimations)
            {
                Assert.AreEqual(CharacterClips.IsLooping(clip.name), clip.loopTime, clip.name);
                clips.Add(clip.name);
            }

            CollectionAssert.IsSubsetOf(CrowdKit.Clips, clips);
            var prefab = Load(CrowdKit.Folder, CrowdKit.NeighbourModel);
            var adult = Load(CrowdKit.Folder, CrowdKit.AdultModel);
            var skins = new Dictionary<string, SkinnedMeshRenderer>();
            foreach (var smr in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                skins[smr.name] = smr;
            }

            // the same skeleton as the walkers' grown-up: same bones, same rest heights
            var bones = Children(prefab);
            var adultBones = Children(adult);
            foreach (string b in new[] { "Hips", "Head", "Hand_R", "Foot_L", "Pail", "Blink" })
            {
                Assert.IsTrue(bones.ContainsKey(b), "no bone " + b);
                Assert.AreEqual(adultBones[b].position.y, bones[b].position.y, 0.002f, b + ": the grown-up body's skeleton");
            }

            foreach (var part in CrowdKit.NeighbourParts)
            {
                foreach (var name in new[] { part.Name, part.Name + CrowdKit.Lod1Suffix })
                {
                    Assert.IsTrue(skins.TryGetValue(name, out var smr), "no skinned mesh " + name);
                    Assert.AreEqual(1, smr.sharedMesh.subMeshCount, name + ": one material");
                    Assert.AreEqual(smr.bones.Length, smr.sharedMesh.bindposes.Length, name);
                }
            }

            foreach (var n in CrowdKit.Neighbours)
            {
                for (int lod = 0; lod < 2; lod++)
                {
                    long tris = 0;
                    foreach (var name in CrowdKit.MeshNames(n, lod))
                    {
                        tris += Triangles(skins[name].sharedMesh);
                    }

                    Assert.LessOrEqual(tris, lod == 0 ? CrowdKit.MaxLod0Triangles : CrowdKit.MaxLod1Triangles, $"{n.Id} LOD{lod}");
                }
            }

            var atlas = Resources.Load<Texture2D>(CrowdKit.Folder + "/" + CrowdKit.NeighbourModel + "_BaseColor");
            Assert.IsNotNull(atlas, "the neighbours' atlas, found by the model's name");
            Assert.AreEqual(CrowdKit.AtlasSize, atlas.width);
        }
    }
}
