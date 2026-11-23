using System;
using System.Collections.Generic;
using SpookyStreet.Core.World;
using Verbs = SpookyStreet.Core.Localization.LocalizationKeys.Verbs;

namespace SpookyStreet.Core.Case
{
    /// <summary>A staged character spot: ground point and the Unity yaw the character faces.</summary>
    public readonly struct CastSpot
    {
        public CastSpot(float x, float y, float z, float facingYaw)
        {
            Position = new StagePoint(x, y, z);
            FacingYaw = facingYaw;
        }

        public StagePoint Position { get; }
        public float FacingYaw { get; }
    }

    /// <summary>
    /// The interior half of case 01 on the stage (S002 interior wiring, house v3): the Use targets in the treehouse
    /// route, the attic, the hall and the workshop; when each is offered; the trigger zones (attic entry, hall); June's
    /// and Aunt Kit's marks; and the finale sightline. Positions come from <see cref="CaseRouteLayout"/>: inside
    /// targets stand in the separate interior (D24.1), outside ones on the street.
    /// </summary>
    public static partial class CaseStage
    {
        public const string PointCloth = "cloth";
        public const string PointSill = "sill";
        public const string PointJuneAttic = "june_attic";
        public const string PointJuneFoyer = "june_foyer";
        public const string PointJunePorch = "june_porch";
        public const string PointKit = "kit";
        public const string PointLatch = "latch";
        public const string PointGrille = "grille";
        public const string PointServicePanel = "service_panel";
        public const string PointLinkArm = "link_arm";
        public const string PointCrank = "crank";

        private const float Ifz = CaseRouteLayout.InteriorFrontZ;

        /// <summary>June on her stool in the attic, turned toward the stair top (seat 0.30 m; house v3 §3.5).</summary>
        public static readonly CastSpot JuneAttic = new CastSpot(-1.1f, CaseRouteLayout.LoftY, Ifz + 2.75f, 35f);

        /// <summary>June's hall mark: by the door but 1.5 m clear of the latch, so their markers never overlap.</summary>
        public static readonly CastSpot JuneFoyer = new CastSpot(-1.35f, CaseRouteLayout.FloorY, Ifz + 1.2f, 150f);

        /// <summary>On the porch for the reopening, west of the door, facing the street.</summary>
        public static readonly CastSpot JunePorch = new CastSpot(-1.35f, CaseRouteLayout.FloorY, CaseRouteLayout.FrontZ - 0.85f, 175f);

        /// <summary>Aunt Kit at her booth (the ticket door), east of the doorway, facing the street.</summary>
        public static readonly CastSpot KitBooth = new CastSpot(1.15f, CaseRouteLayout.FloorY, CaseRouteLayout.FrontZ - 0.75f, 185f);

        /// <summary>The finale restages the arrival sightline: arrival porch eye height, looking at the booth (spec §4).</summary>
        public static readonly StagePoint FinaleEye = new StagePoint(0.35f, 1.75f, -13.6f);
        public static readonly StagePoint FinaleLookAt = new StagePoint(0f, 4.0f, CaseRouteLayout.FrontZ - 0.6f);

        /// <summary>
        /// June's walk from her stool down to her hall mark (presentation only; only the mark persists): round the
        /// stairwell rail to the stair top, down the upper flight to the landing, down the lower flight along the west
        /// wall, then across the hall (house v3 §3.5).
        /// </summary>
        public static readonly StagePoint[] JuneWalk =
        {
            JuneAttic.Position,
            new StagePoint(-0.3f, CaseRouteLayout.LoftY, Ifz + 4.6f),
            new StagePoint(0.55f, CaseRouteLayout.LoftY, Ifz + 5.3f),
            new StagePoint(0.6f, CaseRouteLayout.LoftY, Ifz + 6.3f),
            new StagePoint(0.14f, CaseRouteLayout.LoftY, Ifz + 6.3f),
            new StagePoint(-2.1f, CaseRouteLayout.LandingY, Ifz + 6.3f),
            new StagePoint(-2.85f, CaseRouteLayout.LandingY, Ifz + 6.3f),
            new StagePoint(-2.85f, CaseRouteLayout.LandingY, Ifz + 5.5f),
            new StagePoint(-2.85f, CaseRouteLayout.FloorY, Ifz + 3.26f),
            new StagePoint(-2.85f, CaseRouteLayout.FloorY, Ifz + 2.6f),
            JuneFoyer.Position,
        };

