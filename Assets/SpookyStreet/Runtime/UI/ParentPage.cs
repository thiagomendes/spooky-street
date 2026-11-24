using System;
using System.Collections.Generic;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Parent;
using SpookyStreet.Localization;
using SpookyStreet.UI.Nonverbal;
using UnityEngine;
using UnityEngine.UI;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.Parent;

namespace SpookyStreet.UI
{
    /// <summary>
    /// The "For grown-ups" page (S004 §2.5, engagement §2.9), behind the parent gate: the case and the questions
    /// solved, the child's own answer comics (read-only, drawn like the notebook's), places explored, friends met,
    /// keepsakes with the only totals in the game, time played this week as a total and seven day dots, what the game
    /// practises and the privacy lines. Rebuilt from the saves every time it opens (<see cref="ParentProgress"/>);
    /// nothing leaves the device and there is no share or export button.
    /// </summary>
    public sealed class ParentPage : MonoBehaviour
    {
        private Func<ParentProgressPage> _source;

        public ReadingSheet Sheet { get; private set; }

        /// <summary>The model the page shows now (tests).</summary>
        public ParentProgressPage Shown { get; private set; }

        /// <summary>The comic panels drawn (tests): one list per solved question.</summary>
        public List<List<string>> ComicsDrawn { get; } = new List<List<string>>();

        public bool IsOpen => Sheet != null && Sheet.IsOpen;

        public event Action Closed;

        public static ParentPage Create(Func<ParentProgressPage> source)
        {
            var sheet = ReadingSheet.Create("ParentPage", Keys.Heading);
            var page = sheet.gameObject.AddComponent<ParentPage>();
            page.Sheet = sheet;
            page._source = source;
            sheet.Closed += () => page.Closed?.Invoke();
            return page;
        }

        public void Open()
        {
            Rebuild();
            Sheet.Open();
        }

        public void Close() => Sheet.Close();

        private void Rebuild()
        {
            Sheet.Clear();
            ComicsDrawn.Clear();
            var model = _source != null ? _source() : null;
            Shown = model;
            if (model == null)
            {
                return;
            }

            var sheet = Sheet;
            sheet.Paragraph(Keys.Intro, 26);

            // Cases: the case, its status, and a chip per question solved so far.
            sheet.Heading(Keys.Cases);
            var caseRow = sheet.Row(64f, 24f);
            var name = UiFactory.Label(ReadingSheet.Cell(caseRow, "CaseName", new Vector2(640f, 60f)), "Text",
                model.CaseNameKey, 32, TextAnchor.MiddleLeft, UiFactory.Ink, UiFactory.BoldFont);
            UiFactory.Stretch(name.rectTransform);
            var statusCell = ReadingSheet.Cell(caseRow, "Status", new Vector2(300f, 60f));
            var tag = UiFactory.PaperTag(statusCell, "StatusTag", model.CaseStatusKey, 28, new Vector2(0.5f, 0.5f),
                Vector2.zero, new Vector2(300f, 60f), -1.5f, Color.white);
            tag.GetComponentInChildren<Text>().color = model.CaseStatus == ParentCaseStatus.Solved ? WitchGreen : ReadingSheet.Graphite;
            if (model.Solved.Count > 0)
            {
                var chips = sheet.Row(112f);
                foreach (var solved in model.Solved)
                {
                    QuestionChip(chips, solved);
                }
            }

            // The child's own answers: each solved question's comic, in the child's order.
            sheet.Heading(Keys.Answers);
            if (model.Solved.Count == 0)
            {
                sheet.Paragraph(Keys.AnswersNone);
            }

            foreach (var solved in model.Solved)
            {
                sheet.Paragraph(solved.QuestionKey, 30, UiFactory.Ink, UiFactory.BoldFont, 4f);
                Comic(solved.ComicStickers);
                sheet.Paragraph(solved.AnswerKey, 28);
            }

            // Places and friends.
            sheet.Heading(Keys.Places);
            if (model.Places.Count == 0)
            {
                sheet.Paragraph(Keys.NoneYet);
            }
            else
            {
                var row = sheet.Row(176f, 28f);
                foreach (var place in model.Places)
                {
                    PictureChip(row, place.Id, PuppetAtlas.Get(place.Picture), place.LabelKey, false);
                }
            }

            sheet.Heading(Keys.Friends);
            if (model.Friends.Count == 0)
            {
                sheet.Paragraph(Keys.NoneYet);
            }
            else
            {
                var row = sheet.Row(176f, 28f);
                foreach (var friend in model.Friends)
                {
                    PictureChip(row, friend.Id, PortraitAtlas.Get(friend.Picture), friend.LabelKey, true);
                }
            }

            // Keepsakes: the only place with totals.
            sheet.Heading(Keys.Keepsakes);
            sheet.Paragraph(Keys.Bats, 30, null, null, 0f, () => new object[] { model.BatsFound, model.BatsTotal });
            sheet.Paragraph(Keys.Pages, 30, null, null, 0f, () => new object[] { model.PagesEarned, model.PagesTotal });
            sheet.Paragraph(Keys.Pieces, 30, null, null, 0f, () => new object[] { model.PiecesEarned, model.PiecesTotal });

            // Time played this week: a rounded total and seven day dots; no goal, no comparison.
            sheet.Heading(Keys.Time);
            var week = model.Week;
            sheet.Paragraph(week.TotalKey, 32, UiFactory.Ink, UiFactory.BoldFont, 0f, () => week.TotalArgs);
            var dots = sheet.Row(108f, 14f);
            foreach (var day in week.Days)
            {
                DayDot(dots, day);
            }

            sheet.Paragraph(Keys.TimeNote, 26);

            sheet.Heading(Keys.Practises);
            foreach (string key in model.PractiseKeys)
            {
                sheet.Paragraph(key);
            }

            sheet.Heading(Keys.Privacy);
            foreach (string key in model.PrivacyKeys)
            {
                sheet.Paragraph(key);
            }
        }

