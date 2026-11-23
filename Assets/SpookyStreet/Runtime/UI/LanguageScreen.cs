using System;
using System.Collections.Generic;
using SpookyStreet.App;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.Localization;
using SpookyStreet.Localization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using K = SpookyStreet.Core.Localization.LocalizationKeys.Languages;

namespace SpookyStreet.UI
{
    /// <summary>
    /// The language screen (S004 §2.4, stream D1), opened from the globe on the title and the pause card: every
    /// language on the device with its native name, where it comes from and a Use button (no gate). "More languages"
    /// asks for the parent gate, then fetches the host's catalog; Download and Remove sit behind that gate. A modal
    /// paper card over a night-violet scrim, on its own canvas just under the gate's.
    /// Navigation hook for the S004 F1 base: <see cref="FocusOrder"/>, <see cref="DefaultFocus"/> and
    /// <see cref="Back"/>; buttons also get explicit up/down navigation so a gamepad works today.
    /// </summary>
    public sealed class LanguageScreen : MonoBehaviour
    {
        /// <summary>Above the game UI (10) and the case UI, under the parent gate (16) so the gate shows on top.</summary>
        public const int SortingOrder = 14;

        public const int MaxRows = 4;
        private const float CardWidth = 900f;
        private const float CardHeight = 680f;
        private const float RowTop = -128f;
        private const float RowHeight = 104f;

        private readonly List<Row> _rows = new List<Row>();
        private readonly List<Selectable> _focus = new List<Selectable>();
        private LanguageController _languages;
        private Func<Action, bool> _requireParent;
        private RectTransform _card;
        private RectTransform _rowsRoot;
        private Text _status;
        private bool _downloadsUnlocked;
        private bool _dirty;
        private GameObject _returnFocus;

        /// <summary>One language row, for tests and the F1 focus graph.</summary>
        public sealed class Row
        {
            public string Locale;
            public Text Name;
            public Text Note;
            public Button Use;
            public GameObject InUseTag;
            public Button Download;
            public Button Remove;
            public Image Progress;
            public GameObject Root;
        }

        public Canvas Canvas { get; private set; }

        public bool IsOpen => Canvas != null && Canvas.gameObject.activeSelf;

        public Button CloseButton { get; private set; }

        public Button MoreButton { get; private set; }

        /// <summary>The line under the rows (looking, offline, none), or empty.</summary>
        public Text Status => _status;

        /// <summary>A grown-up passed the gate since the screen opened: the catalog's packs can be downloaded.</summary>
        public bool DownloadsUnlocked => _downloadsUnlocked;

        public IReadOnlyList<Row> Rows => _rows;

        /// <summary>Top-to-bottom focus order (rows' buttons, More languages, Close) for gamepad and keyboard.</summary>
        public IReadOnlyList<Selectable> FocusOrder => _focus;

        /// <summary>The first button top-down (a language's Use, else More languages, else Close).</summary>
        public Selectable DefaultFocus => _focus.Count > 0 ? _focus[0] : null;

        public event Action Opened;

        public event Action Closed;

        /// <param name="requireParent">Shows the parent gate and runs the action once a grown-up passed it; returns
        /// false when no gate is available (then nothing guarded happens).</param>
        public static LanguageScreen Create(Transform parent, LanguageController languages, Func<Action, bool> requireParent)
        {
            var canvas = UiFactory.CreateCanvas("LanguageScreen", SortingOrder);
            canvas.transform.SetParent(parent, false);
            var screen = canvas.gameObject.AddComponent<LanguageScreen>();
            screen.Canvas = canvas;
            screen._languages = languages;
            screen._requireParent = requireParent;
            screen.Build();
            languages.Packs.Changed += screen.MarkDirty;
            languages.LanguageChanged += screen.MarkDirty;
            canvas.gameObject.SetActive(false);
            return screen;
        }

        public Row RowFor(string locale)
        {
            foreach (var row in _rows)
            {
                if (string.Equals(row.Locale, locale, StringComparison.OrdinalIgnoreCase))
                {
                    return row;
                }
            }

            return null;
        }

        public void Open()
        {
            _returnFocus = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            _downloadsUnlocked = false;
            Canvas.gameObject.SetActive(true);
            Rebuild();
            Select(DefaultFocus);
            Opened?.Invoke();
        }

