using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Lines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// The animated non-verbal UX in the running game (docs/nonverbal-ux.md §12, D23): vignettes in the speech
    /// bubble, the words setting, the once-per-save memory replay and its skip, the notebook carousel's comics, the
    /// tier-2 bat trail and the parent gate in front of the settings toggles. Fresh temporary save folder per test.
    /// </summary>
    public class NonverbalUiTests : InputTestFixture
    {
        private const string SceneName = "LanternLane";

        private string _saves;
        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            Localizer.ResetForTests();
            _saves = CaseDirector.UseTemporarySaveDirectory();
        }

        private static IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(GameBootstrap.Instance, "scene did not create GameBootstrap");
        }

        private static IEnumerator StartGame()
        {
            yield return LoadGame();
            GameBootstrap.Instance.Ui.NewGameButton.onClick.Invoke();
            yield return Wait(0.6f); // Pip lands on the porch
            Assert.AreEqual(GameState.Playing, GameBootstrap.Instance.Flow.State);
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
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
            }
        }

        private static CaseDirector Director => GameBootstrap.Instance.Director;

        private static IEnumerator TalkToBeaAndDismiss()
        {
            var director = Director;
            director.Perform(CaseAction.Talk(Speaker.Bea));
            yield return Wait(0.4f);
            Assert.IsTrue(director.Dialogue.IsOpen);
            Assert.AreEqual(Lines.BeaSawCape, director.Dialogue.CurrentLine.Key);
            director.Dialogue.Advance();
            yield return Frames(3);
        }

        [UnityTest]
        public IEnumerator VignettePlaysForAScopedLineAndTheCardForOthers()
        {
            yield return StartGame();
            var dialogue = Director.Dialogue;
            dialogue.Play(new[] { new ScriptLine(Speaker.Bea, Lines.BeaSawCape), new ScriptLine(Speaker.Bea, Lines.BeaRepeat) });
            yield return Wait(1.0f);
            Assert.IsTrue(dialogue.ShowsVignette, "Bea's key line plays its vignette in the bubble");
            Assert.AreEqual(C01Vignettes.BeaSawCape, dialogue.Vignette.Current.Id);
            Assert.Greater(dialogue.Vignette.LocalTime, 0.5f, "the vignette advances");
            Assert.Greater(dialogue.Vignette.VisibleParts, 3, "puppets are on the stage");
            Assert.IsTrue(dialogue.TextLineVisible, "words are on by default");

            dialogue.Advance();
            yield return Frames(2);
            Assert.AreEqual(Lines.BeaRepeat, dialogue.CurrentLine.Key);
            Assert.IsTrue(C01Vignettes.For(Lines.BeaRepeat).UsesV1Strip);
            Assert.IsFalse(dialogue.ShowsVignette, "a line without a vignette keeps the v1 card");
            Assert.IsNull(dialogue.Vignette.Current);
            Assert.IsTrue(dialogue.TextLineVisible, "the v1 card always shows its words");
            dialogue.Skip();
        }

        [UnityTest]
        public IEnumerator TheTextLineFollowsTheWordsSetting()
        {
            yield return StartGame();
            var director = Director;
            var dialogue = director.Dialogue;
            director.Nonverbal.ChooseReadingMode(true);
            Assert.IsFalse(director.Nonverbal.Settings.ShowSpeechText);
            Assert.IsTrue(File.Exists(NonverbalSettingsStore.PathsFor(_saves).Latest), "the choice is saved");
            dialogue.Play(new[] { new ScriptLine(Speaker.Vale, Lines.ValeDoorShut) });
            yield return Frames(3);
            Assert.IsTrue(dialogue.ShowsVignette);
            Assert.IsFalse(dialogue.TextLineVisible, "Pictures only hides the words");
            Assert.AreEqual(Localizer.Get(Lines.ValeDoorShut), dialogue.CurrentText, "the line text is still known");

            director.Nonverbal.ChooseReadingMode(false);
            yield return Frames(2);
            Assert.IsTrue(dialogue.TextLineVisible, "Words and pictures shows the line again");
            dialogue.Skip();
        }

        [UnityTest]
        public IEnumerator AMemoryReplayPlaysOnceAndNotAgainAfterAReload()
        {
            yield return StartGame();
            var director = Director;
            var replay = director.Nonverbal.MemoryReplay;
            yield return TalkToBeaAndDismiss();
            Assert.IsTrue(replay.Active, "the first hearing of Bea's key line replays her memory");
            Assert.AreEqual(MemoryReplays.BeaSawCape, replay.Last.Id);
            Assert.IsTrue(director.Nonverbal.Progress.Seen.Contains(MemoryReplays.BeaSawCape), "marked before playing");
            Assert.IsTrue(GameBootstrap.Instance.Controls.GameplayEnabled, "a replay never takes the controls");
            yield return Wait(1.0f);
            Assert.Greater(replay.Weight, 0.9f, "the memory look is up during the hold");
            Assert.IsTrue(replay.Ghost.gameObject.activeSelf, "June's paper ghost runs the route");
            yield return Wait(2.2f);
            Assert.IsFalse(replay.Active, "the replay ends by itself (≤ 2.7 s)");

            // Same line again in this save: no replay.
            director.Dialogue.Play(new[] { new ScriptLine(Speaker.Bea, Lines.BeaSawCape) });
            yield return Frames(2);
            director.Dialogue.Advance();
            yield return Frames(3);
            Assert.IsFalse(replay.Active);
            Assert.AreEqual(1, replay.PlayCount);

            // Reload: the seen set comes back with the save.
            yield return LoadGame();
            var game = GameBootstrap.Instance;
            game.Ui.ContinueButton.onClick.Invoke();
            yield return Wait(0.6f);
            var restored = game.Director;
            Assert.IsTrue(restored.State.Has(CaseFact.BeaSpoke));
            Assert.IsTrue(restored.Nonverbal.Progress.Seen.Contains(MemoryReplays.BeaSawCape), "seen survives a reload");
            restored.Dialogue.Play(new[] { new ScriptLine(Speaker.Bea, Lines.BeaSawCape) });
            yield return Frames(2);
            restored.Dialogue.Advance();
            yield return Frames(3);
            Assert.IsFalse(restored.Nonverbal.MemoryReplay.Active, "not again after a reload");
            Assert.AreEqual(0, restored.Nonverbal.MemoryReplay.PlayCount);
        }

        [UnityTest]
        public IEnumerator SkippingTheReplayBlendsBackAndKeepsTheFact()
        {
            yield return StartGame();
            var director = Director;
            var replay = director.Nonverbal.MemoryReplay;
            yield return TalkToBeaAndDismiss();
            Assert.IsTrue(replay.Active);
            yield return Wait(0.5f);
            replay.SkipButton.onClick.Invoke();
            Assert.IsTrue(replay.Playback == null || replay.Playback.WasSkipped || replay.Playback.IsDone);
            yield return Wait(0.5f);
            Assert.IsFalse(replay.Active, "skip blends back at once");
            Assert.IsTrue(director.State.Has(CaseFact.BeaSpoke), "the clue never depends on the replay");
            Assert.IsTrue(director.Nonverbal.Progress.Seen.Contains(MemoryReplays.BeaSawCape), "a skipped replay counts as seen");

            // The notebook offers it again on demand (the eye on Bea's sticker), without marking anything.
            director.OpenNotebook();
            yield return Frames(2);
            Assert.AreEqual(1, director.Notebook.ReplayEyes.Count, "Bea's sticker offers her memory");
            director.Notebook.ReplayEyes[0].onClick.Invoke();
            yield return Frames(2);
            Assert.IsFalse(director.Notebook.IsOpen);
            Assert.IsTrue(replay.Active, "the eye replays the memory");
            replay.Skip();
        }

        [UnityTest]
        public IEnumerator TheNotebookCarouselPlaysItsComics()
        {
            yield return StartGame();
            var director = Director;
            director.Perform(CaseAction.Of(CaseActionKind.HearKnocks));
            director.Perform(CaseAction.Of(CaseActionKind.EnterAttic));
            if (director.Dialogue.IsOpen)
            {
                director.Dialogue.Skip();
            }

            yield return Frames(2);
            director.OpenNotebook();
            yield return Frames(3);
            var notebook = director.Notebook;
            Assert.AreEqual(3, notebook.PageCount, "one page per question");
            Assert.AreEqual(0, notebook.CurrentPage);
            Assert.IsNotNull(notebook.AnswerStrips[0], "the entry question is answered: its comic is drawn");
            Assert.IsNull(notebook.AnswerStrips[1], "unknown answers are dashed panels");
            var strip = notebook.AnswerStrips[0];
            Assert.IsFalse(strip.Running, "the comic rests until tapped");
            Assert.IsTrue(notebook.PlayStrip(0));
            yield return Wait(1.0f);
            Assert.IsTrue(strip.Running, "tapping the answer plays it");
            Assert.AreEqual(C01Vignettes.NotebookEntry, strip.Current.Id);
            Assert.Greater(strip.LocalTime, 0.5f);
            yield return Wait(strip.Current.Length * C01Vignettes.NotebookLoops + 0.3f);
            Assert.IsTrue(strip.Finished, "it plays twice and holds the end");

            notebook.ShowPage(1);
            yield return Frames(2);
            Assert.AreEqual(1, notebook.CurrentPage, "the carousel moves to the next question");
            notebook.Close();
        }

        [UnityTest]
        public IEnumerator TheTierTwoRadioFliesTheBatTrailAndItStops()
        {
            yield return StartGame();
            var director = Director;
            var layer = director.Nonverbal;
            Assert.IsNotNull(layer.RadioButton, "the radio sits on the HUD");
            layer.RadioButton.onClick.Invoke(); // tier 1: look
            Assert.IsFalse(layer.Guide.Active, "tier 1 has no trail");
            layer.RadioButton.onClick.Invoke(); // tier 2: where
            Assert.IsTrue(layer.Guide.Active, "tier 2 starts the bat trail");
            yield return Wait(1.0f);
            Assert.Greater(layer.Guide.VisibleBats, 0, "paper bats fly the route");
            Assert.LessOrEqual(layer.Guide.VisibleBats, BatTrailSchedule.MaxConcurrentBats);
            yield return Wait(1.2f);
            Assert.IsTrue(layer.Guide.BeaconVisible, "the goal sticker pulses once the first bats arrive");

            // Meeting the objective stops the trail.
            var objective = CaseHints.ObjectiveFor(director.State);
            director.Perform(objective == CaseObjective.Arrival
                ? CaseAction.Of(CaseActionKind.HearKnocks)
                : CaseAction.Of(CaseActionKind.SitOnBench));
            yield return Frames(2);
            Assert.IsFalse(layer.Guide.Active, "the trail stops when the objective changes");
            Assert.AreEqual(0, layer.Guide.VisibleBats);
        }

        [UnityTest]
        public IEnumerator TheParentGateGuardsTheSettingsToggles()
        {
            yield return StartGame();
            var game = GameBootstrap.Instance;
            var layer = game.Director.Nonverbal;
            var menus = layer.Menus;
            game.Flow.Pause();
            yield return Frames(2);
            Assert.IsNotNull(layer.SettingsButton, "the gear sits on the pause card");
            layer.SettingsButton.onClick.Invoke();
            yield return Frames(2);
            Assert.IsTrue(menus.GateOpen, "the gear opens the parent gate first");
            Assert.IsFalse(menus.SettingsOpen, "the toggles stay behind the gate");
            var gate = menus.Gate;

            // One spot is not enough, and letting go resets.
            gate.TouchDown(1, gate.ZoneA.CenterX, gate.ZoneA.CenterY);
            yield return Wait(1.0f);
            Assert.AreEqual(ParentGateState.Waiting, gate.State);
            gate.TouchDown(2, gate.ZoneB.CenterX, gate.ZoneB.CenterY);
            yield return Wait(1.5f);
            Assert.IsFalse(menus.SettingsOpen, "held for only 1.5 s");
            gate.TouchUp(2);
            yield return Frames(2);
            Assert.AreEqual(0f, gate.Progress, 0.001f, "release resets");
            gate.TouchDown(2, gate.ZoneB.CenterX, gate.ZoneB.CenterY);
            yield return Wait(ParentGate.DefaultHoldSeconds + 0.4f);
            Assert.IsTrue(menus.SettingsOpen, "both spots held for 3 s open the settings");
            Assert.IsFalse(menus.GateOpen);

            Assert.IsTrue(layer.Settings.ShowSpeechText);
            menus.ShowSpeechTextToggle.onClick.Invoke();
            Assert.IsFalse(layer.Settings.ShowSpeechText, "the words toggle turns the text line off");
            Assert.IsFalse(game.Director.Dialogue.ShowSpeechText);
            Assert.IsFalse(layer.Settings.HelperAutoHints);
            menus.HelperToggle.onClick.Invoke();
            Assert.IsTrue(layer.Settings.HelperAutoHints, "the Helper toggle turns tier-3 auto help on");
            var saved = new NonverbalSettingsStore(new SpookyStreet.Save.DiskSaveFileSystem(), _saves).Load();
            Assert.IsFalse(saved.ShowSpeechText, "settings are saved at once");
            Assert.IsTrue(saved.HelperAutoHints);

            // A new guarded action needs a new gate.
            menus.CloseSettings();
            layer.SettingsButton.onClick.Invoke();
            Assert.AreNotSame(gate, menus.Gate, "one gate per guarded action");
            Assert.AreEqual(ParentGateState.Waiting, menus.Gate.State);
            menus.CancelGate();
            game.Flow.Resume();
        }

        [UnityTest]
        public IEnumerator TheParentGateAlsoOpensWithTwoHeldButtons()
        {
            yield return StartGame();
            var game = GameBootstrap.Instance;
            var layer = game.Director.Nonverbal;
            var menus = layer.Menus;
            game.Flow.Pause();
            yield return Frames(2);
            layer.SettingsButton.onClick.Invoke();
            yield return Frames(2);
            Assert.IsTrue(menus.GateOpen);

            // One button is not enough (portability, D25: Q + P or both shoulder buttons).
            Press(_keyboard.qKey);
            yield return Wait(1.0f);
            Assert.IsTrue(menus.Gate.IsZoneHeld(0));
            Assert.AreEqual(ParentGateState.Waiting, menus.Gate.State);
            Press(_keyboard.pKey);
            yield return Wait(ParentGate.DefaultHoldSeconds + 0.4f);
            Assert.IsTrue(menus.SettingsOpen, "holding both buttons for 3 s passes the gate");
            Release(_keyboard.qKey);
            Release(_keyboard.pKey);
            menus.CloseSettings();
            game.Flow.Resume();
        }

        [UnityTest]
        public IEnumerator FirstLaunchOffersTheTwoPictureCards()
        {
            yield return LoadGame();
            var layer = GameBootstrap.Instance.Director.Nonverbal;
            Assert.IsTrue(layer.Settings.NeedsFirstLaunchChoice);
            Assert.IsTrue(layer.Menus.ChoiceOpen, "a first launch shows the reading choice over the title");
            layer.Menus.PicturesCard.onClick.Invoke();
            Assert.IsFalse(layer.Menus.ChoiceOpen);
            Assert.IsFalse(layer.Settings.ShowSpeechText, "Pictures only");
            Assert.IsFalse(layer.Settings.NeedsFirstLaunchChoice);

            yield return LoadGame();
            var again = GameBootstrap.Instance.Director.Nonverbal;
            Assert.IsFalse(again.Menus.ChoiceOpen, "asked only once");
            Assert.IsFalse(again.Settings.ShowSpeechText, "the choice persists");
        }
    }
}
