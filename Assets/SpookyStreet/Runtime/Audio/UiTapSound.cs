using UnityEngine;
using UnityEngine.UI;

namespace SpookyStreet.Audio
{
    /// <summary>
    /// Marks a uGUI <see cref="Button"/> whose clicks play the UI tap (<see cref="GameAudio"/> adds one to every button
    /// it finds, so new screens get the tap without any code of their own). Set <see cref="Silent"/> on a control that
    /// plays its own sound instead (a notebook sticker plays its peel, not a tap).
    /// </summary>
    [DisallowMultipleComponent]
    public sealed class UiTapSound : MonoBehaviour
    {
        /// <summary>No tap for this button (it plays its own sound).</summary>
        public bool Silent;

        /// <summary>Taps played for this button (tests).</summary>
        public int Taps { get; private set; }

        internal static UiTapSound Attach(Button button, System.Action onTap)
        {
            var marker = button.gameObject.AddComponent<UiTapSound>();
            button.onClick.AddListener(() =>
            {
                if (marker == null || marker.Silent)
                {
                    return;
                }

                marker.Taps++;
                onTap();
            });
            return marker;
        }
    }
}
