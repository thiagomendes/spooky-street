using System;
using System.Collections.Generic;
using SpookyStreet.Core.Case;
using Lines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;

namespace SpookyStreet.Core.Nonverbal
{
    /// <summary>How the dialogue bubble shows one script line.</summary>
    public sealed class LineVisual
    {
        internal LineVisual(string lineKey, string vignetteId)
        {
            LineKey = lineKey;
            VignetteId = vignetteId;
        }

        /// <summary>The <see cref="CaseScript"/> line key (also its localization key).</summary>
        public string LineKey { get; }

        /// <summary>The animated vignette, or null while the line has none yet.</summary>
        public string VignetteId { get; }

        /// <summary>
        /// Explicit fallback (owner decision D23.3): no vignette, so the bubble keeps the v1 card. No case 01 line uses
        /// it any more; it stays for lines added later before their vignette exists.
        /// </summary>
        public bool UsesV1Strip => VignetteId == null;
    }

    /// <summary>
    /// Case 01 vignette data (docs/nonverbal-ux.md §3, §5; D26 spoiler pass in S002.5a: witnesses show only what they
    /// perceived, no "this matters" ring and no check stamp in case content, and the notebook's answer comic is built
    /// from the child's own stickers in the view instead of a fixed strip). D23.3 staged it: first the four key Bea/Vale
    /// lines and June's resolve line (this file); S002 then gave every other line its vignette
    /// (<c>C01Vignettes.Lines.cs</c>), so every <see cref="CaseScript"/> line key maps to one and no line keeps the v1
    /// fallback. Coordinates are stage-normalized (see <see cref="PuppetPose"/>); parts marked as flipbooks
    /// (<see cref="PuppetParts.JuneRun"/>) cycle their frames in the view while the track moves.
    /// </summary>
    public static partial class C01Vignettes
    {
        public const string BeaSawCape = "vg.bea.saw_cape";
        public const string BeaDoorOpen = "vg.bea.door_open";
        public const string ValeDoorShut = "vg.vale.door_shut";
        public const string ValeWasShut = "vg.vale.was_shut";
        public const string JuneResolve = "vg.june.resolve";


        /// <summary>The first five key lines (owner decision D23.3), each with its own approved choreography.</summary>
        public static readonly IReadOnlyList<string> KeyLineKeys = new[]
        {
            Lines.BeaSawCape, Lines.BeaDoorOpen, Lines.ValeDoorShut, Lines.ValeWasShut, Lines.JuneResolve,
        };

        /// <summary>The line keys that get a vignette: every case 01 line (S002 closed D23.3's staging).</summary>
        public static readonly IReadOnlyList<string> ScopedLineKeys = new List<string>(CaseScript.AllLineKeys);

        public static readonly IReadOnlyList<Vignette> All = BuildAll();

        private static readonly Dictionary<string, Vignette> ById = Index();

        private static readonly Dictionary<string, string> LineToVignette = BuildLineMap();

        /// <summary>
        /// The line table: one entry per <see cref="CaseScript.AllLineKeys"/>, in script order, with its vignette or
        /// the explicit v1 fallback.
        /// </summary>
        public static readonly IReadOnlyList<LineVisual> LineTable = BuildLineTable();

        public static Vignette Get(string vignetteId) =>
            vignetteId != null && ById.TryGetValue(vignetteId, out var v) ? v : null;

        /// <summary>What the bubble shows for <paramref name="lineKey"/>; null for keys that are not case lines.</summary>
        public static LineVisual For(string lineKey)
        {
            if (lineKey == null || CaseScript.ConditionFor(lineKey) == null)
            {
                return null;
            }

            return new LineVisual(lineKey, LineToVignette.TryGetValue(lineKey, out string id) ? id : null);
        }

        private static Vignette[] BuildAll()
        {
            var all = new List<Vignette>
            {
                BuildBeaSawCape(), BuildBeaDoorOpen(), BuildValeDoorShut(), BuildValeWasShut(), BuildJuneResolve(),
            };
            all.AddRange(BuildLineVignettes());
            all.AddRange(BuildStreetVignettes());
            return all.ToArray();
        }

        private static Dictionary<string, string> BuildLineMap()
        {
            var map = new Dictionary<string, string>(StringComparer.Ordinal)
            {
                { Lines.BeaSawCape, BeaSawCape },
                { Lines.BeaDoorOpen, BeaDoorOpen },
                { Lines.ValeDoorShut, ValeDoorShut },
                { Lines.ValeWasShut, ValeWasShut },
                { Lines.JuneResolve, JuneResolve },
            };
            foreach (var pair in LineVignettes())
            {
                map.Add(pair.Line, pair.Vignette);
            }

            return map;
        }

