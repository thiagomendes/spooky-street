using System.Collections.Generic;
using Lines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;

namespace SpookyStreet.Core.Nonverbal
{
    /// <summary>
    /// The rest of case 01 (S002, closing D23.3's staging): every caption, examine and dialogue line that had only the
    /// v1 card now has a short vignette, so pre-readers get each line as pictures while the text line sits under the
    /// stage (one hybrid reading mode, D23.1). Same rules as the key lines: 2–4 s, 1–3 beats, at most two moving parts,
    /// one idea per beat; clue lines show the clue itself (the silver cloth, the sill snag, the foil streamer). Lines
    /// that say the same thing share a vignette (June's two greetings; the owl-bow caption reuses June's resolve).
    /// The mapping table is in docs/nonverbal-ux.md §3.
    /// </summary>
    public static partial class C01Vignettes
    {
        // Captions (the caption strip's mini stage).
        public const string CaptionKnocks = "vg.caption.knocks";
        public const string CaptionFoyerHum = "vg.caption.foyer_hum";
        public const string CaptionGrille = "vg.caption.grille";
        public const string CaptionArmLinked = "vg.caption.arm_linked";
        public const string CaptionCrankGap = "vg.caption.crank_gap";

        // Pip's examine lines.
        public const string PipStreamer = "vg.pip.streamer";
        public const string PipCloth = "vg.pip.cloth";
        public const string PipSill = "vg.pip.sill";
        public const string PipDoorStuck = "vg.pip.door_stuck";
        public const string PipBowl = "vg.pip.bowl";
        public const string PipBowlSweet = "vg.pip.bowl_sweet";
        public const string PipOwlPanel = "vg.pip.owl_panel";

        // Bea, Mr. Vale, June, Aunt Kit.
        public const string BeaRepeat = "vg.bea.repeat";
        public const string BeaClosed = "vg.bea.closed";
        public const string ValeInviteSit = "vg.vale.invite_sit";
        public const string ValeRepeat = "vg.vale.repeat";
        public const string ValeOpenNow = "vg.vale.open_now";
        public const string ValeClosed = "vg.vale.closed";
        public const string ValeSavedSweet = "vg.vale.saved_sweet";
        public const string JuneGreet = "vg.june.greet";
        public const string JuneDoorSticks = "vg.june.door_sticks";
        public const string JuneDoorAlreadyOpen = "vg.june.door_already_open";
        public const string JuneLatchReminder = "vg.june.latch_reminder";
        public const string JuneLatchThanks = "vg.june.latch_thanks";
        public const string JuneListenFloor = "vg.june.listen_floor";
        public const string JuneGoOutside = "vg.june.go_outside";
        public const string JuneClosed = "vg.june.closed";
        public const string KitFirstShow = "vg.kit.first_show";
        public const string KitThanks = "vg.kit.thanks";
        public const string KitAsk = "vg.kit.ask";

        // Properties, not fields: static initializers in different partial files run in no defined order, and the
        // tables in C01Vignettes.cs are built from these during type initialization.

        /// <summary>Knock, knock … knock (the owl machine's rhythm).</summary>
        private static float[] KnockRhythm => new[] { 0.3f, 0.7f, 1.6f };

        private static Vignette[] BuildLineVignettes() => new[]
        {
            BuildCaptionKnocks(), BuildCaptionFoyerHum(), BuildCaptionGrille(), BuildCaptionArmLinked(),
            BuildCaptionCrankGap(),
            BuildPipStreamer(), BuildClueMatch(PipCloth, "cloth", 1.3f), BuildClueMatch(PipSill, PuppetParts.SillSnag, 1.3f),
            BuildPipDoorStuck(), BuildPipBowl(), BuildPipBowlSweet(),
            BuildBeaRepeat(), BuildBeaClosed(),
            BuildValeInviteSit(), BuildValeRepeat(), BuildDoorOpenWow(ValeOpenNow, false), BuildValeClosed(),
            BuildValeSavedSweet(),
            BuildJuneGreet(), BuildJuneDoorSticks(), BuildDoorOpenWow(JuneDoorAlreadyOpen, true), BuildJuneLatchReminder(),
            BuildJuneLatchThanks(), BuildJuneListenFloor(), BuildJuneGoOutside(), BuildJuneClosed(),
            BuildKitFirstShow(), BuildKitThanks(), BuildPipOwlPanel(), BuildKitAsk(),
        };

