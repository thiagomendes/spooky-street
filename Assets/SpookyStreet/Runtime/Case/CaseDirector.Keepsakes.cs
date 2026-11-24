using System.Collections.Generic;
using SpookyStreet.App;
using SpookyStreet.Core.Audio;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Keepsakes;
using SpookyStreet.Core.Save;
using SpookyStreet.Localization;
using SpookyStreet.Player;
using SpookyStreet.UI;
using SpookyStreet.World;
using UnityEngine;
using TitleKeys = SpookyStreet.Core.Localization.LocalizationKeys;

namespace SpookyStreet.Case
{
    /// <summary>
    /// Keepsakes in play (S004 stream A2): the bat mobile on Pip's porch, the dress-up trunk and its screen, the costume
    /// Pip wears (in play and on the title), the costume remarks, the notebook disc's single wobble and the page that
    /// assembles when a find completes one, and the Continue line's bat count. No pop-up, no number but a count, no
    /// total (engagement §2): everything else the child sees of the keepsakes is in the world or the notebook.
    /// </summary>
    public sealed partial class CaseDirector
    {
        private readonly KeepsakeNotebook _keepsakePages = new KeepsakeNotebook();

        /// <summary>The bat mobile on the arrival porch (the visible count).</summary>
        public BatMobile Mobile { get; private set; }

        /// <summary>The dress-up trunk on the arrival porch.</summary>
        public CostumeTrunk Trunk { get; private set; }

        /// <summary>The trunk's screen.</summary>
        public DressUpView DressUp { get; private set; }

        /// <summary>The pieces Pip wears (null without Pip's avatar).</summary>
        public PipCostume Costume { get; private set; }

        /// <summary>Pages completed this session that assemble the next time the notebook opens.</summary>
        public KeepsakeNotebook KeepsakePages => _keepsakePages;

        private void BuildKeepsakePlay(GameBootstrap game)
        {
            Mobile = BatMobile.Create(transform, game.Materials);
            Trunk = CostumeTrunk.Create(transform, game.Materials, OpenTrunk);
            Trunk.Target.Offered = feet => _flow.State == GameState.Playing && CostumeTrunk.Offered(feet, Keepsakes.State);
            Targets.Extra = Trunk.Target;
            var avatar = _pip.GetComponent<PipAvatar>();
            Costume = avatar != null ? PipCostume.Attach(avatar, game.Materials) : null;
            DressUp = DressUpView.Create((RectTransform)UiCanvas.transform);
            DressUp.Toggled = (piece, on) => WearPiece(piece, on);
            DressUp.Closed += CloseTrunk;
            Keepsakes.Found += OnBatFound;
        }

        /// <summary>The mobile, the costume and the title line follow the keepsakes (load, New game, Replay).</summary>
        private void RefreshKeepsakes()
        {
            var state = Keepsakes != null ? Keepsakes.State : KeepsakeState.Empty;
            Mobile?.Show(state.BatCount);
            Costume?.Apply(state.WornPieces);
            DressUp?.Refresh(state);
            RefreshContinueDetail(HasSave && State.Count > 0);
        }

        /// <summary>
        /// Continue's second line (<c>ui.title.continue_detail</c>): the open question and the bats found, only once a
        /// bat is found (S004 §3 A2 "count only": a count, never a total, never "0 paper bats").
        /// </summary>
        private void RefreshContinueDetail(bool canContinue)
        {
            var craft = _ui != null && _ui.ContinueButton != null ? _ui.ContinueButton.GetComponent<CraftButton>() : null;
            if (craft == null)
            {
                return;
            }

            var keepsakes = Keepsakes != null ? Keepsakes.State : null;
            if (!canContinue || !KeepsakeTitle.ShowsBatCount(keepsakes))
            {
                craft.SetDetail(null);
                return;
            }

            string line = ContinueRecap.Build(State, AnchorId).LineKey;
            craft.SetDetail(TitleKeys.TitleContinueDetail, () => Keepsakes.State.BatCount,
                () => new object[] { Localizer.Get(line), Keepsakes.State.BatCount });
        }

        /// <summary>A find: the mobile gains its bat; a completed page wobbles the notebook disc once and waits to assemble.</summary>
        private void OnBatFound(BatFindResult result)
        {
            Mobile?.Show(result.State.BatCount);
            if (result.CompletedPage > 0)
            {
                _discWobble = NotebookDiscWobbleSeconds;
                _keepsakePages.Completed(result.CompletedPage);
                AudioCues.StickerWobble();
            }
        }

        /// <summary>The remark a friend makes on Pip's worn piece, after their lines, once ever (Kit after the stamp).</summary>
        private IReadOnlyList<ScriptLine> WithCostumeRemark(Speaker speaker, IReadOnlyList<ScriptLine> lines)
        {
            if (Keepsakes == null || lines == null || lines.Count == 0)
            {
                return lines;
            }

            var remark = CostumeRemarks.Due(Keepsakes.State, speaker, State.Has(CaseFact.CaseStamped));
            if (remark == null)
            {
                return lines;
            }

            Keepsakes.MarkSaid(remark.Id);
            var said = new List<ScriptLine>(lines) { remark.Line };
            return said;
        }

        /// <summary>Use on the trunk: the lid opens, Pip turns to the street in front of it, the cards show.</summary>
        private void OpenTrunk()
        {
            if (_flow.State != GameState.Playing || _modal || Keepsakes == null || !KeepsakePorch.TrunkOffered(Keepsakes.State))
            {
                return;
            }

            Bench.End();
            Captions.Clear();
            SetModal(true);
            Trunk.Open(_pip, Keepsakes.State);
            DressUp.Open(Keepsakes.State);
        }

        /// <summary>Opens the trunk as Use on it does (tests, captures).</summary>
        public void UseTrunk() => OpenTrunk();

        private void CloseTrunk()
        {
            Trunk.Close();
            SetModal(false);
        }

        /// <summary>Puts a piece on or takes it off: saved at once, worn at once.</summary>
        public void WearPiece(CostumePiece piece, bool on)
        {
            if (Keepsakes == null || piece == null)
            {
                return;
            }

            if (Keepsakes.Wear(piece.Id, on))
            {
                if (on)
                {
                    AudioCues.StickerDrop();
                }
                else
                {
                    AudioCues.StickerPick();
                }
            }

            Costume?.Apply(Keepsakes.State.WornPieces);
            DressUp?.Refresh(Keepsakes.State);
        }
    }
}