        /// <summary>Her cruise speed: the walk clip's pace (0.55 m steps, a 1.0 s cycle), so her feet do not slide.</summary>
        public const float JuneWalkSpeed = 1.1f;

        /// <summary>
        /// June has no walk clip yet (art to come), so her glide is shaped to read as a walk: she first turns toward the
        /// path for <see cref="JuneTurnSeconds"/>, speeds up over <see cref="JuneEaseIn"/> metres, slows down over the last
        /// <see cref="JuneEaseOut"/> metres, and faces a point <see cref="JuneLookAhead"/> metres further along the path,
        /// so she turns into each corner before she reaches it.
        /// </summary>
        public const float JuneTurnSeconds = 0.4f;

        public const float JuneEaseIn = 0.7f;
        public const float JuneEaseOut = 0.9f;
        public const float JuneLookAhead = 0.45f;

        /// <summary>Seconds from June's first turn to her stop at the foyer mark.</summary>
        public static float JuneWalkSeconds
        {
            get
            {
                float length = PlanLength(JuneWalk);
                EaseLengths(length, out float easeIn, out float easeOut);
                return JuneTurnSeconds + (length - easeIn - easeOut) / JuneWalkSpeed + 2f * (easeIn + easeOut) / JuneWalkSpeed;
            }
        }

        /// <summary>
        /// Metres walked <paramref name="seconds"/> after June starts (0 while she turns toward the path), with a
        /// constant-acceleration ease in and ease out around the cruise at <see cref="JuneWalkSpeed"/>.
        /// </summary>
        public static float JuneWalkDistance(float seconds)
        {
            float length = PlanLength(JuneWalk);
            EaseLengths(length, out float easeIn, out float easeOut);
            float v = JuneWalkSpeed;
            float t = seconds - JuneTurnSeconds;
            if (t <= 0f)
            {
                return 0f;
            }

            float t1 = 2f * easeIn / v;
            float cruise = length - easeIn - easeOut;
            float t2 = cruise / v;
            float t3 = 2f * easeOut / v;
            if (t < t1)
            {
                return 0.5f * (v / t1) * t * t;
            }

            if (t < t1 + t2)
            {
                return easeIn + v * (t - t1);
            }

            float left = t1 + t2 + t3 - t;
            return left <= 0f ? length : length - 0.5f * (v / t3) * left * left;
        }

        /// <summary>Where June faces at <paramref name="distance"/> along her walk: toward a point a little ahead.</summary>
        public static void JuneFacing(float distance, out float headingX, out float headingZ)
        {
            float length = PlanLength(JuneWalk);
            var here = AlongPlan(JuneWalk, distance, out headingX, out headingZ);
            if (distance + 0.05f >= length)
            {
                return;
            }

            var ahead = AlongPlan(JuneWalk, Math.Min(length, distance + JuneLookAhead), out _, out _);
            float dx = ahead.X - here.X, dz = ahead.Z - here.Z;
            float l = (float)Math.Sqrt(dx * dx + dz * dz);
            if (l > 1e-3f)
            {
                headingX = dx / l;
                headingZ = dz / l;
            }
        }

        private static void EaseLengths(float length, out float easeIn, out float easeOut)
        {
            float scale = Math.Min(1f, length / (JuneEaseIn + JuneEaseOut));
            easeIn = JuneEaseIn * scale;
            easeOut = JuneEaseOut * scale;
        }

        public static readonly IReadOnlyList<InteractionPoint> InteriorPoints = BuildInteriorPoints();

        private static IReadOnlyList<InteractionPoint> _allPoints;

        /// <summary>Every Use target of the case, exterior then interior (built on first use: partial-class field
        /// initializers in different files run in no guaranteed order).</summary>
        public static IReadOnlyList<InteractionPoint> AllPoints =>
            _allPoints ?? (_allPoints = Concat(ExteriorPoints, InteriorPoints));

