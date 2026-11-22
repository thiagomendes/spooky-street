using System;
using System.Collections.Generic;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.Case;
using SpookyStreet.Localization;
using UnityEngine;
using UnityEngine.UI;
using UiKeys = SpookyStreet.Core.Localization.LocalizationKeys.CaseUi;

namespace SpookyStreet.UI
{
    /// <summary>
    /// The felt caption strip (docs/ui-language.md "Captions and accessibility"): bracketed sound text and radio
    /// hints on opaque night felt, bottom-centre between the thumb clusters. A caption with a source gets a purple
    /// direction disc whose candy arrow turns toward the source as the camera moves, plus a direction word in the
    /// text, so the cue never depends on sound. Captions queue and time out on game time (pause freezes them).
    /// </summary>
    public sealed class CaptionStrip : MonoBehaviour
    {
        public const float MinSeconds = 3.5f;
        public const float SecondsPerCharacter = 0.06f;

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

        private const float RestY = 30f;
        private const float RaisedY = 300f;

        /// <summary>Camera used for the direction cue (the gameplay camera).</summary>
        public Camera ViewCamera { get; set; }

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

        /// <summary>Lifts the strip above the speech card while a conversation is open, so both stay readable.</summary>
        public bool Raised
        {
            get => _raised;
            set
            {
                _raised = value;
                if (_root != null)
                {
                    _root.anchoredPosition = new Vector2(0f, value ? RaisedY : RestY);
                }
            }
        }

        /// <summary>Localization key of the caption on screen (null when nothing shows).</summary>
        public string CurrentKey => _current?.Key;

        /// <summary>The text currently on the strip.</summary>
        public string CurrentText => _current != null ? _text.text : null;

        /// <summary>True while the current caption shows a direction cue.</summary>
        public bool HasDirectionCue => _cue != null && _cue.activeSelf;

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
            ApplySize();
            _root.gameObject.SetActive(false);
        }

        private void ApplySize()
        {
            if (_text == null)
            {
                return;
            }

            int size = _largeText ? 44 : UiFactory.BodySize;
            _text.fontSize = size;
            _text.resizeTextMaxSize = size;
            _text.resizeTextMinSize = 26;
            _root.sizeDelta = new Vector2(_root.sizeDelta.x, _largeText ? 116f : 92f);
        }

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

        public void Clear()
        {
            _queue.Clear();
            _current = null;
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
                _root.gameObject.SetActive(false);
                return;
            }

            _current = _queue.Dequeue();
            _left = _current.Seconds;
            _root.gameObject.SetActive(true);
            Refresh();
        }

        private void Update()
        {
            if (_current == null)
            {
                return;
            }

            // Fit between the thumb clusters (stick ghost ≈ 330 units from one edge, Use + Jump ≈ 400 from the other).
            float width = Mathf.Clamp(_area.rect.width - 820f, 480f, 720f);
            if (Mathf.Abs(_root.sizeDelta.x - width) > 0.5f)
            {
                _root.sizeDelta = new Vector2(width, _root.sizeDelta.y);
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
            bool cue = source.HasValue && ViewCamera != null;
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

            _text.rectTransform.offsetMin = new Vector2(cue ? 100f : 30f, 8f);
            _text.rectTransform.offsetMax = new Vector2(-26f, -8f);
            if (_text.text != text)
            {
                _text.text = text;
            }
        }
    }
}
