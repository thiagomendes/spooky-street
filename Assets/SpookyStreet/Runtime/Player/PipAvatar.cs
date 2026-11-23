using SpookyStreet.Core.Animation;
using SpookyStreet.Core.Movement;
using SpookyStreet.World;
using UnityEngine;

namespace SpookyStreet.Player
{
    /// <summary>
    /// Pip's greybox body (spec §2): a child in a homemade moth costume under a short rain cape, built from
    /// palette-tinted primitives so the silhouette reads from the gameplay camera (behind and slightly above).
    /// Antennae are ~30% larger than life on purpose. Procedural motion: breathing idle, gait bob and roll,
    /// lagging antenna sway that settles when Pip stops. Never cloth silver (June's clue colour).
    /// When the original rigged model (tools/blender, imported by Editor/ModelImportRules.cs) is present at
    /// <see cref="ModelPath"/>, Pip is that model instead: URP Lit materials from its baked textures, scaled to a
    /// child's height with feet on Pip's origin, animated by <see cref="PipAnimator"/>; the primitives are not built.
    /// Without the model nothing is logged and the primitive avatar is used exactly as before.
    /// </summary>
    public sealed class PipAvatar : MonoBehaviour
    {
        /// <summary>Resources folder of Pip's model and its baked textures.</summary>
        public const string ModelFolder = "Models/Characters/Pip";
        public const string ModelName = "Pip";
        public const string ModelPath = ModelFolder + "/" + ModelName;

        /// <summary>Optional face texture names, applied to renderers whose name contains "Face".</summary>
        public static readonly string[] FaceTextureNames = { "Pip_Face", "Pip_FaceMap" };

        [Header("Imported model")]
        [Tooltip("Yaw added to the model so its face looks down Pip's +Z (depends on the FBX export axes).")]
        [SerializeField] private float modelYawOffset = 180f;
        [Tooltip("A model whose height (antennae included) is outside this range is rescaled to the target height.")]
        [SerializeField] private Vector2 nativeHeightRange = new Vector2(1.0f, 1.6f);
        [SerializeField] private float modelTargetHeight = 1.2f;

        [SerializeField] private float idleBobHz = 0.6f;
        [SerializeField] private float idleBobFraction = 0.02f;
        [SerializeField] private float stepHz = 2.5f;
        [SerializeField] private float stepBob = 0.045f;
        [SerializeField] private float stepRollDegrees = 3f;
        [SerializeField] private float antennaSwayDegrees = 6f;

        [Header("Moveset squash (volume-preserving, eased out)")]
        [Tooltip("Wide-and-flat kick when the bat cape spreads.")]
        [SerializeField] private float glideSquash = 0.05f;
        [Tooltip("Tuck kick when a roll starts.")]
        [SerializeField] private float rollSquash = 0.04f;

        private Transform _body;
        private Transform[] _antennae;
        private Quaternion[] _antennaRest;
        private PipMotor _motor;
        private float _maxSpeed = 4f;
        private float _gait;
        private float _phase;
        private readonly SquashPulse _squash = new SquashPulse();
        private float _holdStretch;

        public Transform Body => _body;

        /// <summary>True when Pip is the imported, animated model rather than the primitive fallback.</summary>
        public bool UsesModel { get; private set; }

        /// <summary>The model's animation driver; null for the primitive avatar or a model without clips.</summary>
        public PipAnimator Animator { get; private set; }

        /// <summary>Uniform scale applied to the imported model (1 when it was authored at game size).</summary>
        public float ModelScale { get; private set; } = 1f;

