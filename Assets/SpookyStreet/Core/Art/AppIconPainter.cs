using System;

namespace SpookyStreet.Core.Art
{
    /// <summary>
    /// Paints the placeholder app icon procedurally (original artwork, spec §6 palette): a twilight sky,
    /// the indigo owl house with its glowing round attic window, and a small moon. Output is opaque RGB
    /// (App Store icons must not contain alpha). Replace with final art when the visual language is locked.
    /// </summary>
    public static class AppIconPainter
    {
        private static readonly float[] SkyTop = Rgb(0x1F2A40);
        private static readonly float[] SkyBottom = Rgb(0x344767);
        private static readonly float[] Ground = Rgb(0x2A3349);
        private static readonly float[] House = Rgb(0x1E2233);
        private static readonly float[] Window = Rgb(0xF6AD58);
        private static readonly float[] Moon = Rgb(0xDAE2EE);
        private static readonly float[] Door = Rgb(0x394461);

        /// <summary>Returns size × size RGB24 pixels, row 0 at the bottom (Unity texture order).</summary>
        public static byte[] PaintRgb(int size)
        {
            if (size < 16)
            {
                throw new ArgumentOutOfRangeException(nameof(size), "Icon size must be at least 16 px.");
            }

            var pixels = new byte[size * size * 3];
            float px = 1f / size;
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float[] c = Lerp(SkyBottom, SkyTop, Smooth(0.2f, 1f, v));

                    // Moon, upper right.
                    c = Blend(c, Moon, Coverage(Distance(u, v, 0.79f, 0.84f) - 0.055f, px));

                    // Warm glow behind the attic window.
                    float glow = 1f - Smooth(0.07f, 0.24f, Distance(u, v, 0.5f, 0.64f));
                    c = Blend(c, Window, glow * 0.35f);

                    // House body and peaked roof.
                    float body = Coverage(Box(u, v, 0.5f, 0.38f, 0.25f, 0.2f), px);
                    float roof = Coverage(Roof(u, v), px);
                    c = Blend(c, House, Math.Max(body, roof));

                    // Round attic window (repeats the owl's eye shape) and a dim door.
                    c = Blend(c, Window, Coverage(Distance(u, v, 0.5f, 0.64f) - 0.075f, px));
                    c = Blend(c, Door, Coverage(Box(u, v, 0.5f, 0.27f, 0.055f, 0.09f), px));

                    // Ground strip.
                    c = Blend(c, Ground, Coverage(v - 0.18f, px));

                    int i = (y * size + x) * 3;
                    pixels[i] = ToByte(c[0]);
                    pixels[i + 1] = ToByte(c[1]);
                    pixels[i + 2] = ToByte(c[2]);
                }
            }

            return pixels;
        }

        // Signed distance helpers: negative inside the shape.
        private static float Distance(float u, float v, float cx, float cy) =>
            (float)Math.Sqrt((u - cx) * (u - cx) + (v - cy) * (v - cy));

        private static float Box(float u, float v, float cx, float cy, float hx, float hy) =>
            Math.Max(Math.Abs(u - cx) - hx, Math.Abs(v - cy) - hy);

        private static float Roof(float u, float v)
        {
            // Triangle with apex (0.5, 0.86) and base from 0.19 to 0.81 at v = 0.56.
            float baseDistance = 0.56f - v;
            float slope = Math.Abs(u - 0.5f) / 0.31f + (v - 0.56f) / 0.30f - 1f;
            return Math.Max(baseDistance, slope * 0.2f);
        }

        /// <summary>Anti-aliased coverage of a signed distance over about one pixel.</summary>
        private static float Coverage(float signedDistance, float pixel) =>
            Clamp01(0.5f - signedDistance / pixel);

        private static float Smooth(float edge0, float edge1, float x)
        {
            float t = Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static float[] Blend(float[] a, float[] b, float t)
        {
            t = Clamp01(t);
            return t <= 0f ? a : Lerp(a, b, t);
        }

        private static float[] Lerp(float[] a, float[] b, float t) =>
            new[] { a[0] + (b[0] - a[0]) * t, a[1] + (b[1] - a[1]) * t, a[2] + (b[2] - a[2]) * t };

        private static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;

        private static byte ToByte(float x) => (byte)Math.Round(Clamp01(x) * 255f);

        private static float[] Rgb(int hex) =>
            new[] { ((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f };
    }
}
