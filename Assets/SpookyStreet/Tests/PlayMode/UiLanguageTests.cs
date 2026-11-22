using System.Collections;
using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.App;
using SpookyStreet.Case;
using SpookyStreet.Controls;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Localization;
using SpookyStreet.Localization;
using SpookyStreet.UI;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.SceneManagement;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace SpookyStreet.Tests.PlayMode
{
    /// <summary>
    /// The craft UI language in the game (docs/ui-language.md): baked sprites load (no procedural fallbacks, no cube
    /// maps), every control is a real touch target inside the safe area, each screen has one orange action, modal
    /// screens dim the world with night violet, and HUD chrome never out-shines the scene.
    /// </summary>
    public class UiLanguageTests
    {
        /// <summary>44 pt platform floor at 2 reference units per pt.</summary>
        private const float MinHudControl = 44f * UiFactory.UnitsPerPoint;

        [SetUp]
        public void SetUp()
        {
            Localizer.ResetForTests();
            CaseDirector.UseTemporarySaveDirectory(); // no save leaks between tests (S002)
        }

        private static IEnumerator LoadGame()
        {
            yield return SceneManager.LoadSceneAsync("LanternLane", LoadSceneMode.Single);
            yield return null;
            Assert.IsNotNull(GameBootstrap.Instance, "scene did not create GameBootstrap");
        }

        private static Rect CanvasRect(RectTransform rect, RectTransform space)
        {
            var corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 min = new Vector2(float.MaxValue, float.MaxValue);
            Vector2 max = new Vector2(float.MinValue, float.MinValue);
            foreach (var corner in corners)
            {
                Vector2 local = space.InverseTransformPoint(corner);
                min = Vector2.Min(min, local);
                max = Vector2.Max(max, local);
            }

            return Rect.MinMaxRect(min.x, min.y, max.x, max.y);
        }

        private static List<RectTransform> Controls(GameObject root)
        {
            var controls = new List<RectTransform>();
            foreach (var selectable in root.GetComponentsInChildren<Selectable>(true))
            {
                controls.Add((RectTransform)selectable.transform);
            }

            foreach (var button in root.GetComponentsInChildren<TouchButton>(true))
            {
                controls.Add((RectTransform)button.transform);
            }

            return controls;
        }

        [UnityTest]
        public IEnumerator BakedSpritesLoadAsTwoDimensionalTextures()
        {
            yield return LoadGame();
            var game = GameBootstrap.Instance;
            foreach (var spec in UiSpriteCatalog.All)
            {
                var sprite = UiSprites.TryLoad(spec.Name);
                Assert.IsNotNull(sprite, spec.Name + " did not load from Resources/UI");
                Assert.AreEqual(TextureDimension.Tex2D, sprite.texture.dimension, spec.Name + " must be a 2D texture");
                Assert.AreEqual(new Vector4(spec.Left, spec.Bottom, spec.Right, spec.Top), sprite.border, spec.Name);
            }

            Assert.IsEmpty(UiSprites.Missing, "procedural fallbacks drawn for: " + string.Join(", ", UiSprites.Missing));
            foreach (var image in game.Ui.GetComponentsInChildren<Image>(true))
            {
                if (image.sprite != null)
                {
                    Assert.IsNotNull(image.sprite.texture, image.name + " has a sprite with no texture");
                    Assert.AreEqual(TextureDimension.Tex2D, image.sprite.texture.dimension, image.name);
                }
            }
        }

        [UnityTest]
        public IEnumerator HudControlsAreLargeEnoughAndInsideTheSafeArea()
        {
            yield return LoadGame();
            var game = GameBootstrap.Instance;
            game.Ui.NewGameButton.onClick.Invoke();
            yield return null;
            Canvas.ForceUpdateCanvases();

            var safe = game.Ui.SafeArea;
            Rect safeRect = safe.rect;
            var controls = Controls(game.Ui.Hud);
            Assert.GreaterOrEqual(controls.Count, 3, "expected Jump, Use and Pause");
            Assert.Contains((RectTransform)game.Ui.UseButton.transform, controls, "Use is a touch button in play");
            foreach (var control in controls)
            {
                Rect r = CanvasRect(control, safe);
                Assert.GreaterOrEqual(r.width, MinHudControl - 0.5f, control.name + " narrower than 44 pt");
                Assert.GreaterOrEqual(r.height, MinHudControl - 0.5f, control.name + " shorter than 44 pt");
                Assert.IsTrue(r.xMin >= safeRect.xMin - 0.5f && r.xMax <= safeRect.xMax + 0.5f &&
                              r.yMin >= safeRect.yMin - 0.5f && r.yMax <= safeRect.yMax + 0.5f,
                    $"{control.name} {r} leaves the safe area {safeRect}");
            }

            // With nothing to interact with, Use is the moveset button (roll on the ground, dive in the air): shown,
            // with the "Roll" verb. Pause matches the documented size.
            Assert.IsTrue(game.Ui.UseButton.activeInHierarchy, "Use is shown in play");
            Assert.AreEqual(LocalizationKeys.HudRoll, game.Ui.UseVerbKey, "Use reads Roll with no interaction in range");
            Assert.AreEqual(UiFactory.UseDiameter, ((RectTransform)game.Ui.UseButton.transform).rect.width, 0.01f);
            Assert.AreEqual(UiFactory.SystemDiscDiameter, ((RectTransform)game.Ui.PauseButton.transform).rect.width, 0.01f);
            var verb = game.Ui.UseButton.transform.Find("VerbTag");
            Assert.IsNotNull(verb, "Use shows its verb on a paper tag");
            Rect tag = CanvasRect((RectTransform)verb, safe);
            Assert.GreaterOrEqual(tag.yMin, safeRect.yMin - 0.5f, "Use verb tag leaves the safe area");
        }

        [UnityTest]
        public IEnumerator TitleAndPauseButtonsAreTouchTargetsWithOnePrimary()
        {
            yield return LoadGame();
            var game = GameBootstrap.Instance;
            AssertOnePrimary(game.Ui.TitleScreen, "title");
            Assert.IsFalse(game.Ui.ContinueButton.gameObject.activeSelf, "no saves yet: Continue stays hidden");
            Assert.AreEqual(CraftButtonStyle.Primary, game.Ui.NewGameButton.GetComponent<CraftButton>().Style,
                "with no Continue, New game is the title's single orange action");

            game.Ui.NewGameButton.onClick.Invoke();
            yield return null;
            game.Flow.Pause();
            yield return null;
            Assert.AreEqual(GameState.Paused, game.Flow.State);
            AssertOnePrimary(game.Ui.PauseScreen, "pause");
            Assert.AreEqual(CraftButtonStyle.Primary, game.Ui.ResumeButton.GetComponent<CraftButton>().Style);

            // Modal screens dim the world first with a night-violet scrim (75–85 %).
            var scrim = game.Ui.PauseScreen.GetComponent<Image>();
            Assert.That(scrim.color.a, Is.InRange(0.75f, 0.85f));
            Assert.AreEqual(Palette.NightViolet.r, scrim.color.r, 0.01f);
            Assert.AreEqual(Palette.NightViolet.b, scrim.color.b, 0.01f);

            foreach (var control in Controls(game.Ui.PauseScreen))
            {
                Assert.GreaterOrEqual(control.rect.height, UiFactory.MinTouchTarget - 0.5f, control.name);
                Assert.GreaterOrEqual(control.rect.width, UiFactory.MinTouchTarget - 0.5f, control.name);
            }
        }

        [UnityTest]
        public IEnumerator PressingDarkensTheFeltAndRestores()
        {
            yield return LoadGame();
            var game = GameBootstrap.Instance;
            var press = game.Ui.NewGameButton.GetComponent<PressScale>();
            var face = game.Ui.NewGameButton.GetComponent<Image>();
            Color rest = face.color;
            press.OnPointerDown(null);
            Assert.Less(face.color.r, rest.r, "pressed felt is darker");
            yield return null;
            yield return null;
            Assert.Less(press.transform.localScale.x, 1f, "pressed control squashes");
            press.OnPointerUp(null);
            Assert.AreEqual(rest, face.color, "released felt returns to its colour");
        }

        [UnityTest]
        public IEnumerator HudChromeStaysDimmerThanBone()
        {
            yield return LoadGame();
            var game = GameBootstrap.Instance;
            game.Ui.NewGameButton.onClick.Invoke();
            yield return null;

            // Brightness rule: in play no chrome tint is brighter than bone paper, and paper is dimmed below it, so
            // the lit attic window stays the brightest value. (Baked sprites are at most bone-bright themselves.)
            float bone = Luminance(Palette.WarmPaper);
            foreach (var graphic in game.Ui.Hud.GetComponentsInChildren<Graphic>(true))
            {
                float lit = Luminance(graphic.color) * SurfaceLuminance(graphic as Image);
                Assert.LessOrEqual(lit, bone + 0.001f, graphic.name + " is brighter than bone");
            }

            var prompt = game.Ui.FirstPrompt.GetComponent<Image>();
            Assert.Less(Luminance(prompt.color), 1f, "in-play paper is dimmed below full bone");
        }

        private static float Luminance(Color c) => 0.2126f * c.r + 0.7152f * c.g + 0.0722f * c.b;

        /// <summary>Baked felt and paper surfaces carry their own colour (tinted white); white line art does not.</summary>
        private static float SurfaceLuminance(Image image)
        {
            if (image == null || image.sprite == null)
            {
                return 1f;
            }

            switch (image.sprite.name)
            {
                case UiSpriteCatalog.FeltDiscNight:
                    return Luminance(Palette.NightViolet);
                case UiSpriteCatalog.FeltDiscOrange:
                case UiSpriteCatalog.FeltButtonOrange:
                    return Luminance(Palette.JackOrange);
                case UiSpriteCatalog.FeltDiscBone:
                case UiSpriteCatalog.PaperCard:
                case UiSpriteCatalog.PaperTag:
                case UiSpriteCatalog.GlyphKey:
                case UiSpriteCatalog.GhostHand:
                    return Luminance(Palette.WarmPaper);
                default:
                    return 1f;
            }
        }

        private static void AssertOnePrimary(GameObject screen, string label)
        {
            int primaries = 0;
            foreach (var button in screen.GetComponentsInChildren<CraftButton>(false))
            {
                if (button.Style == CraftButtonStyle.Primary)
                {
                    primaries++;
                }
            }

            Assert.AreEqual(1, primaries, label + " must show exactly one orange primary action");
        }
    }
}
