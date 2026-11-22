using System;

namespace SpookyStreet.Core.Art
{
    /// <summary>
    /// Paints the handcrafted night sky procedurally (original artwork, spec §6 Halloween night families, art bible
    /// §3): a painterly violet gradient on grained paper with a horizon mist and faint paper-cut cloud strips, a
    /// star mask the sky shader twinkles, a paper moon with stitched felt craters, and a paper grain tile for the
    /// cut-paper clouds. Engine-free so the same pixels can be rendered and reviewed outside Unity.
    /// All outputs are sRGB bytes, row 0 at the bottom (Unity texture order), and deterministic.
    /// </summary>
    public static class SkyPainter
    {
        /// <summary>One star of the scatter, in texture space.</summary>
        public readonly struct Star
        {
            public Star(float x, float y, float radius, float level, float phase, bool sparkle)
            {
                X = x;
                Y = y;
                Radius = radius;
                Level = level;
                Phase = phase;
                Sparkle = sparkle;
            }

            /// <summary>Centre in texels.</summary>
            public float X { get; }

            public float Y { get; }

            /// <summary>Core radius in texels (before the zenith stretch).</summary>
            public float Radius { get; }

            /// <summary>Peak brightness 0..1.</summary>
            public float Level { get; }

            /// <summary>Twinkle phase 0..1; neighbours never pulse together.</summary>
            public float Phase { get; }

            /// <summary>The few large stars are four-point paper stars.</summary>
            public bool Sparkle { get; }
        }

        public const int StarCount = 300;

        /// <summary>Stars stay above the cloud strips and the mist.</summary>
        public const float StarFloorV = 0.2f;

        /// <summary>Texture V the dome maps to the zenith's neighbourhood; stars stop short of the pinch.</summary>
        public const float StarCeilingV = 0.9f;

        private static readonly float[] Mist = Rgb(0x614181);
        private static readonly float[] MistLight = Rgb(0x7C5E9E);
        private static readonly float[] HauntedPurple = Rgb(0x5B3A7A);
        private static readonly float[] DuskViolet = Rgb(0x3D2C66);
        private static readonly float[] NightViolet = Rgb(0x2A2550);
        private static readonly float[] DeepViolet = Rgb(0x1B1840);
        private static readonly float[] Zenith = Rgb(0x100D2E);
        private static readonly float[] StarLight = Rgb(0xFFF2C8);

        private static readonly float[] MoonPaper = Rgb(0xEDE3BF);
        private static readonly float[] MoonSea = Rgb(0xDCCFA6);
        private static readonly float[] MoonCrater = Rgb(0xC9BE98);
        private static readonly float[] MoonShade = Rgb(0xAFA27C);
        private static readonly float[] MoonLight = Rgb(0xFBF4DA);
        private static readonly float[] MoonThread = Rgb(0x9C8E6C);

        // Felt craters: centre (0..1, y up), radius, stitched outline.
        private static readonly float[][] Craters =
        {
            new[] { 0.34f, 0.66f, 0.115f, 1f },
            new[] { 0.67f, 0.44f, 0.085f, 1f },
            new[] { 0.42f, 0.30f, 0.095f, 1f },
            new[] { 0.63f, 0.74f, 0.045f, 0f },
            new[] { 0.22f, 0.42f, 0.04f, 0f },
            new[] { 0.78f, 0.60f, 0.03f, 0f },
            new[] { 0.55f, 0.56f, 0.026f, 0f },
            new[] { 0.60f, 0.22f, 0.034f, 0f },
        };

        /// <summary>
        /// The sky paint as <paramref name="width"/> × <paramref name="height"/> RGB24. U tiles seamlessly (the dome
        /// repeats it around the azimuth); V runs from the horizon mist to the zenith. With
        /// <paramref name="bakeStars"/> the star scatter is painted in (the fallback without the twinkle shader).
        /// </summary>
        public static byte[] PaintSky(int width, int height, bool bakeStars)
        {
            CheckSize(width, height);
            var pixels = new float[width * height * 3];

            // Paper-cut cloud strips near the horizon, far to near; each has a scalloped top that tiles in U.
            float[] bandBase = { 0.215f, 0.15f, 0.085f };
            float[] bandAmp = { 0.04f, 0.04f, 0.035f };
            int[] bandLobes = { 4, 5, 7 };
            int[] bandRipples = { 11, 13, 17 };
            float[] bandLift = { 0.1f, 0.15f, 0.2f };
            var bandTop = new float[bandBase.Length][];
            for (int b = 0; b < bandBase.Length; b++)
            {
                bandTop[b] = new float[width];
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;
                    float lobe = (float)Math.Pow(Math.Abs(Math.Sin(Math.PI * (bandLobes[b] * u + 0.37 * b + 0.11))), 0.8);
                    float ripple = (float)Math.Abs(Math.Sin(Math.PI * (bandRipples[b] * u + 0.23 * b + 0.05)));
                    float swell = Noise1(u * 3f, 3, 40 + b);
                    float deckle = (Hash(x, 977 + b * 31, 5) - 0.5f) * 0.0022f;
                    bandTop[b][x] = bandBase[b] + bandAmp[b] * (lobe * (0.45f + 0.35f * swell) + ripple * 0.3f) + deckle;
                }
            }

