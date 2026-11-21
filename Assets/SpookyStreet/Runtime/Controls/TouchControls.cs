using SpookyStreet.Core.Movement;
using UnityEngine;
using UnityEngine.EventSystems;

namespace SpookyStreet.Controls
{
    /// <summary>Aggregates the on-screen controls so gameplay code reads one place.</summary>
    public sealed class TouchControls : MonoBehaviour
    {
        public FloatingStick Stick { get; set; }
        public TouchButton JumpButton { get; set; }
        public OrbitDragArea OrbitArea { get; set; }

        public Vector2 StickValue => Stick != null && Stick.isActiveAndEnabled ? Stick.Value : Vector2.zero;

        public bool ConsumeJump() => JumpButton != null && JumpButton.ConsumePress();

        /// <summary>Orbit drag accumulated since the last call, in reference-canvas pixels.</summary>
        public Vector2 ConsumeOrbitDelta() => OrbitArea != null ? OrbitArea.ConsumeDelta() : Vector2.zero;

        /// <summary>Clears every held or pending touch so nothing sticks across pause, focus loss or menus.</summary>
        public void ResetAll()
        {
            if (Stick != null)
            {
                Stick.ResetStick();
            }

            if (JumpButton != null)
            {
                JumpButton.ResetButton();
            }

            if (OrbitArea != null)
            {
                OrbitArea.ResetArea();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus)
            {
                ResetAll();
            }
        }
    }

    /// <summary>
    /// Floating stick (spec §5): the origin appears where the left thumb lands and trails the thumb when
    /// it travels past the radius. Tracks exactly one pointer id so a second finger cannot hijack it.
    /// </summary>
    public sealed class FloatingStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public float Radius = 110f;
        public float DeadZone = 0.12f;
        public RectTransform BaseVisual;
        public RectTransform KnobVisual;

        private RectTransform _rect;
        private Canvas _canvas;
        private int _pointerId = int.MinValue;
        private Vector2 _origin;

        public Vector2 Value { get; private set; }

        public bool Active => _pointerId != int.MinValue;

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _canvas = GetComponentInParent<Canvas>();
            SetVisualsVisible(false);
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Active)
            {
                return;
            }

            _pointerId = eventData.pointerId;
            _origin = ToLocal(eventData);
            Value = Vector2.zero;
            SetVisualsVisible(true);
            UpdateVisuals(_origin);
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId)
            {
                return;
            }

            Vector2 finger = ToLocal(eventData);
            StickMath.FollowOrigin(_origin.x, _origin.y, finger.x, finger.y, Radius, out float ox, out float oy);
            _origin = new Vector2(ox, oy);
            StickMath.Evaluate(finger.x - _origin.x, finger.y - _origin.y, Radius, DeadZone, out float vx, out float vy);
            Value = new Vector2(vx, vy);
            UpdateVisuals(finger);
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == _pointerId)
            {
                ResetStick();
            }
        }

        public void ResetStick()
        {
            _pointerId = int.MinValue;
            Value = Vector2.zero;
            SetVisualsVisible(false);
        }

        private void OnDisable() => ResetStick();

        private Vector2 ToLocal(PointerEventData eventData)
        {
            Camera eventCamera = _canvas != null && _canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? _canvas.worldCamera
                : null;
            RectTransformUtility.ScreenPointToLocalPointInRectangle(_rect, eventData.position, eventCamera, out Vector2 local);
            return local;
        }

        private void UpdateVisuals(Vector2 finger)
        {
            if (BaseVisual != null)
            {
                BaseVisual.anchoredPosition = _origin;
            }

            if (KnobVisual != null)
            {
                KnobVisual.anchoredPosition = _origin + Vector2.ClampMagnitude(finger - _origin, Radius);
            }
        }

        private void SetVisualsVisible(bool visible)
        {
            if (BaseVisual != null)
            {
                BaseVisual.gameObject.SetActive(visible);
            }

            if (KnobVisual != null)
            {
                KnobVisual.gameObject.SetActive(visible);
            }
        }
    }

    /// <summary>Press-edge button for thumbs: holding never generates repeated presses.</summary>
    public sealed class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        private bool _pendingPress;
        private int _pointerId = int.MinValue;

        public bool Held => _pointerId != int.MinValue;

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Held)
            {
                return;
            }

            _pointerId = eventData.pointerId;
            _pendingPress = true;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == _pointerId)
            {
                _pointerId = int.MinValue;
            }
        }

        public void OnPointerExit(PointerEventData eventData) => OnPointerUp(eventData);

        public bool ConsumePress()
        {
            bool pressed = _pendingPress;
            _pendingPress = false;
            return pressed;
        }

        public void ResetButton()
        {
            _pendingPress = false;
            _pointerId = int.MinValue;
        }

        private void OnDisable() => ResetButton();
    }

    /// <summary>Drag on empty screen space to orbit the camera (spec §5).</summary>
    public sealed class OrbitDragArea : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        private int _pointerId = int.MinValue;
        private Vector2 _accumulated;
        private Canvas _canvas;

        private void Awake() => _canvas = GetComponentInParent<Canvas>();

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_pointerId == int.MinValue)
            {
                _pointerId = eventData.pointerId;
            }
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (eventData.pointerId != _pointerId)
            {
                return;
            }

            float scale = _canvas != null && _canvas.scaleFactor > 0f ? _canvas.scaleFactor : 1f;
            _accumulated += eventData.delta / scale;
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (eventData.pointerId == _pointerId)
            {
                _pointerId = int.MinValue;
            }
        }

        public Vector2 ConsumeDelta()
        {
            Vector2 delta = _accumulated;
            _accumulated = Vector2.zero;
            return delta;
        }

        public void ResetArea()
        {
            _pointerId = int.MinValue;
            _accumulated = Vector2.zero;
        }

        private void OnDisable() => ResetArea();
    }
}
