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

        /// <summary>The contextual Use disc (interaction verb, or roll/dive while nothing is in range).</summary>
        public TouchButton UseButton { get; set; }
        public OrbitDragArea OrbitArea { get; set; }

        public Vector2 StickValue => Stick != null && Stick.isActiveAndEnabled ? Stick.Value : Vector2.zero;

        public bool ConsumeJump() => JumpButton != null && JumpButton.ConsumePress();

        /// <summary>True while a thumb holds the Jump disc.</summary>
        public bool JumpHeld => JumpButton != null && JumpButton.isActiveAndEnabled && JumpButton.Held;

        public bool ConsumeUse() => UseButton != null && UseButton.ConsumePress();

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

            if (UseButton != null)
            {
                UseButton.ResetButton();
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
    /// At rest a ghost sits at <see cref="RestPosition"/> so a child sees where to put the thumb; it pulses
    /// until the first real move, then stays as a faint marker. The direction a child's thumb means is steadied by
    /// <see cref="StickSteering"/> (tremor smoothing and a light pull onto up, the sides and the diagonals), so a
    /// stick held straight walks straight (owner, 2026-11-28).
    /// </summary>
    public sealed class FloatingStick : MonoBehaviour, IPointerDownHandler, IDragHandler, IPointerUpHandler
    {
        public float Radius = 110f;
        public float DeadZone = 0.12f;
        public RectTransform BaseVisual;
        public RectTransform KnobVisual;
        /// <summary>Alpha group over base + knob.</summary>
        public CanvasGroup Visuals;
        /// <summary>Ghost position in this rect's local space (relative to its pivot).</summary>
        public Vector2 RestPosition;
        public float PulsePeriod = 1.6f;
        public float PulseMinAlpha = 0.45f;
        public float PulseMaxAlpha = 0.8f;
        public float RestAlpha = 0.45f;

        private RectTransform _rect;
        private Canvas _canvas;
        private int _pointerId = int.MinValue;
        private Vector2 _origin;
        private readonly StickSteering _steering = new StickSteering(new StickSteeringTuning());
        private Vector2 _raw;
        private Vector2 _steered;
        private int _steeredFrame = -1;

        /// <summary>The thumb's offset exactly as dragged (dead zone and radius applied, no steering).</summary>
        public Vector2 RawValue => _raw;

        /// <summary>
        /// The stick as gameplay reads it: <see cref="RawValue"/> steadied once per frame (reading it again in the
        /// same frame returns the same value).
        /// </summary>
        public Vector2 Value
        {
            get
            {
                if (_steeredFrame != Time.frameCount)
                {
                    _steeredFrame = Time.frameCount;
                    _steering.Step(Time.deltaTime, _raw.x, _raw.y, out float x, out float y);
                    _steered = new Vector2(x, y);
                }

                return _steered;
            }
        }

        public bool Active => _pointerId != int.MinValue;

        /// <summary>True once the player has pushed the stick past the dead zone at least once.</summary>
        public bool HasMoved { get; private set; }

        private void Awake()
        {
            _rect = (RectTransform)transform;
            _canvas = GetComponentInParent<Canvas>();
            ShowRest();
        }

        // RestPosition is assigned after AddComponent (which already ran Awake), so place the ghost again here.
        private void Start() => ShowRest();

        public void OnPointerDown(PointerEventData eventData)
        {
            if (Active)
            {
                return;
            }

            _pointerId = eventData.pointerId;
            _origin = ToLocal(eventData);
            _raw = Vector2.zero;
            SetAlpha(1f);
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
            _raw = new Vector2(vx, vy);
            HasMoved |= _raw.sqrMagnitude > 0f;
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
            _raw = Vector2.zero;
            _steered = Vector2.zero;
            _steeredFrame = -1;
            _steering.Reset();
            ShowRest();
        }

        private void OnDisable() => ResetStick();

        private void Update()
        {
            if (Active)
            {
                return;
            }

            if (HasMoved)
            {
                SetAlpha(RestAlpha);
                return;
            }

            float wave = 0.5f - 0.5f * Mathf.Cos(Time.unscaledTime * Mathf.PI * 2f / Mathf.Max(0.1f, PulsePeriod));
            SetAlpha(Mathf.Lerp(PulseMinAlpha, PulseMaxAlpha, wave));
        }

        private void ShowRest()
        {
            _origin = RestPosition;
            UpdateVisuals(RestPosition);
            SetAlpha(HasMoved ? RestAlpha : PulseMinAlpha);
        }

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

        private void SetAlpha(float alpha)
        {
            if (Visuals != null)
            {
                Visuals.alpha = alpha;
            }
        }
    }

    /// <summary>
    /// Press-edge button for thumbs: holding never generates repeated presses. A thumb already pressing another
    /// touch button that slides onto this one presses it too (slide from Jump to Use mid-glide dives without lifting
    /// the thumb); a hover or a camera drag never does.
    /// </summary>
    public sealed class TouchButton : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler,
        IPointerEnterHandler
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

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (eventData == null || Held)
            {
                return;
            }

            GameObject pressedOn = eventData.rawPointerPress != null ? eventData.rawPointerPress : eventData.pointerPress;
            if (pressedOn == null || pressedOn == gameObject)
            {
                return;
            }

            var origin = pressedOn.GetComponentInParent<TouchButton>();
            if (origin != null && origin != this)
            {
                OnPointerDown(eventData);
            }
        }

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
