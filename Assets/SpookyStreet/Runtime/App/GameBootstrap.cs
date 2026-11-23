using SpookyStreet.Audio;
using SpookyStreet.CameraSystem;
using SpookyStreet.Case;
using SpookyStreet.Characters;
using SpookyStreet.Core.Flow;
using SpookyStreet.Controls;
using SpookyStreet.Localization;
using SpookyStreet.Player;
using SpookyStreet.UI;
using SpookyStreet.World;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.SceneManagement;

namespace SpookyStreet.App
{
    /// <summary>
    /// The only object authored in the gameplay scene. Builds the S000 walking skeleton through the normal
    /// player entry point: localization → world (greybox, then the original environment models when committed) →
    /// atmosphere → Pip → camera → UI → title screen.
    /// Owns pause semantics (spec §5): pausing suspends input, simulation and audio, and returning from the
    /// background opens paused.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private PipMovementTuning movementTuning;
        [Tooltip("URP Lit base for every palette material. Must be a URP material referenced by the scene.")]
        [SerializeField] private Material greyboxMaterial;
        [Tooltip("URP Unlit base for glows and the painted sky card.")]
        [SerializeField] private Material glowMaterial;
        [SerializeField] private bool leftHandedLayout;
        [SerializeField] private int targetFrameRate = 60;

        private GameInput _input;

        public static GameBootstrap Instance { get; private set; }

        public GameFlow Flow { get; private set; }

        public GameInput Controls => _input;

        public PipMotor Pip { get; private set; }

        public CameraRig CameraRig { get; private set; }

        public GameUi Ui { get; private set; }

        /// <summary>Paper bursts, streaks and dust that make the moveset readable.</summary>
        public MoveFeedback Feedback { get; private set; }

        public BuildInfo BuildInfo { get; private set; }

        public MaterialLibrary Materials { get; private set; }

        /// <summary>The original environment models (owl house, porch kit, props) when committed.</summary>
        public EnvironmentDressing Environment { get; private set; }

        /// <summary>The handcrafted sky: paper moon, drifting clouds, twinkling stars, shooting stars and bats.</summary>
        public MagicSky Sky { get; private set; }

        /// <summary>Bea, her bicycle and Mr. Vale when their models are committed (ambient only until S002).</summary>
        public WitnessCast Witnesses { get; private set; }

        /// <summary>Case 01 in the world (S002): state, saves, interactions, dialogue, captions and notebook.</summary>
        public CaseDirector Director { get; private set; }

        /// <summary>The approved sound effects wired to play (S003): steps, moves, clue sounds, UI taps, the night bed.</summary>
        public GameAudio Audio { get; private set; }

