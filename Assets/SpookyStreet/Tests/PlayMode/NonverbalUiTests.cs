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
using SpookyStreet.UI.Nonverbal;
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

        /// <summary>Every case 01 line by speaker, with the speaker who says it.</summary>
        private static readonly (Speaker Speaker, string[] Keys)[] LinesBySpeaker =
        {
            (Speaker.Bea, new[]
            {
                Lines.BeaSawCape, Lines.BeaRepeat, Lines.BeaDoorOpen, Lines.BeaClosed, Lines.BeaRemembers,
            }),
            (Speaker.Vale, new[]
            {
                Lines.ValeDoorShut, Lines.ValeInviteSit, Lines.ValeRepeat, Lines.ValeWasShut, Lines.ValeOpenNow,
                Lines.ValeClosed, Lines.ValeSavedSweet, Lines.ValeRemembers, Lines.ValeLanternPail,
            }),
            (Speaker.June, new[]
            {
                Lines.JuneGreet, Lines.JuneGreetShortcut, Lines.JuneDoorSticks, Lines.JuneDoorAlreadyOpen,
                Lines.JuneLatchReminder, Lines.JuneLatchThanks, Lines.JuneListenFloor, Lines.JuneGoOutside,
                Lines.JuneResolve, Lines.JuneClosed, Lines.JuneRemembers, Lines.JuneStarCape,
            }),
            (Speaker.Kit, new[] { Lines.KitFirstShow, Lines.KitThanks, Lines.KitAsk, Lines.KitRemembers, Lines.KitShowmanHat }),
            (Speaker.Pip, new[]
            {
                Lines.ExamineStreamer, Lines.ExamineCloth, Lines.ExamineSill, Lines.ExamineDoorStuck, Lines.ExamineBowl,
                Lines.ExamineBowlSweet, Lines.ExamineOwlPanel, Lines.ExamineOwlBooth,
            }),
        };

        [UnityTest]
        public IEnumerator EveryCaseLineShowsItsVignetteAndItsTextLine()
        {
            yield return StartGame();
            var director = Director;
            var dialogue = director.Dialogue;
            int seen = 0;
            foreach (var (speaker, keys) in LinesBySpeaker)
            {
                var lines = keys.Select(k => new ScriptLine(speaker, k)).ToArray();
                dialogue.Play(lines);
                for (int i = 0; i < lines.Length; i++)
                {
                    yield return Wait(0.25f);
                    string key = lines[i].Key;
                    Assert.AreEqual(key, dialogue.CurrentLine.Key);
                    Assert.IsTrue(dialogue.ShowsVignette, key + " plays in the bubble");
                    Assert.AreEqual(C01Vignettes.For(key).VignetteId, dialogue.Vignette.Current.Id, key);
                    Assert.Greater(dialogue.Vignette.LocalTime, 0.05f, key + ": the vignette advances");
                    Assert.Greater(dialogue.Vignette.VisibleParts, 0, key + ": puppets are on the stage");
                    Assert.IsTrue(dialogue.TextLineVisible, key + ": the words show under the pictures (D23.1)");
                    Assert.AreEqual(Localizer.Get(key), dialogue.CurrentText, key);
                    seen++;
                    dialogue.Advance();
                    yield return null;
                }

                Assert.IsFalse(dialogue.IsOpen, speaker + ": the conversation closes after its last line");
                director.Nonverbal.MemoryReplay.End(false);
            }

            // Captions: the strip keeps its words and plays the vignette on its mini stage.
            foreach (string key in CaseScript.AllLineKeys.Where(k => k.StartsWith("c01.caption.")))
            {
                director.Captions.Clear();
                director.Captions.ShowKey(key);
                yield return Wait(0.25f);
                Assert.IsTrue(director.Captions.ShowsVignette, key + " plays on the caption's mini stage");
                Assert.AreEqual(C01Vignettes.For(key).VignetteId, director.Captions.Vignette.Current.Id, key);
                Assert.Greater(director.Captions.Vignette.VisibleParts, 0, key);
                Assert.AreEqual(Localizer.Get(key), director.Captions.CurrentText, key + ": the words stay");
                seen++;
            }

            Assert.AreEqual(CaseScript.AllLineKeys.Count(), seen, "every case 01 line was shown");

            // A hint is not a case line: the strip shows it alone.
            director.Captions.ShowNow("c01.hint.arrival.observe");
            yield return null;
            Assert.IsFalse(director.Captions.ShowsVignette, "hints have no mini stage");
            director.Captions.Clear();
        }

        [UnityTest]
        public IEnumerator EachLineShowsItsEmotionsPortraitAndCaptionsShowNone()
        {
            // S004 §2.6: the face disc shows the speaker's badge in the line's LineVoices emotion.
            yield return StartGame();
            var director = Director;
            var dialogue = director.Dialogue;
            Assert.IsTrue(PortraitAtlas.Available, "the portrait atlas loads from Resources");
            var lines = new[]
            {
                new ScriptLine(Speaker.Bea, Lines.BeaSawCape), // surprised
                new ScriptLine(Speaker.Bea, Lines.BeaClosed), // happy
                new ScriptLine(Speaker.June, Lines.JuneListenFloor), // whisper
                new ScriptLine(Speaker.Pip, Lines.ExamineCloth), // thinking
                new ScriptLine(Speaker.Vale, Lines.ValeDoorShut), // neutral
            };
            dialogue.Play(lines);
            foreach (var line in lines)
            {
                yield return Wait(0.3f);
                Assert.AreEqual(line.Key, dialogue.CurrentLine.Key);
                string expected = LinePortraits.SpriteName(line);
                Assert.IsNotNull(expected, line.Key);
                Assert.AreEqual(expected, dialogue.PortraitId, line.Key + ": the emotion's portrait");
                var image = dialogue.PortraitImage;
                Assert.IsTrue(image.enabled && image.gameObject.activeInHierarchy, line.Key + ": the badge shows");
                Assert.AreEqual(expected, image.sprite.name, line.Key);
                Assert.AreEqual(1f, image.color.a, 0.01f, line.Key + ": the swap settles fully opaque");
                dialogue.Advance();
                yield return null;
            }

            Assert.AreEqual("portrait.bea.surprised", LinePortraits.SpriteName(lines[0]));
            Assert.AreEqual("portrait.june.whisper", LinePortraits.SpriteName(lines[2]));
            Assert.IsFalse(dialogue.IsOpen);
            Assert.IsNull(dialogue.PortraitId, "no portrait once the card closes");
            director.Nonverbal.MemoryReplay.End(false);

            // Reduced motion: the face swaps at once, with no fade.
            dialogue.ReducedMotion = true;
            dialogue.Play(lines.Take(2).ToArray());
            yield return null;
            dialogue.Advance();
            yield return null;
            Assert.AreEqual("portrait.bea.happy", dialogue.PortraitId);
            Assert.AreEqual(1f, dialogue.PortraitImage.color.a, 0.01f, "reduced motion: no fade");
            dialogue.Cancel();
            dialogue.ReducedMotion = false;

            // A caption has no face: the dialogue shows no badge for it, and the caption strip keeps its own look.
            dialogue.Play(new[] { new ScriptLine(Speaker.Caption, Lines.CaptionGrille) });
            yield return null;
            Assert.IsNull(dialogue.PortraitId, "captions show no portrait");
            Assert.IsFalse(dialogue.PortraitImage.enabled);
            dialogue.Cancel();
            director.Captions.ShowKey(Lines.CaptionGrille);
            yield return Wait(0.25f);
            var portraitTexture = PortraitAtlas.Texture;
            foreach (var img in director.Captions.GetComponentsInChildren<UnityEngine.UI.Image>(true))
            {
                Assert.IsFalse(img.sprite != null && img.sprite.texture == portraitTexture,
                    img.name + ": the caption strip shows no portrait");
            }

            director.Captions.Clear();
        }

        [UnityTest]
        public IEnumerator MemoryReplaysNeverAutoPlayAndOnlyHazyOnesAreOffered()
        {
            // D26 §7: the first hearing only offers the memory in the notebook; nothing auto-plays. Since S002.5b
            // Bea's is restaged as a hazy silver blur, so both witnesses' stickers offer theirs.
            yield return StartGame();
            var director = Director;
            var replay = director.Nonverbal.MemoryReplay;
            yield return TalkToBeaAndDismiss();
            yield return Wait(0.5f);
            Assert.IsFalse(replay.Active, "no auto-play after Bea's line");
            Assert.AreEqual(0, replay.PlayCount);
            Assert.IsTrue(director.Nonverbal.Progress.Seen.Contains(MemoryReplays.BeaSawCape), "offered on first hearing");

            director.Perform(CaseAction.Talk(Speaker.Vale));
            yield return Wait(0.3f);
            director.Dialogue.Skip();
            yield return Frames(3);
            Assert.IsFalse(replay.Active, "no auto-play after Vale's line either");

            director.OpenNotebook();
            yield return Frames(2);
            var notebook = director.Notebook;
            notebook.ShowPage(notebook.PageQuestions.IndexOf(CaseNotebook.Question(CaseNotebook.QuestionEntry)));
            yield return Frames(2);
            Assert.AreEqual(2, notebook.ReplayEyes.Count, "Bea's and Vale's stickers offer their memories");
            var valeEye = notebook.TraySticker(CaseNotebook.ValeDoor).transform.Find("WatchAgain");
            Assert.IsNotNull(valeEye);
            Assert.IsNotNull(notebook.TraySticker(CaseNotebook.BeaFlash).transform.Find("WatchAgain"));
            valeEye.GetComponentInChildren<UnityEngine.UI.Button>(true).onClick.Invoke();
            yield return Frames(2);
            Assert.IsFalse(notebook.IsOpen);
            Assert.IsTrue(replay.Active, "the eye replays the memory on demand");
            Assert.AreEqual(MemoryReplays.ValeDoorShut, replay.Last.Id);
            yield return Wait(0.5f);
            replay.SkipButton.onClick.Invoke();
            yield return Wait(0.5f);
            Assert.IsFalse(replay.Active, "skip blends back at once");
            Assert.IsTrue(director.State.Has(CaseFact.ValeSpoke), "the clue never depends on the replay");
        }

        [UnityTest]
        public IEnumerator TheNotebookCarouselHasOnePagePerRaisedQuestionAndNoAnswers()
        {
            yield return StartGame();
            var director = Director;
            director.Perform(CaseAction.Of(CaseActionKind.HearKnocks));
            director.Perform(CaseAction.Of(CaseActionKind.EnterAttic));
            director.Perform(CaseAction.Talk(Speaker.June));
            if (director.Dialogue.IsOpen)
            {
                director.Dialogue.Skip();
            }

            yield return Frames(2);
            director.OpenNotebook();
            yield return Frames(3);
            var notebook = director.Notebook;
            Assert.AreEqual(2, notebook.PageCount, "the knocks and June raised two questions; the workshop has not asked yet");
            Assert.AreEqual(0, notebook.CurrentPage);
            Assert.IsNull(notebook.AnswerLabels[1], "entering the attic no longer answers how June got in (D26)");
            Assert.AreEqual(0, notebook.ComicPanels.Count, "no comic before the child solves");
            Assert.IsNull(notebook.FoundStamp);
            notebook.ShowPage(1);
            yield return Frames(2);
            Assert.AreEqual(1, notebook.CurrentPage, "the carousel moves to the next question");
            Assert.AreEqual(3, notebook.Slots.Count, "three empty slots, all the same shape");
            foreach (var slot in notebook.Slots)
            {
                Assert.AreEqual(notebook.Slots[0].rectTransform.sizeDelta, slot.rectTransform.sizeDelta);
                Assert.AreEqual(0, slot.GetComponentsInChildren<SpookyStreet.UI.NotebookSticker>().Length, "nothing placed for the child");
            }

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
            Assert.IsNotNull(layer.Guide.TrailArea, "the trail leads to an area");
            yield return Wait(1.0f);
            Assert.Greater(layer.Guide.VisibleBats, 0, "paper bats fly toward the area");
            Assert.LessOrEqual(layer.Guide.VisibleBats, BatTrailSchedule.MaxConcurrentBats);
            yield return Wait(1.6f);
            Assert.Greater(layer.Guide.CirclingBats, 0, "the first bats circle the area (no goal sticker since S002.5b)");

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

            Assert.IsNull(menus.Canvas.transform.Find("NonverbalSettings/Content/SettingsCard/WordsRow"),
                "one hybrid reading mode (D23.1): no words toggle, only Helper");
            Assert.IsFalse(layer.Settings.HelperAutoHints);
            menus.HelperToggle.onClick.Invoke();
            Assert.IsTrue(layer.Settings.HelperAutoHints, "the Helper toggle turns tier-3 auto help on");
            var saved = new NonverbalSettingsStore(new SpookyStreet.Save.DiskSaveFileSystem(), _saves).Load();
            Assert.IsTrue(saved.HelperAutoHints, "settings are saved at once");

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
        public IEnumerator NoReadingChoiceAtFirstLaunchAndOldSettingsStillLoad()
        {
            // A settings file from the build with the reading choice: Pictures only picked, Helper on.
            Directory.CreateDirectory(_saves);
            File.WriteAllText(NonverbalSettingsStore.PathsFor(_saves).Latest,
                "{\"schemaVersion\":1,\"showSpeechText\":false,\"helperAutoHints\":true,\"firstLaunchChoiceMade\":true}");
            yield return LoadGame();
            var layer = GameBootstrap.Instance.Director.Nonverbal;
            Assert.IsNull(layer.Menus.Canvas.transform.Find("ReadingChoice"), "no first-launch reading cards (D23.1)");
            Assert.IsTrue(layer.Settings.HelperAutoHints, "the old file's Helper choice is kept");

            GameBootstrap.Instance.Ui.NewGameButton.onClick.Invoke();
            yield return Wait(0.6f);
            Assert.AreEqual(GameState.Playing, GameBootstrap.Instance.Flow.State, "nothing stands between title and play");
            var dialogue = Director.Dialogue;
            dialogue.Play(new[] { new ScriptLine(Speaker.Vale, Lines.ValeDoorShut) });
            yield return Frames(3);
            Assert.IsTrue(dialogue.ShowsVignette);
            Assert.IsTrue(dialogue.TextLineVisible, "an old Pictures-only file no longer hides the words");
            dialogue.Skip();
        }
    }
}
