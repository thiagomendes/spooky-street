using System;

namespace SpookyStreet.Core.World
{
    /// <summary>The owl house's ways in (D24.1): each has an end on the street and an end in the interior.</summary>
    public enum ThresholdId
    {
        None,

        /// <summary>June's dormer window: the sill outside ↔ the attic alcove inside.</summary>
        Dormer,

        /// <summary>The front door: the porch ↔ the hall. Only once the latch is lifted (the door is open).</summary>
        FrontDoor,
    }

    /// <summary>One end of a threshold: the exit zone Pip walks into and the arrival point the paired end sends Pip to.</summary>
    public sealed class ThresholdEnd
    {
        internal ThresholdEnd(ThresholdId id, bool inside, float minX, float maxX, float minY, float maxY, float minZ,
            float maxZ, float arriveX, float arriveY, float arriveZ, float arriveYaw)
        {
            Id = id;
            Inside = inside;
            MinX = minX;
            MaxX = maxX;
            MinY = minY;
            MaxY = maxY;
            MinZ = minZ;
            MaxZ = maxZ;
            ArriveX = arriveX;
            ArriveY = arriveY;
            ArriveZ = arriveZ;
            ArriveYaw = arriveYaw;
        }

        public ThresholdId Id { get; }

        /// <summary>True for the interior end.</summary>
        public bool Inside { get; }

        /// <summary>The exit zone (Pip's feet, world metres).</summary>
        public float MinX { get; }

        public float MaxX { get; }
        public float MinY { get; }
        public float MaxY { get; }
        public float MinZ { get; }
        public float MaxZ { get; }

        /// <summary>Where Pip stands after arriving through this end (feet), and the Unity yaw Pip faces.</summary>
        public float ArriveX { get; }

        public float ArriveY { get; }
        public float ArriveZ { get; }
        public float ArriveYaw { get; }

        /// <summary>Only the front door needs the latch lifted; June's window stands open from the start.</summary>
        public bool NeedsDoorOpen => Id == ThresholdId.FrontDoor;

        /// <summary>The exit zone's centre on the floor (hints and bat trails point here from the other side).</summary>
        public float CentreX => (MinX + MaxX) * 0.5f;

        public float CentreZ => (MinZ + MaxZ) * 0.5f;

        public float FloorY => MinY + 0.3f;

        /// <summary>True when Pip's feet at (x, y, z) are in this end's exit zone, grown by <paramref name="margin"/>.</summary>
        public bool Contains(float x, float y, float z, float margin = 0f) =>
            x >= MinX - margin && x <= MaxX + margin && y >= MinY - margin && y <= MaxY + margin &&
            z >= MinZ - margin && z <= MaxZ + margin;

        public override string ToString() => $"{Id} {(Inside ? "inside" : "outside")}";
    }

    /// <summary>
    /// The thresholds between the street-scale owl house and its roomier interior (D24, D24.1). Zones come from
    /// <see cref="CaseRouteLayout"/> and the house v3 anchor table (docs/S002-house-v3.md §3.2). The arrival points sit a
    /// little further into each place than that table suggests (the attic 0.8 m, the hall 0.7 m, the porch 0.3 m), so
    /// the camera behind Pip has its whole boom from the first frame. Every arrival point stands outside both ends' exit
    /// zones (more than <see cref="ArrivalClearance"/> away), so a crossing can never bounce straight back. Pure.
    /// </summary>
    public static class Thresholds
    {
        /// <summary>Least distance from an arrival point to any exit zone.</summary>
        public const float ArrivalClearance = 0.5f;

        private const float Ifz = CaseRouteLayout.InteriorFrontZ;

        /// <summary>On the street: Pip at the bridge's end and on the sill, against the window plane (the body stops
        /// Pip's capsule at x −3.33, its skin a few centimetres before). The window opens above the body's top
        /// (2.8 m), so a glide or jump can carry Pip over the sill onto that top until the low roof step stops it
        /// (x −2.78): the zone reaches that far, so such an entry still crosses (TestFlight 23 approaches).</summary>
        public static readonly ThresholdEnd DormerOutside = new ThresholdEnd(ThresholdId.Dormer, false,
            -3.55f, -2.6f, CaseRouteLayout.ExteriorSillY - 0.3f, CaseRouteLayout.ExteriorSillY + 0.9f,
            CaseRouteLayout.ExteriorWindowZ - 0.45f, CaseRouteLayout.ExteriorWindowZ + 0.45f,
            -4.1f, 2.47f, CaseRouteLayout.ExteriorWindowZ, -90f);

