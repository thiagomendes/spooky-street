using System;
using System.Collections.Generic;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Localization;
using SpookyStreet.UI.Nonverbal;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using HintKeys = SpookyStreet.Core.Localization.LocalizationKeys.Hints;
using NoteKeys = SpookyStreet.Core.Localization.LocalizationKeys.Notebook;
using UiKeys = SpookyStreet.Core.Localization.LocalizationKeys.CaseUi;

namespace SpookyStreet.UI
{
    /// <summary>
    /// Pip's notebook, animated (docs/nonverbal-ux.md §5, docs/evidence/s002/nonverbal_v2/nv2_notebook.gif): one
    /// question per bone-paper page in a swipeable carousel (edge arrows, dot pips, neighbours peeking). A page holds
    /// only the order badge (dots, no digits), the question as three picture chips, the answer strip and the clue
    /// stickers. A proven answer is a tiny three-panel comic: tapping it plays
    /// <see cref="C01Vignettes.AnswerStripFor"/> twice and holds the end (green check); an unknown one shows dashed
    /// panels with a pencil "?". Found clues are stickers that wiggle in the first time they appear; tapping one shows
    /// its note in the caption strip, and stickers with a memory replay carry an eye that replays it on demand. The
    /// question and answer words always show under the pictures (one hybrid reading mode, D23.1). Once the case is closed
    /// a last page holds the final card. Everything is rebuilt from <see cref="CaseNotebook.Build"/> on each open, so
    /// no display text is stored.
    /// </summary>
    public sealed class NotebookView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private static readonly Color Graphite = new Color32(0x4A, 0x44, 0x58, 0xFF);
        private static readonly Color WitchGreen = new Color32(0x8D, 0xBF, 0x3F, 0xFF);

        public static readonly Vector2 PageSize = new Vector2(780f, 480f);
        public const float PageSpacing = 900f;
        public const float StripWidth = 690f;
        public const float StripHeight = 170f;
        public const float StickerWiggleSeconds = 0.8f;

        private readonly List<Button> _slips = new List<Button>();
        private readonly List<RectTransform> _pages = new List<RectTransform>();
        private readonly List<Image> _dots = new List<Image>();
        private readonly List<Image[]> _stitches = new List<Image[]>();
        private static readonly Color PanelThread = new Color(0.36f, 0.23f, 0.48f, 0.45f);
        private readonly HashSet<string> _shownStickers = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<(RectTransform Rect, float Start, float Angle)> _wiggles =
            new List<(RectTransform, float, float)>();

        private RectTransform _viewport;
        private RectTransform _track;
        private RectTransform _dotRow;
        private Button _prev;
        private Button _next;
        private float _trackX;
        private float _dragStart;
        private bool _dragging;

        /// <summary>Current case state (set by the director).</summary>
        public Func<CaseState> State { get; set; }

        /// <summary>Shows a localized note or hint in the caption strip.</summary>
        public Action<string> ShowCaption { get; set; }

        /// <summary>Asks the radio for the next hint (returns its key).</summary>
        public Func<string> RequestHint { get; set; }

        /// <summary>"Replay case" was pressed (the director asks for confirmation).</summary>
        public Action ReplayRequested { get; set; }

        /// <summary>Seen memory replays in this save (the eye shows only for replays already offered).</summary>
        public Func<IReadOnlyCollection<string>> SeenReplays { get; set; }

        /// <summary>The eye on a sticker: play this memory replay now (on demand, nothing is recorded).</summary>
        public Action<MemoryReplayDefinition> WatchReplay { get; set; }


        /// <summary>Gate for strip animation time (pause); null always runs.</summary>
        public Func<bool> CanAnimate { get; set; }

        public bool ReducedMotion { get; set; }

        public event Action Closed;

        public bool IsOpen => gameObject.activeSelf;

        public Button CloseButton { get; private set; }

        public Button HintButton { get; private set; }

        public Button BackToStreetButton { get; private set; }

        public Button ReplayButton { get; private set; }

        /// <summary>The final card (null until the case is closed and the notebook is open).</summary>
        public GameObject FinalCard { get; private set; }

        /// <summary>Question texts, one per question page, in order (tests).</summary>
        public List<Text> QuestionLabels { get; } = new List<Text>();

