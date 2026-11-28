using System;
using System.Collections.Generic;
using SpookyStreet.App;
using SpookyStreet.Core.LivingStreet;
using SpookyStreet.Localization;
using SpookyStreet.UI.Nonverbal;
using UnityEngine;
using UnityEngine.UI;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.LivingStreet;

namespace SpookyStreet.UI
{
    /// <summary>
    /// The notebook's street page (living-street §4, §9 phase 1), last in the carousel once Pip has made her lantern or
    /// rung a neighbour's bell: a paper drawing of her porch as it is now (her lantern hangs lit on it once folded) and a
    /// face disc with the name of each neighbour she has met. Keepsakes rules (S004 §2.1): only what was done, no empty
    /// slot, no silhouette of someone not yet met, no count. Placeholder paper art until the street page's drawings.
    /// </summary>
    public sealed partial class NotebookView
    {
        private readonly List<Image> _streetFaces = new List<Image>();

        /// <summary>What the free world remembers (set by the director); null shows no street page.</summary>
        public Func<NeighbourhoodState> Neighbourhood { get; set; }

        /// <summary>The street page (null when there is none).</summary>
        public RectTransform StreetPage { get; private set; }

        /// <summary>The face discs drawn on it, in street order (tests).</summary>
        public IReadOnlyList<Image> StreetFaces => _streetFaces;

        /// <summary>The lantern drawn on the porch (null before Pip folds one).</summary>
        public RectTransform StreetLantern { get; private set; }

        private void BuildStreetPage()
        {
            StreetPage = null;
            StreetLantern = null;
            _streetFaces.Clear();
            var state = Neighbourhood != null ? Neighbourhood() : null;
            if (!StreetPageRules.Shown(state))
            {
                return;
            }

            var page = NewPage("StreetPage");
            StreetPage = page;
            _pageViews.Add(new PageView { Page = page, Question = null, Overlay = UiFactory.Stretch(UiFactory.Rect(page, "Overlay")) });
            AccessibleName.Set(page.gameObject, Keys.StreetPage);
            var title = UiFactory.Label(page, "Title", Keys.StreetPage, 30, TextAnchor.MiddleCenter, UiFactory.Ink, UiFactory.DisplayFont);
            UiFactory.Place(title.rectTransform, new Vector2(0.5f, 1f), new Vector2(0f, -46f), new Vector2(600f, 50f));

            // Pip's porch, drawn in paper: deck, steps, two posts, a rail and the roof line; her lantern on the left post.
            var porch = UiFactory.Rect(page, "Porch");
            UiFactory.Place(porch, new Vector2(0.5f, 0.5f), new Vector2(-200f, -20f), new Vector2(380f, 330f));
            var wood = Palette.Hex(0x8A6B52);
            Block(porch, "Wall", new Vector2(0f, 40f), new Vector2(330f, 200f), Palette.Hex(0x4E5878));
            Block(porch, "Door", new Vector2(40f, 10f), new Vector2(80f, 140f), Palette.Plum);
            Block(porch, "Deck", new Vector2(0f, -70f), new Vector2(360f, 26f), wood);
            Block(porch, "Step", new Vector2(40f, -98f), new Vector2(120f, 22f), wood);
            Block(porch, "PostL", new Vector2(-160f, 30f), new Vector2(16f, 200f), wood);
            Block(porch, "PostR", new Vector2(160f, 30f), new Vector2(16f, 200f), wood);
            Block(porch, "Rail", new Vector2(-100f, -30f), new Vector2(120f, 10f), wood);
            var roofL = Block(porch, "RoofL", new Vector2(-92f, 168f), new Vector2(210f, 24f), Palette.HauntedPurple);
            roofL.rectTransform.localRotation = Quaternion.Euler(0f, 0f, 14f);
            var roofR = Block(porch, "RoofR", new Vector2(92f, 168f), new Vector2(210f, 24f), Palette.HauntedPurple);
            roofR.rectTransform.localRotation = Quaternion.Euler(0f, 0f, -14f);
            if (state.Lantern.HasValue)
            {
                var lantern = state.Lantern.Value;
                var glow = UiFactory.Sprite(porch, "LanternGlow", UiFactory.Circle,
                    UiFactory.WithAlpha(Palette.CandyCornYellow, 0.35f), new Vector2(0.5f, 0.5f), new Vector2(-124f, 70f),
                    new Vector2(110f, 110f));
                glow.raycastTarget = false;
                StreetLantern = UiFactory.Rect(porch, "Lantern");
                UiFactory.Place(StreetLantern, new Vector2(0.5f, 0.5f), new Vector2(-124f, 70f), new Vector2(64f, 64f));
                var tile = Resources.Load<Sprite>(SpookyStreet.Core.World.LanternArt.ShapeTile(LanternCraft.ShapeId(lantern.Shape)));
                if (tile != null)
                {
                    // The lantern art's picture, tinted with her paper colour (ink stays ink).
                    UiFactory.Sprite(StreetLantern, "Tile", tile, Palette.Hex(LanternCraft.ColourRgb(lantern.Colour)),
                        new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(80f, 80f)).preserveAspect = true;
                }
                else
                {
                    LanternGlyph.Build(StreetLantern, lantern.Shape, 64f, Palette.Hex(LanternCraft.ColourRgb(lantern.Colour)));
                }
                AccessibleName.Set(StreetLantern.gameObject, LanternCraft.ShapeKey(lantern.Shape));
            }

            // The neighbours Pip has met: a face disc in their door's colour, the name under it.
            var faces = StreetPageRules.Faces(state);
            for (int i = 0; i < faces.Count; i++)
            {
                var door = faces[i];
                var at = new Vector2(190f + (i % 2) * 170f - (faces.Count == 1 ? 85f : 0f), 70f - (i / 2) * 190f);
                var disc = UiFactory.Sprite(page, "Face_" + door.Id, UiFactory.Circle, Palette.Hex(StreetPageRules.FaceRgb(door)),
                    new Vector2(0.5f, 0.5f), at, new Vector2(116f, 116f));
                UiFactory.InkShadow(disc, new Vector2(3f, -4f), 0.45f);
                var face = UiFactory.Sprite(disc.transform, "Smile", PuppetAtlas.Get("pg.wave_hand"), Palette.WarmPaper,
                    new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72f, 72f));
                face.preserveAspect = true;
                var name = UiFactory.Label(page, "Name_" + door.Id, door.SpeakerKey, 24, TextAnchor.UpperCenter, UiFactory.Ink,
                    UiFactory.BoldFont);
                UiFactory.Place(name.rectTransform, new Vector2(0.5f, 0.5f), at + new Vector2(0f, -84f), new Vector2(170f, 44f));
                _streetFaces.Add(disc);
            }
        }

        private static Image Block(RectTransform parent, string name, Vector2 at, Vector2 size, Color color)
        {
            var image = UiFactory.Sprite(parent, name, null, color, new Vector2(0.5f, 0.5f), at, size);
            image.raycastTarget = false;
            return image;
        }

    }
}
