using System.Collections.Generic;

namespace SpookyStreet.Core.Nonverbal
{
    /// <summary>
    /// The street remembers case 01 in words (S003a, D29): each friend's changed greeting on a later visit gets its own
    /// vignette under the same rules as every case line (2–4 s, 1–3 beats, at most two moving parts, one idea per beat).
    /// Each shows only what the speaker has now (Bea's owl sticker, Vale watching the owl, June outside by the open door,
    /// Kit's memory of the owl's friends) and never how case 01 was solved.
    /// </summary>
    public static partial class C01Vignettes
    {
        public const string BeaRemembers = "vg.bea.remembers";
        public const string ValeRemembers = "vg.vale.remembers";
        public const string JuneRemembers = "vg.june.remembers";
        public const string KitRemembers = "vg.kit.remembers";

        private static Vignette[] BuildStreetVignettes() => new[]
        {
            BuildBeaRemembers(), BuildValeRemembers(), BuildJuneRemembers(), BuildKitRemembers(),
        };

        /// <summary>"Pip's back! See my owl? I'm your lookout now!": Pip hops up to Bea's bike, the owl sticker on it bows
        /// once, then an eye opens over the bike (the lookout).</summary>
        private static Vignette BuildBeaRemembers()
        {
            return new Vignette(BeaRemembers, 3.0f, 0,
                Beats(0.2f, 0.9f, 1.1f, 1.6f, 1.9f, 2.6f),
                new[]
                {
                    Still("bike", "bike", 0.56f, 0.40f, 1.5f),
                    Hop("pip", "pip", 0.2f, 0.7f, 0.10f, 0.40f, 0.22f, 0.40f, 0.9f),
                    Track("owl", "owl",
                        K(0f, 0.66f, 0.64f, 0.6f, 0f, 1f),
                        K(1.10f, 0.66f, 0.64f, 0.6f, 0f, 1f),
                        K(1.30f, 0.66f, 0.61f, 0.6f, -25f, 1f, Ease.Out),
                        K(1.60f, 0.66f, 0.64f, 0.6f, 0f, 1f, Ease.InOut)),
                    Track("eye", "eye",
                        K(1.90f, 0.56f, 0.82f, 0.3f, 0f, 0f, Ease.InOut, PuppetParts.EyeShut),
                        K(2.15f, 0.56f, 0.82f, 0.75f, 0f, 1f, Ease.Back, PuppetParts.EyeShut),
                        K(2.30f, 0.56f, 0.82f, 0.75f, 0f, 1f, Ease.Step, "pg.eye")),
                },
                new VignetteCue[0]);
        }

        /// <summary>"Evening, detective. I watch the owl now, not the door.": Vale's bench; an eye opens over it; up on
        /// the right the owl bows twice for him.</summary>
        private static Vignette BuildValeRemembers()
        {
            var owl = new List<VignetteKey> { K(0f, 0.72f, 0.58f, 1.2f, 0f, 1f) };
            foreach (float t in new[] { 1.2f, 1.9f })
            {
                owl.Add(K(t, 0.72f, 0.58f, 1.2f, 0f, 1f));
                owl.Add(K(t + 0.18f, 0.72f, 0.55f, 1.2f, -25f, 1f, Ease.Out));
                owl.Add(K(t + 0.42f, 0.72f, 0.58f, 1.2f, 0f, 1f, Ease.InOut));
            }

            return new Vignette(ValeRemembers, 3.0f, 0,
                Beats(0.2f, 0.8f, 1.2f, 2.4f),
                new[]
                {
                    Still("bench", "bench", 0.26f, 0.34f, 1.2f),
                    Track("eye", "eye",
                        K(0.20f, 0.30f, 0.70f, 0.3f, 0f, 0f, Ease.InOut, PuppetParts.EyeShut),
                        K(0.45f, 0.30f, 0.70f, 0.8f, 0f, 1f, Ease.Back, PuppetParts.EyeShut),
                        K(0.60f, 0.30f, 0.70f, 0.8f, 0f, 1f, Ease.Step, "pg.eye")),
                    new VignetteTrack("owl", Part("owl"), owl.ToArray()),
                },
                new VignetteCue[0]);
        }

        /// <summary>"Psst, Pip! The door opens for me now. Come play outside!": the door stands open; June hops out of it;
        /// a hand waves (come play).</summary>
        private static Vignette BuildJuneRemembers()
        {
            return new Vignette(JuneRemembers, 3.0f, 0,
                Beats(0.2f, 1.0f, 1.3f, 2.5f),
                new[]
                {
                    Still("door", "door_open", 0.30f, 0.46f, 1.3f),
                    Hop("june", "june", 0.2f, 0.8f, 0.30f, 0.42f, 0.62f, 0.40f, 1.1f),
                    Wave("come", 1.3f, 0.82f, 0.66f, 0.7f),
                },
                new VignetteCue[0]);
        }

        /// <summary>"Welcome back, detective! My old owl had friends once, long ago.": inside the pencil-cloud memory frame
        /// the owl bows once, then two "?" friends pop beside it (who they were stays a wonder).</summary>
        private static Vignette BuildKitRemembers()
        {
            return new Vignette(KitRemembers, 3.2f, 0,
                Beats(0.2f, 0.9f, 1.3f, 2.3f),
                new[]
                {
                    Still("memory", "memory", 0.50f, 0.50f, 1.0f),
                    Track("owl", "owl",
                        K(0f, 0.50f, 0.50f, 0.9f, 0f, 1f),
                        K(0.25f, 0.50f, 0.50f, 0.9f, 0f, 1f),
                        K(0.45f, 0.50f, 0.47f, 0.9f, -25f, 1f, Ease.Out),
                        K(0.75f, 0.50f, 0.50f, 0.9f, 0f, 1f, Ease.InOut)),
                    Pop("friend1", "question", 1.3f, 0.30f, 0.52f, 0.5f),
                    Pop("friend2", "question", 1.8f, 0.70f, 0.52f, 0.5f),
                },
                new VignetteCue[0]);
        }
    }
}
