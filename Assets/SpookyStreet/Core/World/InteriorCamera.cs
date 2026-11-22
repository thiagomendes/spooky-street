using System;
using System.Collections.Generic;

namespace SpookyStreet.Core.World
{
    /// <summary>Which part of the owl house Pip is in, for the interior camera profile.</summary>
    public enum InteriorRoom
    {
        None,

        /// <summary>The ground floor: the entry bay and the low hall under the loft.</summary>
        Foyer,

        /// <summary>On the stair along the back wall.</summary>
        Stair,

        /// <summary>On the loft (June's attic).</summary>
        Attic,

        /// <summary>In June's dormer alcove, just inside the window.</summary>
        Alcove,
    }

    /// <summary>The boom a room asks for: length, shoulder rise above the pivot and the pitch range (degrees, down +).</summary>
    public readonly struct InteriorCameraProfile
    {
        public InteriorCameraProfile(float distance, float rise, float minPitch, float maxPitch)
        {
            Distance = distance;
            Rise = rise;
            MinPitch = minPitch;
            MaxPitch = maxPitch;
        }

        public float Distance { get; }
        public float Rise { get; }
        public float MinPitch { get; }
        public float MaxPitch { get; }
    }

    /// <summary>What the interior camera solver is asked: where Pip is, how the player has turned the camera.</summary>
    public struct InteriorCameraRequest
    {
        public float PipX, PipY, PipZ;

        /// <summary>The player's orbit (degrees; pitch positive looks down).</summary>
        public float Yaw, Pitch;

        /// <summary>The rig's pivot height above Pip's feet (before the shoulder rise).</summary>
        public float PivotHeight;

        /// <summary>The yaw offset the last solve chose (kept when it is still as good, so the camera never dithers).</summary>
        public float PreviousYawOffset;

        public bool DoorOpen;

        /// <summary>A character Pip is talking to (or about to): the camera also keeps a clear line to them.</summary>
        public bool HasFocus;

        public float FocusX, FocusY, FocusZ;
    }

    /// <summary>The solved boom (apply as yaw offset, pitch, shoulder rise and boom length).</summary>
    public readonly struct InteriorShot
    {
        public InteriorShot(InteriorRoom room, float distance, float pitch, float yawOffset, float rise, bool clear,
            float cameraX, float cameraY, float cameraZ)
        {
            Room = room;
            Distance = distance;
            Pitch = pitch;
            YawOffset = yawOffset;
            Rise = rise;
            Clear = clear;
            CameraX = cameraX;
            CameraY = cameraY;
            CameraZ = cameraZ;
        }

        public InteriorRoom Room { get; }

        /// <summary>False outside the house: the normal follow camera applies.</summary>
        public bool Active => Room != InteriorRoom.None;

        public float Distance { get; }
        public float Pitch { get; }
        public float YawOffset { get; }
        public float Rise { get; }

        /// <summary>True when the boom is at least <see cref="InteriorCamera.MinDistance"/> with every sightline clear.</summary>
        public bool Clear { get; }

        public float CameraX { get; }
        public float CameraY { get; }
        public float CameraZ { get; }

        public override string ToString() =>
            $"{Room} d {Distance:F2} pitch {Pitch:F0} yaw+{YawOffset:F0} rise {Rise:F2} {(Clear ? "clear" : "TIGHT")} " +
            $"cam ({CameraX:F2}, {CameraY:F2}, {CameraZ:F2})";
    }

    /// <summary>
    /// The owl house's indoor camera (S002 interior polish). Indoors the camera never leaves the rooms: it lives in the
    /// house's air (<see cref="InAir"/>: the hall under the loft, the entry bay, the stairwell and the attic under its
    /// roof) and never touches a solid (<see cref="Blockers"/>: every shell and interior collider slab plus the dressing
    /// that has no collider: the loft's ceiling joists and bunting, the lanterns, the stair's closed side and
    /// balustrade). Each room has a profile (a shorter boom, a lower shoulder under the 1.7 m hall ceiling, a pitch
    /// range). <see cref="Solve"/> keeps the player's orbit when the boom fits; otherwise it searches pitch first, then
    /// small yaw swings, then larger ones, for the longest boom with clear lines from the lens to Pip's head and chest
    /// (and the character being talked to). <see cref="ClearDistance"/> is the per-frame probe that shortens the boom
    /// at once against anything in the way. Pure, so the whole house can be swept in EditMode.
    /// </summary>
    public static class InteriorCamera
    {
        /// <summary>The camera's collision sphere (matches the rig's Cinemachine radius).</summary>
        public const float Radius = 0.2f;

