using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpookyStreet.Core.Flow;
using F = SpookyStreet.Core.Flow.LanguageScreenFocus;

namespace SpookyStreet.Tests.Core
{
    /// <summary>The language screen's focus graph (S004 D1 on F1): sound in every state the screen shows.</summary>
    public class LanguageScreenFocusTests
    {
        private static UiFocusGraph WithEnabled(params string[] enabled)
        {
            var graph = F.Build();
            foreach (string id in graph.Order)
            {
                graph.SetEnabled(id, id == F.Close || Array.IndexOf(enabled, id) >= 0);
            }

            return graph;
        }

        [Test]
        public void EveryStateOfTheLanguageScreenIsSound()
        {
            var states = new List<(string, string[])>
            {
                ("English only, in use, no host configured", new string[0]),
                ("English only, in use", new[] { F.More }),
                ("after the gate: the pseudo pack can be downloaded", new[] { F.Main(1), F.More }),
                ("downloading", new[] { F.More }),
                ("pseudo downloaded, English in use", new[] { F.Main(1), F.Side(1), F.More }),
                ("pseudo in use", new[] { F.Main(0), F.Side(1), F.More }),
                ("three languages, an update offered", new[] { F.Main(0), F.Main(1), F.Side(1), F.Side(2), F.More }),
                ("every slot", new[] { F.Main(0), F.Side(0), F.Main(1), F.Side(1), F.Main(2), F.Side(2), F.More }),
            };
            foreach (var (name, enabled) in states)
            {
                var problems = WithEnabled(enabled).Validate();
                Assert.IsEmpty(problems, name + ":\n" + string.Join("\n", problems));
            }
        }

        [Test]
        public void FocusStartsOnALanguageAndBackCloses()
        {
            var graph = WithEnabled(F.Main(0), F.Main(1), F.Side(1), F.More);
            Assert.AreEqual(F.Main(0), graph.Start());
            Assert.AreEqual(F.Close, graph.Exit);
            Assert.AreEqual(F.Main(1), graph.Move(F.Main(0), UiDirection.Down));
            Assert.AreEqual(F.Side(1), graph.Move(F.Main(1), UiDirection.Left), "Remove sits left of Use");
            Assert.AreEqual(F.More, graph.Move(F.Main(1), UiDirection.Down));
            Assert.AreEqual(F.Close, graph.Move(F.Main(0), UiDirection.Up));
            Assert.AreEqual(F.More, WithEnabled(F.More).Start(), "English in use: More languages first");
            Assert.AreEqual(F.Close, WithEnabled().Start());
        }
    }
}
