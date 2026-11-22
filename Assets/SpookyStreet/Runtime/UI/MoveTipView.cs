using SpookyStreet.App;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.Localization;
using SpookyStreet.Controls;
using SpookyStreet.Core.Movement;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Core.Tips;
using SpookyStreet.Localization;
using SpookyStreet.Player;
using SpookyStreet.World;
using UnityEngine;
using UnityEngine.UI;

namespace SpookyStreet.UI
{
    /// <summary>
    /// First-time moveset tips (S000.6 feel pass) on a paper tag just above the control they name: the Jump disc
    /// for hold, chain, glide, wall jump and ledge climb, the Use disc for the dive. The tag leads with the control's
    /// glyph in the current scheme (D25): the disc's ink icon on touch, the lit face button or the key cap otherwise. Which tip and when is the engine-free
    /// <see cref="MoveTipCoach"/>/<see cref="TipScheduler"/>; this view only shows the current one. The tag never
    /// takes input (no raycast targets), flutters in like a paper card (a plain cross-fade with reduced motion) and
    /// drops away when the tip times out or its move is performed. Game time: frozen while paused.
    /// </summary>
    public sealed class MoveTipView : MonoBehaviour
    {
        private const float FadeInSeconds = 0.22f;
        private const float FadeOutSeconds = 0.15f;
        private const float ReducedFadeSeconds = 0.12f;
        private const float RestAngle = 1.5f;

        private PipMotor _pip;
        private RectTransform _tag;
        private CanvasGroup _group;
        private LocalizedText _text;
        private Image _icon;
        private ControlGlyphView _glyph;
        private Vector2 _jumpSlot;
        private Vector2 _useSlot;
        private Vector2 _slot;
        private float _showTime;
        private MoveTip _shown = MoveTip.None;
        private float _restAngle = RestAngle;

        public MoveTipCoach Coach { get; private set; }

        public TipScheduler Scheduler => Coach.Scheduler;

        /// <summary>The paper tag (inactive while no tip is visible).</summary>
        public GameObject Tag => _tag.gameObject;

        /// <summary>The tip currently on the tag, including while it fades out.</summary>
        public MoveTip Shown => _shown;

        /// <summary>The control-scheme glyph on the tag (hidden on touch, where the disc icon shows).</summary>
        public ControlGlyphView Glyph => _glyph;

        /// <summary>The touch icon on the tag (hidden while a gamepad or keyboard glyph shows).</summary>
        public Image Icon => _icon;

        public static string KeyFor(MoveTip tip)
        {
            switch (tip)
            {
                case MoveTip.HoldJump: return LocalizationKeys.TipHoldJump;
                case MoveTip.ChainJump: return LocalizationKeys.TipChainJump;
                case MoveTip.Glide: return LocalizationKeys.TipGlide;
                case MoveTip.Dive: return LocalizationKeys.TipDive;
                case MoveTip.WallJump: return LocalizationKeys.TipWallJump;
                case MoveTip.LedgeClimb: return LocalizationKeys.TipLedgeClimb;
                default: return null;
            }
        }

        /// <summary>
        /// Builds the tag under <paramref name="hud"/>. Slots are anchored positions in the action-cluster corner
        /// (<paramref name="corner"/>): above the Jump disc and above the Use disc.
        /// </summary>
        public static MoveTipView Create(Transform hud, PipMotor pip, Vector2 corner, Vector2 jumpSlot, Vector2 useSlot,
            bool leftHanded)
        {
            var tag = UiFactory.PaperTag(hud, "MoveTip", LocalizationKeys.TipHoldJump, UiFactory.BodySize, corner,
                jumpSlot, new Vector2(540f, 100f), leftHanded ? -RestAngle : RestAngle, UiFactory.PlayPaper);
            var text = tag.transform.Find("Text") as RectTransform;
            if (text != null)
            {
                text.offsetMin = new Vector2(84f, 6f);
                text.offsetMax = new Vector2(-22f, -6f);
            }

            // Leading ink glyph of the control the tip names, and a candy-corn stitch strip along the bottom edge: the
            // tag's highlight, so it reads as "new" (ui-language: candy tags mark new things).
            var icon = UiFactory.Sprite(tag.transform, "Icon", UiFactory.Baked(UiSpriteCatalog.IconJump),
                UiFactory.Ink, new Vector2(0f, 0.5f), new Vector2(46f, 0f), new Vector2(56f, 56f));
            // D25: on a gamepad or keyboard the tag shows that scheme's glyph (lit face button, key cap) in the icon's
            // place; on touch the on-screen disc's own ink icon stays.
            var glyph = ControlGlyphView.Create(tag.transform, "Glyph", new Vector2(0f, 0.5f), new Vector2(46f, 0f), 56f);
            var strip = UiFactory.Sprite(tag.transform, "CandyStitch", null,
                Palette.CandyCornYellow, new Vector2(0.5f, 0f), new Vector2(0f, 12f), new Vector2(440f, 6f));
            strip.raycastTarget = false;
            foreach (var graphic in tag.GetComponentsInChildren<Graphic>(true))
            {
                graphic.raycastTarget = false; // tips never block input
            }

            var view = tag.gameObject.AddComponent<MoveTipView>();
            var group = tag.gameObject.AddComponent<CanvasGroup>();
            group.blocksRaycasts = false;
            group.interactable = false;
            group.alpha = 0f;
            view._pip = pip;
            view._tag = tag.rectTransform;
            view._group = group;
            view._text = tag.GetComponentInChildren<LocalizedText>(true);
            view._icon = icon;
            view._glyph = glyph;
            view._jumpSlot = jumpSlot;
            view._restAngle = leftHanded ? -RestAngle : RestAngle;
            view._useSlot = useSlot;
            view.UseStore(new PlayerPrefsTipStore());
            if (pip != null)
            {
                pip.Stepped += view.OnMotorStep;
            }

            ControlSchemeTracker.Changed += view.OnSchemeChanged;
            tag.gameObject.SetActive(false);
            return view;
        }

