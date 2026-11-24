using System;
using System.Collections.Generic;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.Keepsakes;
using SpookyStreet.Core.World;
using SpookyStreet.Localization;
using SpookyStreet.UI.Nonverbal;
using UnityEngine;
using UnityEngine.UI;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.Keepsakes;

namespace SpookyStreet.UI
{
    /// <summary>
    /// The notebook's keepsakes page (S004 §2.1, stream A2), after the case pages once a paper bat is found: the old
    /// sketch pages earned so far, each with its caption under the drawing, and a small stack of the folded bats not yet
    /// in a page with its own number (never a total, no empty frame for a page to come, engagement §2.3 and §2.5). The
    /// page completed by the 10th, 20th or 30th bat assembles from its ten bats the next time the notebook opens: ten
    /// folded pieces cut on the drawing's 5 × 2 crease grid fly from the stack into place and the caption follows
    /// (they simply fade in with reduced motion).
    /// </summary>
    public sealed partial class NotebookView
    {
        public const float AssemblySeconds = 1.6f;
        private const float PieceStagger = 0.08f;
        private const float PieceFlight = 0.6f;

        private sealed class AssemblyPiece
        {
            public RectTransform Rect;
            public RawImage Image;
            public Vector2 From;
            public Vector2 To;
            public float Delay;
            public float Spin;
        }

        private readonly List<Image> _sketches = new List<Image>();
        private readonly List<Text> _sketchCaptions = new List<Text>();
        private readonly List<AssemblyPiece> _assembly = new List<AssemblyPiece>();
        private RectTransform _keepsakePage;
        private RectTransform _batStack;
        private float _assemblyTime = -1f;

        /// <summary>The keepsakes (set by the director); null shows no keepsakes page.</summary>
        public Func<KeepsakeState> Keepsakes { get; set; }

        /// <summary>Pages completed since the notebook last opened (set by the director).</summary>
        public KeepsakeNotebook KeepsakePages { get; set; }

        /// <summary>The keepsakes page (null when there is none).</summary>
        public RectTransform KeepsakePage => _keepsakePage;

        /// <summary>Its index in the carousel (-1 when there is none).</summary>
        public int KeepsakePageIndex => _keepsakePage != null ? _pages.IndexOf(_keepsakePage) : -1;

        /// <summary>The sketch pages shown (earned only), in order.</summary>
        public IReadOnlyList<Image> SketchImages => _sketches;

        public IReadOnlyList<Text> SketchCaptions => _sketchCaptions;

        /// <summary>The number beside the bat stack (null when the stack is empty).</summary>
        public Text BatStackLabel { get; private set; }

        /// <summary>Folded bats drawn in the stack.</summary>
        public int BatStackCount { get; private set; }

        /// <summary>The page assembling now (0 when none).</summary>
        public int AssemblingPage { get; private set; }

        /// <summary>Assemblies played since the scene loaded (tests: once per completed page).</summary>
        public int AssembliesPlayed { get; private set; }

        private void BuildKeepsakePage()
        {
            _sketches.Clear();
            _sketchCaptions.Clear();
            ClearAssembly();
            _keepsakePage = null;
            _batStack = null;
            BatStackLabel = null;
            BatStackCount = 0;
            var state = Keepsakes != null ? Keepsakes() : null;
            if (!KeepsakeNotebook.HasPage(state))
            {
                return;
            }

            var page = NewPage("KeepsakePage");
            _keepsakePage = page;
            _pageViews.Add(new PageView { Page = page, Question = null, Overlay = UiFactory.Stretch(UiFactory.Rect(page, "Overlay")) });
            AccessibleName.Set(page.gameObject, Keys.NotebookPage);

            var pages = state.Pages;
            bool stack = KeepsakeNotebook.ShowsStack(state);
            // Up to three drawings side by side over the page's width (the stack takes the lower right when present).
            float width = pages.Count switch { 1 => 470f, 2 => stack ? 320f : 380f, _ => 268f };
            float height = width * KeepsakeArt.SketchPageHeight / KeepsakeArt.SketchPageWidth;
            float span = PageSize.x - 80f - (stack && pages.Count < 3 ? 180f : 0f);
            float step = pages.Count > 0 ? span / pages.Count : 0f;
            for (int i = 0; i < pages.Count; i++)
            {
                float x = -PageSize.x * 0.5f + 40f + step * (i + 0.5f);
                float y = pages.Count == 3 ? 40f : 30f;
                var sprite = Resources.Load<Sprite>(KeepsakeArt.SketchPage(pages[i].Order));
                var image = UiFactory.Sprite(page, "Sketch" + pages[i].Order, sprite, Color.white, new Vector2(0.5f, 0.5f),
                    new Vector2(x, y), new Vector2(width, height));
                image.preserveAspect = true;
                image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, i % 2 == 0 ? -1.5f : 1.2f);
                _sketches.Add(image);
                var caption = UiFactory.Label(page, "Caption" + pages[i].Order, pages[i].CaptionKey, 24,
                    TextAnchor.UpperCenter, UiFactory.Ink, UiFactory.BodyFont);
                UiFactory.Place(caption.rectTransform, new Vector2(0.5f, 0.5f), new Vector2(x, y - height * 0.5f - 46f),
                    new Vector2(Mathf.Min(step - 16f, 420f), 80f));
                caption.verticalOverflow = VerticalWrapMode.Overflow;
                _sketchCaptions.Add(caption);
            }

