using System.Collections.Generic;
using SpookyStreet.Core.Animation;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.World;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.Cutscenes;
using Lines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;
using N = SpookyStreet.Core.Case.CaseNotebook;

namespace SpookyStreet.Core.Cutscenes
{
    /// <summary>World effects a cutscene can ask the stage for (the runtime maps each to the existing view).</summary>
    public static class CutsceneWorld
    {
        /// <summary>The owl house's front door shown shut (as Mr. Vale saw it); undone by <see cref="DoorRestore"/>.</summary>
        public const string DoorShut = "world.door_shut";

        /// <summary>The front door back to what the facts say.</summary>
        public const string DoorRestore = "world.door_restore";

        /// <summary>The workshop nook's near wall steps aside so the mechanism reads (as in the crank cutaway).</summary>
        public const string NookOpen = "world.nook_open";

        public const string NookClose = "world.nook_close";

        /// <summary>One cam cycle: three knocks, the follower lifting, and the owl bowing when the arm is linked.</summary>
        public const string MechanismCycle = "world.mechanism_cycle";

        /// <summary>A soft glint on Kit's old photo (the season seed; no explanation).</summary>
        public const string PhotoGlint = "world.photo_glint";

        /// <summary>The signal Pip's opening walk sends when Pip reaches the porch (the walk's wait point).</summary>
        public const string PipArrived = "walk.pip";

        /// <summary>The booth's little bell dings "on the hour" (S004 show clock).</summary>
        public const string BoothBell = "world.booth_bell";

        /// <summary>
        /// The owl's show: the wind wheel whirrs and the rooftop owl bows on three knocks (the street's wind cycle, so
        /// only once the arm is linked; after case 01 it always is).
        /// </summary>
        public const string OwlShow = "world.owl_show";

        private const string GlintPrefix = "world.glint.";

        /// <summary>
        /// The hall lamp's light runs along a gilt puppet in the troupe photo (<see cref="KeepsakeArt.Puppets"/>). Light on
        /// foil, not magic (world-and-cases §5). <paramref name="soft"/> is the smaller, dimmer second glint.
        /// </summary>
        public static string Glint(string puppet, bool soft = false) => GlintPrefix + (soft ? "soft." : "") + puppet;

        /// <summary>The puppet a glint cue names (and whether it is the soft one), or false.</summary>
        public static bool TryParseGlint(string id, out string puppet, out bool soft)
        {
            puppet = null;
            soft = false;
            if (id == null || !id.StartsWith(GlintPrefix, System.StringComparison.Ordinal))
            {
                return false;
            }

            string rest = id.Substring(GlintPrefix.Length);
            if (rest.StartsWith("soft.", System.StringComparison.Ordinal))
            {
                soft = true;
                rest = rest.Substring(5);
            }

            puppet = rest;
            return System.Array.IndexOf(KeepsakeArt.Puppets, rest) >= 0;
        }
    }

    /// <summary>
    /// Case 01's cutscenes (D28, narrative bible §5, <c>docs/world-and-cases.md</c> case 01 slots): the opening, where
    /// Pip arrives as the new kid on Lantern Lane in Candlebrook and play starts on the arrival porch, and the reveal
    /// payoff, staged from the stickers the child placed. Only exported clips are used (Walk, Wave, OpenBooth and the
    /// S003a testimony gestures ShadeEyes and CheckWatch; no hand-keyed animation). The S003a pass (docs/S003a-pass.md)
    /// set the gestures, the babble (each witness in the emotion of the matching line in play, <see cref="Audio.LineVoices"/>
    /// via <see cref="CutsceneAudio.BabbleAsIn"/>) and the music: the street theme opens the opening; the reveal score is
    /// timed to end as Aunt Kit reopens her booth, so the street theme comes back from its start there and never over
    /// the owl's three knocks (<see cref="RevealMusicAt"/>).
    /// </summary>
    public static class C01Cutscenes
    {
        public const string OpeningId = "cs.c01.opening";
        public const string RevealId = "cs.c01.reveal";

        /// <summary>The season seed (S004, bible §5 slot 4): straight after the reveal, once.</summary>
        public const string SeedId = "cs.c01.seed";

        /// <summary>"The street remembers" (S004, bible §5 slot 5): the first load after the stamp, once.</summary>
        public const string StreetRemembersId = "cs.c01.street_remembers";

