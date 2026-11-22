using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.Nonverbal
{
    /// <summary>
    /// The puppet parts library (docs/nonverbal-ux.md §9): the v1 pictograms (`nv_icons.png`, drawn by
    /// <c>tools/ui_mock/nonverbal_mock.py</c> GLYPHS) reused as parts, plus the v2 puppet parts. The runtime
    /// <c>PuppetAtlas</c> must hold one sprite per ID (named exactly the ID); vignette data may use nothing else.
    /// </summary>
    public static class PuppetParts
    {
        public const string Prefix = "pg.";

        // v2 parts (new art, baked with the atlas).
        public const string JuneRun = "pg.june_run";
        public const string PipBody = "pg.pip_body";
        public const string BeaArm = "pg.bea_arm";
        public const string Clock = "pg.clock";
        public const string ClockHand = "pg.clock_hand";
        public const string Puff = "pg.puff";
        public const string Stage = "pg.stage";
        public const string EyeShut = "pg.eye_shut";
        public const string KeyRing = "pg.key_ring";

        /// <summary>v1 pictogram names, grouped as in the v1 parts sheet.</summary>
        private static readonly string[] V1 =
        {
            // characters
            "pip", "bea", "vale", "june", "kit",
            // places and things
            "house", "house_open", "house_side", "house_cut", "window", "june_window", "door_shut", "door_open",
            "stuck", "lock", "latch", "hedge", "hedge_cape", "bench", "treehouse", "bridge", "porch", "bike", "lamp",
            "pin",
            // clues
            "cape", "cloth", "streamer", "feet", "sweet", "bowl", "candy",
            // the machine
            "knock3", "knock_ring", "floor_hum", "grille", "cam", "arm", "connect", "gap", "crank", "wind", "owl",
            "owl_bow", "booth",
            // senses, logic, feelings
            "eye", "ear", "hands", "pointer", "question", "check", "not", "heart", "moon", "star", "bat", "memory",
            "quiet",
            // moves
            "tip_walk", "tip_jump", "tip_hold", "tip_chain", "tip_glide", "tip_dive",
            // emotes
            "emo_wow", "emo_huh", "emo_love", "emo_tune", "emo_think", "emo_proud",
        };

        private static readonly string[] V2 = { JuneRun, PipBody, BeaArm, Clock, ClockHand, Puff, Stage, EyeShut, KeyRing };

        /// <summary>Every part ID, v1 then v2.</summary>
        public static readonly IReadOnlyList<string> All = Build();

        private static readonly HashSet<string> Set = new HashSet<string>(All, StringComparer.Ordinal);

        public static bool Contains(string id) => id != null && Set.Contains(id);

        /// <summary>"pg.window" for "window".</summary>
        public static string Id(string pictogram) => Prefix + pictogram;

        private static string[] Build()
        {
            var all = new string[V1.Length + V2.Length];
            for (int i = 0; i < V1.Length; i++)
            {
                all[i] = Prefix + V1[i];
            }

            Array.Copy(V2, 0, all, V1.Length, V2.Length);
            return all;
        }
    }
}
