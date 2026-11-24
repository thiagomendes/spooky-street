using System.Linq;
using NUnit.Framework;
using SpookyStreet.Core.Flow;
using SpookyStreet.Core.Parent;
using SpookyStreet.Core.Save;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// S004 stream E: the settings card's rows change exactly their <see cref="PreferencesData"/> field, survive a
    /// save and reload, and the card (and the reading sheets) are sound focus graphs.
    /// </summary>
    public class SettingsCardTests
    {
        [Test]
        public void EverySwitchFlipsItsOwnFieldAndPersists()
        {
            var fs = new MemorySaveFileSystem();
            foreach (var row in SettingsCard.All.Where(r => !r.IsSlider))
            {
                var prefs = new PreferencesData();
                string before = PreferencesData.Serialize(prefs);
                bool was = row.Get(prefs);
                Assert.AreEqual(!was, SettingsCard.Toggle(prefs, row.Id), row.Id);
                Assert.AreNotEqual(before, PreferencesData.Serialize(prefs), row.Id + " changes the file");
                var store = new PreferencesStore(fs, "/save");
                store.Save(prefs);
                var back = new PreferencesStore(fs, "/save").Load();
                Assert.AreEqual(!was, row.Get(back), row.Id + " survives a reload");
                foreach (var other in SettingsCard.All.Where(r => r != row))
                {
                    if (other.IsSlider)
                    {
                        Assert.AreEqual(other.GetValue(new PreferencesData()), other.GetValue(back), 1e-4, row.Id + " left " + other.Id);
                    }
                    else
                    {
                        Assert.AreEqual(other.Get(new PreferencesData()), other.Get(back), row.Id + " left " + other.Id);
                    }
                }
            }
        }

        [Test]
        public void SlidersStepAndPersist()
        {
            var prefs = new PreferencesData();
            Assert.AreEqual(0.9f, SettingsCard.Step(prefs, SettingsCard.Music, -1), 1e-4);
            Assert.AreEqual(1f, SettingsCard.Step(prefs, SettingsCard.Music, +1), 1e-4);
            Assert.AreEqual(1f, SettingsCard.Step(prefs, SettingsCard.Music, +1), 1e-4, "clamped");
            Assert.AreEqual(0.5f, SettingsCard.Row(SettingsCard.CameraSpeed).GetValue(prefs), 1e-4, "1x sits mid-slider");
            SettingsCard.Step(prefs, SettingsCard.CameraSpeed, +1);
            Assert.Greater(prefs.CameraSensitivity, 1f);
            for (int i = 0; i < 20; i++)
            {
                SettingsCard.Step(prefs, SettingsCard.CameraSpeed, -1);
            }

            Assert.AreEqual(0.25f, prefs.CameraSensitivity, 1e-4);
            Assert.IsTrue(PreferencesData.TryRead(PreferencesData.Serialize(prefs), out var back, out _));
            Assert.AreEqual(0.25f, back.CameraSensitivity, 1e-4);
            Assert.AreEqual(3f, SettingsCard.SensitivityFromSlider(1f), 1e-4);
            Assert.AreEqual(0.25f, SettingsCard.SensitivityFromSlider(0f), 1e-4);
            foreach (float s in new[] { 0.25f, 0.7f, 1f, 1.8f, 3f })
            {
                Assert.AreEqual(s, SettingsCard.SensitivityFromSlider(SettingsCard.SliderFromSensitivity(s)), 1e-4);
            }
        }

        [Test]
        public void EveryStoredAccessibilityFieldHasAControl()
        {
            var ids = SettingsCard.All.Select(r => r.Id).ToList();
            CollectionAssert.IsSubsetOf(new[]
            {
                SettingsCard.ReducedMotion, SettingsCard.LargeCaptions, SettingsCard.OpaqueCaptions, SettingsCard.Haptics,
                SettingsCard.InvertY, SettingsCard.CameraSpeed, SettingsCard.LeftHanded, SettingsCard.Music,
                SettingsCard.Sounds, SettingsCard.Voices,
            }, ids);
            Assert.AreEqual(ids.Count, ids.Distinct().Count());
            var cells = SettingsCard.All.Select(r => (r.Column, r.Row)).Append((SettingsCard.HelperRow.Column, SettingsCard.HelperRow.Row)).ToList();
            Assert.AreEqual(cells.Count, cells.Distinct().Count(), "one row per cell");
        }

        [Test]
        public void TheCardIsASoundFocusGraph()
        {
            var graph = SettingsCard.BuildFocus();
            Assert.IsEmpty(graph.Validate(), string.Join("; ", graph.Validate()));
            Assert.AreEqual(SettingsCard.Helper, graph.Start());
            Assert.AreEqual(SettingsCard.Close, graph.Exit);
            Assert.AreEqual(SettingsCard.Music, graph.Move(SettingsCard.Helper, UiDirection.Down));
            Assert.AreEqual(SettingsCard.CameraSpeed, graph.Move(SettingsCard.Helper, UiDirection.Right));
            Assert.AreEqual(SettingsCard.Language, graph.Move(SettingsCard.Voices, UiDirection.Down));
            Assert.AreEqual(SettingsCard.Credits, graph.Move(SettingsCard.GrownUps, UiDirection.Right));
            Assert.AreEqual(SettingsCard.Close, graph.Move(SettingsCard.ReducedMotion, UiDirection.Up));

            // The entries stay reachable while any one row is hidden.
            foreach (string id in graph.Order.Where(i => i != SettingsCard.Close))
            {
                var g = SettingsCard.BuildFocus();
                g.SetEnabled(id, false);
                Assert.IsEmpty(g.Validate(), id + " hidden: " + string.Join("; ", g.Validate()));
            }
        }

        [Test]
        public void TheReadingSheetsAreSoundFocusGraphs()
        {
            foreach (string id in new[] { "parent", "credits" })
            {
                var graph = ReadingSheetFocus.Build(id);
                Assert.IsEmpty(graph.Validate());
                Assert.AreEqual(ReadingSheetFocus.Sheet, graph.Start());
                Assert.AreEqual(ReadingSheetFocus.Close, graph.Move(ReadingSheetFocus.Sheet, UiDirection.Up));
                Assert.AreEqual(ReadingSheetFocus.Close, graph.Exit);
            }
        }
    }
}
