using SpookyStreet.Controls;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Nonverbal;
using UnityEngine;

namespace SpookyStreet.UI
{
    /// <summary>
    /// Sticker memories in the notebook (owner decision D32, docs/S004-sticker-memory.md). Every found sticker can show
    /// a small card with a short memory of the place where the child found it and one descriptive caption:
    /// <list type="bullet">
    /// <item>Touch: a tap on a tray sticker picks it up (tap-tap, unchanged) and opens its memory at the same time, so
    /// the very first tap teaches it. Holding any sticker (tray or slot) opens the memory without picking it up or
    /// sending it home. A drag never opens it and closes an open one, so the slots are in view for the drop.</item>
    /// <item>Gamepad and keyboard: Submit picks as before (and opens the memory, like a tap); Inspect (West, F or I)
    /// opens the memory of the sticker under the outline, in the tray or in a slot, without picking it up. Its glyph
    /// rides a corner of the outline the open memory card cannot cover (Core <see cref="GlyphPlacement"/>).</item>
    /// </list>
    /// Opening a memory never places a sticker, never counts a try and never records anything (Core
    /// <see cref="StickerMemories.Open"/> only reads). The card covers the question and the slots, never the tray; it
    /// closes by itself after about four seconds, or on a press on it, a placement, a drag, a page turn, a cursor move
    /// or Back (Back first lets go of a held sticker and its memory, then puts an inspected memory away, then closes).
    /// </summary>
    public sealed partial class NotebookView
    {
        private StickerMemoryCard _memoryCard;
        private ControlGlyphView _inspectPrompt;

        /// <summary>The world side that draws the memory pictures (set by the nonverbal layer; null draws the sticker).</summary>
        public IStickerMemoryStage MemoryStage { get; set; }

        /// <summary>The memory card (null until a memory first opens).</summary>
        public StickerMemoryCard MemoryCard => _memoryCard;

        public bool MemoryOpen => _memoryCard != null && _memoryCard.IsOpen;

        /// <summary>The sticker whose memory is open, or null.</summary>
        public string MemorySticker => MemoryOpen ? _memoryCard.StickerId : null;

        /// <summary>The Inspect glyph by the focus outline (gamepad and keyboard only).</summary>
        public ControlGlyphView InspectPrompt => _inspectPrompt;

        /// <summary>
        /// Opens the memory of a found sticker on this notebook without picking it up, placing it or sending it home
        /// (the hold gesture, Inspect, and tests). False when the sticker has no memory or is not found.
        /// </summary>
        public bool InspectSticker(string stickerId)
        {
            if (!IsOpen || _checking)
            {
                return false;
            }

            return OpenMemory(stickerId);
        }

        /// <summary>Inspect on the cursor (West, F, I): the sticker under the outline, in the tray or in a slot.</summary>
        public void InspectAtCursor()
        {
            _cursorVisible = true;
            var view = Current;
            if (view == null || _checking)
            {
                return;
            }

            string id = _cursor.InspectTarget(CurrentBoard, view.TrayIds);
            if (id == null)
            {
                return;
            }

            if (MemorySticker == id)
            {
                CloseMemory(); // pressed again: put the memory away
                return;
            }

            OpenMemory(id);
        }

        private bool OpenMemory(string stickerId)
        {
            var memory = StickerMemories.Open(stickerId, CurrentState);
            if (memory == null)
            {
                return false;
            }

            if (_memoryCard == null)
            {
                // Over the top of the current page (the question and the slots), clear of the tray below.
                var content = (RectTransform)_viewport.parent;
                float y = PageSize.y * 0.5f + 14f - 10f - StickerMemoryCard.Size.y * 0.5f;
                _memoryCard = StickerMemoryCard.Create(content, new Vector2(0f, y));
            }

            _memoryCard.Stage = MemoryStage;
            _memoryCard.CanAnimate = () => CanAnimate == null || CanAnimate(); // the notebook's clock, read live
            _memoryCard.Open(memory, ReducedMotion);
            return true;
        }

        /// <summary>Puts the memory card away (harmless when none is open).</summary>
        public void CloseMemory()
        {
            if (_memoryCard != null)
            {
                _memoryCard.Close();
            }
        }

        private void OnStickerHeld(NotebookSticker sticker)
        {
            if (sticker != null && _dragHome == null)
            {
                InspectSticker(sticker.StickerId);
            }
        }

        /// <summary>The Inspect glyph rides the outline while it rings a sticker, on a corner the memory card leaves clear.</summary>
        private void TickInspectPrompt()
        {
            var scheme = ControlSchemeTracker.Current;
            var view = Current;
            bool onSticker = scheme != ControlScheme.Touch && _focusRing != null && _focusRing.Showing && view != null &&
                             IsOpen && _cursor.InspectTarget(CurrentBoard, view.TrayIds) != null;
            if (!onSticker)
            {
                if (_inspectPrompt != null && _inspectPrompt.gameObject.activeSelf)
                {
                    _inspectPrompt.gameObject.SetActive(false);
                }

                return;
            }

            if (_inspectPrompt == null)
            {
                _inspectPrompt = ControlGlyphView.Create(_viewport.parent, "InspectPrompt", new Vector2(0.5f, 0.5f),
                    Vector2.zero, 46f);
            }

            var glyph = ControlGlyphs.For(scheme, ControlPrompt.Inspect);
            if (!_inspectPrompt.gameObject.activeSelf || _inspectPrompt.Glyph.BindingPath != glyph.BindingPath)
            {
                _inspectPrompt.Show(glyph);
            }

            _inspectPrompt.transform.SetAsLastSibling();
            var corners = new Vector3[4];
            ((RectTransform)_focusRing.transform).GetWorldCorners(corners);
            var outline = Box(corners);
            UiBox? cover = null;
            if (MemoryOpen)
            {
                ((RectTransform)_memoryCard.transform).GetWorldCorners(corners);
                cover = Box(corners);
            }

            ((RectTransform)_inspectPrompt.transform).GetWorldCorners(corners);
            float size = Mathf.Max(corners[2].x - corners[0].x, corners[2].y - corners[0].y);
            var at = GlyphPlacement.Inspect(outline, cover, size);
            var position = _inspectPrompt.transform.position;
            _inspectPrompt.transform.position = new Vector3(at.X, at.Y, position.z);
        }

        /// <summary>The glyph's box where it now stands (tests: the memory card never covers it).</summary>
        public UiBox InspectPromptBox()
        {
            var corners = new Vector3[4];
            ((RectTransform)_inspectPrompt.transform).GetWorldCorners(corners);
            return Box(corners);
        }

        /// <summary>The open memory card's box (tests).</summary>
        public UiBox MemoryCardBox()
        {
            var corners = new Vector3[4];
            ((RectTransform)_memoryCard.transform).GetWorldCorners(corners);
            return Box(corners);
        }

        private static UiBox Box(Vector3[] corners)
        {
            float xMin = float.MaxValue, yMin = float.MaxValue, xMax = float.MinValue, yMax = float.MinValue;
            foreach (var c in corners)
            {
                xMin = Mathf.Min(xMin, c.x);
                yMin = Mathf.Min(yMin, c.y);
                xMax = Mathf.Max(xMax, c.x);
                yMax = Mathf.Max(yMax, c.y);
            }

            return new UiBox(xMin, yMin, xMax, yMax);
        }
    }
}