            if (stack)
            {
                BuildBatStack(page, state.LooseBats, pages.Count);
            }
        }

        /// <summary>
        /// The folded bats not yet in a page, each one drawn, with their number under them: a 3-wide pile in the free
        /// column beside one or two drawings (or the page's middle with none), a single row under three drawings. CI run
        /// 180: an ink-tinted pile merged into one blot and its number fell off the page's edge.
        /// </summary>
        private void BuildBatStack(RectTransform page, int count, int drawings)
        {
            _batStack = UiFactory.Rect(page, "BatStack");
            bool row = drawings >= 3;
            float scale = drawings == 0 ? 1.4f : 1f;
            Vector2 cell = new Vector2(row ? 46f : 58f, 46f) * scale;
            Vector2 size = row ? new Vector2(cell.x * 8f + 80f, 60f) : new Vector2(cell.x * 2f + 80f, cell.y * 3f + 70f);
            var at = drawings == 0 ? new Vector2(0f, 30f)
                : row ? new Vector2(0f, -PageSize.y * 0.5f + 66f)
                : new Vector2(PageSize.x * 0.5f - (drawings == 1 ? 175f : 110f), 50f);
            UiFactory.Place(_batStack, new Vector2(0.5f, 0.5f), at, size);
            var sprite = PuppetAtlas.Get("pg.bat");
            for (int i = 0; i < count; i++)
            {
                // Folded paper dropped on the page: each a little turned.
                int column = row ? i : i % 3;
                int line = row ? 0 : i / 3;
                float x = row ? (column - (count - 1) * 0.5f) * cell.x : (column - 1) * cell.x;
                // Rows fill from the label up, so the pile always sits on its number (run 183: a gap under three bats).
                int rows = (count + 2) / 3;
                float y = row ? 0f : -size.y * 0.5f + 56f + cell.y * 0.5f + (rows - 1 - line) * cell.y;
                var bat = UiFactory.Sprite(_batStack, "FoldedBat" + i, sprite, Color.white, new Vector2(0.5f, 0.5f),
                    new Vector2(x, y), new Vector2(row ? 60f : 84f, 56f) * scale);
                bat.preserveAspect = true;
                bat.rectTransform.localRotation = Quaternion.Euler(0f, 0f, ((i * 53) % 25) - 12f);
            }

            BatStackCount = count;
            var label = UiFactory.Label(_batStack, "Count", null, 28, TextAnchor.MiddleCenter, UiFactory.Ink, UiFactory.BoldFont);
            UiFactory.Place(label.rectTransform, row ? new Vector2(1f, 0.5f) : new Vector2(0.5f, 0f),
                row ? new Vector2(110f, 0f) : new Vector2(0f, 8f), new Vector2(220f, 44f));
            label.gameObject.AddComponent<LocalizedText>().SetPlural(Keys.NotebookBatStack, () => count,
                () => new object[] { count });
            BatStackLabel = label;
        }

        /// <summary>On opening: a page completed since last time turns to the keepsakes page and assembles there, once.</summary>
        private void OpenKeepsakeAssembly()
        {
            var pending = KeepsakePages != null ? KeepsakePages.TakePending() : null;
            if (pending == null || pending.Count == 0 || _keepsakePage == null)
            {
                return;
            }

            ShowPage(KeepsakePageIndex, true);
            int page = pending[pending.Count - 1];
            var state = Keepsakes != null ? Keepsakes() : null;
            int index = state != null ? IndexOfPage(state, page) : -1;
            if (index < 0 || index >= _sketches.Count)
            {
                return;
            }

            StartAssembly(page, _sketches[index], _sketchCaptions[index]);
        }

