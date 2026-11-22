using System.Collections.Generic;
using SpookyStreet.Core.Nonverbal;
using UnityEngine;

namespace SpookyStreet.UI.Nonverbal
{
    /// <summary>
    /// The baked puppet sheet (<c>Resources/UI/puppet_atlas.png</c>, tools/ui_mock/puppet_atlas.py) cut into one
    /// sprite per part with the engine-free <see cref="PuppetAtlasLayout"/> rect table. Sprites are created once and
    /// cached; all of them share the one texture, so a vignette is a single UI batch. When the sheet is missing the
    /// lookups return null and callers draw nothing (the text line and the face disc still carry the line).
    /// </summary>
    public static class PuppetAtlas
    {
        /// <summary>Sprite pixels per canvas unit; only the 9-sliced stage border depends on it.</summary>
        public const float PixelsPerUnit = 100f;

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
                    s_texture = Resources.Load<Texture2D>(PuppetAtlasLayout.ResourcePath);
                    if (s_texture == null)
                    {
                        Debug.LogWarning("[PuppetAtlas] Resources/" + PuppetAtlasLayout.ResourcePath +
                                         " missing; run tools/ui_mock/puppet_atlas.py.");
                    }
                }

                return s_texture;
            }
        }

        public static bool Available => Texture != null;

        /// <summary>The part (or extra) sprite, or null when it is unknown or the sheet is missing.</summary>
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
            if (texture == null || !PuppetAtlasLayout.TryGet(id, out var rect))
            {
                return null;
            }

            // The import may scale the sheet down (low-memory devices): rects are authored for the full size.
            float k = texture.width / (float)PuppetAtlasLayout.AtlasSize;
            var r = new Rect(rect.X * k, rect.Y * k, rect.Width * k, rect.Height * k);
            float border = rect.Border * k;
            sprite = Sprite.Create(texture, r, new Vector2(0.5f, 0.5f), PixelsPerUnit * k, 0, SpriteMeshType.FullRect,
                new Vector4(border, border, border, border));
            sprite.name = id;
            Cache[id] = sprite;
            return sprite;
        }

        /// <summary>Size in canvas units of <paramref name="id"/> at scale 1 on a stage <paramref name="stageHeight"/> tall.</summary>
        public static Vector2 NominalSize(string id, float stageHeight)
        {
            if (!PuppetAtlasLayout.TryGet(id, out var rect))
            {
                return new Vector2(stageHeight * 0.85f, stageHeight * 0.85f);
            }

            float h = rect.StageHeight * stageHeight;
            return new Vector2(h * rect.Aspect, h);
        }

        /// <summary>The flipbook frame <paramref name="frame"/> of a part (June running), or the part itself.</summary>
        public static string FrameOf(string part, int frame)
        {
            if (part != PuppetParts.JuneRun)
            {
                return part;
            }

            int n = PuppetAtlasLayout.JuneRunFrames.Count + 1;
            int f = ((frame % n) + n) % n;
            return f == 0 ? part : PuppetAtlasLayout.JuneRunFrames[f - 1];
        }
    }
}
