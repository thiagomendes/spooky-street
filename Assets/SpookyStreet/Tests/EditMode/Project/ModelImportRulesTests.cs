using NUnit.Framework;
using SpookyStreet.Core.Animation;
using SpookyStreet.EditorTools;
using SpookyStreet.Player;
using UnityEditor;
using UnityEngine;

namespace SpookyStreet.Tests.Project
{
    /// <summary>
    /// Import rules for original models. The path and naming rules always run; the checks against Pip's real
    /// import run once the model is committed and are skipped (not failed) before that.
    /// </summary>
    public class ModelImportRulesTests
    {
        private const string PipFolder = ModelImportRules.ModelsRoot + "Characters/Pip/";
        private const string PipFbx = PipFolder + "Pip.fbx";

        [Test]
        public void ClassifiesModelPaths()
        {
            Assert.IsTrue(ModelImportRules.IsCharacterPath(PipFbx));
            Assert.IsTrue(ModelImportRules.IsGameModelPath(ModelImportRules.ModelsRoot + "Props/Porch/Lantern.fbx"));
            Assert.IsFalse(ModelImportRules.IsCharacterPath(ModelImportRules.ModelsRoot + "Props/Porch/Lantern.fbx"));
            Assert.IsFalse(ModelImportRules.IsGameModelPath("Assets/Other/Characters/Pip.fbx"));
        }

        [Test]
        public void OnlyStreetSetModelsImportReadableForStaticBatching()
        {
            string env = ModelImportRules.ModelsRoot + "Environment/";
            Assert.IsTrue(ModelImportRules.IsStaticBatchedPath(env + "Lawn.fbx"));
            Assert.IsTrue(ModelImportRules.IsStaticBatchedPath(env + "NeighbourHouseB.fbx"));
            Assert.IsFalse(ModelImportRules.IsStaticBatchedPath(env + "OwlHouse.fbx"), "animated parts live in the owl house");
            Assert.IsFalse(ModelImportRules.IsStaticBatchedPath(env + "PaperBat.fbx"));
            Assert.IsFalse(ModelImportRules.IsStaticBatchedPath(PipFbx));
            Assert.IsFalse(ModelImportRules.IsStaticBatchedPath("Assets/Other/Environment/Lawn.fbx"));
        }

        [Test]
        public void ClassifiesBakedTextures()
        {
            Assert.AreEqual(TextureImporterType.NormalMap, ModelImportRules.TextureTypeFor(PipFolder + "Pip_Normal.png"));
            Assert.AreEqual(TextureImporterType.Default, ModelImportRules.TextureTypeFor(PipFolder + "Pip_BaseColor.png"));
            Assert.IsTrue(ModelImportRules.IsColorTexture(PipFolder + "Pip_BaseColor.png"));
            Assert.IsTrue(ModelImportRules.IsColorTexture(PipFolder + "Pip_Face.png"));
            Assert.IsFalse(ModelImportRules.IsColorTexture(PipFolder + "Pip_Normal.png"));
            Assert.IsFalse(ModelImportRules.IsColorTexture(PipFolder + "Pip_Roughness.png"));
        }

        [Test]
        public void ResourcePathsMatchTheModelsRoot()
        {
            StringAssert.StartsWith("Assets/SpookyStreet/Content/Resources/", ModelImportRules.ModelsRoot);
            Assert.AreEqual(PipFolder + "Pip",
                "Assets/SpookyStreet/Content/Resources/" + PipAvatar.ModelPath);
        }

