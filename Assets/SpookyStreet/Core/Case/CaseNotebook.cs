using System;
using System.Collections.Generic;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.Notebook;

namespace SpookyStreet.Core.Case
{
    /// <summary>
    /// One clue sticker (D26, docs/mystery-first.md §3): a perception the child earned by looking, listening or
    /// talking. Stickers live in one tray shared by every question; none of them says which question it belongs to.
    /// </summary>
    public sealed class ClueSticker
    {
        internal ClueSticker(string id, CaseFact fact, string placeAnchor, string noteKey, string tagKey, bool inTray = true,
            CaseFact? lostWhen = null)
        {
            Id = id;
            Fact = fact;
            PlaceAnchor = placeAnchor;
            NoteKey = noteKey;
            TagKey = tagKey;
            InTray = inTray;
            LostWhen = lostWhen;
        }

        /// <summary>Stable saved ID, e.g. "c01.sticker.cloth" (answers and the notebook field store these).</summary>
        public string Id { get; }

        /// <summary>The fact that earns the sticker. Nothing else can.</summary>
        public CaseFact Fact { get; }

        /// <summary>Where the clue is found (a <see cref="CaseAnchors"/> ID): the "?" outline's place badge.</summary>
        public string PlaceAnchor { get; }

        /// <summary>What the child perceived (shown on tap). Never a conclusion.</summary>
        public string NoteKey { get; }

        /// <summary>Short name for readers (final card, accessibility).</summary>
        public string TagKey { get; }

        /// <summary>False for keepsakes that never go in the tray (the saved sweet): they only show on the final card.</summary>
        public bool InTray { get; }

        /// <summary>A fact after which the clue can no longer be found (the stuck door once it is open).</summary>
        public CaseFact? LostWhen { get; }

        public bool Found(CaseState state) => state != null && state.Has(Fact);

        /// <summary>Not found yet and still out in the world (counts as a "?" outline in the tray).</summary>
        public bool Outstanding(CaseState state) =>
            InTray && !Found(state) && !(LostWhen.HasValue && state != null && state.Has(LostWhen.Value));
    }

    /// <summary>
    /// One case question (D26): appears when the world raises it, has one slot per idea, and is answered only by a
    /// set of stickers that covers every idea with a different sticker. Decoys belong to no idea.
    /// </summary>
    public sealed class CaseQuestion
    {
        internal CaseQuestion(string id, string questionKey, string answerKey, CaseFact solvedFact, CaseFact[] raisedBy,
            string[][] ideas, string[] decoys, string[] musing)
        {
            Id = id;
            QuestionKey = questionKey;
            AnswerKey = answerKey;
            SolvedFact = solvedFact;
            RaisedBy = raisedBy;
            var list = new List<IReadOnlyList<string>>(ideas.Length);
            foreach (var idea in ideas)
            {
                list.Add(idea);
            }

            Ideas = list;
            Decoys = decoys ?? Array.Empty<string>();
            MusingParts = musing;
        }

        /// <summary>Stable ID, e.g. "c01.note.q_entry" (kept from S002).</summary>
        public string Id { get; }

        public string QuestionKey { get; }

        /// <summary>The answer words, shown only once the child has solved it.</summary>
        public string AnswerKey { get; }

        /// <summary>Written only by a right set of stickers (<see cref="CaseActionKind.Solve"/>).</summary>
        public CaseFact SolvedFact { get; }

        /// <summary>Any of these facts raises the question (the world asks it).</summary>
        public IReadOnlyList<CaseFact> RaisedBy { get; }

        /// <summary>Each idea lists the stickers that can stand for it. One slot per idea.</summary>
        public IReadOnlyList<IReadOnlyList<string>> Ideas { get; }

        public int SlotCount => Ideas.Count;

        /// <summary>The honest decoy for this question (set aside after many tries).</summary>
        public IReadOnlyList<string> Decoys { get; }

        /// <summary>Pip's musing when stuck: two pictures that reframe the question, never a pairing.</summary>
        public IReadOnlyList<string> MusingParts { get; }

        public bool IsRaised(CaseState state)
        {
            if (state == null)
            {
                return false;
            }

            foreach (var fact in RaisedBy)
            {
                if (state.Has(fact))
                {
                    return true;
                }
            }

            return state.Has(SolvedFact);
        }

        public bool IsSolved(CaseState state) => state != null && state.Has(SolvedFact);

        /// <summary>True when <paramref name="stickerId"/> can stand for at least one idea.</summary>
        public bool Fits(string stickerId)
        {
            foreach (var idea in Ideas)
            {
                foreach (string s in idea)
                {
                    if (s == stickerId)
                    {
                        return true;
                    }
                }
            }

            return false;
        }

