using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.Case
{
    /// <summary>
    /// The deduction rules of the notebook (D26, docs/mystery-first.md §5). A set is right when every slot holds a
    /// different sticker and each idea of the question is covered by its own sticker (a perfect matching between
    /// stickers and ideas; slot order never matters). Decoys cover no idea, so they never complete a set. Pure.
    /// </summary>
    public static class CaseDeduction
    {
        /// <summary>From this wrong try on, stickers that fit stay in place with a pencil tick (§5.4).</summary>
        public const int KeepFittingFromTry = 3;

        /// <summary>After this many wrong tries, with every needed clue found, the decoy is set aside (§5.4).</summary>
        public const int DecoyAsideAfterTries = 5;

        /// <summary>Pip pauses this long on a full set before the verdict (§5.3).</summary>
        public const float CheckPauseSeconds = 0.4f;

        /// <summary>
        /// A maximum matching of <paramref name="stickers"/> (null entries allowed: empty slots) to the question's ideas.
        /// Returns, per sticker, the idea index it stands for, or -1. Earlier stickers win ties, so the stickers a
        /// child placed first are the ones that stay.
        /// </summary>
        public static int[] Match(CaseQuestion question, IReadOnlyList<string> stickers)
        {
            if (question == null)
            {
                throw new ArgumentNullException(nameof(question));
            }

            int n = stickers?.Count ?? 0;
            var ideaOf = new int[n];
            var stickerOf = new int[question.SlotCount];
            for (int i = 0; i < n; i++)
            {
                ideaOf[i] = -1;
            }

            for (int j = 0; j < stickerOf.Length; j++)
            {
                stickerOf[j] = -1;
            }

            var seen = new HashSet<string>(StringComparer.Ordinal);
            for (int i = 0; i < n; i++)
            {
                string id = stickers[i];
                if (id == null || !seen.Add(id))
                {
                    continue; // an empty slot, or the same sticker twice, stands for nothing
                }

                var visited = new bool[stickerOf.Length];
                Augment(question, stickers, i, ideaOf, stickerOf, visited);
            }

            return ideaOf;
        }

        private static bool Augment(CaseQuestion question, IReadOnlyList<string> stickers, int i, int[] ideaOf,
            int[] stickerOf, bool[] visited)
        {
            for (int j = 0; j < question.SlotCount; j++)
            {
                if (visited[j] || !Contains(question.Ideas[j], stickers[i]))
                {
                    continue;
                }

                visited[j] = true;
                if (stickerOf[j] < 0 || Augment(question, stickers, stickerOf[j], ideaOf, stickerOf, visited))
                {
                    stickerOf[j] = i;
                    ideaOf[i] = j;
                    return true;
                }
            }

            return false;
        }

        /// <summary>How many ideas the best assignment of <paramref name="stickers"/> covers.</summary>
        public static int Covered(CaseQuestion question, IReadOnlyList<string> stickers)
        {
            int count = 0;
            foreach (int idea in Match(question, stickers))
            {
                if (idea >= 0)
                {
                    count++;
                }
            }

            return count;
        }

        /// <summary>True for a full set of different stickers that covers every idea.</summary>
        public static bool IsCorrect(CaseQuestion question, IReadOnlyList<string> stickers)
        {
            if (question == null || stickers == null || stickers.Count != question.SlotCount)
            {
                return false;
            }

            return Covered(question, stickers) == question.SlotCount;
        }

        /// <summary>The found stickers can answer the question (every needed clue is in the tray).</summary>
        public static bool CanSolve(CaseQuestion question, IEnumerable<string> found)
        {
            var list = new List<string>(found ?? Array.Empty<string>());
            return Covered(question, list) == question.SlotCount;
        }

        /// <summary>The answer comic's panels: the child's stickers in idea order (a right set only).</summary>
        public static IReadOnlyList<string> Comic(CaseQuestion question, IReadOnlyList<string> stickers)
        {
            if (!IsCorrect(question, stickers))
            {
                throw new ArgumentException("The comic is drawn only from a right set.", nameof(stickers));
            }

            var ideaOf = Match(question, stickers);
            var panels = new string[question.SlotCount];
            for (int i = 0; i < stickers.Count; i++)
            {
                panels[ideaOf[i]] = stickers[i];
            }

            return panels;
        }

        /// <summary>
        /// A right set for a solved question when the child's own set is not known (a save migrated from before the
        /// deduction notebook): found stickers first, then each idea's first sticker.
        /// </summary>
        public static IReadOnlyList<string> CanonicalAnswer(CaseQuestion question, CaseState state)
        {
            var pool = new List<string>();
            foreach (var sticker in CaseNotebook.Found(state ?? CaseState.Empty, true))
            {
                if (question.Fits(sticker.Id))
                {
                    pool.Add(sticker.Id);
                }
            }

            foreach (var idea in question.Ideas)
            {
                foreach (string id in idea)
                {
                    if (!pool.Contains(id))
                    {
                        pool.Add(id);
                    }
                }
            }

            var ideaOf = Match(question, pool);
            var panels = new string[question.SlotCount];
            for (int i = 0; i < pool.Count; i++)
            {
                if (ideaOf[i] >= 0)
                {
                    panels[ideaOf[i]] = pool[i];
                }
            }

            return panels;
        }

        /// <summary>
        /// Pip's musing when stuck on <paramref name="question"/> (radio inside the notebook, §6): two pictures that
        /// reframe the question and, when a needed clue is still out in the world, that clue's PLACE (never which
        /// stickers go together). Null place when every needed clue is already in the tray.
        /// </summary>
        public static DeductionMusing Musing(CaseQuestion question, CaseState state)
        {
            if (question == null)
            {
                throw new ArgumentNullException(nameof(question));
            }

            state = state ?? CaseState.Empty;
            var found = new List<string>(CaseNotebook.FoundIds(state));
            int covered = Covered(question, found);
            ClueSticker missing = null;
            if (covered < question.SlotCount)
            {
                // The first findable clue (in idea order) that would let the found stickers cover one more idea.
                foreach (var idea in question.Ideas)
                {
                    foreach (string id in idea)
                    {
                        var sticker = CaseNotebook.Sticker(id);
                        if (sticker == null || !sticker.Outstanding(state))
                        {
                            continue;
                        }

                        var with = new List<string>(found) { id };
                        if (Covered(question, with) > covered)
                        {
                            missing = sticker;
                            break;
                        }
                    }

                    if (missing != null)
                    {
                        break;
                    }
                }
            }

            return new DeductionMusing(question.MusingParts, missing?.Id, missing?.PlaceAnchor);
        }

        private static bool Contains(IReadOnlyList<string> list, string id)
        {
            foreach (string s in list)
            {
                if (s == id)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>Pip's musing and, if a needed clue is still out there, where it is (the place, not the clue's partner).</summary>
    public sealed class DeductionMusing
    {
        internal DeductionMusing(IReadOnlyList<string> parts, string missingSticker, string missingPlace)
        {
            Parts = parts;
            MissingStickerId = missingSticker;
            MissingPlaceAnchor = missingPlace;
        }

        /// <summary>The two pictures in Pip's thought cloud (puppet part IDs).</summary>
        public IReadOnlyList<string> Parts { get; }

        /// <summary>The unfound clue whose "?" outline lights (for the view only; its picture stays hidden).</summary>
        public string MissingStickerId { get; }

        /// <summary>Where that clue is (a <see cref="CaseAnchors"/> ID shown as a place badge), or null.</summary>
        public string MissingPlaceAnchor { get; }

        public bool PointsToPlace => MissingPlaceAnchor != null;
    }

    public enum DeductionVerdict
    {
        /// <summary>A slot is still empty: nothing to check.</summary>
        NotFull,

        /// <summary>Wrong: the stickers wobble home (or, from the third try, only those that do not fit).</summary>
        Wrong,

        /// <summary>Right: glow, the comic, the solved fact.</summary>
        Right,
    }

    /// <summary>The outcome of checking a full board.</summary>
    public sealed class DeductionCheck
    {
        internal DeductionCheck(DeductionVerdict verdict, int tries, IReadOnlyList<int> returned, IReadOnlyList<int> kept,
            bool decoyAside, IReadOnlyList<string> placed, IReadOnlyList<string> comic)
        {
            Verdict = verdict;
            Tries = tries;
            ReturnedSlots = returned;
            KeptSlots = kept;
            DecoySetAside = decoyAside;
            Placed = placed;
            Comic = comic;
        }

        public DeductionVerdict Verdict { get; }

        /// <summary>Wrong tries so far on this question (in memory only, never shown, never saved).</summary>
        public int Tries { get; }

        /// <summary>Slots whose sticker wobbles home, in slot order.</summary>
        public IReadOnlyList<int> ReturnedSlots { get; }

        /// <summary>Slots whose sticker fits and stays with a pencil tick (from the third try).</summary>
        public IReadOnlyList<int> KeptSlots { get; }

        /// <summary>This check set the decoy aside (pencil circle in the tray).</summary>
        public bool DecoySetAside { get; }

        /// <summary>The stickers as placed when checked (slot order).</summary>
        public IReadOnlyList<string> Placed { get; }

        /// <summary>The answer comic (the child's stickers in idea order); null unless right.</summary>
        public IReadOnlyList<string> Comic { get; }
    }

    /// <summary>
    /// One question's slots while the child works on it (memory only, D26 §5.5: no penalty, no counter, nothing
    /// saved). Placing never checks by itself; the view calls <see cref="Check"/> after the 0.4 s pause on a full set.
    /// </summary>
    public sealed class DeductionBoard
    {
        private readonly string[] _slots;
        private readonly bool[] _kept;

        public DeductionBoard(CaseQuestion question)
        {
            Question = question ?? throw new ArgumentNullException(nameof(question));
            _slots = new string[question.SlotCount];
            _kept = new bool[question.SlotCount];
        }

        public CaseQuestion Question { get; }

        public IReadOnlyList<string> Slots => _slots;

        public int SlotCount => _slots.Length;

        /// <summary>Wrong tries so far.</summary>
        public int Tries { get; private set; }

        /// <summary>The decoy has been set aside for this question: it cannot be placed here any more.</summary>
        public bool DecoyAside { get; private set; }

        public bool IsFull
        {
            get
            {
                foreach (string s in _slots)
                {
                    if (s == null)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        public bool IsEmpty
        {
            get
            {
                foreach (string s in _slots)
                {
                    if (s != null)
                    {
                        return false;
                    }
                }

                return true;
            }
        }

        /// <summary>The slot holding <paramref name="stickerId"/>, or -1 (then it is in the tray).</summary>
        public int SlotOf(string stickerId)
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == stickerId)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>The slot's sticker stayed after a wrong try because it fits (pencil tick).</summary>
        public bool IsKept(int slot) => slot >= 0 && slot < _kept.Length && _kept[slot];

        /// <summary>The first empty slot, or -1.</summary>
        public int FirstEmpty()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                if (_slots[i] == null)
                {
                    return i;
                }
            }

            return -1;
        }

        /// <summary>
        /// Presses <paramref name="stickerId"/> into <paramref name="slot"/>. A sticker already in another slot moves;
        /// whatever the slot held goes home to the tray. Refused for a set-aside decoy or a sticker not found.
        /// </summary>
        public bool Place(int slot, string stickerId, ICollection<string> found)
        {
            if (slot < 0 || slot >= _slots.Length || stickerId == null || found == null || !found.Contains(stickerId))
            {
                return false;
            }

            if (DecoyAside && Question.IsDecoy(stickerId))
            {
                return false;
            }

            int from = SlotOf(stickerId);
            if (from == slot)
            {
                return true;
            }

            if (from >= 0)
            {
                _slots[from] = null;
                _kept[from] = false;
            }

            _slots[slot] = stickerId;
            _kept[slot] = false;
            return true;
        }

        /// <summary>Sends the slot's sticker home (tap a placed sticker).</summary>
        public string Remove(int slot)
        {
            if (slot < 0 || slot >= _slots.Length)
            {
                return null;
            }

            string id = _slots[slot];
            _slots[slot] = null;
            _kept[slot] = false;
            return id;
        }

        /// <summary>Empties every slot (the stickers go home; tries are kept).</summary>
        public void Clear()
        {
            for (int i = 0; i < _slots.Length; i++)
            {
                _slots[i] = null;
                _kept[i] = false;
            }
        }

        /// <summary>
        /// Checks a full board (§5.3, §5.4). Wrong: tries + 1; tries 1–2 send every sticker home; from try 3 the
        /// stickers that fit stay (ticked); after try 5, with every needed clue found, the decoy is set aside. Right:
        /// the comic from the child's stickers; the stickers go home (they stay usable for other questions).
        /// </summary>
        public DeductionCheck Check(IEnumerable<string> found)
        {
            var placed = (string[])_slots.Clone();
            if (!IsFull)
            {
                return new DeductionCheck(DeductionVerdict.NotFull, Tries, Array.Empty<int>(), Array.Empty<int>(), false,
                    placed, null);
            }

            if (CaseDeduction.IsCorrect(Question, placed))
            {
                var comic = CaseDeduction.Comic(Question, placed);
                Clear();
                Tries = 0;
                return new DeductionCheck(DeductionVerdict.Right, 0, Array.Empty<int>(), Array.Empty<int>(), false, placed,
                    comic);
            }

            Tries++;
            var returned = new List<int>(_slots.Length);
            var kept = new List<int>(_slots.Length);
            var ideaOf = Tries >= CaseDeduction.KeepFittingFromTry ? CaseDeduction.Match(Question, placed) : null;
            for (int i = 0; i < _slots.Length; i++)
            {
                if (ideaOf != null && ideaOf[i] >= 0)
                {
                    kept.Add(i);
                    _kept[i] = true;
                }
                else
                {
                    returned.Add(i);
                    _slots[i] = null;
                    _kept[i] = false;
                }
            }

            bool aside = false;
            if (!DecoyAside && Question.Decoys.Count > 0 && Tries >= CaseDeduction.DecoyAsideAfterTries &&
                CaseDeduction.CanSolve(Question, found))
            {
                DecoyAside = true;
                aside = true;
            }

            return new DeductionCheck(DeductionVerdict.Wrong, Tries, returned, kept, aside, placed, null);
        }
    }

    /// <summary>The boards of every question for this play session (memory only; a new case starts fresh).</summary>
    public sealed class DeductionSession
    {
        private readonly Dictionary<string, DeductionBoard> _boards = new Dictionary<string, DeductionBoard>(StringComparer.Ordinal);

        public DeductionBoard Board(CaseQuestion question)
        {
            if (question == null)
            {
                throw new ArgumentNullException(nameof(question));
            }

            if (!_boards.TryGetValue(question.Id, out var board))
            {
                board = new DeductionBoard(question);
                _boards.Add(question.Id, board);
            }

            return board;
        }

        public void Reset() => _boards.Clear();
    }

    /// <summary>
    /// The child's own right sets, per solved question (the comic is drawn from them). Saved beside the facts
    /// (case save schema 2) as "questionId=sticker+sticker"; display text never enters it. Immutable.
    /// </summary>
    public sealed class CaseAnswers
    {
        public static readonly CaseAnswers Empty = new CaseAnswers(new Dictionary<string, string[]>(StringComparer.Ordinal));

        private const char Equal = '=';
        private const char Plus = '+';
        private readonly Dictionary<string, string[]> _sets;

        private CaseAnswers(Dictionary<string, string[]> sets)
        {
            _sets = sets;
        }

        public int Count => _sets.Count;

        /// <summary>The set placed on <paramref name="questionId"/>, or null.</summary>
        public IReadOnlyList<string> Get(string questionId) =>
            questionId != null && _sets.TryGetValue(questionId, out var set) ? set : null;

        public CaseAnswers With(string questionId, IReadOnlyList<string> stickers)
        {
            var copy = new Dictionary<string, string[]>(_sets, StringComparer.Ordinal);
            var set = new string[stickers.Count];
            for (int i = 0; i < set.Length; i++)
            {
                set[i] = stickers[i];
            }

            copy[questionId] = set;
            return new CaseAnswers(copy);
        }

        /// <summary>Save strings, in case question order.</summary>
        public IReadOnlyList<string> ToSaveStrings()
        {
            var list = new List<string>(_sets.Count);
            foreach (var question in CaseNotebook.Questions)
            {
                if (_sets.TryGetValue(question.Id, out var set))
                {
                    list.Add(question.Id + Equal + string.Join(Plus.ToString(), set));
                }
            }

            return list;
        }

        /// <summary>
        /// Keeps only right sets for questions the state has solved; a solved question without a valid set gets a
        /// canonical one (a migrated save). <paramref name="issues"/> collects what was repaired.
        /// </summary>
        public static CaseAnswers FromSaveStrings(IEnumerable<string> raw, CaseState state, ICollection<string> issues)
        {
            var sets = new Dictionary<string, string[]>(StringComparer.Ordinal);
            if (raw != null)
            {
                foreach (string entry in raw)
                {
                    int eq = entry?.IndexOf(Equal) ?? -1;
                    var question = eq > 0 ? CaseNotebook.Question(entry.Substring(0, eq)) : null;
                    var set = eq > 0 ? entry.Substring(eq + 1).Split(Plus) : null;
                    if (question == null || !question.IsSolved(state) || !CaseDeduction.IsCorrect(question, set) ||
                        sets.ContainsKey(question.Id))
                    {
                        issues?.Add("dropped answer " + entry);
                        continue;
                    }

                    sets.Add(question.Id, set);
                }
            }

            foreach (var question in CaseNotebook.Questions)
            {
                if (question.IsSolved(state) && !sets.ContainsKey(question.Id))
                {
                    var canonical = CaseDeduction.CanonicalAnswer(question, state);
                    var set = new string[canonical.Count];
                    for (int i = 0; i < set.Length; i++)
                    {
                        set[i] = canonical[i];
                    }

                    sets.Add(question.Id, set);
                    issues?.Add("answer derived for " + question.Id);
                }
            }

            return new CaseAnswers(sets);
        }
    }
}
