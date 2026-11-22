using System;
using System.Collections.Generic;
using SpookyStreet.Core.World;
using Lines = SpookyStreet.Core.Localization.LocalizationKeys.CaseLines;
using UiKeys = SpookyStreet.Core.Localization.LocalizationKeys.CaseUi;

namespace SpookyStreet.Core.Case
{
    /// <summary>A point on the stage in world metres (Unity axes: X east, Y up, Z north).</summary>
    public readonly struct StagePoint
    {
        public StagePoint(float x, float y, float z)
        {
            X = x;
            Y = y;
            Z = z;
        }

        public float X { get; }
        public float Y { get; }
        public float Z { get; }

        public override string ToString() => $"({X:F2}, {Y:F2}, {Z:F2})";
    }

    /// <summary>Which side of the owl house shell a Use target can be used from.</summary>
    public enum PointSide
    {
        Any,
        Outside,
        Inside,
    }

    /// <summary>One authored Use target of the case: what it reports, where it is and how close Pip must be.</summary>
    public sealed class InteractionPoint
    {
        internal InteractionPoint(string id, CaseAction action, StagePoint position, float reach, float markerHeight,
            PointSide side = PointSide.Any, string verbKey = null)
        {
            Id = id;
            Action = action;
            Position = position;
            Reach = reach;
            MarkerHeight = markerHeight;
            Side = side;
            _verbKey = verbKey;
        }

        private readonly string _verbKey;

        /// <summary>Offered only from this side of the house shell (the latch from inside, the door from outside).</summary>
        public PointSide Side { get; }

        /// <summary>Stable name (logs and tests only; never saved).</summary>
        public string Id { get; }

        public CaseAction Action { get; }

        /// <summary>Ground point of the target (horizontal distance is measured from here).</summary>
        public StagePoint Position { get; }

        /// <summary>Horizontal distance (m) from Pip's centre within which the target is offered.</summary>
        public float Reach { get; }

        /// <summary>Height above <see cref="Position"/> of the floating interaction marker.</summary>
        public float MarkerHeight { get; }

        /// <summary>Localization key of the verb on the Use disc.</summary>
        public string VerbKey => _verbKey ?? CaseInteractions.VerbKey(Action.Kind);
    }

    /// <summary>
    /// Where the exterior half of case 01 happens (S002 wiring): the Use targets, the safe anchors Pip can be restored
    /// to and the trigger zones. Positions come from <see cref="EnvironmentLayout"/>, so the stage follows the street.
    /// Interior targets (attic, foyer, workshop) are added when their rooms exist.
    /// </summary>
    public static partial class CaseStage
    {
        public const string PointBea = "bea";
        public const string PointVale = "vale";
        public const string PointBench = "bench";
        public const string PointBicycle = "bicycle";
        public const string PointFrontDoor = "front_door";
        public const string PointCandyBowl = "candy_bowl";

        /// <summary>Spec §5: choose a Use target within about 1.6 m (larger for targets behind a collider).</summary>
        public const float DefaultReach = 1.6f;

        /// <summary>A target is ignored when Pip stands this much higher or lower than it.</summary>
        public const float MaxHeightDifference = 1.6f;

        /// <summary>How strongly the camera's centre wins between two targets in range (0 = distance only).</summary>
        public const float CameraWeight = 0.35f;

        /// <summary>The arrival cue plays after this much walking ("during the first few steps", spec §3).</summary>
        public const float FirstStepsDistance = 0.8f;

        /// <summary>Walking past this line north of the arrival porch plays the cue too, however Pip got there.</summary>
        public const float ArrivalCueLineZ = -11.2f;

        /// <summary>Arrival porch zone for <see cref="CaseActionKind.ArriveAtPorch"/>.</summary>
        public const float PorchHalfWidth = 2.2f;
        public const float PorchNorthEdgeZ = -12.2f;

