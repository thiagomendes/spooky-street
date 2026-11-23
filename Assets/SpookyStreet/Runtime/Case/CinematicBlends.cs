using Unity.Cinemachine;
using UnityEngine;

namespace SpookyStreet.Case
{
    /// <summary>
    /// The camera blend of the memory replay, shared with the cutscenes: custom Cinemachine blends from any camera into
    /// the named cinematic cameras and back (an ease in and out, or a cut under reduced motion), installed on the brain.
    /// </summary>
    public static class CinematicBlends
    {
        /// <summary>Cinemachine's wildcard camera name in custom blends.</summary>
        public const string AnyCamera = "**ANY CAMERA**";

        /// <summary>
        /// Sets <paramref name="brain"/>'s custom blends so every camera in <paramref name="cameras"/> blends in and out
        /// over <paramref name="seconds"/> (cuts when <paramref name="cut"/> or zero). Reuses <paramref name="settings"/>.
        /// </summary>
        public static void Configure(CinemachineBrain brain, ref CinemachineBlenderSettings settings, string name, bool cut,
            float seconds, params string[] cameras)
        {
            if (brain == null)
            {
                return;
            }

            if (settings == null)
            {
                settings = ScriptableObject.CreateInstance<CinemachineBlenderSettings>();
                settings.name = name;
            }

            var style = cut || seconds <= 0f ? CinemachineBlendDefinition.Styles.Cut : CinemachineBlendDefinition.Styles.EaseInOut;
            var blend = new CinemachineBlendDefinition(style, Mathf.Max(0f, seconds));
            var blends = new CinemachineBlenderSettings.CustomBlend[cameras.Length * 2];
            for (int i = 0; i < cameras.Length; i++)
            {
                blends[2 * i] = new CinemachineBlenderSettings.CustomBlend { From = AnyCamera, To = cameras[i], Blend = blend };
                blends[2 * i + 1] = new CinemachineBlenderSettings.CustomBlend { From = cameras[i], To = AnyCamera, Blend = blend };
            }

            settings.CustomBlends = blends;
            brain.CustomBlends = settings;
        }
    }
}
