using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpookyStreet.Core.Localization;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// S004 D2: the built-in Brazilian Portuguese table (docs/S004-ptbr.md). It passes the same validator a downloaded
    /// pack does with every key present, stays inside the +35 % length budget the layouts are proven against (the
    /// pseudo pack), and passes the fear-word and conclusion lint in Portuguese (narrative bible §5, CASE-04).
    /// </summary>
    public class PortugueseTableTests
    {
        private static string PortugueseText() =>
            File.ReadAllText(Path.Combine(Path.GetDirectoryName(LocalizationTests.BundledEnglishPath()), "pt-BR.json"));

        private static StringTable English => StringTable.Parse(LocalizationTests.BundledEnglishJson());

        private static StringTable Portuguese => StringTable.Parse(PortugueseText());

        /// <summary>Lower-case words of a text (letters only, accents kept).</summary>
        private static List<string> Words(string text) =>
            Regex.Matches(text.ToLowerInvariant(), @"[\p{L}]+").Cast<Match>().Select(m => m.Value).ToList();

        [Test]
        public void ThePortugueseTablePassesThePackValidatorWithEveryKey()
        {
            string text = PortugueseText();
            var table = StringTable.Parse(text);
            Assert.AreEqual("pt-BR", table.Locale);
            Assert.AreEqual("Português (Brasil)", table.NativeName);
            var manifest = PackManifest.For(table, "pt-BR.json");
            var result = PackValidator.ValidateText(manifest, text, English);
            Assert.IsTrue(result.Ok, result.Error + " " + result.Detail);
            Assert.AreEqual(1f, result.Coverage, "every English key: " + (result.MissingKeys == null ? "" : string.Join(", ", result.MissingKeys)));
            CollectionAssert.AreEquivalent(English.Entries.Select(e => e.Key), table.Entries.Select(e => e.Key),
                "the same keys as English, no extras");
        }

        [Test]
        public void ItIsTheBuiltInSlotOfTheShippedConfig()
        {
            string path = Path.Combine(Path.GetDirectoryName(LocalizationTests.BundledEnglishPath()), "languages.json");
            var slot = LanguageConfig.Parse(File.ReadAllText(path)).BuiltIn.Single();
            Assert.AreEqual(Portuguese.Locale, slot.Locale);
            Assert.AreEqual(Portuguese.NativeName, slot.NativeName);
        }

        [Test]
        public void EveryTextIsTranslatedExceptNamesAndMarks()
        {
            var keep = new HashSet<string>
            {
                "ui.title.game_name", "ui.speaker.pip", "ui.speaker.bea", "ui.speaker.june", "ui.notebook.unknown_mark",
                "ui.notebook.discovery_separator", "ui.caption.with_direction", "ui.language.size_kb",
            };
            var english = English;
            foreach (var entry in Portuguese.Entries)
            {
                Assert.IsTrue(english.TryGetEntry(entry.Key, out var source));
                if (keep.Contains(entry.Key))
                {
                    Assert.AreEqual(source.Value, entry.Value, entry.Key + " stays as it is");
                    continue;
                }

                Assert.AreNotEqual(string.Join("|", source.Texts), string.Join("|", entry.Texts), entry.Key + " is still English");
                Assert.AreEqual(source.Context, entry.Context, entry.Key + ": the translator context is English's");
            }
        }

        /// <summary>
        /// Each text fits in the length of its English text through the pseudo-localizer (+35 % and two brackets),
        /// which is what every screen's layout is checked against in the visual captures.
        /// </summary>
        [Test]
        public void EveryTextStaysWithinThePlus35PercentBudget()
        {
            var english = English;
            var problems = new List<string>();
            foreach (var entry in Portuguese.Entries)
            {
                english.TryGetEntry(entry.Key, out var source);
                var forms = entry.IsPlural ? entry.Plural.Keys.ToList() : new List<PluralCategory> { PluralCategory.Other };
                foreach (var category in forms)
                {
                    string pt = entry.Form(category);
                    int budget = PseudoLocalizer.Transform(source.Form(category)).Length;
                    if (pt.Length > budget)
                    {
                        problems.Add($"{entry.Key} ({category}): {pt.Length} > {budget}: {pt}");
                    }
                }
            }

            Assert.IsEmpty(problems, string.Join("\n", problems));
        }

        [Test]
        public void ThePluralFormsAreOneAndOther()
        {
            foreach (var entry in Portuguese.Entries.Where(e => e.IsPlural))
            {
                CollectionAssert.AreEquivalent(new[] { PluralCategory.One, PluralCategory.Other }, entry.Plural.Keys, entry.Key);
            }

            var table = Portuguese;
            Assert.IsTrue(table.TryGetPlural("ui.title.recap_stickers", 1, out string one));
            Assert.AreEqual("{0} figurinha", one);
            Assert.IsTrue(table.TryGetPlural("ui.title.recap_stickers", 5, out string five));
            Assert.AreEqual("{0} figurinhas", five);
        }

        /// <summary>No fear words anywhere (narrative bible §5: "perigo", "monstro", "morto" and their family).</summary>
        [Test]
        public void NoFearWords()
        {
            var fear = new[]
            {
                "perigo", "perigosa", "perigoso", "monstro", "monstros", "morto", "morta", "mortos", "morte", "morrer",
                "medo", "assustador", "assustadora", "assusta", "terror", "sangue", "caveira", "matar", "fantasma",
                "pesadelo", "grito", "gritou",
            };
            foreach (var entry in Portuguese.Entries)
            {
                foreach (string text in entry.Texts)
                {
                    foreach (string word in Words(text))
                    {
                        CollectionAssert.DoesNotContain(fear, word, entry.Key + ": no fear words: " + text);
                    }
                }
            }
        }

        /// <summary>
        /// Perception-only lines (captions, Pip's looks, clue notes and tags, story-scene lines, hints) never state a
        /// conclusion, and hints never name who or what (the English rules of CutsceneTests and mystery-first.md).
        /// </summary>
        [Test]
        public void PerceptionLinesAndHintsStateNoConclusion()
        {
            var phrases = new[]
            {
                "porque", "por isso", "a resposta", "resolvid", "entrou por", "foi a june", "foi ela", "então a june",
                "significa", "quer dizer", "deve ser", "com certeza",
            };
            var prefixes = new[]
            {
                "c01.caption.", "c01.examine.", "c01.notebook.note_", "c01.notebook.tag_", "c01.cutscene.", "c01.hint.",
                "ui.hint.",
            };
            int checkedLines = 0;
            foreach (var entry in Portuguese.Entries.Where(e => prefixes.Any(p => e.Key.StartsWith(p))))
            {
                string text = entry.Value.ToLowerInvariant();
                checkedLines++;
                foreach (string phrase in phrases)
                {
                    StringAssert.DoesNotContain(phrase, text, entry.Key + ": perception only, never a conclusion");
                }

                if (entry.Key.StartsWith("c01.hint.") || entry.Key.StartsWith("ui.hint."))
                {
                    foreach (string word in new[] { "june", "capa", "cape" })
                    {
                        CollectionAssert.DoesNotContain(Words(entry.Value), word, entry.Key + ": a hint never names who");
                    }
                }
            }

            Assert.Greater(checkedLines, 60);

            // The opening shows nothing the child could find (as in English: no knock, June, silver, owl or flash).
            foreach (var entry in Portuguese.Entries.Where(e => e.Key.StartsWith("c01.cutscene.opening.")))
            {
                foreach (string clue in new[] { "batida", "june", "prate", "coruja", "brilh", "toc" })
                {
                    StringAssert.DoesNotContain(clue, entry.Value.ToLowerInvariant(), entry.Key + ": nothing to find in the opening");
                }
            }
        }

        /// <summary>The owner's name decisions (S004 plan, open decision 2a): place and family titles translated.</summary>
        [Test]
        public void NamesFollowTheOwnersDecision()
        {
            var table = Portuguese;
            table.TryGet("ui.title.case_name", out string caseName);
            StringAssert.Contains("Rua das Lanternas", caseName);
            table.TryGet("c01.cutscene.opening.town", out string town);
            StringAssert.Contains("Candlebrook", town);
            StringAssert.Contains("Rua das Lanternas", town);
            table.TryGet("ui.speaker.kit", out string kit);
            Assert.AreEqual("Tia Kit", kit);
            table.TryGet("ui.speaker.vale", out string vale);
            Assert.AreEqual("Sr. Vale", vale);
            foreach (var entry in table.Entries)
            {
                StringAssert.DoesNotContain("Lantern Lane", entry.Value ?? string.Empty, entry.Key);
                StringAssert.DoesNotContain("Aunt Kit", entry.Value ?? string.Empty, entry.Key);
            }

            // The tips name the Jump button by its label, as the English contexts ask.
            table.TryGet("ui.hud.jump", out string jump);
            foreach (string key in new[] { "tip.move.hold_jump", "tip.move.glide", "ui.hint.move.jump", "ui.hint.move.high_jump", "ui.hint.move.glide" })
            {
                table.TryGet(key, out string tip);
                StringAssert.Contains(jump, tip, key);
            }
        }
    }
}
