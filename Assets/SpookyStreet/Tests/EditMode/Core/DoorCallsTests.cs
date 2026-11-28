using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpookyStreet.Core.LivingStreet;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// A long stay by one door no longer repeats the groups' call (docs/living-street-slice.md §6): a neighbour's door
    /// calls once while Pip stays near it, and again only after she has walked away and come back. No timer decides it.
    /// </summary>
    public class DoorCallsTests
    {
        private static NeighbourDoor Fogg => LivingStreetLayout.Door(LivingStreetLayout.Fogg);

        [Test]
        public void ADoorCallsOnceWhilePipStaysNear()
        {
            var calls = new DoorCalls();
            var door = Fogg;
            Assert.IsTrue(calls.Ring(door.Id, door.DoorX, door.DoorZ, door.StandX, door.StandZ), "the first call is heard");
            calls.Update(door.StandX, door.StandZ);
            Assert.IsTrue(calls.Holding(door.Id));
            for (int lap = 0; lap < 5; lap++)
            {
                calls.Update(door.StandX + 3f, door.StandZ);
                Assert.IsFalse(calls.Ring(door.Id, door.DoorX, door.DoorZ, door.StandX + 3f, door.StandZ), "lap " + lap);
            }
        }

        [Test]
        public void ADoorCallsAgainOnlyAfterPipWalksAwayAndBack()
        {
            var calls = new DoorCalls();
            var door = Fogg;
            Assert.IsTrue(calls.Ring(door.Id, door.DoorX, door.DoorZ, door.DoorX, door.DoorZ - 2f));

            // At the edge of hearing (between 14 and 16 m) the door still holds: no flicker on the boundary.
            float edge = DoorCalls.HearingDistance + DoorCalls.LeaveMargin * 0.5f;
            calls.Update(door.DoorX, door.DoorZ - edge);
            Assert.IsTrue(calls.Holding(door.Id));
            Assert.IsFalse(calls.Ring(door.Id, door.DoorX, door.DoorZ, door.DoorX, door.DoorZ - 2f));

            // Out of hearing a ring plays nothing and is not remembered.
            float away = DoorCalls.HearingDistance + DoorCalls.LeaveMargin + 1f;
            calls.Update(door.DoorX, door.DoorZ - away);
            Assert.IsFalse(calls.Holding(door.Id), "she walked away");
            Assert.IsFalse(calls.Ring(door.Id, door.DoorX, door.DoorZ, door.DoorX, door.DoorZ - away), "too far to hear");
            Assert.IsFalse(calls.Holding(door.Id));

            // Back by the door: it calls again, once.
            Assert.IsTrue(calls.Ring(door.Id, door.DoorX, door.DoorZ, door.DoorX, door.DoorZ - 2f));
            Assert.IsFalse(calls.Ring(door.Id, door.DoorX, door.DoorZ, door.DoorX, door.DoorZ - 2f));
        }

        [Test]
        public void EachDoorHoldsItsOwnCallAndResetClearsThem()
        {
            var calls = new DoorCalls();
            var doors = LivingStreetLayout.Doors;
            // Pip in the middle of the lane hears all three doors once each.
            float x = doors.Average(d => d.DoorX);
            float z = doors.Average(d => d.DoorZ);
            foreach (var door in doors)
            {
                Assert.IsTrue(calls.Ring(door.Id, door.DoorX, door.DoorZ, x, z), door.Id);
            }

            foreach (var door in doors)
            {
                Assert.IsFalse(calls.Ring(door.Id, door.DoorX, door.DoorZ, x, z), door.Id);
            }

            calls.Reset();
            Assert.IsTrue(calls.Ring(doors[0].Id, doors[0].DoorX, doors[0].DoorZ, x, z), "a new game: doors call again");
        }

        [Test]
        public void TwentyMinutesByOneDoorHearItsCallOnce()
        {
            // The crowd's own clock: groups ring Mr. Fogg's door lap after lap while Pip stands at his steps.
            var crowd = new CrowdClock();
            var calls = new DoorCalls();
            var door = Fogg;
            var events = new List<CrowdEvent>();
            var inputs = new CrowdInputs { PipX = door.StandX, PipZ = door.StandZ, PipOutside = true, CaseOpen = true };
            int rings = 0;
            int heard = 0;
            for (int frame = 0; frame < 60 * 60 * 20; frame++)
            {
                events.Clear();
                calls.Update(inputs.PipX, inputs.PipZ);
                crowd.Advance(1f / 60f, inputs, events);
                foreach (var e in events.Where(e => e.Kind == CrowdEventKind.DoorRung && e.DoorId == door.Id))
                {
                    rings++;
                    heard += calls.Ring(e.DoorId, door.DoorX, door.DoorZ, inputs.PipX, inputs.PipZ) ? 1 : 0;
                }
            }

            TestContext.WriteLine($"rings at {door.Id}: {rings}, calls heard: {heard}");
            Assert.GreaterOrEqual(rings, 3, "groups keep ringing his door lap after lap");
            Assert.AreEqual(1, heard, "the child hears the call once while she stays");

            // She walks to the far end of the street and back: the next ring calls again.
            calls.Update(door.StandX, door.StandZ - 30f);
            Assert.IsTrue(calls.Ring(door.Id, door.DoorX, door.DoorZ, door.StandX, door.StandZ));
        }
    }
}