        /// <summary>Sightlines are tested as thin capsules.</summary>
        public const float SightRadius = 0.03f;

        /// <summary>The shortest boom that still frames all of Pip (1.1 m tall) in a 50° lens.</summary>
        public const float MinDistance = 1.3f;

        /// <summary>Step of the probe along the boom.</summary>
        public const float Step = 0.08f;

        /// <summary>Pip's head and chest above the feet: the points that must stay in clear view.</summary>
        public const float HeadHeight = 0.95f;

        public const float ChestHeight = 0.6f;

        /// <summary>A talking partner's chest above the feet (June seated on her stool is lower than standing).</summary>
        public const float FocusHeight = 0.75f;

        /// <summary>Under the loft: the joists hang to 2.23 m and the bunting to 2.17 m.</summary>
        public const float HallCeilingY = 2.15f;

        /// <summary>Garlands, the star lantern's cord and the collar ties hang above this in the attic.</summary>
        public const float AtticTopY = 4.4f;

        /// <summary>Rafters, the wind-drive rod and the roof boards: keep this far below the ceiling slope.</summary>
        public const float RoofMargin = 0.3f;

        /// <summary>
        /// Extra cost of a swing past 90°: the lens then looks back at Pip and the stick's directions flip, so a shorter
        /// boom behind Pip is preferred whenever one exists.
        /// </summary>
        public const float FacingPenalty = 6f;

        /// <summary>How far the shoulder may drop when the room is too tight at the profile's rise.</summary>
        public const float LowShoulder = 0.4f;

        public const float AlcoveInnerX = CaseRouteLayout.InnerMinX + 0.96f;
        public const float AlcoveCeilingY = 3.75f;

        private static readonly float[] YawSteps = { 0f, 25f, -25f, 50f, -50f, 75f, -75f, 105f, -105f, 135f, -135f, 165f, -165f, 180f };

        private static readonly Blocker[] FixedBlockers = BuildBlockers();
        private static readonly Blocker OpenDoorLeaf = BuildDoorLeaf();

        /// <summary>Every solid the indoor camera avoids (door closed).</summary>
        public static IReadOnlyList<string> BlockerNames
        {
            get
            {
                var names = new List<string>();
                foreach (var b in FixedBlockers)
                {
                    names.Add(b.Name);
                }

                return names;
            }
        }

        public static InteriorRoom RoomOf(float x, float y, float z)
        {
            if (!CaseRouteLayout.Inside(x, z))
            {
                return InteriorRoom.None;
            }

            if (y > CaseRouteLayout.LoftY - 0.3f)
            {
                return InAlcove(x, z) ? InteriorRoom.Alcove : InteriorRoom.Attic;
            }

            if (OnStairFootprint(x, z) && y > CaseRouteLayout.FloorY + 0.15f)
            {
                return InteriorRoom.Stair;
            }

            return InteriorRoom.Foyer;
        }

        public static InteriorCameraProfile ProfileFor(InteriorRoom room)
        {
            switch (room)
            {
                case InteriorRoom.Foyer:
                    // A low shoulder and a near-level pitch keep the lens under the hall's joists at full length.
                    return new InteriorCameraProfile(2.4f, 0.1f, -4f, 9f);
                case InteriorRoom.Stair:
                    // Look down over the balustrade from the stairwell; under the low loft behind the stair, from high above Pip.
                    return new InteriorCameraProfile(2.2f, 0.2f, 10f, 66f);
                case InteriorRoom.Attic:
                case InteriorRoom.Alcove:
                    return new InteriorCameraProfile(2.2f, 0.15f, 2f, 40f);
                default:
                    return new InteriorCameraProfile(0f, 0f, 0f, 0f);
            }
        }

        private static bool InAlcove(float x, float z) =>
            x < AlcoveInnerX && Math.Abs(z - CaseRouteLayout.WindowZ) < (CaseRouteLayout.AlcoveMaxZ - CaseRouteLayout.AlcoveMinZ) * 0.5f;

