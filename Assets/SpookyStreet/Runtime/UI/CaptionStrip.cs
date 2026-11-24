using System;
using System.Collections.Generic;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Localization;
using SpookyStreet.UI.Nonverbal;
using UnityEngine;
using UnityEngine.UI;
using UiKeys = SpookyStreet.Core.Localization.LocalizationKeys.CaseUi;

namespace SpookyStreet.UI
{
    /// <summary>
    /// The felt caption strip (docs/ui-language.md "Captions and accessibility"): bracketed sound text and radio
    /// hints on opaque night felt, bottom-centre between the thumb clusters. A caption with a source gets a purple
    /// direction disc whose candy arrow turns toward the source as the camera moves, plus a direction word in the
    /// text, so the cue never depends on sound. Only directional sounds get the cue, and only when their source is away
    /// from Pip (<see cref="CaptionDirection.ShouldCue"/>). Text wraps to at most <see cref="MaxLines"/> lines: the strip
    /// grows to fit them, and only a caption that still needs a third line steps its size down (never below 13 pt).
    /// Captions queue and time out on game time (pause freezes them). A case 01 caption that has a vignette
    /// (<see cref="C01Vignettes.For"/>, S002) also plays it on a small night-felt stage that sits on the strip's top
    /// edge, so pre-readers see the sound as pictures (one hybrid reading mode, D23.1: the words stay). Hints and
    /// notebook notes have no vignette and show the strip alone.
    /// </summary>
    public sealed class CaptionStrip : MonoBehaviour
    {
        public const float MinSeconds = 3.5f;
        public const float SecondsPerCharacter = 0.06f;

        /// <summary>Captions wrap to two lines at most.</summary>
        public const int MaxLines = 2;

        /// <summary>13 pt at the reference (docs/ui-language.md: never shrink below 13 pt).</summary>
        public const int MinFontSize = 26;

        /// <summary>Strip height for one line (default and large text) and the padding around the text.</summary>
        public const float RestHeight = 92f;
        public const float LargeRestHeight = 116f;
        public const float VerticalPadding = 12f;

        /// <summary>The mini vignette stage above the strip: height, and width as a multiple of it (the bubble's stage shape).</summary>
        public const float StageHeight = 108f;
        public const float StageAspect = 4.6f;

        /// <summary>Text insets: left with and without the direction disc, and right.</summary>
        public const float CueInset = 96f;
        public const float PlainInset = 30f;
        public const float RightInset = 26f;

        /// <summary>The strip sits between the thumb clusters (stick ghost and Use + Jump take 820 units together).</summary>
        public const float ClusterReserve = 820f;
        public const float MinWidth = 480f;
        public const float MaxWidth = 720f;

        /// <summary>How a caption lays out in the strip.</summary>
        public readonly struct Fit
        {
            public Fit(int fontSize, int lines, float textHeight, float stripHeight, float textWidth)
            {
                FontSize = fontSize;
                Lines = lines;
                TextHeight = textHeight;
                StripHeight = stripHeight;
                TextWidth = textWidth;
            }

            public int FontSize { get; }
            public int Lines { get; }
            public float TextHeight { get; }
            public float StripHeight { get; }
            public float TextWidth { get; }

            /// <summary>The text is whole: within the line limit and inside the strip's padding.</summary>
            public bool Fits => Lines <= MaxLines && TextHeight <= StripHeight - 2f * VerticalPadding + 0.5f;

            public override string ToString() => $"{Lines} line(s) at {FontSize}, text {TextHeight:F0} in strip {StripHeight:F0}";
        }

        /// <summary>Night felt from the bone paper tag: bone × tint ≈ night violet (#2A2550).</summary>
        private static readonly Color NightTint = new Color(0.172f, 0.161f, 0.4f, 0.94f);

        /// <summary>Haunted purple from the bone felt disc.</summary>
        private static readonly Color PurpleTint = new Color(0.37f, 0.25f, 0.61f, 1f);

        private sealed class Entry
        {
            public string Key;
            public Func<Vector3?> Source;
            public float Seconds;
        }

        private readonly Queue<Entry> _queue = new Queue<Entry>();
        private RectTransform _root;
        private RectTransform _area;
        private Text _text;
        private GameObject _cue;
        private RectTransform _arrow;
        private Entry _current;
        private float _left;
        private bool _largeText;
        private bool _raised;
        private string _fitText;
        private float _fitWidth;
        private bool _fitCue;
        private Fit _fit;
        private readonly TextGenerator _generator = new TextGenerator();
        private Image _stagePanel;

