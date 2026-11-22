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
            float length = CaseStage.PathLength(CaseStage.JuneWalk);
            Assert.AreEqual(0f, CaseStage.JuneWalkDistance(CaseStage.JuneTurnSeconds * 0.9f), "she turns before she moves");
            float total = CaseStage.JuneWalkSeconds;
            Assert.AreEqual(length, CaseStage.JuneWalkDistance(total), 1e-3f, "she ends on her mark");
            Assert.AreEqual(length, CaseStage.JuneWalkDistance(total + 5f), 1e-3f);
            Assert.Less(total, 13.5f, "the walk stays short");

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
        public void SheFacesWhereSheIsGoing()
        {
            float length = CaseStage.PathLength(CaseStage.JuneWalk);
            for (float d = 0f; d < length - 0.5f; d += 0.25f)
            {
                CaseStage.JuneFacing(d, out float hx, out float hz);
                Assert.AreEqual(1f, Math.Sqrt(hx * hx + hz * hz), 1e-3f, "a unit heading at " + d);
                var here = CaseStage.Along(CaseStage.JuneWalk, d, out _, out _);
                var later = CaseStage.Along(CaseStage.JuneWalk, d + CaseStage.JuneLookAhead, out _, out _);
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