        private static readonly Color WitchGreen = new Color32(0x4E, 0x8A, 0x2E, 0xFF);

        private static void QuestionChip(RectTransform row, ParentSolvedQuestion solved)
        {
            var cell = ReadingSheet.Cell(row, "Chip_" + solved.QuestionId, new Vector2(200f, 104f));
            var card = UiFactory.Sprite(cell, "Card", UiFactory.Baked(UiSpriteCatalog.PaperCard), Color.white,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(200f, 104f));
            UiFactory.InkShadow(card, new Vector2(3f, -4f), 0.45f);
            var pictures = solved.Pictures ?? Array.Empty<string>();
            for (int i = 0; i < pictures.Count; i++)
            {
                var art = UiFactory.Sprite(card.transform, "Art" + i, PuppetAtlas.Get(pictures[i]), Color.white,
                    new Vector2(0.5f, 0.5f), new Vector2((i - (pictures.Count - 1) * 0.5f) * 72f - 16f, 0f), new Vector2(70f, 70f));
                art.preserveAspect = true;
            }

            var check = UiFactory.Sprite(card.transform, "Check", UiFactory.Baked(UiSpriteCatalog.IconCheck), WitchGreen,
                new Vector2(1f, 0.5f), new Vector2(-28f, 0f), new Vector2(40f, 40f));
            check.preserveAspect = true;
            AccessibleName.Set(cell.gameObject, solved.QuestionKey);
        }