        /// <summary>
        /// Attic entry zone: Pip's feet in the dormer alcove just inside June's window (where the dormer threshold puts
        /// Pip). Entering it reports <see cref="CaseActionKind.EnterAttic"/> (the reducer ignores repeats), so a resume
        /// or any other way into the alcove records the entry too.
        /// </summary>
        public static bool InAtticEntry(float x, float y, float z) =>
            CaseRouteLayout.Inside(x, z) && x > CaseRouteLayout.InnerWindowX - 0.05f && x < -1.45f &&
            Math.Abs(z - CaseRouteLayout.InnerWindowZ) < 0.75f &&
            y > CaseRouteLayout.LoftY - 0.3f && y < CaseRouteLayout.LoftY + 1.0f;

        /// <summary>Hall zone: inside the interior at ground-floor height.</summary>
        public static bool InFoyer(float x, float y, float z) =>
            CaseRouteLayout.Inside(x, z) && y < CaseRouteLayout.FloorY + 0.9f && x < CaseRouteLayout.NookX;

        /// <summary>
        /// Whether <paramref name="point"/> is offered right now: the right side of the shell, the character present at
        /// that mark, the latch only while the door is latched, the arm and crank only behind an open service panel.
        /// <paramref name="juneWalking"/> hides June's markers during her walk down the stair.
        /// </summary>
        public static bool IsOffered(InteractionPoint point, CaseWorldView view, bool pipInside, bool juneWalking)
        {
            if (point == null)
            {
                return false;
            }

            if ((point.Side == PointSide.Inside && !pipInside) || (point.Side == PointSide.Outside && pipInside))
            {
                return false;
            }

            switch (point.Id)
            {
                case PointFrontDoor:
                case PointLatch:
                    return view.FrontDoor == DoorState.Latched;
                case PointJuneAttic:
                    return view.June == JunePlacement.Attic && !juneWalking;
                case PointJuneFoyer:
                    return view.June == JunePlacement.Foyer && !juneWalking;
                case PointJunePorch:
                    return view.June == JunePlacement.Porch;
                case PointKit:
                    return view.KitPresent;
                case PointServicePanel:
                    return !view.ServicePanelOpen;
                case PointLinkArm:
                    return view.ServicePanelOpen && !view.ArmLinked;
                case PointCrank:
                    return view.ServicePanelOpen;
                default:
                    return true;
            }
        }

        private static InteractionPoint[] BuildInteriorPoints()
        {
            float roof = CaseRouteLayout.TreehouseY;
            float floor = CaseRouteLayout.FloorY;
            float workX = CaseRouteLayout.NookX - 0.05f;
            return new[]
            {
                // The silver scrap on the bridge-head post, at child eye level from the shed roof.
                new InteractionPoint(PointCloth, CaseAction.Of(CaseActionKind.ExamineCloth),
                    new StagePoint(CaseRouteLayout.ClothX - 0.15f, roof, CaseRouteLayout.ClothZ), DefaultReach,
                    CaseRouteLayout.ClothY - roof),
                // The matching snag on the window's north jamb (SillSnag pivot −3.57, 1.97, 4.42), seen from the alcove.
                new InteractionPoint(PointSill, CaseAction.Of(CaseActionKind.ExamineSill),
                    new StagePoint(-3.4f, CaseRouteLayout.SillY, Ifz + 1.97f), 1.3f, 0.42f, PointSide.Inside),
                new InteractionPoint(PointJuneAttic, CaseAction.Talk(Speaker.June), JuneAttic.Position, 1.9f, 1.15f,
                    PointSide.Inside),
                new InteractionPoint(PointJuneFoyer, CaseAction.Talk(Speaker.June), JuneFoyer.Position, 1.8f, 1.55f,
                    PointSide.Inside),
                new InteractionPoint(PointJunePorch, CaseAction.Talk(Speaker.June), JunePorch.Position, 1.8f, 1.55f,
                    PointSide.Outside),
                new InteractionPoint(PointKit, CaseAction.Talk(Speaker.Kit), KitBooth.Position, 1.8f, 1.75f,
                    PointSide.Outside),
                // The wooden latch on the inside of the door (DoorLatch pivot at x 0.12; the bar runs east).
                new InteractionPoint(PointLatch, CaseAction.Of(CaseActionKind.LiftLatch),
                    new StagePoint(0.35f, floor, Ifz + 0.3f), DefaultReach, 1.0f, PointSide.Inside),
                // Workshop, through the partition: the low grille, the child-height service panel, then the arm and
                // crank behind it. Each sits at its own part of the partition so the markers stay apart.
                new InteractionPoint(PointGrille, CaseAction.Of(CaseActionKind.ExamineGrille),
                    new StagePoint(workX, floor, CaseRouteLayout.MechanismZ + 0.65f), DefaultReach, 0.45f, PointSide.Inside),
                // The child-height service panel is painted with an owl: opening it is the owl-panel clue (D26).
                new InteractionPoint(PointServicePanel, CaseAction.Of(CaseActionKind.ExamineOwlPanel),
                    new StagePoint(workX, floor, CaseRouteLayout.MechanismZ - 0.2f), DefaultReach, 1.05f, PointSide.Inside,
                    Verbs.Open),
                new InteractionPoint(PointLinkArm, CaseAction.Of(CaseActionKind.LinkArm),
                    new StagePoint(workX, floor, CaseRouteLayout.MechanismZ - 0.55f), DefaultReach, 0.95f, PointSide.Inside),
                // A child never aims at the thin handle: the crank's target is the whole crank disc (spec §5).
                new InteractionPoint(PointCrank, CaseAction.Of(CaseActionKind.TurnCrank),
                    new StagePoint(workX, floor, CaseRouteLayout.MechanismZ + 0.05f), DefaultReach, 0.85f, PointSide.Inside),
            };
        }