        /// <summary>Back / Close: hides the screen and gives focus back to what opened it.</summary>
        public void Back()
        {
            if (!IsOpen)
            {
                return;
            }

            Canvas.gameObject.SetActive(false);
            _downloadsUnlocked = false;
            if (_returnFocus != null && _returnFocus.activeInHierarchy)
            {
                Select(_returnFocus.GetComponent<Selectable>());
            }

            Closed?.Invoke();
        }

        /// <summary>"More languages": the parent gate, then the host's catalog (the only network call it makes).</summary>
        public void RequestMore()
        {
            if (_downloadsUnlocked)
            {
                _languages.Packs.FetchCatalog();
                return;
            }

            _requireParent?.Invoke(() =>
            {
                _downloadsUnlocked = true;
                _languages.Packs.FetchCatalog();
                MarkDirty();
            });
        }

        /// <summary>Remove asks for the gate every time.</summary>
        public void RequestRemove(string locale)
        {
            _requireParent?.Invoke(() => _languages.Remove(locale));
        }

        private void OnDestroy()
        {
            if (_languages != null)
            {
                _languages.Packs.Changed -= MarkDirty;
                _languages.LanguageChanged -= MarkDirty;
            }
        }

        private void MarkDirty() => _dirty = true;

        private void LateUpdate()
        {
            if (_dirty && IsOpen)
            {
                Rebuild();
            }
        }

        private void Build()
        {
            var scrim = UiFactory.Panel(Canvas.transform, "Scrim", UiFactory.WithAlpha(Palette.NightViolet, 0.86f));
            var content = UiFactory.Stretch(UiFactory.Rect(scrim.transform, "Content"));
            content.gameObject.AddComponent<SafeAreaFitter>();
            var cardImage = UiFactory.PaperCard(content, "LanguageCard", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(CardWidth, CardHeight), -0.8f);
            _card = cardImage.rectTransform;
            var top = new Vector2(0.5f, 1f);

            var globe = UiFactory.Sprite(_card, "Globe", UiFactory.Baked(UiSpriteCatalog.IconGlobe), UiFactory.Ink,
                new Vector2(0f, 1f), new Vector2(70f, -64f), new Vector2(56f, 56f));
            globe.raycastTarget = false;
            var heading = UiFactory.Label(_card, "Heading", K.Heading, 56, TextAnchor.MiddleCenter, UiFactory.Ink,
                UiFactory.DisplayFont);
            UiFactory.Place(heading.rectTransform, top, new Vector2(0f, -64f), new Vector2(560f, 80f));

            var close = UiFactory.FeltDisc(_card, "CloseLanguages", UiSpriteCatalog.FeltDiscNight, Palette.WarmPaper,
                UiSpriteCatalog.IconClose, Palette.WarmPaper, null, UiFactory.SystemDiscDiameter, Vector2.one,
                new Vector2(-34f, -34f), 1f);
            CloseButton = close.gameObject.AddComponent<Button>();
            CloseButton.targetGraphic = close;
            CloseButton.transition = Selectable.Transition.None;
            CloseButton.onClick.AddListener(Back);

            _rowsRoot = UiFactory.Stretch(UiFactory.Rect(_card, "Rows"));

            _status = UiFactory.Label(_card, "Status", null, UiFactory.CaptionSize, TextAnchor.MiddleCenter,
                UiFactory.WithAlpha(UiFactory.Ink, 0.85f), UiFactory.BoldFont);
            UiFactory.Place(_status.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 136f), new Vector2(780f, 46f));
            _status.verticalOverflow = VerticalWrapMode.Overflow;

            MoreButton = UiFactory.SecondaryButton(_card, "MoreLanguages", K.More, new Vector2(0.5f, 0f),
                new Vector2(0f, 70f), new Vector2(440f, 96f), RequestMore, UiSpriteCatalog.IconGlobe);
        }

