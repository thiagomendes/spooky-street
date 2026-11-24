using SpookyStreet.Controls;
using SpookyStreet.Core.Nonverbal;
using UnityEngine;

namespace SpookyStreet.UI
{
    /// <summary>
    /// The notebook's control prompts (S004 F2, frame 77; F1 left them to this pass): with a gamepad or keyboard the
    /// page discs carry the page-turn glyphs (the bumpers, Q and R), the close disc the Back glyph (East, Escape), and
    /// the candy focus outline carries the Submit glyph (South, Enter) at its corner, so the child sees which button
    /// picks up and places the sticker the outline rings. On touch none show: the stickers and discs are the controls.
    /// </summary>
    public sealed partial class NotebookView
    {
        private ControlGlyphView _pagePrevPrompt;
        private ControlGlyphView _pageNextPrompt;
        private ControlGlyphView _closePrompt;
        private ControlGlyphView _submitPrompt;
        private RectTransform _promptLayer;
        private ControlScheme? _promptScheme;

        /// <summary>The Submit glyph at the focus outline's corner (tests, frame 77).</summary>
        public ControlGlyphView SubmitPrompt => _submitPrompt;

        /// <summary>The page-turn glyph on the next-page disc.</summary>
        public ControlGlyphView PageRightPrompt => _pageNextPrompt;

        private void BuildPrompts(RectTransform content)
        {
            _pagePrevPrompt = ControlGlyphView.Create(_prev.transform, "PagePrompt", new Vector2(0.5f, 0f),
                new Vector2(0f, -38f), 52f);
            _pageNextPrompt = ControlGlyphView.Create(_next.transform, "PagePrompt", new Vector2(0.5f, 0f),
                new Vector2(0f, -38f), 52f);
            _closePrompt = ControlGlyphView.Create(CloseButton.transform, "BackPrompt", new Vector2(0f, 0f),
                new Vector2(-14f, -14f), 48f);
            _promptLayer = UiFactory.Stretch(UiFactory.Rect(content, "FocusPrompt"));
            _submitPrompt = ControlGlyphView.Create(_promptLayer, "SubmitPrompt", new Vector2(0.5f, 0.5f), Vector2.zero,
                52f);
        }

        private void TickPrompts()
        {
            if (_submitPrompt == null)
            {
                return;
            }

            var scheme = ControlSchemeTracker.Current;
            if (_promptScheme != scheme)
            {
                _promptScheme = scheme;
                _pagePrevPrompt.Show(ControlGlyphs.For(scheme, ControlPrompt.PageLeft));
                _pageNextPrompt.Show(ControlGlyphs.For(scheme, ControlPrompt.PageRight));
                _closePrompt.Show(ControlGlyphs.For(scheme, ControlPrompt.Back));
            }

            // The Submit glyph rides the outline's lower-right corner while the outline shows (never on touch).
            bool ringed = scheme != ControlScheme.Touch && _focusRing != null && _focusRing.Showing;
            if (!ringed)
            {
                if (_submitPrompt.gameObject.activeSelf)
                {
                    _submitPrompt.Show(ControlGlyphs.For(ControlScheme.Touch, ControlPrompt.Submit));
                }

                return;
            }

            var glyph = ControlGlyphs.For(scheme, ControlPrompt.Submit);
            if (!_submitPrompt.gameObject.activeSelf || _submitPrompt.Glyph.BindingPath != glyph.BindingPath)
            {
                _submitPrompt.Show(glyph);
            }

            _promptLayer.SetAsLastSibling();
            var corners = new Vector3[4];
            ((RectTransform)_focusRing.transform).GetWorldCorners(corners);
            _submitPrompt.transform.position = corners[3]; // bottom-right
        }
    }
}