        /// <summary>Line key → vignette for every line outside the original key five.</summary>
        private static (string Line, string Vignette)[] LineVignettes() => new[]
        {
            (Lines.CaptionArrivalKnocks, CaptionKnocks),
            (Lines.CaptionFoyerHum, CaptionFoyerHum),
            (Lines.CaptionGrille, CaptionGrille),
            (Lines.CaptionArmLinked, CaptionArmLinked),
            (Lines.CaptionCrankGap, CaptionCrankGap),
            (Lines.CaptionOwlBows, JuneResolve), // "Three knocks. Outside, the owl bows three times." = the resolve
            (Lines.ExamineStreamer, PipStreamer),
            (Lines.ExamineCloth, PipCloth),
            (Lines.ExamineSill, PipSill),
            (Lines.ExamineDoorStuck, PipDoorStuck),
            (Lines.ExamineBowl, PipBowl),
            (Lines.ExamineBowlSweet, PipBowlSweet),
            (Lines.BeaRepeat, BeaRepeat),
            (Lines.BeaClosed, BeaClosed),
            (Lines.ValeInviteSit, ValeInviteSit),
            (Lines.ValeRepeat, ValeRepeat),
            (Lines.ValeOpenNow, ValeOpenNow),
            (Lines.ValeClosed, ValeClosed),
            (Lines.ValeSavedSweet, ValeSavedSweet),
            (Lines.JuneGreet, JuneGreet),
            (Lines.JuneGreetShortcut, JuneGreet), // the same hello, only longer
            (Lines.JuneDoorSticks, JuneDoorSticks),
            (Lines.JuneDoorAlreadyOpen, JuneDoorAlreadyOpen),
            (Lines.JuneLatchReminder, JuneLatchReminder),
            (Lines.JuneLatchThanks, JuneLatchThanks),
            (Lines.JuneListenFloor, JuneListenFloor),
            (Lines.JuneGoOutside, JuneGoOutside),
            (Lines.JuneClosed, JuneClosed),
            (Lines.KitFirstShow, KitFirstShow),
            (Lines.KitThanks, KitThanks),
            (Lines.ExamineOwlPanel, PipOwlPanel),
            (Lines.KitAsk, KitAsk),
        };

        // ------------------------------------------------------------------------------------------- captions

        /// <summary>"[Three knocks from the blue house]": the house, knock rings in the knock, knock … knock rhythm.</summary>
        private static Vignette BuildCaptionKnocks()
        {
            var cues = new List<VignetteCue>();
            foreach (float k in KnockRhythm)
            {
                cues.Add(new VignetteCue(k, CueKind.Knock, "house", 0.35f));
            }

            return new Vignette(CaptionKnocks, 2.8f, 0,
                Beats(0.3f, 1.05f, 1.6f, 1.95f),
                new[] { Still("house", "house", 0.48f, 0.46f, 1.45f) },
                cues.ToArray());
        }

        /// <summary>"[Knock, knock… knock. The floorboards tremble.]": the floor planks jump on each knock (a perception).</summary>
        private static Vignette BuildCaptionFoyerHum()
        {
            var floor = Shiver(0.46f, 0.42f, 1.5f, KnockRhythm);
            var cues = new List<VignetteCue>();
            foreach (float k in KnockRhythm)
            {
                cues.Add(new VignetteCue(k, CueKind.Knock, "floor", 0.35f));
            }

            return new Vignette(CaptionFoyerHum, 2.8f, 0,
                Beats(0.3f, 1.05f, 1.6f, 1.95f),
                new[] { new VignetteTrack("floor", Part("floor_hum"), floor) },
                cues.ToArray());
        }

        /// <summary>"[A wooden piece lifts three times behind the owl panel.]": a wooden piece peeks up over the grille
        /// three times; then an eye opens beside it (look here).</summary>
        private static Vignette BuildCaptionGrille()
        {
            var piece = new List<VignetteKey> { K(0f, 0.56f, 0.60f, 0.8f, 0f, 1f) };
            foreach (float t in new[] { 0.3f, 0.9f, 1.5f })
            {
                piece.Add(K(t, 0.56f, 0.60f, 0.8f, 0f, 1f));
                piece.Add(K(t + 0.2f, 0.56f, 0.90f, 0.8f, 0f, 1f, Ease.Out));
                piece.Add(K(t + 0.4f, 0.56f, 0.60f, 0.8f, 0f, 1f, Ease.InOut));
            }

            return new Vignette(CaptionGrille, 3.0f, 0,
                Beats(0.3f, 1.9f, 2.1f, 2.5f),
                new[]
                {
                    new VignetteTrack("piece", PuppetParts.LatchBar, piece.ToArray()), // behind the panel
                    Still("grille", "grille", 0.56f, 0.40f, 1.3f),
                    Pop("eye", "eye", 2.1f, 0.24f, 0.50f, 1.0f),
                },
                new VignetteCue[0]);
        }

        /// <summary>"[Click!]": the arm swings onto the cam, a click puff. No check stamp (D26).</summary>
        private static Vignette BuildCaptionArmLinked()
        {
            return new Vignette(CaptionArmLinked, 2.4f, 0,
                Beats(0.4f, 1.0f, 1.0f, 1.4f),
                new[]
                {
                    Still("cam", "cam", 0.36f, 0.48f, 1.3f),
                    Track("arm", "arm",
                        K(0f, 0.68f, 0.24f, 1.2f, -35f, 1f),
                        K(0.40f, 0.68f, 0.24f, 1.2f, -35f, 1f),
                        K(1.00f, 0.58f, 0.50f, 1.2f, 0f, 1f, Ease.Back)),
                },
                new[] { new VignetteCue(1.00f, CueKind.Puff, "arm", 0.4f) });
        }

