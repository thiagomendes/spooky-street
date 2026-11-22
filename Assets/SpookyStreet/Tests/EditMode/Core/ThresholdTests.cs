using System;
using NUnit.Framework;
using SpookyStreet.Core.World;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// The owl house thresholds (D24.1): each way in pairs an exit zone on the street with one in the interior; every
    /// arrival point stands clear of every exit zone on a floor Pip can stand on; a crossing never fires twice; the
    /// front door only works once the latch is lifted; and the paper page-turn wipe covers the screen for the move,
    /// once, in about half a second (a quick fade with reduced motion).
    /// </summary>
    public class ThresholdTests
    {
        private const float Dt = 1f / 60f;

        [Test]
        public void EachThresholdPairsAStreetEndWithAnInteriorEnd()
        {
            Assert.AreEqual(4, Thresholds.Ends.Length);
            foreach (var end in Thresholds.Ends)
            {
                var pair = Thresholds.Pair(end);
                Assert.AreEqual(end.Id, pair.Id, end.ToString());
                Assert.AreNotEqual(end.Inside, pair.Inside, end.ToString());
                Assert.AreSame(end, Thresholds.Pair(pair), "pairing is symmetric");
                Assert.AreEqual(end.Inside, CaseRouteLayout.Inside(end.CentreX, end.CentreZ), end + " zone on its side");
                Assert.AreEqual(end.Inside, CaseRouteLayout.Inside(end.ArriveX, end.ArriveZ), end + " arrival on its side");
            }

            Assert.AreSame(Thresholds.DormerInside, Thresholds.Pair(Thresholds.DormerOutside));
            Assert.AreSame(Thresholds.DoorInside, Thresholds.Pair(Thresholds.DoorOutside));
            Assert.Throws<ArgumentOutOfRangeException>(() => Thresholds.End(ThresholdId.None, true));
        }

        [Test]
        public void ArrivalPointsStandOutsideEveryExitZone()
        {
            foreach (var end in Thresholds.Ends)
            {
                foreach (var zone in Thresholds.Ends)
                {
                    bool sameLevel = end.ArriveY >= zone.MinY && end.ArriveY <= zone.MaxY;
                    if (sameLevel)
                    {
                        Assert.GreaterOrEqual(Thresholds.PlanDistance(zone, end.ArriveX, end.ArriveZ),
                            Thresholds.ArrivalClearance, $"{end} arrival vs {zone} zone");
                    }
                }

                Assert.IsNull(Thresholds.ExitAt(end.ArriveX, end.ArriveY, end.ArriveZ, doorOpen: true), end.ToString());
                Assert.IsFalse(Thresholds.NearAnyZone(end.ArriveX, end.ArriveY, end.ArriveZ, ThresholdTracker.Hysteresis),
                    end + ": the tracker re-arms right where Pip arrives");
            }
        }

        [Test]
        public void ArrivalPointsAreOnFloorsAndClear()
        {
            foreach (var end in Thresholds.Ends)
            {
                float ground = CaseRouteLayout.GroundAt(end.ArriveX, end.ArriveZ, end.ArriveY + 0.05f);
                Assert.AreEqual(end.ArriveY, ground, 0.03f, end + " arrival stands on a floor");
                Assert.IsNull(CaseRouteLayout.Blocking(end.ArriveX, ground, end.ArriveZ, doorOpen: true), end + " is clear");
            }

            // Pip arrives facing away from the threshold, into the place.
            Assert.AreEqual(90f, Thresholds.DormerInside.ArriveYaw, 1e-3f, "into the attic (+X)");
            Assert.AreEqual(-90f, Thresholds.DormerOutside.ArriveYaw, 1e-3f, "back along the bridge (−X)");
            Assert.AreEqual(0f, Thresholds.DoorInside.ArriveYaw, 1e-3f, "into the hall (+Z)");
            Assert.AreEqual(180f, Thresholds.DoorOutside.ArriveYaw, 1e-3f, "out onto the porch (−Z)");
        }

        [Test]
        public void EveryExitZoneIsReachableOnFoot()
        {
            // A place in each zone where a standing Pip fits (door open) and stands on a floor.
            var reach = new[]
            {
                (Thresholds.DormerOutside, -3.36f, CaseRouteLayout.ExteriorSillY, CaseRouteLayout.ExteriorWindowZ),
                (Thresholds.DormerInside, -3.45f, CaseRouteLayout.LoftY, CaseRouteLayout.InnerWindowZ),
                (Thresholds.DoorOutside, 0.05f, CaseRouteLayout.FloorY, CaseRouteLayout.FrontZ - 0.3f),
                (Thresholds.DoorInside, 0f, CaseRouteLayout.FloorY, CaseRouteLayout.InteriorFrontZ + 0.05f),
            };
            foreach (var (end, x, y, z) in reach)
            {
                Assert.IsTrue(end.Contains(x, y, z), end.ToString());
                Assert.IsNull(CaseRouteLayout.Blocking(x, y, z, doorOpen: true), end + " can be stood in");
                Assert.AreEqual(y, CaseRouteLayout.GroundAt(x, z, y + 0.05f), 0.02f, end + " has a floor");
            }

            // The view boxes behind the openings stay out of reach.
            Assert.IsNotNull(CaseRouteLayout.Blocking(0f, CaseRouteLayout.FloorY, CaseRouteLayout.InteriorFrontZ - 0.2f, true));
            Assert.IsNotNull(CaseRouteLayout.Blocking(-3.6f, CaseRouteLayout.LoftY, CaseRouteLayout.InnerWindowZ, true));
        }

        [Test]
        public void TheFrontDoorWorksBothWaysOnlyOnceOpen()
        {
            float y = CaseRouteLayout.FloorY;
            Assert.IsNull(Thresholds.ExitAt(0f, y, CaseRouteLayout.FrontZ - 0.3f, doorOpen: false), "stuck from the porch");
            Assert.AreSame(Thresholds.DoorOutside, Thresholds.ExitAt(0f, y, CaseRouteLayout.FrontZ - 0.3f, doorOpen: true));
            Assert.AreSame(Thresholds.DoorInside, Thresholds.ExitAt(0f, y, CaseRouteLayout.InteriorFrontZ + 0.1f, true));
            // June's window is open from the start.
            Assert.AreSame(Thresholds.DormerOutside,
                Thresholds.ExitAt(-3.36f, CaseRouteLayout.ExteriorSillY, CaseRouteLayout.ExteriorWindowZ, doorOpen: false));
        }

        [Test]
        public void ACrossingNeverFiresTwice()
        {
            var tracker = new ThresholdTracker();
            var outside = Thresholds.DormerOutside;
            var inside = Thresholds.DormerInside;
            float sillY = CaseRouteLayout.ExteriorSillY;
            float wz = CaseRouteLayout.ExteriorWindowZ;
            tracker.Reset();
            Assert.IsNull(tracker.Step(Dt, -4.5f, sillY, wz, false), "on the bridge");
            Assert.IsTrue(tracker.Armed, "armed away from the zones");

            // Walk up to the window: exactly one crossing, however long Pip stays in the zone.
            int fired = 0;
            for (float x = -4.5f; x <= -3.36f; x += 0.02f)
            {
                if (tracker.Step(Dt, x, sillY, wz, false) != null)
                {
                    fired++;
                }
            }

            for (int i = 0; i < 120; i++)
            {
                if (tracker.Step(Dt, -3.36f, sillY, wz, false) != null)
                {
                    fired++;
                }
            }

            Assert.AreEqual(1, fired, "one crossing");

            // Arrived inside: even standing in the inner zone during the cooldown nothing fires.
            tracker.Arrived();
            for (int i = 0; i < 20; i++)
            {
                Assert.IsNull(tracker.Step(Dt, -3.45f, CaseRouteLayout.LoftY, CaseRouteLayout.InnerWindowZ, false));
            }

            // Back at the arrival point: re-armed after the cooldown, then a deliberate walk back out crosses again.
            for (int i = 0; i < 40; i++)
            {
                Assert.IsNull(tracker.Step(Dt, inside.ArriveX, inside.ArriveY, inside.ArriveZ, false));
            }

            Assert.IsTrue(tracker.Armed);
            Assert.AreSame(inside, tracker.Step(Dt, -3.45f, CaseRouteLayout.LoftY, CaseRouteLayout.InnerWindowZ, false));
            Assert.IsFalse(tracker.Armed);
            // Hysteresis: hovering at the zone's edge never re-arms.
            for (int i = 0; i < 120; i++)
            {
                Assert.IsNull(tracker.Step(Dt, inside.MaxX + 0.1f, CaseRouteLayout.LoftY, CaseRouteLayout.InnerWindowZ, false));
            }

            Assert.IsFalse(tracker.Armed, "within the hysteresis band");
            Assert.AreSame(outside, Thresholds.Pair(inside));
        }

        [Test]
        public void AResetInsideAZoneWaitsForPipToStepOut()
        {
            var tracker = new ThresholdTracker();
            tracker.Reset();
            float y = CaseRouteLayout.FloorY;
            float z = CaseRouteLayout.FrontZ - 0.3f;
            for (int i = 0; i < 60; i++)
            {
                Assert.IsNull(tracker.Step(Dt, 0f, y, z, true), "a resume in the doorway never crosses");
            }

            Assert.IsNull(tracker.Step(Dt, 0f, y, z - 1.2f, true));
            Assert.AreSame(Thresholds.DoorOutside, tracker.Step(Dt, 0f, y, z, true));
            // The latched door never fires, armed or not.
            tracker.Reset();
            tracker.Step(Dt, 0f, y, z - 1.2f, false);
            Assert.IsTrue(tracker.Armed);
            for (int i = 0; i < 60; i++)
            {
                Assert.IsNull(tracker.Step(Dt, 0f, y, z, false));
            }
        }

        [Test]
        public void HintsLeadThroughTheRightThreshold()
        {
            Assert.IsNull(Thresholds.WayTo(false, false, false), "same side: straight to the goal");
            Assert.IsNull(Thresholds.WayTo(true, true, true));
            Assert.AreSame(Thresholds.DormerOutside, Thresholds.WayTo(false, true, false), "in by June's window");
            Assert.AreSame(Thresholds.DoorOutside, Thresholds.WayTo(false, true, true), "in by the open door");
            Assert.AreSame(Thresholds.DormerInside, Thresholds.WayTo(true, false, false), "out by the window while latched");
            Assert.AreSame(Thresholds.DoorInside, Thresholds.WayTo(true, false, true), "out by the open door");
        }

        [Test]
        public void TheWipeCoversTheScreenForTheMoveOnce()
        {
            var wipe = new ThresholdWipe();
            Assert.IsFalse(wipe.Active);
            Assert.IsTrue(wipe.Start(reducedMotion: false));
            Assert.IsFalse(wipe.Start(false), "never twice at once");
            float t = 0f;
            int moves = 0;
            float lastCoverage = 0f;
            bool covering = true;
            while (wipe.Active && t < 2f)
            {
                bool move = wipe.Step(Dt);
                t += Dt;
                if (move)
                {
                    moves++;
                    Assert.AreEqual(1f, wipe.Coverage, 1e-3f, "the screen is fully covered when Pip moves");
                    Assert.AreEqual(WipePhase.Covered, wipe.Phase);
                    covering = false;
                }

                if (covering)
                {
                    Assert.GreaterOrEqual(wipe.Coverage, lastCoverage - 1e-4f, "the page only comes in while covering");
                }
                else if (wipe.Phase == WipePhase.Revealing)
                {
                    Assert.LessOrEqual(wipe.Coverage, lastCoverage + 1e-4f, "and only goes out while revealing");
                }

                lastCoverage = wipe.Coverage;
                Assert.LessOrEqual(Math.Abs(wipe.Tilt), ThresholdWipe.MaxTilt + 1e-3f);
            }

            Assert.AreEqual(1, moves, "Pip moves exactly once");
            Assert.That(t, Is.InRange(0.45f, 0.65f), "about half a second");
            Assert.AreEqual(0f, wipe.Coverage, 1e-3f, "gone at the end");
            Assert.AreEqual(1f, wipe.Turn, 1e-3f);
        }

        [Test]
        public void ReducedMotionFadesQuickly()
        {
            var wipe = new ThresholdWipe();
            wipe.Start(reducedMotion: true);
            float t = 0f;
            int moves = 0;
            while (wipe.Active && t < 2f)
            {
                if (wipe.Step(Dt))
                {
                    moves++;
                    Assert.AreEqual(1f, wipe.Alpha, 1e-3f, "opaque when Pip moves");
                }

                Assert.AreEqual(0f, wipe.Tilt, "no rotation");
                Assert.AreEqual(0f, wipe.PageLeft, "no sweep: the whole page fades");
                t += Dt;
            }

            Assert.AreEqual(1, moves);
            Assert.Less(t, 0.45f, "a quick fade");
        }

        [Test]
        public void ASlowFrameStillHoldsTheCoverForTheCameraToSettle()
        {
            var wipe = new ThresholdWipe();
            wipe.Start(false);
            Assert.IsTrue(wipe.Step(0.5f), "one long frame covers and moves");
            Assert.AreEqual(WipePhase.Covered, wipe.Phase);
            wipe.Step(0.5f);
            Assert.AreEqual(WipePhase.Covered, wipe.Phase, "at least two covered frames, however long");
            wipe.Step(0.5f);
            Assert.AreEqual(WipePhase.Revealing, wipe.Phase);
            wipe.Cancel();
            Assert.IsFalse(wipe.Active);
        }
    }
}
