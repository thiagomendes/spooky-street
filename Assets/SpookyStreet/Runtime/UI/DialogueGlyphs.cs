using SpookyStreet.Controls;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Localization;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.UI;

namespace SpookyStreet.UI
{
    /// <summary>
    /// The conversation's next and skip discs follow the active control scheme (D25): on touch the next disc shows
    /// its arrow and the skip button stands alone; with a gamepad or keyboard the next disc shows the advance button
    /// or key (<see cref="ControlPrompt.Next"/>) and a small glyph of the skip button or key sits on the skip button
    /// (<see cref="ControlPrompt.Skip"/>, which this component also binds: gamepad east, Tab). The v1 card's
    /// "continue" line follows too. Lives on the dialogue root, so it runs only while a conversation is open.
    /// </summary>
    public sealed class DialogueGlyphs : MonoBehaviour
    {
        private DialogueView _view;
        private Image[] _nextIcons;
        private ControlGlyphView[] _nextGlyphs;
        private ControlGlyphView _skipGlyph;
        private LocalizedText _continue;
        private InputActionMap _map;
        private InputAction _skip;
        private ControlScheme? _scheme;

        /// <summary>The scheme the discs show now.</summary>
        public ControlScheme? Scheme => _scheme;

        public ControlGlyphView NextGlyph(int index) => _nextGlyphs[index];

        public ControlGlyphView SkipGlyph => _skipGlyph;

        /// <summary>True while the next discs show their arrow (touch).</summary>
        public bool NextShowsArrow => _nextIcons.Length > 0 && _nextIcons[0] != null && _nextIcons[0].enabled;

        public static DialogueGlyphs Attach(DialogueView view)
        {
            var glyphs = view.gameObject.AddComponent<DialogueGlyphs>();
            glyphs.Build(view);
            return glyphs;
        }

        private void Build(DialogueView view)
        {
            _view = view;
            var content = view.transform.Find("Content");
            var discs = new[] { content.Find("SpeechCard/NextDisc"), content.Find("SpeechBubble/NextDisc") };
            _nextIcons = new Image[discs.Length];
            _nextGlyphs = new ControlGlyphView[discs.Length];
            for (int i = 0; i < discs.Length; i++)
            {
                var disc = discs[i];
                var icon = disc != null ? disc.Find("Icon") : null;
                _nextIcons[i] = icon != null ? icon.GetComponent<Image>() : null;
                var size = disc != null ? ((RectTransform)disc).rect.width : 88f;
                _nextGlyphs[i] = ControlGlyphView.Create(disc != null ? disc : content, "Glyph", new Vector2(0.5f, 0.5f),
                    Vector2.zero, Mathf.Max(48f, size * 0.62f));
            }

            var skip = view.SkipButton != null ? view.SkipButton.transform : null;
            _skipGlyph = ControlGlyphView.Create(skip != null ? skip : content, "SkipGlyph", new Vector2(0f, 0.5f),
                new Vector2(-40f, 0f), 58f);
            var line = content.Find("SpeechCard/TapToContinue");
            _continue = line != null ? line.GetComponent<LocalizedText>() : null;

            _map = new InputActionMap("DialogueSkip");
            _skip = _map.AddAction("Skip", InputActionType.Button, ControlGlyphs.KeySkip);
            _skip.AddBinding(ControlGlyphs.PadSkip);
        }

        private void OnEnable()
        {
            _map?.Enable();
            _scheme = null;
            Apply();
        }

        private void OnDisable() => _map?.Disable();

        private void OnDestroy() => _map?.Dispose();

        private void Update()
        {
            Apply();
            bool canRead = _view.CanAdvance == null || _view.CanAdvance();
            if (canRead && _skip.WasPressedThisFrame())
            {
                _view.Skip();
            }
        }

        private void Apply()
        {
            if (_view == null)
            {
                return;
            }

            var scheme = ControlSchemeTracker.Current;
            if (_scheme == scheme)
            {
                return;
            }

            _scheme = scheme;
            bool touch = scheme == ControlScheme.Touch;
            for (int i = 0; i < _nextIcons.Length; i++)
            {
                if (_nextIcons[i] != null)
                {
                    _nextIcons[i].enabled = touch;
                }

                _nextGlyphs[i].Show(ControlGlyphs.For(scheme, ControlPrompt.Next));
            }

            _skipGlyph.Show(ControlGlyphs.For(scheme, ControlPrompt.Skip));
            if (_continue != null)
            {
                _continue.SetKey(ControlGlyphs.ContinueKey(scheme));
            }
        }
    }
}
