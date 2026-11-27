using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.LivingStreet
{
    /// <summary>The URP quality tier the crowd budgets for (portability: high, medium, low).</summary>
    public enum CrowdTier
    {
        Low,
        Medium,
        High,
    }

    /// <summary>How much of a walker this frame draws and animates (living-street §8).</summary>
    public enum WalkerDetail
    {
        /// <summary>Off screen or past the budget: not drawn, not animated (it still advances on its loop).</summary>
        Hidden,

        /// <summary>Near and in view: full body, animated every frame.</summary>
        Full,

        /// <summary>In view beyond <see cref="CrowdBudget.ReducedRateDistance"/>: animated at 15 Hz.</summary>
        Reduced,

        /// <summary>In view beyond <see cref="CrowdBudget.StandeeDistance"/> or past the animated cap: a flat paper standee.</summary>
        Standee,
    }

    /// <summary>
    /// The iPhone budget of living-street §8 as rules: walkers animated in view per tier (8 / 6 / 4), standees in view,
    /// walkers alive per sector, the distances where animation drops to 15 Hz and where a walker becomes a paper standee,
    /// and the crowd's CPU allowance per frame. Pure, so the per-frame decision is tested here and the runtime only
    /// applies it.
    /// </summary>
    public readonly struct CrowdBudget
    {
        public const float ReducedRateDistance = 12f;
        public const float StandeeDistance = 18f;

        /// <summary>Animation updates per second beyond <see cref="ReducedRateDistance"/>.</summary>
        public const float ReducedRateHz = 15f;

        private CrowdBudget(CrowdTier tier, int animated, int standees, int alive, float cpuMs, float gpuMs)
        {
            Tier = tier;
            AnimatedInView = animated;
            StandeesInView = standees;
            AlivePerSector = alive;
            CpuMs = cpuMs;
            GpuMs = gpuMs;
        }

        public CrowdTier Tier { get; }

        /// <summary>Fully animated walkers in view (LOD0 to 8 m, LOD1 to 18 m).</summary>
        public int AnimatedInView { get; }

        /// <summary>Paper standees in view beyond the animated ones.</summary>
        public int StandeesInView { get; }

        /// <summary>Walkers alive per sector (the rest wait off screen on their loops).</summary>
        public int AlivePerSector { get; }

        /// <summary>Crowd CPU allowance per frame (logic, animation and skinning dispatch).</summary>
        public float CpuMs { get; }

        public float GpuMs { get; }

        public static CrowdBudget For(CrowdTier tier)
        {
            switch (tier)
            {
                case CrowdTier.High: return new CrowdBudget(tier, 8, 12, 24, 1.2f, 1.3f);
                case CrowdTier.Medium: return new CrowdBudget(tier, 6, 8, 18, 1.0f, 1.0f);
                default: return new CrowdBudget(tier, 4, 6, 12, 0.8f, 0.8f);
            }
        }

        /// <summary>The tier from the URP quality level index (0 low, 1 medium, 2 and above high).</summary>
        public static CrowdTier TierForQualityLevel(int level) =>
            level <= 0 ? CrowdTier.Low : level == 1 ? CrowdTier.Medium : CrowdTier.High;

        /// <summary>
        /// Decides every walker's detail this frame. <paramref name="distances"/> are camera distances and
        /// <paramref name="inView"/> whether each walker's bounds touch the view. Nearest walkers in view get the animated
        /// slots; beyond the tier's cap, or beyond <see cref="StandeeDistance"/>, they are standees, and past the standee
        /// cap they are hidden. Writes into <paramref name="result"/> (same length).
        /// </summary>
        public void Decide(IReadOnlyList<float> distances, IReadOnlyList<bool> inView, WalkerDetail[] result,
            int[] scratchOrder)
        {
            if (distances == null || inView == null || result == null || scratchOrder == null)
            {
                throw new ArgumentNullException(nameof(distances));
            }

            int n = result.Length;
            for (int i = 0; i < n; i++)
            {
                scratchOrder[i] = i;
            }

            // Insertion sort by distance: n is a handful, and it allocates nothing.
            for (int i = 1; i < n; i++)
            {
                int key = scratchOrder[i];
                int j = i - 1;
                while (j >= 0 && distances[scratchOrder[j]] > distances[key])
                {
                    scratchOrder[j + 1] = scratchOrder[j];
                    j--;
                }

                scratchOrder[j + 1] = key;
            }

            int animated = 0;
            int standees = 0;
            for (int k = 0; k < n; k++)
            {
                int i = scratchOrder[k];
                if (!inView[i])
                {
                    result[i] = WalkerDetail.Hidden;
                    continue;
                }

                float d = distances[i];
                bool lowTierFar = Tier == CrowdTier.Low && d > ReducedRateDistance;
                if (d <= StandeeDistance && animated < AnimatedInView && !lowTierFar)
                {
                    animated++;
                    result[i] = d > ReducedRateDistance ? WalkerDetail.Reduced : WalkerDetail.Full;
                }
                else if (standees < StandeesInView)
                {
                    standees++;
                    result[i] = WalkerDetail.Standee;
                }
                else
                {
                    result[i] = WalkerDetail.Hidden;
                }
            }
        }

        /// <summary>
        /// True when a reduced-rate walker animates on this frame: its pose advances at <see cref="ReducedRateHz"/>,
        /// staggered by <paramref name="walker"/> so the work spreads across frames.
        /// </summary>
        public static bool ReducedTick(double time, double previousTime, int walker)
        {
            double offset = (walker % 4) / (4.0 * ReducedRateHz);
            return Math.Floor((time + offset) * ReducedRateHz) != Math.Floor((previousTime + offset) * ReducedRateHz);
        }
    }

    /// <summary>
    /// Frame times for the device readout (living-street §8: p95 ≤ 18 ms and p99 ≤ 33.3 ms after 15 minutes of
    /// traversal): a ring of the last <see cref="Capacity"/> frame times with their median, 95th and 99th percentiles,
    /// and the crowd's own average cost per frame. Allocation-free while recording.
    /// </summary>
    public sealed class FrameTimeStats
    {
        public const int Capacity = 4096;

        /// <summary>Frames needed before the readout shows numbers (about 20 s at 60 fps).</summary>
        public const int MinimumFrames = 1200;

        /// <summary>A frame longer than this is a hitch from loading or the app returning, not play: not recorded.</summary>
        public const float IgnoreAboveMs = 500f;

        private readonly float[] _frames = new float[Capacity];
        private readonly float[] _sorted = new float[Capacity];
        private int _count;
        private int _next;
        private double _crowdSum;
        private int _crowdFrames;

        public int Count => _count;

        public bool Ready => _count >= MinimumFrames;

        /// <summary>Average crowd cost (ms per frame) since the last reset.</summary>
        public float CrowdMs => _crowdFrames > 0 ? (float)(_crowdSum / _crowdFrames) : 0f;

        public void Record(float frameMs, float crowdMs)
        {
            if (!(frameMs > 0f) || frameMs > IgnoreAboveMs)
            {
                return;
            }

            _frames[_next] = frameMs;
            _next = (_next + 1) % Capacity;
            _count = Math.Min(_count + 1, Capacity);
            if (crowdMs >= 0f)
            {
                _crowdSum += crowdMs;
                _crowdFrames++;
            }
        }

        public void Reset()
        {
            _count = 0;
            _next = 0;
            _crowdSum = 0.0;
            _crowdFrames = 0;
        }

        /// <summary>The <paramref name="percent"/>th percentile (nearest rank) of the recorded frames; 0 when none.</summary>
        public float Percentile(float percent)
        {
            if (_count == 0)
            {
                return 0f;
            }

            Array.Copy(_frames, _sorted, _count);
            Array.Sort(_sorted, 0, _count);
            int rank = (int)Math.Ceiling(Clamp(percent, 0f, 100f) / 100f * _count);
            return _sorted[Math.Max(0, Math.Min(_count - 1, rank - 1))];
        }

        public float Median => Percentile(50f);
        public float P95 => Percentile(95f);
        public float P99 => Percentile(99f);

        /// <summary>The spec §8 targets: p95 ≤ 18 ms and p99 ≤ 33.3 ms.</summary>
        public bool MeetsTargets => Ready && P95 <= 18f && P99 <= 33.3f;

        private static float Clamp(float v, float lo, float hi) => v < lo ? lo : v > hi ? hi : v;
    }

    /// <summary>
    /// Who may see the frame-time readout (living-street §8, §10): never the child. It sits on the grown-ups' page
    /// behind the parent gate, and only in development and test (TestFlight) builds; a store release hides it.
    /// </summary>
    public static class FrameReadoutPolicy
    {
        public static bool Shown(bool developmentBuild, bool testBuild, bool storeRelease) =>
            !storeRelease && (developmentBuild || testBuild);
    }
}