        public bool IsDecoy(string stickerId)
        {
            foreach (string s in Decoys)
            {
                if (s == stickerId)
                {
                    return true;
                }
            }

            return false;
        }
    }

    /// <summary>What a question page shows.</summary>
    public sealed class NotebookQuestion
    {
        internal NotebookQuestion(CaseQuestion definition, bool solved, IReadOnlyList<string> answer)
        {
            Definition = definition;
            Solved = solved;
            AnswerStickers = answer;
        }

        public CaseQuestion Definition { get; }

        public string QuestionKey => Definition.QuestionKey;

        public bool Solved { get; }

        /// <summary>The answer words, or null while unsolved (the page never shows an answer before the child solves).</summary>
        public string AnswerKey => Solved ? Definition.AnswerKey : null;

        /// <summary>The comic's stickers (the child's own, in idea order), or null while unsolved.</summary>
        public IReadOnlyList<string> AnswerStickers { get; }
    }

    /// <summary>The final card: the player's actual discoveries in the order they made them.</summary>
    public sealed class NotebookFinalCard
    {
        internal NotebookFinalCard(IReadOnlyList<ClueSticker> discoveries, int total)
        {
            Discoveries = discoveries;
            Total = total;
        }

        public string HeadingKey => Keys.FinalHeading;

        public string StampKey => Keys.CaseClosedStamp;

        public IReadOnlyList<ClueSticker> Discoveries { get; }

        public int Found => Discoveries.Count;

        public int Total { get; }

        /// <summary>Format with (Found, Total).</summary>
        public string CountKey => Keys.CluesCount;

        /// <summary>Quiet primary action.</summary>
        public string BackToStreetKey => Keys.BackToStreet;

        /// <summary>Separate action that must be confirmed (<see cref="ReplayConfirmKey"/>).</summary>
        public string ReplayKey => Keys.Replay;

        public string ReplayConfirmKey => Keys.ReplayConfirm;
    }

    /// <summary>The notebook for a state: the raised questions, the shared tray and, once stamped, the final card.</summary>
    public sealed class NotebookPage
    {
        internal NotebookPage(IReadOnlyList<NotebookQuestion> questions, IReadOnlyList<ClueSticker> tray, int outstanding,
            NotebookFinalCard finalCard)
        {
            Questions = questions;
            Tray = tray;
            OutstandingCount = outstanding;
            FinalCard = finalCard;
        }

        /// <summary>Only the questions the world has raised, in case order.</summary>
        public IReadOnlyList<NotebookQuestion> Questions { get; }

        /// <summary>Every found sticker, in the order found (shared by all questions).</summary>
        public IReadOnlyList<ClueSticker> Tray { get; }

        /// <summary>How many clues are still out in the world (one generic "?" outline each; count only).</summary>
        public int OutstandingCount { get; }

        /// <summary>Null until the case is stamped (the three questions solved after the reopening).</summary>
        public NotebookFinalCard FinalCard { get; }
    }

    /// <summary>
    /// Case 01's notebook (D26, docs/mystery-first.md §3, §5): the clue stickers, the three questions with their
    /// ideas, and the page model. Questions never answer themselves: an answer exists only after the child places a
    /// right set (<see cref="CaseDeduction"/>), which the reducer records as a solved fact. Pure.
    /// </summary>
    public static class CaseNotebook
    {
        public const string QuestionEntry = "c01.note.q_entry";
        public const string QuestionSource = "c01.note.q_source";
        public const string QuestionMoved = "c01.note.q_moved";

        public const string BeaFlash = "c01.sticker.bea_flash";
        public const string ValeDoor = "c01.sticker.vale_door";
        public const string BenchView = "c01.sticker.bench_view";
        public const string StuckDoor = "c01.sticker.stuck_door";
        public const string Streamer = "c01.sticker.streamer";
        public const string Cloth = "c01.sticker.cloth";
        public const string SillSnag = "c01.sticker.sill";
        public const string FloorHum = "c01.sticker.floor_hum";
        public const string WoodenPiece = "c01.sticker.wooden_piece";
        public const string OwlPanel = "c01.sticker.owl_panel";
        public const string OwlBooth = "c01.sticker.owl_booth";
        public const string LooseArm = "c01.sticker.loose_arm";
        public const string Sweet = "c01.sticker.sweet";

