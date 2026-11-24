using System;

namespace SpookyStreet.Core.World
{
    /// <summary>
    /// Names and measures of the S004 seed and keepsake art (S004 plan §2.1, §2.2): the troupe photo, the owl crest,
    /// the collectible paper bat, the bat mobile, the dress-up trunk, Pip's three costume pieces and the three sketch
    /// pages. Art only: collecting, counting and saving live elsewhere. The numbers mirror the Blender builders
    /// (tools/blender/env/interior.py, owl_house.py, keepsakes.py, tools/blender/chars/costume_pieces.py) and their
    /// manifests (keepsakes_manifest.json, costume_manifest.json).
    /// <para>Frames: Blender builder space (x, y, z up) is Unity (x, z, y) in a model's own frame.</para>
    /// </summary>
    public static class KeepsakeArt
    {
        // ---- the troupe photo (its own model; hangs in the workshop mechanism's frame, facing the foyer) ----
        public const string TroupePhoto = "TroupePhoto";

        /// <summary>Name of the spawned instance: the cutscenes find Kit's photo by it, under the mechanism.</summary>
        public const string KitPhoto = "KitPhoto";

        /// <summary>The frame, outside (metres).</summary>
        public const float PhotoWidth = 0.7f;
        public const float PhotoHeight = 0.45f;

        /// <summary>Frame centre in the mechanism's frame (interior.py PHOTO): between the dado and frieze rails.</summary>
        public const float PhotoLocalX = -0.8f;
        public const float PhotoLocalY = 2.26f;
        public const float PhotoLocalZ = 0f;

        /// <summary>The model's origin in the world: on the partition's face (the mechanism's yaw applies).</summary>
        public const float PhotoOriginX = CaseRouteLayout.MechanismX;

        /// <summary>The frame centre in the world (the mechanism stands at yaw 90, so its -X runs north).</summary>
        public const float PhotoWorldX = PhotoOriginX - 0.03f;
        public const float PhotoWorldY = CaseRouteLayout.NookFloorY + PhotoLocalY;
        public const float PhotoWorldZ = CaseRouteLayout.MechanismZ - PhotoLocalX;

        /// <summary>The photo looks west, into the hall.</summary>
        public const float PhotoFacingYaw = 270f;

        public const string PuppetOwl = "Puppet_Owl";
        public const string PuppetFox = "Puppet_Fox";
        public const string PuppetMoth = "Puppet_Moth";
        public const string PuppetMoon = "Puppet_Moon";
        public const string PuppetBell = "Puppet_Bell";

        /// <summary>
        /// The five gilt puppets, left to right as the Players stand (Kit's owl first; the bell's Player is half out of
        /// the frame). Each is a child of the photo whose pivot is the puppet's centre: the seed's glints land there.
        /// </summary>
        public static readonly string[] Puppets = { PuppetOwl, PuppetFox, PuppetMoth, PuppetMoon, PuppetBell };

        /// <summary>The Players' crest on the photo's mount, lower right corner (a child; pivot at its centre).</summary>
        public const string PhotoCrest = "PhotoCrest";

        /// <summary>
        /// Where a puppet (or the mount crest) sits on the photo's face, from the frame centre (interior.py PUPPETS and
        /// PHOTO_CREST): <paramref name="across"/> left to right as a viewer in the hall sees it, <paramref name="up"/> up.
        /// The cutscenes use it when the model is not loaded; with the model they read the parts' pivots.
        /// </summary>
        public static bool TryGetPhotoPart(string part, out float across, out float up)
        {
            switch (part)
            {
                case PuppetOwl: across = -0.257f; up = 0.074f; return true;
                case PuppetFox: across = -0.092f; up = 0.092f; return true;
                case PuppetMoth: across = 0.057f; up = 0.066f; return true;
                case PuppetMoon: across = 0.176f; up = 0.104f; return true;
                case PuppetBell: across = 0.243f; up = 0.05f; return true;
                case PhotoCrest: across = 0.268f; up = -0.143f; return true;
                default: across = 0f; up = 0f; return false;
            }
        }

