using UnityEngine;

namespace SpookyStreet.UI
{
    /// <summary>
    /// Scales a fixed-size card down (never up) so it fits its parent, usually the safe area, with a margin: one layout
    /// for a wide iPhone, a 4:3 iPad or a desktop window (D25). Rechecked whenever the parent's size changes.
    /// </summary>
    public sealed class UiFitInside : MonoBehaviour
    {
        private RectTransform _rect;
        private Vector2 _size;
        private float _margin;
        private Vector2 _lastParent = new Vector2(-1f, -1f);

        /// <summary>The scale in use (1 when the card fits as designed).</summary>
        public float Scale { get; private set; } = 1f;

        public static UiFitInside Attach(RectTransform rect, Vector2 designSize, float margin)
        {
            var fit = rect.gameObject.AddComponent<UiFitInside>();
            fit._rect = rect;
            fit._size = designSize;
            fit._margin = margin;
            fit.Refit();
            return fit;
        }

        private void OnEnable() => Refit();

        private void LateUpdate() => Refit();

        public void Refit()
        {
            if (_rect == null || !(_rect.parent is RectTransform parent))
            {
                return;
            }

            var available = parent.rect.size;
            if (available == _lastParent || available.x <= 0f || available.y <= 0f)
            {
                return;
            }

            _lastParent = available;
            float sx = (available.x - 2f * _margin) / _size.x;
            float sy = (available.y - 2f * _margin) / _size.y;
            Scale = Mathf.Clamp(Mathf.Min(sx, sy), 0.4f, 1f);
            _rect.localScale = new Vector3(Scale, Scale, 1f);
        }
    }
}
