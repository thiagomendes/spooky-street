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
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using Lines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// TestFlight 23 ("right after entering through the window, I got stuck"): Pip comes in through June's dormer the
    /// way a player does, with the real motor and the gamepad stick: walking straight in, walking in at either side of
    /// the exit zone, gliding in from above the bridge and with a held jump, at 30 and 60 frames a second. After each
    /// arrival control is back, the sky is hidden, the lens is inside the closed shell, the safety net has nothing
    /// to do, and Pip walks away in all four directions: 2 m east and north, and to the window's exit zone (west) and
    /// the trunk (south), the most the attic allows there (<see cref="InteriorWalkMap.FreeRun"/>), with the lens inside
    /// the shell all the way. Every test runs against a fresh save folder.
    /// </summary>
    public class DormerArrivalTests : InputTestFixture
    {
        private const string SceneName = "LanternLane";

        private Gamepad _pad;

        public enum Approach
        {
            Straight,
            SideSouth,
            SideNorth,
            Glide,
            HeldJump,
        }

        public override void Setup()
        {
            base.Setup();
            _pad = InputSystem.AddDevice<Gamepad>();
            Localizer.ResetForTests();
            CaseDirector.UseTemporarySaveDirectory();
            AmbientMotion.ReducedMotion = false;
        }

        public override void TearDown()
        {
            Time.captureDeltaTime = 0f;
            AmbientMotion.ReducedMotion = false;
            base.TearDown();
        }

        private static GameBootstrap Game => GameBootstrap.Instance;

        private static CaseDirector Director => GameBootstrap.Instance.Director;

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                yield return null;
            }
        }

        private static IEnumerator StartGame(bool hearKnocks = true)
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(GameBootstrap.Instance, "scene did not create GameBootstrap");
            if (Game.Environment == null || !Game.Environment.UsesModels)
            {
                Assert.Ignore("Environment models are not committed: the interior is not staged.");
            }

            Assert.IsNotNull(Director.Travel, "the thresholds are wired with the models");
            Game.Ui.NewGameButton.onClick.Invoke();
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            Assert.AreEqual(GameState.Playing, Game.Flow.State);
            if (!hearKnocks)
            {
                yield break;
            }

            // The arrival cue first, so no conversation holds the thresholds.
            Director.Perform(CaseAction.Of(CaseActionKind.HearKnocks));
            yield return SkipModals();
        }

        private static IEnumerator SkipModals()
        {
            for (int i = 0; i < 4 && Director.ModalOpen; i++)
            {
                if (Director.Dialogue.IsOpen)
                {
                    Director.Dialogue.Skip();
                }

                yield return Wait(0.4f);
            }
        }

        /// <summary>The stick deflection that moves Pip along world direction <paramref name="dir"/> with this frame's lens.</summary>
        private void Steer(Vector3 dir)
        {
            Vector2 stick = Vector2.zero;
            if (dir.sqrMagnitude > 1e-4f)
            {
                var cam = Game.CameraRig.OutputCamera.transform;
                Vector3 forward = Vector3.ProjectOnPlane(cam.forward, Vector3.up);
                if (forward.sqrMagnitude < 1e-4f)
                {
                    forward = Vector3.ProjectOnPlane(cam.up, Vector3.up);
                }

                forward.Normalize();
                Vector3 right = Vector3.Cross(Vector3.up, forward);
                dir.y = 0f;
                dir.Normalize();
                stick = new Vector2(Vector3.Dot(dir, right), Vector3.Dot(dir, forward));
            }

            Set(_pad.leftStick, stick, queueEventOnly: true);
        }

        private static Vector3 Arrival => new Vector3(Thresholds.DormerInside.ArriveX, Thresholds.DormerInside.ArriveY,
            Thresholds.DormerInside.ArriveZ);

        private static void AssertLensInShell(string context)
        {
            Vector3 lens = Game.CameraRig.OutputCamera.transform.position;
            Assert.IsTrue(InteriorSafety.InsideShell(lens.x, lens.y, lens.z), $"{context}: the lens {lens:F2} is inside the shell");
            if (Game.Sky != null)
            {
                Assert.IsTrue(Game.Sky.Hidden, $"{context}: the sky is hidden indoors");
            }
        }

        /// <summary>Places Pip outside for <paramref name="approach"/>, then drives the stick (and Jump) into the dormer.</summary>
        private IEnumerator Enter(Approach approach)
        {
            float wz = CaseRouteLayout.ExteriorWindowZ;
            Vector3 start;
            switch (approach)
            {
                case Approach.SideSouth:
                    start = new Vector3(-4.6f, 2.55f, wz - 0.12f);
                    break;
                case Approach.SideNorth:
                    start = new Vector3(-4.6f, 2.55f, wz + 0.12f);
                    break;
                case Approach.Glide:
                    // Dropping in from above the shed end of the bridge, the cape spread after the fall begins.
                    start = new Vector3(-6.5f, 5.0f, wz);
                    break;
                case Approach.HeldJump:
                    start = new Vector3(-3.95f, 2.55f, wz);
                    break;
                default:
                    start = new Vector3(-4.6f, 2.55f, wz);
                    break;
            }

            Set(_pad.leftStick, Vector2.zero, queueEventOnly: true);
            Release(_pad.buttonSouth, queueEventOnly: true);
            Game.Pip.Teleport(start);
            Game.Pip.transform.rotation = Quaternion.Euler(0f, 90f, 0f);
            Director.CutCamera();
            Director.Travel.ResetTracker();
            yield return Wait(approach == Approach.Glide ? 0.45f : 0.8f);
            Assert.IsTrue(approach == Approach.Glide || Director.Travel.Tracker.Armed, $"{approach}: armed before the window");
            int before = Director.Travel.Crossings;
            if (approach == Approach.Glide)
            {
                Press(_pad.buttonSouth, queueEventOnly: true);
            }

            float t = 0f;
            bool jumped = false;
            while (!Director.Travel.Active && t < 4f)
            {
                Steer(Vector3.right);
                if (approach == Approach.HeldJump && !jumped && t > 0.05f)
                {
                    Press(_pad.buttonSouth, queueEventOnly: true);
                    jumped = true;
                }

                t += Time.deltaTime;
                yield return null;
            }

            Assert.IsTrue(Director.Travel.Active,
                $"{approach}: the page turns at June's window (Pip at {Game.Pip.transform.position:F2})");
            if (approach == Approach.Glide)
            {
                Assert.IsTrue(Game.Pip.Gliding || !Game.Pip.Grounded, "the glide carried Pip in");
            }

            yield return null;
            Assert.IsFalse(Game.Controls.GameplayEnabled, $"{approach}: input is ignored during the page-turn");
            float until = Time.time + 3f;
            while (Director.Travel.Active && Time.time < until)
            {
                Steer(Vector3.right); // the player keeps pushing through the page
                yield return null;
            }

            Set(_pad.leftStick, Vector2.zero, queueEventOnly: true);
            Release(_pad.buttonSouth, queueEventOnly: true);
            Assert.IsFalse(Director.Travel.Active, $"{approach}: the page-turn ends");
            Assert.AreEqual(before + 1, Director.Travel.Crossings, $"{approach}: one crossing");
        }

        private IEnumerator AssertArrival(Approach approach, int fps)
        {
            string context = $"{approach} at {fps} fps";
            for (int i = 0; i < 3; i++)
            {
                yield return null;
            }

            yield return SkipModals();
            yield return Wait(0.3f);
            Vector3 pip = Game.Pip.transform.position;
            Assert.Less(new Vector2(pip.x - Arrival.x, pip.z - Arrival.z).magnitude, 0.3f, $"{context}: at the arrival ({pip:F2})");
            Assert.AreEqual(Arrival.y, pip.y, 0.15f, $"{context}: on the attic floor");
            Assert.IsTrue(Game.Controls.GameplayEnabled, $"{context}: control is back after the page");
            Assert.IsTrue(Director.Framing.Active, $"{context}: the interior framing drives the lens");
            AssertLensInShell(context);
            Vector3 lens = Game.CameraRig.OutputCamera.transform.position;
            Assert.IsTrue(InteriorCamera.InAir(lens.x, lens.y, lens.z), $"{context}: the lens {lens:F2} is in the attic's air");
            Assert.AreEqual(0, Director.Rescues, $"{context}: the safety net has nothing to do");
            Assert.AreNotEqual(Lines.CaptionArrivalKnocks, Director.Captions.CurrentKey,
                $"{context}: the street's knock caption does not follow Pip indoors");
            AssertFramed(context);
        }

        /// <summary>
        /// S002 attic framing: no architecture fills the arrival frame. In the scene, rays through a 24 × 12 grid of
        /// screen points find no collider within 0.4 m of the lens over more than a post's width; in the Core model
        /// (<see cref="InteriorCamera.FrameCover"/>) no wall or slope beside the lens fills more than an eighth of it.
        /// </summary>
        private static void AssertFramed(string context)
        {
            var cam = Game.CameraRig.OutputCamera;
            float near = ScreenShareCloserThan(cam, 0.4f);
            Vector3 lens = cam.transform.position;
            float pitch = Mathf.DeltaAngle(0f, cam.transform.eulerAngles.x);
            float frame = InteriorCamera.FrameCover(lens.x, lens.y, lens.z, cam.transform.eulerAngles.y, pitch,
                Director.House.DoorOpen);
            TestContext.WriteLine($"{context}: {Director.Framing.Shot}, lens {lens:F2}, colliders within 0.4 m " +
                                  $"{near:P0} of the screen, frame cover {frame:P0}");
            Assert.LessOrEqual(near, 0.03f, $"{context}: nothing solid against the lens ({near:P0} of the screen)");
            Assert.LessOrEqual(frame, 0.15f, $"{context}: no wall fills the frame ({frame:P0}, {Director.Framing.Shot})");
        }

        /// <summary>The share of a 24 × 12 grid of screen rays that hit a collider (not Pip or a character) within <paramref name="within"/>.</summary>
        private static float ScreenShareCloserThan(Camera cam, float within)
        {
            const int columns = 24;
            const int rows = 12;
            int hits = 0;
            for (int i = 0; i < columns; i++)
            {
                for (int k = 0; k < rows; k++)
                {
                    var ray = cam.ViewportPointToRay(new Vector3((i + 0.5f) / columns, (k + 0.5f) / rows, 0f));
                    foreach (var hit in Physics.RaycastAll(ray, within, ~0, QueryTriggerInteraction.Ignore))
                    {
                        if (hit.collider.transform.IsChildOf(Game.Pip.transform) ||
                            hit.collider.GetComponentInParent<SpookyStreet.Characters.Witness>() != null)
                        {
                            continue;
                        }

                        hits++;
                        break;
                    }
                }
            }

            return hits / (float)(columns * rows);
        }

        /// <summary>From the arrival Pip walks along each world direction as far as the attic allows there (at most 2 m).</summary>
        private IEnumerator WalkAway(Approach approach, int fps)
        {
            var dirs = new[] { Vector3.right, Vector3.forward, Vector3.left, Vector3.back };
            foreach (var dir in dirs)
            {
                string context = $"{approach} at {fps} fps, walking {dir}";
                Game.Pip.Teleport(Arrival + Vector3.up * 0.03f);
                Game.Pip.transform.rotation = Quaternion.Euler(0f, Thresholds.DormerInside.ArriveYaw, 0f);
                Director.CutCamera();
                yield return Wait(0.3f);
                float free = InteriorWalkMap.FreeRun(Arrival.x, Arrival.y, Arrival.z, dir.x, dir.z, 3f, Director.House.DoorOpen);
                // Stop short of the window's exit zone (the walk is not a crossing) and of the trunk.
                float target = Mathf.Min(2f, free - (dir == Vector3.left ? 0.5f : 0.12f));
                int crossings = Director.Travel.Crossings;
                Vector3 from = Game.Pip.transform.position;
                float t = 0f;
                float along = 0f;
                while (t < 3f)
                {
                    along = Vector3.Dot(Game.Pip.transform.position - from, dir);
                    if (along >= target)
                    {
                        break;
                    }

                    Steer(dir);
                    AssertLensInShell(context);
                    t += Time.deltaTime;
                    yield return null;
                }

                Set(_pad.leftStick, Vector2.zero, queueEventOnly: true);
                TestContext.WriteLine($"{context}: {along:F2} m of {target:F2} (free run {free:F2}) in {t:F2} s");
                Assert.GreaterOrEqual(along, target - 0.05f, $"{context}: Pip walks away ({along:F2} of {target:F2} m)");
                Assert.AreEqual(crossings, Director.Travel.Crossings, $"{context}: no crossing on the walk");
                yield return Wait(0.2f);
                AssertLensInShell(context);
            }

            Assert.AreEqual(0, Director.Rescues, $"{approach}: the safety net never fired");
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator EveryDormerApproachArrivesWithControlAndRoomToWalkAt30Fps()
        {
            yield return EveryApproach(30);
        }

        [UnityTest]
        [Timeout(300000)]
        public IEnumerator EveryDormerApproachArrivesWithControlAndRoomToWalkAt60Fps()
        {
            yield return EveryApproach(60);
        }

        private IEnumerator EveryApproach(int fps)
        {
            Time.captureDeltaTime = 1f / fps;
            yield return StartGame();
            foreach (Approach approach in System.Enum.GetValues(typeof(Approach)))
            {
                yield return Enter(approach);
                yield return AssertArrival(approach, fps);
                yield return WalkAway(approach, fps);
            }
        }

        /// <summary>
        /// The safety net in the real scene: Pip pushing into the trunk by the front wall for several seconds, and Pip
        /// standing on top of the attic's roof plate (outside the shell), are both set down on a safe anchor (with a
        /// paper puff).
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator TheSafetyNetSetsPipDownOnASafeAnchor()
        {
            yield return StartGame();
            yield return Enter(Approach.Straight);
            yield return AssertArrival(Approach.Straight, 0);

            // Pushing south into the trunk by the front gable from open floor: a child just pushing. Never moved.
            Vector3 byTrunk = new Vector3(-1.3f, CaseRouteLayout.LoftY + 0.03f, CaseRouteLayout.InteriorFrontZ + 1.12f);
            Game.Pip.Teleport(byTrunk);
            Director.CutCamera();
            yield return Wait(0.3f);
            float t = 0f;
            while (t < 2f * InteriorRescue.StuckSeconds + 1f)
            {
                Steer(Vector3.back);
                t += Time.deltaTime;
                yield return null;
            }

            Set(_pad.leftStick, Vector2.zero, queueEventOnly: true);
            Assert.AreEqual(0, Director.Rescues, $"{t:F1} s of pushing into the trunk from open floor never moves Pip");
            Assert.GreaterOrEqual(Director.Rescue.EnclosureChecks, 1, "the long pushes were checked and found free");
            Assert.Less(Vector3.Distance(Game.Pip.transform.position, byTrunk), 0.5f, "Pip is still by the trunk");

            // Shut in: boxed in under the upper flight (unreachable on foot; as if glitched in), pushing north against
            // the back wall. After a few seconds Pip is set down elsewhere.
            int puffs = Game.Feedback != null ? Game.Feedback.PuffsPlayed : 0;
            Vector3 wedged = new Vector3(-1.5f, CaseRouteLayout.FloorY + 0.03f, CaseRouteLayout.InteriorFrontZ + 6.55f);
            Assert.IsTrue(InteriorSafety.Enclosed(wedged.x, wedged.y - 0.03f, wedged.z, Director.House.DoorOpen),
                "under the stair is shut in");
            Game.Pip.Teleport(wedged);
            Director.CutCamera();
            yield return Wait(0.3f);
            t = 0f;
            while (Director.Rescues == 0 && t < InteriorRescue.StuckSeconds + 2f)
            {
                Steer(Vector3.forward);
                t += Time.deltaTime;
                yield return null;
            }

            Set(_pad.leftStick, Vector2.zero, queueEventOnly: true);
            Assert.AreEqual(1, Director.Rescues, $"shut in under the stair: the safety net moves Pip (Pip at " +
                                                 $"{Game.Pip.transform.position:F2})");
            Assert.AreEqual(RescueReason.Stuck, Director.LastRescue);
            Assert.GreaterOrEqual(t, InteriorRescue.StuckSeconds - 0.2f, "only after several seconds of pushing");
            Vector3 pip = Game.Pip.transform.position;
            Assert.Greater(Vector3.Distance(pip, wedged), InteriorRescue.AwayDistance - 0.1f, "set down away from the spot");
            Assert.IsTrue(InteriorRescue.OnWalkableArea(pip.x, pip.y, pip.z, Director.House.DoorOpen), "on walkable floor");
            if (Game.Feedback != null && !AmbientMotion.ReducedMotion)
            {
                Assert.Greater(Game.Feedback.PuffsPlayed, puffs, "with a paper puff");
            }

            yield return Wait(0.3f);
            AssertLensInShell("after the rescue");
            // Setting Pip down in the attic's entry can replay its arrival beat: let any lines finish first.
            yield return SkipModals();
            Assert.IsFalse(Director.ModalOpen, "no conversation holds Pip");

            // Off the walkable area: Pip on top of the attic's roof plate (outside the shell, as if glitched out
            // through the slope). The safety net sets Pip down inside. (A fall out of the shell is caught first by
            // the fall return to the last safe ground, which is inside too.)
            float roofTop = CaseRouteLayout.KneeY + (CaseRouteLayout.InnerMaxX - 1.5f) * CaseRouteLayout.RoofSlope + 0.25f;
            var onRoof = new Vector3(1.5f, roofTop + 0.05f, CaseRouteLayout.InteriorFrontZ + 3f);
            Game.Pip.Teleport(onRoof);
            float until = Time.time + 2f;
            while (Director.Rescues == 1 && Time.time < until)
            {
                yield return null;
            }

            Assert.AreEqual(2, Director.Rescues, $"on the roof: the safety net moves Pip (Pip at " +
                $"{Game.Pip.transform.position:F2}, off for {Director.Rescue.OffTime:F2} s, grounded {Game.Pip.Grounded}, " +
                $"safe returns {Game.Pip.SafeReturns})");
            Assert.AreEqual(RescueReason.OffWalkable, Director.LastRescue);
            pip = Game.Pip.transform.position;
            Assert.IsTrue(CaseRouteLayout.Inside(pip.x, pip.z), "set down inside the interior");
            yield return Wait(0.5f);
            pip = Game.Pip.transform.position;
            Assert.IsTrue(InteriorRescue.OnWalkableArea(pip.x, pip.y, pip.z, Director.House.DoorOpen), "standing on a floor");
            AssertLensInShell("after the second rescue");
        }

        /// <summary>
        /// D26: captions describe what Pip perceives. If the knocks first come due while Pip is already inside the owl
        /// house (a save resumed indoors), they are heard through the floor from the workshop below: the floor hum
        /// caption, never "[Three knocks from the blue house] (to the right)".
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator KnocksHeardIndoorsAreCaptionedAsTheFloorHum()
        {
            yield return StartGame(false);
            Assert.IsFalse(Director.State.Has(CaseFact.HeardKnocks), "the knocks have not come yet");
            Game.Pip.Teleport(Arrival + Vector3.up * 0.03f);
            Game.Pip.transform.rotation = Quaternion.Euler(0f, Thresholds.DormerInside.ArriveYaw, 0f);
            Director.CutCamera();
            float until = Time.time + 2f;
            while (!Director.State.Has(CaseFact.HeardKnocks) && Time.time < until)
            {
                yield return null;
            }

            Assert.IsTrue(Director.State.Has(CaseFact.HeardKnocks), "the knocks still come (the story beat is kept)");
            for (int i = 0; i < 3; i++)
            {
                yield return null;
            }

            Assert.AreEqual(Lines.CaptionFoyerHum, Director.Captions.CurrentKey, "heard through the floor indoors");
            Assert.IsTrue(Director.Captions.HasDirectionCue, "pointing at the workshop below");
            yield return SkipModals();
        }

        /// <summary>
        /// Pip landing on June's head (a jump from the trunk, say) is never teleported: the safety net slides her gently
        /// off June, and she drops to the attic floor beside her.
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator StandingOnJuneSlidesPipOffBesideHer()
        {
            yield return StartGame();
            yield return Enter(Approach.Straight);
            yield return AssertArrival(Approach.Straight, 0);
            var june = Director.Cast != null ? Director.Cast.June : null;
            if (june == null || !june.isActiveAndEnabled)
            {
                Assert.Ignore("June is not staged in the attic.");
            }

            Vector3 j = june.transform.position;
            Vector3 onHead = j + new Vector3(0.08f, InteriorRescue.CharacterHeight + 0.05f, 0f);
            Game.Pip.Teleport(onHead);
            int slides = Director.SlideOffFrames;
            float until = Time.time + 3f;
            bool landed = false;
            while (Time.time < until)
            {
                Vector3 p = Game.Pip.transform.position;
                if (Game.Pip.Grounded && p.y < j.y + 0.3f)
                {
                    landed = true;
                    break;
                }

                yield return null;
            }

            Vector3 pip = Game.Pip.transform.position;
            TestContext.WriteLine($"on June's head: {Director.SlideOffFrames - slides} slide frames, landed at {pip:F2}");
            Assert.AreEqual(0, Director.Rescues, "never teleported off June");
            Assert.IsTrue(landed, $"Pip came down off June onto the floor (at {pip:F2})");
            Assert.Less(new Vector2(pip.x - j.x, pip.z - j.z).magnitude, 1.5f, "beside June, not set down elsewhere");
            Assert.IsTrue(InteriorRescue.OnWalkableArea(pip.x, pip.y, pip.z, Director.House.DoorOpen), "on the attic floor");
        }
    }
}
