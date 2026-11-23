using System.IO;
using NUnit.Framework;
using SpookyStreet.Core.Audio;
using SpookyStreet.EditorTools;
using UnityEditor;
using UnityEngine;

namespace SpookyStreet.Tests.Project
{
    /// <summary>
    /// The approved effects (S003, D20.1) are in the project with every take, imported by <see cref="AudioImportRules"/>:
    /// Vorbis at the category's quality, mono for 3D one-shots, decompress-on-load for short sounds, streaming in the
    /// background for the night bed, 48 kHz kept; they load from Resources; voices and music are not imported.
    /// </summary>
    public class AudioImportTests
    {
        private static readonly AudioCatalog Catalog = AudioEvents.CreateCatalog();

        private static string PathOf(AudioEventDef def, int take) =>
            AudioImportPlan.AssetFolder + def.Id + "/" + def.Id + "_t" + take + AudioImportPlan.FileExtension(def.Id);

        [Test]
        public void RulesRecogniseOnlyCatalogTakes()
        {
            Assert.AreEqual(AudioEvents.UiTap, AudioImportRules.DefinitionFor(AudioImportPlan.AssetFolder + "ui_tap/ui_tap_t5.wav").Id);
            Assert.IsNull(AudioImportRules.DefinitionFor(AudioImportPlan.AssetFolder + "ui_tap/ui_tap_t6.wav"), "no sixth take");
            Assert.IsNull(AudioImportRules.DefinitionFor(AudioImportPlan.AssetFolder + "nope/nope_t1.wav"));
            Assert.IsNull(AudioImportRules.DefinitionFor("Assets/SpookyStreet/Content/Resources/Models/x.wav"));

            var bed = AudioImportRules.SampleSettingsFor(AudioImportPlan.For(Catalog.Get(AudioEvents.AmbNight)));
            Assert.AreEqual(AudioClipLoadType.Streaming, bed.loadType);
            Assert.AreEqual(AudioCompressionFormat.Vorbis, bed.compressionFormat);
            Assert.AreEqual(AudioSampleRateSetting.PreserveSampleRate, bed.sampleRateSetting);
            var tap = AudioImportRules.SampleSettingsFor(AudioImportPlan.For(Catalog.Get(AudioEvents.UiTap)));
            Assert.AreEqual(AudioClipLoadType.DecompressOnLoad, tap.loadType);
            Assert.IsTrue(tap.preloadAudioData);
        }

        [Test]
        public void EveryApprovedTakeIsImportedWithItsRules()
        {
            int clips = 0;
            foreach (var id in AudioImportPlan.Approved)
            {
                var def = Catalog.Get(id);
                var plan = AudioImportPlan.For(def);
                for (int take = 1; take <= def.Variants; take++)
                {
                    string path = PathOf(def, take);
                    var importer = AssetImporter.GetAtPath(path) as AudioImporter;
                    Assert.IsNotNull(importer, path + " is missing or not audio");
                    var settings = importer.defaultSampleSettings;
                    Assert.AreEqual(AudioCompressionFormat.Vorbis, settings.compressionFormat, path);
                    Assert.AreEqual(plan.VorbisQuality, settings.quality, 0.011f, path + " Vorbis quality");
                    Assert.AreEqual(plan.Load == AudioLoad.Streaming ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad,
                        settings.loadType, path);
                    Assert.AreEqual(AudioSampleRateSetting.PreserveSampleRate, settings.sampleRateSetting, path);
                    Assert.AreEqual(plan.ForceMono, importer.forceToMono, path + " mono");
                    Assert.AreEqual(plan.LoadInBackground, importer.loadInBackground, path);

                    var clip = Resources.Load<AudioClip>(def.ResourcePath(take));
                    Assert.IsNotNull(clip, "Resources/" + def.ResourcePath(take));
                    Assert.AreEqual(AudioImportPlan.SampleRate, clip.frequency, path);
                    Assert.AreEqual(id == AudioEvents.AmbNight ? 2 : 1, clip.channels, path + " channels");
                    Assert.AreEqual(settings.loadType, clip.loadType, path);
                    Assert.Greater(clip.length, 0.05f, path);
                    if (def.Markers.Count > 0)
                    {
                        Assert.Greater(clip.length, def.Markers[def.Markers.Count - 1], path + " holds every marker");
                    }

                    clips++;
                }
            }

            Assert.AreEqual(70, clips, "18 approved effects, 70 takes");
        }

        [Test]
        public void IosAudioSessionRespectsTheSilentSwitchAndMixes()
        {
            // Unity's Ambient session: the Ring/Silent switch silences the game and a parent's music keeps playing.
            Assert.IsFalse(PlayerSettings.muteOtherAudioSources, "keep the Ambient category (docs/S003-sfx-wiring.md)");
        }

        [Test]
        public void VoicesAndMusicAreNotImported()
        {
            foreach (string folder in Directory.GetDirectories(AudioImportPlan.AssetFolder.TrimEnd('/')))
            {
                string id = Path.GetFileName(folder);
                Assert.IsTrue(AudioImportPlan.IsApproved(id), id + " is not an approved effect (D20.1)");
            }

            Assert.IsNull(Resources.Load<AudioClip>(Catalog.Get(AudioEvents.VoicePip).ResourcePath(1)));
            Assert.IsNull(Resources.Load<AudioClip>(Catalog.Get(AudioEvents.MusicMotifPluck).ResourcePath(1)));
        }
    }
}
