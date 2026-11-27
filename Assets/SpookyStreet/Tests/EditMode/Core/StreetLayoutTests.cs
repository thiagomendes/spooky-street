using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.World;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// The S000.4 street set (lawn, path stones, sidewalk, boundary hedges, neighbour houses, leaves): tiles on
    /// their grids at one rotation, the mandatory route and the path stones kept clear, the owl house's baked
    /// stepping stones left uncovered, and the neighbours outside the playable block.
    /// </summary>
    public class StreetLayoutTests
    {
        private static IEnumerable<Placement[]> Tables()
        {
            yield return EnvironmentLayout.LawnTiles;
            yield return EnvironmentLayout.PathTiles;
            yield return EnvironmentLayout.SidewalkTiles;
            yield return EnvironmentLayout.BoundaryHedges;
            yield return EnvironmentLayout.NeighbourHouses;
            yield return EnvironmentLayout.LeafScatters;
        }

        [Test]
        public void StreetSetKeepsTheMandatoryRouteClear()
        {
            var offenders = new List<string>();
            foreach (var table in Tables())
            {
                foreach (var p in table)
                {
                    if (EnvironmentLayout.BlocksRoute(p))
                    {
                        offenders.Add($"{p.Model} at ({p.X}, {p.Z})");
                    }
                }
            }

            Assert.IsEmpty(offenders, "street set blocks the route:\n" + string.Join("\n", offenders));
        }

        [Test]
        public void StreetSetAddsNoCollidersAndNoDensityDetails()
        {
            // Ground pieces are at most 3 cm tall (Pip walks on the greybox's flat colliders), the greybox hedge
            // boxes keep blocking, and repeated decoration never counts toward the density rule.
            foreach (var table in Tables())
            {
                foreach (var p in table)
                {
                    Assert.IsFalse(p.Solid, $"{p.Model} at ({p.X}, {p.Z}) should not add a collider");
                    Assert.IsFalse(p.Detail, $"{p.Model} at ({p.X}, {p.Z}) should not count as a detail");
                }
            }
        }

        [Test]
        public void LawnTilesShareOneRotationOnAFourMetreGrid()
        {
            var seen = new HashSet<(int, int)>();
            foreach (var p in EnvironmentLayout.LawnTiles)
            {
                Assert.AreEqual(EnvironmentLayout.Lawn, p.Model);
                Assert.AreEqual(EnvironmentLayout.LawnYaw, p.Yaw, 1e-4f, "every lawn tile must use the same rotation");
                Assert.AreEqual(1f, p.Scale, 1e-4f);
                Assert.AreEqual(0f, p.Y, 1e-4f);
                float i = (p.X - EnvironmentLayout.LawnMinX) / EnvironmentLayout.LawnTile - 0.5f;
                float j = (p.Z - EnvironmentLayout.LawnMinZ) / EnvironmentLayout.LawnTile - 0.5f;
                Assert.AreEqual(Math.Round(i), i, 1e-3f, $"lawn tile off the grid at x = {p.X}");
                Assert.AreEqual(Math.Round(j), j, 1e-3f, $"lawn tile off the grid at z = {p.Z}");
                Assert.IsTrue(seen.Add(((int)Math.Round(i), (int)Math.Round(j))), $"duplicate lawn tile at ({p.X}, {p.Z})");
            }

            int columns = (int)Math.Round((EnvironmentLayout.LawnMaxX - EnvironmentLayout.LawnMinX) / EnvironmentLayout.LawnTile);
            int rows = (int)Math.Round((EnvironmentLayout.LawnMaxZ - EnvironmentLayout.LawnMinZ) / EnvironmentLayout.LawnTile);
            Assert.AreEqual(columns * rows, seen.Count, "the lawn grid has holes");
        }

        [Test]
        public void LawnCoversTheBlockAndTheNeighbours()
        {
            Assert.LessOrEqual(EnvironmentLayout.LawnMinX, -EnvironmentLayout.BlockHalfX);
            Assert.GreaterOrEqual(EnvironmentLayout.LawnMaxX, EnvironmentLayout.BlockHalfX);
            Assert.LessOrEqual(EnvironmentLayout.LawnMinZ, -EnvironmentLayout.BlockHalfZ);
            Assert.GreaterOrEqual(EnvironmentLayout.LawnMaxZ, EnvironmentLayout.BlockHalfZ);
            foreach (var p in EnvironmentLayout.NeighbourHouses)
            {
                Assert.That(p.X, Is.InRange(EnvironmentLayout.LawnMinX, EnvironmentLayout.LawnMaxX), p.Model);
                Assert.That(p.Z, Is.InRange(EnvironmentLayout.LawnMinZ, EnvironmentLayout.LawnMaxZ), p.Model);
            }
        }

        [Test]
        public void PathStripsInterlockAlongTheRouteAndSkipTheSidewalk()
        {
            var path = EnvironmentLayout.PathTiles;
            Assert.IsNotEmpty(path);
            float previous = float.NegativeInfinity;
            foreach (var p in path)
            {
                Assert.AreEqual(EnvironmentLayout.PathStones, p.Model);
                Assert.AreEqual(EnvironmentLayout.PathYaw, p.Yaw, 1e-4f, "path strips must share one rotation");
                Assert.AreEqual(0f, p.X, 1e-4f, "path strips run along the route's centre line");
                Assert.Greater(p.Z, previous);
                float offset = (p.Z - EnvironmentLayout.PathStartZ) / EnvironmentLayout.PathTile - 0.5f;
                float offsetBack = (p.Z - EnvironmentLayout.SidewalkBackZ) / EnvironmentLayout.PathTile - 0.5f;
                bool onGrid = Math.Abs(offset - Math.Round(offset)) < 1e-3 || Math.Abs(offsetBack - Math.Round(offsetBack)) < 1e-3;
                Assert.IsTrue(onGrid, $"path strip at z = {p.Z} is off its 2 m spacing");

                float z0 = p.Z - EnvironmentLayout.PathTile * 0.5f;
                float z1 = p.Z + EnvironmentLayout.PathTile * 0.5f;
                Assert.IsFalse(z1 > EnvironmentLayout.SidewalkFrontZ + 1e-3f && z0 < EnvironmentLayout.SidewalkBackZ - 1e-3f,
                    $"path strip at z = {p.Z} lies under the sidewalk");
                Assert.LessOrEqual(z1, EnvironmentLayout.PathEndZ + 1e-3f,
                    "the owl house's baked stepping stones must stay uncovered");
                previous = p.Z;
            }

            // The stones reach from the arrival steps to the sidewalk without a gap, and continue through the gate.
            Assert.LessOrEqual(path[0].Z - EnvironmentLayout.PathTile * 0.5f, EnvironmentLayout.RouteStartZ);
            Assert.Greater(path[path.Length - 1].Z, EnvironmentLayout.FrontYardZ, "stones continue through the gate");
        }

        [Test]
        public void SidewalkRunsAlongXInFrontOfTheYardLine()
        {
            float previous = float.NegativeInfinity;
            foreach (var p in EnvironmentLayout.SidewalkTiles)
            {
                Assert.AreEqual(EnvironmentLayout.SidewalkYaw, p.Yaw, 1e-4f);
                Assert.AreEqual(EnvironmentLayout.SidewalkZ, p.Z, 1e-4f);
                Assert.AreEqual(EnvironmentLayout.SidewalkLift, p.Y, 1e-4f);
                if (!float.IsNegativeInfinity(previous))
                {
                    Assert.AreEqual(EnvironmentLayout.SidewalkTile, p.X - previous, 1e-4f, "sidewalk modules must butt");
                }

                previous = p.X;
            }

            Assert.Less(EnvironmentLayout.SidewalkBackZ, EnvironmentLayout.FrontYardZ - 0.4f,
                "the sidewalk stays in front of the hedge, gate and fence");
            Assert.Greater(EnvironmentLayout.SidewalkLift + 0.006f, 0.018f, "the sidewalk bed must cover the lawn's swells");
        }

        [Test]
        public void BoundaryHedgesCloseEveryBlockEdge()
        {
            var north = new List<float>();
            var south = new List<float>();
            var west = new List<float>();
            var east = new List<float>();
            foreach (var p in EnvironmentLayout.BoundaryHedges)
            {
                if (Math.Abs(p.Z - EnvironmentLayout.HedgeLineZ) < 1e-3f) north.Add(p.X);
                else if (Math.Abs(p.Z + EnvironmentLayout.HedgeLineZ) < 1e-3f) south.Add(p.X);
                else if (Math.Abs(p.X + EnvironmentLayout.HedgeLineX) < 1e-3f) west.Add(p.Z);
                else if (Math.Abs(p.X - EnvironmentLayout.HedgeLineX) < 1e-3f ||
                         Math.Abs(p.X - EnvironmentLayout.HedgeLineX - EnvironmentLayout.EastGapNudge) < 1e-3f) east.Add(p.Z);
                else Assert.Fail($"stray hedge at ({p.X}, {p.Z})");
            }

            AssertCovered(north, -EnvironmentLayout.HedgeLineX + 0.5f, EnvironmentLayout.HedgeLineX - 0.5f, "north");
            AssertCovered(south, -EnvironmentLayout.HedgeLineX + 0.5f, EnvironmentLayout.HedgeLineX - 0.5f, "south");
            AssertCovered(west, -EnvironmentLayout.HedgeLineZ, EnvironmentLayout.HedgeLineZ, "west");

            // Living street (D34): the east edge is closed except the lane's 6 m gap, which leads into the east lane.
            AssertCovered(east.FindAll(z => z < 0f), -EnvironmentLayout.HedgeLineZ, EnvironmentLayout.EastGapMinZ, "east (south of the gap)");
            AssertCovered(east.FindAll(z => z > 0f), EnvironmentLayout.EastGapMaxZ, EnvironmentLayout.HedgeLineZ, "east (north of the gap)");
            foreach (float z in east)
            {
                Assert.IsTrue(z + EnvironmentLayout.HedgeLength * 0.5f <= EnvironmentLayout.EastGapMinZ + 1e-3f ||
                              z - EnvironmentLayout.HedgeLength * 0.5f >= EnvironmentLayout.EastGapMaxZ - 1e-3f,
                    $"an east hedge at z = {z} closes the lane's gap");
            }

            Assert.AreEqual(6f, EnvironmentLayout.EastGapMaxZ - EnvironmentLayout.EastGapMinZ, 1e-4f, "a 6 m gap (§2.2)");
        }

        private static void AssertCovered(List<float> centres, float from, float to, string edge)
        {
            centres.Sort();
            float reach = from;
            foreach (float c in centres)
            {
                Assert.LessOrEqual(c - EnvironmentLayout.HedgeLength * 0.5f, reach + 1e-3f, $"gap in the {edge} hedge before {c}");
                reach = Math.Max(reach, c + EnvironmentLayout.HedgeLength * 0.5f);
            }

            Assert.GreaterOrEqual(reach, to - 1e-3f, $"the {edge} hedge stops short");
        }

        [Test]
        public void NeighbourHousesStandOutsideThePlayableBlock()
        {
            Assert.That(EnvironmentLayout.NeighbourHouses.Length, Is.InRange(3, 5));
            var models = new HashSet<string>();
            foreach (var p in EnvironmentLayout.NeighbourHouses)
            {
                models.Add(p.Model);
                Assert.IsTrue(EnvironmentLayout.OutsideBlock(p),
                    $"{p.Model} at ({p.X}, {p.Z}) reaches into the 44 × 36 m block (half {p.HalfX} × {p.HalfZ})");
                float distance = (float)Math.Sqrt(p.X * p.X + p.Z * p.Z);
                Assert.That(distance, Is.InRange(15f, 32f), $"{p.Model} should stand at skyline distance");

                // Plain back walls never face the street: the front (-Z at yaw 0) points toward the plaza.
                double yaw = p.Yaw * Math.PI / 180.0;
                double fx = -Math.Sin(yaw);
                double fz = -Math.Cos(yaw);
                Assert.Less(fx * p.X + fz * p.Z, 0.0, $"{p.Model} at ({p.X}, {p.Z}) turns its back to the street");
            }

            Assert.AreEqual(3, models.Count, "reuse all three neighbour designs");
            var houses = EnvironmentLayout.NeighbourHouses;
            for (int i = 0; i < houses.Length; i++)
            {
                for (int k = i + 1; k < houses.Length; k++)
                {
                    Assert.IsFalse(EnvironmentLayout.Overlaps(houses[i], houses[k]),
                        $"{houses[i].Model} at ({houses[i].X}, {houses[i].Z}) overlaps {houses[k].Model} at ({houses[k].X}, {houses[k].Z})");
                }
            }
        }

        [Test]
        public void NeighbourHousesKeepTheAtticWindowsSkyClear()
        {
            // From the arrival porch, nothing tall stands straight behind the owl house gable (|x| < 5 m).
            foreach (var p in EnvironmentLayout.NeighbourHouses)
            {
                Assert.Greater(Math.Abs(p.X) - p.HalfX, 4.5f, $"{p.Model} crowds the attic window's negative space");
            }
        }

        [Test]
        public void LeavesNeverLieOnThePathStones()
        {
            foreach (var leaf in EnvironmentLayout.LeafScatters)
            {
                Assert.IsFalse(EnvironmentLayout.OnPath(leaf) && leaf.Z < EnvironmentLayout.FrontYardZ,
                    $"{leaf.Model} at ({leaf.X}, {leaf.Z}) lies on the path");
                foreach (var strip in EnvironmentLayout.PathTiles)
                {
                    Assert.IsFalse(EnvironmentLayout.Overlaps(leaf, strip),
                        $"{leaf.Model} at ({leaf.X}, {leaf.Z}) overlaps the path stones at z = {strip.Z}");
                }

                bool onSidewalk = leaf.Z + leaf.HalfZ > EnvironmentLayout.SidewalkFrontZ &&
                                  leaf.Z - leaf.HalfZ < EnvironmentLayout.SidewalkBackZ;
                Assert.AreEqual(onSidewalk ? EnvironmentLayout.LeafOnSidewalkY : 0f, leaf.Y, 1e-4f,
                    $"{leaf.Model} at ({leaf.X}, {leaf.Z}) should rest on {(onSidewalk ? "the sidewalk" : "the lawn")}");
            }

            Assert.GreaterOrEqual(EnvironmentLayout.LeafScatters.Length, 5, "a handful of leaf drifts along the walk");
        }

        [Test]
        public void FootprintTurnsWithYaw()
        {
            EnvironmentLayout.Footprint(6f, 0.8f, 0f, out float hx, out float hz);
            Assert.AreEqual(3f, hx, 1e-4f);
            Assert.AreEqual(0.4f, hz, 1e-4f);
            EnvironmentLayout.Footprint(6f, 0.8f, 90f, out hx, out hz);
            Assert.AreEqual(0.4f, hx, 1e-4f);
            Assert.AreEqual(3f, hz, 1e-4f);
        }

        [Test]
        public void NeighbourWindowsStayUnderBloom()
        {
            Assert.AreEqual(GlowLevels.NeighbourWindow, GlowLevels.IntensityFor("NeighbourWindow_Glow"));
            Assert.Less(GlowLevels.NeighbourWindow, 1f, "neighbour windows stay under the bloom threshold");
            Assert.Less(GlowLevels.NeighbourWindow, GlowLevels.SideWindow);
            Assert.Less(GlowLevels.NeighbourWindow, GlowLevels.AtticWindow);
            Assert.AreEqual(GlowLevels.JackFace, GlowLevels.IntensityFor("Face_Glow"));
        }

        [Test]
        public void StreetSetModelListNamesEveryPiece()
        {
            var models = new HashSet<string>(EnvironmentLayout.StreetSetModels);
            foreach (var table in Tables())
            {
                foreach (var p in table)
                {
                    Assert.IsTrue(models.Contains(p.Model), p.Model + " missing from StreetSetModels");
                }
            }
        }
    }
}
