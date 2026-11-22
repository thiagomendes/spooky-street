using System;
using NUnit.Framework;
using SpookyStreet.Core.Case;

namespace SpookyStreet.Tests.Core
{
    /// <summary>
    /// June's walk down to the hall while she has no walk clip (S002 interior polish): she turns first, eases in and
    /// out, never exceeds her walking speed, arrives exactly on her mark, and faces where she is going.
    /// </summary>
    public class JuneWalkTests
    {
        private const float Dt = 1f / 60f;

        [Test]
        public void SheTurnsFirstThenEasesInAndOut()
        {
            float length = CaseStage.PlanLength(CaseStage.JuneWalk);
            Assert.AreEqual(0f, CaseStage.JuneWalkDistance(CaseStage.JuneTurnSeconds * 0.9f), "she turns before she moves");
            float total = CaseStage.JuneWalkSeconds;
            Assert.AreEqual(length, CaseStage.JuneWalkDistance(total), 1e-3f, "she ends on her mark");
            Assert.AreEqual(length, CaseStage.JuneWalkDistance(total + 5f), 1e-3f);
            Assert.Less(total, 15.5f, "the walk stays short (house v3: down two flights of the bigger stair)");

            float previous = 0f;
            float fastest = 0f;
            for (float t = Dt; t <= total + Dt; t += Dt)
            {
                float d = CaseStage.JuneWalkDistance(t);
                float speed = (d - previous) / Dt;
                Assert.GreaterOrEqual(d, previous - 1e-5f, "never backwards");
                Assert.LessOrEqual(speed, CaseStage.JuneWalkSpeed + 0.02f, $"never faster than a walk at {t:F2} s");
                fastest = Math.Max(fastest, speed);
                previous = d;
            }

            Assert.AreEqual(CaseStage.JuneWalkSpeed, fastest, 0.03f, "she reaches her walking pace");
            float start = (CaseStage.JuneWalkDistance(CaseStage.JuneTurnSeconds + 0.1f) - CaseStage.JuneWalkDistance(CaseStage.JuneTurnSeconds)) / 0.1f;
            float stop = (CaseStage.JuneWalkDistance(total) - CaseStage.JuneWalkDistance(total - 0.1f)) / 0.1f;
            Assert.Less(start, 0.3f * CaseStage.JuneWalkSpeed, "a gentle start");
            Assert.Less(stop, 0.3f * CaseStage.JuneWalkSpeed, "a gentle stop");
        }

        [Test]
        public void LevelFloorsAtTheClipPaceAndTheStairFasterAlongItsSlope()
        {
            // The walk clip is 0.55 m steps on a 1.0 s cycle: 1.1 m/s over the floor plan.
            Assert.AreEqual(1.1f, CaseStage.JuneWalkSpeed, 1e-4f);
            float plan = CaseStage.PlanLength(CaseStage.JuneWalk);
            bool sawStair = false;
            for (float d = 0.05f; d < plan; d += 0.1f)
            {
                CaseStage.AlongPlan(CaseStage.JuneWalk, d, out _, out _, out float slope);
                float along = CaseStage.JuneWalkSpeed * slope;
                if (slope > 1.05f)
                {
                    sawStair = true;
                    Assert.AreEqual(1.3f, along, 0.12f, "on the stair she moves ~1.3 m/s along the slope at " + d);
                }
                else
                {
                    Assert.AreEqual(1.1f, along, 0.02f, "1.1 m/s on level floor at " + d);
                }
            }

            Assert.IsTrue(sawStair, "the walk includes the stair");
            var end = CaseStage.AlongPlan(CaseStage.JuneWalk, plan, out _, out _);
            Assert.AreEqual(CaseStage.JuneFoyer.Position.X, end.X, 1e-3f);
            Assert.AreEqual(CaseStage.JuneFoyer.Position.Z, end.Z, 1e-3f);
        }

        [Test]
        public void SheFacesWhereSheIsGoing()
        {
            float length = CaseStage.PlanLength(CaseStage.JuneWalk);
            for (float d = 0f; d < length - 0.5f; d += 0.25f)
            {
                CaseStage.JuneFacing(d, out float hx, out float hz);
                Assert.AreEqual(1f, Math.Sqrt(hx * hx + hz * hz), 1e-3f, "a unit heading at " + d);
                var here = CaseStage.AlongPlan(CaseStage.JuneWalk, d, out _, out _);
                var later = CaseStage.AlongPlan(CaseStage.JuneWalk, d + CaseStage.JuneLookAhead, out _, out _);
                float dx = later.X - here.X, dz = later.Z - here.Z;
                float l = (float)Math.Sqrt(dx * dx + dz * dz);
                if (l > 0.05f)
                {
                    Assert.Greater((hx * dx + hz * dz) / l, 0.99f, "toward the point just ahead at " + d);
                }
            }
        }
    }
}
