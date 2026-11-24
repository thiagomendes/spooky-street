using System;
using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Keepsakes;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Localization;
using SpookyStreet.UI;
using SpookyStreet.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SpookyStreet.Tests.Visual
{
    /// <summary>
    /// S004 stream E frames (75–76, reserved for stream E): 75 the "For grown-ups" page mid-case (one question solved,
    /// two friends, two places, 12 bats, a week of play), 75b the same page scrolled to the keepsakes and the week,
    /// 75c the page in Brazilian Portuguese, 75d the answers, places and friends; 76 the settings card with the accessibility switches (keyboard focus on
    /// Reduce motion, switched on), 76b the card in pt-BR; review frames 76c the credits and 76d the credits in pt-BR.
    /// </summary>
    [Category("Visual")]
    public class ParentSettingsCaptureTests : InputTestFixture
    {
        private const int Width = 1278;
        private const int Height = 590;
        private const float MaxMagenta = 0.005f;

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
            AmbientMotion.ReducedMotion = false;
            if (SystemInfo.graphicsDeviceType == GraphicsDeviceType.Null)
            {
                Assert.Ignore("No graphics device (-nographics); visual captures need rendering.");
            }
        }

        public override void TearDown()
        {
            AmbientMotion.ReducedMotion = false;
            SpookyStreet.Controls.ControlSchemeTracker.Override = null;
            LanguageController.ResetOverridesForTests();
            Localizer.ResetForTests();
            base.TearDown();
        }

        private static GameBootstrap Game => GameBootstrap.Instance;

        private static CaseDirector Director => Game.Director;

        private static IEnumerator Shot(string name, Canvas canvas)
        {
            yield return FrameCapture.Capture(Game.CameraRig.OutputCamera, canvas, name, Width, Height);
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
            yield return WaitUnscaled(0.3f);
        }

        private static CaseAction A(CaseActionKind kind) => CaseAction.Of(kind);

        /// <summary>A mid-case game: witnesses heard, the cloth seen, inside the house, the knocks' question solved.</summary>
        private static IEnumerator PlayMidCase()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            Game.Ui.NewGameButton.onClick.Invoke();
            yield return WaitUnscaled(1.0f);
            yield return Do(A(CaseActionKind.HearKnocks), CaseAction.Talk(Speaker.Bea), CaseAction.Talk(Speaker.Vale),
                A(CaseActionKind.SitOnBench), A(CaseActionKind.ExamineCloth), A(CaseActionKind.EnterAttic),
                A(CaseActionKind.LiftLatch), A(CaseActionKind.EnterFoyer), A(CaseActionKind.ExamineGrille),
                CaseAction.Solve(CaseNotebook.QuestionSource, new[] { CaseNotebook.FloorHum, CaseNotebook.WoodenPiece }));
            Director.Dialogue.Skip();
            Director.Cutaway.Skip();
            if (Director.Cutscenes != null && Director.Cutscenes.Playing)
            {
                Director.Cutscenes.Player.SkipNow();
            }

            var store = Director.Keepsakes.Store;
            foreach (var spot in PaperBatLayout.All)
            {
                if (store.State.BatCount >= 12)
                {
                    break;
                }

                store.Find(spot.Id);
            }

            // A week of play on the page's own clock: four days, about 1 h 20 min in all.
            var corner = Game.GrownUps;
            var today = new DateTime(2026, 11, 24, 18, 0, 0);
            corner.Clock = () => today;
            var minutes = new[] { (5, 12), (3, 25), (1, 31), (0, 12) };
            foreach (var (daysAgo, mins) in minutes)
            {
                for (int t = 0; t < mins * 120; t++)
                {
                    corner.Playtime.Tick(0.5, true, today.AddDays(-daysAgo));
                }
            }

            Game.Flow.Pause();
            yield return null;
        }

        private static IEnumerator OpenCardThroughTheGate()
        {
            Director.Nonverbal.SettingsButton.onClick.Invoke();
            yield return null;
            var gate = Director.Nonverbal.Menus.Gate;
            gate.TouchDown(1, gate.ZoneA.CenterX, gate.ZoneA.CenterY);
            gate.TouchDown(2, gate.ZoneB.CenterX, gate.ZoneB.CenterY);
            yield return WaitUnscaled(ParentGate.DefaultHoldSeconds + 0.4f);
            Assert.IsTrue(Director.Nonverbal.Menus.SettingsOpen, "the gate passed");
            yield return null;
        }

        private static IEnumerator Captures(string suffix, bool english)
        {
            var menus = Director.Nonverbal.Menus;
            var corner = Game.GrownUps;

            // 75: the page mid-case, then (75b) scrolled down to the keepsakes and the week.
            menus.GrownUpsEntry.onClick.Invoke();
            yield return WaitUnscaled(0.4f);
            var page = corner.ParentPage;
            Assert.IsTrue(page.IsOpen);
            Assert.AreEqual(1, page.Shown.Solved.Count, "one question solved: its comic");
            Assert.AreEqual(12, page.Shown.BatsFound);
            yield return Shot(english ? "75_parent_page" : "75c_parent_page" + suffix, page.Sheet.Canvas);
            if (english)
            {
                page.Sheet.ScrollBy(page.Sheet.ScrollRange * 0.3f);
                yield return WaitUnscaled(0.2f);
                yield return Shot("75d_parent_page_answers", page.Sheet.Canvas);
                page.Sheet.ScrollBy(page.Sheet.ScrollRange * 0.32f);
                yield return WaitUnscaled(0.2f);
                yield return Shot("75b_parent_page_lower", page.Sheet.Canvas);
            }

            page.Close();
            yield return null;

            // 76: the settings card, Reduce motion switched on and focused by keyboard.
            SpookyStreet.Controls.ControlSchemeTracker.Override = ControlScheme.KeyboardMouse;
            menus.Switch(SpookyStreet.Core.Parent.SettingsCard.ReducedMotion).onClick.Invoke();
            menus.SettingsNavigation.FocusOn(SpookyStreet.Core.Parent.SettingsCard.ReducedMotion);
            yield return WaitUnscaled(0.3f);
            yield return Shot(english ? "76_settings_card" : "76b_settings_card" + suffix, menus.Canvas);
            menus.Switch(SpookyStreet.Core.Parent.SettingsCard.ReducedMotion).onClick.Invoke();
            SpookyStreet.Controls.ControlSchemeTracker.Override = null;

            // 76c: the credits with the start of the licences.
            menus.CreditsEntry.onClick.Invoke();
            yield return WaitUnscaled(0.3f);
            Assert.AreEqual(2, corner.Credits.LicencesShown);
            yield return WaitUnscaled(0.2f);
            yield return Shot(english ? "76c_credits" : "76d_credits" + suffix, corner.Credits.Sheet.Canvas);
            corner.Credits.Close();
            yield return null;
        }

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator CapturesTheGrownUpsCornerInEnglishAndPortuguese()
        {
            yield return PlayMidCase();
            yield return OpenCardThroughTheGate();
            yield return Captures("", true);

            // The same screens in Brazilian Portuguese (built in, no download).
            Director.Nonverbal.Menus.LanguageEntry.onClick.Invoke();
            yield return null;
            var screen = Game.LanguageScreen;
            Assert.IsTrue(screen.IsOpen);
            screen.RowFor("pt-BR").Use.onClick.Invoke();
            yield return null;
            Assert.AreEqual("pt-BR", Localizer.ActiveLocale);
            screen.Back();
            yield return WaitUnscaled(0.3f);
            Assert.IsTrue(Director.Nonverbal.Menus.SettingsOpen, "back on the card");
            yield return Captures("_ptbr", false);
        }
    }
}
