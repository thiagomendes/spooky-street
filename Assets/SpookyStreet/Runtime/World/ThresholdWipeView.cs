using SpookyStreet.Core.Art;
using SpookyStreet.Core.World;
using SpookyStreet.UI;
using SpookyStreet.UI.Nonverbal;
using UnityEngine;
using UnityEngine.UI;

namespace SpookyStreet.World
{
    /// <summary>
    /// The paper page-turn of a threshold crossing (D24.1), drawn in the UI language (docs/ui-language.md): a sheet
    /// of deckled bone paper with its running stitch and a hard ink shadow turns over the screen from the right, the
    /// light catching the lifting edge, while the place's puppet stamp (June's window, the open door) sits in the
    /// middle of the page; then it turns on to the left and uncovers the new place. Reduced motion shows the same page
    /// as a quick cross-fade. The layout follows <see cref="ThresholdWipe"/> each frame; this only draws it. It blocks
    /// taps while it is up, so a touch can never reach the controls during the crossing.
    /// </summary>
    public sealed class ThresholdWipeView : MonoBehaviour
    {
        /// <summary>Over the case UI (5), under the HUD and pause menu (10), so a pause mid-crossing still shows.</summary>
        public const int SortingOrder = 8;

        /// <summary>The page overhangs the screen by this much (screen heights) so its tilt never shows a corner.</summary>
        public const float Overhang = 0.18f;

        private static Sprite s_rampLeft;
        private static Sprite s_rampRight;

        private CanvasGroup _group;
        private RectTransform _root;
        private RectTransform _page;
        private RectTransform _curl;
        private RectTransform _crease;
        private RectTransform _shade;
        private RectTransform _fold;
        private Image _glyph;

        public Canvas Canvas { get; private set; }

        public RectTransform Page => _page;

        /// <summary>The puppet stamp in the middle of the page.</summary>
        public Image Glyph => _glyph;

        /// <summary>True while the page is on screen.</summary>
        public bool Visible => _root != null && _root.gameObject.activeSelf;

        public static ThresholdWipeView Create(Transform parent)
        {
            var canvas = UiFactory.CreateCanvas("ThresholdWipe", SortingOrder);
            canvas.transform.SetParent(parent, false);
            var view = canvas.gameObject.AddComponent<ThresholdWipeView>();
            view.Canvas = canvas;
            view.Build();
            return view;
        }

        private void Build()
        {
            _root = UiFactory.Stretch(UiFactory.Rect(Canvas.transform, "Wipe"));
            _group = _root.gameObject.AddComponent<CanvasGroup>();
            // Swallows taps while the page is up.
            var catcher = UiFactory.Panel(_root, "TapCatcher", new Color(0f, 0f, 0f, 0f));
            catcher.raycastTarget = true;

            var page = UiFactory.Sprite(_root, "Page", null, Color.white, Vector2.zero, Vector2.zero, Vector2.zero);
            UiFactory.ApplySurface(page, UiSpriteCatalog.PaperCard, Palette.WarmPaper, Color.white);
            UiFactory.InkShadow(page, new Vector2(-10f, -12f), 0.55f);
            _page = page.rectTransform;
            UiFactory.StitchLayer(page.transform, Palette.HauntedPurple);

            // The shadow the lifting page throws on the world, just ahead of its moving edge.
            var shade = UiFactory.Sprite(_page, "CastShadow", Ramp(), UiFactory.WithAlpha(Palette.InkBlack, 0.5f),
                Vector2.zero, Vector2.zero, Vector2.zero);
            _shade = shade.rectTransform;

            // The page bends as it turns: a soft shade across it just behind the lifted edge.
            var fold = UiFactory.Sprite(_page, "Fold", Ramp(), UiFactory.WithAlpha(Palette.InkBlack, 0.22f), Vector2.zero,
                Vector2.zero, Vector2.zero);
            _fold = fold.rectTransform;

            // The light on the curling edge, and the crease where the curl meets the flat page.
            var curl = UiFactory.Sprite(_page, "Curl", Ramp(), UiFactory.WithAlpha(Color.white, 0.75f), Vector2.zero,
                Vector2.zero, Vector2.zero);
            _curl = curl.rectTransform;
            var crease = UiFactory.Sprite(_page, "Crease", null, UiFactory.WithAlpha(Palette.InkBlack, 0.28f),
                Vector2.zero, Vector2.zero, Vector2.zero);
            _crease = crease.rectTransform;

            // The place's stamp: a puppet part in a stitched ring, like the goal stickers.
            var ring = UiFactory.Sprite(_page, "StampRing", UiSprites.TryLoad(UiSpriteCatalog.StitchRing),
                Palette.HauntedPurple, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(250f, 250f));
            ring.preserveAspect = true;
            _glyph = UiFactory.Sprite(ring.transform, "Stamp", null, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(190f, 190f));
            _glyph.preserveAspect = true;
            _root.gameObject.SetActive(false);
        }

