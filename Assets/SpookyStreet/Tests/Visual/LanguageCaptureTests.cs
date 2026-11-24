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
    /// S004 stream D language frames (73–74, reserved for stream D). D1: 73 the language screen with the pseudo pack
    /// downloading after the parent gate (English and the built-in pt-BR above it); 74p the notebook's Q-entry page in
    /// the pseudo-language (+35 % text); review frames 73b (screen, pack in use) and 74b (title, pseudo). D2: 74 the
    /// Q-entry page in Brazilian Portuguese; review frames 73c (screen in pt-BR), 74c (title), 74d (Bea's line with
    /// her portrait) and 74e (Mr. Vale's line).
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
            yield return Shot("74p_notebook_pseudo", Director.UiCanvas);
        }

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator CapturesTheGameInBrazilianPortuguese()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            // 73c: the language screen after choosing the built-in pt-BR (no gate, no download).
            var game = GameBootstrap.Instance;
            var screen = game.LanguageScreen;
            game.Ui.LanguageButton.onClick.Invoke();
            yield return null;
            Assert.IsNotNull(screen.RowFor("pt-BR"), "pt-BR ships in the app");
            screen.RowFor("pt-BR").Use.onClick.Invoke();
            yield return null;
            yield return null;
            Assert.AreEqual("pt-BR", Localizer.ActiveLocale);
            Assert.AreEqual("Três batidas na Rua das Lanternas", game.Ui.CaseName.text);
            yield return WaitUnscaled(0.3f);
            yield return Shot("73c_language_screen_ptbr", screen.Canvas);

            // 74c: the title in pt-BR.
            screen.Back();
            yield return null;
            yield return WaitUnscaled(0.3f);
            yield return Shot("74c_title_ptbr", game.Ui.GetComponent<Canvas>());

            // 74d / 74e: Bea's first line with her portrait, then Mr. Vale's, on the speech card (framed like 14_dialogue).
            game.Ui.NewGameButton.onClick.Invoke();
            yield return WaitUnscaled(1.0f);
            yield return Do(A(CaseActionKind.HearKnocks));
            var bea = CaseStage.Find(CaseStage.PointBea).Position;
            var bike = CaseStage.Find(CaseStage.PointBicycle).Position;
            var beaAt = new Vector3(bea.X, 0f, bea.Z);
            Vector3 away = beaAt - new Vector3(bike.X, 0f, bike.Z);
            away = (away.normalized + Vector3.right).normalized;
            game.Pip.Teleport(beaAt + away * 2.4f + Vector3.up * 0.1f);
            game.Pip.transform.rotation = Quaternion.LookRotation(-away, Vector3.up);
            game.CameraRig.Orbit.RequestRecenter();
            yield return WaitUnscaled(1.5f);
            Director.Perform(CaseAction.Talk(Speaker.Bea));
            yield return WaitUnscaled(1.0f);
            Assert.IsTrue(Director.Dialogue.IsOpen, "74d: Bea's line is on the card");
            Assert.IsTrue(Director.Dialogue.PortraitImage.enabled, "74d: her portrait shows");
            var camera = game.CameraRig.OutputCamera;
            var brain = camera.GetComponent("CinemachineBrain") as Behaviour;
            if (brain != null)
            {
                brain.enabled = false;
            }

            var pip = game.Pip.transform;
            camera.transform.position = pip.position + pip.right * 0.9f + Vector3.up * 1.3f - pip.forward * 1.4f;
            camera.transform.LookAt(beaAt + Vector3.up * 1.05f);
            yield return WaitUnscaled(1.5f);
            yield return Shot("74d_dialogue_bea_ptbr", Director.UiCanvas);
            Director.Dialogue.Skip();
            yield return null;
            Director.Perform(CaseAction.Talk(Speaker.Vale));
            yield return WaitUnscaled(2.5f);
            Assert.IsTrue(Director.Dialogue.IsOpen, "74e: Mr. Vale's line is on the card");
            var words = Director.Dialogue.BubbleLine;
            Assert.GreaterOrEqual(words.cachedTextGenerator.characterCountVisible, words.text.Length - 1,
                "74e: Mr. Vale's 61-character line wraps to two lines and is drawn to its end");
            yield return Shot("74e_dialogue_vale_ptbr", Director.UiCanvas);
            if (brain != null)
            {
                brain.enabled = true;
            }

            // 74: the notebook's "Como a June entrou?" page in pt-BR, two clues found.
            Director.Dialogue.Skip();
            yield return null;
            yield return Do(A(CaseActionKind.SitOnBench), A(CaseActionKind.ExamineCloth));
            Director.OpenNotebook();
            yield return null;
            var notebook = Director.Notebook;
            notebook.ShowPage(notebook.PageQuestions.IndexOf(N.Question(N.QuestionEntry)));
            yield return WaitUnscaled(1.0f);
            Director.Captions.Clear();
            yield return Shot("74_notebook_ptbr", Director.UiCanvas);
        }
    }
}
