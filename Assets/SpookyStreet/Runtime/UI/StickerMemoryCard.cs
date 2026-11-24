using System;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Cutscenes;
using SpookyStreet.Localization;
using UnityEngine;
using UnityEngine.UI;
using MemoryKeys = SpookyStreet.Core.Localization.LocalizationKeys.StickerMemory;

namespace SpookyStreet.UI
{
    /// <summary>
    /// Draws a sticker memory's picture (D32): the real place, through a small camera of its own. The notebook only
    /// asks for poses; the world side (<c>Case.StickerMemoryStage</c>) knows the house, the sky and the cast.
    /// </summary>
    public interface IStickerMemoryStage
    {
        /// <summary>The picture; null without a graphics device (the card then shows the sticker's drawing).</summary>
        Texture Texture { get; }

        /// <summary>Gets the world ready for <paramref name="memory"/> (the interior drawn while it shows, if it is inside).</summary>
        void Begin(StickerMemory memory);

        /// <summary>Draws the picture from <paramref name="pose"/> now; false when the world was not ready this frame.</summary>
        bool Render(CamPose pose);

        /// <summary>Gives the world back as it was.</summary>
        void End();
    }

    /// <summary>
    /// The sticker memory card (owner decision D32, docs/S004-sticker-memory.md): a small paper card over the top of the
    /// notebook page with a 2–3 second picture of the place where the child found the sticker (a slow drift in, like the
    /// reveal's sticker shots), the sticker itself in the corner and one descriptive caption. It holds the settled frame,
    /// then fades by itself; pressing it closes it at once. Under reduced motion it is a still frame that cuts away. It
    /// never places, picks or counts anything: the notebook decides when it opens. The tray stays uncovered below it.
    /// </summary>
    public sealed class StickerMemoryCard : MonoBehaviour
    {
        public static readonly Vector2 Size = new Vector2(660f, 312f);
        public static readonly Vector2 PictureSize = new Vector2(624f, 208f);
        public const float PopSeconds = 0.16f;

        /// <summary>The picture's pencil-memory tint (MemoryReplayView's lilac).</summary>
        public static readonly Color Lilac = new Color(0.62f, 0.52f, 0.86f, 1f);

        private CanvasGroup _group;
        private RectTransform _rect;
        private RawImage _picture;
        private RectTransform _pictureFrame;
        private RectTransform _drawing;
        private RectTransform _chip;
        private Text _caption;
        private LocalizedText _captionKey;
        private bool _stageBegun;
        private int _rendered;

        public IStickerMemoryStage Stage { get; set; }

        /// <summary>Gate for the card's clock (the game's pause); null always runs.</summary>
        public Func<bool> CanAnimate { get; set; }

        public StickerMemoryPlayback Playback { get; private set; }

        public bool IsOpen => Playback != null && gameObject.activeSelf;

        public string StickerId => IsOpen ? Playback.Memory.StickerId : null;

        public Text Caption => _caption;

        public string CaptionKey => IsOpen ? Playback.Memory.CaptionKey : null;

        public RawImage Picture => _picture;

        /// <summary>The real place is drawn (a graphics device and a stage); else the sticker's drawing stands in.</summary>
        public bool ShowsPlace => IsOpen && _picture.enabled && _picture.texture != null;

        /// <summary>Pictures drawn since this memory opened (tests: a still frame stops drawing under reduced motion).</summary>
        public int RenderedFrames => _rendered;

        /// <summary>Memories opened since the scene loaded (tests).</summary>
        public int OpenCount { get; private set; }

        public Button CloseButton { get; private set; }

        public event Action Closed;

        public static StickerMemoryCard Create(RectTransform parent, Vector2 position)
        {
            var card = UiFactory.PaperCard(parent, "StickerMemory", new Vector2(0.5f, 0.5f), position, Size, -0.6f, Lilac);
            card.raycastTarget = true;
            var view = card.gameObject.AddComponent<StickerMemoryCard>();
            view.Build(card);
            card.gameObject.SetActive(false);
            return view;
        }

        private void Build(Image card)
        {
            _rect = card.rectTransform;
            _group = card.gameObject.AddComponent<CanvasGroup>();

            // The picture in a thin ink frame, then the caption under it.
            var frame = UiFactory.Sprite(card.transform, "PictureFrame", IconFactory.RoundedRect, Palette.InkBlack,
                new Vector2(0.5f, 1f), new Vector2(0f, -18f - PictureSize.y * 0.5f), PictureSize + new Vector2(8f, 8f));
            _pictureFrame = frame.rectTransform;
            var picture = UiFactory.Rect(frame.transform, "Picture");
            UiFactory.Place(picture, new Vector2(0.5f, 0.5f), Vector2.zero, PictureSize);
            _picture = picture.gameObject.AddComponent<RawImage>();
            _picture.raycastTarget = false;
            _picture.color = Color.white;

            // Without a picture (no graphics device, or no world): the sticker's own drawing on night felt.
            _drawing = UiFactory.Rect(frame.transform, "Drawing");
            UiFactory.Place(_drawing, new Vector2(0.5f, 0.5f), Vector2.zero, PictureSize);

            // The memory look: a faint lilac pencil tint over the place (as the in-world memory replays).
            var tint = UiFactory.Sprite(frame.transform, "MemoryTint", null, UiFactory.WithAlpha(Lilac, 0.1f),
                new Vector2(0.5f, 0.5f), Vector2.zero, PictureSize);
            tint.raycastTarget = false;

            _caption = UiFactory.Label(card.transform, "Caption", null, 30, TextAnchor.MiddleCenter, UiFactory.Ink,
                UiFactory.BoldFont);
            UiFactory.Place(_caption.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 42f), new Vector2(Size.x - 44f, 70f));
            _captionKey = _caption.gameObject.AddComponent<LocalizedText>();

