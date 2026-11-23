using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.World;
using SpookyStreet.EditorTools;
using SpookyStreet.Player;
using UnityEditor;
using UnityEngine;

namespace SpookyStreet.Tests.Project
{
    /// <summary>
    /// The S004 seed and keepsake art as Unity imports it (S004 plan, stream B): static props with the named children
    /// the game drives, baked textures with their roles, the sketch pages as sprites, and the costume pieces where
    /// Pip's bones are. Missing files skip (not fail), like the other model import tests.
    /// </summary>
    public class KeepsakeArtImportTests
    {
        private const string Env = ModelImportRules.ModelsRoot + "Environment/";
        private const string Costume = "Assets/SpookyStreet/Content/Resources/" + KeepsakeArt.CostumeFolder;
        private const string EnvResources = "Models/Environment/";

        private static GameObject Load(string resourcePath)
        {
            var prefab = Resources.Load<GameObject>(resourcePath);
            if (prefab == null)
            {
                Assert.Ignore(resourcePath + " is not committed.");
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
                    var p = prefab.transform.InverseTransformPoint(filter.transform.TransformPoint(corner));
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

        [TestCase(Env + KeepsakeArt.TroupePhoto + ".fbx")]
        [TestCase(Env + KeepsakeArt.OwlCrest + ".fbx")]
        [TestCase(Env + KeepsakeArt.PaperBat + ".fbx")]
        [TestCase(Env + KeepsakeArt.BatMobile + ".fbx")]
        [TestCase(Env + KeepsakeArt.DressUpTrunk + ".fbx")]
        [TestCase(Costume + KeepsakeArt.ShowmanHat + ".fbx")]
        [TestCase(Costume + KeepsakeArt.LanternPail + ".fbx")]
        public void ModelsImportAsStaticPropsWithoutImportedMaterials(string path)
        {
            var importer = AssetImporter.GetAtPath(path) as ModelImporter;
            if (importer == null)
            {
                Assert.Ignore(path + " is not committed.");
            }

            Assert.IsTrue(ModelImportRules.IsGameModelPath(path));
            Assert.IsFalse(ModelImportRules.IsCharacterPath(path), "a prop, not a rig");
            Assert.IsFalse(ModelImportRules.IsStaticBatchedPath(path), "its parts move, or it is hung on something that does");
            Assert.AreEqual(ModelImporterAnimationType.None, importer.animationType);
            Assert.IsFalse(importer.importAnimation);
            Assert.AreEqual(ModelImporterMaterialImportMode.None, importer.materialImportMode);
            Assert.IsTrue(importer.bakeAxisConversion);
        }

        [TestCase(Env + KeepsakeArt.TroupePhoto, 1024)]
        [TestCase(Env + KeepsakeArt.OwlCrest, 512)]
        [TestCase(Env + KeepsakeArt.KeepsakesAtlas, 1024)]
        [TestCase(Costume + KeepsakeArt.CostumeAtlas, 512)]
        public void BakedTextureSetsImportWithTheirRoles(string basePath, int size)
        {
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
            Assert.AreEqual(size, texture.width);
            Assert.AreEqual(size, texture.height);
        }

        [Test]
        public void TroupePhotoIsALandscapeFrameWithFivePuppetsAndTheMountCrest()
        {
            var prefab = Load(EnvResources + KeepsakeArt.TroupePhoto);
            var parts = Children(prefab);
            foreach (string puppet in KeepsakeArt.Puppets)
            {
                Assert.IsTrue(parts.ContainsKey(puppet), "TroupePhoto.fbx has no " + puppet);
                Assert.IsNotNull(parts[puppet].GetComponent<MeshFilter>(), puppet + " is drawn");
            }

            Assert.IsTrue(parts.ContainsKey(KeepsakeArt.PhotoCrest), "the crest on the mount");
            var box = ModelBounds(prefab);
            Assert.That(box.size.x, Is.InRange(KeepsakeArt.PhotoWidth - 0.02f, KeepsakeArt.PhotoWidth + 0.02f), "0.7 m wide");
            Assert.That(box.size.y, Is.InRange(KeepsakeArt.PhotoHeight, KeepsakeArt.PhotoHeight + 0.15f), "0.45 m tall plus its cord");
            Assert.Less(box.size.z, 0.08f, "flat on the wall");
            Assert.That(box.center.x, Is.EqualTo(0f).Within(0.02f), "origin at the frame centre");
            // Left to right as the Players stand (the model faces -Z, so its left is -X ... or +X if mirrored: the order
            // must be monotonic either way).
            float first = parts[KeepsakeArt.Puppets[0]].localPosition.x;
            float last = parts[KeepsakeArt.Puppets[4]].localPosition.x;
            float sign = Mathf.Sign(last - first);
            for (int i = 1; i < KeepsakeArt.Puppets.Length; i++)
            {
                float step = parts[KeepsakeArt.Puppets[i]].localPosition.x - parts[KeepsakeArt.Puppets[i - 1]].localPosition.x;
                Assert.Greater(step * sign, 0.04f, KeepsakeArt.Puppets[i] + " stands beside the one before");
            }

            Assert.That(Triangles(prefab), Is.InRange(2000L, 8000L));
        }

        [Test]
        public void OwlCrestIsARoundSignOfItsSize()
        {
            var prefab = Load(EnvResources + KeepsakeArt.OwlCrest);
            var box = ModelBounds(prefab);
            Assert.That(box.size.x, Is.InRange(KeepsakeArt.CrestDiameter, KeepsakeArt.CrestDiameter + 0.06f), "0.35 m board with its edge");
            Assert.That(box.size.y, Is.InRange(KeepsakeArt.CrestDiameter, KeepsakeArt.CrestDiameter + 0.15f), "plus its hangers");
            Assert.Less(box.size.z, 0.25f, "a flat sign on its two arms");
            Assert.That(Triangles(prefab), Is.InRange(500L, 5000L));
            Assert.IsNotNull(Resources.Load<Texture2D>(EnvResources + KeepsakeArt.OwlCrest + "_BaseColor"), "its own texture set");
        }

        [Test]
        public void PaperBatHasTwoHingedWings()
        {
            var prefab = Load(EnvResources + KeepsakeArt.PaperBat);
            var parts = Children(prefab);
            Assert.IsTrue(parts.ContainsKey(KeepsakeArt.WingLeft) && parts.ContainsKey(KeepsakeArt.WingRight));
            var left = parts[KeepsakeArt.WingLeft].localPosition;
            var right = parts[KeepsakeArt.WingRight].localPosition;
            Assert.AreEqual(left.x, -right.x, 1e-3f, "the hinges mirror each other");
            Assert.AreEqual(left.y, right.y, 1e-3f);
            Assert.That(Mathf.Abs(left.x), Is.InRange(0.02f, 0.06f), "beside the body");
            var box = ModelBounds(prefab);
            Assert.That(box.size.x, Is.InRange(KeepsakeArt.BatSpan - 0.04f, KeepsakeArt.BatSpan + 0.02f), "wingspan, open");
            Assert.That(box.min.y, Is.InRange(-0.01f, 0.03f), "origin at the body's lowest point");
            Assert.That(Triangles(prefab), Is.InRange(200L, 1500L), "thirty of them share the street");
            Assert.IsFalse(parts.ContainsKey("PaperBatKeepsake_Glow"), "paper, not a lamp");
        }

        [Test]
        public void BatMobileHasItsRingsLanternsAndThirtyHooksUnderTheHook()
        {
            var prefab = Load(EnvResources + KeepsakeArt.BatMobile);
            var parts = Children(prefab);
            for (int ring = 1; ring <= KeepsakeArt.MobileRings; ring++)
            {
                Assert.IsTrue(parts.ContainsKey(KeepsakeArt.Ring(ring)), KeepsakeArt.Ring(ring));
                Assert.IsTrue(parts.ContainsKey(KeepsakeArt.RingClose(ring)), KeepsakeArt.RingClose(ring));
                Assert.IsTrue(parts.ContainsKey(KeepsakeArt.RingLantern(ring)), KeepsakeArt.RingLantern(ring));
            }

            float previous = 0f;
            for (int n = 1; n <= KeepsakeArt.MobileHooks; n++)
            {
                Assert.IsTrue(parts.ContainsKey(KeepsakeArt.Hook(n)), KeepsakeArt.Hook(n));
                var at = parts[KeepsakeArt.Hook(n)].localPosition;
                Assert.Less(at.y, -0.3f, KeepsakeArt.Hook(n) + " hangs under the top hook");
                Assert.Greater(at.y, -KeepsakeArt.MobileDrop, KeepsakeArt.Hook(n));
                Assert.Less(new Vector2(at.x, at.z).magnitude, KeepsakeArt.MobileRadius, KeepsakeArt.Hook(n));
                if ((n - 1) % KeepsakeArt.HooksPerRing == 0 && n > 1)
                {
                    Assert.Less(at.y, previous + 0.1f, "each ring hangs under the one before");
                }

                previous = at.y;
            }

            var box = ModelBounds(prefab);
            Assert.That(box.max.y, Is.InRange(-0.02f, 0.02f), "origin at the hook on the eave");
            Assert.Greater(box.min.y, -KeepsakeArt.MobileDrop);
            Assert.That(Triangles(prefab), Is.InRange(1000L, 5000L));
        }

        [Test]
        public void DressUpTrunkHasALidHingedOnItsBackEdge()
        {
            var prefab = Load(EnvResources + KeepsakeArt.DressUpTrunk);
            var parts = Children(prefab);
            Assert.IsTrue(parts.ContainsKey(KeepsakeArt.TrunkLid));
            var hinge = parts[KeepsakeArt.TrunkLid].localPosition;
            Assert.AreEqual(KeepsakeArt.TrunkSize, hinge.y, 0.01f, "on the top edge");
            Assert.AreEqual(KeepsakeArt.TrunkSize / 2f, Mathf.Abs(hinge.z), 0.01f, "on the back edge");
            Assert.AreEqual(0f, hinge.x, 0.01f);
            var box = ModelBounds(prefab);
            Assert.That(box.min.y, Is.InRange(-0.01f, 0.01f), "stands on the porch");
            Assert.That(box.size.x, Is.InRange(KeepsakeArt.TrunkSize, KeepsakeArt.TrunkSize + 0.12f), "the size of the porch crate");
            Assert.That(Triangles(prefab), Is.InRange(800L, 5000L));
        }

        [Test]
        public void SketchPagesImportAsFullSizeSprites()
        {
            for (int page = 1; page <= KeepsakeArt.SketchPages; page++)
            {
                string path = KeepsakeArt.SketchPageAssetFolder + "sketch_page_" + page + ".png";
                var importer = AssetImporter.GetAtPath(path) as TextureImporter;
                if (importer == null)
                {
                    Assert.Ignore(path + " is not committed.");
                }

                Assert.IsTrue(KeepsakeArt.IsSketchPageAssetPath(path));
                Assert.AreEqual(TextureImporterType.Sprite, importer.textureType, path);
                Assert.AreEqual(TextureImporterShape.Texture2D, importer.textureShape, path);
                Assert.AreEqual(SpriteImportMode.Single, importer.spriteImportMode, path);
                Assert.IsFalse(importer.mipmapEnabled, path);
                Assert.IsTrue(importer.sRGBTexture, path);
                var ios = importer.GetPlatformTextureSettings(KeepsakeArtImportRules.IosPlatform);
                Assert.IsTrue(ios.overridden, path + " iOS override");
                var sprite = Resources.Load<Sprite>(KeepsakeArt.SketchPage(page));
                Assert.IsNotNull(sprite, KeepsakeArt.SketchPage(page) + " does not load as a Sprite from Resources");
                Assert.AreEqual(KeepsakeArt.SketchPageWidth, sprite.texture.width, path);
                Assert.AreEqual(KeepsakeArt.SketchPageHeight, sprite.texture.height, path);
            }
        }

        [Test]
        public void StarCapeIsPipsAtlasAtTheSameSize()
        {
            var star = Resources.Load<Texture2D>(KeepsakeArt.StarCapeTexture);
            var pip = Resources.Load<Texture2D>(PipAvatar.ModelPath + "_BaseColor");
            if (star == null || pip == null)
            {
                Assert.Ignore("The star cape texture or Pip's atlas is not committed.");
            }

            Assert.AreEqual(pip.width, star.width, "the same atlas, texel for texel outside the lining");
            Assert.AreEqual(pip.height, star.height);
            var importer = AssetImporter.GetAtPath("Assets/SpookyStreet/Content/Resources/" + KeepsakeArt.StarCapeTexture + ".png") as TextureImporter;
            Assert.IsNotNull(importer);
            Assert.IsTrue(importer.sRGBTexture, "colour data");
            Assert.AreEqual(TextureImporterType.Default, importer.textureType);
        }

        [Test]
        public void CostumePiecesSitWherePipsBonesAre()
        {
            var pip = Resources.Load<GameObject>(PipAvatar.ModelPath);
            var hat = Resources.Load<GameObject>(KeepsakeArt.CostumeFolder + KeepsakeArt.ShowmanHat);
            var pail = Resources.Load<GameObject>(KeepsakeArt.CostumeFolder + KeepsakeArt.LanternPail);
            if (pip == null || hat == null || pail == null)
            {
                Assert.Ignore("Pip.fbx or the costume pieces are not committed.");
            }

            var bones = Children(pip);
            Assert.IsTrue(bones.ContainsKey(KeepsakeArt.HatBone), "Pip.fbx has no " + KeepsakeArt.HatBone + " bone");
            Assert.IsTrue(bones.ContainsKey(KeepsakeArt.PailBone), "Pip.fbx has no " + KeepsakeArt.PailBone + " bone");
            Assert.IsTrue(bones.ContainsKey(KeepsakeArt.PipPailMesh), "Pip.fbx has no " + KeepsakeArt.PipPailMesh + " mesh to hide");
            // Both are authored in Pip's model space in her rest pose: compare in each prefab's own root frame.
            Vector3 head = pip.transform.InverseTransformPoint(bones[KeepsakeArt.HatBone].position);
            Vector3 grip = pip.transform.InverseTransformPoint(bones[KeepsakeArt.PailBone].position);
            var hatBox = ModelBounds(hat);
            var pailBox = ModelBounds(pail);
            Assert.That(hatBox.min.y - head.y, Is.InRange(0.15f, 0.32f), "the brim rests on her hair, above the head bone");
            Assert.Less(new Vector2(hatBox.center.x - head.x, hatBox.center.z - head.z).magnitude, 0.12f, "on her head, a little tilted");
            Assert.That(hatBox.size.y, Is.InRange(0.08f, 0.2f), "a small hat");
            Assert.That(pailBox.max.y - grip.y, Is.InRange(-0.03f, 0.05f), "the handle's top is in her hand");
            Assert.Less(new Vector2(pailBox.center.x - grip.x, pailBox.center.z - grip.z).magnitude, 0.06f, "it hangs under her hand");
            Assert.That(pailBox.size.y, Is.InRange(0.18f, 0.34f));
            Assert.IsTrue(Children(pail).ContainsKey(KeepsakeArt.PailGlow), "the lantern's paper is the glow part");
            Assert.That(Triangles(hat) + Triangles(pail), Is.InRange(800L, 4000L));
        }
    }
}