        private const float RestY = 30f;
        private const float RaisedY = 300f;

        /// <summary>Height of the raised strip; the speech bubble (non-verbal UX) sets it above itself.</summary>
        public float RaisedHeight { get; set; } = RaisedY;

        /// <summary>Camera used for the direction cue (the gameplay camera).</summary>
        public Camera ViewCamera { get; set; }

        /// <summary>Where Pip is: a sound's source this close gets no direction cue.</summary>
        public Func<Vector3> Listener { get; set; }

        /// <summary>The layout of the caption on screen.</summary>
        public Fit CurrentFit => _fit;

        /// <summary>The strip (its height follows the caption's line count).</summary>
        public RectTransform Strip => _root;

        /// <summary>Text size hook (docs/ui-language.md: 17 pt default, 22 pt large). No settings screen yet.</summary>
        public bool LargeText
        {
            get => _largeText;
            set
            {
                _largeText = value;
                ApplySize();
            }
        }

        public bool Showing => _current != null;

        /// <summary>Opaque captions (settings, S004 stream E): the night backing is fully solid instead of 94 %.</summary>
        public bool Opaque
        {
            get => _opaque;
            set
            {
                _opaque = value;
                var backing = _root != null ? _root.GetComponent<Image>() : null;
                if (backing != null)
                {
                    var c = backing.color;
                    c.a = value ? 1f : NightTint.a;
                    backing.color = c;
                }
            }
        }

        private bool _opaque;

        /// <summary>The backing's alpha now (tests).</summary>
        public float BackingAlpha => _root != null && _root.GetComponent<Image>() != null ? _root.GetComponent<Image>().color.a : 0f;

        /// <summary>Lifts the strip above the speech card while a conversation is open, so both stay readable.</summary>
        public bool Raised
        {
            get => _raised;
            set
            {
                _raised = value;
                if (_root != null)
                {
                    _root.anchoredPosition = new Vector2(0f, value ? RaisedHeight : RestY);
                }
            }
        }

        /// <summary>Localization key of the caption on screen (null when nothing shows).</summary>
        public string CurrentKey => _current?.Key;

        /// <summary>The text currently on the strip.</summary>
        public string CurrentText => _current != null ? _text.text : null;

        /// <summary>True while the current caption shows a direction cue.</summary>
        public bool HasDirectionCue => _cue != null && _cue.activeSelf;

        /// <summary>The caption's mini stage (null before <see cref="Create"/>).</summary>
        public VignettePlayer Vignette { get; private set; }

        /// <summary>True while the current caption plays its vignette on the mini stage.</summary>
        public bool ShowsVignette => _stagePanel != null && _stagePanel.gameObject.activeSelf;

        /// <summary>Reduced motion for the mini stage (still beats with cross-fades).</summary>
        public bool ReducedMotion { get; set; }

        /// <summary>Signed angle (degrees, positive = right) the arrow currently points at.</summary>
        public float CueAngle { get; private set; }

        public static CaptionStrip Create(RectTransform parent)
        {
            var area = UiFactory.Rect(parent, "CaptionArea");
            UiFactory.Stretch(area);
            var strip = area.gameObject.AddComponent<CaptionStrip>();
            strip._area = area;
            strip.Build();
            return strip;
        }

        private void Build()
        {
            var backing = UiFactory.Sprite(_area, "CaptionStrip", null, Color.white, new Vector2(0.5f, 0f),
                new Vector2(0f, 30f), new Vector2(620f, 92f));
            UiFactory.ApplySurface(backing, UiSpriteCatalog.PaperTag, Palette.NightViolet, NightTint);
            backing.rectTransform.pivot = new Vector2(0.5f, 0f);
            backing.rectTransform.anchoredPosition = new Vector2(0f, 30f);
            UiFactory.InkShadow(backing, new Vector2(3f, -4f), 0.5f);
            UiFactory.StitchLayer(backing.transform, UiFactory.WithAlpha(Palette.CandyCornYellow, 0.8f));
            _root = backing.rectTransform;

            var cue = UiFactory.Sprite(_root, "DirectionCue", null, Color.white, new Vector2(0f, 0.5f),
                new Vector2(50f, 0f), new Vector2(68f, 68f));
            UiFactory.ApplySurface(cue, UiSpriteCatalog.FeltDiscBone, Palette.HauntedPurple, PurpleTint);
            var arrow = UiFactory.Sprite(cue.transform, "Arrow", UiFactory.Baked(UiSpriteCatalog.IconArrow),
                Palette.CandyCornYellow, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(44f, 44f));
            _arrow = arrow.rectTransform;
            _cue = cue.gameObject;

            _text = UiFactory.Label(_root, "Text", null, UiFactory.BodySize, TextAnchor.MiddleLeft, Palette.WarmPaper,
                UiFactory.BoldFont);
            UiFactory.Stretch(_text.rectTransform);
            // The strip grows to fit the lines (Fit), so the text is sized explicitly and never truncated.
            _text.resizeTextForBestFit = false;
            _text.verticalOverflow = VerticalWrapMode.Overflow;
            BuildStage();
            ApplySize();
            _root.gameObject.SetActive(false);
        }