        /// <summary>Replaces the store (and the schedule state); tests use a <see cref="MemoryTipStore"/>.</summary>
        public void UseStore(ITipStore store)
        {
            Coach = new MoveTipCoach(new TipScheduler(store));
            _shown = MoveTip.None;
            if (_tag != null)
            {
                _tag.gameObject.SetActive(false);
            }
        }

        /// <summary>Advances the schedule on game time and animates the tag. Called by the HUD every frame in play.</summary>
        public void Tick(float deltaTime, bool blocked)
        {
            Scheduler.Blocked = blocked;
            Scheduler.Tick(deltaTime);
            MoveTip current = Scheduler.Current;
            if (current != MoveTip.None && current != _shown)
            {
                Present(current);
            }

            if (_shown == MoveTip.None || deltaTime <= 0f)
            {
                return;
            }

            bool reduced = AmbientMotion.ReducedMotion;
            if (current == _shown)
            {
                _showTime += deltaTime;
                float t = Mathf.Clamp01(_showTime / (reduced ? ReducedFadeSeconds : FadeInSeconds));
                _group.alpha = t;
                if (!reduced)
                {
                    // Paper flutter-in: from 12 pt below and ±4° to its resting angle, ease-out-back.
                    float e = EaseOutBack(t);
                    _tag.anchoredPosition = _slot + new Vector2(0f, -24f * (1f - e));
                    float rest = _restAngle;
                    _tag.localRotation = Quaternion.Euler(0f, 0f, Mathf.LerpUnclamped(rest + 4f, rest, e));
                }
            }
            else
            {
                _group.alpha = Mathf.MoveTowards(_group.alpha, 0f,
                    deltaTime / (reduced ? ReducedFadeSeconds : FadeOutSeconds));
                if (!reduced)
                {
                    _tag.anchoredPosition += Vector2.down * (60f * deltaTime);
                }

                if (_group.alpha <= 0f)
                {
                    _shown = MoveTip.None;
                    _tag.gameObject.SetActive(false);
                    _tag.anchoredPosition = _slot;
                }
            }
        }

        /// <summary>Hides any tip at once (leaving play). It stays marked as shown.</summary>
        public void HideNow()
        {
            Scheduler.Dismiss();
            _shown = MoveTip.None;
            _group.alpha = 0f;
            _tag.gameObject.SetActive(false);
        }

        private void Present(MoveTip tip)
        {
            _shown = tip;
            _showTime = 0f;
            _slot = MoveTipIds.Prompt(tip) == ControlPrompt.Use ? _useSlot : _jumpSlot;
            _tag.anchoredPosition = _slot;
            _tag.localRotation = Quaternion.Euler(0f, 0f, _restAngle);
            _text.SetKey(KeyFor(tip));
            _icon.sprite = UiFactory.Baked(tip == MoveTip.Dive ? UiSpriteCatalog.IconRoll : UiSpriteCatalog.IconJump);
            ShowGlyph(tip);
            _group.alpha = 0f;
            _tag.gameObject.SetActive(true);
        }

        /// <summary>The tip's control in the current scheme (D25): the touch icon, or the gamepad or keyboard glyph.</summary>
        private void ShowGlyph(MoveTip tip)
        {
            ControlScheme scheme = ControlSchemeTracker.Current;
            ControlGlyph glyph = ControlGlyphs.For(scheme, MoveTipIds.Prompt(tip));
            if (_glyph != null)
            {
                _glyph.Show(glyph);
            }

            _icon.gameObject.SetActive(glyph.IsTouch || _glyph == null);
        }

        private void OnSchemeChanged(ControlScheme scheme)
        {
            if (_shown != MoveTip.None)
            {
                ShowGlyph(_shown);
            }
        }

        private void OnMotorStep(MotorStep step)
        {
            if (_pip != null && _pip.TuningData != null)
            {
                Coach.Observe(step, _pip.State, _pip.TuningData);
            }
        }

        private void OnDestroy()
        {
            if (_pip != null)
            {
                _pip.Stepped -= OnMotorStep;
            }

            ControlSchemeTracker.Changed -= OnSchemeChanged;
        }

        private static float EaseOutBack(float t)
        {
            const float c1 = 1.70158f;
            const float c3 = c1 + 1f;
            float u = t - 1f;
            return 1f + c3 * u * u * u + c1 * u * u;
        }
    }
}
