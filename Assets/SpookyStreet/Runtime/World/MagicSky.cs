using System.Collections.Generic;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.World;
using UnityEngine;
using UnityEngine.Rendering;

namespace SpookyStreet.World
{
    /// <summary>
    /// The handcrafted sky (owner request, art bible §3): a paper moon with a soft halo, three rings of cut-paper
    /// clouds drifting past it, twinkling stars, an occasional shooting star and paper bats crossing the moon.
    /// A sibling of <see cref="AmbientMotion"/>: it runs on scaled game time, so the title and pause screens
    /// (<c>Time.timeScale = 0</c>) hold it still, and <see cref="AmbientMotion.ReducedMotion"/> removes the
    /// shooting stars and bats and slows the clouds and the twinkle. The timing lives in <see cref="SkySchedule"/>.
    /// Everything stays under the bloom threshold so the attic window remains the brightest point.
    /// With the two sky shaders available (<see cref="Crafted"/>) the pieces use them; otherwise the sky degrades
    /// to the committed URP Unlit base (stars baked into the paint, flat clouds and bats, no halo or shooting
    /// star) rather than drawing anything in the error shader.
    /// </summary>
    public sealed class MagicSky : MonoBehaviour
    {
        /// <summary>Committed materials that reference the sky shaders, so player builds keep them.</summary>
        public const string DomeMaterialPath = "Materials/Sky_Dome";
        public const string PaperMaterialPath = "Materials/Sky_Paper";
        public const string DomeShaderName = "SpookyStreet/SkyDome";
        public const string PaperShaderName = "SpookyStreet/SkyPaper";

        public const int SkyTextureWidth = 1024;
        public const int SkyTextureHeight = 512;
        public const int MoonTextureSize = 256;
        public const int GrainTextureSize = 128;

        /// <summary>Shooting stars travel on a sphere just inside the dome.</summary>
        public const float ShootingStarRadius = 49.5f;
        public const float BatWingspan = 2f;

        /// <summary>The arrival porch view: the middle ring's first cloud is lined up with the moon from here.</summary>
        public static readonly Vector3 ReferenceView = new Vector3(0f, 1.7f, -13f);

        private const int Seed = 1031;
        private const float BatDepth = 0.6f;

        private static readonly int BaseMapId = Shader.PropertyToID("_BaseMap");
        private static readonly int BaseColorId = Shader.PropertyToID("_BaseColor");
        private static readonly int StarMapId = Shader.PropertyToID("_StarMap");
        private static readonly int StarColorId = Shader.PropertyToID("_StarColor");
        private static readonly int GlowColorId = Shader.PropertyToID("_GlowColor");
        private static readonly int RimColorId = Shader.PropertyToID("_RimColor");
        private static readonly int MoonPositionId = Shader.PropertyToID("_MoonPosition");
        private static readonly int MaxValueId = Shader.PropertyToID("_MaxValue");
        private static readonly int SkyTimeId = Shader.PropertyToID("_SkyTime");
        private static readonly int SrcBlendId = Shader.PropertyToID("_SrcBlend");
        private static readonly int DstBlendId = Shader.PropertyToID("_DstBlend");
        private static readonly int ZWriteId = Shader.PropertyToID("_ZWrite");
        private static readonly int CullId = Shader.PropertyToID("_Cull");

        private readonly List<Object> _owned = new List<Object>();
        private readonly List<Transform> _layers = new List<Transform>();
        private readonly List<Transform> _bats = new List<Transform>();
        private float[] _layerStart;
        private float[] _batSize;
        private float _moonRadius;
        private Material _domeMaterial;
        private Renderer _dome;
        private bool _hidden;
        private Material _streakMaterial;
        private Mesh _streakMesh;
        private Vector3[] _streakVertices;
        private GameObject _streak;
        private int _seenStars;
        private float _starAzimuth;

