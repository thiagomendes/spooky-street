using System;
using System.Collections.Generic;
using SpookyStreet.Core.Case;
using RecapKeys = SpookyStreet.Core.Localization.LocalizationKeys.Recap;

namespace SpookyStreet.Core.Save
{
    /// <summary>
    /// The picture recap on the title's Continue (engagement §1.10, spec §5): where Pip was, the open question as its
    /// picture chips with its words (the hybrid text line, D23.1), the stickers found so far and the soft chapter the
    /// child is in, so a 6-year-old resumes without reading. Built only from the save. It never carries an answer: no
    /// answer words, no comic, no right set, no hint and no route (D26); a solved question only moves the chapter on.
    /// </summary>
    public sealed class ContinueRecap
    {
        /// <summary>Soft chapter stops (engagement §1.10, owner answer 4a): the witnesses, then one per solved question.</summary>
        public const int ChapterCount = 4;

        /// <summary>At most this many tiny sticker pictures; the count carries the rest.</summary>
        public const int MaxStickerIcons = 4;

        private ContinueRecap(string placeAnchor, CaseQuestion open, IReadOnlyList<string> stickers, int total,
            bool closed, int chapter)
        {
            PlaceAnchor = placeAnchor;
            OpenQuestionId = open?.Id;
            QuestionKey = open?.QuestionKey;
            Chips = open != null ? QuestionChips(open.Id) : Array.Empty<string>();
            Stickers = stickers;
            TotalStickers = total;
            CaseClosed = closed;
            Chapter = chapter;
        }

        /// <summary>The safe anchor Continue puts Pip at (a <see cref="CaseAnchors"/> ID, validated).</summary>
        public string PlaceAnchor { get; }

        /// <summary>The first raised, unsolved question, or null (none raised yet, all answered, or the case is closed).</summary>
        public string OpenQuestionId { get; }

        /// <summary>The open question's words (the text line), or null.</summary>
        public string QuestionKey { get; }

        /// <summary>The open question as puppet-atlas chips (the notebook page's own pictures); empty without one.</summary>
        public IReadOnlyList<string> Chips { get; }

        /// <summary>Found tray stickers, in the order the child found them (never which question they answer).</summary>
        public IReadOnlyList<string> Stickers { get; }

        public int StickerCount => Stickers.Count;

        public int TotalStickers { get; }

        /// <summary>The "Case closed" stamp is on the notebook: the recap shows the stamp instead of a question.</summary>
        public bool CaseClosed { get; }

        /// <summary>1..<see cref="ChapterCount"/>: 1 before any answer, +1 per solved question.</summary>
        public int Chapter { get; }

        /// <summary>"Last time": the card's small heading.</summary>
        public string HeadingKey => RecapKeys.Heading;

        /// <summary>The words under the pictures: the open question, or the stamp once closed; null when neither.</summary>
        public string LineKey => CaseClosed ? Localization.LocalizationKeys.Notebook.CaseClosedStamp : QuestionKey;

        /// <summary>Format with (count) for the sticker row.</summary>
        public string StickersKey => RecapKeys.Stickers;

        public static ContinueRecap Build(CaseState state, string anchorId)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            bool closed = state.Has(CaseFact.CaseStamped);
            var stickers = new List<string>();
            foreach (var sticker in CaseNotebook.Found(state, true))
            {
                stickers.Add(sticker.Id);
            }

            int total = 0;
            foreach (var sticker in CaseNotebook.Stickers)
            {
                total += sticker.InTray ? 1 : 0;
            }

            return new ContinueRecap(CaseAnchors.Resolve(anchorId, state), closed ? null : CaseNotebook.FirstOpen(state),
                stickers, total, closed, ChapterOf(state));
        }

        /// <summary>The soft chapter of <paramref name="state"/>: 1 + solved questions, capped at <see cref="ChapterCount"/>.</summary>
        public static int ChapterOf(CaseState state)
        {
            int solved = 0;
            foreach (var question in CaseNotebook.Questions)
            {
                solved += question.IsSolved(state) ? 1 : 0;
            }

            return Math.Min(ChapterCount, 1 + solved);
        }

        /// <summary>
        /// A question as picture chips (the notebook page and the recap share them): who or what it is about, a "?",
        /// and the place. Chips name the question, never its answer.
        /// </summary>
        public static string[] QuestionChips(string questionId)
        {
            switch (questionId)
            {
                case CaseNotebook.QuestionEntry:
                    return new[] { "pg.june", "pg.question", "pg.house" };
                case CaseNotebook.QuestionSource:
                    return new[] { "pg.knock3", "pg.question", "pg.house" };
                default:
                    return new[] { "pg.knock_ring", "pg.question" };
            }
        }
    }
}
