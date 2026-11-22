using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.World;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// S002 interior wiring, engine-free: the authored route colliders make the mandatory loop walkable without a jump
    /// (spec §4: ramp 1:5 and ≥ 1.6 m wide, no precision), the skilled ascent fits the tap jump, the interior targets,
    /// marks and anchors stand on walkable floors, and the cut-away and mechanism rules hold (MAP-01, CASE-01/02).
    /// </summary>
    public class CaseRouteTests
    {
        private const float Sample = 0.1f;

        /// <summary>Highest walk surface Pip can stand on at (x, z) from a height of <paramref name="fromY"/>.</summary>
        private static float Ground(float x, float z, float fromY) =>
            CaseRouteLayout.GroundAt(x, z, fromY + CaseRouteLayout.StepOffset + 0.01f);

        /// <summary>Why a standing Pip at (x, y, z) would be stopped, or null.</summary>
        private static string Obstacle(float x, float y, float z, bool doorOpen)
        {
            foreach (var slab in CaseRouteLayout.Slabs)
            {
                if (doorOpen && slab.Name == CaseRouteLayout.DoorBlockerName)
                {
                    continue;
                }

                float y0 = y + CaseRouteLayout.StepOffset + 0.02f;
                float y1 = y + CaseRouteLayout.PipHeight;
                if (slab.Kind == SlabKind.Wall && slab.Blocks(x, z, y0, y1, CaseRouteLayout.PipRadius))
                {
                    return slab.Name;
                }

                // Headroom: the underside of a floor above.
                if (slab.Kind == SlabKind.Walk && slab.TryTopAt(x, z, -CaseRouteLayout.PipRadius + 0.05f, out float top) &&
                    top - slab.Depth > y0 && top - slab.Depth < y1)
                {
                    return slab.Name + " (headroom)";
                }
            }

            if (y < 0.5f)
            {
                foreach (var p in ExteriorSolids())
                {
                    if (Math.Abs(p.X - x) < p.HalfX + CaseRouteLayout.PipRadius &&
                        Math.Abs(p.Z - z) < p.HalfZ + CaseRouteLayout.PipRadius)
                    {
                        return p.Model;
                    }
                }
            }

            return null;
        }

        private static IEnumerable<Placement> ExteriorSolids()
        {
            foreach (var p in EnvironmentLayout.Street)
            {
                if (p.Solid)
                {
                    yield return p;
                }
            }

            foreach (var p in EnvironmentLayout.Witnesses)
            {
                if (p.Solid)
                {
                    yield return p;
                }
            }

            foreach (var p in CaseRouteLayout.Models)
            {
                if (p.Solid)
                {
                    yield return p;
                }
            }
        }

        /// <summary>Walks the polyline in 10 cm samples: never a rise or drop beyond the automatic step, never blocked.</summary>
        private static void AssertWalkable(RoutePoint[] route, bool doorOpen)
        {
            float y = Ground(route[0].X, route[0].Z, route[0].Y);
            Assert.AreEqual(route[0].Y, y, 0.1f, "start " + route[0]);
            for (int i = 1; i < route.Length; i++)
            {
                var a = route[i - 1];
                var b = route[i];
                float dx = b.X - a.X, dz = b.Z - a.Z;
                int steps = Math.Max(1, (int)Math.Ceiling(Math.Sqrt(dx * dx + dz * dz) / Sample));
                for (int k = 1; k <= steps; k++)
                {
                    float t = k / (float)steps;
                    float x = a.X + dx * t, z = a.Z + dz * t;
                    float next = Ground(x, z, y);
                    Assert.LessOrEqual(Math.Abs(next - y), CaseRouteLayout.StepOffset,
                        $"{a.Name} → {b.Name}: a {next - y:F2} m step at ({x:F2}, {z:F2})");
                    y = next;
                    string obstacle = Obstacle(x, y, z, doorOpen);
                    Assert.IsNull(obstacle, $"{a.Name} → {b.Name}: blocked by {obstacle} at ({x:F2}, {y:F2}, {z:F2})");
                }

                Assert.AreEqual(b.Y, y, 0.1f, "arrived at " + b);
            }
        }

        [Test]
        public void MandatoryRouteIsWalkableWithoutAJump()
        {
            AssertWalkable(CaseRouteLayout.MandatoryRoute, doorOpen: true);
        }

        [Test]
        public void TheLatchedDoorStopsTheWayOut()
        {
            var doorway = Array.Find(CaseRouteLayout.MandatoryRoute, p => p.Name == "doorway");
            float z = CaseRouteLayout.FrontZ;
            Assert.AreEqual(CaseRouteLayout.DoorBlockerName, Obstacle(doorway.X, CaseRouteLayout.FloorY, z, doorOpen: false));
            Assert.IsNull(Obstacle(doorway.X, CaseRouteLayout.FloorY, z, doorOpen: true));
        }

        [Test]
        public void RampIsOneInFiveAndWideEnough()
        {
            int count = 0;
            foreach (var slab in CaseRouteLayout.RampSlabs())
            {
                count++;
                Assert.LessOrEqual(slab.Gradient, CaseRouteLayout.MaxRampGradient, slab.Name + " steeper than 1:5");
                Assert.GreaterOrEqual(slab.Width, CaseRouteLayout.MinRampWidth, slab.Name + " narrower than 1.6 m");
            }

            Assert.AreEqual(3, count, "leg 1, landing, leg 2");
            Assert.AreEqual(CaseRouteLayout.TreehouseY, CaseRouteLayout.Find("Ramp_Leg2").EndY, 1e-3f, "leg 2 meets the shed roof");
        }

        [Test]
        public void WindowLetsPipThroughWithoutAJump()
        {
            // Between the jambs with room for the capsule, under the arch's shoulders and head.
            Assert.Greater(CaseRouteLayout.WindowHalfWidth, CaseRouteLayout.PipRadius + 0.15f);
            Assert.Greater(CaseRouteLayout.WindowSpring + 0.37f, CaseRouteLayout.PipHeight + 0.1f);
            Assert.AreEqual(1.45f, CaseRouteLayout.WindowSpring + CaseRouteLayout.WindowHalfWidth, 1e-3f, "1.45 m apex");
            var bridge = CaseRouteLayout.Find("Bridge");
            Assert.AreEqual(CaseRouteLayout.SillY, bridge.EndY, 1e-3f, "the bridge arrives at sill height");
        }

        [Test]
        public void SkilledAscentFitsTheTapJump()
        {
            var r = CaseRouteLayout.SkilledAscent;
            // yard → crate → awning: hops; awning: walk; awning → roof: a hop over the wall slice.
            Assert.AreEqual(CaseRouteLayout.CrateSize, r[1].Y, 1e-3f);
            Assert.LessOrEqual(r[1].Y - r[0].Y, CaseRouteLayout.TapJumpApex - 0.2f, "onto the crate");
            Assert.LessOrEqual(r[2].Y - r[1].Y, CaseRouteLayout.TapJumpApex - 0.2f, "crate → awning");
            float gap = (r[2].Z - 0.1f) - (CaseRouteLayout.CrateZ + CaseRouteLayout.CrateSize * 0.5f);
            Assert.That(gap, Is.InRange(0f, 0.7f), "crate to awning edge: a short gap (spec: optional gaps start at 0.7-1.0 m)");
            var slope = CaseRouteLayout.Find("Awning_Slope");
            Assert.Less(Math.Atan(slope.Gradient) * 180.0 / Math.PI, 45.0, "the awning is walkable (slope limit 45)");
            float wall = CaseRouteLayout.AwningWallTopY;
            Assert.LessOrEqual(wall - r[3].Y, CaseRouteLayout.TapJumpApex - 0.3f, "over the awning wall");
            Assert.AreEqual(CaseRouteLayout.TreehouseY, Ground(r[4].X, r[4].Z, wall), 1e-3f, "lands on the shed roof");
            Assert.AreEqual(r[2].Y, Ground(r[2].X, r[2].Z, r[2].Y), 0.1f, "awning low edge");
            Assert.AreEqual(r[3].Y, Ground(r[3].X, r[3].Z, r[3].Y), 0.1f, "awning top");
        }

        [Test]
        public void BothAscentsReachTheSameRoof()
        {
            var mandatory = Array.Find(CaseRouteLayout.MandatoryRoute, p => p.Name == "shed roof");
            var skilled = CaseRouteLayout.SkilledAscent[CaseRouteLayout.SkilledAscent.Length - 1];
            // Walk from where the awning lands to the bridge head: the two routes join before the evidence.
            AssertWalkable(new[]
            {
                new RoutePoint("from awning", skilled.X, skilled.Y, skilled.Z),
                new RoutePoint("roof", mandatory.X, mandatory.Y, mandatory.Z),
            }, doorOpen: false);
            var cloth = CaseStage.Find(CaseStage.PointCloth);
            Assert.AreEqual(CaseRouteLayout.TreehouseY, cloth.Position.Y, 1e-3f, "cloth is on the shared roof");
        }

        [Test]
        public void InteriorTargetsCoverTheInteriorBeats()
        {
            var kinds = new HashSet<CaseActionKind>();
            var speakers = new HashSet<Speaker>();
            foreach (var point in CaseStage.InteriorPoints)
            {
                kinds.Add(point.Action.Kind);
                if (point.Action.Kind == CaseActionKind.Talk)
                {
                    speakers.Add(point.Action.Speaker);
                }

                Assert.IsNotNull(point.VerbKey, point.Id);
                Assert.AreSame(point, CaseStage.Find(point.Id));
            }

            CollectionAssert.IsSupersetOf(kinds, new[]
            {
                CaseActionKind.ExamineCloth, CaseActionKind.ExamineSill, CaseActionKind.LiftLatch,
                CaseActionKind.ExamineGrille, CaseActionKind.LinkArm, CaseActionKind.TurnCrank,
            });
            CollectionAssert.AreEquivalent(new[] { Speaker.June, Speaker.Kit }, speakers);
            Assert.AreEqual(PointSide.Inside, CaseStage.Find(CaseStage.PointLatch).Side);
            Assert.AreEqual(PointSide.Outside, CaseStage.Find(CaseStage.PointFrontDoor).Side);
        }

        [Test]
        public void TargetsAreOfferedOnlyWhenTheWorldHasThem()
        {
            var empty = CaseWorldView.From(CaseState.Empty);
            bool Offered(string id, CaseWorldView v, bool inside) => CaseStage.IsOffered(CaseStage.Find(id), v, inside, false);
            Assert.IsFalse(Offered(CaseStage.PointLatch, empty, false), "never the latch from the porch");
            Assert.IsTrue(Offered(CaseStage.PointLatch, empty, true));
            Assert.IsFalse(Offered(CaseStage.PointFrontDoor, empty, true));
            Assert.IsTrue(Offered(CaseStage.PointJuneAttic, empty, true));
            Assert.IsFalse(Offered(CaseStage.PointJuneFoyer, empty, true));
            Assert.IsFalse(Offered(CaseStage.PointKit, empty, false), "Kit only for the reopening");
            Assert.IsTrue(Offered(CaseStage.PointServicePanel, empty, true));
            Assert.IsFalse(Offered(CaseStage.PointCrank, empty, true), "crank behind the closed panel");
            Assert.IsFalse(CaseStage.IsOffered(CaseStage.Find(CaseStage.PointJuneAttic), empty, true, true), "walking");

            var seen = CaseWorldView.From(CaseState.FromFacts(new[] { CaseFact.SawCam, CaseFact.EnteredByWindow }));
            Assert.IsFalse(Offered(CaseStage.PointServicePanel, seen, true));
            Assert.IsTrue(Offered(CaseStage.PointCrank, seen, true), "crank early is allowed (a tap and a gap)");
            Assert.IsTrue(Offered(CaseStage.PointLinkArm, seen, true));

            var closed = CaseWorldView.From(CaseScenarioTests.PlayCanonical().State);
            Assert.IsTrue(Offered(CaseStage.PointKit, closed, false));
            Assert.IsTrue(Offered(CaseStage.PointJunePorch, closed, false));
            Assert.IsFalse(Offered(CaseStage.PointLatch, closed, true), "the door is open");
            Assert.IsFalse(Offered(CaseStage.PointLinkArm, closed, true), "already linked");
        }

        [Test]
        public void JuneAndTheLatchMarkersStandApart()
        {
            var june = CaseStage.Find(CaseStage.PointJuneFoyer).Position;
            var latch = CaseStage.Find(CaseStage.PointLatch).Position;
            float d = (float)Math.Sqrt((june.X - latch.X) * (june.X - latch.X) + (june.Z - latch.Z) * (june.Z - latch.Z));
            Assert.Greater(d, 1.2f, "spec §5: separate their use markers spatially");
        }

        [Test]
        public void MarksAnchorsAndJunesWalkStandOnFloors()
        {
            foreach (var spot in new[] { CaseStage.JuneAttic, CaseStage.JuneFoyer })
            {
                var p = spot.Position;
                Assert.AreEqual(p.Y, CaseRouteLayout.GroundAt(p.X, p.Z, p.Y + 0.05f), 0.05f, p.ToString());
            }

            foreach (string id in new[]
                     {
                         CaseAnchors.Treehouse, CaseAnchors.AtticSill, CaseAnchors.Attic, CaseAnchors.Foyer,
                         CaseAnchors.Workshop,
                     })
            {
                Assert.IsTrue(CaseStage.TryGetAnchor(id, out var a), id + " staged");
                float ground = CaseRouteLayout.GroundAt(a.X, a.Z, a.Y + 0.05f);
                Assert.AreEqual(a.Y, ground, 0.12f, id + " stands on its floor");
                Assert.IsNull(Obstacle(a.X, ground, a.Z, false), id + " is clear");
                Assert.AreEqual(CaseAnchors.IsInside(id), CaseRouteLayout.Inside(a.X, a.Z), id + " side");
                Assert.AreEqual(id, CaseStage.NearestAnchor(a.X, a.Y, a.Z), id + " is its own nearest anchor");
            }

            var walk = CaseStage.JuneWalk;
            for (int i = 0; i < walk.Length; i++)
            {
                var p = walk[i];
                Assert.AreEqual(p.Y, CaseRouteLayout.GroundAt(p.X, p.Z, p.Y + 0.05f), 0.25f, "June's walk point " + i);
            }

            Assert.Less(CaseStage.PathLength(walk) / CaseStage.JuneWalkSpeed, 12f, "a short authored walk");
            var end = CaseStage.Along(walk, 1000f, out _, out _);
            Assert.AreEqual(CaseStage.JuneFoyer.Position.Z, end.Z, 1e-3f);
        }

        [Test]
        public void AtticAndFoyerSavesNeverMix()
        {
            Assert.AreEqual(CaseAnchors.Attic, CaseStage.NearestAnchor(-0.5f, 2.5f, 14.5f));
            Assert.AreEqual(CaseAnchors.Foyer, CaseStage.NearestAnchor(-0.5f, 0.6f, 12.5f));
            Assert.AreEqual(CaseAnchors.BluePorch, CaseStage.NearestAnchor(0f, 0.6f, 10.4f), "outside the door");
            Assert.AreEqual(CaseAnchors.AtticSill, CaseStage.NearestAnchor(-4.4f, 2.45f, 14f));
            var inside = CaseState.FromFacts(new[] { CaseFact.EnteredByWindow });
            CaseStage.RestorePoint(CaseAnchors.Workshop, inside, out string id);
            Assert.AreEqual(CaseAnchors.Workshop, id, "the workshop is staged now");
        }

        [Test]
        public void EnteringZonesMatchTheOpenings()
        {
            Assert.IsTrue(CaseStage.InAtticEntry(-2.8f, CaseRouteLayout.SillY, CaseRouteLayout.WindowZ));
            Assert.IsFalse(CaseStage.InAtticEntry(-4.0f, CaseRouteLayout.SillY, CaseRouteLayout.WindowZ), "still on the bridge");
            Assert.IsFalse(CaseStage.InAtticEntry(-2.8f, 0.6f, CaseRouteLayout.WindowZ), "foyer below");
            Assert.IsTrue(CaseStage.InFoyer(-0.6f, 0.6f, 12.4f));
            Assert.IsFalse(CaseStage.InFoyer(-0.6f, 2.5f, 14f), "attic");
            Assert.IsFalse(CaseStage.InFoyer(0f, 0.6f, 10.3f), "porch");
        }

        [Test]
        public void CutAwayShowsTheInteriorOnlyNearAWayIn()
        {
            Assert.IsFalse(InteriorVisibility.Decide(0f, 0f, 0f, 1.6f, true, true).ShowInterior, "plaza");
            Assert.IsTrue(InteriorVisibility.Decide(-0.6f, 0.6f, 12.4f, 1.6f, false, true).ShowInterior, "foyer");
            Assert.IsFalse(InteriorVisibility.Decide(0.05f, 0.6f, 10.4f, 1.6f, false, true).ShowInterior, "porch, latched");
            Assert.IsTrue(InteriorVisibility.Decide(0.05f, 0.6f, 10.4f, 1.6f, true, true).ShowInterior, "porch, door open");
            Assert.IsTrue(InteriorVisibility.Decide(-3.4f, 2.5f, 14f, 3.4f, false, true).ShowInterior, "at the window");
            Assert.IsFalse(InteriorVisibility.Decide(-5.6f, 2.4f, 14f, 3.4f, false, true).ShowInterior, "bridge head");
            Assert.IsTrue(InteriorVisibility.Decide(-0.6f, 0.6f, 12.4f, 3.0f, false, true).HideAttic, "high camera");
            Assert.IsFalse(InteriorVisibility.Decide(-0.6f, 2.5f, 14f, 3.6f, false, true).HideAttic, "Pip upstairs");
        }

        [Test]
        public void MechanismCamFollowerAndOwlStayInStep()
        {
            float[] knocks = MechanismMotion.KnockTimes;
            Assert.AreEqual(3, knocks.Length);
            Assert.Less(knocks[1] - knocks[0], knocks[2] - knocks[1], "short, short, pause, last");
            Assert.LessOrEqual(MechanismMotion.CycleSeconds, 4f, "fits the four-second cutaway");
            for (int i = 0; i < knocks.Length; i++)
            {
                float k = knocks[i];
                Assert.AreEqual(MechanismMotion.CamDegreesPerKnock * (i + 1), MechanismMotion.CamAngle(k, knocks), 0.5f,
                    "120 degrees per knock");
                Assert.Greater(MechanismMotion.FollowerLift(k - 0.02f, knocks), 0.9f, "lifted just before knock " + i);
                Assert.Less(MechanismMotion.FollowerLift(k + 0.07f, knocks), 0.05f, "dropped on knock " + i);
                Assert.Greater(MechanismMotion.OwlBow(k + 0.15f, knocks), 0.5f, "the owl bows with knock " + i);
                Assert.AreEqual(i + 1, MechanismMotion.KnocksBy(k, knocks));
            }

            Assert.AreEqual(0f, MechanismMotion.OwlBow(MechanismMotion.CycleSeconds, knocks), 1e-3f, "back upright");
            Assert.AreEqual(360f, MechanismMotion.CamAngle(MechanismMotion.CycleSeconds, knocks), 0.5f, "one turn");
            Assert.AreEqual(1, MechanismMotion.KnocksBy(MechanismMotion.GapCycleSeconds, MechanismMotion.GapKnockTimes),
                "crank before the arm: a single tap");
        }
    }
}