        /// <summary>Every case 01 cutscene, in story order (the case page offers them in this order).</summary>
        public static readonly IReadOnlyList<string> Ids = new[] { OpeningId, RevealId, SeedId, StreetRemembersId };

        public static bool IsKnown(string id) => id == OpeningId || id == RevealId || id == SeedId || id == StreetRemembersId;

        /// <summary>
        /// The puppet whose glint seeds the next case (world-and-cases §4: case 02 is the moon's). Data, so a re-ordered
        /// season changes this one ID.
        /// </summary>
        public const string SeedPuppet = KeepsakeArt.PuppetMoon;

        /// <summary>Where Pip stands when play starts: the arrival porch, facing the owl house (spec §4).</summary>
        public static readonly StagePoint PorchSpawn = new StagePoint(0f, 0.62f, -13.4f);

        /// <summary>Where Pip's opening walk starts: the back of the porch, by the moving boxes.</summary>
        public static readonly StagePoint PorchBack = new StagePoint(-0.5f, 0.62f, -14.7f);

        /// <summary>The moving boxes on the arrival porch's back corner (the opening's "new kid" hint).</summary>
        public static readonly StagePoint BoxesCorner = new StagePoint(1.2f, 0.6f, -14.6f);

        /// <summary>The play camera behind Pip on the porch (pivot 0.95 m, rise 0.35 m, boom 3.8 m, pitch 4°, FOV 50).</summary>
        public static readonly CamPose PorchPlayView =
            new CamPose(new StagePoint(0.17f, 2.18f, -17.2f), new StagePoint(0.17f, 1.48f, -7.2f), 50f);

        // ------------------------------------------------------------------------------------------- opening

        public static readonly Cutscene Opening = BuildOpening();

        private static Cutscene BuildOpening()
        {
            var beats = new List<CutsceneBeat>
            {
                // 1. Dusk over Lantern Lane: the street, its pumpkins and lamps, the owl house at the end.
                new CutsceneBeat("town", 5.5f,
                        new CameraShot(P(1.2f, 7.4f, -21.5f), P(0f, 3.6f, 6f), 42f)
                            .DriftTo(P(0.6f, 5.6f, -18.8f), P(0f, 3.8f, 8f), 40f))
                    .Caption(Keys.OpeningTown)
                    .Music(0f, CutsceneAudio.MusicStreetTheme)
                    .Cue(CastCue.Place(0f, CastActor.Pip, PorchBack, 20f)),

                // 2. The new kid: moving boxes on the porch, and Pip steps out to its front edge.
                new CutsceneBeat("boxes", 4f,
                        new CameraShot(P(-1f, 1.35f, -16.9f), P(0.4f, 0.95f, -13.2f), 46f)
                            .DriftTo(P(-0.9f, 1.38f, -16.6f), P(0.4f, 1f, -13f), 44f))
                    .Caption(Keys.OpeningBoxes)
                    .Cue(CastCue.WalkTo(0.5f, CastActor.Pip, 1.3f, PorchBack, PorchSpawn))
                    .Wait(CutsceneWorld.PipArrived, 1.5f),

                // 3. Up the lane, the owl house's round window glows (it is lit from the start; the knocks come in play).
                new CutsceneBeat("window", 4f,
                        new CameraShot(P(0.9f, 1.85f, -12.4f), P(0f, 5.55f, 10.9f), 17f)
                            .DriftTo(P(0.9f, 1.85f, -12.4f), P(0f, 5.65f, 10.9f), 14.5f))
                    .Caption(Keys.OpeningWindow)
                    .Babble(1.6f, CutsceneAudio.Babble(CastActor.Pip, BabbleEmotion.Thinking)),

                // 4. A neighbour waves hello: Bea by her bicycle.
                new CutsceneBeat("wave", 3.5f,
                        new CameraShot(P(-4.4f, 1.2f, -4f), P(-7f, 1.05f, -1.35f), 34f)
                            .DriftTo(P(-4.5f, 1.2f, -3.8f), P(-7f, 1.05f, -1.35f), 32f))
                    .Caption(Keys.OpeningWave)
                    .Cue(CastCue.Gesture(0.3f, CastActor.Bea, CharacterClips.Wave, P(0f, 1f, -13.4f)))
                    .Babble(0.4f, CutsceneAudio.Babble(CastActor.Bea, BabbleEmotion.Happy)),

                // 5. The arrival-porch composition: settles into the play camera behind Pip; play starts here.
                new CutsceneBeat("porch", 3.5f,
                    new CameraShot(P(0.9f, 3.1f, -18.8f), P(0f, 1.7f, -6f), 46f)
                        .DriftTo(PorchPlayView.Eye, PorchPlayView.Aim, PorchPlayView.Fov))
                    .Cue(CastCue.Place(0f, CastActor.Pip, PorchSpawn, 0f)),
            };

            // Played through or skipped, play starts on the arrival porch facing the owl house.
            return new Cutscene(OpeningId, beats, 0.8f, new[] { CastCue.Place(0f, CastActor.Pip, PorchSpawn, 0f) });
        }

