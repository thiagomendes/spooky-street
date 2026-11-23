using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.World
{
    /// <summary>A safe place in the owl house interior Pip can be set down on (feet, Unity yaw).</summary>
    public readonly struct SafeAnchor
    {
        public SafeAnchor(string name, float x, float y, float z, float yaw)
        {
            Name = name;
            X = x;
            Y = y;
            Z = z;
            Yaw = yaw;
        }

        public string Name { get; }
        public float X { get; }
        public float Y { get; }
        public float Z { get; }
        public float Yaw { get; }

        public override string ToString() => $"{Name} ({X:F2}, {Y:F2}, {Z:F2})";
    }

    /// <summary>Why the safety net moved Pip.</summary>
    public enum RescueReason
    {
        None,

        /// <summary>Pip was in the interior space but not on its walkable area (off every floor, inside a solid, out of the shell).</summary>
        OffWalkable,

        /// <summary>
        /// Pip was pushed with the stick for <see cref="InteriorRescue.StuckSeconds"/> without leaving a small spot, and
        /// the spot is shut in (<see cref="InteriorSafety.Enclosed"/>). Pushing against a wall in open floor never is.
        /// </summary>
        Stuck,

        /// <summary>
        /// Not a move: Pip is standing on a character's head (June). The runtime slides her gently off, toward
        /// <see cref="InteriorRescue.SlideX"/>, <see cref="InteriorRescue.SlideZ"/>, every frame this is returned.
        /// </summary>
        SlideOff,
    }

    /// <summary>
    /// The owl house interior's safety rules (TestFlight 23, "stuck right after entering through the window"):
    /// the sky is hidden while Pip or the lens is indoors (<see cref="SkyHidden"/>), the lens is kept inside the
    /// closed shell (<see cref="InsideShell"/>, <see cref="ShellReach"/>), and the safe anchors the safety net
    /// (<see cref="InteriorRescue"/>) sets Pip down on. Pure.
    /// </summary>
    public static class InteriorSafety
    {
        /// <summary>The lens keeps this far inside the shell's inner faces (more than the near clip plane).</summary>
        public const float ShellMargin = 0.15f;

        private const float Ifz = CaseRouteLayout.InteriorFrontZ;

        /// <summary>Where the safety net sets Pip down: open floor in the main area, clear of every exit zone.</summary>
        public static readonly SafeAnchor[] Anchors =
        {
            new SafeAnchor("attic arrival", Thresholds.DormerInside.ArriveX, CaseRouteLayout.LoftY,
                Thresholds.DormerInside.ArriveZ, Thresholds.DormerInside.ArriveYaw),
            new SafeAnchor("attic front", -0.4f, CaseRouteLayout.LoftY, Ifz + 1.6f, 90f),
            new SafeAnchor("attic middle", 0.6f, CaseRouteLayout.LoftY, Ifz + 4.5f, 0f),
            new SafeAnchor("attic east", 1.0f, CaseRouteLayout.LoftY, Ifz + 2.0f, 0f),
            new SafeAnchor("stair landing", -2.85f, CaseRouteLayout.LandingY, Ifz + 6.2f, 90f),
            new SafeAnchor("hall arrival", Thresholds.DoorInside.ArriveX, CaseRouteLayout.FloorY,
                Thresholds.DoorInside.ArriveZ, Thresholds.DoorInside.ArriveYaw),
            new SafeAnchor("hall west", -1.2f, CaseRouteLayout.FloorY, Ifz + 1.8f, 0f),
            new SafeAnchor("hall back", 0.0f, CaseRouteLayout.FloorY, Ifz + 4.5f, 0f),
        };

        /// <summary>
        /// The sky (dome, moon, clouds, bats, shooting stars) is not drawn while Pip or the lens is in the interior's
        /// plan. The dome is an opaque, depth-writing sphere (<see cref="SkyModel.DomeRadius"/>) that cuts through the
        /// south of the interior: from a lens inside the sphere it hid everything beyond it behind a flat purple
        /// wall (TestFlight 23). From inside the closed shell nothing of the sky can be seen anyway.
        /// </summary>
        public static bool SkyHidden(float pipX, float pipZ, float lensX, float lensZ) =>
            CaseRouteLayout.Inside(pipX, pipZ) || CaseRouteLayout.Inside(lensX, lensZ);

        /// <summary>
        /// True when (x, y, z) is inside the interior's closed shell with <paramref name="margin"/> to spare: between
        /// the inner walls, above the hall floor and under the roof's inner faces, or in the doorway or the window's
        /// reveal (closed beyond by their painted view boxes). A lens there sees only inner faces, never the void.
        /// </summary>
        public static bool InsideShell(float x, float y, float z, float margin = 0f)
        {
            // The window's opening and reveal (to x −3.75, closed beyond by the painted view box) and the doorway.
            bool window = y > CaseRouteLayout.LoftY + margin && y < CaseRouteLayout.SillY + CaseRouteLayout.WindowApex - margin &&
                          Math.Abs(z - CaseRouteLayout.InnerWindowZ) < CaseRouteLayout.WindowHalfWidth - margin;
            bool door = y > CaseRouteLayout.FloorY + margin && y < CaseRouteLayout.DoorTopY - margin &&
                        Math.Abs(x) < CaseRouteLayout.DoorHalfWidth - margin;
            float minX = window ? RevealX : CaseRouteLayout.InnerMinX + margin;
            float minZ = door ? Ifz - 0.3f : CaseRouteLayout.InnerMinZ + margin;
            if (x < minX || x > CaseRouteLayout.InnerMaxX - margin || z < minZ ||
                z > CaseRouteLayout.InnerMaxZ - margin || y < CaseRouteLayout.FloorY + margin)
            {
                return false;
            }

            if (x < CaseRouteLayout.InnerMinX || z < CaseRouteLayout.InnerMinZ)
            {
                return true; // in the reveal or the doorway (their heights are checked above)
            }

            float roof = CaseRouteLayout.KneeY +
                         (CaseRouteLayout.InnerMaxX - Math.Abs(x)) * CaseRouteLayout.RoofSlope;
            // The dormer alcove rises to its own ridge.
            if (x < InteriorCamera.AlcoveInnerX && z > CaseRouteLayout.AlcoveMinZ && z < CaseRouteLayout.AlcoveMaxZ)
            {
                roof = Math.Max(roof, CaseRouteLayout.AlcoveEaveY);
            }

            return y < roof - margin;
        }

        /// <summary>The window's reveal runs out to here (the painted view box beyond).</summary>
        public const float RevealX = -3.75f;

        /// <summary>
        /// The longest boom up to <paramref name="maxDistance"/> from the shoulder (hx, hy, hz) along yaw and pitch
        /// whose whole length stays <see cref="InsideShell"/> with <see cref="ShellMargin"/> (0 when the shoulder
        /// itself is not): the final clamp on the indoor lens, whatever the solver and the probe chose.
        /// </summary>
        public static float ShellReach(float hx, float hy, float hz, float yaw, float pitch, float maxDistance)
        {
            InteriorCamera.BackDirection(yaw, pitch, out float bx, out float by, out float bz);
            const float step = 0.02f;
            float best = 0f;
            for (float d = 0f; d <= maxDistance + 1e-4f; d += step)
            {
                if (!InsideShell(hx + bx * d, hy + by * d, hz + bz * d, ShellMargin))
                {
                    break;
                }

                best = d;
            }

            return Math.Min(best, maxDistance);
        }

        /// <summary>How far Pip must be able to get in a direction for it to count as a way on.</summary>
        public const float FreeReach = 0.6f;

        /// <summary>Directions tried round Pip (every 45°).</summary>
        public const int Directions = 8;

        /// <summary>Fewer ways on than this and Pip is shut in.</summary>
        public const int MinFreeDirections = 2;

        /// <summary>
        /// How many of the eight compass directions a standing Pip at (x, y, z) can get <see cref="FreeReach"/> along
        /// (walking, stepping up, stepping off an edge or into a threshold's exit zone:
        /// <see cref="InteriorWalkMap.EscapeRun"/>). Against a wall in open floor that is five or more.
        /// </summary>
        public static int FreeDirections(float x, float y, float z, bool doorOpen)
        {
            var slabs = InteriorWalkMap.Solids(doorOpen);
            int free = 0;
            for (int i = 0; i < Directions; i++)
            {
                double a = i * 2.0 * Math.PI / Directions;
                if (InteriorWalkMap.EscapeRun(slabs, x, y, z, (float)Math.Sin(a), (float)Math.Cos(a), FreeReach) >=
                    FreeReach - 1e-3f)
                {
                    free++;
                }
            }

            return free;
        }

        /// <summary>
        /// True when Pip at (x, y, z) is shut in: fewer than <see cref="MinFreeDirections"/> ways on within
        /// <see cref="FreeReach"/>, or boxed in under a stair flight (the walk map's only pockets, never reachable on
        /// foot). Only then does a long push count as stuck.
        /// </summary>
        public static bool Enclosed(float x, float y, float z, bool doorOpen) =>
            UnderAFlight(x, y, z) || FreeDirections(x, y, z, doorOpen) < MinFreeDirections;

        /// <summary>Pip's head under the treads of either flight, in the boxed-in space below the stair.</summary>
        public static bool UnderAFlight(float x, float y, float z)
        {
            float head = y + CaseRouteLayout.PipHeight * 0.8f;
            bool westWell = x < CaseRouteLayout.StairBayX && z > CaseRouteLayout.StairFootZ &&
                            z < CaseRouteLayout.LandingZ;
            bool upperWell = !westWell && x >= CaseRouteLayout.StairBayX && x < CaseRouteLayout.StairTopX &&
                             z > CaseRouteLayout.UpperFlightMinZ;
            return (westWell && head < CaseRouteLayout.LowerFlightAt(z)) ||
                   (upperWell && head < CaseRouteLayout.UpperFlightAt(x));
        }

        /// <summary>
        /// The safe anchor nearest Pip's feet (height counts double, so the floor Pip is on wins). With
        /// <paramref name="awayFrom"/> the anchors within <see cref="InteriorRescue.AwayDistance"/> of Pip are skipped
        /// (Pip may be stuck right by one).
        /// </summary>
        public static SafeAnchor Nearest(float x, float y, float z, bool awayFrom = false)
        {
            SafeAnchor best = Anchors[0];
            float bestD = float.MaxValue;
            foreach (var a in Anchors)
            {
                float dx = a.X - x;
                float dz = a.Z - z;
                float dy = (a.Y - y) * 2f;
                if (awayFrom && dx * dx + dz * dz < InteriorRescue.AwayDistance * InteriorRescue.AwayDistance)
                {
                    continue;
                }

                float d = dx * dx + dy * dy + dz * dz;
                if (d < bestD)
                {
                    bestD = d;
                    best = a;
                }
            }

            return best;
        }
    }

    /// <summary>
    /// The interior safety net (TestFlight 23): if Pip is ever in the interior space but off its walkable area, or
    /// is pushed with the stick for several seconds without leaving a small spot that is shut in
    /// (<see cref="InteriorSafety.Enclosed"/>), Pip is set down gently on the nearest safe anchor (the runtime adds a
    /// small paper puff). Pushing against a wall in open floor never moves her; standing on a character's head slides
    /// her gently off instead (S002 attic framing). Feed it one frame at a time. Pure.
    /// </summary>
    public sealed class InteriorRescue
    {
        /// <summary>Seconds off the walkable area (standing, or anywhere out of the shell) before the rescue.</summary>
        public const float OffWalkableSeconds = 0.75f;

        /// <summary>Seconds of pushing without leaving the spot before the rescue.</summary>
        public const float StuckSeconds = 4f;

        /// <summary>The spot: moving further than this from where the push started counts as getting away.</summary>
        public const float SpotRadius = 0.3f;

        /// <summary>A stick deflection at least this strong counts as pushing.</summary>
        public const float PushThreshold = 0.5f;

        /// <summary>A stuck Pip is set down on an anchor at least this far away (not the one Pip is stuck by).</summary>
        public const float AwayDistance = 0.75f;

        /// <summary>The safety net watches this band round the interior (the street never reaches it: its boundary is at z 18).</summary>
        public const float SpaceMargin = 4f;

        /// <summary>How fast Pip is slid off a character's head (metres per second; she drops off the edge).</summary>
        public const float SlideSpeed = 1.4f;

        /// <summary>A character's capsule (June): radius and height (EnvironmentLayout.WitnessRadius / WitnessHeight).</summary>
        public const float CharacterRadius = EnvironmentLayout.WitnessRadius;

        public const float CharacterHeight = EnvironmentLayout.WitnessHeight;

        private readonly List<(float X, float Y, float Z)> _characters = new List<(float X, float Y, float Z)>(2);

        private float _off;
        private float _stuck;
        private bool _hasSpot;
        private float _spotX;
        private float _spotY;
        private float _spotZ;

        public bool DoorOpen { get; set; }

        /// <summary>The shut-in test (defaults to <see cref="InteriorSafety.Enclosed"/>; tests may replace it).</summary>
        public Func<float, float, float, bool, bool> Enclosure { get; set; }

        /// <summary>Long pushes that were checked and found Pip free (diagnostics, tests).</summary>
        public int EnclosureChecks { get; private set; }

        /// <summary>Seconds pushed in the current spot (tests, diagnostics).</summary>
        public float StuckTime => _stuck;

        public float OffTime => _off;

        /// <summary>Where to slide Pip (a horizontal unit direction) while <see cref="RescueReason.SlideOff"/> is returned.</summary>
        public float SlideX { get; private set; }

        public float SlideZ { get; private set; }

        /// <summary>Characters standing in the interior this frame (feet), set before <see cref="Step"/>.</summary>
        public void ClearCharacters() => _characters.Clear();

        public void AddCharacter(float x, float y, float z) => _characters.Add((x, y, z));

        /// <summary>
        /// True when Pip's feet at (x, y, z) rest on top of the character standing at (cx, cy, cz): over the head,
        /// within the two capsules' radii.
        /// </summary>
        public static bool OnCharacter(float x, float y, float z, float cx, float cy, float cz)
        {
            float dx = x - cx;
            float dz = z - cz;
            float reach = CharacterRadius + CaseRouteLayout.PipRadius;
            return dx * dx + dz * dz < reach * reach && y > cy + CharacterHeight - 0.35f &&
                   y < cy + CharacterHeight + 0.5f;
        }

        /// <summary>
        /// The way to slide Pip off a character's head: straight away from the head's centre, or, with Pip right on
        /// top of it, toward the most open direction round the character.
        /// </summary>
        public void SlideDirection(float x, float z, float cx, float cy, float cz, out float dirX, out float dirZ)
        {
            float dx = x - cx;
            float dz = z - cz;
            float len = (float)Math.Sqrt(dx * dx + dz * dz);
            if (len > 0.05f)
            {
                dirX = dx / len;
                dirZ = dz / len;
                return;
            }

            var slabs = InteriorWalkMap.Solids(DoorOpen);
            float best = -1f;
            dirX = 1f;
            dirZ = 0f;
            for (int i = 0; i < InteriorSafety.Directions; i++)
            {
                double a = i * 2.0 * Math.PI / InteriorSafety.Directions;
                float sx = (float)Math.Sin(a);
                float sz = (float)Math.Cos(a);
                float r = CharacterRadius + CaseRouteLayout.PipRadius + 0.05f;
                float run = InteriorWalkMap.EscapeRun(slabs, cx + sx * r, cy, cz + sz * r, sx, sz, InteriorSafety.FreeReach);
                if (run > best + 1e-3f)
                {
                    best = run;
                    dirX = sx;
                    dirZ = sz;
                }
            }
        }

        public void Reset()
        {
            _off = 0f;
            _stuck = 0f;
            _hasSpot = false;
        }

        /// <summary>True when (x, z) is in the interior space: the shell's plan and a band round it.</summary>
        public static bool InInteriorSpace(float x, float z) =>
            x > CaseRouteLayout.InnerMinX - SpaceMargin && x < CaseRouteLayout.InnerMaxX + SpaceMargin &&
            z > CaseRouteLayout.InteriorFrontZ - SpaceMargin && z < CaseRouteLayout.InnerMaxZ + SpaceMargin;

        /// <summary>
        /// True when Pip's feet at (x, y, z) are on the interior's walkable area: in the shell's plan, between the hall
        /// floor and the roof (never on top of a roof plate), over a slab top within reach of the feet (at the centre or anywhere under the capsule's
        /// rim) and not sunk into a solid.
        /// </summary>
        public static bool OnWalkableArea(float x, float y, float z, bool doorOpen)
        {
            if (!CaseRouteLayout.Inside(x, z) || y < CaseRouteLayout.FloorY - 0.4f || y > CaseRouteLayout.ApexY)
            {
                return false;
            }

            // In the front wall's thickness (the hall slab runs on under it), beside or above the doorway: no floor.
            if (z < CaseRouteLayout.InnerMinZ - 0.05f &&
                (Math.Abs(x) >= CaseRouteLayout.DoorHalfWidth || y > CaseRouteLayout.FloorY + 0.3f))
            {
                return false;
            }

            // Upstairs the feet stay under the roof's inner faces: on top of a roof plate is outside the shell.
            if (y > CaseRouteLayout.LoftY - 0.3f && !InteriorSafety.InsideShell(x, y + 0.3f, z))
            {
                return false;
            }

            float r = CaseRouteLayout.PipRadius * 0.9f;
            bool supported = false;
            float[] ox = { 0f, r, -r, 0f, 0f };
            float[] oz = { 0f, 0f, 0f, r, -r };
            for (int i = 0; i < ox.Length && !supported; i++)
            {
                foreach (var slab in CaseRouteLayout.Slabs)
                {
                    if (slab.Group == SlabGroup.Interior && slab.TryTopAt(x + ox[i], z + oz[i], 0f, out float top) &&
                        top > y - 0.35f && top < y + 0.25f)
                    {
                        supported = true;
                        break;
                    }
                }
            }

            if (!supported)
            {
                return false;
            }

            // Sunk into a solid: the capsule's core (half its radius) overlaps a slab above the step.
            foreach (var slab in CaseRouteLayout.Slabs)
            {
                if (slab.Group != SlabGroup.Interior || (doorOpen && slab.Name == CaseRouteLayout.DoorBlockerName))
                {
                    continue;
                }

                if (slab.Blocks(x, z, y + CaseRouteLayout.StepOffset + 0.05f, y + CaseRouteLayout.PipHeight - 0.05f,
                        CaseRouteLayout.PipRadius * 0.5f))
                {
                    return false;
                }
            }

            return true;
        }

        /// <summary>
        /// One frame. <paramref name="stick"/> is the move stick's deflection (0…1); <paramref name="controllable"/> is
        /// false while the player has no control (a page-turn, a conversation, the notebook, a cutaway). Returns why
        /// Pip should be set down on a safe anchor now, or <see cref="RescueReason.None"/>.
        /// </summary>
        public RescueReason Step(float dt, float x, float y, float z, bool grounded, float stick, bool controllable)
        {
            dt = Math.Max(0f, dt);
            if (!controllable || !InInteriorSpace(x, z))
            {
                Reset();
                return RescueReason.None;
            }

            // On a character's head (June): never a teleport, she is slid gently off.
            if (grounded)
            {
                foreach (var c in _characters)
                {
                    if (OnCharacter(x, y, z, c.X, c.Y, c.Z))
                    {
                        _off = 0f;
                        _stuck = 0f;
                        _hasSpot = false;
                        SlideDirection(x, z, c.X, c.Y, c.Z, out float sx, out float sz);
                        SlideX = sx;
                        SlideZ = sz;
                        return RescueReason.SlideOff;
                    }
                }
            }

            // Off the walkable area: standing where there is no floor, sunk into a solid, or out of the shell at all
            // (in the air too: nothing out there can be landed on).
            bool outOfShell = !CaseRouteLayout.Inside(x, z) || y < CaseRouteLayout.FloorY - 0.4f;
            if (outOfShell || (grounded && !OnWalkableArea(x, y, z, DoorOpen)))
            {
                _off += dt;
                if (_off >= OffWalkableSeconds)
                {
                    Reset();
                    return RescueReason.OffWalkable;
                }
            }
            else
            {
                _off = 0f;
            }

            if (stick < PushThreshold)
            {
                _stuck = 0f;
                _hasSpot = false;
                return RescueReason.None;
            }

            float dx = x - _spotX;
            float dy = y - _spotY;
            float dz = z - _spotZ;
            if (!_hasSpot || dx * dx + dy * dy + dz * dz > SpotRadius * SpotRadius)
            {
                _hasSpot = true;
                _spotX = x;
                _spotY = y;
                _spotZ = z;
                _stuck = 0f;
                return RescueReason.None;
            }

            _stuck += dt;
            if (_stuck >= StuckSeconds)
            {
                // A long push only rescues where the walk map says Pip is shut in: against a wall in open floor she is
                // just pushing, and is never moved. The spot is checked again after another full push.
                bool enclosed = Enclosure != null ? Enclosure(x, y, z, DoorOpen) : InteriorSafety.Enclosed(x, y, z, DoorOpen);
                _stuck = 0f;
                if (enclosed)
                {
                    Reset();
                    return RescueReason.Stuck;
                }

                EnclosureChecks++;
            }

            return RescueReason.None;
        }
    }
}
