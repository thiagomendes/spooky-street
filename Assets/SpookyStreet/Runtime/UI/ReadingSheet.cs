using System;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Parent;
using SpookyStreet.Localization;
using UnityEngine;
using UnityEngine.UI;

namespace SpookyStreet.UI
{
    /// <summary>
    /// A calm paper sheet for grown-ups (S004 stream E: the "For grown-ups" page and the credits): its own canvas
    /// above the settings card, a night scrim, one large paper sheet in the safe area with a heading and a close disc,
    /// and a vertical scroll of sections. Touch drags or flicks the sheet; gamepad and keyboard (F1) move between the
    /// close disc and the sheet, up and down scroll it a step, the bumpers or Q and R a page, and Back closes.
    /// </summary>
    public sealed class ReadingSheet : MonoBehaviour
    {
        public const int SortingOrder = 17;

        public static readonly Color Graphite = new Color32(0x4A, 0x44, 0x58, 0xFF);

        /// <summary>One d-pad step scrolls this many canvas units.</summary>
        public const float Step = 180f;

        private RectTransform _viewport;

        public Canvas Canvas { get; private set; }

        public bool IsOpen => Canvas != null && Canvas.gameObject.activeSelf;

        public Button CloseButton { get; private set; }

        public ScrollRect Scroll { get; private set; }

        /// <summary>The scrolling column; sections are added to it top to bottom.</summary>
        public RectTransform Content { get; private set; }

        public UiFocusScreen Navigation { get; private set; }

        public RectTransform Sheet { get; private set; }

        public event Action Closed;

        public static ReadingSheet Create(string name, string headingKey)
        {
            var canvas = UiFactory.CreateCanvas(name, SortingOrder);
            var sheet = canvas.gameObject.AddComponent<ReadingSheet>();
            sheet.Canvas = canvas;
            canvas.gameObject.SetActive(false); // built hidden: attaching navigation never touches the title's selection
            sheet.Build(headingKey);
            return sheet;
        }

        private void Build(string headingKey)
        {
            var root = (RectTransform)Canvas.transform;
            UiFactory.Panel(root, "Scrim", UiFactory.WithAlpha(Palette.NightViolet, 0.92f));
            var safe = UiFactory.Stretch(UiFactory.Rect(root, "Content"));
            safe.gameObject.AddComponent<SafeAreaFitter>();
            var paper = UiFactory.PaperCard(safe, "Sheet", new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero, 0f);
            Sheet = paper.rectTransform;
            Sheet.anchorMin = Vector2.zero;
            Sheet.anchorMax = Vector2.one;
            Sheet.offsetMin = new Vector2(24f, 18f);
            Sheet.offsetMax = new Vector2(-24f, -18f);

            var heading = UiFactory.Label(paper.transform, "Heading", headingKey, 48, TextAnchor.MiddleLeft, UiFactory.Ink,
                UiFactory.DisplayFont);
            heading.rectTransform.anchorMin = new Vector2(0f, 1f);
            heading.rectTransform.anchorMax = new Vector2(1f, 1f);
            heading.rectTransform.pivot = new Vector2(0f, 1f);
            heading.rectTransform.offsetMin = new Vector2(56f, -100f);
            heading.rectTransform.offsetMax = new Vector2(-150f, -24f);

            var close = UiFactory.FeltDisc(paper.transform, "Close", UiSpriteCatalog.FeltDiscNight, Palette.WarmPaper,
                UiSpriteCatalog.IconClose, Palette.WarmPaper, null, UiFactory.SystemDiscDiameter, Vector2.one,
                new Vector2(-66f, -62f), 1f);
            CloseButton = close.gameObject.AddComponent<Button>();
            CloseButton.targetGraphic = close;
            CloseButton.transition = Selectable.Transition.None;
            CloseButton.onClick.AddListener(Close);

            _viewport = UiFactory.Rect(paper.transform, "Viewport");
            _viewport.anchorMin = Vector2.zero;
            _viewport.anchorMax = Vector2.one;
            _viewport.offsetMin = new Vector2(44f, 26f);
            _viewport.offsetMax = new Vector2(-44f, -118f);
            var hit = _viewport.gameObject.AddComponent<Image>();
            hit.color = new Color(1f, 1f, 1f, 0f);
            _viewport.gameObject.AddComponent<RectMask2D>();

            Content = UiFactory.Rect(_viewport, "Column");
            Content.anchorMin = new Vector2(0f, 1f);
            Content.anchorMax = new Vector2(1f, 1f);
            Content.pivot = new Vector2(0.5f, 1f);
            Content.offsetMin = Vector2.zero;
            Content.offsetMax = Vector2.zero;
            var layout = Content.gameObject.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14f;
            layout.padding = new RectOffset(12, 12, 4, 24);
            layout.childControlWidth = true;
            layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            var fitter = Content.gameObject.AddComponent<ContentSizeFitter>();
            fitter.verticalFit = ContentSizeFitter.FitMode.PreferredSize;

            Scroll = _viewport.gameObject.AddComponent<ScrollRect>();
            Scroll.viewport = _viewport;
            Scroll.content = Content;
            Scroll.horizontal = false;
            Scroll.vertical = true;
            Scroll.movementType = ScrollRect.MovementType.Clamped;
            Scroll.scrollSensitivity = 40f;
            Scroll.inertia = true;

            var nav = UiFocusScreen.Attach(Canvas.gameObject, ReadingSheetFocus.Build(Canvas.name), paper.transform);
            nav.Bind(ReadingSheetFocus.Close, CloseButton);
            nav.Bind(ReadingSheetFocus.Sheet, _viewport, null);
            nav.DirectionOverride = OnDirection;
            nav.PageLeft = () => ScrollBy(-ViewportHeight);
            nav.PageRight = () => ScrollBy(ViewportHeight);
            Navigation = nav;
        }

