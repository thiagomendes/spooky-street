using System;
using System.Collections.Generic;
using SpookyStreet.Core.Audio;
using SpookyStreet.Core.World;

namespace SpookyStreet.Core.Case
{
    /// <summary>The small idle motion a clue makes to call for curiosity (D26 §2.3, docs/mystery-first.md §3).</summary>
    public enum AttractionKind
    {
        /// <summary>A brief catch of light on the thing itself, twice (never a ring, never an arrow).</summary>
        Glint,

        /// <summary>Cloth lifting and settling in a passing gust.</summary>
        Flutter,

        /// <summary>Dust motes hopping off the floorboards on each knock.</summary>
        DustShimmer,

        /// <summary>A soft rattle on each knock (the panel, the grille's slats of light).</summary>
        Rattle,

        /// <summary>A loose part swinging after each knock.</summary>
        Wobble,

        /// <summary>The rooftop wind wheel spinning up as a wind gust starts a knock cycle.</summary>
        Whir,

        /// <summary>Foil catching the lamp (the decoy gets the same treatment as every clue: D26 §2.7).</summary>
        Twinkle,
    }

    /// <summary>What sets an attraction's rhythm.</summary>
    public enum AttractionTrigger
    {
        /// <summary>An idle loop of <see cref="ClueAttraction.Period"/> seconds (gusts, glints).</summary>
        Idle,

        /// <summary>Each knock of the workshop mechanism (wind cycles and the crank), with the cam-tap sound.</summary>
        Knock,

        /// <summary>The start of each wind-driven knock cycle, with the wind-wheel sound.</summary>
        WindCycle,
    }

    /// <summary>
    /// One clue's diegetic attraction (S002.5b, D26): where it plays, how close Pip must be, what it looks like, what
    /// sets its rhythm and which clue sound it pairs with. It plays only while the clue's sticker is still out in the
    /// world; once found it stops for good.
    /// </summary>
    public sealed class ClueAttraction
    {
        internal ClueAttraction(string id, string stickerId, CaseFact foundWhen, AttractionKind[] kinds,
            AttractionTrigger trigger, StagePoint at, float range, float maxRise, string sound,
            CaseFact? needs = null, float period = 0f, CaseFact? lostWhen = null)
        {
            Id = id;
            StickerId = stickerId;
            FoundWhen = foundWhen;
            Kinds = kinds;
            Trigger = trigger;
            At = at;
            Range = range;
            MaxRise = maxRise;
            Sound = sound;
            Needs = needs;
            LostWhen = lostWhen;
            Period = period;
        }

        /// <summary>Stable name (logs and tests only; never saved).</summary>
        public string Id { get; }

        /// <summary>The sticker the attraction calls for (null for the wind wheel, which has no sticker yet).</summary>
        public string StickerId { get; }

        /// <summary>Once this fact is held the attraction never plays again.</summary>
        public CaseFact FoundWhen { get; }

        public IReadOnlyList<AttractionKind> Kinds { get; }

        public AttractionTrigger Trigger { get; }

        /// <summary>Where the thing is (the glint's point; motions act on the model there).</summary>
        public StagePoint At { get; }

        /// <summary>Plan distance (m) from Pip within which it plays: close enough to be seen at phone size.</summary>
        public float Range { get; }

        /// <summary>It plays only while Pip's feet are within this height of <see cref="At"/> (other floors cannot see it).</summary>
        public float MaxRise { get; }

        /// <summary>The <c>AudioEvents</c> id it pairs with (already played by GameAudio at that moment), or null.</summary>
        public string Sound { get; }

        /// <summary>A fact the thing needs to be visible (the loose arm behind the opened panel), or null.</summary>
        public CaseFact? Needs { get; }

        /// <summary>A fact after which the thing can no longer be examined (the owl panel once it swings open), or null.</summary>
        public CaseFact? LostWhen { get; }

        /// <summary>Idle loop length (s) for <see cref="AttractionTrigger.Idle"/>.</summary>
        public float Period { get; }

        public bool Has(AttractionKind kind)
        {
            foreach (var k in Kinds)
            {
                if (k == kind)
                {
                    return true;
                }
            }

            return false;
        }

        /// <summary>Not found yet and visible now (the state half of <see cref="ClueAttractions.ShouldPlay"/>).</summary>
        public bool Waiting(CaseState state) =>
            state != null && !state.Has(FoundWhen) && (!Needs.HasValue || state.Has(Needs.Value)) &&
            !(LostWhen.HasValue && state.Has(LostWhen.Value));

        /// <summary>Pip is near enough (plan range, same floor) to see it.</summary>
        public bool InRange(float x, float y, float z)
        {
            float dx = x - At.X;
            float dz = z - At.Z;
            return dx * dx + dz * dz <= Range * Range && Math.Abs(y - At.Y) <= MaxRise &&
                   CaseRouteLayout.Inside(x, z) == CaseRouteLayout.Inside(At.X, At.Z);
        }
    }

