using NUnit.Framework;
using SpookyStreet.EditorTools;
using UnityEditor;
using UnityEngine;

namespace SpookyStreet.Tests.Project
{
    /// <summary>Asserts the committed project settings match the S000 contract inside the real editor.</summary>
    public class ProjectSetupTests
    {
        [Test]
        public void ProjectChecksPass()
        {
            var problems = ProjectChecks.Run();
            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void EditorMatchesPinnedVersion()
        {
            Assert.AreEqual(ProjectChecks.ReadPinnedEditorVersion(), Application.unityVersion);
        }

        [Test]
        public void MovementTuningAssetHasSpecDefaults()
        {
            var tuning = AssetDatabase.LoadAssetAtPath<Player.PipMovementTuning>(
                "Assets/SpookyStreet/Config/PipMovementTuning.asset");
            Assert.IsNotNull(tuning, "tuning asset missing or its script GUID is wrong");
            Assert.AreEqual(4f, tuning.maxSpeed, 1e-4f);
            Assert.AreEqual(0.15f, tuning.accelerationTime, 1e-4f);
            Assert.AreEqual(0.12f, tuning.decelerationTime, 1e-4f);
            Assert.AreEqual(1f, tuning.jumpApexHeight, 1e-4f);
            Assert.AreEqual(0.10f, tuning.coyoteTime, 1e-4f);
            // Spec §5 starts the buffer at 0.12 s; the S000.6 feel pass (TestFlight 11.1 feedback) tuned it and the
            // chain to what a thumb can hit on touch (docs/S000.6-moveset.md).
            Assert.AreEqual(0.15f, tuning.jumpBufferTime, 1e-4f);
            Assert.AreEqual(0.35f, tuning.chainJumpWindow, 1e-4f);
            Assert.AreEqual(0.25f, tuning.chainJumpMinSpeedFraction, 1e-4f);
            Assert.AreEqual(1.6f, tuning.jumpHoldApexHeight, 1e-4f);
            Assert.AreEqual(3.8f, tuning.cameraDistance, 1e-4f);
        }

        [Test]
        public void GeneratedAppIconIsOpaqueAppStoreSize()
        {
            var icon = AppIconBuilder.CreateTexture();
            try
            {
                Assert.AreEqual(AppIconBuilder.Size, icon.width);
                Assert.AreEqual(AppIconBuilder.Size, icon.height);
                Assert.AreEqual(TextureFormat.RGB24, icon.format, "App Store icons must not carry alpha");
                Color window = icon.GetPixelBilinear(0.5f, 0.64f);
                Assert.Greater(window.r, 0.9f, "attic window is lit amber");
            }
            finally
            {
                Object.DestroyImmediate(icon);
            }
        }

        [Test]
        public void GameplaySceneReferencesTuningAsset()
        {
            var scene = UnityEditor.SceneManagement.EditorSceneManager.OpenScene(ProjectChecks.GameplayScene,
                UnityEditor.SceneManagement.OpenSceneMode.Single);
            var bootstraps = Object.FindObjectsByType<App.GameBootstrap>(FindObjectsSortMode.None);
            Assert.AreEqual(1, bootstraps.Length, "scene must contain exactly one GameBootstrap");
            var serialized = new SerializedObject(bootstraps[0]);
            Assert.IsNotNull(serialized.FindProperty("movementTuning").objectReferenceValue);
            Assert.IsTrue(scene.IsValid());
        }
    }
}
