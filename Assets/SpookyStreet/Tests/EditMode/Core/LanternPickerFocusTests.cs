using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Localization;
using SpookyStreet.Core.LivingStreet;
using SpookyStreet.Core.World;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// The lantern picker (living-street §4.2, D35): every shape and colour and the fold button are reachable with the
    /// d-pad or the arrow keys from where the screen opens (touch presses the same buttons), every shape has a name key,
    /// and the shape and colour IDs are the lantern art's (docs/living-street-lantern-art.md).
    /// </summary>
    public class LanternPickerFocusTests
    {
        [Test]
        public void EveryShapeColourAndTheFoldButtonAreReachableWithTheDpad()
        {
            var graph = LanternCraft.FocusGraph();
            var seen = new HashSet<string> { graph.DefaultFocus };
            var queue = new Queue<string>(seen);
            while (queue.Count > 0)
            {
                string at = queue.Dequeue();
                foreach (UiDirection d in new[] { UiDirection.Up, UiDirection.Down, UiDirection.Left, UiDirection.Right })
                {
                    string next = graph.Move(at, d);
                    if (next != null && seen.Add(next))
                    {
                        queue.Enqueue(next);
                    }
                }
            }

            var expected = LanternCraft.Shapes.Select(LanternCraft.FocusShape)
                .Concat(LanternCraft.Colours.Select(LanternCraft.FocusColour))
                .Concat(new[] { LanternCraft.FocusFold, LanternCraft.FocusClose });
            CollectionAssert.IsSubsetOf(expected.ToList(), seen.ToList());
            Assert.AreEqual(LanternCraft.FocusClose, graph.Exit, "Back closes the screen");

            // Right walks the shapes in order; Down goes from the shapes to the colours, then to the fold button.
            string focus = LanternCraft.FocusShape(LanternShape.Bat);
            foreach (var shape in LanternCraft.Shapes.Skip(1))
            {
                focus = graph.Move(focus, UiDirection.Right);
                Assert.AreEqual(LanternCraft.FocusShape(shape), focus);
            }

            StringAssert.StartsWith("colour.", graph.Move(focus, UiDirection.Down));
            Assert.AreEqual(LanternCraft.FocusFold, graph.Move(graph.Move(focus, UiDirection.Down), UiDirection.Down));
        }

        [Test]
        public void TheShapesHaveNamesAndTheArtsIds()
        {
            var english = StringTable.Parse(LocalizationTests.BundledEnglishJson());
            foreach (var shape in LanternCraft.Shapes)
            {
                Assert.IsTrue(english.TryGetEntry(LanternCraft.ShapeKey(shape), out var entry));
                Assert.IsNotEmpty(entry.Value);
                Assert.IsTrue(LanternArt.IsShape(LanternCraft.ShapeId(shape)), "the lantern art has the " + shape + " model");
            }

            foreach (var colour in LanternCraft.Colours)
            {
                Assert.IsTrue(LanternArt.IsColour(LanternCraft.ColourId(colour)));
                Assert.AreEqual(LanternArt.PaperRgb(LanternCraft.ColourId(colour)), LanternCraft.ColourRgb(colour),
                    "the picker tints with the art's paper colour");
            }

            Assert.AreEqual(LanternArt.Table, LivingStreetLayout.Props.Single(p => p.Model == LanternArt.Table).Model,
                "the Green's craft table is the lantern art's table");
        }
    }
}