        private void Rebuild()
        {
            _dirty = false;
            string keepLocale = null;
            string keepButton = null;
            var current = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            foreach (var row in _rows)
            {
                if (current != null && current.transform.parent == row.Root.transform)
                {
                    keepLocale = row.Locale;
                    keepButton = current.name;
                }
            }

            foreach (var row in _rows)
            {
                row.Root.SetActive(false);
                Destroy(row.Root);
            }

            _rows.Clear();
            _focus.Clear();
            var packs = _languages.Packs;
            var options = new List<LanguageOption>();
            foreach (var option in packs.Options)
            {
                // Catalog languages appear only once a grown-up opened the download list.
                if (option.Source != LanguageSource.Available || _downloadsUnlocked)
                {
                    options.Add(option);
                }
            }

            for (int i = 0; i < options.Count && i < MaxRows; i++)
            {
                _rows.Add(BuildRow(options[i], i));
            }

            foreach (var row in _rows)
            {
                AddFocus(row.Use);
                AddFocus(row.Download);
                AddFocus(row.Remove);
            }

            bool downloads = packs.DownloadsConfigured;
            MoreButton.gameObject.SetActive(downloads);
            if (downloads)
            {
                AddFocus(MoreButton);
            }

            AddFocus(CloseButton);
            LinkNavigation();
            RefreshStatus(packs, options);
            if (EventSystem.current != null && IsOpen)
            {
                // Keep focus on the same row's same button across a rebuild (download progress rebuilds the rows).
                var kept = keepLocale != null ? RowFor(keepLocale) : null;
                var again = kept != null ? kept.Root.transform.Find(keepButton) : null;
                if (again != null && again.GetComponent<Selectable>() != null)
                {
                    Select(again.GetComponent<Selectable>());
                }
                else if (keepLocale != null && kept != null)
                {
                    Select(kept.Use != null ? kept.Use : kept.Download != null ? kept.Download : (Selectable)kept.Remove);
                }

                var selected = EventSystem.current.currentSelectedGameObject;
                if (selected == null || !selected.activeInHierarchy)
                {
                    Select(DefaultFocus);
                }
            }
        }

        private void RefreshStatus(LanguagePacks packs, List<LanguageOption> shown)
        {
            var text = _status.GetComponent<LocalizedText>();
            if (text == null)
            {
                text = _status.gameObject.AddComponent<LocalizedText>();
            }
            string key = null;
            if (_downloadsUnlocked)
            {
                bool anyAvailable = false;
                foreach (var option in shown)
                {
                    anyAvailable |= option.Source == LanguageSource.Available;
                }

                if (packs.CatalogBusy)
                {
                    key = K.Checking;
                }
                else if (packs.CatalogError != PackError.None)
                {
                    key = K.Offline;
                }
                else if (packs.Catalog != null && !anyAvailable)
                {
                    key = K.None;
                }
            }

            _status.gameObject.SetActive(key != null);
            if (key != null)
            {
                text.SetKey(key);
            }
        }

