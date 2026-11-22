using System;
using System.Collections.Generic;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.Case;
using SpookyStreet.Localization;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using HintKeys = SpookyStreet.Core.Localization.LocalizationKeys.Hints;
using NoteKeys = SpookyStreet.Core.Localization.LocalizationKeys.Notebook;
using ShellKeys = SpookyStreet.Core.Localization.LocalizationKeys;
using UiKeys = SpookyStreet.Core.Localization.LocalizationKeys.CaseUi;

namespace SpookyStreet.UI
{
    /// <summary>
    /// Pip's notebook (spec §3 "Notebook", docs/evidence/s000.2/ui/ui_notebook.jpg): a plum felt cover holding a paper
    /// spread with the three questions. A proven question shows its answer and a green "Found it!" stamp; an unknown
    /// one a question mark in a pencil silhouette. Optional evidence sits beside its question as small pinned slips;
    /// tapping a found slip shows its note in the caption strip. Once the case is closed the final card lists the
    /// player's own discoveries with "Back to the street" and a separate, confirmed "Replay case". The hint radio
    /// lives here too. Paper bats are not counted yet (no collectibles), so no counter is drawn.
    /// The page is rebuilt from <see cref="CaseNotebook.Build"/> each time it opens.
    /// </summary>
    public sealed class NotebookView : MonoBehaviour
    {
        private static readonly Color Graphite = new Color32(0x4A, 0x44, 0x58, 0xFF);
        private static readonly Color OrangeInk = new Color32(0xC8, 0x5A, 0x12, 0xFF);
        private static readonly Color WitchGreen = new Color32(0x8D, 0xBF, 0x3F, 0xFF);

        private RectTransform _left;
        private RectTransform _right;
        private RectTransform _leftContent;
        private RectTransform _rightContent;
        private readonly List<Button> _slips = new List<Button>();

        /// <summary>Current case state (set by the director).</summary>
        public Func<CaseState> State { get; set; }

        /// <summary>Shows a localized note or hint in the caption strip.</summary>
        public Action<string> ShowCaption { get; set; }

        /// <summary>Asks the radio for the next hint (returns its key).</summary>
        public Func<string> RequestHint { get; set; }

        /// <summary>"Replay case" was pressed (the director asks for confirmation).</summary>
        public Action ReplayRequested { get; set; }

        public event Action Closed;

        public bool IsOpen => gameObject.activeSelf;

        public Button CloseButton { get; private set; }

        public Button HintButton { get; private set; }

        public Button BackToStreetButton { get; private set; }

        public Button ReplayButton { get; private set; }

        /// <summary>The final card (null until the case is closed and the notebook is open).</summary>
        public GameObject FinalCard { get; private set; }

        /// <summary>Question texts on the open spread, in order (tests).</summary>
        public List<Text> QuestionLabels { get; } = new List<Text>();

        /// <summary>Answer labels on the open spread (null entries for unknown questions).</summary>
        public List<Text> AnswerLabels { get; } = new List<Text>();

        /// <summary>Found annotation slips (tap shows their note).</summary>
        public IReadOnlyList<Button> FoundSlips => _slips;

        public static NotebookView Create(RectTransform canvasRoot)
        {
            var scrim = UiFactory.Panel(canvasRoot, "Notebook", UiFactory.WithAlpha(Palette.NightViolet, 0.84f));
            var view = scrim.gameObject.AddComponent<NotebookView>();
            view.Build(scrim.rectTransform);
            scrim.gameObject.SetActive(false);
            return view;
        }

        private void Build(RectTransform root)
        {
            var content = UiFactory.Stretch(UiFactory.Rect(root, "Content"));
            content.gameObject.AddComponent<SafeAreaFitter>();
            var centre = new Vector2(0.5f, 0.5f);

            // Plum felt cover with a candy-corn stitch, then two bone pages.
            UiFactory.PaperCard(content, "Cover", centre, new Vector2(0f, 34f), new Vector2(1196f, 580f), 0f,
                Palette.CandyCornYellow, new Color(0.52f, 0.29f, 0.57f, 1f));
            _left = UiFactory.PaperCard(content, "LeftPage", centre, new Vector2(-293f, 34f), new Vector2(570f, 532f), 0f,
                UiFactory.WithAlpha(Palette.HauntedPurple, 0f)).rectTransform;
            _right = UiFactory.PaperCard(content, "RightPage", centre, new Vector2(293f, 34f), new Vector2(570f, 532f), 0f,
                UiFactory.WithAlpha(Palette.HauntedPurple, 0f)).rectTransform;
            for (int i = 0; i < 12; i++)
            {
                // Spiral binding: candy-corn ties across the gutter.
                var tie = UiFactory.Sprite(content, "Binding", IconFactory.RoundedRect, Palette.CandyCornYellow, centre,
                    new Vector2(0f, 34f + 240f - i * 43.6f), new Vector2(34f, 7f));
                tie.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -8f);
            }

