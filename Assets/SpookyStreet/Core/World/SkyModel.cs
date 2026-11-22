using System;

namespace SpookyStreet.Core.World
{
    /// <summary>One ring of paper clouds turning slowly about the sky dome's vertical axis.</summary>
    public readonly struct CloudLayer
    {
        public CloudLayer(float radius, float height, float degreesPerSecond, float startDegrees, int clouds, float cloudWidth,
            float cloudHeight, float mist)
        {
            Radius = radius;
            Height = height;
            DegreesPerSecond = degreesPerSecond;
            StartDegrees = startDegrees;
            Clouds = clouds;
            CloudWidth = cloudWidth;
            CloudHeight = cloudHeight;
            Mist = mist;
        }

        /// <summary>Horizontal distance from the dome's axis, metres.</summary>
        public float Radius { get; }

        /// <summary>World height of the clouds' base line (individual clouds vary around it).</summary>
        public float Height { get; }

        public float DegreesPerSecond { get; }

        /// <summary>Ring angle when the game starts.</summary>
        public float StartDegrees { get; }

        public int Clouds { get; }

        public float CloudWidth { get; }

        public float CloudHeight { get; }

        /// <summary>0 = crisp near paper, 1 = melted into the horizon haze.</summary>
        public float Mist { get; }
    }

    /// <summary>Where a shooting star travels, as angles on the sky dome.</summary>
    public readonly struct ShootingStarArc
    {
        public ShootingStarArc(float startAzimuth, float startElevation, float sweepDegrees, float dropDegrees)
        {
            StartAzimuth = startAzimuth;
            StartElevation = startElevation;
            SweepDegrees = sweepDegrees;
            DropDegrees = dropDegrees;
        }

        /// <summary>Degrees from the view direction (negative is left of it).</summary>
        public float StartAzimuth { get; }

        public float StartElevation { get; }

        /// <summary>Signed azimuth travelled over the streak.</summary>
        public float SweepDegrees { get; }

        /// <summary>Elevation lost over the streak (always falling).</summary>
        public float DropDegrees { get; }

        public float AzimuthAt(float progress) => StartAzimuth + SweepDegrees * progress;

        /// <summary>A slightly curved fall: slow at first, steeper at the end.</summary>
        public float ElevationAt(float progress) => StartElevation - DropDegrees * progress * (0.7f + 0.3f * progress);
    }

    /// <summary>A small flock of paper bats crossing in front of the moon.</summary>
    public readonly struct BatFlight
    {
        public BatFlight(int count, int direction, float startHeight, float endHeight)
        {
            Count = count;
            Direction = direction;
            StartHeight = startHeight;
            EndHeight = endHeight;
        }

        public int Count { get; }

        /// <summary>+1 flies left to right, −1 right to left.</summary>
        public int Direction { get; }

        /// <summary>Height where the lead bat enters and leaves, in moon radii from the moon's centre.</summary>
        public float StartHeight { get; }

        public float EndHeight { get; }
    }

    /// <summary>
    /// Numbers and pure maths for the handcrafted sky (owner request "magical handcrafted sky"; art bible §3: the
    /// attic window stays the brightest point, so nothing here reaches the bloom threshold). The sky is paper on a
    /// slow carousel: cloud rings turn about the dome's axis and wrap seamlessly, stars twinkle, a shooting star
    /// falls every 20–40 s and bats cross the moon now and then. Reduced motion (spec §5/§6) removes the shooting
    /// stars and bats and slows the clouds and the twinkle.
    /// </summary>
    public static class SkyModel
    {
        /// <summary>Nothing in the sky may be brighter than this (bloom starts at 1).</summary>
        public const float BrightestValue = 0.95f;