        // -------------------------------------------------------------------------------------------- reveal

        /// <summary>Sticker shots and story beats: lengths in seconds.</summary>
        public const float StickerSeconds = 1.6f;

        public const float StorySeconds = 3.2f;

        /// <summary>The questions in the order the true story happened: June's way in, the knocks, what they move.</summary>
        public static readonly IReadOnlyList<string> RevealOrder = new[] { N.QuestionEntry, N.QuestionSource, N.QuestionMoved };

        /// <summary>
        /// The reveal payoff (D26, D28), built from the child's own solved stickers: for each question, one shot per
        /// sticker the child placed (in the comic's order), then the story beat that stages it; then Aunt Kit reopening
        /// her booth, where it ends (the old photo is the <see cref="Seed"/> that follows it, S004). Null unless the case
        /// is stamped (the three deductions and the reopening): it never runs ahead of the child.
        /// </summary>
        public static Cutscene Reveal(CaseState state, CaseAnswers answers)
        {
            if (state == null || !state.Has(CaseFact.CaseStamped))
            {
                return null;
            }

            var sets = new List<IReadOnlyList<string>>();
            foreach (string id in RevealOrder)
            {
                var question = N.Question(id);
                if (!question.IsSolved(state))
                {
                    return null;
                }

                var set = answers?.Get(id);
                if (set == null || !CaseDeduction.IsCorrect(question, set))
                {
                    // An older save solved by proof: the notebook draws the same canonical comic (CaseAnswers).
                    set = CaseDeduction.CanonicalAnswer(question, state);
                }

                if (set == null || !CaseDeduction.IsCorrect(question, set))
                {
                    return null;
                }

                sets.Add(CaseDeduction.Comic(question, set));
            }

            var beats = new List<CutsceneBeat>();
            for (int q = 0; q < RevealOrder.Count; q++)
            {
                foreach (string sticker in sets[q])
                {
                    beats.Add(StickerBeat(sticker, beats));
                }

                beats.Add(StoryBeat(RevealOrder[q], beats));
            }

            float reopenAt = 0f;
            foreach (var beat in beats)
            {
                reopenAt += beat.Duration;
            }

            // S004: the reveal ends on the reopening; the photo moved into the seed cutscene that follows it.
            beats.Add(ReopenBeat(beats));
            MusicAt(beats, RevealMusicAt(reopenAt), CutsceneAudio.MusicReveal);
            return new Cutscene(RevealId, beats, 0.8f, null, hidePip: true);
        }

        /// <summary>
        /// When the reveal score starts (seconds into the reveal) so that it ends as the reopening beat starts at
        /// <paramref name="reopenAt"/>: its quiet ending then lies under the owl's bows and the street theme comes back
        /// from its start (the music plan's rule after a cue) as Aunt Kit opens her booth. Never before the first frame.
        /// </summary>
        public static float RevealMusicAt(float reopenAt) =>
            System.Math.Max(0f, reopenAt - CutsceneAudio.MusicSeconds(CutsceneAudio.MusicReveal));

        /// <summary>Adds a music cue at an absolute time of the cutscene, inside the beat that holds it.</summary>
        private static void MusicAt(List<CutsceneBeat> beats, float at, string id)
        {
            float start = 0f;
            for (int i = 0; i < beats.Count; i++)
            {
                if (at < start + beats[i].Duration || i == beats.Count - 1)
                {
                    beats[i].Music(System.Math.Min(System.Math.Max(0f, at - start), beats[i].Duration), id);
                    return;
                }

                start += beats[i].Duration;
            }
        }

