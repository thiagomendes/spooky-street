using System;
using UnityEngine;

namespace SpookyStreet.App
{
    /// <summary>
    /// Build provenance embedded by the CI batch build (Resources/BuildInfo.json) so testers can quote
    /// the exact commit and workflow run in TestFlight feedback. Absent in editor play mode.
    /// </summary>
    [Serializable]
    public sealed class BuildInfo
    {
        public const string ResourcePath = "BuildInfo";

        public string commit = "editor";
        public string runId = "local";
        public string buildNumber = "0";
        public string unityVersion = string.Empty;
        public string builtAtUtc = string.Empty;

        public string ShortCommit => string.IsNullOrEmpty(commit) || commit.Length <= 7 ? commit : commit.Substring(0, 7);

        /// <summary>Human-readable identifier such as "abc1234 · run 57".</summary>
        public string Identifier => runId == "local" ? ShortCommit : ShortCommit + " · run " + runId;

        public static BuildInfo Load()
        {
            var asset = Resources.Load<TextAsset>(ResourcePath);
            if (asset == null)
            {
                return new BuildInfo { unityVersion = Application.unityVersion };
            }

            try
            {
                return JsonUtility.FromJson<BuildInfo>(asset.text) ?? new BuildInfo();
            }
            catch (ArgumentException)
            {
                return new BuildInfo();
            }
        }
    }
}
