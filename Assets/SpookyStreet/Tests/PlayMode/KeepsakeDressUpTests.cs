using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Keepsakes;
using SpookyStreet.Core.Save;
using SpookyStreet.Core.World;
using SpookyStreet.Localization;
using SpookyStreet.Player;
using SpookyStreet.Save;
using SpookyStreet.UI;
using SpookyStreet.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// S004 A2 keepsakes in play: the 10th bat wobbles the notebook disc once and its page assembles once in the
    /// notebook; the mobile hangs exactly the found bats and lights its lantern at 10; the trunk (a solid box, offered
    /// only with an earned piece) opens a screen with only earned cards that dresses Pip at once; the costume is saved,
    /// shown on the title after a reload and cleared by a confirmed New game; a costume remark plays once; the Continue
    /// line counts the bats. Fresh temporary save folder per test.
    /// </summary>
    public class KeepsakeDressUpTests : InputTestFixture
    {
        private string _directory;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            InputSystem.AddDevice<Gamepad>();
            Localizer.ResetForTests();
            _directory = CaseDirector.UseTemporarySaveDirectory();
        }

        private static CaseDirector Director => GameBootstrap.Instance.Director;

        private static PaperBatCollectibles Bats => Director.Keepsakes;

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
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
            }
        }

        /// <summary>Into play without New game's question (keepsakes on disk would make it ask, and clear them).</summary>
        private static IEnumerator Play()
        {
            GameBootstrap.Instance.Flow.StartNewGame();
            yield return Frames(10);
            if (Director.Cutscenes.Playing)
            {
                Director.Cutscenes.Player.SkipNow();
                yield return Frames(3);
            }

            Assert.AreEqual(GameState.Playing, GameBootstrap.Instance.Flow.State);
            GameBootstrap.Instance.Ui.FirstPrompt.SetActive(false);
        }

        private KeepsakeStore Store()
        {
            var store = new KeepsakeStore(new DiskSaveFileSystem(), _directory);
            store.Load();
            return store;
        }

        private KeepsakeState OnDisk() => Store().State;

        private void WriteKeepsakes(int count, params CostumePiece[] worn)
        {
            var store = Store();
            for (int i = 1; i <= count; i++)
            {
                store.Find(PaperBatLayout.IdFor(i));
            }

            foreach (var piece in worn)
            {
                store.Wear(piece.Id, true);
            }
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator TheTenthBatWobblesTheDiscOnceAndItsPageAssemblesOnce()
        {
            WriteKeepsakes(9);
            yield return Load();
            yield return Play();
            Assert.AreEqual(9, Director.Mobile.BatsShown, "nine bats hang on the mobile");
            Assert.IsFalse(Director.Mobile.LanternLit(1));
            Assert.AreEqual(0, Bats.State.PagesCompleted);

            Assert.IsTrue(Bats.Collect(PaperBatLayout.IdFor(10)), "the tenth bat");
            yield return Frames(3);
            Assert.AreEqual(1, Bats.State.PagesCompleted);
            CollectionAssert.AreEqual(new[] { CostumePieces.StarCape }, Bats.State.EarnedPieces, "the cape is earned");
            Assert.AreEqual(10, Director.Mobile.BatsShown);
            Assert.IsTrue(Director.Mobile.LanternLit(1), "ring 1 closes round its lit lantern");
            Assert.IsTrue(Director.NotebookDiscWobbling, "the disc wobbles once");
            CollectionAssert.AreEqual(new[] { 1 }, Director.KeepsakePages.Pending);
            Assert.IsFalse(Director.Notice.IsOpen, "no pop-up");
            Assert.IsFalse(Director.ModalOpen, "play goes on");
            yield return Wait(1.2f);
            Assert.IsFalse(Director.NotebookDiscWobbling, "the wobble ends");

            // The 11th bat completes nothing.
            Assert.IsTrue(Bats.Collect(PaperBatLayout.IdFor(11)));
            CollectionAssert.AreEqual(new[] { 1 }, Director.KeepsakePages.Pending);

            Director.OpenNotebook();
            yield return Frames(2);
            var notebook = Director.Notebook;
            Assert.GreaterOrEqual(notebook.KeepsakePageIndex, 0, "the keepsakes page");
            Assert.AreEqual(notebook.KeepsakePageIndex, notebook.CurrentPage, "it opens on the keepsakes page");
            Assert.IsTrue(notebook.Assembling, "page 1 assembles from its ten bats");
            Assert.AreEqual(1, notebook.AssemblingPage);
            Assert.AreEqual(1, notebook.SketchImages.Count, "only the earned page, no empty frames");
            Assert.AreEqual(1, notebook.BatStackCount, "the eleventh bat waits in the stack");
            Assert.AreEqual("1 paper bat", notebook.BatStackLabel.text);
            yield return Wait(NotebookView.AssemblySeconds + 0.5f);
            Assert.IsFalse(notebook.Assembling);
            Assert.IsTrue(notebook.SketchImages[0].enabled, "the drawing is whole");
            Assert.AreEqual(Localizer.Get(Keys.Keepsakes.PageLanterns), notebook.SketchCaptions[0].text);
            Assert.AreEqual(1, notebook.AssembliesPlayed);
            notebook.Close();
            yield return Frames(3);

            Director.OpenNotebook();
            yield return Frames(2);
            Assert.IsFalse(Director.Notebook.Assembling, "once only");
            Assert.AreEqual(1, Director.Notebook.AssembliesPlayed);
            Director.Notebook.Close();
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator TheMobileHangsExactlyTheFoundBats()
        {
            WriteKeepsakes(12);
            yield return Load();
            var mobile = Director.Mobile;
            Assert.IsTrue(mobile.HasModel, "BatMobile.fbx is spawned");
            Assert.AreEqual(12, mobile.BatsShown, "on the title too: one bat per find");
            for (int hook = 1; hook <= KeepsakeArt.MobileHooks; hook++)
            {
                Assert.AreEqual(hook <= 12, mobile.HookVisible(hook), "hook " + hook);
                Assert.AreEqual(hook <= 12, mobile.BatOn(hook) != null && mobile.BatOn(hook).activeSelf, "bat on hook " + hook);
            }

            Assert.IsTrue(mobile.LanternLit(1));
            Assert.IsFalse(mobile.LanternLit(2));
            Vector3 at = mobile.Model.position;
            Assert.AreEqual(KeepsakePorch.MobileY, at.y, 0.01f, "hangs from its pole on the porch");
            var bounds = ModelKit.RendererBounds(mobile.Model.gameObject);
            Assert.Greater(bounds.min.y, KeepsakePorch.DeckY + 1.4f, "Pip walks under it: " + bounds.min.y);
            Debug.Log($"[KeepsakeDressUp] mobile bounds {bounds.min:F2}..{bounds.max:F2}");

            yield return Play();
            Assert.IsTrue(Bats.Collect(PaperBatLayout.IdFor(13)));
            Assert.AreEqual(13, mobile.BatsShown);
            Assert.IsTrue(mobile.HookVisible(13));
        }

        [UnityTest]
        [Timeout(150000)]
        public IEnumerator TheTrunkDressesPipAndTheCostumeIsSavedAndShownOnTheTitle()
        {
            WriteKeepsakes(30);
            yield return Load();
            var costume = Director.Costume;
            Assert.IsNotNull(costume);
            if (!costume.CanDress)
            {
                Assert.Ignore("Pip's model is not committed.");
            }

            Assert.IsFalse(costume.CapeStarred);
            yield return Play();
            var trunk = Director.Trunk;
            Assert.IsTrue(trunk.HasModel, "DressUpTrunk.fbx is spawned");

            // A1 issue 4: the trunk is a real box in play: a ray from above lands on its top, and Pip cannot walk in.
            Physics.SyncTransforms();
            var top = new Vector3(KeepsakePorch.TrunkX, KeepsakePorch.TrunkY + 1.5f, KeepsakePorch.TrunkZ);
            int mask = Physics.DefaultRaycastLayers & ~(1 << GameBootstrap.Instance.Pip.gameObject.layer);
            Assert.IsTrue(Physics.Raycast(top, Vector3.down, out var hit, 2f, mask, QueryTriggerInteraction.Ignore));
            Assert.AreEqual(trunk.Collider, hit.collider, "the trunk's own collider: " + hit.collider.name);
            Assert.AreEqual(KeepsakePorch.TrunkY + KeepsakeArt.TrunkSize, hit.point.y, 0.02f);

            // Offered only near its front, as "Dress up".
            var pip = GameBootstrap.Instance.Pip;
            var spot = KeepsakePorch.DressingSpot;
            pip.Teleport(new Vector3(spot.X, spot.Y, spot.Z));
            pip.transform.rotation = Quaternion.Euler(0f, 180f, 0f);
            yield return Frames(10);
            Assert.IsTrue(Director.Targets.ExtraActive, "the trunk is the Use target");
            Assert.AreEqual(Keys.Verbs.DressUp, Director.Targets.VerbKey);
            Director.Targets.Interact();
            yield return Wait(0.6f);
            var view = Director.DressUp;
            Assert.IsTrue(view.IsOpen, "the trunk's screen");
            Assert.IsTrue(Director.ModalOpen);
            Assert.AreEqual(1f, trunk.LidOpen, 0.01f, "the lid is open");
            Assert.IsTrue(trunk.CameraActive, "the dressing camera frames Pip");
            Assert.IsFalse(trunk.CoverShown, "all three earned: nothing to hide");
            Assert.AreEqual(3, view.VisibleCards);

            view.CardOf(CostumePieces.StarCape).onClick.Invoke();
            view.CardOf(CostumePieces.ShowmanHat).onClick.Invoke();
            view.CardOf(CostumePieces.LanternPail).onClick.Invoke();
            yield return Frames(3);
            Assert.IsTrue(costume.CapeStarred, "the star lining");
            Assert.IsNotNull(costume.Hat);
            Assert.IsTrue(costume.Hat.activeInHierarchy, "the hat");
            Assert.AreEqual(KeepsakeArt.HatBone, costume.Hat.transform.parent.name, "rides her head bone");
            var hatBox = ModelKit.RendererBounds(costume.Hat);
            Vector3 feet = pip.transform.position;
            Debug.Log($"[KeepsakeDressUp] Pip feet {feet:F2}, hat {hatBox.min:F2}..{hatBox.max:F2}");
            Assert.Less(new Vector2(hatBox.center.x - feet.x, hatBox.center.z - feet.z).magnitude, 0.35f, "on her, not beside her");
            Assert.That(hatBox.min.y - feet.y, Is.InRange(0.9f, 1.6f), "on her head");
            Assert.IsTrue(costume.Pail != null && costume.Pail.activeInHierarchy, "the lantern pail");
            Assert.IsFalse(costume.PlainPail.enabled, "in place of the pumpkin pail");
            var pailBox = ModelKit.RendererBounds(costume.Pail);
            Assert.Less(new Vector2(pailBox.center.x - feet.x, pailBox.center.z - feet.z).magnitude, 0.6f, "in her hand");
            Assert.That(pailBox.center.y - feet.y, Is.InRange(0.05f, 0.9f), "hanging from her hand");

            // Taking one off works at once too; the file follows every tap.
            view.CardOf(CostumePieces.LanternPail).onClick.Invoke();
            yield return Frames(2);
            Assert.IsFalse(costume.Pail.activeSelf);
            Assert.IsTrue(costume.PlainPail.enabled, "the pumpkin pail is back");
            var disk = OnDisk();
            Assert.IsTrue(disk.IsWearing(CostumePieces.StarCape.Id) && disk.IsWearing(CostumePieces.ShowmanHat.Id));
            Assert.IsFalse(disk.IsWearing(CostumePieces.LanternPail.Id));

            // Back (gamepad East, Escape) closes the trunk like the close disc.
            view.Navigation.Back();
            yield return Wait(0.6f);
            Assert.IsFalse(view.IsOpen);
            Assert.IsFalse(Director.ModalOpen);
            Assert.IsFalse(trunk.CameraActive);
            Assert.AreEqual(0f, trunk.LidOpen, 0.01f, "the lid shuts");

            // The app closes and opens again: the title shows Pip in her cape and hat.
            yield return Load();
            Assert.AreEqual(GameState.Title, GameBootstrap.Instance.Flow.State);
            Assert.IsTrue(Director.Costume.CapeStarred, "still worn after the reload, on the title");
            Assert.IsTrue(Director.Costume.Hat != null && Director.Costume.Hat.activeInHierarchy);
            Assert.IsTrue(Director.Costume.Pail == null || !Director.Costume.Pail.activeSelf);
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator TheTrunkStaysShutUntilAPieceIsEarnedAndHidesUnearnedPieces()
        {
            WriteKeepsakes(9);
            yield return Load();
            yield return Play();
            var pip = GameBootstrap.Instance.Pip;
            var spot = KeepsakePorch.DressingSpot;
            pip.Teleport(new Vector3(spot.X, spot.Y, spot.Z));
            yield return Frames(10);
            Assert.IsFalse(Director.Targets.ExtraActive, "no empty trunk");

            Assert.IsTrue(Bats.Collect(PaperBatLayout.IdFor(10)));
            yield return Frames(5);
            Assert.IsTrue(Director.Targets.ExtraActive, "the cape is earned: the trunk opens");
            Director.UseTrunk();
            yield return Wait(0.5f);
            Assert.AreEqual(1, Director.DressUp.VisibleCards, "only the cape's card");
            Assert.IsFalse(Director.DressUp.CardOf(CostumePieces.ShowmanHat).gameObject.activeSelf);
            Assert.IsTrue(Director.Trunk.CoverShown, "a cloth hides what is still to be earned");
            Director.DressUp.CloseButton.onClick.Invoke();
            yield return Frames(3);
            Assert.IsFalse(Director.ModalOpen);
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator AConfirmedNewGameTakesTheCostumeOff()
        {
            WriteKeepsakes(30, CostumePieces.StarCape, CostumePieces.ShowmanHat);
            yield return Load();
            var costume = Director.Costume;
            if (!costume.CanDress)
            {
                Assert.Ignore("Pip's model is not committed.");
            }

            Assert.IsTrue(costume.CapeStarred);
            Assert.AreEqual(30, Director.Mobile.BatsShown);
            GameBootstrap.Instance.Ui.NewGameButton.onClick.Invoke();
            yield return Frames(2);
            Assert.IsTrue(Director.Notice.IsOpen);
            Director.Notice.PrimaryButton.onClick.Invoke();
            yield return Frames(5);
            if (KeepsakeRules.NewGameClearsKeepsakes)
            {
                Assert.IsFalse(costume.CapeStarred, "decision 4: the costume goes with the bats");
                Assert.IsFalse(costume.Hat != null && costume.Hat.activeSelf);
                Assert.AreEqual(0, Director.Mobile.BatsShown, "the mobile is bare");
                Assert.AreEqual(0, OnDisk().WornPieces.Count);
            }
        }

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator AFriendRemarksOnTheCostumeOnce()
        {
            WriteKeepsakes(10, CostumePieces.StarCape);
            yield return Load();
            yield return Play();
            var heard = new List<string>();
            yield return Talk(Speaker.June, heard);
            CollectionAssert.Contains(heard, Keys.CaseLines.JuneStarCape, "June notices the star cape");
            Assert.AreEqual(Keys.CaseLines.JuneStarCape, heard[heard.Count - 1], "after her own lines");
            Assert.IsTrue(OnDisk().HasSaid(CostumeRemarks.JuneCape.Id), "saved as said");

            heard.Clear();
            yield return Talk(Speaker.June, heard);
            CollectionAssert.DoesNotContain(heard, Keys.CaseLines.JuneStarCape, "once ever");
            heard.Clear();
            yield return Talk(Speaker.Vale, heard);
            CollectionAssert.DoesNotContain(heard, Keys.CaseLines.ValeLanternPail, "the pail is not earned");
        }

        private static IEnumerator Talk(Speaker speaker, List<string> heard)
        {
            var dialogue = Director.Dialogue;
            Director.Perform(CaseAction.Talk(speaker));
            yield return Wait(0.3f);
            int guard = 0;
            while (dialogue.IsOpen && guard++ < 12)
            {
                heard.Add(dialogue.CurrentLine.Key);
                dialogue.Advance();
                yield return Wait(0.25f);
            }

            Assert.IsFalse(dialogue.IsOpen, speaker + ": the talk ends");
            yield return Wait(InteractCooldownSeconds);
        }

        private const float InteractCooldownSeconds = CaseDirector.InteractCooldown + 0.1f;

        [UnityTest]
        [Timeout(90000)]
        public IEnumerator TheContinueLineCountsTheBatsOnlyOnceOneIsFound()
        {
            var state = CaseState.Empty.Apply(CaseAction.Of(CaseActionKind.HearKnocks)).State;
            var cases = new CaseSaveStore(new DiskSaveFileSystem(), _directory);
            cases.Load();
            Assert.IsTrue(cases.Save(state, CaseAnchors.Plaza));
            yield return Load();
            var craft = GameBootstrap.Instance.Ui.ContinueButton.GetComponent<CraftButton>();
            Assert.IsTrue(GameBootstrap.Instance.Ui.ContinueButton.gameObject.activeSelf);
            Assert.IsFalse(craft.HasDetail, "no bats: no line (never '0 paper bats')");

            WriteKeepsakes(7);
            yield return Load();
            craft = GameBootstrap.Instance.Ui.ContinueButton.GetComponent<CraftButton>();
            Assert.IsTrue(craft.HasDetail);
            StringAssert.Contains("7 paper bats", craft.Detail.text);
            StringAssert.DoesNotContain("30", craft.Detail.text, "a count, never a total");
        }
    }
}
