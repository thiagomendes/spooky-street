using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Localization;
using SpookyStreet.Core.Movement;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Core.Tips;
using SpookyStreet.Core.World;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// S003 traversal marks in Lantern Lane: only the shed's west side, the front-yard gate posts and the street lamp's
    /// arm are marked; they name colliders that exist; the house, the deck, the rails and the route tops stay
    /// unmarked; and nothing the new moves reach skips the case 01 route or comes near its clues and thresholds. Plus
    /// the wall and ledge tips (D25 glyphs).
    /// </summary>
    public class TraversalMarkTests
    {
        private static readonly MovementTuningData Tuning = new MovementTuningData();

        [Test]
        public void OnlyTheChosenSurfacesAreMarked()
        {
            var names = new List<string>();
            foreach (var mark in CollisionRules.TraversalMarks)
            {
                names.Add(mark.ToString());
            }

            CollectionAssert.AreEquivalent(new[]
            {
                "Shed_RoofSouth WallJump", "FenceGate/PostWest WallJump", "FenceGate/PostEast WallJump",
                "StreetLamp/Head Ledge",
            }, names);
        }

        [Test]
        public void EveryMarkNamesAnExistingCollider()
        {
            foreach (var mark in CollisionRules.TraversalMarks)
            {
                Assert.AreNotEqual(SurfaceMarks.None, mark.Marks, mark.ToString());
                Assert.IsFalse(string.IsNullOrEmpty(mark.Note), mark + " explains itself");
                if (mark.Model == null)
                {
                    var slab = CaseRouteLayout.Find(mark.Part);
                    Assert.AreNotEqual(SlabGroup.Reference, slab.Group, mark + " must be a slab the stage builds");
                    continue;
                }

                Assert.IsTrue(CollisionRules.TryGet(mark.Model, out var recipe), mark.Model);
                Assert.IsTrue(recipe.Solid, mark.Model);
                bool found = false;
                foreach (var part in recipe.Parts)
                {
                    found |= part.Name == mark.Part;
                }

                Assert.IsTrue(found, mark + " names a part of the recipe");
            }
        }

        [Test]
        public void UnmarkedCollidersReportNoMarks()
        {
            foreach (string slab in new[]
                     {
                         "House_Body", "Shed_RoofNorth", "Shed_Threshold", "Shed_WestRail", "Bridge", "Window_Sill",
                         "Ramp_Leg1", "Ramp_Leg2", "Ramp_Leg2South", "Cabin_North", "Cabin_BayFloor", "Awning_Wall",
                     })
            {
                Assert.AreEqual(SurfaceMarks.None, CollisionRules.MarksForSlab(slab, out _, out _), slab);
            }

            Assert.AreEqual(SurfaceMarks.None, CollisionRules.MarksForPart(EnvironmentLayout.TreehousePlatform, "Deck", out _, out _));
            Assert.AreEqual(SurfaceMarks.None, CollisionRules.MarksForPart(EnvironmentLayout.TreehousePlatform, "RailFront", out _, out _));
            Assert.AreEqual(SurfaceMarks.None, CollisionRules.MarksForPart(EnvironmentLayout.OwlHouse, "PorchRoof", out _, out _));
            Assert.AreEqual(SurfaceMarks.None, CollisionRules.MarksForPart(EnvironmentLayout.StreetLamp, "Pole", out _, out _));
            Assert.AreEqual(SurfaceMarks.None, CollisionRules.MarksForPart(EnvironmentLayout.FenceSection, "Fence", out _, out _));
            Assert.AreEqual(SurfaceMarks.None, CollisionRules.MarksForPart(null, "Shed_RoofSouth", out _, out _),
                "a slab name is not a part");
            Assert.AreEqual(SurfaceMarks.None, CollisionRules.MarksForSlab("PostWest", out _, out _), "a part name is not a slab");
        }

        [Test]
        public void TheShedMarkIsItsWestFaceOnly()
        {
            Assert.AreEqual(SurfaceMarks.WallJump, CollisionRules.MarksForSlab("Shed_RoofSouth", out float fx, out float fz));
            Assert.AreEqual(-1f, fx, "the west face (toward the treehouse and the yard pocket)");
            Assert.AreEqual(0f, fz);
            var shed = CaseRouteLayout.Find("Shed_RoofSouth");
            Assert.AreEqual(CaseRouteLayout.RoofMinX, shed.StartX - shed.Width * 0.5f, 1e-4f, "the west face is at x -7.95");
            Assert.AreEqual(CaseRouteLayout.TreehouseY, shed.StartY, 1e-4f, "it rises to the 2.4 m roof");
            Assert.AreEqual(CaseRouteLayout.TreehouseY, shed.Depth, 1e-4f, "from the ground");
            Assert.AreEqual(SlabKind.Walk, shed.Kind, "the roof stays the route's walk top; only the side face is marked");
        }

        [Test]
        public void TheLampArmIsACatchableLedgeWithRoomOnTop()
        {
            Assert.IsTrue(CollisionRules.TryGet(EnvironmentLayout.StreetLamp, out var lamp));
            ColliderPart head = default;
            foreach (var part in lamp.Parts)
            {
                if (part.Name == "Head")
                {
                    head = part;
                }
            }

            Assert.AreEqual(3.0f, head.Top, 1e-4f);
            float hangFeet = head.Top - Tuning.LedgeHangHandHeight;
            Assert.Greater(hangFeet, Tuning.LedgeMinHangClearance + 1f, "Pip hangs well clear of the path");
            Assert.Greater(head.BX, 2f * Tuning.BodyRadius, "room to stand along the arm");
            Assert.Greater(head.BZ, 2f * (Tuning.BodyRadius - 0.03f), "and across it");
            // Reach: the catch window opens once the feet are within hand height + reach above of the top (1.43 m),
            // so the held jump (2.1 m) and the third tapped chain jump (1.5 m) catch it on the way down; a tap does not.
            float lowestCatchFeet = head.Top - Tuning.LedgeHangHandHeight - Tuning.LedgeGrabReachAbove;
            Assert.AreEqual(1.43f, lowestCatchFeet, 1e-3f);
            Assert.Less(lowestCatchFeet, Tuning.EffectiveHoldApexHeight, "a high jump catches it");
            Assert.Greater(lowestCatchFeet, Tuning.JumpApexHeight, "a tap does not");
        }

        [Test]
        public void TheGatePostsAreShortKickPostsOnly()
        {
            Assert.IsTrue(CollisionRules.TryGet(EnvironmentLayout.FenceGate, out var gate));
            foreach (var part in gate.Parts)
            {
                SurfaceMarks marks = CollisionRules.MarksForPart(EnvironmentLayout.FenceGate, part.Name, out _, out _);
                Assert.AreEqual(SurfaceMarks.WallJump, marks, part.Name);
                // A slide needs the chest (0.55 m up) against the post and 0.35 m of air: a short slide below 0.73 m.
                float highestSlideFeet = part.Top - 0.55f;
                Assert.Greater(highestSlideFeet, Tuning.WallSlideMinFeetHeight, "a slide fits on the post");
                float kickApex = highestSlideFeet + Tuning.WallJumpHeight;
                Assert.Less(kickApex, Tuning.EffectiveHoldApexHeight, "a kick never reaches higher than a held jump");
            }
        }

        /// <summary>Highest feet height a kick off a marked wall of height <paramref name="top"/> can reach.</summary>
        private static float KickReach(float top) => top - 0.55f + Tuning.WallJumpHeight;

        [Test]
        public void TheShedKickReachesNothingNewAndStaysFarFromTheCase()
        {
            // One marked wall: no chain, so the reach is one kick from the highest slide (chest at the 2.4 m top).
            float reach = KickReach(CaseRouteLayout.TreehouseY);
            Assert.AreEqual(3.05f, reach, 1e-3f);
            var westRail = CaseRouteLayout.Find("Shed_WestRail");
            Assert.Less(reach, westRail.StartY, "below the shed's west rail top (3.4 m): no way onto the roof from the pocket");
            var deckRailTop = CollisionRules.DeckTop + CollisionRules.DeckRailHeight;
            Assert.Less(reach, deckRailTop, "below the deck's rails (3.35 m)");
            // The kick goes west (away from the house, the bridge and June's window).
            CollisionRules.MarksForSlab("Shed_RoofSouth", out float fx, out _);
            Assert.Less(fx, 0f);
            Assert.Greater(CaseRouteLayout.ExteriorWindowX - CaseRouteLayout.RoofMinX, 4f,
                "June's window is more than 4 m east of the marked face, behind the shed");
            Assert.Greater(CaseRouteLayout.ClothX - CaseRouteLayout.RoofMinX, 1.5f,
                "the cloth clue is on the far (east) side of the roof, behind its rail");
        }

        [Test]
        public void TheNewReachKeepsClearOfEveryThresholdAndClue()
        {
            // Every place the case cares about on the street: June's window (the Dormer threshold), its sill, the cloth
            // and the front door. The new moves start at the marked surfaces; their reach envelope is a few metres.
            var places = new (string Name, float X, float Z)[]
            {
                ("dormer threshold", (Thresholds.DormerOutside.MinX + Thresholds.DormerOutside.MaxX) * 0.5f,
                    (Thresholds.DormerOutside.MinZ + Thresholds.DormerOutside.MaxZ) * 0.5f),
                ("cloth clue", CaseRouteLayout.ClothX, CaseRouteLayout.ClothZ),
                ("front door", 0f, CaseRouteLayout.FrontZ),
            };
            var starts = new (string Name, float X, float Z, float Radius)[]
            {
                ("gate posts", -0.1f, EnvironmentLayout.FrontYardZ, 2.5f),
                // From the lamp's top (3.0 m) the cape glides about 7 m before landing.
                ("street lamp", 2.2f, -9.6f, 8f),
            };
            foreach (var start in starts)
            {
                foreach (var place in places)
                {
                    float dx = place.X - start.X;
                    float dz = place.Z - start.Z;
                    float distance = (float)Math.Sqrt(dx * dx + dz * dz);
                    if (start.Name == "gate posts" && place.Name == "front door")
                    {
                        // The gate is on the porch path: the door is reached on foot anyway, and only from the porch.
                        continue;
                    }

                    Assert.Greater(distance, start.Radius + 1f, $"{start.Name} is clear of the {place.Name}");
                }
            }

            // The shed's west face kicks west into the yard pocket; every place lies east of it, behind the solid shed.
            foreach (var place in places)
            {
                Assert.Greater(place.X, CaseRouteLayout.RoofMinX + 1.5f, $"the {place.Name} is behind the shed");
            }

            // The marks never touch the mandatory route's walk tops (only the shed's side face, which no leg walks).
            foreach (var leg in CaseRouteLayout.MandatoryLegs)
            {
                foreach (var point in leg.Points)
                {
                    if (point.Y > 0.5f && Math.Abs(point.X - CaseRouteLayout.RoofMinX) < 0.3f)
                    {
                        Assert.Fail($"route point {point} stands at the marked face");
                    }
                }
            }
        }

        // --- Tips ---------------------------------------------------------------------------------------------------

        [Test]
        public void WallAndLedgeTipsHaveStableIdsGlyphsAndShortLines()
        {
            Assert.AreEqual("move.wall_jump", MoveTipIds.Id(MoveTip.WallJump));
            Assert.AreEqual("move.ledge_climb", MoveTipIds.Id(MoveTip.LedgeClimb));
            Assert.AreEqual(ControlPrompt.Jump, MoveTipIds.Prompt(MoveTip.WallJump));
            Assert.AreEqual(ControlPrompt.Jump, MoveTipIds.Prompt(MoveTip.LedgeClimb));
            Assert.AreEqual(ControlPrompt.HoldJump, MoveTipIds.Prompt(MoveTip.HoldJump));
            Assert.AreEqual(ControlPrompt.Use, MoveTipIds.Prompt(MoveTip.Dive));
            Assert.AreEqual(PadButton.South, ControlGlyphs.For(ControlScheme.Gamepad, MoveTipIds.Prompt(MoveTip.WallJump)).Pad,
                "the gamepad tag lights the Jump face button");
            Assert.AreEqual(ControlGlyphs.KeyJump,
                ControlGlyphs.For(ControlScheme.KeyboardMouse, MoveTipIds.Prompt(MoveTip.LedgeClimb)).BindingPath);
            Assert.IsTrue(ControlGlyphs.For(ControlScheme.Touch, MoveTipIds.Prompt(MoveTip.WallJump)).IsTouch,
                "on touch the Jump disc itself is the glyph");

            var english = StringTable.Parse(LocalizationTests.BundledEnglishJson());
            foreach (string key in new[] { LocalizationKeys.TipWallJump, LocalizationKeys.TipLedgeClimb })
            {
                Assert.IsTrue(english.TryGet(key, out string value), key);
                Assert.That(value.Length, Is.InRange(3, 30), key);
                StringAssert.Contains("Jump", value, "names the Jump control like the other tips");
                CollectionAssert.Contains(LocalizationKeys.All, key);
            }
        }

        [Test]
        public void TheWallTipShowsOnALingeringSlideAndEndsOnAKick()
        {
            var coach = new MoveTipCoach(new TipScheduler(new MemoryTipStore()));
            var world = new ClimbTestWorld().Add(0f, 0f, -3f, 1f, 5f, 3f, SurfaceMarks.WallJump);
            world.Place(-0.3f, 4f, 0f);
            world.FacingX = 1f;
            world.FacingZ = 0f;
            const float dt = 1f / 60f;
            MoveTip shownAt = MoveTip.None;
            float slide = 0f;
            for (int i = 0; i < 120 && shownAt == MoveTip.None; i++)
            {
                MotorStep step = world.Frame(new MotorInput { MoveX = 1f }, dt);
                coach.Observe(step, world.State, world.Tuning);
                slide = world.State.WallSliding ? slide + dt : slide;
                shownAt = coach.Scheduler.Current;
            }

            Assert.AreEqual(MoveTip.WallJump, shownAt);
            Assert.That(slide, Is.EqualTo(MoveTipCoach.WallHintSlideSeconds).Within(dt * 2f), "after a short slide");
            MotorStep kick = world.Frame(new MotorInput { JumpPressed = true, JumpHeld = true }, dt);
            coach.Observe(kick, world.State, world.Tuning);
            Assert.IsTrue(kick.WallJumped);
            Assert.AreEqual(MoveTip.None, coach.Scheduler.Current, "the kick ends the tip");
            Assert.IsTrue(coach.Scheduler.LastEndedByPerforming);
        }

        [Test]
        public void TheClimbTipShowsOnALingeringHangAndAClimbBeforeItMeansNeverShown()
        {
            var store = new MemoryTipStore();
            var coach = new MoveTipCoach(new TipScheduler(store));
            var world = new ClimbTestWorld().Add(0f, 0f, -2f, 2f, 2.4f, 2f, SurfaceMarks.Ledge);
            world.Place(-0.3f, 1.6f, 0f);
            world.FacingX = 1f;
            world.FacingZ = 0f;
            const float dt = 1f / 60f;
            for (int i = 0; i < 120 && coach.Scheduler.Current == MoveTip.None; i++)
            {
                coach.Observe(world.Frame(new MotorInput(), dt), world.State, world.Tuning);
            }

            Assert.AreEqual(MoveTip.LedgeClimb, coach.Scheduler.Current);
            Assert.GreaterOrEqual(world.State.HangTime, MoveTipCoach.LedgeHintHangSeconds);
            MotorStep climb = world.Frame(new MotorInput { JumpPressed = true, JumpHeld = true }, dt);
            coach.Observe(climb, world.State, world.Tuning);
            Assert.IsTrue(climb.ClimbStarted);
            Assert.AreEqual(MoveTip.None, coach.Scheduler.Current);

            // A child who climbs at once never sees the tip.
            var quick = new MoveTipCoach(new TipScheduler(new MemoryTipStore()));
            var again = new ClimbTestWorld().Add(0f, 0f, -2f, 2f, 2.4f, 2f, SurfaceMarks.Ledge);
            again.Place(-0.3f, 1.6f, 0f);
            again.FacingX = 1f;
            again.FacingZ = 0f;
            for (int i = 0; i < 60 && !again.State.Hanging; i++)
            {
                quick.Observe(again.Frame(new MotorInput(), dt), again.State, again.Tuning);
            }

            quick.Observe(again.Frame(new MotorInput { JumpPressed = true }, dt), again.State, again.Tuning);
            Assert.IsTrue(quick.Scheduler.HasShown(MoveTip.LedgeClimb), "learned before the tip: marked, never shown");
            Assert.AreEqual(MoveTip.None, quick.Scheduler.Current);
        }
    }
}