        public void Build(MaterialLibrary materials, PipMovementTuning tuning)
        {
            _motor = GetComponent<PipMotor>();
            if (_motor != null)
            {
                _motor.Stepped -= OnMotorStep;
                _motor.Stepped += OnMotorStep;
            }

            _maxSpeed = Mathf.Max(0.1f, tuning.maxSpeed);
            float s = tuning.height / 1.15f;

            _body = new GameObject("PipBody").transform;
            _body.SetParent(transform, false);
            if (TryBuildModel(materials))
            {
                return;
            }

            // Legs and feet (dark tights).
            for (int side = -1; side <= 1; side += 2)
            {
                Part(PrimitiveType.Capsule, "Leg", new Vector3(side * 0.09f, 0.12f, 0f) * s, new Vector3(0.14f, 0.11f, 0.14f) * s,
                    Quaternion.Euler(0f, 0f, side * 3f), materials.Get(Palette.PipLegs));
            }

            // Fuzzy moth torso.
            Part(PrimitiveType.Sphere, "Torso", new Vector3(0f, 0.47f, 0f) * s, new Vector3(0.42f, 0.5f, 0.36f) * s,
                Quaternion.identity, materials.Get(Palette.PipFuzz));

            // Wings poke out below the cape; ink eyespots echo the round attic window (spec §6 shape rhyme).
            for (int side = -1; side <= 1; side += 2)
            {
                var wing = Part(PrimitiveType.Sphere, "Wing", new Vector3(side * 0.22f, 0.55f, -0.17f) * s,
                    new Vector3(0.36f, 0.5f, 0.05f) * s, Quaternion.Euler(0f, side * 25f, side * -12f),
                    materials.Get(Palette.PipWing));
                var spot = Part(PrimitiveType.Sphere, "WingEyespot", Vector3.zero, Vector3.one, Quaternion.identity,
                    materials.Get(Palette.Ink));
                spot.SetParent(wing, false);
                spot.localPosition = new Vector3(0f, 0.05f, -0.6f);
                spot.localScale = new Vector3(0.42f, 0.3f, 0.4f);
            }

            // Short amber rain cape draped over the shoulders and back, with its hood up: from the gameplay camera
            // (behind) Pip reads as a small hooded figure, the warm focal point against the indigo street.
            Part(PrimitiveType.Sphere, "RainCape", new Vector3(0f, 0.69f, -0.04f) * s, new Vector3(0.6f, 0.44f, 0.54f) * s,
                Quaternion.Euler(-6f, 0f, 0f), materials.Get(Palette.PipCape));
            Part(PrimitiveType.Sphere, "Hood", new Vector3(0f, 0.97f, -0.02f) * s, new Vector3(0.4f, 0.4f, 0.4f) * s,
                Quaternion.identity, materials.Get(Palette.PipCape));
            Part(PrimitiveType.Sphere, "Face", new Vector3(0f, 0.95f, 0.13f) * s, new Vector3(0.27f, 0.25f, 0.14f) * s,
                Quaternion.identity, materials.Get(Palette.PipFace));
            for (int side = -1; side <= 1; side += 2)
            {
                Part(PrimitiveType.Sphere, "Eye", new Vector3(side * 0.055f, 0.97f, 0.2f) * s, Vector3.one * 0.05f * s,
                    Quaternion.identity, materials.Get(Palette.Ink));
            }

            // Soft antennae with feathered tips, pivoting at the hood so they can sway.
            _antennae = new Transform[2];
            _antennaRest = new Quaternion[2];
            for (int i = 0; i < 2; i++)
            {
                int side = i == 0 ? -1 : 1;
                var pivot = new GameObject("AntennaPivot").transform;
                pivot.SetParent(_body, false);
                pivot.localPosition = new Vector3(side * 0.07f, 1.12f, 0.02f) * s;
                pivot.localRotation = Quaternion.Euler(-8f, 0f, -side * 20f);
                var stem = Part(PrimitiveType.Cylinder, "AntennaStem", new Vector3(0f, 0.12f, 0f) * s,
                    new Vector3(0.03f, 0.12f, 0.03f) * s, Quaternion.identity, materials.Get(Palette.Ink));
                stem.SetParent(pivot, false);
                var tip = Part(PrimitiveType.Sphere, "AntennaTip", new Vector3(0f, 0.25f, 0f) * s,
                    new Vector3(0.12f, 0.07f, 0.04f) * s, Quaternion.Euler(0f, 0f, side * 25f), materials.Get(Palette.PipFace));
                tip.SetParent(pivot, false);
                _antennae[i] = pivot;
                _antennaRest[i] = pivot.localRotation;
            }

            // Satchel on the hip with the little torch clipped on.
            Part(PrimitiveType.Cube, "Satchel", new Vector3(0.24f, 0.42f, -0.02f) * s, new Vector3(0.12f, 0.16f, 0.2f) * s,
                Quaternion.Euler(0f, 0f, -8f), materials.Get(Palette.Timber));
            Part(PrimitiveType.Sphere, "Torch", new Vector3(0.31f, 0.5f, 0.07f) * s, Vector3.one * 0.06f * s,
                Quaternion.identity, materials.Glow(Palette.PumpkinAmber, 1.5f));
        }

