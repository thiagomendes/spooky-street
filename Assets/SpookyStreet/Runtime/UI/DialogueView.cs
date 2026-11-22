using System;
using System.Collections.Generic;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.Case;
using SpookyStreet.Localization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.InputSystem;
using UnityEngine.UI;
using UiKeys = SpookyStreet.Core.Localization.LocalizationKeys.CaseUi;

namespace SpookyStreet.UI
{
    /// <summary>
    /// The speech card (docs/ui-language.md, docs/evidence/s000.2/ui/ui_dialogue.jpg): a stitched paper card at the
    /// bottom with the speaker's portrait in a felt frame (a coloured initial until portrait art exists), an orange
    /// felt name tab, line pips, "Tap to continue" with an orange next disc, and Skip top-right away from the reading
    /// path. A tap anywhere, Use, Jump, Enter or the gamepad face buttons advance; <see cref="DialogueAdvanceGate"/>
    /// makes sure holding a button never advances, so the press that opened the conversation cannot skip a line.
    /// </summary>
    public sealed class DialogueView : MonoBehaviour
    {
        private static readonly Color Graphite = new Color32(0x4A, 0x44, 0x58, 0xFF);

        private readonly DialogueAdvanceGate _gate = new DialogueAdvanceGate();
        private readonly List<ScriptLine> _lines = new List<ScriptLine>();
        private readonly List<Image> _pips = new List<Image>();

        private InputActionMap _map;
        private InputAction _advance;
        private RectTransform _card;
        private Image _portrait;
        private Text _initial;
        private Image _nameTab;
        private LocalizedText _name;
        private Text _line;
        private RectTransform _pipRow;
        private bool _tapped;
        private float _flutter;
        private bool _largeText;

        /// <summary>Raised once when the last line is dismissed or the conversation is skipped.</summary>
        public event Action Finished;

        /// <summary>
        /// Gate for reading input (set by the director): false while the game is paused, so pause suspends the
        /// conversation.
        /// </summary>
        public Func<bool> CanAdvance { get; set; }

        public bool IsOpen => gameObject.activeSelf;

        public int LineIndex { get; private set; }

        public int LineCount => _lines.Count;

        public ScriptLine CurrentLine => LineIndex < _lines.Count ? _lines[LineIndex] : default;

        /// <summary>The visible line's text (as localized).</summary>
        public string CurrentText => _line != null ? _line.text : null;

        public Button SkipButton { get; private set; }

        /// <summary>Text size hook (dialogue line 26 pt, large 31 pt). No settings screen yet.</summary>
        public bool LargeText
        {
            get => _largeText;
            set
            {
                _largeText = value;
                if (_line != null)
                {
                    _line.fontSize = value ? 62 : 52;
                    _line.resizeTextMaxSize = _line.fontSize;
                }
            }
        }

        public static DialogueView Create(RectTransform canvasRoot)
        {
            var root = UiFactory.Stretch(UiFactory.Rect(canvasRoot, "Dialogue"));
            var view = root.gameObject.AddComponent<DialogueView>();
            view.Build(root);
            root.gameObject.SetActive(false);
            return view;
        }

        private void Build(RectTransform root)
        {
            // Tapping anywhere advances; the catcher also keeps taps off the hidden HUD below.
            var catcher = UiFactory.Panel(root, "TapCatcher", new Color(0f, 0f, 0f, 0f));
            var catcherButton = catcher.gameObject.AddComponent<Button>();
            catcherButton.transition = Selectable.Transition.None;
            catcherButton.onClick.AddListener(() => _tapped = true);

            // Modal screens dim the world first: a night-violet fade over the lower half.
            var fade = UiFactory.Panel(root, "Scrim", UiFactory.WithAlpha(Palette.NightViolet, 0.8f), raycast: false);
            fade.sprite = VerticalFade();
            fade.rectTransform.anchorMax = new Vector2(1f, 0.62f);

            var content = UiFactory.Stretch(UiFactory.Rect(root, "Content"));
            content.gameObject.AddComponent<SafeAreaFitter>();

            var card = UiFactory.PaperCard(content, "SpeechCard", new Vector2(0.5f, 0f), new Vector2(70f, 150f),
                new Vector2(1000f, 236f), -0.6f);
            card.raycastTarget = true;
            var cardButton = card.gameObject.AddComponent<Button>();
            cardButton.transition = Selectable.Transition.None;
            cardButton.onClick.AddListener(() => _tapped = true);
            _card = card.rectTransform;

            // Portrait: a felt disc frame with the speaker's initial (portrait art is a later pass).
            _portrait = UiFactory.Sprite(content, "Portrait", null, Color.white, new Vector2(0.5f, 0f),
                new Vector2(70f - 500f - 6f, 182f), new Vector2(188f, 188f));
            _portrait.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -3f);
            UiFactory.InkShadow(_portrait, new Vector2(4f, -6f), 0.55f);
            UiFactory.Sprite(_portrait.transform, "Ring", UiFactory.Baked(UiSpriteCatalog.StitchRing),
                Palette.CandyCornYellow, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(188f, 188f));
            _initial = UiFactory.Label(_portrait.transform, "Initial", null, 104, TextAnchor.MiddleCenter, UiFactory.Ink,
                UiFactory.DisplayFont);
            UiFactory.Stretch(_initial.rectTransform);