        private static bool OnStairFootprint(float x, float z) =>
            x > CaseRouteLayout.StairBottomX - 0.05f && x < CaseRouteLayout.StairTopX + 0.05f &&
            z > CaseRouteLayout.StairMinZ - 0.05f && z < CaseRouteLayout.StairMaxZ + 0.05f;

        /// <summary>Stair tread height at <paramref name="x"/> (the floor before it, the loft after it).</summary>
        public static float StairSurface(float x)
        {
            float t = (x - CaseRouteLayout.StairBottomX) / (CaseRouteLayout.StairTopX - CaseRouteLayout.StairBottomX);
            t = Math.Max(0f, Math.Min(1f, t));
            return CaseRouteLayout.FloorY + t * (CaseRouteLayout.LoftY - CaseRouteLayout.FloorY);
        }

        /// <summary>Ceiling slope height at <paramref name="x"/> (the knee walls at the sides, the ridge at x = 0).</summary>
        public static float RoofAt(float x)
        {
            float slope = (CaseRouteLayout.ApexY - CaseRouteLayout.KneeY) / CaseRouteLayout.InnerMaxX;
            return CaseRouteLayout.KneeY + (CaseRouteLayout.InnerMaxX - Math.Abs(x)) * slope;
        }

        /// <summary>True when (x, y, z) is room air inside the house (walls and solids are tested by the blockers).</summary>
        public static bool InAir(float x, float y, float z)
        {
            if (x < CaseRouteLayout.InnerMinX || x > CaseRouteLayout.InnerMaxX || z < CaseRouteLayout.InnerMinZ ||
                z > CaseRouteLayout.InnerMaxZ || y < CaseRouteLayout.FloorY + 0.1f)
            {
                return false;
            }

            // The workshop nook (behind its partition, below the loft) is never a camera place.
            if (x > CaseRouteLayout.NookX && z > CaseRouteLayout.NookMinZ && y < CaseRouteLayout.LoftY)
            {
                return false;
            }

            float top = Math.Min(AtticTopY, RoofAt(x) - RoofMargin);
            if (InAlcove(x, z))
            {
                top = Math.Max(top, AlcoveCeilingY);
            }

            return y <= top;
        }

        /// <summary>A camera sphere at (x, y, z) is in room air and touches no solid.</summary>
        public static bool SphereClear(float x, float y, float z, bool doorOpen, float radius = Radius)
        {
            if (!InAir(x, y, z))
            {
                return false;
            }

            var c = new V(x, y, z);
            foreach (var b in FixedBlockers)
            {
                if (b.OverlapsSphere(c, radius))
                {
                    return false;
                }
            }

            return !doorOpen || !OpenDoorLeaf.OverlapsSphere(c, radius);
        }

        /// <summary>True when a sphere at (x, y, z) touches any indoor solid (room air is not checked).</summary>
        public static bool TouchesSolid(float x, float y, float z, float radius, bool doorOpen)
        {
            var c = new V(x, y, z);
            foreach (var b in FixedBlockers)
            {
                if (b.OverlapsSphere(c, radius))
                {
                    return true;
                }
            }

            return doorOpen && OpenDoorLeaf.OverlapsSphere(c, radius);
        }

        /// <summary>The first solid crossing the line from a to b, or null when the line is clear.</summary>
        public static string FirstBlocker(float ax, float ay, float az, float bx, float by, float bz, bool doorOpen)
        {
            var a = new V(ax, ay, az);
            var b = new V(bx, by, bz);
            foreach (var blocker in FixedBlockers)
            {
                if (blocker.HitsSegment(a, b, SightRadius))
                {
                    return blocker.Name;
                }
            }

            return doorOpen && OpenDoorLeaf.HitsSegment(a, b, SightRadius) ? OpenDoorLeaf.Name : null;
        }

        public static bool SightClear(float ax, float ay, float az, float bx, float by, float bz, bool doorOpen) =>
            FirstBlocker(ax, ay, az, bx, by, bz, doorOpen) == null;

        /// <summary>The shoulder point the boom starts from, after lowering the rise until it is in clear air.</summary>
        public static float SafeRise(float pipX, float pipY, float pipZ, float pivotHeight, float rise, bool doorOpen)
        {
            for (float r = rise; r > -0.45f; r -= 0.05f)
            {
                if (SphereClear(pipX, pipY + pivotHeight + r, pipZ, doorOpen, Radius * 0.75f))
                {
                    return r;
                }
            }

            return rise;
        }

