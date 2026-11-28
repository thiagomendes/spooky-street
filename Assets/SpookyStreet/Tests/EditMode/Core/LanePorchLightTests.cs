using NUnit.Framework;
using SpookyStreet.Core.LivingStreet;
using SpookyStreet.Core.World;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// The east lane's houses in the game (docs/living-street-lane-houses.md): the two fronts that face away from the moon
    /// get a small porch light that stays far from the case (it never lights a clue or the case block, so it can never
    /// read as a hint), under the owl house's door lamp, warmer only where the gag warms the lamp; the neighbours stand on
    /// their landing, Mr. Fogg a little further out so his wave clears the door head.
    /// </summary>
    public class LanePorchLightTests
    {
        [Test]
        public void OnlyTheFrontsAwayFromTheMoonGetAPorchLight()
        {
            Assert.IsTrue(LivingStreetLayout.HasPorchLight(LivingStreetLayout.Door(LivingStreetLayout.Fogg)));
            Assert.IsTrue(LivingStreetLayout.HasPorchLight(LivingStreetLayout.Door(LivingStreetLayout.Pearl)));
            Assert.IsFalse(LivingStreetLayout.HasPorchLight(LivingStreetLayout.Door(LivingStreetLayout.Abbotts)),
                "the Abbotts face the moon and read without one");
            Assert.IsFalse(LivingStreetLayout.HasPorchLight(null));
            Assert.AreEqual(0f, LivingStreetLayout.PorchLight(LivingStreetLayout.Door(LivingStreetLayout.Abbotts), true));
        }

        [Test]
        public void PorchLightsStayUnderTheOwlHousesDoorLampAndWarmOnlyWithTheGag()
        {
            foreach (var door in LivingStreetLayout.Doors)
            {
                if (!LivingStreetLayout.HasPorchLight(door))
                {
                    continue;
                }

                float rest = LivingStreetLayout.PorchLight(door, false);
                float answering = LivingStreetLayout.PorchLight(door, true);
                Assert.Greater(rest, 0f, door.Id);
                Assert.Less(answering, LivingStreetLayout.OwlDoorLight, door.Id + ": the owl house stays the best-lit front");
                if (door.Gag.LampBoost > 0f)
                {
                    Assert.Greater(answering, rest, door.Id + ": the light warms with the lamp");
                    Assert.AreEqual((LivingStreetLayout.PorchLampGlow + door.Gag.LampBoost) / LivingStreetLayout.PorchLampGlow,
                        answering / rest, 1e-4f, door.Id + ": in step with the lamp's glass");
                }
                else
                {
                    Assert.AreEqual(rest, answering, 1e-6f, door.Id + ": no gag, no change");
                }
            }

            Assert.Less(LivingStreetLayout.PorchLampGlow, GlowLevels.AtticWindow);
            Assert.AreEqual(1.7f, LivingStreetLayout.PorchLampGlow + LivingStreetLayout.Door(LivingStreetLayout.Pearl).Gag.LampBoost,
                1e-4f, "Nana Pearl's lamp at 1.7 (the art doc's value)");
        }

        [Test]
        public void PorchLightsNeverReachTheCaseBlockOrAClue()
        {
            foreach (var door in LivingStreetLayout.Doors)
            {
                if (!LivingStreetLayout.HasPorchLight(door))
                {
                    continue;
                }

                LivingStreetLayout.PorchLightWorld(door, out float x, out float z);
                float range = LivingStreetLayout.PorchLightRange;
                Assert.IsTrue(LivingStreetLayout.InEastLane(x, z), door.Id + ": the light is in the lane");
                Assert.Greater(x - EnvironmentLayout.BlockHalfX, range, door.Id + ": its reach ends before the case block");
                float clue = LivingStreetRules.ClueDistance(x, z, out string nearest);
                Assert.Greater(clue, range + 1.5f, door.Id + ": far from " + nearest);
            }
        }

        [Test]
        public void PorchLightsHangInFrontOfTheDoorUnderTheEaves()
        {
            foreach (var door in LivingStreetLayout.Doors)
            {
                LivingStreetLayout.PorchLightLocal(door, out float lx, out float ly, out float lz);
                LaneHouseArt.Door(door.Id, out float across, out float wall);
                Assert.Less(lz, wall, door.Id + ": in front of the wall");
                Assert.Greater(lz, LaneHouseArt.Stand(door.Id), door.Id + ": behind the visitor's stand");
                Assert.Less(System.Math.Abs(lx - across), LaneHouseArt.LampAcross, door.Id + ": between the door and its lamp");
                Assert.Greater(ly, LaneHouseArt.Sill + LaneHouseArt.OpeningHeight, door.Id + ": above the door head");
                Assert.Less(ly, LaneHouseArt.Sill + LaneHouseArt.OpeningHeight + 0.32f, door.Id + ": under the hood");
            }
        }

        [Test]
        public void NeighboursStandOnTheirLandingAndMrFoggClearsHisDoorHead()
        {
            foreach (var door in LivingStreetLayout.Doors)
            {
                float o = LivingStreetLayout.NeighbourOut(door);
                Assert.GreaterOrEqual(o, LivingStreetLayout.DoorwayOut, door.Id);
                Assert.Less(o, LivingStreetLayout.LandingDepth, door.Id + ": on the landing, not on the steps");
            }

            Assert.Greater(LivingStreetLayout.NeighbourOut(LivingStreetLayout.Door(LivingStreetLayout.Fogg)), LivingStreetLayout.DoorwayOut,
                "Mr. Fogg stands a little further out, so his wave clears the door head");
        }
    }
}
