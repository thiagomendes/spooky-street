using SpookyStreet.Core.Audio;
using UnityEditor;
using UnityEngine;

namespace SpookyStreet.EditorTools
{
    /// <summary>
    /// Import settings for every approved sound effect under <c>Content/Resources/Audio</c> (S003), kept in code like
    /// <see cref="ModelImportRules"/> so they are reviewable and survive .meta regeneration (tools/unity_meta.py writes
    /// bare importer metas). The rules themselves are engine-free and unit tested (<see cref="AudioImportPlan"/>):
    /// Vorbis at a quality per category, mono for 3D one-shots, decompress-on-load for short sounds and streaming for
    /// the night bed, every clip at its 48 kHz master rate. The same settings apply on every platform (D25).
    /// </summary>
    public sealed class AudioImportRules : AssetPostprocessor
    {
        public override uint GetVersion() => 1;

        /// <summary>The event definition a game audio path belongs to (null for anything else).</summary>
        public static AudioEventDef DefinitionFor(string assetPath)
        {
            if (!AudioImportPlan.TryParse(assetPath, out string id, out int take)) return null;
            return AudioEvents.CreateCatalog().TryGet(id, out var def) && take <= def.Variants ? def : null;
        }

        /// <summary>The importer sample settings for a plan entry.</summary>
        public static AudioImporterSampleSettings SampleSettingsFor(AudioImportSettings plan) => new AudioImporterSampleSettings
        {
            loadType = plan.Load == AudioLoad.Streaming ? AudioClipLoadType.Streaming : AudioClipLoadType.DecompressOnLoad,
            compressionFormat = AudioCompressionFormat.Vorbis,
            quality = plan.VorbisQuality,
            sampleRateSetting = AudioSampleRateSetting.PreserveSampleRate,
            sampleRateOverride = (uint)plan.SampleRate,
            preloadAudioData = plan.Load != AudioLoad.Streaming,
        };

        private void OnPreprocessAudio()
        {
            var def = DefinitionFor(assetPath);
            if (def == null)
            {
                if (assetPath.StartsWith(AudioImportPlan.AssetFolder, System.StringComparison.Ordinal))
                {
                    Debug.LogWarning("[AudioImportRules] not a catalog take, imported with defaults: " + assetPath);
                }

                return;
            }

            var plan = AudioImportPlan.For(def);
            var importer = (AudioImporter)assetImporter;
            importer.forceToMono = plan.ForceMono;
            importer.loadInBackground = plan.LoadInBackground;
            importer.ambisonic = false;
            importer.defaultSampleSettings = SampleSettingsFor(plan);
            // One setting everywhere: drop any platform override a hand edit may have left.
            foreach (var platform in new[] { "iOS", "Android", "Standalone", "WebGL" })
            {
                importer.ClearSampleSettingOverride(platform);
            }
        }
    }
}