        private static IReadOnlyList<InteractionPoint> Concat(IReadOnlyList<InteractionPoint> a, IReadOnlyList<InteractionPoint> b)
        {
            var all = new List<InteractionPoint>(a.Count + b.Count);
            all.AddRange(a);
            all.AddRange(b);
            return all;
        }

        /// <summary>Length of <paramref name="path"/> over the floor plan (x and z only).</summary>
        public static float PlanLength(IReadOnlyList<StagePoint> path)
        {
            float total = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                total += PlanDistance(path[i - 1], path[i]);
            }

            return total;
        }

        /// <summary>
        /// Point <paramref name="planDistance"/> floor-plan metres along <paramref name="path"/> (clamped), and its heading.
        /// <paramref name="slopeFactor"/> is the segment's 3D length over its plan length (1 on level floors).
        /// </summary>
        public static StagePoint AlongPlan(IReadOnlyList<StagePoint> path, float planDistance, out float headingX,
            out float headingZ) => AlongPlan(path, planDistance, out headingX, out headingZ, out _);

        public static StagePoint AlongPlan(IReadOnlyList<StagePoint> path, float planDistance, out float headingX,
            out float headingZ, out float slopeFactor)
        {
            headingX = 0f;
            headingZ = 1f;
            slopeFactor = 1f;
            if (path.Count == 0)
            {
                return default;
            }

            float left = Math.Max(0f, planDistance);
            for (int i = 1; i < path.Count; i++)
            {
                var a = path[i - 1];
                var b = path[i];
                float d = PlanDistance(a, b);
                if (d > 1e-4f)
                {
                    headingX = (b.X - a.X) / d;
                    headingZ = (b.Z - a.Z) / d;
                    slopeFactor = Distance(a, b) / d;
                }

                if (left <= d || i == path.Count - 1)
                {
                    float t = d > 1e-4f ? Math.Min(1f, left / d) : 1f;
                    return new StagePoint(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
                }

                left -= d;
            }

            return path[path.Count - 1];
        }

        private static float PlanDistance(StagePoint a, StagePoint b)
        {
            float dx = b.X - a.X, dz = b.Z - a.Z;
            return (float)Math.Sqrt(dx * dx + dz * dz);
        }

        public static float PathLength(IReadOnlyList<StagePoint> path)
        {
            float total = 0f;
            for (int i = 1; i < path.Count; i++)
            {
                total += Distance(path[i - 1], path[i]);
            }

            return total;
        }

        /// <summary>Point <paramref name="distance"/> metres along <paramref name="path"/> (clamped), and its heading.</summary>
        public static StagePoint Along(IReadOnlyList<StagePoint> path, float distance, out float headingX, out float headingZ)
        {
            headingX = 0f;
            headingZ = 1f;
            if (path.Count == 0)
            {
                return default;
            }

            float left = Math.Max(0f, distance);
            for (int i = 1; i < path.Count; i++)
            {
                var a = path[i - 1];
                var b = path[i];
                float d = Distance(a, b);
                float hx = b.X - a.X, hz = b.Z - a.Z;
                float hl = (float)Math.Sqrt(hx * hx + hz * hz);
                if (hl > 1e-4f)
                {
                    headingX = hx / hl;
                    headingZ = hz / hl;
                }

                if (left <= d || i == path.Count - 1)
                {
                    float t = d > 1e-4f ? Math.Min(1f, left / d) : 1f;
                    return new StagePoint(a.X + (b.X - a.X) * t, a.Y + (b.Y - a.Y) * t, a.Z + (b.Z - a.Z) * t);
                }

                left -= d;
            }

            return path[path.Count - 1];
        }

        private static float Distance(StagePoint a, StagePoint b)
        {
            float dx = b.X - a.X, dy = b.Y - a.Y, dz = b.Z - a.Z;
            return (float)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        }
    }

