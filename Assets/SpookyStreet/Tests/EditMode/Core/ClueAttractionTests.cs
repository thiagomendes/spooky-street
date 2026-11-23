using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Audio;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.World;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// S002.5b diegetic clue attractions (D26 §2.3, docs/mystery-first.md §3): every physical clue (and the honest
    /// decoy) calls for curiosity with its own small motion while Pip is near and it is not found; never after.
    /// </summary>
    public class ClueAttractionTests
    {
        /// <summary>The stickers found by looking at a thing in the world (people and the bench view are not things).</summary>
        private static readonly string[] PhysicalClues =
        {
            CaseNotebook.Cloth, CaseNotebook.SillSnag, CaseNotebook.FloorHum, CaseNotebook.WoodenPiece,
            CaseNotebook.OwlPanel, CaseNotebook.LooseArm, CaseNotebook.OwlBooth, CaseNotebook.Streamer,
        };

        private static CaseState Visible(ClueAttraction a) =>
            a.Needs.HasValue ? CaseState.FromFacts(new[] { a.Needs.Value }) : CaseState.Empty;

        [Test]
        public void EveryPhysicalClueHasExactlyOneAttraction()
        {
            foreach (string sticker in PhysicalClues)
            {
                var a = ClueAttractions.ForSticker(sticker);
                Assert.IsNotNull(a, sticker + " calls for curiosity");
                Assert.AreEqual(CaseNotebook.Sticker(sticker).Fact, a.FoundWhen, sticker + " stops once its sticker is found");
                Assert.Greater(a.Kinds.Count, 0);
                int count = 0;
                foreach (var other in ClueAttractions.All)
                {
                    if (other.StickerId == sticker)
                    {
                        count++;
                    }
                }

                Assert.AreEqual(1, count, sticker);
            }

            var ids = new HashSet<string>();
            foreach (var a in ClueAttractions.All)
            {
                Assert.IsTrue(ids.Add(a.Id), "duplicate " + a.Id);
            }

            Assert.IsNotNull(ClueAttractions.Get(ClueAttractions.WindWheel), "the wind wheel whirs before a knock cycle");
            Assert.AreEqual(AttractionTrigger.WindCycle, ClueAttractions.Get(ClueAttractions.WindWheel).Trigger);
        }

        [Test]
        public void EachUnfoundClueInRangePlaysAnAttraction()
        {
            foreach (var a in ClueAttractions.All)
            {
                var state = Visible(a);
                // Pip a step from the thing, on its floor.
                float y = a.At.Y - Math.Min(a.MaxRise, 0.5f);
                Assert.IsTrue(ClueAttractions.ShouldPlay(a, state, a.At.X + 1f, y, a.At.Z), a.Id + " plays near its clue");
                CollectionAssert.Contains(ClueAttractions.Active(state, a.At.X + 1f, y, a.At.Z), a, a.Id);
                if (!CaseRouteLayout.Inside(a.At.X, a.At.Z))
                {
                    Assert.IsTrue(ClueAttractions.ShouldPlay(a, state, a.At.X + a.Range * 0.9f, y, a.At.Z),
                        a.Id + " plays from the edge of its range");
                }
            }
        }

        [Test]
        public void NoAttractionPlaysOnceItsClueIsFound()
        {
            foreach (var a in ClueAttractions.All)
            {
                var facts = new List<CaseFact> { a.FoundWhen };
                if (a.Needs.HasValue)
                {
                    facts.Add(a.Needs.Value);
                }

                var found = CaseState.FromFacts(facts);
                float y = a.At.Y - Math.Min(a.MaxRise, 0.5f);
                Assert.IsFalse(ClueAttractions.ShouldPlay(a, found, a.At.X + 1f, y, a.At.Z), a.Id + " stops once found");
                Assert.IsFalse(ClueAttractions.ShouldPlay(a, found, a.At.X, a.At.Y, a.At.Z), a.Id);
            }
        }

        [Test]
        public void FoundForGoodInEveryReachableState()
        {
            // Whatever order the child plays in, an attraction never plays in a state that holds its clue.
            foreach (var state in CaseReducerTests.ReachableStates())
            {
                foreach (var a in ClueAttractions.All)
                {
                    if (state.Has(a.FoundWhen))
                    {
                        Assert.IsFalse(a.Waiting(state), $"{a.Id} in {state}");
                    }
                }
            }
        }

        [Test]
        public void NothingPlaysOutOfRangeOrFromAnotherFloorOrTheOtherSideOfTheWalls()
        {
            foreach (var a in ClueAttractions.All)
            {
                var state = Visible(a);
                Assert.IsFalse(ClueAttractions.ShouldPlay(a, state, a.At.X + a.Range + 0.5f, a.At.Y, a.At.Z), a.Id + " too far");
                Assert.IsFalse(ClueAttractions.ShouldPlay(a, state, a.At.X, a.At.Y + a.MaxRise + 0.5f, a.At.Z),
                    a.Id + " another floor");
            }

            // The workshop's attractions do not play on the street outside, however close in plan.
            var grille = ClueAttractions.Get(ClueAttractions.Grille);
            Assert.IsTrue(CaseRouteLayout.Inside(grille.At.X, grille.At.Z));
            Assert.AreEqual(0, ClueAttractions.Active(CaseState.Empty, 0f, 0f, -0.5f).FindAll(x => x.Id == ClueAttractions.Grille).Count);
            // Nothing in the plaza at the start calls (the attractions wait at their own places).
            var plaza = ClueAttractions.Active(CaseState.Empty, 0f, 0.1f, -0.5f);
            foreach (var a in plaza)
            {
                Assert.AreNotEqual(ClueAttractions.Cloth, a.Id, "the cloth calls from the route, not from the plaza");
            }
        }

        [Test]
        public void TheLooseArmCallsOnlyOnceThePanelIsOpen()
        {
            var arm = ClueAttractions.Get(ClueAttractions.LooseArm);
            Assert.AreEqual(CaseFact.SawCam, arm.Needs, "the grille look and the owl panel both swing the panel open");
            Assert.IsFalse(arm.Waiting(CaseState.Empty), "hidden behind the closed panel");
            Assert.IsTrue(arm.Waiting(CaseState.FromFacts(new[] { CaseFact.SawCam })));
            var panel = ClueAttractions.Get(ClueAttractions.OwlPanel);
            Assert.IsTrue(panel.Waiting(CaseState.Empty));
            Assert.IsFalse(panel.Waiting(CaseState.FromFacts(new[] { CaseFact.SawCam })), "the panel stops rattling once open");
        }

        [Test]
        public void TheClothCallsFromTheRouteUpToTheTreehouse()
        {
            var cloth = ClueAttractions.Get(ClueAttractions.Cloth);
            CollectionAssert.Contains(cloth.Kinds, AttractionKind.Flutter);
            CollectionAssert.Contains(cloth.Kinds, AttractionKind.Glint);
            // From the ramp's landing, the shed roof and the bridge head, before it is examined.
            var landing = new StagePoint(-12.45f, CaseRouteLayout.RampLandingY, 15.85f);
            var roof = new StagePoint(CaseRouteLayout.RoofMinX + 1f, CaseRouteLayout.TreehouseY, CaseRouteLayout.BridgeZ - 0.5f);
            foreach (var p in new[] { landing, roof })
            {
                Assert.IsTrue(ClueAttractions.ShouldPlay(cloth, CaseState.Empty, p.X, p.Y, p.Z), p.ToString());
            }
        }

        [Test]
        public void KnockAttractionsPairWithTheCamTapAndTheWheelWithItsWhir()
        {
            foreach (var a in ClueAttractions.All)
            {
                if (a.Trigger == AttractionTrigger.Knock)
                {
                    Assert.AreEqual(AudioEvents.ClueCamTap, a.Sound, a.Id);
                }
            }

            Assert.AreEqual(AudioEvents.ClueWindWheel, ClueAttractions.Get(ClueAttractions.WindWheel).Sound);
            foreach (var a in ClueAttractions.All)
            {
                if (a.Trigger == AttractionTrigger.Idle)
                {
                    Assert.Greater(a.Period, 1f, a.Id + " loops slowly enough to stay subtle");
                }
            }
        }

        [Test]
        public void ThePulsesAreBriefAndReducedMotionShrinksMovement()
        {
            // A glint flashes twice then rests for most of its loop (subtle, never a steady glow).
            int lit = 0;
            const float period = 4.5f;
            for (float t = 0f; t < period; t += 0.01f)
            {
                float g = ClueAttractions.Glint(t, period);
                Assert.That(g, Is.InRange(0f, 1f));
                if (g > 0.05f)
                {
                    lit++;
                }
            }

            Assert.Less(lit / (period / 0.01f), 0.2f, "dark for most of the loop");
            Assert.Greater(ClueAttractions.Glint(ClueAttractions.GlintSeconds * 0.5f, period), 0.9f, "first flash");
            Assert.Greater(ClueAttractions.Glint(ClueAttractions.GlintGap + ClueAttractions.GlintSeconds * 0.5f, period), 0.9f,
                "second flash");
            Assert.AreEqual(ClueAttractions.Glint(0.1f, period), ClueAttractions.Glint(0.1f + period, period), 1e-4f, "loops");

            Assert.AreEqual(1f, ClueAttractions.KnockRing(0f), 1e-4f);
            Assert.AreEqual(0f, ClueAttractions.KnockRing(ClueAttractions.KnockRingSeconds));
            Assert.AreEqual(0f, ClueAttractions.KnockRing(-0.1f));
            Assert.AreEqual(0f, ClueAttractions.Whir(ClueAttractions.WhirSeconds));
            Assert.Greater(ClueAttractions.Whir(0.4f), 0.8f, "the wheel spins up before the first knock at 0.45 s");
            Assert.AreEqual(0f, ClueAttractions.Gust(3.5f, period), "the cloth rests between gusts");
            Assert.Greater(ClueAttractions.Gust(1f, period), 0.95f);

            Assert.AreEqual(1f, ClueAttractions.MotionScale(false));
            Assert.Less(ClueAttractions.MotionScale(true), 0.5f, "reduced motion keeps the light and shrinks motion");
        }
    }
}