        /// <summary>
        /// True while the sky is not drawn: the camera is in the closed owl house interior (D24.1), part of which lies
        /// beyond the dome's opaque, depth-writing sphere, so a lens indoors would otherwise see the dome cut through
        /// the rooms as a flat purple wall (TestFlight 23). Nothing of the sky can be seen from inside the shell.
        /// </summary>
        public bool Hidden
        {
            get => _hidden;
            set
            {
                if (_hidden == value)
                {
                    return;
                }

                _hidden = value;
                if (_dome != null)
                {
                    _dome.forceRenderingOff = value;
                }

                foreach (var piece in GetComponentsInChildren<Renderer>(true))
                {
                    piece.forceRenderingOff = value;
                }
            }
        }

        /// <summary>True when the sky shaders are in use; false on the URP Unlit fallback.</summary>
        public bool Crafted { get; private set; }

        public SkySchedule Schedule { get; private set; }

        /// <summary>The gameplay camera: shooting stars fall in the part of the sky it faces.</summary>
        public Transform View { get; set; }

        public Transform Moon { get; private set; }

        public Renderer MoonDisc { get; private set; }

        /// <summary>Null on the fallback path.</summary>
        public Renderer MoonHalo { get; private set; }

        /// <summary>Null on the fallback path.</summary>
        public GameObject ShootingStar => _streak;

        public IReadOnlyList<Transform> CloudLayers => _layers;

        public IReadOnlyList<Transform> Bats => _bats;

        /// <summary>
        /// The sky dome's material: the painted sky with twinkling stars on the sky shader, or, without it, the
        /// same paint with the stars baked in on the URP Unlit base.
        /// </summary>
        public static Material CreateDomeMaterial(MaterialLibrary materials)
        {
            Material template = LoadCrafted(DomeMaterialPath, DomeShaderName);
            Texture2D paint = NewTexture("NightSky", SkyTextureWidth, SkyTextureHeight, TextureFormat.RGB24, false,
                TextureWrapMode.Repeat, TextureWrapMode.Clamp,
                SkyPainter.PaintSky(SkyTextureWidth, SkyTextureHeight, bakeStars: template == null));
            if (template == null)
            {
                Debug.LogWarning("[MagicSky] Sky dome shader unavailable; using the URP Unlit sky with baked stars.");
                return materials.Unlit(paint);
            }

            byte[] mask = SkyPainter.PaintStarMask(SkyTextureWidth, SkyTextureHeight);
            TextureFormat maskFormat = TextureFormat.RG16;
            if (!SystemInfo.SupportsTextureFormat(maskFormat))
            {
                maskFormat = TextureFormat.RGBA32;
                var wide = new byte[SkyTextureWidth * SkyTextureHeight * 4];
                for (int i = 0, o = 0; i < mask.Length; i += 2, o += 4)
                {
                    wide[o] = mask[i];
                    wide[o + 1] = mask[i + 1];
                    wide[o + 3] = 255;
                }

                mask = wide;
            }

            Texture2D stars = NewTexture("NightSkyStars", SkyTextureWidth, SkyTextureHeight, maskFormat, true,
                TextureWrapMode.Repeat, TextureWrapMode.Clamp, mask);
            var material = new Material(template) { name = "Sky_Dome_Painted" };
            material.SetTexture(BaseMapId, paint);
            material.SetTexture(StarMapId, stars);
            material.SetColor(StarColorId, Palette.StarLight);
            material.SetColor(GlowColorId, Palette.MoonSkyGlow);
            material.SetVector(MoonPositionId, Atmosphere.MoonPosition);
            material.SetFloat(MaxValueId, SkyModel.BrightestValue);
            material.SetFloat(SkyTimeId, 0f);
            return material;
        }

        /// <summary>
        /// Builds the moon, halo, cloud rings, bats and shooting star under <paramref name="parent"/>.
        /// <paramref name="dome"/> is the sky dome's renderer (its stars follow this component's clock).
        /// </summary>
        public static MagicSky Build(Transform parent, MaterialLibrary materials, Renderer dome)
        {
            var go = new GameObject("MagicSky");
            go.transform.SetParent(parent, false);
            var sky = go.AddComponent<MagicSky>();
            sky.Construct(materials, dome);
            return sky;
        }

