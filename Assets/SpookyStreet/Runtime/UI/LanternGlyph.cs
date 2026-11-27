using System.Collections.Generic;
using SpookyStreet.App;
using SpookyStreet.Core.LivingStreet;
using SpookyStreet.UI.Nonverbal;
using UnityEngine;
using UnityEngine.UI;

namespace SpookyStreet.UI
{
    /// <summary>
    /// A paper lantern's picture in the UI (the lantern table's cards and the notebook's street page), built from paper
    /// shapes so it needs no new sprite: bat (the puppet atlas's bat), cat (a round head with two pointed ears), ghost
    /// (a round top, a sheet and a scalloped hem) and leaf (a three-lobed maple leaf with its stem). D35: never moon,
    /// star or pumpkin. The paper parts take the lantern's colour; the eyes stay ink.
    /// </summary>
    public static class LanternGlyph
    {
        /// <summary>Builds the picture of <paramref name="shape"/> inside <paramref name="parent"/>; returns the tintable parts.</summary>
        public static List<Image> Build(RectTransform parent, LanternShape shape, float size, Color color)
        {
            var paper = new List<Image>();
            var root = UiFactory.Rect(parent, "Lantern_" + LanternCraft.ShapeId(shape));
            UiFactory.Place(root, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(size, size));
            float s = size;
            switch (shape)
            {
                case LanternShape.Bat:
                    var bat = Piece(root, "Bat", PuppetAtlas.Get("pg.bat"), Vector2.zero, new Vector2(s, s * 0.8f), 0f, paper);
                    bat.preserveAspect = true;
                    break;
                case LanternShape.Cat:
                    Piece(root, "EarL", null, new Vector2(-s * 0.22f, s * 0.2f), new Vector2(s * 0.3f, s * 0.3f), 45f, paper);
                    Piece(root, "EarR", null, new Vector2(s * 0.22f, s * 0.2f), new Vector2(s * 0.3f, s * 0.3f), 45f, paper);
                    Piece(root, "Head", UiFactory.Circle, new Vector2(0f, -s * 0.05f), new Vector2(s * 0.7f, s * 0.62f), 0f, paper);
                    Eye(root, new Vector2(-s * 0.13f, -s * 0.02f), s * 0.09f);
                    Eye(root, new Vector2(s * 0.13f, -s * 0.02f), s * 0.09f);
                    break;
                case LanternShape.Ghost:
                    Piece(root, "Top", UiFactory.Circle, new Vector2(0f, s * 0.12f), new Vector2(s * 0.62f, s * 0.62f), 0f, paper);
                    Piece(root, "Sheet", null, new Vector2(0f, -s * 0.12f), new Vector2(s * 0.62f, s * 0.42f), 0f, paper);
                    for (int i = 0; i < 3; i++)
                    {
                        Piece(root, "Hem" + i, UiFactory.Circle, new Vector2((i - 1) * s * 0.2f, -s * 0.33f),
                            new Vector2(s * 0.22f, s * 0.2f), 0f, paper);
                    }

                    Eye(root, new Vector2(-s * 0.11f, s * 0.1f), s * 0.1f);
                    Eye(root, new Vector2(s * 0.11f, s * 0.1f), s * 0.1f);
                    break;
                default:
                    Piece(root, "Stem", null, new Vector2(0f, -s * 0.34f), new Vector2(s * 0.05f, s * 0.24f), 0f, paper);
                    Piece(root, "LobeL", null, new Vector2(-s * 0.2f, -s * 0.02f), new Vector2(s * 0.32f, s * 0.32f), 45f, paper);
                    Piece(root, "LobeR", null, new Vector2(s * 0.2f, -s * 0.02f), new Vector2(s * 0.32f, s * 0.32f), 45f, paper);
                    Piece(root, "Lobe", null, new Vector2(0f, s * 0.08f), new Vector2(s * 0.42f, s * 0.42f), 45f, paper);
                    break;
            }

            foreach (var image in paper)
            {
                image.color = color;
            }

            return paper;
        }

        private static Image Piece(RectTransform root, string name, Sprite sprite, Vector2 at, Vector2 size, float rotation,
            List<Image> paper)
        {
            var image = UiFactory.Sprite(root, name, sprite, Color.white, new Vector2(0.5f, 0.5f), at, size);
            image.rectTransform.localRotation = Quaternion.Euler(0f, 0f, rotation);
            image.raycastTarget = false;
            paper.Add(image);
            return image;
        }

        private static void Eye(RectTransform root, Vector2 at, float size)
        {
            var eye = UiFactory.Sprite(root, "Eye", UiFactory.Circle, Palette.InkBlack, new Vector2(0.5f, 0.5f), at,
                new Vector2(size, size));
            eye.raycastTarget = false;
        }
    }
}