        /// <summary>The framing of one sticker's place (where the child found it).</summary>
        public static CameraShot StickerShot(string stickerId, ShotTransition transition)
        {
            switch (stickerId)
            {
                case N.BeaFlash:
                    // Over Bea's shoulder: up high by the side of the house, past the hedge.
                    return Shot(transition, P(-9f, 1.5f, -4.6f), P(-4.6f, 3.1f, 14f), 34f, P(-8.9f, 1.55f, -4.3f), P(-4.6f, 3.1f, 14f), 31f);
                case N.ValeDoor:
                    // Over Mr. Vale's shoulder from his bench: the front door.
                    return Shot(transition, P(10.6f, 1.5f, -2.6f), P(0f, 1.45f, 10.9f), 28f, P(10.5f, 1.5f, -2.4f), P(0f, 1.45f, 10.9f), 26f);
                case N.BenchView:
                    // From the bench seat: the door ahead, the hedge hiding the side.
                    return Shot(transition, P(8.36f, 1.17f, 0.27f), P(0f, 1.4f, 10.9f), 32f, P(8.3f, 1.17f, 0.4f), P(0f, 1.4f, 10.9f), 29f);
                case N.StuckDoor:
                    return Shot(transition, P(0f, 1.3f, 8.2f), P(0f, 1.45f, 10.95f), 44f, P(0f, 1.3f, 8.5f), P(0f, 1.45f, 10.95f), 42f);
                case N.Cloth:
                    return Shot(transition, P(-4.6f, 3.6f, 12.6f), P(-5.87f, 3.35f, 14.5f), 30f, P(-4.75f, 3.6f, 12.8f), P(-5.87f, 3.35f, 14.5f), 27f);
                case N.SillSnag:
                    return Shot(transition, P(-5.2f, 3.4f, 12.7f), P(-3.15f, 2.8f, 14f), 32f, P(-5.05f, 3.38f, 12.85f), P(-3.15f, 2.8f, 14f), 29f);
                case N.FloorHum:
                    return Shot(transition, I(-1.5f, 1.45f, 0.75f), I(0.9f, 0.7f, 3.6f), 50f, I(-1.4f, 1.4f, 0.9f), I(0.9f, 0.7f, 3.6f), 47f);
                case N.WoodenPiece:
                    return Shot(transition, I(0.6f, 1.35f, 5.3f), I(2.6f, 1f, 4.8f), 42f, I(0.75f, 1.3f, 5.25f), I(2.6f, 1f, 4.8f), 39f);
                case N.OwlPanel:
                    return Shot(transition, I(0.9f, 1.2f, 3.7f), I(2.15f, 1f, 4.4f), 40f, I(1f, 1.18f, 3.75f), I(2.15f, 1f, 4.4f), 37f);
                case N.OwlBooth:
                    return Shot(transition, P(2.9f, 1.45f, 7.7f), P(1.3f, 1.5f, 10.4f), 36f, P(2.8f, 1.45f, 7.9f), P(1.3f, 1.5f, 10.4f), 33f);
                case N.LooseArm:
                    return Shot(transition, I(0.7f, 1.5f, 3.6f), I(2.5f, 1.1f, 4.2f), 40f, I(0.8f, 1.48f, 3.65f), I(2.5f, 1.1f, 4.2f), 37f);
                default:
                    return null;
            }
        }

        private static CutsceneBeat StickerBeat(string stickerId, List<CutsceneBeat> before)
        {
            var shot = StickerShot(stickerId, ShotTransition.Cut);
            if (shot == null)
            {
                throw new System.ArgumentException("no reveal shot for sticker " + stickerId, nameof(stickerId));
            }

            var transition = TransitionInto(shot.Start.Eye, before);
            shot = StickerShot(stickerId, transition == ShotTransition.Blend ? ShotTransition.Cut : transition);
            var beat = new CutsceneBeat("sticker:" + stickerId, StickerSeconds, shot)
                .Sticker(stickerId)
                .Caption(N.Sticker(stickerId).NoteKey);
            if (before.Count == 0)
            {
                // The reveal starts clean: June and Kit step out of frame until their own beats.
                beat.Cue(CastCue.Hide(0f, CastActor.June)).Cue(CastCue.Hide(0f, CastActor.Kit));
            }

            switch (stickerId)
            {
                case N.BeaFlash:
                    // Where she looked, as in her testimony: a hand over her eyes, up high by the side of the house.
                    beat.Cue(CastCue.Gesture(0.1f, CastActor.Bea, CharacterClips.ShadeEyes, P(-4.5f, 3f, 14f)))
                        .Babble(0.25f, CutsceneAudio.BabbleAsIn(CastActor.Bea, Lines.BeaSawCape));
                    break;
                case N.ValeDoor:
                    // When he looked: his watch, then his eyes back on the door he saw shut.
                    beat.World(0f, CutsceneWorld.DoorShut)
                        .Cue(CastCue.Gesture(0f, CastActor.Vale, CharacterClips.CheckWatch, P(0f, 1.4f, 10.9f)))
                        .Cue(CastCue.LookAt(0f, CastActor.Vale, P(0f, 1.4f, 10.9f), StickerSeconds))
                        .Babble(0.3f, CutsceneAudio.BabbleAsIn(CastActor.Vale, Lines.ValeDoorShut));
                    break;
                case N.BenchView:
                    beat.World(0f, CutsceneWorld.DoorShut)
                        .Cue(CastCue.LookAt(0f, CastActor.Vale, P(0f, 1.4f, 10.9f), StickerSeconds));
                    break;
                case N.StuckDoor:
                    beat.World(0f, CutsceneWorld.DoorShut);
                    break;
                case N.FloorHum:
                    beat.World(0.1f, CutsceneWorld.MechanismCycle);
                    break;
                case N.WoodenPiece:
                case N.OwlPanel:
                case N.LooseArm:
                    beat.World(0f, CutsceneWorld.NookOpen);
                    break;
            }

            return beat;
        }

