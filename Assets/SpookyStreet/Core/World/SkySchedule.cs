using System;

namespace SpookyStreet.Core.World
{
    /// <summary>
    /// Clocks and timers of the handcrafted sky, advanced with scaled game time (so a paused game, which passes a
    /// zero step, freezes everything). Reduced motion never shows a shooting star or a bat flight: running ones are
    /// dropped and their timers hold until it is turned off again. Deterministic for a given seed.
    /// </summary>
    public sealed class SkySchedule
    {
        private readonly Random _random;

        public SkySchedule(int seed)
        {
            _random = new Random(seed);
            ShootingStarIn = SkyModel.NextInterval(_random, SkyModel.ShootingStarMinInterval,
                SkyModel.ShootingStarMaxInterval);
            BatsIn = SkyModel.NextInterval(_random, SkyModel.BatFirstMinDelay, SkyModel.BatFirstMaxDelay);
        }

        /// <summary>Seconds of cloud drift (slower in reduced motion).</summary>
        public double CloudClock { get; private set; }

        /// <summary>Seconds of star twinkle, wrapped at <see cref="SkyModel.TwinklePeriod"/>.</summary>
        public double TwinkleClock { get; private set; }

        /// <summary>Game time until the next shooting star starts.</summary>
        public float ShootingStarIn { get; private set; }

        public bool ShootingStarActive { get; private set; }

        /// <summary>0..1 through the running streak.</summary>
        public float ShootingStarProgress { get; private set; }

        public ShootingStarArc Arc { get; private set; }

        public int ShootingStarsStarted { get; private set; }

        public float BatsIn { get; private set; }

        public bool BatsActive { get; private set; }

        public float BatProgress { get; private set; }

        public BatFlight Flight { get; private set; }

        public int BatFlightsStarted { get; private set; }

        public void Advance(float deltaTime, bool reducedMotion)
        {
            if (deltaTime <= 0f)
            {
                return; // paused
            }

            CloudClock += deltaTime * SkyModel.CloudSpeedScale(reducedMotion);
            TwinkleClock = (TwinkleClock + deltaTime * SkyModel.TwinkleSpeedScale(reducedMotion)) % SkyModel.TwinklePeriod;

            if (reducedMotion)
            {
                if (ShootingStarActive)
                {
                    EndShootingStar();
                }

                if (BatsActive)
                {
                    EndBats();
                }

                return;
            }

            if (ShootingStarActive)
            {
                ShootingStarProgress += deltaTime / SkyModel.ShootingStarSeconds;
                if (ShootingStarProgress >= 1f)
                {
                    EndShootingStar();
                }
            }
            else
            {
                ShootingStarIn -= deltaTime;
                if (ShootingStarIn <= 0f)
                {
                    StartShootingStar(SkyModel.NextArc(_random), 0f);
                }
            }

            if (BatsActive)
            {
                BatProgress += deltaTime / SkyModel.BatFlightSeconds;
                if (BatProgress >= 1f + SkyModel.BatMaxCount * 0.055f)
                {
                    EndBats();
                }
            }
            else
            {
                BatsIn -= deltaTime;
                if (BatsIn <= 0f)
                {
                    StartBats(SkyModel.NextFlight(_random), 0f);
                }
            }
        }

        /// <summary>Starts a streak now, part-way through (review captures and tests).</summary>
        public void StartShootingStar(ShootingStarArc arc, float progress)
        {
            Arc = arc;
            ShootingStarActive = true;
            ShootingStarProgress = Math.Min(Math.Max(progress, 0f), 0.999f);
            ShootingStarsStarted++;
        }

        /// <summary>Starts a bat flight now, part-way through (review captures and tests).</summary>
        public void StartBats(BatFlight flight, float progress)
        {
            Flight = flight;
            BatsActive = true;
            BatProgress = Math.Max(progress, 0f);
            BatFlightsStarted++;
        }

        /// <summary>Moves the cloud clock (review captures line a cloud up with the moon).</summary>
        public void SetCloudClock(double seconds) => CloudClock = seconds;

        private void EndShootingStar()
        {
            ShootingStarActive = false;
            ShootingStarProgress = 0f;
            ShootingStarIn = SkyModel.NextInterval(_random, SkyModel.ShootingStarMinInterval,
                SkyModel.ShootingStarMaxInterval);
        }

        private void EndBats()
        {
            BatsActive = false;
            BatProgress = 0f;
            BatsIn = SkyModel.NextInterval(_random, SkyModel.BatMinInterval, SkyModel.BatMaxInterval);
        }
    }
}
