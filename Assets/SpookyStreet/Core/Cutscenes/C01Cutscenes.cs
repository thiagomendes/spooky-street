using System.Collections.Generic;
using SpookyStreet.Core.Animation;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.World;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.Cutscenes;
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
    }

    /// <summary>
    /// Case 01's cutscenes (D28, narrative bible §5, <c>docs/world-and-cases.md</c> case 01 slots): the opening, where
    /// Pip arrives as the new kid on Lantern Lane in Candlebrook and play starts on the arrival porch, and the reveal
    /// payoff, staged from the stickers the child placed. Only existing clips are used (Walk, Wave, Point, OpenBooth);
    /// the gesture, babble and music pass is data for S003a. Babble and music IDs are placeholders until then.
    /// </summary>
    public static class C01Cutscenes
    {
        public const string OpeningId = "cs.c01.opening";
        public const string RevealId = "cs.c01.reveal";

        public static readonly IReadOnlyList<string> Ids = new[] { OpeningId, RevealId };

        public static bool IsKnown(string id) => id == OpeningId || id == RevealId;

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
                    .Music(0f, "music.c01.opening")
                    .Cue(CastCue.Place(0f, CastActor.Pip, PorchBack, 20f)),

                // 2. The new kid: moving boxes on the porch, and Pip steps out to its front edge.
                new CutsceneBeat("boxes", 4f,
                        new CameraShot(P(1.45f, 1.3f, -16.3f), P(-0.15f, 0.95f, -12.6f), 44f)
                            .DriftTo(P(1.3f, 1.35f, -16f), P(-0.1f, 1f, -12.2f), 44f))
                    .Caption(Keys.OpeningBoxes)
                    .Cue(CastCue.WalkTo(0.5f, CastActor.Pip, 1.3f, PorchBack, PorchSpawn))
                    .Wait(CutsceneWorld.PipArrived, 1.5f),

                // 3. Up the lane, the owl house's round window glows (it is lit from the start; the knocks come in play).
                new CutsceneBeat("window", 4f,
                        new CameraShot(P(0.9f, 1.85f, -12.4f), P(0f, 7.35f, 10.9f), 17f)
                            .DriftTo(P(0.9f, 1.85f, -12.4f), P(0f, 7.45f, 10.9f), 13.5f))
                    .Caption(Keys.OpeningWindow),

                // 4. A neighbour waves hello: Bea by her bicycle.
                new CutsceneBeat("wave", 3.5f,
                        new CameraShot(P(-4.4f, 1.2f, -4f), P(-7f, 1.05f, -1.35f), 34f)
                            .DriftTo(P(-4.5f, 1.2f, -3.8f), P(-7f, 1.05f, -1.35f), 32f))
                    .Caption(Keys.OpeningWave)
                    .Cue(CastCue.Gesture(0.3f, CastActor.Bea, CharacterClips.Wave, P(0f, 1f, -13.4f)))
                    .Babble(0.4f, "babble.c01.bea.hello"),

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
        /// her booth and the season seed (Kit's old photo, unexplained). Null unless the case is stamped (the three
        /// deductions and the reopening): it never runs ahead of the child.
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

            beats.Add(ReopenBeat(beats));
            beats.Add(SeedBeat(beats));
            return new Cutscene(RevealId, beats, 0.8f, null, hidePip: true);
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
                    return Shot(transition, P(9.7f, 1.3f, -1.9f), P(0f, 1.45f, 10.9f), 30f, P(9.6f, 1.3f, -1.7f), P(0f, 1.45f, 10.9f), 27f);
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
                beat.Cue(CastCue.Hide(0f, CastActor.June)).Cue(CastCue.Hide(0f, CastActor.Kit)).Music(0f, "music.c01.reveal");
            }

            switch (stickerId)
            {
                case N.BeaFlash:
                    beat.Cue(CastCue.Gesture(0.1f, CastActor.Bea, CharacterClips.Point, P(-4.5f, 3f, 14f)));
                    break;
                case N.ValeDoor:
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
                    shot = Story(P(-8.3f, 3.65f, 6.4f), P(-4.9f, 2.9f, 13.9f), 36f, P(-8.2f, 3.65f, 6.5f), P(-4.2f, 2.9f, 14f), 34f, before);
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
            var shot = Story(P(0.35f, 1.45f, 6.6f), P(0f, 1.35f, 10.2f), 36f, P(0.3f, 1.42f, 7f), P(0f, 1.35f, 10.2f), 34f, before);
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
                .Babble(0.4f, "babble.c01.kit.cheer")
                .Babble(1.1f, "babble.c01.june.cheer");
        }

        private static CutsceneBeat SeedBeat(List<CutsceneBeat> before)
        {
            // The season seed, unexplained: a slow push in on Kit's old photo in the hall, and one soft glint.
            var shot = Story(P(0.25f, 0.05f, 1.5f), P(0f, 0f, 0f), 30f, P(0.12f, 0.02f, 1.05f), P(0f, 0f, 0f), 26f, before,
                CutsceneAnchors.KitPhoto);
            return new CutsceneBeat("seed", 3.4f, shot)
                .Caption(Keys.RevealPhoto)
                .World(1.4f, CutsceneWorld.PhotoGlint);
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
