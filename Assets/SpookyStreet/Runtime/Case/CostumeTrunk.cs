using System;
using SpookyStreet.App;
using SpookyStreet.Core.Art;
using SpookyStreet.Core.Audio;
using SpookyStreet.Core.Case;
using SpookyStreet.Core.Keepsakes;
using SpookyStreet.Core.World;
using SpookyStreet.Player;
using SpookyStreet.UI;
using SpookyStreet.World;
using Unity.Cinemachine;
using UnityEngine;
using Verbs = SpookyStreet.Core.Localization.LocalizationKeys.Verbs;

namespace SpookyStreet.Case
{
    /// <summary>
    /// Pip's dress-up trunk on the arrival porch (S004 §2.1, stream A2): one of the moving boxes, the
    /// <c>DressUpTrunk</c> model with a solid box collider, its hinged lid (<see cref="KeepsakeArt.TrunkLid"/>, +105°
    /// about local X). Once a costume piece is earned it is a Use target ("Dress up", <see cref="KeepsakePorch"/>): the
    /// lid swings open, Pip turns to the street in front of it, a fixed camera frames her with the open trunk behind,
    /// and the <see cref="DressUpView"/> lists the earned pieces to put on or take off at once. The choice is saved with
    /// the keepsakes and shows in play and on the title (<see cref="PipCostume"/>). Until all three pieces are earned a
    /// plum cloth lies over the trunk's contents, so a piece not yet earned is never seen (no locked silhouettes,
    /// engagement §2.5).
    /// </summary>
    public sealed class CostumeTrunk : MonoBehaviour
    {
        public const float LidSeconds = 0.35f;
        public const float BlendSeconds = 0.5f;

        private Transform _lid;
        private Quaternion _lidRest;
        private float _lidOpen;
        private bool _wantOpen;
        private GameObject _cover;
        private CinemachineCamera _camera;

        /// <summary>The trunk's root in the world.</summary>
        public Transform Root { get; private set; }

        /// <summary>The trunk's collider (the box Pip stands on and walks around).</summary>
        public BoxCollider Collider { get; private set; }

        /// <summary>The Use target the director's targets offer (null until built).</summary>
        public ExtraUseTarget Target { get; private set; }

        /// <summary>True when the DressUpTrunk model was found.</summary>
        public bool HasModel { get; private set; }

        /// <summary>How open the lid is (0 shut, 1 open).</summary>
        public float LidOpen => _lidOpen;

        public bool CoverShown => _cover != null && _cover.activeSelf;

        public bool CameraActive => _camera != null && _camera.gameObject.activeSelf;

        public static CostumeTrunk Create(Transform parent, MaterialLibrary materials, Action interact)
        {
            var go = new GameObject("DressUpTrunk");
            go.transform.SetParent(parent, false);
            var trunk = go.AddComponent<CostumeTrunk>();
            trunk.Build(materials, interact);
            return trunk;
        }

