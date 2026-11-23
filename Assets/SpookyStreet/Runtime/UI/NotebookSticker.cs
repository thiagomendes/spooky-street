using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SpookyStreet.UI
{
    /// <summary>
    /// One clue sticker on a notebook page (S002.5a): reports taps and drags to the <see cref="NotebookView"/>,
    /// which owns every rule. A sticker sits in the tray (<see cref="SlotIndex"/> -1) or in a slot. Drags on a sticker
    /// never reach the page carousel behind it (the deepest drag handler wins).
    /// </summary>
    public sealed class NotebookSticker : MonoBehaviour, IPointerClickHandler, IBeginDragHandler, IDragHandler,
        IEndDragHandler
    {
        /// <summary>The sticker ID.</summary>
        public string StickerId { get; set; }

        /// <summary>The slot this sticker sits in; -1 in the tray.</summary>
        public int SlotIndex { get; set; } = -1;

        public RectTransform Rect => (RectTransform)transform;

        public Action<NotebookSticker> Tapped { get; set; }

        public Action<NotebookSticker, PointerEventData> DragBegan { get; set; }

        public Action<NotebookSticker, PointerEventData> Dragged { get; set; }

        public Action<NotebookSticker, PointerEventData> DragEnded { get; set; }

        private bool _dragging;

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_dragging)
            {
                Tapped?.Invoke(this);
            }
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
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
            DragEnded?.Invoke(this, eventData);
        }
    }
}