        /// <summary>"[Tap. The striped arm swings in the air.]": the crank turns once, one tap; the loose arm dangles
        /// and swings. No ring on the gap, no ⊘ on the owl (D26: what is seen, not what it means).</summary>
        private static Vignette BuildCaptionCrankGap()
        {
            return new Vignette(CaptionCrankGap, 3.0f, 0,
                Beats(0.2f, 1.2f, 1.3f, 2.8f),
                new[]
                {
                    Track("crank", "crank",
                        K(0f, 0.28f, 0.48f, 1.2f, 0f, 1f),
                        K(0.20f, 0.28f, 0.48f, 1.2f, 0f, 1f),
                        K(1.20f, 0.28f, 0.48f, 1.2f, -360f, 1f, Ease.InOut)),
                    Track("arm", "arm",
                        K(0f, 0.70f, 0.44f, 1.2f, -10f, 1f),
                        K(1.30f, 0.70f, 0.44f, 1.2f, -10f, 1f),
                        K(1.60f, 0.70f, 0.44f, 1.2f, 18f, 1f, Ease.Out),
                        K(1.95f, 0.70f, 0.44f, 1.2f, -14f, 1f, Ease.InOut),
                        K(2.30f, 0.70f, 0.44f, 1.2f, 9f, 1f, Ease.InOut),
                        K(2.70f, 0.70f, 0.44f, 1.2f, -10f, 1f, Ease.InOut)),
                },
                new[] { new VignetteCue(1.20f, CueKind.Knock, "crank", 0.3f) });
        }

        // ------------------------------------------------------------------------------------------- Pip examines

        /// <summary>"Crinkly, shiny foil.": the lavender foil swishes and twinkles. No cape beside it, no ⊘: comparing
        /// it with the silver cloth is the child's job (D26, the honest decoy).</summary>
        private static Vignette BuildPipStreamer()
        {
            return new Vignette(PipStreamer, 2.6f, 0,
                Beats(0.2f, 1.4f, 1.6f, 2.2f),
                new[]
                {
                    Track("streamer", "streamer",
                        K(0f, 0.46f, 0.50f, 1.5f, 0f, 1f),
                        K(0.20f, 0.46f, 0.50f, 1.5f, 0f, 1f),
                        K(0.50f, 0.46f, 0.53f, 1.5f, 9f, 1f),
                        K(0.80f, 0.46f, 0.47f, 1.5f, -7f, 1f),
                        K(1.10f, 0.46f, 0.53f, 1.5f, 6f, 1f),
                        K(1.40f, 0.46f, 0.50f, 1.5f, 0f, 1f)),
                    Track("shine", PuppetParts.Sparkle,
                        K(0.20f, 0.64f, 0.78f, 0.25f, 0f, 0f),
                        K(0.55f, 0.64f, 0.78f, 0.6f, 0f, 1f, Ease.Back),
                        K(1.00f, 0.64f, 0.78f, 0.42f, 30f, 1f),
                        K(1.60f, 0.64f, 0.78f, 0.6f, 0f, 1f),
                        K(2.20f, 0.64f, 0.78f, 0.45f, 30f, 1f)),
                },
                new VignetteCue[0]);
        }

        /// <summary>
        /// "Soft silver cloth, caught on a splinter." / "A silver thread on the sill.": the clue pops up and flutters in
        /// the draught, glinting twice. No ring (nothing says "this matters") and no cape beside it: matching it is the
        /// child's deduction (D26).
        /// </summary>
        private static Vignette BuildClueMatch(string id, string clue, float scale)
        {
            return new Vignette(id, 2.8f, 0,
                Beats(0.1f, 0.45f, 0.6f, 2.4f),
                new[]
                {
                    Track("clue", clue,
                        K(0.10f, 0.46f, 0.50f, scale * 0.3f, 0f, 0f),
                        K(0.40f, 0.46f, 0.50f, scale, 0f, 1f, Ease.Back),
                        K(0.80f, 0.46f, 0.52f, scale, 8f, 1f, Ease.InOut),
                        K(1.20f, 0.46f, 0.49f, scale, -6f, 1f, Ease.InOut),
                        K(1.60f, 0.46f, 0.52f, scale, 7f, 1f, Ease.InOut),
                        K(2.00f, 0.46f, 0.50f, scale, 0f, 1f, Ease.InOut)),
                    Track("glint", PuppetParts.Sparkle,
                        K(0.60f, 0.62f, 0.66f, 0.2f, 0f, 0f),
                        K(0.80f, 0.62f, 0.66f, 0.5f, 0f, 1f, Ease.Out),
                        K(1.10f, 0.62f, 0.66f, 0.2f, 45f, 0f, Ease.InOut),
                        K(1.70f, 0.30f, 0.40f, 0.2f, 0f, 0f),
                        K(1.90f, 0.30f, 0.40f, 0.45f, 0f, 1f, Ease.Out),
                        K(2.20f, 0.30f, 0.40f, 0.2f, 45f, 0f, Ease.InOut)),
                },
                new VignetteCue[0]);
        }

        /// <summary>"It won't budge.": the door rattles twice but does not open. No padlock, no ring (D26).</summary>
        private static Vignette BuildPipDoorStuck()
        {
            var first = Rattle(0.46f, 0.48f, 1.4f, 0.2f, 1.1f);
            var keys = new List<VignetteKey>(first);
            var second = Rattle(0.46f, 0.48f, 1.4f, 1.6f, 2.5f);
            for (int i = 2; i < second.Length; i++)
            {
                keys.Add(second[i]);
            }

            return new Vignette(PipDoorStuck, 2.8f, 0,
                Beats(0.2f, 1.1f, 1.6f, 2.5f),
                new[] { new VignetteTrack("door", Part("door_shut"), keys.ToArray()) },
                new VignetteCue[0]);
        }

