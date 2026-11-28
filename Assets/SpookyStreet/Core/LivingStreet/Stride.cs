using System;
using SpookyStreet.Core.Crowd;

namespace SpookyStreet.Core.LivingStreet
{
    /// <summary>
    /// What a crowd body (or a door neighbour) plays this frame. <see cref="Locomotion"/> is the old rule (Walk while
    /// moving, Idle standing); the rest are one-shots the Core sequences at an exact time, so the body is a pure function
    /// of the clock (deterministic at 30 and 60 fps). Names are the art contract's (<see cref="CrowdClipTimings"/>); a body
    /// without the new clips plays <see cref="WalkerClips.Fallback"/>.
    /// </summary>
    public enum WalkerClip
    {
        Locomotion,
        WalkStart,
        WalkStop,
        HoldPail,
        Cheer,
        GiveTreat,
        OpenDoorPeek,
        Wave,
        Talk,
    }

    /// <summary>Clip names, lengths and the stand-ins used until the art's clips are in the FBX files.</summary>
    public static class WalkerClips
    {
        /// <summary>The clip's name in the FBX (null for <see cref="WalkerClip.Locomotion"/>).</summary>
        public static string Name(WalkerClip clip)
        {
            switch (clip)
            {
                case WalkerClip.WalkStart: return CrowdClipTimings.WalkStart;
                case WalkerClip.WalkStop: return CrowdClipTimings.WalkStop;
                case WalkerClip.HoldPail: return CrowdClipTimings.HoldPail;
                case WalkerClip.Cheer: return CrowdClipTimings.Cheer;
                case WalkerClip.GiveTreat: return CrowdClipTimings.GiveTreat;
                case WalkerClip.OpenDoorPeek: return CrowdClipTimings.OpenDoorPeek;
                case WalkerClip.Wave: return "Wave";
                case WalkerClip.Talk: return "Talk";
                default: return null;
            }
        }

        /// <summary>
        /// The shipped clip that stands in while the new one is missing (the kit's Idle, Walk, Wave and Talk): a pail
        /// held out and a peek at the door stand still, a cheer waves, a hand-out gestures, and the start and stop of a
        /// walk are the walk itself (its speed follows the ground speed, so the feet still match).
        /// </summary>
        public static string Fallback(WalkerClip clip)
        {
            switch (clip)
            {
                case WalkerClip.WalkStart:
                case WalkerClip.WalkStop:
                    return "Walk";
                case WalkerClip.Cheer:
                    return "Wave";
                case WalkerClip.GiveTreat:
                    return "Talk";
                case WalkerClip.Wave:
                case WalkerClip.Talk:
                    return Name(clip);
                default:
                    return "Idle";
            }
        }

        /// <summary>Seconds a one-shot lasts (the art contract's frames at 30 fps).</summary>
        public static float Seconds(WalkerClip clip)
        {
            switch (clip)
            {
                case WalkerClip.WalkStart: return CrowdClipTimings.WalkStartFrames / CrowdClipTimings.Fps;
                case WalkerClip.WalkStop: return CrowdClipTimings.WalkStopFrames / CrowdClipTimings.Fps;
                case WalkerClip.HoldPail: return CrowdClipTimings.HoldPailFrames / CrowdClipTimings.Fps;
                case WalkerClip.Cheer: return CrowdClipTimings.CheerFrames / CrowdClipTimings.Fps;
                case WalkerClip.GiveTreat: return CrowdClipTimings.GiveTreatFrames / CrowdClipTimings.Fps;
                case WalkerClip.OpenDoorPeek: return CrowdClipTimings.OpenDoorPeekFrames / CrowdClipTimings.Fps;
                default: return 0f;
            }
        }

        /// <summary>Seconds into GiveTreat at which the sweet is let go over the pail.</summary>
        public static float GiveRelease => Seconds(WalkerClip.GiveTreat) * CrowdClipTimings.GiveTreatRelease;

        /// <summary>Seconds into HoldPail at which the sweet lands in the pail.</summary>
        public static float PailDrop => Seconds(WalkerClip.HoldPail) * CrowdClipTimings.HoldPailDrop;

