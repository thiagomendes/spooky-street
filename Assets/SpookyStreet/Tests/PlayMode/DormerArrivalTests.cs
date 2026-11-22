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

        private static IEnumerator StartGame()
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
                    start = new Vector3(-5.0f, 4.0f, wz);
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
            yield return Wait(approach == Approach.Glide ? 0.25f : 0.8f);
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
        /// out of the shell beside the interior, are both set down on a safe anchor (with a paper puff).
        /// </summary>
        [UnityTest]
        [Timeout(120000)]
        public IEnumerator TheSafetyNetSetsPipDownOnASafeAnchor()
        {
            yield return StartGame();
            yield return Enter(Approach.Straight);
            yield return AssertArrival(Approach.Straight, 0);

            // Pushing south into the trunk by the front wall: no way on, so after a few seconds Pip is set down elsewhere.
            int puffs = Game.Feedback != null ? Game.Feedback.PuffsPlayed : 0;
            Vector3 wedged = new Vector3(-1.3f, CaseRouteLayout.LoftY + 0.03f, CaseRouteLayout.InteriorFrontZ + 1.12f);
            Game.Pip.Teleport(wedged);
            Director.CutCamera();
            yield return Wait(0.3f);
            float t = 0f;
            while (Director.Rescues == 0 && t < InteriorRescue.StuckSeconds + 2f)
            {
                Steer(Vector3.back);
                t += Time.deltaTime;
                yield return null;
            }

            Set(_pad.leftStick, Vector2.zero, queueEventOnly: true);
            Assert.AreEqual(1, Director.Rescues, "pushed without getting anywhere: the safety net moves Pip");
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

            // Out of the shell (beside the interior, in its band): Pip falls with nothing below; the safety net sets
            // Pip down inside before the street's fall return would.
            var beside = new Vector3(CaseRouteLayout.InnerMaxX + 1.5f, CaseRouteLayout.LoftY, CaseRouteLayout.InteriorFrontZ + 3f);
            Game.Pip.Teleport(beside);
            float until = Time.time + 2f;
            while (Director.Rescues == 1 && Time.time < until)
            {
                yield return null;
            }

            Assert.AreEqual(2, Director.Rescues, $"out of the shell: the safety net moves Pip (Pip at " +
                $"{Game.Pip.transform.position:F2}, off for {Director.Rescue.OffTime:F2} s, input " +
                $"{Game.Controls.GameplayEnabled}, modal {Director.ModalOpen}, safe returns {Game.Pip.SafeReturns})");
            Assert.AreEqual(RescueReason.OffWalkable, Director.LastRescue);
            pip = Game.Pip.transform.position;
            Assert.IsTrue(CaseRouteLayout.Inside(pip.x, pip.z), "set down inside the interior");
            yield return Wait(0.5f);
            pip = Game.Pip.transform.position;
            Assert.IsTrue(InteriorRescue.OnWalkableArea(pip.x, pip.y, pip.z, Director.House.DoorOpen), "standing on a floor");
            AssertLensInShell("after the second rescue");
        }
    }
}
