using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace SpookyStreet.UI
{
    /// <summary>
    /// Press feedback for every tappable craft control (docs/ui-language.md, States and Motion): squash to 0.94 in
    /// 60 ms while the finger is down, release with a 120 ms overshoot to 1.02 → 1.0; the felt or paper face gets
    /// 14 % darker, the drop shadow flattens to 1 pt, and a paper button's stitch can turn orange. Runs on
    /// unscaled time so it works on the paused screen.
    /// </summary>
    public sealed class PressScale : MonoBehaviour, IPointerDownHandler, IPointerUpHandler, IPointerExitHandler
    {
        public const float SquashSeconds = 0.06f;
        public const float ReleaseSeconds = 0.12f;

        public float PressedScale = 0.94f;
        public float Overshoot = 1.02f;

        /// <summary>Value multiplier for <see cref="Darken"/> while pressed (fill −14 % value).</summary>
        public float PressedValue = 0.86f;

        /// <summary>Faces that get darker while pressed (felt disc, button face).</summary>
        public Graphic[] Darken;

        /// <summary>Optional stitch layer whose colour changes while pressed.</summary>
        public Graphic Stitch;

        public bool ChangeStitch;
        public Color StitchPressed;

        private Color[] _restColors;
        private Color _stitchRest;
        private Shadow _shadow;
        private Vector2 _shadowRest;
        private bool _pressed;
        private float _releaseTime = -1f;
        private float _releaseFrom = 1f;

        public bool Pressed => _pressed;

        public void OnPointerDown(PointerEventData eventData) => Press();

        public void OnPointerUp(PointerEventData eventData) => Release();

        public void OnPointerExit(PointerEventData eventData) => Release();

        private void Press()
        {
            if (_pressed)
            {
                return;
            }

            _pressed = true;
            _releaseTime = -1f;
            if (Darken != null)
            {
                _restColors = new Color[Darken.Length];
                for (int i = 0; i < Darken.Length; i++)
                {
                    if (Darken[i] == null)
                    {
                        continue;
                    }

                    Color c = Darken[i].color;
                    _restColors[i] = c;
                    Darken[i].color = new Color(c.r * PressedValue, c.g * PressedValue, c.b * PressedValue, c.a);
                }
            }

            if (ChangeStitch && Stitch != null)
            {
                _stitchRest = Stitch.color;
                Stitch.color = StitchPressed;
            }

            _shadow = GetComponent<Shadow>();
            if (_shadow != null)
            {
                _shadowRest = _shadow.effectDistance;
                _shadow.effectDistance = new Vector2(Mathf.Sign(_shadowRest.x) * 2f, Mathf.Sign(_shadowRest.y) * 2f);
            }
        }

        private void Release()
        {
            if (!_pressed)
            {
                return;
            }

            _pressed = false;
            RestoreLook();
            _releaseFrom = transform.localScale.x;
            _releaseTime = 0f;
        }

        private void RestoreLook()
        {
            if (Darken != null && _restColors != null)
            {
                for (int i = 0; i < Darken.Length && i < _restColors.Length; i++)
                {
                    if (Darken[i] != null)
                    {
                        Darken[i].color = _restColors[i];
                    }
                }
            }

            if (ChangeStitch && Stitch != null)
            {
                Stitch.color = _stitchRest;
            }

            if (_shadow != null)
            {
                _shadow.effectDistance = _shadowRest;
            }
        }

        private void OnDisable()
        {
            if (_pressed)
            {
                _pressed = false;
                RestoreLook();
            }

            _releaseTime = -1f;
            transform.localScale = Vector3.one;
        }

        private void Update()
        {
            float dt = Time.unscaledDeltaTime;
            float s = transform.localScale.x;
            if (_pressed)
            {
                s = Mathf.MoveTowards(s, PressedScale, dt * (1f - PressedScale) / SquashSeconds);
            }
            else if (_releaseTime >= 0f)
            {
                _releaseTime += dt;
                float t = _releaseTime / ReleaseSeconds;
                if (t >= 1f)
                {
                    s = 1f;
                    _releaseTime = -1f;
                }
                else if (t < 0.6f)
                {
                    s = Mathf.Lerp(_releaseFrom, Overshoot, t / 0.6f);
                }
                else
                {
                    s = Mathf.Lerp(Overshoot, 1f, (t - 0.6f) / 0.4f);
                }
            }
            else if (!Mathf.Approximately(s, 1f))
            {
                s = 1f;
            }

            transform.localScale = new Vector3(s, s, 1f);
        }
    }
}
