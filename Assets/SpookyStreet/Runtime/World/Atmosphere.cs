using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SpookyStreet.World
{
    /// <summary>
    /// Light story for the arrival view (docs/art-bible.md): cool moonlight from behind the street rim-lights the
    /// silhouettes, warm amber pools carry the eye, shadows stay blue-indigo. Also owns the global post-processing
    /// Volume; the camera must have post-processing enabled for it to do anything.
    /// </summary>
    public static class Atmosphere
    {
        public static Transform Build(Transform parent)
        {
            var root = new GameObject("Atmosphere").transform;
            root.SetParent(parent, false);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Palette.AmbientSky;
            RenderSettings.ambientEquatorColor = Palette.HouseIndigo;
            RenderSettings.ambientGroundColor = Palette.AmbientGround;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Palette.SkyHorizon;
            RenderSettings.fogStartDistance = 25f;
            RenderSettings.fogEndDistance = 110f;

            var moon = Light("Moonlight", root, LightType.Directional, Palette.Moonlight, 0.55f);
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.75f;
            moon.transform.rotation = Quaternion.Euler(30f, 150f, 0f);

            var attic = Light("AtticLamp", root, LightType.Point, Palette.PumpkinAmber, 3f);
            attic.range = 7f;
            attic.transform.position = GreyboxBuilder.AtticWindow + new Vector3(0f, -0.2f, -0.9f);

            var porch = Light("ArrivalPorchLamp", root, LightType.Point, Palette.PumpkinAmber, 2.2f);
            porch.range = 6f;
            porch.transform.position = GreyboxBuilder.PorchLamp + new Vector3(-0.3f, -0.35f, 0.3f);

            // Warm light over the owl house's door so the façade reads at dusk, not as a black cut-out.
            var door = Light("OwlHouseDoorLamp", root, LightType.Point, Palette.PumpkinAmber, 1.6f);
            door.range = 5f;
            door.transform.position = new Vector3(0f, 2.6f, GreyboxBuilder.HouseFrontZ - 0.9f);

            BuildVolume(root);
            return root;
        }

        /// <summary>Turns on HDR and post-processing for a script-created camera (both default off).</summary>
        public static void ConfigureCamera(Camera camera)
        {
            camera.allowHDR = true;
            var data = camera.GetUniversalAdditionalCameraData();
            data.renderPostProcessing = true;
            data.antialiasing = AntialiasingMode.FastApproximateAntialiasing;
        }

        private static Light Light(string name, Transform parent, LightType type, Color color, float intensity)
        {
            var light = new GameObject(name).AddComponent<Light>();
            light.transform.SetParent(parent, false);
            light.type = type;
            light.color = color;
            light.intensity = intensity;
            return light;
        }

        private static void BuildVolume(Transform parent)
        {
            var go = new GameObject("GlobalVolume");
            go.transform.SetParent(parent, false);
            var volume = go.AddComponent<Volume>();
            volume.isGlobal = true;
            volume.priority = 0f;

            var profile = ScriptableObject.CreateInstance<VolumeProfile>();
            profile.name = "LanternLane_Twilight";

            // Neutral keeps pumpkin amber from drifting to yellow (ACES pushes it).
            profile.Add<Tonemapping>().mode.Override(TonemappingMode.Neutral);

            var bloom = profile.Add<Bloom>();
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.35f);
            bloom.scatter.Override(0.6f);
            bloom.tint.Override(Palette.PumpkinAmber);

            var vignette = profile.Add<Vignette>();
            vignette.intensity.Override(0.25f);
            vignette.color.Override(Palette.Ink);
            vignette.smoothness.Override(0.45f);

            var colour = profile.Add<ColorAdjustments>();
            colour.postExposure.Override(0.15f);
            colour.contrast.Override(8f);
            colour.saturation.Override(5f);

            volume.sharedProfile = profile;
        }
    }
}