        /// <summary>In the attic: Pip in the window's reveal (the view box stops Pip at x −3.53).</summary>
        public static readonly ThresholdEnd DormerInside = new ThresholdEnd(ThresholdId.Dormer, true,
            -3.95f, -3.2f, CaseRouteLayout.LoftY - 0.3f, CaseRouteLayout.LoftY + 1.0f,
            CaseRouteLayout.InnerWindowZ - 0.5f, CaseRouteLayout.InnerWindowZ + 0.5f,
            -1.7f, CaseRouteLayout.LoftY, CaseRouteLayout.InnerWindowZ, 90f);

        /// <summary>On the porch: Pip in the open doorway (the body stops Pip at z 10.72).</summary>
        public static readonly ThresholdEnd DoorOutside = new ThresholdEnd(ThresholdId.FrontDoor, false,
            -0.5f, 0.5f, CaseRouteLayout.FloorY - 0.3f, CaseRouteLayout.FloorY + 0.9f,
            CaseRouteLayout.FrontZ - 0.5f, CaseRouteLayout.FrontZ + 0.3f,
            0f, CaseRouteLayout.FloorY, CaseRouteLayout.FrontZ - 1.55f, 180f);

        /// <summary>In the hall: Pip in the open doorway (the view box stops Pip at builder depth −0.06).</summary>
        public static readonly ThresholdEnd DoorInside = new ThresholdEnd(ThresholdId.FrontDoor, true,
            -0.5f, 0.5f, CaseRouteLayout.FloorY - 0.3f, CaseRouteLayout.FloorY + 0.9f, Ifz - 0.45f, Ifz + 0.3f,
            0f, CaseRouteLayout.FloorY, Ifz + 2.0f, 0f);

        /// <summary>Every end, outside ones first.</summary>
        public static readonly ThresholdEnd[] Ends = { DormerOutside, DoorOutside, DormerInside, DoorInside };

        /// <summary>The other end of <paramref name="end"/>'s threshold.</summary>
        public static ThresholdEnd Pair(ThresholdEnd end)
        {
            if (end == null)
            {
                throw new ArgumentNullException(nameof(end));
            }

            return End(end.Id, !end.Inside);
        }

        public static ThresholdEnd End(ThresholdId id, bool inside)
        {
            switch (id)
            {
                case ThresholdId.Dormer:
                    return inside ? DormerInside : DormerOutside;
                case ThresholdId.FrontDoor:
                    return inside ? DoorInside : DoorOutside;
                default:
                    throw new ArgumentOutOfRangeException(nameof(id), id, "not a threshold");
            }
        }

        /// <summary>The end whose exit zone holds Pip's feet and that can be used now (the door only once open).</summary>
        public static ThresholdEnd ExitAt(float x, float y, float z, bool doorOpen)
        {
            foreach (var end in Ends)
            {
                if (end.Contains(x, y, z) && (doorOpen || !end.NeedsDoorOpen))
                {
                    return end;
                }
            }

            return null;
        }

