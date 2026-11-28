using SpookyStreet.App;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Flow;
using SpookyStreet.LivingStreet;
using SpookyStreet.UI;
using UnityEngine;

namespace SpookyStreet.Case
{
    /// <summary>
    /// The living street's test slice beside the case (docs/living-street.md §9 phase 1, D34): the stage is built with
    /// the case, its Use targets join the case's (a case point in reach always wins, §6 rule 2), its file loads with the
    /// saves, a confirmed New game clears it and Replay keeps it (D31), and it is ticked while playing, held still
    /// during cutscenes, the finale and the cutaway (§3.4 rule 7).
    /// </summary>
    public sealed partial class CaseDirector
    {
        /// <summary>The east lane, the crowd, the doors and the lantern (null before <see cref="BuildLivingStreet"/>).</summary>
        public LivingStreetStage Living { get; private set; }

        private System.Action<ReadingSheet> _parentSections;

        private void BuildLivingStreet(GameBootstrap game)
        {
            Living = LivingStreetStage.Create(transform, game, (RectTransform)UiCanvas.transform, Captions);
            Living.CanInteract = () => _flow.State == GameState.Playing && !_modal && !(Cutscenes != null && Cutscenes.Playing);
            Living.SetModal = open =>
            {
                if (open)
                {
                    Bench.End();
                    Captions.Clear();
                }

                SetModal(open);
            };
            Notebook.Neighbourhood = () => Living != null ? Living.State : null;
            foreach (var target in Living.Targets)
            {
                Targets.AddExtra(target);
            }

            var build = game.BuildInfo;
            _parentSections = sheet =>
            {
                if (Living != null)
                {
                    Living.AppendParentSections(sheet, build);
                }
            };
            ParentPage.AppendSections = _parentSections;
        }

        /// <summary>The street's frame: held still while a cutscene, the finale or the cutaway frames the world.</summary>
        private void TickLivingStreet(float dt, bool crossing)
        {
            if (Living == null)
            {
                return;
            }

            bool frozen = (Cutscenes != null && Cutscenes.Playing) || FinalePlaying || (Cutaway != null && Cutaway.Active) || crossing;
            Living.Tick(dt, frozen, State);
        }

        private void OnDestroyLivingStreet()
        {
            if (_parentSections != null && ParentPage.AppendSections == _parentSections)
            {
                ParentPage.AppendSections = null;
            }
        }
    }
}