        private void Awake()
        {
            // Last loaded scene wins; a reload (tests, "back to title" later) replaces the previous instance.
            Instance = this;
            Application.targetFrameRate = targetFrameRate;
            QualitySettings.vSyncCount = 0;

            Localizer.Initialize();
            BuildInfo = BuildInfo.Load();
            Debug.Log($"[SpookyStreet] Boot {Application.version} ({BuildInfo.Identifier}) Unity {Application.unityVersion}");

            if (movementTuning == null)
            {
                Debug.LogWarning("[SpookyStreet] No PipMovementTuning assigned; using defaults.");
                movementTuning = ScriptableObject.CreateInstance<PipMovementTuning>();
            }

            Flow = new GameFlow();
            _input = new GameInput();

            Materials = new MaterialLibrary(greyboxMaterial, glowMaterial);
            Environment = new EnvironmentDressing(Materials);
            Transform world = new GreyboxBuilder(Materials)
            {
                UseEnvironmentModels = Environment.Available,
                UseStreetSet = Environment.StreetSetAvailable,
            }.Build();
            Environment.Build(world);
            Sky = Atmosphere.Build(world, Materials, Environment.Anchors).GetComponentInChildren<MagicSky>();
            Pip = CreatePip(movementTuning, Materials);
            Witnesses = WitnessCast.Build(world, Materials, Pip.transform, Environment.UsesModels);

            int obstacleLayers = Physics.DefaultRaycastLayers & ~(1 << Pip.gameObject.layer);
            CameraRig = CameraRig.Create(Pip, movementTuning, _input, Flow, obstacleLayers);
            Pip.Configure(movementTuning, _input, Flow, CameraRig.OutputCamera.transform);

            if (Sky != null)
            {
                Sky.View = CameraRig.OutputCamera.transform; // shooting stars fall where the player is looking
            }

            Feedback = MoveFeedback.Create(Pip, Materials, CameraRig.OutputCamera.transform);
            Ui = GameUi.Create(Flow, _input, Pip, BuildInfo, leftHandedLayout);

            // Keep everything built at runtime in this scene so unloading it cleans up completely.
            Adopt(world.gameObject);
            Adopt(Pip.gameObject);
            Adopt(Feedback.gameObject);
            Adopt(CameraRig.gameObject);
            Adopt(Ui.gameObject);
            if (EventSystem.current != null)
            {
                Adopt(EventSystem.current.gameObject);
            }

            Flow.StateChanged += OnStateChanged;

            // After this component's own StateChanged handler, so Continue can move Pip after the new-game spawn.
            Director = CaseDirector.Create(this);
            Adopt(Director.gameObject);
            Audio = GameAudio.Create(this);
            Adopt(Audio.gameObject);
            ApplySuspension(Flow.State);
        }

        private void Adopt(GameObject root)
        {
            if (root.transform.parent == null && root.scene != gameObject.scene)
            {
                SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            }
        }

        private static PipMotor CreatePip(PipMovementTuning tuning, MaterialLibrary materials)
        {
            var pip = new GameObject("Pip") { tag = "Player" };
            int playerLayer = LayerMask.NameToLayer("Player");
            pip.layer = playerLayer >= 0 ? playerLayer : 0;
            pip.transform.SetPositionAndRotation(GreyboxBuilder.ArrivalSpawn, Quaternion.identity);
            pip.AddComponent<CharacterController>();
            pip.AddComponent<SafeGroundTracker>();
            var motor = pip.AddComponent<PipMotor>();

            pip.AddComponent<PipAvatar>().Build(materials, tuning);
            return motor;
        }

        private void Update()
        {
            if (_input.PausePressedThisFrame())
            {
                Flow.TogglePause();
            }
        }

        private void OnStateChanged(GameState previous, GameState current)
        {
            if (previous == GameState.Title && current == GameState.Playing)
            {
                // New game always starts on the arrival porch facing the owl house.
                Pip.Teleport(GreyboxBuilder.ArrivalSpawn);
                Pip.transform.rotation = Quaternion.identity;
            }

            ApplySuspension(current);
        }

        private void ApplySuspension(GameState state)
        {
            bool running = state == GameState.Playing;
            Time.timeScale = running ? 1f : 0f;
            AudioListener.pause = !running;
            _input.SetGameplayEnabled(running);
            Screen.sleepTimeout = running ? SleepTimeout.NeverSleep : SleepTimeout.SystemSetting;
        }

        private void OnApplicationPause(bool paused)
        {
            if (paused && Flow != null)
            {
                Flow.OnApplicationSuspended();
            }
        }

        private void OnApplicationFocus(bool hasFocus)
        {
            if (!hasFocus && Flow != null && !Application.isEditor)
            {
                Flow.OnApplicationSuspended();
            }
        }

        private void OnDestroy()
        {
            if (Flow != null)
            {
                Flow.StateChanged -= OnStateChanged;
            }

            _input?.Dispose();
            _input = null;
            if (Instance == this)
            {
                Time.timeScale = 1f;
                AudioListener.pause = false;
                Instance = null;
            }
        }
    }
}
