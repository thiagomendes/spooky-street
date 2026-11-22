using SpookyStreet.CameraSystem;
using SpookyStreet.Core.World;
using UnityEngine;

namespace SpookyStreet.Case
{
    /// <summary>
    /// Drives the follow camera inside the owl house interior and the treehouse cabin (S002 interior polish, house v3)
    /// from <see cref="InteriorCamera"/>: each room's
    /// profile (boom, shoulder, pitch range), a yaw swing when the space behind Pip is too tight, and a per-frame probe
    /// that pulls the boom in at once whenever anything solid would come between the lens and Pip. The full solve runs a
    /// few times a second (and whenever Pip, the orbit or the talking partner change); the probe runs every frame
    /// against the eased framing actually on screen. Outside the house it hands the camera back untouched.
    /// </summary>
    public sealed class InteriorFraming
    {
        /// <summary>Seconds between full solves while nothing changes much.</summary>
        public const float SolveInterval = 0.15f;

        /// <summary>The boom never collapses into Pip's shoulder, even when the probe finds something very close.</summary>
        public const float ShortestBoom = 0.3f;

        private readonly CameraRig _rig;
        private float _since = float.MaxValue;
        private Vector3 _lastPip;
        private float _lastYaw;
        private float _lastPitch;
        private Vector3? _lastFocus;
        private bool _engaged;

        public InteriorFraming(CameraRig rig)
        {
            _rig = rig;
        }

        /// <summary>The last solve (inactive outside the house).</summary>
        public InteriorShot Shot { get; private set; }

        /// <summary>This frame's probe limit on the boom (0 while outside).</summary>
        public float Limit { get; private set; }

        public bool Active => _engaged;

        public void Tick(Vector3 pip, bool doorOpen, Vector3? focus, float dt)
        {
            if (_rig == null)
            {
                return;
            }

            if (InteriorCamera.RoomOf(pip.x, pip.y, pip.z) == InteriorRoom.None)
            {
                Release();
                return;
            }

            var orbit = _rig.Orbit;
            _since += dt;
            bool focusChanged = focus.HasValue != _lastFocus.HasValue ||
                                focus.HasValue && (focus.Value - _lastFocus.Value).sqrMagnitude > 0.04f;
            bool moved = (pip - _lastPip).sqrMagnitude > 0.0025f ||
                         Mathf.Abs(Mathf.DeltaAngle(orbit.Yaw, _lastYaw)) > 2f || Mathf.Abs(orbit.Pitch - _lastPitch) > 2f;
            if (!_engaged || focusChanged || (moved && _since >= SolveInterval) || _since >= 1f)
            {
                SolveNow(pip, doorOpen, focus);
            }

            _engaged = true;
            _rig.Indoors = true;
            _rig.DistanceOverride = Shot.Distance;
            _rig.PitchOverride = Shot.Pitch;
            _rig.RiseOverride = Shot.Rise;
            _rig.YawOffsetTarget = Shot.YawOffset;

            // The probe: the framing on screen this frame (eased yaw, pitch and shoulder) never looks through a solid.
            float handY = _rig.Pivot.position.y + _rig.Rise;
            float reach = InteriorCamera.ProfileFor(Shot.Room).Distance;
            Limit = Mathf.Max(ShortestBoom, InteriorCamera.ClearDistance(pip.x, handY, pip.z, _rig.EffectiveYaw,
                _rig.EffectivePitch, reach, pip.x, pip.y, pip.z, doorOpen));
            _rig.DistanceLimit = Limit;
        }

        /// <summary>
        /// A threshold crossing just placed Pip (D24.1): solve now for the new place and apply it at once, trusting the
        /// solve's boom for this frame (the per-frame probe needs the rig's pivot, which moves with the cut).
        /// </summary>
        public void Snap(Vector3 pip, bool doorOpen)
        {
            if (_rig == null)
            {
                return;
            }

            if (InteriorCamera.RoomOf(pip.x, pip.y, pip.z) == InteriorRoom.None)
            {
                Release();
                return;
            }

            _engaged = false;
            SolveNow(pip, doorOpen, null);
            _engaged = true;
            _rig.Indoors = true;
            _rig.DistanceOverride = Shot.Distance;
            _rig.PitchOverride = Shot.Pitch;
            _rig.RiseOverride = Shot.Rise;
            _rig.YawOffsetTarget = Shot.YawOffset;
            Limit = Mathf.Max(ShortestBoom, Shot.Distance);
            _rig.DistanceLimit = Limit;
        }

        private void SolveNow(Vector3 pip, bool doorOpen, Vector3? focus)
        {
            var orbit = _rig.Orbit;
            var request = new InteriorCameraRequest
            {
                PipX = pip.x,
                PipY = pip.y,
                PipZ = pip.z,
                Yaw = orbit.Yaw,
                Pitch = orbit.Pitch,
                PivotHeight = _rig.PivotHeight,
                PreviousYawOffset = _engaged ? Shot.YawOffset : 0f,
                DoorOpen = doorOpen,
                HasFocus = focus.HasValue,
                FocusX = focus?.x ?? 0f,
                FocusY = focus?.y ?? 0f,
                FocusZ = focus?.z ?? 0f,
            };
            Shot = InteriorCamera.Solve(request);
            _since = 0f;
            _lastPip = pip;
            _lastYaw = orbit.Yaw;
            _lastPitch = orbit.Pitch;
            _lastFocus = focus;
        }

        /// <summary>Hands the camera back to the outdoor follow (eases back to the player's orbit).</summary>
        public void Release()
        {
            Limit = 0f;
            Shot = default;
            if (!_engaged || _rig == null)
            {
                _engaged = false;
                return;
            }

            _engaged = false;
            _rig.Indoors = false;
            _rig.DistanceOverride = null;
            _rig.DistanceLimit = null;
            _rig.PitchOverride = null;
            _rig.RiseOverride = null;
            _rig.YawOffsetTarget = 0f;
        }
    }
}
