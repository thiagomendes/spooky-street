using System;
using System.Collections.Generic;
using SpookyStreet.App;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.LivingStreet;
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
        public static readonly Vector2 CardSize = new Vector2(150f, 150f);
        public const float SwatchSize = 96f;

        private readonly Dictionary<LanternShape, Image> _shapes = new Dictionary<LanternShape, Image>();
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
            float width = (CardSize.x + 24f) * 4f + 60f;
            var board = UiFactory.PaperCard(content, "Board", new Vector2(0.5f, 0.5f), new Vector2(0f, 30f),
                new Vector2(width, 520f), -0.6f);
            var rows = board.transform;

            for (int i = 0; i < LanternCraft.Shapes.Count; i++)
            {
                var shape = LanternCraft.Shapes[i];
                float x = (i - 1.5f) * (CardSize.x + 24f);
                var card = UiFactory.PaperCard(rows, "Shape_" + LanternCraft.ShapeId(shape), new Vector2(0.5f, 1f),
                    new Vector2(x, -36f - CardSize.y * 0.5f), CardSize, i % 2 == 0 ? -1.5f : 1.2f);
                card.raycastTarget = true;
                var picture = UiFactory.Rect(card.transform, "Picture");
                UiFactory.Place(picture, new Vector2(0.5f, 0.5f), new Vector2(0f, 14f), new Vector2(96f, 96f));
                var art = LanternGlyph.Build(picture, shape, 96f, Color.white);
                var name = UiFactory.Label(card.transform, "Name", LanternCraft.ShapeKey(shape), 24, TextAnchor.MiddleCenter,
                    UiFactory.Ink, UiFactory.BoldFont);
                UiFactory.Place(name.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 22f), new Vector2(CardSize.x - 12f, 36f));
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
                float x = (i - 1.5f) * (CardSize.x + 24f);
                var swatch = UiFactory.Sprite(rows, "Colour_" + LanternCraft.ColourId(colour), UiFactory.Circle,
                    Palette.Hex(LanternCraft.ColourRgb(colour)), new Vector2(0.5f, 1f), new Vector2(x, -CardSize.y - 120f),
                    new Vector2(SwatchSize, SwatchSize), raycast: true);
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

        /// <summary>The chosen card stands up straight and its picture takes the chosen paper colour.</summary>
        private void Refresh()
        {
            var tint = Palette.Hex(LanternCraft.ColourRgb(Colour));
            foreach (var pair in _shapes)
            {
                bool chosen = pair.Key == Shape;
                pair.Value.color = chosen ? Color.white : new Color(0.86f, 0.84f, 0.8f, 1f);
                pair.Value.rectTransform.localScale = Vector3.one * (chosen ? 1.08f : 1f);
                foreach (var part in _shapeArt[pair.Key])
                {
                    part.color = chosen ? tint : new Color(tint.r, tint.g, tint.b, 0.55f);
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