        private void Build(MaterialLibrary materials, Action interact)
        {
            Root = new GameObject("TrunkRoot").transform;
            Root.SetParent(transform, false);
            Root.position = new Vector3(KeepsakePorch.TrunkX, KeepsakePorch.TrunkY, KeepsakePorch.TrunkZ);
            var kit = materials != null
                ? new ModelKit(materials, EnvironmentDressing.ModelFolder) { FallbackAtlas = KeepsakeArt.KeepsakesAtlas }
                : null;
            var model = kit != null ? kit.Spawn(KeepsakeArt.DressUpTrunk, Root, Vector3.zero) : null;
            if (model != null)
            {
                HasModel = true;
                _lid = ModelKit.FindDeep(model.transform, KeepsakeArt.TrunkLid);
                // The lid's hinge is on the back edge: turn the trunk so the back faces the porch's back (-Z) and the
                // front, with its bat label, faces the street.
                if (_lid != null && _lid.position.z > Root.position.z + 0.05f)
                {
                    model.transform.localRotation = Quaternion.Euler(0f, 180f, 0f) * model.transform.localRotation;
                }

                _lidRest = _lid != null ? _lid.localRotation : Quaternion.identity;
            }
            else
            {
                var box = GameObject.CreatePrimitive(PrimitiveType.Cube);
                box.name = "GreyboxTrunk";
                Destroy(box.GetComponent<Collider>());
                box.transform.SetParent(Root, false);
                box.transform.localPosition = new Vector3(0f, KeepsakeArt.TrunkSize * 0.5f, 0f);
                box.transform.localScale = Vector3.one * KeepsakeArt.TrunkSize;
                materials?.Apply(box.GetComponent<Renderer>(), Palette.Timber);
            }

            // A plain box collider of the trunk's size (the lid is never solid): the A1 issue 4 check runs in play.
            Collider = Root.gameObject.AddComponent<BoxCollider>();
            Collider.center = new Vector3(0f, KeepsakeArt.TrunkSize * 0.5f, 0f);
            Collider.size = new Vector3(KeepsakeArt.TrunkSize, KeepsakeArt.TrunkSize, KeepsakeArt.TrunkSize);

            // The plum cloth over the contents (shown while a piece is still to be earned).
            _cover = GameObject.CreatePrimitive(PrimitiveType.Cube);
            _cover.name = "TrunkCloth";
            Destroy(_cover.GetComponent<Collider>());
            _cover.transform.SetParent(Root, false);
            _cover.transform.localPosition = new Vector3(0f, KeepsakeArt.TrunkSize - 0.07f, 0f);
            _cover.transform.localScale = new Vector3(KeepsakeArt.TrunkSize - 0.1f, 0.03f, KeepsakeArt.TrunkSize - 0.1f);
            materials?.Apply(_cover.GetComponent<Renderer>(), Palette.Plum);
            _cover.SetActive(false);

            BuildCamera();
            Target = new ExtraUseTarget
            {
                VerbKey = Verbs.DressUp,
                IconName = UiSpriteCatalog.IconExamine,
                Marker = Root.position + Vector3.up * KeepsakePorch.TrunkMarkerHeight,
                Interact = interact,
            };
        }

        private void BuildCamera()
        {
            var go = new GameObject("DressUpCamera");
            go.transform.SetParent(transform, false);
            var eye = V(KeepsakePorch.DressingCamera);
            go.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(V(KeepsakePorch.DressingLook) - eye, Vector3.up));
            go.SetActive(false);
            _camera = go.AddComponent<CinemachineCamera>();
            var lens = _camera.Lens;
            lens.FieldOfView = KeepsakePorch.DressingFieldOfView;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 200f;
            _camera.Lens = lens;
        }

        private static Vector3 V(StagePoint p) => new Vector3(p.X, p.Y, p.Z);

        /// <summary>Whether Pip at <paramref name="feet"/> can open the trunk with <paramref name="state"/>'s pieces.</summary>
        public static bool Offered(Vector3 feet, KeepsakeState state) =>
            KeepsakePorch.TrunkOffered(state) && KeepsakePorch.TrunkInReach(feet.x, feet.y, feet.z);

        /// <summary>Opens the trunk: lid, dressing camera, Pip on her spot facing the street.</summary>
        public void Open(PipMotor pip, KeepsakeState state)
        {
            _wantOpen = true;
            _cover.SetActive(state == null || state.EarnedPieces.Count < CostumePieces.All.Count);
            _camera.gameObject.SetActive(true);
            if (pip != null)
            {
                pip.Teleport(V(KeepsakePorch.DressingSpot));
                pip.transform.rotation = Quaternion.identity;
            }

            AudioCues.PageTurn();
        }

        /// <summary>Shuts the lid and gives the camera back.</summary>
        public void Close()
        {
            _wantOpen = false;
            _camera.gameObject.SetActive(false);
        }

        /// <summary>Snaps the trunk shut (resets, the title).</summary>
        public void Snap()
        {
            Close();
            _lidOpen = 0f;
            Pose();
        }

        private void Update()
        {
            float target = _wantOpen ? 1f : 0f;
            if (Mathf.Approximately(_lidOpen, target))
            {
                return;
            }

            // Unscaled: the trunk also shuts while the game pauses for the title.
            _lidOpen = Mathf.MoveTowards(_lidOpen, target, Time.unscaledDeltaTime / LidSeconds);
            Pose();
            if (_lidOpen <= 0f)
            {
                _cover.SetActive(false);
            }
        }

        private void Pose()
        {
            if (_lid != null)
            {
                float t = Mathf.SmoothStep(0f, 1f, _lidOpen);
                _lid.localRotation = _lidRest * Quaternion.AngleAxis(KeepsakeArt.TrunkLidOpenDegrees * t, Vector3.right);
            }
        }
    }
}