        /// <summary>Answer labels (null entries for unknown questions).</summary>
        public List<Text> AnswerLabels { get; } = new List<Text>();

        /// <summary>The answer strip player per question (null while unknown).</summary>
        public List<VignettePlayer> AnswerStrips { get; } = new List<VignettePlayer>();

        /// <summary>The tap targets over the answer strips (null while unknown).</summary>
        public List<Button> StripButtons { get; } = new List<Button>();

        /// <summary>Found clue stickers (tap shows their note).</summary>
        public IReadOnlyList<Button> FoundSlips => _slips;

        /// <summary>Eye buttons on stickers that offer a memory replay.</summary>
        public List<Button> ReplayEyes { get; } = new List<Button>();

        public int PageCount => _pages.Count;

        public int CurrentPage { get; private set; }

        public static NotebookView Create(RectTransform canvasRoot)
        {
            var scrim = UiFactory.Panel(canvasRoot, "Notebook", UiFactory.WithAlpha(Palette.NightViolet, 0.86f));
            var view = scrim.gameObject.AddComponent<NotebookView>();
            view.Build(scrim.rectTransform);
            scrim.gameObject.SetActive(false);
            return view;
        }

        private void Build(RectTransform root)
        {
            var content = UiFactory.Stretch(UiFactory.Rect(root, "Content"));
            content.gameObject.AddComponent<SafeAreaFitter>();
            _viewport = UiFactory.Stretch(UiFactory.Rect(content, "Carousel"));
            _track = UiFactory.Rect(_viewport, "Pages");
            UiFactory.Place(_track, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), PageSize);

            _dotRow = UiFactory.Rect(content, "PageDots");
            UiFactory.Place(_dotRow, new Vector2(0.5f, 0.5f), new Vector2(0f, -PageSize.y * 0.5f - 8f), new Vector2(200f, 24f));

            _prev = Disc(content, "PreviousPage", UiSpriteCatalog.IconArrow, new Vector2(0f, 0.5f), new Vector2(70f, 20f),
                () => ShowPage(CurrentPage - 1));
            // The arrow glyph points up: turn it to point left and right.
            _prev.transform.Find("Icon").localRotation = Quaternion.Euler(0f, 0f, 90f);
            _next = Disc(content, "NextPage", UiSpriteCatalog.IconArrow, new Vector2(1f, 0.5f), new Vector2(-70f, 20f),
                () => ShowPage(CurrentPage + 1));
            _next.transform.Find("Icon").localRotation = Quaternion.Euler(0f, 0f, -90f);