        private static int IndexOfPage(KeepsakeState state, int order)
        {
            var pages = state.Pages;
            for (int i = 0; i < pages.Count; i++)
            {
                if (pages[i].Order == order)
                {
                    return i;
                }
            }

            return -1;
        }

        private void StartAssembly(int page, Image sketch, Text caption)
        {
            ClearAssembly();
            AssemblingPage = page;
            AssembliesPlayed++;
            _assemblyTime = 0f;
            var sprite = sketch.sprite;
            var rect = sketch.rectTransform;
            Vector2 size = rect.sizeDelta;
            Vector2 from = _batStack != null ? (Vector2)rect.parent.InverseTransformPoint(_batStack.position) : new Vector2(0f, -PageSize.y * 0.5f);
            int index = 0;
            for (int row = 0; row < 2; row++)
            {
                for (int column = 0; column < 5; column++)
                {
                    var go = new GameObject("Fold" + index, typeof(RectTransform), typeof(RawImage));
                    var piece = (RectTransform)go.transform;
                    piece.SetParent(rect.parent, false);
                    piece.sizeDelta = new Vector2(size.x / 5f, size.y / 2f);
                    var raw = go.GetComponent<RawImage>();
                    raw.raycastTarget = false;
                    if (sprite != null)
                    {
                        var tex = sprite.texture;
                        var r = sprite.textureRect;
                        raw.texture = tex;
                        raw.uvRect = new Rect((r.x + r.width * column / 5f) / tex.width, (r.y + r.height * (1 - row) / 2f) / tex.height,
                            r.width / 5f / tex.width, r.height / 2f / tex.height);
                    }
                    else
                    {
                        raw.color = Palette.WarmPaper;
                    }

                    Vector2 to = rect.anchoredPosition + new Vector2((column - 2f) * size.x / 5f, (0.5f - row) * size.y / 2f);
                    _assembly.Add(new AssemblyPiece
                    {
                        Rect = piece,
                        Image = raw,
                        From = from,
                        To = to,
                        Delay = index * PieceStagger,
                        Spin = (index % 2 == 0 ? 1f : -1f) * (120f + index * 17f),
                    });
                    index++;
                }
            }

            sketch.enabled = false;
            caption.color = UiFactory.WithAlpha(UiFactory.Ink, 0f);
            TickKeepsakeAssembly(0f);
        }

        private void TickKeepsakeAssembly(float dt)
        {
            if (_assemblyTime < 0f)
            {
                return;
            }

            _assemblyTime += dt;
            bool still = ReducedMotion;
            foreach (var piece in _assembly)
            {
                float t = Mathf.Clamp01((_assemblyTime - piece.Delay) / PieceFlight);
                float e = 1f - (1f - t) * (1f - t);
                if (still)
                {
                    piece.Rect.anchoredPosition = piece.To;
                    piece.Rect.localRotation = Quaternion.identity;
                    piece.Rect.localScale = Vector3.one;
                    piece.Image.color = new Color(1f, 1f, 1f, t);
                }
                else
                {
                    piece.Rect.anchoredPosition = Vector2.Lerp(piece.From, piece.To, e) + Vector2.up * Mathf.Sin(t * Mathf.PI) * 60f;
                    piece.Rect.localRotation = Quaternion.Euler(0f, 0f, piece.Spin * (1f - e));
                    piece.Rect.localScale = Vector3.one * Mathf.Lerp(0.35f, 1f, e);
                    piece.Image.color = new Color(1f, 1f, 1f, Mathf.Clamp01(t * 3f));
                }
            }

            if (_assemblyTime >= AssemblySeconds)
            {
                FinishAssembly();
            }
        }

        private void FinishAssembly()
        {
            int index = _sketches.FindIndex(s => s != null && !s.enabled);
            ClearAssembly();
            if (index >= 0)
            {
                _sketches[index].enabled = true;
                _sketchCaptions[index].color = UiFactory.Ink;
                Core.Audio.AudioCues.CorrectGlow();
            }
        }

        private void ClearAssembly()
        {
            foreach (var piece in _assembly)
            {
                if (piece.Rect != null)
                {
                    Destroy(piece.Rect.gameObject);
                }
            }

            _assembly.Clear();
            _assemblyTime = -1f;
            AssemblingPage = 0;
        }

        /// <summary>True while the pieces are flying (tests, captures).</summary>
        public bool Assembling => _assemblyTime >= 0f;
    }
}
