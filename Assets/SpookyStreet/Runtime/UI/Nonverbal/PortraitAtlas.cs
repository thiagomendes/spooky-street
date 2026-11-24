using System.Collections.Generic;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Nonverbal;
using UnityEngine;

namespace SpookyStreet.UI.Nonverbal
{
    /// <summary>
    /// The baked dialogue portraits (<c>Resources/UI/Portraits/portraits.png</c>, tools/blender/export/portraits.py)
    /// cut into one sprite per speaker and emotion with the engine-free <see cref="PortraitAtlasLayout"/> table, the
    /// way <see cref="PuppetAtlas"/> cuts the puppet sheet. Sprites are created once and cached. When the atlas is
    /// missing the lookups return null and the dialogue keeps the puppet face (then the initial) in the felt disc.
    /// </summary>
    public static class PortraitAtlas
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static Texture2D s_texture;
        private static bool s_loaded;

        public static Texture2D Texture
        {
            get
            {
                if (!s_loaded)
                {
                    s_loaded = true;
                    s_texture = Resources.Load<Texture2D>(PortraitAtlasLayout.ResourcePath);
                    if (s_texture == null)
                    {
                        Debug.LogWarning("[PortraitAtlas] Resources/" + PortraitAtlasLayout.ResourcePath +
                                         " missing; run tools/blender/export/portraits.py.");
                    }
                }

                return s_texture;
            }
        }

        public static bool Available => Texture != null;

        /// <summary>The portrait of a line (its speaker in its <c>LineVoices</c> emotion), or null for a caption.</summary>
        public static Sprite For(ScriptLine line) => Get(LinePortraits.SpriteName(line));

        /// <summary>The badge sprite <c>portrait.&lt;speaker&gt;.&lt;emotion&gt;</c>, or null when unknown or missing.</summary>
        public static Sprite Get(string id)
        {
            if (id == null)
            {
                return null;
            }

            if (Cache.TryGetValue(id, out var sprite) && sprite != null)
            {
                return sprite;
            }

            var texture = Texture;
            if (texture == null || !PortraitAtlasLayout.TryGet(id, out var rect))
            {
                return null;
            }

            // The import may scale the atlas down (low-memory devices): rects are authored for the full size.
            float k = texture.width / (float)PortraitAtlasLayout.AtlasSize;
            var r = new Rect(rect.X * k, rect.Y * k, rect.Width * k, rect.Height * k);
            sprite = Sprite.Create(texture, r, new Vector2(PortraitAtlasLayout.DiscCenterX, PortraitAtlasLayout.DiscCenterY),
                100f * k, 0, SpriteMeshType.FullRect);
            sprite.name = id;
            Cache[id] = sprite;
            return sprite;
        }

        /// <summary>
        /// Anchors (fractions of a round frame) that put a cell's disc on the frame's centre with a diameter of
        /// <paramref name="discFraction"/> of the frame; the head and hat rise above the frame's top edge.
        /// </summary>
        public static void AnchorsOnFrame(float discFraction, out Vector2 anchorMin, out Vector2 anchorMax)
        {
            float w = discFraction / (2f * PortraitAtlasLayout.DiscRadius); // cell width in frame widths
            anchorMin = new Vector2(0.5f - PortraitAtlasLayout.DiscCenterX * w, 0.5f - PortraitAtlasLayout.DiscCenterY * w);
            anchorMax = new Vector2(anchorMin.x + w, anchorMin.y + w);
        }
    }
}