            CloseButton = DiscButton(content, "CloseButton", UiSpriteCatalog.IconClose, Vector2.one, new Vector2(-60f, -58f),
                Close);
            HintButton = UiFactory.PrimaryButton(content, "HintButton", HintKeys.HudHint, new Vector2(1f, 0f),
                new Vector2(-150f, 58f), new Vector2(256f, 96f), OnHint, UiSpriteCatalog.IconRadio);
            HintButton.transform.localRotation = Quaternion.Euler(0f, 0f, -1.5f);
        }

        private static Button DiscButton(Transform parent, string name, string icon, Vector2 anchor, Vector2 position,
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
            // Stays under the caption strip (a later sibling), so hints and notes read over the spread.
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

        private void OnHint()
        {
            string key = RequestHint?.Invoke();
            if (key != null)
            {
                ShowCaption?.Invoke(key);
            }
        }

        /// <summary>Redraws both pages from the current state.</summary>
        public void Rebuild()
        {
            var state = State != null ? State() : CaseState.Empty;
            var page = CaseNotebook.Build(state);
            if (_leftContent != null)
            {
                Destroy(_leftContent.gameObject);
                Destroy(_rightContent.gameObject);
            }

            _slips.Clear();
            QuestionLabels.Clear();
            AnswerLabels.Clear();
            FinalCard = null;
            BackToStreetButton = null;
            ReplayButton = null;
            _leftContent = UiFactory.Stretch(UiFactory.Rect(_left, "Page"));
            _rightContent = UiFactory.Stretch(UiFactory.Rect(_right, "Page"));

            var title = PageText(_leftContent, "Heading", NoteKeys.Title, 52, Palette.InkBlack, UiFactory.DisplayFont,
                38f, 16f, 480f, 72f);
            title.alignment = TextAnchor.MiddleLeft;
            PageText(_leftContent, "CaseName", ShellKeys.TitleCaseName, 28, Graphite, UiFactory.BoldFont, 40f, 76f, 480f, 44f);

            float y = 120f;
            y = Question(_leftContent, 1, page.Questions[0], y);
            Slips(_leftContent, page.Questions[0].Annotations, y);

            y = 26f;
            y = Question(_rightContent, 2, page.Questions[1], y);
            y = Slips(_rightContent, page.Questions[1].Annotations, y) + 14f;
            y = Question(_rightContent, 3, page.Questions[2], y);
            y = Slips(_rightContent, page.Questions[2].Annotations, y) + 10f;
            Slips(_rightContent, page.Extras, y);

            if (page.FinalCard != null)
            {
                BuildFinalCard(page.FinalCard);
            }
        }

        private float Question(RectTransform page, int number, NotebookQuestion question, float y)
        {
            PageText(page, "Number" + number, null, 44, OrangeInk, UiFactory.DisplayFont, 34f, y, 44f, 52f).text =
                number.ToString();
            QuestionLabels.Add(PageText(page, "Question" + number, question.QuestionKey, 32, Palette.InkBlack,
                UiFactory.BoldFont, 82f, y - 2f, 456f, 56f));
            y += 58f;
            if (question.Answered)
            {
                var answer = PageText(page, "Answer" + number, question.AnswerKey, 28, Palette.InkBlack, UiFactory.BoldFont,
                    82f, y, 336f, 128f);
                answer.alignment = TextAnchor.UpperLeft;
                AnswerLabels.Add(answer);
                var stamp = UiFactory.PaperTag(page, "FoundStamp", UiKeys.NotebookFound, 28, new Vector2(0f, 1f),
                    new Vector2(478f, -(y + 40f)), new Vector2(150f, 56f), -8f, Color.white);
                stamp.GetComponentInChildren<Text>().color = WitchGreen;
                UiFactory.StitchLayer(stamp.transform, WitchGreen);
                UiFactory.Sprite(stamp.transform, "Check", UiFactory.Baked(UiSpriteCatalog.IconCheck), WitchGreen,
                    new Vector2(0f, 0.5f), new Vector2(4f, 0f), new Vector2(30f, 30f));
            }
            else
            {
                AnswerLabels.Add(null);
                var silhouette = UiFactory.Sprite(page, "Unknown" + number, IconFactory.Disc,
                    UiFactory.WithAlpha(Graphite, 0.16f), new Vector2(0f, 1f), new Vector2(82f + 38f, -(y + 38f)),
                    new Vector2(76f, 76f));
                UiFactory.Sprite(silhouette.transform, "Ring", UiFactory.Baked(UiSpriteCatalog.StitchRing),
                    UiFactory.WithAlpha(Graphite, 0.8f), new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(76f, 76f));
                var mark = UiFactory.Label(silhouette.transform, "Mark", null, 52, TextAnchor.MiddleCenter,
                    UiFactory.WithAlpha(Graphite, 0.85f), UiFactory.DisplayFont);
                UiFactory.Stretch(mark.rectTransform);
                mark.gameObject.AddComponent<LocalizedText>().SetKey(UiKeys.NotebookUnknownMark);
                PageText(page, "NotFound" + number, NoteKeys.Unknown, 30, Graphite, UiFactory.BodyFont, 172f, y + 14f, 360f, 48f);
            }

            return y + (question.Answered ? 132f : 88f);
        }

        /// <summary>Two columns of small pinned slips; returns the y below them.</summary>
        private float Slips(RectTransform page, IReadOnlyList<NotebookAnnotation> annotations, float y)
        {
            int column = 0;
            foreach (var annotation in annotations)
            {
                float x = 30f + column * 258f;
                float angle = (annotation.Id.GetHashCode() & 3) - 1.5f;
                if (annotation.Found)
                {
                    var slip = UiFactory.PaperTag(page, "Slip", CaseNotebookTags.TagKey(annotation.Id), 26,
                        new Vector2(0f, 1f), new Vector2(x + 126f, -(y + 26f)), new Vector2(252f, 52f), angle, Color.white);
                    FitSlipText(slip);
                    slip.raycastTarget = true;
                    var button = slip.gameObject.AddComponent<Button>();
                    button.transition = Selectable.Transition.None;
                    string note = annotation.CaptionKey;
                    button.onClick.AddListener(() => ShowCaption?.Invoke(note));
                    _slips.Add(button);
                }
                else
                {
                    var slip = UiFactory.PaperTag(page, "UnknownSlip", null, 26, new Vector2(0f, 1f),
                        new Vector2(x + 126f, -(y + 26f)), new Vector2(252f, 52f), angle,
                        new Color(0.8f, 0.78f, 0.82f, 0.45f));
                    FitSlipText(slip);
                    var text = slip.GetComponentInChildren<Text>();
                    text.gameObject.AddComponent<LocalizedText>().SetKey(UiKeys.NotebookUnknownMark);
                    text.color = UiFactory.WithAlpha(Graphite, 0.7f);
                }

                column++;
                if (column == 2)
                {
                    column = 0;
                    y += 54f;
                }
            }

            return column == 0 ? y : y + 54f;
        }

        private void BuildFinalCard(NotebookFinalCard final)
        {
            var card = UiFactory.PaperCard(_rightContent, "FinalCard", new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(540f, 500f), 1.2f, Palette.HauntedPurple);
            FinalCard = card.gameObject;
            var page = card.rectTransform;
            PageText(page, "Heading", final.HeadingKey, 44, Palette.InkBlack, UiFactory.DisplayFont, 30f, 18f, 480f, 68f);
            var stamp = UiFactory.PaperTag(page, "ClosedStamp", final.StampKey, 32, new Vector2(1f, 1f),
                new Vector2(-120f, -110f), new Vector2(200f, 62f), -9f, Color.white);
            stamp.GetComponentInChildren<Text>().color = WitchGreen;
            UiFactory.StitchLayer(stamp.transform, WitchGreen);
            var count = PageText(page, "Count", null, 28, Graphite, UiFactory.BoldFont, 30f, 86f, 300f, 46f);
            count.gameObject.AddComponent<LocalizedText>().SetKey(final.CountKey,
                () => new object[] { final.Found, final.Total });

            var names = new List<string>();
            foreach (var entry in final.Discoveries)
            {
                string key = CaseNotebookTags.TagKey(entry.Id) ?? entry.TextKey;
                names.Add(Localizer.Get(key));
            }

            var list = PageText(page, "Discoveries", null, 28, Palette.InkBlack, UiFactory.BoldFont, 30f, 140f, 480f, 236f);
            list.text = string.Join(Localizer.Get(UiKeys.NotebookDiscoverySeparator), names);
            list.alignment = TextAnchor.UpperLeft;
            list.resizeTextMinSize = 26;

            var bottom = new Vector2(0.5f, 0f);
            BackToStreetButton = UiFactory.SecondaryButton(page, "BackToStreet", final.BackToStreetKey, bottom,
                new Vector2(-128f, 66f), new Vector2(250f, 96f), Close);
            ReplayButton = UiFactory.SecondaryButton(page, "ReplayCase", final.ReplayKey, bottom, new Vector2(132f, 66f),
                new Vector2(230f, 96f), () => ReplayRequested?.Invoke());
        }

        /// <summary>
        /// Uses the slip's full height for its label: Andika's line box is about 1.5 × the font size, and uGUI draws
        /// nothing when a line does not fit (CI run 45 frame 15_notebook).
        /// </summary>
        private static void FitSlipText(Image slip)
        {
            var rect = slip.GetComponentInChildren<Text>().rectTransform;
            rect.offsetMin = new Vector2(12f, 0f);
            rect.offsetMax = new Vector2(-12f, 0f);
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
