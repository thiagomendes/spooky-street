using System.Collections.Generic;
using SpookyStreet.Core.World;
using UnityEngine;

namespace SpookyStreet.World
{
    /// <summary>
    /// Ambient life on the environment models: spinners (the owl house wind wheel), a gentle sway (the rooftop owl
    /// puppet) and bobbing (paper bats). Runs on scaled game time, the same clock the game pauses through
    /// <c>Time.timeScale = 0</c>, so nothing moves on the title or pause screens. <see cref="ReducedMotion"/> stops
    /// decorative sway and bob (spec §6) and halves the wheel. No allocations per frame.
    /// </summary>
    public sealed class AmbientMotion : MonoBehaviour
    {
        private enum Kind
        {
            Spin,
            Sway,
            Bob,
        }

        private struct Item
        {
            public Transform Target;
            public Kind Kind;
            public Quaternion Rotation;
            public Vector3 Position;
            public Vector3 Axis;
            public float Phase;
        }

        private readonly List<Item> _items = new List<Item>();
        private double _time;
        private double _wheelTime;

        /// <summary>
        /// Reduced-motion preference (spec §5 settings). There is no settings screen yet; the settings work sets
        /// this, and ambient motion follows it on the next frame.
        /// </summary>
        public static bool ReducedMotion { get; set; }

        public int Count => _items.Count;

        /// <summary>Game time this component has animated (stays still while paused).</summary>
        public double ElapsedTime => _time;

        /// <summary>Turns <paramref name="target"/> continuously about its local <paramref name="localAxis"/>.</summary>
        public void AddSpinner(Transform target, Vector3 localAxis) => Add(target, Kind.Spin, localAxis);

        /// <summary>A small nod and sway about the target's pivot (a puppet's feet).</summary>
        public void AddSway(Transform target) => Add(target, Kind.Sway, Vector3.zero);

        /// <summary>A soft vertical bob with a slight turn (paper bats on threads).</summary>
        public void AddBob(Transform target) => Add(target, Kind.Bob, Vector3.zero);

        private void Add(Transform target, Kind kind, Vector3 axis)
        {
            if (target == null)
            {
                return;
            }

            _items.Add(new Item
            {
                Target = target,
                Kind = kind,
                Rotation = target.localRotation,
                Position = target.localPosition,
                Axis = axis.sqrMagnitude > 0f ? axis.normalized : Vector3.forward,
                Phase = AmbientMotionModel.PhaseFor(_items.Count),
            });
        }

        private void Update()
        {
            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return; // paused: timeScale 0
            }

            bool reduced = ReducedMotion;
            float deco = AmbientMotionModel.DecorativeScale(reduced);
            _time += dt;
            _wheelTime += dt * AmbientMotionModel.WheelSpeed(reduced) / AmbientMotionModel.WindWheelDegreesPerSecond;
            float wheel = AmbientMotionModel.SpinDegrees(_wheelTime, AmbientMotionModel.WindWheelDegreesPerSecond);

            for (int i = 0; i < _items.Count; i++)
            {
                Item item = _items[i];
                if (item.Target == null)
                {
                    continue;
                }

                switch (item.Kind)
                {
                    case Kind.Spin:
                        item.Target.localRotation = item.Rotation * Quaternion.AngleAxis(wheel, item.Axis);
                        break;
                    case Kind.Sway:
                        float nod = AmbientMotionModel.Oscillate(_time, AmbientMotionModel.OwlNodHz,
                            AmbientMotionModel.OwlNodDegrees * deco, item.Phase);
                        float sway = AmbientMotionModel.Oscillate(_time, AmbientMotionModel.OwlSwayHz,
                            AmbientMotionModel.OwlSwayDegrees * deco, item.Phase + 0.25f);
                        item.Target.localRotation = item.Rotation * Quaternion.Euler(nod, 0f, sway);
                        break;
                    case Kind.Bob:
                        float bob = AmbientMotionModel.Oscillate(_time, AmbientMotionModel.BatBobHz,
                            AmbientMotionModel.BatBobMetres * deco, item.Phase);
                        float turn = AmbientMotionModel.Oscillate(_time, AmbientMotionModel.BatBobHz * 0.5f,
                            AmbientMotionModel.BatTurnDegrees * deco, item.Phase + 0.5f);
                        item.Target.localPosition = item.Position + new Vector3(0f, bob, 0f);
                        item.Target.localRotation = item.Rotation * Quaternion.Euler(0f, turn, turn * 0.5f);
                        break;
                }
            }
        }
    }
}