        /// <summary>Pip's HandTreat: its length and release (her own rig, the same 48 frames).</summary>
        public static float HandTreatSeconds => CrowdClipTimings.HandTreatFrames / CrowdClipTimings.Fps;

        public static float HandRelease => HandTreatSeconds * CrowdClipTimings.HandTreatRelease;

        /// <summary>Seconds into OpenDoorPeek at which the hand reaches the door edge (the leaf starts to swing).</summary>
        public static float PeekHandOnDoor => Seconds(WalkerClip.OpenDoorPeek) * CrowdClipTimings.OpenDoorPeekHandOnDoor;

        /// <summary>
        /// How far a child stands from a giver to receive (the giver's knuckles over the pail's mouth at the release):
        /// <see cref="CrowdClipTimings.GiveTreatReleaseForward"/> + <see cref="CrowdClipTimings.HoldPailMouthForward"/>.
        /// </summary>
        public static float ReceiveDistance => CrowdClipTimings.GiveTreatReleaseForward + CrowdClipTimings.HoldPailMouthForward;

        /// <summary>The same for Pip's HandTreat.</summary>
        public static float PipReceiveDistance => CrowdClipTimings.HandTreatReleaseForward + CrowdClipTimings.HoldPailMouthForward;
    }

    /// <summary>
    /// A short walk from one spot to another with the feet planted (art contract <see cref="CrowdClipTimings"/>): WalkStart
    /// from standing, the walk at the clip's own pace, WalkStop to standing, the root moved exactly by
    /// <see cref="CrowdClipTimings.RootTravel"/> during the start and the stop. The body turns toward where it goes while
    /// it sets off and to its final facing while it stops, never on the spot at a standstill (a turn with no travel steps
    /// through it at a slow walk). Used for a child stepping up to a door and back, a family gathering in its arc, a
    /// neighbour coming out to the steps. Pure and frame-rate independent.
    /// </summary>
    public readonly struct Stride
    {
        /// <summary>Below this a stride is a turn on the spot.</summary>
        public const float MinTravel = 0.05f;

        /// <summary>A turn on the spot steps round at this rate (degrees per second).</summary>
        public const float TurnRate = 150f;

        private readonly float _fromX, _fromZ, _fromY, _toX, _toZ, _toY, _fromYaw, _toYaw, _heading, _distance, _scale;
        private readonly string _model;

        public Stride(float fromX, float fromZ, float fromYaw, float toX, float toZ, float toYaw, string model,
            float fromY = 0f, float toY = 0f)
        {
            _fromX = fromX;
            _fromZ = fromZ;
            _fromY = fromY;
            _toX = toX;
            _toZ = toZ;
            _toY = toY;
            _fromYaw = fromYaw;
            _toYaw = toYaw;
            _model = model ?? CrowdKit.ChildModel;
            float dx = toX - fromX;
            float dz = toZ - fromZ;
            _distance = (float)Math.Sqrt(dx * dx + dz * dz);
            _heading = _distance > 1e-5f ? (float)(Math.Atan2(dx, dz) * 180.0 / Math.PI) : fromYaw;
            float ends = CrowdClipTimings.WalkTransitionDistance(CrowdClipTimings.WalkStart, _model) +
                         CrowdClipTimings.WalkTransitionDistance(CrowdClipTimings.WalkStop, _model);
            _scale = _distance < ends ? _distance / ends : 1f;
            if (_distance < MinTravel)
            {
                Duration = Math.Max(0.3f, Math.Abs(DeltaYaw(fromYaw, toYaw)) / TurnRate);
                Turning = Math.Abs(DeltaYaw(fromYaw, toYaw)) > 2f;
                StartSeconds = 0f;
                StopSeconds = 0f;
                return;
            }

            Turning = false;
            StartSeconds = WalkerClips.Seconds(WalkerClip.WalkStart);
            StopSeconds = WalkerClips.Seconds(WalkerClip.WalkStop);
            float cruise = _distance - ends * _scale;
            Duration = StartSeconds + StopSeconds + cruise / CrowdKit.WalkSpeed(_model);
        }

        public float Duration { get; }

        public float StartSeconds { get; }

        public float StopSeconds { get; }

        /// <summary>True for a stride that only turns (it steps round slowly).</summary>
        public bool Turning { get; }

        public float Distance => _distance;

        /// <summary>The pose <paramref name="t"/> seconds into the stride (clamped to its ends).</summary>
        public void Sample(float t, out float x, out float z, out float y, out float yaw, out float speed, out WalkerClip clip,
            out float clipTime)
        {
            t = t < 0f ? 0f : t > Duration ? Duration : t;
            if (_distance < MinTravel)
            {
                float u = Duration > 0f ? t / Duration : 1f;
                x = _fromX + (_toX - _fromX) * u;
                z = _fromZ + (_toZ - _fromZ) * u;
                y = _fromY + (_toY - _fromY) * u;
                yaw = LerpYaw(_fromYaw, _toYaw, Smooth(u));
                // A turn on the spot steps round at a slow walk (never a slide); a still stride stands.
                speed = Turning && u < 1f ? 0.35f : 0f;
                clip = WalkerClip.Locomotion;
                clipTime = 0f;
                return;
            }

            float s;
            float v = CrowdKit.WalkSpeed(_model);
            float start = CrowdClipTimings.WalkTransitionDistance(CrowdClipTimings.WalkStart, _model) * _scale;
            float stop = CrowdClipTimings.WalkTransitionDistance(CrowdClipTimings.WalkStop, _model) * _scale;
            float cruiseEnd = Duration - StopSeconds;
            if (t < StartSeconds)
            {
                float u = t / StartSeconds;
                s = CrowdClipTimings.RootTravel(CrowdClipTimings.WalkStart, _model, u) * _scale;
                speed = v * _scale * Math.Min(1f, u * 1.5f);
                clip = WalkerClip.WalkStart;
                clipTime = t;
                yaw = LerpYaw(_fromYaw, _heading, Smooth(u));
            }
            else if (t < cruiseEnd)
            {
                s = start + (t - StartSeconds) * v;
                speed = v;
                clip = WalkerClip.Locomotion;
                clipTime = 0f;
                yaw = _heading;
            }
            else
            {
                float u = StopSeconds > 0f && t < Duration - 1e-5f ? (t - cruiseEnd) / StopSeconds : 1f;
                s = _distance - stop + CrowdClipTimings.RootTravel(CrowdClipTimings.WalkStop, _model, u) * _scale;
                speed = u >= 1f ? 0f : v * _scale * (1f - u);
                clip = u >= 1f ? WalkerClip.Locomotion : WalkerClip.WalkStop;
                clipTime = t - cruiseEnd;
                yaw = LerpYaw(_heading, _toYaw, Smooth(u));
            }

            float f = _distance > 1e-5f ? s / _distance : 1f;
            x = _fromX + (_toX - _fromX) * f;
            z = _fromZ + (_toZ - _fromZ) * f;
            y = _fromY + (_toY - _fromY) * f;
        }

        /// <summary>Signed smallest turn from <paramref name="a"/> to <paramref name="b"/> (degrees, -180..180).</summary>
        public static float DeltaYaw(float a, float b)
        {
            float d = (b - a) % 360f;
            if (d > 180f)
            {
                d -= 360f;
            }
            else if (d < -180f)
            {
                d += 360f;
            }

            return d;
        }

        public static float LerpYaw(float a, float b, float t) => a + DeltaYaw(a, b) * t;

        /// <summary>Unity yaw looking from (ax, az) toward (bx, bz).</summary>
        public static float YawTo(float ax, float az, float bx, float bz) =>
            (float)(Math.Atan2(bx - ax, bz - az) * 180.0 / Math.PI);

        private static float Smooth(float u)
        {
            u = u < 0f ? 0f : u > 1f ? 1f : u;
            return u * u * (3f - 2f * u);
        }
    }
}
