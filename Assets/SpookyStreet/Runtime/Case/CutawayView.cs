using System;
using SpookyStreet.Core.Art;
using SpookyStreet.UI;
using Unity.Cinemachine;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UiKeys = SpookyStreet.Core.Localization.LocalizationKeys.CaseUi;

namespace SpookyStreet.Case
{
    /// <summary>
    /// The test-crank cutaway (spec §3: at most four seconds, skippable; only when <c>CaseStep.PlayCutaway</c>): a
    /// fixed camera looks into the workshop nook with its near wall hidden, so the cam turning, the follower lifting
    /// on each of the three knocks and the reseated linkage are in frame, while a felt-framed inset shows the rooftop
    /// owl bowing on the same knocks. The inset camera renders on request into its own small texture (it is never a
    /// second full-screen render). A tap, Use, Jump, Enter or the Skip button ends it early. Runs on game time.
    /// </summary>
    public sealed class CutawayView : MonoBehaviour
    {
        public const float MaxSeconds = 4f;
        public const float FieldOfView = 46f;

        private CinemachineCamera _vcam;
        private CinemachineBrain _brain;
        private Camera _inset;
        private RenderTexture _insetTexture;
        private RectTransform _ui;
        private InputActionMap _map;
        private InputAction _skip;
        private float _left;
        private bool _armed;
        private Action _ended;

        public bool Active { get; private set; }

        public float Elapsed => MaxSeconds - _left;

        public Transform CameraTransform => _vcam.transform;

        public Camera InsetCamera => _inset;

        public GameObject Overlay => _ui.gameObject;

        public Button SkipButton { get; private set; }

        public static CutawayView Create(Transform parent, Camera output, RectTransform canvasRoot)
        {
            var go = new GameObject("CutawayView");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<CutawayView>();
            view.Build(output, canvasRoot);
            return view;
        }

        private void Build(Camera output, RectTransform canvasRoot)
        {
            var cameraObject = new GameObject("CutawayCamera");
            cameraObject.transform.SetParent(transform, false);
            cameraObject.SetActive(false);
            _vcam = cameraObject.AddComponent<CinemachineCamera>();
            var lens = _vcam.Lens;
            lens.FieldOfView = FieldOfView;
            lens.NearClipPlane = 0.05f;
            lens.FarClipPlane = 120f;
            _vcam.Lens = lens;
            _brain = output != null ? output.GetComponent<CinemachineBrain>() : null;

            // No render target without a graphics device (batchmode -nographics PlayMode runs).
            if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Null)
            {
                _insetTexture = new RenderTexture(512, 320, 16, RenderTextureFormat.ARGB32) { name = "OwlInset" };
            }

            var insetObject = new GameObject("OwlInsetCamera");
            insetObject.transform.SetParent(transform, false);
            _inset = insetObject.AddComponent<Camera>();
            _inset.enabled = false; // rendered on request only
            _inset.targetTexture = _insetTexture;
            _inset.fieldOfView = 30f;
            _inset.nearClipPlane = 0.1f;
            _inset.farClipPlane = 80f;
            _inset.clearFlags = CameraClearFlags.SolidColor;
            _inset.backgroundColor = Palette.NightViolet;
            if (output != null)
            {
                _inset.cullingMask = output.cullingMask;
            }

