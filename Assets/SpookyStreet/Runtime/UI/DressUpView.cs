using System;
using System.Collections.Generic;
using SpookyStreet.App;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.Keepsakes;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Core.World;
using SpookyStreet.Localization;
using SpookyStreet.UI.Nonverbal;
using UnityEngine;
using UnityEngine.UI;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.Keepsakes;

namespace SpookyStreet.UI
{
    /// <summary>
    /// The dress-up trunk's screen (S004 §2.1, stream A2): over the dressing view of Pip in front of her open trunk, one
    /// paper card per <b>earned</b> piece in a column on the right (no locked card, no empty slot), each a picture with
    /// its name under it; a worn piece's card glows with a candy stitch and a tick. Tapping a card puts the piece on or
    /// takes it off, at once, on Pip. The night felt disc closes the trunk. Touch first; gamepad and keyboard move a
    /// focus outline over the same cards through the S004 UI base (<see cref="UiFocusScreen"/>, graph
    /// <see cref="DressUpFocus"/>): Submit presses, Back closes.
    /// </summary>
    public sealed class DressUpView : MonoBehaviour
    {
        public static readonly Vector2 CardSize = new Vector2(330f, 176f);
        public const float CardGap = 18f;

        private sealed class Card
        {
            public CostumePiece Piece;
            public Button Button;
            public Image Face;
            public Image Picture;
            public Image Stitch;
            public GameObject Tick;
        }

        private readonly List<Card> _cards = new List<Card>();
        private RectTransform _column;
        private UiFocusScreen _nav;

        /// <summary>A card was pressed: put the piece on (true) or take it off.</summary>
        public Action<CostumePiece, bool> Toggled { get; set; }

        /// <summary>The close disc (or Back).</summary>
        public event Action Closed;

        public bool IsOpen => gameObject.activeSelf;

        public Button CloseButton { get; private set; }

        public UiFocusScreen Navigation => _nav;

        /// <summary>The card of <paramref name="piece"/> (tests; hidden when unearned).</summary>
        public Button CardOf(CostumePiece piece)
        {
            foreach (var card in _cards)
            {
                if (card.Piece == piece)
                {
                    return card.Button;
                }
            }

            return null;
        }

        /// <summary>Cards shown now, in order (only earned pieces).</summary>
        public int VisibleCards
        {
            get
            {
                int n = 0;
                foreach (var card in _cards)
                {
                    n += card.Button.gameObject.activeSelf ? 1 : 0;
                }

                return n;
            }
        }

        public static DressUpView Create(RectTransform canvasRoot)
        {
            // No scrim: the child watches Pip change in the world. A clear panel only keeps taps off the HUD below.
            var root = UiFactory.Panel(canvasRoot, "DressUp", new Color(0f, 0f, 0f, 0f));
            var view = root.gameObject.AddComponent<DressUpView>();
            view.Build(root.rectTransform);
            root.gameObject.SetActive(false);
            return view;
        }

        private void Build(RectTransform root)
        {
            var content = UiFactory.Stretch(UiFactory.Rect(root, "Content"));
            content.gameObject.AddComponent<SafeAreaFitter>();
            _column = UiFactory.Rect(content, "Cards");
            UiFactory.Place(_column, new Vector2(1f, 0.5f), new Vector2(-CardSize.x * 0.5f - 150f, -10f),
                new Vector2(CardSize.x, CardSize.y * 3f + CardGap * 2f));

            var disc = UiFactory.FeltDisc(content, "CloseButton", UiSpriteCatalog.FeltDiscNight, Palette.WarmPaper,
                UiSpriteCatalog.IconClose, Palette.WarmPaper, null, UiFactory.SystemDiscDiameter, Vector2.one,
                new Vector2(-60f, -58f), 1f);
            CloseButton = disc.gameObject.AddComponent<Button>();
            CloseButton.targetGraphic = disc;
            CloseButton.transition = Selectable.Transition.None;
            CloseButton.onClick.AddListener(Close);
            AccessibleName.Set(CloseButton.gameObject, Keys.TrunkClose);

            int index = 0;
            foreach (var piece in CostumePieces.All)
            {
                _cards.Add(BuildCard(piece, index++));
            }

            _nav = UiFocusScreen.Attach(gameObject, DressUpFocus.Build(), content);
            _nav.Bind(DressUpFocus.Close, CloseButton);
            foreach (var card in _cards)
            {
                _nav.Bind(DressUpFocus.Piece(card.Piece), card.Button);
            }

            _nav.AddPrompt(ControlPrompt.Back, CloseButton.transform, new Vector2(0.5f, 0f), new Vector2(0f, -40f), 48f);
        }