        /// <summary>
        /// Where the line from <paramref name="view"/> to <paramref name="target"/> crosses the cylinder of
        /// <paramref name="radius"/> about the dome's axis: the azimuth (degrees, 0 = north, clockwise) and the
        /// height. False when the target is inside the cylinder.
        /// </summary>
        public static bool CrossingPoint(Vector3 view, Vector3 target, float radius, out float azimuth, out float height)
        {
            Vector3 centre = GreyboxBuilder.SkyDomeCentre;
            var from = new Vector2(view.x - centre.x, view.z - centre.z);
            var along = new Vector2(target.x - view.x, target.z - view.z);
            float a = Vector2.Dot(along, along);
            float b = 2f * Vector2.Dot(from, along);
            float c = Vector2.Dot(from, from) - radius * radius;
            float discriminant = b * b - 4f * a * c;
            azimuth = 0f;
            height = 0f;
            if (a < 1e-6f || discriminant < 0f)
            {
                return false;
            }

            float t = (-b + Mathf.Sqrt(discriminant)) / (2f * a);
            if (t <= 0f || t > 1f)
            {
                return false;
            }

            Vector2 point = from + along * t;
            azimuth = Mathf.Atan2(point.x, point.y) * Mathf.Rad2Deg;
            height = view.y + (target.y - view.y) * t;
            return true;
        }

        /// <summary>World position at <paramref name="azimuth"/> / <paramref name="elevation"/> degrees on a sphere about the dome's centre.</summary>
        public static Vector3 DomePoint(float azimuth, float elevation, float radius)
        {
            float a = azimuth * Mathf.Deg2Rad;
            float e = elevation * Mathf.Deg2Rad;
            float ring = Mathf.Cos(e) * radius;
            return GreyboxBuilder.SkyDomeCentre + new Vector3(Mathf.Sin(a) * ring, Mathf.Sin(e) * radius, Mathf.Cos(a) * ring);
        }

        /// <summary>
        /// Review captures and tests: turns the middle ring so its first cloud sits
        /// <paramref name="offsetDegrees"/> (ring degrees, positive to the right) from the moon as seen from
        /// <paramref name="viewPoint"/>.
        /// </summary>
        public void ShowCloudOverMoon(Vector3 viewPoint, float offsetDegrees)
        {
            CloudLayer layer = SkyModel.Layers[SkyModel.MoonLayer];
            if (!CrossingPoint(viewPoint, Atmosphere.MoonPosition, layer.Radius, out float azimuth, out _))
            {
                return;
            }

            float turn = SkyModel.CloudAngle(0.0, 0f, azimuth + offsetDegrees - _layerStart[SkyModel.MoonLayer]);
            Schedule.SetCloudClock(turn / layer.DegreesPerSecond);
            Apply();
        }

        /// <summary>Review captures and tests: a three-bat flight at <paramref name="progress"/> (0.5 = over the moon).</summary>
        public void ShowBats(float progress)
        {
            Schedule.StartBats(new BatFlight(SkyModel.BatMaxCount, 1, -0.2f, 0.25f), progress);
            Apply();
        }

        /// <summary>
        /// Review captures and tests: a shooting star at <paramref name="progress"/> of a fixed arc around
        /// <paramref name="viewAzimuth"/> (degrees, 0 = north). Returns the head's world position; on the fallback
        /// path (no streak) the position is still returned.
        /// </summary>
        public Vector3 ShowShootingStar(float progress, float viewAzimuth)
        {
            var arc = new ShootingStarArc(-12f, 46f, 20f, 10f);
            Schedule.StartShootingStar(arc, progress);
            _seenStars = Schedule.ShootingStarsStarted;
            _starAzimuth = viewAzimuth;
            Apply();
            float p = Schedule.ShootingStarProgress;
            return DomePoint(viewAzimuth + arc.AzimuthAt(p), arc.ElevationAt(p), ShootingStarRadius);
        }

