using UnityEngine;

namespace SpookyStreet.UI
{
    /// <summary>Keeps a full-screen RectTransform inside Screen.safeArea (notch, home indicator, rotation).</summary>
    [RequireComponent(typeof(RectTransform))]
    public sealed class SafeAreaFitter : MonoBehaviour
    {
        private Rect _applied;
        private Vector2Int _screen;

        private void OnEnable() => Apply(true);

        private void Update() => Apply(false);

        private void Apply(bool force)
        {
            Rect safe = Screen.safeArea;
            var screen = new Vector2Int(Screen.width, Screen.height);
            if (!force && safe == _applied && screen == _screen)
            {
                return;
            }

            _applied = safe;
            _screen = screen;
            if (screen.x <= 0 || screen.y <= 0)
            {
                return;
            }

            var rect = (RectTransform)transform;
            rect.anchorMin = new Vector2(safe.xMin / screen.x, safe.yMin / screen.y);
            rect.anchorMax = new Vector2(safe.xMax / screen.x, safe.yMax / screen.y);
            rect.offsetMin = Vector2.zero;
            rect.offsetMax = Vector2.zero;
        }
    }
}
