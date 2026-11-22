using SpookyStreet.Core.Art;
using UnityEngine;
using UnityEngine.UI;

namespace SpookyStreet.UI
{
    /// <summary>
    /// The interaction marker (docs/ui-language.md "Motion"): a small bone felt disc with the target's glyph that
    /// floats over the selected Use target and bobs 2 pt at 1.2 Hz, so the player sees what Use will act on. The verb
    /// itself stays on the Use disc's paper tag. Hidden with no target, behind the camera or while a modal is open.
    /// </summary>
    public sealed class InteractionPrompt : MonoBehaviour
    {
        public const float BobHz = 1.2f;
        public const float BobUnits = 4f;

        private RectTransform _canvasRect;
        private RectTransform _marker;
        private Image _icon;
        private string _iconName;
        private float _phase;

        /// <summary>Reduced-motion hook: no bob.</summary>
        public bool ReducedMotion { get; set; }

        public bool Visible => _marker.gameObject.activeSelf;

        public static InteractionPrompt Create(RectTransform canvasRoot)
        {
            var go = UiFactory.Rect(canvasRoot, "InteractionPrompt");
            UiFactory.Stretch(go);
            var prompt = go.gameObject.AddComponent<InteractionPrompt>();
            prompt._canvasRect = canvasRoot;
            var disc = UiFactory.Sprite(go, "Marker", null, Color.white, new Vector2(0.5f, 0.5f), Vector2.zero,
                new Vector2(72f, 72f));
            UiFactory.ApplySurface(disc, UiSpriteCatalog.FeltDiscBone, Palette.WarmPaper, new Color(0.93f, 0.93f, 0.93f, 1f));
            UiFactory.InkShadow(disc, new Vector2(2f, -3f), 0.5f);
            UiFactory.Sprite(disc.transform, "Ring", UiFactory.Baked(UiSpriteCatalog.StitchRing), Palette.JackOrange,
                new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(72f, 72f));
            prompt._icon = UiFactory.Sprite(disc.transform, "Icon", UiFactory.Baked(UiSpriteCatalog.IconExamine),
                UiFactory.Ink, new Vector2(0.5f, 0.5f), Vector2.zero, new Vector2(40f, 40f));
            prompt._iconName = UiSpriteCatalog.IconExamine;
            prompt._marker = disc.rectTransform;
            prompt._marker.gameObject.SetActive(false);
            return prompt;
        }

        /// <summary>Places the marker over <paramref name="world"/>, or hides it when <paramref name="world"/> is null.</summary>
        public void Track(Camera camera, Vector3? world, string iconName)
        {
            bool show = world.HasValue && camera != null;
            Vector3 screen = Vector3.zero;
            if (show)
            {
                screen = camera.WorldToScreenPoint(world.Value);
                show = screen.z > 0.1f;
            }

            if (_marker.gameObject.activeSelf != show)
            {
                _marker.gameObject.SetActive(show);
            }

            if (!show)
            {
                return;
            }

            if (iconName != null && iconName != _iconName)
            {
                _iconName = iconName;
                _icon.sprite = UiFactory.Baked(iconName);
            }

            RectTransformUtility.ScreenPointToLocalPointInRectangle(_canvasRect, screen, null, out Vector2 local);
            _phase += Time.deltaTime * BobHz * 2f * Mathf.PI;
            float bob = ReducedMotion ? 0f : Mathf.Sin(_phase) * BobUnits;
            _marker.anchoredPosition = local + new Vector2(0f, bob);
        }
    }
}