        [Test]
        public void PipModelImportsAsGenericRigWithCleanLoopingClips()
        {
            var importer = AssetImporter.GetAtPath(PipFbx) as ModelImporter;
            if (importer == null)
            {
                Assert.Ignore("Pip.fbx is not committed yet; the primitive avatar is in use.");
            }

            Assert.AreEqual(ModelImporterAnimationType.Generic, importer.animationType);
            Assert.AreEqual(ModelImporterMaterialImportMode.None, importer.materialImportMode);
            Assert.IsTrue(importer.bakeAxisConversion);
            var clips = importer.clipAnimations;
            Assert.IsNotEmpty(clips, "Pip.fbx has no animation takes");
            bool hasIdle = false;
            foreach (var clip in clips)
            {
                StringAssert.DoesNotContain("|", clip.name, "clip name not cleaned: " + clip.name);
                Assert.AreEqual(PipClips.IsLooping(clip.name), clip.loopTime, clip.name + " loop flag");
                hasIdle |= clip.name == PipClips.Required;
            }

            Assert.IsTrue(hasIdle, "Pip.fbx needs an Idle clip to animate");
            var loaded = Resources.LoadAll<AnimationClip>(PipAvatar.ModelPath);
            Assert.IsTrue(PipAnimator.CanAnimate(loaded), "Idle clip not loadable from Resources");
        }

        // tools/blender/export/pip_anims.py CLIPS, frames at 30 fps: WallKick 10, LedgeGrab 6, LedgeClimb 12 (the
        // motor's 0.4 s climb), LedgeHang 48 (looping).
        [TestCase("WallKick", 8f, 12f, false)]
        [TestCase("LedgeGrab", 5f, 8f, false)]
        [TestCase("LedgeClimb", 11f, 13f, false)]
        [TestCase("LedgeHang", 40f, 56f, true)]
        public void PipHasTheWallAndLedgeClips(string clipName, float minFrames, float maxFrames, bool loops)
        {
            var importer = AssetImporter.GetAtPath(PipFbx) as ModelImporter;
            if (importer == null)
            {
                Assert.Ignore("Pip.fbx is not committed yet; the primitive avatar is in use.");
            }

            bool found = false;
            foreach (var clip in importer.clipAnimations)
            {
                if (clip.name != clipName)
                {
                    continue;
                }

                found = true;
                Assert.AreEqual(loops, clip.loopTime, clipName + " loop flag");
                Assert.That(clip.lastFrame - clip.firstFrame, Is.InRange(minFrames, maxFrames), clipName + " length");
            }

            Assert.IsTrue(found, "Pip.fbx has no " + clipName + " take");
            Assert.IsTrue(PipClips.IsKnown(clipName));
            Assert.AreEqual(loops, PipClips.IsLooping(clipName));
            var loaded = Resources.LoadAll<AnimationClip>(PipAvatar.ModelPath);
            AnimationClip asset = PipAnimator.Find(loaded, clipName);
            Assert.IsNotNull(asset, clipName + " not loadable from Resources");
            Assert.That(asset.length * 30f, Is.InRange(minFrames, maxFrames), clipName + " seconds");
        }

        [TestCase("Bea", "Idle", "Talk")]
        [TestCase("Vale", "SitIdle", "SitTalk")]
        public void WitnessModelsImportWithLoopingClipsAndAOneShotWave(string model, string rest, string engaged)
        {
            string fbx = ModelImportRules.ModelsRoot + "Characters/" + model + "/" + model + ".fbx";
            var importer = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (importer == null)
            {
                Assert.Ignore(model + ".fbx is not committed.");
            }

            Assert.AreEqual(ModelImporterAnimationType.Generic, importer.animationType);
            Assert.AreEqual(ModelImporterMaterialImportMode.None, importer.materialImportMode);
            var names = new System.Collections.Generic.List<string>();
            foreach (var clip in importer.clipAnimations)
            {
                StringAssert.DoesNotContain("|", clip.name, "clip name not cleaned: " + clip.name);
                Assert.AreEqual(CharacterClips.IsLooping(clip.name), clip.loopTime, clip.name + " loop flag");
                names.Add(clip.name);
            }

            CollectionAssert.Contains(names, rest);
            CollectionAssert.Contains(names, engaged);
            CollectionAssert.Contains(names, CharacterClips.Wave);
            Assert.IsTrue(CharacterClips.IsLooping(rest) && CharacterClips.IsLooping(engaged));
            Assert.IsFalse(CharacterClips.IsLooping(CharacterClips.Wave));
            var loaded = Resources.LoadAll<AnimationClip>(SpookyStreet.Characters.NpcAvatar.PathFor(model));
            Assert.IsNotNull(PipAnimator.Find(loaded, rest), rest + " not loadable from Resources");
        }