            _nameTab = UiFactory.PaperTag(card.transform, "NameTab", null, 40, new Vector2(0f, 1f), new Vector2(240f, 6f),
                new Vector2(210f, 66f), -3f, new Color(0.98f, 0.56f, 0.24f, 1f));
            var nameText = _nameTab.GetComponentInChildren<Text>();
            nameText.font = UiFactory.DisplayFont;
            _name = nameText.gameObject.AddComponent<LocalizedText>();

            _line = UiFactory.Label(card.transform, "Line", null, 52, TextAnchor.UpperLeft, UiFactory.Ink, UiFactory.BodyFont);
            UiFactory.Stretch(_line.rectTransform);
            _line.rectTransform.offsetMin = new Vector2(120f, 56f);
            _line.rectTransform.offsetMax = new Vector2(-40f, -26f);
            _line.resizeTextMinSize = 36;

            _pipRow = UiFactory.Rect(card.transform, "LinePips");
            UiFactory.Place(_pipRow, Vector2.zero, new Vector2(124f, 30f), new Vector2(300f, 24f), new Vector2(0f, 0.5f));

            var next = UiFactory.FeltDisc(card.transform, "NextDisc", UiSpriteCatalog.FeltDiscOrange,
                Palette.CandyCornYellow, UiSpriteCatalog.IconNext, UiFactory.Ink, null, 96f, new Vector2(1f, 0f),
                new Vector2(-62f, 40f), 1f);
            next.raycastTarget = false;
            var hint = UiFactory.Label(card.transform, "TapToContinue", UiKeys.DialogueContinue, 28, TextAnchor.MiddleRight,
                Graphite, UiFactory.BoldFont);
            UiFactory.Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(-124f, 34f), new Vector2(300f, 40f),
                new Vector2(1f, 0.5f));

            SkipButton = UiFactory.SecondaryButton(content, "SkipButton", UiKeys.DialogueSkip, Vector2.one,
                new Vector2(-136f, -62f), new Vector2(232f, 96f), Skip, UiSpriteCatalog.IconSkip);
            SkipButton.transform.localRotation = Quaternion.Euler(0f, 0f, 1.2f);

