using System;

namespace SpookyStreet.Core.Art
{
    /// <summary>
    /// WCAG 2.1 contrast arithmetic on 0xRRGGBB colours (docs/ui-language.md "Contrast": ink on bone 13.8:1, every text
    /// at least AA). Text needs 4.5:1 (AA) and a state marker such as "this card is chosen" 3:1 against what it sits on
    /// (WCAG 1.4.11, non-text contrast).
    /// </summary>
    public static class ColourContrast
    {
        /// <summary>WCAG AA for text of any size.</summary>
        public const double TextAA = 4.5;

        /// <summary>WCAG AAA for body text: what the game's ink on paper reaches.</summary>
        public const double TextAAA = 7.0;

        /// <summary>WCAG 1.4.11: user-interface components and state markers.</summary>
        public const double NonText = 3.0;

        /// <summary>Relative luminance (sRGB, WCAG 2.1 formula).</summary>
        public static double Luminance(int rgb)
        {
            return 0.2126 * Channel((rgb >> 16) & 0xFF) + 0.7152 * Channel((rgb >> 8) & 0xFF) + 0.0722 * Channel(rgb & 0xFF);
        }

        /// <summary>Contrast ratio between two colours, 1 to 21.</summary>
        public static double Ratio(int a, int b)
        {
            double la = Luminance(a);
            double lb = Luminance(b);
            return (Math.Max(la, lb) + 0.05) / (Math.Min(la, lb) + 0.05);
        }

        /// <summary>A surface colour tinted by a vertex colour (uGUI multiplies them channel by channel).</summary>
        public static int Multiply(int a, int b)
        {
            int r = ((a >> 16) & 0xFF) * ((b >> 16) & 0xFF) / 255;
            int g = ((a >> 8) & 0xFF) * ((b >> 8) & 0xFF) / 255;
            int bl = (a & 0xFF) * (b & 0xFF) / 255;
            return (r << 16) | (g << 8) | bl;
        }

        /// <summary><paramref name="top"/> drawn at <paramref name="alpha"/> over <paramref name="under"/>.</summary>
        public static int Over(int top, int under, double alpha)
        {
            int Mix(int shift) => (int)Math.Round(((top >> shift) & 0xFF) * alpha + ((under >> shift) & 0xFF) * (1 - alpha));
            return (Mix(16) << 16) | (Mix(8) << 8) | Mix(0);
        }

        private static double Channel(int c)
        {
            double s = c / 255.0;
            return s <= 0.03928 ? s / 12.92 : Math.Pow((s + 0.055) / 1.055, 2.4);
        }
    }
}
