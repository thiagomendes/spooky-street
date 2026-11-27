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
using UiKeys = SpookyStreet.Core.Localization.LocalizationKeys.CaseUi;

namespace SpookyStreet.UI
{
    /// <summary>
    /// Pip's deduction notebook (S002.5a, D26, docs/mystery-first.md §5, mock mf_notebook_deduction): one page per
    /// question the world has raised, in a swipeable carousel (edge arrows, dot pips, neighbours peeking). A page holds
    /// the order badge, the question as picture chips with its words under them (D23.1), two or three empty dashed slots
    /// joined by "+", and the shared clue tray (a felt strip) with every found sticker plus one "?" outline per clue still
    /// out in the world. The child drags a sticker onto a slot, or taps the sticker then the slot; keyboard and gamepad
    /// move a cursor over the slots and the tray (D25). A full set is checked after Pip's 0.4 s pause: a wrong set shakes
    /// and wobbles home while Pip thinks (from the third try the stickers that fit stay pinned; after the fifth the decoy
    /// is set aside); a right set glows, the case records the deduction, and the slots give way to the answer comic drawn
    /// from the child's own stickers. Nothing is answered, ticked or pre-shaped before the child solves it. The radio on
    /// an open question gives Pip's musing and, if a needed clue is still out there, that clue's place. Once the case is
    /// stamped a last page holds the final card. Every page is rebuilt from <see cref="CaseNotebook.Build"/>, the
    /// <see cref="DeductionSession"/> (memory only) and the saved <see cref="CaseAnswers"/>; no display text is stored.
    /// </summary>
    public sealed partial class NotebookView : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        private static readonly Color Graphite = new Color32(0x4A, 0x44, 0x58, 0xFF);
        private static readonly Color WitchGreen = new Color32(0x8D, 0xBF, 0x3F, 0xFF);

        public static readonly Vector2 PageSize = new Vector2(940f, 560f);
        public const float PageSpacing = 1060f;
        public const float StickerWiggleSeconds = 0.8f;

        private readonly List<RectTransform> _pages = new List<RectTransform>();
        private readonly List<Image> _dots = new List<Image>();
        private readonly HashSet<string> _shownStickers = new HashSet<string>(StringComparer.Ordinal);

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

        /// <summary>The child's saved right sets (the comic is drawn from them).</summary>
        public Func<CaseAnswers> Answers { get; set; }

        /// <summary>
        /// The child placed a full right set: the director applies <see cref="CaseAction.Solve"/>, saves the set and
        /// returns the step (null when nothing was recorded).
        /// </summary>
        public Func<string, IReadOnlyList<string>, CaseStep> Solve { get; set; }

        /// <summary>The boards of this play session (memory only; replaced on a new case).</summary>
        public DeductionSession Session { get; set; } = new DeductionSession();

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

        /// <summary>Gate for animation time (pause); null always runs.</summary>
        public Func<bool> CanAnimate { get; set; }

        public bool ReducedMotion { get; set; }

        public event Action Closed;

        public bool IsOpen => gameObject.activeSelf;

        public Button CloseButton { get; private set; }

        public Button HintButton { get; private set; }

        public Button BackToStreetButton { get; private set; }

        public Button ReplayButton { get; private set; }

        /// <summary>The final card (null until the case is stamped and the notebook is open).</summary>
        public GameObject FinalCard { get; private set; }

        /// <summary>Question texts, one per raised question page, in order (tests).</summary>
        public List<Text> QuestionLabels { get; } = new List<Text>();

        /// <summary>Answer labels per question page (null while unsolved: no answer before the child solves).</summary>
        public List<Text> AnswerLabels { get; } = new List<Text>();

        /// <summary>The questions on the pages, in page order.</summary>
        public List<CaseQuestion> PageQuestions { get; } = new List<CaseQuestion>();

        /// <summary>Eye buttons on stickers that offer a memory replay (current page's tray).</summary>
        public List<Button> ReplayEyes { get; } = new List<Button>();

        public int PageCount => _pages.Count;

        public int CurrentPage { get; private set; }