        private void Construct(MaterialLibrary materials, Renderer dome)
        {
            _dome = dome;
            Schedule = new SkySchedule(Seed);
            Vector3 centre = GreyboxBuilder.SkyDomeCentre;
            _moonRadius = Atmosphere.MoonDiameter * 0.5f;

            Material template = LoadCrafted(PaperMaterialPath, PaperShaderName);
            Crafted = template != null;
            if (!Crafted)
            {
                Debug.LogWarning("[MagicSky] Sky paper shader unavailable; using flat URP Unlit pieces (no halo or shooting star).");
            }

            if (dome != null && dome.sharedMaterial != null)
            {
                // The dome material and its textures are runtime-made for this sky; free them with it.
                Material domeMaterial = dome.sharedMaterial;
                _owned.Add(domeMaterial);
                if (domeMaterial.HasProperty(BaseMapId))
                {
                    Own(domeMaterial.GetTexture(BaseMapId));
                }

                if (domeMaterial.HasProperty(SkyTimeId))
                {
                    _domeMaterial = domeMaterial;
                    _domeMaterial.SetVector(MoonPositionId, Atmosphere.MoonPosition);
                    Own(domeMaterial.GetTexture(StarMapId));
                }
            }

            // Moon: a textured paper disc, and behind it the soft halo card.
            Moon = new GameObject("Moon").transform;
            Moon.SetParent(transform, false);
            Moon.SetPositionAndRotation(Atmosphere.MoonPosition,
                Quaternion.LookRotation(Atmosphere.MoonPosition - Atmosphere.MoonFacing));
            Texture2D moonTexture = Own(NewTexture("PaperMoon", MoonTextureSize, MoonTextureSize, TextureFormat.RGB24,
                false, TextureWrapMode.Clamp, TextureWrapMode.Clamp, SkyPainter.PaintMoon(MoonTextureSize, GlowLevels.Moon)));
            Material moonMaterial = Crafted
                ? Paper(template, "Sky_Moon", moonTexture, false, Color.clear)
                : Own(materials.Unlit(moonTexture));
            MoonDisc = Piece("MoonDisc", Moon, Vector3.zero, Own(SkyMeshes.Disc(_moonRadius, 64)), moonMaterial);
            if (Crafted)
            {
                Material haloMaterial = Paper(template, "Sky_Halo", Texture2D.whiteTexture, true, Color.clear);
                MoonHalo = Piece("MoonHalo", Moon, new Vector3(0f, 0f, 0.06f),
                    Own(SkyMeshes.Halo(_moonRadius, 64, Palette.MoonHalo)), haloMaterial);
            }

            // Cloud rings and bats share one opaque cut-paper material.
            Material paper = null;
            if (Crafted)
            {
                Texture2D grain = Own(NewTexture("PaperGrain", GrainTextureSize, GrainTextureSize, TextureFormat.RGB24,
                    false, TextureWrapMode.Repeat, TextureWrapMode.Repeat, SkyPainter.PaintPaperGrain(GrainTextureSize)));
                Color rim = Palette.MoonRim;
                rim.a = 0.85f;
                paper = Paper(template, "Sky_PaperCut", grain, false, rim);
            }

            _layerStart = new float[SkyModel.Layers.Length];
            for (int i = 0; i < SkyModel.Layers.Length; i++)
            {
                CloudLayer layer = SkyModel.Layers[i];
                float firstBase = layer.Height;
                _layerStart[i] = layer.StartDegrees;
                if (i == SkyModel.MoonLayer &&
                    CrossingPoint(ReferenceView, Atmosphere.MoonPosition, layer.Radius, out float azimuth, out float height))
                {
                    firstBase = height - layer.CloudHeight * 0.45f;
                    _layerStart[i] = azimuth - SkyModel.MoonCloudLeadDegrees;
                }

                var ring = new GameObject("CloudLayer_" + i).transform;
                ring.SetParent(transform, false);
                ring.position = new Vector3(centre.x, 0f, centre.z);
                Material material = Crafted ? paper : materials.Glow(SkyMeshes.LayerColour(layer));
                Piece("PaperClouds", ring, Vector3.zero, Own(SkyMeshes.CloudRing(layer, i, firstBase)), material);
                _layers.Add(ring);
            }

            Mesh batMesh = Own(SkyMeshes.Flat(SkyShapes.Bat(BatWingspan), Palette.InkBlack, "PaperBat"));
            Material batMaterial = Crafted ? paper : materials.Glow(Palette.InkBlack);
            _batSize = new float[SkyModel.BatMaxCount];
            for (int b = 0; b < SkyModel.BatMaxCount; b++)
            {
                _batSize[b] = 1f - 0.14f * b;
                Renderer bat = Piece("SkyBat_" + b, Moon, Vector3.zero, batMesh, batMaterial);
                bat.gameObject.SetActive(false);
                _bats.Add(bat.transform);
            }

            if (Crafted)
            {
                _streakMesh = Own(SkyMeshes.Streak(Palette.StarLight, out _streakVertices));
                _streakMaterial = Paper(template, "Sky_Streak", Texture2D.whiteTexture, true, Color.clear);
                _streak = Piece("ShootingStar", transform, Vector3.zero, _streakMesh, _streakMaterial).gameObject;
                _streak.SetActive(false);
            }

            Apply();
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return; // paused: timeScale 0
            }

