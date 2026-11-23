using System;
using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Localization;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using N = SpookyStreet.Core.Case.CaseNotebook;

namespace SpookyStreet.Tests.Visual
{
    /// <summary>
    /// S004 D1 language packs (frames 73–74, reserved for stream D): 73 the language screen with the pseudo pack
    /// downloading after the parent gate; 74 the notebook's Q-entry page in the pseudo-language (+35 % text). Review
    /// frames: the language screen once the pack is in use, and the title in the pseudo-language.
    /// </summary>
    [Category("Visual")]
    public class LanguageCaptureTests : InputTestFixture
    {
        private const int Width = 1278;
        private const int Height = 590;
        private const float MaxMagenta = 0.005f;

        /// <summary>An in-memory pack host: the catalog answers at once, the pack holds at 40 % until released.</summary>
        private sealed class HeldHost : IPackTransport
        {
            public const string CatalogUrl = "https://packs.example.test/spooky/catalog.json";
            public readonly Dictionary<string, byte[]> Files = new Dictionary<string, byte[]>();
            private Action _pending;

            public void Get(string url, long maxBytes, Action<float> progress, Action<PackFetch> done)
            {
                if (!Files.TryGetValue(url, out byte[] bytes))
                {
                    done(PackFetch.Failure(PackError.Offline, "404"));
                    return;
                }

                if (url == CatalogUrl)
                {
                    done(PackFetch.Success(bytes));
                    return;
                }

                progress?.Invoke(0.4f);
                _pending = () => done(PackFetch.Success(bytes));
            }

            public void Release()
            {
                var pending = _pending;
                _pending = null;
                pending?.Invoke();
            }
        }

        private HeldHost _host;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            CaseDirector.UseTemporarySaveDirectory();
            SpookyStreet.Controls.ControlSchemeTracker.Override = null;
            Localizer.ResetForTests();
            Localizer.Initialize();
            LanguageController.ResetOverridesForTests();
            LanguageController.DeviceLocalesOverride = new[] { "en" };
            _host = new HeldHost();
            var pseudo = PseudoLocalizer.CreateTable(Localizer.Catalog.Fallback);
            _host.Files["https://packs.example.test/spooky/qps-ploc.json"] = PackHash.Utf8.GetBytes(pseudo.ToJson());
            _host.Files[HeldHost.CatalogUrl] = PackHash.Utf8.GetBytes(new PackCatalog(new[]
            {
                PackManifest.For(pseudo, "qps-ploc.json", development: true),
            }).ToJson());
            LanguageController.TransportOverride = _host;
            LanguageController.CatalogUrlOverride = HeldHost.CatalogUrl;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("No graphics device (-nographics); visual captures need rendering.");
            }
        }

        public override void TearDown()
        {
            LanguageController.ResetOverridesForTests();
            Localizer.ResetForTests();
            base.TearDown();
        }

        private static CaseDirector Director => GameBootstrap.Instance.Director;

        private static IEnumerator Shot(string name, Canvas canvas)
        {
            var game = GameBootstrap.Instance;
            yield return FrameCapture.Capture(game.CameraRig.OutputCamera, canvas, name, Width, Height);
            float magenta = FrameCapture.MagentaFraction(FrameCapture.LastFrame);
            Assert.LessOrEqual(magenta, MaxMagenta, $"{name}: {magenta:P1} of the frame is error-shader magenta");
            Assert.Greater(FrameCapture.MeanLuminance(FrameCapture.LastFrame), 0.02f, $"{name}: frame is black");
        }

        private static IEnumerator WaitUnscaled(float seconds)
        {
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until)
            {
                yield return null;
            }
        }

        private static IEnumerator Do(params CaseAction[] actions)
        {
            foreach (var action in actions)
            {
                Director.Perform(action);
                Director.Dialogue.Skip();
                Director.Cutaway.Skip();
                yield return null;
                yield return null;
            }

            Director.Captions.Clear();
            yield return WaitUnscaled(0.5f);
        }

        private static CaseAction A(CaseActionKind kind) => CaseAction.Of(kind);

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator CapturesTheLanguageScreenAndThePseudoLanguage()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            var game = GameBootstrap.Instance;
            var screen = game.LanguageScreen;
            game.Ui.LanguageButton.onClick.Invoke();
            yield return null;
            screen.MoreButton.onClick.Invoke();
            yield return null;
            var gate = Director.Nonverbal.Menus.Gate;
            gate.TouchDown(1, gate.ZoneA.CenterX, gate.ZoneA.CenterY);
            gate.TouchDown(2, gate.ZoneB.CenterX, gate.ZoneB.CenterY);
            yield return WaitUnscaled(ParentGate.DefaultHoldSeconds + 0.4f);
            Assert.IsTrue(screen.DownloadsUnlocked, "the gate passed");
            yield return null;

            // 73: the language screen, English in use, the pseudo pack downloading (40 %).
            screen.RowFor(PseudoLocalizer.Locale).Download.onClick.Invoke();
            yield return null;
            yield return null;
            Assert.IsNotNull(screen.RowFor(PseudoLocalizer.Locale).Progress, "a progress bar while downloading");
            yield return WaitUnscaled(0.3f);
            yield return Shot("73_language_screen", screen.Canvas);

            _host.Release();
            yield return null;
            screen.RowFor(PseudoLocalizer.Locale).Use.onClick.Invoke();
            yield return null;
            yield return null;
            Assert.AreEqual(PseudoLocalizer.Locale, Localizer.ActiveLocale);
            yield return Shot("73b_language_screen_pseudo", screen.Canvas);
            screen.Back();
            yield return null;
            yield return WaitUnscaled(0.3f);
            yield return Shot("74b_title_pseudo", game.Ui.GetComponent<Canvas>());

            // 74: the notebook's "How did June get in?" page in the pseudo-language, two clues found.
            game.Ui.NewGameButton.onClick.Invoke();
            yield return WaitUnscaled(1.0f);
            yield return Do(A(CaseActionKind.HearKnocks), CaseAction.Talk(Speaker.Bea), CaseAction.Talk(Speaker.Vale),
                A(CaseActionKind.SitOnBench), A(CaseActionKind.ExamineCloth));
            Director.OpenNotebook();
            yield return null;
            var notebook = Director.Notebook;
            notebook.ShowPage(notebook.PageQuestions.IndexOf(N.Question(N.QuestionEntry)));
            yield return WaitUnscaled(1.0f);
            Director.Captions.Clear();
            yield return Shot("74_notebook_pseudo", Director.UiCanvas);
        }
    }
}