        /// <summary>The question on the current page, or null on the final card.</summary>
        public CaseQuestion CurrentQuestion => CurrentPage >= 0 && CurrentPage < PageQuestions.Count ? PageQuestions[CurrentPage] : null;

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
            UiFactory.Place(_track, new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), PageSize);

            _dotRow = UiFactory.Rect(content, "PageDots");
            UiFactory.Place(_dotRow, new Vector2(0.5f, 0.5f), new Vector2(0f, -PageSize.y * 0.5f - 6f), new Vector2(200f, 24f));

            _prev = Disc(content, "PreviousPage", UiSpriteCatalog.IconArrow, new Vector2(0f, 0.5f), new Vector2(62f, 14f),
                () => ShowPage(CurrentPage - 1));
            // The arrow glyph points up: turn it to point left and right.
            _prev.transform.Find("Icon").localRotation = Quaternion.Euler(0f, 0f, 90f);
            _next = Disc(content, "NextPage", UiSpriteCatalog.IconArrow, new Vector2(1f, 0.5f), new Vector2(-62f, 14f),
                () => ShowPage(CurrentPage + 1));
            _next.transform.Find("Icon").localRotation = Quaternion.Euler(0f, 0f, -90f);

            CloseButton = Disc(content, "CloseButton", UiSpriteCatalog.IconClose, Vector2.one, new Vector2(-60f, -58f), Close);
            AccessibleName.Set(CloseButton.gameObject, UiKeys.NotebookClose);
            // The radio: a felt disc in the bottom-right corner, as in the mock (mf_deduction_help).
            HintButton = Disc(content, "HintButton", UiSpriteCatalog.IconRadio, new Vector2(1f, 0f), new Vector2(-62f, 70f),
                OnHint);
            HintButton.GetComponent<Image>().sprite = UiFactory.Baked(UiSpriteCatalog.FeltDiscOrange);
            HintButton.GetComponent<Image>().color = Color.white;
            AccessibleName.Set(HintButton.gameObject, HintKeys.HudHint);
            BuildInput();
            BuildPrompts(content);
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
            // Stays under the caption strip (a later sibling), so notes read over the pages.
            gameObject.SetActive(true);
            Rebuild();
            ResumeCheck();
            _cursor.Reset();
            _cursorVisible = false;
            // The notebook's own cursor drives keyboard and gamepad (D25); no uGUI selection, so Enter never closes it.
            if (EventSystem.current != null)
            {
                EventSystem.current.SetSelectedGameObject(null);
            }

            _input.Enable();
            OpenKeepsakeAssembly();
        }

        /// <summary>Opens on the first open question (Kit's "So, detective?").</summary>
        public void OpenOnFirstOpenQuestion()
        {
            Open();
            var state = State != null ? State() : CaseState.Empty;
            var open = CaseNotebook.FirstOpen(state);
            int index = open != null ? PageQuestions.IndexOf(open) : -1;
            if (index >= 0)
            {
                ShowPage(index, true);
                ResumeCheck();
            }
        }

        /// <summary>A full set left unchecked (the notebook closed during Pip's pause) is checked on reopening.</summary>
        private void ResumeCheck()
        {
            var view = Current;
            if (view != null && CurrentBoard != null && CurrentBoard.IsFull && !_checking)
            {
                AfterPlacement(view);
            }
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            EndTransient();
            _input.Disable();
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        /// <summary>Closes without raising <see cref="Closed"/> (back to title).</summary>
        public void Cancel()
        {
            EndTransient();
            _input.Disable();
            gameObject.SetActive(false);
        }

        /// <summary>Forgets which stickers already wiggled in and every board (a fresh case).</summary>
        public void ResetStickers()
        {
            _shownStickers.Clear();
            Session.Reset();
        }

        private void OnHint()
        {
            var question = CurrentQuestion;
            var state = State != null ? State() : CaseState.Empty;
            if (question != null && !question.IsSolved(state) && !_checking)
            {
                // Stuck on a question (§6): Pip muses, and a missing clue shows its place, never its partner.
                ShowMusing(CaseDeduction.Musing(question, state));
                return;
            }

            string key = RequestHint?.Invoke();
            if (key != null)
            {
                ShowCaption?.Invoke(key);
            }
        }

        /// <summary>
        /// Rebuilds every page from the current state and shows the first page (the final card once stamped), or keeps
        /// the current question when one is open.
        /// </summary>
        public void Rebuild() => Rebuild(null);

        private void Rebuild(string keepQuestion)
        {
            var state = State != null ? State() : CaseState.Empty;
            var page = CaseNotebook.Build(state, Answers != null ? Answers() : null);
            EndTransient();
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
            _wiggles.Clear();
            _pageViews.Clear();
            QuestionLabels.Clear();
            AnswerLabels.Clear();
            PageQuestions.Clear();
            ReplayEyes.Clear();
            FinalCard = null;
            BackToStreetButton = null;
            ReplayButton = null;

            for (int i = 0; i < page.Questions.Count; i++)
            {
                BuildQuestionPage(i, page.Questions[i]);
            }

            if (page.Questions.Count == 0)
            {
                BuildEmptyPage();
            }

            int finalIndex = 0;
            if (page.FinalCard != null)
            {
                finalIndex = _pages.Count;
                var finalPage = NewPage("FinalPage");
                BuildFinalCard(finalPage, page.FinalCard);
            }

            // S004: the keepsakes page after the case pages (earned sketch pages and the bat stack).
            BuildKeepsakePage();

            for (int i = 0; i < _pages.Count; i++)
            {
                _dots.Add(UiFactory.Sprite(_dotRow, "Dot" + i, IconFactory.Disc, Palette.WarmPaper, new Vector2(0.5f, 0.5f),
                    new Vector2((i - (_pages.Count - 1) * 0.5f) * 30f, 0f), new Vector2(16f, 16f)));
            }

            int show = finalIndex;
            if (keepQuestion != null)
            {
                for (int i = 0; i < PageQuestions.Count; i++)
                {
                    if (PageQuestions[i].Id == keepQuestion)
                    {
                        show = i;
                    }
                }
            }

            ShowPage(show, true);
        }

        private RectTransform NewPage(string name)
        {
            var card = UiFactory.PaperCard(_track, name, new Vector2(0.5f, 0.5f), new Vector2(_pages.Count * PageSpacing, 0f),
                PageSize, (_pages.Count % 2 == 0 ? -0.5f : 0.5f), UiFactory.WithAlpha(Palette.HauntedPurple, 0.7f));
            card.gameObject.AddComponent<CanvasGroup>();
            _pages.Add(card.rectTransform);
            return card.rectTransform;
        }

        /// <summary>Before the world raises any question: a quiet page with the empty tray (nothing to answer yet).</summary>
        private void BuildEmptyPage()
        {
            var page = NewPage("Waiting");
            var view = new PageView { Page = page, Question = null };
            _pageViews.Add(view);
            var mark = UiFactory.Sprite(page, "Question", PuppetAtlas.Get("pg.question"), UiFactory.WithAlpha(Color.white, 0.5f),
                new Vector2(0.5f, 1f), new Vector2(0f, -170f), new Vector2(120f, 120f));
            mark.preserveAspect = true;
            BuildTray(view);
            view.Overlay = UiFactory.Stretch(UiFactory.Rect(page, "Overlay"));
        }

        private void BuildFinalCard(RectTransform page, NotebookFinalCard final)
        {
            FinalCard = page.gameObject;
            _pageViews.Add(new PageView { Page = page, Question = null, Overlay = UiFactory.Stretch(UiFactory.Rect(page, "Overlay")) });
            var topLeft = new Vector2(0f, 1f);
            PageText(page, "Heading", final.HeadingKey, 44, Palette.InkBlack, UiFactory.DisplayFont, 44f, 26f, 520f, 68f);
            var stamp = UiFactory.PaperTag(page, "ClosedStamp", final.StampKey, 32, new Vector2(1f, 1f),
                new Vector2(-160f, -84f), new Vector2(230f, 70f), -9f, Color.white);
            stamp.GetComponentInChildren<Text>().color = WitchGreen;
            UiFactory.StitchLayer(stamp.transform, WitchGreen);
            var count = PageText(page, "Count", null, 28, Graphite, UiFactory.BoldFont, 44f, 96f, 360f, 46f);
            count.gameObject.AddComponent<LocalizedText>().SetKey(final.CountKey,
                () => new object[] { final.Found, final.Total });

            // The discoveries as stickers (pictures), with their names in a line below for readers.
            var names = new List<string>();
            float x = 44f;
            float y = -196f;
            foreach (var sticker in final.Discoveries)
            {
                names.Add(Localizer.Get(sticker.TagKey));
                if (x + 96f > PageSize.x - 30f)
                {
                    x = 44f;
                    y -= 92f;
                }

                if (y > -380f)
                {
                    var chip = StickerCard(page, sticker.Id, topLeft, new Vector2(x + 44f, y), new Vector2(88f, 80f),
                        (names.Count % 2 == 0) ? 2f : -2f);
                    chip.raycastTarget = false;
                    x += 98f;
                }
            }

            var list = PageText(page, "Discoveries", null, 24, Palette.InkBlack, UiFactory.BoldFont, 44f, 344f, 850f, 92f);
            list.text = string.Join(Localizer.Get(UiKeys.NotebookDiscoverySeparator), names);
            list.alignment = TextAnchor.UpperLeft;
            list.resizeTextMinSize = 22;

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

            int before = CurrentPage;
            CurrentPage = Mathf.Clamp(index, 0, _pages.Count - 1);
            if (before != CurrentPage)
            {
                if (!snap)
                {
                    Core.Audio.AudioCues.PageTurn(); // the paper flip (S003)
                }

                _cursor.Drop();
                HideThought();
                HidePlace();
                CloseMemory();
            }

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
            RefreshEyes();
            LayoutPages();
            RefreshSelection();
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
                    group.blocksRaycasts = i == CurrentPage;
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

            bool run = CanAnimate == null || CanAnimate();
            float dt = run ? Time.unscaledDeltaTime : 0f;
            TickInput();
            TickTweens(dt);
            TickWiggles(dt);
            TickComic(dt);
            TickKeepsakeAssembly(dt);
            TickSelection();
            TickPrompts();
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

        private void OnDestroy()
        {
            _input?.Dispose();
        }
    }
}