        private static CutsceneBeat StoryBeat(string questionId, List<CutsceneBeat> before)
        {
            CameraShot shot;
            CutsceneBeat beat;
            switch (questionId)
            {
                case N.QuestionEntry:
                    // June crosses the treehouse bridge and slips in at the little window.
                    shot = Story(P(-8.3f, 3.65f, 6.4f), P(-5.1f, 2.95f, 13.9f), 31f, P(-8.2f, 3.65f, 6.5f), P(-4.2f, 2.95f, 14f), 29f, before);
                    beat = new CutsceneBeat("story:entry", StorySeconds, shot)
                        .Caption(Keys.RevealEntry)
                        .World(0f, CutsceneWorld.DoorRestore)
                        .Cue(CastCue.Show(0f, CastActor.June))
                        .Cue(CastCue.WalkTo(0.1f, CastActor.June, 1.1f, P(-6.7f, 2.42f, 13.5f), P(-5.75f, 2.42f, 14f),
                            P(-3.5f, 2.5f, 14f)))
                        .Cue(CastCue.Hide(3.15f, CastActor.June));
                    return beat;
                case N.QuestionSource:
                    // The crank cutaway's framing: the cam turns and the wooden piece knocks three times.
                    shot = Story(I(0.3f, 2.1f, 4.9f), I(2.9f, 1.5f, 4.5f), 46f, I(0.45f, 2f, 4.85f), I(2.9f, 1.4f, 4.5f), 42f, before);
                    return new CutsceneBeat("story:source", StorySeconds, shot)
                        .Caption(Keys.RevealSource)
                        .World(0f, CutsceneWorld.NookOpen)
                        .World(0.2f, CutsceneWorld.MechanismCycle);
                case N.QuestionMoved:
                    // The rooftop owl bows on the three knocks, seen from the street.
                    shot = Story(P(1.6f, -1.4f, 6f), P(0f, -0.4f, 0f), 26f, P(1.3f, -1.3f, 5.2f), P(0f, -0.4f, 0f), 24f, before,
                        CutsceneAnchors.Owl);
                    return new CutsceneBeat("story:moved", StorySeconds, shot)
                        .Caption(Keys.RevealMoved)
                        .World(0f, CutsceneWorld.NookClose)
                        .World(0.2f, CutsceneWorld.MechanismCycle);
                default:
                    throw new System.ArgumentException("unknown question " + questionId, nameof(questionId));
            }
        }

        private static CutsceneBeat ReopenBeat(List<CutsceneBeat> before)
        {
            var june = CaseStage.JunePorch;
            var kit = CaseStage.KitBooth;
            var shot = Story(P(0.35f, 1.45f, 6.3f), P(0f, 1.35f, 10.2f), 40f, P(0.3f, 1.42f, 6.7f), P(0f, 1.35f, 10.2f), 38f, before);
            return new CutsceneBeat("reopen", 4.2f, shot)
                .Caption(Keys.RevealReopen)
                .World(0f, CutsceneWorld.DoorRestore)
                .World(0f, CutsceneWorld.NookClose)
                .Cue(CastCue.Place(0f, CastActor.June, june.Position, june.FacingYaw))
                .Cue(CastCue.Place(0f, CastActor.Kit, kit.Position, kit.FacingYaw))
                .Cue(CastCue.Show(0f, CastActor.June))
                .Cue(CastCue.Show(0f, CastActor.Kit))
                .Cue(CastCue.OneShot(0.2f, CastActor.Kit, CharacterClips.OpenBooth))
                .Cue(CastCue.OneShot(1f, CastActor.June, CharacterClips.Wave))
                .Babble(0.4f, CutsceneAudio.BabbleAsIn(CastActor.Kit, Lines.KitFirstShow))
                .Babble(1.1f, CutsceneAudio.BabbleAsIn(CastActor.June, Lines.JuneGoOutside));
        }

