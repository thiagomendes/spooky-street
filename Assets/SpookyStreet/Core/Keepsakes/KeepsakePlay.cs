using System;
using System.Collections.Generic;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.World;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.Keepsakes;

namespace SpookyStreet.Core.Keepsakes
{
    /// <summary>
    /// The bat mobile on the arrival porch (S004 §2.1): the diegetic count. One hook shows per found bat with that bat
    /// hanging on it, so there is never an empty hook (engagement §2.5); a ring's hoop shows once it has a bat (ring 1,
    /// the mobile's frame, always), and at 10, 20 and 30 the ring closes round its little lantern, which lights. Pure.
    /// </summary>
    public static class BatMobileRules
    {
        /// <summary>Hooks that carry a bat: one per found bat, at most 30.</summary>
        public static int HooksShown(int batCount) => Math.Max(0, Math.Min(batCount, KeepsakeArt.MobileHooks));

        /// <summary>Hook 1..30 shows (with its bat) once that many bats are found.</summary>
        public static bool HookShown(int hook, int batCount) => hook >= 1 && hook <= HooksShown(batCount);

        /// <summary>Ring 1 is the mobile's frame; rings 2 and 3 appear with their first bat.</summary>
        public static bool RingShown(int ring, int batCount) =>
            ring == 1 || (ring >= 1 && ring <= KeepsakeArt.MobileRings && batCount > (ring - 1) * KeepsakeArt.HooksPerRing);

        /// <summary>Ring 1..3 closes when its ten bats are found (10, 20, 30).</summary>
        public static bool RingClosed(int ring, int batCount) =>
            ring >= 1 && ring <= KeepsakeArt.MobileRings && batCount >= ring * KeepsakeArt.HooksPerRing;

        /// <summary>The ring's little lantern lights as it closes.</summary>
        public static bool LanternLit(int ring, int batCount) => RingClosed(ring, batCount);
    }

    /// <summary>
    /// Where the keepsakes live on Pip's own porch, the arrival porch (deck 4 × 2 m at 0.6 m, front edge z −13; S004 §2.1):
    /// the bat mobile hanging from a lantern pole on the east rail (in the title frame's upper right), and the dress-up
    /// trunk, one of the moving boxes on the back corner. Pure data and rules, tested in Core.
    /// </summary>
    public static class KeepsakePorch
    {
        public const float DeckY = 0.6f;
        public const float DeckMinX = -2f;
        public const float DeckMaxX = 2f;
        public const float DeckMinZ = -15f;
        public const float DeckMaxZ = -13f;

        /// <summary>The lantern pole stands on the east rail (inside its collider, so Pip never walks into it).</summary>
        public const float PoleX = 1.95f;
        public const float PoleZ = -13.6f;
        public const float PoleTop = 3.55f;

        /// <summary>The mobile's hook under the pole's arm (the BatMobile model's origin).</summary>
        public const float MobileX = 1.6f;
        public const float MobileY = 3.4f;
        public const float MobileZ = -13.6f;

        /// <summary>The mobile's lowest point (its bats included).</summary>
        public const float MobileBottom = MobileY - KeepsakeArt.MobileDrop;

        /// <summary>The trunk: the porch crate's size, on the back corner beside the moving boxes, its front to the street.</summary>
        public const float TrunkX = 0.7f;
        public const float TrunkY = DeckY;
        public const float TrunkZ = -14.62f;

        /// <summary>The trunk's front face (the side Pip opens it from).</summary>
        public const float TrunkFrontZ = TrunkZ + KeepsakeArt.TrunkSize * 0.5f;

        /// <summary>Pip's centre within this of the trunk's front centre (horizontal metres) offers "Dress up".</summary>
        public const float TrunkReach = 1.0f;

        /// <summary>Above the trunk's top, the Use marker floats.</summary>
        public const float TrunkMarkerHeight = KeepsakeArt.TrunkSize + 0.35f;

        /// <summary>Where Pip stands while she dresses up: in front of the open trunk, turned to the street (yaw 0).</summary>
        public static readonly StagePoint DressingSpot = new StagePoint(TrunkX, DeckY + 0.02f, TrunkFrontZ + 0.55f);