            CloseButton = card.gameObject.AddComponent<Button>();
            CloseButton.targetGraphic = card;
            CloseButton.transition = Selectable.Transition.None;
            CloseButton.navigation = new Navigation { mode = Navigation.Mode.None };
            CloseButton.onClick.AddListener(Close);
            AccessibleName.Set(card.gameObject, MemoryKeys.Card);
        }

        /// <summary>Opens <paramref name="memory"/> (replacing any open one).</summary>
        public void Open(StickerMemory memory, bool reducedMotion)
        {
            if (memory == null)
            {
                return;
            }

            if (IsOpen)
            {
                EndStage();
            }

            Playback = new StickerMemoryPlayback(memory, reducedMotion);
            OpenCount++;
            _rendered = 0;
            gameObject.SetActive(true);
            transform.SetAsLastSibling();
            _group.alpha = 1f;
            _rect.localScale = reducedMotion ? Vector3.one : new Vector3(0.92f, 0.92f, 1f);
            _captionKey.SetKey(memory.CaptionKey);

            if (_chip != null)
            {
                Destroy(_chip.gameObject);
            }

            // The sticker itself sits in the picture's corner, so the child sees which sticker this memory belongs to.
            var chip = NotebookView.StickerCard(_pictureFrame, memory.StickerId, new Vector2(0f, 0f), new Vector2(62f, 46f),
                new Vector2(100f, 74f), -4f);
            chip.raycastTarget = false;
            _chip = chip.rectTransform;

            foreach (Transform child in _drawing)
            {
                Destroy(child.gameObject);
            }

            var texture = Stage != null ? Stage.Texture : null;
            _picture.texture = texture;
            _picture.enabled = texture != null;
            _drawing.gameObject.SetActive(texture == null);
            if (texture == null)
            {
                var felt = UiFactory.Sprite(_drawing, "Felt", IconFactory.RoundedRect, Palette.NightViolet,
                    new Vector2(0.5f, 0.5f), Vector2.zero, PictureSize);
                felt.raycastTarget = false;
                var art = NotebookView.StickerCard(_drawing, memory.StickerId, new Vector2(0.5f, 0.5f), Vector2.zero,
                    new Vector2(250f, 180f), 2f);
                art.raycastTarget = false;
            }

            if (Stage != null)
            {
                Stage.Begin(memory);
                _stageBegun = true;
                Draw();
            }
        }

        /// <summary>Closes now (a press on the card, a placement, a page turn, Back); harmless when closed.</summary>
        public void Close()
        {
            if (Playback == null && !gameObject.activeSelf)
            {
                return;
            }

            EndStage();
            Playback = null;
            gameObject.SetActive(false);
            Closed?.Invoke();
        }

        private void EndStage()
        {
            if (_stageBegun && Stage != null)
            {
                Stage.End();
            }

            _stageBegun = false;
        }

        private void Draw()
        {
            if (Stage == null || Playback == null || Stage.Texture == null)
            {
                return;
            }

            // Reduced motion: a still frame. Draw until two frames have landed (the interior may need a frame to show),
            // then keep that picture.
            if (Playback.ReducedMotion && _rendered >= 2)
            {
                return;
            }

            if (Stage.Render(Playback.Pose))
            {
                _rendered++;
            }
        }

        private void LateUpdate()
        {
            if (Playback == null)
            {
                gameObject.SetActive(false); // closed while the notebook was hidden
                return;
            }

            float dt = CanAnimate == null || CanAnimate() ? Time.unscaledDeltaTime : 0f;
            Playback.Advance(dt);
            _group.alpha = Playback.Alpha;
            if (!Playback.ReducedMotion)
            {
                float pop = Mathf.Clamp01(Playback.Elapsed / PopSeconds);
                float s = Mathf.Lerp(0.92f, 1f, 1f - (1f - pop) * (1f - pop));
                _rect.localScale = new Vector3(s, s, 1f);
            }

            Draw();
            if (Playback.IsDone)
            {
                Close();
            }
        }

        private void OnDisable()
        {
            if (Playback != null)
            {
                EndStage();
                Playback = null;
            }
        }
    }
}