        /// <summary>
        /// The sky dome (an inward-facing, depth-writing hemisphere, GreyboxBuilder) is centred under the middle of the
        /// lawn ring with this radius. It is opaque: from a lens inside its sphere it hides everything beyond it, which
        /// is why the closed owl house interior (z ≈ 58–65, D24.1, partly beyond the sphere) hides the sky while the
        /// camera is indoors (<see cref="InteriorSafety.SkyHidden"/>).
        /// </summary>
        public const float DomeCentreX = 0f;

        public const float DomeCentreY = -3f;
        public const float DomeCentreZ = 8f;
        public const float DomeRadius = 52f;

        /// <summary>True when (x, y, z) is inside the dome's sphere (a lens there sees the dome's inner faces).</summary>
        public static bool InsideDome(float x, float y, float z)
        {
            float dx = x - DomeCentreX;
            float dy = y - DomeCentreY;
            float dz = z - DomeCentreZ;
            return dx * dx + dy * dy + dz * dz < DomeRadius * DomeRadius;
        }

        /// <summary>Peak opacity of the moon's halo card where it meets the disc.</summary>
        public const float HaloOpacity = 0.42f;

        /// <summary>Halo reach in moon radii.</summary>
        public const float HaloRadii = 2.7f;

        public const float ShootingStarMinInterval = 20f;
        public const float ShootingStarMaxInterval = 40f;
        public const float ShootingStarSeconds = 0.9f;

        /// <summary>Shooting stars stay in the upper sky: they start no lower than this and never fall below the floor.</summary>
        public const float ShootingStarMinElevation = 26f;
        public const float ShootingStarMaxElevation = 52f;
        public const float ShootingStarFloorElevation = 12f;

        /// <summary>Half-width of the window around the view direction a shooting star may start in.</summary>
        public const float ShootingStarViewWindow = 50f;

        public const float BatMinInterval = 26f;
        public const float BatMaxInterval = 52f;
        public const float BatFirstMinDelay = 9f;
        public const float BatFirstMaxDelay = 18f;
        public const float BatFlightSeconds = 7.5f;
        public const int BatMinCount = 2;
        public const int BatMaxCount = 3;

        /// <summary>How far to each side of the moon a flight starts and ends, in moon radii.</summary>
        public const float BatTravelRadii = 3.4f;
        public const float BatFlapHz = 3.1f;

        public const float ReducedCloudScale = 0.25f;
        public const float ReducedTwinkleScale = 0.3f;

        /// <summary>The twinkle clock wraps here so the shader's sine keeps its precision in long sessions.</summary>
        public const double TwinklePeriod = 2048.0 * Math.PI;

        /// <summary>Index into <see cref="Layers"/> of the ring that passes in front of the moon.</summary>
        public const int MoonLayer = 1;

        /// <summary>
        /// How far behind the moon (in ring degrees) the middle ring's first cloud starts, so it drifts across the
        /// moon within the first half minute of play.
        /// </summary>
        public const float MoonCloudLeadDegrees = 22f;

        /// <summary>
        /// Far (behind the moon, low, hazy), middle (in front of the moon; its first cloud is placed on the line
        /// from the arrival porch to the moon) and near (higher, larger, faster). All sit between the neighbour
        /// houses and the dome.
        /// </summary>
        public static readonly CloudLayer[] Layers =
        {
            new CloudLayer(45f, 9.5f, 0.32f, 40f, 3, 21f, 4.6f, 0.75f),
            new CloudLayer(36f, 18f, 0.8f, 0f, 3, 14f, 3.6f, 0.25f),
            new CloudLayer(33f, 26.5f, 1.25f, 200f, 3, 15f, 4.2f, 0f),
        };

        /// <summary>Cloud speed scale: clouds keep drifting in reduced motion, at a quarter of the speed.</summary>
        public static float CloudSpeedScale(bool reducedMotion) => reducedMotion ? ReducedCloudScale : 1f;

        public static float TwinkleSpeedScale(bool reducedMotion) => reducedMotion ? ReducedTwinkleScale : 1f;

