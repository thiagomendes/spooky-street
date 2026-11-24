using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Localization;

namespace SpookyStreet.Tests.Core
{
    /// <summary>CLDR plural rules and schema 2 string tables (S004 stream D1).</summary>
    public class PluralRulesTests
    {
        private const string PluralTable =
            "{\"schemaVersion\":2,\"locale\":\"en\",\"nativeName\":\"English\",\"entries\":[" +
            "{\"key\":\"bats\",\"plural\":{\"one\":\"{0} bat\",\"other\":\"{0} bats\"},\"context\":\"c\"}," +
            "{\"key\":\"plain\",\"value\":\"Hi\",\"context\":\"c\"}]}";

        [TestCase("en", 0, PluralCategory.Other)]
        [TestCase("en", 1, PluralCategory.One)]
        [TestCase("en", 2, PluralCategory.Other)]
        [TestCase("de", 1, PluralCategory.One)]
        [TestCase("de", 7, PluralCategory.Other)]
        [TestCase("pt-BR", 0, PluralCategory.One)]
        [TestCase("pt-BR", 1, PluralCategory.One)]
        [TestCase("pt-BR", 2, PluralCategory.Other)]
        [TestCase("pt-BR", 1000000, PluralCategory.Many)]
        [TestCase("fr", 0, PluralCategory.One)]
        [TestCase("fr", 1, PluralCategory.One)]
        [TestCase("fr", 2, PluralCategory.Other)]
        [TestCase("es-419", 0, PluralCategory.Other)]
        [TestCase("es-419", 1, PluralCategory.One)]
        [TestCase("es-419", 2000000, PluralCategory.Many)]
        [TestCase("it", 1, PluralCategory.One)]
        [TestCase("qps-ploc", 1, PluralCategory.One)]
        [TestCase("ja", 1, PluralCategory.Other)]
        [TestCase("", 1, PluralCategory.Other)]
        public void SelectsTheCldrCategory(string locale, long count, PluralCategory expected)
        {
            Assert.AreEqual(expected, PluralRules.Select(locale, count));
            CollectionAssert.Contains(PluralRules.CategoriesFor(locale), expected);
        }

        [Test]
        public void RequiredFormsLeaveManyToOther()
        {
            CollectionAssert.AreEqual(new[] { PluralCategory.One, PluralCategory.Other }, PluralRules.RequiredFor("pt-BR"));
            CollectionAssert.AreEqual(new[] { PluralCategory.Other }, PluralRules.RequiredFor("ja"));
            Assert.IsTrue(PluralRules.TryParse("few", out var few));
            Assert.AreEqual("few", PluralRules.Name(few));
            Assert.IsFalse(PluralRules.TryParse("several", out _));
        }

        [Test]
        public void SchemaTwoTablesCarryPluralForms()
        {
            var table = StringTable.Parse(PluralTable);
            Assert.IsTrue(table.TryGetEntry("bats", out var entry));
            Assert.IsTrue(entry.IsPlural);
            Assert.AreEqual("{0} bats", entry.Value, "Value is the 'other' form for older callers");
            Assert.IsTrue(table.TryGetPlural("bats", 1, out string one));
            Assert.AreEqual("{0} bat", one);
            Assert.IsTrue(table.TryGetPlural("plain", 1, out string plain));
            Assert.AreEqual("Hi", plain);
        }

        [Test]
        public void SchemaOneStillLoadsButCannotHoldPlurals()
        {
            Assert.AreEqual(1, StringTable.Parse(
                "{\"schemaVersion\":1,\"locale\":\"en\",\"entries\":[{\"key\":\"a\",\"value\":\"A\"}]}").Count);
            Assert.Throws<FormatException>(() => StringTable.Parse(PluralTable.Replace("\"schemaVersion\":2", "\"schemaVersion\":1")));
            Assert.Throws<FormatException>(() => StringTable.Parse(PluralTable.Replace("\"other\":\"{0} bats\"", "\"two\":\"{0}\"")));
            Assert.Throws<FormatException>(() => StringTable.Parse(PluralTable.Replace("\"one\":", "\"several\":")));
            Assert.Throws<FormatException>(() => StringTable.Parse(PluralTable.Replace("\"schemaVersion\":2", "\"schemaVersion\":3")));
        }

