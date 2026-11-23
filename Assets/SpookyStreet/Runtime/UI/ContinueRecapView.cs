using System.Collections.Generic;
using SpookyStreet.App;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.Save;
using SpookyStreet.Localization;
using SpookyStreet.UI.Nonverbal;
using UnityEngine;
using UnityEngine.UI;

namespace SpookyStreet.UI
{
    /// <summary>
    /// The picture recap beside Continue on the title (engagement §1.10, spec §5): a small paper card with the place
    /// Pip was as its puppet picture, the open question as the notebook's picture chips with its words under them (the
    /// hybrid text line, D23.1), the stickers found so far as tiny stickers and a count, and the soft chapter as candy
    /// dots. A closed case shows the green "Case closed" stamp instead of a question. Built from
    /// <see cref="ContinueRecap"/> only, so it can never show an answer, a hint or a route (D26). Reuses the notebook's
    /// paper, stickers and the puppet atlas; no new art.
    /// </summary>
    public sealed class ContinueRecapView : MonoBehaviour
    {
        public static readonly Vector2 CardSize = new Vector2(420f, 236f);

        private static readonly Color Graphite = new Color32(0x4A, 0x44, 0x58, 0xFF);
        private static readonly Color WitchGreen = new Color32(0x8D, 0xBF, 0x3F, 0xFF);

        private RectTransform _card;
        private Image _place;
        private RectTransform _chips;
        private Text _line;
        private GameObject _stamp;
        private RectTransform _stickers;
        private Text _count;
        private readonly List<Image> _dots = new List<Image>();
        private readonly List<string> _chipParts = new List<string>();
        private readonly List<string> _stickerIds = new List<string>();

        /// <summary>The recap shown (null when the card is hidden).</summary>
        public ContinueRecap Recap { get; private set; }

        public bool Visible => _card != null && _card.gameObject.activeSelf;

        public RectTransform Card => _card;

        /// <summary>The place picture's puppet part (tests).</summary>
        public string PlacePart { get; private set; }

        /// <summary>The chips' puppet parts, in order (tests).</summary>
        public IReadOnlyList<string> ChipParts => _chipParts;

        /// <summary>The tiny stickers' IDs, in order (tests).</summary>
        public IReadOnlyList<string> StickerIds => _stickerIds;

        /// <summary>The text line under the pictures (question words or nothing).</summary>
        public Text Line => _line;

        public GameObject Stamp => _stamp;

        /// <summary>Candy dots filled for the chapter reached.</summary>
        public int FilledDots { get; private set; }

        /// <summary>Every text the card shows right now (tests check that no answer words appear).</summary>
        public IEnumerable<string> Texts
        {
            get
            {
                if (_card == null)
                {
                    yield break;
                }

                foreach (var text in _card.GetComponentsInChildren<Text>(false))
                {
                    yield return text.text;
                }
            }
        }

        public static ContinueRecapView Create(GameUi ui)
        {
            var parent = ui.TitleContent;
            var view = parent.gameObject.AddComponent<ContinueRecapView>();
            view.Build(parent, ui.ContinueSlot);
            view.Show(null);
            return view;
        }