        private static IReadOnlyList<LineVisual> BuildLineTable()
        {
            var table = new List<LineVisual>();
            foreach (string key in CaseScript.AllLineKeys)
            {
                table.Add(new LineVisual(key, LineToVignette.TryGetValue(key, out string id) ? id : null));
            }

            return table;
        }

        private static Dictionary<string, Vignette> Index()
        {
            var map = new Dictionary<string, Vignette>(StringComparer.Ordinal);
            foreach (var v in All)
            {
                map.Add(v.Id, v);
            }

            return map;
        }

        // ------------------------------------------------------------------------------------------- speech

        /// <summary>
        /// "Something silver flashed up high, by the side of the house!" (D26, mf_bea_testimony): only what Bea saw. A
        /// silver glint streaks high over the hedge toward the roof and is gone; then her eye, then a "?". No June, no
        /// lit window, no pointing hand, no ring.
        /// </summary>
        private static Vignette BuildBeaSawCape()
        {
            return new Vignette(BeaSawCape, 4.0f, 0,
                Beats(0.4f, 1.5f, 2.0f, 2.4f, 2.8f, 3.4f),
                new[]
                {
                    Still("treehouse", "treehouse", 0.14f, 0.56f, 0.95f),
                    Still("wall", "house_side", 0.78f, 0.50f, 1.1f),
                    Still("hedge", "hedge", 0.28f, 0.22f, 1.7f),
                    Still("hedge2", "hedge", 0.62f, 0.22f, 1.7f),
                    Still("hedge3", "hedge", 0.94f, 0.22f, 1.7f),
                    Track("flash", PuppetParts.Sparkle,
                        K(0.40f, 0.26f, 0.70f, 0.30f, 0f, 0f),
                        K(0.55f, 0.30f, 0.71f, 0.55f, 0f, 1f, Ease.Out),
                        K(1.20f, 0.62f, 0.74f, 0.45f, 90f, 1f, Ease.InOut),
                        K(1.50f, 0.70f, 0.75f, 0.25f, 140f, 0f, Ease.InOut)),
                    Pop("eye", "eye", 2.0f, 0.46f, 0.56f, 0.9f),
                    Pop("what", "question", 2.8f, 0.60f, 0.66f, 0.6f),
                },
                new VignetteCue[0]);
        }

        /// <summary>
        /// "The door's open now? That silver flash was long before." Inside the pencil-cloud memory frame the silver
        /// glint streaks over the hedge again and is gone; then Bea nods. No June, no window.
        /// </summary>
        private static Vignette BuildBeaDoorOpen()
        {
            return new Vignette(BeaDoorOpen, 3.4f, 0,
                Beats(0.2f, 1.6f, 2.2f, 3.2f),
                new[]
                {
                    Still("memory", "memory", 0.55f, 0.50f, 1.0f),
                    Still("hedge", "hedge", 0.45f, 0.34f, 1.2f),
                    Still("hedge2", "hedge", 0.72f, 0.34f, 1.2f),
                    Track("flash", PuppetParts.Sparkle,
                        K(0.20f, 0.36f, 0.62f, 0.3f, 0f, 0f),
                        K(0.35f, 0.40f, 0.63f, 0.5f, 0f, 1f, Ease.Out),
                        K(1.30f, 0.74f, 0.66f, 0.4f, 120f, 1f, Ease.InOut),
                        K(1.60f, 0.80f, 0.67f, 0.2f, 160f, 0f, Ease.InOut)),
                    Track("bea", "bea",
                        K(0f, 0.07f, 0.50f, 0.9f, 0f, 1f),
                        K(2.20f, 0.07f, 0.50f, 0.9f, 0f, 1f),
                        K(2.45f, 0.07f, 0.47f, 0.9f, -10f, 1f, Ease.Out),
                        K(2.70f, 0.07f, 0.50f, 0.9f, 0f, 1f, Ease.InOut),
                        K(2.95f, 0.07f, 0.47f, 0.9f, -10f, 1f, Ease.Out),
                        K(3.20f, 0.07f, 0.50f, 0.9f, 0f, 1f, Ease.InOut)),
                },
                new VignetteCue[0]);
        }

