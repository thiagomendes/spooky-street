using System;
using System.Collections.Generic;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Keepsakes;
using SpookyStreet.Core.Street;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.Parent;
using CaseKeys = SpookyStreet.Core.Localization.LocalizationKeys.CaseUi;

namespace SpookyStreet.Core.Parent
{
    /// <summary>How far the child is in a case, as the grown-ups' page says it.</summary>
    public enum ParentCaseStatus
    {
        NotStarted,
        InProgress,
        Solved,
    }

    /// <summary>A question the child solved: its picture chip, and the child's own answer comic.</summary>
    public sealed class ParentSolvedQuestion
    {
        internal ParentSolvedQuestion(CaseQuestion question, IReadOnlyList<string> comic)
        {
            QuestionId = question.Id;
            QuestionKey = question.QuestionKey;
            AnswerKey = question.AnswerKey;
            Pictures = question.MusingParts;
            ComicStickers = comic;
        }

        public string QuestionId { get; }

        public string QuestionKey { get; }

        /// <summary>The answer words (only ever for a solved question).</summary>
        public string AnswerKey { get; }

        /// <summary>The question's picture chip (puppet parts).</summary>
        public IReadOnlyList<string> Pictures { get; }

        /// <summary>The comic's stickers, in the child's order.</summary>
        public IReadOnlyList<string> ComicStickers { get; }
    }

    /// <summary>A place (or a friend) on the page: a stable id, its name key and its picture.</summary>
    public sealed class ParentChip
    {
        internal ParentChip(string id, string labelKey, string picture)
        {
            Id = id;
            LabelKey = labelKey;
            Picture = picture;
        }

        public string Id { get; }

        public string LabelKey { get; }

        /// <summary>A puppet part (places) or a portrait sprite id (friends).</summary>
        public string Picture { get; }

        public override string ToString() => Id;
    }

    /// <summary>
    /// Everything the "For grown-ups" page shows (S004 §2.5), computed on the device from the local saves each time
    /// it opens: the case save (facts and the child's placed answers), the street file, the keepsakes file and the
    /// playtime ledger. Nothing is stored here and nothing leaves the device. It never carries the answer of a
    /// question the child has not solved, and wrong notebook tries are never stored, so never shown. This is the only
    /// place in the game that shows keepsake totals ("17 of 30").
    /// </summary>
    public sealed class ParentProgressPage
    {
        public string CaseNameKey { get; internal set; }

        public ParentCaseStatus CaseStatus { get; internal set; }

        public string CaseStatusKey { get; internal set; }

        /// <summary>The solved questions in case order (their chips and the child's comics).</summary>
        public IReadOnlyList<ParentSolvedQuestion> Solved { get; internal set; }

        public IReadOnlyList<ParentChip> Places { get; internal set; }

        public IReadOnlyList<ParentChip> Friends { get; internal set; }

        public int BatsFound { get; internal set; }

        public int BatsTotal { get; internal set; }

        public int PagesEarned { get; internal set; }

        public int PagesTotal { get; internal set; }

        public int PiecesEarned { get; internal set; }

        public int PiecesTotal { get; internal set; }

        public PlaytimeWeek Week { get; internal set; }

        /// <summary>What the child practises (three fixed lines).</summary>
        public IReadOnlyList<string> PractiseKeys => ParentProgress.Practises;

        /// <summary>What the game does with data (fixed lines).</summary>
        public IReadOnlyList<string> PrivacyKeys => ParentProgress.Privacy;
    }

    public static class ParentProgress
    {
        public const string PlaceLane = "place.lantern_lane";
        public const string PlaceHouse = "place.owl_house_inside";
        public const string PlaceTreehouse = "place.treehouse";

        public static readonly IReadOnlyList<string> Practises = new[] { Keys.PractiseNoticing, Keys.PractiseComparing, Keys.PractiseExplaining };

        public static readonly IReadOnlyList<string> Privacy = new[] { Keys.PrivacyNoAds, Keys.PrivacyOnDevice, Keys.PrivacyLanguages };

