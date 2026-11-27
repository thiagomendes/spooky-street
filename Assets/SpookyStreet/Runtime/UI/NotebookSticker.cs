using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SpookyStreet.UI
{
    /// <summary>
    /// One clue sticker on a notebook page (S002.5a): reports taps, holds and drags to the <see cref="NotebookView"/>,
    /// which owns every rule. A sticker sits in the tray (<see cref="SlotIndex"/> -1) or in a slot. Drags on a sticker
    /// never reach the page carousel behind it (the deepest drag handler wins). A finger held still on it for
    /// <see cref="HoldSeconds"/> reports <see cref="Held"/> (D32: show its memory) and that press is then not a tap, so
    /// holding never picks a sticker up or sends it home; moving the finger is still a drag.
    /// </summary>
    public sealed class NotebookSticker : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler,
        IEndDragHandler, IPointerDownHandler, IPointerUpHandler
    {
        /// <summary>How long a still press must last to count as a hold (short enough for a 6-year-old to find).</summary>
        public const float HoldSeconds = 0.45f;

        /// <summary>The sticker ID.</summary>
        public string StickerId { get; set; }

        /// <summary>The slot this sticker sits in; -1 in the tray.</summary>
        public int SlotIndex { get; set; } = -1;

        public RectTransform Rect => (RectTransform)transform;

        public Action<NotebookSticker> Tapped { get; set; }

        /// <summary>A still press reached <see cref="HoldSeconds"/> (D32: the memory, never a pick).</summary>
        public Action<NotebookSticker> Held { get; set; }

        public Action<NotebookSticker, PointerEventData> DragBegan { get; set; }

        public Action<NotebookSticker, PointerEventData> Dragged { get; set; }

        public Action<NotebookSticker, PointerEventData> DragEnded { get; set; }

        private bool _dragging;
        private bool _pressed;
        private bool _held;
        private float _pressedAt;

        /// <summary>The current press became a hold (tests).</summary>
        public bool HoldFired => _held;

        public void OnPointerDown(PointerEventData eventData)
        {
            _pressed = true;
            _held = false;
            _pressedAt = Time.unscaledTime;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            _pressed = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (_held)
            {
                _held = false; // the hold already answered this press
                return;
            }

            if (!_dragging)
            {
                Tapped?.Invoke(this);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _pressed = false; // a moving finger is a drag, never a hold
            _dragging = true;
            DragBegan?.Invoke(this, eventData);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (_dragging)
            {
                Dragged?.Invoke(this, eventData);
            }
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging)
            {
                return;
            }

            _dragging = false;
            _held = false;
            DragEnded?.Invoke(this, eventData);
        }

        private void Update()
        {
            if (_pressed && !_dragging && !_held && Time.unscaledTime - _pressedAt >= HoldSeconds)
            {
                _held = true;
                Held?.Invoke(this);
            }
        }

        private void OnDisable()
        {
            _pressed = false;
            _dragging = false;
        }
    }
}
