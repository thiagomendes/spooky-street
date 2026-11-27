namespace SpookyStreet.Core.Localization
{
    public static partial class LocalizationKeys
    {
        /// <summary>
        /// The notebook's sticker memories (D32, docs/S004-sticker-memory.md): one descriptive caption per clue sticker
        /// under its short memory picture, plus the card's accessibility name. Captions say what and where, never what
        /// it means (D26). Kept in its own file like <see cref="CaseUi"/>; <see cref="StickerMemory.All"/> is checked
        /// against en.json and pt-BR.json by StickerMemoryTests.
        /// </summary>
        public static class StickerMemory
        {
            public const string BeaFlash = "c01.memory.bea_flash";
            public const string ValeDoor = "c01.memory.vale_door";
            public const string BenchView = "c01.memory.bench_view";
            public const string StuckDoor = "c01.memory.stuck_door";
            public const string Streamer = "c01.memory.streamer";
            public const string Cloth = "c01.memory.cloth";
            public const string Sill = "c01.memory.sill";
            public const string FloorHum = "c01.memory.floor_hum";
            public const string WoodenPiece = "c01.memory.wooden_piece";
            public const string OwlPanel = "c01.memory.owl_panel";
            public const string OwlBooth = "c01.memory.owl_booth";
            public const string LooseArm = "c01.memory.loose_arm";

            /// <summary>Accessibility name of the open memory card (pressing it closes it).</summary>
            public const string Card = "ui.notebook.memory_card";

            public static readonly string[] Captions =
            {
                BeaFlash, ValeDoor, BenchView, StuckDoor, Streamer, Cloth, Sill, FloorHum, WoodenPiece, OwlPanel, OwlBooth,
                LooseArm,
            };

            public static readonly string[] All =
            {
                BeaFlash, ValeDoor, BenchView, StuckDoor, Streamer, Cloth, Sill, FloorHum, WoodenPiece, OwlPanel, OwlBooth,
                LooseArm, Card,
            };
        }
    }
}