        /// <summary>"A bowl piled high with candy.": candies drop in one by one, then a sparkle.</summary>
        private static Vignette BuildPipBowl()
        {
            return new Vignette(PipBowl, 2.8f, 0,
                Beats(0.2f, 1.6f, 1.9f, 2.3f),
                new[]
                {
                    Drop("candy1", "candy", 0.2f, 0.41f, 0.64f, 0.6f, -15f),
                    Drop("candy2", "candy", 0.7f, 0.59f, 0.64f, 0.6f, 12f),
                    Drop("candy3", "candy", 1.2f, 0.50f, 0.78f, 0.6f, 0f),
                    Still("bowl", "bowl", 0.50f, 0.38f, 1.6f),
                    Pop("shine", PuppetParts.Sparkle, 1.9f, 0.76f, 0.76f, 0.55f),
                },
                new VignetteCue[0]);
        }

        /// <summary>"One sweet, saved in a tiny knight helmet.": the helmet sweet hops out of the bowl; a little puff.</summary>
        private static Vignette BuildPipBowlSweet()
        {
            return new Vignette(PipBowlSweet, 2.8f, 0,
                Beats(0.3f, 0.9f, 1.2f, 2.2f),
                new[]
                {
                    Still("bowl", "bowl", 0.28f, 0.40f, 1.1f),
                    Track("sweet", "sweet",
                        K(0.30f, 0.28f, 0.50f, 0.6f, 0f, 0f),
                        K(0.60f, 0.45f, 0.80f, 1.1f, 10f, 1f, Ease.Out),
                        K(0.90f, 0.62f, 0.48f, 1.2f, 0f, 1f, Ease.Back)),
                },
                new[] { new VignetteCue(1.20f, CueKind.Puff, "sweet", 0.4f) });
        }

        // ------------------------------------------------------------------------------------------- Bea

        /// <summary>"Up high, over the hedge. Then it was gone!": the silver glint skims above the hedge top and
        /// fades. No cape, no feet, no ⊘ (D26: only the flash she saw).</summary>
        private static Vignette BuildBeaRepeat()
        {
            return new Vignette(BeaRepeat, 3.0f, 0,
                Beats(0.2f, 1.8f, 2.0f, 2.6f),
                new[]
                {
                    Track("flash", PuppetParts.Sparkle,
                        K(0.20f, 0.18f, 0.66f, 0.3f, 0f, 0f),
                        K(0.40f, 0.22f, 0.66f, 0.55f, 0f, 1f, Ease.Out),
                        K(1.50f, 0.70f, 0.70f, 0.45f, 120f, 1f, Ease.InOut),
                        K(1.80f, 0.78f, 0.71f, 0.2f, 160f, 0f, Ease.InOut)),
                    Still("hedge", "hedge", 0.28f, 0.32f, 1.6f),
                    Still("hedge2", "hedge", 0.56f, 0.32f, 1.6f),
                    Still("hedge3", "hedge", 0.84f, 0.32f, 1.6f),
                    Pop("what", "question", 2.0f, 0.86f, 0.70f, 0.6f),
                },
                new VignetteCue[0]);
        }

        /// <summary>"Three knocks, three bows! I'm putting an owl on my bike.": a little owl hops onto the bike, a
        /// heart pops, the owl bows once.</summary>
        private static Vignette BuildBeaClosed()
        {
            return new Vignette(BeaClosed, 3.0f, 0,
                Beats(0.3f, 1.1f, 1.5f, 1.8f, 2.1f, 2.7f),
                new[]
                {
                    Still("bike", "bike", 0.52f, 0.40f, 1.6f),
                    Track("owl", "owl",
                        K(0f, 0.16f, 0.40f, 0.75f, 0f, 1f),
                        K(0.30f, 0.16f, 0.40f, 0.75f, 0f, 1f),
                        K(0.70f, 0.40f, 0.92f, 0.75f, 0f, 1f, Ease.Out),
                        K(1.10f, 0.64f, 0.72f, 0.75f, 0f, 1f, Ease.InOut),
                        K(2.10f, 0.64f, 0.72f, 0.75f, 0f, 1f),
                        K(2.40f, 0.64f, 0.70f, 0.75f, -25f, 1f, Ease.Out),
                        K(2.70f, 0.64f, 0.72f, 0.75f, 0f, 1f, Ease.InOut)),
                    Pop("heart", "heart", 1.5f, 0.86f, 0.74f, 0.6f),
                },
                new VignetteCue[0]);
        }

        // ------------------------------------------------------------------------------------------- Mr. Vale

        /// <summary>"Sit down and see for yourself.": Pip hops onto the bench; an eye opens above it.</summary>
        private static Vignette BuildValeInviteSit()
        {
            return new Vignette(ValeInviteSit, 2.8f, 0,
                Beats(0.3f, 1.0f, 1.3f, 1.75f),
                new[]
                {
                    Still("bench", "bench", 0.46f, 0.36f, 1.5f),
                    Hop("pip", "pip", 0.3f, 0.7f, 0.18f, 0.40f, 0.46f, 0.62f, 0.85f),
                    Track("eye", "eye",
                        K(1.30f, 0.80f, 0.72f, 0.3f, 0f, 0f, Ease.InOut, PuppetParts.EyeShut),
                        K(1.55f, 0.80f, 0.72f, 0.95f, 0f, 1f, Ease.Back, PuppetParts.EyeShut),
                        K(1.70f, 0.80f, 0.72f, 0.95f, 0f, 1f, Ease.Step, "pg.eye")),
                },
                new VignetteCue[0]);
        }