        private bool TryBuildModel(MaterialLibrary materials)
        {
            var kit = new ModelKit(materials, ModelFolder);
            var prefab = kit.Load(ModelName);
            if (prefab == null)
            {
                return false;
            }

            var model = Instantiate(prefab, _body, false);
            model.name = "PipModel";
            model.transform.localRotation = Quaternion.Euler(0f, modelYawOffset, 0f);
            ModelKit.SetLayer(model.transform, gameObject.layer);

            Material skin = kit.MaterialFor(ModelName);
            Material face = null;
            foreach (var faceName in FaceTextureNames)
            {
                var texture = kit.Texture(faceName);
                if (texture != null)
                {
                    face = materials.Textured(texture, null);
                    break;
                }
            }

            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                bool isFace = face != null &&
                              renderer.gameObject.name.IndexOf("Face", System.StringComparison.OrdinalIgnoreCase) >= 0;
                MaterialLibrary.ApplyAll(renderer, isFace ? face : skin);
            }

            FitHeight(model);

            var clips = Resources.LoadAll<AnimationClip>(ModelPath);
            var animator = model.GetComponentInChildren<UnityEngine.Animator>();
            if (animator == null)
            {
                animator = model.AddComponent<UnityEngine.Animator>();
            }

            var driver = model.AddComponent<PipAnimator>();
            if (driver.Build(animator, clips, _motor, _maxSpeed))
            {
                Animator = driver;
            }
            else
            {
                Destroy(driver);
                if (Debug.isDebugBuild)
                {
                    Debug.Log($"[PipAvatar] Pip model has no {PipClips.Required} clip ({clips.Length} clips); " +
                              "showing its bind pose.");
                }
            }

            UsesModel = true;
            if (Debug.isDebugBuild)
            {
                var baseMap = skin != null && skin.HasProperty("_BaseMap") ? skin.GetTexture("_BaseMap") : null;
                Debug.Log($"[PipAvatar] skin material '{(skin != null ? skin.name : "none")}' shader " +
                          $"'{(skin != null ? skin.shader.name : "none")}', base map " +
                          $"{(baseMap != null ? $"{baseMap.name} {baseMap.width}x{baseMap.height} {baseMap.graphicsFormat}" : "MISSING")}, " +
                          $"face {(face != null ? face.name : "none")}");
                Debug.Log($"[PipAvatar] model: {clips.Length} clips, scale {ModelScale:F3}, " +
                          $"height {ModelKit.RendererBounds(model).size.y:F2} m, animated {Animator != null}");
            }

            return true;
        }

        /// <summary>
        /// Keeps a model authored at game size as it is, rescales one authored at another unit scale to a child's
        /// height, then puts its lowest point on Pip's origin (the capsule's base).
        /// </summary>
        private void FitHeight(GameObject model)
        {
            Bounds bounds = ModelKit.RendererBounds(model);
            float height = bounds.size.y;
            if (height > 0.01f && (height < nativeHeightRange.x || height > nativeHeightRange.y))
            {
                ModelScale = modelTargetHeight / height;
                model.transform.localScale *= ModelScale;
                bounds = ModelKit.RendererBounds(model);
            }

            float lift = transform.position.y - bounds.min.y;
            if (height > 0.01f && Mathf.Abs(lift) > 0.02f)
            {
                model.transform.position += Vector3.up * lift;
            }
        }

        private Transform Part(PrimitiveType type, string name, Vector3 position, Vector3 scale, Quaternion rotation,
            Material material)
        {
            var go = GameObject.CreatePrimitive(type);
            go.name = name;
            Destroy(go.GetComponent<Collider>());
            go.layer = gameObject.layer;
            go.transform.SetParent(_body, false);
            go.transform.localPosition = position;
            go.transform.localRotation = rotation;
            go.transform.localScale = scale;
            var renderer = go.GetComponent<Renderer>();
            renderer.sharedMaterial = material;
            return go.transform;
        }