            CloseButton = Disc(content, "CloseButton", UiSpriteCatalog.IconClose, Vector2.one, new Vector2(-60f, -58f), Close);
            HintButton = UiFactory.PrimaryButton(content, "HintButton", HintKeys.HudHint, new Vector2(1f, 0f),
                new Vector2(-150f, 58f), new Vector2(256f, 96f), OnHint, UiSpriteCatalog.IconRadio);
            HintButton.transform.localRotation = Quaternion.Euler(0f, 0f, -1.5f);
        }

        private static Button Disc(Transform parent, string name, string icon, Vector2 anchor, Vector2 position,
            UnityEngine.Events.UnityAction onClick)
        {
            var disc = UiFactory.FeltDisc(parent, name, UiSpriteCatalog.FeltDiscNight, Palette.WarmPaper, icon,
                Palette.WarmPaper, null, UiFactory.SystemDiscDiameter, anchor, position, 1f);
            var button = disc.gameObject.AddComponent<Button>();
            button.targetGraphic = disc;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(onClick);
            return button;
        }

        public void Open()
        {
            // Stays under the caption strip (a later sibling), so hints and notes read over the pages.
            gameObject.SetActive(true);
            Rebuild();
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(CloseButton.gameObject);
            }
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        /// <summary>Closes without raising <see cref="Closed"/> (back to title).</summary>
        public void Cancel() => gameObject.SetActive(false);

        /// <summary>Forgets which stickers already wiggled in (a fresh case).</summary>
        public void ResetStickers() => _shownStickers.Clear();

        private void OnHint()
        {
            string key = RequestHint?.Invoke();
            if (key != null)
            {
                ShowCaption?.Invoke(key);
            }
        }

        /// <summary>Rebuilds every page from the current state and shows the first page (the final card once closed).</summary>
        public void Rebuild()
        {
            var state = State != null ? State() : CaseState.Empty;
            var page = CaseNotebook.Build(state);
            foreach (var old in _pages)
            {
                Destroy(old.gameObject);
            }

            foreach (var dot in _dots)
            {
                Destroy(dot.gameObject);
            }

            _pages.Clear();
            _dots.Clear();
            _slips.Clear();
            _wiggles.Clear();
            QuestionLabels.Clear();
            AnswerLabels.Clear();
            AnswerStrips.Clear();
            StripButtons.Clear();
            _stitches.Clear();
            ReplayEyes.Clear();
            FinalCard = null;
            BackToStreetButton = null;
            ReplayButton = null;

            const bool words = true; // one hybrid reading mode (D23.1): words always sit under the pictures
            var seen = SeenReplays != null ? SeenReplays() : (IReadOnlyCollection<string>)Array.Empty<string>();
            for (int i = 0; i < page.Questions.Count; i++)
            {
                var q = page.Questions[i];
                var annotations = new List<NotebookAnnotation>(q.Annotations);
                if (i == page.Questions.Count - 1)
                {
                    annotations.AddRange(page.Extras);
                }

                BuildQuestionPage(i, q, annotations, state, seen, words);
            }

            if (page.FinalCard != null)
            {
                var finalPage = NewPage("FinalPage");
                BuildFinalCard(finalPage, page.FinalCard);
            }

            for (int i = 0; i < _pages.Count; i++)
            {
                _dots.Add(UiFactory.Sprite(_dotRow, "Dot" + i, IconFactory.Disc, Palette.WarmPaper, new Vector2(0.5f, 0.5f),
                    new Vector2((i - (_pages.Count - 1) * 0.5f) * 30f, 0f), new Vector2(16f, 16f)));
            }

            ShowPage(page.FinalCard != null ? _pages.Count - 1 : 0, true);
        }

        private RectTransform NewPage(string name)
        {
            var card = UiFactory.PaperCard(_track, name, new Vector2(0.5f, 0.5f), new Vector2(_pages.Count * PageSpacing, 0f),
                PageSize, (_pages.Count % 2 == 0 ? -0.6f : 0.6f), UiFactory.WithAlpha(Palette.HauntedPurple, 0.7f));
            card.gameObject.AddComponent<CanvasGroup>();
            _pages.Add(card.rectTransform);
            return card.rectTransform;
        }

        private void BuildQuestionPage(int index, NotebookQuestion question, IReadOnlyList<NotebookAnnotation> annotations,
            CaseState state, IReadOnlyCollection<string> seen, bool words)
        {
            var page = NewPage("Question" + (index + 1));
            var topLeft = new Vector2(0f, 1f);

            // Order badge: candy dots (no digits), then the question as three picture chips.
            for (int d = 0; d <= index; d++)
            {
                var dot = UiFactory.Sprite(page, "Order" + d, UiFactory.Baked(UiSpriteCatalog.FeltDiscOrange), Color.white,
                    topLeft, new Vector2(46f + d * 20f, -56f), new Vector2(22f, 22f));
                UiFactory.InkShadow(dot, new Vector2(1f, -2f), 0.5f);
            }

            string[] chips = ChipsFor(question.Definition.Id);
            const float chipX = 140f;
            for (int c = 0; c < chips.Length; c++)
            {
                var chip = UiFactory.PaperTag(page, "Chip" + c, null, 20, topLeft, new Vector2(chipX + c * 74f, -56f),
                    new Vector2(68f, 68f), c % 2 == 0 ? -2f : 2f, Color.white);
                var art = UiFactory.Sprite(chip.transform, "Art", PuppetAtlas.Get(chips[c]), Color.white,
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(58f, 58f));
                art.preserveAspect = true;
            }

            var label = UiFactory.Label(page, "QuestionText", question.QuestionKey, 28, TextAnchor.MiddleLeft,
                UiFactory.Ink, UiFactory.BoldFont);
            UiFactory.Place(label.rectTransform, topLeft, new Vector2(chipX + chips.Length * 74f - 10f, -56f),
                new Vector2(PageSize.x - (chipX + chips.Length * 74f) - 20f, 56f), new Vector2(0f, 0.5f));
            label.gameObject.SetActive(words);
            QuestionLabels.Add(label);

            BuildStrip(page, question, words);
            BuildStickers(page, annotations, state, seen);
        }

        private static string[] ChipsFor(string questionId)
        {
            switch (questionId)
            {
                case CaseNotebook.QuestionEntry:
                    return new[] { "pg.june", "pg.question", "pg.house" };
                case CaseNotebook.QuestionSource:
                    return new[] { "pg.knock3", "pg.question", "pg.house_cut" };
                default:
                    return new[] { "pg.knock_ring", "pg.question", "pg.connect" };
            }
        }

        private void BuildStrip(RectTransform page, NotebookQuestion question, bool words)
        {
            var strip = UiFactory.Rect(page, "AnswerStrip");
            UiFactory.Place(strip, new Vector2(0.5f, 1f), new Vector2(0f, -110f - StripHeight * 0.5f),
                new Vector2(StripWidth, StripHeight));
            var vignette = question.Answered ? C01Vignettes.AnswerStripFor(question.Definition.Id) : null;
            float[] xs = { 0.17f, 0.50f, 0.83f };
            var stitches = new Image[3];
            for (int p = 0; p < 3; p++)
            {
                float x = (xs[p] - 0.5f) * StripWidth;
                var panel = UiFactory.Sprite(strip, "Panel" + p, UiFactory.Baked(UiSpriteCatalog.PaperCard),
                    question.Answered ? Color.white : new Color(0.9f, 0.88f, 0.9f, 0.55f), new Vector2(0.5f, 0.5f),
                    new Vector2(x, 0f), new Vector2(196f, StripHeight));
                if (!question.Answered)
                {
                    // Unknown: a dashed outline with a pencil question mark (shape, not colour).
                    var dash = UiFactory.StitchLayer(panel.transform, UiFactory.WithAlpha(Graphite, 0.7f));
                    if (dash != null)
                    {
                        dash.rectTransform.offsetMin = new Vector2(4f, 4f);
                        dash.rectTransform.offsetMax = new Vector2(-4f, -4f);
                    }

                    var mark = UiFactory.Sprite(panel.transform, "Unknown", PuppetAtlas.Get("pg.question"),
                        UiFactory.WithAlpha(Color.white, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(90f, 90f));
                    mark.preserveAspect = true;
                }
                else
                {
                    stitches[p] = UiFactory.StitchLayer(panel.transform, PanelThread);
                }

                if (p < 2)
                {
                    var arrow = UiFactory.Sprite(strip, "Arrow" + p, UiFactory.Baked(UiSpriteCatalog.IconArrow),
                        Palette.HauntedPurple, new Vector2(0.5f, 0.5f), new Vector2(x + StripWidth * 0.165f, 0f),
                        new Vector2(30f, 30f));
                    arrow.preserveAspect = true;
                    arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f); // the glyph points up
                }
            }

            if (vignette == null)
            {
                AnswerStrips.Add(null);
                StripButtons.Add(null);
                AnswerLabels.Add(null);
                _stitches.Add(null);
                return;
            }

            var player = VignettePlayer.Create(strip, "Comic");
            player.CanRun = () => CanAnimate == null || CanAnimate();
            player.ReducedMotion = ReducedMotion;
            player.SuppressRings = true; // the active panel's stitch turns orange instead (nv2_notebook)
            player.ShowStill(vignette, 0f);
            AnswerStrips.Add(player);
            _stitches.Add(stitches);
            var tap = UiFactory.Panel(strip, "TapToPlay", new Color(0f, 0f, 0f, 0f));
            var button = tap.gameObject.AddComponent<Button>();
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => player.Play(vignette));
            StripButtons.Add(button);

            var answer = UiFactory.Label(page, "Answer", question.AnswerKey, 26, TextAnchor.MiddleCenter, Graphite,
                UiFactory.BoldFont);
            UiFactory.Place(answer.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -110f - StripHeight - 46f),
                new Vector2(PageSize.x - 80f, 84f));
            answer.gameObject.SetActive(words);
            AnswerLabels.Add(answer);
        }

        /// <summary>Plays the answer comic of question <paramref name="index"/> (as a tap on it would).</summary>
        public bool PlayStrip(int index)
        {
            if (index < 0 || index >= StripButtons.Count || StripButtons[index] == null)
            {
                return false;
            }

            StripButtons[index].onClick.Invoke();
            return true;
        }

        private void BuildStickers(RectTransform page, IReadOnlyList<NotebookAnnotation> annotations, CaseState state,
            IReadOnlyCollection<string> seen)
        {
            var row = UiFactory.Rect(page, "Stickers");
            UiFactory.Place(row, new Vector2(0f, 0f), new Vector2(30f, 66f), new Vector2(PageSize.x - 60f, 100f),
                new Vector2(0f, 0.5f));
            float x = 0f;
            foreach (var annotation in annotations)
            {
                float angle = (annotation.Id.GetHashCode() & 3) - 1.5f;
                string[] art = StickerArt(annotation.Id);
                float width = 16f + art.Length * 46f;
                if (x + width > PageSize.x - 60f)
                {
                    break;
                }

                if (annotation.Found)
                {
                    var sticker = UiFactory.PaperTag(row, "Sticker", null, 20, new Vector2(0f, 0.5f),
                        new Vector2(x + width * 0.5f, 0f), new Vector2(width, 72f), angle, Color.white);
                    sticker.raycastTarget = true;
                    for (int a = 0; a < art.Length; a++)
                    {
                        var img = UiFactory.Sprite(sticker.transform, "Art" + a, PuppetAtlas.Get(art[a]), Color.white,
                            new Vector2(0f, 0.5f), new Vector2(8f + 23f + a * 46f, 0f), new Vector2(46f, 46f));
                        img.preserveAspect = true;
                    }

                    var button = sticker.gameObject.AddComponent<Button>();
                    button.transition = Selectable.Transition.None;
                    string note = annotation.CaptionKey;
                    button.onClick.AddListener(() => ShowCaption?.Invoke(note));
                    _slips.Add(button);

                    var replays = MemoryReplayPolicy.OnDemandFor(annotation.Id, state, seen);
                    if (replays.Count > 0)
                    {
                        var replay = replays[0];
                        // A small night-felt badge with the eye; its clear hit area keeps the full touch size.
                        var eye = UiFactory.Sprite(sticker.transform, "WatchAgain", null, new Color(0f, 0f, 0f, 0f),
                            new Vector2(1f, 1f), new Vector2(-2f, -2f),
                            new Vector2(UiFactory.MinTouchTarget, UiFactory.MinTouchTarget), raycast: true);
                        var badge = UiFactory.Sprite(eye.transform, "Badge", UiFactory.Baked(UiSpriteCatalog.FeltDiscNight),
                            Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46f, 46f));
                        UiFactory.InkShadow(badge, new Vector2(2f, -3f), 0.45f);
                        UiFactory.Sprite(badge.transform, "Ring", UiFactory.Baked(UiSpriteCatalog.StitchRing),
                            Palette.CandyCornYellow, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46f, 46f));
                        var eyeArt = UiFactory.Sprite(badge.transform, "Eye", PuppetAtlas.Get("pg.eye"), Color.white,
                            new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(38f, 38f));
                        eyeArt.preserveAspect = true;

                        var eyeButton = eye.gameObject.AddComponent<Button>();
                        eyeButton.targetGraphic = eye;
                        eyeButton.transition = Selectable.Transition.None;
                        eyeButton.onClick.AddListener(() => WatchReplay?.Invoke(replay));
                        ReplayEyes.Add(eyeButton);
                    }

                    if (_shownStickers.Add(annotation.Id) && !ReducedMotion)
                    {
                        _wiggles.Add((sticker.rectTransform, -1f, angle));
                    }
                }
                else
                {
                    var slot = UiFactory.Sprite(row, "EmptySlot", null, UiFactory.WithAlpha(Graphite, 0.08f),
                        new Vector2(0f, 0.5f), new Vector2(x + width * 0.5f, 0f), new Vector2(width, 72f));
                    UiFactory.StitchLayer(slot.transform, UiFactory.WithAlpha(Graphite, 0.45f));
                }

                x += width + 9f;
            }
        }

        /// <summary>The picture on a clue sticker: who said it and what (Bea + cape), or the found thing.</summary>
        public static string[] StickerArt(string entryId)
        {
            switch (entryId)
            {
                case "c01.note.bea":
                    return new[] { "pg.bea", "pg.cape" };
                case "c01.note.vale":
                    return new[] { "pg.vale", "pg.door_shut" };
                case "c01.note.bench":
                    return new[] { "pg.bench", "pg.eye" };
                case "c01.note.knocks":
                    return new[] { "pg.knock3" };
                case "c01.note.streamer":
                    return new[] { "pg.streamer" };
                case "c01.note.cloth":
                    return new[] { "pg.cloth" };
                case "c01.note.sill":
                    return new[] { "pg.window", "pg.cloth" };
                case "c01.note.june":
                    return new[] { "pg.june", "pg.june_window" };
                case "c01.note.sweet":
                    return new[] { "pg.sweet" };
                default:
                    return new[] { "pg.question" };
            }
        }

        private void BuildFinalCard(RectTransform page, NotebookFinalCard final)
        {
            FinalCard = page.gameObject;
            var topLeft = new Vector2(0f, 1f);
            PageText(page, "Heading", final.HeadingKey, 44, Palette.InkBlack, UiFactory.DisplayFont, 40f, 22f, 480f, 68f);
            var stamp = UiFactory.PaperTag(page, "ClosedStamp", final.StampKey, 32, new Vector2(1f, 1f),
                new Vector2(-150f, -80f), new Vector2(220f, 66f), -9f, Color.white);
            stamp.GetComponentInChildren<Text>().color = WitchGreen;
            UiFactory.StitchLayer(stamp.transform, WitchGreen);
            var count = PageText(page, "Count", null, 28, Graphite, UiFactory.BoldFont, 40f, 92f, 360f, 46f);
            count.gameObject.AddComponent<LocalizedText>().SetKey(final.CountKey,
                () => new object[] { final.Found, final.Total });

            // The discoveries as stickers (pictures), with their names in a line below for readers.
            var names = new List<string>();
            float x = 40f;
            foreach (var entry in final.Discoveries)
            {
                string key = CaseNotebookTags.TagKey(entry.Id) ?? entry.TextKey;
                names.Add(Localizer.Get(key));
                var art = StickerArt(entry.Id);
                if (x + 76f < PageSize.x - 40f)
                {
                    var chip = UiFactory.PaperTag(page, "Found", null, 20, topLeft, new Vector2(x + 34f, -180f),
                        new Vector2(68f, 68f), (names.Count % 2 == 0) ? 2f : -2f, Color.white);
                    var img = UiFactory.Sprite(chip.transform, "Art", PuppetAtlas.Get(art[0]), Color.white,
                        new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56f, 56f));
                    img.preserveAspect = true;
                    x += 76f;
                }
            }

            var list = PageText(page, "Discoveries", null, 26, Palette.InkBlack, UiFactory.BoldFont, 40f, 226f, 700f, 120f);
            list.text = string.Join(Localizer.Get(UiKeys.NotebookDiscoverySeparator), names);
            list.alignment = TextAnchor.UpperLeft;
            list.resizeTextMinSize = 26;

            var bottom = new Vector2(0.5f, 0f);
            BackToStreetButton = UiFactory.SecondaryButton(page, "BackToStreet", final.BackToStreetKey, bottom,
                new Vector2(-150f, 70f), new Vector2(270f, 96f), Close);
            ReplayButton = UiFactory.SecondaryButton(page, "ReplayCase", final.ReplayKey, bottom, new Vector2(150f, 70f),
                new Vector2(250f, 96f), () => ReplayRequested?.Invoke());
        }

        // ------------------------------------------------------------------------------------------ carousel

        /// <summary>Slides to page <paramref name="index"/> (clamped).</summary>
        public void ShowPage(int index) => ShowPage(index, false);

        private void ShowPage(int index, bool snap)
        {
            if (_pages.Count == 0)
            {
                return;
            }

            CurrentPage = Mathf.Clamp(index, 0, _pages.Count - 1);
            if (snap || ReducedMotion)
            {
                _trackX = -CurrentPage * PageSpacing;
            }

            for (int i = 0; i < _dots.Count; i++)
            {
                _dots[i].color = i == CurrentPage ? Palette.CandyCornYellow : UiFactory.WithAlpha(Palette.WarmPaper, 0.4f);
            }

            _prev.gameObject.SetActive(CurrentPage > 0);
            _next.gameObject.SetActive(CurrentPage < _pages.Count - 1);
            LayoutPages();
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            _dragging = true;
            _dragStart = _trackX;
        }

        public void OnDrag(PointerEventData eventData)
        {
            var canvas = GetComponentInParent<Canvas>();
            float scale = canvas != null ? canvas.scaleFactor : 1f;
            _trackX += eventData.delta.x / Mathf.Max(0.01f, scale);
            LayoutPages();
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            _dragging = false;
            float moved = _trackX - _dragStart;
            int target = CurrentPage;
            if (moved < -PageSpacing * 0.15f)
            {
                target++;
            }
            else if (moved > PageSpacing * 0.15f)
            {
                target--;
            }

            ShowPage(target);
        }

        private void LayoutPages()
        {
            for (int i = 0; i < _pages.Count; i++)
            {
                float x = i * PageSpacing + _trackX;
                var page = _pages[i];
                page.anchoredPosition = new Vector2(x, 0f);
                float away = Mathf.Clamp01(Mathf.Abs(x) / PageSpacing);
                float s = Mathf.Lerp(1f, 0.86f, away);
                page.localScale = new Vector3(s, s, 1f);
                var group = page.GetComponent<CanvasGroup>();
                if (group != null)
                {
                    group.alpha = Mathf.Lerp(1f, 0.45f, away); // neighbours peek as quiet paper
                }
            }
        }

        private void Update()
        {
            float target = -CurrentPage * PageSpacing;
            if (!_dragging && Mathf.Abs(_trackX - target) > 0.5f)
            {
                _trackX = Mathf.Lerp(_trackX, target, 1f - Mathf.Exp(-14f * Time.unscaledDeltaTime));
                LayoutPages();
            }

            // The comic's active panel gets the orange stitch (one panel at a time, as in the mock).
            for (int i = 0; i < AnswerStrips.Count && i < _stitches.Count; i++)
            {
                var strip = AnswerStrips[i];
                var stitches = _stitches[i];
                if (strip == null || stitches == null)
                {
                    continue;
                }

                int active = strip.ActiveRingX < 0f ? -1 : Mathf.Clamp(Mathf.RoundToInt((strip.ActiveRingX - 0.17f) / 0.33f), 0, 2);
                for (int p = 0; p < stitches.Length; p++)
                {
                    if (stitches[p] != null)
                    {
                        stitches[p].color = p == active ? Palette.JackOrange : PanelThread;
                    }
                }
            }

            for (int i = _wiggles.Count - 1; i >= 0; i--)
            {
                var (rect, start, angle) = _wiggles[i];
                if (rect == null)
                {
                    _wiggles.RemoveAt(i);
                    continue;
                }

                if (start < 0f)
                {
                    start = Time.unscaledTime;
                    _wiggles[i] = (rect, start, angle);
                }

                float p = Mathf.Clamp01((Time.unscaledTime - start) / StickerWiggleSeconds);
                float s = Mathf.Lerp(1.5f, 1f, VignetteSampler.Apply(Ease.Out, Mathf.Min(1f, p * 2.5f)));
                rect.localScale = new Vector3(s, s, 1f);
                rect.localRotation = Quaternion.Euler(0f, 0f, angle + VignetteSampler.Wiggle(p));
                if (p >= 1f)
                {
                    _wiggles.RemoveAt(i);
                }
            }
        }

        /// <summary>A label placed from the page's top-left corner (x, y down, width, height).</summary>
        private static Text PageText(RectTransform page, string name, string key, int size, Color color, Font font, float x,
            float y, float width, float height)
        {
            var text = UiFactory.Label(page, name, key, size, TextAnchor.MiddleLeft, color, font);
            UiFactory.Place(text.rectTransform, new Vector2(0f, 1f), new Vector2(x, -y), new Vector2(width, height),
                new Vector2(0f, 1f));
            return text;
        }
    }
}
