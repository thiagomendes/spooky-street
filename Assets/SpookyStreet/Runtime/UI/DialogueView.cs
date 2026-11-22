using System;
using System.Collections.Generic;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Localization;
using SpookyStreet.UI.Nonverbal;
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
    /// Non-verbal UX v2 (D23, docs/nonverbal-ux.md §3): a line with a vignette (<see cref="C01Vignettes.For"/>) shows
    /// the speech bubble instead: bone paper with its tail on the speaker in the world, the speaker's face disc on
    /// its left edge, a night-felt stage where the paper-puppet vignette loops (<see cref="VignettePlayer"/>) and the
    /// small text line under it only while <see cref="ShowSpeechText"/> is on. Lines without a vignette yet keep the
    /// v1 card (with the face disc), whose words always show. Gesture cues go to the director
    /// (<see cref="GestureCue"/>); every line closed with next raises <see cref="LineDismissed"/> (memory replays).
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
        private RectTransform _content;
        private Image _face;
        private RectTransform _bubble;
        private Image _tail;
        private Image _stagePanel;
        private Text _bubbleLine;
        private bool _showSpeechText = true;
        private bool _reducedMotion;

        /// <summary>Bubble size and the stage inset (canvas units; the mock's 520×120 pt stage at @2x).</summary>
        public static readonly Vector2 BubbleSize = new Vector2(1060f, 327f);
        public const float StageInset = 26f;
        public const float StageHeight = 231f;
        public const float BubbleBottom = 40f;

        /// <summary>Raised once when the last line is dismissed or the conversation is skipped.</summary>
        public event Action Finished;

        /// <summary>The player closed a line with next (not Skip): the memory replay trigger (§12 step 4).</summary>
        public event Action<ScriptLine> LineDismissed;

        /// <summary>A vignette's world cue on its first pass: the speaker plays this gesture clip (Bea: "Point").</summary>
        public event Action<ScriptLine, string> GestureCue;

        /// <summary>World point the bubble's tail aims at (the speaker's head), or null.</summary>
        public Func<Vector3?> SpeakerAnchor { get; set; }

        /// <summary>The world camera for the tail.</summary>
        public Camera ViewCamera { get; set; }

        /// <summary>Top of the speech bubble in the dialogue content (canvas units from the bottom).</summary>
        public float BubbleTop => BubbleBottom + (_bubble != null ? _bubble.sizeDelta.y : BubbleSize.y);

        /// <summary>The puppet stage of the bubble.</summary>
        public VignettePlayer Vignette { get; private set; }

        /// <summary>True while the current line shows the animated bubble (false: the v1 card).</summary>
        public bool ShowsVignette { get; private set; }

        /// <summary>The words of the current line are on screen (always on the v1 card).</summary>
        public bool TextLineVisible => IsOpen && (ShowsVignette ? _bubbleLine.gameObject.activeSelf : _card.gameObject.activeSelf);

        /// <summary><see cref="NonverbalSettings.ShowSpeechText"/>: the small text line under the stage.</summary>
        public bool ShowSpeechText
        {
            get => _showSpeechText;
            set
            {
                _showSpeechText = value;
                if (IsOpen && _lines.Count > 0)
                {
                    ApplyLayout();
                }
            }
        }

        /// <summary>Reduced motion: still beats with cross-fades, no pulsing.</summary>
        public bool ReducedMotion
        {
            get => _reducedMotion;
            set
            {
                _reducedMotion = value;
                if (Vignette != null)
                {
                    Vignette.ReducedMotion = value;
                }
            }
        }

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
            DialogueGlyphs.Attach(view); // next and skip glyphs per control scheme (D25)
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
            _content = content;
            BuildBubble(content);

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
            var ring = UiFactory.Sprite(_portrait.transform, "Ring", UiFactory.Baked(UiSpriteCatalog.StitchRing),
                Palette.CandyCornYellow, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(188f, 188f));
            UiFactory.Stretch(ring.rectTransform); // follows the disc's size on the card and on the bubble
            _initial = UiFactory.Label(_portrait.transform, "Initial", null, 104, TextAnchor.MiddleCenter, UiFactory.Ink,
                UiFactory.DisplayFont);
            UiFactory.Stretch(_initial.rectTransform);
            // The speaker's face (puppet part) inside the disc: who is talking, for pre-readers.
            _face = UiFactory.Sprite(_portrait.transform, "Face", null, Color.white, new Vector2(0.5f, 0.5f),
                new Vector2(0f, 4f), Vector2.zero);
            _face.rectTransform.anchorMin = new Vector2(0.1f, 0.1f);
            _face.rectTransform.anchorMax = new Vector2(0.9f, 0.9f);
            _face.rectTransform.sizeDelta = Vector2.zero;
            _face.preserveAspect = true;

            _nameTab = UiFactory.PaperTag(card.transform, "NameTab", null, 40, new Vector2(0f, 1f), new Vector2(240f, 6f),
                new Vector2(210f, 66f), -3f, new Color(0.98f, 0.56f, 0.24f, 1f));
            var nameText = _nameTab.GetComponentInChildren<Text>();
            nameText.font = UiFactory.DisplayFont;
            _name = nameText.gameObject.AddComponent<LocalizedText>();

            _line = UiFactory.Label(card.transform, "Line", null, 52, TextAnchor.UpperLeft, UiFactory.Ink, UiFactory.BodyFont);
            UiFactory.Stretch(_line.rectTransform);
            _line.rectTransform.offsetMin = new Vector2(120f, 60f);
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
            UiFactory.Place(hint.rectTransform, new Vector2(1f, 0f), new Vector2(-124f, 36f), new Vector2(300f, 48f),
                new Vector2(1f, 0.5f));

            SkipButton = UiFactory.SecondaryButton(content, "SkipButton", UiKeys.DialogueSkip, Vector2.one,
                new Vector2(-136f, -62f), new Vector2(232f, 96f), Skip, UiSpriteCatalog.IconSkip);
            SkipButton.transform.localRotation = Quaternion.Euler(0f, 0f, 1.2f);

            // The portrait is shared: it sits on the card or on the bubble's left edge (ApplyLayout).
            _portrait.transform.SetAsLastSibling();
            SkipButton.transform.SetAsLastSibling();

            _map = new InputActionMap("Dialogue");
            _advance = _map.AddAction("Advance", InputActionType.Button, "<Keyboard>/e");
            _advance.AddBinding("<Keyboard>/space");
            _advance.AddBinding("<Keyboard>/enter");
            _advance.AddBinding("<Gamepad>/buttonSouth");
            _advance.AddBinding("<Gamepad>/buttonWest");
        }

        private void BuildBubble(RectTransform content)
        {
            // Tail first, so the bubble's paper covers its base.
            _tail = UiFactory.Sprite(content, "BubbleTail", PuppetAtlas.Get(PuppetAtlasLayout.BubbleTail), Color.white,
                new Vector2(0.5f, 0f), Vector2.zero, new Vector2(72f, 92f));
            _tail.rectTransform.pivot = new Vector2(0.5f, 0f);
            UiFactory.InkShadow(_tail, new Vector2(3f, -4f), 0.45f);

            var bubble = UiFactory.PaperCard(content, "SpeechBubble", new Vector2(0.5f, 0f), new Vector2(0f, BubbleBottom),
                BubbleSize, 0f, Palette.HauntedPurple);
            bubble.raycastTarget = true;
            bubble.rectTransform.pivot = new Vector2(0.5f, 0f);
            var bubbleButton = bubble.gameObject.AddComponent<Button>();
            bubbleButton.transition = Selectable.Transition.None;
            bubbleButton.onClick.AddListener(() => _tapped = true);
            _bubble = bubble.rectTransform;

            // Stage: a lifted night-felt panel (pg.stage, 9-sliced) holding the masked puppet player.
            _stagePanel = UiFactory.Sprite(_bubble, "Stage", PuppetAtlas.Get(PuppetParts.Stage),
                new Color(0.2f, 0.18f, 0.38f, 1f), new Vector2(0.5f, 1f), new Vector2(0f, -StageInset),
                new Vector2(BubbleSize.x - 2f * StageInset, StageHeight));
            _stagePanel.rectTransform.pivot = new Vector2(0.5f, 1f);
            if (_stagePanel.sprite != null)
            {
                _stagePanel.color = Color.white;
            }

            var inner = UiFactory.Stretch(UiFactory.Rect(_stagePanel.transform, "Inner"));
            inner.offsetMin = new Vector2(8f, 8f);
            inner.offsetMax = new Vector2(-8f, -8f);
            Vignette = VignettePlayer.Create(inner, "Vignette");
            Vignette.CanRun = () => CanAdvance == null || CanAdvance();
            Vignette.CueStarted += OnCue;

            _bubbleLine = UiFactory.Label(_bubble, "TextLine", null, 30, TextAnchor.MiddleCenter, Graphite,
                UiFactory.BoldFont);
            UiFactory.Place(_bubbleLine.rectTransform, new Vector2(0.5f, 0f), new Vector2(30f, 36f),
                new Vector2(BubbleSize.x - 300f, 60f));

            var next = UiFactory.FeltDisc(_bubble, "NextDisc", UiSpriteCatalog.FeltDiscOrange, Palette.CandyCornYellow,
                UiSpriteCatalog.IconNext, UiFactory.Ink, null, 88f, new Vector2(1f, 0f), new Vector2(-4f, 12f), 1f);
            next.raycastTarget = false;

            _bubble.gameObject.SetActive(false);
            _tail.gameObject.SetActive(false);
        }

        private void OnCue(VignetteCue cue, int loop)
        {
            // The vignette plays once with the voice, then loops quietly: world gestures only on the first pass.
            if (cue.Kind == CueKind.Gesture && loop == 0 && IsOpen && LineIndex < _lines.Count)
            {
                GestureCue?.Invoke(_lines[LineIndex], cue.Arg);
            }
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

            if (LineIndex < _lines.Count)
            {
                LineDismissed?.Invoke(_lines[LineIndex]);
            }

            if (!IsOpen)
            {
                return; // a listener closed the conversation
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
            Vignette.Stop();
            ShowsVignette = false;
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
            _bubbleLine.text = _line.text;
            _name.SetKey(CaseSpeakers.NameKey(line.Speaker));
            ApplyPortrait(line.Speaker);
            var visual = C01Vignettes.For(line.Key);
            var vignette = visual != null && !visual.UsesV1Strip && PuppetAtlas.Available
                ? C01Vignettes.Get(visual.VignetteId)
                : null;
            ShowsVignette = vignette != null;
            ApplyLayout();
            Vignette.ReducedMotion = _reducedMotion;
            Vignette.Play(vignette);
            for (int i = 0; i < _pips.Count; i++)
            {
                _pips[i].color = i == index ? Palette.HauntedPurple : UiFactory.WithAlpha(Palette.HauntedPurple, 0.3f);
            }

            _gate.Begin(_advance.IsPressed());
            _tapped = false;
            _flutter = 0f;
        }

        /// <summary>Bubble (with or without its text line) or the v1 card; the face disc and pips follow.</summary>
        private void ApplyLayout()
        {
            _card.gameObject.SetActive(!ShowsVignette);
            _bubble.gameObject.SetActive(ShowsVignette);
            _tail.gameObject.SetActive(ShowsVignette && _tail.sprite != null);
            var portrait = _portrait.rectTransform;
            if (!ShowsVignette)
            {
                portrait.anchoredPosition = new Vector2(70f - 500f - 6f, 182f);
                portrait.sizeDelta = new Vector2(188f, 188f);
                _pipRow.SetParent(_card.transform, false);
                UiFactory.Place(_pipRow, Vector2.zero, new Vector2(124f, 30f), new Vector2(300f, 24f), new Vector2(0f, 0.5f));
                for (int i = 0; i < _pips.Count; i++)
                {
                    var pip = _pips[i].rectTransform;
                    pip.anchoredPosition = new Vector2(10f + i * 32f, 0f);
                    pip.sizeDelta = new Vector2(18f, 18f);
                }

                return;
            }

            bool words = _showSpeechText;
            float height = words ? BubbleSize.y : StageInset * 2f + StageHeight;
            _bubble.sizeDelta = new Vector2(BubbleSize.x, height);
            _bubbleLine.gameObject.SetActive(words);
            portrait.anchoredPosition = new Vector2(-BubbleSize.x * 0.5f + 4f, BubbleBottom + 70f);
            portrait.sizeDelta = new Vector2(150f, 150f);
            _pipRow.SetParent(_bubble, false);
            UiFactory.Place(_pipRow, new Vector2(1f, 1f), new Vector2(-190f, -StageInset * 0.5f + 1f),
                new Vector2(120f, 20f), new Vector2(0f, 0.5f));
            for (int i = 0; i < _pips.Count; i++)
            {
                var pip = _pips[i].rectTransform;
                pip.anchoredPosition = new Vector2(10f + i * 28f, 0f);
                pip.sizeDelta = new Vector2(14f, 14f);
            }

            PlaceTail();
        }

        /// <summary>The tail rises from the bubble's top edge toward the speaker's head in the world.</summary>
        private void PlaceTail()
        {
            if (!_tail.gameObject.activeSelf)
            {
                return;
            }

            float top = BubbleBottom + _bubble.sizeDelta.y - 16f;
            float half = BubbleSize.x * 0.5f - 200f;
            float x = 0f;
            float angle = 0f;
            Vector3? anchor = SpeakerAnchor?.Invoke();
            if (anchor.HasValue && ViewCamera != null)
            {
                Vector3 screen = ViewCamera.WorldToScreenPoint(anchor.Value);
                var canvas = GetComponentInParent<Canvas>();
                Camera uiCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null;
                if (screen.z > 0f && RectTransformUtility.ScreenPointToLocalPointInRectangle(_content, screen, uiCamera,
                        out var local))
                {
                    // The content rect is stretched: measure from its bottom centre, like the bubble.
                    var rect = _content.rect;
                    float lx = local.x - rect.center.x;
                    float ly = local.y - rect.yMin;
                    x = Mathf.Clamp(lx, -half, half);
                    float rise = Mathf.Max(40f, ly - top);
                    angle = Mathf.Clamp(-Mathf.Atan2(lx - x, rise) * Mathf.Rad2Deg, -38f, 38f);
                }
            }

            var tail = _tail.rectTransform;
            tail.anchoredPosition = new Vector2(x, top);
            tail.localRotation = Quaternion.Euler(0f, 0f, angle);
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
            _face.sprite = PuppetAtlas.Get(FacePart(speaker));
            _face.enabled = _face.sprite != null;
            _initial.text = _face.enabled ? string.Empty : InitialOf(name);
            _initial.color = ink;
        }

        /// <summary>The face puppet part for a speaker (pg.bea, pg.vale, ...), null for captions.</summary>
        public static string FacePart(Speaker speaker)
        {
            switch (speaker)
            {
                case Speaker.Pip:
                    return "pg.pip";
                case Speaker.Bea:
                    return "pg.bea";
                case Speaker.Vale:
                    return "pg.vale";
                case Speaker.June:
                    return "pg.june";
                case Speaker.Kit:
                    return "pg.kit";
                default:
                    return null;
            }
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
                // Paper in: a small settle on each line (none with reduced motion).
                _flutter = _reducedMotion ? 1f : Mathf.Min(1f, _flutter + Time.unscaledDeltaTime / 0.18f);
                float s = Mathf.Lerp(0.96f, 1f, 1f - (1f - _flutter) * (1f - _flutter));
                _card.localScale = new Vector3(s, s, 1f);
                _bubble.localScale = new Vector3(s, s, 1f);
            }

            if (ShowsVignette)
            {
                PlaceTail();
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