        /// <summary>Current squash value (negative = stretched tall), for tests and diagnostics.</summary>
        public float SquashValue { get; private set; }

        private void OnMotorStep(MotorStep step)
        {
            // Readability (S000.6 feel pass, MoveFeedbackRules): chained jumps stretch more at each stage and land
            // with a growing squash; a dive snaps the cape back.
            if (step.Jumped)
            {
                _squash.Kick(MoveFeedbackRules.TakeOffStretch(step.ChainStage));
            }
            else if (step.WallJumped)
            {
                _squash.Kick(MoveFeedbackRules.WallKickStretch);
            }
            else if (step.LedgeGrabbed)
            {
                _squash.Kick(MoveFeedbackRules.LedgeCatchSquash);
            }
            else if (step.WallSlideStarted)
            {
                _squash.Kick(MoveFeedbackRules.WallGripSquash);
            }
            else if (step.DiveStarted)
            {
                _squash.Kick(MoveFeedbackRules.DiveSnapStretch);
            }
            else if (step.BoostStarted)
            {
                // The high jump's boost: the cape flares up as Pip shoots up.
                _squash.Kick(MoveFeedbackRules.BoostFlareStretch);
            }
            else if (step.GlideStarted)
            {
                _squash.Kick(glideSquash);
            }
            else if (step.RollStarted)
            {
                _squash.Kick(rollSquash);
            }
            else if (step.Landed && _motor != null)
            {
                _squash.Kick(MoveFeedbackRules.LandingSquash(_motor.ChainStage));
            }
        }

        private void OnDestroy()
        {
            if (_motor != null)
            {
                _motor.Stepped -= OnMotorStep;
            }
        }

        private void LateUpdate()
        {
            if (_body == null)
            {
                return;
            }

            // Squash from the feet (the body's origin), so the cape spreads without lifting Pip off the ground.
            // A held jump keeps a small extra stretch while it rises, so a hold reads differently from a tap.
            float holdTarget = _motor != null && _motor.HoldingJumpRise ? MoveFeedbackRules.HeldJumpStretch : 0f;
            _holdStretch = Mathf.MoveTowards(_holdStretch, holdTarget,
                MoveFeedbackRules.HeldJumpStretchRate * Time.deltaTime);
            SquashValue = MoveFeedbackRules.CombinedSquash(_squash.Step(Time.deltaTime), _holdStretch);
            SquashPulse.ToScale(SquashValue, out float wide, out float tall);
            _body.localScale = new Vector3(wide, tall, wide);

            // The imported model's clips carry breathing, gait and antenna motion; its wall and ledge clips are
            // authored against the motor's own hang spot (ClimbRules), so the body needs no offset while hanging.
            if (UsesModel)
            {
                return;
            }

            float dt = Time.deltaTime;
            float speedN = _motor != null ? Mathf.Clamp01(_motor.HorizontalSpeed / _maxSpeed) : 0f;
            bool grounded = _motor == null || _motor.Grounded;
            // Blend gait in/out over ~0.3 s so stopping settles instead of freezing.
            _gait = Mathf.MoveTowards(_gait, grounded ? speedN : 0f, dt / 0.3f);
            _phase += dt * stepHz * Mathf.PI * 2f * Mathf.Max(_gait, 0.0001f);

            float t = Time.time;
            float idle = Mathf.Sin(t * idleBobHz * Mathf.PI * 2f) * idleBobFraction * 1.15f * (1f - _gait);
            float step = Mathf.Abs(Mathf.Sin(_phase)) * stepBob * _gait;
            _body.localPosition = new Vector3(0f, idle + step, 0f);
            _body.localRotation = Quaternion.Euler(4f * _gait, 0f, Mathf.Sin(_phase * 0.5f) * stepRollDegrees * _gait);

            for (int i = 0; i < _antennae.Length; i++)
            {
                float sway = Mathf.Sin(t * 1.8f + i * 1.3f) * antennaSwayDegrees * (0.4f + _gait);
                _antennae[i].localRotation = _antennaRest[i] * Quaternion.Euler(-sway * 0.6f * _gait, 0f, sway);
            }
        }
    }
}
