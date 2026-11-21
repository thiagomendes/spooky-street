using System;
using System.Collections.Generic;
using System.IO;
using SpookyStreet.Core.Localization;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpookyStreet.EditorTools
{
    /// <summary>
    /// Project invariants that must hold before any player build (and are asserted by EditMode tests).
    /// Returns human-readable problems instead of throwing so CI logs show all of them at once.
    /// </summary>
    public static class ProjectChecks
    {
        public const string GameplayScene = "Assets/SpookyStreet/Scenes/LanternLane.unity";
        public const string EnglishTablePath = "Assets/SpookyStreet/Content/Resources/Localization/en.json";
        public const string PinnedVersionFile = "ProjectSettings/ProjectVersion.txt";

        public static List<string> Run()
        {
            var problems = new List<string>();

            string pinned = ReadPinnedEditorVersion();
            if (pinned != Application.unityVersion)
            {
                problems.Add($"Editor {Application.unityVersion} differs from pinned {pinned} in {PinnedVersionFile}.");
            }

            RenderPipelineAsset pipeline = GraphicsSettings.defaultRenderPipeline;
            if (pipeline == null || pipeline.GetType().FullName !=
                "UnityEngine.Rendering.Universal.UniversalRenderPipelineAsset")
            {
                problems.Add("Graphics settings must use a Universal Render Pipeline asset (found " +
                             (pipeline == null ? "none" : pipeline.GetType().FullName) + ").");
            }

            int inputHandler = ReadActiveInputHandler();
            if (inputHandler != 1)
            {
                problems.Add($"Active Input Handling must be 'Input System Package (New)' (1), found {inputHandler}.");
            }

            var scenes = EditorBuildSettings.scenes;
            if (scenes.Length == 0 || scenes[0].path != GameplayScene || !scenes[0].enabled)
            {
                problems.Add($"Build settings must list {GameplayScene} first and enabled.");
            }

            foreach (var scene in scenes)
            {
                if (scene.enabled && !File.Exists(scene.path))
                {
                    problems.Add("Build settings reference a missing scene: " + scene.path);
                }
            }

            try
            {
                var table = StringTable.Parse(File.ReadAllText(EnglishTablePath));
                foreach (string key in LocalizationKeys.All)
                {
                    if (!table.Contains(key))
                    {
                        problems.Add("Bundled English table is missing key " + key);
                    }
                }
            }
            catch (Exception e) when (e is IOException || e is FormatException)
            {
                problems.Add("Bundled English table unreadable: " + e.Message);
            }

            if (PlayerSettings.defaultInterfaceOrientation != UIOrientation.AutoRotation ||
                PlayerSettings.allowedAutorotateToPortrait || PlayerSettings.allowedAutorotateToPortraitUpsideDown ||
                !PlayerSettings.allowedAutorotateToLandscapeLeft || !PlayerSettings.allowedAutorotateToLandscapeRight)
            {
                problems.Add("Player orientation must be landscape-only auto-rotation.");
            }

            return problems;
        }

        public static string ReadPinnedEditorVersion()
        {
            foreach (string line in File.ReadAllLines(PinnedVersionFile))
            {
                if (line.StartsWith("m_EditorVersion:", StringComparison.Ordinal))
                {
                    return line.Substring("m_EditorVersion:".Length).Trim();
                }
            }

            return "(unset)";
        }

        /// <summary>0 = legacy Input Manager, 1 = Input System package, 2 = both.</summary>
        public static int ReadActiveInputHandler()
        {
            var settings = AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/ProjectSettings.asset");
            if (settings == null || settings.Length == 0)
            {
                return -1;
            }

            var property = new SerializedObject(settings[0]).FindProperty("activeInputHandler");
            return property == null ? -1 : property.intValue;
        }

        /// <summary>Batch entry point: <c>-executeMethod SpookyStreet.EditorTools.ProjectChecks.RunFromCommandLine</c>.</summary>
        public static void RunFromCommandLine()
        {
            var problems = Run();
            foreach (string problem in problems)
            {
                Debug.LogError("[ProjectChecks] " + problem);
            }

            Debug.Log($"[ProjectChecks] {problems.Count} problem(s).");
            EditorApplication.Exit(problems.Count == 0 ? 0 : 1);
        }
    }
}
