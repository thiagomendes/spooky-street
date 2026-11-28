using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.World;
using SpookyStreet.EditorTools;
using UnityEditor;
using UnityEngine;

namespace SpookyStreet.Tests.Project
{
    /// <summary>
    /// The paper lantern table's art as Unity imports it (docs/living-street-lantern-art.md, <see cref="LanternArt"/>):
    /// four lanterns hanging from their origin with one tintable paper child, the folding table with its lit samples,
    /// Pip's porch hook with its hang point and bell button, one shared atlas, the iPhone budget, and the picker's
    /// pictures as sprites. Missing files skip (not fail), like the other model import tests.
    /// <para>Measures are taken in the prefab's world frame (Unity axes, Y up): an imported FBX root carries the Z-up to
    /// Y-up turn, so children's local positions are still in the builder's axes (CI run 153).</para>
    /// </summary>
    public class LanternArtImportTests
    {
        private const string Folder = ModelImportRules.ModelsRoot + "Environment/";

        private static GameObject Load(string model)
        {
            var prefab = Resources.Load<GameObject>(LanternArt.Folder + "/" + model);
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

        private static long Triangles(GameObject prefab)
        {
            long total = 0;
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
            {
                var mesh = filter.sharedMesh;
                for (int i = 0; mesh != null && i < mesh.subMeshCount; i++)
                {
                    total += mesh.GetIndexCount(i) / 3;
                }
            }

            return total;
        }

        /// <summary>Bounds of every mesh under the prefab, in the prefab's world frame (its root sits at the origin).</summary>
        private static Bounds ModelBounds(GameObject prefab)
        {
            bool any = false;
            var box = new Bounds();
            foreach (var filter in prefab.GetComponentsInChildren<MeshFilter>(true))
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

        private static IEnumerable<string> Models()
        {
            foreach (string shape in LanternArt.Shapes)
            {
                yield return LanternArt.Model(shape);
            }

            yield return LanternArt.Table;
            yield return LanternArt.Hook;
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
                Assert.IsFalse(ModelImportRules.IsStaticBatchedPath(path), path + ": spawned by the living street, its paper tinted");
                Assert.AreEqual(ModelImporterAnimationType.None, importer.animationType, path);
                Assert.IsFalse(importer.importAnimation, path);
                Assert.AreEqual(ModelImporterMaterialImportMode.None, importer.materialImportMode, path);
                Assert.IsTrue(importer.bakeAxisConversion, path);
            }
        }

        [Test]
        public void SharedAtlasImportsWithItsRoles()
        {
            string basePath = Folder + LanternArt.Atlas;
            var baseColor = AssetImporter.GetAtPath(basePath + "_BaseColor.png") as TextureImporter;
            if (baseColor == null)
            {
                Assert.Ignore(basePath + " textures are not committed.");
            }

            var normal = AssetImporter.GetAtPath(basePath + "_Normal.png") as TextureImporter;
            var emission = AssetImporter.GetAtPath(basePath + "_Emission.png") as TextureImporter;
            Assert.IsNotNull(normal, basePath + "_Normal.png");
            Assert.IsNotNull(emission, basePath + "_Emission.png");
            Assert.AreEqual(TextureImporterType.Default, baseColor.textureType);
            Assert.AreEqual(TextureImporterShape.Texture2D, baseColor.textureShape);
            Assert.IsTrue(baseColor.sRGBTexture);
            Assert.AreEqual(TextureImporterType.NormalMap, normal.textureType);
            Assert.IsTrue(emission.sRGBTexture);
            var texture = AssetDatabase.LoadAssetAtPath<Texture2D>(basePath + "_BaseColor.png");
            Assert.IsNotNull(texture, "loads as a Texture2D");
            Assert.AreEqual(LanternArt.AtlasSize, texture.width);
            Assert.AreEqual(LanternArt.AtlasSize, texture.height);
            foreach (string model in Models())
            {
                Assert.IsNull(Resources.Load<Texture2D>(LanternArt.Folder + "/" + model + "_BaseColor"),
                    model + " shares the Lanterns atlas (ModelKit.FallbackAtlas)");
            }
        }

        [Test]
        public void EachLanternHangsFromItsOriginWithOneTintablePaper()
        {
            foreach (string shape in LanternArt.Shapes)
            {
                var prefab = Load(LanternArt.Model(shape));
                var parts = Children(prefab);
                Assert.IsTrue(parts.ContainsKey(LanternArt.PaperGlow), shape + ": no " + LanternArt.PaperGlow);
                var paper = parts[LanternArt.PaperGlow];
                Assert.IsNotNull(paper.GetComponent<MeshFilter>(), shape + ": the paper is drawn");
                Assert.Less(paper.position.magnitude, 1e-4f, shape + ": the paper shares the lantern's origin");
                Assert.AreEqual(GlowLevels.PaperLantern, GlowLevels.IntensityFor(paper.name), shape);
                var glows = 0;
                foreach (var name in parts.Keys)
                {
                    glows += GlowLevels.IsGlowPart(name) ? 1 : 0;
                }

                Assert.AreEqual(1, glows, shape + ": only the paper glows (ink, wire and tassel stay unlit and untinted)");
                var box = ModelBounds(prefab);
                Assert.That(box.max.y, Is.InRange(-0.01f, 0.02f), shape + ": the origin is the top of the hang ring");
                Assert.That(box.size.y, Is.InRange(0.28f, 0.48f), shape + ": a lantern about 0.3 m tall plus its bail");
                Assert.That(box.size.x, Is.InRange(0.24f, 0.62f), shape + ": width (the bat with its wings)");
                Assert.That(box.size.z, Is.InRange(0.06f, 0.16f), shape + ": a figure lantern, two faces and a pleated band");
                Assert.That(box.center.x, Is.EqualTo(0f).Within(0.03f), shape + ": hangs plumb");
                float drop = LanternArt.DropToCentre(shape);
                Assert.That(-drop, Is.InRange(box.min.y + 0.05f, box.max.y - 0.1f), shape + ": its light is inside the paper");
                Assert.That(Triangles(prefab), Is.InRange(300L, 2000L), shape + ": phone budget");
            }
        }

        [Test]
        public void TableIsAChildsCraftTableWithItsLitSamples()
        {
            var prefab = Load(LanternArt.Table);
            var parts = Children(prefab);
            Assert.IsTrue(parts.ContainsKey(LanternArt.TableSamplesGlow), "the string of finished lanterns");
            Assert.AreEqual(GlowLevels.PaperLantern, GlowLevels.IntensityFor(LanternArt.TableSamplesGlow));
            var samples = Children(prefab)[LanternArt.TableSamplesGlow].GetComponent<MeshFilter>().sharedMesh.bounds;
            var box = ModelBounds(prefab);
            Assert.That(box.min.y, Is.InRange(-0.02f, 0.02f), "stands on the ground");
            Assert.That(box.max.y, Is.InRange(LanternArt.TablePoleHeight, LanternArt.TablePoleHeight + 0.1f), "the poles' finials");
            Assert.That(box.size.x, Is.InRange(LanternArt.TableWidth, LanternArt.TableWidth + 0.12f), "the top with its edge band");
            Assert.That(box.size.z, Is.InRange(LanternArt.TableDepth - 0.1f, LanternArt.TableDepth + 0.15f), "the legs' feet");
            Assert.That(box.center.x, Is.EqualTo(0f).Within(0.05f), "origin under the table's middle");
            Assert.Greater(samples.size.x, LanternArt.TableWidth * 0.6f, "the samples hang across the string");
            Assert.That(Triangles(prefab), Is.InRange(2000L, 5000L), "props: 0.5-5k triangles");
        }

        [Test]
        public void HookHangsTheLanternAtItsDocumentedPointAndHasABellButton()
        {
            var prefab = Load(LanternArt.Hook);
            var parts = Children(prefab);
            Assert.IsTrue(parts.ContainsKey(LanternArt.HookHang), "the hook eye");
            Assert.IsTrue(parts.ContainsKey(LanternArt.BellButton), "the bell's push button");
            var hang = parts[LanternArt.HookHang].position;
            Assert.AreEqual(LanternArt.HangX, hang.x, 0.02f, "the arm reaches along +X");
            Assert.AreEqual(LanternArt.HangHeight, hang.y, 0.02f, "high over the deck: a hung lantern clears Pip");
            Assert.AreEqual(LanternArt.HangZ, hang.z, 0.03f);
            var bell = parts[LanternArt.BellButton].position;
            Assert.AreEqual(LanternArt.BellHeight, bell.y, 0.02f, "at a child's reach");
            Assert.Less(bell.z, 0f, "on the post's front (-Z)");
            var box = ModelBounds(prefab);
            Assert.That(box.min.y, Is.InRange(-0.01f, 0.02f), "stands on the deck");
            Assert.That(box.max.y, Is.InRange(LanternArt.HangHeight, LanternArt.HangHeight + 0.25f), "the arm and the finial");
            Assert.Greater(box.max.x, LanternArt.HangX, "the curl reaches past the hang point");
            foreach (string shape in LanternArt.Shapes)
            {
                // the lowest a hung lantern reaches, over the deck: Pip (1.05 m) walks under it
                var lantern = ModelBounds(Load(LanternArt.Model(shape)));
                Assert.Greater(LanternArt.HangHeight + lantern.min.y, 1.35f, shape + " hangs clear of Pip's head");
            }

            Assert.That(Triangles(prefab), Is.InRange(500L, 2000L));
        }

        [Test]
        public void PickerPicturesImportAsTransparentSprites()
        {
            var resources = new List<string>();
            foreach (string shape in LanternArt.Shapes)
            {
                resources.Add(LanternArt.ShapeTile(shape));
            }

            foreach (string colour in LanternArt.Colours)
            {
                resources.Add(LanternArt.ColourTile(colour));
            }

            foreach (string resource in resources)
            {
                string path = "Assets/SpookyStreet/Content/Resources/" + resource + ".png";
                Assert.IsTrue(LanternArt.IsTileAssetPath(path), path);
                Assert.IsTrue(KeepsakeArtImportRules.IsSpritePath(path), path);
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Assert.Ignore(path + " is not committed.");
                }

                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType, path);
                Assert.AreEqual(TextureImporterShape.Texture2D, importer.textureShape, path);
                Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode, path);
                Assert.IsTrue(importer.alphaIsTransparency, path);
                Assert.IsFalse(importer.mipmapEnabled, path);
                Assert.IsTrue(importer.sRGBTexture, path);
                Assert.IsTrue(importer.GetPlatformTextureSettings(KeepsakeArtImportRules.IosPlatform).overridden, path + " iOS");
                var sprite = Resources.Load<Sprite>(resource);
                Assert.IsNotNull(sprite, resource + " loads as a Sprite");
                Assert.AreEqual(LanternArt.TileSize, sprite.texture.width, path);
                Assert.AreEqual(LanternArt.TileSize, sprite.texture.height, path);
            }
        }
    }
}
