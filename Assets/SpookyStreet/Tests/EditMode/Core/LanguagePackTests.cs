using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SpookyStreet.Core.Localization;
using SpookyStreet.Core.Save;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// An in-memory pack host for tests: serves bytes by URL synchronously, or fails the way a phone's network does
    /// (offline, cut off part-way). Records every URL asked for, so tests can prove nothing is fetched unasked.
    /// </summary>
    public sealed class FakePackHost : IPackTransport
    {
        public const string CatalogUrl = "https://packs.example.test/spooky/catalog.json";

        public Dictionary<string, byte[]> Files { get; } = new Dictionary<string, byte[]>(StringComparer.Ordinal);

        public List<string> Requests { get; } = new List<string>();

        public bool Offline { get; set; }

        /// <summary>Deliver only this share of a file, then report the connection lost (an interrupted download).</summary>
        public float CutOffAt { get; set; } = -1f;

        /// <summary>Hold the answer until <see cref="Release"/> (a download in progress).</summary>
        public bool Hold { get; set; }

        private Action _pending;

        public void Get(string url, long maxBytes, Action<float> progress, Action<PackFetch> done)
        {
            Requests.Add(url);
            Action answer = () =>
            {
                if (Offline || !Files.TryGetValue(url, out byte[] bytes))
                {
                    done(PackFetch.Failure(PackError.Offline, Offline ? "offline" : "404"));
                    return;
                }

                progress?.Invoke(0.5f);
                if (CutOffAt >= 0f)
                {
                    byte[] partial = bytes.Take((int)(bytes.Length * CutOffAt)).ToArray();
                    done(PackFetch.Failure(PackError.Interrupted, "connection lost", partial));
                    return;
                }

                progress?.Invoke(1f);
                done(PackFetch.Success(bytes));
            };

            if (Hold)
            {
                _pending = answer;
            }
            else
            {
                answer();
            }
        }

        public void Release()
        {
            var pending = _pending;
            _pending = null;
            pending?.Invoke();
        }

        /// <summary>Publishes a table as a pack next to the catalog and returns its manifest.</summary>
        public PackManifest Publish(StringTable table, bool development = false, string file = null)
        {
            file = file ?? table.Locale + ".json";
            Files["https://packs.example.test/spooky/" + file] = PackHash.Utf8.GetBytes(table.ToJson());
            return PackManifest.For(table, file, development);
        }

        public void PublishCatalog(params PackManifest[] packs)
        {
            Files[CatalogUrl] = PackHash.Utf8.GetBytes(new PackCatalog(packs).ToJson());
        }
    }

    /// <summary>A file system that is full: every write fails.</summary>
    public sealed class FullDiskFileSystem : ISaveFileSystem
    {
        public FullDiskFileSystem(MemorySaveFileSystem inner)
        {
            Inner = inner;
        }

        public MemorySaveFileSystem Inner { get; }

        public bool Exists(string path) => Inner.Exists(path);

        public string ReadAllText(string path) => Inner.ReadAllText(path);

        public void WriteAllTextDurable(string path, string contents)
        {
            Inner.Files[path] = contents.Substring(0, contents.Length / 3); // a partial file is left behind
            throw new IOException("No space left on device");
        }

        public void Replace(string source, string destination, string backup) => Inner.Replace(source, destination, backup);

        public void Move(string source, string destination) => Inner.Move(source, destination);

        public void Delete(string path) => Inner.Delete(path);
    }

    /// <summary>
    /// The language pack pipeline (S004 §2.4, spec §7 list): download, validate, stage, activate atomically, keep the
    /// old pack on any failure, fall back to English per key, survive restarts offline, remove, and never touch saves.
    /// </summary>
    public class LanguagePackTests
    {
        private const string Dir = "/save";

        private static StringTable English => StringTable.Parse(LocalizationTests.BundledEnglishJson());

        private static StringTable Pseudo() => PseudoLocalizer.CreateTable(English);

        /// <summary>A copy of <paramref name="source"/> with entries changed by <paramref name="edit"/> (null drops one).</summary>
        private static StringTable Edited(StringTable source, string locale, Func<StringEntry, StringEntry> edit,
            string nativeName = "Test")
        {
            var entries = new List<StringEntry>();
            foreach (var entry in source.Entries)
            {
                var changed = edit(entry);
                if (changed != null)
                {
                    entries.Add(changed);
                }
            }

            return new StringTable(locale, nativeName, source.ContentVersion, entries);
        }

        private static StringEntry WithValue(StringEntry e, string value) => new StringEntry(e.Key, value, e.Context);

        private static (PackManifest, byte[]) Pack(StringTable table)
        {
            byte[] bytes = PackHash.Utf8.GetBytes(table.ToJson());
            return (PackManifest.For(table, table.Locale + ".json"), bytes);
        }

        private static PackValidation Check(StringTable table)
        {
            var (manifest, bytes) = Pack(table);
            return PackValidator.Validate(manifest, bytes, English);
        }

        private static LanguagePacks Packs(ISaveFileSystem fs, FakePackHost host, bool development = true,
            Func<string> builtInPt = null)
        {
            var english = English;
            var builtIn = new List<(BuiltInLanguage, Func<string>)>
            {
                (new BuiltInLanguage("pt-BR", "Português (Brasil)", "Localization/pt-BR"), builtInPt ?? (() => null)),
            };
            var packs = new LanguagePacks(english, new PackInstaller(fs, Dir, english), host, FakePackHost.CatalogUrl,
                builtIn, development);
            packs.LoadInstalled();
            return packs;
        }

        private static Dictionary<string, string> NonLanguageFiles(MemorySaveFileSystem fs) =>
            fs.Files.Where(f => !f.Key.Contains(PackInstaller.FolderName)).ToDictionary(f => f.Key, f => f.Value);

        // ------------------------------------------------------------------------------------------- validation

        [Test]
        public void ThePseudoPackPassesEveryCheck()
        {
            var result = Check(Pseudo());
            Assert.IsTrue(result.Ok, result.Error + " " + result.Detail);
            Assert.AreEqual(1f, result.Coverage);
            Assert.AreEqual(PseudoLocalizer.Locale, result.Table.Locale);
        }

        [Test]
        public void ACorruptFileIsRefusedByItsHash()
        {
            var (manifest, bytes) = Pack(Pseudo());
            bytes[bytes.Length / 2] ^= 0x20;
            Assert.AreEqual(PackError.HashMismatch, PackValidator.Validate(manifest, bytes, English).Error);
            Assert.AreEqual(PackError.Interrupted,
                PackValidator.Validate(manifest, bytes.Take(bytes.Length - 10).ToArray(), English).Error);
            Assert.AreEqual(PackError.TooLarge,
                PackValidator.Validate(manifest, bytes.Concat(new byte[] { 32 }).ToArray(), English).Error);
            Assert.AreEqual(PackError.Interrupted, PackValidator.Validate(manifest, null, English).Error);
        }

        [Test]
        public void ANewerSchemaIsRefused()
        {
            var table = Pseudo();
            byte[] bytes = PackHash.Utf8.GetBytes(table.ToJson());
            var manifest = new PackManifest(table.Locale, table.NativeName, table.ContentVersion, 3, bytes.Length,
                PackHash.Sha256Hex(bytes), "x.json");
            Assert.AreEqual(PackError.NewerSchema, PackValidator.Validate(manifest, bytes, English).Error);

            byte[] newer = PackHash.Utf8.GetBytes(table.ToJson().Replace("\"schemaVersion\": 2", "\"schemaVersion\": 9"));
            var claimed = new PackManifest(table.Locale, table.NativeName, table.ContentVersion, 2, newer.Length,
                PackHash.Sha256Hex(newer), "x.json");
            Assert.AreEqual(PackError.NewerSchema, PackValidator.Validate(claimed, newer, English).Error,
                "the file's own schema counts even when the manifest understates it");
            Assert.AreEqual(PackError.NewerSchema, PackCatalog.TryParse("{\"catalogVersion\":2,\"packs\":[]}", out _));
        }

        [Test]
        public void AMissingGlyphIsRefused()
        {
            var cyrillic = Edited(Pseudo(), PseudoLocalizer.Locale,
                e => e.Key == LocalizationKeys.TitleNewGame ? WithValue(e, "Новая игра") : e, PseudoLocalizer.NativeName);
            var result = Check(cyrillic);
            Assert.AreEqual(PackError.MissingGlyph, result.Error);
            StringAssert.Contains(LocalizationKeys.TitleNewGame, result.Detail);
            StringAssert.Contains("U+041D", result.Detail);

            var emojiName = Edited(Pseudo(), PseudoLocalizer.Locale, e => e, "Fun \U0001F600");
            Assert.AreEqual(PackError.MissingGlyph, Check(emojiName).Error, "the native name is drawn too");
        }

        [Test]
        public void PlaceholdersAndTagsMustMatchEnglish()
        {
            var dropped = Edited(Pseudo(), PseudoLocalizer.Locale,
                e => e.Key == LocalizationKeys.TitleBuildLabel ? WithValue(e, "Version {0}") : e, PseudoLocalizer.NativeName);
            Assert.AreEqual(PackError.PlaceholderMismatch, Check(dropped).Error);
            var extra = Edited(Pseudo(), PseudoLocalizer.Locale,
                e => e.Key == LocalizationKeys.TitleNewGame ? WithValue(e, "New {2}") : e, PseudoLocalizer.NativeName);
            Assert.AreEqual(PackError.PlaceholderMismatch, Check(extra).Error);
            var broken = Edited(Pseudo(), PseudoLocalizer.Locale,
                e => e.Key == LocalizationKeys.TitleBuildLabel ? WithValue(e, "Version {0} · {1") : e, PseudoLocalizer.NativeName);
            Assert.AreEqual(PackError.PlaceholderMismatch, Check(broken).Error, "braces that would not format");
            var tag = Edited(Pseudo(), PseudoLocalizer.Locale,
                e => e.Key == LocalizationKeys.TitleNewGame ? WithValue(e, "<b>New</b> game") : e, PseudoLocalizer.NativeName);
            Assert.AreEqual(PackError.PlaceholderMismatch, Check(tag).Error, "rich-text tags English does not have");
            var reordered = Edited(Pseudo(), PseudoLocalizer.Locale,
                e => e.Key == LocalizationKeys.TitleBuildLabel ? WithValue(e, "{1} · versão {0}") : e, PseudoLocalizer.NativeName);
            Assert.IsTrue(Check(reordered).Ok, "translators may reorder placeholders");
        }

        [Test]
        public void PluralFormsMustMatchEnglishAndTheLocale()
        {
            var flat = Edited(Pseudo(), PseudoLocalizer.Locale,
                e => e.Key == LocalizationKeys.Recap.Stickers ? WithValue(e, "{0} štíçkérš") : e, PseudoLocalizer.NativeName);
            Assert.AreEqual(PackError.PluralMismatch, Check(flat).Error, "English has forms, the pack must too");

            var pluralised = Edited(Pseudo(), PseudoLocalizer.Locale, e => e.Key == LocalizationKeys.TitleNewGame
                ? new StringEntry(e.Key, null, e.Context, new Dictionary<PluralCategory, string> { [PluralCategory.Other] = "New" })
                : e, PseudoLocalizer.NativeName);
            Assert.AreEqual(PackError.PluralMismatch, Check(pluralised).Error);

            var portuguese = Edited(English, "pt-BR", e => e.Key == LocalizationKeys.Recap.Stickers
                ? new StringEntry(e.Key, null, e.Context, new Dictionary<PluralCategory, string> { [PluralCategory.Other] = "{0} adesivos" })
                : e, "Português (Brasil)");
            var result = Check(portuguese);
            Assert.AreEqual(PackError.PluralMismatch, result.Error, "pt needs its 'one' form");
            StringAssert.Contains("'one'", result.Detail);

            var countless = Edited(English, "pt-BR", e => e.Key == LocalizationKeys.Recap.Stickers
                ? new StringEntry(e.Key, null, e.Context, new Dictionary<PluralCategory, string>
                {
                    [PluralCategory.One] = "um adesivo",
                    [PluralCategory.Other] = "{0} adesivos",
                })
                : e, "Português (Brasil)");
            Assert.IsTrue(Check(countless).Ok, "a 'one' form may spell its number out");
        }

        [Test]
        public void TheLocaleMustMatchItsManifest()
        {
            var table = Pseudo();
            byte[] bytes = PackHash.Utf8.GetBytes(table.ToJson());
            var manifest = new PackManifest("fr", "Français", table.ContentVersion, 2, bytes.Length,
                PackHash.Sha256Hex(bytes), "fr.json");
            Assert.AreEqual(PackError.WrongLocale, PackValidator.Validate(manifest, bytes, English).Error);
            byte[] garbage = PackHash.Utf8.GetBytes("{ not json");
            var bad = new PackManifest("fr", "Français", "", 2, garbage.Length, PackHash.Sha256Hex(garbage), "fr.json");
            Assert.AreEqual(PackError.Unreadable, PackValidator.Validate(bad, garbage, English).Error);
            byte[] notUtf8 = { 0x7B, 0xFF, 0xFE, 0x7D };
            var binary = new PackManifest("fr", "Français", "", 2, notUtf8.Length, PackHash.Sha256Hex(notUtf8), "fr.json");
            Assert.AreEqual(PackError.Unreadable, PackValidator.Validate(binary, notUtf8, English).Error);
        }

        [Test]
        public void AnOlderPackLoadsAndItsMissingKeysShowEnglish()
        {
            var english = English;
            var newest = english.Entries.Skip(english.Count - 15).Select(e => e.Key).ToList();
            var older = Edited(Pseudo(), PseudoLocalizer.Locale, e => newest.Contains(e.Key) ? null : e, PseudoLocalizer.NativeName);
            var result = Check(older);
            Assert.IsTrue(result.Ok, result.Error + " " + result.Detail);
            Assert.Less(result.Coverage, 1f);
            CollectionAssert.AreEquivalent(newest, result.MissingKeys);

            var catalog = new LocalizationCatalog(english);
            catalog.SetActive(result.Table);
            Assert.IsTrue(english.TryGet(newest[0], out string englishText));
            Assert.AreEqual(englishText, catalog.Get(newest[0]), "a key the pack lacks falls back to bundled English");
            StringAssert.StartsWith("[", catalog.Get(LocalizationKeys.TitleNewGame));

            var half = Edited(Pseudo(), PseudoLocalizer.Locale, e => e.Key.GetHashCode() % 2 == 0 ? null : e,
                PseudoLocalizer.NativeName);
            Assert.AreEqual(PackError.Incomplete, Check(half).Error, "far too old for this game");

            var newer = new StringTable(PseudoLocalizer.Locale, PseudoLocalizer.NativeName, "9.9",
                Pseudo().Entries.Concat(new[] { new StringEntry("ui.future.thing", "[Füturé]", "later") }));
            Assert.IsTrue(Check(newer).Ok, "keys this build does not know are ignored");
        }

        // ------------------------------------------------------------------------------------------- installer

        [Test]
        public void AnInstalledPackSurvivesARestartOffline()
        {
            var fs = new MemorySaveFileSystem();
            var (manifest, bytes) = Pack(Pseudo());
            var installer = new PackInstaller(fs, Dir, English);
            installer.Load();
            var result = installer.Install(manifest, bytes);
            Assert.IsTrue(result.Ok, result.ToString());
            Assert.IsFalse(fs.Exists(installer.StagingPath(manifest.Locale)), "no staging file left");

            var relaunched = new PackInstaller(fs, Dir, English).Load();
            Assert.AreEqual(1, relaunched.Count);
            Assert.AreEqual(PseudoLocalizer.Locale, relaunched[0].Locale);
            StringAssert.StartsWith("[", relaunched[0].Table.TryGet(LocalizationKeys.TitleNewGame, out string v) ? v : "");

            var host = new FakePackHost { Offline = true };
            var packs = Packs(fs, host);
            CollectionAssert.Contains(packs.UsableLocales.ToList(), PseudoLocalizer.Locale, "selectable offline");
            Assert.IsEmpty(host.Requests, "launch makes no network call");
        }

        [Test]
        public void AnInterruptedDownloadKeepsTheOldPack()
        {
            var fs = new MemorySaveFileSystem();
            var host = new FakePackHost();
            host.PublishCatalog(host.Publish(Pseudo(), development: true));
            var packs = Packs(fs, host);
            packs.FetchCatalog();
            PackResult first = null;
            packs.Download(PseudoLocalizer.Locale, r => first = r);
            Assert.IsTrue(first.Ok, first.ToString());

            var v2 = Edited(Pseudo(), PseudoLocalizer.Locale,
                e => e.Key == LocalizationKeys.TitleNewGame ? WithValue(e, "[Ñéw v2]") : e, PseudoLocalizer.NativeName);
            host.PublishCatalog(host.Publish(v2, development: true));
            packs.FetchCatalog();
            Assert.IsTrue(packs.Option(PseudoLocalizer.Locale).Outdated, "the catalog has a newer copy");
            host.CutOffAt = 0.4f;
            PackResult second = null;
            packs.Download(PseudoLocalizer.Locale, r => second = r);
            Assert.AreEqual(PackError.Interrupted, second.Error);
            Assert.AreEqual(PackError.Interrupted, packs.Option(PseudoLocalizer.Locale).LastError);
            Assert.IsFalse(packs.Busy);

            var reloaded = Packs(fs, host);
            Assert.IsTrue(reloaded.TableFor(PseudoLocalizer.Locale).TryGet(LocalizationKeys.TitleNewGame, out string text));
            Assert.AreNotEqual("[Ñéw v2]", text, "still the first copy");

            host.CutOffAt = -1f;
            PackResult third = null;
            reloaded.FetchCatalog();
            reloaded.Download(PseudoLocalizer.Locale, r => third = r);
            Assert.IsTrue(third.Ok, third.ToString());
            Assert.IsTrue(Packs(fs, host).TableFor(PseudoLocalizer.Locale).TryGet(LocalizationKeys.TitleNewGame, out text));
            Assert.AreEqual("[Ñéw v2]", text, "the retry replaced it");
            Assert.AreEqual(1, fs.Files.Keys.Count(k => k.Contains(PseudoLocalizer.Locale + "-")), "the old file was deleted");
        }

        [Test]
        public void AFullDeviceKeepsTheOldPack()
        {
            var memory = new MemorySaveFileSystem();
            var (manifest, bytes) = Pack(Pseudo());
            var installer = new PackInstaller(memory, Dir, English);
            installer.Load();
            Assert.IsTrue(installer.Install(manifest, bytes).Ok);

            var v2 = Edited(Pseudo(), PseudoLocalizer.Locale,
                e => e.Key == LocalizationKeys.TitleNewGame ? WithValue(e, "[Ñéw v2]") : e, PseudoLocalizer.NativeName);
            var (manifest2, bytes2) = Pack(v2);
            var full = new PackInstaller(new FullDiskFileSystem(memory), Dir, English);
            full.Load();
            var result = full.Install(manifest2, bytes2);
            Assert.AreEqual(PackError.InsufficientStorage, result.Error);
            Assert.IsFalse(memory.Exists(full.StagingPath(manifest2.Locale)), "the partial staging file was removed");
            var still = new PackInstaller(memory, Dir, English).Load();
            Assert.AreEqual(manifest.Sha256, still.Single().Manifest.Sha256);

            var tight = new PackInstaller(memory, Dir, English, freeBytes: () => manifest2.Size);
            tight.Load();
            Assert.AreEqual(PackError.InsufficientStorage, tight.Install(manifest2, bytes2).Error, "checked before writing");
        }

        [Test]
        public void AnInstallInterruptedAtAnyStepLeavesTheOldOrTheNewPack()
        {
            var v1 = Pseudo();
            var v2 = Edited(v1, PseudoLocalizer.Locale,
                e => e.Key == LocalizationKeys.TitleNewGame ? WithValue(e, "[Ñéw v2]") : e, PseudoLocalizer.NativeName);
            var (m1, b1) = Pack(v1);
            var (m2, b2) = Pack(v2);
            var baseline = new MemorySaveFileSystem();
            var setup = new PackInstaller(baseline, Dir, English);
            setup.Load();
            Assert.IsTrue(setup.Install(m1, b1).Ok);

            bool sawOld = false;
            bool sawNew = false;
            for (int crashAt = 0; crashAt < 40; crashAt++)
            {
                var fs = baseline.Clone();
                fs.CrashAfter = crashAt;
                var installer = new PackInstaller(fs, Dir, English);
                installer.Load();
                bool crashed = false;
                try
                {
                    installer.Install(m2, b2);
                }
                catch (SimulatedCrash)
                {
                    crashed = true;
                }

                fs.CrashAfter = -1;
                var after = new PackInstaller(fs, Dir, English).Load();
                Assert.AreEqual(1, after.Count, $"crash at {crashAt}: exactly one pseudo pack loads");
                string sha = after[0].Manifest.Sha256;
                Assert.IsTrue(sha == m1.Sha256 || sha == m2.Sha256, $"crash at {crashAt}");
                sawOld |= sha == m1.Sha256;
                sawNew |= sha == m2.Sha256;
                if (!crashed)
                {
                    Assert.AreEqual(m2.Sha256, sha);
                    break;
                }
            }

            Assert.IsTrue(sawOld && sawNew, "the sweep covered crashes before and after the switch");
        }

        [Test]
        public void RemovingTheActivePackRevertsToEnglish()
        {
            var fs = new MemorySaveFileSystem();
            var host = new FakePackHost();
            host.PublishCatalog(host.Publish(Pseudo(), development: true));
            var packs = Packs(fs, host);
            packs.FetchCatalog();
            packs.Download(PseudoLocalizer.Locale);
            var catalog = new LocalizationCatalog(English);
            catalog.SetActive(packs.TableFor(PseudoLocalizer.Locale));
            StringAssert.StartsWith("[", catalog.Get(LocalizationKeys.TitleNewGame));

            Assert.IsTrue(packs.Remove(PseudoLocalizer.Locale).Ok);
            Assert.IsNull(packs.TableFor(PseudoLocalizer.Locale));
            Assert.IsFalse(fs.Files.Keys.Any(k => k.Contains(PseudoLocalizer.Locale + "-")), "the file is gone");
            // The caller (LanguageController) switches back; at the next launch the saved choice is no longer usable.
            Assert.AreEqual("en", LanguageChoice.Startup(PseudoLocalizer.Locale, true, new[] { "en" },
                Packs(fs, host).UsableLocales));
            Assert.AreEqual(PackError.UnknownPack, packs.Remove(PseudoLocalizer.Locale).Error);
            Assert.AreEqual(PackError.BuiltIn, packs.Remove("en").Error);
        }

        [Test]
        public void ProgressIsTheSameBeforeAndAfterASwitch()
        {
            var fs = new MemorySaveFileSystem();
            var caseStore = new CaseSaveStore(fs, Dir);
            caseStore.Load();
            Assert.IsTrue(caseStore.Save(CaseScenarioTests.PlayCanonical().State, "c01.anchor.workshop"));
            var prefs = new PreferencesStore(fs, Dir);
            prefs.Save(new PreferencesData { MusicVolume = 0.5f });
            var before = NonLanguageFiles(fs);

            var host = new FakePackHost();
            host.PublishCatalog(host.Publish(Pseudo(), development: true));
            var packs = Packs(fs, host);
            packs.FetchCatalog();
            packs.Download(PseudoLocalizer.Locale);
            packs.Remove(PseudoLocalizer.Locale);
            packs.FetchCatalog();
            packs.Download(PseudoLocalizer.Locale);
            CollectionAssert.AreEquivalent(before, NonLanguageFiles(fs), "installing and removing packs never touches saves");
            var loaded = new CaseSaveStore(fs, Dir).Load();
            Assert.AreEqual(CaseScenarioTests.PlayCanonical().State.Facts.Count, loaded.State.Facts.Count);

            // Switching language changes only the preferences' locale, never the case save.
            var p = prefs.Load();
            p.Locale = PseudoLocalizer.Locale;
            p.LocaleChosen = true;
            prefs.Save(p);
            Assert.AreEqual(before.Where(f => f.Key.Contains("case-")).ToList(),
                NonLanguageFiles(fs).Where(f => f.Key.Contains("case-")).ToList());
            var back = prefs.Load();
            Assert.AreEqual(PseudoLocalizer.Locale, back.Locale);
            Assert.IsTrue(back.LocaleChosen);
            Assert.AreEqual(0.5f, back.MusicVolume);
        }

        // ------------------------------------------------------------------------------------------- catalog & host

        [Test]
        public void NothingIsFetchedUntilAskedAndOfflineIsReported()
        {
            var fs = new MemorySaveFileSystem();
            var host = new FakePackHost { Offline = true };
            var packs = Packs(fs, host);
            Assert.IsEmpty(host.Requests);
            Assert.AreEqual(1, packs.Options.Count, "English only");
            PackError error = PackError.None;
            packs.FetchCatalog(e => error = e);
            Assert.AreEqual(PackError.Offline, error);
            Assert.AreEqual(PackError.Offline, packs.CatalogError);
            CollectionAssert.AreEqual(new[] { FakePackHost.CatalogUrl }, host.Requests);
            Assert.AreEqual(PackError.UnknownPack, Captured(packs, PseudoLocalizer.Locale).Error);
        }

        private static PackResult Captured(LanguagePacks packs, string locale)
        {
            PackResult result = null;
            packs.Download(locale, r => result = r);
            return result;
        }

        [Test]
        public void DevelopmentPacksAreHiddenInProductionBuilds()
        {
            var fs = new MemorySaveFileSystem();
            var host = new FakePackHost();
            var portuguese = Edited(English, "pt-BR", e => e.IsPlural
                ? new StringEntry(e.Key, null, e.Context, new Dictionary<PluralCategory, string>
                {
                    [PluralCategory.One] = "um", [PluralCategory.Other] = e.Value,
                })
                : e, "Português (Brasil)");
            host.PublishCatalog(host.Publish(portuguese), host.Publish(Pseudo(), development: true));
            var production = Packs(fs, host, development: false);
            production.FetchCatalog();
            CollectionAssert.AreEqual(new[] { "en", "pt-BR" }, production.Options.Select(o => o.Locale).ToList());
            Assert.AreEqual(PackError.UnknownPack, Captured(production, PseudoLocalizer.Locale).Error);

            var development = Packs(fs, host);
            development.FetchCatalog();
            CollectionAssert.AreEqual(new[] { "en", "pt-BR", PseudoLocalizer.Locale },
                development.Options.Select(o => o.Locale).ToList());
            Assert.AreEqual(LanguageSource.Available, development.Option("pt-BR").Source);
            Assert.IsFalse(development.Option("pt-BR").Usable);
        }

        [Test]
        public void DownloadsReportProgressAndRunOneAtATime()
        {
            var fs = new MemorySaveFileSystem();
            var host = new FakePackHost();
            host.PublishCatalog(host.Publish(Pseudo(), development: true));
            var packs = Packs(fs, host);
            packs.FetchCatalog();
            host.Hold = true;
            PackResult result = null;
            packs.Download(PseudoLocalizer.Locale, r => result = r);
            Assert.IsTrue(packs.Busy);
            Assert.IsTrue(packs.Option(PseudoLocalizer.Locale).Downloading);
            Assert.AreEqual(0f, packs.Option(PseudoLocalizer.Locale).Progress);
            Assert.AreEqual(PackError.Busy, Captured(packs, PseudoLocalizer.Locale).Error);
            int changes = 0;
            packs.Changed += () => changes++;
            host.Release();
            Assert.IsTrue(result.Ok, result.ToString());
            Assert.GreaterOrEqual(changes, 3, "progress 0.5, 1 and done");
            Assert.AreEqual(LanguageSource.Installed, packs.Option(PseudoLocalizer.Locale).Source);
            Assert.IsFalse(packs.Option(PseudoLocalizer.Locale).Downloading);
            Assert.IsTrue(packs.Option(PseudoLocalizer.Locale).Removable);
        }

        [Test]
        public void OnlyHttpsOnTheCatalogsOwnHost()
        {
            Assert.IsTrue(PackUrls.IsAllowed("https://example.test/c.json"));
            Assert.IsFalse(PackUrls.IsAllowed("http://example.test/c.json"));
            Assert.IsFalse(PackUrls.IsAllowed("file:///etc/passwd"));
            Assert.IsFalse(PackUrls.IsAllowed("http://127.0.0.1:8080/c.json"));
            Assert.IsTrue(PackUrls.IsAllowed("http://127.0.0.1:8080/c.json", allowLoopbackHttp: true));
            Assert.IsFalse(PackUrls.IsAllowed("http://example.test/c.json", allowLoopbackHttp: true));
            Assert.AreEqual("https://example.test/packs/qps-ploc.json",
                PackUrls.Resolve("https://example.test/packs/catalog.json", "qps-ploc.json"));
            Assert.IsNull(PackUrls.Resolve("https://example.test/packs/catalog.json", "https://evil.test/x.json"));
            Assert.IsNull(PackUrls.Resolve("https://example.test/packs/catalog.json", "http://example.test/x.json"));
            Assert.IsNull(PackUrls.Resolve("http://example.test/catalog.json", "x.json"));

            var packs = new LanguagePacks(English, new PackInstaller(new MemorySaveFileSystem(), Dir, English),
                new FakePackHost(), "http://example.test/catalog.json", null, true);
            Assert.IsFalse(packs.DownloadsConfigured);
            PackError error = PackError.None;
            packs.FetchCatalog(e => error = e);
            Assert.AreEqual(PackError.NotHttps, error);
        }

        [Test]
        public void TheCatalogSkipsBadEntriesAndLocalesAreSafeFileNames()
        {
            var good = PackManifest.For(Pseudo(), "qps-ploc.json", true);
            var goodJson = new System.Text.StringBuilder();
            good.WriteJson(goodJson, "");
            string json = "{\"catalogVersion\":1,\"packs\":[{\"locale\":\"../../evil\",\"size\":5,\"sha256\":\"" +
                          new string('a', 64) + "\",\"file\":\"x\"},{\"locale\":\"fr\",\"size\":5,\"sha256\":\"short\"," +
                          "\"file\":\"x\"}," + goodJson + "]}";
            Assert.AreEqual(PackError.None, PackCatalog.TryParse(json, out var catalog), json);
            Assert.AreEqual(1, catalog.Packs.Count);
            Assert.AreEqual(good.Sha256, catalog.Find("QPS-PLOC").Sha256);
            Assert.IsTrue(catalog.Find("qps-ploc").Development);
            Assert.AreEqual(PackError.Unreadable, PackCatalog.TryParse("nope", out _));
            Assert.AreEqual(PackError.Unreadable, PackCatalog.TryParse("{\"packs\":[]}", out _));
            Assert.IsFalse(PackManifest.IsLocaleCode("../x"));
            Assert.IsFalse(PackManifest.IsLocaleCode("-x"));
            Assert.IsTrue(PackManifest.IsLocaleCode("pt-BR"));
        }

        // ------------------------------------------------------------------------------------------- built-in & launch

        [Test]
        public void ABuiltInSlotIsOfferedOnlyWhenItsTableShips()
        {
            var fs = new MemorySaveFileSystem();
            var host = new FakePackHost();
            Assert.IsNull(Packs(fs, host).Option("pt-BR"), "no pt-BR table in the build yet (D2 adds it)");

            var portuguese = Edited(English, "pt-BR", e => e, "Português (Brasil)");
            var packs = Packs(fs, host, builtInPt: () => portuguese.ToJson());
            var option = packs.Option("pt-BR");
            Assert.AreEqual(LanguageSource.BuiltIn, option.Source);
            Assert.IsTrue(option.Usable);
            Assert.IsFalse(option.Removable);
            Assert.AreEqual("pt-BR", packs.TableFor("pt-BR").Locale);
            Assert.AreEqual(PackError.BuiltIn, packs.Remove("pt-BR").Error);
            Assert.IsNull(Packs(fs, host, builtInPt: () => "{ broken").Option("pt-BR"), "a broken table is not offered");
        }

        [TestCase(null, false, new[] { "pt-BR" }, "pt-BR")]
        [TestCase(null, false, new[] { "pt-PT" }, "pt-BR")]
        [TestCase(null, false, new[] { "de", "pt_BR" }, "pt-BR")]
        [TestCase(null, false, new[] { "ja" }, "en")]
        [TestCase("en", false, new[] { "pt-BR" }, "pt-BR")]
        [TestCase("en", true, new[] { "pt-BR" }, "en")]
        [TestCase("pt-BR", true, new[] { "en" }, "pt-BR")]
        [TestCase("fr", true, new[] { "pt-BR" }, "en")]
        [TestCase(null, false, new[] { "qps-ploc" }, "en")]
        [TestCase("qps-ploc", true, new[] { "en" }, "qps-ploc")]
        public void TheLaunchLanguageFollowsTheChoiceThenTheDevice(string saved, bool chosen, string[] device, string expected)
        {
            Assert.AreEqual(expected, LanguageChoice.Startup(saved, chosen, device, new[] { "en", "pt-BR", "qps-ploc" }));
        }

        [Test]
        public void TheShippedConfigIsHttpsWithAPortugueseSlot()
        {
            string path = Path.Combine(Path.GetDirectoryName(LocalizationTests.BundledEnglishPath()), "languages.json");
            var config = LanguageConfig.Parse(File.ReadAllText(path));
            Assert.IsTrue(PackUrls.IsAllowed(config.CatalogUrl), config.CatalogUrl);
            Assert.AreEqual("pt-BR", config.BuiltIn.Single().Locale);
            Assert.AreEqual("Localization/pt-BR", config.BuiltIn.Single().Resource);
            Assert.IsNull(PackValidator.MissingGlyph(config.BuiltIn.Single().NativeName, FontCoverage.Covers));
        }

        [Test]
        public void PreferencesRememberThatTheLanguageWasChosen()
        {
            var p = new PreferencesData { Locale = "pt-BR", LocaleChosen = true };
            Assert.IsTrue(PreferencesData.TryRead(PreferencesData.Serialize(p), out var back, out _));
            Assert.IsTrue(back.LocaleChosen);
            Assert.IsTrue(PreferencesData.TryRead("{\"schemaVersion\":1,\"locale\":\"en\"}", out var old, out _));
            Assert.IsFalse(old.LocaleChosen, "older files: the device decides until the player picks");
        }
    }
}
