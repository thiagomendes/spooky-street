using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.Keepsakes
{
    /// <summary>Where a costume piece goes on Pip: three slots, so all three can be worn together.</summary>
    public enum CostumeSlot
    {
        /// <summary>The cape's lining (a material variant only).</summary>
        CapeLining,

        /// <summary>The trick-or-treat pail.</summary>
        Pail,

        /// <summary>Between her bat ears.</summary>
        Hat,
    }

    /// <summary>One cosmetic costume piece (S004 §2.1): earned with a sketch page, worn in play and on the title.</summary>
    public sealed class CostumePiece
    {
        internal CostumePiece(string id, int order, CostumeSlot slot)
        {
            Id = id;
            Order = order;
            Slot = slot;
        }

        /// <summary>Stable ID (never display text).</summary>
        public string Id { get; }

        /// <summary>1–3: earned at 10, 20 and 30 bats.</summary>
        public int Order { get; }

        public CostumeSlot Slot { get; }

        public override string ToString() => Id;
    }

    /// <summary>The three pieces, in order: star-lined cape, lantern pail, showman's hat. Cosmetic only.</summary>
    public static class CostumePieces
    {
        public static readonly CostumePiece StarCape = new CostumePiece("keepsake.costume.star_cape", 1, CostumeSlot.CapeLining);

        public static readonly CostumePiece LanternPail = new CostumePiece("keepsake.costume.lantern_pail", 2, CostumeSlot.Pail);

        public static readonly CostumePiece ShowmanHat = new CostumePiece("keepsake.costume.showman_hat", 3, CostumeSlot.Hat);

        public static readonly IReadOnlyList<CostumePiece> All = new[] { StarCape, LanternPail, ShowmanHat };

        public static bool TryGet(string id, out CostumePiece piece)
        {
            foreach (var p in All)
            {
                if (string.Equals(p.Id, id, StringComparison.Ordinal))
                {
                    piece = p;
                    return true;
                }
            }

            piece = null;
            return false;
        }
    }
}