        /// <summary>Bench seat point on the other end from Mr. Vale, in the bench's own frame.</summary>
        public const float BenchSeatLocalX = -0.45f;

        /// <summary>Eye height of a child seated on the bench (seat 0.47 m + 0.6 m).</summary>
        public const float BenchEyeHeight = 1.07f;

        /// <summary>The front door's centre on the porch side (house front wall, porch deck height).</summary>
        public static readonly StagePoint FrontDoor =
            new StagePoint(EnvironmentLayout.HouseX, 0.6f, EnvironmentLayout.HouseZ - 2f);

        public static readonly IReadOnlyList<InteractionPoint> ExteriorPoints = BuildPoints();

        private static readonly Dictionary<string, StagePoint> Anchors = new Dictionary<string, StagePoint>(StringComparer.Ordinal)
        {
            { CaseAnchors.ArrivalPorch, new StagePoint(0f, 0.62f, -13.4f) },
            { CaseAnchors.Plaza, new StagePoint(0f, 0.1f, -0.5f) },
            { CaseAnchors.BicycleCorner, new StagePoint(-5.6f, 0.1f, -0.4f) },
            { CaseAnchors.Bench, new StagePoint(6.4f, 0.1f, 1.4f) },
            { CaseAnchors.BluePorch, new StagePoint(0f, 0.7f, 10f) },
            // Interior pass: the treehouse route and, in the separate interior (house v3, D24.1), the three rooms.
            { CaseAnchors.SideYard, new StagePoint(-11.2f, 0.1f, 5.6f) },
            { CaseAnchors.Treehouse, new StagePoint(-6.6f, 2.45f, 13.4f) },
            { CaseAnchors.AtticSill, new StagePoint(-4.1f, 2.5f, CaseRouteLayout.BridgeZ) },
            { CaseAnchors.Attic, new StagePoint(-1.6f, 4.05f, CaseRouteLayout.InteriorFrontZ + 1.5f) },
            { CaseAnchors.Foyer, new StagePoint(-0.8f, 0.65f, CaseRouteLayout.InteriorFrontZ + 1.9f) },
            { CaseAnchors.Workshop, new StagePoint(1.45f, 0.65f, CaseRouteLayout.InteriorFrontZ + 4.6f) },
        };

        /// <summary>Anchors that exist on the stage (every anchor of the case since the interior pass).</summary>
        public static IEnumerable<string> StagedAnchors => Anchors.Keys;

        public static bool TryGetAnchor(string anchorId, out StagePoint point)
        {
            if (anchorId != null && Anchors.TryGetValue(anchorId, out point))
            {
                return true;
            }

            point = default;
            return false;
        }

        /// <summary>
        /// Where to place Pip for a saved anchor: the anchor after <see cref="CaseAnchors.Resolve"/>, or the plaza when
        /// that anchor has no place on the stage yet (an interior room that is not built).
        /// </summary>
        public static StagePoint RestorePoint(string anchorId, CaseState state, out string resolvedId)
        {
            resolvedId = CaseAnchors.Resolve(anchorId, state);
            if (TryGetAnchor(resolvedId, out var point))
            {
                return point;
            }

            resolvedId = CaseAnchors.Fallback;
            return Anchors[CaseAnchors.Fallback];
        }

        /// <summary>The staged anchor nearest to (x, z) at ground level: see <see cref="NearestAnchor(float, float, float)"/>.</summary>
        public static string NearestAnchor(float x, float z) => NearestAnchor(x, 0f, z);

