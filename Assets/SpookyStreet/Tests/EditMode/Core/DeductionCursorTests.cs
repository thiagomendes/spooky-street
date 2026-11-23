using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Case;
using N = SpookyStreet.Core.Case.CaseNotebook;

namespace SpookyStreet.Tests.Core
{
    /// <summary>S002.5a: one tap-tap model for touch, keyboard and gamepad (D25).</summary>
    public class DeductionCursorTests
    {
        private static HashSet<string> Found(params CaseFact[] facts) => N.FoundIds(CaseState.FromFacts(facts));

        [Test]
        public void UsePicksFromTheTrayJumpsToTheFirstEmptySlotAndPlaces()
        {
            var found = Found(CaseFact.FloorHum, CaseFact.SawCam, CaseFact.ExaminedStreamer);
            var board = new DeductionBoard(N.Question(N.QuestionSource));
            var cursor = new DeductionCursor();
            var tray = new List<string> { N.FloorHum, N.WoodenPiece, N.Streamer };
            Assert.AreEqual(CursorRow.Tray, cursor.Row);

            cursor.Move(1, 0, board.SlotCount, tray.Count);
            Assert.AreEqual(1, cursor.Index);
            Assert.AreEqual(CursorResult.Picked, cursor.Use(board, tray, found));
            Assert.AreEqual(N.WoodenPiece, cursor.Held);
            Assert.AreEqual(CursorRow.Slots, cursor.Row);
            Assert.AreEqual(0, cursor.Index, "the first empty slot");
            Assert.AreEqual(CursorResult.Placed, cursor.Use(board, tray, found));
            Assert.AreEqual(N.WoodenPiece, board.Slots[0]);
            Assert.IsNull(cursor.Held);

            // Use on a filled slot with nothing held sends its sticker home.
            Assert.AreEqual(CursorResult.Returned, cursor.Use(board, tray, found));
            Assert.IsTrue(board.IsEmpty);
        }

        [Test]
        public void PickingTheHeldStickerAgainLetsItGoAndMovesClamp()
        {
            var found = Found(CaseFact.FloorHum);
            var board = new DeductionBoard(N.Question(N.QuestionSource));
            var cursor = new DeductionCursor();
            var tray = new List<string> { N.FloorHum };
            cursor.Use(board, tray, found);
            cursor.Set(CursorRow.Tray, 0);
            Assert.AreEqual(CursorResult.Dropped, cursor.Use(board, tray, found));
            Assert.IsNull(cursor.Held);

            cursor.Move(-5, 0, 2, 1);
            Assert.AreEqual(0, cursor.Index);
            cursor.Move(0, -1, 2, 1);
            Assert.AreEqual(CursorRow.Slots, cursor.Row);
            cursor.Move(9, 0, 2, 1);
            Assert.AreEqual(1, cursor.Index, "clamped to the last slot");
            cursor.Move(0, 1, 2, 1);
            Assert.AreEqual(CursorRow.Tray, cursor.Row);
            Assert.AreEqual(0, cursor.Index);
            Assert.AreEqual(CursorResult.None, new DeductionCursor().Use(board, new string[0], found), "an empty tray");
        }

        [Test]
        public void ASetAsideDecoyIsRefused()
        {
            var state = CaseState.FromFacts(new[]
            {
                CaseFact.BeaSpoke, CaseFact.ValeSpoke, CaseFact.ExaminedCloth, CaseFact.ExaminedSill, CaseFact.ExaminedStreamer,
            });
            var found = N.FoundIds(state);
            var board = new DeductionBoard(N.Question(N.QuestionEntry));
            for (int t = 0; t < CaseDeduction.DecoyAsideAfterTries; t++)
            {
                board.Clear();
                board.Place(0, N.Streamer, found);
                board.Place(1, N.BeaFlash, found);
                board.Place(2, N.ValeDoor, found);
                board.Check(found);
            }

            Assert.IsTrue(board.DecoyAside);
            board.Clear();
            var cursor = new DeductionCursor();
            Assert.AreEqual(CursorResult.Refused, cursor.Use(board, new[] { N.Streamer }, found));
            Assert.IsNull(cursor.Held);
        }
    }
}
