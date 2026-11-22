using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.World;
using SpookyStreet.Localization;
using SpookyStreet.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.InputSystem.Controls;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Lines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// S002 interior wiring with house v3 in the scene: the treehouse route and the house are walkable with real
    /// colliders (MAP-01) and each leg ends in a threshold that carries Pip, under the paper page-turn, into the
    /// separate interior and back out (D24.1); the canonical playthrough closes the case through both thresholds with the
    /// right evidence (CASE-01); the curious-child order works (CASE-02); saves after the window entry, the latch and
    /// the crank reload inside the interior and rebuild the door, June, the mechanism and the owl (SAVE-01); a round
    /// trip through both thresholds never double-triggers. Every test runs against a fresh save folder.
    /// </summary>
    public class CaseInteriorTests : InputTestFixture
    {
        private const string SceneName = "LanternLane";
        private const float Ifz = CaseRouteLayout.InteriorFrontZ;

        private Keyboard _keyboard;

        public override void Setup()
        {
            base.Setup();
            _keyboard = InputSystem.AddDevice<Keyboard>();
            Localizer.ResetForTests();
            CaseDirector.UseTemporarySaveDirectory();
            AmbientMotion.ReducedMotion = false;
        }

        public override void TearDown()
        {
            AmbientMotion.ReducedMotion = false;
            base.TearDown();
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
            Assert.IsNotNull(Director.Travel, "the thresholds are wired with the models");
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

        private static Vector3 Arrival(ThresholdEnd end) => new Vector3(end.ArriveX, end.ArriveY, end.ArriveZ);

        private static bool PipInside => CaseRouteLayout.Inside(Game.Pip.transform.position.x, Game.Pip.transform.position.z);

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

        /// <summary>
        /// Stands Pip just clear of <paramref name="exit"/>'s zone (so the tracker is armed), steps into it and waits for
        /// the page-turn to finish. Asserts one crossing, input held during it, and Pip at the paired arrival point.
        /// </summary>
        private static IEnumerator Cross(ThresholdEnd exit, Vector3 approach, Vector3 inZone)
        {
            var travel = Director.Travel;
            int before = travel.Crossings;
            Game.Pip.Teleport(approach + Vector3.up * 0.05f);
            yield return Wait(0.8f);
            Assert.IsTrue(travel.Tracker.Armed, $"armed before {exit}");
            Game.Pip.Teleport(inZone + Vector3.up * 0.05f);
            float until = Time.time + 1f;
            while (!travel.Active && Time.time < until)
            {
                yield return null;
            }

            Assert.IsTrue(travel.Active, $"the page turns at {exit}");
            yield return null;
            Assert.IsFalse(Game.Controls.GameplayEnabled, "input is ignored during the page-turn");
            until = Time.time + 2f;
            while (travel.Active && Time.time < until)
            {
                yield return null;
            }

            Assert.IsFalse(travel.Active, "the page-turn ends");
            yield return Frames(3);
            Assert.AreEqual(before + 1, travel.Crossings, $"exactly one crossing at {exit}");
            Assert.IsTrue(Game.Controls.GameplayEnabled || Director.ModalOpen, "control is back after the page-turn");
            var arrive = Thresholds.Pair(exit);
            Vector3 pip = Game.Pip.transform.position;
            Assert.Less(new Vector2(pip.x - arrive.ArriveX, pip.z - arrive.ArriveZ).magnitude, 0.3f,
                $"Pip arrives at {arrive} ({pip})");
            Assert.AreEqual(arrive.Inside, PipInside, "on the arrival's side of the walls");
        }

        /// <summary>From the bridge end in by June's window (the crossing reports EnterAttic on arrival).</summary>
        private static IEnumerator EnterByWindow()
        {
            yield return Cross(Thresholds.DormerOutside,
                new Vector3(-3.95f, 2.5f, CaseRouteLayout.ExteriorWindowZ),
                new Vector3(-3.36f, 2.5f, CaseRouteLayout.ExteriorWindowZ));
            yield return Frames(3);
        }

        private static IEnumerator OutByTheDoor()
        {
            yield return Cross(Thresholds.DoorInside, new Vector3(0f, CaseRouteLayout.FloorY, Ifz + 1.0f),
                new Vector3(0f, CaseRouteLayout.FloorY, Ifz + 0.05f));
        }

        private static IEnumerator WaitForJune()
        {
            float until = Time.time + 22f;
            while (Director.Cast.JuneWalking && Time.time < until)
            {
                yield return null;
            }

            Assert.IsFalse(Director.Cast.JuneWalking, "June reached her hall mark");
        }

        private static Vector3 Workshop(float alongFromMechanism) =>
            new Vector3(CaseRouteLayout.NookX - 0.7f, CaseRouteLayout.FloorY, CaseRouteLayout.MechanismZ + alongFromMechanism);

        private static Vector3 LatchStand => new Vector3(0.35f, CaseRouteLayout.FloorY, Ifz + 1.0f);

        private static Vector3 JuneAtticStand => new Vector3(-0.4f, CaseRouteLayout.LoftY, Ifz + 2.4f);

        private static Vector3 SillStand => new Vector3(-2.6f, CaseRouteLayout.LoftY, Ifz + 1.75f);

        // ---------------------------------------------------------------------------------------------- MAP-01

        /// <summary>
        /// Drives Pip's CharacterController (motor off) along each leg of the mandatory route with gravity, as a walk at
        /// a steady pace with no jump: every waypoint is reached at its height, and each leg's end crosses its threshold
        /// to the next leg's start.
        /// </summary>
        [UnityTest]
        [Timeout(150000)]
        public IEnumerator MandatoryRouteIsWalkableWithTheAuthoredColliders()
        {
            yield return StartGame();
            // The arrival cue first, so no conversation holds the thresholds while Pip walks.
            Director.Perform(CaseAction.Of(CaseActionKind.HearKnocks));
            yield return SkipDialogue();
            Director.House.SetDoor(true);
            var pip = Game.Pip;
            var controller = pip.GetComponent<CharacterController>();
            var legs = CaseRouteLayout.MandatoryLegs;
            var first = legs[0].Points[0];
            pip.Teleport(new Vector3(first.X, first.Y + 0.05f, first.Z));
            pip.enabled = false;
            yield return Wait(0.6f);
            float vy = 0f;
            int crossings = Director.Travel.Crossings;
            foreach (var leg in legs)
            {
                var route = leg.Points;
                Vector3 start = pip.transform.position;
                Assert.Less(new Vector2(start.x - route[0].X, start.z - route[0].Z).magnitude, 0.3f,
                    $"{leg.Name} starts at {route[0]} (Pip at {start})");
                for (int i = 1; i < route.Length; i++)
                {
                    var goal = new Vector3(route[i].X, route[i].Y, route[i].Z);
                    float limit = Time.time + 8f;
                    while (Time.time < limit && !Director.Travel.Active)
                    {
                        float dt = Mathf.Min(Time.deltaTime, 0.05f);
                        Vector3 to = goal - pip.transform.position;
                        to.y = 0f;
                        if (to.magnitude < 0.12f)
                        {
                            break;
                        }

                        vy = controller.isGrounded ? -2f : vy - 9.8f * dt;
                        Vector3 step = to.normalized * Mathf.Min(to.magnitude, 3f * dt);
                        controller.Move(new Vector3(step.x, vy * dt, step.z));
                        yield return null;
                    }

                    if (Director.Travel.Active)
                    {
                        Assert.AreEqual(route.Length - 1, i, $"{leg.Name}: crossed early, before {route[i]}");
                        break;
                    }

                    Vector3 at = pip.transform.position;
                    Assert.Less(new Vector2(at.x - goal.x, at.z - goal.z).magnitude, 0.3f, $"stuck before {route[i]} at {at}");
                    Assert.AreEqual(goal.y, at.y, 0.3f, $"wrong height at {route[i]}");
                }

                if (leg.Exit == ThresholdId.None)
                {
                    continue;
                }

                float until = Time.time + 3f;
                while ((Director.Travel.Active || Director.Travel.Crossings == crossings) && Time.time < until)
                {
                    yield return null;
                }

                Assert.AreEqual(crossings + 1, Director.Travel.Crossings, $"{leg.Name} ends through {leg.Exit}");
                crossings = Director.Travel.Crossings;
                vy = 0f;
                yield return Wait(0.3f);
            }

            pip.enabled = true;
            Assert.IsFalse(PipInside, "the route ends on the porch");
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
        [Timeout(200000)]
        public IEnumerator CanonicalPlaythroughClosesTheCaseWithTheRightEvidence()
        {
            yield return StartGame();
            var director = Director;
            var house = director.House;
            var workshop = director.Workshop;
            var cast = director.Cast;
            Assert.IsFalse(house.InteriorShown, "the rooms are not drawn from the arrival porch");
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

            // In by June's window: the page turns, Pip arrives in the attic.
            yield return EnterByWindow();
            Assert.IsTrue(director.State.Has(CaseFact.EnteredByWindow), "the window entry is recorded");
            Assert.IsFalse(director.State.Has(CaseFact.AtticFirst), "the witnesses spoke first");
            Assert.IsTrue(house.InteriorShown, "inside: the interior is drawn");
            Assert.IsTrue(house.Body.gameObject.activeSelf, "the exterior is never cut away");
            Assert.IsTrue(house.Attic.gameObject.activeSelf, "the attic is always drawn");
            Assert.IsTrue(director.Framing.Active, "the interior camera has the room");
            Vector3 lens = Game.CameraRig.OutputCamera.transform.position;
            Assert.IsTrue(CaseRouteLayout.Inside(lens.x, lens.z), $"the lens cut into the attic ({lens})");

            yield return Use(CaseStage.PointSill, SillStand);
            Assert.IsTrue(director.State.Has(CaseFact.ExaminedSill));
            yield return SkipDialogue();

            Assert.AreEqual(CharacterClipsKneeTap, cast.June.Avatar.Loop, "June taps the rhythm on her stool");
            yield return Use(CaseStage.PointJuneAttic, JuneAtticStand);
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
            Assert.Less(Vector3.Distance(june, At(CaseStage.JuneFoyer.Position)), 0.2f, "June at her hall mark");

            // Hall: the hum, then the latch opens both leaves.
            CaseStage.TryGetAnchor(CaseAnchors.Foyer, out var foyer);
            Game.Pip.Teleport(At(foyer));
            yield return Wait(0.4f);
            Assert.AreEqual(Lines.CaptionFoyerHum, director.Captions.CurrentKey, "the hall hums");
            yield return Use(CaseStage.PointLatch, LatchStand);
            Assert.IsTrue(director.State.Has(CaseFact.FrontOpen));
            yield return SkipDialogue();
            yield return Wait(1.4f);
            Assert.AreEqual(OwlHouseStage.DoorOpenDegrees, house.DoorAngle, 1f, "the exterior leaf opens 90°");
            Assert.AreEqual(OwlHouseStage.InnerDoorOpenDegrees, house.InnerDoorAngle, 1f, "the inner leaf opens 100°");
            Assert.LessOrEqual(OwlHouseStage.DoorOpenDegrees, 90f, "art note: the exterior leaf never passes 90°");
            Assert.IsTrue(house.LatchLifted);

            // Workshop: grille (sawCam, the panel swings open), arm, crank and the cutaway.
            yield return Use(CaseStage.PointGrille, Workshop(0.8f));
            Assert.IsTrue(director.State.Has(CaseFact.SawCam));
            Assert.IsTrue(house.NookWallHidden, "the follower is shown lifting");
            yield return Wait(MechanismMotionCycle + 0.2f);
            Assert.AreEqual(3, workshop.KnocksThisCycle, "three knocks through the grille");
            Assert.Greater(workshop.FollowerLift, 0.9f, "the follower lifted");
            Assert.AreEqual(0, workshop.BowsThisCycle, "no bow while the arm has slipped");
            Assert.IsTrue(workshop.PanelOpen);

            yield return Use(CaseStage.PointLinkArm, Workshop(-0.8f));
            Assert.IsTrue(director.State.Has(CaseFact.LinkedArm));
            Assert.IsTrue(workshop.ArmSeated.gameObject.activeSelf);
            Assert.IsFalse(workshop.ArmSlipped.gameObject.activeSelf);
            Assert.IsFalse(director.Captions.HasDirectionCue, "what Pip sees in the workshop takes no direction cue");

            yield return Use(CaseStage.PointCrank, Workshop(0.1f));
            Assert.IsTrue(director.State.Has(CaseFact.TestedOwl));
            Assert.IsTrue(director.LastStep.PlayCutaway);
            Assert.IsTrue(director.Cutaway.Active, "the cutaway plays");
            Assert.IsTrue(director.ModalOpen, "gameplay waits during the cutaway");
            Vector3 eye = director.Cutaway.CameraTransform.position;
            Assert.IsTrue(CaseRouteLayout.Inside(eye.x, eye.z), "the cutaway looks into the nook in the interior");
            Assert.IsFalse(CaseRouteLayout.Inside(director.Cutaway.InsetCamera.transform.position.x,
                director.Cutaway.InsetCamera.transform.position.z), "its inset still looks at the rooftop owl on the street");
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

            // Out through the open front door: the page turns, Pip arrives on the porch. Then the arrival porch.
            yield return OutByTheDoor();
            Assert.IsFalse(house.InteriorShown, "back on the street, the rooms are not drawn");
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
        [Timeout(170000)]
        public IEnumerator AtticFirstAndAnEarlyCrankStillReachTheSameTruth()
        {
            yield return StartGame();
            var director = Director;
            var workshop = director.Workshop;

            // Straight up the route, no interviews, no cloth.
            director.Perform(CaseAction.Of(CaseActionKind.HearKnocks));
            yield return EnterByWindow();
            Assert.IsTrue(director.State.Has(CaseFact.AtticFirst), "route proof before any interview");
            yield return Use(CaseStage.PointJuneAttic, JuneAtticStand);
            Assert.AreEqual(Lines.JuneGreetShortcut, director.Dialogue.CurrentLine.Key, "June acknowledges the shortcut");
            yield return SkipDialogue();
            yield return WaitForJune();

            // Workshop before the latch: open the panel, crank early (one tap, a gap, no fact).
            CaseStage.TryGetAnchor(CaseAnchors.Workshop, out var workshopAnchor);
            Game.Pip.Teleport(At(workshopAnchor));
            yield return Wait(0.3f);
            yield return Use(CaseStage.PointServicePanel, Workshop(-0.35f));
            Assert.IsTrue(director.State.Has(CaseFact.SawCam), "looking through the open panel shows the cam");
            yield return Wait(0.9f);
            Assert.IsTrue(workshop.PanelOpen);
            Assert.AreEqual(WorkshopView.PanelOpenDegrees, workshop.PanelDegrees, 0.5f, "the panel swings +112 degrees");
            yield return Wait(MechanismMotionCycle);

            int facts = director.State.Count;
            yield return Use(CaseStage.PointCrank, Workshop(0.1f));
            Assert.AreEqual(CaseOutcome.CrankGap, director.LastStep.Outcome);
            Assert.AreEqual(facts, director.State.Count, "no fact from the early crank");
            CollectionAssert.Contains(director.LastStep.Lines, new ScriptLine(Speaker.Caption, Lines.CaptionCrankGap));
            Assert.IsFalse(director.Cutaway.Active, "no cutaway without the flag");
            yield return Wait(MechanismMotion.GapCycleSeconds + 0.1f);
            Assert.AreEqual(1, workshop.KnocksThisCycle, "a single tap");
            Assert.AreEqual(0, workshop.BowsThisCycle);
            Assert.IsTrue(workshop.ArmSlipped.gameObject.activeSelf, "the visible gap where the arm should connect");

            // Repeat a talk: nothing gained twice.
            yield return Use(CaseStage.PointJuneFoyer, new Vector3(-1.35f, CaseRouteLayout.FloorY, Ifz + 1.9f));
            Assert.AreEqual(Lines.JuneLatchReminder, director.Dialogue.CurrentLine.Key);
            Assert.AreEqual(0, director.LastStep.Gained.Count);
            yield return SkipDialogue();

            // The front door is still stuck: no way out through it yet.
            Assert.IsNull(Thresholds.ExitAt(0f, CaseRouteLayout.FloorY, Ifz + 0.05f, Director.House.DoorOpen));

            yield return Use(CaseStage.PointLinkArm, Workshop(-0.8f));
            yield return Use(CaseStage.PointCrank, Workshop(0.1f));
            Assert.IsTrue(director.State.Has(CaseFact.TestedOwl));
            director.Cutaway.Skip();
            Assert.IsFalse(director.Cutaway.Active, "the cutaway is skippable");
            yield return Frames(3);

            yield return Use(CaseStage.PointLatch, LatchStand);
            Assert.AreEqual(Lines.JuneLatchThanks, director.Dialogue.CurrentLine.Key);
            yield return SkipDialogue();

            yield return OutByTheDoor();
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
            Assert.AreEqual(CaseAnchors.IsInside(expected), PipInside, expected + ": restored on its side of the walls");
        }

        /// <summary>A resume inside the interior shows it, with the camera in the room from the first frames.</summary>
        private static IEnumerator AssertResumedIndoors()
        {
            yield return Frames(3);
            Assert.IsTrue(PipInside, "the save reloads inside the interior");
            Assert.IsTrue(Director.House.InteriorShown, "the interior is drawn");
            Assert.IsTrue(Director.Framing.Active, "the interior camera has the room");
            Vector3 lens = Game.CameraRig.OutputCamera.transform.position;
            Assert.IsTrue(CaseRouteLayout.Inside(lens.x, lens.z), $"the lens is in the interior ({lens})");
            Assert.IsFalse(Director.Travel.Active, "a resume never crosses a threshold");
        }

        [UnityTest]
        [Timeout(170000)]
        public IEnumerator ReloadAfterWindowEntryLatchAndCrankRebuildsTheWorld()
        {
            yield return StartGame();
            Director.Perform(CaseAction.Of(CaseActionKind.HearKnocks));

            // 1. Interrupted right after the window entry.
            yield return EnterByWindow();
            Assert.IsTrue(Director.State.Has(CaseFact.EnteredByWindow));
            yield return Wait(0.5f);
            Director.Autosave();
            yield return ReloadAndContinue();
            Assert.IsTrue(Director.State.Has(CaseFact.EnteredByWindow));
            Assert.IsTrue(Director.State.Has(CaseFact.AtticFirst));
            Assert.AreEqual(CaseAnchors.Attic, Director.AnchorId, "the attic save restores in the attic");
            AssertPipAtAnchor(CaseAnchors.Attic);
            yield return AssertResumedIndoors();
            Assert.AreEqual(0f, Director.House.DoorAngle, 0.1f, "door still latched");
            Assert.AreEqual(JunePlacement.Attic, Director.Cast.JuneMark, "June still in the attic");
            Assert.IsFalse(Director.Workshop.ArmLinked);

            // 2. Interrupted right after the latch (June found first).
            yield return Use(CaseStage.PointSill, SillStand);
            yield return SkipDialogue();
            yield return Use(CaseStage.PointJuneAttic, JuneAtticStand);
            yield return SkipDialogue();
            CaseStage.TryGetAnchor(CaseAnchors.Foyer, out var foyer);
            Game.Pip.Teleport(At(foyer));
            yield return Wait(0.5f);
            yield return Use(CaseStage.PointLatch, LatchStand);
            Assert.IsTrue(Director.State.Has(CaseFact.FrontOpen));
            yield return ReloadAndContinue();
            Assert.IsTrue(Director.State.Has(CaseFact.FrontOpen));
            AssertPipAtAnchor(CaseAnchors.Foyer);
            yield return AssertResumedIndoors();
            Assert.AreEqual(OwlHouseStage.DoorOpenDegrees, Director.House.DoorAngle, 0.5f, "the door is rebuilt open");
            Assert.AreEqual(JunePlacement.Foyer, Director.Cast.JuneMark, "June is never stranded upstairs");
            Assert.Less(Vector3.Distance(Director.Cast.June.transform.position, At(CaseStage.JuneFoyer.Position)), 0.1f);

            // 3. Interrupted right after the crank (during the cutaway).
            CaseStage.TryGetAnchor(CaseAnchors.Workshop, out var workshopAnchor);
            Game.Pip.Teleport(At(workshopAnchor));
            yield return Wait(0.5f);
            Director.Perform(CaseAction.Of(CaseActionKind.ExamineGrille));
            Director.Perform(CaseAction.Of(CaseActionKind.LinkArm));
            yield return Use(CaseStage.PointCrank, Workshop(0.1f));
            Assert.IsTrue(Director.Cutaway.Active);
            yield return ReloadAndContinue();
            Assert.IsTrue(Director.State.Has(CaseFact.TestedOwl));
            AssertPipAtAnchor(CaseAnchors.Workshop);
            yield return AssertResumedIndoors();
            Assert.IsFalse(Director.Cutaway.Active, "a reset during the reveal rebuilds, it does not replay");
            Assert.IsTrue(Director.Workshop.PanelOpen, "panel open");
            Assert.IsTrue(Director.Workshop.ArmLinked, "arm linked");
            Assert.IsTrue(Director.Workshop.OwlBowing, "the owl bows from now on");
            Assert.AreEqual(OwlHouseStage.DoorOpenDegrees, Director.House.DoorAngle, 0.5f);
            Assert.IsFalse(Director.Cast.Kit.gameObject.activeSelf, "Kit waits for the reopening");
            Assert.IsFalse(Director.ModalOpen);
            Assert.IsTrue(Game.Controls.GameplayEnabled);
        }

        // ------------------------------------------------------------------------------------------- thresholds

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ThresholdRoundTripNeverDoubleTriggers()
        {
            yield return StartGame();
            var travel = Director.Travel;
            var house = Director.House;
            Director.Perform(CaseAction.Of(CaseActionKind.HearKnocks));

            // In by the window, then stand still at the arrival point: nothing more happens.
            yield return EnterByWindow();
            Assert.AreEqual(1, travel.Crossings);
            Assert.IsTrue(Director.State.Has(CaseFact.EnteredByWindow), "enteredByWindow fires on the dormer entry");
            Assert.IsTrue(Director.State.Has(CaseFact.AtticFirst), "and atticFirst, as before");
            yield return Wait(1.5f);
            Assert.AreEqual(1, travel.Crossings, "no bounce back from the arrival point");
            Assert.IsTrue(house.InteriorShown);

            // Back out by the window: Pip arrives on the bridge, the rooms stop drawing.
            yield return Cross(Thresholds.DormerInside, Arrival(Thresholds.DormerInside),
                new Vector3(-3.45f, CaseRouteLayout.LoftY, CaseRouteLayout.InnerWindowZ));
            Assert.AreEqual(2, travel.Crossings);
            yield return Frames(3);
            Assert.IsFalse(house.InteriorShown, "on the bridge the rooms are not drawn");
            Vector3 lens = Game.CameraRig.OutputCamera.transform.position;
            Assert.IsFalse(CaseRouteLayout.Inside(lens.x, lens.z), "the lens cut back out to the street");

            // The latched front door is stuck from the porch: standing in its doorway crosses nothing.
            Game.Pip.Teleport(new Vector3(0f, 0.65f, CaseRouteLayout.FrontZ - 1.4f));
            yield return Wait(0.8f);
            Game.Pip.Teleport(new Vector3(0.05f, 0.65f, CaseRouteLayout.FrontZ - 0.3f));
            yield return Wait(0.8f);
            Assert.AreEqual(2, travel.Crossings, "the outside door is stuck while latched");
            Assert.IsFalse(travel.Active);

            // Once open, the door works both ways (step back first, so the open doorway is walked into).
            Game.Pip.Teleport(new Vector3(0f, 0.65f, CaseRouteLayout.FrontZ - 1.4f));
            yield return Wait(0.3f);
            house.SetDoor(true);
            yield return Cross(Thresholds.DoorOutside, new Vector3(0f, 0.6f, CaseRouteLayout.FrontZ - 1.4f),
                new Vector3(0.05f, 0.6f, CaseRouteLayout.FrontZ - 0.3f));
            Assert.AreEqual(3, travel.Crossings);
            Assert.IsTrue(house.InteriorShown);
            yield return OutByTheDoor();
            Assert.AreEqual(4, travel.Crossings);

            // Reduced motion: the same crossing as a quick fade.
            AmbientMotion.ReducedMotion = true;
            float started = Time.time;
            yield return Cross(Thresholds.DoorOutside, new Vector3(0f, 0.6f, CaseRouteLayout.FrontZ - 1.4f),
                new Vector3(0.05f, 0.6f, CaseRouteLayout.FrontZ - 0.3f));
            Assert.IsTrue(travel.Wipe.ReducedMotion, "reduced motion fades instead of turning the page");
            Assert.AreEqual(5, travel.Crossings);
            AmbientMotion.ReducedMotion = false;
            Assert.Greater(Time.time - started, 0f);
        }
    }
}
