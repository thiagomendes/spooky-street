using UnityEngine;
using UnityEngine.UI;

namespace SpookyStreet.UI
{
    /// <summary>
    /// The stitched focus outline for gamepad and keyboard (S004 F1, docs/ui-language.md: candy-corn stitch): the
    /// orange "this matters" key ring is retired from case content (docs/mystery-first.md §8) and this candy-corn
    /// stitch is the one sign of UI focus on every screen. It is a shape, not only a colour, so it reads without
    /// colour vision. It never catches touches. Put it on an overlay above the elements it rings; it follows its target
    /// every frame (a sliding page, a sticker gliding home) and hides when the target goes away.
    /// S004 F2 (frame 81: the yellow stitch alone was faint on the notebook's cream paper): the stitch now runs on a
    /// solid ink band (<see cref="Edge"/>), so the ring has a dark edge on paper and a candy one on felt.
    /// </summary>
    public sealed class UiFocus : MonoBehaviour
    {
        /// <summary>How far the outline's rect reaches past each side of the target.</summary>
        public const float Margin = 8f;

        /// <summary>How far the stitch reaches past the outline's rect.</summary>
        public const float StitchReach = 6f;

        private RectTransform _rect;

        /// <summary>The element ringed now (null while hidden).</summary>
        public RectTransform Target { get; private set; }

        /// <summary>The candy-corn running stitch (null when the stitch sprite is missing).</summary>
        public Image Thread { get; private set; }

        /// <summary>The solid ink band under the stitch (always there, so focus never depends on the stitch art).</summary>
        public Image Edge { get; private set; }

        /// <summary>The band's ink: dark against the cream paper, with the light stitch drawn on top of it.</summary>
        public static readonly Color EdgeColor = new Color(0.118f, 0.102f, 0.149f, 0.92f);

        public bool Showing => gameObject.activeSelf && Target != null;

        public static UiFocus Create(Transform overlay, string name = "Focus")
        {
            var ring = UiFactory.Sprite(overlay, name, IconFactory.RoundedRect, new Color(0f, 0f, 0f, 0f),
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.one);
            ring.raycastTarget = false;
            var focus = ring.gameObject.AddComponent<UiFocus>();
            focus._rect = ring.rectTransform;
            var edge = UiFactory.Sprite(ring.transform, "InkEdge", IconFactory.RoundedFrame, EdgeColor,
                new Vector2(0.5f, 0.5f), Vector2.zero, Vector2.zero);
            edge.type = Image.Type.Sliced;
            edge.raycastTarget = false;
            UiFactory.Stretch(edge.rectTransform);
            edge.rectTransform.offsetMin = new Vector2(-StitchReach - 2f, -StitchReach - 2f);
            edge.rectTransform.offsetMax = new Vector2(StitchReach + 2f, StitchReach + 2f);
            focus.Edge = edge;
            focus.Thread = UiFactory.StitchLayer(ring.transform, Palette.CandyCornYellow);
            if (focus.Thread != null)
            {
                focus.Thread.rectTransform.offsetMin = new Vector2(-StitchReach, -StitchReach);
                focus.Thread.rectTransform.offsetMax = new Vector2(StitchReach, StitchReach);
            }

            ring.gameObject.SetActive(false);
            return focus;
        }

        /// <summary>Rings <paramref name="target"/> (null hides).</summary>
        public void Show(RectTransform target)
        {
            Target = target;
            if (target == null)
            {
                gameObject.SetActive(false);
                return;
            }

            gameObject.SetActive(true);
            Follow();
        }

        public void Hide() => Show(null);

        private void LateUpdate()
        {
            if (Target == null || !Target.gameObject.activeInHierarchy)
            {
                Hide();
                return;
            }

            Follow();
        }

        private void Follow()
        {
            _rect.position = Target.position;
            // The target's size in the outline's own space (they may sit under differently scaled parents).
            var lossy = Target.lossyScale;
            var own = _rect.parent != null ? _rect.parent.lossyScale : Vector3.one;
            var size = new Vector2(Target.rect.width * lossy.x / Mathf.Max(1e-5f, own.x),
                Target.rect.height * lossy.y / Mathf.Max(1e-5f, own.y));
            _rect.sizeDelta = size + new Vector2(Margin * 2f, Margin * 2f);
            _rect.rotation = Target.rotation;
        }
    }
}