            float texelV = 1f / height;
            var c = new float[3];
            for (int y = 0; y < height; y++)
            {
                float v = (y + 0.5f) / height;
                for (int x = 0; x < width; x++)
                {
                    float u = (x + 0.5f) / width;

                    // Painterly gradient: the stops wobble like broad brush strokes instead of ruled lines.
                    float wobble = (Fbm(u * 6f, v * 3f, 6, 2, 11) - 0.5f) * 0.09f * Smooth(0.04f, 0.3f, v);
                    Gradient(v + wobble, c);

                    for (int b = 0; b < bandBase.Length; b++)
                    {
                        float inside = bandTop[b][x] - v;
                        float cover = Clamp01(0.5f + inside / texelV);
                        if (cover <= 0f)
                        {
                            continue;
                        }

                        // A flat paper tone a little lighter than the sky behind it, with a moonlit cut edge.
                        float[] tone = BandTone(bandBase[b], bandLift[b]);
                        float edge = 1f - Clamp01(inside / (texelV * 2.6f));
                        for (int k = 0; k < 3; k++)
                        {
                            float value = tone[k] + (MistLight[k] - tone[k]) * 0.4f * edge;
                            c[k] += (value - c[k]) * cover;
                        }
                    }

                    // Horizon mist over everything low, so distant roofs and the strips melt into it.
                    float mist = (1f - Smooth(0.0f, 0.2f, v)) * 0.8f;
                    float streak = Fbm(u * 10f, v * 46f, 10, 2, 23) - 0.5f;
                    float grain = Hash(x, y, 3) - 0.5f;
                    float shade = 1f + streak * 0.09f + grain * 0.04f;
                    int i = (y * width + x) * 3;
                    for (int k = 0; k < 3; k++)
                    {
                        float value = c[k] + (Mist[k] - c[k]) * mist;
                        pixels[i + k] = value * shade;
                    }
                }
            }

            if (bakeStars)
            {
                Star[] stars = Stars(width, height);
                for (int s = 0; s < stars.Length; s++)
                {
                    StampStar(stars[s], width, height, (index, level) =>
                    {
                        for (int k = 0; k < 3; k++)
                        {
                            float value = pixels[index * 3 + k];
                            pixels[index * 3 + k] = value + (StarLight[k] * 0.92f - value) * level * 0.85f;
                        }
                    });
                }
            }

            return ToBytes(pixels);
        }

        /// <summary>
        /// The star mask as two bytes per pixel (RG16): R is the star's brightness, G its twinkle phase (spread a
        /// little past the star so filtering never blends a phase with the empty sky).
        /// </summary>
        public static byte[] PaintStarMask(int width, int height)
        {
            CheckSize(width, height);
            var level = new float[width * height];
            var phase = new float[width * height];
            Star[] stars = Stars(width, height);
            for (int s = 0; s < stars.Length; s++)
            {
                Star star = stars[s];
                float stretch = ZenithStretch(star.Y / height);
                int reach = (int)Math.Ceiling((star.Sparkle ? star.Radius * 4.2f : star.Radius * 1.6f) + 3f);
                int reachX = (int)Math.Ceiling(reach * stretch);
                int cx = (int)Math.Floor(star.X);
                int cy = (int)Math.Floor(star.Y);
                for (int dy = -reach; dy <= reach; dy++)
                {
                    int py = cy + dy;
                    if (py < 0 || py >= height)
                    {
                        continue;
                    }

                    for (int dx = -reachX; dx <= reachX; dx++)
                    {
                        int px = ((cx + dx) % width + width) % width;
                        phase[py * width + px] = star.Phase;
                    }
                }

                StampStar(star, width, height, (index, value) => level[index] = Math.Max(level[index], value));
            }

            var bytes = new byte[width * height * 2];
            for (int i = 0; i < level.Length; i++)
            {
                bytes[i * 2] = ToByte(level[i]);
                bytes[i * 2 + 1] = ToByte(phase[i]);
            }

            return bytes;
        }