        /// <summary>"Not one creak from that door all evening.": the door stays still while the moon crosses the sky
        /// (all evening); then ⊘ thumps onto the door (never opened), the same grammar as his first line.</summary>
        private static Vignette BuildValeRepeat()
        {
            return new Vignette(ValeRepeat, 3.2f, 0,
                Beats(0.2f, 2.0f, 2.2f, 3.0f),
                new[]
                {
                    Still("door", "door_shut", 0.44f, 0.46f, 1.4f),
                    Track("moon", "moon",
                        K(0.20f, 0.16f, 0.78f, 0.7f, 0f, 1f),
                        K(2.00f, 0.86f, 0.80f, 0.7f, 0f, 1f, Ease.InOut)),
                    Stamp("not", 2.2f, 0.56f, 0.40f, 0.95f),
                },
                new[] { new VignetteCue(2.20f, CueKind.Stamp, "not", 0.8f) });
        }

        /// <summary>
        /// "It's open now, though. Well, well." / June: "And you got the front door open already? It always sticks for
        /// me.": the shut door swings open with a puff, then a "wow" pops. June's version starts with Pip beside the
        /// door (you opened it) and ends with a sparkle for him.
        /// </summary>
        private static Vignette BuildDoorOpenWow(string id, bool praisePip)
        {
            var tracks = new List<VignetteTrack>
            {
                Track("door", "door_shut",
                    K(0f, 0.46f, 0.46f, 1.4f, 0f, 1f),
                    K(0.40f, 0.46f, 0.46f, 1.4f, 0f, 1f),
                    K(0.45f, 0.46f, 0.46f, 1.4f, 0f, 1f, Ease.Step, "pg.door_open")),
                Pop("wow", "emo_wow", 1.2f, 0.74f, 0.62f, 1.0f),
            };
            if (praisePip)
            {
                tracks.Insert(0, Still("pip", "pip", 0.20f, 0.44f, 1.0f));
                tracks.Add(Pop("shine", PuppetParts.Sparkle, 2.0f, 0.26f, 0.82f, 0.5f));
            }

            return new Vignette(id, praisePip ? 2.8f : 2.4f, 0,
                praisePip ? Beats(0.4f, 0.85f, 1.2f, 1.5f, 2.0f, 2.3f) : Beats(0.4f, 0.85f, 1.2f, 1.5f),
                tracks.ToArray(),
                new[] { new VignetteCue(0.45f, CueKind.Puff, "door", 0.4f) });
        }

        /// <summary>"Three bows! From my bench I'd never have guessed.": the owl bows three times; "wow" pops by the bench.</summary>
        private static Vignette BuildValeClosed()
        {
            var owl = new List<VignetteKey> { K(0f, 0.62f, 0.50f, 1.35f, 0f, 1f) };
            foreach (float t in new[] { 0.2f, 0.7f, 1.2f })
            {
                owl.Add(K(t, 0.62f, 0.50f, 1.35f, 0f, 1f));
                owl.Add(K(t + 0.15f, 0.62f, 0.47f, 1.35f, -25f, 1f, Ease.Out));
                owl.Add(K(t + 0.35f, 0.62f, 0.50f, 1.35f, 0f, 1f, Ease.InOut));
            }

            return new Vignette(ValeClosed, 3.0f, 0,
                Beats(0.2f, 1.55f, 1.9f, 2.2f),
                new[]
                {
                    Still("bench", "bench", 0.22f, 0.36f, 1.1f),
                    new VignetteTrack("owl", Part("owl"), owl.ToArray()),
                    Pop("wow", "emo_wow", 1.9f, 0.28f, 0.76f, 0.8f),
                },
                new VignetteCue[0]);
        }

        /// <summary>"Saved that one for you, detective.": the helmet sweet slides over to Pip; a heart pops.</summary>
        private static Vignette BuildValeSavedSweet()
        {
            return new Vignette(ValeSavedSweet, 2.8f, 0,
                Beats(0.3f, 1.3f, 1.6f, 1.9f),
                new[]
                {
                    Still("pip", "pip", 0.78f, 0.46f, 1.1f),
                    Track("sweet", "sweet",
                        K(0f, 0.22f, 0.44f, 1.1f, 0f, 1f),
                        K(0.30f, 0.22f, 0.44f, 1.1f, 0f, 1f),
                        K(1.30f, 0.56f, 0.40f, 1.1f, 0f, 1f, Ease.Back)),
                    Pop("heart", "heart", 1.6f, 0.66f, 0.80f, 0.6f),
                },
                new VignetteCue[0]);
        }

        // ------------------------------------------------------------------------------------------- June