        /// <summary>
        /// The staged anchor nearest to (x, y, z): what an autosave records as the last safe anchor. Height counts
        /// three times as much as plan distance, so the attic and the hall (one above the other) never mix, and an
        /// inside anchor is only chosen for a point in the interior (and an outside one for a point on the street),
        /// so a save on the porch never restores Pip behind the latched door and an interior save restores inside.
        /// </summary>
        public static string NearestAnchor(float x, float y, float z)
        {
            string best = CaseAnchors.Fallback;
            float bestDistance = float.MaxValue;
            bool inside = CaseRouteLayout.Inside(x, z);
            foreach (var pair in Anchors)
            {
                if (CaseAnchors.IsInside(pair.Key) != inside)
                {
                    continue;
                }

                float dx = pair.Value.X - x;
                float dy = (pair.Value.Y - y) * 3f;
                float dz = pair.Value.Z - z;
                float d = dx * dx + dy * dy + dz * dz;
                if (d < bestDistance)
                {
                    bestDistance = d;
                    best = pair.Key;
                }
            }

            return best;
        }

        /// <summary>True once the arrival cue should play (it plays once; the reducer ignores repeats).</summary>
        public static bool ShouldHearKnocks(CaseState state, float walkedSincePlay, float pipZ) =>
            !state.Has(CaseFact.HeardKnocks) && (walkedSincePlay >= FirstStepsDistance || pipZ >= ArrivalCueLineZ);

        /// <summary>Pip stands on the arrival porch (the finale's sightline).</summary>
        public static bool OnArrivalPorch(float x, float z) => Math.Abs(x) <= PorchHalfWidth && z <= PorchNorthEdgeZ;

        public static InteractionPoint Find(string id)
        {
            foreach (var point in AllPoints)
            {
                if (point.Id == id)
                {
                    return point;
                }
            }

            return null;
        }

        private static InteractionPoint[] BuildPoints()
        {
            Placement bea = default, vale = default, bike = default, bowl = default;
            foreach (var placement in EnvironmentLayout.Witnesses)
            {
                switch (placement.Model)
                {
                    case EnvironmentLayout.Bea:
                        bea = placement;
                        break;
                    case EnvironmentLayout.Vale:
                        vale = placement;
                        break;
                    case EnvironmentLayout.BeaBicycle:
                        bike = placement;
                        break;
                }
            }

            foreach (var placement in EnvironmentLayout.Street)
            {
                if (placement.Model == EnvironmentLayout.CandyBowl)
                {
                    bowl = placement;
                }
            }

            EnvironmentLayout.LocalToWorld(EnvironmentLayout.BenchX, EnvironmentLayout.BenchZ, EnvironmentLayout.BenchYaw,
                BenchSeatLocalX, 0f, out float seatX, out float seatZ);
            return new[]
            {
                new InteractionPoint(PointBea, CaseAction.Talk(Speaker.Bea), new StagePoint(bea.X, 0f, bea.Z), 1.8f, 1.75f),
                new InteractionPoint(PointVale, CaseAction.Talk(Speaker.Vale), new StagePoint(vale.X, 0f, vale.Z), 1.8f, 1.55f),
                new InteractionPoint(PointBench, CaseAction.Of(CaseActionKind.SitOnBench), new StagePoint(seatX, 0f, seatZ),
                    1.8f, 0.95f),
                new InteractionPoint(PointBicycle, CaseAction.Of(CaseActionKind.ExamineStreamer),
                    new StagePoint(bike.X, 0f, bike.Z), 1.8f, 1.35f),
                new InteractionPoint(PointFrontDoor, CaseAction.Of(CaseActionKind.TryFrontDoor), FrontDoor, DefaultReach,
                    1.9f, PointSide.Outside),
                // The candy bowl sits on the arrival porch crate; a short reach keeps it off the Use disc at the spawn.
                new InteractionPoint(PointCandyBowl, CaseAction.Of(CaseActionKind.ExamineCandyBowl),
                    new StagePoint(bowl.X, bowl.Y, bowl.Z), 1.15f, 0.45f),
            };
        }
    }