        private static readonly (string Id, string Key, string Picture, CaseFact[] Facts)[] PlaceTable =
        {
            (PlaceLane, Keys.PlaceLane, "pg.lamp", null), // any progress at all: the case starts on the lane
            (PlaceTreehouse, Keys.PlaceTreehouse, "pg.treehouse", new[] { CaseFact.ExaminedCloth, CaseFact.EnteredByWindow }),
            (PlaceHouse, Keys.PlaceHouse, "pg.house_cut",
                new[] { CaseFact.EnteredByWindow, CaseFact.FrontOpen, CaseFact.FloorHum, CaseFact.SawCam }),
        };

        private static readonly (Speaker Speaker, string Key, CaseFact Fact)[] FriendTable =
        {
            (Speaker.Bea, CaseKeys.SpeakerBea, CaseFact.BeaSpoke),
            (Speaker.Vale, CaseKeys.SpeakerVale, CaseFact.ValeSpoke),
            (Speaker.June, CaseKeys.SpeakerJune, CaseFact.FoundJune),
            (Speaker.Kit, CaseKeys.SpeakerKit, CaseFact.CaseClosed),
        };

        /// <summary>The portrait shown for a friend on the page (their happy face).</summary>
        public static string PortraitOf(Speaker speaker) => "portrait." + speaker.ToString().ToLowerInvariant() + ".happy";

        public static ParentProgressPage Build(CaseState state, CaseAnswers answers, StreetState street,
            KeepsakeState keepsakes, PlaytimeLedger playtime, DateTime localNow)
        {
            state = state ?? CaseState.Empty;
            street = street ?? StreetState.Empty;
            keepsakes = keepsakes ?? KeepsakeState.Empty;
            playtime = playtime ?? new PlaytimeLedger();

            var page = new ParentProgressPage { CaseNameKey = Localization.LocalizationKeys.TitleCaseName };
            bool solved = StreetRules.Solved(state) || street.HasSolved(CaseDefinition.CaseId);
            page.CaseStatus = solved ? ParentCaseStatus.Solved
                : state.Count > 0 ? ParentCaseStatus.InProgress : ParentCaseStatus.NotStarted;
            page.CaseStatusKey = page.CaseStatus == ParentCaseStatus.Solved ? Keys.CaseSolved
                : page.CaseStatus == ParentCaseStatus.InProgress ? Keys.CaseInProgress : Keys.CaseNotStarted;

            // The notebook's own page model: a comic exists only for a question the child solved.
            var notebook = CaseNotebook.Build(state, answers);
            var solvedQuestions = new List<ParentSolvedQuestion>();
            foreach (var question in notebook.Questions)
            {
                if (question.Solved && question.AnswerStickers != null)
                {
                    solvedQuestions.Add(new ParentSolvedQuestion(question.Definition, question.AnswerStickers));
                }
            }

            page.Solved = solvedQuestions;

            var places = new List<ParentChip>();
            foreach (var row in PlaceTable)
            {
                bool been = row.Facts == null ? state.Count > 0 : AnyOf(state, row.Facts);
                if (been)
                {
                    places.Add(new ParentChip(row.Id, row.Key, row.Picture));
                }
            }

            page.Places = places;

            var friends = new List<ParentChip>();
            foreach (var row in FriendTable)
            {
                if (state.Has(row.Fact))
                {
                    friends.Add(new ParentChip("friend." + row.Speaker.ToString().ToLowerInvariant(), row.Key,
                        PortraitOf(row.Speaker)));
                }
            }

            page.Friends = friends;

            page.BatsFound = keepsakes.BatCount;
            page.BatsTotal = KeepsakeRules.TotalBats;
            page.PagesEarned = keepsakes.Pages.Count;
            page.PagesTotal = SketchPages.All.Count;
            page.PiecesEarned = keepsakes.EarnedPieces.Count;
            page.PiecesTotal = CostumePieces.All.Count;
            page.Week = playtime.Week(localNow);
            return page;
        }

        private static bool AnyOf(CaseState state, CaseFact[] facts)
        {
            foreach (var fact in facts)
            {
                if (state.Has(fact))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