    /// <summary>
    /// The workshop mechanism's motion through one knock cycle (spec §2, §6): the shaft turns the three-lobed cam
    /// 120 degrees per knock; each lobe lifts the follower and drops it on the knock ("short, short, pause, last",
    /// <see cref="KnockTimes"/>); when the arm is linked the owl bows on every knock. One function of time so the
    /// cam, follower, crank and owl are in sync frame for frame. Pure.
    /// </summary>
    public static class MechanismMotion
    {
        /// <summary>Knock contacts in seconds from the start of a cycle (the arrival cue's rhythm, stretched slightly).</summary>
        public static readonly float[] KnockTimes = { 0.45f, 0.85f, 1.9f };

        /// <summary>The follower lifts this long before each knock, then drops on it.</summary>
        public const float LiftLead = 0.28f;

        public const float CycleSeconds = 2.6f;
        public const float FollowerLiftDegrees = 16f;
        public const float OwlBowDegrees = 28f;
        public const float BowSeconds = 0.42f;
        public const float CamDegreesPerKnock = 120f;

        /// <summary>The crank's single tap when the arm is not linked: one knock, no bow.</summary>
        public static readonly float[] GapKnockTimes = { 0.45f };

        public const float GapCycleSeconds = 1.1f;

        /// <summary>Cam angle (degrees) after <paramref name="t"/> seconds of a cycle of <paramref name="knocks"/>.</summary>
        public static float CamAngle(float t, float[] knocks)
        {
            float angle = 0f;
            for (int i = 0; i < knocks.Length; i++)
            {
                float start = i == 0 ? 0f : knocks[i - 1];
                float end = knocks[i];
                if (t >= end)
                {
                    angle += CamDegreesPerKnock;
                }
                else if (t > start)
                {
                    angle += CamDegreesPerKnock * Smooth((t - start) / Math.Max(1e-3f, end - start));
                    break;
                }
                else
                {
                    break;
                }
            }

            return angle;
        }

        /// <summary>Follower lift (0..1): rises over the lobe, snaps down on the knock.</summary>
        public static float FollowerLift(float t, float[] knocks)
        {
            float lift = 0f;
            foreach (float knock in knocks)
            {
                float since = t - (knock - LiftLead);
                if (since >= 0f && t < knock)
                {
                    lift = Math.Max(lift, Smooth(since / LiftLead));
                }
                else if (t >= knock && t < knock + 0.06f)
                {
                    lift = Math.Max(lift, 1f - (t - knock) / 0.06f);
                }
            }

            return lift;
        }

        /// <summary>Owl bow (0..1): dips on each knock and comes back up within <see cref="BowSeconds"/>.</summary>
        public static float OwlBow(float t, float[] knocks)
        {
            float bow = 0f;
            foreach (float knock in knocks)
            {
                float since = t - knock;
                if (since >= -0.05f && since < BowSeconds)
                {
                    float u = Math.Max(0f, since + 0.05f) / (BowSeconds + 0.05f);
                    bow = Math.Max(bow, (float)Math.Sin(u * Math.PI));
                }
            }

            return bow;
        }

        /// <summary>Number of knocks that have sounded by <paramref name="t"/>.</summary>
        public static int KnocksBy(float t, float[] knocks)
        {
            int count = 0;
            foreach (float knock in knocks)
            {
                if (t >= knock)
                {
                    count++;
                }
            }

            return count;
        }

        private static float Smooth(float u)
        {
            u = Math.Max(0f, Math.Min(1f, u));
            return u * u * (3f - 2f * u);
        }
    }
}