        /// <summary>
        /// "Pip! You found my secret way in! Did you hear the knocks too?" (and the shortcut version): a waving hand
        /// says hello beside June; then the house knocks, knock … knock.
        /// </summary>
        private static Vignette BuildJuneGreet()
        {
            return new Vignette(JuneGreet, 3.0f, 0,
                Beats(0.1f, 1.3f, 1.5f, 2.4f),
                new[]
                {
                    Still("june", "june", 0.22f, 0.46f, 1.15f),
                    Wave("hand", 0.1f, 0.43f, 0.52f, 1.0f),
                    Still("house", "house", 0.76f, 0.46f, 1.3f),
                },
                new[]
                {
                    new VignetteCue(1.50f, CueKind.Knock, "house", 0.3f),
                    new VignetteCue(1.85f, CueKind.Knock, "house", 0.3f),
                    new VignetteCue(2.40f, CueKind.Knock, "house", 0.3f),
                });
        }

        /// <summary>"It sticks. Give me a hand?": the door rattles shut; two hands pop up beside it.</summary>
        private static Vignette BuildJuneDoorSticks()
        {
            return new Vignette(JuneDoorSticks, 3.0f, 0,
                Beats(0.2f, 1.2f, 1.5f, 2.4f),
                new[]
                {
                    new VignetteTrack("door", Part("door_shut"), Rattle(0.40f, 0.48f, 1.4f, 0.2f, 1.2f)),
                    Track("hands", "hands",
                        K(1.50f, 0.70f, 0.40f, 0.35f, 0f, 0f),
                        K(1.80f, 0.70f, 0.46f, 1.1f, 0f, 1f, Ease.Back),
                        K(2.10f, 0.70f, 0.50f, 1.1f, 0f, 1f, Ease.InOut),
                        K(2.40f, 0.70f, 0.46f, 1.1f, 0f, 1f, Ease.InOut)),
                },
                new VignetteCue[0]);
        }

        /// <summary>"The latch is down by the front door. Lift it with me?": two hands lift the latch; the door swings
        /// open. No ring (D26).</summary>
        private static Vignette BuildJuneLatchReminder()
        {
            return new Vignette(JuneLatchReminder, 3.4f, 0,
                Beats(0.2f, 1.0f, 1.2f, 2.1f, 2.3f, 2.8f),
                new[]
                {
                    Track("door", "door_shut",
                        K(0f, 0.40f, 0.46f, 1.45f, 0f, 1f),
                        K(2.30f, 0.40f, 0.46f, 1.45f, 0f, 1f),
                        K(2.35f, 0.40f, 0.46f, 1.45f, 0f, 1f, Ease.Step, "pg.door_open")),
                    Track("latch", PuppetParts.LatchBar,
                        K(0f, 0.40f, 0.50f, 1.15f, 0f, 1f),
                        K(1.60f, 0.40f, 0.50f, 1.15f, 0f, 1f),
                        K(2.10f, 0.40f, 0.74f, 1.15f, 0f, 1f, Ease.Out)),
                    Track("hands", "hands",
                        K(1.20f, 0.40f, 0.10f, 0.9f, 0f, 0f),
                        K(1.60f, 0.40f, 0.36f, 0.9f, 0f, 1f, Ease.Out),
                        K(2.10f, 0.40f, 0.60f, 0.9f, 0f, 1f, Ease.Out)),
                },
                new[]
                {
                    new VignetteCue(2.35f, CueKind.Puff, "door", 0.45f),
                });
        }

        /// <summary>"There! It just needed two hands.": the door pops open; the two hands appear; a heart.</summary>
        private static Vignette BuildJuneLatchThanks()
        {
            return new Vignette(JuneLatchThanks, 2.8f, 0,
                Beats(0.2f, 0.65f, 0.9f, 1.2f, 1.6f, 1.9f),
                new[]
                {
                    Track("door", "door_shut",
                        K(0f, 0.32f, 0.46f, 1.4f, 0f, 1f),
                        K(0.20f, 0.32f, 0.46f, 1.4f, 0f, 1f),
                        K(0.25f, 0.32f, 0.46f, 1.4f, 0f, 1f, Ease.Step, "pg.door_open")),
                    Pop("hands", "hands", 0.9f, 0.66f, 0.44f, 1.1f),
                    Pop("heart", "heart", 1.6f, 0.84f, 0.78f, 0.6f),
                },
                new[] { new VignetteCue(0.25f, CueKind.Puff, "door", 0.4f) });
        }

        /// <summary>"I came down to find the knocks. Listen…": an ear pops up, then knock rings in the knock, knock …
        /// knock rhythm. No jumping floorboards: where the knocks come from is the child's question (D26).</summary>
        private static Vignette BuildJuneListenFloor()
        {
            var cues = new List<VignetteCue>();
            foreach (float k in KnockRhythm)
            {
                cues.Add(new VignetteCue(k + 0.6f, CueKind.Knock, "ear", 0.35f));
            }

            return new Vignette(JuneListenFloor, 3.0f, 0,
                Beats(0.1f, 0.5f, 0.9f, 2.6f),
                new[]
                {
                    Still("june", "june", 0.26f, 0.46f, 1.1f),
                    Pop("ear", "ear", 0.1f, 0.64f, 0.54f, 1.05f),
                },
                cues.ToArray());
        }

