using System.Collections;
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
    /// <summary>The question pages of the notebook: slots, the shared tray, placing and checking (S002.5a).</summary>
    public sealed partial class NotebookView
    {
        private static readonly Color PanelThread = new Color(0.36f, 0.23f, 0.48f, 0.45f);

        public const float SlotWidth = 182f;
        public const float SlotHeight = 122f;
        public const float SlotGap = 60f;
        public const float SlotRowY = -212f;
        public const float StickerWidth = 100f;
        public const float StickerHeight = 74f;
        public const int TrayColumns = 8;
        public const int TrayCells = 16;
        public const float WobbleSeconds = 0.55f;
        public const float WobbleStagger = 0.14f;
        public const float ShakeSeconds = 0.35f;
        public const float ThinkSeconds = 2.2f;
        public const float GlowSeconds = 0.6f;
        public const float ComicPanelSeconds = 0.5f;

        private sealed class PageView
        {
            public RectTransform Page;
            public CaseQuestion Question;
            public bool Solved;
            public RectTransform SlotRow;
            public readonly List<Image> Slots = new List<Image>();
            public readonly List<Image> SlotStitches = new List<Image>();
            public readonly List<NotebookSticker> Placed = new List<NotebookSticker>();
            public RectTransform Tray;
            public readonly List<string> TrayIds = new List<string>();
            public readonly List<NotebookSticker> TrayStickers = new List<NotebookSticker>();
            public readonly List<Image> Outlines = new List<Image>();
            public readonly List<Button> Eyes = new List<Button>();
            public RectTransform Overlay;
            public readonly List<Image> ComicPanels = new List<Image>();
            public readonly List<Image> ComicStitches = new List<Image>();
            public GameObject FoundStamp;
        }

        private sealed class Tween
        {
            public RectTransform Rect;
            /// <summary>The page the glide is measured on: positions are page-local, so a canvas mode or scale change
            /// mid-glide (a capture, a rotation) never throws a sticker off the page.</summary>
            public Transform Space;
            public Vector3 From;
            public Vector3 To;
            public float Delay;
            public float Duration;
            public float Wobble;
            public float Elapsed;
            public float BaseAngle;
        }

        private readonly List<PageView> _pageViews = new List<PageView>();
        private readonly List<Tween> _tweens = new List<Tween>();
        private readonly List<(RectTransform Rect, float Start, float Angle)> _wiggles =
            new List<(RectTransform, float, float)>();
        private readonly List<GameObject> _placeMarks = new List<GameObject>();
        private readonly DeductionCursor _cursor = new DeductionCursor();

        private bool _checking;
        private Coroutine _checkRoutine;
        private GameObject _thought;
        private float _thoughtLeft;
        private RectTransform _shake;
        private float _shakeLeft;
        private string _animateComicFor;
        private PageView _comicView;
        private float _comicTime;
        private float _slideToFinal = -1f;
        private Image _focusRing;
        private bool _cursorVisible;
        private RectTransform _dragHome;

        private InputActionMap _input;
        private InputAction _left;
        private InputAction _right;
        private InputAction _up;
        private InputAction _down;
        private InputAction _use;
        private InputAction _back;
        private InputAction _prevPage;
        private InputAction _nextPage;

        // ------------------------------------------------------------------------------------- test and view hooks

        /// <summary>A full set is waiting for, or showing, its verdict (input is ignored meanwhile).</summary>
        public bool Checking => _checking;

        /// <summary>The last verdict (tests and captures).</summary>
        public DeductionCheck LastCheck { get; private set; }

        /// <summary>The board of the current question (null on the final card or a solved page).</summary>
        public DeductionBoard CurrentBoard
        {
            get
            {
                var view = Current;
                return view != null && view.Question != null && !view.Solved ? Session.Board(view.Question) : null;
            }
        }

        /// <summary>Stickers in the current page's tray, in tray order.</summary>
        public IReadOnlyList<NotebookSticker> TrayStickers => Current != null ? Current.TrayStickers : (IReadOnlyList<NotebookSticker>)new NotebookSticker[0];

        /// <summary>The current page's slots (empty on solved pages and the final card).</summary>
        public IReadOnlyList<Image> Slots => Current != null ? Current.Slots : (IReadOnlyList<Image>)new Image[0];

        /// <summary>"?" outlines in the current page's tray: one per clue still out in the world.</summary>
        public int OutlineCount => Current != null ? Current.Outlines.Count : 0;

        /// <summary>The comic's panels on the current page (empty until the child solves the question).</summary>
        public IReadOnlyList<Image> ComicPanels => Current != null ? Current.ComicPanels : (IReadOnlyList<Image>)new Image[0];

        /// <summary>The "Found it!" stamp on the current page (null until solved and the comic has played).</summary>
        public GameObject FoundStamp => Current != null ? Current.FoundStamp : null;

        /// <summary>The comic is still drawing itself.</summary>
        public bool ComicPlaying => _comicView != null;

        /// <summary>Pip's thought cloud (shown after a wrong set, and for the musing).</summary>
        public GameObject Thought => _thought;

        /// <summary>The place badge over a missing clue's outline (null unless the radio pointed at a place).</summary>
        public GameObject PlaceBadge => _placeMarks.Count > 0 ? _placeMarks[_placeMarks.Count - 1] : null;

        /// <summary>The sticker the cursor or a tap is holding (tap-tap), or null.</summary>
        public string HeldSticker => _cursor.Held;

        public CursorRow CursorRow => _cursor.Row;

        public int CursorIndex => _cursor.Index;

        /// <summary>The sticker sitting in slot <paramref name="slot"/> of the current page, or null.</summary>
        public NotebookSticker SlotSticker(int slot) =>
            Current != null && slot >= 0 && slot < Current.Placed.Count ? Current.Placed[slot] : null;

        public NotebookSticker TraySticker(string id)
        {
            foreach (var s in TrayStickers)
            {
                if (s != null && s.StickerId == id)
                {
                    return s;
                }
            }

            return null;
        }

        private PageView Current => CurrentPage >= 0 && CurrentPage < _pageViews.Count ? _pageViews[CurrentPage] : null;

        private CaseState CurrentState => State != null ? State() : CaseState.Empty;

        // ------------------------------------------------------------------------------------- page building

        private void BuildQuestionPage(int index, NotebookQuestion question)
        {
            var page = NewPage("Question" + (index + 1));
            var view = new PageView { Page = page, Question = question.Definition, Solved = question.Solved };
            _pageViews.Add(view);
            PageQuestions.Add(question.Definition);
            var topLeft = new Vector2(0f, 1f);

            // Order badge: candy dots (no digits), then the question as picture chips with its words beside them.
            for (int d = 0; d <= index; d++)
            {
                var dot = UiFactory.Sprite(page, "Order" + d, UiFactory.Baked(UiSpriteCatalog.FeltDiscOrange), Color.white,
                    topLeft, new Vector2(46f + d * 20f, -58f), new Vector2(22f, 22f));
                UiFactory.InkShadow(dot, new Vector2(1f, -2f), 0.5f);
            }

            string[] chips = ChipsFor(question.Definition.Id);
            float chipX = 104f + index * 20f;
            for (int c = 0; c < chips.Length; c++)
            {
                var chip = UiFactory.PaperTag(page, "Chip" + c, null, 20, topLeft, new Vector2(chipX + 34f + c * 74f, -58f),
                    new Vector2(68f, 68f), c % 2 == 0 ? -2f : 2f, Color.white);
                var art = UiFactory.Sprite(chip.transform, "Art", PuppetAtlas.Get(chips[c]), Color.white,
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(56f, 56f));
                art.preserveAspect = true;
            }

            float textX = chipX + chips.Length * 74f + 20f;
            var label = UiFactory.Label(page, "QuestionText", question.QuestionKey, 32, TextAnchor.MiddleLeft,
                UiFactory.Ink, UiFactory.BoldFont);
            UiFactory.Place(label.rectTransform, topLeft, new Vector2(textX, -58f),
                new Vector2(PageSize.x - textX - 40f, 60f), new Vector2(0f, 0.5f));
            QuestionLabels.Add(label);

            if (question.Solved)
            {
                AnswerLabels.Add(BuildComic(view, question.AnswerStickers, question.AnswerKey,
                    _animateComicFor == question.Definition.Id && !ReducedMotion));
            }
            else
            {
                AnswerLabels.Add(null);
                view.SlotRow = UiFactory.Rect(page, "Slots");
                UiFactory.Place(view.SlotRow, new Vector2(0.5f, 1f), new Vector2(0f, SlotRowY), new Vector2(PageSize.x, SlotHeight));
                BuildSlots(view);
            }

            BuildTray(view);
            view.Overlay = UiFactory.Stretch(UiFactory.Rect(page, "Overlay"));
        }

        private static string[] ChipsFor(string questionId)
        {
            switch (questionId)
            {
                case CaseNotebook.QuestionEntry:
                    return new[] { "pg.june", "pg.question", "pg.house" };
                case CaseNotebook.QuestionSource:
                    return new[] { "pg.knock3", "pg.question", "pg.house" };
                default:
                    return new[] { "pg.knock_ring", "pg.question" };
            }
        }

        private static float SlotX(int slot, int count) => (slot - (count - 1) * 0.5f) * (SlotWidth + SlotGap);

        private void BuildSlots(PageView view)
        {
            view.Slots.Clear();
            view.SlotStitches.Clear();
            view.Placed.Clear();
            var board = Session.Board(view.Question);
            int n = board.SlotCount;
            for (int k = 0; k < n; k++)
            {
                float x = SlotX(k, n);
                // An empty dashed slot: the same for every slot, so its shape never says which clue goes there.
                var slot = UiFactory.Sprite(view.SlotRow, "Slot" + k, IconFactory.RoundedRect,
                    UiFactory.WithAlpha(Graphite, 0.07f), new Vector2(0.5f, 0.5f), new Vector2(x, 0f),
                    new Vector2(SlotWidth, SlotHeight), raycast: true);
                var stitch = UiFactory.StitchLayer(slot.transform, UiFactory.WithAlpha(Graphite, 0.6f));
                view.Slots.Add(slot);
                view.SlotStitches.Add(stitch);
                int index = k;
                var button = slot.gameObject.AddComponent<Button>();
                button.targetGraphic = slot;
                button.transition = Selectable.Transition.None;
                button.navigation = new Navigation { mode = Navigation.Mode.None };
                button.onClick.AddListener(() => TapSlot(index));
                AccessibleName.Set(slot.gameObject, UiKeys.NotebookSlot);

                if (k < n - 1)
                {
                    // The "+" between slots is drawn (two felt bars), not text.
                    var plusAt = new Vector2(x + (SlotWidth + SlotGap) * 0.5f, 0f);
                    UiFactory.Sprite(view.SlotRow, "PlusH" + k, IconFactory.RoundedRect, Palette.HauntedPurple,
                        new Vector2(0.5f, 0.5f), plusAt, new Vector2(26f, 7f));
                    UiFactory.Sprite(view.SlotRow, "PlusV" + k, IconFactory.RoundedRect, Palette.HauntedPurple,
                        new Vector2(0.5f, 0.5f), plusAt, new Vector2(7f, 26f));
                }

                string id = board.Slots[k];
                NotebookSticker placed = null;
                if (id != null)
                {
                    placed = StickerView(slot.transform, id, new Vector2(0.5f, 0.5f), Vector2.zero,
                        new Vector2(SlotWidth - 24f, SlotHeight - 24f), k % 2 == 0 ? -1.5f : 1.5f, k);
                    if (board.IsKept(k))
                    {
                        // From the third try a sticker that fits stays pinned (§5.4): a pin, never a check mark.
                        var pin = UiFactory.Sprite(placed.transform, "Pin", PuppetAtlas.Get("pg.pin"), Color.white,
                            new Vector2(0.5f, 1f), new Vector2(0f, -4f), new Vector2(34f, 34f));
                        pin.preserveAspect = true;
                    }
                }

                view.Placed.Add(placed);
            }
        }

        private void BuildTray(PageView view)
        {
            var state = CurrentState;
            var tray = UiFactory.Sprite(view.Page, "Tray", IconFactory.RoundedRect, Palette.NightViolet, new Vector2(0.5f, 0f),
                new Vector2(0f, 128f), new Vector2(PageSize.x - 60f, 206f), raycast: true);
            UiFactory.InkShadow(tray, new Vector2(2f, -3f), 0.5f);
            var thread = UiFactory.StitchLayer(tray.transform, UiFactory.WithAlpha(Palette.WarmPaper, 0.55f));
            if (thread != null)
            {
                thread.rectTransform.offsetMin = new Vector2(8f, 8f);
                thread.rectTransform.offsetMax = new Vector2(-8f, -8f);
            }

            view.Tray = tray.rectTransform;
            view.TrayIds.Clear();
            view.TrayStickers.Clear();
            view.Outlines.Clear();
            view.Eyes.Clear();
            var board = view.Question != null && !view.Solved ? Session.Board(view.Question) : null;
            foreach (var sticker in CaseNotebook.Found(state, true))
            {
                if (board != null && board.SlotOf(sticker.Id) >= 0)
                {
                    continue; // on this page's slots
                }

                view.TrayIds.Add(sticker.Id);
            }

            var seen = SeenReplays != null ? SeenReplays() : (IReadOnlyCollection<string>)new string[0];
            int cell = 0;
            foreach (string id in view.TrayIds)
            {
                if (cell >= TrayCells)
                {
                    break;
                }

                var s = StickerView(view.Tray, id, new Vector2(0.5f, 0.5f), TrayCell(cell), new Vector2(StickerWidth, StickerHeight),
                    ((id.GetHashCode() & 3) - 1.5f) * 1.2f, -1);
                view.TrayStickers.Add(s);
                if (board != null && board.DecoyAside && view.Question.IsDecoy(id))
                {
                    // Set aside after many tries (§5.4): a pencil circle, quieter paper. Never red, never a cross.
                    var group = s.gameObject.AddComponent<CanvasGroup>();
                    group.alpha = 0.5f;
                    var circle = UiFactory.Sprite(s.transform, "SetAside", UiFactory.Baked(UiSpriteCatalog.StitchRing),
                        Graphite, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(StickerWidth + 22f, StickerWidth + 22f));
                    circle.raycastTarget = false;
                    AccessibleName.Set(s.gameObject, UiKeys.NotebookSetAside);
                }

                foreach (var replay in MemoryReplayPolicy.OnDemandFor(id, state, seen))
                {
                    view.Eyes.Add(Eye(s.transform, replay));
                    break;
                }

                if (_shownStickers.Add(id) && !ReducedMotion)
                {
                    _wiggles.Add((s.Rect, -1f, s.Rect.localEulerAngles.z));
                }

                cell++;
            }

            int outlines = Mathf.Min(CaseNotebook.Outstanding(state).Count, TrayCells - cell);
            for (int o = 0; o < outlines; o++)
            {
                // One generic "?" per clue still out in the world: the count, never which.
                var outline = UiFactory.Sprite(view.Tray, "Outline" + o, IconFactory.RoundedRect,
                    UiFactory.WithAlpha(Palette.WarmPaper, 0.06f), new Vector2(0.5f, 0.5f), TrayCell(cell),
                    new Vector2(StickerWidth - 30f, StickerHeight - 6f));
                UiFactory.StitchLayer(outline.transform, UiFactory.WithAlpha(Palette.WarmPaper, 0.5f));
                var mark = UiFactory.Sprite(outline.transform, "Mark", PuppetAtlas.Get("pg.question"),
                    UiFactory.WithAlpha(Color.white, 0.55f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
                mark.preserveAspect = true;
                AccessibleName.Set(outline.gameObject, UiKeys.NotebookOutstanding);
                view.Outlines.Add(outline);
                cell++;
            }
        }

        private static Vector2 TrayCell(int cell)
        {
            int row = cell / TrayColumns;
            int column = cell % TrayColumns;
            return new Vector2((column - (TrayColumns - 1) * 0.5f) * (StickerWidth + 6f), row == 0 ? 46f : -46f);
        }

        private Button Eye(Transform sticker, MemoryReplayDefinition replay)
        {
            // A small night-felt badge with the eye; its clear hit area keeps the full touch size.
            var eye = UiFactory.Sprite(sticker, "WatchAgain", null, new Color(0f, 0f, 0f, 0f), new Vector2(1f, 1f),
                new Vector2(-4f, -4f), new Vector2(UiFactory.MinTouchTarget * 0.7f, UiFactory.MinTouchTarget * 0.7f), raycast: true);
            var badge = UiFactory.Sprite(eye.transform, "Badge", UiFactory.Baked(UiSpriteCatalog.FeltDiscNight), Color.white,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(36f, 36f));
            UiFactory.InkShadow(badge, new Vector2(2f, -3f), 0.45f);
            var eyeArt = UiFactory.Sprite(badge.transform, "Eye", PuppetAtlas.Get("pg.eye"), Color.white,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(30f, 30f));
            eyeArt.preserveAspect = true;
            var button = eye.gameObject.AddComponent<Button>();
            button.targetGraphic = eye;
            button.transition = Selectable.Transition.None;
            button.onClick.AddListener(() => WatchReplay?.Invoke(replay));
            AccessibleName.Set(eye.gameObject, SpookyStreet.Core.Localization.LocalizationKeys.NonverbalUi.MemoryWatchAgain);
            return button;
        }

        private void RefreshEyes()
        {
            ReplayEyes.Clear();
            if (Current != null)
            {
                ReplayEyes.AddRange(Current.Eyes);
            }
        }

        /// <summary>A paper sticker with the clue's pictures (who said it and what, or the thing found).</summary>
        private static Image StickerCard(Transform parent, string id, Vector2 anchor, Vector2 position, Vector2 size, float angle)
        {
            var card = UiFactory.PaperTag(parent, "Sticker", null, 20, anchor, position, size, angle, Color.white);
            string[] art = StickerArt(id);
            float side = Mathf.Min(size.y * 0.7f, (size.x - 16f) / art.Length);
            for (int a = 0; a < art.Length; a++)
            {
                var img = UiFactory.Sprite(card.transform, "Art" + a, PuppetAtlas.Get(art[a]), Color.white,
                    new Vector2(0.5f, 0.5f), new Vector2((a - (art.Length - 1) * 0.5f) * side, 0f), new Vector2(side, side));
                img.preserveAspect = true;
            }

            AccessibleName.Set(card.gameObject, CaseNotebook.Sticker(id)?.TagKey ?? UiKeys.NotebookOutstanding);
            return card;
        }

        private NotebookSticker StickerView(Transform parent, string id, Vector2 anchor, Vector2 position, Vector2 size,
            float angle, int slot)
        {
            var card = StickerCard(parent, id, anchor, position, size, angle);
            card.raycastTarget = true;
            var sticker = card.gameObject.AddComponent<NotebookSticker>();
            sticker.StickerId = id;
            sticker.SlotIndex = slot;
            sticker.Tapped = OnStickerTapped;
            sticker.DragBegan = OnStickerDragBegan;
            sticker.Dragged = OnStickerDragged;
            sticker.DragEnded = OnStickerDragEnded;
            return sticker;
        }

        /// <summary>The picture on a clue sticker: who said it and what (Bea + a flash), or the thing found.</summary>
        public static string[] StickerArt(string stickerId)
        {
            switch (stickerId)
            {
                case CaseNotebook.BeaFlash:
                    return new[] { "pg.bea", "pg.sparkle" };
                case CaseNotebook.ValeDoor:
                    return new[] { "pg.vale", "pg.door_shut" };
                case CaseNotebook.BenchView:
                    return new[] { "pg.bench", "pg.eye" };
                case CaseNotebook.StuckDoor:
                    return new[] { "pg.door_shut", "pg.hands" };
                case CaseNotebook.Streamer:
                    return new[] { "pg.bike", "pg.streamer" };
                case CaseNotebook.Cloth:
                    return new[] { "pg.cloth" };
                case CaseNotebook.SillSnag:
                    return new[] { "pg.window", PuppetParts.SillSnag };
                case CaseNotebook.FloorHum:
                    return new[] { "pg.floor_hum" };
                case CaseNotebook.WoodenPiece:
                    return new[] { "pg.grille", "pg.eye" };
                case CaseNotebook.OwlPanel:
                    return new[] { "pg.grille", "pg.owl" };
                case CaseNotebook.OwlBooth:
                    return new[] { "pg.booth", "pg.owl" };
                case CaseNotebook.LooseArm:
                    return new[] { "pg.arm" };
                case CaseNotebook.Sweet:
                    return new[] { "pg.sweet" };
                default:
                    return new[] { "pg.question" };
            }
        }

        /// <summary>The picture of a place (the "?" outline's badge when Pip is stuck).</summary>
        public static string PlacePart(string anchorId)
        {
            switch (anchorId)
            {
                case CaseAnchors.Plaza:
                case CaseAnchors.Bench:
                    return "pg.bench";
                case CaseAnchors.BicycleCorner:
                    return "pg.bike";
                case CaseAnchors.Treehouse:
                case CaseAnchors.SideYard:
                    return "pg.treehouse";
                case CaseAnchors.AtticSill:
                case CaseAnchors.Attic:
                    return "pg.window";
                case CaseAnchors.Foyer:
                case CaseAnchors.Workshop:
                    return "pg.house";
                case CaseAnchors.BluePorch:
                    return "pg.booth";
                default:
                    return "pg.porch";
            }
        }

        // ------------------------------------------------------------------------------------- comic

        private Text BuildComic(PageView view, IReadOnlyList<string> stickers, string answerKey, bool animate)
        {
            int n = stickers.Count;
            const float panelW = 232f, panelH = 158f, gap = 46f;
            var strip = UiFactory.Rect(view.Page, "Comic");
            UiFactory.Place(strip, new Vector2(0.5f, 1f), new Vector2(0f, SlotRowY - 6f), new Vector2(PageSize.x, panelH));
            view.ComicPanels.Clear();
            view.ComicStitches.Clear();
            for (int p = 0; p < n; p++)
            {
                float x = (p - (n - 1) * 0.5f) * (panelW + gap);
                var panel = UiFactory.Sprite(strip, "Panel" + p, UiFactory.Baked(UiSpriteCatalog.PaperCard), Color.white,
                    new Vector2(0.5f, 0.5f), new Vector2(x, 0f), new Vector2(panelW, panelH));
                UiFactory.InkShadow(panel, new Vector2(3f, -4f), 0.5f);
                view.ComicStitches.Add(UiFactory.StitchLayer(panel.transform, PanelThread));
                string[] art = StickerArt(stickers[p]);
                float side = Mathf.Min(116f, (panelW - 24f) / art.Length);
                for (int a = 0; a < art.Length; a++)
                {
                    var img = UiFactory.Sprite(panel.transform, "Art" + a, PuppetAtlas.Get(art[a]), Color.white,
                        new Vector2(0.5f, 0.5f), new Vector2((a - (art.Length - 1) * 0.5f) * side, 0f), new Vector2(side, side));
                    img.preserveAspect = true;
                }

                AccessibleName.Set(panel.gameObject, CaseNotebook.Sticker(stickers[p])?.TagKey);
                view.ComicPanels.Add(panel);
                if (p < n - 1)
                {
                    var arrow = UiFactory.Sprite(strip, "Arrow" + p, UiFactory.Baked(UiSpriteCatalog.IconArrow),
                        Palette.HauntedPurple, new Vector2(0.5f, 0.5f), new Vector2(x + (panelW + gap) * 0.5f, 0f),
                        new Vector2(30f, 30f));
                    arrow.preserveAspect = true;
                    arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f); // the glyph points up
                }

                if (animate)
                {
                    panel.rectTransform.localScale = Vector3.zero;
                }
            }

            var answer = UiFactory.Label(view.Page, "Answer", answerKey, 30, TextAnchor.MiddleCenter, Graphite,
                UiFactory.BoldFont);
            UiFactory.Place(answer.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, SlotRowY - panelH * 0.5f - 52f),
                new Vector2(PageSize.x - 90f, 64f));

            var stamp = UiFactory.PaperTag(view.Page, "FoundStamp", UiKeys.NotebookFound, 30, new Vector2(1f, 1f),
                new Vector2(-128f, -128f), new Vector2(200f, 66f), -9f, Color.white);
            stamp.GetComponentInChildren<Text>().color = WitchGreen;
            UiFactory.StitchLayer(stamp.transform, WitchGreen);
            view.FoundStamp = stamp.gameObject;
            if (animate)
            {
                answer.canvasRenderer.SetAlpha(0f);
                stamp.gameObject.SetActive(false);
                _comicView = view;
                _comicTime = 0f;
            }

            return answer;
        }

        private void TickComic(float dt)
        {
            var view = _comicView;
            if (view == null || view.Page == null)
            {
                _comicView = null;
                TickSlide(dt);
                return;
            }

            // The comic draws itself panel by panel (pop and orange stitch), plays twice, then holds (§5.3).
            _comicTime += dt;
            int n = view.ComicPanels.Count;
            float loop = n * ComicPanelSeconds;
            for (int p = 0; p < n; p++)
            {
                float local = _comicTime - p * ComicPanelSeconds;
                float grow = Mathf.Clamp01(local / 0.3f);
                float s = grow <= 0f ? 0f : VignetteSampler.Apply(Ease.Back, grow);
                view.ComicPanels[p].rectTransform.localScale = new Vector3(s, s, 1f);
                if (view.ComicStitches[p] != null)
                {
                    float inLoop = _comicTime % loop;
                    bool active = _comicTime < loop * 2f && inLoop >= p * ComicPanelSeconds && inLoop < (p + 1) * ComicPanelSeconds;
                    view.ComicStitches[p].color = active ? Palette.JackOrange : PanelThread;
                }
            }

            var answer = view.Page.Find("Answer");
            if (answer != null)
            {
                answer.GetComponent<Text>().canvasRenderer.SetAlpha(Mathf.Clamp01((_comicTime - loop) / 0.4f));
            }

            if (_comicTime >= loop * 2f)
            {
                view.FoundStamp.SetActive(true);
                _wiggles.Add((view.FoundStamp.GetComponent<RectTransform>(), -1f, -9f));
                _comicView = null;
            }
        }

        private void TickSlide(float dt)
        {
            if (_slideToFinal < 0f)
            {
                return;
            }

            _slideToFinal -= dt;
            if (_slideToFinal < 0f && FinalCard != null)
            {
                ShowPage(_pages.Count - 1);
            }
        }

        // ------------------------------------------------------------------------------------- placing

        /// <summary>Tap on a sticker (tray: pick it up and hear its note; slot: send it home).</summary>
        public void TapSticker(string stickerId)
        {
            var view = Current;
            if (view == null)
            {
                return;
            }

            foreach (var s in view.Placed)
            {
                if (s != null && s.StickerId == stickerId)
                {
                    OnStickerTapped(s);
                    return;
                }
            }

            var tray = TraySticker(stickerId);
            if (tray != null)
            {
                OnStickerTapped(tray);
            }
        }

        /// <summary>Tap on a slot: place the held sticker, or send the slot's sticker home.</summary>
        public void TapSlot(int slot)
        {
            if (_checking || CurrentBoard == null)
            {
                return;
            }

            _cursor.Set(Core.Case.CursorRow.Slots, slot);
            ApplyCursor();
        }

        private void OnStickerTapped(NotebookSticker sticker)
        {
            if (_checking || sticker == null)
            {
                return;
            }

            var view = Current;
            var board = CurrentBoard;
            if (board == null || view == null)
            {
                // A solved page or the waiting page: the tray only reads out its notes.
                ShowNote(sticker.StickerId);
                return;
            }

            if (sticker.SlotIndex >= 0)
            {
                _cursor.Drop();
                _cursor.Set(Core.Case.CursorRow.Slots, sticker.SlotIndex);
                ApplyCursor();
                return;
            }

            _cursor.Set(Core.Case.CursorRow.Tray, view.TrayIds.IndexOf(sticker.StickerId));
            ApplyCursor();
        }

        private void ShowNote(string stickerId)
        {
            var sticker = CaseNotebook.Sticker(stickerId);
            if (sticker != null)
            {
                ShowCaption?.Invoke(sticker.NoteKey);
            }
        }

        /// <summary>Use on the cursor's target (shared by taps, keyboard and gamepad).</summary>
        private void ApplyCursor()
        {
            var view = Current;
            var board = CurrentBoard;
            if (view == null || board == null)
            {
                return;
            }

            var before = SnapshotPositions(view);
            string target = _cursor.Row == Core.Case.CursorRow.Tray && _cursor.Index < view.TrayIds.Count
                ? view.TrayIds[_cursor.Index]
                : null;
            string inSlot = _cursor.Row == Core.Case.CursorRow.Slots && _cursor.Index < board.SlotCount ? board.Slots[_cursor.Index] : null;
            var result = _cursor.Use(board, view.TrayIds, CaseNotebook.FoundIds(CurrentState));
            switch (result)
            {
                case CursorResult.Picked:
                    ShowNote(target);
                    HideThought();
                    break;
                case CursorResult.Placed:
                    HideThought();
                    HidePlace();
                    RebuildBoard(view, before, null);
                    AfterPlacement(view);
                    break;
                case CursorResult.Returned:
                    RebuildBoard(view, before, new[] { inSlot });
                    break;
                case CursorResult.Refused:
                    var refused = target != null ? TraySticker(target) : null;
                    if (refused != null && !ReducedMotion)
                    {
                        _wiggles.Add((refused.Rect, -1f, refused.Rect.localEulerAngles.z));
                    }

                    break;
            }

            RefreshSelection();
        }

        private Dictionary<string, Vector3> SnapshotPositions(PageView view)
        {
            var map = new Dictionary<string, Vector3>();
            foreach (var s in view.TrayStickers)
            {
                if (s != null)
                {
                    map[s.StickerId] = view.Page.InverseTransformPoint(s.Rect.position);
                }
            }

            foreach (var s in view.Placed)
            {
                if (s != null)
                {
                    map[s.StickerId] = view.Page.InverseTransformPoint(s.Rect.position);
                }
            }

            return map;
        }

        /// <summary>
        /// Rebuilds the current page's slots and tray from the board, then glides every sticker that moved from where
        /// it was (a placed one presses in; <paramref name="wobble"/> ones wobble home in turn).
        /// </summary>
        private void RebuildBoard(PageView view, Dictionary<string, Vector3> before, IList<string> wobble)
        {
            for (int i = view.SlotRow.childCount - 1; i >= 0; i--)
            {
                Destroy(view.SlotRow.GetChild(i).gameObject);
                view.SlotRow.GetChild(i).SetParent(null, false);
            }

            Destroy(view.Tray.gameObject);
            view.Tray.SetParent(null, false);
            BuildSlots(view);
            BuildTray(view);
            view.Overlay.SetAsLastSibling();
            Canvas.ForceUpdateCanvases();
            RefreshEyes();

            int order = 0;
            foreach (var s in AllStickers(view))
            {
                if (s == null || before == null || !before.TryGetValue(s.StickerId, out var from))
                {
                    continue;
                }

                Vector3 to = view.Page.InverseTransformPoint(s.Rect.position);
                if ((from - to).sqrMagnitude < 0.5f)
                {
                    continue;
                }

                bool home = wobble != null && wobble.Contains(s.StickerId);
                _tweens.Add(new Tween
                {
                    Rect = s.Rect, Space = view.Page, From = from, To = to, Delay = home ? order++ * WobbleStagger : 0f,
                    Duration = home ? WobbleSeconds : 0.18f, Wobble = home ? 14f : 0f, BaseAngle = s.Rect.localEulerAngles.z,
                });
                s.Rect.position = view.Page.TransformPoint(from);
            }

            if (ReducedMotion)
            {
                FinishTweens();
            }
        }

        private static IEnumerable<NotebookSticker> AllStickers(PageView view)
        {
            foreach (var s in view.Placed)
            {
                yield return s;
            }

            foreach (var s in view.TrayStickers)
            {
                yield return s;
            }
        }

        private void AfterPlacement(PageView view)
        {
            var board = Session.Board(view.Question);
            if (board.IsFull && !_checking)
            {
                _checking = true;
                _checkRoutine = StartCoroutine(CheckRoutine(view));
            }
        }

        private IEnumerator CheckRoutine(PageView view)
        {
            // Pip pauses on a full set before the verdict (§5.3).
            yield return new WaitForSecondsRealtime(CaseDeduction.CheckPauseSeconds);
            var board = Session.Board(view.Question);
            var state = CurrentState;
            var before = SnapshotPositions(view);
            var check = board.Check(CaseNotebook.FoundIds(state));
            LastCheck = check;
            if (check.Verdict == DeductionVerdict.Wrong)
            {
                // A small shake, the stickers wobble home in turn (a soft tok, never a buzzer), Pip thinks. No count.
                _shake = view.SlotRow;
                _shakeLeft = ReducedMotion ? 0f : ShakeSeconds;
                yield return new WaitForSecondsRealtime(ReducedMotion ? 0f : ShakeSeconds);
                var home = new List<string>();
                foreach (int slot in check.ReturnedSlots)
                {
                    home.Add(check.Placed[slot]);
                }

                RebuildBoard(view, before, home);
                ShowThought(new[] { "pg.pip", "pg.emo_think" }, ThinkSeconds, UiKeys.NotebookThinking);
                yield return new WaitForSecondsRealtime(ReducedMotion ? 0f : WobbleSeconds + WobbleStagger * home.Count);
                _checking = false;
                _checkRoutine = null;
                RefreshSelection();
                yield break;
            }

            if (check.Verdict == DeductionVerdict.Right)
            {
                // The stitches glow orange and the stickers press in; then the comic built from them.
                foreach (var stitch in view.SlotStitches)
                {
                    if (stitch != null)
                    {
                        stitch.color = Palette.JackOrange;
                    }
                }

                foreach (var s in view.Placed)
                {
                    if (s != null)
                    {
                        s.Rect.localScale = new Vector3(1.08f, 1.08f, 1f);
                    }
                }

                yield return new WaitForSecondsRealtime(ReducedMotion ? 0f : GlowSeconds);
                var step = Solve?.Invoke(view.Question.Id, check.Placed);
                _checking = false;
                _checkRoutine = null;
                _animateComicFor = view.Question.Id;
                Rebuild(view.Question.Id);
                _animateComicFor = null;
                if (step != null && step.StampedCase)
                {
                    // The last deduction stamps the case: after the comic, the final card slides in.
                    _slideToFinal = ReducedMotion ? 0.2f : 1.4f;
                }

                yield break;
            }

            _checking = false;
            _checkRoutine = null;
        }

        // ------------------------------------------------------------------------------------- drag

        private void OnStickerDragBegan(NotebookSticker sticker, PointerEventData eventData)
        {
            var view = Current;
            if (_checking || view == null || CurrentBoard == null || sticker == null)
            {
                _dragHome = null;
                return;
            }

            // It lifts and follows the thumb above everything on the page.
            _cursor.Drop();
            _dragHome = sticker.Rect;
            sticker.Rect.SetParent(view.Overlay, true);
            sticker.Rect.SetAsLastSibling();
            sticker.Rect.localScale = new Vector3(1.12f, 1.12f, 1f);
            sticker.Rect.localRotation = Quaternion.Euler(0f, 0f, 3f);
            OnStickerDragged(sticker, eventData);
            HideThought();
        }

        private void OnStickerDragged(NotebookSticker sticker, PointerEventData eventData)
        {
            if (_dragHome == null || sticker == null || sticker.Rect != _dragHome)
            {
                return;
            }

            var view = Current;
            if (RectTransformUtility.ScreenPointToWorldPointInRectangle(view.Overlay, eventData.position,
                    eventData.pressEventCamera, out var world))
            {
                sticker.Rect.position = world;
            }
        }

        private void OnStickerDragEnded(NotebookSticker sticker, PointerEventData eventData)
        {
            if (_dragHome == null || sticker == null || sticker.Rect != _dragHome)
            {
                return;
            }

            _dragHome = null;
            var view = Current;
            var board = CurrentBoard;
            if (view == null || board == null)
            {
                return;
            }

            int target = -1;
            for (int k = 0; k < view.Slots.Count; k++)
            {
                if (RectTransformUtility.RectangleContainsScreenPoint(view.Slots[k].rectTransform, eventData.position,
                        eventData.pressEventCamera))
                {
                    target = k;
                }
            }

            var before = SnapshotPositions(view);
            before[sticker.StickerId] = view.Page.InverseTransformPoint(sticker.Rect.position);
            var found = CaseNotebook.FoundIds(CurrentState);
            bool placed = target >= 0 && board.Place(target, sticker.StickerId, found);
            if (!placed && sticker.SlotIndex >= 0 && target < 0)
            {
                board.Remove(sticker.SlotIndex); // dragged off its slot: it goes home
            }

            Destroy(sticker.gameObject);
            RebuildBoard(view, before, placed ? null : new[] { sticker.StickerId });
            if (placed)
            {
                HidePlace();
                AfterPlacement(view);
            }

            RefreshSelection();
        }

        // ------------------------------------------------------------------------------------- Pip's thoughts

        private void ShowThought(string[] parts, float seconds, string nameKey)
        {
            HideThought();
            var view = Current;
            if (view == null)
            {
                return;
            }

            // A thought cloud in the page's top-right corner: two trailing puffs and a paper bubble with pictures.
            var root = UiFactory.Rect(view.Overlay, "Thought");
            UiFactory.Place(root, new Vector2(1f, 1f), new Vector2(-180f, -150f), new Vector2(300f, 120f));
            UiFactory.Sprite(root, "Puff1", IconFactory.Disc, Palette.WarmPaper, new Vector2(0.5f, 0.5f),
                new Vector2(-118f, -60f), new Vector2(16f, 16f));
            UiFactory.Sprite(root, "Puff2", IconFactory.Disc, Palette.WarmPaper, new Vector2(0.5f, 0.5f),
                new Vector2(-96f, -42f), new Vector2(26f, 26f));
            float width = 34f + parts.Length * 66f;
            var bubble = UiFactory.Sprite(root, "Bubble", IconFactory.RoundedRect, Color.white, new Vector2(0.5f, 0.5f),
                new Vector2(0f, 8f), new Vector2(width, 92f));
            UiFactory.ApplySurface(bubble, UiSpriteCatalog.PaperTag, Palette.WarmPaper, Color.white);
            UiFactory.InkShadow(bubble, new Vector2(3f, -4f), 0.5f);
            for (int i = 0; i < parts.Length; i++)
            {
                var art = UiFactory.Sprite(bubble.transform, "Art" + i, PuppetAtlas.Get(parts[i]), Color.white,
                    new Vector2(0.5f, 0.5f), new Vector2((i - (parts.Length - 1) * 0.5f) * 66f, 0f), new Vector2(60f, 60f));
                art.preserveAspect = true;
            }

            AccessibleName.Set(root.gameObject, nameKey);
            _thought = root.gameObject;
            _thoughtLeft = seconds;
            if (!ReducedMotion)
            {
                _wiggles.Add((bubble.rectTransform, -1f, 0f));
            }
        }

        private void HideThought()
        {
            if (_thought != null)
            {
                Destroy(_thought);
            }

            _thought = null;
            _thoughtLeft = 0f;
        }

        private void ShowMusing(DeductionMusing musing)
        {
            var parts = new List<string> { "pg.pip" };
            parts.AddRange(musing.Parts);
            ShowThought(parts.ToArray(), float.PositiveInfinity, UiKeys.NotebookThinking);
            HidePlace();
            var view = Current;
            if (!musing.PointsToPlace || view == null || view.Outlines.Count == 0)
            {
                return;
            }

            // The "?" outline lights and shows its place, never the clue's picture or its partner.
            var outline = view.Outlines[0];
            var ring = UiFactory.Sprite(outline.transform, "Lit", UiFactory.Baked(UiSpriteCatalog.StitchRing),
                Palette.JackOrange, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(StickerHeight + 34f, StickerHeight + 34f));
            ring.raycastTarget = false;
            _placeMarks.Add(ring.gameObject);
            var badge = UiFactory.PaperTag(outline.transform, "Place", null, 20, new Vector2(1f, 1f), new Vector2(4f, 4f),
                new Vector2(62f, 62f), 4f, Color.white);
            var art = UiFactory.Sprite(badge.transform, "Art", PuppetAtlas.Get(PlacePart(musing.MissingPlaceAnchor)),
                Color.white, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(48f, 48f));
            art.preserveAspect = true;
            AccessibleName.Set(badge.gameObject, UiKeys.NotebookPlace);
            _placeMarks.Add(badge.gameObject);
            if (!ReducedMotion)
            {
                _wiggles.Add((badge.rectTransform, -1f, 4f));
            }
        }

        private void HidePlace()
        {
            foreach (var mark in _placeMarks)
            {
                if (mark != null)
                {
                    Destroy(mark);
                }
            }

            _placeMarks.Clear();
        }

        /// <summary>Stops checks, thoughts, drags and glides (page rebuilds, close, back to title).</summary>
        private void EndTransient()
        {
            if (_checkRoutine != null)
            {
                StopCoroutine(_checkRoutine);
                _checkRoutine = null;
            }

            _checking = false;
            _dragHome = null;
            _shake = null;
            _comicView = null;
            _slideToFinal = -1f;
            HideThought();
            HidePlace();
            FinishTweens();
            if (_focusRing != null)
            {
                Destroy(_focusRing.gameObject);
                _focusRing = null;
            }
        }

        // ------------------------------------------------------------------------------------- animation

        private void TickTweens(float dt)
        {
            for (int i = _tweens.Count - 1; i >= 0; i--)
            {
                var t = _tweens[i];
                if (t.Rect == null || t.Space == null)
                {
                    _tweens.RemoveAt(i);
                    continue;
                }

                t.Elapsed += dt;
                float p = Mathf.Clamp01((t.Elapsed - t.Delay) / t.Duration);
                float e = VignetteSampler.Apply(t.Wobble > 0f ? Ease.Back : Ease.Out, p);
                t.Rect.position = t.Space.TransformPoint(Vector3.LerpUnclamped(t.From, t.To, e));
                float wobble = t.Wobble * Mathf.Sin(p * Mathf.PI * 3f) * (1f - p);
                t.Rect.localRotation = Quaternion.Euler(0f, 0f, t.BaseAngle + wobble);
                if (p >= 1f)
                {
                    t.Rect.position = t.Space.TransformPoint(t.To);
                    t.Rect.localRotation = Quaternion.Euler(0f, 0f, t.BaseAngle);
                    _tweens.RemoveAt(i);
                }
            }

            if (_shake != null)
            {
                _shakeLeft -= dt;
                float x = _shakeLeft > 0f ? Mathf.Sin(_shakeLeft * 60f) * 9f * (_shakeLeft / ShakeSeconds) : 0f;
                _shake.anchoredPosition = new Vector2(x, SlotRowY);
                if (_shakeLeft <= 0f)
                {
                    _shake = null;
                }
            }

            if (_thought != null && !float.IsInfinity(_thoughtLeft))
            {
                _thoughtLeft -= dt;
                if (_thoughtLeft <= 0f)
                {
                    HideThought();
                }
            }
        }

        private void FinishTweens()
        {
            foreach (var t in _tweens)
            {
                if (t.Rect != null && t.Space != null)
                {
                    t.Rect.position = t.Space.TransformPoint(t.To);
                    t.Rect.localRotation = Quaternion.Euler(0f, 0f, t.BaseAngle);
                }
            }

            _tweens.Clear();
        }

        private void TickWiggles(float dt)
        {
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
                float s = Mathf.Lerp(1.4f, 1f, VignetteSampler.Apply(Ease.Out, Mathf.Min(1f, p * 2.5f)));
                rect.localScale = new Vector3(s, s, 1f);
                rect.localRotation = Quaternion.Euler(0f, 0f, angle + VignetteSampler.Wiggle(p));
                if (p >= 1f)
                {
                    _wiggles.RemoveAt(i);
                }
            }
        }

        // ------------------------------------------------------------------------------------- keyboard and gamepad (D25)

        private void BuildInput()
        {
            _input = new InputActionMap("Notebook");
            _left = KeyAction("Left", "<Keyboard>/leftArrow", "<Keyboard>/a", "<Gamepad>/dpad/left", "<Gamepad>/leftStick/left");
            _right = KeyAction("Right", "<Keyboard>/rightArrow", "<Keyboard>/d", "<Gamepad>/dpad/right", "<Gamepad>/leftStick/right");
            _up = KeyAction("Up", "<Keyboard>/upArrow", "<Keyboard>/w", "<Gamepad>/dpad/up", "<Gamepad>/leftStick/up");
            _down = KeyAction("Down", "<Keyboard>/downArrow", "<Keyboard>/s", "<Gamepad>/dpad/down", "<Gamepad>/leftStick/down");
            _use = KeyAction("Use", "<Keyboard>/enter", "<Keyboard>/space", "<Keyboard>/e", "<Gamepad>/buttonSouth");
            _back = KeyAction("Back", "<Keyboard>/escape", "<Keyboard>/backspace", "<Gamepad>/buttonEast");
            _prevPage = KeyAction("PreviousPage", "<Keyboard>/q", "<Keyboard>/pageUp", "<Gamepad>/leftShoulder");
            _nextPage = KeyAction("NextPage", "<Keyboard>/r", "<Keyboard>/pageDown", "<Gamepad>/rightShoulder");
        }

        private InputAction KeyAction(string name, params string[] bindings)
        {
            var action = _input.AddAction(name, InputActionType.Button, bindings[0]);
            for (int i = 1; i < bindings.Length; i++)
            {
                action.AddBinding(bindings[i]);
            }

            return action;
        }

        private void TickInput()
        {
            if (!IsOpen || _input == null || !_input.enabled)
            {
                return;
            }

            if (_left.WasPressedThisFrame())
            {
                Navigate(-1, 0);
            }
            else if (_right.WasPressedThisFrame())
            {
                Navigate(1, 0);
            }
            else if (_up.WasPressedThisFrame())
            {
                Navigate(0, -1);
            }
            else if (_down.WasPressedThisFrame())
            {
                Navigate(0, 1);
            }

            if (_use.WasPressedThisFrame())
            {
                Submit();
            }
            else if (_back.WasPressedThisFrame())
            {
                if (_cursor.Held != null)
                {
                    _cursor.Drop();
                    RefreshSelection();
                }
                else
                {
                    Close();
                }
            }
            else if (_prevPage.WasPressedThisFrame())
            {
                ShowPage(CurrentPage - 1);
            }
            else if (_nextPage.WasPressedThisFrame())
            {
                ShowPage(CurrentPage + 1);
            }
        }

        /// <summary>Moves the notebook's cursor (arrows, WASD, d-pad, stick).</summary>
        public void Navigate(int dx, int dy)
        {
            _cursorVisible = true;
            var view = Current;
            if (view == null)
            {
                return;
            }

            int slots = CurrentBoard != null ? CurrentBoard.SlotCount : 0;
            if (dx != 0 && dy == 0 && (_cursor.Row == Core.Case.CursorRow.Tray ? view.TrayIds.Count : slots) == 0)
            {
                ShowPage(CurrentPage + dx); // nothing to move over: turn the page
                return;
            }

            _cursor.Move(dx, dy, slots, view.TrayIds.Count);
            RefreshSelection();
        }

        /// <summary>Use on the cursor's target (Enter, Space, E, gamepad South).</summary>
        public void Submit()
        {
            _cursorVisible = true;
            if (_checking)
            {
                return;
            }

            var view = Current;
            if (view == null)
            {
                return;
            }

            if (FinalCard != null && view.Page.gameObject == FinalCard)
            {
                Close();
                return;
            }

            if (CurrentBoard == null)
            {
                if (_cursor.Row == Core.Case.CursorRow.Tray && _cursor.Index < view.TrayIds.Count)
                {
                    ShowNote(view.TrayIds[_cursor.Index]);
                }

                return;
            }

            ApplyCursor();
        }

        /// <summary>The held sticker lifts with an orange stitch; the keyboard cursor shows a candy ring.</summary>
        private void RefreshSelection()
        {
            var view = Current;
            if (view == null)
            {
                return;
            }

            foreach (var s in view.TrayStickers)
            {
                if (s == null)
                {
                    continue;
                }

                bool held = s.StickerId == _cursor.Held;
                var stitch = s.transform.Find("HeldStitch");
                if (held && stitch == null)
                {
                    var thread = UiFactory.StitchLayer(s.transform, Palette.JackOrange);
                    if (thread != null)
                    {
                        thread.name = "HeldStitch";
                    }

                    s.Rect.localScale = new Vector3(1.12f, 1.12f, 1f);
                }
                else if (!held && stitch != null)
                {
                    Destroy(stitch.gameObject);
                    s.Rect.localScale = Vector3.one;
                }
            }

            TickSelection();
        }

        private void TickSelection()
        {
            var view = Current;
            RectTransform target = null;
            if (_cursorVisible && view != null && IsOpen)
            {
                if (_cursor.Row == Core.Case.CursorRow.Slots && _cursor.Index < view.Slots.Count)
                {
                    target = view.Slots[_cursor.Index].rectTransform;
                }
                else if (_cursor.Row == Core.Case.CursorRow.Tray && _cursor.Index < view.TrayStickers.Count &&
                         view.TrayStickers[_cursor.Index] != null)
                {
                    target = view.TrayStickers[_cursor.Index].Rect;
                }
            }

            if (target == null)
            {
                if (_focusRing != null)
                {
                    _focusRing.gameObject.SetActive(false);
                }

                return;
            }

            if (_focusRing == null || _focusRing.transform.parent != view.Overlay)
            {
                if (_focusRing != null)
                {
                    Destroy(_focusRing.gameObject);
                }

                _focusRing = UiFactory.Sprite(view.Overlay, "Cursor", IconFactory.RoundedRect, new Color(0f, 0f, 0f, 0f),
                    new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
                var thread = UiFactory.StitchLayer(_focusRing.transform, Palette.CandyCornYellow);
                if (thread != null)
                {
                    thread.rectTransform.offsetMin = new Vector2(-6f, -6f);
                    thread.rectTransform.offsetMax = new Vector2(6f, 6f);
                }
            }

            _focusRing.gameObject.SetActive(true);
            _focusRing.rectTransform.position = target.position;
            _focusRing.rectTransform.sizeDelta = target.rect.size + new Vector2(16f, 16f);
        }
    }
}
