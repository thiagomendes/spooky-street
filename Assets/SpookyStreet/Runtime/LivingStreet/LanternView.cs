using System;
using System.Collections.Generic;
using SpookyStreet.App;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.LivingStreet;
using SpookyStreet.Core.World;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Localization;
using SpookyStreet.UI;
using UnityEngine;
using UnityEngine.UI;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.LivingStreet;

namespace SpookyStreet.LivingStreet
{
    /// <summary>
    /// The paper lantern table's screen (living-street §4.2): a row of four shape cards (picture and name), a row of four
    /// colour swatches (no words; their accessible names say the colour), and a big "Fold it!" button. Choosing
    /// recolours the cards at once, so a pre-reader sees the lantern before folding it. No scrim: Pip stands at the
    /// table in the world. Touch first; gamepad and keyboard move a focus outline through the S004 UI base (Back closes).
    /// </summary>
    public sealed class LanternView : MonoBehaviour
    {
        /// <summary>A shape card (Core <see cref="LanternPickerLayout"/>: wide enough for the longest name in every language).</summary>
        public static readonly Vector2 CardSize = new Vector2(LanternPickerLayout.CardWidth, LanternPickerLayout.CardHeight);

        /// <summary>The picture's size on a card; the bat's tile is drawn larger (its wings leave a small body).</summary>
        public const float PictureSize = 118f;
        public const float BatPictureScale = 1.3f;
        public const float SwatchSize = 96f;

        private readonly Dictionary<LanternShape, Image> _shapes = new Dictionary<LanternShape, Image>();
        private readonly Dictionary<LanternShape, Image> _mats = new Dictionary<LanternShape, Image>();
        private readonly Dictionary<LanternShape, RectTransform> _slots = new Dictionary<LanternShape, RectTransform>();
        private readonly Dictionary<LanternShape, Image> _stitches = new Dictionary<LanternShape, Image>();
        private readonly Dictionary<LanternShape, List<Image>> _shapeArt = new Dictionary<LanternShape, List<Image>>();
        private readonly Dictionary<LanternColour, Image> _swatches = new Dictionary<LanternColour, Image>();
        private readonly Dictionary<LanternShape, Button> _shapeButtons = new Dictionary<LanternShape, Button>();
        private readonly Dictionary<LanternColour, Button> _colourButtons = new Dictionary<LanternColour, Button>();
        private UiFocusScreen _nav;

        public LanternShape Shape { get; private set; } = LanternCraft.Default.Shape;
        public LanternColour Colour { get; private set; } = LanternCraft.Default.Colour;

        /// <summary>"Fold it!": the chosen lantern.</summary>
        public event Action<PipLantern> Folded;

        /// <summary>The close disc (or Back) without folding.</summary>
        public event Action Closed;

        public bool IsOpen => gameObject.activeSelf;

        public Button FoldButton { get; private set; }

        public Button CloseButton { get; private set; }

        public UiFocusScreen Navigation => _nav;

        public Button ShapeButton(LanternShape shape) => _shapeButtons[shape];

        /// <summary>The name under each shape's card (tests: readable names).</summary>
        public Dictionary<LanternShape, Text> ShapeNames { get; } = new Dictionary<LanternShape, Text>();

        /// <summary>The card of a shape (tests: its tint and scale).</summary>
        public Image ShapeCard(LanternShape shape) => _shapes[shape];

        /// <summary>The plum mat under a shape's card, shown only while it is the chosen one (tests).</summary>
        public Image ChosenMat(LanternShape shape) => _mats[shape];

        /// <summary>The slot holding a shape's card and mat; its scale is the chosen card's size (tests).</summary>
        public RectTransform ShapeSlot(LanternShape shape) => _slots[shape];

        /// <summary>The pictures on a shape's card (tests: never faded).</summary>
        public IReadOnlyList<Image> ShapeArt(LanternShape shape) => _shapeArt[shape];

        /// <summary>The paper board behind the rows (tests: it fits the screen and clears the close disc).</summary>
        public RectTransform Board { get; private set; }

        public Button ColourButton(LanternColour colour) => _colourButtons[colour];

        public static LanternView Create(RectTransform canvasRoot)
        {
            var root = UiFactory.Panel(canvasRoot, "LanternTable", new Color(0f, 0f, 0f, 0f));
            var view = root.gameObject.AddComponent<LanternView>();
            view.Build(root.rectTransform);
            root.gameObject.SetActive(false);
            return view;
        }