            // Overlay: a felt-framed picture of the owl, top left, and Skip at top right.
            _ui = UiFactory.Stretch(UiFactory.Rect(canvasRoot, "Cutaway"));
            var catcher = UiFactory.Panel(_ui, "TapCatcher", new Color(0f, 0f, 0f, 0f));
            var catcherButton = catcher.gameObject.AddComponent<Button>();
            catcherButton.transition = Selectable.Transition.None;
            catcherButton.onClick.AddListener(Skip);
            var content = UiFactory.Stretch(UiFactory.Rect(_ui, "Content"));
            content.gameObject.AddComponent<SafeAreaFitter>();
            var frame = UiFactory.PaperCard(content, "OwlFrame", new Vector2(0f, 1f), new Vector2(250f, -170f),
                new Vector2(420f, 276f), 1.5f, Palette.CandyCornYellow);
            frame.raycastTarget = false;
            var picture = UiFactory.Rect(frame.transform, "OwlPicture");
            UiFactory.Stretch(picture);
            picture.offsetMin = new Vector2(18f, 18f);
            picture.offsetMax = new Vector2(-18f, -18f);
            var raw = picture.gameObject.AddComponent<RawImage>();
            raw.texture = _insetTexture;
            raw.raycastTarget = false;
            SkipButton = UiFactory.SecondaryButton(content, "SkipButton", UiKeys.DialogueSkip, Vector2.one,
                new Vector2(-136f, -62f), new Vector2(232f, 96f), Skip, UiSpriteCatalog.IconSkip);
            _ui.gameObject.SetActive(false);

            _map = new InputActionMap("Cutaway");
            _skip = _map.AddAction("Skip", InputActionType.Button, "<Keyboard>/e");
            _skip.AddBinding("<Keyboard>/space");
            _skip.AddBinding("<Keyboard>/enter");
            _skip.AddBinding("<Gamepad>/buttonSouth");
            _skip.AddBinding("<Gamepad>/buttonWest");
        }

        /// <summary>
        /// Plays the cutaway looking from <paramref name="eye"/> at <paramref name="target"/>, with the inset on the
        /// owl from <paramref name="owlEye"/>. <paramref name="ended"/> runs once when it ends (time or skip).
        /// </summary>
        public void Play(Vector3 eye, Vector3 target, Vector3 owlEye, Vector3 owl, Action ended)
        {
            _ended = ended;
            _vcam.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(target - eye, Vector3.up));
            _inset.transform.SetPositionAndRotation(owlEye, Quaternion.LookRotation(owl - owlEye, Vector3.up));
            if (_brain != null)
            {
                _brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.Cut, 0f);
            }

            _vcam.gameObject.SetActive(true);
            _ui.gameObject.SetActive(true);
            _ui.SetAsLastSibling();
            _map.Enable();
            _armed = !_skip.IsPressed();
            _left = MaxSeconds;
            Active = true;
            RenderInset();
        }

        public void Skip()
        {
            if (Active)
            {
                End();
            }
        }

        /// <summary>Ends now; <paramref name="notify"/> false is a silent cancel (back to title, replay).</summary>
        public void End(bool notify = true)
        {
            if (!Active)
            {
                return;
            }

            Active = false;
            _map.Disable();
            _vcam.gameObject.SetActive(false);
            _ui.gameObject.SetActive(false);
            if (_brain != null)
            {
                _brain.DefaultBlend = new CinemachineBlendDefinition(CinemachineBlendDefinition.Styles.EaseInOut,
                    BenchView.BlendSeconds);
            }

            var ended = _ended;
            _ended = null;
            if (notify)
            {
                ended?.Invoke();
            }
        }

        /// <summary>Renders the owl inset now (each frame while active; tests call it before a capture).</summary>
        public void RenderInset()
        {
            if (_insetTexture == null)
            {
                return;
            }

            var request = new RenderPipeline.StandardRequest { destination = _insetTexture };
            if (RenderPipeline.SupportsRenderRequest(_inset, request))
            {
                RenderPipeline.SubmitRenderRequest(_inset, request);
            }
        }

        private void Update()
        {
            if (!Active)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return; // paused
            }

            if (!_skip.IsPressed())
            {
                _armed = true;
            }

            _left -= dt;
            if (_left <= 0f || (_armed && _skip.WasPressedThisFrame()))
            {
                End();
            }
        }

        private void LateUpdate()
        {
            if (Active)
            {
                RenderInset();
            }
        }

        private void OnDestroy()
        {
            _map?.Dispose();
            if (_insetTexture != null)
            {
                _insetTexture.Release();
                Destroy(_insetTexture);
            }
        }
    }
}
