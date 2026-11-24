using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.Localization
{
    /// <summary>CLDR plural categories (https://cldr.unicode.org/index/cldr-spec/plural-rules).</summary>
    public enum PluralCategory
    {
        Zero,
        One,
        Two,
        Few,
        Many,
        Other,
    }

    /// <summary>
    /// Engine-free CLDR cardinal plural rules for whole counts, for the languages the bundled fonts cover (S004 §2.4):
    /// English, Portuguese (0 and 1 are "one"), Spanish, French (0 and 1 are "one"), German, Italian and Dutch. Counts
    /// in the game are whole numbers (stickers, bats), so the decimal operands of the CLDR rules are not needed. Any
    /// other language uses "other" for every count until its rule is added here.
    /// </summary>
    public static class PluralRules
    {
        private static readonly PluralCategory[] OneOther = { PluralCategory.One, PluralCategory.Other };
        private static readonly PluralCategory[] OneManyOther = { PluralCategory.One, PluralCategory.Many, PluralCategory.Other };
        private static readonly PluralCategory[] OtherOnly = { PluralCategory.Other };

        /// <summary>The category a count takes in <paramref name="locale"/> (BCP-47 tag, e.g. "pt-BR").</summary>
        public static PluralCategory Select(string locale, long count)
        {
            long n = Math.Abs(count);
            switch (Language(locale))
            {
                case "en":
                case "de":
                case "nl":
                case "qps":
                    return n == 1 ? PluralCategory.One : PluralCategory.Other;
                case "pt":
                case "fr":
                    // CLDR: i = 0,1 → one; e = 0 and i ≠ 0 and i % 1000000 = 0 → many (whole millions).
                    if (n <= 1)
                    {
                        return PluralCategory.One;
                    }

                    return n % 1000000 == 0 ? PluralCategory.Many : PluralCategory.Other;
                case "es":
                case "it":
                    if (n == 1)
                    {
                        return PluralCategory.One;
                    }

                    return n != 0 && n % 1000000 == 0 ? PluralCategory.Many : PluralCategory.Other;
                default:
                    return PluralCategory.Other;
            }
        }

        /// <summary>Every category <see cref="Select"/> can return for whole counts in the locale.</summary>
        public static IReadOnlyList<PluralCategory> CategoriesFor(string locale)
        {
            switch (Language(locale))
            {
                case "en":
                case "de":
                case "nl":
                case "qps":
                    return OneOther;
                case "pt":
                case "fr":
                case "es":
                case "it":
                    return OneManyOther;
                default:
                    return OtherOnly;
            }
        }

        /// <summary>Categories a translation must spell out: "many" may fall back to "other" (CLDR allows it here).</summary>
        public static IReadOnlyList<PluralCategory> RequiredFor(string locale)
        {
            var required = new List<PluralCategory>();
            foreach (var category in CategoriesFor(locale))
            {
                if (category != PluralCategory.Many)
                {
                    required.Add(category);
                }
            }

            return required;
        }

        public static string Name(PluralCategory category)
        {
            switch (category)
            {
                case PluralCategory.Zero: return "zero";
                case PluralCategory.One: return "one";
                case PluralCategory.Two: return "two";
                case PluralCategory.Few: return "few";
                case PluralCategory.Many: return "many";
                default: return "other";
            }
        }

        public static bool TryParse(string name, out PluralCategory category)
        {
            switch (name)
            {
                case "zero": category = PluralCategory.Zero; return true;
                case "one": category = PluralCategory.One; return true;
                case "two": category = PluralCategory.Two; return true;
                case "few": category = PluralCategory.Few; return true;
                case "many": category = PluralCategory.Many; return true;
                case "other": category = PluralCategory.Other; return true;
                default: category = PluralCategory.Other; return false;
            }
        }

        /// <summary>The language subtag in lower case ("pt-BR" → "pt", "qps-ploc" → "qps").</summary>
        public static string Language(string locale)
        {
            if (string.IsNullOrEmpty(locale))
            {
                return string.Empty;
            }

            int dash = locale.IndexOfAny(new[] { '-', '_' });
            return (dash < 0 ? locale : locale.Substring(0, dash)).ToLowerInvariant();
        }
    }
}