        /// <summary>The dressing camera: in front of Pip and a little up, the open trunk behind her, Pip left of centre
        /// so the pieces' cards fit on the right.</summary>
        public static readonly StagePoint DressingCamera = new StagePoint(0.05f, 1.72f, -11.55f);

        public static readonly StagePoint DressingLook = new StagePoint(0.3f, 1.0f, -14.05f);

        public const float DressingFieldOfView = 40f;

        /// <summary>The trunk is a dress-up target only once a piece is earned (it never opens on an empty slot).</summary>
        public static bool TrunkOffered(KeepsakeState state) => state != null && state.EarnedPieces.Count > 0;

        /// <summary>True when Pip (feet at x, y, z) stands on the deck within reach of the trunk's front.</summary>
        public static bool TrunkInReach(float x, float y, float z)
        {
            if (Math.Abs(y - DeckY) > 0.45f)
            {
                return false;
            }

            float dx = x - TrunkX;
            float dz = z - TrunkFrontZ;
            return dz > -0.1f && dx * dx + dz * dz <= TrunkReach * TrunkReach;
        }
    }

    /// <summary>
    /// The dress-up trunk screen's focus (S004 F1 base): the close disc on top, one card per earned piece in a column.
    /// Unearned pieces are never shown (no empty slot), so their cards are disabled and drop out of the graph.
    /// </summary>
    public static class DressUpFocus
    {
        public const string Close = "close";

        public static string Piece(CostumePiece piece) => piece.Id;

        /// <summary>The graph with only <paramref name="state"/>'s earned pieces enabled (null: all three).</summary>
        public static UiFocusGraph Build(KeepsakeState state = null)
        {
            var graph = new UiFocusGraph("dress_up");
            graph.Add(Close, 0, 0);
            int row = 1;
            foreach (var piece in CostumePieces.All)
            {
                graph.Add(Piece(piece), row++, 0);
            }

            graph.Exit = Close;
            graph.DefaultFocus = Piece(CostumePieces.StarCape);
            if (state != null)
            {
                int earned = KeepsakeRules.PiecesEarned(state.BatCount);
                foreach (var piece in CostumePieces.All)
                {
                    graph.SetEnabled(Piece(piece), piece.Order <= earned);
                }
            }

            return graph;
        }

        /// <summary>The name under each piece's picture.</summary>
        public static string NameKey(CostumePiece piece)
        {
            if (piece == CostumePieces.StarCape)
            {
                return Keys.PieceStarCape;
            }

            return piece == CostumePieces.LanternPail ? Keys.PiecePail : Keys.PieceHat;
        }
    }

    /// <summary>
    /// The notebook's keepsakes page (S004 §2.1): earned sketch pages only and a small stack of the folded bats not yet
    /// in a page, with its own number (never a total). The page completed by the 10th, 20th or 30th bat assembles from
    /// its ten bats the next time the notebook opens, once (memory only: a page completed and never seen before the app
    /// closes simply shows assembled).
    /// </summary>
    public sealed class KeepsakeNotebook
    {
        private readonly List<int> _pending = new List<int>();

        /// <summary>The page appears with the first bat.</summary>
        public static bool HasPage(KeepsakeState state) => state != null && state.BatCount > 0;

        /// <summary>The bat stack beside the pages shows only while it holds a bat.</summary>
        public static bool ShowsStack(KeepsakeState state) => state != null && state.LooseBats > 0;

        /// <summary>Pages waiting to assemble (in order).</summary>
        public IReadOnlyList<int> Pending => _pending;

        /// <summary>A find completed <paramref name="page"/> (1–3; 0 ignored).</summary>
        public void Completed(int page)
        {
            if (page >= 1 && page <= SketchPages.All.Count && !_pending.Contains(page))
            {
                _pending.Add(page);
            }
        }

        /// <summary>The notebook opened: the pages to assemble now, each once.</summary>
        public IReadOnlyList<int> TakePending()
        {
            var taken = _pending.ToArray();
            _pending.Clear();
            return taken;
        }

        /// <summary>A New game or a reload forgets what was waiting.</summary>
        public void Reset() => _pending.Clear();
    }

    /// <summary>The title's Continue line (S004 §3 A2: "count only"): the bat count shows only once a bat is found.</summary>
    public static class KeepsakeTitle
    {
        public static bool ShowsBatCount(KeepsakeState state) => state != null && state.BatCount > 0;
    }
}
