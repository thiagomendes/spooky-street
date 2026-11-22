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
        /// Explicit fallback (owner decision D23.3): no vignette yet, so the bubble keeps the v1 pictogram strip.
        /// </summary>
        public bool UsesV1Strip => VignetteId == null;
    }

    /// <summary>
    /// Case 01 vignette data (docs/nonverbal-ux.md §3, §5), staged per owner decision D23.3: the four key Bea/Vale
    /// lines, June's resolve line and the three notebook answer strips. Every <see cref="CaseScript"/> line key has a
    /// <see cref="LineVisual"/>; lines outside the stage fall back to the v1 strip. Coordinates are stage-normalized
    /// (see <see cref="PuppetPose"/>); parts marked as flipbooks (<see cref="PuppetParts.JuneRun"/>) cycle their
    /// frames in the view while the track moves.
    /// </summary>
    public static class C01Vignettes
    {
        public const string BeaSawCape = "vg.bea.saw_cape";
        public const string BeaDoorOpen = "vg.bea.door_open";
        public const string ValeDoorShut = "vg.vale.door_shut";
        public const string ValeWasShut = "vg.vale.was_shut";
        public const string JuneResolve = "vg.june.resolve";
        public const string NotebookEntry = "vg.notebook.q_entry";
        public const string NotebookSource = "vg.notebook.q_source";
        public const string NotebookMoved = "vg.notebook.q_moved";

        /// <summary>Speech vignettes loop until the player taps next; notebook strips play twice and stop (§5).</summary>
        public const int NotebookLoops = 2;

        /// <summary>The line keys that get a vignette now (owner decision D23.3).</summary>
        public static readonly IReadOnlyList<string> ScopedLineKeys = new[]
        {
            Lines.BeaSawCape, Lines.BeaDoorOpen, Lines.ValeDoorShut, Lines.ValeWasShut, Lines.JuneResolve,
        };

        public static readonly IReadOnlyList<Vignette> All = new[]
        {
            BuildBeaSawCape(), BuildBeaDoorOpen(), BuildValeDoorShut(), BuildValeWasShut(), BuildJuneResolve(),
            BuildNotebookEntry(), BuildNotebookSource(), BuildNotebookMoved(),
        };

        private static readonly Dictionary<string, Vignette> ById = Index();

        private static readonly Dictionary<string, string> LineToVignette = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { Lines.BeaSawCape, BeaSawCape },
            { Lines.BeaDoorOpen, BeaDoorOpen },
            { Lines.ValeDoorShut, ValeDoorShut },
            { Lines.ValeWasShut, ValeWasShut },
            { Lines.JuneResolve, JuneResolve },
        };

        private static readonly Dictionary<string, string> QuestionToStrip = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { CaseNotebook.QuestionEntry, NotebookEntry },
            { CaseNotebook.QuestionSource, NotebookSource },
            { CaseNotebook.QuestionMoved, NotebookMoved },
        };

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

        /// <summary>The animated answer strip of a notebook question (played on tap once answered).</summary>
        public static Vignette AnswerStripFor(string questionId) =>
            questionId != null && QuestionToStrip.TryGetValue(questionId, out string id) ? Get(id) : null;

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
        /// "I saw June's silver cape go in. Right there!" June runs across the bridge (one moving part), pops into
        /// the window (puff, the window lights with her inside), Bea's hand points and the key ring pulses.
        /// </summary>
        private static Vignette BuildBeaSawCape()
        {
            const float winX = 0.86f, winY = 0.52f;
            return new Vignette(BeaSawCape, 3.6f, 0,
                Beats(0.2f, 1.7f, 1.7f, 2.1f, 2.2f, 3.6f),
                new[]
                {
                    Still("treehouse", "treehouse", 0.10f, 0.58f, 1.0f),
                    Still("bridge", "bridge", 0.44f, 0.34f, 1.0f),
                    Still("wall", "house_side", 0.88f, 0.50f, 1.1f),
                    Still("window", "window", winX, winY, 0.8f),
                    Track("window_lit", "june_window",
                        K(1.95f, winX, winY, 0.8f, 0f, 0f),
                        K(2.10f, winX, winY, 0.8f, 0f, 1f, Ease.Out)),
                    Track("june", PuppetParts.JuneRun,
                        K(0.20f, 0.14f, 0.46f, 0.7f, 0f, 0f),
                        K(0.45f, 0.20f, 0.46f, 1.0f, 0f, 1f, Ease.Back),
                        K(1.70f, 0.74f, 0.42f, 1.0f, 0f, 1f, Ease.InOut),
                        K(1.95f, winX, winY, 0.45f, 0f, 0f, Ease.Out)),
                    Track("hand", "pointer",
                        K(2.20f, 0.55f, winY, 1.0f, 0f, 0f),
                        K(2.60f, 0.70f, winY, 1.0f, 0f, 1f, Ease.Back)),
                },
                new[]
                {
                    new VignetteCue(1.95f, CueKind.Puff, "window_lit", 0.4f),
                    new VignetteCue(2.20f, CueKind.Gesture, null, 0f, "Point"),
                    new VignetteCue(2.50f, CueKind.Ring, "window_lit", 1.1f),
                });
        }

        /// <summary>
        /// "The door's open now? I saw June's cape go in by the side, long before that." Inside the pencil-cloud
        /// memory frame, beats 1–2 of the saw-cape vignette replay; then Bea nods.
        /// </summary>
        private static Vignette BuildBeaDoorOpen()
        {
            const float winX = 0.80f, winY = 0.52f;
            return new Vignette(BeaDoorOpen, 3.4f, 0,
                Beats(0.2f, 1.6f, 1.6f, 2.0f, 2.2f, 3.2f),
                new[]
                {
                    Still("memory", "memory", 0.55f, 0.50f, 1.0f),
                    Still("treehouse", "treehouse", 0.22f, 0.56f, 0.8f),
                    Still("bridge", "bridge", 0.50f, 0.36f, 0.8f),
                    Still("window", "window", winX, winY, 0.65f),
                    Track("window_lit", "june_window",
                        K(1.60f, winX, winY, 0.65f, 0f, 0f),
                        K(1.80f, winX, winY, 0.65f, 0f, 1f, Ease.Out)),
                    Track("june", PuppetParts.JuneRun,
                        K(0.20f, 0.26f, 0.46f, 0.8f, 0f, 1f),
                        K(1.40f, 0.70f, 0.44f, 0.8f, 0f, 1f, Ease.InOut),
                        K(1.60f, winX, winY, 0.4f, 0f, 0f, Ease.Out)),
                    Track("bea", "bea",
                        K(0f, 0.07f, 0.50f, 0.9f, 0f, 1f),
                        K(2.20f, 0.07f, 0.50f, 0.9f, 0f, 1f),
                        K(2.45f, 0.07f, 0.47f, 0.9f, -10f, 1f, Ease.Out),
                        K(2.70f, 0.07f, 0.50f, 0.9f, 0f, 1f, Ease.InOut),
                        K(2.95f, 0.07f, 0.47f, 0.9f, -10f, 1f, Ease.Out),
                        K(3.20f, 0.07f, 0.50f, 0.9f, 0f, 1f, Ease.InOut)),
                },
                new[]
                {
                    new VignetteCue(1.60f, CueKind.Puff, "window_lit", 0.4f),
                });
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

        // ------------------------------------------------------------------------------------------- notebook

        private const float P1 = 0.17f, P2 = 0.50f, P3 = 0.83f, PY = 0.50f;

        /// <summary>How did June get inside? Treehouse → bridge → window: June runs panel to panel, the window lights.</summary>
        private static Vignette BuildNotebookEntry()
        {
            return new Vignette(NotebookEntry, 3.2f, NotebookLoops,
                Beats(0.3f, 1.9f, 1.9f, 2.2f, 2.4f, 2.9f),
                new[]
                {
                    Still("panel1", "treehouse", P1, PY, 0.8f),
                    Still("panel2", "bridge", P2, PY, 0.8f),
                    Still("panel3", "window", P3, PY, 0.8f),
                    Track("lit", "june_window",
                        K(1.90f, P3, PY, 0.8f, 0f, 0f),
                        K(2.10f, P3, PY, 0.8f, 0f, 1f, Ease.Out)),
                    Track("june", PuppetParts.JuneRun,
                        K(0.30f, P1 - 0.05f, PY - 0.1f, 0.6f, 0f, 1f),
                        K(1.75f, P3 - 0.03f, PY - 0.1f, 0.6f, 0f, 1f, Ease.InOut),
                        K(1.90f, P3, PY, 0.35f, 0f, 0f, Ease.Out)),
                    Check(2.40f),
                },
                new[]
                {
                    new VignetteCue(0.30f, CueKind.Ring, "panel1", 0.6f),
                    new VignetteCue(0.90f, CueKind.Ring, "panel2", 0.6f),
                    new VignetteCue(1.50f, CueKind.Ring, "panel3", 0.8f),
                    new VignetteCue(2.40f, CueKind.Stamp, "check", 0.5f),
                });
        }

        /// <summary>Where are the knocks coming from? The floor hums, the eye looks through the grille, the cam turns.</summary>
        private static Vignette BuildNotebookSource()
        {
            return new Vignette(NotebookSource, 3.2f, NotebookLoops,
                Beats(0.2f, 0.8f, 0.9f, 1.5f, 1.6f, 2.9f),
                new[]
                {
                    Still("panel1", "floor_hum", P1, PY, 0.8f),
                    Still("panel2", "grille", P2, PY, 0.8f),
                    Track("eye", "eye",
                        K(0.90f, P2 - 0.12f, PY + 0.05f, 0.4f, 0f, 0f),
                        K(1.50f, P2, PY + 0.05f, 0.5f, 0f, 1f, Ease.Out)),
                    Track("cam", "cam",
                        K(0f, P3, PY, 0.8f, 0f, 1f),
                        K(1.60f, P3, PY, 0.8f, 0f, 1f),
                        K(2.40f, P3, PY, 0.8f, -180f, 1f, Ease.InOut)),
                    Check(2.50f),
                },
                new[]
                {
                    new VignetteCue(0.20f, CueKind.Knock, "panel1", 0.6f),
                    new VignetteCue(0.80f, CueKind.Ring, "panel2", 0.8f),
                    new VignetteCue(1.60f, CueKind.Ring, "cam", 0.8f),
                    new VignetteCue(2.50f, CueKind.Stamp, "check", 0.4f),
                });
        }

        /// <summary>What did the knocking move? The crank turns, the arm connects, the owl bows.</summary>
        private static Vignette BuildNotebookMoved()
        {
            return new Vignette(NotebookMoved, 3.2f, NotebookLoops,
                Beats(0.2f, 1.0f, 1.0f, 1.5f, 1.5f, 2.9f),
                new[]
                {
                    Track("crank", "crank",
                        K(0f, P1, PY, 0.8f, 0f, 1f),
                        K(0.20f, P1, PY, 0.8f, 0f, 1f),
                        K(1.00f, P1, PY, 0.8f, -360f, 1f, Ease.InOut)),
                    Track("arm", "arm",
                        K(0f, P2 - 0.08f, PY, 0.8f, 0f, 1f),
                        K(1.00f, P2 - 0.08f, PY, 0.8f, 0f, 1f),
                        K(1.50f, P2, PY, 0.8f, 0f, 1f, Ease.Back)),
                    Track("owl", "owl",
                        K(0f, P3, PY, 0.8f, 0f, 1f),
                        K(1.50f, P3, PY, 0.8f, 0f, 1f),
                        K(1.80f, P3, PY - 0.03f, 0.8f, -25f, 1f, Ease.Out),
                        K(2.10f, P3, PY, 0.8f, 0f, 1f, Ease.InOut)),
                    Check(2.40f),
                },
                new[]
                {
                    new VignetteCue(0.20f, CueKind.Ring, "crank", 0.8f),
                    new VignetteCue(1.00f, CueKind.Ring, "arm", 0.5f),
                    new VignetteCue(1.50f, CueKind.Ring, "owl", 0.7f),
                    new VignetteCue(2.40f, CueKind.Stamp, "check", 0.5f),
                });
        }

        // ------------------------------------------------------------------------------------------- helpers

        private static VignetteTrack Check(float t) => Track("check", "check",
            K(t, 0.92f, 0.82f, 1.6f, -8f, 0f),
            K(t + 0.25f, 0.92f, 0.82f, 1.0f, -8f, 1f, Ease.Out));

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
