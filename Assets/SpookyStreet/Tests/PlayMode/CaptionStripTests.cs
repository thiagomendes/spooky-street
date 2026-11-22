using System.Collections;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpookyStreet.Case;
using SpookyStreet.Core.Case;
using SpookyStreet.Localization;
using SpookyStreet.UI;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;
using Lines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;
using UiKeys = SpookyStreet.Core.Localization.LocalizationKeys.CaseUi;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// The caption strip with the real font (S002 interior polish): every text that can reach the strip (sound captions,
    /// with each direction word where they take one, radio hints and notebook notes) wraps to at most two lines at
    /// 13 pt or more inside the strip at the iPhone landscape reference (1334 × 750 units, the narrowest strip) and on a
    /// notched iPhone's safe area; a shown caption is never cut off; and only directional sounds away from Pip get the
    /// direction cue, with the right word.
    /// </summary>
    public class CaptionStripTests
    {
        private readonly List<GameObject> _made = new List<GameObject>();

        [SetUp]
        public void SetUp()
        {
            Localizer.ResetForTests();
            CaseDirector.UseTemporarySaveDirectory();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (var go in _made)
            {
                Object.Destroy(go);
            }

            _made.Clear();
        }

        /// <summary>A strip in a caption area of the given size (canvas reference units).</summary>
        private CaptionStrip MakeStrip(float areaWidth, float areaHeight)
        {
            var canvasObject = new GameObject("CaptionTestCanvas", typeof(Canvas));
            _made.Add(canvasObject);
            canvasObject.GetComponent<Canvas>().renderMode = RenderMode.ScreenSpaceOverlay;
            var area = UiFactory.Rect(canvasObject.transform, "Area");
            area.anchorMin = area.anchorMax = new Vector2(0.5f, 0.5f);
            area.sizeDelta = new Vector2(areaWidth, areaHeight);
            return CaptionStrip.Create(area);
        }

        /// <summary>Every key the strip can show, with its text and whether it can carry the direction cue.</summary>
        private static IEnumerable<(string Key, string Text, bool Cue)> StripTexts()
        {
            var words = new[] { UiKeys.DirectionAhead, UiKeys.DirectionLeft, UiKeys.DirectionRight, UiKeys.DirectionBehind };
            foreach (var entry in Localizer.Catalog.Active.Entries)
            {
                string key = entry.Key;
                if (!key.StartsWith("c01.caption.") && !key.StartsWith("c01.hint.") && !key.StartsWith("c01.notebook.note_"))
                {
                    continue;
                }

                yield return (key, entry.Value, false);
                if (CaptionDirection.IsDirectionalSound(key))
                {
                    foreach (string word in words)
                    {
                        yield return (key, Localizer.Format(UiKeys.CaptionWithDirection, entry.Value, Localizer.Get(word)), true);
                    }
                }
            }
        }

        [UnityTest]
        public IEnumerator EveryStripTextFitsTwoLinesOnAPhone()
        {
            // 1334 × 750: the canvas reference (iPhone 8/SE landscape, no notch). 1443: an iPhone 12-16 landscape safe
            // area (2532 × 1170 px → 1623 units wide, less 47 pt each side).
            foreach (float areaWidth in new[] { 1334f, 1443f })
            {
                var strip = MakeStrip(areaWidth, 710f);
                yield return null;
                var texts = StripTexts().ToList();
                Assert.Greater(texts.Count, 40, "captions, hints and notes are all checked");
                var failures = new List<string>();
                foreach (var t in texts)
                {
                    var fit = strip.Measure(t.Text, t.Cue);
                    if (!fit.Fits || fit.FontSize < CaptionStrip.MinFontSize || fit.Lines > CaptionStrip.MaxLines)
                    {
                        failures.Add($"{t.Key} '{t.Text}' (cue {t.Cue}): {fit}");
                    }

                    // The grown strip stays a strip: it never climbs into the middle of the screen.
                    if (fit.StripHeight > 160f)
                    {
                        failures.Add($"{t.Key}: strip {fit.StripHeight:F0} units tall");
                    }
                }

                Assert.IsEmpty(failures, $"area {areaWidth}:\n" + string.Join("\n", failures));
                Assert.AreEqual(CaptionStrip.WidthFor(areaWidth), strip.Measure("x", false).TextWidth + CaptionStrip.PlainInset +
                    CaptionStrip.RightInset, 0.5f);
            }
        }

        [UnityTest]
        public IEnumerator AShownCaptionWrapsInsteadOfBeingCutOff()
        {
            var strip = MakeStrip(1334f, 750f);
            yield return null;
            strip.ShowNow(Lines.CaptionGrille);
            yield return null;
            yield return null;
            Canvas.ForceUpdateCanvases();
            var text = strip.Strip.GetComponentsInChildren<Text>().First(t => t.name == "Text");
            Assert.AreEqual(Localizer.Get(Lines.CaptionGrille), text.text);
            Assert.LessOrEqual(strip.CurrentFit.Lines, CaptionStrip.MaxLines, strip.CurrentFit.ToString());
            Assert.LessOrEqual(text.preferredHeight, text.rectTransform.rect.height + 1f,
                $"the text ({text.preferredHeight:F0}) fits its rect ({text.rectTransform.rect.height:F0})");
            var settings = text.GetGenerationSettings(text.rectTransform.rect.size);
            var generator = new TextGenerator();
            generator.Populate(text.text, settings);
            Assert.LessOrEqual(generator.lineCount, CaptionStrip.MaxLines, "two lines at most in the strip");
            Assert.Greater(strip.Strip.rect.height, CaptionStrip.RestHeight - 1f);
        }

        [UnityTest]
        public IEnumerator OnlyDirectionalSoundsAwayFromPipGetTheCue()
        {
            var strip = MakeStrip(1334f, 750f);
            var cameraObject = new GameObject("CueCamera", typeof(Camera));
            _made.Add(cameraObject);
            cameraObject.transform.SetPositionAndRotation(Vector3.zero, Quaternion.identity); // looking north (+Z)
            strip.ViewCamera = cameraObject.GetComponent<Camera>();
            strip.Listener = () => Vector3.zero;
            yield return null;

            void Show(string key, Vector3 source)
            {
                strip.Clear();
                strip.ShowKey(key, () => source);
            }

            // What Pip sees or does: never a cue, even with a source.
            Show(Lines.CaptionArmLinked, new Vector3(0f, 0f, 10f));
            Assert.IsFalse(strip.HasDirectionCue, "the arm clicking in is not a directional sound");
            Assert.AreEqual(Localizer.Get(Lines.CaptionArmLinked), strip.CurrentText, "no direction word");
            Show(Lines.CaptionGrille, new Vector3(0f, 0f, 10f));
            Assert.IsFalse(strip.HasDirectionCue);
            Show(Lines.CaptionOwlBows, new Vector3(0f, 0f, 10f));
            Assert.IsFalse(strip.HasDirectionCue);

            // A sound whose source is right by Pip: no direction.
            Show(Lines.CaptionFoyerHum, new Vector3(0.8f, 0f, 0.6f));
            Assert.IsFalse(strip.HasDirectionCue, "the hum right under Pip has no direction");

            // Directional sounds away from Pip: the word follows the camera.
            var expected = new (Vector3 Source, string Word)[]
            {
                (new Vector3(0f, 3f, 10f), UiKeys.DirectionAhead),
                (new Vector3(10f, 0f, 1f), UiKeys.DirectionRight),
                (new Vector3(-10f, 0f, 1f), UiKeys.DirectionLeft),
                (new Vector3(0.5f, 0f, -10f), UiKeys.DirectionBehind),
            };
            foreach (var e in expected)
            {
                Show(Lines.CaptionArrivalKnocks, e.Source);
                Assert.IsTrue(strip.HasDirectionCue, $"knocks from {e.Source}");
                Assert.AreEqual(Localizer.Format(UiKeys.CaptionWithDirection, Localizer.Get(Lines.CaptionArrivalKnocks),
                    Localizer.Get(e.Word)), strip.CurrentText);
            }

            Show(Lines.CaptionFoyerHum, new Vector3(-4f, 0f, 0.5f));
            Assert.IsTrue(strip.HasDirectionCue);
            StringAssert.EndsWith("(" + Localizer.Get(UiKeys.DirectionLeft) + ")", strip.CurrentText);
        }
    }
}
