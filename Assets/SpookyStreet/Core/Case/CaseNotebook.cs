using System;
using System.Collections.Generic;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.Notebook;

namespace SpookyStreet.Core.Case
{
    /// <summary>One notebook entry: a question's answer or a small optional-evidence annotation.</summary>
    public sealed class NotebookEntryDefinition
    {
        internal NotebookEntryDefinition(string id, CaseFact proof, string textKey, string questionId)
        {
            Id = id;
            Proof = proof;
            TextKey = textKey;
            QuestionId = questionId;
        }

        /// <summary>Stable saved ID, e.g. "c01.note.q_entry".</summary>
        public string Id { get; }

        /// <summary>The physical proof (fact) that writes this entry. Nothing else can.</summary>
        public CaseFact Proof { get; }

        /// <summary>Answer text, or the annotation's caption shown on tap.</summary>
        public string TextKey { get; }

        /// <summary>The question an annotation sits beside; null for an answer or a bonus annotation.</summary>
        public string QuestionId { get; }

        public bool IsAnswer => QuestionId == null && CaseNotebook.IsQuestion(Id);
    }

    public sealed class NotebookQuestionDefinition
    {
        internal NotebookQuestionDefinition(string id, string questionKey, string answerKey, CaseFact proof)
        {
            Id = id;
            QuestionKey = questionKey;
            AnswerKey = answerKey;
            Proof = proof;
        }

        public string Id { get; }

        public string QuestionKey { get; }

        public string AnswerKey { get; }

        public CaseFact Proof { get; }
    }

    /// <summary>What the notebook shows for one question.</summary>
    public sealed class NotebookQuestion
    {
        internal NotebookQuestion(NotebookQuestionDefinition definition, bool answered, IReadOnlyList<NotebookAnnotation> annotations)
        {
            Definition = definition;
            Answered = answered;
            Annotations = annotations;
        }

        public NotebookQuestionDefinition Definition { get; }

        public string QuestionKey => Definition.QuestionKey;

        /// <summary>False: draw a question mark and a silhouette (accessible name <c>ui.notebook.unknown</c>).</summary>
        public bool Answered { get; }

        /// <summary>The answer key, or null while unanswered.</summary>
        public string AnswerKey => Answered ? Definition.AnswerKey : null;

        public IReadOnlyList<NotebookAnnotation> Annotations { get; }
    }

    public sealed class NotebookAnnotation
    {
        internal NotebookAnnotation(NotebookEntryDefinition definition, bool found)
        {
            Definition = definition;
            Found = found;
        }

        public NotebookEntryDefinition Definition { get; }

        public string Id => Definition.Id;

        public bool Found { get; }

        /// <summary>Caption on tap, or null while not found (draw the silhouette).</summary>
        public string CaptionKey => Found ? Definition.TextKey : null;
    }

    /// <summary>The final card: the player's actual discoveries in the order they made them.</summary>
    public sealed class NotebookFinalCard
    {
        internal NotebookFinalCard(IReadOnlyList<NotebookEntryDefinition> discoveries, int total)
        {
            Discoveries = discoveries;
            Total = total;
        }

        public string HeadingKey => Keys.FinalHeading;

        public string StampKey => Keys.CaseClosedStamp;

        public IReadOnlyList<NotebookEntryDefinition> Discoveries { get; }

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

    /// <summary>The notebook spread for a state.</summary>
    public sealed class NotebookPage
    {
        internal NotebookPage(IReadOnlyList<NotebookQuestion> questions, IReadOnlyList<NotebookAnnotation> extras,
            NotebookFinalCard finalCard)
        {
            Questions = questions;
            Extras = extras;
            FinalCard = finalCard;
        }

        public IReadOnlyList<NotebookQuestion> Questions { get; }

        /// <summary>Bonus annotations that belong to no question.</summary>
        public IReadOnlyList<NotebookAnnotation> Extras { get; }

        /// <summary>Null until the case is closed.</summary>
        public NotebookFinalCard FinalCard { get; }
    }

    /// <summary>
    /// The notebook of case 01 (spec §3): three questions, each answered only by its physical proof, optional
    /// evidence as annotations, and a final card of the player's own discoveries. Pure.
    /// </summary>
    public static class CaseNotebook
    {
        public const string QuestionEntry = "c01.note.q_entry";
        public const string QuestionSource = "c01.note.q_source";
        public const string QuestionMoved = "c01.note.q_moved";