        [Test]
        public void BeaHasAOneShotPointClip()
        {
            string fbx = ModelImportRules.ModelsRoot + "Characters/Bea/Bea.fbx";
            var importer = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (importer == null)
            {
                Assert.Ignore("Bea.fbx is not committed.");
            }

            bool found = false;
            foreach (var clip in importer.clipAnimations)
            {
                if (clip.name != CharacterClips.Point)
                {
                    continue;
                }

                found = true;
                Assert.IsFalse(clip.loopTime, "Point plays once");
                // tools/blender/export/witness_anims.py POINT_FRAMES: 54 frames at 30 fps
                Assert.That(clip.lastFrame - clip.firstFrame, Is.InRange(48f, 60f), "Point lasts 1.6 to 2.0 s");
            }

            Assert.IsTrue(found, "Bea.fbx has no Point take");
            Assert.IsFalse(CharacterClips.IsLooping(CharacterClips.Point));
            var loaded = Resources.LoadAll<AnimationClip>(SpookyStreet.Characters.NpcAvatar.PathFor("Bea"));
            Assert.IsNotNull(PipAnimator.Find(loaded, CharacterClips.Point), "Point not loadable from Resources");
        }

        // S003a testimony gestures and hint glances. Frames at 30 fps from tools/blender/export: witness_anims.py
        // (SHADE_FRAMES 66, WATCH_FRAMES 72), junekit_anims.py (LISTEN_FRAMES 66, PONDER_FRAMES 66) and
        // pip_anims.py (GLANCE_FRAMES 40). Each plays once and is loadable from Resources, where the game finds it.
        [TestCase("Bea", "ShadeEyes", 60f, 72f)]
        [TestCase("Vale", "CheckWatch", 66f, 78f)]
        [TestCase("June", "Listen", 60f, 72f)]
        [TestCase("Kit", "Ponder", 60f, 72f)]
        [TestCase("Pip", "GlanceLeft", 36f, 44f)]
        [TestCase("Pip", "GlanceRight", 36f, 44f)]
        public void CharactersHaveTheirGestureClips(string model, string clipName, float minFrames, float maxFrames)
        {
            string fbx = ModelImportRules.ModelsRoot + "Characters/" + model + "/" + model + ".fbx";
            var importer = AssetImporter.GetAtPath(fbx) as ModelImporter;
            if (importer == null)
            {
                Assert.Ignore(model + ".fbx is not committed.");
            }

            bool found = false;
            foreach (var clip in importer.clipAnimations)
            {
                StringAssert.DoesNotContain("|", clip.name, "clip name not cleaned: " + clip.name);
                if (clip.name != clipName)
                {
                    continue;
                }

                found = true;
                Assert.IsFalse(clip.loopTime, clipName + " plays once");
                Assert.That(clip.lastFrame - clip.firstFrame, Is.InRange(minFrames, maxFrames), clipName + " length");
            }

            Assert.IsTrue(found, model + ".fbx has no " + clipName + " take");
            Assert.IsFalse(CharacterClips.IsLooping(clipName));
            if (model == "Pip")
            {
                Assert.IsTrue(PipClips.IsKnown(clipName));
                Assert.IsTrue(PipClips.IsGlance(clipName));
            }
            else
            {
                Assert.AreEqual(clipName, CharacterClips.TestimonyGestureOf(model));
            }

            var loaded = Resources.LoadAll<AnimationClip>(SpookyStreet.Characters.NpcAvatar.PathFor(model));
            AnimationClip asset = PipAnimator.Find(loaded, clipName);
            Assert.IsNotNull(asset, clipName + " not loadable from Resources");
            Assert.That(asset.length * 30f, Is.InRange(minFrames, maxFrames), clipName + " seconds");
        }

