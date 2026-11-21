using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace SpookyStreet.Core.Localization
{
    /// <summary>
    /// Minimal strict JSON reader (objects → Dictionary&lt;string, object&gt;, arrays → List&lt;object&gt;,
    /// numbers → double). Kept engine-free so string tables and future pack manifests can be parsed and
    /// tested without Unity. Throws FormatException with a character offset on malformed input.
    /// </summary>
    public static class MiniJson
    {
        public static object Parse(string json)
        {
            if (json == null)
            {
                throw new ArgumentNullException(nameof(json));
            }

            var reader = new Reader(json);
            reader.SkipWhitespace();
            object value = reader.ReadValue();
            reader.SkipWhitespace();
            if (!reader.AtEnd)
            {
                throw reader.Error("unexpected trailing content");
            }

            return value;
        }

        private sealed class Reader
        {
            private readonly string _text;
            private int _pos;

            public Reader(string text)
            {
                _text = text;
                // Tolerate a UTF-8 byte order mark written by some editors.
                if (_text.Length > 0 && _text[0] == '﻿')
                {
                    _pos = 1;
                }
            }

            public bool AtEnd => _pos >= _text.Length;

            public FormatException Error(string message) =>
                new FormatException($"JSON parse error at offset {_pos}: {message}");

            public void SkipWhitespace()
            {
                while (!AtEnd && (_text[_pos] == ' ' || _text[_pos] == '\t' || _text[_pos] == '\n' || _text[_pos] == '\r'))
                {
                    _pos++;
                }
            }

            public object ReadValue()
            {
                if (AtEnd)
                {
                    throw Error("unexpected end of input");
                }

                char c = _text[_pos];
                switch (c)
                {
                    case '{': return ReadObject();
                    case '[': return ReadArray();
                    case '"': return ReadString();
                    case 't': ExpectLiteral("true"); return true;
                    case 'f': ExpectLiteral("false"); return false;
                    case 'n': ExpectLiteral("null"); return null;
                    default:
                        if (c == '-' || (c >= '0' && c <= '9'))
                        {
                            return ReadNumber();
                        }

                        throw Error($"unexpected character '{c}'");
                }
            }

            private Dictionary<string, object> ReadObject()
            {
                var result = new Dictionary<string, object>(StringComparer.Ordinal);
                _pos++; // {
                SkipWhitespace();
                if (Peek('}'))
                {
                    _pos++;
                    return result;
                }

                while (true)
                {
                    SkipWhitespace();
                    if (!Peek('"'))
                    {
                        throw Error("expected property name");
                    }

                    string key = ReadString();
                    if (result.ContainsKey(key))
                    {
                        throw Error($"duplicate property '{key}'");
                    }

                    SkipWhitespace();
                    Expect(':');
                    SkipWhitespace();
                    result[key] = ReadValue();
                    SkipWhitespace();
                    if (Peek(','))
                    {
                        _pos++;
                        continue;
                    }

                    Expect('}');
                    return result;
                }
            }

            private List<object> ReadArray()
            {
                var result = new List<object>();
                _pos++; // [
                SkipWhitespace();
                if (Peek(']'))
                {
                    _pos++;
                    return result;
                }

                while (true)
                {
                    SkipWhitespace();
                    result.Add(ReadValue());
                    SkipWhitespace();
                    if (Peek(','))
                    {
                        _pos++;
                        continue;
                    }

                    Expect(']');
                    return result;
                }
            }

            private string ReadString()
            {
                Expect('"');
                var sb = new StringBuilder();
                while (true)
                {
                    if (AtEnd)
                    {
                        throw Error("unterminated string");
                    }

                    char c = _text[_pos++];
                    if (c == '"')
                    {
                        return sb.ToString();
                    }

                    if (c < ' ')
                    {
                        throw Error("control character in string");
                    }

                    if (c != '\\')
                    {
                        sb.Append(c);
                        continue;
                    }

                    if (AtEnd)
                    {
                        throw Error("unterminated escape");
                    }

                    char e = _text[_pos++];
                    switch (e)
                    {
                        case '"': sb.Append('"'); break;
                        case '\\': sb.Append('\\'); break;
                        case '/': sb.Append('/'); break;
                        case 'b': sb.Append('\b'); break;
                        case 'f': sb.Append('\f'); break;
                        case 'n': sb.Append('\n'); break;
                        case 'r': sb.Append('\r'); break;
                        case 't': sb.Append('\t'); break;
                        case 'u':
                            if (_pos + 4 > _text.Length)
                            {
                                throw Error("truncated unicode escape");
                            }

                            string hex = _text.Substring(_pos, 4);
                            if (!int.TryParse(hex, NumberStyles.HexNumber, CultureInfo.InvariantCulture, out int code))
                            {
                                throw Error("invalid unicode escape");
                            }

                            sb.Append((char)code);
                            _pos += 4;
                            break;
                        default:
                            throw Error($"invalid escape '\\{e}'");
                    }
                }
            }

            private double ReadNumber()
            {
                int start = _pos;
                if (Peek('-'))
                {
                    _pos++;
                }

                while (!AtEnd && "0123456789.eE+-".IndexOf(_text[_pos]) >= 0)
                {
                    _pos++;
                }

                string token = _text.Substring(start, _pos - start);
                if (!double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out double value))
                {
                    throw Error($"invalid number '{token}'");
                }

                return value;
            }

            private void ExpectLiteral(string literal)
            {
                if (string.CompareOrdinal(_text, _pos, literal, 0, literal.Length) != 0)
                {
                    throw Error($"expected '{literal}'");
                }

                _pos += literal.Length;
            }

            private bool Peek(char c) => !AtEnd && _text[_pos] == c;

            private void Expect(char c)
            {
                if (!Peek(c))
                {
                    throw Error($"expected '{c}'");
                }

                _pos++;
            }
        }
    }
}
