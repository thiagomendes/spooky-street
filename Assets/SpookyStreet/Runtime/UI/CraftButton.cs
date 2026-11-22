using System;
using SpookyStreet.Core.Art;
using SpookyStreet.Localization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SpookyStreet.UI
{
    /// <summary>Primary = orange felt with a candy stitch (one per screen); secondary = bone paper, purple stitch.</summary>
    public enum CraftButtonStyle
    {
        Primary,
        Secondary,
    }

    /// <summary>
    /// The look of a craft button (docs/ui-language.md): face sprite, tiled running stitch, optional icon, label and
    /// an optional sub-label (e.g. Continue's chapter and bat count once saves exist). The style can change at run
    /// time, so a screen keeps exactly one orange action. Keyboard/gamepad focus turns a paper stitch candy.
    /// </summary>
    public sealed class CraftButton : MonoBehaviour, ISelectHandler, IDeselectHandler
    {
        public Image Face;
        public Image Stitch;
        public Image Icon;
        public Text Label;
        public Text Detail;
        public PressScale Press;

        private bool _selected;

        public CraftButtonStyle Style { get; private set; }

        public bool HasDetail => Detail != null && Detail.gameObject.activeSelf;

        public void SetStyle(CraftButtonStyle style)
        {
            Style = style;
            bool primary = style == CraftButtonStyle.Primary;
            UiFactory.ApplySurface(Face, primary ? UiSpriteCatalog.FeltButtonOrange : UiSpriteCatalog.PaperCard,
                primary ? Palette.JackOrange : Palette.WarmPaper, Color.white);
            if (Press != null)
            {
                Press.ChangeStitch = !primary;
                Press.StitchPressed = Palette.JackOrange;
            }

            ApplyStitch();
        }

        /// <summary>
        /// Shows a small second line under the label (null key hides it). Hook for Continue's save summary
        /// (<c>ui.title.continue_detail</c>); nothing calls it until saves exist.
        /// </summary>
        public void SetDetail(string key, Func<object[]> arguments = null)
        {
            if (Detail == null)
            {
                return;
            }

            bool show = !string.IsNullOrEmpty(key);
            Detail.gameObject.SetActive(show);
            if (show)
            {
                var localized = Detail.GetComponent<LocalizedText>();
                if (localized == null)
                {
                    localized = Detail.gameObject.AddComponent<LocalizedText>();
                }

                localized.SetKey(key, arguments);
            }

            var label = Label.rectTransform;
            label.anchorMin = new Vector2(0f, show ? 0.36f : 0f);
            label.anchorMax = Vector2.one;
        }

        public void OnSelect(BaseEventData eventData)
        {
            _selected = true;
            ApplyStitch();
        }

        public void OnDeselect(BaseEventData eventData)
        {
            _selected = false;
            ApplyStitch();
        }

        private void ApplyStitch()
        {
            if (Stitch == null)
            {
                return;
            }

            bool primary = Style == CraftButtonStyle.Primary;
            Stitch.color = primary || _selected ? Palette.CandyCornYellow : Palette.HauntedPurple;
        }
    }
}
