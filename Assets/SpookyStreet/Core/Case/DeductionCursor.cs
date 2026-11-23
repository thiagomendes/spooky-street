using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.Case
{
    /// <summary>Where the notebook's cursor is: the question's slots or the shared tray.</summary>
    public enum CursorRow
    {
        Slots,
        Tray,
    }

    /// <summary>What a Use press (or a tap) did on the deduction board.</summary>
    public enum CursorResult
    {
        None,

        /// <summary>A tray sticker is now held (tap-tap: the next slot tap places it).</summary>
        Picked,

        /// <summary>The held sticker was let go (picked again, or Back).</summary>
        Dropped,

        /// <summary>The held sticker was pressed into a slot.</summary>
        Placed,

        /// <summary>A placed sticker was sent home to the tray.</summary>
        Returned,

        /// <summary>The sticker cannot go there (set-aside decoy).</summary>
        Refused,
    }

    /// <summary>
    /// The notebook's tap-tap and cursor model (D25, docs/mystery-first.md §5.2): one model serves touch (tap a
    /// sticker, tap a slot), keyboard and gamepad (move over the slots and the tray, Use picks and places). Drag is the
    /// view's own gesture and ends in <see cref="DeductionBoard.Place"/>. Pure.
    /// </summary>
    public sealed class DeductionCursor
    {
        public CursorRow Row { get; private set; } = CursorRow.Tray;

        public int Index { get; private set; }

        /// <summary>The sticker picked up and waiting for a slot, or null.</summary>
        public string Held { get; private set; }

        public void Reset()
        {
            Row = CursorRow.Tray;
            Index = 0;
            Held = null;
        }

        public void Drop() => Held = null;

        /// <summary>Puts the cursor on a target (a tap or a pointer hover does this before <see cref="Use"/>).</summary>
        public void Set(CursorRow row, int index)
        {
            Row = row;
            Index = Math.Max(0, index);
        }

        /// <summary>Moves by one step: left/right within a row, up to the slots, down to the tray. Clamped.</summary>
        public void Move(int dx, int dy, int slotCount, int trayCount)
        {
            if (dy < 0 && Row == CursorRow.Tray && slotCount > 0)
            {
                Row = CursorRow.Slots;
                Index = Math.Min(Index * slotCount / Math.Max(1, trayCount), slotCount - 1);
            }
            else if (dy > 0 && Row == CursorRow.Slots && trayCount > 0)
            {
                Row = CursorRow.Tray;
                Index = Math.Min(Index * trayCount / Math.Max(1, slotCount), trayCount - 1);
            }

            int count = Row == CursorRow.Slots ? slotCount : trayCount;
            Index = count <= 0 ? 0 : Math.Max(0, Math.Min(count - 1, Index + dx));
        }

        /// <summary>
        /// Use on the cursor's target. In the tray: pick the sticker (or let it go if it is already held), and jump to
        /// the first empty slot. On a slot: place the held sticker, or send the slot's sticker home.
        /// </summary>
        public CursorResult Use(DeductionBoard board, IReadOnlyList<string> tray, ICollection<string> found)
        {
            if (board == null)
            {
                throw new ArgumentNullException(nameof(board));
            }

            if (Row == CursorRow.Tray)
            {
                if (tray == null || Index >= tray.Count || tray[Index] == null)
                {
                    return CursorResult.None;
                }

                string id = tray[Index];
                if (Held == id)
                {
                    Held = null;
                    return CursorResult.Dropped;
                }

                if (board.DecoyAside && board.Question.IsDecoy(id))
                {
                    return CursorResult.Refused;
                }

                Held = id;
                int empty = board.FirstEmpty();
                if (empty >= 0)
                {
                    Row = CursorRow.Slots;
                    Index = empty;
                }

                return CursorResult.Picked;
            }

            if (Index >= board.SlotCount)
            {
                return CursorResult.None;
            }

            if (Held != null)
            {
                bool placed = board.Place(Index, Held, found);
                Held = null;
                return placed ? CursorResult.Placed : CursorResult.Refused;
            }

            return board.Remove(Index) != null ? CursorResult.Returned : CursorResult.None;
        }
    }
}
