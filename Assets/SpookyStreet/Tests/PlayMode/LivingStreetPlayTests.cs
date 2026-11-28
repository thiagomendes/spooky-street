using System.Collections;
using System.Collections.Generic;
using System.IO;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.Audio;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Localization;
using SpookyStreet.Core.LivingStreet;
using SpookyStreet.Core.Save;
using SpookyStreet.Core.World;
using SpookyStreet.LivingStreet;
using SpookyStreet.Localization;
using SpookyStreet.World;
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

        /// <summary>A label's text width in canvas units at its full font size on one line (no best-fit shrinking).</summary>
        private static float FullSizeWidth(UnityEngine.UI.Text label, string value)
        {
            var settings = label.GetGenerationSettings(Vector2.zero);
            settings.resizeTextForBestFit = false;
            settings.fontSize = label.fontSize;
            settings.scaleFactor = 1f;
            settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            settings.generateOutOfBounds = true;
            return new TextGenerator().GetPreferredWidth(value, settings);
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
            var heard = new List<string>();
            System.Action<string> listen = heard.Add;
            AudioCues.Requested += listen;
            Director.Targets.Interact();
            AudioCues.Requested -= listen;
            CollectionAssert.Contains(heard, AudioEvents.StreetDoorbell, "the ring is the doorbell's ding-dong");
            CollectionAssert.DoesNotContain(heard, AudioEvents.NoteCorrectGlow, "never the notebook's chime");
            Assert.IsNotNull(Resources.Load<AudioClip>(AudioEvents.CreateCatalog().Get(AudioEvents.StreetDoorbell).ResourcePath(1)),
                "the doorbell's takes ship in the build");
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
        [Timeout(180000)]
        public IEnumerator ALongStayByOneDoorHearsTheGroupsCallOnce()
        {
            yield return StartGame();
            var fogg = LivingStreetLayout.Door(LivingStreetLayout.Fogg);
            PlacePip(new Vector3(fogg.StandX, 0.1f, fogg.StandZ - 0.3f), new Vector3(fogg.DoorX, 1f, fogg.DoorZ));
            int foggRings = 0;
            var bells = new List<string>();
            System.Action<string> happened = what => foggRings += what == "group rang " + fogg.Id ? 1 : 0;
            System.Action<string> listen = id =>
            {
                if (id == AudioEvents.StreetDoorbell)
                {
                    bells.Add(id);
                }
            };
            Stage.Happened += happened;
            AudioCues.Requested += listen;
            Time.timeScale = 6f;
            float until = Time.realtimeSinceStartup + 90f;
            while (foggRings < 3 && Time.realtimeSinceStartup < until)
            {
                if (Director.Dialogue.IsOpen)
                {
                    Director.Dialogue.Skip();
                }

                yield return null;
            }

            Time.timeScale = 1f;
            Stage.Happened -= happened;
            AudioCues.Requested -= listen;
            Debug.Log($"[LivingStreetPlayTests] rings at {fogg.Id}: {foggRings}, group rings {Stage.GroupDoorRings}, " +
                      $"calls heard {Stage.GroupCallsHeard}, bells {bells.Count}");
            Assert.GreaterOrEqual(foggRings, 3, "groups rang Mr. Fogg's door lap after lap");
            Assert.IsTrue(Stage.Calls.Holding(fogg.Id), "his door called once and holds while Pip stays");
            Assert.LessOrEqual(Stage.GroupCallsHeard, LivingStreetLayout.Doors.Count, "each door near her called at most once");
            Assert.AreEqual(Stage.GroupCallsHeard, bells.Count, "a held call rings no bell either");

            // She walks away to her own porch and back: his door may call again.
            PlacePip(new Vector3(0f, 0.62f, -13.4f), Vector3.zero);
            yield return null;
            yield return null;
            Assert.IsFalse(Stage.Calls.Holding(fogg.Id), "walking away lets the door call again");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator TheLaneStandsTheNeighboursOwnHousesDoorsAndLamps()
        {
            yield return StartGame();
            if (!GameBootstrap.Instance.Environment.UsesModels)
            {
                Assert.Ignore("Environment models are not committed.");
            }

            var set = Stage.Set;
            Assert.IsTrue(set.UsesModels);
            Assert.AreEqual(LivingStreetLayout.Doors.Count, set.Doors.Count);
            foreach (var rig in set.Doors)
            {
                var door = rig.Door;
                string id = door.Id;

                // The house: the lane's own model where the placeholder stood, with its LOD group and bounds collider.
                Assert.IsNull(set.StaticRoot.Find(door.House), id + ": the placeholder street-set house is gone");
                Assert.IsNull(set.StaticRoot.Find("House_" + id), id + ": no greybox house");
                Assert.IsNotNull(rig.House, id + ": the house art is spawned");
                Assert.AreEqual(LaneHouseArt.House(id), rig.House.name);
                Assert.AreEqual(set.StaticRoot, rig.House.parent, id + ": culled with the lane's static root");
                Assert.AreEqual(new Vector3(door.X, 0f, door.Z), rig.House.localPosition);
                Assert.IsNotNull(rig.House.GetComponentInChildren<LODGroup>(), id + ": LOD0 near, LOD1 far");
                Assert.IsNotNull(rig.House.GetComponent<BoxCollider>(), id + ": Pip walks round it, not through it");
                Assert.IsNotNull(ModelKit.FindDeep(rig.House, LaneHouseArt.RoomGlow), id + ": its own lit hall and rooms");

                // Its leaf on the rig's hinge, its lamp driven by the rig; the placeholder knob, bell and doorway card dropped.
                var frame = rig.Hinge.parent;
                Assert.AreEqual(LaneHouseArt.DoorLeaf(id), rig.Leaf.name);
                Assert.AreEqual(rig.Hinge, rig.Leaf.parent, id + ": the leaf swings with the hinge");
                Assert.AreEqual(LaneHouseArt.LampGlow, rig.Lamp.name);
                Assert.IsNotNull(ModelKit.FindDeep(frame, LaneHouseArt.PorchLamp(id)), id + ": the lamp model");
                Assert.AreSame(rig.LampRest, rig.Lamp.sharedMaterial, id + ": the lamp at rest");
                foreach (string placeholder in new[] { "Knob", "Leaf", "PorchLamp", "Doorbell", "Doorway" })
                {
                    Assert.IsNull(ModelKit.FindDeep(frame, placeholder), id + ": no placeholder " + placeholder);
                }

                // The neighbours in house clothes from the crowd kit, standing on the landing.
                Assert.AreEqual(door.Gag.Figures, rig.Figures.Count);
                for (int i = 0; i < rig.Figures.Count; i++)
                {
                    Assert.IsTrue(rig.Figures[i].IsKit, id + ": a crowd kit neighbour");
                    Assert.AreEqual(door.WallLocalZ - LivingStreetLayout.NeighbourOut(door), rig.FigureRest[i].z, 1e-4f);
                }

                // The porch light on the fronts that face away from the moon.
                if (LivingStreetLayout.HasPorchLight(door))
                {
                    Assert.IsNotNull(rig.PorchLight, id + ": a porch light");
                    Assert.AreEqual(LightType.Point, rig.PorchLight.type);
                    Assert.AreEqual(LightShadows.None, rig.PorchLight.shadows, id + ": no real-time shadow");
                    Assert.AreEqual(LivingStreetLayout.PorchLight(door, false), rig.PorchLight.intensity, 1e-4f);
                    Assert.AreEqual(LivingStreetLayout.PorchLightRange, rig.PorchLight.range, 1e-4f);
                }
                else
                {
                    Assert.IsNull(rig.PorchLight, id);
                }
            }

            var owlDoor = GameObject.Find("OwlHouseDoorLamp");
            if (owlDoor != null)
            {
                Assert.AreEqual(LivingStreetLayout.OwlDoorLight, owlDoor.GetComponent<Light>().intensity, 1e-4f,
                    "the lane's porch lights are measured against the owl house's door lamp");
            }

            // Nana Pearl answers: her lamp and her porch light warm while she is in the doorway, then cool again.
            var pearl = LivingStreetLayout.Door(LivingStreetLayout.Pearl);
            LivingStreetSet.DoorRig pearlRig = null;
            foreach (var rig in set.Doors)
            {
                pearlRig = rig.Door.Id == pearl.Id ? rig : pearlRig;
            }

            PlacePip(new Vector3(pearl.StandX, 0.1f, pearl.StandZ - 0.3f), new Vector3(pearl.DoorX, 1f, pearl.DoorZ));
            yield return Wait(0.3f);
            float free = Time.realtimeSinceStartup + 8f;
            while (Stage.Doors.Busy(pearl.Id) && Time.realtimeSinceStartup < free)
            {
                yield return Wait(0.1f); // a walking group may be at her door
            }

            Assert.IsTrue(Stage.Ring(pearl));
            yield return Wait(pearl.Gag.OpenSeconds + 0.3f);
            Assert.IsTrue(Stage.Doors.NeighbourShown(pearl.Id));
            Assert.AreSame(pearlRig.LampBright, pearlRig.Lamp.sharedMaterial, "her lamp warms");
            Assert.AreEqual(LivingStreetLayout.PorchLight(pearl, true), pearlRig.PorchLight.intensity, 1e-4f, "her porch light warms");
            yield return Wait(NeighbourDoors.HoldSeconds + NeighbourDoors.CloseSeconds + 0.5f);
            Assert.AreEqual(DoorPhase.Closed, Stage.Doors.Phase(pearl.Id));
            Assert.AreSame(pearlRig.LampRest, pearlRig.Lamp.sharedMaterial);
            Assert.AreEqual(LivingStreetLayout.PorchLight(pearl, false), pearlRig.PorchLight.intensity, 1e-4f);
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

            // Every name, in English and in Portuguese, fits on one line at full size with paper to spare on both
            // sides ("Little ghost" used to fill its card, "Fantasminha" was wider than its card).
            foreach (var locale in new[] { "en", "pt-BR" })
            {
                var asset = Resources.Load<TextAsset>("Localization/" + locale);
                Assert.IsNotNull(asset, locale);
                var table = StringTable.Parse(asset.text);
                Localizer.Catalog.SetActive(table);
                yield return null;
                yield return null;
                foreach (var pair in screen.ShapeNames)
                {
                    var label = pair.Value;
                    Assert.IsTrue(table.TryGet(LanternCraft.ShapeKey(pair.Key), out string value), locale + " " + pair.Key);
                    Assert.AreEqual(value, label.text, locale + ": the label shows the catalog's name");
                    float width = FullSizeWidth(label, value);
                    float box = label.rectTransform.rect.width;
                    Debug.Log($"[LivingStreetPlayTests] {locale} '{value}': {width:F1} of {box:F0} units");
                    Assert.LessOrEqual(width, LanternPickerLayout.LabelTextBudget,
                        $"{locale} '{value}' is {width:F1} units, over the {LanternPickerLayout.LabelTextBudget} budget");
                    Assert.GreaterOrEqual((box - width) * 0.5f, LanternPickerLayout.MinLabelMargin, $"{locale} '{value}' margin");
                    Assert.AreEqual(1, label.cachedTextGenerator.lineCount, $"{locale} '{value}' stays on one line");
                    Assert.GreaterOrEqual(label.cachedTextGenerator.fontSizeUsedForBestFit,
                        Mathf.FloorToInt(label.fontSize * label.pixelsPerUnit) - 1, $"{locale} '{value}' is not shrunk");
                }
            }

            Localizer.Catalog.ResetToFallback();
            yield return null;

            // Resting cards keep full paper and full pictures; the chosen one has its plum mat and is larger.
            screen.ChooseShape(LanternShape.Leaf);
            yield return null; // a frame later: nothing resets the chosen size
            foreach (var shape in LanternCraft.Shapes)
            {
                bool chosen = shape == LanternShape.Leaf;
                Assert.AreEqual(chosen, screen.ChosenMat(shape).gameObject.activeSelf, shape + " mat");
                Assert.AreEqual(chosen ? LanternPickerLayout.ChosenScale : 1f, screen.ShapeSlot(shape).localScale.x, 1e-4f, shape + " size");
                Assert.AreEqual(1f, screen.ShapeCard(shape).color.r, 1e-3f, shape + ": full paper");
                foreach (var art in screen.ShapeArt(shape))
                {
                    Assert.AreEqual(1f, art.color.a, 1e-3f, shape + ": the picture is never faded");
                }
            }

            // The board sits inside the screen and below the close disc.
            var boardCorners = new Vector3[4];
            screen.Board.GetWorldCorners(boardCorners);
            var discCorners = new Vector3[4];
            ((RectTransform)screen.CloseButton.transform).GetWorldCorners(discCorners);
            bool apart = boardCorners[2].x < discCorners[0].x || boardCorners[2].y < discCorners[0].y;
            Assert.IsTrue(apart, "the board never runs under the close disc");
            Assert.Greater(boardCorners[0].x, 0f);
            Assert.Less(boardCorners[2].x, Screen.width);

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
