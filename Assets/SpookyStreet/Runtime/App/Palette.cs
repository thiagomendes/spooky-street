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

        // Greybox helpers derived from the palette (not part of the spec palette). Art bible: docs/art-bible.md.
        public static readonly Color Ground = Hex(0x3F4559);
        public static readonly Color Hedge = Hex(0x2E4A43);
        public static readonly Color Timber = Hex(0x6B5A4E);
        public static readonly Color Path = Hex(0x7E6C5F);
        public static readonly Color Ink = Hex(0x1E2233);
        public static readonly Color RoofShadow = Hex(0x283044);
        public static readonly Color SkyHorizon = Hex(0x515665);
        public static readonly Color AmbientSky = Hex(0x3E557C);
        public static readonly Color AmbientGround = Hex(0x2A2430);
        public static readonly Color Moonlight = Hex(0xB8C6E0);

        // Pip (spec §2): moth costume under a short rain cape. Never cloth silver: that is June's clue colour.
        public static readonly Color PipCape = Hex(0xC58A46);
        public static readonly Color PipFuzz = WarmPaper;
        public static readonly Color PipFace = Hex(0xE6D3AE);
        public static readonly Color PipWing = Hex(0xCDB894);
        public static readonly Color PipLegs = Hex(0x2B3044);

        public static Color Hex(int rgb, float alpha = 1f) =>
            new Color(((rgb >> 16) & 0xFF) / 255f, ((rgb >> 8) & 0xFF) / 255f, (rgb & 0xFF) / 255f, alpha);
    }
}
