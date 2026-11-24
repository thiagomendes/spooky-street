using System;
using System.IO;
using NUnit.Framework;
using SpookyStreet.Core.Localization;

namespace SpookyStreet.Tests.Core
{
    public class LocalizationTests
    {
        private const string Minimal =
            "{\"schemaVersion\":1,\"locale\":\"en\",\"nativeName\":\"English\",\"entries\":[" +
            "{\"key\":\"a\",\"value\":\"Alpha\",\"context\":\"ctx\"},{\"key\":\"fmt\",\"value\":\"Hi {0}\"}]}";

        /// <summary>Finds the bundled English table whether running in Unity or the dotnet test project.</summary>
        public static string BundledEnglishJson() => File.ReadAllText(BundledEnglishPath());

        /// <summary>Path of the bundled English table (its folder also holds languages.json).</summary>
        public static string BundledEnglishPath()
        {
            string relative = Path.Combine("Assets", "SpookyStreet", "Content", "Resources", "Localization", "en.json");
            string dir = Directory.GetCurrentDirectory();
            for (int i = 0; i < 8 && dir != null; i++)
            {
                string candidate = Path.Combine(dir, relative);
                if (File.Exists(candidate))
                {
                    return candidate;
                }

                dir = Path.GetDirectoryName(dir);
            }

            string fromEnv = Environment.GetEnvironmentVariable("SSS_PROJECT_ROOT");
            if (!string.IsNullOrEmpty(fromEnv))
            {
                return Path.Combine(fromEnv, relative);
            }

            throw new FileNotFoundException("Could not locate " + relative);
        }

        [Test]
        public void ParsesEntriesAndContext()
        {
            var table = StringTable.Parse(Minimal);
            Assert.AreEqual("en", table.Locale);
            Assert.AreEqual(2, table.Count);
            Assert.IsTrue(table.TryGet("a", out string value));
            Assert.AreEqual("Alpha", value);
        }

        [Test]
        public void RejectsDuplicateKeysAndNewerSchema()
        {
            Assert.Throws<FormatException>(() => StringTable.Parse(
                "{\"schemaVersion\":1,\"locale\":\"en\",\"entries\":[{\"key\":\"a\",\"value\":\"1\"},{\"key\":\"a\",\"value\":\"2\"}]}"));
            Assert.Throws<FormatException>(() => StringTable.Parse(
                "{\"schemaVersion\":99,\"locale\":\"en\",\"entries\":[]}"));
            Assert.Throws<FormatException>(() => StringTable.Parse("{\"schemaVersion\":1,\"locale\":\"en\""));
        }

        [Test]
        public void MissingKeyFallsBackToBundledEnglish()
        {
            var english = StringTable.Parse(Minimal);
            var partial = new StringTable("xx", "Test", "1", new[] { new StringEntry("a", "Alfa", null) });
            var catalog = new LocalizationCatalog(english);
            catalog.SetActive(partial);
            Assert.AreEqual("Alfa", catalog.Get("a"));
            Assert.AreEqual("Hi Pip", catalog.Format("fmt", "Pip"));
        }

        [Test]
        public void KeyMissingEverywhereShowsMarkerAndReportsOnce()
        {
            var catalog = new LocalizationCatalog(StringTable.Parse(Minimal));
            int reports = 0;
            catalog.MissingKey += _ => reports++;
            Assert.AreEqual("⟦nope⟧", catalog.Get("nope"));
            catalog.Get("nope");
            Assert.AreEqual(1, reports);
        }

        [Test]
        public void BrokenTranslatedPlaceholderDoesNotThrow()
        {
            var english = StringTable.Parse(Minimal);
            var broken = new StringTable("xx", "Test", "1", new[] { new StringEntry("fmt", "Oi {1", null) });
            var catalog = new LocalizationCatalog(english);
            catalog.SetActive(broken);
            Assert.AreEqual("Oi {1", catalog.Format("fmt", "Pip"));
        }

        [Test]
        public void ResetToFallbackRaisesChanged()
        {
            var catalog = new LocalizationCatalog(StringTable.Parse(Minimal));
            int changes = 0;
            catalog.Changed += () => changes++;
            catalog.SetActive(PseudoLocalizer.CreateTable(catalog.Fallback));
            catalog.ResetToFallback();
            Assert.AreEqual(2, changes);
            Assert.AreSame(catalog.Fallback, catalog.Active);
        }

        [Test]
        public void PseudoExpandsAtLeast35PercentAndKeepsPlaceholders()
        {
            string pseudo = PseudoLocalizer.Transform("Version {0} · {1}");
            StringAssert.Contains("{0}", pseudo);
            StringAssert.Contains("{1}", pseudo);
            string plain = "Drag on the left side of the screen to walk.";
            Assert.GreaterOrEqual(PseudoLocalizer.Transform(plain).Length, (int)Math.Ceiling(plain.Length * 1.35));
            Assert.AreNotEqual(plain, PseudoLocalizer.Transform(plain));
        }

        [Test]
        public void BundledEnglishContainsEveryCodeKeyWithContext()
        {
            var table = StringTable.Parse(BundledEnglishJson());
            Assert.AreEqual("en", table.Locale);
            foreach (string key in LocalizationKeys.All)
            {
                Assert.IsTrue(table.Contains(key), "bundled English is missing key " + key);
            }

            foreach (var entry in table.Entries)
            {
                Assert.IsNotEmpty(entry.Context, "translator context missing for " + entry.Key);
                Assert.IsNotEmpty(entry.Value, "empty English value for " + entry.Key);
            }
        }

        [Test]
        public void TitleAndFirstPromptAreLocalizedThroughKeys()
        {
            var catalog = new LocalizationCatalog(StringTable.Parse(BundledEnglishJson()));
            Assert.AreEqual("Spooky Street", catalog.Get(LocalizationKeys.TitleGameName));
            Assert.AreEqual("Three Knocks on Lantern Lane", catalog.Get(LocalizationKeys.TitleCaseName));
            catalog.SetActive(PseudoLocalizer.CreateTable(catalog.Fallback));
            StringAssert.StartsWith("[", catalog.Get(LocalizationKeys.TitleGameName));
            StringAssert.StartsWith("[", catalog.Get(LocalizationKeys.PromptMoveTouch));
        }

        [Test]
        public void JsonParsesEscapesAndRejectsGarbage()
        {
            var parsed = (System.Collections.Generic.Dictionary<string, object>)MiniJson.Parse(
                "{\"s\":\"a\\n\\u00e9\\\"\",\"n\":-1.5e1,\"b\":true,\"z\":null,\"l\":[1,2]}");
            Assert.AreEqual("a\né\"", parsed["s"]);
            Assert.AreEqual(-15.0, parsed["n"]);
            Assert.AreEqual(true, parsed["b"]);
            Assert.IsNull(parsed["z"]);
            Assert.Throws<FormatException>(() => MiniJson.Parse("{\"a\":1} trailing"));
            Assert.Throws<FormatException>(() => MiniJson.Parse("{'a':1}"));
        }
    }
}