        private void Comic(IReadOnlyList<string> stickers)
        {
            const float panelW = 196f, panelH = 128f, gap = 40f;
            var row = Sheet.Row(panelH + 8f, gap);
            var drawn = new List<string>();
            for (int p = 0; p < stickers.Count; p++)
            {
                var cell = ReadingSheet.Cell(row, "Panel" + p, new Vector2(panelW, panelH));
                var panel = UiFactory.Sprite(cell, "Card", UiFactory.Baked(UiSpriteCatalog.PaperCard), Color.white,
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(panelW, panelH));
                UiFactory.InkShadow(panel, new Vector2(3f, -4f), 0.45f);
                UiFactory.StitchLayer(panel.transform, UiFactory.WithAlpha(Palette.HauntedPurple, 0.7f));
                string[] art = NotebookView.StickerArt(stickers[p]);
                float side = Mathf.Min(96f, (panelW - 24f) / Mathf.Max(1, art.Length));
                for (int a = 0; a < art.Length; a++)
                {
                    var img = UiFactory.Sprite(panel.transform, "Art" + a, PuppetAtlas.Get(art[a]), Color.white,
                        new Vector2(0.5f, 0.5f), new Vector2((a - (art.Length - 1) * 0.5f) * side, 0f), new Vector2(side, side));
                    img.preserveAspect = true;
                }

                AccessibleName.Set(cell.gameObject, CaseNotebook.Sticker(stickers[p])?.TagKey);
                if (p < stickers.Count - 1)
                {
                    var arrow = UiFactory.Sprite(cell, "Arrow", UiFactory.Baked(UiSpriteCatalog.IconArrow), Palette.HauntedPurple,
                        new Vector2(1f, 0.5f), new Vector2(gap * 0.5f, 0f), new Vector2(26f, 26f));
                    arrow.preserveAspect = true;
                    arrow.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -90f); // the glyph points up
                }

                drawn.Add(stickers[p]);
            }

            ComicsDrawn.Add(drawn);
        }

        private static void PictureChip(RectTransform row, string id, Sprite picture, string labelKey, bool round)
        {
            var cell = ReadingSheet.Cell(row, id, new Vector2(250f, 172f));
            var frame = round
                ? UiFactory.Sprite(cell, "Frame", UiFactory.Circle, UiFactory.WithAlpha(Palette.NightViolet, 0.85f),
                    new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(108f, 108f))
                : UiFactory.Sprite(cell, "Frame", UiFactory.Baked(UiSpriteCatalog.PaperCard), Color.white,
                    new Vector2(0.5f, 1f), new Vector2(0f, -56f), new Vector2(150f, 108f));
            UiFactory.InkShadow(frame, new Vector2(3f, -4f), 0.4f);
            if (picture != null)
            {
                var img = UiFactory.Sprite(frame.transform, "Picture", picture, Color.white, new Vector2(0.5f, 0.5f),
                    round ? new Vector2(0f, 8f) : Vector2.zero, round ? new Vector2(128f, 128f) : new Vector2(96f, 96f));
                img.preserveAspect = true;
            }

            var label = UiFactory.Label(cell, "Label", labelKey, 26, TextAnchor.UpperCenter, UiFactory.Ink, UiFactory.BoldFont);
            UiFactory.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 30f), new Vector2(250f, 60f));
        }

        private static void DayDot(RectTransform row, PlaytimeDay day)
        {
            var cell = ReadingSheet.Cell(row, "Day" + (int)day.Weekday, new Vector2(84f, 104f));
            float size = day.Level == 0 ? 22f : 22f + day.Level * 10f;
            var dot = UiFactory.Sprite(cell, "Dot", UiFactory.Circle,
                day.Level == 0 ? UiFactory.WithAlpha(ReadingSheet.Graphite, 0.25f) : Palette.HauntedPurple,
                new Vector2(0.5f, 1f), new Vector2(0f, -28f), new Vector2(size, size));
            dot.rectTransform.anchoredPosition = new Vector2(0f, -28f);
            var label = UiFactory.Label(cell, "Weekday", day.WeekdayKey, 26, TextAnchor.MiddleCenter,
                day.IsToday ? UiFactory.Ink : ReadingSheet.Graphite, day.IsToday ? UiFactory.BoldFont : UiFactory.BodyFont);
            UiFactory.Place(label.rectTransform, new Vector2(0.5f, 0f), new Vector2(0f, 24f), new Vector2(84f, 48f));
        }
    }
}
