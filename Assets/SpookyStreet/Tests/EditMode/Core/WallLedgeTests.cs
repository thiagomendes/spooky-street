using System;
using NUnit.Framework;
using SpookyStreet.Core.Animation;
using SpookyStreet.Core.Movement;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// S003 wall slide, wall jump and ledge grab in the engine-free motor, on <see cref="ClimbTestWorld"/>: the contact
    /// rules, the slide's speed cap, the kick vector, the input windows and coyote time, two-wall chains, the ledge
    /// geometry (catch window, hang spot, climb-up placement, drop), and that unmarked surfaces never trigger anything.
    /// Every trajectory runs at 30 and 60 fps.
    /// </summary>
    public class WallLedgeTests
    {
        private const SurfaceMarks Wall = SurfaceMarks.WallJump;
        private const SurfaceMarks Ledge = SurfaceMarks.Ledge;

        /// <summary>A 4 m tall wall whose west face is at x = 0 (Pip lives at x &lt; 0).</summary>
        private static ClimbTestWorld WestFacingWall(SurfaceMarks marks) =>
            new ClimbTestWorld().Add(0f, 0f, -3f, 1f, 4f, 3f, marks);

        private static MotorInput Stick(float x, float z, bool jumpPressed = false, bool jumpHeld = false) =>
            new MotorInput { MoveX = x, MoveZ = z, JumpPressed = jumpPressed, JumpHeld = jumpHeld };

        /// <summary>Drops Pip from <paramref name="feetY"/> against the west face, stick into it, until she slides.</summary>
        private static ClimbTestWorld SlidingOnWall(float fps, float feetY = 3f)
        {
            var world = WestFacingWall(Wall);
            world.Place(-0.3f, feetY, 0f);
            world.FacingX = 1f;
            world.FacingZ = 0f;
            float dt = 1f / fps;
            for (int i = 0; i < 30 && !world.State.WallSliding; i++)
            {
                world.Frame(Stick(1f, 0f), dt);
            }

            Assert.IsTrue(world.State.WallSliding, "falling against a marked wall with the stick into it slides");
            return world;
        }

        // --- Wall contact rules -------------------------------------------------------------------------------------

        private static MotorState Falling()
        {
            var state = MotorState.Initial;
            state.Grounded = false;
            state.TimeSinceGrounded = 1f;
            state.VelocityY = -2f;
            return state;
        }

        private static MotorInput AtWall(float distance = 0.3f, float clearance = 2f, SurfaceMarks marks = Wall) =>
            new MotorInput
            {
                MoveX = 1f,
                Wall = new WallProbe
                {
                    Hit = true, Marks = marks, NormalX = -1f, NormalZ = 0f, Distance = distance,
                    FeetClearance = clearance,
                },
            };

        [Test]
        public void WallContactNeedsAMarkedFaceCloseByWithAirUnderTheFeet()
        {
            var tuning = new MovementTuningData();
            var state = Falling();
            Assert.IsTrue(ClimbRules.CanStartWallSlide(state, AtWall(), tuning), "falling into a marked wall slides");
            Assert.IsFalse(ClimbRules.CanStartWallSlide(state, AtWall(marks: SurfaceMarks.None), tuning), "unmarked");
            Assert.IsFalse(ClimbRules.CanStartWallSlide(state, AtWall(marks: SurfaceMarks.Ledge), tuning),
                "a ledge-only mark is not a wall");
            Assert.IsTrue(ClimbRules.CanStartWallSlide(state, AtWall(distance: 0.39f), tuning), "radius + slack");
            Assert.IsFalse(ClimbRules.CanStartWallSlide(state, AtWall(distance: 0.45f), tuning), "too far from the face");
            Assert.IsFalse(ClimbRules.CanStartWallSlide(state, AtWall(clearance: 0.2f), tuning), "too near the ground");

            var input = AtWall();
            input.MoveX = 0f;
            Assert.IsFalse(ClimbRules.CanStartWallSlide(state, input, tuning), "neither steering nor moving into it");
            state.VelocityX = 2f;
            Assert.IsTrue(ClimbRules.CanStartWallSlide(state, input, tuning), "moving into the wall is enough");

            var rising = Falling();
            rising.VelocityY = 1f;
            Assert.IsFalse(ClimbRules.CanStartWallSlide(rising, AtWall(), tuning), "no slide while rising");
            Assert.IsTrue(ClimbRules.WallUsable(rising, AtWall(), tuning), "but the wall still counts for a kick");

            var grounded = MotorState.Initial;
            Assert.IsFalse(ClimbRules.CanStartWallSlide(grounded, AtWall(), tuning), "never from the ground");
            var takeOff = Falling();
            takeOff.TimeSinceGrounded = 0.05f;
            Assert.IsFalse(ClimbRules.CanStartWallSlide(takeOff, AtWall(), tuning), "not inside coyote time");
        }

        [Test]
        public void UnmarkedSurfacesNeverTriggerAnyMove([Values(30f, 60f)] float fps)
        {
            float dt = 1f / fps;

            // An unmarked wall: falling into it, mashing Jump, never slides or kicks.
            var wall = WestFacingWall(SurfaceMarks.None);
            wall.ReportUnmarked = true;
            wall.Place(-0.3f, 3.5f, 0f);
            for (int i = 0; i < (int)(5f * fps) && !wall.Grounded; i++)
            {
                MotorStep step = wall.Frame(Stick(1f, 0f, i % 4 == 0, true), dt);
                Assert.IsFalse(wall.State.WallSliding, "never slides");
                Assert.IsFalse(step.WallJumped || step.WallSlideStarted, "never kicks or slides at frame " + i);
            }

            Assert.IsTrue(wall.Grounded, "falls to the ground beside the unmarked wall");

            // An unmarked ledge: falling past its edge in reach never catches it.
            var ledge = new ClimbTestWorld().Add(0f, 0f, -2f, 2f, 2.4f, 2f, SurfaceMarks.None);
            ledge.ReportUnmarked = true;
            ledge.Place(-0.3f, 2.4f, 0f);
            ledge.FacingX = 1f;
            ledge.FacingZ = 0f;
            for (int i = 0; i < (int)(2f * fps) && !ledge.Grounded; i++)
            {
                MotorStep step = ledge.Frame(Stick(1f, 0f, false, false), dt);
                Assert.IsFalse(step.LedgeGrabbed || ledge.State.Hanging, "never hangs from an unmarked edge");
            }

            // The rules alone: a perfect probe with no mark is ignored.
            var tuning = new MovementTuningData();
            var state = Falling();
            var probe = new MotorInput
            {
                FacingX = 1f,
                Ledge = new LedgeProbe
                {
                    Hit = true, Marks = SurfaceMarks.None, NormalX = -1f, EdgeDistance = 0.3f, TopAbove = 1.27f,
                    RoomToClimb = true, RoomToHang = true, HangClearance = 2f,
                },
            };
            Assert.IsFalse(ClimbRules.CanGrabLedge(state, probe, tuning));
            probe.Ledge.Marks = SurfaceMarks.WallJump;
            Assert.IsFalse(ClimbRules.CanGrabLedge(state, probe, tuning), "a wall-only mark is not a ledge");
            probe.Ledge.Marks = SurfaceMarks.Ledge;
            Assert.IsTrue(ClimbRules.CanGrabLedge(state, probe, tuning), "the same probe, marked, is caught");
        }

        // --- Slide ------------------------------------------------------------------------------------------------

        [Test]
        public void TheSlideGripsThenCapsTheFallAtOnePointFive([Values(30f, 60f)] float fps)
        {
            var world = WestFacingWall(Wall);
            world.Place(-0.3f, 3.6f, 0f);
            world.FacingX = 1f;
            world.FacingZ = 0f;
            world.State.VelocityY = -6f; // falling fast into the wall
            float dt = 1f / fps;
            bool started = false;
            float slideTime = 0f;
            float fastestAfterBrake = 0f;
            while (!world.Grounded && world.Time < 4f)
            {
                MotorStep step = world.Frame(Stick(0.6f, 0f), dt);
                started |= step.WallSlideStarted;
                if (!world.State.WallSliding)
                {
                    continue;
                }

                slideTime += dt;
                if (slideTime > 0.25f && slideTime < world.Tuning.WallSlideGripTime)
                {
                    Assert.LessOrEqual(-world.State.VelocityY, world.Tuning.WallSlideGripSpeed + 1e-3f, "grip");
                }

                if (slideTime > 0.4f)
                {
                    fastestAfterBrake = Math.Max(fastestAfterBrake, -world.State.VelocityY);
                }
            }

            Assert.IsTrue(started, "the slide starts");
            Assert.That(fastestAfterBrake, Is.EqualTo(world.Tuning.WallSlideSpeed).Within(1e-3f),
                "the slide settles exactly at its cap");
            Assert.IsFalse(world.State.WallSliding, "the slide ends on landing");
            Assert.AreEqual(0f, world.Y, 1e-4f);
        }

        [Test]
        public void TheSlideGripReadsAsACatch([Values(30f, 60f)] float fps)
        {
            var world = SlidingOnWall(fps);
            float dt = 1f / fps;
            for (float t = 0f; t < world.Tuning.WallSlideGripTime - 0.05f; t += dt)
            {
                world.Frame(Stick(0f, 0f), dt);
            }

            Assert.IsTrue(world.State.WallSliding, "letting go of the stick keeps the slide (touch thumbs rest)");
            Assert.LessOrEqual(-world.State.VelocityY, world.Tuning.WallSlideGripSpeed + 0.05f,
                "early in the slide Pip grips at about 0.6 m/s");
        }

        [Test]
        public void PointingAwayLetsGoAfterAMomentNotAtOnce([Values(30f, 60f)] float fps)
        {
            var world = SlidingOnWall(fps);
            float dt = 1f / fps;
            world.Frame(Stick(-1f, 0f), dt);
            Assert.IsTrue(world.State.WallSliding, "a brush away does not let go");
            float held = dt;
            while (world.State.WallSliding && held < 1f)
            {
                world.Frame(Stick(-1f, 0f), dt);
                held += dt;
            }

            Assert.IsFalse(world.State.WallSliding);
            Assert.That(held, Is.EqualTo(world.Tuning.WallSlideReleaseTime).Within(dt + 1e-4f));
        }

        // --- Kick ---------------------------------------------------------------------------------------------------

        [Test]
        public void KickVectorThrowsAwayAndUpToTheWallJumpHeight()
        {
            var tuning = new MovementTuningData();
            ClimbRules.KickVelocity(-1f, 0f, tuning, out float vx, out float vy, out float vz);
            Assert.AreEqual(-tuning.WallJumpAwaySpeed, vx, 1e-5f, "straight away from a west face");
            Assert.AreEqual(0f, vz, 1e-5f);
            Assert.AreEqual(Math.Sqrt(2.0 * tuning.Gravity * tuning.WallJumpHeight), vy, 1e-4f);
            Assert.AreEqual(4.5f, tuning.WallJumpAwaySpeed);
            Assert.AreEqual(1.2f, tuning.WallJumpHeight);

            ClimbRules.KickVelocity(0.6f, 0.8f, tuning, out vx, out _, out vz);
            Assert.AreEqual(0.6f * 4.5f, vx, 1e-4f);
            Assert.AreEqual(0.8f * 4.5f, vz, 1e-4f);
            ClimbRules.KickVelocity(3f, 4f, tuning, out vx, out _, out vz);
            Assert.AreEqual(4.5f, Math.Sqrt(vx * vx + vz * vz), 1e-4f, "the normal is normalised");
        }

        [Test]
        public void AKickRisesTheWallJumpHeightAndFliesAway([Values(30f, 60f)] float fps)
        {
            var world = SlidingOnWall(fps);
            float dt = 1f / fps;
            float kickY = world.Y;
            float kickX = world.X;
            MotorStep kick = world.Frame(Stick(0f, 0f, true, true), dt);
            Assert.IsTrue(kick.WallJumped, "Jump during the slide kicks");
            Assert.IsTrue(kick.WallSlideEnded);
            Assert.IsFalse(kick.Jumped, "a kick is not a ground jump (no chain, no boost)");
            float apex = world.Y;
            while (world.State.VelocityY > 0f)
            {
                world.Frame(Stick(0f, 0f, false, true), dt);
                apex = Math.Max(apex, world.Y);
                Assert.IsFalse(world.State.Boosting, "a held kick never gets the high-jump boost");
            }

            Assert.That(apex - kickY, Is.EqualTo(world.Tuning.WallJumpHeight).Within(0.03f), "kick apex");
            Assert.Less(world.X, kickX - 1f, "she flew away from the wall");
            Assert.IsFalse(world.State.WallSliding);
        }

        [Test]
        public void TheControlLockKeepsAStickHeldAtTheWallFromCancellingTheKick([Values(30f, 60f)] float fps)
        {
            var world = SlidingOnWall(fps);
            float dt = 1f / fps;
            world.Frame(Stick(1f, 0f, true, true), dt);
            for (float t = 0f; t < world.Tuning.WallJumpControlLockTime - dt; t += dt)
            {
                world.Frame(Stick(1f, 0f, false, true), dt);
            }

            Assert.Less(world.State.VelocityX, -3f, "still flying away at the end of the lock with the stick at the wall");
        }

        [Test]
        public void AnEarlyPressIsBufferedUntilTheWallIsTouched([Values(30f, 60f)] float fps)
        {
            var world = WestFacingWall(Wall);
            world.Place(-1.2f, 2.5f, 0f);
            world.State.VelocityX = 3f;
            world.FacingX = 1f;
            world.FacingZ = 0f;
            float dt = 1f / fps;
            bool kicked = false;
            bool pressed = false;
            for (int i = 0; i < (int)fps && !kicked; i++)
            {
                // Press once when Pip is 0.1 s (at 3 m/s) from touching the face.
                bool press = !pressed && world.X > -0.28f - 0.3f;
                pressed |= press;
                kicked = world.Frame(Stick(1f, 0f, press, press), dt).WallJumped;
            }

            Assert.IsTrue(pressed);
            Assert.IsTrue(kicked, "a press up to the jump buffer before touching the wall kicks on contact");
        }

        [Test]
        public void CoyoteTimeAfterLeavingTheWall([Values(30f, 60f)] float fps)
        {
            Assert.IsTrue(KickAfterLeaving(fps, 0.15f), "0.15 s after letting go still kicks");
            Assert.IsFalse(KickAfterLeaving(fps, 0.5f), "0.5 s after letting go is too late");
        }

        private static bool KickAfterLeaving(float fps, float delay)
        {
            var world = SlidingOnWall(fps);
            float dt = 1f / fps;
            while (world.State.WallSliding)
            {
                world.Frame(Stick(-1f, 0f), dt);
            }

            for (float t = 0f; t < delay - 1e-4f; t += dt)
            {
                world.Frame(Stick(-1f, 0f), dt);
            }

            return world.Frame(Stick(-1f, 0f, true, true), dt).WallJumped;
        }

        [Test]
        public void JumpingBesideAWallThenPressingAgainKicks([Values(30f, 60f)] float fps)
        {
            var world = WestFacingWall(Wall);
            world.Place(-0.29f, 0f, 0f);
            float dt = 1f / fps;
            Assert.IsTrue(world.Frame(Stick(1f, 0f, true, true), dt).Jumped, "a ground jump beside the wall");
            bool kicked = false;
            for (int i = 0; i < (int)(0.3f * fps) && !kicked; i++)
            {
                kicked = world.Frame(Stick(1f, 0f, i == (int)(0.2f * fps), true), dt).WallJumped;
            }

            Assert.IsTrue(kicked, "touching the wall while rising counts for a kick (generous)");
        }

        [Test]
        public void ChainedKicksClimbBetweenTwoWalls([Values(30f, 60f)] float fps)
        {
            // Two marked walls facing each other 2 m apart (faces at x = -1 and x = +1), 8 m tall.
            var world = new ClimbTestWorld()
                .Add(-2f, 0f, -3f, -1f, 8f, 3f, Wall)
                .Add(1f, 0f, -3f, 2f, 8f, 3f, Wall);
            world.Place(0.7f, 1.2f, 0f);
            world.FacingX = 1f;
            world.FacingZ = 0f;
            float dt = 1f / fps;
            float startY = world.Y;
            int kicks = 0;
            float stickX = 1f;
            for (int i = 0; i < (int)(6f * fps) && kicks < 6; i++)
            {
                bool touching = world.State.WallSliding || world.State.TimeSinceWall <= 0f;
                MotorStep step = world.Frame(Stick(stickX, 0f, touching, true), dt);
                if (step.WallJumped)
                {
                    kicks++;
                    stickX = -stickX; // steer at the other wall
                }
            }

            Assert.AreEqual(6, kicks, "six kicks back and forth");
            TestContext.WriteLine($"chain at {fps} fps: {world.Y - startY:F2} m above the start after {kicks} kicks");
            Assert.Greater(world.Y - startY, 5f, "each kick gains about a metre between two walls");
        }

        [Test]
        public void OneWallAloneCannotBeClimbed([Values(30f, 60f)] float fps)
        {
            var world = SlidingOnWall(fps, 2.5f);
            float dt = 1f / fps;
            float firstKickY = world.Y;
            world.Frame(Stick(0f, 0f, true, true), dt);
            int kicks = 1;
            float highest = world.Y;
            for (int i = 0; i < (int)(3f * fps) && !world.Grounded; i++)
            {
                // Steer straight back at the same wall and mash Jump.
                float before = world.Y;
                MotorStep step = world.Frame(Stick(1f, 0f, i % 3 == 0, true), dt);
                kicks += step.WallJumped ? 1 : 0;
                if (step.WallJumped)
                {
                    Assert.Less(before, firstKickY, "a second kick off the same wall only below the first");
                }

                highest = Math.Max(highest, world.Y);
            }

            Assert.LessOrEqual(highest, firstKickY + world.Tuning.WallJumpHeight + 0.03f,
                "never above the first kick's apex");
        }

        [Test]
        public void TheSameWallRuleClearsOnLanding()
        {
            var tuning = new MovementTuningData();
            var state = Falling();
            state.KickedWall = true;
            state.KickNormalX = -1f;
            state.KickFeetY = 2f;
            var input = AtWall();
            input.FeetY = 2.5f;
            Assert.IsFalse(ClimbRules.WallUsable(state, input, tuning), "the kicked wall, above the kick");
            input.FeetY = 1.9f;
            Assert.IsTrue(ClimbRules.WallUsable(state, input, tuning), "below the kick it is a wall again");
            input.FeetY = 2.5f;
            input.Wall.NormalX = 1f;
            Assert.IsTrue(ClimbRules.WallUsable(state, input, tuning), "the opposite wall is always fine");

            var world = SlidingOnWall(60f, 1.5f);
            world.Frame(Stick(0f, 0f, true, true), 1f / 60f);
            Assert.IsTrue(world.State.KickedWall);
            while (!world.Grounded)
            {
                world.Frame(Stick(0f, 0f), 1f / 60f);
            }

            world.Frame(Stick(0f, 0f), 1f / 60f);
            Assert.IsFalse(world.State.KickedWall, "landing clears the same-wall rule");
        }

        [Test]
        public void NoDiveWhileHeldByAWallOrALedge()
        {
            var tuning = new MovementTuningData();
            var state = Falling();
            state.WallSliding = true;
            Assert.AreEqual(UseAction.None, MovementModel.ResolveUse(state, new MotorInput(), tuning));
            Assert.IsFalse(state.CanDive(tuning));
            state.WallSliding = false;
            state.Hanging = true;
            Assert.AreEqual(UseAction.None, MovementModel.ResolveUse(state, new MotorInput(), tuning));
            state.Hanging = false;
            Assert.AreEqual(UseAction.Dive, MovementModel.ResolveUse(state, new MotorInput(), tuning));
        }

        // --- Ledge geometry -----------------------------------------------------------------------------------------

        [Test]
        public void TheCatchWindowIsGenerousAroundTheHangingHands()
        {
            var tuning = new MovementTuningData();
            Assert.AreEqual(1.27f, tuning.LedgeHangHandHeight, "the LedgeHang clip's hand height");
            Assert.IsTrue(ClimbRules.TopInReach(1.27f, tuning));
            Assert.IsTrue(ClimbRules.TopInReach(0.93f, tuning), "0.34 m short still catches");
            Assert.IsTrue(ClimbRules.TopInReach(1.56f, tuning), "0.29 m past still catches");
            Assert.IsFalse(ClimbRules.TopInReach(0.9f, tuning));
            Assert.IsFalse(ClimbRules.TopInReach(1.6f, tuning));
        }

        private static MotorInput LedgeAhead(float edge = 0.3f, float top = 1.27f) => new MotorInput
        {
            FacingX = 1f,
            Ledge = new LedgeProbe
            {
                Hit = true, Marks = Ledge, NormalX = -1f, NormalZ = 0f, EdgeDistance = edge, TopAbove = top,
                RoomToClimb = true, RoomToHang = true, HangClearance = 1f,
            },
        };

        [Test]
        public void ALedgeIsCaughtOnlyWithRoomAirAndReach()
        {
            var tuning = new MovementTuningData();
            var state = Falling();
            Assert.IsTrue(ClimbRules.CanGrabLedge(state, LedgeAhead(), tuning));
            Assert.IsTrue(ClimbRules.CanGrabLedge(state, LedgeAhead(edge: 0.62f), tuning), "reach 0.35 past the body");
            Assert.IsFalse(ClimbRules.CanGrabLedge(state, LedgeAhead(edge: 0.7f), tuning), "too far");
            Assert.IsFalse(ClimbRules.CanGrabLedge(state, LedgeAhead(top: 0.8f), tuning), "the top is too low");
            Assert.IsFalse(ClimbRules.CanGrabLedge(state, LedgeAhead(top: 1.7f), tuning), "the top is too high");

            var input = LedgeAhead();
            input.Ledge.RoomToClimb = false;
            Assert.IsFalse(ClimbRules.CanGrabLedge(state, input, tuning), "nowhere to stand on top: no catch");
            input = LedgeAhead();
            input.Ledge.RoomToHang = false;
            Assert.IsFalse(ClimbRules.CanGrabLedge(state, input, tuning), "nowhere to hang");
            input = LedgeAhead();
            input.Ledge.HangClearance = 0.1f;
            Assert.IsFalse(ClimbRules.CanGrabLedge(state, input, tuning), "she would stand, not hang");
            input = LedgeAhead();
            input.FacingX = -1f;
            Assert.IsFalse(ClimbRules.CanGrabLedge(state, input, tuning), "back to the ledge, not steering at it");
            input.MoveX = 1f;
            Assert.IsTrue(ClimbRules.CanGrabLedge(state, input, tuning), "steering at it is enough");

            var rising = Falling();
            rising.VelocityY = 3f;
            Assert.IsFalse(ClimbRules.CanGrabLedge(rising, LedgeAhead(), tuning), "rising fast: reach the top first");
            rising.VelocityY = 1f;
            Assert.IsTrue(ClimbRules.CanGrabLedge(rising, LedgeAhead(), tuning), "near the apex catches");
            var cooled = Falling();
            cooled.LedgeRegrabLeft = 0.2f;
            Assert.IsFalse(ClimbRules.CanGrabLedge(cooled, LedgeAhead(), tuning), "right after a drop");
            Assert.IsFalse(ClimbRules.CanGrabLedge(MotorState.Initial, LedgeAhead(), tuning), "never from the ground");
        }

        [Test]
        public void HangSnapAndClimbTargetPlacement()
        {
            var tuning = new MovementTuningData();
            var ledge = LedgeAhead(edge: 0.5f, top: 1.1f).Ledge;
            ClimbRules.HangSnap(ledge, tuning, out float dx, out float dy, out float dz);
            Assert.AreEqual(0.5f - 0.34f, dx, 1e-5f, "toward the face until 0.34 m out");
            Assert.AreEqual(0f, dz, 1e-5f);
            Assert.AreEqual(1.1f - 1.27f, dy, 1e-5f, "hands on the top");

            ClimbRules.ClimbTarget(-1f, 0f, tuning, out dx, out dy, out dz);
            Assert.AreEqual(0.34f + 0.4f, dx, 1e-5f, "over the edge to 0.4 m in from it");
            Assert.AreEqual(1.27f + 0.06f, dy, 1e-5f, "feet just above the top");
            Assert.AreEqual(0f, dz, 1e-5f);

            ClimbRules.ClimbProgress(0f, tuning, out float rise, out float forward);
            Assert.AreEqual(0f, rise);
            Assert.AreEqual(0f, forward);
            ClimbRules.ClimbProgress(tuning.LedgeClimbTime * tuning.LedgeClimbRiseFraction, tuning, out rise, out forward);
            Assert.AreEqual(1f, rise, 1e-5f, "risen before stepping over");
            Assert.AreEqual(0f, forward, 1e-5f);
            ClimbRules.ClimbProgress(tuning.LedgeClimbTime, tuning, out rise, out forward);
            Assert.AreEqual(1f, forward, 1e-5f);
        }

        /// <summary>A marked 2.4 m ledge whose west face is at x = 0, Pip falling past it facing the face.</summary>
        private static ClimbTestWorld HangingFromLedge(float fps)
        {
            var world = new ClimbTestWorld().Add(0f, 0f, -2f, 2f, 2.4f, 2f, Ledge);
            world.Place(-0.3f, 1.6f, 0f);
            world.FacingX = 1f;
            world.FacingZ = 0f;
            float dt = 1f / fps;
            for (int i = 0; i < (int)fps && !world.State.Hanging; i++)
            {
                world.Frame(Stick(0f, 0f), dt);
            }

            Assert.IsTrue(world.State.Hanging, "falling past a marked ledge in reach catches it");
            for (float t = 0f; t < 0.2f; t += dt)
            {
                world.Frame(Stick(0f, 0f), dt);
            }

            return world;
        }

        [Test]
        public void FallingPastAMarkedLedgeHangsAtTheExactSpot([Values(30f, 60f)] float fps)
        {
            var world = HangingFromLedge(fps);
            Assert.AreEqual(2.4f - 1.27f, world.Y, 1e-4f, "hands on the top");
            Assert.AreEqual(-0.34f, world.X, 1e-4f, "0.34 m out from the face");
            Assert.AreEqual(0f, world.State.VelocityY, "no gravity while hanging");
            Assert.AreEqual(1f, world.FacingX, 1e-4f, "facing the wall");
            float y = world.Y;
            for (int i = 0; i < 30; i++)
            {
                world.Frame(Stick(0.3f, 0.2f), 1f / fps);
            }

            Assert.AreEqual(y, world.Y, 1e-5f, "she stays put while hanging");
            Assert.IsTrue(world.State.Hanging);
        }

        [Test]
        public void JumpClimbsOntoTheTop([Values(30f, 60f)] float fps)
        {
            var world = HangingFromLedge(fps);
            float dt = 1f / fps;
            MotorStep climb = world.Frame(Stick(0f, 0f, true, true), dt);
            Assert.IsTrue(climb.ClimbStarted);
            bool ended = false;
            for (int i = 0; i < (int)fps && !ended; i++)
            {
                ended = world.Frame(Stick(0f, 0f, false, true), dt).ClimbEnded;
            }

            Assert.IsTrue(ended, "the climb finishes");
            for (int i = 0; i < 10; i++)
            {
                world.Frame(Stick(0f, 0f), dt);
            }

            Assert.IsTrue(world.Grounded, "standing on the top");
            Assert.AreEqual(2.4f, world.Y, 1e-3f);
            Assert.AreEqual(0.4f, world.X, 0.01f, "0.4 m in from the edge");
        }

        [Test]
        public void DownOrAwayDrops([Values(30f, 60f)] float fps)
        {
            Assert.IsTrue(Drops(fps, new MotorInput { StickY = -1f }), "pulling the stick down drops");
            Assert.IsTrue(Drops(fps, Stick(-1f, 0f)), "pointing away from the wall drops");
            Assert.IsFalse(Drops(fps, Stick(0f, 1f)), "sideways does not");
            Assert.IsFalse(Drops(fps, Stick(1f, 0f)), "toward the wall does not");
        }

        private static bool Drops(float fps, MotorInput input)
        {
            var world = HangingFromLedge(fps);
            float dt = 1f / fps;
            bool dropped = false;
            float held = 0f;
            for (int i = 0; i < (int)(0.5f * fps) && !dropped; i++)
            {
                dropped = world.Frame(input, dt).LedgeDropped;
                held += dt;
            }

            if (!dropped)
            {
                return false;
            }

            Assert.GreaterOrEqual(held, world.Tuning.LedgeDropHoldTime - 1e-4f, "a held pull, not a brush");
            for (float t = 0f; t < world.Tuning.LedgeRegrabDelay - 0.05f && !world.Grounded; t += dt)
            {
                world.Frame(Stick(0f, 0f), dt);
                Assert.IsFalse(world.State.Hanging, "no instant re-catch of the ledge she let go");
            }

            return true;
        }

        [Test]
        public void AHighJumpCatchesTheThreeMetreLampTopAndATapDoesNot([Values(30f, 60f)] float fps)
        {
            // The street lamp's head: top at 3.0 m, 0.97 x 0.7 m, 0.8 m deep (CollisionRules StreetLamp "Head").
            Assert.IsTrue(JumpAtLedge(fps, 3.0f, true), "a held jump catches the 3.0 m top");
            Assert.IsFalse(JumpAtLedge(fps, 3.0f, false), "a tap jump falls short");
        }

        private static bool JumpAtLedge(float fps, float top, bool hold)
        {
            var world = new ClimbTestWorld().Add(0f, top - 0.8f, -0.35f, 0.97f, top, 0.35f, Ledge);
            world.Place(-0.5f, 0f, 0f);
            world.FacingX = 1f;
            world.FacingZ = 0f;
            float dt = 1f / fps;
            world.Frame(Stick(0.3f, 0f, true, hold), dt);
            for (int i = 0; i < (int)(2f * fps) && !world.Grounded; i++)
            {
                if (world.Frame(Stick(0.3f, 0f, false, hold), dt).LedgeGrabbed)
                {
                    return true;
                }
            }

            return false;
        }

        // --- Feedback, animation and tips hooks -------------------------------------------------------------------

        [Test]
        public void KickAndCatchPlayTheirPaperEffects()
        {
            Assert.AreEqual(MoveBursts.WallKickPuff, MoveFeedbackRules.BurstsFor(new MotorStep { WallJumped = true }, false));
            Assert.AreEqual(MoveBursts.LedgeGrabDust, MoveFeedbackRules.BurstsFor(new MotorStep { LedgeGrabbed = true }, false));
            Assert.AreEqual(MoveBursts.None, MoveFeedbackRules.BurstsFor(new MotorStep { WallJumped = true }, true),
                "reduced motion skips the puffs");
            Assert.That(MoveFeedbackRules.PieceCount(MoveBursts.WallKickPuff), Is.InRange(4, 16));
            Assert.That(MoveFeedbackRules.PieceCount(MoveBursts.LedgeGrabDust), Is.InRange(2, 8));
            Assert.Less(MoveFeedbackRules.WallKickStretch, 0f, "the kick stretches");
            Assert.Greater(MoveFeedbackRules.LedgeCatchSquash, 0f, "the catch squashes");
        }

        [Test]
        public void PosesFollowTheSlideTheHangAndTheKick()
        {
            var tracker = new PipPoseTracker();
            var air = new PipPoseInput { Grounded = false, VerticalSpeed = -1f };
            tracker.Step(new PipPoseInput { Grounded = true }, 0.1f);
            tracker.Step(air, 0.2f);
            air.WallSliding = true;
            Assert.AreEqual(PipPose.WallSlide, tracker.Step(air, 0.1f));
            air.WallSliding = false;
            air.Kicked = true;
            air.VerticalSpeed = 7f;
            Assert.AreEqual(PipPose.JumpStart, tracker.Step(air, 1f / 60f), "the kick restarts the take-off");
            Assert.IsTrue(tracker.TookOff);
            air.Kicked = false;
            air.LedgeHanging = true;
            air.VerticalSpeed = 0f;
            Assert.AreEqual(PipPose.LedgeHang, tracker.Step(air, 0.5f));
        }
    }

}
