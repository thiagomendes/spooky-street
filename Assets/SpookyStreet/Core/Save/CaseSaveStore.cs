using System;
using System.Collections.Generic;
using SpookyStreet.Core.Case;
using SaveKeys = SpookyStreet.Core.Localization.LocalizationKeys.Save;

namespace SpookyStreet.Core.Save
{
    /// <summary>What resuming found, and what to tell the player.</summary>
    public sealed class CaseResume
    {
        internal CaseResume(SaveLoadStatus status, CaseSaveData data, IReadOnlyList<string> issues)
        {
            Status = status;
            Data = data;
            Issues = issues;
        }

        public SaveLoadStatus Status { get; }

        /// <summary>The save to resume (Latest or Recovered), else null.</summary>
        public CaseSaveData Data { get; }

        public IReadOnlyList<string> Issues { get; }

        /// <summary>A save exists to continue (title screen shows Continue).</summary>
        public bool CanContinue => Data != null;

        /// <summary>Restored case state (empty when there is nothing to continue).</summary>
        public CaseState State => Data != null ? Data.ToState() : CaseState.Empty;

        /// <summary>The child's saved right sets (empty when there is nothing to continue).</summary>
        public CaseAnswers Answers => Data != null ? Data.ToAnswers() : CaseAnswers.Empty;

        /// <summary>The validated safe anchor to place Pip at (unknown → plaza).</summary>
        public string AnchorId => Data != null ? Data.AnchorId : CaseAnchors.Start;

        /// <summary>Notice heading/body keys for the player, or null when nothing needs saying.</summary>
        public string NoticeHeadingKey =>
            Status == SaveLoadStatus.Newer ? SaveKeys.NewerHeading
            : Status == SaveLoadStatus.Unreadable ? SaveKeys.UnreadableHeading
            : null;

        public string NoticeBodyKey =>
            Status == SaveLoadStatus.Newer ? SaveKeys.NewerBody
            : Status == SaveLoadStatus.Unreadable ? SaveKeys.UnreadableBody
            : Status == SaveLoadStatus.Recovered ? SaveKeys.Recovered
            : null;

        /// <summary>The notice must offer an explicit "Start fresh" (and "Back").</summary>
        public bool OffersFreshStart => Status == SaveLoadStatus.Newer || Status == SaveLoadStatus.Unreadable;
    }

    /// <summary>
    /// The case save: resume with recovery, autosave, explicit fresh start and replay. Preferences are a separate
    /// <see cref="PreferencesStore"/>, so nothing here can touch them.
    /// </summary>
    public sealed class CaseSaveStore
    {
        private readonly SaveSlotStore _slot;

        public CaseSaveStore(ISaveFileSystem fs, string directory)
        {
            _slot = new SaveSlotStore(fs, SaveSlotPaths.ForCase(directory), CaseSaveCodec.Check);
        }

        public SaveSlotPaths Paths => _slot.Paths;

        /// <summary>True while a newer-schema save must be preserved; autosave is skipped until a fresh start.</summary>
        public bool WritesBlocked => _slot.WritesBlocked;

        public CaseResume Load()
        {
            var load = _slot.Load();
            if (!load.HasContents)
            {
                return new CaseResume(load.Status, null, Array.Empty<string>());
            }

            var read = CaseSaveCodec.Read(load.Contents);
            return new CaseResume(load.Status, read.Data, read.Issues);
        }

        /// <summary>
        /// Autosave (on fact change, closure and safe transitions). Returns false, writing nothing, while a newer
        /// save is being preserved.
        /// </summary>
        public bool Save(CaseState state, string anchorId) => Save(state, anchorId, null);

        /// <summary>Autosave with the child's right sets (written when a question is solved).</summary>
        public bool Save(CaseState state, string anchorId, CaseAnswers answers)
        {
            if (_slot.WritesBlocked)
            {
                return false;
            }

            _slot.Write(CaseSaveCodec.Serialize(CaseSaveData.FromState(state, anchorId, answers)));
            return true;
        }

        /// <summary>The player explicitly chose to start fresh after a save notice. A newer file is kept aside.</summary>
        public CaseSaveData StartFresh()
        {
            if (_slot.WritesBlocked)
            {
                _slot.PreserveLatestAndUnblock();
            }

            var fresh = CaseSaveData.Fresh();
            _slot.Write(CaseSaveCodec.Serialize(fresh));
            return fresh;
        }

        /// <summary>Confirmed "Replay case": resets only case data (facts, notebook, anchor).</summary>
        public CaseSaveData Replay() => StartFresh();
    }

    /// <summary>Preferences and language, in their own file with the same atomic write and recovery.</summary>
    public sealed class PreferencesStore
    {
        private readonly SaveSlotStore _slot;

        public PreferencesStore(ISaveFileSystem fs, string directory)
        {
            _slot = new SaveSlotStore(fs, SaveSlotPaths.ForPreferences(directory), PreferencesData.Check);
        }

        public SaveSlotPaths Paths => _slot.Paths;

        /// <summary>Loads preferences, or defaults when none are readable (a newer file is left untouched).</summary>
        public PreferencesData Load()
        {
            var load = _slot.Load();
            if (load.HasContents && PreferencesData.TryRead(load.Contents, out var preferences, out _))
            {
                return preferences;
            }

            return new PreferencesData();
        }

        /// <summary>
        /// Writes preferences after the player changes a setting. A newer-schema preferences file is first moved
        /// aside untouched (it holds no progress, so the player's explicit change is enough to replace it).
        /// </summary>
        public void Save(PreferencesData preferences)
        {
            if (_slot.WritesBlocked)
            {
                _slot.PreserveLatestAndUnblock();
            }

            _slot.Write(PreferencesData.Serialize(preferences));
        }
    }
}
