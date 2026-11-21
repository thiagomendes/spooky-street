using SpookyStreet.CameraSystem;
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
    /// player entry point: localization → world → Pip → camera → UI → title screen.
    /// Owns pause semantics (spec §5): pausing suspends input, simulation and audio, and returning from the
    /// background opens paused.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public sealed class GameBootstrap : MonoBehaviour
    {
        [SerializeField] private PipMovementTuning movementTuning;
        [SerializeField] private bool leftHandedLayout;
        [SerializeField] private int targetFrameRate = 60;

        private GameInput _input;

        public static GameBootstrap Instance { get; private set; }

        public GameFlow Flow { get; private set; }

        public GameInput Controls => _input;

        public PipMotor Pip { get; private set; }

        public CameraRig CameraRig { get; private set; }

        public GameUi Ui { get; private set; }

        public BuildInfo BuildInfo { get; private set; }

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

            Transform world = new GreyboxBuilder().Build();
            Pip = CreatePip(movementTuning);

            int obstacleLayers = Physics.DefaultRaycastLayers & ~(1 << Pip.gameObject.layer);
            CameraRig = CameraRig.Create(Pip, movementTuning, _input, Flow, obstacleLayers);
            Pip.Configure(movementTuning, _input, Flow, CameraRig.OutputCamera.transform);

            Ui = GameUi.Create(Flow, _input, Pip, BuildInfo, leftHandedLayout);

            // Keep everything built at runtime in this scene so unloading it cleans up completely.
            Adopt(world.gameObject);
            Adopt(Pip.gameObject);
            Adopt(CameraRig.gameObject);
            Adopt(Ui.gameObject);
            if (EventSystem.current != null)
            {
                Adopt(EventSystem.current.gameObject);
            }

            Flow.StateChanged += OnStateChanged;
            ApplySuspension(Flow.State);
        }

        private void Adopt(GameObject root)
        {
            if (root.transform.parent == null && root.scene != gameObject.scene)
            {
                SceneManager.MoveGameObjectToScene(root, gameObject.scene);
            }
        }

        private static PipMotor CreatePip(PipMovementTuning tuning)
        {
            var pip = new GameObject("Pip") { tag = "Player" };
            int playerLayer = LayerMask.NameToLayer("Player");
            pip.layer = playerLayer >= 0 ? playerLayer : 0;
            pip.transform.SetPositionAndRotation(GreyboxBuilder.ArrivalSpawn, Quaternion.identity);
            pip.AddComponent<CharacterController>();
            pip.AddComponent<SafeGroundTracker>();
            var motor = pip.AddComponent<PipMotor>();

            // Greybox body: capsule with a satchel nub so facing reads from behind.
            var body = GameObject.CreatePrimitive(PrimitiveType.Capsule);
            body.name = "Body_Greybox";
            Destroy(body.GetComponent<Collider>());
            body.layer = pip.layer;
            body.transform.SetParent(pip.transform, false);
            body.transform.localScale = new Vector3(tuning.radius * 2f, tuning.height * 0.5f, tuning.radius * 2f);
            body.transform.localPosition = new Vector3(0f, tuning.height * 0.5f, 0f);
            body.GetComponent<Renderer>().material.color = Palette.ClothSilver;

            var satchel = GameObject.CreatePrimitive(PrimitiveType.Cube);
            satchel.name = "Satchel_Greybox";
            Destroy(satchel.GetComponent<Collider>());
            satchel.layer = pip.layer;
            satchel.transform.SetParent(pip.transform, false);
            satchel.transform.localScale = new Vector3(0.3f, 0.25f, 0.12f);
            satchel.transform.localPosition = new Vector3(0f, tuning.height * 0.55f, -tuning.radius - 0.04f);
            satchel.GetComponent<Renderer>().material.color = Palette.PumpkinAmber;
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