        /// <summary>The booth crest's board centre in the world (the house stands at its layout origin with no yaw).</summary>
        public const float CrestWorldX = EnvironmentLayout.HouseX + CrestLocalX;
        public const float CrestWorldY = CrestLocalY;
        public const float CrestWorldZ = EnvironmentLayout.HouseZ + CrestLocalZ;

        // ---- the owl crest over the booth (its own model, so the Players' other machines can carry it later) ----
        public const string OwlCrest = "OwlCrest";

        /// <summary>The sign's painted face: a glow part at <see cref="GlowLevels.CrestSign"/> (lit paint, no bloom).</summary>
        public const string CrestFace = "CrestSign" + GlowLevels.Suffix;

        /// <summary>Diameter of the painted board: it must read from the plaza.</summary>
        public const float CrestDiameter = 0.35f;

        /// <summary>
        /// Board centre (back face) in the owl house's frame: hung from the porch beam on the door's axis, in front of
        /// the bunting, so from the plaza it sits right over the ticket shutter (owl_house.py CREST_AT less the house
        /// origin). The house faces -Z.
        /// </summary>
        public const float CrestLocalX = 0f;
        public const float CrestLocalY = 2.475f;
        public const float CrestLocalZ = -4.07f;

        // ---- keepsakes: shared atlas "Keepsakes" (ModelKit.FallbackAtlas) ----
        public const string KeepsakesAtlas = "Keepsakes";
        public const string PaperBat = "PaperBatKeepsake";
        public const string WingLeft = "Wing_L";
        public const string WingRight = "Wing_R";

        /// <summary>Wingspan of the collectible bat, open (metres). Its origin is the lowest point of its body.</summary>
        public const float BatSpan = 0.34f;

        /// <summary>
        /// The bat is modelled open (cream inner fold and pencil lines showing). Shut, each wing turns about its
        /// upright fold by this many degrees (from the open pose, which already leans 12) over the front, so only the
        /// ink-black backs show. The fold is the wing's
        /// local Z (<c>Vector3.forward</c>): children of an imported model keep the builder's Z-up axes under the root's
        /// turn. <see cref="WingLeft"/>: <c>rest * AngleAxis(+WingShutDegrees, forward)</c>; <see cref="WingRight"/>:
        /// <c>rest * AngleAxis(-WingShutDegrees, forward)</c> (proved on the import by KeepsakeArtImportTests).
        /// </summary>
        public const float WingShutDegrees = 150f;

        public const string BatMobile = "BatMobile";
        public const int MobileRings = 3;
        public const int HooksPerRing = 10;
        public const int MobileHooks = MobileRings * HooksPerRing;

        /// <summary>Scale of a keepsake bat hung on the mobile; it hangs by its ears, this far (unscaled) under the hook.</summary>
        public const float MobileBatScale = 0.4f;
        public const float MobileBatHang = 0.111f;

        /// <summary>Overall drop of the mobile under its hook (its origin), bats included.</summary>
        public const float MobileDrop = 1.02f;
        public const float MobileRadius = 0.33f;

        /// <summary>The hoop of ring 1..3 with its gap (always drawn once the ring has a bat).</summary>
        public static string Ring(int ring) => "Ring" + Checked(ring, MobileRings);

        /// <summary>The piece that closes ring 1..3 when its ten bats are found.</summary>
        public static string RingClose(int ring) => "Ring" + Checked(ring, MobileRings) + "_Close";

        /// <summary>The lit paper of ring 1..3's little lantern (a glow part at <see cref="GlowLevels.KeepsakeLantern"/>).</summary>
        public static string RingLantern(int ring) => "KeepsakeLantern" + Checked(ring, MobileRings) + GlowLevels.Suffix;

        /// <summary>Hook 1..30, in the order bats are hung (ring 1 first). Its pivot is where the bat hangs.</summary>
        public static string Hook(int number) => "Hook_" + Checked(number, MobileHooks).ToString("00");

        public const string DressUpTrunk = "DressUpTrunk";

        /// <summary>
        /// The trunk's lid: hinged on the back edge (its pivot). Open: <c>rest * AngleAxis(TrunkLidOpenDegrees, right)</c>
        /// in its own frame.
        /// </summary>
        public const string TrunkLid = "TrunkLid";
        public const float TrunkLidOpenDegrees = 105f;
        public const float TrunkSize = 0.6f;

