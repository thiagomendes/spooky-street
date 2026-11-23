using System.Collections;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Controls;
using SpookyStreet.Core.Animation;
using SpookyStreet.Core.Localization;
using SpookyStreet.Core.Movement;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Core.Tips;
using SpookyStreet.Core.World;
using SpookyStreet.Localization;
using SpookyStreet.Player;
using SpookyStreet.World;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// S003 wall slide, wall jump and ledge grab in the real scene, driven with the gamepad stick and South button:
    /// a slide and a kick on the shed's west side under the treehouse; a chain between two marked test walls (a
    /// test-only rig outside the block); a catch, a climb and a drop on the street lamp's arm; and no slide or catch on
    /// unmarked surfaces (the owl house siding, the porch roof and an unmarked twin of a marked ledge). The rules and
    /// the numbers are covered by the Core WallLedgeTests and TraversalMarkTests.
    /// </summary>
    public class WallLedgeTests : InputTestFixture
    {
        private const string SceneName = "LanternLane";

        /// <summary>The test rig's floor centre: south of the block, beyond the boundary wall and the lawn ring.</summary>
        private static readonly Vector3 RigCentre = new Vector3(0f, 0f, -45f);

        private Gamepad _pad;
        private int _slides;
        private int _kicks;
        private int _grabs;
        private int _climbs;
        private int _drops;

        public override void Setup()
        {
            base.Setup();
            _pad = InputSystem.AddDevice<Gamepad>();
            Localizer.ResetForTests();
            CaseDirector.UseTemporarySaveDirectory(); // no save leaks between tests (S002)
            _slides = _kicks = _grabs = _climbs = _drops = 0;
        }

        public override void TearDown()
        {
            ControlSchemeTracker.Override = null;
            base.TearDown();
        }

        private IEnumerator StartGame()
        {
            yield return SceneManager.LoadSceneAsync(SceneName, LoadSceneMode.Single);
            yield return null;
            var game = GameBootstrap.Instance;
            Assert.IsNotNull(game, "scene did not create GameBootstrap");
            game.Ui.NewGameButton.onClick.Invoke();
            for (int i = 0; i < 10; i++)
            {
                yield return null;
            }

            game.Ui.Tips.UseStore(new MemoryTipStore());
            game.Pip.Stepped += Count;
        }

        private void Count(MotorStep step)
        {
            _slides += step.WallSlideStarted ? 1 : 0;
            _kicks += step.WallJumped ? 1 : 0;
            _grabs += step.LedgeGrabbed ? 1 : 0;
            _climbs += step.ClimbStarted ? 1 : 0;
            _drops += step.LedgeDropped ? 1 : 0;
        }

        private static IEnumerator Wait(float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                yield return null;
            }
        }

        /// <summary>Pushes the left stick so Pip moves along world <paramref name="direction"/> (camera-relative input).</summary>
        private void Steer(Vector3 direction)
        {
            var camera = GameBootstrap.Instance.CameraRig.OutputCamera.transform;
            Vector3 forward = Vector3.ProjectOnPlane(camera.forward, Vector3.up).normalized;
            Vector3 right = Vector3.Cross(Vector3.up, forward);
            direction.y = 0f;
            direction.Normalize();
            var stick = new Vector2(Vector3.Dot(direction, right), Vector3.Dot(direction, forward));
            Set(_pad.leftStick, stick.sqrMagnitude > 1e-4f ? stick.normalized : Vector2.zero);
        }

        private void StopSteering() => Set(_pad.leftStick, Vector2.zero);

        private void JumpDown() => Press(_pad.buttonSouth, queueEventOnly: true);

        private void JumpUp() => Release(_pad.buttonSouth, queueEventOnly: true);

        private IEnumerator TapJump()
        {
            JumpDown();
            yield return null;
            JumpUp();
            yield return null;
        }

        /// <summary>Holds the stick along <paramref name="direction"/> for <paramref name="seconds"/> (re-aimed every frame).</summary>
        private IEnumerator SteerFor(Vector3 direction, float seconds)
        {
            float until = Time.time + seconds;
            while (Time.time < until)
            {
                Steer(direction);
                yield return null;
            }
        }

        private static void Face(PipMotor pip, Vector3 direction) =>
            pip.transform.rotation = Quaternion.LookRotation(new Vector3(direction.x, 0f, direction.z), Vector3.up);

        /// <summary>Skips any case line that happens to open, so the stick keeps driving Pip.</summary>
        private static void KeepPlaying()
        {
            var dialogue = GameBootstrap.Instance.Director.Dialogue;
            if (dialogue.IsOpen)
            {
                dialogue.Skip();
            }
        }

        // --- The shed's west side --------------------------------------------------------------------------------

        /// <summary>On the lawn in the yard pocket west of the tall shed, north of the treehouse deck.</summary>
        private static readonly Vector3 ShedPocket = new Vector3(CaseRouteLayout.RoofMinX - 0.85f, 0.05f, 14.1f);

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator SlidesDownAndKicksOffTheShedWall()
        {
            yield return StartGame();
            var game = GameBootstrap.Instance;
            var pip = game.Pip;
            game.Ui.FirstPrompt.SetActive(false);
            pip.Teleport(ShedPocket);
            Face(pip, Vector3.right);
            yield return Wait(0.3f);
            Assert.IsTrue(pip.Grounded, "standing in the yard pocket by the shed");

            // A held high jump into the shed's west face, the stick pushed at the wall.
            JumpDown();
            float deadline = Time.time + 1.6f;
            while (!pip.WallSliding && Time.time < deadline)
            {
                Steer(Vector3.right);
                KeepPlaying();
                yield return null;
            }

            JumpUp();
            Assert.IsTrue(pip.WallSliding, "falling against the marked shed wall slides");
            Assert.GreaterOrEqual(_slides, 1);
            Assert.AreEqual(SurfaceMarks.WallJump, pip.LastWallProbe.Marks);
            Assert.Less(pip.LastWallProbe.NormalX, -0.95f, "the west face");
            Assert.Greater(Vector3.Dot(pip.transform.forward, Vector3.right), 0.9f, "Pip faces the wall");

            // The thumb rests: the slide goes on, capped (measured once any faster fall has been braked).
            StopSteering();
            yield return Wait(0.15f);
            float fastest = 0f;
            for (float t = 0f; t < 0.3f && pip.WallSliding; t += Time.deltaTime)
            {
                fastest = Mathf.Max(fastest, -pip.VerticalSpeed);
                yield return null;
            }

            Assert.IsTrue(pip.WallSliding, "still sliding with the stick released");
            Assert.LessOrEqual(fastest, pip.TuningData.WallSlideSpeed + 0.05f, "the slide caps the fall");
            var animator = pip.GetComponent<PipAvatar>().Animator;
            if (animator != null)
            {
                Assert.AreEqual(PipPose.WallSlide, animator.Pose);
            }

            // Kick.
            float x = pip.transform.position.x;
            yield return TapJump();
            Assert.AreEqual(1, _kicks, "Jump kicks off the wall");
            yield return Wait(0.3f);
            Assert.Less(pip.transform.position.x, x - 0.8f, "away from the shed, to the west");
            Assert.IsFalse(pip.WallSliding);
            var feedback = Object.FindAnyObjectByType<MoveFeedback>();
            Assert.IsNotNull(feedback);
            Assert.IsTrue((feedback.Played & MoveBursts.WallKickPuff) != 0, "the kick puffs paper off the wall");

            deadline = Time.time + 2f;
            while (!pip.Grounded && Time.time < deadline)
            {
                yield return null;
            }

            Assert.IsTrue(pip.Grounded);
            Assert.AreEqual(0, pip.SafeReturns);
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator TheOwlHouseSidingNeverStartsASlide()
        {
            yield return StartGame();
            var game = GameBootstrap.Instance;
            var pip = game.Pip;
            game.Ui.FirstPrompt.SetActive(false);
            // The lawn between the shed and the owl house's west wall (x -3.05), south of the bridge.
            pip.Teleport(new Vector3(-3.95f, 0.05f, 12.3f));
            Face(pip, Vector3.right);
            yield return Wait(0.3f);
            for (int jump = 0; jump < 2; jump++)
            {
                // A held high jump into the siding, then Jump mashed against it all the way down.
                JumpDown();
                float until = Time.time + 1.5f;
                for (int frame = 0; Time.time < until; frame++)
                {
                    Steer(Vector3.right);
                    KeepPlaying();
                    if (frame > 20 && frame % 6 == 0)
                    {
                        JumpUp();
                    }
                    else if (frame > 20 && frame % 6 == 3)
                    {
                        JumpDown();
                    }

                    Assert.IsFalse(pip.WallSliding, "the house siding is unmarked");
                    yield return null;
                }

                JumpUp();
                StopSteering();
                yield return Wait(0.3f);
            }

            Assert.AreEqual(0, _slides);
            Assert.AreEqual(0, _kicks);
            Assert.AreEqual(0, _grabs);
        }

        // --- Test-only rig: two marked walls, a marked and an unmarked ledge -------------------------------------

        private static GameObject Box(Transform parent, string name, Vector3 centre, Vector3 size, SurfaceMarks marks)
        {
            var go = GameObject.CreatePrimitive(PrimitiveType.Cube);
            go.name = name;
            go.transform.SetParent(parent, false);
            go.transform.position = centre;
            go.transform.localScale = size;
            ClimbSurface.Mark(go, marks, Vector3.zero);
            return go;
        }

        /// <summary>
        /// Floor (safe ground), two marked 8 m walls facing each other 2 m apart (faces at x ±1 from the centre), and two
        /// 2.4 m blocks 4 m east: the north one marked as a ledge, the south one identical but unmarked.
        /// </summary>
        private static Transform BuildRig()
        {
            var rig = new GameObject("S003_TestRig").transform;
            var floor = Box(rig, "Floor", RigCentre + new Vector3(2f, -0.25f, 0f), new Vector3(14f, 0.5f, 14f),
                SurfaceMarks.None);
            int safe = LayerMask.NameToLayer(SafeGroundTracker.SafeGroundLayerName);
            floor.layer = safe >= 0 ? safe : 0;
            Box(rig, "WallWest", RigCentre + new Vector3(-1.5f, 4f, 0f), new Vector3(1f, 8f, 3f), SurfaceMarks.WallJump);
            Box(rig, "WallEast", RigCentre + new Vector3(1.5f, 4f, 0f), new Vector3(1f, 8f, 3f), SurfaceMarks.WallJump);
            Box(rig, "LedgeMarked", RigCentre + new Vector3(5f, 1.2f, 3f), new Vector3(2f, 2.4f, 2f), SurfaceMarks.Ledge);
            Box(rig, "LedgeUnmarked", RigCentre + new Vector3(5f, 1.2f, -3f), new Vector3(2f, 2.4f, 2f), SurfaceMarks.None);
            Physics.SyncTransforms();
            return rig;
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ChainedWallJumpsClimbBetweenTwoMarkedWalls()
        {
            yield return StartGame();
            var game = GameBootstrap.Instance;
            var pip = game.Pip;
            game.Ui.FirstPrompt.SetActive(false);
            var rig = BuildRig();
            pip.Teleport(RigCentre + new Vector3(0.5f, 0.05f, 0f));
            Face(pip, Vector3.right);
            yield return Wait(0.3f);
            Assert.IsTrue(pip.Grounded, "on the rig floor");
            float startY = pip.transform.position.y;

            // A held jump at the east wall, then a kick whenever Pip touches a wall, steering at the other one.
            Vector3 toward = Vector3.right;
            JumpDown();
            yield return null;
            JumpUp();
            float highest = startY;
            float deadline = Time.time + 6f;
            while (_kicks < 5 && Time.time < deadline)
            {
                Steer(toward);
                KeepPlaying();
                var probe = pip.LastWallProbe;
                bool touching = pip.WallSliding || (probe.Hit && probe.Distance < 0.4f);
                if (touching && !pip.Grounded)
                {
                    int before = _kicks;
                    yield return TapJump();
                    if (_kicks > before)
                    {
                        toward = -toward;
                    }
                }
                else
                {
                    yield return null;
                }

                highest = Mathf.Max(highest, pip.transform.position.y);
            }

            Debug.Log($"[S003] chain: {_kicks} kicks, {_slides} slides, highest feet {highest - startY:F2} m");
            Assert.GreaterOrEqual(_kicks, 4, "back-and-forth kicks between the walls");
            Assert.Greater(highest - startY, 3.5f, "the chain climbs well above a held jump (2.1 m)");
            StopSteering();
            Object.Destroy(rig.gameObject);
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator AnUnmarkedTwinLedgeIsNeverCaughtButTheMarkedOneIs()
        {
            yield return StartGame();
            var game = GameBootstrap.Instance;
            var pip = game.Pip;
            game.Ui.FirstPrompt.SetActive(false);
            var rig = BuildRig();

            // The unmarked block: a held jump at its face, falling past its edge, never hangs.
            pip.Teleport(RigCentre + new Vector3(3.3f, 0.05f, -3f));
            Face(pip, Vector3.right);
            yield return Wait(0.3f);
            JumpDown();
            yield return SteerFor(Vector3.right, 1.4f);
            JumpUp();
            Assert.AreEqual(0, _grabs, "no catch on the unmarked edge");
            Assert.IsFalse(pip.Hanging);

            // Its marked twin: the same jump hangs.
            pip.Teleport(RigCentre + new Vector3(3.3f, 0.05f, 3f));
            Face(pip, Vector3.right);
            yield return Wait(0.3f);
            JumpDown();
            float deadline = Time.time + 1.4f;
            while (!pip.Hanging && Time.time < deadline)
            {
                Steer(Vector3.right);
                yield return null;
            }

            JumpUp();
            StopSteering();
            Assert.IsTrue(pip.Hanging, "the marked twin is caught");
            Object.Destroy(rig.gameObject);
        }

        // --- The street lamp's arm (the ledge) ----------------------------------------------------------------------

        /// <summary>The lamp head's collider (CollisionRules StreetLamp "Head", marked as a ledge).</summary>
        private static BoxCollider LampHead()
        {
            var game = GameBootstrap.Instance;
            foreach (var p in EnvironmentLayout.Street)
            {
                if (p.Model != EnvironmentLayout.StreetLamp)
                {
                    continue;
                }

                foreach (var t in game.Environment.Root.GetComponentsInChildren<Transform>(true))
                {
                    if (t.name == ModelColliders.PartPrefix + "Head" &&
                        Vector3.Distance(t.parent.position, new Vector3(p.X, p.Y, p.Z)) < 0.1f)
                    {
                        return t.GetComponent<BoxCollider>();
                    }
                }
            }

            return null;
        }

        /// <summary>Stands Pip on the path beside the lamp arm's long side, facing it, and returns the face's outward normal.</summary>
        private IEnumerator StandBesideLamp(BoxCollider head, System.Action<Vector3> normal)
        {
            var pip = GameBootstrap.Instance.Pip;
            Bounds b = head.bounds;
            // The long side away from the arrival path's centre line is clear of the pole: take the face whose normal is
            // the head's local Z, on the side nearer the street (either works; both are open air).
            Vector3 n = head.transform.forward;
            n.y = 0f;
            n.Normalize();
            Vector3 centre = b.center;
            Vector3 stand = centre + n * (head.size.z * 0.5f + 0.75f);
            stand.y = 0.05f;
            pip.Teleport(stand);
            Face(pip, -n);
            yield return Wait(0.35f);
            Assert.IsTrue(pip.Grounded, "standing on the path beside the lamp");
            normal(n);
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator AHighJumpCatchesTheLampArmAndJumpClimbsOnTop()
        {
            yield return StartGame();
            var game = GameBootstrap.Instance;
            var pip = game.Pip;
            game.Ui.FirstPrompt.SetActive(false);
            if (!game.Environment.UsesModels)
            {
                Assert.Ignore("Environment models are not committed.");
            }

            var head = LampHead();
            Assert.IsNotNull(head, "the street lamp's head collider");
            Assert.IsTrue(head.TryGetComponent(out ClimbSurface surface), "the lamp arm is marked");
            Assert.AreEqual(SurfaceMarks.Ledge, surface.Marks);
            float top = head.bounds.max.y;
            Assert.AreEqual(3.0f, top, 0.05f);
            Vector3 n = Vector3.zero;
            yield return StandBesideLamp(head, v => n = v);
            // The earlier move tips count as learned, so none of them (or its cooldown) holds back the climb tip.
            foreach (var learned in new[] { MoveTip.HoldJump, MoveTip.ChainJump, MoveTip.Glide, MoveTip.Dive })
            {
                game.Ui.Tips.Scheduler.Performed(learned);
            }

            // A tap falls short.
            yield return TapJump();
            // Stay put: the stick off, so the tap only rises and lands where Pip stood.
            StopSteering();
            yield return Wait(1.0f);
            Assert.AreEqual(0, _grabs, "a 1 m tap does not reach a 3 m ledge");
            yield return StandBesideLamp(head, v => n = v);

            // A held high jump, the stick at the arm: catch and hang.
            JumpDown();
            float deadline = Time.time + 1.6f;
            while (!pip.Hanging && Time.time < deadline)
            {
                Steer(-n);
                KeepPlaying();
                yield return null;
            }

            JumpUp();
            StopSteering();
            Assert.IsTrue(pip.Hanging, "the high jump catches the lamp arm");
            yield return Wait(0.5f);
            Vector3 feet = pip.transform.position;
            Assert.AreEqual(top - pip.TuningData.LedgeHangHandHeight, feet.y, 0.06f, "hands on the top");
            float out_ = Vector3.Dot(feet - head.bounds.ClosestPoint(feet), n);
            Assert.AreEqual(pip.TuningData.LedgeHangWallOffset, out_, 0.06f, "hanging 0.34 m out from the face");
            Assert.Greater(Vector3.Dot(pip.transform.forward, -n), 0.9f, "facing the lamp arm");
            var avatar = pip.GetComponent<PipAvatar>();
            Assert.Greater(avatar.HangLean, 0.9f, "the body leans in so the hand meets the edge");
            if (avatar.Animator != null)
            {
                Assert.AreEqual(PipPose.LedgeHang, avatar.Animator.Pose);
            }

            var feedback = Object.FindAnyObjectByType<MoveFeedback>();
            Assert.IsTrue((feedback.Played & MoveBursts.LedgeGrabDust) != 0, "a little dust at the hands");
            Assert.AreEqual(0, pip.SafeReturns, "hanging is not falling");
            Assert.AreEqual(MoveTip.LedgeClimb, game.Ui.Tips.Shown, "the first hang teaches the climb");

            // Jump climbs onto the arm.
            yield return TapJump();
            Assert.AreEqual(1, _climbs);
            deadline = Time.time + 1.5f;
            while (!pip.Grounded && Time.time < deadline)
            {
                yield return null;
            }

            yield return Wait(0.2f);
            Assert.IsTrue(pip.Grounded, "standing on the lamp arm");
            Assert.AreEqual(top, pip.transform.position.y, 0.08f);
            Assert.IsTrue(head.bounds.Contains(new Vector3(pip.transform.position.x, top - 0.01f, pip.transform.position.z)),
                "on top of the arm, not beside it");
            Assert.AreNotEqual(MoveTip.LedgeClimb, game.Ui.Tips.Scheduler.Current, "climbing ends the tip");
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator PullingAwayDropsFromTheLampArm()
        {
            yield return StartGame();
            var game = GameBootstrap.Instance;
            var pip = game.Pip;
            game.Ui.FirstPrompt.SetActive(false);
            if (!game.Environment.UsesModels)
            {
                Assert.Ignore("Environment models are not committed.");
            }

            var head = LampHead();
            Vector3 n = Vector3.zero;
            yield return StandBesideLamp(head, v => n = v);
            JumpDown();
            float deadline = Time.time + 1.6f;
            while (!pip.Hanging && Time.time < deadline)
            {
                Steer(-n);
                yield return null;
            }

            JumpUp();
            StopSteering();
            Assert.IsTrue(pip.Hanging);
            yield return Wait(0.4f);
            Assert.IsTrue(pip.Hanging, "a resting stick keeps the hang");

            // Pull the stick away from the arm: she lets go and falls to the path, without catching it again.
            yield return SteerFor(n, 0.25f);
            StopSteering();
            Assert.AreEqual(1, _drops, "pulling away drops");
            Assert.IsFalse(pip.Hanging);
            deadline = Time.time + 2f;
            while (!pip.Grounded && Time.time < deadline)
            {
                Assert.IsFalse(pip.Hanging, "no re-catch on the way down");
                yield return null;
            }

            Assert.IsTrue(pip.Grounded, "back on the path");
            Assert.Less(pip.transform.position.y, 0.5f);
            Assert.AreEqual(1, _grabs);
            Assert.AreEqual(0, pip.SafeReturns);
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator ThePorchRoofEdgeIsNeverCaught()
        {
            yield return StartGame();
            var game = GameBootstrap.Instance;
            var pip = game.Pip;
            game.Ui.FirstPrompt.SetActive(false);
            // On the path just outside the porch roof's front eave (about 2.9 m up, its face about 4.76 m in front of the
            // house origin), facing it: a held jump puts the eave in the catch window, so a marked edge would be caught.
            var spot = new Vector3(0f, 0.05f, EnvironmentLayout.HouseZ - 4.76f - 0.55f);
            for (int jump = 0; jump < 3; jump++)
            {
                pip.Teleport(spot);
                Face(pip, Vector3.forward);
                yield return Wait(0.3f);
                JumpDown();
                float deadline = Time.time + 1.4f;
                while (Time.time < deadline)
                {
                    KeepPlaying();
                    Assert.IsFalse(pip.Hanging, "the porch roof is unmarked");
                    yield return null;
                }

                JumpUp();
                yield return Wait(0.3f);
            }

            Assert.AreEqual(0, _grabs);
        }

        [UnityTest]
        [Timeout(120000)]
        public IEnumerator TheWallTipShowsTheGamepadJumpGlyph()
        {
            yield return StartGame();
            var game = GameBootstrap.Instance;
            var pip = game.Pip;
            game.Ui.FirstPrompt.SetActive(false);
            ControlSchemeTracker.Override = ControlScheme.Gamepad;
            var rig = BuildRig();
            pip.Teleport(RigCentre + new Vector3(0.6f, 3.5f, 0f));
            Face(pip, Vector3.right);
            var tips = game.Ui.Tips;
            float deadline = Time.time + 2f;
            while (tips.Shown != MoveTip.WallJump && Time.time < deadline)
            {
                Steer(Vector3.right);
                yield return null;
            }

            StopSteering();
            Assert.AreEqual(MoveTip.WallJump, tips.Shown, "a lingering slide teaches the kick");
            Assert.AreEqual(LocalizationKeys.TipWallJump, tips.Tag.GetComponentInChildren<LocalizedText>(true).Key);
            Assert.IsTrue(tips.Glyph.HasGlyph, "the gamepad glyph shows on the tag");
            Assert.AreEqual(PadButton.South, tips.Glyph.LitButton, "the Jump face button is lit");
            Assert.IsFalse(tips.Icon.gameObject.activeSelf, "the touch icon gives way to the glyph");

            ControlSchemeTracker.Override = ControlScheme.Touch;
            yield return null;
            Assert.IsFalse(tips.Glyph.HasGlyph, "on touch the Jump disc itself is the glyph");
            Assert.IsTrue(tips.Icon.gameObject.activeSelf);

            yield return TapJump();
            Assert.GreaterOrEqual(_kicks, 1);
            yield return Wait(0.1f);
            Assert.AreNotEqual(MoveTip.WallJump, tips.Scheduler.Current, "the kick ends the tip");
            Object.Destroy(rig.gameObject);
        }
    }
}