    /// <summary>
    /// Picks the Use target (spec §5): within reach, at a similar height, weighted toward the camera's centre. Pure;
    /// the world adds line of sight and modal state.
    /// </summary>
    public static class UseTargetPicker
    {
        /// <summary>Index of the best target in <paramref name="points"/>, or -1 when none is in range.</summary>
        public static int Pick(float pipX, float pipY, float pipZ, float cameraForwardX, float cameraForwardZ,
            IReadOnlyList<InteractionPoint> points)
        {
            if (points == null)
            {
                return -1;
            }

            float fl = (float)Math.Sqrt(cameraForwardX * cameraForwardX + cameraForwardZ * cameraForwardZ);
            float fx = fl > 1e-4f ? cameraForwardX / fl : 0f;
            float fz = fl > 1e-4f ? cameraForwardZ / fl : 0f;
            int best = -1;
            float bestScore = float.MaxValue;
            for (int i = 0; i < points.Count; i++)
            {
                var point = points[i];
                if (point == null)
                {
                    continue;
                }

                float dx = point.Position.X - pipX;
                float dz = point.Position.Z - pipZ;
                float d = (float)Math.Sqrt(dx * dx + dz * dz);
                if (d > point.Reach || Math.Abs(point.Position.Y - pipY) > CaseStage.MaxHeightDifference)
                {
                    continue;
                }

                float facing = d > 1e-4f ? (dx * fx + dz * fz) / d : 1f;
                float score = d / point.Reach - CaseStage.CameraWeight * facing;
                if (score < bestScore)
                {
                    bestScore = score;
                    best = i;
                }
            }

            return best;
        }
    }

    /// <summary>Where a sound comes from, relative to the camera (caption direction words, spec §6 A11Y).</summary>
    public enum CaptionBearing
    {
        Ahead,
        Right,
        Behind,
        Left,
    }

    /// <summary>
    /// Direction cues for captions. Only a sound heard from somewhere (the arrival knocks from the attic, the hall
    /// floor's hum from the workshop) gets the arrow and the direction word; captions for what Pip sees or does (the
    /// grille, the arm clicking in, the crank's gap, the owl bowing in the cutaway) never do. A source within
    /// <see cref="MinSourceDistance"/> of Pip (horizontally) is "here", not a direction, so it gets no cue either.
    /// </summary>
    public static class CaptionDirection
    {
        /// <summary>Closer than this to Pip, a sound's source needs no direction cue.</summary>
        public const float MinSourceDistance = 1.5f;

        private static readonly HashSet<string> DirectionalSounds = new HashSet<string>(StringComparer.Ordinal)
        {
            Lines.CaptionArrivalKnocks,
            Lines.CaptionFoyerHum,
        };

        /// <summary>Caption keys that describe a sound coming from a place (and so may carry a direction cue).</summary>
        public static IReadOnlyCollection<string> DirectionalSoundKeys => DirectionalSounds;

        public static bool IsDirectionalSound(string key) => key != null && DirectionalSounds.Contains(key);

        /// <summary>
        /// Whether the caption <paramref name="key"/> shows a direction cue: a directional sound whose source lies at
        /// least <see cref="MinSourceDistance"/> from the listener (horizontal metres between them).
        /// </summary>
        public static bool ShouldCue(string key, bool hasSource, float sourceDistance) =>
            hasSource && IsDirectionalSound(key) && sourceDistance >= MinSourceDistance;

        /// <summary>
        /// Signed angle in degrees from the camera's forward to the source, both projected on the ground; positive
        /// means to the right (clockwise seen from above). 0 when either vector is degenerate.
        /// </summary>
        public static float SignedAngle(float forwardX, float forwardZ, float toSourceX, float toSourceZ)
        {
            float cross = forwardZ * toSourceX - forwardX * toSourceZ;
            float dot = forwardX * toSourceX + forwardZ * toSourceZ;
            if (Math.Abs(cross) < 1e-6f && Math.Abs(dot) < 1e-6f)
            {
                return 0f;
            }

            return (float)(Math.Atan2(cross, dot) * 180.0 / Math.PI);
        }