        private float ViewportHeight => _viewport != null ? Mathf.Max(100f, _viewport.rect.height - 60f) : 400f;

        /// <summary>How far the sheet can scroll (0 when it all fits).</summary>
        public float ScrollRange => Content != null && _viewport != null ? Mathf.Max(0f, Content.rect.height - _viewport.rect.height) : 0f;

        /// <summary>How far down the sheet is scrolled, in canvas units.</summary>
        public float Scrolled => ScrollRange * (1f - Scroll.verticalNormalizedPosition);

        private bool OnDirection(UiDirection direction)
        {
            if (Navigation.Focus != ReadingSheetFocus.Sheet)
            {
                return false;
            }

            switch (direction)
            {
                case UiDirection.Down:
                    ScrollBy(Step);
                    return true;
                case UiDirection.Up:
                    if (Scrolled <= 0.5f)
                    {
                        return false; // at the top: up goes to the close disc
                    }

                    ScrollBy(-Step);
                    return true;
                default:
                    return true;
            }
        }

        /// <summary>Scrolls by <paramref name="units"/> (positive: down), clamped to the sheet.</summary>
        public void ScrollBy(float units)
        {
            Canvas.ForceUpdateCanvases();
            float range = ScrollRange;
            if (range <= 0f)
            {
                return;
            }

            float scrolled = Mathf.Clamp(Scrolled + units, 0f, range);
            Scroll.StopMovement();
            Scroll.verticalNormalizedPosition = 1f - scrolled / range;
        }

        public void Open()
        {
            Canvas.gameObject.SetActive(true);
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(Content);
            Scroll.verticalNormalizedPosition = 1f;
            Navigation.FocusOn(ReadingSheetFocus.Sheet);
        }

        public void Close()
        {
            if (!IsOpen)
            {
                return;
            }

            Canvas.gameObject.SetActive(false);
            Closed?.Invoke();
        }

        /// <summary>Removes every section (the page is rebuilt from the saves each time it opens).</summary>
        public void Clear()
        {
            for (int i = Content.childCount - 1; i >= 0; i--)
            {
                var child = Content.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }

        // ------------------------------------------------------------------------------------- building blocks

        /// <summary>A section heading.</summary>
        public Text Heading(string key) => Paragraph(key, 36, UiFactory.Ink, UiFactory.DisplayFont, 10f);

        /// <summary>A wrapped line of reading text (no shrinking: the sheet grows instead).</summary>
        public Text Paragraph(string key, int size = 30, Color? color = null, Font font = null, float spaceAbove = 0f,
            Func<object[]> args = null)
        {
            var parent = Content;
            if (spaceAbove > 0f)
            {
                Spacer(spaceAbove);
            }

            var text = UiFactory.Label(parent, "Text", null, size, TextAnchor.UpperLeft, color ?? Graphite,
                font ?? UiFactory.BodyFont);
            text.resizeTextForBestFit = false;
            text.verticalOverflow = VerticalWrapMode.Overflow;
            text.lineSpacing = 1.05f;
            if (key != null)
            {
                text.gameObject.AddComponent<LocalizedText>().SetKey(key, args);
            }

            return text;
        }

        public void Spacer(float height)
        {
            var space = UiFactory.Rect(Content, "Space");
            space.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
        }

        /// <summary>A left-aligned row of fixed-size items (chips, panels, dots) <paramref name="height"/> tall.</summary>
        public RectTransform Row(float height, float spacing = 18f)
        {
            var row = UiFactory.Rect(Content, "Row");
            row.gameObject.AddComponent<LayoutElement>().preferredHeight = height;
            var layout = row.gameObject.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = spacing;
            layout.childAlignment = TextAnchor.MiddleLeft;
            layout.childControlWidth = false;
            layout.childControlHeight = false;
            layout.childForceExpandWidth = false;
            layout.childForceExpandHeight = false;
            return row;
        }

        /// <summary>A fixed-size cell in a row.</summary>
        public static RectTransform Cell(RectTransform row, string name, Vector2 size)
        {
            var cell = UiFactory.Rect(row, name);
            cell.sizeDelta = size;
            return cell;
        }

        private void OnDestroy()
        {
            Closed = null;
        }
    }
}
