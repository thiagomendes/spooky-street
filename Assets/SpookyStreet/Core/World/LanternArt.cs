using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.World
{
    /// <summary>
    /// Names, measures and colours of the paper lantern table's art (docs/living-street.md §4.2, D34, D35;
    /// docs/living-street-lantern-art.md): the four lanterns Pip can fold, the folding craft table on the Green, her
    /// porch hook and the picker's pictures. Art only: the table's interaction, saving and the groups ringing her bell
    /// live elsewhere. The numbers mirror the Blender builders (tools/blender/env/lanterns.py) and their manifest
    /// (lanterns_manifest.json).
    /// <para>Shapes and colours are keyed by stable string IDs (the same as the slice's lantern IDs), never by
    /// display text. D35: never a moon, star or pumpkin (the Players' moon, June's star witch, Bea's pumpkin).</para>
    /// <para>Frames: Blender builder space (x, y, z up) is Unity (x, z, y) in a model's own frame; models face -Z.</para>
    /// </summary>
    public static class LanternArt
    {
        /// <summary>Resources folder of the models and the shared atlas (load with
        /// <c>new ModelKit(materials, Folder) { FallbackAtlas = Atlas }</c>).</summary>
        public const string Folder = "Models/Environment";

        public const string Atlas = "Lanterns";
        public const int AtlasSize = 1024;

        // ---- the four lanterns ----

        public const string Bat = "bat";
        public const string Cat = "cat";
        public const string Ghost = "ghost";
        public const string Leaf = "leaf";

        /// <summary>The shapes the picker offers, in its order.</summary>
        public static readonly IReadOnlyList<string> Shapes = new[] { Bat, Cat, Ghost, Leaf };

        /// <summary>Looks a lantern must never take (D35, living-street §3.4 rule 6).</summary>
        public static readonly IReadOnlyList<string> ReservedShapes = new[] { "moon", "star", "pumpkin" };

        /// <summary>The lit paper of a lantern: the one renderer the game tints to the chosen colour.</summary>
        public const string PaperGlow = "PaperLantern" + GlowLevels.Suffix;

        /// <summary>Model (FBX) name of a shape: <c>bat</c> → <c>PaperLanternBat</c>.</summary>
        public static string Model(string shapeId)
        {
            CheckShape(shapeId);
            return "PaperLantern" + char.ToUpperInvariant(shapeId[0]) + shapeId.Substring(1);
        }

        /// <summary>
        /// From a lantern's origin (the top of its hang ring) down to the middle of its paper: where its light is, and
        /// where an optional small point light goes (metres, down the model's -Y once imported).
        /// </summary>
        public static float DropToCentre(string shapeId)
        {
            switch (CheckShape(shapeId))
            {
                case Bat: return 0.172f;
                case Cat: return 0.184f;
                case Ghost: return 0.246f;
                default: return 0.257f;
            }
        }

        // ---- the picker's colours: paper (the base colour) and the light through it (the emission) ----

        public const string Orange = "orange";
        public const string Plum = "plum";
        public const string Green = "green";
        public const string Yellow = "yellow";

        /// <summary>The colours the picker offers, in its order.</summary>
        public static readonly IReadOnlyList<string> Colours = new[] { Orange, Plum, Green, Yellow };

        /// <summary>sRGB paper colour (0xRRGGBB): the lantern paper's <c>_BaseColor</c>, and the swatch's colour.</summary>
        public static int PaperRgb(string colourId)
        {
            switch (CheckColour(colourId))
            {
                case Orange: return 0xEE7F2D;
                case Plum: return 0x7A3E6E;
                case Green: return 0x5E9A3A;
                default: return 0xF6D365;
            }
        }

        /// <summary>
        /// sRGB colour of the candlelight through that paper (0xRRGGBB): the lantern paper's <c>_EmissionColor</c> is
        /// this colour (linear) times <see cref="GlowLevels.PaperLantern"/>. A little lighter and warmer than the paper.
        /// </summary>
        public static int LightRgb(string colourId)
        {
            switch (CheckColour(colourId))
            {
                case Orange: return 0xF08A3A;
                case Plum: return 0xA04C88;
                case Green: return 0x9CC24C;
                default: return 0xF2CC62;
            }
        }

        // ---- the folding craft table ----

        /// <summary>The table (origin on the floor under its middle; Pip folds at its front, the model's -Z).</summary>
        public const string Table = "LanternTable";

        /// <summary>The table's string of finished lanterns, lit (colours baked in; not tinted).</summary>
        public const string TableSamplesGlow = "PaperLanternSamples" + GlowLevels.Suffix;

        public const float TableWidth = 1.3f;
        public const float TableDepth = 0.6f;
        public const float TableTopHeight = 0.62f;
        public const float TablePoleHeight = 1.78f;

        /// <summary>The table's samples, left to right from its front: shape and colour.</summary>
        public static readonly IReadOnlyList<KeyValuePair<string, string>> TableSamples = new[]
        {
            new KeyValuePair<string, string>(Bat, Plum), new KeyValuePair<string, string>(Cat, Orange),
            new KeyValuePair<string, string>(Ghost, Yellow), new KeyValuePair<string, string>(Leaf, Green),
        };

        // ---- Pip's porch hook ----

        /// <summary>The hook: a post clamped to the porch rail's corner with a curled iron arm (origin on the deck).</summary>
        public const string Hook = "LanternHook";

        /// <summary>The hook eye at the arm's end; its pivot is where a lantern's origin hangs.</summary>
        public const string HookHang = "LanternHang";

        /// <summary>The doorbell's push button on the post's front (pivot on the plate; pressed along the model's +Z).</summary>
        public const string BellButton = "BellButton";

        /// <summary>The hang point in the hook's frame (Unity axes, unturned): along +X, up, and its depth.</summary>
        public const float HangX = 0.35f;
        public const float HangHeight = 1.99f;
        public const float HangZ = -0.02f;

        public const float BellHeight = 0.98f;

        /// <summary>
        /// The art's suggested place on Pip's porch: the slice's crook post spot on the west rail's front corner, on the
        /// deck (0.6 m), turned like the kit pieces (yaw 180) so the bell faces the plaza and the lantern hangs out over
        /// the lawn, west of the rail.
        /// </summary>
        public const float PorchHookX = -1.95f;
        public const float PorchHookY = 0.6f;
        public const float PorchHookZ = -13.15f;
        public const float PorchHookYaw = 180f;

        // ---- the picker's pictures: Resources/UI/LanternPicker (tools/blender/env/lanterns_tiles.py) ----

        public const string TileAssetFolder = "Assets/SpookyStreet/Content/Resources/UI/LanternPicker/";
        public const int TileSize = 256;

        /// <summary>Resources path of a shape's picture: neutral near-white paper with an ink face and outline, made to be
        /// multiplied by the chosen colour (<c>Image.color</c>).</summary>
        public static string ShapeTile(string shapeId) => "UI/LanternPicker/lantern_shape_" + CheckShape(shapeId);

        /// <summary>Resources path of a colour's picture: a folded square of that tissue, lit, in its own colour (draw it
        /// with <c>Image.color</c> white).</summary>
        public static string ColourTile(string colourId) => "UI/LanternPicker/lantern_colour_" + CheckColour(colourId);

        public static bool IsTileAssetPath(string assetPath) =>
            assetPath != null && assetPath.StartsWith(TileAssetFolder, StringComparison.Ordinal) &&
            assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase);

        public static bool IsShape(string id) => id != null && IndexOf(Shapes, id) >= 0;

        public static bool IsColour(string id) => id != null && IndexOf(Colours, id) >= 0;

        private static string CheckShape(string id)
        {
            if (!IsShape(id))
            {
                throw new ArgumentException("Not a lantern shape: " + id, nameof(id));
            }

            return id;
        }

        private static string CheckColour(string id)
        {
            if (!IsColour(id))
            {
                throw new ArgumentException("Not a lantern colour: " + id, nameof(id));
            }

            return id;
        }

        private static int IndexOf(IReadOnlyList<string> list, string id)
        {
            for (int i = 0; i < list.Count; i++)
            {
                if (string.Equals(list[i], id, StringComparison.Ordinal))
                {
                    return i;
                }
            }

            return -1;
        }
    }
}
