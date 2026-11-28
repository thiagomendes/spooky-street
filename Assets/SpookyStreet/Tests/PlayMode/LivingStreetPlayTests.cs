using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.LivingStreet;
using SpookyStreet.Core.Save;
using SpookyStreet.Core.World;
using SpookyStreet.LivingStreet;
using SpookyStreet.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.LivingStreet;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// The living street's test slice in the running game (docs/living-street-slice.md): the walkers loop and never come
    /// within 1.5 m of a clue, the heart holds at most three, a door reacts to Pip's ring with its gag and remembers
    /// the visit, and the lantern table lights Pip's porch so groups ring her bell and she hands out a treat. The crowd's
    /// own frame cost is logged for the report. Fresh temporary save folder per test.
    /// </summary>
    public class LivingStreetPlayTests : InputTestFixture
    {
        private string _directory;

        public override void Setup()
        {
            base.Setup();
            InputSystem.AddDevice<Keyboard>();
            Localizer.ResetForTests();
            _directory = CaseDirector.UseTemporarySaveDirectory();
        }

        public override void TearDown()
        {
            Time.timeScale = 1f;
            base.TearDown();
        }

        private static CaseDirector Director => GameBootstrap.Instance.Director;

        private static LivingStreetStage Stage => Director.Living;

        private static IEnumerator StartGame()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(GameBootstrap.Instance);
            GameBootstrap.Instance.Ui.NewGameButton.onClick.Invoke();
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            Assert.IsNotNull(Stage, "the living street is built with the case");
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.realtimeSinceStartup + seconds;
            while (Time.realtimeSinceStartup < until)
            {
                var dialogue = Director.Dialogue;
                if (dialogue.IsOpen)
                {
                    dialogue.Skip();
                }

                yield return null;
            }
        }

        private static void PlacePip(Vector3 at, Vector3 lookAt)
        {
            var pip = GameBootstrap.Instance.Pip;
            pip.Teleport(at);
            Vector3 to = lookAt - at;
            to.y = 0f;
            pip.transform.rotation = Quaternion.LookRotation(to, Vector3.up);
        }

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator WalkersLoopAndNeverComeNearAClue()
        {
            yield return StartGame();
            var crowd = Stage.Crowd;
            Assert.That(crowd.Bodies.Count, Is.InRange(6, 8));
            PlacePip(new Vector3(-12f, 0.1f, -8f), new Vector3(0f, 0f, 0f)); // out of the walkers' way

            var lastMetres = new float[crowd.Clock.GroupCount];
            var laps = new int[crowd.Clock.GroupCount];
            var start = new List<Vector3>();
            var farthest = new float[crowd.Bodies.Count];
            foreach (var body in crowd.Bodies)
            {
                start.Add(body.Root.position);
            }

            float worst = float.MaxValue;
            string worstAt = null;
            int mostInHeart = 0;
            double crowdMs = 0.0;
            int frames = 0;
            float maxMs = 0f;
            Time.timeScale = 5f;
            float until = Time.realtimeSinceStartup + 36f;
            while (Time.realtimeSinceStartup < until)
            {
                yield return null;
                if (Director.Dialogue.IsOpen)
                {
                    Director.Dialogue.Skip();
                }

                int inHeart = 0;
                for (int i = 0; i < crowd.Bodies.Count; i++)
                {
                    var body = crowd.Bodies[i];
                    Vector3 p = body.Root.position;
                    farthest[i] = Mathf.Max(farthest[i], (p - start[i]).magnitude);
                    float d = LivingStreetRules.ClueDistance(p.x, p.z, out string clue);
                    if (d < worst)
                    {
                        worst = d;
                        worstAt = $"{body.Root.name} at {p} near {clue}";
                    }

                    inHeart += LivingStreetLayout.InHeart(p.x, p.z) ? 1 : 0;
                }

                mostInHeart = Mathf.Max(mostInHeart, inHeart);
                Assert.LessOrEqual(inHeart, CrowdClock.CaseHeartCap, "the heart holds at most three walkers during case 01 (D34)");
                for (int g = 0; g < laps.Length; g++)
                {
                    float m = crowd.Clock.LapMetres(g);
                    if (m + 5f < lastMetres[g])
                    {
                        laps[g]++;
                    }

                    lastMetres[g] = m;
                }

                crowdMs += crowd.LastMs;
                maxMs = Mathf.Max(maxMs, crowd.LastMs);
                frames++;
            }

            Time.timeScale = 1f;
            Debug.Log($"[LivingStreetPlayTests] crowd frame cost: mean {crowdMs / Mathf.Max(1, frames):F3} ms, max {maxMs:F3} ms over " +
                      $"{frames} frames, {crowd.Bodies.Count} walkers, tier {Stage.Tier}; closest to a clue {worst:F2} m " +
                      $"({worstAt}); most in the heart {mostInHeart}; laps {string.Join(", ", laps)}");
            Assert.GreaterOrEqual(worst, LivingStreetRules.ClueClearance, worstAt);
            for (int g = 0; g < laps.Length; g++)
            {
                Assert.GreaterOrEqual(laps[g], 1, crowd.Clock.RouteOf(g).Id + " walked its whole loop");
            }

            int moved = 0;
            for (int i = 0; i < crowd.Bodies.Count; i++)
            {
                // The farthest a walker got from where it started (a loop brings it back home, so not the end point).
                moved += farthest[i] > 5f ? 1 : 0;
            }

            Assert.AreEqual(crowd.Bodies.Count, moved, "every walker moves");
            Assert.Greater(Stage.Frames.Count, 100, "the frame readout records while playing");
            Assert.Less(crowdMs / Mathf.Max(1, frames), 2.0, "the crowd stays a small part of a frame even in the editor");

            // Walkers never block Pip: they carry no colliders. Each is the crowd kit's merged body: one draw call.
            foreach (var body in crowd.Bodies)
            {
                Assert.IsEmpty(body.Root.GetComponentsInChildren<Collider>(), body.Root.name + " must not collide");
                Assert.IsTrue(body.IsKit, body.Root.name + " uses the crowd kit, not a placeholder");
                Assert.AreEqual(1, body.DrawCalls, body.Root.name + ": one merged skinned mesh");
                int drawn = 0;
                foreach (var skinned in body.Root.GetComponentsInChildren<SkinnedMeshRenderer>())
                {
                    drawn += skinned.enabled ? 1 : 0;
                }

                Assert.LessOrEqual(drawn, 1, body.Root.name + ": at most one renderer drawn (LOD0 or LOD1)");
                int lod0 = CrowdKitBodyFactory.Triangles(body, 0);
                int lod1 = CrowdKitBodyFactory.Triangles(body, 1);
                Debug.Log($"[LivingStreetPlayTests] {body.Root.name}: LOD0 {lod0} triangles, LOD1 {lod1}");
                Assert.That(lod0, Is.InRange(1000, SpookyStreet.Core.Crowd.CrowdKit.MaxLod0Triangles));
                Assert.That(lod1, Is.InRange(1, SpookyStreet.Core.Crowd.CrowdKit.MaxLod1Triangles));
            }
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ADoorAnswersPipsRingWithItsGagAndRemembersHer()
        {
            yield return StartGame();
            var fogg = LivingStreetLayout.Door(LivingStreetLayout.Fogg);
            PlacePip(new Vector3(fogg.StandX, 0.1f, fogg.StandZ - 0.3f), new Vector3(fogg.DoorX, 1f, fogg.DoorZ));
            yield return Wait(0.6f);
            Assert.IsTrue(Director.Targets.ExtraActive, "the bell is the Use target at the foot of the steps");
            Assert.AreEqual(Keys.VerbRing, Director.Targets.VerbKey);

            var rig = Stage.Set.Doors[0];
            Assert.AreEqual(fogg.Id, rig.Door.Id);
            Assert.IsFalse(rig.Figures[0].Drawn, "Mr. Fogg waits inside until the door opens");
            Director.Targets.Interact();
            Assert.AreEqual(1, Stage.PipRings);
            Assert.AreEqual(1, Stage.State.Visits(fogg.Id));
            yield return Wait(fogg.Gag.OpenSeconds + 0.4f);
            float swing = rig.Hinge.localEulerAngles.y;
            Assert.Greater(swing, 60f, "the door swings open");
            Assert.IsTrue(rig.Figures[0].Drawn, "Mr. Fogg stands in the doorway");
            Assert.IsFalse(Director.Targets.ExtraActive && Director.Targets.VerbKey == Keys.VerbRing, "an answering door is not rung again");

            yield return Wait(NeighbourDoors.HoldSeconds + NeighbourDoors.CloseSeconds + 0.5f);
            Assert.AreEqual(DoorPhase.Closed, Stage.Doors.Phase(fogg.Id));
            Assert.AreEqual(0f, Stage.Doors.SwingDegrees(fogg.Id));
            Assert.IsFalse(rig.Figures[0].Drawn);

            // The visit is in its own file beside the case save, and a second ring greets Pip by name.
            string file = NeighbourhoodStore.PathsFor(_directory).Latest;
            Assert.IsTrue(File.Exists(file), "neighbourhood.json is written");
            StringAssert.Contains("street.free.door.fogg", File.ReadAllText(file));
            Assert.IsTrue(Stage.Ring(fogg));
            Assert.AreEqual(2, Stage.State.Visits(fogg.Id));

            // The notebook's street page shows Mr. Fogg's face, and only his (no slot for neighbours not yet met).
            Director.OpenNotebook();
            yield return null;
            var notebook = Director.Notebook;
            Assert.IsNotNull(notebook.StreetPage, "the street page appears once Pip has rung a bell");
            Assert.AreEqual(1, notebook.StreetFaces.Count);
            Assert.IsNull(notebook.StreetLantern, "no lantern drawn before she folds one");
            notebook.Close();
        }

        [UnityTest]
        [Timeout(240000)]
        public IEnumerator TheLanternTableLightsPipsPorchAndGroupsRingHerBell()
        {
            yield return StartGame();
            Assert.IsFalse(Stage.Set.PorchLanternLit, "no lantern before she folds one");

            // Use at the table opens the lantern screen; Pip picks a bat in green and folds it.
            PlacePip(new Vector3(LivingStreetLayout.TableUseX, 0.1f, LivingStreetLayout.TableUseZ + 0.3f),
                new Vector3(LivingStreetLayout.TableX, 1f, LivingStreetLayout.TableZ));
            yield return Wait(0.5f);
            Assert.AreEqual(Keys.VerbMake, Director.Targets.VerbKey);
            Director.Targets.Interact();
            yield return null;
            var screen = Stage.LanternScreen;
            Assert.IsTrue(screen.IsOpen);
            Assert.IsTrue(Director.ModalOpen, "the screen holds the controls");
            // Every shape has its name under the card, readable (the text fits its box).
            foreach (var pair in screen.ShapeNames)
            {
                Assert.IsTrue(pair.Value.gameObject.activeInHierarchy, pair.Key + " name shown");
                Assert.IsNotEmpty(pair.Value.text, pair.Key + " name");
                Assert.LessOrEqual(pair.Value.preferredHeight, pair.Value.rectTransform.rect.height + 1f, pair.Key + " name fits");
            }

            // Gamepad and keyboard: the d-pad walks every shape and colour and presses them (touch is the same buttons).
            var nav = screen.Navigation;
            nav.FocusOn(LanternCraft.FocusShape(LanternShape.Bat));
            foreach (var shape in LanternCraft.Shapes)
            {
                Assert.AreEqual(LanternCraft.FocusShape(shape), nav.Focus);
                nav.Submit();
                Assert.AreEqual(shape, screen.Shape, "Submit picks " + shape);
                nav.Navigate(UiDirection.Right);
            }

            nav.FocusOn(LanternCraft.FocusColour(LanternColour.Orange));
            foreach (var colour in LanternCraft.Colours)
            {
                Assert.AreEqual(LanternCraft.FocusColour(colour), nav.Focus);
                nav.Submit();
                Assert.AreEqual(colour, screen.Colour, "Submit picks " + colour);
                nav.Navigate(UiDirection.Right);
            }

            // Touch: the bat in green, folded.
            screen.ShapeButton(LanternShape.Bat).onClick.Invoke();
            screen.ColourButton(LanternColour.Green).onClick.Invoke();
            screen.FoldButton.onClick.Invoke();
            yield return null;
            Assert.IsFalse(screen.IsOpen);
            Assert.IsTrue(Stage.State.LanternLit);
            Assert.AreEqual(new PipLantern(LanternShape.Bat, LanternColour.Green), Stage.State.Lantern.Value);
            Assert.IsTrue(Stage.Set.PorchLanternLit, "it hangs lit on her porch");
            var paper = SpookyStreet.World.ModelKit.FindDeep(Stage.Set.PorchLantern, SpookyStreet.Core.World.LanternArt.PaperGlow);
            Assert.IsNotNull(paper, "the porch lantern is the lantern art's bat (its paper child)");
            Assert.AreEqual(new PipLantern(LanternShape.Bat, LanternColour.Green), Stage.Set.ShownLantern.Value);

            // Pip waits on her porch; a group comes through the heart and rings her bell.
            PlacePip(new Vector3(0f, 0.62f, -13.4f), new Vector3(0f, 0f, 0f));
            yield return Wait(0.3f);
            Assert.IsTrue(Stage.PorchOpen, "the radio points elsewhere, so groups may visit");
            Time.timeScale = 6f;
            float until = Time.realtimeSinceStartup + 60f;
            while (Stage.Crowd.Clock.WaitingAtPorch < 0 && Time.realtimeSinceStartup < until)
            {
                if (Director.Dialogue.IsOpen)
                {
                    Director.Dialogue.Skip();
                }

                yield return null;
            }

            Time.timeScale = 1f;
            Assert.GreaterOrEqual(Stage.Crowd.Clock.WaitingAtPorch, 0, "a group waits at Pip's steps");
            Assert.Greater(Stage.PorchRings, 0, "the group rang her bell");
            Assert.IsNotNull(Stage.Set.Hook, "the lantern art's porch hook");
            Assert.IsNotNull(Stage.Set.BellButton, "the push button on the hook post (never a bell shape)");
            Assert.IsNotNull(Stage.Set.TableArt, "the lantern art's craft table on the Green");
            yield return Wait(0.3f);
            Assert.IsTrue(Director.Targets.ExtraActive);
            Assert.AreEqual(Keys.VerbTreat, Director.Targets.VerbKey);
            Director.Targets.Interact();
            Assert.AreEqual(1, Stage.TreatsGiven);
            yield return Wait(CrowdClock.CheerSeconds + 0.5f);
            Assert.AreEqual(-1, Stage.Crowd.Clock.WaitingAtPorch, "the group cheers and walks on");

            // Replay case keeps the lantern (D31); the file holds it beside the case save.
            Director.Replay();
            yield return null;
            Assert.IsTrue(Stage.Set.PorchLanternLit, "Replay keeps the street's lantern");
            StringAssert.Contains("\"lanternShape\": \"bat\"", File.ReadAllText(NeighbourhoodStore.PathsFor(_directory).Latest));
        }
    }
}
