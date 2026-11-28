using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Audio;
using SpookyStreet.Core.World;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// S003 effects wiring, engine-free: which events are imported and how (Vorbis quality per category, mono for 3D
    /// one-shots, decompress-on-load for short sounds, streaming for the bed), the surface-to-footstep mapping, the
    /// stride cadence, the notebook cue hooks and the pause and indoor rules of the mix.
    /// </summary>
    public class AudioWiringTests
    {
        private static readonly AudioCatalog Catalog = AudioEvents.CreateCatalog();

        [Test]
        public void ApprovedEffectsAreInTheCatalogAndOnlyTheV2VoicesAndMusic()
        {
            Assert.AreEqual(19, AudioImportPlan.Approved.Length, "the 18 effects of D20.1 and the street doorbell");
            Assert.IsEmpty(Catalog.Validate());
            foreach (var id in AudioImportPlan.Approved)
            {
                Assert.IsTrue(Catalog.TryGet(id, out var def), id);
                Assert.AreNotEqual(AudioBus.Voice, def.Bus, id);
                Assert.AreNotEqual(AudioBus.Music, def.Bus, id);
            }

            foreach (var def in Catalog.Events)
            {
                // D20.1 rejected the S003 formant voices and the plucked motif; D20.2 approved their v2 replacements.
                bool rejected = (def.Id.StartsWith("voice_") || def.Id.StartsWith("music_")) &&
                                !VoiceMusicEvents.IsVoice(def.Id) && !VoiceMusicEvents.IsMusic(def.Id);
                Assert.AreEqual(!rejected, AudioImportPlan.IsApproved(def.Id), def.Id + " (D20.1, D20.2)");
            }
        }

        [Test]
        public void CatalogMapsGameplayEventsToTheirBusesAndRules()
        {
            var knocks = Catalog.Get(AudioEvents.ClueKnocksDistant);
            Assert.IsTrue(knocks.Spatial);
            Assert.AreEqual(AudioBus.Sfx, knocks.Bus);
            CollectionAssert.AreEqual(AudioEvents.KnockOnsets, knocks.Markers);
            Assert.GreaterOrEqual(knocks.MinDistance, 10f, "the knocks carry from the owl house to the arrival porch");
            Assert.IsTrue(Catalog.Get(AudioEvents.ClueCamTap).Spatial);
            Assert.IsTrue(Catalog.Get(AudioEvents.ClueWindWheel).Spatial);
            Assert.IsFalse(Catalog.Get(AudioEvents.ClueFloorHum).Spatial, "the hum is all around in the hall");
            Assert.IsTrue(Catalog.Get(AudioEvents.MoveGlideLoop).Loop);
            Assert.IsTrue(Catalog.Get(AudioEvents.AmbNight).Loop);
            Assert.AreEqual(AudioBus.Ambience, Catalog.Get(AudioEvents.AmbNight).Bus);
            foreach (var id in new[] { AudioEvents.StepWood, AudioEvents.StepGrass, AudioEvents.StepStone })
            {
                var step = Catalog.Get(id);
                Assert.Greater(step.MinIntervalSeconds, 0f, id + " is throttled");
                Assert.Less(step.MinIntervalSeconds, FootstepCadence.RunStride / FootstepCadence.RunSpeed,
                    id + ": the throttle never drops a running step");
                Assert.AreEqual("Audio/" + id + "/" + id + "_t5", step.ResourcePath(5));
            }
        }

        [Test]
        public void ImportRulesFollowTheCategories()
        {
            var knocks = AudioImportPlan.For(Catalog.Get(AudioEvents.ClueKnocksDistant));
            Assert.AreEqual(AudioCategory.Clue, knocks.Category);
            Assert.AreEqual(0.8f, knocks.VorbisQuality);
            Assert.IsTrue(knocks.ForceMono, "3D one-shot");
            Assert.AreEqual(AudioLoad.DecompressOnLoad, knocks.Load);
            Assert.AreEqual(48000, knocks.SampleRate);

            var bed = AudioImportPlan.For(Catalog.Get(AudioEvents.AmbNight));
            Assert.AreEqual(AudioCategory.Ambience, bed.Category);
            Assert.AreEqual(AudioLoad.Streaming, bed.Load);
            Assert.IsTrue(bed.LoadInBackground);
            Assert.IsFalse(bed.ForceMono, "the bed is stereo");
            Assert.AreEqual(".ogg", AudioImportPlan.FileExtension(AudioEvents.AmbNight));

            var glide = AudioImportPlan.For(Catalog.Get(AudioEvents.MoveGlideLoop));
            Assert.AreEqual(AudioCategory.Movement, glide.Category);
            Assert.AreEqual(AudioLoad.DecompressOnLoad, glide.Load, "a short loop decodes once");
            Assert.IsFalse(glide.ForceMono);

            Assert.AreEqual(AudioCategory.Notebook, AudioImportPlan.CategoryOf(AudioEvents.UiPageTurn), "as mastered");
            Assert.AreEqual(AudioCategory.Ui, AudioImportPlan.CategoryOf(AudioEvents.UiTap));
            Assert.AreEqual(AudioCategory.Footstep, AudioImportPlan.CategoryOf(AudioEvents.StepStone));
            Assert.AreEqual(AudioCategory.Notebook, AudioImportPlan.CategoryOf(AudioEvents.NoteCorrectGlow));
            Assert.Less(AudioImportPlan.QualityFor(AudioCategory.Footstep), AudioImportPlan.QualityFor(AudioCategory.Clue));

            foreach (var id in AudioImportPlan.Approved)
            {
                var def = Catalog.Get(id);
                var plan = AudioImportPlan.For(def);
                Assert.That(plan.VorbisQuality, Is.InRange(0.4f, 0.9f), id);
                Assert.AreEqual(def.Spatial && !def.Loop, plan.ForceMono, id + ": mono exactly for 3D one-shots");
                Assert.AreEqual(id == AudioEvents.AmbNight, plan.Load == AudioLoad.Streaming, id + ": only the bed streams");
                if (id != AudioEvents.AmbNight) Assert.AreEqual(".wav", AudioImportPlan.FileExtension(id));
            }
        }

        [Test]
        public void TheDoorbellIsItsOwnSoundNeverTheNotebookChime()
        {
            var bell = Catalog.Get(AudioEvents.StreetDoorbell);
            Assert.AreEqual(AudioBus.Sfx, bell.Bus);
            Assert.AreEqual(4, bell.Variants);
            Assert.IsFalse(bell.Loop);
            Assert.Greater(bell.MinIntervalSeconds, 0.5f, "two rings at once play one ding-dong");
            Assert.IsTrue(AudioImportPlan.IsApproved(AudioEvents.StreetDoorbell), "ships in the build");
            Assert.AreEqual(AudioCategory.Street, AudioImportPlan.CategoryOf(AudioEvents.StreetDoorbell));
            var plan = AudioImportPlan.For(bell);
            Assert.AreEqual(AudioLoad.DecompressOnLoad, plan.Load, "short: decoded once, starts on the frame");
            Assert.AreEqual(".wav", AudioImportPlan.FileExtension(AudioEvents.StreetDoorbell));

            var heard = new List<string>();
            AudioCues.ClearListeners();
            AudioCues.Requested += heard.Add;
            try
            {
                AudioCues.Doorbell();
            }
            finally
            {
                AudioCues.ClearListeners();
            }

            CollectionAssert.AreEqual(new[] { AudioEvents.StreetDoorbell }, heard);
            CollectionAssert.DoesNotContain(heard, AudioEvents.NoteCorrectGlow, "the borrowed chime is gone");
        }

        [Test]
        public void ImportPathsParseIntoIdAndTake()
        {
            Assert.IsTrue(AudioImportPlan.TryParse(AudioImportPlan.AssetFolder + "ui_tap/ui_tap_t2.wav", out var id, out var take));
            Assert.AreEqual("ui_tap", id);
            Assert.AreEqual(2, take);
            Assert.IsTrue(AudioImportPlan.TryParse(AudioImportPlan.AssetFolder + "amb_night/amb_night_t3.ogg", out id, out take));
            Assert.AreEqual(3, take);
            Assert.IsFalse(AudioImportPlan.TryParse(AudioImportPlan.AssetFolder + "ui_tap/step_wood_t1.wav", out _, out _));
            Assert.IsFalse(AudioImportPlan.TryParse(AudioImportPlan.AssetFolder + "ui_tap/ui_tap_t0.wav", out _, out _));
            Assert.IsFalse(AudioImportPlan.TryParse(AudioImportPlan.AssetFolder + "ui_tap.wav", out _, out _));
            Assert.IsFalse(AudioImportPlan.TryParse("Assets/Other/ui_tap/ui_tap_t1.wav", out _, out _));
            Assert.IsFalse(AudioImportPlan.TryParse(null, out _, out _));
        }

        [Test]
        public void SurfacesMapToTheirFootsteps()
        {
            Assert.AreEqual(AudioEvents.StepWood, FootstepSurfaces.EventFor(FootstepSurface.Wood));
            Assert.AreEqual(AudioEvents.StepGrass, FootstepSurfaces.EventFor(FootstepSurface.Grass));
            Assert.AreEqual(AudioEvents.StepStone, FootstepSurfaces.EventFor(FootstepSurface.Stone));

            var cases = new Dictionary<string, FootstepSurface>
            {
                { "Ground", FootstepSurface.Grass },
                { "GroundRing_East", FootstepSurface.Grass },
                { "Lawn", FootstepSurface.Grass },
                { "Path", FootstepSurface.Stone },
                { "PathStones", FootstepSurface.Stone },
                { "Sidewalk", FootstepSurface.Stone },
                { "ArrivalPorch", FootstepSurface.Wood },
                { "PorchStep2", FootstepSurface.Wood },
                { "Collider_Deck", FootstepSurface.Wood },
                { "OwlHouse_Step", FootstepSurface.Wood },
                { "Hall_Floor", FootstepSurface.Wood },
                { "Bench", FootstepSurface.Wood },
            };
            foreach (var pair in cases)
            {
                Assert.AreEqual(pair.Value, FootstepSurfaces.Classify(new[] { pair.Key }, false), pair.Key);
            }

            Assert.IsFalse(FootstepSurfaces.TryClassify("Colliders", out _));
            Assert.AreEqual(FootstepSurface.Grass, FootstepSurfaces.Classify(new[] { "Mystery" }, false), "outdoors defaults to the lawn");
            Assert.AreEqual(FootstepSurface.Grass, FootstepSurfaces.Classify(null, false));
            Assert.AreEqual(FootstepSurface.Wood, FootstepSurfaces.Classify(new[] { "Ground" }, true), "inside the house it is boards");
            Assert.AreEqual(FootstepSurface.Wood, FootstepSurfaces.Classify(new[] { "Collider_Deck", "Colliders", "Lawn" }, false),
                "the part's own name beats its parents'");
            Assert.AreEqual(FootstepSurface.Stone, FootstepSurfaces.Classify(new[] { "Colliders", "PathStones_3" }, false),
                "unnamed parts take the model's name");
            Assert.AreEqual(FootstepSurface.Stone, FootstepSurfaces.Classify(new[] { "Ground" }, false, onSidewalk: true),
                "the lawn collider under the sidewalk slabs");
            Assert.AreEqual(FootstepSurface.Wood, FootstepSurfaces.Classify(new[] { "PorchStep0" }, false, onSidewalk: true));
        }

        [Test]
        public void EveryWalkableSlabOfTheRouteAndHouseIsWood()
        {
            int walkable = 0;
            foreach (var slab in CaseRouteLayout.Slabs)
            {
                if (slab.Kind != SlabKind.Walk) continue;
                walkable++;
                Assert.IsTrue(FootstepSurfaces.TryClassify(slab.Name, out var surface), slab.Name + " has no surface keyword");
                Assert.AreEqual(FootstepSurface.Wood, surface, slab.Name);
            }

            Assert.Greater(walkable, 10);
        }

        [Test]
        public void SidewalkBandFollowsTheStreetLayout()
        {
            Assert.IsTrue(FootstepSurfaces.OnSidewalk(0f, EnvironmentLayout.SidewalkZ));
            Assert.IsTrue(FootstepSurfaces.OnSidewalk(-20f, EnvironmentLayout.SidewalkFrontZ + 0.1f));
            Assert.IsFalse(FootstepSurfaces.OnSidewalk(0f, EnvironmentLayout.SidewalkZ + 1.5f));
            Assert.IsFalse(FootstepSurfaces.OnSidewalk(0f, -5f));
            Assert.IsFalse(FootstepSurfaces.OnSidewalk(30f, EnvironmentLayout.SidewalkZ));
        }

        [Test]
        public void CadenceStepsOncePerStrideOnTheGroundOnly()
        {
            var cadence = new FootstepCadence();
            const float dt = 1f / 60f;
            int steps = 0;
            float firstAt = -1f;
            for (int i = 0; i < 600; i++) // 10 s walking at the walk clip's speed
            {
                if (cadence.Advance(FootstepCadence.WalkSpeed * dt, FootstepCadence.WalkSpeed, true))
                {
                    steps++;
                    if (firstAt < 0f) firstAt = i * dt;
                }
            }

            float expected = FootstepCadence.WalkSpeed * 10f / FootstepCadence.WalkStride;
            Assert.That(steps, Is.InRange((int)expected - 1, (int)expected + 1), "one step per 0.55 m");
            Assert.Less(firstAt, 0.2f, "the first footfall comes right away");

            cadence.Reset();
            steps = 0;
            for (int i = 0; i < 600; i++)
            {
                if (cadence.Advance(FootstepCadence.RunSpeed * dt, FootstepCadence.RunSpeed, true)) steps++;
            }

            float run = FootstepCadence.RunSpeed * 10f / FootstepCadence.RunStride;
            Assert.That(steps, Is.InRange((int)run - 1, (int)run + 1), "longer strides when running");
            Assert.Greater(FootstepCadence.StrideFor(3f), FootstepCadence.WalkStride);

            steps = 0;
            for (int i = 0; i < 120; i++)
            {
                if (cadence.Advance(FootstepCadence.RunSpeed * dt, FootstepCadence.RunSpeed, false)) steps++;
                if (cadence.Advance(0.001f, 0.06f, true)) steps++; // standing, a little shuffle
            }

            Assert.AreEqual(0, steps, "no steps in the air or standing");
        }

        [Test]
        public void NotebookHooksRaiseTheirEvents()
        {
            var heard = new List<string>();
            AudioCues.ClearListeners();
            AudioCues.Requested += heard.Add;
            try
            {
                AudioCues.StickerPick();
                AudioCues.StickerDrop();
                AudioCues.StickerWobble();
                AudioCues.CorrectGlow();
                AudioCues.PageTurn();
                AudioCues.UiTap();
                AudioCues.Play(AudioEvents.StepWood);
            }
            finally
            {
                AudioCues.ClearListeners();
            }

            CollectionAssert.AreEqual(new[]
            {
                AudioEvents.NoteStickerPick, AudioEvents.NoteStickerDrop, AudioEvents.NoteStickerWobble,
                AudioEvents.NoteCorrectGlow, AudioEvents.UiPageTurn, AudioEvents.UiTap, AudioEvents.StepWood,
            }, heard);
            Assert.DoesNotThrow(AudioCues.StickerPick, "no listener: nothing happens");
            foreach (var id in heard) Assert.IsTrue(AudioImportPlan.IsApproved(id), id);
        }

        [Test]
        public void PauseHoldsGameSoundButNotMusicOrTheUiTap()
        {
            Assert.IsTrue(AudioMix.PausesWithGame(Catalog.Get(AudioEvents.StepGrass)));
            Assert.IsTrue(AudioMix.PausesWithGame(Catalog.Get(AudioEvents.ClueKnocksDistant)));
            Assert.IsTrue(AudioMix.PausesWithGame(Catalog.Get(AudioEvents.AmbNight)));
            Assert.IsTrue(AudioMix.PausesWithGame(Catalog.Get(AudioEvents.VoiceBea)));
            Assert.IsFalse(AudioMix.PausesWithGame(Catalog.Get(AudioEvents.UiTap)), "the pause menu's own buttons tap");
            Assert.IsFalse(AudioMix.PausesWithGame(Catalog.Get(AudioEvents.MusicMotifWood)));
        }

        [Test]
        public void TheBedIsQuieterAndMuffledIndoors()
        {
            Assert.AreEqual(0f, AudioMix.AmbienceTrimDb(false));
            Assert.Less(AudioMix.AmbienceTrimDb(true), -6f);
            Assert.AreEqual(AudioMix.OpenLowPassHz, AudioMix.AmbienceLowPassHz(false));
            Assert.Less(AudioMix.AmbienceLowPassHz(true), 2000f);
            Assert.AreEqual(-3f, AudioMix.Approach(0f, -12f, 3f));
            Assert.AreEqual(-12f, AudioMix.Approach(-11f, -12f, 3f));
            Assert.AreEqual(2f, AudioMix.Approach(5f, 0f, 3f));
            var bed = Catalog.Get(AudioEvents.AmbNight);
            Assert.AreEqual(AudioMix.DbToLinear(AudioMix.IndoorAmbienceDb), AudioMix.PlayGain(bed, 0f, false, AudioMix.IndoorAmbienceDb), 1e-5f);
            Assert.AreEqual(0f, AudioMix.PlayGain(bed, AudioMix.MinDb, false, -12f), "a muted slider stays silent");
        }
    }
}