            Schedule.Advance(dt, AmbientMotion.ReducedMotion);
            Apply();
        }

        private void Apply()
        {
            for (int i = 0; i < _layers.Count; i++)
            {
                float angle = SkyModel.CloudAngle(Schedule.CloudClock, SkyModel.Layers[i].DegreesPerSecond, _layerStart[i]);
                _layers[i].localRotation = Quaternion.Euler(0f, angle, 0f);
            }

            if (_domeMaterial != null)
            {
                _domeMaterial.SetFloat(SkyTimeId, (float)Schedule.TwinkleClock);
            }

            ApplyBats();
            ApplyShootingStar();
        }

        private void ApplyBats()
        {
            BatFlight flight = Schedule.Flight;
            for (int b = 0; b < _bats.Count; b++)
            {
                Transform bat = _bats[b];
                bool show = Schedule.BatsActive && b < flight.Count;
                float x = 0f, y = 0f, flap = 0f;
                if (show)
                {
                    SkyModel.BatPose(flight, b, Schedule.BatProgress, out x, out y, out flap);
                    show = Mathf.Abs(x) <= SkyModel.BatTravelRadii;
                }

                if (bat.gameObject.activeSelf != show)
                {
                    bat.gameObject.SetActive(show);
                }

                if (!show)
                {
                    continue;
                }

                // Wings fold as a width wobble; the body rocks with each beat and banks into the flight.
                float size = _batSize[b];
                bat.localPosition = new Vector3(x * _moonRadius, y * _moonRadius, -BatDepth - 0.05f * b);
                bat.localRotation = Quaternion.Euler(0f, 0f, -6f * flight.Direction + 8f * flap);
                bat.localScale = new Vector3(size * (0.72f + 0.28f * flap), size * (1f - 0.08f * flap), 1f);
            }
        }

        private void ApplyShootingStar()
        {
            if (_streak == null)
            {
                return;
            }

            bool show = Schedule.ShootingStarActive;
            if (_streak.activeSelf != show)
            {
                _streak.SetActive(show);
            }

            if (!show)
            {
                return;
            }

            if (_seenStars != Schedule.ShootingStarsStarted)
            {
                _seenStars = Schedule.ShootingStarsStarted;
                _starAzimuth = ViewAzimuth();
            }

            float p = Schedule.ShootingStarProgress;
            ShootingStarArc arc = Schedule.Arc;
            Vector3 head = DomePoint(_starAzimuth + arc.AzimuthAt(p), arc.ElevationAt(p), ShootingStarRadius);
            Vector3 before = DomePoint(_starAzimuth + arc.AzimuthAt(p - 0.05f), arc.ElevationAt(p - 0.05f),
                ShootingStarRadius);
            Vector3 travel = (head - before).normalized;
            Vector3 forward = (head - GreyboxBuilder.SkyDomeCentre).normalized;
            // Local +X follows the travel direction, local −Z faces back into the dome.
            _streak.transform.SetPositionAndRotation(head, Quaternion.LookRotation(forward, Vector3.Cross(forward, travel)));
            _streakVertices[0] = new Vector3(-SkyMeshes.StreakLength * SkyModel.ShootingStarTrail(p), 0f, 0f);
            _streakMesh.SetVertices(_streakVertices);
            _streakMaterial.SetColor(BaseColorId, new Color(1f, 1f, 1f, SkyModel.ShootingStarGlow(p)));
        }

