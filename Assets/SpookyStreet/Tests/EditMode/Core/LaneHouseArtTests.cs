using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Crowd;
using SpookyStreet.Core.LivingStreet;
using SpookyStreet.Core.World;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// The east lane houses' art contract (docs/living-street-houses-art.md): every house fits the door, wall, footprint and
    /// stand the slice already uses for its placeholder, so the swap is only names; the neighbours are crowd kit recipes
    /// on the grown-up body in house clothes, one per slot, with no reserved look; the lamps glow under the attic window.
    /// </summary>
    public class LaneHouseArtTests
    {
        [Test]
        public void HousesAreTheSlicesDoorsAndNamedAfterTheirNeighbours()
        {
            CollectionAssert.AreEqual(new[] { LivingStreetLayout.Fogg, LivingStreetLayout.Pearl, LivingStreetLayout.Abbotts },
                LaneHouseArt.Houses);
            Assert.AreEqual("LaneHouseFogg", LaneHouseArt.House(LaneHouseArt.Fogg));
            Assert.AreEqual("LaneDoorPearl", LaneHouseArt.DoorLeaf(LaneHouseArt.Pearl));
            Assert.AreEqual("LanePorchLampAbbotts", LaneHouseArt.PorchLamp(LaneHouseArt.Abbotts));
            Assert.AreEqual(LaneHouseArt.House(LaneHouseArt.Pearl), LaneHouseArt.Atlas(LaneHouseArt.Pearl), "the house finds its atlas by name");
            Assert.Throws<ArgumentException>(() => LaneHouseArt.House("owl"));
            var names = new HashSet<string>();
            foreach (string id in LaneHouseArt.Houses)
            {
                Assert.IsTrue(names.Add(LaneHouseArt.House(id)));
                Assert.IsTrue(names.Add(LaneHouseArt.DoorLeaf(id)));
                Assert.IsTrue(names.Add(LaneHouseArt.PorchLamp(id)));
                CollectionAssert.DoesNotContain(EnvironmentLayout.StreetSetModels, LaneHouseArt.House(id),
                    "not a street-set model: the heart's skyline does not wait for it");
            }
        }

        [Test]
        public void EachHouseFitsItsDoorWallFootprintAndStand()
        {
            foreach (var door in LivingStreetLayout.Doors)
            {
                LaneHouseArt.Door(door.Id, out float across, out float wall);
                Assert.AreEqual(door.DoorLocalX, across, 1e-4f, door.Id + ": the door's centre");
                Assert.AreEqual(door.WallLocalZ, wall, 1e-4f, door.Id + ": the front wall");
                EnvironmentLayout.Size(door.House, out float w, out float d);
                LaneHouseArt.Footprint(door.Id, out float fw, out float fd);
                Assert.AreEqual(w, fw, 1e-4f, door.Id + ": footprint width (the slice's bounds checks)");
                Assert.AreEqual(d, fd, 1e-4f, door.Id + ": footprint depth");
                float yaw = door.Yaw * (float)Math.PI / 180f;
                float standLocal = (door.StandX - door.X) * (float)Math.Sin(yaw) + (door.StandZ - door.Z) * (float)Math.Cos(yaw);
                Assert.AreEqual(LaneHouseArt.Stand(door.Id), standLocal, 0.01f, door.Id + ": the visitor's stand");
                Assert.Less(LaneHouseArt.Stand(door.Id), -fd / 2f, door.Id + ": the stand is outside the footprint");
            }

            Assert.AreEqual(LivingStreetLayout.DoorWidth, LaneHouseArt.LeafWidth, 1e-4f);
            Assert.AreEqual(LivingStreetLayout.DoorHeight, LaneHouseArt.LeafHeight, 1e-4f);
            Assert.AreEqual(LivingStreetLayout.DoorSill, LaneHouseArt.Sill, 1e-4f);
            Assert.Less(LaneHouseArt.Lod1ScreenHeight, LaneHouseArt.Lod0ScreenHeight);
        }

        [Test]
        public void LampsAndLitRoomsStayUnderTheAtticWindow()
        {
            Assert.AreEqual(GlowLevels.Lamp, GlowLevels.IntensityFor(LaneHouseArt.LampGlow));
            Assert.AreEqual(GlowLevels.RoomCard, GlowLevels.IntensityFor(LaneHouseArt.RoomGlow));
            foreach (var door in LivingStreetLayout.Doors)
            {
                // the slice's lamp: 0.9 at rest, plus Nana Pearl's warm boost while she answers
                Assert.Less(0.9f + door.Gag.LampBoost, GlowLevels.AtticWindow * 0.5f, door.Id + ": well under the attic window");
            }

            Assert.Less(GlowLevels.RoomCard, GlowLevels.AtticWindow * 0.5f);
        }

        [Test]
        public void TheNeighboursAreGrownUpsInHouseClothesOnePartPerSlot()
        {
            var ids = new List<string>();
            foreach (var door in LivingStreetLayout.Doors)
            {
                ids.AddRange(LivingStreetLayout.NeighboursAt(door));
            }

            var recipes = new List<string>();
            foreach (var n in CrowdKit.Neighbours)
            {
                recipes.Add(n.Id);
            }

            CollectionAssert.AreEquivalent(ids, recipes, "one recipe for every neighbour in a doorway");
            var seen = new HashSet<string>();
            foreach (var n in CrowdKit.Neighbours)
            {
                Assert.AreSame(n, CrowdKit.Find(n.Id), n.Id + ": the slice's look resolves to the art");
                Assert.AreEqual(CrowdKit.NeighbourModel, n.Model);
                Assert.IsTrue(n.IsGrownUp, n.Id);
                Assert.IsTrue(CrowdLooks.IsAllowed(n.Id), n.Id);
                var slots = new HashSet<string>();
                foreach (string part in n.Parts)
                {
                    string slot = CrowdKit.SlotOf(n.Model, part);
                    Assert.IsNotNull(slot, $"{n.Id}: {part} is a neighbour part");
                    Assert.IsTrue(slots.Add(slot), $"{n.Id}: two parts in {slot}");
                }

                foreach (string required in new[] { CrowdKit.SlotBody, CrowdKit.SlotHands, CrowdKit.SlotShoes, CrowdKit.SlotHead, CrowdKit.SlotHair })
                {
                    Assert.IsTrue(slots.Contains(required), n.Id + " has no " + required);
                }

                var sorted = new List<string>(n.Parts);
                sorted.Sort(StringComparer.Ordinal);
                Assert.IsTrue(seen.Add(string.Join(",", sorted)), n.Id + " repeats another neighbour");
            }

            Assert.AreEqual(CrowdKit.WalkSpeed(CrowdKit.AdultModel), CrowdKit.WalkSpeed(CrowdKit.NeighbourModel), "the grown-up body's clips");
        }

        [Test]
        public void NoNeighbourPartUsesAReservedLookAndNamesAreUnique()
        {
            var names = new HashSet<string>();
            foreach (var p in CrowdKit.NeighbourParts)
            {
                Assert.IsTrue(names.Add(p.Name), "duplicate part " + p.Name);
                StringAssert.DoesNotContain("_LOD", p.Name.ToUpperInvariant(), "Unity builds LOD groups from _LOD<n> names");
                foreach (var word in CrowdKit.ReservedWords)
                {
                    StringAssert.DoesNotContain(word, p.Name, "living-street §3.4 rule 6: reserved look");
                }
            }

            Assert.AreSame(CrowdKit.NeighbourParts, CrowdKit.PartsOf(CrowdKit.NeighbourModel));
            Assert.IsNull(CrowdKit.SlotOf(CrowdKit.AdultModel, "Body_Fogg"), "the walkers' file is unchanged");
        }
    }
}