        /// <summary>Unit vector from the shoulder back to the camera for a yaw and pitch (degrees; pitch down +).</summary>
        public static void BackDirection(float yaw, float pitch, out float x, out float y, out float z)
        {
            double yr = yaw * Math.PI / 180.0;
            double pr = pitch * Math.PI / 180.0;
            x = (float)(-Math.Sin(yr) * Math.Cos(pr));
            y = (float)Math.Sin(pr);
            z = (float)(-Math.Cos(yr) * Math.Cos(pr));
        }

        /// <summary>
        /// The probe: the longest boom up to <paramref name="maxDistance"/> from the shoulder (hx, hy, hz) along the
        /// yaw and pitch whose camera sphere stays in clear air and sees Pip's head and chest (and the focus) all the way.
        /// </summary>
        public static float ClearDistance(float hx, float hy, float hz, float yaw, float pitch, float maxDistance,
            float pipX, float pipY, float pipZ, bool doorOpen, bool hasFocus = false, float fx = 0f, float fy = 0f,
            float fz = 0f)
        {
            var scope = Near(new V(hx, hy, hz), maxDistance, hasFocus ? new V(fx, fy, fz) : (V?)null, doorOpen);
            return Probe(scope, new V(hx, hy, hz), yaw, pitch, maxDistance, Targets(pipX, pipY, pipZ, hasFocus, fx, fy, fz));
        }

        /// <summary>Chooses the boom for this frame's request (see the class summary). Inactive outside the house.</summary>
        public static InteriorShot Solve(in InteriorCameraRequest request)
        {
            var room = RoomOf(request.PipX, request.PipY, request.PipZ);
            if (room == InteriorRoom.None)
            {
                return default;
            }

            var profile = ProfileFor(room);
            float rise = SafeRise(request.PipX, request.PipY, request.PipZ, request.PivotHeight, profile.Rise, request.DoorOpen);
            var hand = new V(request.PipX, request.PipY + request.PivotHeight + rise, request.PipZ);
            V? focus = request.HasFocus ? new V(request.FocusX, request.FocusY + FocusHeight, request.FocusZ) : (V?)null;
            var scope = Near(hand, profile.Distance, focus, request.DoorOpen);
            var targets = Targets(request.PipX, request.PipY, request.PipZ, request.HasFocus, request.FocusX,
                request.FocusY, request.FocusZ);
            float preferred = Clamp(request.Pitch, profile.MinPitch, profile.MaxPitch);

            // The player's own framing wins whenever it fits.
            float d0 = Probe(scope, hand, request.Yaw, preferred, profile.Distance, targets);
            if (d0 >= profile.Distance - 1e-3f)
            {
                return Shot(room, d0, preferred, 0f, rise, hand, request.Yaw, true);
            }

            float bestScore = float.MaxValue;
            float bestD = d0, bestPitch = preferred, bestYaw = 0f, bestRise = rise;
            var hands = new List<(float Rise, V Hand)> { (rise, hand) };
            float low = SafeRise(request.PipX, request.PipY, request.PipZ, request.PivotHeight, rise - LowShoulder,
                request.DoorOpen);
            if (low < rise - 0.1f)
            {
                hands.Add((low, new V(request.PipX, request.PipY + request.PivotHeight + low, request.PipZ)));
            }

            // Candidates in order of their best possible score, so the search stops as soon as nothing can win.
            var candidates = new List<(float Bound, int Hand, float Yaw, float Pitch)>();
            var yaws = new List<float>(YawSteps.Length + 1) { request.PreviousYawOffset };
            yaws.AddRange(YawSteps);
            for (int h = 0; h < hands.Count; h++)
            {
                foreach (float yawOffset in yaws)
                {
                    foreach (float pitch in Pitches(preferred, profile))
                    {
                        float bound = Math.Abs(pitch - preferred) * 0.06f + Math.Abs(yawOffset) * 0.05f +
                                      (Math.Abs(yawOffset) > 90f ? FacingPenalty : 0f) +
                                      Math.Abs(yawOffset - request.PreviousYawOffset) * 0.015f +
                                      (rise - hands[h].Rise) * 2f;
                        candidates.Add((bound, h, yawOffset, pitch));
                    }
                }
            }

            candidates.Sort((x, y) => x.Bound.CompareTo(y.Bound));
            foreach (var c in candidates)
            {
                if (c.Bound >= bestScore)
                {
                    break;
                }

                float d = Probe(scope, hands[c.Hand].Hand, request.Yaw + c.Yaw, c.Pitch, profile.Distance, targets);
                float score = c.Bound + (profile.Distance - d) * 8f;
                if (d < MinDistance)
                {
                    score += 100f + (MinDistance - d) * 50f;
                }

                if (score < bestScore)
                {
                    bestScore = score;
                    bestD = d;
                    bestPitch = c.Pitch;
                    bestYaw = c.Yaw;
                    bestRise = hands[c.Hand].Rise;
                }
            }

            var bestHand = new V(request.PipX, request.PipY + request.PivotHeight + bestRise, request.PipZ);
            return Shot(room, bestD, bestPitch, bestYaw, bestRise, bestHand, request.Yaw, bestD >= MinDistance);
        }

