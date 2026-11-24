namespace SpookyStreet.Core.Nonverbal
{
    /// <summary>
    /// The costume remarks (S004 §2.1, engagement §1.9): one friend notices a keepsake piece Pip wears, once ever. Each
    /// line gets its own vignette under the same rules as every case line (2–4 s, 1–3 beats, at most two moving parts,
    /// one idea per beat). They show only Pip, the piece's idea and the friend's delight, never anything of the case.
    /// </summary>
    public static partial class C01Vignettes
    {
        public const string JuneStarCape = "vg.june.star_cape";
        public const string ValeLanternPail = "vg.vale.lantern_pail";
        public const string KitShowmanHat = "vg.kit.showman_hat";

        private static Vignette[] BuildRemarkVignettes() => new[]
        {
            BuildJuneStarCape(), BuildValeLanternPail(), BuildKitShowmanHat(),
        };

        /// <summary>"Stars on your cape! You look like the night sky!": Pip stands still; two stars pop beside her
        /// cape, then the moon rises over her (the night sky).</summary>
        private static Vignette BuildJuneStarCape()
        {
            return new Vignette(JuneStarCape, 3.0f, 0,
                Beats(0.2f, 1.3f, 1.5f, 2.6f),
                new[]
                {
                    Still("pip", "pip", 0.50f, 0.42f, 1.3f),
                    Pop("star1", "star", 0.3f, 0.30f, 0.50f, 0.45f),
                    Pop("star2", "star", 0.8f, 0.70f, 0.56f, 0.45f),
                    Track("moon", "moon",
                        K(1.5f, 0.50f, 0.70f, 0.55f, 0f, 0f),
                        K(1.9f, 0.50f, 0.82f, 0.55f, 0f, 1f, Ease.Out)),
                },
                new VignetteCue[0]);
        }

        /// <summary>"A lantern pail. Very proper for the evening.": Vale on his bench, Pip beside him; a lantern pops in
        /// by her hand, then a heart over them (he likes it).</summary>
        private static Vignette BuildValeLanternPail()
        {
            return new Vignette(ValeLanternPail, 3.0f, 0,
                Beats(0.2f, 1.2f, 1.5f, 2.5f),
                new[]
                {
                    Still("bench", "bench", 0.28f, 0.36f, 1.1f),
                    Still("pip", "pip", 0.72f, 0.42f, 1.1f),
                    Pop("lamp", "lamp", 0.4f, 0.88f, 0.36f, 0.5f),
                    Pop("ok", "heart", 1.5f, 0.50f, 0.78f, 0.55f),
                },
                new VignetteCue[0]);
        }

        /// <summary>"A showman's hat! You'd fit right in.": Kit and Pip; a star pops over Pip's head (the hat), then
        /// the puppet booth pops between them (the show she would fit in).</summary>
        private static Vignette BuildKitShowmanHat()
        {
            return new Vignette(KitShowmanHat, 3.0f, 0,
                Beats(0.2f, 1.2f, 1.5f, 2.6f),
                new[]
                {
                    Still("kit", "kit", 0.24f, 0.42f, 1.1f),
                    Still("pip", "pip", 0.76f, 0.42f, 1.1f),
                    Pop("star", "star", 0.4f, 0.76f, 0.78f, 0.45f),
                    Pop("booth", "booth", 1.5f, 0.50f, 0.66f, 0.7f),
                },
                new VignetteCue[0]);
        }
    }
}
