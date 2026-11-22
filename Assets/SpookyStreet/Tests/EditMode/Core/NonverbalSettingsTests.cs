using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Localization;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Core.Save;
using NvKeys = SpookyStreet.Core.Localization.LocalizationKeys.NonverbalUi;

namespace SpookyStreet.Tests.Core
{
    /// <summary>Helper settings (D23.4), the retired reading mode (D23.1, one hybrid mode) and the non-verbal UI strings.</summary>
    public class NonverbalSettingsTests
    {
        [Test]
        public void DefaultsMatchTheOwnerDecisions()
        {
            var s = new NonverbalSettings();
            Assert.IsFalse(s.HelperAutoHints, "Helper is opt-in");
            StringAssert.DoesNotContain("showSpeechText", NonverbalSettings.Serialize(s), "one hybrid mode: no words setting");
            StringAssert.DoesNotContain("firstLaunchChoiceMade", NonverbalSettings.Serialize(s), "no first-launch choice");
        }

        [Test]
        public void Tier3NeverAutoShowsWithHelperOff()
        {
            var s = new NonverbalSettings();
            Assert.IsFalse(s.MayAutoShowTier3(10000f));
            s.HelperAutoHints = true;
            Assert.IsFalse(s.MayAutoShowTier3(179f));
            Assert.IsTrue(s.MayAutoShowTier3(NonverbalSettings.AutoHintStallSeconds));
        }

        [Test]
        public void RoundTripsAndToleratesMissingFields()
        {
            var s = new NonverbalSettings { HelperAutoHints = true };
            Assert.IsTrue(NonverbalSettings.TryRead(NonverbalSettings.Serialize(s), out var read, out _));
            Assert.IsTrue(read.HelperAutoHints);

            Assert.IsTrue(NonverbalSettings.TryRead("{\"schemaVersion\":2,\"helperAutoHints\":\"yes\"}", out var partial, out _));
            Assert.IsFalse(partial.HelperAutoHints, "invalid field takes its default");

            Assert.AreEqual(SaveCheck.Newer, NonverbalSettings.Check("{\"schemaVersion\":3}"));
            Assert.AreEqual(SaveCheck.Invalid, NonverbalSettings.Check("not json"));
            Assert.AreEqual(SaveCheck.Invalid, NonverbalSettings.Check("{\"schemaVersion\":0}"));
        }

        [Test]
        public void ASchemaOneFileFromTheReadingModeEraStillLoads()
        {
            // Written by the build with the first-launch choice (Pictures only picked, Helper on).
            const string old = "{\"schemaVersion\":1,\"showSpeechText\":false,\"helperAutoHints\":true,\"firstLaunchChoiceMade\":true}";
            Assert.AreEqual(SaveCheck.Valid, NonverbalSettings.Check(old));
            var fs = new MemorySaveFileSystem();
            var paths = NonverbalSettingsStore.PathsFor("saves");
            fs.Files[paths.Latest] = old;
            var store = new NonverbalSettingsStore(fs, "saves");
            var loaded = store.Load();
            Assert.AreEqual(SaveLoadStatus.Latest, store.LastLoadStatus, "an old file is a valid file, not a recovery");
            Assert.IsTrue(loaded.HelperAutoHints, "Helper survives the migration");

            store.Save(loaded);
            string written = fs.Files[paths.Latest];
            StringAssert.Contains("\"schemaVersion\": 2", written);
            StringAssert.DoesNotContain("showSpeechText", written, "the retired field is dropped on the next save");
            StringAssert.DoesNotContain("firstLaunchChoiceMade", written);
        }