        private void Build(RectTransform parent, Vector2 continueSlot)
        {
            var topLeft = new Vector2(0f, 1f);
            var card = UiFactory.PaperCard(parent, "ContinueRecap", topLeft, Vector2.zero, CardSize, -1.2f,
                UiFactory.WithAlpha(Palette.HauntedPurple, 0.7f));
            _card = card.rectTransform;
            _card.pivot = topLeft;
            _card.anchoredPosition = continueSlot + new Vector2(440f + 34f, 6f);
            card.raycastTarget = false;
            AccessibleName.Set(card.gameObject, SpookyStreet.Core.Localization.LocalizationKeys.Recap.Heading);

            var heading = UiFactory.Label(_card, "Heading", SpookyStreet.Core.Localization.LocalizationKeys.Recap.Heading,
                24, TextAnchor.MiddleLeft, Graphite, UiFactory.BoldFont);
            UiFactory.Place(heading.rectTransform, topLeft, new Vector2(26f, -16f), new Vector2(220f, 34f), topLeft);

            // Soft chapter stops as candy dots (no digits): orange felt for the chapters reached.
            for (int d = 0; d < ContinueRecap.ChapterCount; d++)
            {
                var dot = UiFactory.Sprite(_card, "Chapter" + d, UiFactory.Baked(UiSpriteCatalog.FeltDiscOrange), Color.white,
                    new Vector2(1f, 1f), new Vector2(-30f - (ContinueRecap.ChapterCount - 1 - d) * 24f, -32f),
                    new Vector2(18f, 18f));
                _dots.Add(dot);
            }

            // Where Pip was: the place's puppet picture on a tilted paper tag.
            var placeTag = UiFactory.PaperTag(_card, "Place", null, 20, topLeft, new Vector2(84f, -118f),
                new Vector2(118f, 118f), -2.5f, Color.white);
            placeTag.raycastTarget = false;
            _place = UiFactory.Sprite(placeTag.transform, "Art", null, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(98f, 98f));
            _place.preserveAspect = true;

            _chips = UiFactory.Place(UiFactory.Rect(_card, "Chips"), topLeft, new Vector2(170f, -60f),
                new Vector2(230f, 60f), topLeft);
            _line = UiFactory.Label(_card, "Line", null, 22, TextAnchor.UpperLeft, UiFactory.Ink, UiFactory.BoldFont);
            UiFactory.Place(_line.rectTransform, topLeft, new Vector2(170f, -126f), new Vector2(232f, 58f), topLeft);
            _line.resizeTextMinSize = 18;

            var stamp = UiFactory.PaperTag(_card, "ClosedStamp", SpookyStreet.Core.Localization.LocalizationKeys.Notebook.CaseClosedStamp,
                30, topLeft, new Vector2(286f, -104f), new Vector2(214f, 66f), -8f, Color.white);
            stamp.raycastTarget = false;
            stamp.GetComponentInChildren<Text>().color = WitchGreen;
            UiFactory.StitchLayer(stamp.transform, WitchGreen);
            _stamp = stamp.gameObject;

            _stickers = UiFactory.Place(UiFactory.Rect(_card, "Stickers"), topLeft, new Vector2(170f, -186f),
                new Vector2(232f, 46f), topLeft);
            _count = UiFactory.Label(_card, "Count", null, 20, TextAnchor.MiddleCenter, Graphite, UiFactory.BoldFont);
            UiFactory.Place(_count.rectTransform, topLeft, new Vector2(84f, -204f), new Vector2(150f, 30f));
            _count.resizeTextMinSize = 16;
        }

        /// <summary>Shows <paramref name="recap"/> on the card, or hides the card for null (no save to continue).</summary>
        public void Show(ContinueRecap recap)
        {
            Recap = recap;
            _card.gameObject.SetActive(recap != null);
            if (recap == null)
            {
                return;
            }

            PlacePart = NotebookView.PlacePart(recap.PlaceAnchor);
            _place.sprite = PuppetAtlas.Get(PlacePart);

            FilledDots = recap.Chapter;
            for (int d = 0; d < _dots.Count; d++)
            {
                _dots[d].color = d < recap.Chapter ? Color.white : UiFactory.WithAlpha(Graphite, 0.25f);
            }

            Clear(_chips);
            _chipParts.Clear();
            for (int c = 0; c < recap.Chips.Count; c++)
            {
                var chip = UiFactory.PaperTag(_chips, "Chip" + c, null, 20, new Vector2(0f, 0.5f),
                    new Vector2(28f + c * 62f, 0f), new Vector2(56f, 56f), c % 2 == 0 ? -2f : 2f, Color.white);
                chip.raycastTarget = false;
                var art = UiFactory.Sprite(chip.transform, "Art", PuppetAtlas.Get(recap.Chips[c]), Color.white,
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(46f, 46f));
                art.preserveAspect = true;
                _chipParts.Add(recap.Chips[c]);
            }

            _stamp.SetActive(recap.CaseClosed);
            var localized = _line.GetComponent<LocalizedText>();
            if (!recap.CaseClosed && recap.QuestionKey != null)
            {
                if (localized == null)
                {
                    localized = _line.gameObject.AddComponent<LocalizedText>();
                }

                localized.SetKey(recap.QuestionKey);
                _line.gameObject.SetActive(true);
            }
            else
            {
                _line.gameObject.SetActive(false);
            }

            Clear(_stickers);
            _stickerIds.Clear();
            int icons = Mathf.Min(ContinueRecap.MaxStickerIcons, recap.StickerCount);
            for (int s = 0; s < icons; s++)
            {
                string id = recap.Stickers[recap.StickerCount - icons + s]; // the latest finds
                var card = NotebookView.StickerCard(_stickers, id, new Vector2(0f, 0.5f), new Vector2(26f + s * 54f, 0f),
                    new Vector2(50f, 44f), s % 2 == 0 ? 3f : -3f);
                card.raycastTarget = false;
                _stickerIds.Add(id);
            }

            var count = _count.GetComponent<LocalizedText>();
            if (count == null)
            {
                count = _count.gameObject.AddComponent<LocalizedText>();
            }

            count.SetKey(recap.StickersKey, () => new object[] { recap.StickerCount });
        }

        private static void Clear(RectTransform parent)
        {
            for (int i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                child.SetActive(false);
                Destroy(child);
            }
        }
    }
}