        private Card BuildCard(CostumePiece piece, int index)
        {
            var card = new Card { Piece = piece };
            float y = (1 - index) * (CardSize.y + CardGap);
            card.Face = UiFactory.PaperCard(_column, "Card_" + piece.Id, new Vector2(0.5f, 0.5f), new Vector2(0f, y),
                CardSize, index % 2 == 0 ? -1.2f : 1f);
            card.Face.raycastTarget = true;
            card.Stitch = card.Face.transform.Find("Stitch") != null
                ? card.Face.transform.Find("Stitch").GetComponent<Image>()
                : null;
            var sprite = Resources.Load<Sprite>(KeepsakeArt.CostumeIcon(piece.Id));
            if (sprite == null)
            {
                sprite = PuppetAtlas.Get(PuppetPartFor(piece));
            }
            card.Picture = UiFactory.Sprite(card.Face.transform, "Picture", sprite, Color.white, new Vector2(0f, 0.5f),
                new Vector2(92f, 0f), new Vector2(150f, 150f));
            card.Picture.preserveAspect = true;
            var name = UiFactory.Label(card.Face.transform, "Name", DressUpFocus.NameKey(piece), 30, TextAnchor.MiddleLeft,
                UiFactory.Ink, UiFactory.BoldFont);
            UiFactory.Place(name.rectTransform, new Vector2(0f, 0.5f), new Vector2(176f, 0f), new Vector2(140f, 120f),
                new Vector2(0f, 0.5f));
            var tick = UiFactory.Sprite(card.Face.transform, "Worn", UiFactory.Baked(UiSpriteCatalog.IconCheck),
                Palette.JackOrange, new Vector2(1f, 1f), new Vector2(-28f, -28f), new Vector2(44f, 44f));
            card.Tick = tick.gameObject;
            card.Button = card.Face.gameObject.AddComponent<Button>();
            card.Button.targetGraphic = card.Face;
            card.Button.transition = Selectable.Transition.None;
            card.Face.gameObject.AddComponent<PressScale>();
            var captured = card;
            card.Button.onClick.AddListener(() => Toggled?.Invoke(captured.Piece, !captured.Tick.activeSelf));
            return card;
        }

        private static string PuppetPartFor(CostumePiece piece) =>
            piece == CostumePieces.StarCape ? "pg.cape" : piece == CostumePieces.LanternPail ? "pg.lamp" : "pg.star";

        /// <summary>Opens on <paramref name="state"/>: one card per earned piece, worn ones marked.</summary>
        public void Open(KeepsakeState state)
        {
            gameObject.SetActive(true);
            Refresh(state);
            _nav.FocusOn(DressUpFocus.Piece(CostumePieces.StarCape));
        }

        /// <summary>Shows only earned cards, stacked from the top, and marks the worn ones.</summary>
        public void Refresh(KeepsakeState state)
        {
            state ??= KeepsakeState.Empty;
            int earned = KeepsakeRules.PiecesEarned(state.BatCount);
            int shown = 0;
            foreach (var card in _cards)
            {
                bool on = card.Piece.Order <= earned;
                card.Button.gameObject.SetActive(on);
                if (!on)
                {
                    continue;
                }

                var rect = card.Face.rectTransform;
                rect.anchoredPosition = new Vector2(0f, (1 - shown) * (CardSize.y + CardGap));
                shown++;
                bool worn = state.IsWearing(card.Piece.Id);
                card.Tick.SetActive(worn);
                if (card.Stitch != null)
                {
                    card.Stitch.color = worn ? Palette.CandyCornYellow : Palette.HauntedPurple;
                }

                card.Face.color = worn ? Color.white : new Color(0.86f, 0.84f, 0.8f, 1f);
                card.Picture.color = worn ? Color.white : new Color(1f, 1f, 1f, 0.82f);
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

        /// <summary>Closes without raising <see cref="Closed"/> (back to title, a reset).</summary>
        public void Cancel() => gameObject.SetActive(false);
    }
}