        // ---------------------------------------------------------------------------------------------- seed

        /// <summary>
        /// The season seed (S004 plan §2.3, bible §5 slot 4), about 13 s, straight after the reveal: the troupe photo in
        /// the hall, the lamp's light running along young Kit's gilt owl, then, softer, along the moon (the next case's
        /// puppet, <see cref="SeedPuppet"/>); the crest on the photo's mount matched to the crest over Kit's booth; Pip on
        /// the arrival porch, and back to play behind her. Nothing is explained: it only makes the child curious.
        /// Played through or skipped, play starts on the arrival porch.
        /// </summary>
        public static readonly Cutscene Seed = BuildSeed();

        private static Cutscene BuildSeed()
        {
            var beats = new List<CutsceneBeat>();
            string owl = CutsceneAnchors.PhotoPart(KeepsakeArt.PuppetOwl);
            string mount = CutsceneAnchors.PhotoPart(KeepsakeArt.PhotoCrest);

            // 1. The old photo, wide: five young Players in costume before the booth, each with a gilt puppet.
            beats.Add(new CutsceneBeat("photo", 3f,
                    Story(P(0.12f, 0.12f, 1.55f), P(0f, 0f, 0f), 30f, P(0.06f, 0.08f, 1.25f), P(0f, 0f, 0f), 28f, beats,
                        CutsceneAnchors.KitPhoto))
                .Caption(Keys.RevealPhoto));

            // 2. The owl wakes in the photo: a push to the owl in young Kit's hands; the lamp's light runs along its edge.
            beats.Add(new CutsceneBeat("owl", 3f,
                    Story(P(0.03f, 0.03f, 0.5f), P(0f, 0f, 0f), 30f, P(0.015f, 0.02f, 0.36f), P(0f, 0f, 0f), 28f, beats, owl))
                .Caption(Keys.SeedOwl)
                .World(0.9f, CutsceneWorld.Glint(KeepsakeArt.PuppetOwl)));

            // 3. The next one: a slow pan along the five puppets, and the moon glints, softer.
            beats.Add(new CutsceneBeat("moon", 3f,
                    Story(P(0.1f, 0.09f, 0.62f), P(0.1f, 0.08f, 0f), 30f, P(-0.16f, 0.07f, 0.6f), P(-0.17f, 0.06f, 0f), 29f, beats,
                        CutsceneAnchors.KitPhoto))
                .Caption(Keys.SeedMoon)
                .World(1.7f, CutsceneWorld.Glint(SeedPuppet, soft: true)));

            // 4. The same crest: on the photo's mount, then (under the turning page) over Kit's open booth, the same
            // size in the frame; and Pip on the arrival porch, looking up the lane at it.
            beats.Add(new CutsceneBeat("mount", 1f,
                    Story(P(0.02f, 0.02f, 0.4f), P(0f, 0f, 0f), 26f, P(0.01f, 0.01f, 0.34f), P(0f, 0f, 0f), 25f, beats, mount))
                .Caption(Keys.SeedCrest));
            beats.Add(new CutsceneBeat("booth", 1f,
                    Story(P(0.05f, -0.1f, 2.3f), P(0f, 0f, 0f), 26f, P(0.03f, -0.08f, 2f), P(0f, 0f, 0f), 25f, beats,
                        CutsceneAnchors.BoothCrest))
                .Caption(Keys.SeedCrest)
                .Cue(CastCue.Place(0f, CastActor.Pip, PorchSpawn, 0f)));
            beats.Add(new CutsceneBeat("pip", 1f,
                    Shot(ShotTransition.Cut, P(1.9f, 1.4f, -11.2f), P(0f, 1.38f, -13.4f), 38f, P(1.85f, 1.42f, -11.3f),
                        P(0f, 1.42f, -13.4f), 37f))
                .Caption(Keys.SeedCrest)
                .Babble(0.2f, CutsceneAudio.Babble(CastActor.Pip, BabbleEmotion.Thinking)));

            // 5. Back: the drift into the play camera behind Pip, where she stands.
            beats.Add(new CutsceneBeat("back", 1f,
                new CameraShot(P(0.6f, 2.5f, -17.8f), P(0.1f, 1.55f, -7.2f), 48f)
                    .DriftTo(PorchPlayView.Eye, PorchPlayView.Aim, PorchPlayView.Fov)));

            return new Cutscene(SeedId, beats, 0.8f, new[] { CastCue.Place(0f, CastActor.Pip, PorchSpawn, 0f) });
        }

