using System;
using System.IO;
using System.Linq;
using SpookyStreet.App;
using SpookyStreet.Core.Build;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;

namespace SpookyStreet.EditorTools
{
    /// <summary>
    /// Versioned batch build entry points (spec §7 step 3). CI calls, for example:
    /// <c>-executeMethod SpookyStreet.EditorTools.BuildCommand.BuildIos -sssOutput build/iOS
    /// -sssBundleId com.example.app -sssCommit SHA -sssRunId 123</c>.
    /// The iOS method only exports an Xcode project; signing happens later on macOS.
    /// </summary>
    public static class BuildCommand
    {
        public const string BuildInfoAssetPath = "Assets/SpookyStreet/Content/Resources/BuildInfo.json";
        public const string ReportDirectory = "ci-out";

        public static void BuildIos() => RunFromCommandLine(BuildTarget.iOS);

        public static void BuildAndroid() => RunFromCommandLine(BuildTarget.Android);

        private static void RunFromCommandLine(BuildTarget target)
        {
            int exitCode = 1;
            try
            {
                var args = BuildArguments.Parse(Environment.GetCommandLineArgs());
                BuildReport report = Build(target, args);
                exitCode = report.summary.result == BuildResult.Succeeded ? 0 : 1;
            }
            catch (Exception e)
            {
                Debug.LogError("[BuildCommand] " + e);
            }
            finally
            {
                EditorApplication.Exit(exitCode);
            }
        }

        public static BuildReport Build(BuildTarget target, BuildArguments args)
        {
            var problems = ProjectChecks.Run();
            if (problems.Count > 0)
            {
                throw new InvalidOperationException("Project checks failed:\n - " + string.Join("\n - ", problems));
            }

            if (!BuildPipeline.IsBuildTargetSupported(BuildPipeline.GetBuildTargetGroup(target), target))
            {
                throw new InvalidOperationException(
                    $"Build support for {target} is not installed in this editor. Use the matching CI editor image.");
            }

            NamedBuildTarget named = NamedBuildTarget.FromBuildTargetGroup(BuildPipeline.GetBuildTargetGroup(target));
            if (!string.IsNullOrEmpty(args.BundleId))
            {
                PlayerSettings.SetApplicationIdentifier(named, args.BundleId);
            }

            if (target == BuildTarget.iOS)
            {
                PlayerSettings.iOS.buildNumber = args.BuildNumber;
                PlayerSettings.iOS.appleEnableAutomaticSigning = false;
                PlayerSettings.iOS.sdkVersion = iOSSdkVersion.DeviceSDK;
                PlayerSettings.SetScriptingBackend(named, ScriptingImplementation.IL2CPP);
            }
            else if (target == BuildTarget.Android)
            {
                PlayerSettings.SetScriptingBackend(named, ScriptingImplementation.IL2CPP);
                PlayerSettings.Android.targetArchitectures = AndroidArchitecture.ARM64;
            }

            WriteBuildInfo(args);

            string output = string.IsNullOrEmpty(args.OutputPath) ? Path.Combine("build", target.ToString()) : args.OutputPath;
            var options = new BuildPlayerOptions
            {
                scenes = EditorBuildSettings.scenes.Where(s => s.enabled).Select(s => s.path).ToArray(),
                locationPathName = output,
                target = target,
                targetGroup = BuildPipeline.GetBuildTargetGroup(target),
                options = args.Development ? BuildOptions.Development : BuildOptions.None,
            };

            Debug.Log($"[BuildCommand] contract v{BuildArguments.ContractVersion} target={target} output={output} " +
                      $"bundle={PlayerSettings.GetApplicationIdentifier(named)} version={PlayerSettings.bundleVersion} " +
                      $"build={args.BuildNumber} commit={args.Commit} run={args.RunId} unity={Application.unityVersion}");
            BuildReport report = BuildPipeline.BuildPlayer(options);
            WriteReport(report, target, args);
            return report;
        }

        private static void WriteBuildInfo(BuildArguments args)
        {
            var info = new BuildInfo
            {
                commit = args.Commit,
                runId = args.RunId,
                buildNumber = args.BuildNumber,
                unityVersion = Application.unityVersion,
                builtAtUtc = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ"),
            };
            Directory.CreateDirectory(Path.GetDirectoryName(BuildInfoAssetPath));
            File.WriteAllText(BuildInfoAssetPath, JsonUtility.ToJson(info, true));
            AssetDatabase.ImportAsset(BuildInfoAssetPath, ImportAssetOptions.ForceSynchronousImport);
        }

        private static void WriteReport(BuildReport report, BuildTarget target, BuildArguments args)
        {
            Directory.CreateDirectory(ReportDirectory);
            var summary = report.summary;
            string json = "{\n" +
                          $"  \"target\": \"{target}\",\n" +
                          $"  \"result\": \"{summary.result}\",\n" +
                          $"  \"unityVersion\": \"{Application.unityVersion}\",\n" +
                          $"  \"bundleVersion\": \"{PlayerSettings.bundleVersion}\",\n" +
                          $"  \"buildNumber\": \"{args.BuildNumber}\",\n" +
                          $"  \"commit\": \"{args.Commit}\",\n" +
                          $"  \"runId\": \"{args.RunId}\",\n" +
                          $"  \"totalErrors\": {summary.totalErrors},\n" +
                          $"  \"totalWarnings\": {summary.totalWarnings},\n" +
                          $"  \"totalSizeBytes\": {summary.totalSize},\n" +
                          $"  \"durationSeconds\": {summary.totalTime.TotalSeconds:0.0}\n" +
                          "}\n";
            File.WriteAllText(Path.Combine(ReportDirectory, $"build-report-{target}.json"), json);
            Debug.Log($"[BuildCommand] {target} build {summary.result}: {summary.totalErrors} error(s), " +
                      $"{summary.totalWarnings} warning(s), {summary.totalTime.TotalSeconds:0}s");
        }
    }
}
