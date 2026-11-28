using System;

namespace SpookyStreet.Core.Movement
{
    /// <summary>Direction magnetism and tremor smoothing for the move stick (see <see cref="StickSteering"/>).</summary>
    [Serializable]
    public sealed class StickSteeringTuning
    {
        /// <summary>
        /// Magnet directions spread evenly from straight up: 8 is up, the diagonals, the sides and down. 0 turns the
        /// magnetism off.
        /// </summary>
        public int Directions = 8;

        /// <summary>Within this many degrees of a magnet direction the stick points exactly along it.</summary>
        public float SnapDegrees = 6f;

        /// <summary>
        /// From <see cref="SnapDegrees"/> to this many degrees off a magnet direction the pull fades out on a ramp; past
        /// it the stick passes unchanged (every angle in between is still reachable).
        /// </summary>
        public float BlendDegrees = 14f;

        /// <summary>Seconds of smoothing on the thumb's aim (a tremor filter, light enough not to be felt).</summary>
        public float SmoothTime = 0.06f;

        /// <summary>A change of aim bigger than this many degrees skips the smoothing: a deliberate turn has no lag.</summary>
        public float TurnDegrees = 25f;
    }

    /// <summary>
    /// Steadies the move stick so a child's thumb tremor does not steer Pip (owner, 2026-11-28: "held straight, Pip
    /// zigzags"). The aim is lightly smoothed, and near up, down, the sides and the diagonals it is pulled onto that
    /// direction, so a thumb held "straight" means exactly straight. Big changes of aim pass at once; the push
    /// strength (walk or run) is never changed. Stateful: step it once per simulation step, reset it when something
    /// other than the player drives the move.
    /// </summary>
    public sealed class StickSteering
    {
        private const float RadToDeg = (float)(180.0 / Math.PI);
        private const float DegToRad = (float)(Math.PI / 180.0);

        private readonly StickSteeringTuning _tuning;
        private bool _holding;
        private float _aim;

        public StickSteering(StickSteeringTuning tuning)
        {
            _tuning = tuning ?? throw new ArgumentNullException(nameof(tuning));
        }

        /// <summary>The smoothed aim in stick degrees (0 up, + right), meaningful only while the stick is pushed.</summary>
        public float SmoothedAim => _aim;

        public void Reset() => _holding = false;

        /// <summary>Filters one stick sample (magnitude ≤ 1, x right, y up) into the steered stick value.</summary>
        public void Step(float deltaTime, float x, float y, out float outX, out float outY)
        {
            float magnitude = (float)Math.Sqrt(x * x + y * y);
            if (magnitude < 1e-4f)
            {
                _holding = false;
                outX = 0f;
                outY = 0f;
                return;
            }

            float aim = (float)Math.Atan2(x, y) * RadToDeg;
            float change = CameraOrbitModel.DeltaAngle(_aim, aim);
            if (!_holding || Math.Abs(change) >= _tuning.TurnDegrees)
            {
                _holding = true;
                _aim = aim;
            }
            else if (deltaTime > 0f)
            {
                float follow = _tuning.SmoothTime > 1e-4f ? 1f - (float)Math.Exp(-deltaTime / _tuning.SmoothTime) : 1f;
                _aim = CameraOrbitModel.NormalizeAngle(_aim + change * follow);
            }

            float angle = Magnetize(_aim, _tuning) * DegToRad;
            outX = (float)Math.Sin(angle) * magnitude;
            outY = (float)Math.Cos(angle) * magnitude;
        }

        /// <summary>
        /// Pulls <paramref name="degrees"/> onto the nearest magnet direction: exact within SnapDegrees, a linear
        /// fade to no pull at BlendDegrees. Continuous, and never swaps the order of two aims.
        /// </summary>
        public static float Magnetize(float degrees, StickSteeringTuning tuning)
        {
            if (tuning == null || tuning.Directions <= 0)
            {
                return CameraOrbitModel.NormalizeAngle(degrees);
            }

            float step = 360f / tuning.Directions;
            float snap = Math.Max(0f, tuning.SnapDegrees);
            float blend = Math.Min(step * 0.5f, Math.Max(snap + 1e-3f, tuning.BlendDegrees));
            float nearest = (float)Math.Round(degrees / step) * step;
            float off = CameraOrbitModel.DeltaAngle(nearest, degrees);
            float abs = Math.Abs(off);
            if (abs >= blend)
            {
                return CameraOrbitModel.NormalizeAngle(degrees);
            }

            float pulled = abs <= snap ? 0f : blend * (abs - snap) / (blend - snap);
            return CameraOrbitModel.NormalizeAngle(nearest + Math.Sign(off) * pulled);
        }
    }
}