        /// <summary>True when (x, y, z) is in, or within <paramref name="margin"/> of, any exit zone (door open or not).</summary>
        public static bool NearAnyZone(float x, float y, float z, float margin)
        {
            foreach (var end in Ends)
            {
                if (end.Contains(x, y, z, margin))
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Distance in the floor plan from (x, z) to <paramref name="end"/>'s exit zone (0 inside it).</summary>
        public static float PlanDistance(ThresholdEnd end, float x, float z)
        {
            float dx = Math.Max(0f, Math.Max(end.MinX - x, x - end.MaxX));
            float dz = Math.Max(0f, Math.Max(end.MinZ - z, z - end.MaxZ));
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        /// <summary>
        /// Where a hint (bat trail, goal sticker) should lead when its goal is on the other side of the house walls:
        /// the threshold end on Pip's side, the front door once it is open and June's window before. Returns null when
        /// Pip and the goal are on the same side (lead straight to the goal).
        /// </summary>
        public static ThresholdEnd WayTo(bool pipInside, bool goalInside, bool doorOpen)
        {
            if (pipInside == goalInside)
            {
                return null;
            }

            return End(doorOpen ? ThresholdId.FrontDoor : ThresholdId.Dormer, pipInside);
        }
    }

    /// <summary>
    /// Decides when Pip crosses a threshold, without double triggers: a crossing fires only when Pip walks into an
    /// usable exit zone while armed. After any crossing (or a teleport) the tracker disarms; it re-arms once Pip has
    /// been at least <see cref="Hysteresis"/> clear of every exit zone for <see cref="Cooldown"/> seconds. Pure.
    /// </summary>
    public sealed class ThresholdTracker
    {
        /// <summary>Pip must leave every exit zone by this much before the next crossing can fire.</summary>
        public const float Hysteresis = 0.25f;

        /// <summary>Seconds after an arrival before the next crossing can fire.</summary>
        public const float Cooldown = 0.5f;

        private float _cooldown;

        public bool Armed { get; private set; }

        /// <summary>Pip was placed somewhere (a new game, a resume, a test teleport): arm only once clear of the zones.</summary>
        public void Reset()
        {
            Armed = false;
            _cooldown = 0f;
        }

        /// <summary>Pip has just arrived through a threshold.</summary>
        public void Arrived()
        {
            Armed = false;
            _cooldown = Cooldown;
        }

        /// <summary>One frame. Returns the end Pip is leaving through, or null.</summary>
        public ThresholdEnd Step(float dt, float x, float y, float z, bool doorOpen)
        {
            if (_cooldown > 0f)
            {
                _cooldown = Math.Max(0f, _cooldown - Math.Max(0f, dt));
            }

            if (!Armed)
            {
                if (_cooldown <= 0f && !Thresholds.NearAnyZone(x, y, z, Hysteresis))
                {
                    Armed = true;
                }

                return null;
            }

            var exit = Thresholds.ExitAt(x, y, z, doorOpen);
            if (exit != null)
            {
                Armed = false;
            }

            return exit;
        }
    }

    /// <summary>The paper page-turn wipe's phases.</summary>
    public enum WipePhase
    {
        Idle,

        /// <summary>The page sweeps over the screen.</summary>
        Covering,

        /// <summary>The screen is covered: Pip moves and the camera cuts.</summary>
        Covered,

        /// <summary>The page carries on and uncovers the new place.</summary>
        Revealing,
    }

    /// <summary>
    /// The threshold transition's timing (D24.1: about 0.5 s, paper like the UI): a bone-paper page turns over the
    /// screen from the right (<see cref="CoverSeconds"/>), holds while Pip and the camera move
    /// (<see cref="HoldSeconds"/>, at least <see cref="HoldFrames"/> frames so the camera has settled), then turns on
    /// to the left and uncovers the new place. Reduced motion (UI language: 120 ms cross-fades) fades the page in and
    /// out instead. Input is ignored from start to finish. Pure: feed it unscaled-or-game time, read the page.
    /// </summary>
    public sealed class ThresholdWipe
    {
        public const float CoverSeconds = 0.2f;
        public const float HoldSeconds = 0.1f;
        public const float RevealSeconds = 0.22f;
        public const float FadeSeconds = 0.12f;
        public const int HoldFrames = 2;

        /// <summary>Slant of the turning page's edge (degrees) at mid-turn; it settles flat when the page lands.</summary>
        public const float MaxTilt = 7f;

        private float _t;
        private int _heldFrames;
        private bool _moveSignalled;

        public WipePhase Phase { get; private set; } = WipePhase.Idle;

        public bool ReducedMotion { get; private set; }

        /// <summary>True from <see cref="Start"/> until the page has gone: gameplay input is ignored.</summary>
        public bool Active => Phase != WipePhase.Idle;

        /// <summary>Seconds a whole wipe takes (without frame-rate padding of the hold).</summary>
        public float TotalSeconds => ReducedMotion ? 2f * FadeSeconds + HoldSeconds : CoverSeconds + HoldSeconds + RevealSeconds;

        /// <summary>How far the page has turned, 0 (not yet on screen) … 1 (gone off the other side).</summary>
        public float Turn { get; private set; }

        /// <summary>
        /// The page's left and right edges in screen widths (0 = left edge of the screen, 1 = right). While covering the
        /// left edge sweeps in from the right; while revealing the right edge sweeps off to the left.
        /// </summary>
        public float PageLeft => ReducedMotion ? 0f : Phase == WipePhase.Revealing ? -0.15f : 1f - Ease(CoverAmount) * 1.15f;

        public float PageRight => ReducedMotion ? 1f : Phase == WipePhase.Revealing ? 1f - Ease(RevealAmount) * 1.15f + 0.15f : 1.15f;

        /// <summary>The page's opacity (1 except in the reduced-motion fade).</summary>
        public float Alpha
        {
            get
            {
                if (!ReducedMotion)
                {
                    return Active ? 1f : 0f;
                }

                switch (Phase)
                {
                    case WipePhase.Covering:
                        return Clamp01(_t / FadeSeconds);
                    case WipePhase.Covered:
                        return 1f;
                    case WipePhase.Revealing:
                        return 1f - Clamp01(_t / FadeSeconds);
                    default:
                        return 0f;
                }
            }
        }

        /// <summary>How much of the screen the page hides (0 … 1).</summary>
        public float Coverage
        {
            get
            {
                if (ReducedMotion)
                {
                    return Alpha;
                }

                float left = Math.Max(0f, Math.Min(1f, PageLeft));
                float right = Math.Max(0f, Math.Min(1f, PageRight));
                return Math.Max(0f, right - left);
            }
        }

        /// <summary>The leading edge's slant (degrees): the page lifts as it turns, flat when it covers the screen.</summary>
        public float Tilt
        {
            get
            {
                if (ReducedMotion || Phase == WipePhase.Covered || Phase == WipePhase.Idle)
                {
                    return 0f;
                }

                float u = Phase == WipePhase.Covering ? CoverAmount : RevealAmount;
                return MaxTilt * (float)Math.Sin(u * Math.PI) * (Phase == WipePhase.Covering ? 1f : -1f);
            }
        }

        /// <summary>Width of the curl highlight along the moving edge, in screen widths (0 when the page lies flat).</summary>
        public float Curl => ReducedMotion || Phase == WipePhase.Covered || Phase == WipePhase.Idle
            ? 0f
            : 0.06f * (float)Math.Sin((Phase == WipePhase.Covering ? CoverAmount : RevealAmount) * Math.PI) + 0.015f;

        private float CoverAmount => Phase == WipePhase.Covering ? Clamp01(_t / CoverSeconds) : Phase == WipePhase.Idle ? 0f : 1f;

        private float RevealAmount => Phase == WipePhase.Revealing ? Clamp01(_t / RevealSeconds) : 0f;

        /// <summary>Starts a wipe (ignored while one runs). Returns false when one is already running.</summary>
        public bool Start(bool reducedMotion)
        {
            if (Active)
            {
                return false;
            }

            ReducedMotion = reducedMotion;
            Phase = WipePhase.Covering;
            _t = 0f;
            _heldFrames = 0;
            _moveSignalled = false;
            Turn = 0f;
            return true;
        }

        /// <summary>
        /// Advances one frame. Returns true exactly once per wipe, on the first frame the screen is fully covered:
        /// that is when Pip moves and the camera cuts.
        /// </summary>
        public bool Step(float dt)
        {
            if (!Active)
            {
                return false;
            }

            _t += Math.Max(0f, dt);
            bool move = false;
            switch (Phase)
            {
                case WipePhase.Covering:
                    if (_t >= (ReducedMotion ? FadeSeconds : CoverSeconds))
                    {
                        Phase = WipePhase.Covered;
                        _t = 0f;
                        _heldFrames = 0;
                        if (!_moveSignalled)
                        {
                            _moveSignalled = true;
                            move = true;
                        }
                    }

                    break;
                case WipePhase.Covered:
                    _heldFrames++;
                    if (_t >= HoldSeconds && _heldFrames >= HoldFrames)
                    {
                        Phase = WipePhase.Revealing;
                        _t = 0f;
                    }

                    break;
                case WipePhase.Revealing:
                    if (_t >= (ReducedMotion ? FadeSeconds : RevealSeconds))
                    {
                        Phase = WipePhase.Idle;
                        _t = 0f;
                    }

                    break;
            }

            Turn = TurnNow();
            return move;
        }

        /// <summary>Stops at once (a scene reset); the page disappears.</summary>
        public void Cancel()
        {
            Phase = WipePhase.Idle;
            _t = 0f;
            Turn = 0f;
        }

        private float TurnNow()
        {
            switch (Phase)
            {
                case WipePhase.Covering:
                    return 0.5f * CoverAmount;
                case WipePhase.Covered:
                    return 0.5f;
                case WipePhase.Revealing:
                    return 0.5f + 0.5f * RevealAmount;
                default:
                    return _moveSignalled ? 1f : 0f;
            }
        }

        /// <summary>Ease-in-out (smoothstep) so the page starts and lands softly.</summary>
        public static float Ease(float u)
        {
            u = Clamp01(u);
            return u * u * (3f - 2f * u);
        }

        private static float Clamp01(float v) => v < 0f ? 0f : v > 1f ? 1f : v;
    }
}
