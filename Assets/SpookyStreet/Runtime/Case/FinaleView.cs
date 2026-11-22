using SpookyStreet.Core.Case;
using Unity.Cinemachine;
using UnityEngine;

namespace SpookyStreet.Case
{
    /// <summary>
    /// The finale's framing (spec §4 arrival porch: "Finale restages that identical view with the reopened booth"):
    /// a fixed camera at the arrival porch's eye line frames the owl house porch, where Aunt Kit opens her booth with
    /// June beside her and the owl bows on the roof. It holds while the reopening lines play and a moment after, then
    /// blends back to the follow camera; the player keeps camera control afterwards.
    /// </summary>
    public sealed class FinaleView : MonoBehaviour
    {
        public const float FieldOfView = 24f;

        /// <summary>The shot holds this long after the last line before the notebook opens.</summary>
        public const float HoldAfterLines = 1.6f;

        private CinemachineCamera _camera;

        public bool Active { get; private set; }

        public Transform CameraTransform => _camera.transform;

        public static FinaleView Create(Transform parent)
        {
            var go = new GameObject("FinaleView");
            go.transform.SetParent(parent, false);
            var view = go.AddComponent<FinaleView>();
            var cameraObject = new GameObject("FinaleCamera");
            cameraObject.transform.SetParent(go.transform, false);
            var eye = new Vector3(CaseStage.FinaleEye.X, CaseStage.FinaleEye.Y, CaseStage.FinaleEye.Z);
            var at = new Vector3(CaseStage.FinaleLookAt.X, CaseStage.FinaleLookAt.Y, CaseStage.FinaleLookAt.Z);
            cameraObject.transform.SetPositionAndRotation(eye, Quaternion.LookRotation(at - eye, Vector3.up));
            cameraObject.SetActive(false);
            var vcam = cameraObject.AddComponent<CinemachineCamera>();
            var lens = vcam.Lens;
            lens.FieldOfView = FieldOfView;
            lens.NearClipPlane = 0.1f;
            lens.FarClipPlane = 200f;
            vcam.Lens = lens;
            view._camera = vcam;
            return view;
        }

        public void Begin()
        {
            Active = true;
            _camera.gameObject.SetActive(true);
        }

        public void End()
        {
            Active = false;
            _camera.gameObject.SetActive(false);
        }
    }
}