        public static readonly IReadOnlyList<NotebookQuestionDefinition> Questions = new[]
        {
            // How did June get inside? Proof: Pip entered the attic from outside.
            new NotebookQuestionDefinition(QuestionEntry, Keys.QuestionEntry, Keys.AnswerEntry, CaseFact.EnteredByWindow),
            // Where are the knocks coming from? Proof: Pip saw the cam through the grille.
            new NotebookQuestionDefinition(QuestionSource, Keys.QuestionSource, Keys.AnswerSource, CaseFact.SawCam),
            // What did the knocking move? Proof: Pip turned the linked crank and the owl bowed.
            new NotebookQuestionDefinition(QuestionMoved, Keys.QuestionMoved, Keys.AnswerMoved, CaseFact.TestedOwl),
        };

        /// <summary>Every entry the notebook can hold: the three answers then the annotations.</summary>
        public static readonly IReadOnlyList<NotebookEntryDefinition> Entries = new[]
        {
            new NotebookEntryDefinition(QuestionEntry, CaseFact.EnteredByWindow, Keys.AnswerEntry, null),
            new NotebookEntryDefinition(QuestionSource, CaseFact.SawCam, Keys.AnswerSource, null),
            new NotebookEntryDefinition(QuestionMoved, CaseFact.TestedOwl, Keys.AnswerMoved, null),
            new NotebookEntryDefinition("c01.note.knocks", CaseFact.HeardKnocks, Keys.NoteKnocks, QuestionSource),
            new NotebookEntryDefinition("c01.note.bea", CaseFact.BeaSpoke, Keys.NoteBea, QuestionEntry),
            new NotebookEntryDefinition("c01.note.vale", CaseFact.ValeSpoke, Keys.NoteVale, QuestionEntry),
            new NotebookEntryDefinition("c01.note.bench", CaseFact.CheckedView, Keys.NoteBench, QuestionEntry),
            new NotebookEntryDefinition("c01.note.streamer", CaseFact.ExaminedStreamer, Keys.NoteStreamer, QuestionEntry),
            new NotebookEntryDefinition("c01.note.cloth", CaseFact.ExaminedCloth, Keys.NoteCloth, QuestionEntry),
            new NotebookEntryDefinition("c01.note.sill", CaseFact.ExaminedSill, Keys.NoteSill, QuestionEntry),
            new NotebookEntryDefinition("c01.note.june", CaseFact.FoundJune, Keys.NoteJune, QuestionEntry),
            new NotebookEntryDefinition("c01.note.sweet", CaseFact.FoundSweet, Keys.NoteSweet, null),
        };

        public static bool IsQuestion(string id) => id == QuestionEntry || id == QuestionSource || id == QuestionMoved;

        public static bool IsKnownEntry(string id)
        {
            foreach (var entry in Entries)
            {
                if (entry.Id == id)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Entry IDs the facts have written, in discovery order (the save's notebook field).</summary>
        public static IReadOnlyList<string> DiscoveredEntryIds(CaseState state)
        {
            var ids = new List<string>();
            foreach (var entry in DiscoveredEntries(state))
            {
                ids.Add(entry.Id);
            }

            return ids;
        }

        public static IReadOnlyList<NotebookEntryDefinition> DiscoveredEntries(CaseState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var found = new List<NotebookEntryDefinition>();
            foreach (var fact in state.Facts)
            {
                foreach (var entry in Entries)
                {
                    if (entry.Proof == fact)
                    {
                        found.Add(entry);
                    }
                }
            }

            return found;
        }

        public static NotebookPage Build(CaseState state)
        {
            if (state == null)
            {
                throw new ArgumentNullException(nameof(state));
            }

            var questions = new List<NotebookQuestion>(Questions.Count);
            foreach (var question in Questions)
            {
                var annotations = new List<NotebookAnnotation>();
                foreach (var entry in Entries)
                {
                    if (entry.QuestionId == question.Id)
                    {
                        annotations.Add(new NotebookAnnotation(entry, state.Has(entry.Proof)));
                    }
                }

                questions.Add(new NotebookQuestion(question, state.Has(question.Proof), annotations));
            }

            var extras = new List<NotebookAnnotation>();
            foreach (var entry in Entries)
            {
                if (entry.QuestionId == null && !IsQuestion(entry.Id))
                {
                    extras.Add(new NotebookAnnotation(entry, state.Has(entry.Proof)));
                }
            }

            var finalCard = state.Has(CaseFact.CaseClosed)
                ? new NotebookFinalCard(DiscoveredEntries(state), Entries.Count)
                : null;
            return new NotebookPage(questions, extras, finalCard);
        }

        /// <summary>True when every annotation has been found (used by the post-closure hint).</summary>
        public static bool AllFound(CaseState state)
        {
            foreach (var entry in Entries)
            {
                if (!state.Has(entry.Proof))
                {
                    return false;
                }
            }

            return true;
        }
    }
}
