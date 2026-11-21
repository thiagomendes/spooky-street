using SpookyStreet.World;
using UnityEngine;

namespace SpookyStreet.Player
{
    /// <summary>
    /// Pip's greybox body (spec §2): a child in a homemade moth costume under a short rain cape, built from
    /// palette-tinted primitives so the silhouette reads from the gameplay camera (behind and slightly above).
    /// Antennae are ~30% larger than life on purpose. Procedural motion: breathing idle, gait bob and roll,
    /// lagging antenna sway that settles when Pip stops. Never cloth silver (June's clue colour).
    /// </summary>
    public sealed class PipAvatar : MonoBehaviour
    {
        /// <summary>Pip's game model (built by tools/blender/build_pip.py from a CC0 KayKit base).</summary>
        public const string ModelFolder = "Models/Characters/Pip/";
        public const string ModelPath = ModelFolder + "Pip";

        [SerializeField] private float modelYawOffset;
        [SerializeField] private float modelHeightFactor = 1.1f;

        [SerializeField] private float idleBobHz = 0.6f;
        [SerializeField] private float idleBobFraction = 0.02f;
        [SerializeField] private float stepHz = 2.5f;
        [SerializeField] private float stepBob = 0.045f;
        [SerializeField] private float stepRollDegrees = 3f;
        [SerializeField] private float antennaSwayDegrees = 6f;

        private Transform _body;
        private Transform[] _antennae;
        private Quaternion[] _antennaRest;
        private PipMotor _motor;
        private float _maxSpeed = 4f;
        private float _gait;
        private float _phase;

        public Transform Body => _body;

        /// <summary>True when Pip is the imported, animated model rather than the primitive fallback.</summary>
        public bool UsesModel { get; private set; }

        public PipAnimator Animator { get; private set; }

        public Transform[] Wings { get; private set; } = new Transform[0];

        public void Build(MaterialLibrary materials, PipMovementTuning tuning)
        {
            _motor = GetComponent<PipMotor>();
            _maxSpeed = Mathf.Max(0.1f, tuning.maxSpeed);
            float s = tuning.height / 1.15f;

            _body = new GameObject("PipBody").transform;
            _body.SetParent(transform, false);
            if (TryBuildModel(materials, tuning))
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

        private bool TryBuildModel(MaterialLibrary materials, PipMovementTuning tuning)
        {
            var prefab = Resources.Load<GameObject>(ModelPath);
            if (prefab == null)
            {
                return false;
            }

            var clips = Resources.LoadAll<AnimationClip>(ModelPath);
            var model = Instantiate(prefab, _body, false);
            model.name = "PipModel";
            model.transform.localRotation = Quaternion.Euler(0f, modelYawOffset, 0f);
            SetLayer(model.transform, gameObject.layer);

            var cape = materials.Textured(Resources.Load<Texture2D>(ModelFolder + "pip_cape"));
            var fuzz = materials.Textured(Resources.Load<Texture2D>(ModelFolder + "pip_fuzz"));
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                string part = renderer.gameObject.name;
                Material material = part.StartsWith("Pip_AntennaTip") ? materials.Get(Palette.WarmPaper)
                    : part.StartsWith("Pip_Antenna") || part.StartsWith("Pip_WingSpot") ? materials.Get(Palette.Ink)
                    : part.StartsWith("Pip_Wing") ? materials.Get(Palette.PipWing)
                    : part == "Rogue_Body" || part.StartsWith("Rogue_Arm") ? fuzz
                    : cape;
                MaterialLibrary.ApplyAll(renderer, material);
                if (renderer is SkinnedMeshRenderer skinned)
                {
                    skinned.updateWhenOffscreen = true;
                }
            }

            // Scale the chibi model so its hood top sits at about the capsule height, feet on Pip's origin.
            Bounds bounds = BodyBounds(model);
            float height = Mathf.Max(0.01f, bounds.size.y);
            float scale = tuning.height * modelHeightFactor / height;
            model.transform.localScale = Vector3.one * scale;
            bounds = BodyBounds(model);
            model.transform.position += Vector3.up * (transform.position.y - bounds.min.y);

            var antennae = new System.Collections.Generic.List<Transform>();
            var wings = new System.Collections.Generic.List<Transform>();
            foreach (var t in model.GetComponentsInChildren<Transform>(true))
            {
                if (t.name == "Pip_Antenna_L" || t.name == "Pip_Antenna_R")
                {
                    antennae.Add(t);
                }
                else if (t.name == "Pip_Wing_L" || t.name == "Pip_Wing_R")
                {
                    wings.Add(t);
                }
            }

            _antennae = antennae.ToArray();
            _antennaRest = new Quaternion[_antennae.Length];
            for (int i = 0; i < _antennae.Length; i++)
            {
                _antennaRest[i] = _antennae[i].localRotation;
            }

            Wings = wings.ToArray();
            var animator = model.GetComponentInChildren<UnityEngine.Animator>();
            if (animator == null)
            {
                animator = model.AddComponent<UnityEngine.Animator>();
            }

            if (PipAnimator.HasRequiredClips(clips))
            {
                Animator = model.AddComponent<PipAnimator>();
                Animator.Build(animator, clips, _motor, _maxSpeed);
            }
            else
            {
                Debug.LogWarning($"[PipAvatar] Pip model has {clips.Length} clips but not the locomotion set.");
            }

            UsesModel = true;
            Debug.Log($"[PipAvatar] model: {clips.Length} clips, scale {scale:F3}, antennae {_antennae.Length}, " +
                      $"wings {Wings.Length}, animated {Animator != null}");
            return true;
        }

        private static Bounds BodyBounds(GameObject model)
        {
            bool any = false;
            var bounds = new Bounds(model.transform.position, Vector3.zero);
            foreach (var renderer in model.GetComponentsInChildren<Renderer>(true))
            {
                if (renderer.gameObject.name.StartsWith("Pip_Antenna"))
                {
                    continue;
                }

                if (!any)
                {
                    bounds = renderer.bounds;
                    any = true;
                }
                else
                {
                    bounds.Encapsulate(renderer.bounds);
                }
            }

            return bounds;
        }

        private static void SetLayer(Transform root, int layer)
        {
            root.gameObject.layer = layer;
            for (int i = 0; i < root.childCount; i++)
            {
                SetLayer(root.GetChild(i), layer);
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

        private void LateUpdate()
        {
            if (_body == null)
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
            if (!UsesModel)
            {
                // The primitive fallback has no clips: fake breathing and gait procedurally.
                float idle = Mathf.Sin(t * idleBobHz * Mathf.PI * 2f) * idleBobFraction * 1.15f * (1f - _gait);
                float step = Mathf.Abs(Mathf.Sin(_phase)) * stepBob * _gait;
                _body.localPosition = new Vector3(0f, idle + step, 0f);
                _body.localRotation = Quaternion.Euler(4f * _gait, 0f,
                    Mathf.Sin(_phase * 0.5f) * stepRollDegrees * _gait);
            }

            for (int i = 0; i < _antennae.Length; i++)
            {
                float sway = Mathf.Sin(t * 1.8f + i * 1.3f) * antennaSwayDegrees * (0.4f + _gait);
                _antennae[i].localRotation = _antennaRest[i] * Quaternion.Euler(-sway * 0.6f * _gait, 0f, sway);
            }
        }
    }
}
