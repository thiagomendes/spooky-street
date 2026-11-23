using System.Collections;
using System.IO;
using System.Linq;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Core.Save;
using SpookyStreet.Core.Street;
using SpookyStreet.Localization;
using SpookyStreet.Save;
using SpookyStreet.UI;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Lines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;
using N = SpookyStreet.Core.Case.CaseNotebook;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// S002.5b "the street remembers" (D29) and the Continue picture recap in the running game: closing case 01 and
    /// reloading shows the street's changes; a confirmed replay keeps the street but rests case 01's memories until the
    /// stamp; a confirmed New game forgets them; Continue shows a recap of a mid-case and of a closed save, never an
    /// answer. Fresh temporary save folder per test.
    /// </summary>
    public class StreetMemoryPlayTests : InputTestFixture
    {
        private string _directory;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            Localizer.ResetForTests();
            _directory = CaseDirector.UseTemporarySaveDirectory();
        }

        private static CaseDirector Director => GameBootstrap.Instance.Director;

        private static IEnumerator Load()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(GameBootstrap.Instance);
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
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                yield return null;
            }
        }

        private static IEnumerator Continue()
        {
            Assert.IsTrue(GameBootstrap.Instance.Ui.ContinueButton.gameObject.activeSelf, "Continue is offered");
            GameBootstrap.Instance.Ui.ContinueButton.onClick.Invoke();
            yield return Frames(10);
            Assert.AreEqual(GameState.Playing, GameBootstrap.Instance.Flow.State);
        }

        private static IEnumerator Do(params CaseAction[] actions)
        {
            foreach (var action in actions)
            {
                Director.Perform(action);
                Director.Dialogue.Skip();
                Director.Cutaway.Skip();
                yield return Frames(2);
            }

            Director.Captions.Clear();
        }

        private static CaseAction A(CaseActionKind kind) => CaseAction.Of(kind);

        /// <summary>Case 01 to the reopening with the three deductions (the stamp lands with the show).</summary>
        private static CaseState StampedState()
        {
            var state = CaseState.Empty;
            foreach (var action in CanonicalActions())
            {
                state = state.Apply(action).State;
            }

            Assert.IsTrue(state.Has(CaseFact.CaseStamped));
            return state;
        }

        private static CaseAction[] CanonicalActions() => new[]
        {
            A(CaseActionKind.HearKnocks), CaseAction.Talk(Speaker.Bea), CaseAction.Talk(Speaker.Vale),
            A(CaseActionKind.SitOnBench), A(CaseActionKind.ExamineCloth), A(CaseActionKind.EnterAttic),
            A(CaseActionKind.ExamineSill), CaseAction.Solve(N.QuestionEntry, new[] { N.ValeDoor, N.Cloth, N.SillSnag }),
            CaseAction.Talk(Speaker.June), A(CaseActionKind.EnterFoyer), A(CaseActionKind.LiftLatch),
            A(CaseActionKind.ExamineGrille), CaseAction.Solve(N.QuestionSource, new[] { N.FloorHum, N.WoodenPiece }),
            A(CaseActionKind.LinkArm), A(CaseActionKind.TurnCrank),
            CaseAction.Solve(N.QuestionMoved, new[] { N.LooseArm, N.OwlBooth }), A(CaseActionKind.ArriveAtPorch),
        };

        private void WriteCaseSave(CaseState state, string anchor)
        {
            var store = new CaseSaveStore(new DiskSaveFileSystem(), _directory);
            store.Load();
            Assert.IsTrue(store.Save(state, anchor));
        }

        private StreetState StreetOnDisk()
        {
            var store = new StreetStore(new DiskSaveFileSystem(), _directory);
            return store.Load();
        }

        private static void AssertStreetRemembers(string where)
        {
            var director = Director;
            var street = director.Street;
            Assert.IsTrue(street.WorldView.JuneOutside, where);
            Assert.IsFalse(street.WorldView.Resting, where);
            Assert.IsTrue(street.BoothLamp.enabled, where + ": the booth is lit");
            if (street.BeaSticker != null)
            {
                Assert.IsTrue(street.BeaSticker.activeSelf, where + ": Bea's owl sticker");
            }

            if (director.Cast != null)
            {
                Assert.AreEqual(JunePlacement.Treehouse, director.Cast.JuneMark, where + ": June plays outside");
                var spot = CaseStage.JuneTreehouse.Position;
                if (director.Cast.June != null)
                {
                    Assert.Less(Vector3.Distance(director.Cast.June.transform.position, new Vector3(spot.X, spot.Y, spot.Z)), 0.05f);
                }
                Assert.IsTrue(director.Cast.Kit == null || director.Cast.Kit.gameObject.activeSelf, where + ": Kit at her open booth");
            }

            if (director.House != null)
            {
                Assert.IsTrue(director.House.DoorOpen, where + ": the door opens from outside");
            }

            if (director.Workshop != null)
            {
                Assert.IsTrue(director.Workshop.OwlBowing && director.Workshop.WindEnabled, where + ": the owl bows with the wind");
            }

            Assert.IsTrue(CaseStage.IsOffered(CaseStage.Find(CaseStage.PointJuneTreehouse), director.View, false, false));
            Assert.IsFalse(CaseStage.IsOffered(CaseStage.Find(CaseStage.PointJunePorch), director.View, false, false));
        }

        private static void AssertStreetResting(string where)
        {
            var director = Director;
            var street = director.Street;
            Assert.IsFalse(street.WorldView.Any, where);
            Assert.IsTrue(street.WorldView.Resting, where);
            Assert.IsFalse(street.BoothLamp.enabled, where);
            if (street.BeaSticker != null)
            {
                Assert.IsFalse(street.BeaSticker.activeSelf, where);
            }

            if (director.Cast != null)
            {
                Assert.AreEqual(JunePlacement.Attic, director.Cast.JuneMark, where + ": June waits in the attic again");
                Assert.IsFalse(director.Cast.Kit != null && director.Cast.Kit.gameObject.activeSelf, where + ": the booth is shut");
            }

            if (director.House != null)
            {
                Assert.IsFalse(director.House.DoorOpen, where + ": the door sticks again");
            }
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ClosingCase01AndReloadingShowsTheStreetsChanges()
        {
            yield return Load();
            GameBootstrap.Instance.Ui.NewGameButton.onClick.Invoke();
            yield return Frames(10);
            Assert.IsTrue(Director.Street.State.IsEmpty);
            Assert.IsFalse(Director.Street.BoothLamp.enabled);
            yield return Do(CanonicalActions());
            if (Director.Cutscenes.Playing)
            {
                Director.Cutscenes.Player.SkipNow();
            }

            yield return Frames(3);
            Assert.IsTrue(Director.State.Has(CaseFact.CaseStamped));
            Assert.IsTrue(Director.Street.State.HasSolved(CaseDefinition.CaseId), "the stamp teaches the street");
            Assert.IsTrue(File.Exists(StreetStore.PathsFor(_directory).Latest), "street.json beside the case save");
            Assert.IsTrue(StreetOnDisk().HasSolved(CaseDefinition.CaseId));
            Assert.IsTrue(Director.Street.BoothLamp.enabled, "the booth lamp comes on with the stamp");
            if (Director.Cast != null)
            {
                Assert.AreEqual(JunePlacement.Porch, Director.Cast.JuneMark, "the reopening keeps June on the porch");
            }

            // Reload: the world applies the street on load, after the case is closed.
            yield return Load();
            AssertStreetRemembers("after reload");
            yield return Continue();
            AssertStreetRemembers("in play");
            // The street remembers in words (S003a): every friend greets Pip with a changed line.
            yield return AssertGreets(Speaker.June, Lines.JuneRemembers, "June talks by her treehouse, with a new hello");
            yield return AssertGreets(Speaker.Bea, Lines.BeaRemembers, "Bea, with the owl on her bike");
        }

        /// <summary>Talks to <paramref name="speaker"/> and checks the bubble plays <paramref name="key"/> with its
        /// vignette and words, and that the talk leaves the case as it was.</summary>
        private static IEnumerator AssertGreets(Speaker speaker, string key, string message)
        {
            var before = Director.State;
            Director.Perform(CaseAction.Talk(speaker));
            yield return Wait(0.3f);
            var dialogue = Director.Dialogue;
            Assert.IsTrue(dialogue.IsOpen, message);
            Assert.AreEqual(key, dialogue.CurrentLine.Key, message);
            Assert.AreEqual(speaker, dialogue.CurrentLine.Speaker, message);
            Assert.AreEqual(Localizer.Get(key), dialogue.CurrentText, message + ": the words under the pictures");
            Assert.IsTrue(dialogue.ShowsVignette, message + ": its vignette plays");
            Assert.AreEqual(C01Vignettes.For(key).VignetteId, dialogue.Vignette.Current.Id, message);
            Assert.AreSame(before, Director.State, message + ": a greeting changes no case fact");
            dialogue.Skip();
            yield return Frames(2);
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator ATalkAfterARememberedLoadPlaysTheChangedGreeting()
        {
            WriteCaseSave(StampedState(), CaseAnchors.ArrivalPorch);
            yield return Load();
            AssertStreetRemembers("stamped save");
            yield return Continue();
            Assert.IsTrue(Director.Street.WorldView.Remembers(CaseDefinition.CaseId));
            yield return AssertGreets(Speaker.Bea, Lines.BeaRemembers, "Bea");
            yield return AssertGreets(Speaker.Vale, Lines.ValeRemembers, "Mr. Vale");
            yield return AssertGreets(Speaker.June, Lines.JuneRemembers, "June");
            yield return AssertGreets(Speaker.Kit, Lines.KitRemembers, "Aunt Kit at her open booth");
            yield return AssertGreets(Speaker.Kit, Lines.KitRemembers, "and again: the greeting is how she talks now");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ReplayKeepsTheStreetButCase01StartsWholeUntilItsStamp()
        {
            // A stamped save from before the street file existed: the first load teaches the street (migration).
            WriteCaseSave(StampedState(), CaseAnchors.ArrivalPorch);
            Assert.IsFalse(File.Exists(StreetStore.PathsFor(_directory).Latest));
            yield return Load();
            Assert.IsTrue(StreetOnDisk().HasSolved(CaseDefinition.CaseId), "migrated from no file");
            AssertStreetRemembers("stamped save");
            yield return Continue();

            Director.Replay();
            yield return Frames(5);
            Assert.AreEqual(0, Director.State.Count, "replaying resets the case");
            Assert.IsTrue(Director.Street.State.HasSolved(CaseDefinition.CaseId), "the street keeps its memory");
            Assert.IsTrue(StreetOnDisk().HasSolved(CaseDefinition.CaseId), "and so does its file");
            AssertStreetResting("replay");

            // Mid-replay, a reload keeps both: the street remembers, the replayed case's memories rest.
            yield return Do(A(CaseActionKind.HearKnocks), CaseAction.Talk(Speaker.Bea));
            yield return Load();
            Assert.IsTrue(Director.Street.State.HasSolved(CaseDefinition.CaseId));
            AssertStreetResting("replay after reload");
            yield return Continue();

            // The greetings rest with the other memories: the replay hears the first evening's lines.
            Assert.IsFalse(Director.Street.WorldView.Remembers(CaseDefinition.CaseId));
            Director.Perform(CaseAction.Talk(Speaker.Bea));
            yield return Wait(0.3f);
            Assert.AreEqual(Lines.BeaRepeat, Director.Dialogue.CurrentLine.Key, "Bea tells the replay what she saw");
            Director.Dialogue.Skip();
            yield return Frames(2);

            // Solving it again brings the lamp and the sticker back at the stamp, and the rest on the next load.
            var replayed = Director.State;
            foreach (var action in CanonicalActions())
            {
                if (replayed.Apply(action).Gained.Count > 0)
                {
                    yield return Do(action);
                    replayed = Director.State;
                }
            }

            if (Director.Cutscenes.Playing)
            {
                Director.Cutscenes.Player.SkipNow();
            }

            Assert.IsTrue(Director.State.Has(CaseFact.CaseStamped));
            Assert.IsTrue(Director.Street.BoothLamp.enabled);
            yield return Load();
            AssertStreetRemembers("replay stamped again");
            yield return Continue();
            yield return AssertGreets(Speaker.Vale, Lines.ValeRemembers, "the greetings come back with the stamp");
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator AConfirmedNewGameForgetsTheStreet()
        {
            WriteCaseSave(StampedState(), CaseAnchors.ArrivalPorch);
            yield return Load();
            AssertStreetRemembers("before");
            var ui = GameBootstrap.Instance.Ui;
            ui.NewGameButton.onClick.Invoke();
            yield return Frames(2);
            Assert.IsTrue(Director.Notice.IsOpen, "New game asks first");
            Assert.IsTrue(Director.Street.State.HasSolved(CaseDefinition.CaseId), "nothing changes before the answer");
            Director.Notice.PrimaryButton.onClick.Invoke();
            yield return Frames(5);
            Assert.IsTrue(Director.Street.State.IsEmpty);
            Assert.IsTrue(StreetOnDisk().IsEmpty);
            Assert.IsFalse(Director.Street.WorldView.Any);
            Assert.IsFalse(Director.Street.BoothLamp.enabled);
            Assert.IsNull(StreetGreetings.For(Speaker.Bea, Director.Street.WorldView), "the greetings are forgotten too");
            if (Director.Cutscenes.Playing)
            {
                Director.Cutscenes.Player.SkipNow();
                yield return Frames(3);
            }

            Director.Perform(CaseAction.Talk(Speaker.Bea));
            yield return Wait(0.3f);
            Assert.AreEqual(Lines.BeaSawCape, Director.Dialogue.CurrentLine.Key, "Pip is the new kid again");
            Director.Dialogue.Skip();
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator ContinueShowsAPictureRecapOfAMidCaseSave()
        {
            yield return Load();
            Assert.IsFalse(Director.Recap.Visible, "no save, no recap");

            var state = CaseState.Empty;
            foreach (var action in CanonicalActions().Take(7))
            {
                state = state.Apply(action).State; // the witnesses, the bench, the cloth, the attic, the sill
            }

            WriteCaseSave(state, CaseAnchors.Attic);
            yield return Load();
            var recap = Director.Recap;
            Assert.IsTrue(recap.Visible, "Continue shows the recap");
            Assert.IsTrue(GameBootstrap.Instance.Ui.ContinueButton.gameObject.activeSelf);
            Assert.AreEqual(NotebookView.PlacePart(CaseAnchors.Attic), recap.PlacePart, "where Pip was");
            var open = N.FirstOpen(state);
            Assert.IsNotNull(open);
            CollectionAssert.AreEqual(ContinueRecap.QuestionChips(open.Id), recap.ChipParts, "the open question's chips");
            Assert.IsTrue(recap.Line.gameObject.activeSelf);
            Assert.AreEqual(Localizer.Get(open.QuestionKey), recap.Line.text, "its words under the pictures");
            Assert.IsFalse(recap.Stamp.activeSelf);
            Assert.AreEqual(1, recap.FilledDots, "chapter 1: nothing answered yet");
            int found = N.Found(state, true).Count;
            Assert.AreEqual(System.Math.Min(found, ContinueRecap.MaxStickerIcons), recap.StickerIds.Count);
            CollectionAssert.Contains(recap.Texts.ToList(), Localizer.Format(recap.Recap.StickersKey, found));
            AssertNoAnswerText(recap);

            // Continue resumes at that place; back on the title the recap follows the save.
            yield return Continue();
            Assert.AreEqual(CaseAnchors.Attic, Director.AnchorId);
            GameBootstrap.Instance.Flow.Pause();
            yield return Frames(2);
            GameBootstrap.Instance.Flow.QuitToTitle();
            yield return Frames(3);
            Assert.IsTrue(Director.Recap.Visible);
            AssertNoAnswerText(Director.Recap);
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator ContinueShowsTheStampForAClosedSave()
        {
            WriteCaseSave(StampedState(), CaseAnchors.ArrivalPorch);
            yield return Load();
            var recap = Director.Recap;
            Assert.IsTrue(recap.Visible);
            Assert.IsTrue(recap.Stamp.activeSelf, "Case closed");
            Assert.IsEmpty(recap.ChipParts, "no open question");
            Assert.IsFalse(recap.Line.gameObject.activeSelf);
            Assert.AreEqual(ContinueRecap.ChapterCount, recap.FilledDots);
            Assert.AreEqual(NotebookView.PlacePart(CaseAnchors.ArrivalPorch), recap.PlacePart);
            AssertNoAnswerText(recap);
        }

        private static void AssertNoAnswerText(ContinueRecapView recap)
        {
            var texts = recap.Texts.ToList();
            Assert.IsNotEmpty(texts);
            foreach (var question in N.Questions)
            {
                string answer = Localizer.Get(question.AnswerKey);
                foreach (string text in texts)
                {
                    StringAssert.DoesNotContain(answer, text, "the recap never shows an answer");
                }
            }
        }
    }
}
