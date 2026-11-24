using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Keepsakes;
using SpookyStreet.Core.Movement;
using SpookyStreet.Core.World;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// The placement rules of the 30 paper bats (S004 §2.1, stream A1): 10 / 10 / 10 by tier with loop bats on walk and
    /// tap; no bat within 1.5 m of a clue or Use target and none in the workshop nook; none in the side yard and treehouse
    /// region visible from the plaza, front-yard or bench eye points; every bat reachable with its move from the real
    /// tuning; every challenge restarting within 2 s of its anchor; interior bats only in the attic loft and the hall.
    /// </summary>
    public class PaperBatLayoutTests
    {
        private static readonly MovementTuningData Tuning = new MovementTuningData();

        private static IReadOnlyList<PaperBatSpot> Bats => PaperBatLayout.All;

        [Test]
        public void ThirtyBatsWithStablePlaceKeyedIds()
        {
            Assert.AreEqual(KeepsakeRules.TotalBats, Bats.Count);
            for (int i = 0; i < Bats.Count; i++)
            {
                Assert.AreEqual(i + 1, Bats[i].Number);
                Assert.AreEqual("bat.lantern_lane." + (i + 1).ToString("00"), Bats[i].Id);
                Assert.IsTrue(PaperBatLayout.IsKnown(Bats[i].Id));
                StringAssert.DoesNotContain("c01", Bats[i].Id, "place-keyed, never case-keyed");
            }

            Assert.AreEqual(Bats.Count, Bats.Select(b => b.Id).Distinct().Count());
            Assert.IsFalse(PaperBatLayout.IsKnown("bat.lantern_lane.31"));
            Assert.IsFalse(PaperBatLayout.IsKnown("bat.lantern_lane.1"));
            Assert.IsFalse(PaperBatLayout.IsKnown(null));
        }

        [Test]
        public void TenTenTenAndLoopBatsNeedOnlyWalkAndTap()
        {
            Assert.AreEqual(10, Bats.Count(b => b.Tier == BatTier.Loop));
            Assert.AreEqual(10, Bats.Count(b => b.Tier == BatTier.Skilled));
            Assert.AreEqual(10, Bats.Count(b => b.Tier == BatTier.Challenge));
            const BatMoves advanced = BatMoves.LedgeGrab | BatMoves.WallJump | BatMoves.Glide | BatMoves.ChainJump;
            foreach (var bat in Bats)
            {
                switch (bat.Tier)
                {
                    case BatTier.Loop:
                        Assert.AreEqual(BatMoves.Walk, bat.Needs & ~BatMoves.TapJump, bat + ": loop bats walk or tap");
                        Assert.IsFalse(bat.Airborne, bat.ToString());
                        break;
                    case BatTier.Skilled:
                        Assert.AreNotEqual(BatMoves.Walk, bat.Needs, bat + ": a skilled route asks for a jump");
                        Assert.AreEqual(BatMoves.Walk, bat.Needs & (BatMoves.LedgeGrab | BatMoves.WallJump | BatMoves.Glide),
                            bat + ": the marked-surface moves and the glide are the challenges'");
                        break;
                    case BatTier.Challenge:
                        Assert.AreNotEqual(BatMoves.Walk, bat.Needs & advanced, bat + ": a challenge asks for an advanced move");
                        break;
                }
            }

            // The challenge tier covers the S003 moves case 01 has room for: the lamp ledge, the shed kick, glides, chains.
            Assert.IsTrue(Bats.Any(b => (b.Needs & BatMoves.LedgeGrab) != 0));
            Assert.IsTrue(Bats.Any(b => (b.Needs & BatMoves.WallJump) != 0));
            Assert.GreaterOrEqual(Bats.Count(b => (b.Needs & BatMoves.Glide) != 0), 3);
            Assert.IsTrue(Bats.Any(b => b.Tier == BatTier.Challenge && (b.Needs & BatMoves.ChainJump) != 0));
        }

        [Test]
        public void NoBatMarksAClue()
        {
            foreach (var bat in Bats)
            {
                float nearest = PaperBatLayout.NearestClue(bat);
                Assert.GreaterOrEqual(nearest, PaperBatLayout.ClueClearance, bat + $": {nearest:F2} m from a clue or Use target");
                Assert.IsFalse(PaperBatLayout.InWorkshopNook(bat.X, bat.Z), bat + ": no bats in the workshop nook");
            }

            // The clue list covers every Use target, the cloth and the wooden owl.
            Assert.AreEqual(CaseStage.AllPoints.Count + 2, PaperBatLayout.ClueMarks.Count);
        }

        [Test]
        public void NoBatInTheSideYardIsVisibleFromThePlazaFrontYardOrBench()
        {
            int hidden = 0;
            foreach (var bat in Bats)
            {
                if (!PaperBatLayout.InHiddenRegion(bat.X, bat.Z))
                {
                    continue;
                }

                hidden++;
                foreach (var eye in PaperBatLayout.EyePoints)
                {
                    Assert.IsFalse(PaperBatLayout.VisibleFrom(eye, bat.X, bat.Y, bat.Z), bat + " is visible from " + eye);
                }
            }

            Assert.GreaterOrEqual(hidden, 6, "the side yard and treehouse keep their bats, on inner faces");
        }

        [Test]
        public void TheSightModelSeesOpenGroundAndRespectsHedgesAndTheShed()
        {
            var plaza = PaperBatLayout.EyePoints[0];
            Assert.IsTrue(PaperBatLayout.VisibleFrom(plaza, 0f, 1f, -8f), "the open path");
            Assert.IsTrue(PaperBatLayout.VisibleFrom(plaza, 0f, 7.5f, 13f), "above the owl house's roof");
            Assert.IsFalse(PaperBatLayout.VisibleFrom(plaza, -6.5f, 0.3f, 8f), "low behind the side-yard hedge");
            Assert.IsFalse(PaperBatLayout.VisibleFrom(plaza, -6.5f, 1.5f, 17f), "behind the shed");
            Assert.IsFalse(PaperBatLayout.VisibleFrom(plaza, 0f, 1.5f, 15f), "behind the owl house body");
        }

        [Test]
        public void EveryBatIsReachableWithItsMove()
        {
            foreach (var bat in Bats)
            {
                Assert.IsTrue(PaperBatLayout.Reachable(bat, Tuning, out string why), bat + ": " + why);
                if (!float.IsNaN(bat.SitY))
                {
                    Assert.Greater(bat.Y, bat.SitY, bat + ": rests on its surface");
                    Assert.Less(bat.Y - bat.SitY, 0.3f, bat + ": rests on its surface");
                }
            }
        }

        [Test]
        public void BatsOnStageSlabsSitOnTheirTops()
        {
            int checkedSlabs = 0;
            foreach (var bat in Bats)
            {
                if (float.IsNaN(bat.SitY) || bat.SitY < 0.3f || CaseRouteLayout.Inside(bat.X, bat.Z))
                {
                    continue;
                }

                // A bat over an authored walk slab sits on that slab's top (the shed roof, the ramp, the crate is not a slab).
                foreach (var slab in CaseRouteLayout.Slabs)
                {
                    if (slab.Kind == SlabKind.Walk && slab.Group != SlabGroup.Interior && slab.TryTopAt(bat.X, bat.Z, 0f, out float top) &&
                        Math.Abs(top - bat.SitY) < 0.6f)
                    {
                        Assert.AreEqual(top, bat.SitY, 0.05f, bat + " on " + slab.Name);
                        checkedSlabs++;
                    }
                }
            }

            Assert.GreaterOrEqual(checkedSlabs, 2, "the ramp and shed-roof bats are checked against their slabs");
        }

        [Test]
        public void ChallengesRestartWithinTwoSecondsOfTheirAnchor()
        {
            foreach (var bat in Bats)
            {
                Assert.IsTrue(CaseStage.TryGetAnchor(bat.Anchor, out var anchor), bat + ": staged anchor");
                Assert.AreEqual(bat.Inside, CaseAnchors.IsInside(bat.Anchor), bat + ": anchor on the same side of the shell");
                if (bat.Tier != BatTier.Challenge)
                {
                    continue;
                }

                float dx = anchor.X - bat.FromX, dz = anchor.Z - bat.FromZ;
                float distance = (float)Math.Sqrt(dx * dx + dz * dz);
                Assert.LessOrEqual(distance, PaperBatLayout.MaxRestartDistance, bat + $": start {distance:F1} m from its anchor");
                Assert.IsFalse(bat.Inside, bat + ": challenges stay outside");
            }
        }

        [Test]
        public void InteriorBatsOnlyInTheAtticLoftAndTheHall()
        {
            int inside = 0;
            foreach (var bat in Bats.Where(b => b.Inside))
            {
                inside++;
                bool attic = bat.PerchY >= CaseRouteLayout.LoftY - 0.01f;
                bool hall = Math.Abs(bat.PerchY - CaseRouteLayout.FloorY) < 0.01f && bat.X < CaseRouteLayout.NookX;
                Assert.IsTrue(attic || hall, bat + ": attic loft or hall only");
                Assert.AreEqual(BatTier.Loop, bat.Tier, bat + ": inside bats are on the loop");
            }

            Assert.AreEqual(2, inside);
        }

        [Test]
        public void BatsKeepApartAndInsideTheBlock()
        {
            for (int i = 0; i < Bats.Count; i++)
            {
                var a = Bats[i];
                if (!a.Inside)
                {
                    Assert.Less(Math.Abs(a.X), EnvironmentLayout.BlockHalfX, a.ToString());
                    Assert.Less(Math.Abs(a.Z), EnvironmentLayout.BlockHalfZ, a.ToString());
                }

                for (int j = i + 1; j < Bats.Count; j++)
                {
                    var b = Bats[j];
                    float dx = a.X - b.X, dy = a.Y - b.Y, dz = a.Z - b.Z;
                    Assert.GreaterOrEqual((float)Math.Sqrt(dx * dx + dy * dy + dz * dz), PaperBatLayout.MinSpacing, a + " / " + b);
                }
            }
        }

        [Test]
        public void TheDecorativeBatsStayDecoration()
        {
            // The six ink-black decorative bats are not keepsakes and never sit beside a collectible (A1 issue 2, frame
            // 65b: at the lamp head the two could not be told apart), so they keep the bats' own spacing.
            foreach (var decor in EnvironmentLayout.PaperBats)
            {
                foreach (var bat in Bats)
                {
                    float dx = decor.X - bat.X, dy = decor.Y - bat.Y, dz = decor.Z - bat.Z;
                    Assert.GreaterOrEqual((float)Math.Sqrt(dx * dx + dy * dy + dz * dz), PaperBatLayout.MinSpacing,
                        bat + " sits beside a decorative bat at " + decor.X + ", " + decor.Y + ", " + decor.Z);
                }
            }
        }
    }
}