        [Test]
        public void ValeWearsHisWatchOnItsOwnBone()
        {
            var prefab = Resources.Load<GameObject>(SpookyStreet.Characters.NpcAvatar.PathFor("Vale"));
            if (prefab == null)
            {
                Assert.Ignore("Vale.fbx is not committed.");
            }

            Transform watch = null, bone = null;
            foreach (var t in prefab.GetComponentsInChildren<Transform>(true))
            {
                watch = t.name == "Vale_Watch" ? t : watch;
                bone = t.name == "Watch" ? t : bone;
            }

            Assert.IsNotNull(watch, "Vale.fbx has no Vale_Watch mesh");
            Assert.IsNotNull(bone, "Vale.fbx has no Watch bone");
            Assert.AreEqual("LowerArm_L", bone.parent.name, "the watch rides his left forearm");
            var renderer = watch.GetComponent<Renderer>();
            Assert.IsNotNull(renderer, "the watch is drawn");
            var skinned = renderer as SkinnedMeshRenderer;
            Mesh mesh = skinned != null ? skinned.sharedMesh : watch.GetComponent<MeshFilter>()?.sharedMesh;
            Assert.IsNotNull(mesh, "the watch has a mesh");
            long triangles = 0;
            for (int i = 0; i < mesh.subMeshCount; i++)
            {
                triangles += mesh.GetIndexCount(i) / 3;
            }

            Assert.That(triangles, Is.InRange(60L, 400L), "a small mesh");
            Assert.That(mesh.bounds.size.magnitude, Is.InRange(0.03f, 0.3f), "wrist sized");
        }

        [Test]
        public void BicycleImportsAsAStaticProp()
        {
            var importer = AssetImporter.GetAtPath(ModelImportRules.ModelsRoot + "Environment/BeaBicycle.fbx") as ModelImporter;
            if (importer == null)
            {
                Assert.Ignore("BeaBicycle.fbx is not committed.");
            }

            Assert.AreEqual(ModelImporterAnimationType.None, importer.animationType);
            Assert.IsFalse(importer.importAnimation);
        }

        [Test]
        public void PipTexturesImportWithTheirRoles()
        {
            var baseColor = AssetImporter.GetAtPath(PipFolder + "Pip_BaseColor.png") as TextureImporter;
            var normal = AssetImporter.GetAtPath(PipFolder + "Pip_Normal.png") as TextureImporter;
            if (baseColor == null && normal == null)
            {
                Assert.Ignore("Pip's baked textures are not committed yet.");
            }

            if (baseColor != null)
            {
                Assert.AreEqual(TextureImporterType.Default, baseColor.textureType);
                Assert.AreEqual(TextureImporterShape.Texture2D, baseColor.textureShape);
                Assert.IsNotNull(Resources.Load<Texture2D>("Models/Characters/Pip/Pip_BaseColor"),
                    "baked colour texture must load as a Texture2D from Resources");
                Assert.IsTrue(baseColor.sRGBTexture, "BaseColor must be sRGB");
                Assert.IsTrue(baseColor.mipmapEnabled);
                AssertIosOverride(baseColor);
            }

            if (normal != null)
            {
                Assert.AreEqual(TextureImporterType.NormalMap, normal.textureType);
                Assert.IsTrue(normal.mipmapEnabled);
                AssertIosOverride(normal);
            }
        }

        private static void AssertIosOverride(TextureImporter importer)
        {
            var ios = importer.GetPlatformTextureSettings(ModelImportRules.IosPlatform);
            Assert.IsTrue(ios.overridden, importer.assetPath + " has no iOS override");
            Assert.AreEqual(ModelImportRules.MaxTextureSize, ios.maxTextureSize);
        }
    }
}