        public static CaptionBearing BearingOf(float signedAngle)
        {
            float a = Math.Abs(signedAngle);
            if (a <= 45f)
            {
                return CaptionBearing.Ahead;
            }

            if (a >= 135f)
            {
                return CaptionBearing.Behind;
            }

            return signedAngle > 0f ? CaptionBearing.Right : CaptionBearing.Left;
        }

        public static string WordKey(CaptionBearing bearing)
        {
            switch (bearing)
            {
                case CaptionBearing.Right:
                    return UiKeys.DirectionRight;
                case CaptionBearing.Behind:
                    return UiKeys.DirectionBehind;
                case CaptionBearing.Left:
                    return UiKeys.DirectionLeft;
                default:
                    return UiKeys.DirectionAhead;
            }
        }
    }

    /// <summary>Speaker presentation: name tab key and the initial drawn while no portrait art exists.</summary>
    public static class CaseSpeakers
    {
        public static string NameKey(Speaker speaker)
        {
            switch (speaker)
            {
                case Speaker.Pip:
                    return UiKeys.SpeakerPip;
                case Speaker.Bea:
                    return UiKeys.SpeakerBea;
                case Speaker.Vale:
                    return UiKeys.SpeakerVale;
                case Speaker.June:
                    return UiKeys.SpeakerJune;
                case Speaker.Kit:
                    return UiKeys.SpeakerKit;
                default:
                    return null;
            }
        }

        /// <summary>Captions go to the caption strip; everyone else speaks on the dialogue card.</summary>
        public static bool IsCaption(Speaker speaker) => speaker == Speaker.Caption;
    }

    /// <summary>Short labels for the notebook's annotation slips (the full note shows on tap).</summary>
    public static class CaseNotebookTags
    {
        private static readonly Dictionary<string, string> Tags = new Dictionary<string, string>(StringComparer.Ordinal)
        {
            { "c01.note.knocks", UiKeys.TagKnocks },
            { "c01.note.bea", UiKeys.TagBea },
            { "c01.note.vale", UiKeys.TagVale },
            { "c01.note.bench", UiKeys.TagBench },
            { "c01.note.streamer", UiKeys.TagStreamer },
            { "c01.note.cloth", UiKeys.TagCloth },
            { "c01.note.sill", UiKeys.TagSill },
            { "c01.note.june", UiKeys.TagJune },
            { "c01.note.sweet", UiKeys.TagSweet },
        };

        /// <summary>Tag label key for an annotation entry, or null for an answer.</summary>
        public static string TagKey(string entryId) =>
            entryId != null && Tags.TryGetValue(entryId, out string key) ? key : null;
    }

    /// <summary>
    /// When a dialogue line may advance (spec §5: holding Use never repeatedly advances dialogue). A line shows for
    /// at least <see cref="MinShowSeconds"/>; a key or button press advances only if that button was seen released
    /// since the line appeared, so the press that opened the conversation, or a held button, never skips a line. A
    /// tap on the card needs no release (each tap is its own press). Pure; feed it unpaused time only.
    /// </summary>
    public sealed class DialogueAdvanceGate
    {
        public const float MinShowSeconds = 0.25f;

        private bool _armed;
        private float _shown;

        /// <summary>Seconds the current line has been shown (unpaused).</summary>
        public float Shown => _shown;

        /// <summary>A new line appeared; <paramref name="held"/> is whether the advance button is down now.</summary>
        public void Begin(bool held)
        {
            _armed = !held;
            _shown = 0f;
        }

        /// <summary>One frame. True when the line should advance (the caller then calls <see cref="Begin"/>).</summary>
        public bool Step(float dt, bool held, bool pressedThisFrame, bool tapped)
        {
            if (dt > 0f)
            {
                _shown += dt;
            }

            bool press = pressedThisFrame && _armed;
            if (!held)
            {
                _armed = true;
            }

            if (_shown < MinShowSeconds)
            {
                return false;
            }

            return tapped || press;
        }
    }
}
