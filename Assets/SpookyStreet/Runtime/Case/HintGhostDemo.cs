using SpookyStreet.App;
using SpookyStreet.Controls;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.UI;
using UnityEngine;
using UnityEngine.UI;

namespace SpookyStreet.Case
{
    /// <summary>
    /// The tier-3 hint (D26 §6, retiered by S002.5b; D25 glyphs): it only demonstrates a move, never Use on a clue. On
    /// touch a translucent bone-paper ghost hand shows the move on the real Jump disc: a tap for the hop, the wall
    /// kick and the climb up from a ledge, a hold while a candy ring fills for the high jump and the glide. With a
    /// gamepad or a keyboard there is no hand: the bound button or key glyph (<see cref="ControlGlyphs"/>) pulses above
    /// the same control, filling its ring for a held move. It plays <see cref="HintPresentation.DemoLoops"/> loops per
    /// radio press, never takes input and stops when the objective changes. Game time.
    /// </summary>
    public sealed class HintGhostDemo : MonoBehaviour
    {
        public const float HandAlpha = 0.82f;
        public const float HandSize = 230f;
        public const float GlyphSize = 96f;

        private RectTransform _root;
        private RectTransform _hud;
        private Image _hand;
        private Image _targetRing;
        private ControlGlyphView _glyph;
        private RectTransform _target;
        private float _started = -1f;
        private Vector2 _moveDirection = Vector2.up;

        public bool Active => _started >= 0f;

        public HintMove Demo { get; private set; }

        public ControlScheme Scheme { get; private set; }

        /// <summary>The ghost hand is drawn this frame (touch only).</summary>
        public bool HandVisible => Active && _hand.gameObject.activeSelf && _hand.color.a > 0.01f;

        /// <summary>A button or key glyph is shown (gamepad or keyboard only).</summary>
        public bool GlyphVisible => Active && _glyph.HasGlyph;

        public ControlGlyphView Glyph => _glyph;

        public Image Hand => _hand;

        public static HintGhostDemo Create(Transform hud)
        {
            var root = UiFactory.Stretch(UiFactory.Rect(hud, "HintGhostDemo"));
            var demo = root.gameObject.AddComponent<HintGhostDemo>();
            demo._root = root;
            demo._hud = (RectTransform)hud;
            demo.Build();
            return demo;
        }

        private void Build()
        {
            var group = _root.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            _targetRing = UiFactory.Sprite(_root, "TargetRing", UiFactory.Baked(UiSpriteCatalog.StitchRing),
                Palette.CandyCornYellow, Vector2.zero, Vector2.zero, Vector2.one * 160f);
            _targetRing.type = Image.Type.Filled;
            _targetRing.fillMethod = Image.FillMethod.Radial360;
            _targetRing.fillOrigin = (int)Image.Origin360.Top;
            _glyph = ControlGlyphView.Create(_root, "Glyph", Vector2.zero, Vector2.zero, GlyphSize);
            _hand = UiFactory.Sprite(_root, "GhostHand", UiFactory.Baked(UiSpriteCatalog.GhostHand),
                UiFactory.WithAlpha(UiFactory.PlayPaper, HandAlpha), Vector2.zero, Vector2.zero, Vector2.one * HandSize);
            _hand.preserveAspect = true;
            _hand.rectTransform.pivot = new Vector2(UiSpriteCatalog.GhostHandFingertipX, UiSpriteCatalog.GhostHandFingertipY);
            foreach (var graphic in _root.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false;
            }

            _root.gameObject.SetActive(false);
        }

        /// <summary>
        /// Starts the demonstration of <paramref name="move"/> for the active control scheme.
        /// <paramref name="screenDirection"/> is where the move leads on screen (x right, y up); the hand leans that way.
        /// </summary>
        public void Play(HintMove move, Vector2 screenDirection)
        {
            if (move == HintMove.None)
            {
                Stop();
                return;
            }

            Demo = move;
            Scheme = ControlSchemeTracker.Current;
            _moveDirection = screenDirection.sqrMagnitude > 0.0001f ? screenDirection.normalized : Vector2.up;
            _target = FindTarget();
            _glyph.Show(ControlGlyphs.For(Scheme, HintPresentation.PromptFor(move)));
            _started = Time.time;
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
            Tick();
        }

        public void Stop()
        {
            _started = -1f;
            if (_root != null)
            {
                _root.gameObject.SetActive(false);
            }
        }

        /// <summary>The HUD control every move uses: the Jump disc.</summary>
        private RectTransform FindTarget() => _hud.Find("JumpButton") as RectTransform;

        /// <summary>The control's centre in this overlay's local space.</summary>
        private Vector2 TargetCentre()
        {
            if (_target == null)
            {
                return new Vector2(_root.rect.width * 0.5f, _root.rect.height * 0.3f);
            }

            Vector3 world = _target.TransformPoint(_target.rect.center);
            Vector2 local = _root.InverseTransformPoint(world);
            return local - _root.rect.min; // anchors are bottom-left
        }