        /// <summary>Raises the page for a crossing through <paramref name="threshold"/>.</summary>
        public void Show(ThresholdId threshold)
        {
            var sprite = PuppetAtlas.Get(threshold == ThresholdId.FrontDoor ? "pg.door_open" : "pg.june_window");
            _glyph.sprite = sprite;
            _glyph.enabled = sprite != null;
            _root.gameObject.SetActive(true);
            _root.SetAsLastSibling();
        }

        public void Hide()
        {
            _root.gameObject.SetActive(false);
        }

        /// <summary>Lays the page out for this frame of <paramref name="wipe"/>.</summary>
        public void Apply(ThresholdWipe wipe)
        {
            if (!wipe.Active)
            {
                Hide();
                return;
            }

            float screen = Mathf.Max(1f, _root.rect.width);
            bool revealing = wipe.Phase == WipePhase.Revealing;
            _group.alpha = wipe.Alpha;
            // The page spans its edges in screen widths; it hinges on the edge that is not moving.
            _page.anchorMin = new Vector2(wipe.PageLeft, -Overhang);
            _page.anchorMax = new Vector2(wipe.PageRight, 1f + Overhang);
            _page.offsetMin = Vector2.zero;
            _page.offsetMax = Vector2.zero;
            _page.pivot = new Vector2(revealing ? 0f : 1f, 0.5f);
            _page.localRotation = Quaternion.Euler(0f, 0f, wipe.Tilt);

            // The moving edge: the left one while the page comes in, the right one while it goes.
            float edge = revealing ? 1f : 0f;
            float curl = wipe.Curl * screen;
            bool lifting = curl > 1f;
            _curl.gameObject.SetActive(lifting);
            _crease.gameObject.SetActive(lifting);
            _shade.gameObject.SetActive(lifting);
            _fold.gameObject.SetActive(lifting);
            if (lifting)
            {
                Edge(_fold, edge, 2.5f * curl, inside: true, offset: curl);
                Edge(_curl, edge, curl, inside: true);
                Edge(_crease, edge, 3f, inside: true, offset: curl);
                Edge(_shade, edge, 0.06f * screen + curl, inside: false);
            }
        }

        /// <summary>A strip along the page's left (0) or right (1) edge, inside or outside the page, fading away from it.</summary>
        private static void Edge(RectTransform strip, float side, float width, bool inside, float offset = 0f)
        {
            bool extendsRight = (side < 0.5f) == inside;
            strip.anchorMin = new Vector2(side, 0f);
            strip.anchorMax = new Vector2(side, 1f);
            strip.pivot = new Vector2(extendsRight ? 0f : 1f, 0.5f);
            strip.sizeDelta = new Vector2(width, 0f);
            strip.anchoredPosition = new Vector2(extendsRight ? offset : -offset, 0f);
            var image = strip.GetComponent<Image>();
            if (image != null && image.sprite != null)
            {
                image.sprite = Ramp(opaqueLeft: extendsRight);
            }
        }

        /// <summary>A one-row ramp from opaque to clear, made once at runtime (no art asset).</summary>
        private static Sprite Ramp(bool opaqueLeft = true)
        {
            var cached = opaqueLeft ? s_rampLeft : s_rampRight;
            if (cached != null)
            {
                return cached;
            }

            const int width = 64;
            var texture = new Texture2D(width, 1, TextureFormat.RGBA32, false)
            {
                name = opaqueLeft ? "PageRampLeft" : "PageRampRight",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            for (int i = 0; i < width; i++)
            {
                float u = i / (width - 1f);
                float a = opaqueLeft ? 1f - u : u;
                texture.SetPixel(i, 0, new Color(1f, 1f, 1f, a * a));
            }

            texture.Apply(false, true);
            cached = Sprite.Create(texture, new Rect(0f, 0f, width, 1f), new Vector2(0.5f, 0.5f), 100f);
            if (opaqueLeft)
            {
                s_rampLeft = cached;
            }
            else
            {
                s_rampRight = cached;
            }

            return cached;
        }
    }
}
