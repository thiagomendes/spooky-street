using System;
using NUnit.Framework;
using SpookyStreet.Core.Movement;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// "Hold the stick straight and Pip walks straight" (owner and his son on the iPhone, 2026-11-28): a wobbling
    /// child's thumb, the stick filter, the real motor and the camera's auto-recenter run together frame by frame in
    /// the game's order (the motor reads the camera of the previous frame, then the camera turns toward Pip's heading).
    /// </summary>
    public class StraightStickTests
    {
        /// <summary>Sideways band a "straight" 10 m must stay inside: about half a pavement slab, a third of Pip's stride.</summary>
        private const float LateralBand = 0.25f;

        private const int Seeds = 40;

        /// <summary>
        /// A child's thumb: tremor re-aimed every 60–180 ms inside ±7°, a slow wander of ±3° (together ±10° around
        /// <see cref="Aim"/>), and the push jittering between 70% and 100% of the stick radius.
        /// </summary>
        private sealed class WobblyThumb
        {
            private readonly Random _random;
            private readonly float _wanderPhase;
            private float _tremorTarget;
            private float _tremor;
            private float _radiusTarget = 0.9f;
            private float _radius = 0.9f;
            private float _nextPick;
            private float _time;

            public WobblyThumb(int seed)
            {
                _random = new Random(seed);
                _wanderPhase = (float)(_random.NextDouble() * Math.PI * 2.0);
            }

            /// <summary>The direction the child means, in stick degrees (0 up, + to the right).</summary>
            public float Aim { get; set; }

            public void Step(float dt, out float x, out float y)
            {
                _time += dt;
                if (_time >= _nextPick)
                {
                    _tremorTarget = (float)(_random.NextDouble() * 14.0 - 7.0);
                    _radiusTarget = (float)(0.7 + _random.NextDouble() * 0.3);
                    _nextPick = _time + (float)(0.06 + _random.NextDouble() * 0.12);
                }

                float k = 1f - (float)Math.Exp(-dt / 0.04f);
                _tremor += (_tremorTarget - _tremor) * k;
                _radius += (_radiusTarget - _radius) * k;
                float wander = 3f * (float)Math.Sin(2.0 * Math.PI * 0.3 * _time + _wanderPhase);
                float angle = (Aim + _tremor + wander) * (float)(Math.PI / 180.0);
                x = (float)Math.Sin(angle) * _radius;
                y = (float)Math.Cos(angle) * _radius;
            }
        }

        /// <summary>Pip on flat ground: stick filter, motor, her facing (PipMotor.FaceVelocity) and the orbit camera.</summary>
        private sealed class StreetRun
        {
            private readonly MovementTuningData _tuning = new MovementTuningData();
            private readonly StickSteering _steering;
            private readonly float _dt;
            private MotorState _state = MotorState.Initial;

            public StreetRun(float fps, bool steering)
            {
                _dt = 1f / fps;
                Orbit = new CameraOrbitModel(new CameraOrbitTuning(), 0f);
                _steering = steering ? new StickSteering(new StickSteeringTuning()) : null;
            }

            public CameraOrbitModel Orbit { get; }

            public float X { get; private set; }

            public float Z { get; private set; }

            public float Walked { get; private set; }

            /// <summary>Pip's facing in world degrees (turns toward her velocity at the tuned 720°/s).</summary>
            public float Heading { get; private set; }

            public float VelocityYaw => (float)(Math.Atan2(_state.VelocityX, _state.VelocityZ) * 180.0 / Math.PI);

            public void Step(float stickX, float stickY)
            {
                if (_steering != null)
                {
                    _steering.Step(_dt, stickX, stickY, out stickX, out stickY);
                }

                // PipMotor.Update: the stick turns into a world direction through the camera's yaw.
                float yaw = Orbit.Yaw * (float)(Math.PI / 180.0);
                float fx = (float)Math.Sin(yaw), fz = (float)Math.Cos(yaw);
                var input = new MotorInput
                {
                    MoveX = fz * stickX + fx * stickY,
                    MoveZ = -fx * stickX + fz * stickY,
                    StickY = stickY,
                };
                var step = MovementModel.Step(ref _state, input, true, _dt, _tuning);
                X += step.DeltaX;
                Z += step.DeltaZ;
                Walked += (float)Math.Sqrt(step.DeltaX * step.DeltaX + step.DeltaZ * step.DeltaZ);
                if (_state.HorizontalSpeed * _state.HorizontalSpeed >= 0.04f)
                {
                    Heading = CameraOrbitModel.MoveTowardsAngle(Heading, VelocityYaw, 720f * _dt);
                }

                // CameraRig.LateUpdate.
                Orbit.Tick(_dt, _state.HorizontalSpeed, Heading);
            }
        }

        /// <summary>Worst sideways distance (m) from the straight line while Pip walks 10 m with the stick held up.</summary>
        private static float LateralDriftOver10m(int seed, float fps, bool steering)
        {
            var run = new StreetRun(fps, steering);
            var thumb = new WobblyThumb(seed);
            float dt = 1f / fps;
            float worst = 0f;
            for (int i = 0; i < fps * 8f && run.Walked < 10f; i++)
            {
                thumb.Step(dt, out float x, out float y);
                run.Step(x, y);
                worst = Math.Max(worst, Math.Abs(run.X));
            }

            Assert.GreaterOrEqual(run.Walked, 10f, "seed " + seed + ": Pip walks 10 m within 8 s");
            return worst;
        }

        private static (float mean, float worst) Drift(float fps, bool steering)
        {
            float worst = 0f;
            float sum = 0f;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                float drift = LateralDriftOver10m(seed, fps, steering);
                sum += drift;
                worst = Math.Max(worst, drift);
            }

            return (sum / Seeds, worst);
        }

        [TestCase(60f)]
        [TestCase(30f)]
        public void WobblingThumbHeldStraightWalksStraight(float fps)
        {
            var (mean, worst) = Drift(fps, true);
            var (rawMean, rawWorst) = Drift(fps, false);
            TestContext.WriteLine($"{fps} fps, sideways drift over 10 m, {Seeds} thumbs: mean {mean:F3} m, worst {worst:F3} m " +
                                  $"(camera fix alone, no stick filter: mean {rawMean:F3} m, worst {rawWorst:F3} m)");
            Assert.Less(worst, LateralBand, "a wobbling thumb held straight keeps Pip inside the band");
            Assert.Less(rawWorst, 1f, "even unfiltered, the camera no longer turns wobble into a curve");
        }

        /// <summary>Seconds from the thumb starting a 90° swing until Pip's run points the new way (within 10°).</summary>
        private static float QuarterTurnTime(int seed, bool steering)
        {
            const float Fps = 60f;
            const float Dt = 1f / Fps;
            var run = new StreetRun(Fps, steering);
            var thumb = new WobblyThumb(seed);
            for (int i = 0; i < 90; i++)
            {
                thumb.Step(Dt, out float x, out float y);
                run.Step(x, y);
            }

            float target = CameraOrbitModel.NormalizeAngle(run.Orbit.Yaw + 90f);
            for (int i = 1; i <= 60; i++)
            {
                thumb.Aim = Math.Min(90f, 90f * i * Dt / 0.1f); // the thumb swings right in 0.1 s
                thumb.Step(Dt, out float x, out float y);
                run.Step(x, y);
                if (Math.Abs(CameraOrbitModel.DeltaAngle(run.VelocityYaw, target)) <= 10f)
                {
                    return i * Dt;
                }
            }

            return float.PositiveInfinity;
        }

        [Test]
        public void DeliberateQuarterTurnStillCompletesQuickly()
        {
            float worst = 0f;
            float worstExtra = float.NegativeInfinity;
            for (int seed = 1; seed <= Seeds; seed++)
            {
                float filtered = QuarterTurnTime(seed, true);
                float raw = QuarterTurnTime(seed, false);
                worst = Math.Max(worst, filtered);
                worstExtra = Math.Max(worstExtra, filtered - raw);
            }

            TestContext.WriteLine($"90° turn: worst {worst:F3} s, worst lag added by the stick filter {worstExtra * 1000f:F0} ms");
            Assert.LessOrEqual(worst, 0.4f, "a 90° swing of the thumb turns Pip within 0.4 s");
            Assert.LessOrEqual(worstExtra, 0.05f, "the tremor filter adds at most a few frames to a deliberate turn");
        }

        [Test]
        public void CameraStillSwingsBehindALongRunAfterATurn()
        {
            // The child steers Pip along world +X (90°) while the lens still looks along +Z: she keeps her thumb
            // pointing at where Pip goes on screen, as children do, so the aim follows the swinging camera.
            const float Fps = 60f;
            const float Dt = 1f / Fps;
            var run = new StreetRun(Fps, true);
            var thumb = new WobblyThumb(7);
            float lateral = 0f;
            for (int i = 0; i < Fps * 4f; i++)
            {
                thumb.Aim = CameraOrbitModel.DeltaAngle(run.Orbit.Yaw, 90f);
                thumb.Step(Dt, out float x, out float y);
                run.Step(x, y);
                if (run.Walked > 1.5f)
                {
                    lateral = Math.Max(lateral, Math.Abs(run.Z));
                }
            }

            float gap = Math.Abs(CameraOrbitModel.DeltaAngle(run.Orbit.Yaw, 90f));
            TestContext.WriteLine($"after 4 s the lens is {gap:F1}° off behind Pip; her run strays {lateral:F2} m");
            Assert.Less(gap, new CameraOrbitTuning().RecenterDeadZone + 4f, "the camera ends up behind her run");
            Assert.Less(lateral, 1f, "and she keeps running the way the child points");
        }
    }
}