    /// <summary>
    /// Case 01's diegetic clue attractions (S002.5b, D26 §2.3, docs/mystery-first.md §3): every physical clue calls
    /// for curiosity with a small motion of its own when Pip is near and it is not found yet, with no UI arrow and no
    /// "this matters" ring. Motion is game time (pause freezes it) and shrinks under reduced motion. Pure.
    /// </summary>
    public static class ClueAttractions
    {
        public const string Cloth = "cloth";
        public const string Sill = "sill";
        public const string FloorHum = "floor_hum";
        public const string Grille = "grille";
        public const string OwlPanel = "owl_panel";
        public const string LooseArm = "loose_arm";
        public const string WindWheel = "wind_wheel";
        public const string WoodenOwl = "wooden_owl";
        public const string Streamer = "streamer";

        /// <summary>Reduced motion keeps the light (glints) and scales every movement down to this share.</summary>
        public const float ReducedMotionScale = 0.3f;

        /// <summary>A glint flashes twice: on at 0 and at <see cref="GlintGap"/>, each <see cref="GlintSeconds"/> long.</summary>
        public const float GlintSeconds = 0.32f;

        public const float GlintGap = 0.5f;

        /// <summary>A knock's rattle or wobble rings out over this long.</summary>
        public const float KnockRingSeconds = 0.7f;

        /// <summary>The wind wheel's spin-up after a wind gust starts a cycle (the knocks follow at 0.45 s).</summary>
        public const float WhirSeconds = 1.6f;

        private const float Ifz = CaseRouteLayout.InteriorFrontZ;

        public static readonly IReadOnlyList<ClueAttraction> All = Build();

        private static ClueAttraction[] Build()
        {
            var bike = default(Placement);
            foreach (var placement in EnvironmentLayout.Witnesses)
            {
                if (placement.Model == EnvironmentLayout.BeaBicycle)
                {
                    bike = placement;
                }
            }

            float work = CaseRouteLayout.NookX - 0.05f;
            float floor = CaseRouteLayout.FloorY;
            return new[]
            {
                // The silver scrap on the bridge-head post flutters in the gusts and glints twice: seen from the ramp's
                // upper leg, the shed roof and the treehouse deck (mf_clue_glint).
                new ClueAttraction(Cloth, CaseNotebook.Cloth, CaseFact.ExaminedCloth,
                    new[] { AttractionKind.Flutter, AttractionKind.Glint }, AttractionTrigger.Idle,
                    new StagePoint(CaseRouteLayout.ClothX, CaseRouteLayout.ClothY, CaseRouteLayout.ClothZ), 9f, 3.6f,
                    null, period: 4.5f),
                // The thread on the attic sill lifts in the draught and glints, seen from the alcove and the loft.
                new ClueAttraction(Sill, CaseNotebook.SillSnag, CaseFact.ExaminedSill,
                    new[] { AttractionKind.Flutter, AttractionKind.Glint }, AttractionTrigger.Idle,
                    new StagePoint(-3.57f, CaseRouteLayout.SillY + 0.42f, Ifz + 1.97f), 5f, 1.6f, null, period: 5f),
                // The hall's boards tremble with the knocks and dust hops off them toward the grille: seen coming down
                // the stair (the hum itself is found on reaching the hall floor).
                new ClueAttraction(FloorHum, CaseNotebook.FloorHum, CaseFact.FloorHum,
                    new[] { AttractionKind.DustShimmer }, AttractionTrigger.Knock,
                    new StagePoint(0.4f, floor, CaseRouteLayout.MechanismZ), 6.5f, 2.6f, AudioEvents.ClueCamTap),
                // The low grille's three slats of light flicker and rattle softly with each knock.
                new ClueAttraction(Grille, CaseNotebook.WoodenPiece, CaseFact.SawCam,
                    new[] { AttractionKind.Rattle }, AttractionTrigger.Knock,
                    new StagePoint(work, floor + 0.45f, CaseRouteLayout.MechanismZ + 0.65f), 6f, 1.6f, AudioEvents.ClueCamTap),
                // The painted owl panel rattles in its frame with each knock, and its eyes catch the nook lamp (until
                // it swings open: looking through the grille opens it too).
                new ClueAttraction(OwlPanel, CaseNotebook.OwlPanel, CaseFact.OwlPanel,
                    new[] { AttractionKind.Rattle, AttractionKind.Glint }, AttractionTrigger.Knock,
                    new StagePoint(work, floor + 1.05f, CaseRouteLayout.MechanismZ - 0.2f), 6f, 1.6f, AudioEvents.ClueCamTap,
                    lostWhen: CaseFact.SawCam),
                // Behind the opened panel, the loose striped arm swings when the floor hums.
                new ClueAttraction(LooseArm, CaseNotebook.LooseArm, CaseFact.LooseArm,
                    new[] { AttractionKind.Wobble }, AttractionTrigger.Knock,
                    new StagePoint(work, floor + 0.95f, CaseRouteLayout.MechanismZ - 0.55f), 6f, 1.6f, AudioEvents.ClueCamTap,
                    needs: CaseFact.SawCam),
                // The rooftop wind wheel whirs up as each gust starts a knock cycle (no sticker yet: it stops calling
                // once the knocks' question is solved; the wheel itself keeps turning).
                new ClueAttraction(WindWheel, null, CaseFact.SolvedSource,
                    new[] { AttractionKind.Whir }, AttractionTrigger.WindCycle,
                    new StagePoint(EnvironmentLayout.HouseX, 8.83f, EnvironmentLayout.HouseZ - 1.5f), 26f, 12f, AudioEvents.ClueWindWheel),
                // Aunt Kit's wooden owl high on the house front: its eyes catch the porch lamp; the owl stays still.
                new ClueAttraction(WoodenOwl, CaseNotebook.OwlBooth, CaseFact.OwlBooth,
                    new[] { AttractionKind.Glint }, AttractionTrigger.Idle,
                    CaseStage.WoodenOwl, 10f, 7.5f, null, period: 6f),
                // The decoy: the lavender foil streamer twinkles in the lamp like every other clue (no sorting signal).
                new ClueAttraction(Streamer, CaseNotebook.Streamer, CaseFact.ExaminedStreamer,
                    new[] { AttractionKind.Twinkle }, AttractionTrigger.Idle,
                    new StagePoint(bike.X, 0.95f, bike.Z), 7f, 2.5f, null, period: 5f),
            };
        }