        private void Build(RectTransform root)
        {
            var content = UiFactory.Stretch(UiFactory.Rect(root, "Content"));
            content.gameObject.AddComponent<SafeAreaFitter>();
            var board = UiFactory.PaperCard(content, "Board", new Vector2(0.5f, 0.5f),
                new Vector2(0f, LanternPickerLayout.BoardOffsetY),
                new Vector2(LanternPickerLayout.BoardWidth, LanternPickerLayout.BoardHeight), -0.6f);
            Board = board.rectTransform;
            var rows = board.transform;

            var matTint = Palette.Hex(LanternPickerLayout.ChosenMatRgb);
            for (int i = 0; i < LanternCraft.Shapes.Count; i++)
            {
                var shape = LanternCraft.Shapes[i];
                float tilt = i % 2 == 0 ? -1.5f : 1.2f;
                // Each card sits in a slot that carries the chosen card's size (the card's own PressScale owns the
                // card's scale), with the plum mat behind the card.
                var slot = UiFactory.Place(UiFactory.Rect(rows, "Slot_" + LanternCraft.ShapeId(shape)), new Vector2(0.5f, 1f),
                    new Vector2(LanternPickerLayout.ColumnX(i), -36f - CardSize.y * 0.5f), CardSize);
                _slots[shape] = slot;
                var mat = UiFactory.Sprite(slot, "Chosen", null, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero,
                    CardSize + Vector2.one * (2f * LanternPickerLayout.ChosenMatPad));
                UiFactory.ApplySurface(mat, UiSpriteCatalog.PaperTag, Palette.WarmPaper, matTint);
                mat.rectTransform.localRotation = Quaternion.Euler(0f, 0f, tilt);
                mat.raycastTarget = false;
                UiFactory.InkShadow(mat, new Vector2(4f, -6f), 0.6f);
                mat.gameObject.SetActive(false);
                _mats[shape] = mat;
                var card = UiFactory.PaperCard(slot, "Shape_" + LanternCraft.ShapeId(shape), new Vector2(0.5f, 0.5f),
                    Vector2.zero, CardSize, tilt);
                card.raycastTarget = true;
                var stitch = card.transform.Find("Stitch");
                _stitches[shape] = stitch != null ? stitch.GetComponent<Image>() : null;
                var picture = UiFactory.Rect(card.transform, "Picture");
                UiFactory.Place(picture, new Vector2(0.5f, 0.5f), new Vector2(0f, 20f), new Vector2(PictureSize, PictureSize));
                // The lantern art's picture (neutral paper, ink face): tinted with the chosen colour, ink stays ink.
                var tile = Resources.Load<Sprite>(LanternArt.ShapeTile(LanternCraft.ShapeId(shape)));
                List<Image> art;
                if (tile != null)
                {
                    var image = UiFactory.Sprite(picture, "Tile", tile, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero,
                        new Vector2(PictureSize, PictureSize));
                    image.preserveAspect = true;
                    art = new List<Image> { image };
                }
                else
                {
                    art = LanternGlyph.Build(picture, shape, PictureSize, Color.white);
                }

                if (shape == LanternShape.Bat)
                {
                    picture.localScale = Vector3.one * BatPictureScale;
                }

                // The name under the picture (Andika needs about 1.6 lines of height: CI run 204 lost a 36-unit label).
                // The card is wide enough for the longest name in every language at full size, with margin
                // (LanternPickerLayout); the text is never shortened for it.
                var name = UiFactory.Label(card.transform, "Name", LanternCraft.ShapeKey(shape),
                    LanternPickerLayout.LabelFontSize, TextAnchor.MiddleCenter, UiFactory.Ink, UiFactory.BoldFont);
                name.verticalOverflow = VerticalWrapMode.Overflow;
                UiFactory.Place(name.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 26f),
                    new Vector2(LanternPickerLayout.LabelWidth, 48f));
                ShapeNames[shape] = name;
                var button = card.gameObject.AddComponent<Button>();
                button.targetGraphic = card;
                button.transition = Selectable.Transition.None;
                card.gameObject.AddComponent<PressScale>();
                var captured = shape;
                button.onClick.AddListener(() => ChooseShape(captured));
                _shapes[shape] = card;
                _shapeArt[shape] = art;
                _shapeButtons[shape] = button;
            }