        private static InteriorShot Shot(InteriorRoom room, float d, float pitch, float yawOffset, float rise, V hand,
            float yaw, bool clear)
        {
            BackDirection(yaw + yawOffset, pitch, out float bx, out float by, out float bz);
            return new InteriorShot(room, d, pitch, NormalizeAngle(yawOffset), rise, clear, hand.X + bx * d,
                hand.Y + by * d, hand.Z + bz * d);
        }

        private static IEnumerable<float> Pitches(float preferred, InteriorCameraProfile profile)
        {
            yield return preferred;
            for (float k = 8f; k <= 48f; k += 8f)
            {
                if (preferred + k <= profile.MaxPitch + 1e-3f)
                {
                    yield return preferred + k;
                }
            }

            if (profile.MaxPitch > preferred + 1e-3f && (profile.MaxPitch - preferred) % 8f > 1e-3f)
            {
                yield return profile.MaxPitch;
            }

            if (preferred - 6f >= profile.MinPitch - 1e-3f)
            {
                yield return preferred - 6f;
            }
        }

        private static V[] Targets(float pipX, float pipY, float pipZ, bool hasFocus, float fx, float fy, float fz)
        {
            var head = new V(pipX, pipY + HeadHeight, pipZ);
            var chest = new V(pipX, pipY + ChestHeight, pipZ);
            return hasFocus ? new[] { head, chest, new V(fx, fy + FocusHeight, fz) } : new[] { head, chest };
        }

        private static float Probe(List<Blocker> scope, V hand, float yaw, float pitch, float maxDistance, V[] targets)
        {
            BackDirection(yaw, pitch, out float bx, out float by, out float bz);
            var back = new V(bx, by, bz);
            float best = 0f;
            for (float d = Step; ; d += Step)
            {
                bool last = d >= maxDistance - 1e-4f;
                if (last)
                {
                    d = maxDistance;
                }

                var camera = hand + back * d;
                if (!Clear(scope, camera, targets, 2))
                {
                    break;
                }

                best = d;
                if (last)
                {
                    break;
                }
            }

            if (targets.Length <= 2)
            {
                return best;
            }

            // The talking partner only has to be in view where the lens ends up, not all along the boom.
            for (float d = best; d >= MinDistance - 1e-4f; d -= Step)
            {
                if (Clear(scope, hand + back * d, targets, targets.Length))
                {
                    return d;
                }
            }

            return 0f;
        }