        public static ClueAttraction Get(string id)
        {
            foreach (var attraction in All)
            {
                if (attraction.Id == id)
                {
                    return attraction;
                }
            }

            return null;
        }

        /// <summary>The attraction for a sticker, or null.</summary>
        public static ClueAttraction ForSticker(string stickerId)
        {
            foreach (var attraction in All)
            {
                if (attraction.StickerId == stickerId)
                {
                    return attraction;
                }
            }

            return null;
        }

        /// <summary>True when <paramref name="attraction"/> should play for Pip at (x, y, z) in <paramref name="state"/>.</summary>
        public static bool ShouldPlay(ClueAttraction attraction, CaseState state, float x, float y, float z) =>
            attraction != null && attraction.Waiting(state) && attraction.InRange(x, y, z);

        /// <summary>Every attraction that plays now for Pip at (x, y, z).</summary>
        public static List<ClueAttraction> Active(CaseState state, float x, float y, float z)
        {
            var list = new List<ClueAttraction>();
            foreach (var attraction in All)
            {
                if (ShouldPlay(attraction, state, x, y, z))
                {
                    list.Add(attraction);
                }
            }

            return list;
        }

        /// <summary>How much of a motion survives reduced motion (glints are light, not motion, and stay).</summary>
        public static float MotionScale(bool reducedMotion) => reducedMotion ? ReducedMotionScale : 1f;

        /// <summary>
        /// The glint's brightness (0..1) <paramref name="t"/> seconds into its loop: two quick flashes at the start of
        /// each <paramref name="period"/>, then dark.
        /// </summary>
        public static float Glint(float t, float period)
        {
            if (period <= 0f)
            {
                return 0f;
            }

            float u = t % period;
            if (u < 0f)
            {
                u += period;
            }

            return Math.Max(Flash(u), Flash(u - GlintGap));
        }

        /// <summary>A gust (0..1): lifts over the first second of each loop and settles over the next two.</summary>
        public static float Gust(float t, float period)
        {
            if (period <= 0f)
            {
                return 0f;
            }

            float u = t % period;
            if (u < 0f)
            {
                u += period;
            }

            if (u < 1f)
            {
                return Smooth(u);
            }

            return u < 3f ? 1f - Smooth((u - 1f) / 2f) : 0f;
        }

        /// <summary>A knock's ring (0..1) <paramref name="sinceKnock"/> seconds after it: a sharp hit that dies away.</summary>
        public static float KnockRing(float sinceKnock)
        {
            if (sinceKnock < 0f || sinceKnock >= KnockRingSeconds)
            {
                return 0f;
            }

            float u = sinceKnock / KnockRingSeconds;
            return (1f - u) * (1f - u);
        }

        /// <summary>The wheel's extra spin speed (0..1) <paramref name="sinceGust"/> seconds after a wind gust.</summary>
        public static float Whir(float sinceGust)
        {
            if (sinceGust < 0f || sinceGust >= WhirSeconds)
            {
                return 0f;
            }

            float u = sinceGust / WhirSeconds;
            return u < 0.2f ? Smooth(u / 0.2f) : 1f - Smooth((u - 0.2f) / 0.8f);
        }

        private static float Flash(float u)
        {
            if (u < 0f || u >= GlintSeconds)
            {
                return 0f;
            }

            return (float)Math.Sin(u / GlintSeconds * Math.PI);
        }

        private static float Smooth(float u)
        {
            u = Math.Max(0f, Math.Min(1f, u));
            return u * u * (3f - 2f * u);
        }
    }
}
