using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Crowd;

namespace SpookyStreet.Tests.Core
{
    public class CrowdKitTests
    {
        [Test]
        public void TheSliceHasAtLeastEightWalkersOnTwoBodies()
        {
            Assert.GreaterOrEqual(CrowdKit.Walkers.Length, 8, "living-street §7: at least 8 distinct walkers");
            var models = new HashSet<string>();
            var ids = new HashSet<string>();
            foreach (var w in CrowdKit.Walkers)
            {
                Assert.IsTrue(ids.Add(w.Id), "duplicate walker id " + w.Id);
                models.Add(w.Model);
            }

            CollectionAssert.AreEquivalent(new[] { CrowdKit.ChildModel, CrowdKit.AdultModel }, models);
        }

        [Test]
        public void EveryWalkerHasABodyHandsAndShoesAndAtMostOnePartPerSlot()
        {
            foreach (var w in CrowdKit.Walkers)
            {
                var slots = new HashSet<string>();
                foreach (var part in w.Parts)
                {
                    string slot = CrowdKit.SlotOf(w.Model, part);
                    Assert.IsNotNull(slot, $"{w.Id}: {part} is not a part of {w.Model}");
                    Assert.IsTrue(slots.Add(slot), $"{w.Id}: two parts in slot {slot}");
                }

                foreach (var required in new[] { CrowdKit.SlotBody, CrowdKit.SlotHands, CrowdKit.SlotShoes })
                {
                    Assert.IsTrue(slots.Contains(required), $"{w.Id} has no {required}");
                }

                Assert.IsTrue(slots.Contains(CrowdKit.SlotHead) || slots.Contains(CrowdKit.SlotCostume),
                    $"{w.Id}: no head and nothing over it");
            }
        }

        [Test]
        public void NoWalkersShareTheSameParts()
        {
            var seen = new HashSet<string>();
            foreach (var w in CrowdKit.Walkers)
            {
                var sorted = new List<string>(w.Parts);
                sorted.Sort(System.StringComparer.Ordinal);
                Assert.IsTrue(seen.Add(w.Model + ":" + string.Join(",", sorted)), w.Id + " repeats another walker");
            }
        }

        [Test]
        public void NoPartUsesAReservedLook()
        {
            foreach (var parts in new[] { CrowdKit.ChildParts, CrowdKit.AdultParts })
            {
                foreach (var p in parts)
                {
                    foreach (var word in CrowdKit.ReservedWords)
                    {
                        StringAssert.DoesNotContain(word, p.Name, "living-street §3.4 rule 6: reserved look");
                    }
                }
            }
        }

        [Test]
        public void PartNamesAreUniquePerModelAndLod1NamesDoNotTriggerUnityLodGroups()
        {
            foreach (var parts in new[] { CrowdKit.ChildParts, CrowdKit.AdultParts })
            {
                var names = new HashSet<string>();
                foreach (var p in parts)
                {
                    Assert.IsTrue(names.Add(p.Name), "duplicate part " + p.Name);
                }
            }

            var walker = CrowdKit.Find("GhostKid");
            Assert.IsNotNull(walker);
            string[] lod1 = CrowdKit.MeshNames(walker, 1);
            Assert.AreEqual("Ghost_Sheet_Lo", lod1[3]);
            foreach (var n in lod1)
            {
                StringAssert.DoesNotContain("_LOD", n.ToUpperInvariant(), "Unity builds LOD groups from _LOD<n> names");
            }

            CollectionAssert.AreEqual(walker.Parts, CrowdKit.MeshNames(walker, 0));
        }

        [Test]
        public void ParentAndChildStrollAtTheSamePace()
        {
            Assert.AreEqual(CrowdKit.WalkSpeed(CrowdKit.ChildModel), CrowdKit.WalkSpeed(CrowdKit.AdultModel), 0.02f);
            Assert.That(CrowdKit.WalkSpeed(CrowdKit.ChildModel), Is.InRange(0.9f, 1.4f));
            Assert.Less(CrowdKit.ChildHeight, CrowdKit.AdultHeight);
        }
    }
}