        /// <summary>The deterministic star scatter: denser overhead, three size classes.</summary>
        public static Star[] Stars(int width, int height)
        {
            var random = new Random(7);
            var stars = new Star[StarCount];
            for (int i = 0; i < stars.Length; i++)
            {
                float x = (float)random.NextDouble() * width;
                // Square-root bias: fewer stars down in the haze, more in the deep violet overhead.
                float lift = (float)Math.Sqrt(random.NextDouble());
                float v = StarFloorV + (StarCeilingV - StarFloorV) * lift;
                float kind = (float)random.NextDouble();
                float size = (float)random.NextDouble();
                bool sparkle = kind > 0.965f;
                float radius = sparkle ? 2.1f + size * 0.7f : kind > 0.7f ? 1.4f + size * 0.6f : 0.85f + size * 0.45f;
                float level = sparkle ? 0.9f + size * 0.1f : kind > 0.7f ? 0.62f + size * 0.3f : 0.34f + size * 0.34f;
                level *= 0.55f + 0.45f * Smooth(StarFloorV, StarFloorV + 0.18f, v);
                stars[i] = new Star(x, v * height, radius, level, (float)random.NextDouble(), sparkle);
            }

            return stars;
        }

        /// <summary>
        /// The paper moon as size × size RGB24: warm paper with cut-paper seas, felt craters (three with a stitched
        /// outline), felt fuzz and a soft limb. Already scaled by <paramref name="level"/> (under the bloom
        /// threshold), so it is drawn with a white tint. The disc fills the texture's inscribed circle.
        /// </summary>
        public static byte[] PaintMoon(int size, float level)
        {
            CheckSize(size, size);
            var pixels = new float[size * size * 3];
            float px = 1f / size;
            var c = new float[3];
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    Copy(MoonPaper, c);

                    // Seas: a second sheet of paper, cut along a noise contour, with a hairline shadow under its
                    // lower-right edge (the light comes from the upper left).
                    float sea = Fbm(u * 3.1f + 0.4f, v * 3.1f + 1.3f, 64, 3, 51);
                    float seaCover = Smooth(0.575f, 0.59f, sea);
                    float seaShadow = Smooth(0.575f, 0.59f, Fbm((u - px * 2.2f) * 3.1f + 0.4f, (v + px * 2.2f) * 3.1f + 1.3f,
                        64, 3, 51));
                    Mix(c, MoonShade, Clamp01(seaShadow - seaCover) * 0.45f);
                    Mix(c, MoonSea, seaCover);

                    for (int k = 0; k < Craters.Length; k++)
                    {
                        float[] crater = Craters[k];
                        float dx = u - crater[0];
                        float dy = v - crater[1];
                        float r = crater[2];
                        float d = (float)Math.Sqrt(dx * dx + dy * dy);
                        if (d > r * 1.6f)
                        {
                            continue;
                        }

                        // Raised lip catching light on the upper left, then the felt floor.
                        float towardLight = d > 1e-5f ? (-dx + dy) / (d * 1.41421f) : 0f;
                        float lip = Coverage(Math.Abs(d - r * 1.04f) - r * 0.07f, px);
                        Mix(c, MoonLight, lip * Clamp01(towardLight) * 0.75f);
                        float floor = Coverage(d - r, px);
                        Mix(c, MoonCrater, floor);

                        // Inner wall in shadow on the lit side: the floor minus the same disc slid away from the light.
                        float sx = dx - r * 0.2f;
                        float sy = dy + r * 0.2f;
                        float slid = Coverage((float)Math.Sqrt(sx * sx + sy * sy) - r, px);
                        Mix(c, MoonShade, Clamp01(floor - slid) * 0.8f);

                        if (crater[3] > 0.5f)
                        {
                            // Running stitch around the big felt patches.
                            float angle = (float)(Math.Atan2(dy, dx) / (2.0 * Math.PI)) + 0.5f;
                            float stitchCount = (float)Math.Round(r * 150f);
                            float dash = Frac(angle * stitchCount) < 0.58f ? 1f : 0f;
                            float thread = Coverage(Math.Abs(d - r * 1.3f) - px * 0.9f, px);
                            Mix(c, MoonThread, thread * dash * 0.85f);
                        }
                    }

                    // Felt fuzz, a few short fibres and a soft limb so the disc reads as a padded shape.
                    float fuzz = (Fbm(u * 40f, v * 40f, 64, 2, 71) - 0.5f) * 0.09f + (Hash(x, y, 9) - 0.5f) * 0.035f;
                    float fibre = Smooth(0.78f, 0.9f, Fbm(u * 9f, v * 70f, 64, 2, 91)) * 0.035f;
                    float fromCentre = (float)Math.Sqrt((u - 0.5f) * (u - 0.5f) + (v - 0.5f) * (v - 0.5f)) / 0.5f;
                    float limb = 1f - 0.13f * Smooth(0.62f, 1f, fromCentre);
                    float rim = Smooth(0.93f, 0.985f, fromCentre) * 0.07f;
                    float shade = (1f + fuzz + fibre) * limb + rim;
                    int i = (y * size + x) * 3;
                    for (int k = 0; k < 3; k++)
                    {
                        pixels[i + k] = Math.Min(c[k] * shade, 1f) * level;
                    }
                }
            }

            return ToBytes(pixels);
        }

        /// <summary>A tiling near-white paper grain (RGB24) that tints the cut-paper clouds and bats.</summary>
        public static byte[] PaintPaperGrain(int size)
        {
            CheckSize(size, size);
            var pixels = new float[size * size * 3];
            for (int y = 0; y < size; y++)
            {
                float v = (y + 0.5f) / size;
                for (int x = 0; x < size; x++)
                {
                    float u = (x + 0.5f) / size;
                    float pulp = Fbm(u * 8f, v * 8f, 8, 3, 131, 8) - 0.5f;
                    float fibre = Smooth(0.72f, 0.9f, Fbm(u * 4f, v * 32f, 4, 2, 151, 32));
                    float speck = Hash(x, y, 13) - 0.5f;
                    float value = 0.9f + pulp * 0.14f + fibre * 0.06f + speck * 0.04f;
                    int i = (y * size + x) * 3;
                    pixels[i] = value;
                    pixels[i + 1] = value;
                    pixels[i + 2] = value * 1.01f;
                }
            }

            return ToBytes(pixels);
        }

        /// <summary>
        /// The dome pinches texels together toward the zenith, so stars are painted wider up there to stay round
        /// (V maps almost linearly to elevation).
        /// </summary>
        public static float ZenithStretch(float v)
        {
            float cos = (float)Math.Cos(Clamp01(v) * Math.PI * 0.5);
            return 1f / Math.Max(cos, 0.3f);
        }

        private static void StampStar(Star star, int width, int height, Action<int, float> write)
        {
            float stretch = ZenithStretch(star.Y / height);
            float reach = star.Sparkle ? star.Radius * 4.2f : star.Radius * 1.6f;
            int reachY = (int)Math.Ceiling(reach) + 1;
            int reachX = (int)Math.Ceiling(reach * stretch) + 1;
            int cx = (int)Math.Floor(star.X);
            int cy = (int)Math.Floor(star.Y);
            for (int dy = -reachY; dy <= reachY; dy++)
            {
                int py = cy + dy;
                if (py < 0 || py >= height)
                {
                    continue;
                }

                for (int dx = -reachX; dx <= reachX; dx++)
                {
                    int px = ((cx + dx) % width + width) % width;
                    float ox = (cx + dx + 0.5f - star.X) / stretch;
                    float oy = py + 0.5f - star.Y;
                    float d = (float)Math.Sqrt(ox * ox + oy * oy);
                    float value = 1f - Smooth(star.Radius * 0.45f, star.Radius * 1.25f, d);
                    if (star.Sparkle)
                    {
                        // Four-point paper star: an astroid around the round core.
                        float arm = star.Radius * 3.9f;
                        float astroid = (float)(Math.Sqrt(Math.Abs(ox) / arm) + Math.Sqrt(Math.Abs(oy) / arm));
                        value = Math.Max(value, (1f - Smooth(0.72f, 1f, astroid)) * 0.9f);
                    }

                    if (value > 0f)
                    {
                        write(py * width + px, value * star.Level);
                    }
                }
            }
        }

        private static void Gradient(float v, float[] c)
        {
            if (v < 0.13f)
            {
                Lerp(Mist, HauntedPurple, Smooth(0f, 0.13f, v), c);
            }
            else if (v < 0.3f)
            {
                Lerp(HauntedPurple, DuskViolet, Smooth(0.13f, 0.3f, v), c);
            }
            else if (v < 0.48f)
            {
                Lerp(DuskViolet, NightViolet, (v - 0.3f) / 0.18f, c);
            }
            else if (v < 0.72f)
            {
                Lerp(NightViolet, DeepViolet, (v - 0.48f) / 0.24f, c);
            }
            else
            {
                Lerp(DeepViolet, Zenith, Smooth(0.72f, 1f, v), c);
            }
        }

        private static float[] BandTone(float v, float lift)
        {
            var tone = new float[3];
            Gradient(v, tone);
            Mix(tone, MistLight, lift);
            return tone;
        }

        /// <summary>Value noise 0..1, periodic in X (and in Y when <paramref name="periodY"/> is set).</summary>
        private static float Noise(float x, float y, int periodX, int seed, int periodY = 0)
        {
            int x0 = (int)Math.Floor(x);
            int y0 = (int)Math.Floor(y);
            float fx = x - x0;
            float fy = y - y0;
            fx = fx * fx * (3f - 2f * fx);
            fy = fy * fy * (3f - 2f * fy);
            int xa = ((x0 % periodX) + periodX) % periodX;
            int xb = (xa + 1) % periodX;
            int ya = y0;
            int yb = y0 + 1;
            if (periodY > 0)
            {
                ya = ((y0 % periodY) + periodY) % periodY;
                yb = (ya + 1) % periodY;
            }

            float a = Hash(xa, ya, seed);
            float b = Hash(xb, ya, seed);
            float cc = Hash(xa, yb, seed);
            float d = Hash(xb, yb, seed);
            return a + (b - a) * fx + (cc - a) * fy + (a - b - cc + d) * fx * fy;
        }

        private static float Noise1(float x, int period, int seed) => Noise(x, 0.5f, period, seed);

        private static float Fbm(float x, float y, int periodX, int octaves, int seed, int periodY = 0)
        {
            float sum = 0f;
            float weight = 0.5f;
            float total = 0f;
            for (int o = 0; o < octaves; o++)
            {
                sum += Noise(x, y, periodX, seed + o * 17, periodY) * weight;
                total += weight;
                weight *= 0.5f;
                x *= 2f;
                y *= 2f;
                periodX *= 2;
                periodY *= 2;
            }

            return sum / total;
        }

        private static float Hash(int x, int y, int seed)
        {
            unchecked
            {
                uint h = (uint)x * 374761393u + (uint)y * 668265263u + (uint)seed * 2246822519u;
                h = (h ^ (h >> 13)) * 1274126177u;
                h ^= h >> 16;
                return (h & 0xFFFFFF) / 16777216f;
            }
        }

        private static void CheckSize(int width, int height)
        {
            if (width < 16 || height < 16)
            {
                throw new ArgumentOutOfRangeException(nameof(width), "Sky textures must be at least 16 px.");
            }
        }

        private static float Coverage(float signedDistance, float pixel) => Clamp01(0.5f - signedDistance / pixel);

        private static float Smooth(float edge0, float edge1, float x)
        {
            float t = Clamp01((x - edge0) / (edge1 - edge0));
            return t * t * (3f - 2f * t);
        }

        private static void Lerp(float[] a, float[] b, float t, float[] result)
        {
            t = Clamp01(t);
            result[0] = a[0] + (b[0] - a[0]) * t;
            result[1] = a[1] + (b[1] - a[1]) * t;
            result[2] = a[2] + (b[2] - a[2]) * t;
        }

        private static void Mix(float[] c, float[] target, float t)
        {
            t = Clamp01(t);
            c[0] += (target[0] - c[0]) * t;
            c[1] += (target[1] - c[1]) * t;
            c[2] += (target[2] - c[2]) * t;
        }

        private static void Copy(float[] from, float[] to)
        {
            to[0] = from[0];
            to[1] = from[1];
            to[2] = from[2];
        }

        private static float Frac(float x) => x - (float)Math.Floor(x);

        private static float Clamp01(float x) => x < 0f ? 0f : x > 1f ? 1f : x;

        private static byte ToByte(float x) => (byte)Math.Round(Clamp01(x) * 255f);

        private static byte[] ToBytes(float[] pixels)
        {
            var bytes = new byte[pixels.Length];
            for (int i = 0; i < pixels.Length; i++)
            {
                bytes[i] = ToByte(pixels[i]);
            }

            return bytes;
        }

        private static float[] Rgb(int hex) =>
            new[] { ((hex >> 16) & 0xFF) / 255f, ((hex >> 8) & 0xFF) / 255f, (hex & 0xFF) / 255f };
    }
}