        /// <summary>Every sticker, in case order. The tray shows found ones in the order the child found them.</summary>
        public static readonly IReadOnlyList<ClueSticker> Stickers = new[]
        {
            new ClueSticker(BeaFlash, CaseFact.BeaSpoke, CaseAnchors.Plaza, Keys.NoteBea, TagKeys.Bea),
            new ClueSticker(ValeDoor, CaseFact.ValeSpoke, CaseAnchors.Bench, Keys.NoteVale, TagKeys.Vale),
            new ClueSticker(BenchView, CaseFact.CheckedView, CaseAnchors.Bench, Keys.NoteBench, TagKeys.Bench),
            new ClueSticker(StuckDoor, CaseFact.TriedDoor, CaseAnchors.ArrivalPorch, Keys.NoteDoor, TagKeys.Door,
                lostWhen: CaseFact.FrontOpen),
            new ClueSticker(Streamer, CaseFact.ExaminedStreamer, CaseAnchors.BicycleCorner, Keys.NoteStreamer, TagKeys.Streamer),
            new ClueSticker(Cloth, CaseFact.ExaminedCloth, CaseAnchors.Treehouse, Keys.NoteCloth, TagKeys.Cloth),
            new ClueSticker(SillSnag, CaseFact.ExaminedSill, CaseAnchors.AtticSill, Keys.NoteSill, TagKeys.Sill),
            new ClueSticker(FloorHum, CaseFact.FloorHum, CaseAnchors.Foyer, Keys.NoteHum, TagKeys.Hum),
            new ClueSticker(WoodenPiece, CaseFact.SawCam, CaseAnchors.Workshop, Keys.NotePiece, TagKeys.Piece),
            new ClueSticker(OwlPanel, CaseFact.OwlPanel, CaseAnchors.Workshop, Keys.NoteOwlPanel, TagKeys.OwlPanel),
            new ClueSticker(OwlBooth, CaseFact.OwlBooth, CaseAnchors.BluePorch, Keys.NoteOwlBooth, TagKeys.OwlBooth),
            new ClueSticker(LooseArm, CaseFact.LooseArm, CaseAnchors.Workshop, Keys.NoteArm, TagKeys.Arm),
            new ClueSticker(Sweet, CaseFact.FoundSweet, CaseAnchors.Bench, Keys.NoteSweet, TagKeys.Sweet, inTray: false),
        };

        /// <summary>The three questions in case order (docs/mystery-first.md §3).</summary>
        public static readonly IReadOnlyList<CaseQuestion> Questions = new[]
        {
            // Raised by the arrival knocks. Floor hum + wooden piece lifting.
            new CaseQuestion(QuestionSource, Keys.QuestionSource, Keys.AnswerSource, CaseFact.SolvedSource,
                new[] { CaseFact.HeardKnocks, CaseFact.FloorHum, CaseFact.SawCam },
                new[] { new[] { FloorHum }, new[] { WoodenPiece } },
                null, new[] { "pg.ear", "pg.eye" }),

            // Raised once Bea or Vale has spoken (or June, for a child who climbed in first). Not the front door;
            // silver up high; the hidden side. The foil streamer is the honest decoy.
            new CaseQuestion(QuestionEntry, Keys.QuestionEntry, Keys.AnswerEntry, CaseFact.SolvedEntry,
                new[] { CaseFact.BeaSpoke, CaseFact.ValeSpoke, CaseFact.FoundJune },
                new[]
                {
                    new[] { ValeDoor, StuckDoor },
                    new[] { Cloth, SillSnag },
                    new[] { BeaFlash, BenchView, Cloth, SillSnag },
                },
                new[] { Streamer }, new[] { "pg.door_shut", "pg.window" }),

            // Raised in the workshop (the wooden piece, the owl panel or the loose arm). An owl + the loose arm.
            new CaseQuestion(QuestionMoved, Keys.QuestionMoved, Keys.AnswerMoved, CaseFact.SolvedMoved,
                new[] { CaseFact.SawCam, CaseFact.OwlPanel, CaseFact.LooseArm },
                new[] { new[] { OwlPanel, OwlBooth }, new[] { LooseArm } },
                null, new[] { "pg.owl", "pg.arm" }),
        };

        public static bool IsQuestion(string id) => Question(id) != null;

        public static CaseQuestion Question(string id)
        {
            foreach (var question in Questions)
            {
                if (question.Id == id)
                {
                    return question;
                }
            }

            return null;
        }

        public static ClueSticker Sticker(string id)
        {
            foreach (var sticker in Stickers)
            {
                if (sticker.Id == id)
                {
                    return sticker;
                }
            }

            return null;
        }

        /// <summary>The sticker a fact earns, or null.</summary>
        public static ClueSticker StickerFor(CaseFact fact)
        {
            foreach (var sticker in Stickers)
            {
                if (sticker.Fact == fact)
                {
                    return sticker;
                }
            }

            return null;
        }

        /// <summary>A sticker or a question ID (the save's notebook field).</summary>
        public static bool IsKnownEntry(string id) => Sticker(id) != null || IsQuestion(id);

