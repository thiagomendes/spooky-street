using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.Keepsakes
{
    /// <summary>What finding one paper bat did (<see cref="KeepsakeState.Find"/>).</summary>
    public enum BatFind
    {
        /// <summary>A new bat: it joins the stack.</summary>
        Found,

        /// <summary>Already found (bats never respawn): nothing changes.</summary>
        AlreadyFound,

        /// <summary>Not a bat of the layout (an old or foreign ID): ignored.</summary>
        Unknown,
    }

    /// <summary>The result of finding a bat: the next state, what happened and the page it completed (if any).</summary>
    public readonly struct BatFindResult
    {
        public BatFindResult(KeepsakeState state, BatFind outcome, int completedPage)
        {
            State = state;
            Outcome = outcome;
            CompletedPage = completedPage;
        }

        public KeepsakeState State { get; }

        public BatFind Outcome { get; }

        /// <summary>
        /// The sketch page (1–3) this bat completed, or 0. A page completes exactly once, on the find that brings the
        /// count to 10, 20 or 30 (the cue for the notebook disc's single wobble; no pop-up, engagement §2.5).
        /// </summary>
        public int CompletedPage { get; }
    }

    /// <summary>
    /// The keepsakes (S004 §2.1): the paper bats found, the costume pieces worn and the witness remarks already said,
    /// all as stable IDs (never display text). Immutable. Pages and pieces are never stored: they are derived from the
    /// count, so they can never disagree with the bats. Every 10 bats complete one sketch page and earn one costume piece.
    /// </summary>
    public sealed class KeepsakeState : IEquatable<KeepsakeState>
    {
        public static readonly KeepsakeState Empty =
            new KeepsakeState(Array.Empty<string>(), Array.Empty<string>(), Array.Empty<string>());

        private readonly string[] _bats;
        private readonly string[] _worn;
        private readonly string[] _said;

        private KeepsakeState(string[] bats, string[] worn, string[] said)
        {
            _bats = bats;
            _worn = worn;
            _said = said;
        }

        /// <summary>
        /// Builds a state from saved IDs, keeping order and dropping duplicates and unknown IDs (a newer build's bat, a
        /// piece that does not exist) and worn pieces that the bats do not earn, so a file can never wear more than it found.
        /// </summary>
        public static KeepsakeState From(IEnumerable<string> bats, IEnumerable<string> worn, IEnumerable<string> said)
        {
            var found = new List<string>();
            foreach (string id in bats ?? Array.Empty<string>())
            {
                if (PaperBatLayout.IsKnown(id) && !found.Contains(id))
                {
                    found.Add(id);
                }
            }

            int earned = KeepsakeRules.PiecesEarned(found.Count);
            var wearing = new List<string>();
            foreach (string id in worn ?? Array.Empty<string>())
            {
                if (CostumePieces.TryGet(id, out var piece) && piece.Order <= earned && !wearing.Contains(id))
                {
                    wearing.Add(id);
                }
            }

            var remarks = new List<string>();
            foreach (string id in said ?? Array.Empty<string>())
            {
                if (CostumeRemarks.IsKnown(id) && !remarks.Contains(id))
                {
                    remarks.Add(id);
                }
            }

            return new KeepsakeState(found.ToArray(), wearing.ToArray(), remarks.ToArray());
        }

        /// <summary>Found bat IDs, in the order they were found.</summary>
        public IReadOnlyList<string> FoundBats => _bats;

        /// <summary>Costume pieces Pip wears now (IDs, each earned).</summary>
        public IReadOnlyList<string> WornPieces => _worn;

        /// <summary>Witness remarks already said once (IDs).</summary>
        public IReadOnlyList<string> SaidRemarks => _said;

        /// <summary>How many bats were found (the parent page alone shows it against the total).</summary>
        public int BatCount => _bats.Length;

        public bool IsEmpty => _bats.Length == 0 && _worn.Length == 0 && _said.Length == 0;

        public bool HasFound(string batId) => Array.IndexOf(_bats, batId) >= 0;

        public bool IsWearing(string pieceId) => Array.IndexOf(_worn, pieceId) >= 0;

        public bool HasSaid(string remarkId) => Array.IndexOf(_said, remarkId) >= 0;

        /// <summary>Sketch pages completed (0–3): every 10 bats.</summary>
        public int PagesCompleted => KeepsakeRules.PagesCompleted(_bats.Length);

        /// <summary>The completed pages, in order (only earned ones: no empty slot is ever listed).</summary>
        public IReadOnlyList<SketchPage> Pages
        {
            get
            {
                var pages = new List<SketchPage>();
                foreach (var page in SketchPages.All)
                {
                    if (page.Order <= PagesCompleted)
                    {
                        pages.Add(page);
                    }
                }

                return pages;
            }
        }

        /// <summary>The earned costume pieces, in order (only earned ones).</summary>
        public IReadOnlyList<CostumePiece> EarnedPieces
        {
            get
            {
                int earned = KeepsakeRules.PiecesEarned(_bats.Length);
                var pieces = new List<CostumePiece>();
                foreach (var piece in CostumePieces.All)
                {
                    if (piece.Order <= earned)
                    {
                        pieces.Add(piece);
                    }
                }

                return pieces;
            }
        }

        /// <summary>Bats found toward the next page (0–9): the small stack beside the earned pages.</summary>
        public int LooseBats => _bats.Length >= KeepsakeRules.TotalBats ? 0 : _bats.Length % KeepsakeRules.BatsPerPage;

        /// <summary>Finds a bat: idempotent, a found bat is never found twice, an unknown ID changes nothing.</summary>
        public BatFindResult Find(string batId)
        {
            if (!PaperBatLayout.IsKnown(batId))
            {
                return new BatFindResult(this, BatFind.Unknown, 0);
            }

            if (HasFound(batId))
            {
                return new BatFindResult(this, BatFind.AlreadyFound, 0);
            }

            var bats = new string[_bats.Length + 1];
            Array.Copy(_bats, bats, _bats.Length);
            bats[_bats.Length] = batId;
            var next = new KeepsakeState(bats, _worn, _said);
            int page = next.PagesCompleted > PagesCompleted ? next.PagesCompleted : 0;
            return new BatFindResult(next, BatFind.Found, page);
        }

        /// <summary>Puts an earned piece on or takes it off (an unearned or unknown piece changes nothing).</summary>
        public KeepsakeState Wear(string pieceId, bool on)
        {
            if (!CostumePieces.TryGet(pieceId, out var piece) || piece.Order > KeepsakeRules.PiecesEarned(_bats.Length) ||
                IsWearing(pieceId) == on)
            {
                return this;
            }

            var worn = new List<string>(_worn);
            if (on)
            {
                worn.Add(pieceId);
            }
            else
            {
                worn.Remove(pieceId);
            }

            return new KeepsakeState(_bats, worn.ToArray(), _said);
        }

        /// <summary>Records that a remark was said (once ever; an unknown ID changes nothing).</summary>
        public KeepsakeState Said(string remarkId)
        {
            if (!CostumeRemarks.IsKnown(remarkId) || HasSaid(remarkId))
            {
                return this;
            }

            var said = new string[_said.Length + 1];
            Array.Copy(_said, said, _said.Length);
            said[_said.Length] = remarkId;
            return new KeepsakeState(_bats, _worn, said);
        }

        public bool Equals(KeepsakeState other) =>
            other != null && Same(_bats, other._bats) && Same(_worn, other._worn) && Same(_said, other._said);

        private static bool Same(string[] a, string[] b)
        {
            if (a.Length != b.Length)
            {
                return false;
            }

            for (int i = 0; i < a.Length; i++)
            {
                if (!string.Equals(a[i], b[i], StringComparison.Ordinal))
                {
                    return false;
                }
            }

            return true;
        }

        public override bool Equals(object obj) => Equals(obj as KeepsakeState);

        public override int GetHashCode() => _bats.Length * 961 + _worn.Length * 31 + _said.Length;

        public override string ToString() =>
            $"KeepsakeState[{_bats.Length} bats, pages {PagesCompleted}, wearing {string.Join(", ", _worn)}]";
    }

    /// <summary>
    /// The keepsake rules (S004 §2.1, D29, spec §4). Bats never respawn, are never spent and never gate a clue. A
    /// confirmed "Replay case" keeps every keepsake; a confirmed New game clears them per
    /// <see cref="NewGameClearsKeepsakes"/> (the one switch for open decision 4).
    /// </summary>
    public static class KeepsakeRules
    {
        public const int TotalBats = 30;
        public const int BatsPerPage = 10;

        /// <summary>
        /// Open decision 4 (S004 plan §6), the plan's recommendation (a), owner confirmation pending: a confirmed New game
        /// starts Pip over as the new kid, so every bat is back in the world and the pages and pieces go with them. Flip
        /// this one constant to keep keepsakes across a New game instead; <see cref="AfterNewGame"/> is the only reader.
        /// </summary>
        public const bool NewGameClearsKeepsakes = true;

        public static int PagesCompleted(int batCount) => Math.Max(0, Math.Min(batCount, TotalBats)) / BatsPerPage;

        /// <summary>One costume piece with each completed page.</summary>
        public static int PiecesEarned(int batCount) => PagesCompleted(batCount);

        /// <summary>Confirmed "Replay case": keepsakes stay (bats never respawn, spec §4).</summary>
        public static KeepsakeState AfterReplay(KeepsakeState state) => state ?? KeepsakeState.Empty;

        /// <summary>Confirmed New game (decision 4): cleared while <see cref="NewGameClearsKeepsakes"/>, else kept.</summary>
        public static KeepsakeState AfterNewGame(KeepsakeState state) =>
            NewGameClearsKeepsakes ? KeepsakeState.Empty : state ?? KeepsakeState.Empty;
    }
}