        /// <summary>"Come on, let's watch from the porch!": June runs from the open door to the porch; an eye opens.</summary>
        private static Vignette BuildJuneGoOutside()
        {
            return new Vignette(JuneGoOutside, 3.0f, 0,
                Beats(0.2f, 1.5f, 1.8f, 2.4f),
                new[]
                {
                    Still("door", "door_open", 0.18f, 0.48f, 1.3f),
                    Still("porch", "porch", 0.70f, 0.34f, 1.35f),
                    Track("june", PuppetParts.JuneRun,
                        K(0.20f, 0.24f, 0.42f, 1.0f, 0f, 0f),
                        K(0.40f, 0.29f, 0.42f, 1.0f, 0f, 1f, Ease.Out),
                        K(1.50f, 0.64f, 0.60f, 1.0f, 0f, 1f, Ease.InOut)),
                    Pop("eye", "eye", 1.8f, 0.89f, 0.78f, 0.75f),
                },
                new VignetteCue[0]);
        }

        /// <summary>"Knock, knock… knock. I'll be tapping that all night!": knock rings come off the door and June
        /// bobs along in the same rhythm; then the moon rises (all night).</summary>
        private static Vignette BuildJuneClosed()
        {
            var bob = new List<VignetteKey> { K(0f, 0.26f, 0.46f, 1.2f, 0f, 1f) };
            var cues = new List<VignetteCue>();
            foreach (float k in KnockRhythm)
            {
                bob.Add(K(k, 0.26f, 0.46f, 1.2f, 0f, 1f));
                bob.Add(K(k + 0.12f, 0.26f, 0.43f, 1.2f, -8f, 1f, Ease.Out));
                bob.Add(K(k + 0.3f, 0.26f, 0.46f, 1.2f, 0f, 1f, Ease.InOut));
                cues.Add(new VignetteCue(k, CueKind.Knock, "door", 0.3f));
            }

            return new Vignette(JuneClosed, 3.2f, 0,
                Beats(0.3f, 1.9f, 2.1f, 2.9f),
                new[]
                {
                    Still("door", "door_shut", 0.62f, 0.46f, 1.1f),
                    new VignetteTrack("june", Part("june"), bob.ToArray()),
                    Track("moon", "moon",
                        K(2.10f, 0.86f, 0.30f, 0.75f, 0f, 0f),
                        K(2.90f, 0.86f, 0.76f, 0.75f, 0f, 1f, Ease.Out)),
                },
                cues.ToArray());
        }

        // ------------------------------------------------------------------------------------------- Aunt Kit

        /// <summary>"First show of the night. Best seats are on the porch.": a star pops over the owl booth (the show);
        /// Pip hops onto the porch (the best seat).</summary>
        private static Vignette BuildKitFirstShow()
        {
            return new Vignette(KitFirstShow, 3.0f, 0,
                Beats(0.3f, 0.9f, 1.4f, 2.1f),
                new[]
                {
                    Still("booth", "booth", 0.28f, 0.46f, 1.4f),
                    Pop("star", "star", 0.3f, 0.47f, 0.82f, 0.55f),
                    Still("porch", "porch", 0.74f, 0.32f, 1.3f),
                    Hop("pip", "pip", 1.4f, 0.7f, 0.56f, 0.30f, 0.74f, 0.66f, 0.8f),
                },
                new VignetteCue[0]);
        }

        /// <summary>"My old owl is bowing again. Thank you, both of you.": the owl bows twice; a heart pops over Pip,
        /// then over June.</summary>
        private static Vignette BuildKitThanks()
        {
            var owl = new List<VignetteKey> { K(0f, 0.24f, 0.48f, 1.3f, 0f, 1f) };
            foreach (float t in new[] { 0.2f, 0.8f })
            {
                owl.Add(K(t, 0.24f, 0.48f, 1.3f, 0f, 1f));
                owl.Add(K(t + 0.2f, 0.24f, 0.45f, 1.3f, -25f, 1f, Ease.Out));
                owl.Add(K(t + 0.45f, 0.24f, 0.48f, 1.3f, 0f, 1f, Ease.InOut));
            }

            return new Vignette(KitThanks, 3.2f, 0,
                Beats(0.2f, 1.25f, 1.6f, 2.4f),
                new[]
                {
                    new VignetteTrack("owl", Part("owl"), owl.ToArray()),
                    Still("pip", "pip", 0.58f, 0.40f, 1.0f),
                    Still("june", "june", 0.82f, 0.40f, 1.0f),
                    Pop("heart1", "heart", 1.6f, 0.58f, 0.84f, 0.5f),
                    Pop("heart2", "heart", 2.1f, 0.82f, 0.84f, 0.5f),
                },
                new VignetteCue[0]);
        }

        /// <summary>"A painted owl, wings folded.": the little panel swings open on a painted owl, which stays still.</summary>
        private static Vignette BuildPipOwlPanel()
        {
            return new Vignette(PipOwlPanel, 2.4f, 0,
                Beats(0.2f, 0.7f, 0.9f, 2.0f),
                new[]
                {
                    Still("grille", "grille", 0.50f, 0.36f, 1.3f),
                    Pop("owl", "owl", 0.4f, 0.50f, 0.58f, 1.1f),
                    Pop("eye", "eye", 1.2f, 0.80f, 0.62f, 0.8f),
                },
                new VignetteCue[0]);
        }

