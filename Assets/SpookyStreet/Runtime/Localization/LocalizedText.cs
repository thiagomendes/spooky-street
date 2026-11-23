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
        private Func<long> _count;

        public string Key => key;

        public void SetKey(string newKey, Func<object[]> arguments = null)
        {
            key = newKey;
            _arguments = arguments;
            _count = null;
            Refresh();
        }

        /// <summary>A count-dependent key (schema 2 plural forms): the form follows <paramref name="count"/>.</summary>
        public void SetPlural(string newKey, Func<long> count, Func<object[]> arguments)
        {
            key = newKey;
            _arguments = arguments;
            _count = count;
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

            if (_count != null)
            {
                _text.text = Localizer.Plural(key, _count(), _arguments != null ? _arguments() : new object[0]);
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
