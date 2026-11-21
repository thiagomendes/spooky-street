using System;
using NUnit.Framework;
using SpookyStreet.Core.Build;

namespace SpookyStreet.Tests.Core
{
    public class BuildArgumentsTests
    {
        [Test]
        public void ParsesKnownFlagsAndIgnoresUnityFlags()
        {
            var args = BuildArguments.Parse(new[]
            {
                "Unity", "-batchmode", "-quit", "-executeMethod", "X.Y", "-sssOutput", "build/iOS",
                "-sssBundleId", "com.example.spooky", "-sssBuildNumber", "57.2", "-sssCommit", "abc123",
                "-sssRunId", "999", "-sssDevelopment",
            });
            Assert.AreEqual("build/iOS", args.OutputPath);
            Assert.AreEqual("com.example.spooky", args.BundleId);
            Assert.AreEqual("57.2", args.BuildNumber);
            Assert.AreEqual("abc123", args.Commit);
            Assert.AreEqual("999", args.RunId);
            Assert.IsTrue(args.Development);
        }

        [Test]
        public void DefaultsAreSafe()
        {
            var args = BuildArguments.Parse(new string[0]);
            Assert.AreEqual("0", args.BuildNumber);
            Assert.IsNull(args.BundleId);
            Assert.IsFalse(args.Development);
        }

        [TestCase("1")]
        [TestCase("57.1")]
        [TestCase("1.2.3")]
        public void AcceptsAppleBuildNumbers(string value)
        {
            Assert.AreEqual(value, BuildArguments.ValidateBuildNumber(value));
        }

        [TestCase("")]
        [TestCase("1..2")]
        [TestCase("1.2.3.4")]
        [TestCase("v12")]
        public void RejectsInvalidBuildNumbers(string value)
        {
            Assert.Throws<ArgumentException>(() => BuildArguments.ValidateBuildNumber(value));
        }

        [Test]
        public void RejectsBadBundleIdsAndMissingValues()
        {
            Assert.Throws<ArgumentException>(() => BuildArguments.ValidateBundleId("spooky"));
            Assert.Throws<ArgumentException>(() => BuildArguments.ValidateBundleId("com.example.spooky_street"));
            Assert.Throws<ArgumentException>(() => BuildArguments.Parse(new[] { "-sssBuildNumber", "-quit" }));
        }
    }
}
