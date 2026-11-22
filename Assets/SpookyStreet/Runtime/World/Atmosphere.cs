using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;

namespace SpookyStreet.World
{
    /// <summary>Where the warm practical lights sit, from the greybox or from the environment models.</summary>
    public struct AtmosphereAnchors
    {
        /// <summary>Centre of the round attic window (the opening's focal point).</summary>
        public Vector3 AtticWindow;
        public Vector3 AtticLight;
        public Vector3 ArrivalLight;
        public Vector3 DoorLight;

        public static AtmosphereAnchors Greybox => new AtmosphereAnchors
        {
            AtticWindow = GreyboxBuilder.AtticWindow,
            AtticLight = GreyboxBuilder.AtticWindow + new Vector3(0f, -0.2f, -0.9f),
            ArrivalLight = GreyboxBuilder.PorchLamp + new Vector3(-0.3f, -0.35f, 0.3f),
            DoorLight = new Vector3(0f, 2.6f, GreyboxBuilder.HouseFrontZ - 0.9f),
        };
    }

    /// <summary>
    /// Light story for the arrival view (docs/art-bible.md, spec v1.3 Halloween night): a violet night sky with a
    /// paper moon (the handcrafted sky, <see cref="MagicSky"/>), cool moonlight from behind the street rim-lighting
    /// the silhouettes, warm amber practicals (attic window, street lamp, porch lamp) carrying the eye, shadows
    /// staying blue-violet. Mobile-cheap: one shadowed directional light, three unshadowed point lights, trilight
    /// ambient, linear fog. Also owns the global post-processing Volume (bloom picks up the emissive glow parts);
    /// the camera must have post-processing on.
    /// </summary>
    public static class Atmosphere
    {
        /// <summary>
        /// Moon centre: upper left of the arrival view, over the west neighbour's roof and clear of the plaza maple,
        /// just inside the sky dome (so the paper clouds and bats can pass in front of it).
        /// </summary>
        public static readonly Vector3 MoonPosition = new Vector3(-30.4f, 19.2f, 34.9f);
        public const float MoonDiameter = 8.4f;

        /// <summary>The moon disc faces this point: between the dome's centre and the arrival porch.</summary>
        public static readonly Vector3 MoonFacing = new Vector3(0f, -0.65f, -2.5f);

        public static Transform Build(Transform parent, MaterialLibrary materials, AtmosphereAnchors anchors)
        {
            var root = new GameObject("Atmosphere").transform;
            root.SetParent(parent, false);

            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Palette.NightAmbientSky;
            RenderSettings.ambientEquatorColor = Palette.NightAmbientEquator;
            RenderSettings.ambientGroundColor = Palette.NightAmbientGround;
            RenderSettings.ambientIntensity = 1f;
            RenderSettings.fog = true;
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogColor = Palette.NightFog;
            RenderSettings.fogStartDistance = 28f;
            RenderSettings.fogEndDistance = 130f;

            // Cool moonlight from behind the house (north-west), rim-lighting roofs, hedge tops and Pip.
            var moon = Light("Moonlight", root, LightType.Directional, Palette.MoonCool, 0.6f);
            moon.shadows = LightShadows.Soft;
            moon.shadowStrength = 0.7f;
            moon.transform.rotation = Quaternion.Euler(32f, 160f, 0f);

            var attic = Light("AtticLamp", root, LightType.Point, Palette.PumpkinAmber, 3f);
            attic.range = 7f;
            attic.transform.position = anchors.AtticLight;

            var porch = Light("ArrivalPorchLamp", root, LightType.Point, Palette.PumpkinAmber, 2.2f);
            porch.range = 6f;
            porch.transform.position = anchors.ArrivalLight;

            // Warm light over the owl house's door so the façade reads at night, not as a black cut-out.
            var door = Light("OwlHouseDoorLamp", root, LightType.Point, Palette.PumpkinAmber, 1.6f);
            door.range = 5f;
            door.transform.position = anchors.DoorLight;

            if (materials != null)
            {
                // Paper moon, halo, cloud rings, bats and shooting star (all under the bloom threshold, so the
                // attic window keeps the brightest value on screen).
                Transform dome = parent != null ? parent.Find(GreyboxBuilder.SkyDomeName) : null;
                MagicSky.Build(root, materials, dome != null ? dome.GetComponent<Renderer>() : null);
            }

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
            profile.name = "LanternLane_Night";

            // Neutral keeps pumpkin amber from drifting to yellow (ACES pushes it).
            profile.Add<Tonemapping>().mode.Override(TonemappingMode.Neutral);

            var bloom = profile.Add<Bloom>();
            // Glow parts emit above 1 (GlowLevels): the attic window blooms widest, lamps and faces softly.
            bloom.threshold.Override(1f);
            bloom.intensity.Override(0.55f);
            bloom.scatter.Override(0.65f);
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