        /// <summary>Ring angle in [0, 360) after <paramref name="cloudClock"/> seconds of cloud time.</summary>
        public static float CloudAngle(double cloudClock, float degreesPerSecond, float startDegrees)
        {
            double angle = (startDegrees + cloudClock * degreesPerSecond) % 360.0;
            if (angle < 0.0)
            {
                angle += 360.0;
            }

            return angle >= 360.0 ? 0f : (float)angle;
        }

        /// <summary>Signed shortest turn from <paramref name="fromDegrees"/> to <paramref name="toDegrees"/>, in (−180, 180].</summary>
        public static float DeltaAngle(float fromDegrees, float toDegrees)
        {
            float delta = (toDegrees - fromDegrees) % 360f;
            if (delta > 180f)
            {
                delta -= 360f;
            }
            else if (delta <= -180f)
            {
                delta += 360f;
            }

            return delta;
        }

        /// <summary>A wait drawn uniformly from [<paramref name="min"/>, <paramref name="max"/>].</summary>
        public static float NextInterval(Random random, float min, float max) =>
            min + (max - min) * (float)random.NextDouble();

        public static ShootingStarArc NextArc(Random random)
        {
            float azimuth = (2f * (float)random.NextDouble() - 1f) * ShootingStarViewWindow;
            float elevation = ShootingStarMinElevation +
                              (ShootingStarMaxElevation - ShootingStarMinElevation) * (float)random.NextDouble();
            float sweep = (14f + 10f * (float)random.NextDouble()) * (random.Next(2) == 0 ? -1f : 1f);
            float drop = 6f + 8f * (float)random.NextDouble();
            return new ShootingStarArc(azimuth, elevation, sweep, drop);
        }

        public static BatFlight NextFlight(Random random)
        {
            int count = BatMinCount + random.Next(BatMaxCount - BatMinCount + 1);
            int direction = random.Next(2) == 0 ? -1 : 1;
            float start = (2f * (float)random.NextDouble() - 1f) * 0.45f;
            float end = (2f * (float)random.NextDouble() - 1f) * 0.45f;
            return new BatFlight(count, direction, start, end);
        }

        /// <summary>
        /// Streak brightness over its life: a quick flare in, a longer fade out, zero at both ends.
        /// </summary>
        public static float ShootingStarGlow(float progress)
        {
            if (progress <= 0f || progress >= 1f)
            {
                return 0f;
            }

            float rise = Math.Min(progress / 0.18f, 1f);
            float fall = Math.Min((1f - progress) / 0.45f, 1f);
            return rise * fall * fall * (3f - 2f * fall);
        }

        /// <summary>Tail length as a fraction of the full trail: grows out of nothing, then holds.</summary>
        public static float ShootingStarTrail(float progress) => Math.Min(Math.Max(progress, 0f) / 0.3f, 1f);

        /// <summary>
        /// Where bat <paramref name="index"/> of a flight is at <paramref name="progress"/> (0..1), in moon radii
        /// from the moon's centre (x right, y up), plus its wing flap (−1..1). Followers trail the lead bat and
        /// fly a little higher or lower; the lead bat crosses the disc itself half-way through.
        /// </summary>
        public static void BatPose(BatFlight flight, int index, float progress, out float x, out float y, out float flap)
        {
            float own = progress - index * 0.055f;
            float side = index == 0 ? 0f : index % 2 == 1 ? 0.5f : -0.42f;
            x = flight.Direction * BatTravelRadii * (2f * own - 1f);
            float seconds = own * BatFlightSeconds;
            y = flight.StartHeight + (flight.EndHeight - flight.StartHeight) * own + side +
                0.16f * (float)Math.Sin((seconds * 0.8 + index * 0.37) * 2.0 * Math.PI);
            flap = (float)Math.Sin((seconds * BatFlapHz + index * 0.31) * 2.0 * Math.PI);
        }
    }
}