        /// <summary>Questions the world has raised, in case order.</summary>
        public static IReadOnlyList<CaseQuestion> Raised(CaseState state)
        {
            var list = new List<CaseQuestion>(Questions.Count);
            foreach (var question in Questions)
            {
                if (question.IsRaised(state))
                {
                    list.Add(question);
                }
            }

            return list;
        }

        /// <summary>Found stickers in the order the child found them.</summary>
        public static IReadOnlyList<ClueSticker> Found(CaseState state, bool trayOnly)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var found = new List<ClueSticker>();
            foreach (var fact in state.Facts)
            {
                var sticker = StickerFor(fact);
                if (sticker != null && (!trayOnly || sticker.InTray))
                {
                    found.Add(sticker);
                }
            }

            return found;
        }

        /// <summary>IDs of the found tray stickers.</summary>
        public static HashSet<string> FoundIds(CaseState state)
        {
            var ids = new HashSet<string>(StringComparer.Ordinal);
            foreach (var sticker in Found(state, true))
            {
                ids.Add(sticker.Id);
            }

            return ids;
        }

        /// <summary>Clues still out in the world (tray stickers not found and still findable).</summary>
        public static IReadOnlyList<ClueSticker> Outstanding(CaseState state)
        {
            var list = new List<ClueSticker>();
            foreach (var sticker in Stickers)
            {
                if (sticker.Outstanding(state))
                {
                    list.Add(sticker);
                }
            }

            return list;
        }

        /// <summary>
        /// Entry IDs the facts have written (the save's notebook field): found stickers in discovery order, then the
        /// solved questions. Derived, so the save can always be checked against the facts.
        /// </summary>
        public static IReadOnlyList<string> DiscoveredEntryIds(CaseState state)
        {
            var ids = new List<string>();
            foreach (var sticker in Found(state, false))
            {
                ids.Add(sticker.Id);
            }

            foreach (var question in Questions)
            {
                if (question.IsSolved(state))
                {
                    ids.Add(question.Id);
                }
            }

            return ids;
        }

        public static NotebookPage Build(CaseState state, CaseAnswers answers = null)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var questions = new List<NotebookQuestion>(Questions.Count);
            foreach (var question in Raised(state))
            {
                bool solved = question.IsSolved(state);
                IReadOnlyList<string> answer = null;
                if (solved)
                {
                    var saved = answers?.Get(question.Id);
                    answer = saved != null && CaseDeduction.IsCorrect(question, saved)
                        ? CaseDeduction.Comic(question, saved)
                        : CaseDeduction.CanonicalAnswer(question, state);
                }

                questions.Add(new NotebookQuestion(question, solved, answer));
            }

            var finalCard = state.Has(CaseFact.CaseStamped)
                ? new NotebookFinalCard(Found(state, false), Stickers.Count)
                : null;
            return new NotebookPage(questions, Found(state, true), Outstanding(state).Count, finalCard);
        }

        /// <summary>True when no clue is left out in the world (used by the post-closure hint).</summary>
        public static bool AllFound(CaseState state) => Outstanding(state).Count == 0 && state.Has(CaseFact.FoundSweet);

        /// <summary>The first raised, unsolved question (the Continue detail and Kit's question), or null.</summary>
        public static CaseQuestion FirstOpen(CaseState state)
        {
            foreach (var question in Raised(state))
            {
                if (!question.IsSolved(state))
                {
                    return question;
                }
            }

            return null;
        }

        private static class TagKeys
        {
            public const string Bea = Localization.LocalizationKeys.CaseUi.TagBea;
            public const string Vale = Localization.LocalizationKeys.CaseUi.TagVale;
            public const string Bench = Localization.LocalizationKeys.CaseUi.TagBench;
            public const string Door = Localization.LocalizationKeys.CaseUi.TagDoor;
            public const string Streamer = Localization.LocalizationKeys.CaseUi.TagStreamer;
            public const string Cloth = Localization.LocalizationKeys.CaseUi.TagCloth;
            public const string Sill = Localization.LocalizationKeys.CaseUi.TagSill;
            public const string Hum = Localization.LocalizationKeys.CaseUi.TagHum;
            public const string Piece = Localization.LocalizationKeys.CaseUi.TagPiece;
            public const string OwlPanel = Localization.LocalizationKeys.CaseUi.TagOwlPanel;
            public const string OwlBooth = Localization.LocalizationKeys.CaseUi.TagOwlBooth;
            public const string Arm = Localization.LocalizationKeys.CaseUi.TagArm;
            public const string Sweet = Localization.LocalizationKeys.CaseUi.TagSweet;
        }
    }
}