        private float TargetSize() => _target != null ? Mathf.Max(_target.rect.width, _target.rect.height) : 160f;

        private void LateUpdate() => Tick();

        private void Tick()
        {
            if (!Active)
            {
                return;
            }

            float elapsed = Time.time - _started;
            if (elapsed >= HintPresentation.DemoLoops * HintPresentation.DemoLoopSeconds)
            {
                Stop();
                return;
            }

            float t = elapsed % HintPresentation.DemoLoopSeconds;
            Vector2 centre = TargetCentre();
            float size = TargetSize();
            bool touch = Scheme == ControlScheme.Touch;
            _hand.gameObject.SetActive(touch);
            _targetRing.rectTransform.anchoredPosition = centre;
            _targetRing.rectTransform.sizeDelta = Vector2.one * size * 1.18f;
            if (touch)
            {
                TickHand(t, centre, size);
            }
            else
            {
                TickGlyph(t, centre, size);
            }
        }

        /// <summary>Approach (0–0.45 s), press, the action, lift and fade before the loop ends.</summary>
        private void TickHand(float t, Vector2 centre, float size)
        {
            float approach = Smooth(Mathf.Clamp01(t / 0.45f));
            float alpha = Mathf.Clamp01(t / 0.25f) * (1f - Mathf.Clamp01((t - 1.85f) / 0.3f));
            // The hand comes in from the screen's inside, so it never leaves an edge: a control on the right gets a hand
            // from the left, and a control in the lower part of the screen (the thumb clusters) a hand from above,
            // pointing down at it. It leans the way it came.
            bool fromLeft = centre.x > _root.rect.width * 0.5f;
            bool fromAbove = centre.y < _root.rect.height * 0.45f;
            var rest = new Vector2(fromLeft ? -size * 0.55f : size * 0.55f, fromAbove ? size * 0.7f : -size * 0.7f);
            Vector2 fingertip = centre + Vector2.Lerp(rest, Vector2.zero, approach);
            float press = 0f;
            float ring = 0f;
            bool held = HintPresentation.Holds(Demo);
            if (held)
            {
                // Down at 0.5 s and held while the candy ring fills, up at 1.6 s.
                press = Window(t, 0.45f, 1.6f);
                ring = Mathf.Clamp01((t - 0.5f) / 1.0f) * (t < 1.6f ? 1f : 0f);
            }
            else
            {
                // A quick tap: down at 0.55 s, up at 0.8 s.
                press = Window(t, 0.5f, 0.85f);
                ring = press > 0f ? 1f : 0f;
            }

            var hand = _hand.rectTransform;
            hand.anchoredPosition = fingertip;
            float lean = fromAbove ? (fromLeft ? -150f : 150f) : (fromLeft ? -22f : 22f);
            hand.localRotation = Quaternion.Euler(0f, 0f, lean);
            // From above the hand is mirrored, so it stays a right hand with its thumb toward the screen's centre.
            float k = 1f - 0.08f * press;
            hand.localScale = new Vector3(fromAbove ? -k : k, k, 1f);
            _hand.color = UiFactory.WithAlpha(UiFactory.PlayPaper, HandAlpha * alpha);
            _targetRing.gameObject.SetActive(ring > 0f);
            _targetRing.fillAmount = held ? ring : 1f;
            _targetRing.color = UiFactory.WithAlpha(Palette.CandyCornYellow, 0.9f * alpha);
        }

        /// <summary>Gamepad and keyboard: the glyph pulses above the control; held prompts fill its ring.</summary>
        private void TickGlyph(float t, Vector2 centre, float size)
        {
            _targetRing.gameObject.SetActive(false);
            var rect = _glyph.Rect;
            rect.anchoredPosition = centre + new Vector2(0f, size * 0.5f + GlyphSize * 0.85f);
            float pulse = 1f + 0.12f * Mathf.Max(0f, Mathf.Sin(t / HintPresentation.DemoLoopSeconds * Mathf.PI * 4f));
            _glyph.Pulse = pulse;
            float hold = Mathf.Clamp01((t - 0.3f) / 1.2f) * (t < 1.7f ? 1f : 0f);
            _glyph.HoldProgress = hold;
            if (_glyph.Glyph.Shape == GlyphShape.PadStick)
            {
                _glyph.StickDirection = _moveDirection * Smooth(Mathf.Clamp01((t - 0.3f) / 0.5f)) * (t < 1.7f ? 1f : 0f);
            }
        }

        private static float Window(float t, float down, float up) =>
            Mathf.Clamp01((t - down) / 0.08f) * (1f - Mathf.Clamp01((t - up) / 0.08f));

        private static float Smooth(float x) => x * x * (3f - 2f * x);
    }
}