        // ---- Pip's costume pieces: Resources/Models/Costume ----
        public const string CostumeFolder = "Models/Costume/";
        public const string CostumeAtlas = "PipCostume";

        /// <summary>
        /// Star-lined cape: Pip's own atlas with only the cape lining repainted. Swap it for <c>Pip_BaseColor</c> on
        /// the material her atlas meshes share; nothing else changes.
        /// </summary>
        public const string StarCapeTexture = CostumeFolder + "PipStarCape_BaseColor";

        /// <summary>
        /// Showman's hat: a mesh in Pip's model space in her bind pose, to ride her <see cref="HatBone"/>. Attach from
        /// the bind pose, not from the prefab's transforms (they hold an exported pose): with <c>skin</c> a skinned mesh
        /// of the Pip prefab asset that uses the bone at index <c>i</c>, and <c>piece</c> the piece's prefab asset,
        /// <c>m = skin.sharedMesh.bindposes[i] * skin.transform.worldToLocalMatrix * piece.transform.localToWorldMatrix</c>
        /// is the piece's local matrix under the bone (position <c>m.GetColumn(3)</c>, rotation <c>m.rotation</c>,
        /// scale <c>m.lossyScale</c>). The same holds for the lantern pail.
        /// </summary>
        public const string ShowmanHat = "PipShowmanHat";
        public const string HatBone = "Head";

        /// <summary>
        /// Lantern pail: a mesh in Pip's model space (rest pose), to ride her <see cref="PailBone"/> in place of the
        /// <see cref="PipPailMesh"/> renderer. Its paper is the glow child <see cref="PailGlow"/>.
        /// </summary>
        public const string LanternPail = "PipLanternPail";
        public const string PailBone = "Pail";
        public const string PipPailMesh = "Pip_Pail";
        public const string PailGlow = "KeepsakePail" + GlowLevels.Suffix;

        // ---- the sketch pages: Resources/UI/SketchPages ----
        public const int SketchPages = 3;
        public const int SketchPageWidth = 1024;
        public const int SketchPageHeight = 768;
        public const string SketchPageAssetFolder = "Assets/SpookyStreet/Content/Resources/UI/SketchPages/";

        /// <summary>Resources path of sketch page 1..3 (a sprite: old paper with a ragged, transparent edge).</summary>
        public static string SketchPage(int page) => "UI/SketchPages/sketch_page_" + Checked(page, SketchPages);

        // ---- the dress-up trunk's picture cards: Resources/UI/CostumeIcons (tools/blender/chars/costume_pieces.py icons) ----
        public const string CostumeIconAssetFolder = "Assets/SpookyStreet/Content/Resources/UI/CostumeIcons/";
        public const int CostumeIconSize = 256;

        /// <summary>Resources path of a piece's card picture; <paramref name="pieceId"/> is its stable ID
        /// (<c>keepsake.costume.star_cape</c> → <c>UI/CostumeIcons/costume_icon_star_cape</c>).</summary>
        public static string CostumeIcon(string pieceId)
        {
            if (string.IsNullOrEmpty(pieceId))
            {
                throw new ArgumentException("A piece ID is needed.", nameof(pieceId));
            }

            int dot = pieceId.LastIndexOf('.');
            return "UI/CostumeIcons/costume_icon_" + pieceId.Substring(dot + 1);
        }

        public static bool IsCostumeIconAssetPath(string assetPath) =>
            assetPath != null && assetPath.StartsWith(CostumeIconAssetFolder, StringComparison.Ordinal) &&
            assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase);

        public static bool IsSketchPageAssetPath(string assetPath) =>
            assetPath != null && assetPath.StartsWith(SketchPageAssetFolder, StringComparison.Ordinal) &&
            assetPath.EndsWith(".png", StringComparison.OrdinalIgnoreCase);

        private static int Checked(int value, int max)
        {
            if (value < 1 || value > max)
            {
                throw new ArgumentOutOfRangeException(nameof(value), value, "1.." + max);
            }

            return value;
        }
    }
}
