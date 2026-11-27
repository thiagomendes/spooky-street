using System.Linq;
using NUnit.Framework;
using SpookyStreet.Core.LivingStreet;

namespace SpookyStreet.Tests.Core
{
    /// <summary>The notebook's street page (living-street §4): only what Pip has done, never a count or an empty slot.</summary>
    public class StreetPageRulesTests
    {
        [Test]
        public void TheStreetPageShowsOnlyWhatPipHasDone()
        {
            Assert.IsFalse(StreetPageRules.Shown(NeighbourhoodState.Empty), "no empty page");
            Assert.IsEmpty(StreetPageRules.Faces(NeighbourhoodState.Empty));
            var state = NeighbourhoodState.Empty.WithVisit(LivingStreetLayout.Abbotts);
            Assert.IsTrue(StreetPageRules.Shown(state));
            CollectionAssert.AreEqual(new[] { LivingStreetLayout.Abbotts }, StreetPageRules.Faces(state).Select(d => d.Id).ToArray(),
                "only neighbours met: no silhouettes of the others");
            Assert.IsTrue(StreetPageRules.Shown(NeighbourhoodState.Empty.WithLantern(LanternCraft.Default)), "the lantern alone fills it");
            Assert.AreEqual(3, LivingStreetLayout.Doors.Select(StreetPageRules.FaceRgb).Distinct().Count(), "each face its own colour");
        }
    }
}
