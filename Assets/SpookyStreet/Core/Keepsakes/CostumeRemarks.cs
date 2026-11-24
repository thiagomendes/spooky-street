using System;
using System.Collections.Generic;
using SpookyStreet.Core.Case;

namespace SpookyStreet.Core.Keepsakes
{
    /// <summary>One witness remark about a worn costume piece (engagement §1.9): said once ever, by one friend.</summary>
    public sealed class CostumeRemark
    {
        internal CostumeRemark(string id, CostumePiece piece, Speaker speaker, bool afterStamp)
        {
            Id = id;
            Piece = piece;
            Speaker = speaker;
            AfterStamp = afterStamp;
        }

        /// <summary>Stable ID, saved once said (never display text).</summary>
        public string Id { get; }

        public CostumePiece Piece { get; }

        public Speaker Speaker { get; }

        /// <summary>Only once case 01 is closed (Aunt Kit stands at her open booth only then).</summary>
        public bool AfterStamp { get; }

        public override string ToString() => Id;
    }

    /// <summary>
    /// The three remarks (S004 §2.1): June on the cape, Mr. Vale on the pail, Aunt Kit on the hat after the stamp. They
    /// are greetings, never testimony. Their lines, vignettes and voices come with the dress-up (stream A2); this table
    /// holds the stable IDs and the once-only rule so the save can record them now.
    /// </summary>
    public static class CostumeRemarks
    {
        public static readonly CostumeRemark JuneCape =
            new CostumeRemark("keepsake.remark.june.star_cape", CostumePieces.StarCape, Speaker.June, false);

        public static readonly CostumeRemark ValePail =
            new CostumeRemark("keepsake.remark.vale.lantern_pail", CostumePieces.LanternPail, Speaker.Vale, false);

        public static readonly CostumeRemark KitHat =
            new CostumeRemark("keepsake.remark.kit.showman_hat", CostumePieces.ShowmanHat, Speaker.Kit, true);

        public static readonly IReadOnlyList<CostumeRemark> All = new[] { JuneCape, ValePail, KitHat };

        public static bool IsKnown(string id)
        {
            foreach (var remark in All)
            {
                if (string.Equals(remark.Id, id, StringComparison.Ordinal))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>
        /// The remark <paramref name="speaker"/> makes on this talk, or null: Pip wears its piece, it was never said, and
        /// (for Kit) case 01 is closed. Pure; the caller records it with <see cref="KeepsakeState.Said"/>.
        /// </summary>
        public static CostumeRemark Due(KeepsakeState state, Speaker speaker, bool caseStamped)
        {
            if (state == null)
            {
                return null;
            }

            foreach (var remark in All)
            {
                if (remark.Speaker == speaker && state.IsWearing(remark.Piece.Id) && !state.HasSaid(remark.Id) &&
                    (!remark.AfterStamp || caseStamped))
                {
                    return remark;
                }
            }

            return null;
        }
    }
}