        private Row BuildRow(LanguageOption option, int index)
        {
            float y = RowTop - index * RowHeight;
            var root = UiFactory.Rect(_rowsRoot, "Row_" + option.Locale);
            UiFactory.Place(root, new Vector2(0.5f, 1f), new Vector2(0f, y), new Vector2(CardWidth - 80f, RowHeight - 8f),
                new Vector2(0.5f, 1f));
            bool active = string.Equals(option.Locale, _languages.ActiveLocale, StringComparison.OrdinalIgnoreCase);
            var row = new Row { Locale = option.Locale, Root = root.gameObject };
            if (index > 0)
            {
                var rule = UiFactory.Sprite(root, "Rule", null, UiFactory.WithAlpha(Palette.HauntedPurple, 0.35f),
                    new Vector2(0.5f, 1f), new Vector2(0f, 2f), new Vector2(CardWidth - 120f, 3f));
                rule.raycastTarget = false;
            }

            var left = new Vector2(0f, 1f);
            row.Name = UiFactory.Label(root, "Name", null, 38, TextAnchor.MiddleLeft, UiFactory.Ink, UiFactory.BoldFont);
            UiFactory.Place(row.Name.rectTransform, left, new Vector2(12f, -4f), new Vector2(290f, 48f), left);
            row.Name.text = option.NativeName; // the language's own name, from its table: never translated
            row.Note = UiFactory.Label(root, "Note", null, UiFactory.CaptionSize, TextAnchor.MiddleLeft,
                UiFactory.WithAlpha(UiFactory.Ink, 0.75f), UiFactory.BoldFont);
            UiFactory.Place(row.Note.rectTransform, left, new Vector2(12f, -50f), new Vector2(300f, 40f), left);
            row.Note.verticalOverflow = VerticalWrapMode.Overflow; // one caption line; never clipped to nothing
            var note = row.Note.gameObject.AddComponent<LocalizedText>();
            long kb = Math.Max(1, (option.Size + 1023) / 1024);
            if (option.Downloading)
            {
                int percent = Mathf.RoundToInt(option.Progress * 100f);
                note.SetKey(K.Downloading, () => new object[] { percent });
                var track = UiFactory.Sprite(root, "ProgressTrack", null, UiFactory.WithAlpha(Palette.HauntedPurple, 0.25f),
                    left, new Vector2(12f, -92f), new Vector2(300f, 8f));
                track.rectTransform.pivot = left;
                track.rectTransform.anchoredPosition = new Vector2(12f, -92f);
                row.Progress = UiFactory.Sprite(track.transform, "Fill", null, Palette.JackOrange,
                    new Vector2(0f, 0.5f), Vector2.zero, new Vector2(300f * Mathf.Clamp01(option.Progress), 8f));
                row.Progress.rectTransform.pivot = new Vector2(0f, 0.5f);
                row.Progress.rectTransform.anchoredPosition = Vector2.zero;
            }
            else if (option.LastError != PackError.None)
            {
                note.SetKey(K.Failed);
            }
            else if (option.Source == LanguageSource.Installed)
            {
                note.SetKey(K.Downloaded, () => new object[] { Localizer.Format(K.SizeKb, kb) });
            }
            else if (option.Source == LanguageSource.Available)
            {
                note.SetKey(K.SizeKb, () => new object[] { kb });
            }
            else
            {
                note.SetKey(K.InGame);
            }

            var right = new Vector2(1f, 0.5f);
            if (option.Usable)
            {
                if (active)
                {
                    var tag = UiFactory.PaperTag(root, "InUse", K.InUse, 30, right, new Vector2(-130f, 0f),
                        new Vector2(240f, 72f), 1.2f, Palette.CandyCornYellow);
                    var check = UiFactory.Sprite(tag.transform, "Check", UiFactory.Baked(UiSpriteCatalog.IconCheck),
                        UiFactory.Ink, new Vector2(0f, 0.5f), new Vector2(26f, 0f), new Vector2(34f, 34f));
                    check.raycastTarget = false;
                    row.InUseTag = tag.gameObject;
                }
                else
                {
                    string locale = option.Locale;
                    row.Use = UiFactory.SecondaryButton(root, "Use", K.Use, right, new Vector2(-130f, 0f),
                        new Vector2(240f, 96f), () => _languages.Use(locale), UiSpriteCatalog.IconCheck);
                }
            }

            bool unlocked = _downloadsUnlocked && !option.Downloading;
            if (unlocked && (option.Source == LanguageSource.Available || option.Outdated))
            {
                string locale = option.Locale;
                float x = option.Source == LanguageSource.Available ? -130f : -395f;
                row.Download = UiFactory.SecondaryButton(root, "Download", option.Outdated ? K.Update : K.Download, right,
                    new Vector2(x, 0f), new Vector2(option.Source == LanguageSource.Available ? 240f : 230f, 96f),
                    () => _languages.Download(locale), UiSpriteCatalog.IconArrow);
            }
            else if (option.Removable && !option.Downloading && row.Download == null)
            {
                string locale = option.Locale;
                row.Remove = UiFactory.SecondaryButton(root, "Remove", K.Remove, right, new Vector2(-395f, 0f),
                    new Vector2(230f, 96f), () => RequestRemove(locale), UiSpriteCatalog.IconClose);
            }

            return row;
        }

        private void AddFocus(Selectable selectable)
        {
            if (selectable != null && selectable.gameObject.activeSelf)
            {
                _focus.Add(selectable);
            }
        }

        private void LinkNavigation()
        {
            for (int i = 0; i < _focus.Count; i++)
            {
                var nav = new Navigation
                {
                    mode = Navigation.Mode.Explicit,
                    selectOnUp = _focus[(i - 1 + _focus.Count) % _focus.Count],
                    selectOnDown = _focus[(i + 1) % _focus.Count],
                };
                _focus[i].navigation = nav;
            }
        }

        private static void Select(Selectable selectable)
        {
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(selectable != null ? selectable.gameObject : null);
            }
        }
    }
}