        /// <summary>
        /// "That front door? I could see it from my bench. It stayed shut." One still subject (the door), the clock
        /// hand ticks every 0.5 s and Vale's eye blinks once (two moving parts), then the ⊘ stamp thumps on.
        /// </summary>
        private static Vignette BuildValeDoorShut()
        {
            var tick = new List<VignetteKey>();
            for (int i = 0; i <= 5; i++)
            {
                tick.Add(K(i * 0.5f, 0.82f, 0.55f, 1.0f, -60f * i, 1f, i == 0 ? Ease.InOut : Ease.Step));
            }

            return new Vignette(ValeDoorShut, 3.6f, 0,
                Beats(0f, 2.5f, 2.6f, 3.4f),
                new[]
                {
                    Still("wall", "house", 0.50f, 0.50f, 1.2f),
                    Still("door", "door_shut", 0.50f, 0.48f, 1.0f),
                    Still("clock", PuppetParts.Clock, 0.82f, 0.55f, 1.0f),
                    new VignetteTrack("clock_hand", PuppetParts.ClockHand, tick.ToArray()),
                    Track("eye", "eye",
                        K(0f, 0.18f, 0.55f, 1.0f, 0f, 1f),
                        K(1.30f, 0.18f, 0.55f, 1.0f, 0f, 1f, Ease.Step, PuppetParts.EyeShut),
                        K(1.50f, 0.18f, 0.55f, 1.0f, 0f, 1f, Ease.Step, "pg.eye")),
                    Track("not", "not",
                        K(2.60f, 0.60f, 0.42f, 1.6f, 0f, 0f),
                        K(2.85f, 0.60f, 0.42f, 1.0f, 0f, 1f, Ease.Out)),
                },
                new[]
                {
                    new VignetteCue(2.60f, CueKind.Stamp, "not", 0.8f),
                });
        }

        /// <summary>
        /// "It was shut when I looked." Inside the memory frame the moon slides across while the door stays still,
        /// then ⊘ thumps onto the door.
        /// </summary>
        private static Vignette BuildValeWasShut()
        {
            return new Vignette(ValeWasShut, 3.2f, 0,
                Beats(0.2f, 2.4f, 2.5f, 3.2f),
                new[]
                {
                    Still("memory", "memory", 0.50f, 0.50f, 1.0f),
                    Still("door", "door_shut", 0.50f, 0.44f, 0.9f),
                    Track("moon", "moon",
                        K(0.20f, 0.22f, 0.78f, 0.6f, 0f, 1f),
                        K(2.40f, 0.78f, 0.80f, 0.6f, 0f, 1f, Ease.InOut)),
                    Track("not", "not",
                        K(2.50f, 0.60f, 0.40f, 1.5f, 0f, 0f),
                        K(2.75f, 0.60f, 0.40f, 1.0f, 0f, 1f, Ease.Out)),
                },
                new[]
                {
                    new VignetteCue(2.50f, CueKind.Stamp, "not", 0.7f),
                });
        }

        /// <summary>"Hear that? Three knocks, three bows." Knock, knock … knock; the owl bows in the same rhythm.</summary>
        private static Vignette BuildJuneResolve()
        {
            float[] knocks = { 0.3f, 0.7f, 1.6f };
            var owl = new List<VignetteKey> { K(0f, 0.68f, 0.52f, 1.0f, 0f, 1f) };
            var cues = new List<VignetteCue>();
            foreach (float k in knocks)
            {
                owl.Add(K(k, 0.68f, 0.52f, 1.0f, 0f, 1f));
                owl.Add(K(k + 0.15f, 0.68f, 0.50f, 1.0f, -25f, 1f, Ease.Out));
                owl.Add(K(k + 0.30f, 0.68f, 0.52f, 1.0f, 0f, 1f, Ease.InOut));
                cues.Add(new VignetteCue(k, CueKind.Knock, "door", 0.3f));
            }

            return new Vignette(JuneResolve, 2.8f, 0,
                Beats(0.3f, 1.0f, 1.6f, 2.4f),
                new[]
                {
                    Still("porch", "porch", 0.40f, 0.30f, 1.2f),
                    Still("door", "door_open", 0.30f, 0.52f, 1.0f),
                    Still("booth", "booth", 0.68f, 0.50f, 1.1f),
                    new VignetteTrack("owl", "pg.owl", owl.ToArray()),
                },
                cues.ToArray());
        }

        // ------------------------------------------------------------------------------------------- helpers

        private static VignetteBeat[] Beats(params float[] bounds)
        {
            var beats = new VignetteBeat[bounds.Length / 2];
            for (int i = 0; i < beats.Length; i++)
            {
                beats[i] = new VignetteBeat(bounds[2 * i], bounds[2 * i + 1]);
            }

            return beats;
        }

        private static VignetteTrack Still(string id, string part, float x, float y, float scale) =>
            new VignetteTrack(id, Part(part), new VignetteKey(0f, new PuppetPose(x, y, scale)));

        private static VignetteTrack Track(string id, string part, params VignetteKey[] keys) =>
            new VignetteTrack(id, Part(part), keys);

        private static VignetteKey K(float t, float x, float y, float scale, float rotation, float alpha,
            Ease ease = Ease.InOut, string part = null) =>
            new VignetteKey(t, new PuppetPose(x, y, scale, rotation, alpha), ease, part);

        private static string Part(string name) => name.StartsWith(PuppetParts.Prefix, StringComparison.Ordinal) ? name : PuppetParts.Id(name);
    }
}
