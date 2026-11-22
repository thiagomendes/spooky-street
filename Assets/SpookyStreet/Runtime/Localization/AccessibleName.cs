using UnityEngine;

namespace SpookyStreet.Localization
{
    /// <summary>
    /// The localized accessibility name of an icon-only control (docs/ui-language.md "Captions and accessibility"),
    /// kept as a key so it follows the language. <see cref="Name"/> is what a screen-reader bridge announces; the
    /// project does not ship the OS bridge yet (Unity's accessibility module is not in the manifest), so for now the
    /// name is also what tests and the small captions read.
    /// </summary>
    public sealed class AccessibleName : MonoBehaviour
    {
        [SerializeField] private string key;

        public string Key => key;

        /// <summary>The name in the current language.</summary>
        public string Name => string.IsNullOrEmpty(key) ? string.Empty : Localizer.Get(key);

        /// <summary>Names <paramref name="control"/> with <paramref name="nameKey"/> (adds the component if needed).</summary>
        public static AccessibleName Set(GameObject control, string nameKey)
        {
            var name = control.GetComponent<AccessibleName>();
            if (name == null)
            {
                name = control.AddComponent<AccessibleName>();
            }

            name.key = nameKey;
            return name;
        }
    }
}
