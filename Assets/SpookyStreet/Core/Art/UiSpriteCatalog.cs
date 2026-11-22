using System;

namespace SpookyStreet.Core.Art
{
    /// <summary>How a baked UI sprite is drawn by uGUI.</summary>
    public enum UiSpriteDraw
    {
        /// <summary>Stretched as a whole (discs, icons, the logo); keep the aspect where it matters.</summary>
        Simple,
        /// <summary>9-slice: corners fixed, edges and centre stretched (paper and felt surfaces).</summary>
        Sliced,
        /// <summary>9-slice with tiled edges (the running stitch), so dashes repeat instead of stretching.</summary>
        Tiled,
    }

    /// <summary>One baked sprite: file name (no extension) under Resources/UI, draw mode and 9-slice border.</summary>
    public readonly struct UiSpriteSpec
    {
        public UiSpriteSpec(string name, UiSpriteDraw draw, int border = 0)
            : this(name, draw, border, border, border, border)
        {
        }

        public UiSpriteSpec(string name, UiSpriteDraw draw, int left, int bottom, int right, int top)
        {
            Name = name;
            Draw = draw;
            Left = left;
            Bottom = bottom;
            Right = right;
            Top = top;
        }

        public string Name { get; }
        public UiSpriteDraw Draw { get; }
        public int Left { get; }
        public int Bottom { get; }
        public int Right { get; }
        public int Top { get; }
        public bool HasBorder => Left > 0 || Bottom > 0 || Right > 0 || Top > 0;
    }

    /// <summary>
    /// The baked UI craft sprites (art bible v2 §6, docs/ui-language.md). The PNGs are written by
    /// <c>tools/ui_mock/ui_mock.py --export</c> into <c>Content/Resources/UI</c>; the editor import rules read the
    /// borders from here, and the runtime loads sprites by these names. Sprites are drawn at 2 px per reference
    /// unit, so they import at <see cref="PixelsPerUnit"/> = 200 against the canvas' 100 reference pixels per unit:
    /// a border of 44 px is 22 reference units (11 pt) on screen. Keep this table in step with the script.
    /// </summary>
    public static class UiSpriteCatalog
    {
        /// <summary>Folder under Resources.</summary>
        public const string ResourceFolder = "UI";

        /// <summary>Asset folder the import rules apply to.</summary>
        public const string AssetFolder = "Assets/SpookyStreet/Content/Resources/UI/";

        public const int PixelsPerUnit = 200;

        /// <summary>Discs and icons stay at or under this edge length; only the logo is larger.</summary>
        public const int MaxSpriteSize = 256;

        public const int MaxLogoWidth = 1024;

        public const string FeltDiscNight = "felt_disc_night";
        public const string FeltDiscOrange = "felt_disc_orange";
        public const string FeltDiscBone = "felt_disc_bone";
        public const string StitchRing = "stitch_ring";
        public const string PaperCard = "paper_card";
        public const string PaperTag = "paper_tag";
        public const string FeltButtonOrange = "felt_button_orange";
        public const string StitchRect = "stitch_rect";
        public const string StickChevron = "stick_chevron";
        public const string Logo = "logo";
        public const string IconJump = "icon_jump";
        public const string IconExamine = "icon_examine";
        public const string IconPause = "icon_pause";
        public const string IconNotebook = "icon_notebook";
        public const string IconBat = "icon_bat";
        public const string IconGear = "icon_gear";
        public const string IconGlobe = "icon_globe";
        public const string IconPlay = "icon_play";
        public const string IconClose = "icon_close";

        public static readonly UiSpriteSpec[] All =
        {
            new UiSpriteSpec(FeltDiscNight, UiSpriteDraw.Simple),
            new UiSpriteSpec(FeltDiscOrange, UiSpriteDraw.Simple),
            new UiSpriteSpec(FeltDiscBone, UiSpriteDraw.Simple),
            new UiSpriteSpec(StitchRing, UiSpriteDraw.Simple),
            new UiSpriteSpec(PaperCard, UiSpriteDraw.Sliced, 44),
            new UiSpriteSpec(PaperTag, UiSpriteDraw.Sliced, 34),
            new UiSpriteSpec(FeltButtonOrange, UiSpriteDraw.Sliced, 48),
            new UiSpriteSpec(StitchRect, UiSpriteDraw.Tiled, 48),
            new UiSpriteSpec(StickChevron, UiSpriteDraw.Simple),
            new UiSpriteSpec(Logo, UiSpriteDraw.Simple),
            new UiSpriteSpec(IconJump, UiSpriteDraw.Simple),
            new UiSpriteSpec(IconExamine, UiSpriteDraw.Simple),
            new UiSpriteSpec(IconPause, UiSpriteDraw.Simple),
            new UiSpriteSpec(IconNotebook, UiSpriteDraw.Simple),
            new UiSpriteSpec(IconBat, UiSpriteDraw.Simple),
            new UiSpriteSpec(IconGear, UiSpriteDraw.Simple),
            new UiSpriteSpec(IconGlobe, UiSpriteDraw.Simple),
            new UiSpriteSpec(IconPlay, UiSpriteDraw.Simple),
            new UiSpriteSpec(IconClose, UiSpriteDraw.Simple),
        };

        /// <summary>Resources.Load path for a sprite name.</summary>
        public static string ResourcePath(string name) => ResourceFolder + "/" + name;

        /// <summary>Looks a sprite up by its file name (without extension); false when it is not catalogued.</summary>
        public static bool TryFind(string name, out UiSpriteSpec spec)
        {
            foreach (var entry in All)
            {
                if (string.Equals(entry.Name, name, StringComparison.Ordinal))
                {
                    spec = entry;
                    return true;
                }
            }

            spec = default;
            return false;
        }

        /// <summary>Asset path → sprite name, or null when the path is not a PNG directly in the UI folder.</summary>
        public static string NameFromAssetPath(string assetPath)
        {
            if (assetPath == null || !assetPath.StartsWith(AssetFolder, StringComparison.Ordinal) ||
                !assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string rest = assetPath.Substring(AssetFolder.Length);
            return rest.IndexOf('/') >= 0 ? null : rest.Substring(0, rest.Length - 4);
        }
    }
}
