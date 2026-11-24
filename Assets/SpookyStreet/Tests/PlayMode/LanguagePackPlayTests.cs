using System;
using System.Collections;
using System.Collections.Generic;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Localization;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Localization;
using SpookyStreet.UI;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>A pack host in memory (no network): serves the catalog and the pseudo pack, can go offline or hold.</summary>
    public sealed class LocalPackHost : IPackTransport
    {
        public const string CatalogUrl = "https://packs.example.test/spooky/catalog.json";

        private Action _pending;

        public Dictionary<string, byte[]> Files { get; } = new Dictionary<string, byte[]>();

        public List<string> Requests { get; } = new List<string>();

        public bool Offline { get; set; }

        /// <summary>Answer pack downloads only on <see cref="Release"/> (after reporting 40 %).</summary>
        public bool HoldPacks { get; set; }

        public static LocalPackHost WithPseudoPack()
        {
            var host = new LocalPackHost();
            var pseudo = PseudoLocalizer.CreateTable(Localizer.Catalog.Fallback);
            host.Files["https://packs.example.test/spooky/qps-ploc.json"] = PackHash.Utf8.GetBytes(pseudo.ToJson());
            var manifest = PackManifest.For(pseudo, "qps-ploc.json", development: true);
            host.Files[CatalogUrl] = PackHash.Utf8.GetBytes(new PackCatalog(new[] { manifest }).ToJson());
            return host;
        }

        public void Get(string url, long maxBytes, Action<float> progress, Action<PackFetch> done)
        {
            Requests.Add(url);
            if (Offline || !Files.TryGetValue(url, out byte[] bytes))
            {
                done(PackFetch.Failure(PackError.Offline, "offline"));
                return;
            }

            if (HoldPacks && url != CatalogUrl)
            {
                progress?.Invoke(0.4f);
                _pending = () => done(PackFetch.Success(bytes));
                return;
            }

            progress?.Invoke(1f);
            done(PackFetch.Success(bytes));
        }

        public void Release()
        {
            var pending = _pending;
            _pending = null;
            pending?.Invoke();
        }
    }

    /// <summary>
    /// Language packs in the game (S004 D1): the title's globe opens the language screen; More languages asks for the
    /// parent gate, then lists the host's packs; the pseudo pack downloads, switches every visible string and back,
    /// is chosen again after a relaunch offline, and its removal (gated) returns the game to English.
    /// </summary>
    public class LanguagePackPlayTests
    {
        private LocalPackHost _host;

        [SetUp]
        public void SetUp()
        {
            Localizer.ResetForTests();
            CaseDirector.UseTemporarySaveDirectory();
            LanguageController.ResetOverridesForTests();
            LanguageController.DeviceLocalesOverride = new[] { "en" };
            _host = LocalPackHost.WithPseudoPack();
            LanguageController.TransportOverride = _host;
            LanguageController.CatalogUrlOverride = LocalPackHost.CatalogUrl;
        }

        [TearDown]
        public void TearDown()
        {
            LanguageController.ResetOverridesForTests();
            Localizer.ResetForTests();
        }

        private static IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            yield return null;
            yield return null;
            Assert.IsNotNull(GameBootstrap.Instance, "scene did not create GameBootstrap");
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until)
            {
                yield return null;
            }
        }

        /// <summary>A grown-up holds both felt spots for 3 s.</summary>
        public static IEnumerator PassGate()
        {
            var menus = GameBootstrap.Instance.Director.Nonverbal.Menus;
            Assert.IsTrue(menus.GateOpen, "the parent gate is up");
            var gate = menus.Gate;
            gate.TouchDown(1, gate.ZoneA.CenterX, gate.ZoneA.CenterY);
            gate.TouchDown(2, gate.ZoneB.CenterX, gate.ZoneB.CenterY);
            yield return WaitUnscaled(ParentGate.DefaultHoldSeconds + 0.4f);
            Assert.IsFalse(menus.GateOpen, "the gate passed and closed");
        }

        private static string Label(Button button) => button.GetComponent<CraftButton>().Label.text;

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator SwitchingToThePseudoPackChangesOnScreenStringsAndBack()
        {
            yield return LoadGame();
            var game = GameBootstrap.Instance;
            var screen = game.LanguageScreen;
            Assert.AreEqual("en", Localizer.ActiveLocale);
            Assert.AreEqual("Three Knocks on Lantern Lane", game.Ui.CaseName.text);
            Assert.IsEmpty(_host.Requests, "launch fetches nothing");

            game.Ui.LanguageButton.onClick.Invoke();
            yield return null;
            Assert.IsTrue(screen.IsOpen);
            Assert.AreEqual(2, screen.Rows.Count, "English and the built-in pt-BR before a grown-up asks for more");
            Assert.IsNotNull(screen.RowFor("en").InUseTag, "English is in use");
            Assert.AreEqual("English", screen.RowFor("en").Name.text);
            var portugueseRow = screen.RowFor("pt-BR");
            Assert.IsNotNull(portugueseRow, "pt-BR ships in the app (S004 D2)");
            Assert.AreEqual("Português (Brasil)", portugueseRow.Name.text);
            Assert.IsNotNull(portugueseRow.Use, "a built-in language is used with no download");
            Assert.IsNull(portugueseRow.Remove, "a built-in language cannot be removed");
            yield return null;
            Assert.AreEqual(LanguageScreenFocus.More, screen.Navigation.Focus,
                "gamepad and keyboard (F1): English is in use, so focus starts on More languages (as in D1)");

            screen.MoreButton.onClick.Invoke();
            yield return null;
            Assert.IsEmpty(_host.Requests, "nothing is fetched before the gate passes");
            yield return PassGate();
            yield return null;
            CollectionAssert.AreEqual(new[] { LocalPackHost.CatalogUrl }, _host.Requests);
            var pseudoRow = screen.RowFor(PseudoLocalizer.Locale);
            Assert.IsNotNull(pseudoRow, "the development pseudo pack is listed in a development build");
            Assert.IsNotNull(pseudoRow.Download);
            Assert.IsNull(pseudoRow.Use, "not usable before it is downloaded");

            pseudoRow.Download.onClick.Invoke();
            yield return null;
            pseudoRow = screen.RowFor(PseudoLocalizer.Locale);
            Assert.IsNotNull(pseudoRow.Use, "downloaded and validated: it can be used");
            Assert.IsNotNull(pseudoRow.Remove);
            Assert.AreEqual("en", Localizer.ActiveLocale, "downloading does not switch");

            // Use by keyboard or gamepad: focus the pseudo row's main slot and press Submit (the same path as a tap).
            screen.Navigation.FocusOn(LanguageScreenFocus.Main(2));
            Assert.AreEqual(LanguageScreenFocus.Main(2), screen.Navigation.Focus, "row 2: after English and pt-BR");
            screen.Navigation.Submit();
            yield return null;
            Assert.AreEqual(PseudoLocalizer.Locale, Localizer.ActiveLocale);
            StringAssert.StartsWith("[", game.Ui.CaseName.text, "the title's case name is in the pseudo-language");
            StringAssert.StartsWith("[", Label(game.Ui.NewGameButton));
            StringAssert.StartsWith("[", Label(screen.MoreButton), "the language screen itself switched");
            StringAssert.Contains(PseudoLocalizer.NativeName, game.Ui.LanguageButton.GetComponent<CraftButton>().Label.text);
            Assert.AreEqual(PseudoLocalizer.Locale, game.Audio.Preferences.Locale, "the choice is saved in preferences");
            Assert.IsTrue(game.Audio.Preferences.LocaleChosen);

            screen.RowFor("en").Use.onClick.Invoke();
            yield return null;
            Assert.AreEqual("en", Localizer.ActiveLocale);
            Assert.AreEqual("Three Knocks on Lantern Lane", game.Ui.CaseName.text, "and back");
            Assert.AreEqual("New game", Label(game.Ui.NewGameButton));

            // Removing the language in use asks for the gate, then returns the game to English.
            screen.RowFor(PseudoLocalizer.Locale).Use.onClick.Invoke();
            yield return null;
            screen.RowFor(PseudoLocalizer.Locale).Remove.onClick.Invoke();
            yield return null;
            Assert.AreEqual(PseudoLocalizer.Locale, Localizer.ActiveLocale, "nothing happens before the gate passes");
            yield return PassGate();
            yield return null;
            Assert.AreEqual("en", Localizer.ActiveLocale);
            Assert.AreEqual("Three Knocks on Lantern Lane", game.Ui.CaseName.text);
            var row = screen.RowFor(PseudoLocalizer.Locale);
            Assert.IsTrue(row == null || row.Download != null, "the pseudo pack is no longer on the device");
            Assert.IsNull(game.Languages.Packs.TableFor(PseudoLocalizer.Locale));

            screen.Navigation.Back();
            yield return null;
            Assert.IsFalse(screen.IsOpen, "Back (Escape, East) closes the screen like the close disc");
        }

        /// <summary>
        /// S004 D2: pt-BR ships in the app. A Brazilian device starts in it with no request; the title, the language
        /// pill and the screen read Portuguese; choosing English is kept over the device language at the next launch.
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator TheBuiltInPortugueseStartsOnABrazilianDeviceAndNeedsNoDownload()
        {
            LanguageController.DeviceLocalesOverride = new[] { "pt-BR" };
            yield return LoadGame();
            var game = GameBootstrap.Instance;
            Assert.AreEqual("pt-BR", Localizer.ActiveLocale, "the device language picks the built-in table");
            Assert.AreEqual("Três batidas na Rua das Lanternas", game.Ui.CaseName.text);
            Assert.AreEqual("Novo jogo", Label(game.Ui.NewGameButton));
            StringAssert.Contains("Português (Brasil)", game.Ui.LanguageButton.GetComponent<CraftButton>().Label.text);
            Assert.IsEmpty(_host.Requests, "a built-in language needs no request");

            var screen = game.LanguageScreen;
            game.Ui.LanguageButton.onClick.Invoke();
            yield return null;
            Assert.IsNotNull(screen.RowFor("pt-BR").InUseTag, "pt-BR is in use");
            Assert.AreEqual("Mais idiomas", Label(screen.MoreButton));
            screen.RowFor("en").Use.onClick.Invoke();
            yield return null;
            Assert.AreEqual("en", Localizer.ActiveLocale);
            Assert.AreEqual("Three Knocks on Lantern Lane", game.Ui.CaseName.text);
            Assert.IsTrue(game.Audio.Preferences.LocaleChosen);

            Localizer.ResetForTests();
            yield return LoadGame();
            Assert.AreEqual("en", Localizer.ActiveLocale, "the player's choice wins over the device language");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator AChosenPackIsUsedAgainAfterARelaunchOffline()
        {
            yield return LoadGame();
            var languages = GameBootstrap.Instance.Languages;
            languages.Packs.FetchCatalog();
            PackResult result = null;
            languages.Download(PseudoLocalizer.Locale, r => result = r);
            Assert.IsTrue(result != null && result.Ok, result?.ToString());
            Assert.IsTrue(languages.Use(PseudoLocalizer.Locale));
            yield return null;

            _host.Offline = true;
            int requests = _host.Requests.Count;
            Localizer.ResetForTests();
            yield return LoadGame();
            var game = GameBootstrap.Instance;
            Assert.AreEqual(PseudoLocalizer.Locale, Localizer.ActiveLocale, "the saved choice and the installed pack");
            StringAssert.StartsWith("[", game.Ui.CaseName.text);
            Assert.AreEqual(requests, _host.Requests.Count, "relaunch is offline: no request");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator TheDeviceLanguagePicksAnInstalledPackOnlyBeforeAChoice()
        {
            yield return LoadGame();
            var languages = GameBootstrap.Instance.Languages;
            languages.Packs.FetchCatalog();
            languages.Download(PseudoLocalizer.Locale);
            yield return null;

            // The pseudo-language is never picked from the device, and English stays until a choice is made.
            LanguageController.DeviceLocalesOverride = new[] { PseudoLocalizer.Locale };
            Localizer.ResetForTests();
            yield return LoadGame();
            Assert.AreEqual("en", Localizer.ActiveLocale);
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator TheRealDownloaderFetchesFromALocalHttpHost()
        {
            var files = new Dictionary<string, byte[]>();
            string problem = StartListener(out var listener, out string root);
            if (problem != null)
            {
                Assert.Ignore(problem);
            }

            var thread = new Thread(() => Serve(listener, files)) { IsBackground = true };
            thread.Start();
            try
            {
                LanguageController.TransportOverride = null; // the real UnityWebRequest downloader
                LanguageController.AllowLoopbackHttp = true;
                LanguageController.CatalogUrlOverride = root + "catalog.json";
                Localizer.ResetForTests();
                Localizer.Initialize();
                var pseudo = PseudoLocalizer.CreateTable(Localizer.Catalog.Fallback);
                lock (files)
                {
                    files["/packs/qps-ploc.json"] = PackHash.Utf8.GetBytes(pseudo.ToJson());
                    files["/packs/catalog.json"] = PackHash.Utf8.GetBytes(new PackCatalog(new[]
                    {
                        PackManifest.For(pseudo, "qps-ploc.json", development: true),
                    }).ToJson());
                }

                yield return LoadGame();
                var languages = GameBootstrap.Instance.Languages;
                PackError catalogError = PackError.Busy;
                languages.Packs.FetchCatalog(e => catalogError = e);
                float until = Time.unscaledTime + 20f;
                while (catalogError == PackError.Busy && Time.unscaledTime < until)
                {
                    yield return null;
                }

                if (catalogError == PackError.NotHttps)
                {
                    Assert.Ignore("this editor refuses plain HTTP even on loopback (player setting)");
                }

                Assert.AreEqual(PackError.None, catalogError, "catalog over UnityWebRequest");
                PackResult result = null;
                languages.Download(PseudoLocalizer.Locale, r => result = r);
                until = Time.unscaledTime + 20f;
                while (result == null && Time.unscaledTime < until)
                {
                    yield return null;
                }

                Assert.IsNotNull(result, "download finished");
                Assert.IsTrue(result.Ok, result.ToString());
                Assert.IsTrue(languages.Use(PseudoLocalizer.Locale));
                StringAssert.StartsWith("[", GameBootstrap.Instance.Ui.CaseName.text);

                // A truncated file (a cut connection) is refused and the installed copy stays.
                lock (files)
                {
                    byte[] full = files["/packs/qps-ploc.json"];
                    var changed = PseudoLocalizer.CreateTable(Localizer.Catalog.Fallback, 0.5f);
                    byte[] next = PackHash.Utf8.GetBytes(changed.ToJson());
                    files["/packs/catalog.json"] = PackHash.Utf8.GetBytes(new PackCatalog(new[]
                    {
                        PackManifest.For(changed, "qps-ploc-2.json", development: true),
                    }).ToJson());
                    var cut = new byte[next.Length / 2];
                    Array.Copy(next, cut, cut.Length);
                    files["/packs/qps-ploc-2.json"] = cut;
                    Assert.Greater(full.Length, 0);
                }

                catalogError = PackError.Busy;
                languages.Packs.FetchCatalog(e => catalogError = e);
                until = Time.unscaledTime + 20f;
                while (catalogError == PackError.Busy && Time.unscaledTime < until)
                {
                    yield return null;
                }

                result = null;
                languages.Download(PseudoLocalizer.Locale, r => result = r);
                until = Time.unscaledTime + 20f;
                while (result == null && Time.unscaledTime < until)
                {
                    yield return null;
                }

                Assert.IsNotNull(result);
                Assert.AreEqual(PackError.Interrupted, result.Error, result.ToString());
                Assert.AreEqual(PseudoLocalizer.Locale, Localizer.ActiveLocale, "the old pack stays on");
                StringAssert.StartsWith("[", GameBootstrap.Instance.Ui.CaseName.text);
            }
            finally
            {
                listener.Stop();
                listener.Close();
            }
        }

        /// <summary>A loopback HttpListener on a free port, or why there is none (then the test is ignored).</summary>
        private static string StartListener(out HttpListener listener, out string root)
        {
            listener = null;
            root = null;
            try
            {
                var probe = new TcpListener(IPAddress.Loopback, 0);
                probe.Start();
                int port = ((IPEndPoint)probe.LocalEndpoint).Port;
                probe.Stop();
                root = "http://127.0.0.1:" + port + "/packs/";
                listener = new HttpListener();
                listener.Prefixes.Add(root);
                listener.Start();
                return null;
            }
            catch (Exception e)
            {
                return "no loopback HTTP listener here: " + e.Message;
            }
        }

        private static void Serve(HttpListener listener, Dictionary<string, byte[]> files)
        {
            while (listener.IsListening)
            {
                HttpListenerContext context;
                try
                {
                    context = listener.GetContext();
                }
                catch (Exception)
                {
                    return;
                }

                byte[] body;
                lock (files)
                {
                    files.TryGetValue(context.Request.Url.AbsolutePath, out body);
                }

                try
                {
                    context.Response.StatusCode = body != null ? 200 : 404;
                    if (body != null)
                    {
                        context.Response.ContentLength64 = body.Length;
                        context.Response.OutputStream.Write(body, 0, body.Length);
                    }

                    context.Response.Close();
                }
                catch (Exception)
                {
                    // The client went away.
                }
            }
        }
    }
}