        /// <summary>The mini stage: the bubble's night-felt panel (pg.stage) standing on the strip's top edge.</summary>
        private void BuildStage()
        {
            _stagePanel = UiFactory.Sprite(_root, "Stage", PuppetAtlas.Get(PuppetParts.Stage), new Color(0.2f, 0.18f, 0.38f, 1f),
                new Vector2(0.5f, 1f), new Vector2(0f, 4f), new Vector2(StageHeight * StageAspect, StageHeight));
            _stagePanel.rectTransform.pivot = new Vector2(0.5f, 0f);
            if (_stagePanel.sprite != null)
            {
                _stagePanel.color = Color.white;
            }

            UiFactory.InkShadow(_stagePanel, new Vector2(3f, -4f), 0.4f);
            var inner = UiFactory.Stretch(UiFactory.Rect(_stagePanel.transform, "Inner"));
            inner.offsetMin = new Vector2(6f, 6f);
            inner.offsetMax = new Vector2(-6f, -6f);
            Vignette = VignettePlayer.Create(inner, "Vignette");
            Vignette.CanRun = () => Time.timeScale > 0f; // captions run on game time: pause freezes the stage too
            _stagePanel.gameObject.SetActive(false);
        }

        /// <summary>Starts the caption's vignette on the mini stage, or hides the stage (hints, notes, no atlas).</summary>
        private void PlayVignette(string key)
        {
            var visual = C01Vignettes.For(key);
            var vignette = visual != null && !visual.UsesV1Strip && PuppetAtlas.Available
                ? C01Vignettes.Get(visual.VignetteId)
                : null;
            _stagePanel.gameObject.SetActive(vignette != null);
            Vignette.ReducedMotion = ReducedMotion;
            Vignette.Play(vignette);
        }

        private void ApplySize()
        {
            if (_text == null)
            {
                return;
            }

            _fitText = null;
            if (_current != null)
            {
                Refresh();
            }
        }

        /// <summary>Strip width between the thumb clusters for an area this wide.</summary>
        public static float WidthFor(float areaWidth) => Mathf.Clamp(areaWidth - ClusterReserve, MinWidth, MaxWidth);

        /// <summary>
        /// Lays <paramref name="text"/> out in the strip at its current width: the largest size from the default (or large)
        /// size down to <see cref="MinFontSize"/> that needs at most <see cref="MaxLines"/> lines, and the strip height
        /// those lines need.
        /// </summary>
        public Fit Measure(string text, bool cue)
        {
            float width = Mathf.Max(10f, StripWidth - (cue ? CueInset : PlainInset) - RightInset);
            int max = _largeText ? 44 : UiFactory.BodySize;
            float rest = _largeText ? LargeRestHeight : RestHeight;
            var settings = _text.GetGenerationSettings(new Vector2(width, 4000f));
            settings.resizeTextForBestFit = false;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            settings.horizontalOverflow = HorizontalWrapMode.Wrap;
            int size = max;
            int lines = 1;
            float height = 0f;
            for (; ; size -= 2)
            {
                size = Mathf.Max(size, MinFontSize);
                settings.fontSize = size;
                settings.resizeTextMaxSize = size;
                settings.resizeTextMinSize = size;
                _generator.Populate(text ?? string.Empty, settings);
                lines = Mathf.Max(1, _generator.lineCount);
                height = _generator.GetPreferredHeight(text ?? string.Empty, settings) / Mathf.Max(0.01f, _text.pixelsPerUnit);
                if (lines <= MaxLines || size <= MinFontSize)
                {
                    break;
                }
            }

            float strip = Mathf.Max(rest, Mathf.Ceil(height + 2f * VerticalPadding));
            return new Fit(size, lines, height, strip, width);
        }

