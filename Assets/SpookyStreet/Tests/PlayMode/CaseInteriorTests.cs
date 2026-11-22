using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.World;
using SpookyStreet.Localization;
using SpookyStreet.Player;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Lines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// S002 interior wiring in the scene: the treehouse route and the house are walkable with real colliders
    /// (MAP-01), the canonical playthrough closes the case with the right evidence through Use on each target
    /// (CASE-01), the curious-child order works (CASE-02), and saves after the window entry, the latch and the crank
    /// rebuild the door, June, the mechanism and the owl (SAVE-01). Every test runs against a fresh save folder.
    /// </summary>
    public class CaseInteriorTests : InputTestFixture
    {
        private const string SceneName = "LanternLane";

        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            Localizer.ResetForTests();
            CaseDirector.UseTemporarySaveDirectory();
        }

        private static GameBootstrap Game => GameBootstrap.Instance;

        private static CaseDirector Director => GameBootstrap.Instance.Director;

        private static IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(GameBootstrap.Instance, "scene did not create GameBootstrap");
        }

        private static IEnumerator StartGame()
        {
            yield return LoadGame();
            if (Game.Environment == null || !Game.Environment.UsesModels)
            {
                Assert.Ignore("Environment models are not committed: the interior is not staged.");
            }

            Assert.IsNotNull(Director.House, "the interior stage is built with the models");
            Game.Ui.NewGameButton.onClick.Invoke();
            yield return Frames(10);
            Assert.AreEqual(GameState.Playing, Game.Flow.State);
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

        private IEnumerator Tap(KeyControl key)
        {
            Press(key, queueEventOnly: true);
            yield return null;
            yield return null;
            Release(key, queueEventOnly: true);
            yield return null;
        }

        private static Vector3 At(StagePoint p) => new Vector3(p.X, p.Y, p.Z);

        /// <summary>Puts Pip at <paramref name="stand"/>, facing the target, camera behind; asserts the selected target.</summary>
        private static IEnumerator StandFor(string pointId, Vector3 stand)
        {
            var target = At(CaseStage.Find(pointId).Position);
            Game.Pip.Teleport(stand + Vector3.up * 0.05f);
            Vector3 to = target - stand;
            to.y = 0f;
            Game.Pip.transform.rotation = Quaternion.LookRotation(to.normalized, Vector3.up);
            Game.CameraRig.Orbit.RequestRecenter();
            yield return Wait(0.6f);
            var current = Director.Targets.Current;
            Assert.IsNotNull(current, $"no Use target at {stand} (expected {pointId})");
            Assert.AreEqual(pointId, current.Id, "wrong Use target selected");
            Assert.AreEqual(current.VerbKey, Game.Ui.UseVerbKey, "Use disc shows the verb");
        }

        /// <summary>Stands for the target and presses Use (E).</summary>
        private IEnumerator Use(string pointId, Vector3 stand)
        {
            yield return StandFor(pointId, stand);
            yield return Tap(_keyboard.eKey);
            yield return Frames(2);
        }

        private IEnumerator SkipDialogue()
        {
            if (Director.Dialogue.IsOpen)
            {
                Director.Dialogue.Skip();
            }

            yield return Wait(0.4f);
        }

        private static IEnumerator EnterByWindow()
        {
            // From the bridge end (safe ground) into the dormer: the entry zone reports EnterAttic.
            Game.Pip.Teleport(new Vector3(-3.6f, 2.55f, CaseRouteLayout.BridgeZ));
            yield return Wait(0.5f);
            Game.Pip.Teleport(new Vector3(-2.6f, 2.55f, CaseRouteLayout.BridgeZ));
            yield return Wait(0.4f);
        }

        private static IEnumerator WaitForJune()
        {
            float until = Time.time + 15f;
            while (Director.Cast.JuneWalking && Time.time < until)
            {
                yield return null;
            }

            Assert.IsFalse(Director.Cast.JuneWalking, "June reached her foyer mark");
        }

        // ---------------------------------------------------------------------------------------------- MAP-01

        /// <summary>
        /// Drives Pip's CharacterController (motor off) along the mandatory route with gravity, as a walk at a steady
        /// pace with no jump: every waypoint must be reached, at its height, within the time a walk takes.
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator MandatoryRouteIsWalkableWithTheAuthoredColliders()
        {
            yield return StartGame();
            Director.House.SetDoor(true);
            var pip = Game.Pip;
            var controller = pip.GetComponent<CharacterController>();
            var route = CaseRouteLayout.MandatoryRoute;
            pip.Teleport(new Vector3(route[0].X, route[0].Y + 0.05f, route[0].Z));
            pip.enabled = false;
            yield return null;
            float vy = 0f;
            for (int i = 1; i < route.Length; i++)
            {
                var goal = new Vector3(route[i].X, route[i].Y, route[i].Z);
                float limit = Time.time + 6f;
                while (Time.time < limit)
                {
                    float dt = Mathf.Min(Time.deltaTime, 0.05f);
                    Vector3 to = goal - pip.transform.position;
                    to.y = 0f;
                    if (to.magnitude < 0.12f)
                    {
                        break;
                    }

                    vy = controller.isGrounded ? -2f : vy - 9.8f * dt;
                    Vector3 step = to.normalized * Mathf.Min(to.magnitude, 4f * dt);
                    controller.Move(new Vector3(step.x, vy * dt, step.z));
                    yield return null;
                }

                Vector3 at = pip.transform.position;
                Assert.Less(new Vector2(at.x - goal.x, at.z - goal.z).magnitude, 0.3f,
                    $"stuck before {route[i]} at {at}");
                Assert.AreEqual(goal.y, at.y, 0.3f, $"wrong height at {route[i]}");
            }

            pip.enabled = true;
            Assert.IsTrue(Director.House.View.ShowInterior || !CaseRouteLayout.Inside(pip.transform.position.x,
                pip.transform.position.z));
        }

        [UnityTest]
        public IEnumerator SkilledAscentSurfacesHoldPip()
        {
            yield return StartGame();
            foreach (var point in CaseRouteLayout.SkilledAscent)
            {
                Game.Pip.Teleport(new Vector3(point.X, point.Y + 0.4f, point.Z));
                yield return Wait(0.6f);
                Vector3 at = Game.Pip.transform.position;
                Assert.IsTrue(Game.Pip.Grounded, point + " holds Pip");
                // On the awning slope the capsule rests a little above the surface under its centre.
                Assert.AreEqual(point.Y, at.y, 0.2f, point + " height");
            }
        }

        // ---------------------------------------------------------------------------------------------- CASE-01

        [UnityTest]
        [Timeout(180000)]
        public IEnumerator CanonicalPlaythroughClosesTheCaseWithTheRightEvidence()
        {
            yield return StartGame();
            var director = Director;
            var house = director.House;
            var workshop = director.Workshop;
            var cast = director.Cast;
            Assert.IsFalse(house.View.ShowInterior, "the house is closed from the arrival porch");
            Assert.IsTrue(house.Body.gameObject.activeSelf);

            // Both witnesses, the bench (exterior wiring, covered by CaseWiringTests).
            director.Perform(CaseAction.Of(CaseActionKind.HearKnocks));
            director.Perform(CaseAction.Talk(Speaker.Bea));
            yield return SkipDialogue();
            director.Perform(CaseAction.Talk(Speaker.Vale));
            yield return SkipDialogue();
            director.Perform(CaseAction.Of(CaseActionKind.SitOnBench));
            director.Bench.End();

            // Cloth on the bridge-head post (shed roof).
            yield return Use(CaseStage.PointCloth, new Vector3(-6.75f, CaseRouteLayout.TreehouseY, CaseRouteLayout.ClothZ));
            Assert.IsTrue(director.State.Has(CaseFact.ExaminedCloth));
            Assert.AreEqual(Lines.ExamineCloth, director.Dialogue.CurrentLine.Key);
            yield return SkipDialogue();

            // In by June's window.
            yield return EnterByWindow();
            Assert.IsTrue(director.State.Has(CaseFact.EnteredByWindow), "the window entry is recorded");
            Assert.IsFalse(director.State.Has(CaseFact.AtticFirst), "the witnesses spoke first");
            Assert.IsTrue(house.View.ShowInterior, "inside: the interior shows");
            Assert.IsFalse(house.Body.gameObject.activeSelf, "inside: the exterior body hides");
            Assert.IsTrue(house.Interior.gameObject.activeSelf);
            Assert.Less(Game.CameraRig.Distance, 3.5f, "the attic blends to a closer shoulder view");

            yield return Use(CaseStage.PointSill, new Vector3(-2.45f, CaseRouteLayout.LoftY, 14.35f));
            Assert.IsTrue(director.State.Has(CaseFact.ExaminedSill));
            yield return SkipDialogue();

            Assert.AreEqual(CharacterClipsKneeTap, cast.June.Avatar.Loop, "June taps the rhythm on her stool");
            yield return Use(CaseStage.PointJuneAttic, new Vector3(-1.6f, CaseRouteLayout.LoftY, 14.2f));
            Assert.IsTrue(director.State.Has(CaseFact.FoundJune));
            Assert.AreEqual(Lines.JuneGreet, director.Dialogue.CurrentLine.Key);
            yield return SkipDialogue();
            Assert.IsTrue(cast.JuneWalking, "June walks down after her lines");
            yield return Wait(2f);
            Assert.AreEqual(cast.JuneHasWalk ? CaseCast.WalkClip : "Idle", cast.June.Avatar.Loop,
                "June steps on her walk clip when she has one, and glides on Idle otherwise");
            if (cast.JuneHasWalk)
            {
                Assert.AreEqual(1f, cast.June.Avatar.ClipSpeed(CaseCast.WalkClip), 0.25f,
                    "the walk plays at its authored pace while she cruises at 1.1 m/s over the floor plan");
            }
            yield return WaitForJune();
            Vector3 june = cast.June.transform.position;
            Assert.Less(Vector3.Distance(june, At(CaseStage.JuneFoyer.Position)), 0.2f, "June at her foyer mark");

            // Foyer: the hum, then the latch opens both leaves together.
            CaseStage.TryGetAnchor(CaseAnchors.Foyer, out var foyer);
            Game.Pip.Teleport(At(foyer));
            yield return Wait(0.4f);
            Assert.AreEqual(Lines.CaptionFoyerHum, director.Captions.CurrentKey, "the foyer hums");
            Assert.IsTrue(director.LastStep != null);
            yield return Use(CaseStage.PointLatch, new Vector3(0.35f, CaseRouteLayout.FloorY, 12.0f));
            Assert.IsTrue(director.State.Has(CaseFact.FrontOpen));
            yield return SkipDialogue();
            yield return Wait(1.4f);
            Assert.AreEqual(OwlHouseStage.DoorOpenDegrees, house.DoorAngle, 1f, "door open");
            Assert.AreEqual(Quaternion.Angle(house.FrontDoor.rotation, house.InnerDoor.rotation), 0f, 1f,
                "both leaves swing together");
            Assert.IsTrue(house.LatchLifted);

            // Workshop: grille (sawCam, the panel swings open), arm, crank and the cutaway.
            yield return Use(CaseStage.PointGrille, new Vector3(0.7f, CaseRouteLayout.FloorY, 14.9f));
            Assert.IsTrue(director.State.Has(CaseFact.SawCam));
            Assert.IsTrue(house.NookWallHidden, "the follower is shown lifting");
            yield return Wait(MechanismMotionCycle + 0.2f);
            Assert.AreEqual(3, workshop.KnocksThisCycle, "three knocks through the grille");
            Assert.Greater(workshop.FollowerLift, 0.9f, "the follower lifted");
            Assert.AreEqual(0, workshop.BowsThisCycle, "no bow while the arm has slipped");
            Assert.IsTrue(workshop.PanelOpen);

            yield return Use(CaseStage.PointLinkArm, new Vector3(0.75f, CaseRouteLayout.FloorY, 13.3f));
            Assert.IsTrue(director.State.Has(CaseFact.LinkedArm));
            Assert.IsTrue(workshop.ArmSeated.gameObject.activeSelf);
            Assert.IsFalse(workshop.ArmSlipped.gameObject.activeSelf);
            Assert.IsFalse(director.Captions.HasDirectionCue, "what Pip sees in the workshop takes no direction cue");

            yield return Use(CaseStage.PointCrank, new Vector3(0.75f, CaseRouteLayout.FloorY, 14.2f));
            Assert.IsTrue(director.State.Has(CaseFact.TestedOwl));
            Assert.IsTrue(director.LastStep.PlayCutaway);
            Assert.IsTrue(director.Cutaway.Active, "the cutaway plays");
            Assert.IsTrue(director.ModalOpen, "gameplay waits during the cutaway");
            yield return Wait(MechanismMotionCycle + 0.1f);
            Assert.AreEqual(3, workshop.BowsThisCycle, "the owl bows three times with the three knocks");
            float until = Time.time + 2f;
            while (director.Cutaway.Active && Time.time < until)
            {
                yield return null;
            }

            Assert.IsFalse(director.Cutaway.Active, "the cutaway lasts at most four seconds");
            Assert.LessOrEqual(director.Cutaway.Elapsed, CutawayView.MaxSeconds + 0.1f);
            yield return Frames(3);
            Assert.IsFalse(director.ModalOpen);

            // Out through the open door and back to the arrival porch.
            Game.Pip.Teleport(new Vector3(0.05f, CaseRouteLayout.FloorY + 0.05f, CaseRouteLayout.FrontZ - 0.7f));
            yield return Wait(0.3f);
            Game.Pip.Teleport(new Vector3(0f, 0.7f, -13.4f));
            yield return Frames(3);
            Assert.IsTrue(director.State.Has(CaseFact.CaseClosed), "closure on the arrival porch");
            Assert.IsTrue(director.FinalePlaying, "the finale frames the booth from the arrival sightline");
            Assert.IsFalse(director.Captions.Showing, "the finale starts clean: no caption left over from the workshop");
            Assert.IsTrue(cast.Kit.gameObject.activeSelf, "Kit at her booth");
            Assert.AreEqual("OpenBooth", cast.Kit.Avatar.OneShot, "Kit opens her booth");
            Assert.IsFalse(director.Dialogue.IsOpen, "the reopening plays before the card");
            yield return Wait(FinaleView.IntroSeconds + 0.3f);
            Assert.AreEqual(Speaker.June, director.Dialogue.CurrentLine.Speaker);
            Assert.AreEqual(Lines.JuneResolve, director.Dialogue.CurrentLine.Key);
            Assert.Less(Vector3.Distance(cast.June.transform.position, At(CaseStage.JunePorch.Position)), 0.2f);
            director.Dialogue.Advance();
            yield return Wait(0.3f);
            Assert.AreEqual(Lines.KitFirstShow, director.Dialogue.CurrentLine.Key);
            yield return SkipDialogue();
            yield return Wait(FinaleView.HoldAfterLines + 0.3f);
            Assert.IsFalse(director.FinalePlaying);
            Assert.IsTrue(director.Notebook.IsOpen, "the notebook shows the final card");
            Assert.IsNotNull(director.Notebook.FinalCard);

            // The correct evidence: every canonical discovery, the three answers proven.
            foreach (var fact in new[]
                     {
                         CaseFact.HeardKnocks, CaseFact.BeaSpoke, CaseFact.ValeSpoke, CaseFact.CheckedView,
                         CaseFact.ExaminedCloth, CaseFact.EnteredByWindow, CaseFact.ExaminedSill, CaseFact.FoundJune,
                         CaseFact.FrontOpen, CaseFact.SawCam, CaseFact.LinkedArm, CaseFact.TestedOwl, CaseFact.CaseClosed,
                     })
            {
                Assert.IsTrue(director.State.Has(fact), fact.ToString());
            }

            foreach (var answer in director.Notebook.AnswerLabels)
            {
                Assert.IsNotNull(answer, "every question answered");
            }

            director.Notebook.Close();
            yield return Frames(3);
            Assert.IsTrue(Game.Controls.GameplayEnabled, "the player keeps control after the finale");

            // The candy-bowl sweet works after closure.
            director.Perform(CaseAction.Of(CaseActionKind.ExamineCandyBowl));
            Assert.IsTrue(director.State.Has(CaseFact.FoundSweet));
        }

        private const string CharacterClipsKneeTap = "KneeTap";

        private static float MechanismMotionCycle => MechanismMotion.CycleSeconds;

        // ---------------------------------------------------------------------------------------------- CASE-02

        [UnityTest]
        [Timeout(150000)]
        public IEnumerator AtticFirstAndAnEarlyCrankStillReachTheSameTruth()
        {
            yield return StartGame();
            var director = Director;
            var workshop = director.Workshop;

            // Straight up the route, no interviews, no cloth.
            director.Perform(CaseAction.Of(CaseActionKind.HearKnocks));
            yield return EnterByWindow();
            Assert.IsTrue(director.State.Has(CaseFact.AtticFirst), "route proof before any interview");
            yield return Use(CaseStage.PointJuneAttic, new Vector3(-1.6f, CaseRouteLayout.LoftY, 14.2f));
            Assert.AreEqual(Lines.JuneGreetShortcut, director.Dialogue.CurrentLine.Key, "June acknowledges the shortcut");
            yield return SkipDialogue();
            yield return WaitForJune();

            // Workshop before the latch: open the panel, crank early (one tap, a gap, no fact).
            CaseStage.TryGetAnchor(CaseAnchors.Workshop, out var workshopAnchor);
            Game.Pip.Teleport(At(workshopAnchor));
            yield return Wait(0.3f);
            yield return Use(CaseStage.PointServicePanel, new Vector3(0.75f, CaseRouteLayout.FloorY, 13.75f));
            Assert.IsTrue(director.State.Has(CaseFact.SawCam), "looking through the open panel shows the cam");
            yield return Wait(0.9f);
            Assert.IsTrue(workshop.PanelOpen);
            Assert.AreEqual(WorkshopView.PanelOpenDegrees, workshop.PanelDegrees, 0.5f, "the panel swings +112 degrees");
            yield return Wait(MechanismMotionCycle);

            int facts = director.State.Count;
            yield return Use(CaseStage.PointCrank, new Vector3(0.75f, CaseRouteLayout.FloorY, 14.2f));
            Assert.AreEqual(CaseOutcome.CrankGap, director.LastStep.Outcome);
            Assert.AreEqual(facts, director.State.Count, "no fact from the early crank");
            CollectionAssert.Contains(director.LastStep.Lines, new ScriptLine(Speaker.Caption, Lines.CaptionCrankGap));
            Assert.IsFalse(director.Cutaway.Active, "no cutaway without the flag");
            yield return Wait(MechanismMotion.GapCycleSeconds + 0.1f);
            Assert.AreEqual(1, workshop.KnocksThisCycle, "a single tap");
            Assert.AreEqual(0, workshop.BowsThisCycle);
            Assert.IsTrue(workshop.ArmSlipped.gameObject.activeSelf, "the visible gap where the arm should connect");

            // Repeat a talk: nothing gained twice.
            yield return Use(CaseStage.PointJuneFoyer, new Vector3(-1.35f, CaseRouteLayout.FloorY, 12.9f));
            Assert.AreEqual(Lines.JuneLatchReminder, director.Dialogue.CurrentLine.Key);
            Assert.AreEqual(0, director.LastStep.Gained.Count);
            yield return SkipDialogue();

            yield return Use(CaseStage.PointLinkArm, new Vector3(0.75f, CaseRouteLayout.FloorY, 13.3f));
            yield return Use(CaseStage.PointCrank, new Vector3(0.75f, CaseRouteLayout.FloorY, 14.2f));
            Assert.IsTrue(director.State.Has(CaseFact.TestedOwl));
            director.Cutaway.Skip();
            Assert.IsFalse(director.Cutaway.Active, "the cutaway is skippable");
            yield return Frames(3);

            yield return Use(CaseStage.PointLatch, new Vector3(0.35f, CaseRouteLayout.FloorY, 12.0f));
            Assert.AreEqual(Lines.JuneLatchThanks, director.Dialogue.CurrentLine.Key);
            yield return SkipDialogue();

            Game.Pip.Teleport(new Vector3(0f, 0.7f, -13.4f));
            yield return Frames(3);
            Assert.IsTrue(director.State.Has(CaseFact.CaseClosed));
            Assert.IsFalse(director.State.Has(CaseFact.ExaminedCloth), "the cloth stays discoverable after closure");
            yield return Wait(FinaleView.IntroSeconds + 0.3f);
            yield return SkipDialogue();

            // Back to the bench after the solve: optional evidence still works.
            director.Perform(CaseAction.Of(CaseActionKind.SitOnBench));
            Assert.IsTrue(director.State.Has(CaseFact.CheckedView));
        }

        // ---------------------------------------------------------------------------------------------- SAVE-01

        private static IEnumerator ReloadAndContinue()
        {
            yield return LoadGame();
            Assert.AreEqual(GameState.Title, Game.Flow.State);
            Assert.IsTrue(Game.Ui.ContinueButton.gameObject.activeSelf, "Continue is offered");
            Game.Ui.ContinueButton.onClick.Invoke();
            yield return Frames(5);
            Assert.AreEqual(GameState.Playing, Game.Flow.State);
        }

        private static void AssertPipAtAnchor(string expected)
        {
            Assert.AreEqual(expected, Director.AnchorId);
            Assert.IsTrue(CaseStage.TryGetAnchor(expected, out var anchor));
            Vector3 pip = Game.Pip.transform.position;
            Assert.Less(new Vector2(pip.x - anchor.X, pip.z - anchor.Z).magnitude, 0.6f, "Pip resumes at " + expected);
            Assert.AreEqual(anchor.Y, pip.y, 0.4f, "on the anchor's floor");
        }

        [UnityTest]
        [Timeout(150000)]
        public IEnumerator ReloadAfterWindowEntryLatchAndCrankRebuildsTheWorld()
        {
            yield return StartGame();
            Director.Perform(CaseAction.Of(CaseActionKind.HearKnocks));

            // 1. Interrupted right after the window entry.
            yield return EnterByWindow();
            Assert.IsTrue(Director.State.Has(CaseFact.EnteredByWindow));
            yield return ReloadAndContinue();
            Assert.IsTrue(Director.State.Has(CaseFact.EnteredByWindow));
            Assert.IsTrue(Director.State.Has(CaseFact.AtticFirst));
            // The last safe ground was the bridge end or, once Pip landed inside, the attic floor.
            Assert.That(Director.AnchorId, Is.EqualTo(CaseAnchors.AtticSill).Or.EqualTo(CaseAnchors.Attic));
            AssertPipAtAnchor(Director.AnchorId);
            Assert.AreEqual(0f, Director.House.DoorAngle, 0.1f, "door still latched");
            Assert.AreEqual(JunePlacement.Attic, Director.Cast.JuneMark, "June still in the attic");
            Assert.IsFalse(Director.Workshop.ArmLinked);

            // 2. Interrupted right after the latch (June found first).
            yield return Use(CaseStage.PointSill, new Vector3(-2.45f, CaseRouteLayout.LoftY, 14.35f));
            yield return SkipDialogue();
            Game.Pip.Teleport(new Vector3(-2.2f, 2.55f, 14.0f));
            yield return Wait(0.4f);
            yield return Use(CaseStage.PointJuneAttic, new Vector3(-1.6f, CaseRouteLayout.LoftY, 14.2f));
            yield return SkipDialogue();
            CaseStage.TryGetAnchor(CaseAnchors.Foyer, out var foyer);
            Game.Pip.Teleport(At(foyer));
            yield return Wait(0.5f);
            yield return Use(CaseStage.PointLatch, new Vector3(0.35f, CaseRouteLayout.FloorY, 12.0f));
            Assert.IsTrue(Director.State.Has(CaseFact.FrontOpen));
            yield return ReloadAndContinue();
            Assert.IsTrue(Director.State.Has(CaseFact.FrontOpen));
            AssertPipAtAnchor(CaseAnchors.Foyer);
            Assert.AreEqual(OwlHouseStage.DoorOpenDegrees, Director.House.DoorAngle, 0.5f, "the door is rebuilt open");
            Assert.AreEqual(JunePlacement.Foyer, Director.Cast.JuneMark, "June is never stranded upstairs");
            Assert.Less(Vector3.Distance(Director.Cast.June.transform.position, At(CaseStage.JuneFoyer.Position)), 0.1f);

            // 3. Interrupted right after the crank (during the cutaway).
            CaseStage.TryGetAnchor(CaseAnchors.Workshop, out var workshopAnchor);
            Game.Pip.Teleport(At(workshopAnchor));
            yield return Wait(0.5f);
            Director.Perform(CaseAction.Of(CaseActionKind.ExamineGrille));
            Director.Perform(CaseAction.Of(CaseActionKind.LinkArm));
            yield return Use(CaseStage.PointCrank, new Vector3(0.75f, CaseRouteLayout.FloorY, 14.2f));
            Assert.IsTrue(Director.Cutaway.Active);
            yield return ReloadAndContinue();
            Assert.IsTrue(Director.State.Has(CaseFact.TestedOwl));
            AssertPipAtAnchor(CaseAnchors.Workshop);
            Assert.IsFalse(Director.Cutaway.Active, "a reset during the reveal rebuilds, it does not replay");
            Assert.IsTrue(Director.Workshop.PanelOpen, "panel open");
            Assert.IsTrue(Director.Workshop.ArmLinked, "arm linked");
            Assert.IsTrue(Director.Workshop.OwlBowing, "the owl bows from now on");
            Assert.AreEqual(OwlHouseStage.DoorOpenDegrees, Director.House.DoorAngle, 0.5f);
            Assert.IsFalse(Director.Cast.Kit.gameObject.activeSelf, "Kit waits for the reopening");
            Assert.IsFalse(Director.ModalOpen);
            Assert.IsTrue(Game.Controls.GameplayEnabled);
        }

        [UnityTest]
        public IEnumerator InsideTheShellTheCameraLooksInAndTheAtticHidesFromAbove()
        {
            yield return StartGame();
            var house = Director.House;
            Director.Perform(CaseAction.Of(CaseActionKind.HearKnocks));
            yield return EnterByWindow();
            CaseStage.TryGetAnchor(CaseAnchors.Foyer, out var foyer);
            Game.Pip.Teleport(At(foyer));
            yield return Wait(0.5f);
            Assert.IsTrue(house.View.ShowInterior);
            Assert.IsFalse(house.RecessCard.gameObject.activeSelf, "the dark recess card hides with the interior shown");
            bool high = Game.CameraRig.OutputCamera.transform.position.y > InteriorVisibility.HighCamera;
            Assert.AreEqual(high, !house.Attic.gameObject.activeSelf, "attic hides exactly when the camera is high");
            Game.Pip.Teleport(new Vector3(0f, 0.1f, 0f));
            yield return Wait(0.3f);
            Assert.IsFalse(house.View.ShowInterior, "back on the plaza");
            Assert.IsTrue(house.Body.gameObject.activeSelf);
            Assert.IsTrue(house.Attic.gameObject.activeSelf || !house.Interior.gameObject.activeSelf);
        }
    }
}
