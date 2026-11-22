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

        // v1.3 Halloween night families (spec §6, owner).
        public static readonly Color NightViolet = Hex(0x2A2550);
        public static readonly Color HauntedPurple = Hex(0x5B3A7A);
        public static readonly Color Plum = Hex(0x7A3E6E);
        public static readonly Color JackOrange = Hex(0xEE7F2D);
        public static readonly Color CandyCornYellow = Hex(0xF6D365);
        public static readonly Color InkBlack = Hex(0x1E1A26);

        // Night look helpers derived from those families (key frame docs/evidence/s000.2/env_keyframe_v2.png).
        public static readonly Color NightZenith = Hex(0x17143C);
        public static readonly Color NightFog = Hex(0x3A2D5E);
        public static readonly Color NightAmbientSky = Hex(0x4B4A86);
        public static readonly Color NightAmbientEquator = Hex(0x3A2E5C);
        public static readonly Color NightAmbientGround = Hex(0x221C30);
        public static readonly Color MoonCool = Hex(0x8FA8FF);
        public static readonly Color MoonDisc = Hex(0xEDE3BF);
        public static readonly Color MoonCrater = Hex(0xC9BE98);
        public static readonly Color StarLight = Hex(0xFFF2C8);
        public static readonly Color SkylineWall = Hex(0x231E44);
        public static readonly Color SkylineRoof = Hex(0x33255A);

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
