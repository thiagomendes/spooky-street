using System;
using System.Collections.Generic;
using UnityEngine;

namespace SpookyStreet.UI
{
    /// <summary>
    /// Procedural white sprites (tinted at use) for the paper-cutout UI: rounded cards, rings, discs and simple
    /// line icons. Drawn from signed-distance shapes with one-pixel anti-aliasing, so no binary art is committed
    /// and every icon is trivially replaceable when final art lands.
    /// </summary>
    public static class IconFactory
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();

        /// <summary>9-sliced rounded rectangle for paper cards and buttons.</summary>
        public static Sprite RoundedRect => Get("RoundedRect", () =>
        {
            const int size = 96;
            const float radius = 22f;
            return Paint("RoundedRect", size, (x, y) => RoundBox(x, y, size * 0.5f, size * 0.5f, size * 0.5f - 1f,
                size * 0.5f - 1f, radius), new Vector4(30f, 30f, 30f, 30f));
        });

        public static Sprite Disc => Get("Disc", () =>
            Paint("Disc", 128, (x, y) => Length(x - 64f, y - 64f) - 63f, Vector4.zero));

        /// <summary>Ring whose stroke is <paramref name="strokeFraction"/> of the radius.</summary>
        public static Sprite Ring(float strokeFraction = 0.06f) => Get("Ring" + strokeFraction, () =>
            Paint("Ring", 128, (x, y) => Mathf.Abs(Length(x - 64f, y - 64f) - 61f) - 61f * strokeFraction * 0.5f,
                Vector4.zero));

        public static Sprite PauseBars => Get("PauseBars", () => Paint("PauseBars", 128, (x, y) =>
            Mathf.Min(RoundBox(x, y, 46f, 64f, 11f, 34f, 6f), RoundBox(x, y, 82f, 64f, 11f, 34f, 6f)), Vector4.zero));

        /// <summary>Upward chevron (jump).</summary>
        public static Sprite ChevronUp => Get("ChevronUp", () => Paint("ChevronUp", 128, (x, y) =>
            Mathf.Min(Segment(x, y, 30f, 50f, 64f, 84f), Segment(x, y, 98f, 50f, 64f, 84f)) - 9f, Vector4.zero));

        /// <summary>Small chevron pointing up; rotate the image for the other stick directions.</summary>
        public static Sprite StickArrow => Get("StickArrow", () => Paint("StickArrow", 64, (x, y) =>
            Mathf.Min(Segment(x, y, 18f, 26f, 32f, 40f), Segment(x, y, 46f, 26f, 32f, 40f)) - 5f, Vector4.zero));

        /// <summary>Globe line icon (language).</summary>
        public static Sprite Globe => Get("Globe", () => Paint("Globe", 128, (x, y) =>
        {
            float outer = Mathf.Abs(Length(x - 64f, y - 64f) - 50f) - 4.5f;
            float meridian = Mathf.Abs(Length((x - 64f) / 0.45f, y - 64f) - 50f) * 0.45f - 4.5f;
            float equator = Mathf.Max(Mathf.Abs(y - 64f) - 4.5f, Length(x - 64f, 0f) - 50f);
            return Mathf.Min(outer, Mathf.Min(meridian, equator));
        }, Vector4.zero));

        /// <summary>Horizontal fade from opaque (left) to transparent (right), for the title's reading scrim.</summary>
        public static Sprite HorizontalFade => Get("HorizontalFade", () =>
        {
            const int width = 128;
            var texture = new Texture2D(width, 4, TextureFormat.RGBA32, false)
            {
                name = "HorizontalFade",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            for (int x = 0; x < width; x++)
            {
                float t = x / (width - 1f);
                float alpha = 1f - t * t * (3f - 2f * t);
                for (int y = 0; y < 4; y++)
                {
                    texture.SetPixel(x, y, new Color(1f, 1f, 1f, alpha));
                }
            }

            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, width, 4), new Vector2(0.5f, 0.5f), 100f);
        });

        /// <summary>Soft radial falloff (opaque centre, transparent edge): a quiet shade behind HUD clusters.</summary>
        public static Sprite RadialFade => Get("RadialFade", () =>
        {
            const int size = 64;
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = "RadialFade",
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Mathf.Clamp01(Length(x + 0.5f - size * 0.5f, y + 0.5f - size * 0.5f) / (size * 0.5f));
                    float a = Mathf.Pow(1f - d, 1.6f);
                    pixels[y * size + x] = new Color32(255, 255, 255, (byte)(a * 255f));
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f);
        });

        private static Sprite Get(string key, Func<Sprite> create)
        {
            if (!Cache.TryGetValue(key, out var sprite) || sprite == null)
            {
                sprite = create();
                Cache[key] = sprite;
            }

            return sprite;
        }

        private static Sprite Paint(string name, int size, Func<float, float, float> distance, Vector4 border)
        {
            var texture = new Texture2D(size, size, TextureFormat.RGBA32, false)
            {
                name = name,
                wrapMode = TextureWrapMode.Clamp,
                filterMode = FilterMode.Bilinear,
            };
            var pixels = new Color32[size * size];
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = distance(x + 0.5f, y + 0.5f);
                    byte alpha = (byte)(Mathf.Clamp01(0.5f - d) * 255f);
                    pixels[y * size + x] = new Color32(255, 255, 255, alpha);
                }
            }

            texture.SetPixels32(pixels);
            texture.Apply(false, true);
            return Sprite.Create(texture, new Rect(0, 0, size, size), new Vector2(0.5f, 0.5f), 100f, 0,
                SpriteMeshType.FullRect, border);
        }

        private static float Length(float x, float y) => Mathf.Sqrt(x * x + y * y);

        private static float RoundBox(float x, float y, float cx, float cy, float hx, float hy, float r)
        {
            float qx = Mathf.Abs(x - cx) - (hx - r);
            float qy = Mathf.Abs(y - cy) - (hy - r);
            return Length(Mathf.Max(qx, 0f), Mathf.Max(qy, 0f)) + Mathf.Min(Mathf.Max(qx, qy), 0f) - r;
        }

        private static float Segment(float px, float py, float ax, float ay, float bx, float by)
        {
            float pax = px - ax;
            float pay = py - ay;
            float bax = bx - ax;
            float bay = by - ay;
            float h = Mathf.Clamp01((pax * bax + pay * bay) / (bax * bax + bay * bay));
            return Length(pax - bax * h, pay - bay * h);
        }
    }
}