        // ---------------------------------------------------------------------------------- street remembers

        /// <summary>
        /// "The street remembers" (S004 plan §2.3, bible §5 slot 5, D29), about 18 s, on the first load after the stamp:
        /// a crane down over Lantern Lane (rhyming with the opening's first shot); "on the hour" the booth's bell dings,
        /// Mr. Vale checks his watch and the rooftop owl bows; the owl's show close; Bea waves at the owl by her
        /// stickered bike; June waves from the foot of her treehouse way; Kit waves from her open booth; and play starts
        /// behind Pip on the arrival porch. Existing clips only (CheckWatch, Wave, the open booth).
        /// </summary>
        public static readonly Cutscene StreetRemembers = BuildStreetRemembers();

        /// <summary>The rooftop owl, where Bea and Mr. Vale look up to.</summary>
        private static StagePoint RoofOwl => new StagePoint(0f, 8.7f, CaseRouteLayout.FrontZ + 0.2f);

        private static Cutscene BuildStreetRemembers()
        {
            var beats = new List<CutsceneBeat>
            {
                // 1. Back on the lane: the opening's crane down over Lantern Lane, the booth lit.
                new CutsceneBeat("lane", 3f,
                        new CameraShot(P(1.2f, 7.4f, -21.5f), P(0f, 3.6f, 6f), 42f)
                            .DriftTo(P(0.6f, 5.6f, -18.8f), P(0f, 3.8f, 8f), 40f))
                    .Caption(Keys.StreetLane)
                    .Music(0f, CutsceneAudio.MusicStreetTheme)
                    .Cue(CastCue.Place(0f, CastActor.Pip, PorchSpawn, 0f)),

                // 2. On the hour: the booth's bell dings; Mr. Vale checks his watch and looks up as the owl bows. A low
                // side shot from his right, so his face reads (he sits facing the owl house, so a front shot can never
                // hold the owl too): Vale big on the left, looking into the frame at the owl house and the bowing roof
                // owl on the right. It holds both from 19.5:9 to 16:9 (S004 polish).
                new CutsceneBeat("hour", 3f,
                        Shot(ShotTransition.Cut, P(9.5f, 0.68f, 1.12f), P(7.26f, 1.68f, 1.15f), 58f, P(9.42f, 0.7f, 1.18f),
                            P(7.2f, 1.7f, 1.22f), 58f))
                    .Caption(Keys.StreetHour)
                    .World(0.1f, CutsceneWorld.BoothBell)
                    .Cue(CastCue.Gesture(0.25f, CastActor.Vale, CharacterClips.CheckWatch, P(0f, 1.4f, 10.9f)))
                    .Babble(0.4f, CutsceneAudio.BabbleAsIn(CastActor.Vale, Lines.ValeRemembers))
                    .World(0.9f, CutsceneWorld.OwlShow)
                    .Cue(CastCue.LookAt(1.9f, CastActor.Vale, RoofOwl, 1.1f)),

                // 3. The show: the wind wheel whirrs and the rooftop owl bows three times on three knocks.
                new CutsceneBeat("show", 3.5f,
                        new CameraShot(P(1.6f, -1.4f, 6f), P(0f, -0.4f, 0f), 26f)
                            .DriftTo(P(1.3f, -1.3f, 5.2f), P(0f, -0.4f, 0f), 24f)
                            .From(CutsceneAnchors.Owl))
                    .Caption(Keys.StreetShow)
                    .World(0.5f, CutsceneWorld.OwlShow),

                // 4. Bea by her bike, the owl sticker on the basket: she waves at the owl.
                new CutsceneBeat("bea", 2.5f,
                        new CameraShot(P(-4.4f, 1.2f, -4f), P(-7f, 1.05f, -1.35f), 34f)
                            .DriftTo(P(-4.5f, 1.2f, -3.8f), P(-7f, 1.05f, -1.35f), 32f))
                    .Caption(Keys.StreetBea)
                    .Cue(CastCue.Gesture(0.3f, CastActor.Bea, CharacterClips.Wave, RoofOwl))
                    .Babble(0.4f, CutsceneAudio.BabbleAsIn(CastActor.Bea, Lines.BeaRemembers)),

                // 5. June at the foot of her treehouse way waves toward the porch.
                new CutsceneBeat("june", 2.5f,
                        new CameraShot(P(-8.05f, 1.25f, 4.2f), P(-10.45f, 0.95f, 5.6f), 34f)
                            .DriftTo(P(-8.15f, 1.25f, 4.3f), P(-10.45f, 0.95f, 5.6f), 32f))
                    .Cue(CastCue.OneShot(0.3f, CastActor.June, CharacterClips.Wave))
                    .Babble(0.4f, CutsceneAudio.BabbleAsIn(CastActor.June, Lines.JuneRemembers)),

                // 6. Kit at her open booth waves toward the porch, seen from east of the porch's middle post (x 1), so
                // the post stands beside her and never across her (S004 polish), the front door on the left ...
                new CutsceneBeat("kit", 2.3f,
                        new CameraShot(P(2.1f, 1.4f, 6.4f), P(0.95f, 1.5f, 10.2f), 40f)
                            .DriftTo(P(2.05f, 1.38f, 6.75f), P(0.95f, 1.5f, 10.2f), 38f))
                    .Cue(CastCue.OneShot(0.2f, CastActor.Kit, CharacterClips.Wave))
                    .Babble(0.3f, CutsceneAudio.BabbleAsIn(CastActor.Kit, Lines.KitThanks)),

                // ... and the drift into the play camera behind Pip on the arrival porch: play starts here.
                new CutsceneBeat("porch", 1.2f,
                        new CameraShot(P(0.9f, 3.1f, -18.8f), P(0f, 1.7f, -6f), 46f)
                            .DriftTo(PorchPlayView.Eye, PorchPlayView.Aim, PorchPlayView.Fov))
                    .Cue(CastCue.Place(0f, CastActor.Pip, PorchSpawn, 0f)),
            };

            return new Cutscene(StreetRemembersId, beats, 0.8f, new[] { CastCue.Place(0f, CastActor.Pip, PorchSpawn, 0f) });
        }