        private float ViewAzimuth()
        {
            Transform view = View;
            if (view == null && Camera.main != null)
            {
                view = Camera.main.transform;
            }

            if (view == null)
            {
                return 0f;
            }

            Vector3 forward = view.forward;
            return forward.x * forward.x + forward.z * forward.z < 1e-4f ? 0f : Mathf.Atan2(forward.x, forward.z) * Mathf.Rad2Deg;
        }

        private Material Paper(Material template, string materialName, Texture texture, bool blended, Color rim)
        {
            var material = new Material(template) { name = materialName };
            material.SetTexture(BaseMapId, texture);
            material.SetColor(BaseColorId, Color.white);
            material.SetColor(RimColorId, rim);
            material.SetVector(MoonPositionId, Atmosphere.MoonPosition);
            material.SetFloat(MaxValueId, SkyModel.BrightestValue);
            material.SetFloat(SrcBlendId, (float)(blended ? BlendMode.SrcAlpha : BlendMode.One));
            material.SetFloat(DstBlendId, (float)(blended ? BlendMode.OneMinusSrcAlpha : BlendMode.Zero));
            material.SetFloat(ZWriteId, blended ? 0f : 1f);
            material.SetFloat(CullId, (float)CullMode.Off);
            material.SetOverrideTag("RenderType", blended ? "Transparent" : "Opaque");
            material.renderQueue = (int)(blended ? RenderQueue.Transparent : RenderQueue.Geometry);
            return Own(material);
        }

        private static Renderer Piece(string pieceName, Transform parent, Vector3 localPosition, Mesh mesh, Material material)
        {
            var go = new GameObject(pieceName, typeof(MeshFilter), typeof(MeshRenderer));
            go.transform.SetParent(parent, false);
            go.transform.localPosition = localPosition;
            go.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = go.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.lightProbeUsage = LightProbeUsage.Off;
            renderer.reflectionProbeUsage = ReflectionProbeUsage.Off;
            return renderer;
        }

        /// <summary>
        /// The committed material for <paramref name="shaderName"/>, or null when the material or a working shader
        /// is missing (stripped, failed to compile, unsupported on the device).
        /// </summary>
        private static Material LoadCrafted(string path, string shaderName)
        {
            var material = Resources.Load<Material>(path);
            if (material == null || material.shader == null || !material.shader.isSupported ||
                material.shader.name != shaderName)
            {
                return null;
            }

            return material;
        }

        private static Texture2D NewTexture(string textureName, int width, int height, TextureFormat format, bool linear,
            TextureWrapMode wrapU, TextureWrapMode wrapV, byte[] pixels)
        {
            var texture = new Texture2D(width, height, format, false, linear)
            {
                name = textureName,
                wrapModeU = wrapU,
                wrapModeV = wrapV,
                filterMode = FilterMode.Bilinear,
            };
            texture.LoadRawTextureData(pixels);
            texture.Apply(false, true);
            return texture;
        }

        private T Own<T>(T item)
            where T : Object
        {
            if (item != null)
            {
                _owned.Add(item);
            }

            return item;
        }

        private void OnDestroy()
        {
            for (int i = 0; i < _owned.Count; i++)
            {
                if (_owned[i] != null)
                {
                    Destroy(_owned[i]);
                }
            }

            _owned.Clear();
        }
    }
}