        private static bool Clear(List<Blocker> scope, V camera, V[] targets, int targetCount)
        {
            if (!InAir(camera.X, camera.Y, camera.Z))
            {
                return false;
            }

            foreach (var b in scope)
            {
                if (b.OverlapsSphere(camera, Radius))
                {
                    return false;
                }
            }

            for (int i = 0; i < targetCount; i++)
            {
                foreach (var b in scope)
                {
                    if (b.HitsSegment(camera, targets[i], SightRadius))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private static List<Blocker> Near(V hand, float reach, V? focus, bool doorOpen)
        {
            float r = reach + Radius + 0.3f;
            if (focus.HasValue)
            {
                r = Math.Max(r, (focus.Value - hand).Length + 0.5f);
            }

            var list = new List<Blocker>();
            foreach (var b in FixedBlockers)
            {
                if ((b.Centre - hand).Length <= r + b.Bound)
                {
                    list.Add(b);
                }
            }

            if (doorOpen && (OpenDoorLeaf.Centre - hand).Length <= r + OpenDoorLeaf.Bound)
            {
                list.Add(OpenDoorLeaf);
            }

            return list;
        }

        // ------------------------------------------------------------------------------------------------- solids

        private static Blocker[] BuildBlockers()
        {
            var list = new List<Blocker>();
            foreach (var slab in CaseRouteLayout.Slabs)
            {
                if (slab.Group == SlabGroup.Reference || slab.Group == SlabGroup.Route ||
                    slab.Name == CaseRouteLayout.DoorBlockerName)
                {
                    continue;
                }

                list.Add(Blocker.FromSlab(slab));
            }

            const float fy = CaseRouteLayout.FloorY;
            const float loft = CaseRouteLayout.LoftY;
            const float minX = CaseRouteLayout.InnerMinX - 0.1f;
            const float maxX = CaseRouteLayout.InnerMaxX + 0.1f;
            const float maxZ = CaseRouteLayout.InnerMaxZ + 0.1f;
            const float front = CaseRouteLayout.LoftFrontZ - 0.15f;
            const float holeMinX = CaseRouteLayout.StairHoleMinX;
            const float holeMaxX = CaseRouteLayout.StairTopX;
            // The hall ceiling: the loft floor with its joists, fascia and bunting below it (no colliders there).
            list.Add(Blocker.Box("Ceiling_West", minX, HallCeilingY, front, holeMinX, loft, maxZ));
            list.Add(Blocker.Box("Ceiling_Middle", holeMinX, HallCeilingY, front, holeMaxX, loft, CaseRouteLayout.StairMinZ));
            list.Add(Blocker.Box("Ceiling_East", holeMaxX, HallCeilingY, front, maxX, loft, maxZ));
            // The stair's closed side and cupboard (a wedge under the treads) and its balustrade.
            const float sx0 = CaseRouteLayout.StairBottomX;
            const float sx1 = CaseRouteLayout.StairTopX;
            float k = (loft - fy) / (sx1 - sx0);
            list.Add(Blocker.UnderLine("Stair_Body", sx0 - 0.02f, sx1 + 0.05f, CaseRouteLayout.StairMinZ - 0.07f, maxZ,
                fy - 0.5f, sx0, fy - 0.06f, k));
            list.Add(Blocker.UnderLine("Stair_Balustrade", sx0 - 0.2f, sx1, CaseRouteLayout.StairMinZ - 0.1f,
                CaseRouteLayout.StairMinZ + 0.16f, fy - 0.1f, sx0 - 0.1f, fy + 1.0f, k));
            list.Add(Blocker.Box("Stair_Newel", sx0 - 0.2f, fy, CaseRouteLayout.StairMinZ - 0.05f, sx0 - 0.02f, fy + 1.2f,
                CaseRouteLayout.StairMinZ + 0.16f));
            // Hanging lights and paper stars in the entry bay and the attic, the drive shafts by the nook.
            list.Add(Blocker.Box("OwlLantern", -0.3f, 4.0f, CaseRouteLayout.InnerMinZ - 0.1f, 0.3f, 6.5f, CaseRouteLayout.FrontZ + 1.2f));
            Star(list, -1.5f, 4.2f, 11.6f);
            Star(list, 1.35f, 4.6f, 11.8f);
            Star(list, -0.75f, 5.3f, 12.1f);
            Star(list, 1.9f, 3.5f, 11.5f);
            Star(list, -2.1f, 3.4f, 11.9f);
            list.Add(Blocker.Box("AtticStarLantern", -0.45f, 4.1f, 13.75f, 0.05f, 5.2f, 14.95f));
            list.Add(Blocker.Box("WindDriveShaft", 2.03f, loft, 14.0f, 2.23f, 4.2f, 14.2f));
            list.Add(Blocker.Box("PushRod", 1.83f, loft, 13.5f, 1.99f, 4.2f, 13.7f));
            // Dressing near the walls and floor that a low lens could graze.
            list.Add(Blocker.Box("JuneStool", -1.97f, loft, 14.83f, -1.53f, loft + 0.42f, 15.27f));
            list.Add(Blocker.Box("Foyer_PegsAndHat", minX, fy, 12.8f, CaseRouteLayout.InnerMinX + 0.45f, fy + 1.4f, 14.0f));
            list.Add(Blocker.Box("Foyer_Broom", 0.78f, fy, CaseRouteLayout.FrontZ + 0.1f, 1.12f, fy + 1.6f, CaseRouteLayout.FrontZ + 0.8f));
            list.Add(Blocker.Box("Foyer_Crate", 0.82f, fy, 16.18f, 1.42f, fy + 0.4f, 16.72f));
            list.Add(Blocker.Box("Pumpkins_Back", -2.75f, fy, 14.55f, -2.1f, fy + 0.42f, 15.5f));
            list.Add(Blocker.Box("Pumpkin_Door", -1.3f, fy, 11.25f, -0.8f, fy + 0.32f, 11.75f));
            return list.ToArray();
        }

        private static void Star(List<Blocker> list, float x, float y, float z) =>
            list.Add(Blocker.Box("PaperStar", x - 0.18f, y - 0.18f, z - 0.18f, x + 0.18f, 6.5f, z + 0.18f));

        /// <summary>The inner door leaf swung 100° into the hall about its west hinge.</summary>
        private static Blocker BuildDoorLeaf()
        {
            const double a = 100.0 * Math.PI / 180.0;
            float hx = CaseRouteLayout.DoorHingeX;
            float hz = CaseRouteLayout.FrontZ + 0.07f;
            float len = CaseRouteLayout.DoorMaxX - CaseRouteLayout.DoorHingeX;
            float ex = hx + (float)Math.Cos(a) * len;
            float ez = hz + (float)Math.Sin(a) * len;
            var slab = new StageSlab("InnerDoor_Open", SlabKind.Wall, SlabGroup.Interior, hx, CaseRouteLayout.DoorTopY, hz,
                ex, CaseRouteLayout.DoorTopY, ez, 0.14f, CaseRouteLayout.DoorTopY - CaseRouteLayout.FloorY);
            return Blocker.FromSlab(slab);
        }

        private static float Clamp(float v, float min, float max) => v < min ? min : v > max ? max : v;

        private static float NormalizeAngle(float degrees)
        {
            degrees %= 360f;
            if (degrees > 180f)
            {
                degrees -= 360f;
            }
            else if (degrees <= -180f)
            {
                degrees += 360f;
            }

            return degrees;
        }

        // ------------------------------------------------------------------------------------------------ geometry

        private readonly struct V
        {
            public V(float x, float y, float z)
            {
                X = x;
                Y = y;
                Z = z;
            }

            public float X { get; }
            public float Y { get; }
            public float Z { get; }

            public float Length => (float)Math.Sqrt(X * X + Y * Y + Z * Z);

            public V Normalized
            {
                get
                {
                    float l = Length;
                    return l > 1e-6f ? new V(X / l, Y / l, Z / l) : new V(0f, 0f, 0f);
                }
            }

            public static V operator +(V a, V b) => new V(a.X + b.X, a.Y + b.Y, a.Z + b.Z);
            public static V operator -(V a, V b) => new V(a.X - b.X, a.Y - b.Y, a.Z - b.Z);
            public static V operator *(V a, float s) => new V(a.X * s, a.Y * s, a.Z * s);

            public static float Dot(V a, V b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

            public static V Cross(V a, V b) =>
                new V(a.Y * b.Z - a.Z * b.Y, a.Z * b.X - a.X * b.Z, a.X * b.Y - a.Y * b.X);
        }

        /// <summary>A convex solid as half-spaces n·p ≤ d (unit normals), with a bounding sphere for culling.</summary>
        private sealed class Blocker
        {
            private readonly V[] _normals;
            private readonly float[] _offsets;

            private Blocker(string name, V[] normals, float[] offsets, V centre, float bound)
            {
                Name = name;
                _normals = normals;
                _offsets = offsets;
                Centre = centre;
                Bound = bound;
            }

            public string Name { get; }
            public V Centre { get; }
            public float Bound { get; }

            public static Blocker Box(string name, float x0, float y0, float z0, float x1, float y1, float z1)
            {
                var normals = new[]
                {
                    new V(1f, 0f, 0f), new V(-1f, 0f, 0f), new V(0f, 1f, 0f), new V(0f, -1f, 0f), new V(0f, 0f, 1f),
                    new V(0f, 0f, -1f),
                };
                var offsets = new[] { x1, -x0, y1, -y0, z1, -z0 };
                var centre = new V((x0 + x1) * 0.5f, (y0 + y1) * 0.5f, (z0 + z1) * 0.5f);
                float bound = new V(x1 - x0, y1 - y0, z1 - z0).Length * 0.5f;
                return new Blocker(name, normals, offsets, centre, bound);
            }

            /// <summary>
            /// A box in x and z whose top is the line y = topAtX0 + slope (x − x0), cut off below at <paramref name="yMin"/>
            /// (the stair's closed side under its treads, its balustrade along the rail line).
            /// </summary>
            public static Blocker UnderLine(string name, float xMin, float xMax, float zMin, float zMax, float yMin, float x0,
                float topAtX0, float slope)
            {
                var top = new V(-slope, 1f, 0f);
                float len = top.Length;
                var n = top.Normalized;
                float d = (topAtX0 - slope * x0) / len;
                var normals = new[] { new V(1f, 0f, 0f), new V(-1f, 0f, 0f), new V(0f, -1f, 0f), new V(0f, 0f, 1f), new V(0f, 0f, -1f), n };
                var offsets = new[] { xMax, -xMin, -yMin, zMax, -zMin, d };
                float yTop = topAtX0 + slope * (xMax - x0);
                var centre = new V((xMin + xMax) * 0.5f, (yMin + yTop) * 0.5f, (zMin + zMax) * 0.5f);
                float bound = new V(xMax - xMin, yTop - yMin, zMax - zMin).Length * 0.5f;
                return new Blocker(name, normals, offsets, centre, bound);
            }

            /// <summary>The slab's box exactly as the runtime builds its collider (OwlHouseStage.SlabCollider).</summary>
            public static Blocker FromSlab(StageSlab slab)
            {
                var start = new V(slab.StartX, slab.StartY, slab.StartZ);
                var end = new V(slab.EndX, slab.EndY, slab.EndZ);
                var along = end - start;
                float length = along.Length;
                var f = along.Normalized;
                var right = V.Cross(new V(0f, 1f, 0f), f);
                right = right.Length > 1e-4f ? right.Normalized : new V(1f, 0f, 0f);
                var up = V.Cross(f, right).Normalized;
                if (up.Y < 0f)
                {
                    up = up * -1f;
                }

                var centre = (start + end) * 0.5f - up * (slab.Depth * 0.5f);
                float hx = slab.Width * 0.5f, hy = slab.Depth * 0.5f, hz = length * 0.5f;
                var normals = new[] { right, right * -1f, up, up * -1f, f, f * -1f };
                var offsets = new[]
                {
                    V.Dot(right, centre) + hx, -V.Dot(right, centre) + hx, V.Dot(up, centre) + hy, -V.Dot(up, centre) + hy,
                    V.Dot(f, centre) + hz, -V.Dot(f, centre) + hz,
                };
                return new Blocker(slab.Name, normals, offsets, centre, (float)Math.Sqrt(hx * hx + hy * hy + hz * hz));
            }

            public bool OverlapsSphere(V c, float r)
            {
                if ((c - Centre).Length > Bound + r)
                {
                    return false;
                }

                for (int i = 0; i < _normals.Length; i++)
                {
                    if (V.Dot(_normals[i], c) > _offsets[i] + r)
                    {
                        return false;
                    }
                }

                return true;
            }

            /// <summary>Cyrus–Beck clip of the segment a→b against the solid grown by <paramref name="r"/>.</summary>
            public bool HitsSegment(V a, V b, float r)
            {
                var ab = b - a;
                var mid = (a + b) * 0.5f;
                if ((mid - Centre).Length > Bound + r + ab.Length * 0.5f)
                {
                    return false;
                }

                float t0 = 0f, t1 = 1f;
                for (int i = 0; i < _normals.Length; i++)
                {
                    float denom = V.Dot(_normals[i], ab);
                    float num = _offsets[i] + r - V.Dot(_normals[i], a);
                    if (Math.Abs(denom) < 1e-7f)
                    {
                        if (num < 0f)
                        {
                            return false;
                        }

                        continue;
                    }

                    float t = num / denom;
                    if (denom > 0f)
                    {
                        t1 = Math.Min(t1, t);
                    }
                    else
                    {
                        t0 = Math.Max(t0, t);
                    }

                    if (t0 > t1)
                    {
                        return false;
                    }
                }

                return true;
            }
        }
    }
}