            for (int i = 0; i < LanternCraft.Colours.Count; i++)
            {
                var colour = LanternCraft.Colours[i];
                float x = LanternPickerLayout.ColumnX(i);
                // The lantern art's colour picture (a lit square of that tissue), drawn as it is; a plain disc otherwise.
                var colourTile = Resources.Load<Sprite>(LanternArt.ColourTile(LanternCraft.ColourId(colour)));
                var swatch = UiFactory.Sprite(rows, "Colour_" + LanternCraft.ColourId(colour), colourTile != null ? colourTile : UiFactory.Circle,
                    colourTile != null ? Color.white : Palette.Hex(LanternCraft.ColourRgb(colour)), new Vector2(0.5f, 1f),
                    new Vector2(x, -CardSize.y - 112f), new Vector2(SwatchSize, SwatchSize), raycast: true);
                swatch.preserveAspect = true;
                UiFactory.InkShadow(swatch, new Vector2(3f, -4f), 0.5f);
                var ring = UiFactory.Sprite(swatch.transform, "Chosen", UiFactory.Baked(UiSpriteCatalog.StitchRing),
                    Palette.InkBlack, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(SwatchSize + 22f, SwatchSize + 22f));
                ring.gameObject.SetActive(false);
                var button = swatch.gameObject.AddComponent<Button>();
                button.targetGraphic = swatch;
                button.transition = Selectable.Transition.None;
                swatch.gameObject.AddComponent<PressScale>();
                AccessibleName.Set(swatch.gameObject, LanternCraft.ColourKey(colour));
                var captured = colour;
                button.onClick.AddListener(() => ChooseColour(captured));
                _swatches[colour] = swatch;
                _colourButtons[colour] = button;
            }

            FoldButton = UiFactory.PrimaryButton(rows, "Fold", Keys.Fold, new Vector2(0.5f, 0f), new Vector2(0f, 70f),
                new Vector2(300f, 88f), Fold);

            var disc = UiFactory.FeltDisc(content, "CloseButton", UiSpriteCatalog.FeltDiscNight, Palette.WarmPaper,
                UiSpriteCatalog.IconClose, Palette.WarmPaper, null, UiFactory.SystemDiscDiameter, Vector2.one,
                new Vector2(-60f, -58f), 1f);
            CloseButton = disc.gameObject.AddComponent<Button>();
            CloseButton.targetGraphic = disc;
            CloseButton.transition = Selectable.Transition.None;
            CloseButton.onClick.AddListener(Close);
            AccessibleName.Set(CloseButton.gameObject, Keys.Close);

            _nav = UiFocusScreen.Attach(gameObject, LanternCraft.FocusGraph(), content);
            _nav.Bind(LanternCraft.FocusClose, CloseButton);
            _nav.Bind(LanternCraft.FocusFold, FoldButton);
            foreach (var pair in _shapeButtons)
            {
                _nav.Bind(LanternCraft.FocusShape(pair.Key), pair.Value);
            }

            foreach (var pair in _colourButtons)
            {
                _nav.Bind(LanternCraft.FocusColour(pair.Key), pair.Value);
            }

            _nav.AddPrompt(ControlPrompt.Back, CloseButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, -40f), 48f);
            Refresh();
        }

        /// <summary>Opens on <paramref name="current"/> (Pip's lantern, or the table's default the first time).</summary>
        public void Open(PipLantern current)
        {
            Shape = current.Shape;
            Colour = current.Colour;
            gameObject.SetActive(true);
            Refresh();
            _nav.FocusOn(LanternCraft.FocusShape(Shape));
        }

        public void ChooseShape(LanternShape shape)
        {
            Shape = shape;
            Refresh();
        }

        public void ChooseColour(LanternColour colour)
        {
            Colour = colour;
            Refresh();
        }

        /// <summary>
        /// Every card keeps full paper and a full-strength picture in the chosen paper colour, so a resting card reads as
        /// well as the chosen one (it used to be dimmed and faded). The chosen card stands out by more than colour: it
        /// sits on a plum mat, grows a little and its stitch turns orange (Core <see cref="LanternPickerLayout"/>).
        /// </summary>
        private void Refresh()
        {
            var tint = Palette.Hex(LanternCraft.ColourRgb(Colour));
            var resting = Palette.Hex(LanternPickerLayout.RestingCardTintRgb);
            foreach (var pair in _shapes)
            {
                bool chosen = pair.Key == Shape;
                pair.Value.color = chosen ? Color.white : resting;
                _slots[pair.Key].localScale = Vector3.one * (chosen ? LanternPickerLayout.ChosenScale : 1f);
                _mats[pair.Key].gameObject.SetActive(chosen);
                var stitch = _stitches[pair.Key];
                if (stitch != null)
                {
                    stitch.color = Palette.Hex(chosen ? LanternPickerLayout.ChosenStitchRgb : LanternPickerLayout.RestingStitchRgb);
                }

                foreach (var part in _shapeArt[pair.Key])
                {
                    part.color = new Color(tint.r, tint.g, tint.b, chosen ? 1f : LanternPickerLayout.RestingArtAlpha);
                }
            }

            foreach (var pair in _swatches)
            {
                pair.Value.transform.Find("Chosen").gameObject.SetActive(pair.Key == Colour);
            }
        }

        public void Fold()
        {
            if (!IsOpen)
            {
                return;
            }

            gameObject.SetActive(false);
            Folded?.Invoke(new PipLantern(Shape, Colour));
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

        /// <summary>Closes without raising events (back to title, a reset).</summary>
        public void Cancel() => gameObject.SetActive(false);
    }
}
