using System;
using System.Collections.Generic;
using System.Text;

namespace SpookyStreet.Core.Localization
{
    /// <summary>
    /// Generates the engineering pseudo-language (spec §7: "+35% string expansion"). Letters become
    /// accented look-alikes, text is padded and bracketed so truncation is obvious, and format
    /// placeholders such as {0} and rich-text tags such as &lt;b&gt; are preserved untouched.
    /// </summary>
    public static class PseudoLocalizer
    {
        public const string Locale = "qps-ploc";
        public const float DefaultExpansion = 0.35f;

        private const string Plain = "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ";
        private const string Accented = "áƀçđéƒğĥíĵķĺɱñóþǫŕšŧúṽŵẋýžÁƁÇĐÉƑĞĤÍĴĶĹṀÑÓÞǪŔŠŦÚṼŴẊÝŽ";

        public static string Transform(string source, float expansion = DefaultExpansion)
        {
            if (string.IsNullOrEmpty(source))
            {
                return source;
            }

            var sb = new StringBuilder(source.Length * 2);
            int visible = 0;
            sb.Append('[');
            for (int i = 0; i < source.Length; i++)
            {
                char c = source[i];
                if (c == '{' || c == '<')
                {
                    char close = c == '{' ? '}' : '>';
                    int end = source.IndexOf(close, i + 1);
                    if (end > i)
                    {
                        sb.Append(source, i, end - i + 1);
                        i = end;
                        continue;
                    }
                }

                int index = Plain.IndexOf(c);
                sb.Append(index >= 0 ? Accented[index] : c);
                visible++;
            }

            int padding = (int)Math.Ceiling(visible * expansion);
            for (int i = 0; i < padding; i++)
            {
                sb.Append(i % 2 == 0 ? '~' : '·');
            }

            sb.Append(']');
            return sb.ToString();
        }

        /// <summary>Builds the pseudo-language table from a source table (normally bundled English).</summary>
        public static StringTable CreateTable(StringTable source, float expansion = DefaultExpansion)
        {
            var entries = new List<StringEntry>(source.Count);
            foreach (var entry in source.Entries)
            {
                entries.Add(new StringEntry(entry.Key, Transform(entry.Value, expansion), entry.Context));
            }

            return new StringTable(Locale, "[Ƥšéúđó]", source.ContentVersion, entries);
        }
    }
}
