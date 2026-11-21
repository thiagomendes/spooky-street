using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.Build
{
    /// <summary>
    /// Command-line contract for the versioned batch build method (spec §7 step 3). Kept engine-free so
    /// the parsing rules are unit-tested. Flags use a "-sss" prefix to avoid clashing with Unity's own.
    /// </summary>
    public sealed class BuildArguments
    {
        public const int ContractVersion = 1;

        public string OutputPath { get; private set; }

        public string BundleId { get; private set; }

        /// <summary>CFBundleVersion / versionCode placeholder; the macOS job may override it per attempt.</summary>
        public string BuildNumber { get; private set; } = "0";

        public string Commit { get; private set; } = "unknown";

        public string RunId { get; private set; } = "local";

        public bool Development { get; private set; }

        public static BuildArguments Parse(IList<string> args)
        {
            if (args == null)
            {
                throw new ArgumentNullException(nameof(args));
            }

            var result = new BuildArguments();
            for (int i = 0; i < args.Count; i++)
            {
                string flag = args[i];
                switch (flag)
                {
                    case "-sssOutput":
                        result.OutputPath = Value(args, ref i, flag);
                        break;
                    case "-sssBundleId":
                        result.BundleId = ValidateBundleId(Value(args, ref i, flag));
                        break;
                    case "-sssBuildNumber":
                        result.BuildNumber = ValidateBuildNumber(Value(args, ref i, flag));
                        break;
                    case "-sssCommit":
                        result.Commit = Value(args, ref i, flag);
                        break;
                    case "-sssRunId":
                        result.RunId = Value(args, ref i, flag);
                        break;
                    case "-sssDevelopment":
                        result.Development = true;
                        break;
                }
            }

            return result;
        }

        /// <summary>Apple CFBundleVersion: one to three period-separated non-negative integers.</summary>
        public static string ValidateBuildNumber(string value)
        {
            string[] parts = value.Split('.');
            if (parts.Length < 1 || parts.Length > 3)
            {
                throw new ArgumentException($"Build number '{value}' must have 1–3 numeric components.");
            }

            foreach (string part in parts)
            {
                if (part.Length == 0 || part.Length > 9)
                {
                    throw new ArgumentException($"Build number '{value}' has an empty or oversized component.");
                }

                foreach (char c in part)
                {
                    if (c < '0' || c > '9')
                    {
                        throw new ArgumentException($"Build number '{value}' must contain only digits and periods.");
                    }
                }
            }

            return value;
        }

        /// <summary>Reverse-DNS identifier using letters, digits, hyphens and periods.</summary>
        public static string ValidateBundleId(string value)
        {
            if (string.IsNullOrEmpty(value) || value.IndexOf('.') <= 0 || value.EndsWith(".", StringComparison.Ordinal))
            {
                throw new ArgumentException($"Bundle identifier '{value}' must be reverse-DNS, e.g. com.example.game.");
            }

            foreach (char c in value)
            {
                bool ok = (c >= 'a' && c <= 'z') || (c >= 'A' && c <= 'Z') || (c >= '0' && c <= '9') || c == '.' ||
                          c == '-';
                if (!ok)
                {
                    throw new ArgumentException($"Bundle identifier '{value}' contains '{c}'.");
                }
            }

            return value;
        }

        private static string Value(IList<string> args, ref int i, string flag)
        {
            if (i + 1 >= args.Count || args[i + 1].StartsWith("-", StringComparison.Ordinal))
            {
                throw new ArgumentException($"{flag} requires a value.");
            }

            i++;
            return args[i];
        }
    }
}