        private static CameraShot Story(StagePoint eye, StagePoint aim, float fov, StagePoint endEye, StagePoint endAim, float endFov,
            List<CutsceneBeat> before, string anchor = null)
        {
            var start = anchor != null ? CutsceneAnchors.Default(anchor).Apply(eye) : eye;
            var transition = TransitionInto(start, before);
            var shot = new CameraShot(eye, aim, fov, transition, transition == ShotTransition.Blend ? 0.6f : 0f)
                .DriftTo(endEye, endAim, endFov);
            return anchor != null ? shot.From(anchor) : shot;
        }

        private static CameraShot Shot(ShotTransition transition, StagePoint eye, StagePoint aim, float fov, StagePoint endEye,
            StagePoint endAim, float endFov) =>
            new CameraShot(eye, aim, fov, transition).DriftTo(endEye, endAim, endFov);

        /// <summary>
        /// Story beats blend from the last shot in the same space; a change between the street and the interior
        /// turns the page (D24.1); sticker shots cut, like comic panels.
        /// </summary>
        private static ShotTransition TransitionInto(StagePoint eye, List<CutsceneBeat> before)
        {
            if (before.Count == 0)
            {
                return ShotTransition.Cut;
            }

            var last = before[before.Count - 1].Shot;
            var lastEye = last.Anchor != null ? CutsceneAnchors.Default(last.Anchor).Apply(last.End.Eye) : last.End.Eye;
            bool inside = CaseRouteLayout.Inside(eye.X, eye.Z);
            if (CaseRouteLayout.Inside(lastEye.X, lastEye.Z) != inside)
            {
                return ShotTransition.PageTurn;
            }

            return ShotTransition.Blend;
        }

        private static StagePoint P(float x, float y, float z) => new StagePoint(x, y, z);

        /// <summary>A point in the interior, from its front wall (z = <see cref="CaseRouteLayout.InteriorFrontZ"/>).</summary>
        private static StagePoint I(float x, float y, float dz) => new StagePoint(x, y, CaseRouteLayout.InteriorFrontZ + dz);
    }
}