        [Test]
        public void StoreSavesInItsOwnFileAndRecovers()
        {
            var fs = new MemorySaveFileSystem();
            var store = new NonverbalSettingsStore(fs, "saves");
            Assert.IsFalse(store.Load().HelperAutoHints);
            Assert.AreEqual(SaveLoadStatus.Empty, store.LastLoadStatus);

            var s = new NonverbalSettings { HelperAutoHints = true };
            store.Save(s);
            store.Save(s);

            var paths = NonverbalSettingsStore.PathsFor("saves");
            Assert.AreNotEqual(SaveSlotPaths.ForPreferences("saves").Latest, paths.Latest);
            Assert.AreNotEqual(SaveSlotPaths.ForCase("saves").Latest, paths.Latest);
            Assert.IsTrue(fs.Files.ContainsKey(paths.Previous), "one previous valid copy is kept");

            var loaded = new NonverbalSettingsStore(fs, "saves").Load();
            Assert.IsTrue(loaded.HelperAutoHints);

            fs.Files[paths.Latest] = "{ broken";
            var recovering = new NonverbalSettingsStore(fs, "saves");
            var recovered = recovering.Load();
            Assert.AreEqual(SaveLoadStatus.Recovered, recovering.LastLoadStatus);
            Assert.IsTrue(recovered.HelperAutoHints, "the previous copy still has Helper on");
        }

        [Test]
        public void ACrashAtAnyStepKeepsTheOldOrNewSettings()
        {
            var baseFs = new MemorySaveFileSystem();
            new NonverbalSettingsStore(baseFs, "saves").Save(new NonverbalSettings { HelperAutoHints = true });

            for (int crashAfter = 0; crashAfter < 6; crashAfter++)
            {
                var fs = baseFs.Clone();
                var store = new NonverbalSettingsStore(fs, "saves");
                var s = store.Load();
                s.HelperAutoHints = false;
                fs.CrashAfter = fs.Operations + crashAfter;
                bool crashed = false;
                try
                {
                    store.Save(s);
                }
                catch (SimulatedCrash)
                {
                    crashed = true;
                }

                fs.CrashAfter = -1;
                var reload = new NonverbalSettingsStore(fs, "saves");
                var after = reload.Load();
                Assert.That(reload.LastLoadStatus, Is.EqualTo(SaveLoadStatus.Latest).Or.EqualTo(SaveLoadStatus.Recovered),
                    "crash after " + crashAfter + ": the old or the new file is readable");
                if (!crashed)
                {
                    Assert.IsFalse(after.HelperAutoHints, "crash after " + crashAfter + ": the save completed");
                }
            }
        }

        [Test]
        public void ANewerSettingsFileIsKeptAsideOnlyWhenThePlayerChangesSomething()
        {
            var fs = new MemorySaveFileSystem();
            var paths = NonverbalSettingsStore.PathsFor("saves");
            const string newer = "{\"schemaVersion\":7,\"helperAutoHints\":true}";
            fs.Files[paths.Latest] = newer;
            var store = new NonverbalSettingsStore(fs, "saves");
            Assert.IsFalse(store.Load().HelperAutoHints, "defaults while unreadable");
            Assert.AreEqual(newer, fs.Files[paths.Latest], "untouched by loading");
            store.Save(new NonverbalSettings { HelperAutoHints = true });
            Assert.AreEqual(newer, fs.Files[paths.Kept]);
        }

        [Test]
        public void EveryNonverbalKeyIsInBundledEnglishWithContext()
        {
            var table = StringTable.Parse(LocalizationTests.BundledEnglishJson());
            var contexts = new Dictionary<string, string>();
            foreach (var entry in table.Entries)
            {
                contexts[entry.Key] = entry.Context;
            }

            Assert.AreEqual(4, NvKeys.All.Length);
            foreach (string key in NvKeys.All)
            {
                Assert.IsTrue(contexts.ContainsKey(key), "en.json is missing " + key);
                Assert.IsFalse(string.IsNullOrWhiteSpace(contexts[key]), key + " needs translator context");
            }

            Assert.AreEqual(NvKeys.SettingsHelper, NonverbalSettings.HelperLabelKey);
            foreach (string retired in new[] { "ui.reading.choice_words", "ui.reading.choice_pictures", "ui.settings.show_speech_text" })
            {
                Assert.IsFalse(contexts.ContainsKey(retired), retired + " was retired with D23.1 (one hybrid mode)");
            }
        }
    }
}
