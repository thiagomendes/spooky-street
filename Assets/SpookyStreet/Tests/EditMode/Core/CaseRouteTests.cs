using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.World;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// S002 interior wiring (house v3), engine-free: the authored colliders make every leg of the mandatory loop
    /// walkable without a jump and each leg ends in its threshold's exit zone (spec §4: ramp 1:5 and ≥ 1.6 m wide, no
    /// precision), the skilled ascent fits the tap jump, the interior stands apart from the street (D24.1), its
    /// targets, marks and anchors stand on walkable floors, the cabin is closed, and the mechanism rules hold
    /// (MAP-01, CASE-01/02).
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
            var legs = CaseRouteLayout.MandatoryLegs;
            Assert.AreEqual(3, legs.Length, "street, interior, porch");
            for (int i = 0; i < legs.Length; i++)
            {
                var leg = legs[i];
                AssertWalkable(leg.Points, doorOpen: true);
                if (leg.Exit == ThresholdId.None)
                {
                    Assert.AreEqual(legs.Length - 1, i, "only the last leg ends nowhere");
                    continue;
                }

                // The leg ends in its threshold's exit zone on its own side; the next leg starts where the pair arrives.
                var first = leg.Points[0];
                var last = leg.Points[leg.Points.Length - 1];
                bool inside = CaseRouteLayout.Inside(first.X, first.Z);
                var exit = Thresholds.End(leg.Exit, inside);
                Assert.IsTrue(exit.Contains(last.X, last.Y, last.Z), $"{leg.Name} ends in {exit}'s exit zone");
                Assert.AreSame(exit, Thresholds.ExitAt(last.X, last.Y, last.Z, doorOpen: true));
                var arrive = Thresholds.Pair(exit);
                var next = legs[i + 1].Points[0];
                Assert.AreEqual(arrive.ArriveX, next.X, 0.05f, legs[i + 1].Name + " starts at the arrival point");
                Assert.AreEqual(arrive.ArriveZ, next.Z, 0.05f, legs[i + 1].Name + " starts at the arrival point");
                Assert.AreEqual(arrive.ArriveY, next.Y, 0.05f, legs[i + 1].Name + " starts at the arrival point");
            }

            // Once the latch is lifted the door works both ways: from the porch back into the hall.
            var back = CaseRouteLayout.PorchToDoor;
            AssertWalkable(back.Points, doorOpen: true);
            var end = back.Points[back.Points.Length - 1];
            Assert.AreSame(Thresholds.DoorOutside, Thresholds.ExitAt(end.X, end.Y, end.Z, doorOpen: true));
            Assert.IsNull(Thresholds.ExitAt(end.X, end.Y, end.Z, doorOpen: false), "the outside door is stuck while latched");
        }

        [Test]
        public void TheLatchedDoorStopsTheWayOut()
        {
            // Inside, the latched leaf keeps Pip out of the doorway (and so out of the door's exit zone).
            float z = CaseRouteLayout.InteriorFrontZ + 0.25f;
            Assert.AreEqual(CaseRouteLayout.DoorBlockerName, Obstacle(0f, CaseRouteLayout.FloorY, z, doorOpen: false));
            Assert.IsNull(Obstacle(0f, CaseRouteLayout.FloorY, z, doorOpen: true));
            float closest = CaseRouteLayout.InteriorFrontZ + 0.14f + CaseRouteLayout.PipRadius;
            Assert.IsFalse(Thresholds.DoorInside.Contains(0f, CaseRouteLayout.FloorY, closest), "the zone is beyond the leaf");
            // Outside, the solid body stops Pip at the door: the porch side of the door is TryFrontDoor until it opens.
            Assert.AreEqual("House_Body", Obstacle(0.05f, CaseRouteLayout.FloorY, CaseRouteLayout.FrontZ - 0.2f, false));
            var door = CaseStage.Find(CaseStage.PointFrontDoor);
            Assert.AreEqual(PointSide.Outside, door.Side);
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
            // Between the jambs with room for the capsule, under the arch's shoulders, on both sides of the wall.
            Assert.Greater(CaseRouteLayout.WindowHalfWidth, CaseRouteLayout.PipRadius + 0.15f);
            Assert.Greater(CaseRouteLayout.WindowSpring + 0.37f, CaseRouteLayout.PipHeight + 0.1f);
            Assert.AreEqual(1.45f, CaseRouteLayout.WindowSpring + CaseRouteLayout.WindowHalfWidth, 1e-3f, "1.45 m apex");
            var bridge = CaseRouteLayout.Find("Bridge");
            Assert.AreEqual(CaseRouteLayout.ExteriorSillY, bridge.EndY, 1e-3f, "the bridge arrives at sill height");
            // Outside: Pip reaches the window plane on the sill (the body stops Pip there, in the exit zone).
            Assert.IsNull(Obstacle(-3.36f, CaseRouteLayout.ExteriorSillY, CaseRouteLayout.ExteriorWindowZ, false));
            Assert.AreEqual("House_Body", Obstacle(-3.25f, CaseRouteLayout.ExteriorSillY, CaseRouteLayout.ExteriorWindowZ, false));
            // Inside: Pip walks into the reveal (the attic floor runs out to the view box's stop) and its exit zone.
            float x = -3.45f;
            Assert.IsNull(Obstacle(x, CaseRouteLayout.LoftY, CaseRouteLayout.InnerWindowZ, false), "into the reveal");
            Assert.AreEqual(CaseRouteLayout.LoftY, Ground(x, CaseRouteLayout.InnerWindowZ, CaseRouteLayout.LoftY), 1e-3f);
            Assert.IsTrue(Thresholds.DormerInside.Contains(x, CaseRouteLayout.LoftY, CaseRouteLayout.InnerWindowZ));
            Assert.IsNotNull(Obstacle(-3.6f, CaseRouteLayout.LoftY, CaseRouteLayout.InnerWindowZ, false),
                "the view box stays out of reach");
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
            var mandatory = Array.Find(CaseRouteLayout.MandatoryLegs[0].Points, p => p.Name == "shed roof");
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
            Assert.AreEqual(PointSide.Inside, CaseStage.Find(CaseStage.PointSill).Side, "the snag is on the inner jamb");
            foreach (var point in CaseStage.InteriorPoints)
            {
                if (point.Side == PointSide.Any)
                {
                    continue;
                }

                var p = point.Position;
                Assert.AreEqual(point.Side == PointSide.Inside, CaseRouteLayout.Inside(p.X, p.Z),
                    point.Id + " stands on its side of the walls");
            }
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
                Assert.IsTrue(CaseRouteLayout.Inside(p.X, p.Z), p + " is in the interior");
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
                Assert.IsFalse(Thresholds.NearAnyZone(a.X, ground, a.Z, ThresholdTracker.Hysteresis),
                    id + " is clear of every exit zone, so a resume never crosses a threshold");
            }

            var walk = CaseStage.JuneWalk;
            for (int i = 0; i < walk.Length; i++)
            {
                var p = walk[i];
                Assert.AreEqual(p.Y, CaseRouteLayout.GroundAt(p.X, p.Z, p.Y + 0.05f), 0.25f, "June's walk point " + i);
                Assert.IsTrue(CaseRouteLayout.Inside(p.X, p.Z), "June walks inside the interior " + i);
            }

            Assert.Less(CaseStage.PathLength(walk) / CaseStage.JuneWalkSpeed, 15f, "a short authored walk");
            var end = CaseStage.Along(walk, 1000f, out _, out _);
            Assert.AreEqual(CaseStage.JuneFoyer.Position.Z, end.Z, 1e-3f);
        }

        [Test]
        public void AtticAndHallSavesNeverMixAndInteriorSavesRestoreInside()
        {
            float ifz = CaseRouteLayout.InteriorFrontZ;
            Assert.AreEqual(CaseAnchors.Attic, CaseStage.NearestAnchor(0.5f, CaseRouteLayout.LoftY, ifz + 2.5f));
            Assert.AreEqual(CaseAnchors.Foyer, CaseStage.NearestAnchor(-0.5f, CaseRouteLayout.FloorY, ifz + 2.5f));
            Assert.AreEqual(CaseAnchors.Workshop, CaseStage.NearestAnchor(1.6f, CaseRouteLayout.FloorY, ifz + 4.8f));
            Assert.AreEqual(CaseAnchors.BluePorch, CaseStage.NearestAnchor(0f, 0.6f, 10.4f), "outside the door");
            Assert.AreEqual(CaseAnchors.AtticSill, CaseStage.NearestAnchor(-4.4f, 2.45f, 14f));
            Assert.AreEqual(CaseAnchors.AtticSill, CaseStage.NearestAnchor(-3.36f, 2.5f, 14f), "on the outer sill");
            var inside = CaseState.FromFacts(new[] { CaseFact.EnteredByWindow });
            foreach (string id in new[] { CaseAnchors.Attic, CaseAnchors.Foyer, CaseAnchors.Workshop })
            {
                var p = CaseStage.RestorePoint(id, inside, out string resolved);
                Assert.AreEqual(id, resolved, id + " is staged");
                Assert.IsTrue(CaseRouteLayout.Inside(p.X, p.Z), id + " restores in the interior");
            }
        }

        [Test]
        public void EnteringZonesMatchTheOpenings()
        {
            float ifz = CaseRouteLayout.InteriorFrontZ;
            var arrive = Thresholds.DormerInside;
            Assert.IsTrue(CaseStage.InAtticEntry(arrive.ArriveX, arrive.ArriveY, arrive.ArriveZ), "the dormer arrival");
            Assert.IsFalse(CaseStage.InAtticEntry(-4.0f, CaseRouteLayout.ExteriorSillY, CaseRouteLayout.ExteriorWindowZ),
                "still on the bridge");
            Assert.IsFalse(CaseStage.InAtticEntry(-2.8f, CaseRouteLayout.FloorY, ifz + 1.5f), "hall below");
            Assert.IsTrue(CaseStage.InFoyer(-0.6f, CaseRouteLayout.FloorY, ifz + 1.6f));
            Assert.IsTrue(CaseStage.InFoyer(Thresholds.DoorInside.ArriveX, CaseRouteLayout.FloorY, Thresholds.DoorInside.ArriveZ),
                "the door arrival is in the hall");
            Assert.IsFalse(CaseStage.InFoyer(-0.6f, CaseRouteLayout.LoftY, ifz + 2.5f), "attic");
            Assert.IsFalse(CaseStage.InFoyer(0f, 0.6f, 10.3f), "porch");
        }

        [Test]
        public void TheInteriorStandsApartFromTheStreet()
        {
            // D24.1: the rooms are a separate space beyond the boundary walls, so the street never sees them.
            Assert.Greater(CaseRouteLayout.InteriorFrontZ - 1.5f, CollisionRules.WallInnerHalfZ + CollisionRules.WallThickness);
            foreach (var p in EnvironmentLayout.Street)
            {
                Assert.IsFalse(CaseRouteLayout.Inside(p.X, p.Z), p.Model + " is on the street");
            }

            foreach (var p in CaseRouteLayout.Models)
            {
                bool interior = p.Model == CaseRouteLayout.OwlHouseInterior || p.Model == CaseRouteLayout.WorkshopMechanism;
                Assert.AreEqual(interior, CaseRouteLayout.Inside(p.X, p.Z), p.Model);
            }

            for (float x = -22f; x <= 22f; x += 1f)
            {
                for (float z = -18f; z <= 18f; z += 1f)
                {
                    Assert.IsFalse(CaseRouteLayout.Inside(x, z), $"({x}, {z}) inside the block is not the interior");
                }
            }

            foreach (var slab in CaseRouteLayout.Slabs)
            {
                bool inside = CaseRouteLayout.Inside((slab.StartX + slab.EndX) * 0.5f, (slab.StartZ + slab.EndZ) * 0.5f);
                Assert.AreEqual(slab.Group == SlabGroup.Interior, inside, slab.Name + " stands with its group");
            }
        }

        [Test]
        public void TheInteriorCollidersFollowTheHouseV3Anchors()
        {
            foreach (string name in new[]
                     {
                         "Hall_BenchAndRack", "Hall_Console", "Hall_BlanketChest", "Hall_Clock", "Hall_Crate",
                         "Hall_UmbrellaStand", "Attic_CraftTable", "Attic_TrunkEast", "Attic_TrunkBack",
                         "Attic_TrunkAndHatBoxes", "Attic_Crates", "Attic_Pumpkin", "Attic_JuneStool", "Attic_Bookshelf",
                         "Stair_Lower", "Stair_Landing", "Stair_Upper", "Loft_RailWest", "Loft_RailSouth", "Loft_RailNorth",
                         "Workshop_Partition", "Stair_Wall", "Stair_EndWall",
                     })
            {
                Assert.AreEqual(SlabGroup.Interior, CaseRouteLayout.Find(name).Group, name);
            }

            // Both flights rise 0.1889 m per 0.28 m tread (34°), from the hall to the landing to the attic.
            var lower = CaseRouteLayout.Find("Stair_Lower");
            var upper = CaseRouteLayout.Find("Stair_Upper");
            Assert.AreEqual(0.1889f / 0.28f, lower.Gradient, 0.01f);
            Assert.AreEqual(0.1889f / 0.28f, upper.Gradient, 0.01f);
            Assert.AreEqual(CaseRouteLayout.LandingY, lower.EndY, 1e-3f);
            Assert.AreEqual(CaseRouteLayout.LoftY, upper.EndY, 0.02f);
            // The hall is 3.26 m clear; the attic is 4.2 m under the ridge with 1.2 m knee walls.
            Assert.AreEqual(3.26f, CaseRouteLayout.LoftUnderside - CaseRouteLayout.FloorY, 1e-3f);
            Assert.AreEqual(1.2f, CaseRouteLayout.KneeY - CaseRouteLayout.LoftY, 1e-3f);
        }

        [Test]
        public void TheCabinIsClosedWithAWalkableBay()
        {
            Assert.Throws<ArgumentException>(() => CaseRouteLayout.Find("Cabin_Parapet"), "the old parapet is gone");
            foreach (string name in new[] { "Cabin_North", "Cabin_West", "Cabin_EastSouth", "Cabin_BayOuter", "Cabin_RoofNorth" })
            {
                Assert.AreEqual(SlabGroup.Route, CaseRouteLayout.Find(name).Group, name);
            }

            // Pip walks in through the door (east wall, cabin y −1.02…−0.12) and out onto the bay boards.
            float deck = CaseRouteLayout.TreehouseY;
            var path = new[] { (-1.7f, -0.57f), (-0.75f, -0.57f), (-0.75f, 0.6f), (0f, 1.4f), (0.6f, 1.4f) };
            for (int i = 1; i < path.Length; i++)
            {
                for (float t = 0f; t <= 1f; t += 0.05f)
                {
                    float cx = path[i - 1].Item1 + (path[i].Item1 - path[i - 1].Item1) * t;
                    float cy = path[i - 1].Item2 + (path[i].Item2 - path[i - 1].Item2) * t;
                    CaseRouteLayout.CabinToWorld(CaseRouteLayout.CabinPlatX + cx, CaseRouteLayout.CabinPlatY + cy,
                        out float x, out float z);
                    Assert.IsNull(Obstacle(x, deck, z, false), $"cabin ({cx:F2}, {cy:F2})");
                }
            }

            CaseRouteLayout.CabinToWorld(CaseRouteLayout.CabinPlatX, CaseRouteLayout.CabinPlatY + 1.6f, out float bx,
                out float bz);
            Assert.AreEqual(deck, CaseRouteLayout.GroundAt(bx, bz, deck + 0.1f), 1e-3f, "the bay's boards are floor");
            CaseRouteLayout.CabinToWorld(CaseRouteLayout.CabinPlatX, CaseRouteLayout.CabinPlatY + 1.9f, out float ox,
                out float oz);
            Assert.IsNotNull(Obstacle(ox, deck, oz, false), "the bay's outer wall closes it");
            // A jump inside bumps the roof; nothing opens to the sky.
            CaseRouteLayout.CabinToWorld(CaseRouteLayout.CabinPlatX + 0.6f, CaseRouteLayout.CabinPlatY - 0.9f, out float jx,
                out float jz);
            Assert.IsNotNull(Obstacle(jx, deck + CaseRouteLayout.TapJumpApex, jz, false), "the roof over the low side");
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
