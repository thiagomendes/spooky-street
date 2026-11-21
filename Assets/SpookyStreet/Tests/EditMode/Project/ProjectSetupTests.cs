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
            Assert.AreEqual(0.12f, tuning.jumpBufferTime, 1e-4f);
            Assert.AreEqual(3.8f, tuning.cameraDistance, 1e-4f);
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