        [Test]
        public void TablesRoundTripThroughJson()
        {
            var table = StringTable.Parse(PluralTable);
            var again = StringTable.Parse(table.ToJson());
            Assert.AreEqual(table.Count, again.Count);
            Assert.IsTrue(again.TryGetPlural("bats", 1, out string one));
            Assert.AreEqual("{0} bat", one);
            var english = StringTable.Parse(LocalizationTests.BundledEnglishJson());
            var copy = StringTable.Parse(english.ToJson());
            foreach (var entry in english.Entries)
            {
                Assert.IsTrue(copy.TryGetEntry(entry.Key, out var back), entry.Key);
                CollectionAssert.AreEqual(entry.Texts, back.Texts, entry.Key);
                Assert.AreEqual(entry.Context, back.Context, entry.Key);
            }
        }

        [Test]
        public void CatalogPicksTheFormByTheTableThatHasTheKey()
        {
            var english = StringTable.Parse(PluralTable);
            var portuguese = new StringTable("pt-BR", "Português", "1", new[]
            {
                new StringEntry("bats", null, null, new Dictionary<PluralCategory, string>
                {
                    [PluralCategory.One] = "{0} morcego",
                    [PluralCategory.Other] = "{0} morcegos",
                }),
            });
            var catalog = new LocalizationCatalog(english);
            Assert.AreEqual("1 bat", catalog.Plural("bats", 1, 1));
            Assert.AreEqual("0 bats", catalog.Plural("bats", 0, 0));
            catalog.SetActive(portuguese);
            Assert.AreEqual("0 morcego", catalog.Plural("bats", 0, 0), "pt: zero takes the 'one' form");
            Assert.AreEqual("1000000 morcegos", catalog.Plural("bats", 1000000, 1000000), "'many' falls back to 'other'");
            Assert.AreEqual("Hi", catalog.Plural("plain", 3), "a key missing from the pack falls back to English");
            Assert.AreEqual("⟦nope⟧", catalog.Plural("nope", 1));
        }

        [Test]
        public void BundledEnglishUsesPluralsForCounts()
        {
            var catalog = new LocalizationCatalog(StringTable.Parse(LocalizationTests.BundledEnglishJson()));
            Assert.AreEqual("1 sticker", catalog.Plural(LocalizationKeys.Recap.Stickers, 1, 1));
            Assert.AreEqual("5 stickers", catalog.Plural(LocalizationKeys.Recap.Stickers, 5, 5));
            Assert.AreEqual("How? · 1 paper bat", catalog.Plural(LocalizationKeys.TitleContinueDetail, 1, "How?", 1));
            Assert.AreEqual("5 stickers", catalog.Format(LocalizationKeys.Recap.Stickers, 5), "Format shows 'other'");
        }

        [Test]
        public void PseudoTablesKeepPluralFormsAndUseOnlyBundledGlyphs()
        {
            var english = StringTable.Parse(LocalizationTests.BundledEnglishJson());
            var pseudo = PseudoLocalizer.CreateTable(english);
            Assert.IsTrue(pseudo.TryGetEntry(LocalizationKeys.Recap.Stickers, out var stickers));
            Assert.IsTrue(stickers.IsPlural);
            StringAssert.StartsWith("[", stickers.Form(PluralCategory.One));
            StringAssert.Contains("{0}", stickers.Form(PluralCategory.One));
            Assert.IsNull(PackValidator.MissingGlyph(pseudo.NativeName, FontCoverage.Covers));
            foreach (var entry in pseudo.Entries)
            {
                foreach (string text in entry.Texts)
                {
                    Assert.IsNull(PackValidator.MissingGlyph(text, FontCoverage.Covers), entry.Key + ": " + text);
                }
            }
        }

        [Test]
        public void FontCoverageHasLatinOneAndNoCyrillic()
        {
            foreach (char c in "AZaz09 ãçéõúÀÑß·…–—’“”")
            {
                Assert.IsTrue(FontCoverage.Covers(c), "U+" + ((int)c).ToString("X4"));
            }

            Assert.IsTrue(FontCoverage.Covers('\n'), "line breaks are layout, not glyphs");
            Assert.IsFalse(FontCoverage.Covers('Ж'), "Sniglet has no Cyrillic");
            Assert.IsFalse(FontCoverage.Covers(0x4E2D), "no CJK");
            Assert.IsFalse(FontCoverage.Covers(0x1F600), "no emoji");
        }
    }
}
