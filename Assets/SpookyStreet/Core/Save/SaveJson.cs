using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using SpookyStreet.Core.Localization;

namespace SpookyStreet.Core.Save
{
    /// <summary>
    /// Minimal JSON writer for save files (objects of numbers, booleans, strings and string arrays) plus typed
    /// readers over <see cref="MiniJson"/> output. Deterministic output: same data, same bytes.
    /// </summary>
    public sealed class SaveJsonWriter
    {
        private readonly StringBuilder _text = new StringBuilder(256);
        private bool _first = true;

        public SaveJsonWriter()
        {
            _text.Append('{');
        }

        public SaveJsonWriter Number(string name, double value)
        {
            Name(name);
            _text.Append(value.ToString("R", CultureInfo.InvariantCulture));
            return this;
        }

        public SaveJsonWriter Bool(string name, bool value)
        {
            Name(name);
            _text.Append(value ? "true" : "false");
            return this;
        }

        public SaveJsonWriter String(string name, string value)
        {
            Name(name);
            Quote(value);
            return this;
        }

        public SaveJsonWriter Strings(string name, IEnumerable<string> values)
        {
            Name(name);
            _text.Append('[');
            bool first = true;
            foreach (string value in values)
            {
                if (!first)
                {
                    _text.Append(',');
                }

                _text.Append("\n    ");
                Quote(value);
                first = false;
            }

            _text.Append(first ? "]" : "\n  ]");
            return this;
        }

        public override string ToString() => _text + "\n}\n";

        private void Name(string name)
        {
            _text.Append(_first ? "\n  " : ",\n  ");
            _first = false;
            Quote(name);
            _text.Append(": ");
        }

        private void Quote(string value)
        {
            if (value == null)
            {
                _text.Append("null");
                return;
            }

            _text.Append('"');
            foreach (char c in value)
            {
                switch (c)
                {
                    case '"':
                        _text.Append("\\\"");
                        break;
                    case '\\':
                        _text.Append("\\\\");
                        break;
                    case '\n':
                        _text.Append("\\n");
                        break;
                    case '\r':
                        _text.Append("\\r");
                        break;
                    case '\t':
                        _text.Append("\\t");
                        break;
                    default:
                        if (c < 0x20)
                        {
                            _text.Append("\\u").Append(((int)c).ToString("x4", CultureInfo.InvariantCulture));
                        }
                        else
                        {
                            _text.Append(c);
                        }

                        break;
                }
            }

            _text.Append('"');
        }
    }

    /// <summary>Typed field access over a parsed JSON object; every reader is tolerant and reports via bool.</summary>
    public static class SaveJsonReader
    {
        /// <summary>Parses <paramref name="json"/> as a JSON object, or returns null if it is not one.</summary>
        public static Dictionary<string, object> ParseObject(string json)
        {
            if (string.IsNullOrEmpty(json))
            {
                return null;
            }

            try
            {
                return MiniJson.Parse(json) as Dictionary<string, object>;
            }
            catch (FormatException)
            {
                return null;
            }
        }

        public static bool TryInt(Dictionary<string, object> obj, string name, out int value)
        {
            value = 0;
            if (!obj.TryGetValue(name, out object raw) || !(raw is double d) || d != Math.Floor(d) ||
                d < int.MinValue || d > int.MaxValue)
            {
                return false;
            }

            value = (int)d;
            return true;
        }

        public static bool TryNumber(Dictionary<string, object> obj, string name, out double value)
        {
            value = 0;
            if (obj.TryGetValue(name, out object raw) && raw is double d && !double.IsNaN(d) && !double.IsInfinity(d))
            {
                value = d;
                return true;
            }

            return false;
        }

        public static bool TryBool(Dictionary<string, object> obj, string name, out bool value)
        {
            value = false;
            if (obj.TryGetValue(name, out object raw) && raw is bool b)
            {
                value = b;
                return true;
            }

            return false;
        }

        public static bool TryString(Dictionary<string, object> obj, string name, out string value)
        {
            value = null;
            if (obj.TryGetValue(name, out object raw) && raw is string s)
            {
                value = s;
                return true;
            }

            return false;
        }

        /// <summary>A JSON array whose items are all strings.</summary>
        public static bool TryStrings(Dictionary<string, object> obj, string name, out List<string> values)
        {
            values = null;
            if (!obj.TryGetValue(name, out object raw) || !(raw is List<object> list))
            {
                return false;
            }

            var strings = new List<string>(list.Count);
            foreach (object item in list)
            {
                if (!(item is string s))
                {
                    return false;
                }

                strings.Add(s);
            }

            values = strings;
            return true;
        }
    }
}