        private float StripWidth => WidthFor(_area.rect.width);

        /// <summary>Queues a localized caption; <paramref name="source"/> (world position) adds the direction cue.</summary>
        public void ShowKey(string key, Func<Vector3?> source = null)
        {
            string text = Localizer.Get(key);
            Enqueue(new Entry { Key = key, Source = source, Seconds = DurationFor(text) });
        }

        /// <summary>Shows a text now (hints, notebook notes), replacing whatever is on the strip.</summary>
        public void ShowNow(string key)
        {
            _queue.Clear();
            _current = null;
            ShowKey(key);
        }

        /// <summary>
        /// Drops the captions of sounds heard from a place (those with a direction source) from the strip and its
        /// queue: after a threshold crossing they belong to the place Pip left (D26: captions describe what Pip
        /// perceives, and the street's knocks are not heard in the owl house's rooms).
        /// </summary>
        public void DropSounds()
        {
            int count = _queue.Count;
            for (int i = 0; i < count; i++)
            {
                var entry = _queue.Dequeue();
                if (entry.Source == null)
                {
                    _queue.Enqueue(entry);
                }
            }

            if (_current != null && _current.Source != null)
            {
                Next();
            }
        }

        public void Clear()
        {
            _queue.Clear();
            _current = null;
            Vignette.Stop();
            _stagePanel.gameObject.SetActive(false);
            _root.gameObject.SetActive(false);
        }

        public static float DurationFor(string text) =>
            Mathf.Max(MinSeconds, (text != null ? text.Length : 0) * SecondsPerCharacter);

        private void Enqueue(Entry entry)
        {
            _queue.Enqueue(entry);
            if (_current == null)
            {
                Next();
            }
        }

        private void Next()
        {
            if (_queue.Count == 0)
            {
                _current = null;
                Vignette.Stop();
                _stagePanel.gameObject.SetActive(false);
                _root.gameObject.SetActive(false);
                return;
            }

            _current = _queue.Dequeue();
            _left = _current.Seconds;
            _root.gameObject.SetActive(true);
            PlayVignette(_current.Key);
            Refresh();
        }

        private void Update()
        {
            if (_current == null)
            {
                return;
            }

            Refresh();
            _left -= Time.deltaTime;
            if (_left <= 0f)
            {
                Next();
            }
        }

        private void Refresh()
        {
            string text = Localizer.Get(_current.Key);
            Vector3? source = _current.Source != null ? _current.Source() : null;
            bool cue = false;
            if (source.HasValue && ViewCamera != null)
            {
                float distance = float.MaxValue;
                if (Listener != null)
                {
                    Vector3 to = source.Value - Listener();
                    to.y = 0f;
                    distance = to.magnitude;
                }

                cue = CaptionDirection.ShouldCue(_current.Key, true, distance);
            }

            if (cue)
            {
                Vector3 forward = ViewCamera.transform.forward;
                Vector3 to = source.Value - ViewCamera.transform.position;
                CueAngle = CaptionDirection.SignedAngle(forward.x, forward.z, to.x, to.z);
                _arrow.localRotation = Quaternion.Euler(0f, 0f, -CueAngle);
                string word = Localizer.Get(CaptionDirection.WordKey(CaptionDirection.BearingOf(CueAngle)));
                text = Localizer.Format(UiKeys.CaptionWithDirection, text, word);
            }

            if (_cue.activeSelf != cue)
            {
                _cue.SetActive(cue);
            }

            float width = StripWidth;
            if (text != _fitText || cue != _fitCue || Mathf.Abs(width - _fitWidth) > 0.5f)
            {
                _fitText = text;
                _fitCue = cue;
                _fitWidth = width;
                _root.sizeDelta = new Vector2(width, _root.sizeDelta.y);
                _fit = Measure(text, cue);
                _root.sizeDelta = new Vector2(width, _fit.StripHeight);
                _text.fontSize = _fit.FontSize;
                _text.rectTransform.offsetMin = new Vector2(cue ? CueInset : PlainInset, VerticalPadding);
                _text.rectTransform.offsetMax = new Vector2(-RightInset, -VerticalPadding);
            }

            if (_text.text != text)
            {
                _text.text = text;
            }
        }
    }
}