        /// <summary>"So, detective, what happened here?": Kit by her booth; a "?" pops; Pip's notebook disc wobbles.</summary>
        private static Vignette BuildKitAsk()
        {
            return new Vignette(KitAsk, 2.6f, 0,
                Beats(0.2f, 0.8f, 1.0f, 2.2f),
                new[]
                {
                    Still("kit", "kit", 0.28f, 0.46f, 1.2f),
                    Pop("what", "question", 0.5f, 0.50f, 0.74f, 0.7f),
                    Track("pip", "pip",
                        K(0f, 0.76f, 0.44f, 1.0f, 0f, 1f),
                        K(1.00f, 0.76f, 0.44f, 1.0f, 0f, 1f),
                        K(1.30f, 0.76f, 0.47f, 1.0f, 8f, 1f, Ease.Out),
                        K(1.60f, 0.76f, 0.44f, 1.0f, -6f, 1f, Ease.InOut),
                        K(1.90f, 0.76f, 0.44f, 1.0f, 0f, 1f, Ease.InOut)),
                    Pop("think", "emo_think", 1.0f, 0.90f, 0.72f, 0.6f),
                },
                new VignetteCue[0]);
        }

        // ------------------------------------------------------------------------------------------- helpers

        /// <summary>A part that pops in at <paramref name="t"/> (0.3 s, overshoot) and stays.</summary>
        private static VignetteTrack Pop(string id, string part, float t, float x, float y, float scale) =>
            Track(id, part,
                K(t, x, y, scale * 0.3f, 0f, 0f),
                K(t + 0.3f, x, y, scale, 0f, 1f, Ease.Back));

        /// <summary>A stamp (⊘ by default) that drops in big at <paramref name="t"/>; pair it with a Stamp cue.</summary>
        private static VignetteTrack Stamp(string id, float t, float x, float y, float scale, string part = "not") =>
            Track(id, part,
                K(t, x, y, scale * 1.6f, 0f, 0f),
                K(t + 0.25f, x, y, scale, 0f, 1f, Ease.Out));

        /// <summary>A part dropping into place from above the stage (candy into the bowl).</summary>
        private static VignetteTrack Drop(string id, string part, float t, float x, float y, float scale, float rot) =>
            Track(id, part,
                K(t, x, 1.05f, scale, rot * 2f, 1f),
                K(t + 0.35f, x, y, scale, rot, 1f, Ease.Back));

        /// <summary>A hop on an arc from (x0, y0) to (x1, y1) over <paramref name="duration"/>, fading in on take-off.</summary>
        private static VignetteTrack Hop(string id, string part, float t, float duration, float x0, float y0, float x1,
            float y1, float scale) =>
            Track(id, part,
                K(t, x0, y0, scale, 0f, 0f),
                K(t + 0.1f, x0, y0, scale, 0f, 1f),
                K(t + 0.1f + (duration - 0.1f) * 0.5f, (x0 + x1) * 0.5f, System.Math.Max(y0, y1) + 0.2f, scale, 8f, 1f,
                    Ease.Out),
                K(t + duration, x1, y1, scale, 0f, 1f, Ease.InOut));

        /// <summary>The waving hand: pops in, then rocks back and forth four times.</summary>
        private static VignetteTrack Wave(string id, float t, float x, float y, float scale)
        {
            var keys = new List<VignetteKey>
            {
                K(t, x, y, scale * 0.3f, 0f, 0f),
                K(t + 0.25f, x, y, scale, 0f, 1f, Ease.Back),
            };
            float[] swings = { 18f, -14f, 18f, -14f, 0f };
            for (int i = 0; i < swings.Length; i++)
            {
                keys.Add(K(t + 0.25f + 0.19f * (i + 1), x, y, scale, swings[i], 1f, Ease.InOut));
            }

            return new VignetteTrack(id, PuppetParts.WaveHand, keys.ToArray());
        }

        /// <summary>A shut door rattling in place between <paramref name="t0"/> and <paramref name="t1"/> (stuck, not opening).</summary>
        private static VignetteKey[] Rattle(float x, float y, float scale, float t0, float t1)
        {
            var keys = new List<VignetteKey> { K(0f, x, y, scale, 0f, 1f), K(t0, x, y, scale, 0f, 1f) };
            int n = 6;
            for (int i = 1; i <= n; i++)
            {
                float rot = i == n ? 0f : (i % 2 == 0 ? 4f : -4f);
                float dx = i == n ? 0f : (i % 2 == 0 ? 0.006f : -0.006f);
                keys.Add(K(t0 + (t1 - t0) * i / n, x + dx, y, scale, rot, 1f, Ease.InOut));
            }

            return keys.ToArray();
        }

        /// <summary>A part that jumps up a little on each beat time (the floor hum).</summary>
        private static VignetteKey[] Shiver(float x, float y, float scale, float[] beats)
        {
            var keys = new List<VignetteKey> { K(0f, x, y, scale, 0f, 1f) };
            foreach (float b in beats)
            {
                keys.Add(K(b, x, y, scale, 0f, 1f));
                keys.Add(K(b + 0.1f, x, y + 0.05f, scale, 3f, 1f, Ease.Out));
                keys.Add(K(b + 0.3f, x, y, scale, 0f, 1f, Ease.InOut));
            }

            return keys.ToArray();
        }
    }
}
