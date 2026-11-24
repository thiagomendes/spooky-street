using System;
using System.Collections;
using System.IO;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Audio;
using SpookyStreet.Case;
using SpookyStreet.Controls;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Cutscenes;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Core.Parent;
using SpookyStreet.Localization;
using SpookyStreet.UI;
using SpookyStreet.UI.Nonverbal;
using SpookyStreet.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// S004 stream E: the grown-ups' corner in the running game. The parent gate guards the settings card, and so the
    /// "For grown-ups" page and the credits; the card's switches take effect at once (reduced motion reaches the
    /// cutscene blends, large text the captions) and are saved; the card, the page and the credits work by
    /// keyboard and gamepad as well as by touch; playtime counts only unpaused play.
    /// </summary>
    public class ParentSettingsTests : InputTestFixture
    {
        private Keyboard _keyboard;
        private Gamepad _gamepad;
        private string _saves;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            _gamepad = InputSystem.AddDevice<Gamepad>();
            Localizer.ResetForTests();
            _saves = CaseDirector.UseTemporarySaveDirectory();
            ControlSchemeTracker.Override = null;
            AmbientMotion.ReducedMotion = false;
        }

        public override void TearDown()
        {
            AmbientMotion.ReducedMotion = false;
            ControlSchemeTracker.Override = null;
            base.TearDown();
        }

        private static GameBootstrap Game => GameBootstrap.Instance;

        private static CaseDirector Director => Game.Director;

        private static NonverbalMenus Menus => Director.Nonverbal.Menus;

        private static IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            yield return Frames(3);
            Assert.IsNotNull(Game.GrownUps, "the grown-ups' corner is built at boot");
        }

        private static IEnumerator Frames(int count)
        {
            for (int i = 0; i < count; i++)
            {
                yield return null;
            }
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.unscaledTime + seconds;
            while (Time.unscaledTime < until)
            {
                yield return null;
            }
        }

        private IEnumerator Tap(ButtonControl button)
        {
            Press(button, queueEventOnly: true);
            yield return null;
            yield return null;
            Release(button, queueEventOnly: true);
            yield return null;
        }

        private static IEnumerator PassGateByTouch()
        {
            var gate = Menus.Gate;
            Assert.IsNotNull(gate);
            gate.TouchDown(1, gate.ZoneA.CenterX, gate.ZoneA.CenterY);
            gate.TouchDown(2, gate.ZoneB.CenterX, gate.ZoneB.CenterY);
            yield return Wait(ParentGate.DefaultHoldSeconds + 0.4f);
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator TheGateGuardsSettingsThePageAndTheCredits()
        {
            yield return LoadGame();
            var corner = Game.GrownUps;
            Assert.IsNotNull(Game.Ui.TitleSettingsButton, "the title has the grown-ups' gear");
            Game.Ui.TitleSettingsButton.onClick.Invoke();
            yield return Frames(2);
            Assert.IsTrue(Menus.GateOpen, "the gear asks for the gate first");
            Assert.IsFalse(Menus.SettingsOpen);
            Assert.IsFalse(corner.ParentPage.IsOpen);
            Assert.IsFalse(Menus.GrownUpsEntry.gameObject.activeInHierarchy, "the page's entry is behind the gate");

            // Cancelling the gate opens nothing.
            Menus.GateCloseButton.onClick.Invoke();
            yield return Frames(2);
            Assert.IsFalse(Menus.GateOpen);
            Assert.IsFalse(Menus.SettingsOpen);

            Game.Ui.TitleSettingsButton.onClick.Invoke();
            yield return Frames(2);
            yield return PassGateByTouch();
            Assert.IsTrue(Menus.SettingsOpen, "the gate passed: the card");

            Menus.GrownUpsEntry.onClick.Invoke();
            yield return Frames(3);
            Assert.IsTrue(corner.ParentPage.IsOpen, "the page opens from the card");
            Assert.IsFalse(Menus.SettingsOpen, "the card waits underneath");
            Assert.AreEqual(ParentCaseStatus.NotStarted, corner.ParentPage.Shown.CaseStatus);
            corner.ParentPage.Sheet.CloseButton.onClick.Invoke();
            yield return Frames(2);
            Assert.IsFalse(corner.ParentPage.IsOpen);
            Assert.IsTrue(Menus.SettingsOpen, "closing the page goes back to the card");

            Menus.CreditsEntry.onClick.Invoke();
            yield return Frames(3);
            Assert.IsTrue(corner.Credits.IsOpen);
            Assert.AreEqual(2, corner.Credits.LicencesShown, "both OFL licence texts ship in the app");
            Assert.Greater(corner.Credits.Sheet.ScrollRange, 100f, "the licences scroll");
            corner.Credits.Sheet.CloseButton.onClick.Invoke();
            yield return Frames(2);
            Assert.IsTrue(Menus.SettingsOpen);

            Menus.LanguageEntry.onClick.Invoke();
            yield return Frames(2);
            Assert.IsTrue(Game.LanguageScreen.IsOpen, "the language screen opens from the card");
            Game.LanguageScreen.Back();
            yield return Frames(2);
            Assert.IsTrue(Menus.SettingsOpen, "and returns to it");

            // A new visit needs a new gate.
            Menus.SettingsCloseButton.onClick.Invoke();
            yield return Frames(2);
            Game.Ui.TitleSettingsButton.onClick.Invoke();
            yield return Frames(2);
            Assert.IsTrue(Menus.GateOpen);
            Assert.IsFalse(Menus.SettingsOpen);
            Menus.CancelGate();
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator TheSwitchesTakeEffectAtOnceAndAreSaved()
        {
            yield return LoadGame();
            Game.Ui.NewGameButton.onClick.Invoke();
            yield return Frames(3);
            Menus.OpenSettings();
            yield return Frames(1);

            // Reduced motion: the ambient motion and the next cutscene (a cut instead of a blend).
            Assert.IsFalse(AmbientMotion.ReducedMotion);
            Menus.Switch(SettingsCard.ReducedMotion).onClick.Invoke();
            Assert.IsTrue(AmbientMotion.ReducedMotion, "reduced motion is on at once");
            Assert.IsTrue(Menus.SwitchShowsOn(SettingsCard.ReducedMotion));
            Assert.IsTrue(Game.Audio.Preferences.ReducedMotion);

            // Large and solid captions.
            Menus.Switch(SettingsCard.LargeCaptions).onClick.Invoke();
            Assert.IsTrue(Director.Captions.LargeText, "large text reaches the captions");
            Assert.IsTrue(Director.Dialogue.LargeText, "and the speech card");
            Menus.Switch(SettingsCard.OpaqueCaptions).onClick.Invoke();
            Assert.AreEqual(1f, Director.Captions.BackingAlpha, 1e-3f, "solid caption backing");

            // Camera: inverted and fastest.
            float sign = Game.CameraRig.PitchSign;
            Menus.Switch(SettingsCard.InvertY).onClick.Invoke();
            Assert.AreEqual(-sign, Game.CameraRig.PitchSign, "invert up and down");
            Menus.SliderFor(SettingsCard.CameraSpeed).value = 1f;
            Assert.AreEqual(3f, Game.CameraRig.Sensitivity, 1e-3f);

            // Left-handed: the jump disc crosses to the left, the stick zone to the right.
            var jump = (RectTransform)Game.Ui.Hud.transform.Find("JumpButton");
            Assert.Greater(jump.anchorMin.x, 0.5f);
            Menus.Switch(SettingsCard.LeftHanded).onClick.Invoke();
            Assert.Less(jump.anchorMin.x, 0.5f, "left-handed controls swap sides at once");
            Assert.IsTrue(Game.Ui.LeftHanded);
            var zone = (RectTransform)Game.Ui.Hud.transform.Find("StickZone");
            Assert.Greater(zone.anchorMin.x, 0.5f);

            Menus.Switch(SettingsCard.Haptics).onClick.Invoke();
            Assert.IsFalse(GameHaptics.Enabled);
            Menus.CloseSettings();

            Assert.IsTrue(Director.Cutscenes.Play(C01Cutscenes.OpeningId, true));
            yield return Frames(2);
            Assert.IsTrue(Director.Cutscenes.Player.ReducedMotion, "the cutscene plays with reduced motion");
            Director.Cutscenes.Player.SkipNow();
            yield return Wait(GameAudio.SaveDelay + 0.5f);

            // Saved: a fresh boot comes back with every choice.
            yield return LoadGame();
            var p = Game.Audio.Preferences;
            Assert.IsTrue(p.ReducedMotion && p.LargeCaptions && p.OpaqueCaptions && p.InvertY && p.LeftHanded);
            Assert.IsFalse(p.Haptics);
            Assert.AreEqual(3f, p.CameraSensitivity, 1e-3f);
            Assert.IsTrue(AmbientMotion.ReducedMotion, "applied at boot");
            Assert.IsTrue(Director.Captions.LargeText);
            Assert.IsTrue(Game.Ui.LeftHanded);
            Assert.Less(((RectTransform)Game.Ui.Hud.transform.Find("JumpButton")).anchorMin.x, 0.5f);
            Assert.AreEqual(3f, Game.CameraRig.Sensitivity, 1e-3f);
            GameHaptics.Enabled = true;
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator TheCardAndThePageWorkByKeyboardAndGamepad()
        {
            yield return LoadGame();
            Game.Ui.NewGameButton.onClick.Invoke();
            yield return Frames(3);
            Game.Flow.Pause();
            yield return Frames(2);
            Director.Nonverbal.SettingsButton.onClick.Invoke();
            yield return Frames(2);
            Assert.IsTrue(Menus.GateOpen);

            // The gate by keys: Q and P held together.
            Press(_keyboard.qKey);
            yield return Wait(0.5f);
            Assert.IsTrue(Menus.Gate.IsZoneHeld(0), "Q holds the first spot");
            Press(_keyboard.pKey);
            yield return Wait(ParentGate.DefaultHoldSeconds + 0.4f);
            Assert.IsTrue(Menus.SettingsOpen, "Q and P held for 3 s pass the gate");
            Release(_keyboard.qKey);
            Release(_keyboard.pKey);
            yield return Frames(2);
            var nav = Menus.SettingsNavigation;
            Assert.AreEqual(SettingsCard.Helper, nav.Focus, "focus starts on the first row");

            // Down to music, left lowers it a step (focus stays), down to the entries, right, Enter: the page.
            yield return Tap(_keyboard.downArrowKey);
            Assert.AreEqual(SettingsCard.Music, nav.Focus);
            yield return Tap(_keyboard.leftArrowKey);
            Assert.AreEqual(SettingsCard.Music, nav.Focus, "left moves the slider, not the focus");
            Assert.AreEqual(0.9f, Game.Audio.Preferences.MusicVolume, 1e-3f);
            Assert.AreEqual(0.9f, Menus.MusicSlider.value, 1e-3f);
            yield return Tap(_keyboard.upArrowKey);
            yield return Tap(_keyboard.rightArrowKey);
            Assert.AreEqual(SettingsCard.CameraSpeed, nav.Focus, "a switch row carries focus across columns");
            yield return Tap(_keyboard.rightArrowKey);
            Assert.AreEqual(SettingsCard.CameraSpeed, nav.Focus);
            yield return Tap(_keyboard.downArrowKey);
            Assert.AreEqual(SettingsCard.InvertY, nav.Focus);
            yield return Tap(_keyboard.rightArrowKey);
            Assert.AreEqual(SettingsCard.LargeCaptions, nav.Focus);
            yield return Tap(_keyboard.enterKey);
            Assert.IsTrue(Game.Audio.Preferences.LargeCaptions, "Enter flips the focused switch");
            Assert.IsTrue(Director.Captions.LargeText);
            yield return Tap(_keyboard.downArrowKey);
            Assert.AreEqual(SettingsCard.OpaqueCaptions, nav.Focus);
            yield return Tap(_keyboard.downArrowKey);
            Assert.AreEqual(SettingsCard.Haptics, nav.Focus, "the short column hands focus to its neighbour");
            yield return Tap(_keyboard.downArrowKey);
            Assert.AreEqual(SettingsCard.GrownUps, nav.Focus);
            yield return Tap(_keyboard.rightArrowKey);
            Assert.AreEqual(SettingsCard.Credits, nav.Focus);
            yield return Tap(_keyboard.leftArrowKey);
            Assert.AreEqual(SettingsCard.GrownUps, nav.Focus);
            yield return Tap(_keyboard.enterKey);
            yield return Frames(2);
            var page = Game.GrownUps.ParentPage;
            Assert.IsTrue(page.IsOpen, "Enter opens the page");
            Assert.AreEqual(ReadingSheetFocus.Sheet, page.Sheet.Navigation.Focus);
            float range = page.Sheet.ScrollRange;
            Assert.Greater(range, 0f, "the page is taller than the screen");
            yield return Tap(_keyboard.downArrowKey);
            Assert.Greater(page.Sheet.Scrolled, 1f, "down scrolls the sheet");

            // Gamepad: the bumper pages down, d-pad up goes back up, East closes the page, East again the card.
            yield return Tap(_gamepad.rightShoulder);
            float paged = page.Sheet.Scrolled;
            Assert.Greater(paged, ReadingSheet.Step, "a bumper turns a whole page");
            yield return Tap(_gamepad.dpad.up);
            Assert.Less(page.Sheet.Scrolled, paged);
            yield return Tap(_gamepad.buttonEast);
            yield return Frames(2);
            Assert.IsFalse(page.IsOpen, "Back closes the page");
            Assert.IsTrue(Menus.SettingsOpen, "back on the card");
            Assert.AreEqual(GameState.Paused, Game.Flow.State, "and the game stays paused");
            yield return Tap(_keyboard.escapeKey);
            yield return Frames(2);
            Assert.IsFalse(Menus.SettingsOpen, "Escape closes the card");
            Assert.AreEqual(GameState.Paused, Game.Flow.State, "without resuming the game in the same press");
        }

        [UnityTest]
        [Timeout(60000)]
        public IEnumerator PlaytimeCountsOnlyUnpausedPlayAndStaysOnTheDevice()
        {
            yield return LoadGame();
            var corner = Game.GrownUps;
            var today = new DateTime(2026, 11, 24, 18, 0, 0);
            corner.Clock = () => today;
            yield return Wait(0.5f);
            Assert.AreEqual(0, corner.Playtime.SecondsOn(today), "the title does not count");

            Game.Ui.NewGameButton.onClick.Invoke();
            yield return Wait(1.0f);
            double played = corner.Playtime.SecondsOn(today);
            Assert.Greater(played, 0.5);

            Game.Flow.Pause();
            yield return Wait(1.0f);
            Assert.AreEqual(played, corner.Playtime.SecondsOn(today), 0.05, "pause does not count");
            Assert.IsTrue(File.Exists(Path.Combine(_saves, "playtime.json")), "written when play stops");

            var model = corner.BuildProgress();
            Assert.AreNotEqual(ParentCaseStatus.Solved, model.CaseStatus);
            Assert.AreEqual(played, model.Week.TotalSeconds, 0.05, "the page reads the ledger");
            Game.Flow.Resume();
        }
    }
}
