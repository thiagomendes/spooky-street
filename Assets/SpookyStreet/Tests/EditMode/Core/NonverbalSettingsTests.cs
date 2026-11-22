using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Localization;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Core.Save;
using NvKeys = SpookyStreet.Core.Localization.LocalizationKeys.NonverbalUi;

namespace SpookyStreet.Tests.Core
{
    /// <summary>Reading mode and Helper settings (D23.1, D23.4) and the non-verbal UI strings.</summary>
    public class NonverbalSettingsTests
    {
        [Test]
        public void DefaultsMatchTheOwnerDecisions()
        {
            var s = new NonverbalSettings();
            Assert.IsTrue(s.ShowSpeechText, "the small text line is on by default");
            Assert.IsFalse(s.HelperAutoHints, "Helper is opt-in");
            Assert.IsFalse(s.FirstLaunchChoiceMade);
            Assert.IsTrue(s.NeedsFirstLaunchChoice);
        }

        [Test]
        public void OneTapChoiceSetsTheSingleSetting()
        {
            var s = new NonverbalSettings();
            s.ChooseReadingMode(picturesOnly: true);
            Assert.IsFalse(s.ShowSpeechText);
            Assert.IsFalse(s.NeedsFirstLaunchChoice);
            s.ChooseReadingMode(picturesOnly: false);
            Assert.IsTrue(s.ShowSpeechText);
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
            var s = new NonverbalSettings { ShowSpeechText = false, HelperAutoHints = true, FirstLaunchChoiceMade = true };
            Assert.IsTrue(NonverbalSettings.TryRead(NonverbalSettings.Serialize(s), out var read, out _));
            Assert.IsFalse(read.ShowSpeechText);
            Assert.IsTrue(read.HelperAutoHints);
            Assert.IsTrue(read.FirstLaunchChoiceMade);

            Assert.IsTrue(NonverbalSettings.TryRead("{\"schemaVersion\":1,\"helperAutoHints\":\"yes\"}", out var partial, out _));
            Assert.IsTrue(partial.ShowSpeechText, "missing field takes its default");
            Assert.IsFalse(partial.HelperAutoHints, "invalid field takes its default");

            Assert.AreEqual(SaveCheck.Newer, NonverbalSettings.Check("{\"schemaVersion\":2}"));
            Assert.AreEqual(SaveCheck.Invalid, NonverbalSettings.Check("not json"));
            Assert.AreEqual(SaveCheck.Invalid, NonverbalSettings.Check("{\"schemaVersion\":0}"));
        }

        [Test]
        public void StoreSavesInItsOwnFileAndRecovers()
        {
            var fs = new MemorySaveFileSystem();
            var store = new NonverbalSettingsStore(fs, "saves");
            Assert.IsTrue(store.Load().NeedsFirstLaunchChoice);
            Assert.AreEqual(SaveLoadStatus.Empty, store.LastLoadStatus);

            var s = new NonverbalSettings();
            s.ChooseReadingMode(picturesOnly: true);
            store.Save(s);
            s.HelperAutoHints = true;
            store.Save(s);

            var paths = NonverbalSettingsStore.PathsFor("saves");
            Assert.AreNotEqual(SaveSlotPaths.ForPreferences("saves").Latest, paths.Latest);
            Assert.AreNotEqual(SaveSlotPaths.ForCase("saves").Latest, paths.Latest);
            Assert.IsTrue(fs.Files.ContainsKey(paths.Previous), "one previous valid copy is kept");

            var loaded = new NonverbalSettingsStore(fs, "saves").Load();
            Assert.IsFalse(loaded.ShowSpeechText);
            Assert.IsTrue(loaded.HelperAutoHints);
            Assert.IsFalse(loaded.NeedsFirstLaunchChoice);

            fs.Files[paths.Latest] = "{ broken";
            var recovering = new NonverbalSettingsStore(fs, "saves");
            var recovered = recovering.Load();
            Assert.AreEqual(SaveLoadStatus.Recovered, recovering.LastLoadStatus);
            Assert.IsFalse(recovered.ShowSpeechText, "the previous copy still has the reading choice");
        }

        [Test]
        public void ACrashAtAnyStepKeepsTheOldOrNewSettings()
        {
            var baseFs = new MemorySaveFileSystem();
            var first = new NonverbalSettings();
            first.ChooseReadingMode(picturesOnly: true);
            new NonverbalSettingsStore(baseFs, "saves").Save(first);

            for (int crashAfter = 0; crashAfter < 6; crashAfter++)
            {
                var fs = baseFs.Clone();
                var store = new NonverbalSettingsStore(fs, "saves");
                var s = store.Load();
                s.HelperAutoHints = true;
                fs.CrashAfter = fs.Operations + crashAfter;
                try
                {
                    store.Save(s);
                }
                catch (SimulatedCrash)
                {
                }

                fs.CrashAfter = -1;
                var after = new NonverbalSettingsStore(fs, "saves").Load();
                Assert.IsFalse(after.ShowSpeechText, "crash after " + crashAfter + ": the earlier choice survives");
                Assert.IsTrue(after.FirstLaunchChoiceMade, "crash after " + crashAfter);
            }
        }

        [Test]
        public void ANewerSettingsFileIsKeptAsideOnlyWhenThePlayerChangesSomething()
        {
            var fs = new MemorySaveFileSystem();
            var paths = NonverbalSettingsStore.PathsFor("saves");
            const string newer = "{\"schemaVersion\":7,\"showSpeechText\":false}";
            fs.Files[paths.Latest] = newer;
            var store = new NonverbalSettingsStore(fs, "saves");
            Assert.IsTrue(store.Load().ShowSpeechText, "defaults while unreadable");
            Assert.AreEqual(newer, fs.Files[paths.Latest], "untouched by loading");
            store.Save(new NonverbalSettings { FirstLaunchChoiceMade = true });
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

            Assert.AreEqual(7, NvKeys.All.Length);
            foreach (string key in NvKeys.All)
            {
                Assert.IsTrue(contexts.ContainsKey(key), "en.json is missing " + key);
                Assert.IsFalse(string.IsNullOrWhiteSpace(contexts[key]), key + " needs translator context");
            }

            Assert.AreEqual(NvKeys.SettingsShowSpeechText, NonverbalSettings.ShowSpeechTextLabelKey);
            Assert.AreEqual(NvKeys.SettingsHelper, NonverbalSettings.HelperLabelKey);
            var catalog = new LocalizationCatalog(table);
            Assert.AreEqual("Pictures only", catalog.Get(NvKeys.ReadingChoicePictures));
        }
    }
}