            _map = new InputActionMap("Dialogue");
            _advance = _map.AddAction("Advance", InputActionType.Button, "<Keyboard>/e");
            _advance.AddBinding("<Keyboard>/space");
            _advance.AddBinding("<Keyboard>/enter");
            _advance.AddBinding("<Gamepad>/buttonSouth");
            _advance.AddBinding("<Gamepad>/buttonWest");
        }

        /// <summary>Opens the card on the first of <paramref name="lines"/> (captions are filtered out by the caller).</summary>
        public void Play(IReadOnlyList<ScriptLine> lines)
        {
            _lines.Clear();
            if (lines != null)
            {
                _lines.AddRange(lines);
            }

            if (_lines.Count == 0)
            {
                return;
            }

            gameObject.SetActive(true);
            _map.Enable();
            BuildPips();
            Show(0);
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }
        }

        /// <summary>Shows the next line, or finishes after the last one.</summary>
        public void Advance()
        {
            if (!IsOpen)
            {
                return;
            }

            if (LineIndex + 1 < _lines.Count)
            {
                Show(LineIndex + 1);
            }
            else
            {
                Close();
            }
        }

        /// <summary>Skip: dismiss the rest of the conversation (its facts were already recorded).</summary>
        public void Skip()
        {
            if (IsOpen)
            {
                Close();
            }
        }

        /// <summary>Closes without raising <see cref="Finished"/> (back to title, replay).</summary>
        public void Cancel()
        {
            _lines.Clear();
            _map.Disable();
            gameObject.SetActive(false);
        }

        private void Close()
        {
            Cancel();
            Finished?.Invoke();
        }

        private void Show(int index)
        {
            LineIndex = index;
            var line = _lines[index];
            _line.text = Localizer.Get(line.Key);
            _name.SetKey(CaseSpeakers.NameKey(line.Speaker));
            ApplyPortrait(line.Speaker);
            for (int i = 0; i < _pips.Count; i++)
            {
                _pips[i].color = i == index ? Palette.HauntedPurple : UiFactory.WithAlpha(Palette.HauntedPurple, 0.3f);
            }

            _gate.Begin(_advance.IsPressed());
            _tapped = false;
            _flutter = 0f;
        }

        private void BuildPips()
        {
            foreach (var pip in _pips)
            {
                Destroy(pip.gameObject);
            }

            _pips.Clear();
            _pipRow.gameObject.SetActive(_lines.Count > 1);
            for (int i = 0; i < _lines.Count && _lines.Count > 1; i++)
            {
                _pips.Add(UiFactory.Sprite(_pipRow, "Pip" + i, IconFactory.Disc, Palette.HauntedPurple, new Vector2(0f, 0.5f),
                    new Vector2(10f + i * 32f, 0f), new Vector2(18f, 18f)));
            }
        }

        private void ApplyPortrait(Speaker speaker)
        {
            string felt;
            Color tint = Color.white;
            Color ink = UiFactory.Ink;
            switch (speaker)
            {
                case Speaker.Bea:
                    felt = UiSpriteCatalog.FeltDiscOrange;
                    break;
                case Speaker.Vale:
                    felt = UiSpriteCatalog.FeltDiscBone;
                    break;
                case Speaker.Kit:
                    felt = UiSpriteCatalog.FeltDiscBone;
                    tint = new Color(0.55f, 0.32f, 0.6f, 1f); // plum
                    ink = Palette.WarmPaper;
                    break;
                default: // Pip and June: night felt, bone initial
                    felt = UiSpriteCatalog.FeltDiscNight;
                    ink = Palette.WarmPaper;
                    break;
            }

            UiFactory.ApplySurface(_portrait, felt, Palette.JackOrange, tint);
            string name = Localizer.Get(CaseSpeakers.NameKey(speaker) ?? string.Empty);
            _initial.text = InitialOf(name);
            _initial.color = ink;
        }

        /// <summary>The first letter of the displayed name, skipping a title such as "Mr." or "Aunt".</summary>
        public static string InitialOf(string name)
        {
            if (string.IsNullOrEmpty(name))
            {
                return "?";
            }

            string[] words = name.Split(' ');
            string word = words[words.Length - 1];
            return string.IsNullOrEmpty(word) ? name.Substring(0, 1) : word.Substring(0, 1).ToUpperInvariant();
        }

        private void Update()
        {
            if (!IsOpen || _lines.Count == 0)
            {
                return;
            }

            bool canRead = CanAdvance == null || CanAdvance();
            float dt = canRead ? Time.unscaledDeltaTime : 0f;
            bool tapped = _tapped && canRead;
            _tapped = false;
            if (_gate.Step(dt, canRead && _advance.IsPressed(), canRead && _advance.WasPressedThisFrame(), tapped))
            {
                Advance();
            }

            if (_flutter < 1f && IsOpen)
            {
                // Paper in: a small settle on each line (reduced motion would cross-fade instead; no setting yet).
                _flutter = Mathf.Min(1f, _flutter + Time.unscaledDeltaTime / 0.18f);
                float s = Mathf.Lerp(0.96f, 1f, 1f - (1f - _flutter) * (1f - _flutter));
                _card.localScale = new Vector3(s, s, 1f);
            }
        }

        private void OnDestroy()
        {
            _map?.Dispose();
        }

        private static Sprite s_fade;

        /// <summary>Opaque at the bottom, clear at the top (the dialogue scrim).</summary>
        private static Sprite VerticalFade()
        {
            if (s_fade != null)
            {
                return s_fade;
            }

            var texture = new Texture2D(1, 64, TextureFormat.RGBA32, false) { wrapMode = TextureWrapMode.Clamp };
            for (int y = 0; y < 64; y++)
            {
                float t = 1f - y / 63f;
                texture.SetPixel(0, y, new Color(1f, 1f, 1f, t * t * (3f - 2f * t)));
            }

            texture.Apply();
            s_fade = Sprite.Create(texture, new Rect(0f, 0f, 1f, 64f), new Vector2(0.5f, 0.5f));
            return s_fade;
        }
    }
}
