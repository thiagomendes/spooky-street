using UnityEngine;

namespace SpookyStreet
{
    /// <summary>Spec §6 palette. Greybox materials use these so later art passes keep the value structure.</summary>
    public static class Palette
    {
        public static readonly Color TwilightSky = Hex(0x344767);
        public static readonly Color HouseIndigo = Hex(0x394461);
        public static readonly Color PumpkinAmber = Hex(0xF6AD58);
        public static readonly Color ClothSilver = Hex(0xDAE2EE);
        public static readonly Color WarmPaper = Hex(0xF4E6C8);

        // Greybox helpers derived from the palette (not part of the spec palette).
        public static readonly Color Ground = Hex(0x4A5168);
        public static readonly Color Hedge = Hex(0x2E4A43);
        public static readonly Color Timber = Hex(0x6B5A4E);
        public static readonly Color Ink = Hex(0x1E2233);

        public static Color Hex(int rgb, float alpha = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
    }
}
