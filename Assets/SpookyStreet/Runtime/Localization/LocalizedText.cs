using System;
using UnityEngine;
using UnityEngine.UI;

namespace SpookyStreet.Localization
{
    /// <summary>Binds a uGUI Text to a stable key and refreshes it when the language changes.</summary>
    [RequireComponent(typeof(Text))]
    public sealed class LocalizedText : MonoBehaviour
    {
        [SerializeField] private string key;

        private Text _text;
        private Func<object[]> _arguments;

        public string Key => key;

        public void SetKey(string newKey, Func<object[]> arguments = null)
        {
            key = newKey;
            _arguments = arguments;
            Refresh();
        }

        public void Refresh()
        {
            if (_text == null)
            {
                _text = GetComponent<Text>();
            }

            if (_text == null || string.IsNullOrEmpty(key))
            {
                return;
            }

            _text.text = _arguments == null ? Localizer.Get(key) : Localizer.Format(key, _arguments());
        }

        private void OnEnable()
        {
            Localizer.Catalog.Changed += Refresh;
            Refresh();
        }

        private void OnDisable()
        {
            Localizer.Catalog.Changed -= Refresh;
        }
    }
}
