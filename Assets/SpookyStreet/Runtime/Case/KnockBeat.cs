using System;
using SpookyStreet.Core.Audio;
using SpookyStreet.Core.World;
using SpookyStreet.World;
using UnityEngine;

namespace SpookyStreet.Case
{
    /// <summary>
    /// The arrival cue's staging (spec §3 "Arrival"): with each of the three knocks (short, short, pause, last) the
    /// attic window's warm light swells, then settles a little brighter than before; on the second knock a paper bat
    /// shakes loose from the owl house's porch eave and flutters down to the deck. <see cref="SetHeard"/> rebuilds the
    /// settled state from facts on resume (CaseWorldView.PaperBatFallen). Runs on game time (pause freezes it).
    /// The pulses land on the knock onsets inside the sound's takes (<see cref="AudioEvents.KnockOnsets"/>): the
    /// audio starts on <see cref="Started"/>, the same frame as the animation, and both hold under the pause (S003).
    /// </summary>
    public sealed class KnockBeat : MonoBehaviour
    {
        /// <summary>Knock times in seconds: short, short, pause, last (spec §6 sound rhythm), as in the sound itself.</summary>
        public static readonly float[] KnockTimes = AudioEvents.KnockOnsets;

        public const float Duration = 2.4f;
        public const float SettledIntensity = 1.4f;
        public const float PulseIntensity = 4.5f;
        /// <summary>The bat shakes loose on the second knock.</summary>
        public static float BatFallStart => KnockTimes[1];
        public const float BatFallSeconds = 1.3f;

        /// <summary>Under the owl house porch eave, right of the door, and where the bat lands on the deck.</summary>
        public static readonly Vector3 BatHook = new Vector3(1.35f, 2.55f, 9.25f);
        public static readonly Vector3 BatRest = new Vector3(1.05f, 0.63f, 9.7f);

        private Light _warm;
        private Transform _bat;
        private Quaternion _hookRotation;
        private Quaternion _restRotation;
        private float _time = -1f;

        /// <summary>True once the cue has played (or was restored as played).</summary>
        public bool Heard { get; private set; }

        /// <summary>True while the animation runs.</summary>
        public bool Playing => _time >= 0f;

        public Light WarmLight => _warm;

        /// <summary>Where the knocks come from: the attic window's light (the 3D sound plays here).</summary>
        public Vector3 SoundPosition => _warm.transform.position;

        /// <summary>Raised when the cue starts (not on <see cref="SetHeard"/>): the knock sound starts with it.</summary>
        public event Action Started;

        /// <summary>The loose paper bat (null without the environment models).</summary>
        public Transform PaperBat => _bat;

        public static KnockBeat Create(Transform parent, MaterialLibrary materials, Vector3 atticLight, bool useModels)
        {
            var go = new GameObject("KnockBeat");
            go.transform.SetParent(parent, false);
            var beat = go.AddComponent<KnockBeat>();

            var light = new GameObject("AtticWarmth").AddComponent<Light>();
            light.transform.SetParent(go.transform, false);
            light.transform.position = atticLight;
            light.type = LightType.Point;
            light.color = Palette.PumpkinAmber;
            light.range = 7f;
            light.intensity = 0f;
            light.shadows = LightShadows.None;
            beat._warm = light;

            if (useModels && materials != null)
            {
                var kit = new ModelKit(materials, EnvironmentDressing.ModelFolder);
                var bat = kit.Spawn(EnvironmentLayout.PaperBat, go.transform, BatHook, 160f, 0.9f);
                if (bat != null)
                {
                    beat._bat = bat.transform;
                    beat._restRotation = beat._bat.rotation;
                    beat._hookRotation = Quaternion.AngleAxis(-EnvironmentDressing.BatTilt, Vector3.right) * beat._restRotation;
                }
            }

            beat.SetHeard(false);
            return beat;
        }

        /// <summary>Instantly shows the state before or after the cue (resume, new game, replay).</summary>
        public void SetHeard(bool heard)
        {
            Heard = heard;
            _time = -1f;
            _warm.intensity = heard ? SettledIntensity : 0f;
            if (_bat != null)
            {
                _bat.SetPositionAndRotation(heard ? BatRest : BatHook, heard ? _restRotation : _hookRotation);
            }
        }

        public void Play()
        {
            Heard = true;
            _time = 0f;
            Started?.Invoke();
        }

        private void Update()
        {
            if (_time < 0f)
            {
                return;
            }

            float dt = Time.deltaTime;
            if (dt <= 0f)
            {
                return;
            }

            _time += dt;
            float intensity = Mathf.Lerp(0f, SettledIntensity, Mathf.Clamp01(_time / Duration));
            foreach (float knock in KnockTimes)
            {
                float since = _time - knock;
                if (since >= 0f && since < 0.4f)
                {
                    intensity = Mathf.Max(intensity, PulseIntensity * (1f - since / 0.4f));
                }
            }

            _warm.intensity = intensity;
            if (_bat != null)
            {
                float t = Mathf.Clamp01((_time - BatFallStart) / BatFallSeconds);
                float eased = t * t;
                Vector3 position = Vector3.Lerp(BatHook, BatRest, eased);
                position.x += Mathf.Sin(t * Mathf.PI * 3f) * 0.25f * (1f - t);
                Quaternion flutter = Quaternion.AngleAxis(Mathf.Sin(t * Mathf.PI * 4f) * 25f * (1f - t), Vector3.forward);
                _bat.SetPositionAndRotation(position, flutter * Quaternion.Slerp(_hookRotation, _restRotation, t));
            }

            if (_time >= Mathf.Max(Duration, BatFallStart + BatFallSeconds))
            {
                SetHeard(true);
            }
        }
    }
}
