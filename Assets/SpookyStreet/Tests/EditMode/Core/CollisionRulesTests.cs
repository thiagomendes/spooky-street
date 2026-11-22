using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Movement;
using SpookyStreet.Core.World;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// The collision audit (owner feedback, TestFlight 13.1): every placed model is either solid with a recipe or
    /// declared decoration, the treehouse deck is standable at 2.4 m with the trunk through it, the gate posts keep
    /// the mandatory route open, and the boundary walls and ground ring leave no place without ground.
    /// </summary>
    public class CollisionRulesTests
    {
        private const float PipRadius = 0.28f;
        private const float PipHeight = 1.1f;

        /// <summary>Models that look solid and must block Pip (or carry her on top).</summary>
        private static readonly string[] MustBeSolid =
        {
            EnvironmentLayout.OwlHouse, EnvironmentLayout.TreehousePlatform, EnvironmentLayout.Maple,
            EnvironmentLayout.Bench, EnvironmentLayout.PartyTable, EnvironmentLayout.Mailbox, EnvironmentLayout.StreetLamp,
            EnvironmentLayout.Crate, EnvironmentLayout.Pumpkin, EnvironmentLayout.JackOLantern, EnvironmentLayout.FenceSection,
            EnvironmentLayout.FenceGate, EnvironmentLayout.Hedge, EnvironmentLayout.BoundaryHedgeLong,
            EnvironmentLayout.NeighbourHouseA, EnvironmentLayout.NeighbourHouseB, EnvironmentLayout.NeighbourHouseC,
            EnvironmentLayout.KitFloor, EnvironmentLayout.KitSteps, EnvironmentLayout.KitRail, CollisionRules.Awning,
        };

        private static readonly string[] MustBeDecor =
        {
            EnvironmentLayout.PaperBat, EnvironmentLayout.Lawn, EnvironmentLayout.PathStones, EnvironmentLayout.Sidewalk,
            EnvironmentLayout.LeafScatterA, EnvironmentLayout.LeafScatterB, EnvironmentLayout.LeafScatterC,
        };

        [Test]
        public void EveryPlacedModelIsAudited()
        {
            var missing = new List<string>();
            foreach (var p in CollisionRules.PlacedModels())
            {
                if (!CollisionRules.IsAudited(p))
                {
                    missing.Add($"{p.Model} at ({p.X}, {p.Z})");
                }
            }

            Assert.IsEmpty(missing, "placed models with no collision decision:\n" + string.Join("\n", missing));
        }

        [Test]
        public void SolidLookingModelsAreSolidAndDecorationIsNot()
        {
            foreach (var model in MustBeSolid)
            {
                Assert.IsTrue(CollisionRules.TryGet(model, out var recipe), model + " has no recipe");
                Assert.IsTrue(recipe.Solid, model + " should block Pip");
                Assert.IsTrue(recipe.Parts.Length > 0 || !string.IsNullOrEmpty(recipe.CoveredBy),
                    model + " is solid but has no collider part and no covering greybox collider");
            }

            foreach (var model in MustBeDecor)
            {
                Assert.IsTrue(CollisionRules.TryGet(model, out var recipe), model + " has no recipe");
                Assert.IsFalse(recipe.Solid, model + " is decoration");
                Assert.IsEmpty(recipe.Parts, model + " is decoration and must add no collider");
            }
        }

        [Test]
        public void PartsAreWellFormed()
        {
            foreach (var recipe in CollisionRules.Table)
            {
                foreach (var part in recipe.Parts)
                {
                    string at = recipe.Model + "/" + part.Name;
                    switch (part.Shape)
                    {
                        case ColliderShape.Box:
                            Assert.Greater(part.BX, 0f, at);
                            Assert.Greater(part.BY, 0f, at);
                            Assert.Greater(part.BZ, 0f, at);
                            Assert.Less(Math.Abs(part.Pitch), 45f, at + ": a standable slope stays under the 45 degree slope limit");
                            break;
                        case ColliderShape.Capsule:
                            Assert.Greater(part.Radius, 0f, at);
                            break;
                        case ColliderShape.FittedBox:
                            Assert.IsTrue(float.IsNaN(part.AY) || float.IsNaN(part.BY) || part.BY > part.AY, at);
                            break;
                    }
                }
            }
        }

        [Test]
        public void TreehouseDeckIsStandableWithTheTrunkThroughIt()
        {
            Assert.IsTrue(CollisionRules.TryGet(EnvironmentLayout.TreehousePlatform, out var recipe));
            ColliderPart deck = Find(recipe, "Deck");
            Assert.AreEqual(CollisionRules.DeckTop, deck.Top, 1e-3f, "the deck floor is at 2.4 m");
            CollisionRules.MapleTrunkAt(CollisionRules.DeckTop, out float cx, out float cz);
            Assert.GreaterOrEqual(deck.BX, 2f * CollisionRules.DeckHalf, "the floor covers the 2.6 m deck");
            Assert.GreaterOrEqual(deck.BZ, 2f * CollisionRules.DeckHalf);
            Assert.AreEqual(cx, deck.AX, 0.05f, "the deck is centred on the trunk lean");
            Assert.AreEqual(cz, deck.AZ, 0.05f);
            Assert.Less(deck.BY, 0.3f, "a thin floor: Pip under the deck bumps its underside at about 2.25 m");

            foreach (var rail in new[] { Find(recipe, "RailFront"), Find(recipe, "RailSide") })
            {
                Assert.AreEqual(CollisionRules.DeckTop + CollisionRules.DeckRailHeight, rail.Top, 1e-3f, rail.Name);
            }

            // The trunk goes up through the deck: the maple's high trunk capsule spans the deck height at its centre.
            Assert.IsTrue(CollisionRules.TryGet(EnvironmentLayout.Maple, out var maple));
            ColliderPart high = Find(maple, "TrunkHigh");
            Assert.LessOrEqual(Math.Min(high.AY, high.BY), CollisionRules.DeckTop);
            Assert.GreaterOrEqual(Math.Max(high.AY, high.BY), CollisionRules.MapleForkY);
            Assert.AreEqual(cx, high.AX, 1e-3f);
            int limbs = 0;
            foreach (var part in maple.Parts)
            {
                limbs += part.Name.StartsWith("Limb", StringComparison.Ordinal) ? 1 : 0;
            }

            Assert.AreEqual(4, limbs, "the four limb bases above the deck are solid");
        }

        [Test]
        public void GatePostsKeepTheMandatoryRouteOpen()
        {
            Placement gate = default;
            foreach (var p in EnvironmentLayout.Street)
            {
                if (p.Model == EnvironmentLayout.FenceGate)
                {
                    gate = p;
                }
            }

            Assert.IsTrue(CollisionRules.TryGet(EnvironmentLayout.FenceGate, out var recipe));
            float west = float.MinValue;
            float east = float.MaxValue;
            foreach (var post in recipe.Parts)
            {
                EnvironmentLayout.LocalToWorld(gate.X, gate.Z, gate.Yaw, post.AX, post.AZ, out float x, out _);
                if (x < 0f)
                {
                    west = Math.Max(west, x + post.BX * 0.5f);
                }
                else
                {
                    east = Math.Min(east, x - post.BX * 0.5f);
                }
            }

            Assert.Greater(east - west, 0.9f, "the gate opening must stay wider than 0.9 m");
            Assert.Greater(east, PipRadius + 0.05f, "Pip walking the centre line must clear the east post");
            Assert.Less(west, -(PipRadius + 0.05f), "Pip walking the centre line must clear the west post");
        }

        [Test]
        public void WallsCloseTheBlockJustOutsideTheHedgesAndOutreachEveryJump()
        {
            // Inner faces on the outer edge of the hedge lines (1 m deep greybox boxes): no gap to stand in.
            Assert.AreEqual(EnvironmentLayout.HedgeLineZ + 0.5f, CollisionRules.WallInnerHalfZ, 1e-4f);
            Assert.AreEqual(EnvironmentLayout.HedgeLineX + 0.5f, CollisionRules.WallInnerHalfX, 1e-4f);
            Assert.AreEqual(EnvironmentLayout.BlockHalfX, CollisionRules.WallInnerHalfX, 1e-4f, "walls stand on the ground's edge");
            Assert.AreEqual(EnvironmentLayout.BlockHalfZ, CollisionRules.WallInnerHalfZ, 1e-4f);

            var tuning = new MovementTuningData();
            float bestJump = tuning.EffectiveHoldApexHeight * tuning.ChainJumpThirdHeightScale;
            Assert.AreEqual(2.8f, bestJump, 0.01f, "the 3rd held chain jump");
            float reach = CollisionRules.HighestStandableTop + bestJump + PipHeight;
            foreach (var wall in CollisionRules.BoundaryWalls)
            {
                Assert.GreaterOrEqual(wall.Top, reach + 1f, wall.Name + " must outreach the highest jump");
                Assert.LessOrEqual(wall.AY - wall.BY * 0.5f, -1f, wall.Name + " reaches below the ground");
                Assert.GreaterOrEqual(wall.BX, CollisionRules.WallThickness - 1e-4f);
                Assert.GreaterOrEqual(wall.BZ, CollisionRules.WallThickness - 1e-4f);
            }

            // Every in-block standable top is accounted for (neighbour houses stand outside the walls).
            foreach (var recipe in CollisionRules.Table)
            {
                if (recipe.Model.StartsWith("NeighbourHouse", StringComparison.Ordinal))
                {
                    continue;
                }

                foreach (var part in recipe.Parts)
                {
                    if (!float.IsNaN(part.Top))
                    {
                        Assert.LessOrEqual(part.Top, CollisionRules.HighestStandableTop + 1e-3f, recipe.Model + "/" + part.Name);
                    }
                }
            }

            // Corners: the north and south walls run past both side walls.
            AssertInsideAnyWall(CollisionRules.WallInnerHalfX + 0.5f, CollisionRules.WallInnerHalfZ + 0.5f);
            AssertInsideAnyWall(-CollisionRules.WallInnerHalfX - 0.5f, -CollisionRules.WallInnerHalfZ - 0.5f);
            Assert.AreEqual(14f, CollisionRules.WallTop);
        }

        [Test]
        public void NeighbourHousesStandBeyondTheWalls()
        {
            foreach (var p in EnvironmentLayout.NeighbourHouses)
            {
                bool outsideX = Math.Abs(p.X) - p.HalfX >= CollisionRules.WallInnerHalfX;
                bool outsideZ = Math.Abs(p.Z) - p.HalfZ >= CollisionRules.WallInnerHalfZ;
                Assert.IsTrue(outsideX || outsideZ, $"{p.Model} at ({p.X}, {p.Z}) intrudes into the block");
            }
        }

        [Test]
        public void GroundCoversTheWholeLawnRingAndEverySpotInsideTheWalls()
        {
            for (float x = EnvironmentLayout.LawnMinX + 0.25f; x < EnvironmentLayout.LawnMaxX; x += 0.5f)
            {
                for (float z = EnvironmentLayout.LawnMinZ + 0.25f; z < EnvironmentLayout.LawnMaxZ; z += 0.5f)
                {
                    bool block = Math.Abs(x) < EnvironmentLayout.BlockHalfX && Math.Abs(z) < EnvironmentLayout.BlockHalfZ;
                    int ring = 0;
                    foreach (var strip in CollisionRules.GroundRing)
                    {
                        ring += Contains(strip, x, z) ? 1 : 0;
                        Assert.AreEqual(0f, strip.Top, 1e-4f, strip.Name + " tops out level with the block ground");
                    }

                    Assert.AreEqual(1, ring + (block ? 1 : 0), $"({x}, {z}) should lie on exactly one ground piece");
                }
            }
        }

        private static void AssertInsideAnyWall(float x, float z)
        {
            foreach (var wall in CollisionRules.BoundaryWalls)
            {
                if (Contains(wall, x, z))
                {
                    return;
                }
            }

            Assert.Fail($"corner ({x}, {z}) is open");
        }

        private static bool Contains(ColliderPart box, float x, float z) =>
            Math.Abs(x - box.AX) < box.BX * 0.5f && Math.Abs(z - box.AZ) < box.BZ * 0.5f;

        private static ColliderPart Find(ColliderRecipe recipe, string name)
        {
            foreach (var part in recipe.Parts)
            {
                if (part.Name == name)
                {
                    return part;
                }
            }

            Assert.Fail($"{recipe.Model} has no part {name}");
            return default;
        }
    }
}
