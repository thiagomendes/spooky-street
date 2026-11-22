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
    /// The interior half of case 01 on the stage (S002 interior wiring): the Use targets in the treehouse route, the
    /// attic, the foyer and the workshop; when each is offered; the trigger zones (window entry, foyer); June's and
    /// Aunt Kit's marks; and the finale sightline. Positions come from <see cref="CaseRouteLayout"/>.
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

        /// <summary>June on her stool by the window, tapping the rhythm on her knee (seat 0.30 m).</summary>
        public static readonly CastSpot JuneAttic = new CastSpot(-1.75f, CaseRouteLayout.LoftY, CaseRouteLayout.FrontZ + 4.05f, -133f);

        /// <summary>June's foyer mark: by the door but 1.5 m clear of the latch, so their markers never overlap.</summary>
        public static readonly CastSpot JuneFoyer = new CastSpot(-1.35f, CaseRouteLayout.FloorY, CaseRouteLayout.FrontZ + 1.2f, 150f);

        /// <summary>On the porch for the reopening, west of the door, facing the street.</summary>
        public static readonly CastSpot JunePorch = new CastSpot(-1.35f, CaseRouteLayout.FloorY, CaseRouteLayout.FrontZ - 0.85f, 175f);

        /// <summary>Aunt Kit at her booth (the ticket door), east of the doorway, facing the street.</summary>
        public static readonly CastSpot KitBooth = new CastSpot(1.15f, CaseRouteLayout.FloorY, CaseRouteLayout.FrontZ - 0.75f, 185f);

        /// <summary>The finale restages the arrival sightline: arrival porch eye height, looking at the booth (spec §4).</summary>
        public static readonly StagePoint FinaleEye = new StagePoint(0.35f, 1.75f, -13.6f);
        public static readonly StagePoint FinaleLookAt = new StagePoint(0f, 2.4f, CaseRouteLayout.FrontZ - 0.6f);

        /// <summary>
        /// June's walk from her stool down the stair to her foyer mark (presentation only; only the mark persists).
        /// Round the stairwell rail, down the stair (it rises east), then across the foyer.
        /// </summary>
        public static readonly StagePoint[] JuneWalk =
        {
            JuneAttic.Position,
            new StagePoint(-0.6f, CaseRouteLayout.LoftY, 15.3f),
            new StagePoint(1.3f, CaseRouteLayout.LoftY, 15.45f),
            new StagePoint(1.3f, CaseRouteLayout.LoftY, 16.36f),
            new StagePoint(0.85f, CaseRouteLayout.LoftY, 16.36f),
            new StagePoint(-2.15f, CaseRouteLayout.FloorY, 16.36f),
            new StagePoint(-2.35f, CaseRouteLayout.FloorY, 15.3f),
            JuneFoyer.Position,
        };

        public const float JuneWalkSpeed = 1.3f;

        public static readonly IReadOnlyList<InteractionPoint> InteriorPoints = BuildInteriorPoints();

        private static IReadOnlyList<InteractionPoint> _allPoints;

        /// <summary>Every Use target of the case, exterior then interior (built on first use: partial-class field
        /// initializers in different files run in no guaranteed order).</summary>
        public static IReadOnlyList<InteractionPoint> AllPoints =>
            _allPoints ?? (_allPoints = Concat(ExteriorPoints, InteriorPoints));

        /// <summary>
        /// Window-entry zone: Pip's feet inside the dormer opening, past the outer wall. Entering it reports
        /// <see cref="CaseActionKind.EnterAttic"/> (the reducer ignores repeats).
        /// </summary>
        public static bool InAtticEntry(float x, float y, float z) =>
            x > CaseRouteLayout.WindowX - 0.05f && x < CaseRouteLayout.InnerMinX + 0.9f &&
            Math.Abs(z - CaseRouteLayout.WindowZ) < CaseRouteLayout.WindowHalfWidth + 0.2f &&
            y > CaseRouteLayout.SillY - 0.3f && y < CaseRouteLayout.SillY + 1.0f;

        /// <summary>Foyer zone: inside the shell at ground-floor height.</summary>
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
            float front = CaseRouteLayout.FrontZ;
            float roof = CaseRouteLayout.TreehouseY;
            float floor = CaseRouteLayout.FloorY;
            float workX = CaseRouteLayout.NookX - 0.05f;
            return new[]
            {
                // The silver scrap on the bridge-head post, at child eye level from the shed roof.
                new InteractionPoint(PointCloth, CaseAction.Of(CaseActionKind.ExamineCloth),
                    new StagePoint(CaseRouteLayout.ClothX - 0.15f, roof, CaseRouteLayout.ClothZ), DefaultReach,
                    CaseRouteLayout.ClothY - roof),
                // The matching snag on the window's north jamb (SillSnag pivot), seen from the bridge or the attic.
                new InteractionPoint(PointSill, CaseAction.Of(CaseActionKind.ExamineSill),
                    new StagePoint(-2.83f, CaseRouteLayout.SillY, front + 3.47f), 1.3f, 0.55f),
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
                    new StagePoint(0.35f, floor, front + 0.3f), DefaultReach, 1.0f, PointSide.Inside),
                // Workshop, through the partition: the low grille, the child-height service panel, then the arm and
                // crank behind it. Each sits at its own part of the partition so the markers stay apart.
                new InteractionPoint(PointGrille, CaseAction.Of(CaseActionKind.ExamineGrille),
                    new StagePoint(workX, floor, CaseRouteLayout.MechanismZ + 0.65f), DefaultReach, 0.45f, PointSide.Inside),
                new InteractionPoint(PointServicePanel, CaseAction.Of(CaseActionKind.ExamineGrille),
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

        /// <summary>Length of a polyline (June's walk).</summary>
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

    /// <summary>What of the owl house to draw this frame.</summary>
    public readonly struct InteriorView
    {
        public InteriorView(bool showInterior, bool hideAttic)
        {
            ShowInterior = showInterior;
            HideAttic = hideAttic;
        }

        /// <summary>Hide OwlHouseBody and DoorRecessCard, keep the porch, show OwlHouseInterior.</summary>
        public bool ShowInterior { get; }

        /// <summary>The attic floor and roof would hide Pip on the ground floor from a high camera.</summary>
        public bool HideAttic { get; }
    }

    /// <summary>
    /// The cut-away rule (spec §5): the interior shows when Pip is inside the shell, or at an open way in (the open
    /// front door, June's window) within <see cref="NearOpening"/>; then the exterior body and the door's dark recess
    /// card hide, so the camera looks in through the culled near walls. On the ground floor with the camera above
    /// the loft, the attic hides too. Pure.
    /// </summary>
    public static class InteriorVisibility
    {
        public const float NearOpening = 1.1f;

        /// <summary>Camera above this height (with Pip downstairs) hides the attic.</summary>
        public const float HighCamera = 2.3f;

        /// <summary>Pip's feet below this are on the ground floor.</summary>
        public const float GroundFloorTop = 1.7f;

        public static InteriorView Decide(float pipX, float pipY, float pipZ, float cameraY, bool doorOpen, bool windowOpen)
        {
            bool inside = CaseRouteLayout.Inside(pipX, pipZ);
            bool atDoor = doorOpen && Math.Abs(pipX - 0.06f) < 0.6f + NearOpening &&
                          pipZ > CaseRouteLayout.FrontZ - NearOpening && pipZ <= CaseRouteLayout.FrontZ + 0.2f &&
                          pipY < CaseRouteLayout.FloorY + 1f;
            float wx = pipX - CaseRouteLayout.WindowX;
            float wz = pipZ - CaseRouteLayout.WindowZ;
            bool atWindow = windowOpen && wx > -NearOpening && wx < 0.3f &&
                            Math.Abs(wz) < CaseRouteLayout.WindowHalfWidth + 0.6f &&
                            pipY > CaseRouteLayout.SillY - 0.5f;
            bool show = inside || atDoor || atWindow;
            bool hideAttic = show && inside && pipY < GroundFloorTop && cameraY > HighCamera;
            return new InteriorView(show, hideAttic);
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
