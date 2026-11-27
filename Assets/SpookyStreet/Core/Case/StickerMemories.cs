using System;
using System.Collections.Generic;
using SpookyStreet.Core.Cutscenes;
using SpookyStreet.Core.Nonverbal;
using SpookyStreet.Core.World;
using Keys = SpookyStreet.Core.Localization.LocalizationKeys.StickerMemory;

namespace SpookyStreet.Core.Case
{
    /// <summary>How the world must look while a memory is drawn, so it shows what the child saw then (D32).</summary>
    [Flags]
    public enum MemoryLook
    {
        None = 0,

        /// <summary>The front door as the child saw it then: shut (it may be open by now).</summary>
        DoorShut = 1,

        /// <summary>The workshop's near wall left out, as when the child looked through the grille.</summary>
        NookOpen = 2,
    }

    /// <summary>
    /// One sticker's memory (owner decision D32): a short camera move on the place where the child found the clue, and
    /// one caption that says what it was and where. It recalls, never explains: no caption says what a clue means or
    /// which question it answers (D26), and opening it changes nothing in the case.
    /// </summary>
    public sealed class StickerMemory
    {
        internal StickerMemory(string stickerId, string captionKey, CamPose from, CamPose to, MemoryLook look)
        {
            StickerId = stickerId;
            CaptionKey = captionKey;
            From = from;
            To = to;
            Look = look;
        }

        public string StickerId { get; }

        /// <summary>The one descriptive line under the picture ("The silver cloth, caught on the ramp.").</summary>
        public string CaptionKey { get; }

        /// <summary>Where the shot starts and where it settles (a slow drift in, like the reveal's sticker shots).</summary>
        public CamPose From { get; }

        public CamPose To { get; }

        public MemoryLook Look { get; }

        /// <summary>The shot stands in the owl house interior (D24.1), which the world must draw while it shows.</summary>
        public bool Indoors => CaseRouteLayout.Inside(To.Eye.X, To.Eye.Z);

        /// <summary>The pose at shot progress <paramref name="t"/> (0..1, already eased).</summary>
        public CamPose PoseAt(float t) => CamPose.Lerp(From, To, Math.Max(0f, Math.Min(1f, t)));
    }

    /// <summary>
    /// Case 01's sticker memories (D32): one per tray sticker, the honest decoy included, so no sticker is singled out
    /// (D26 §2.7). The shots are the reveal's sticker framings (<see cref="C01Cutscenes.StickerShot"/>: where the child
    /// found each clue), plus one for the foil streamer, which the reveal never shows. Pure.
    /// </summary>
    public static class StickerMemories
    {
        /// <summary>The moving shot (D32: a 2–3 second memory).</summary>
        public const float ShotSeconds = 2.4f;

        /// <summary>The settled frame and its caption stay this long after the shot.</summary>
        public const float HoldSeconds = 1.3f;

        /// <summary>Then the card fades away by itself (a tap closes it at any time).</summary>
        public const float FadeSeconds = 0.25f;

        public static float CardSeconds => ShotSeconds + HoldSeconds + FadeSeconds;

        public static readonly IReadOnlyList<StickerMemory> All = Build();

        /// <summary>The memory of a sticker, or null (the saved sweet is a keepsake, never in the tray).</summary>
        public static StickerMemory For(string stickerId)
        {
            foreach (var memory in All)
            {
                if (memory.StickerId == stickerId)
                {
                    return memory;
                }
            }

            return null;
        }

        /// <summary>
        /// What inspecting a sticker shows: its memory once the child has found it, else null. Reads the state and
        /// returns data; nothing is recorded, placed or counted (inspecting is free).
        /// </summary>
        public static StickerMemory Open(string stickerId, CaseState state)
        {
            var sticker = CaseNotebook.Sticker(stickerId);
            return sticker != null && sticker.InTray && sticker.Found(state) ? For(stickerId) : null;
        }

        /// <summary>The foil streamer on Bea's bicycle, from the plaza side at a child's eye height.</summary>
        public static CameraShot StreamerShot()
        {
            var bike = ClueAttractions.Get(ClueAttractions.Streamer).At;
            var aim = new StagePoint(bike.X, 0.85f, bike.Z);
            return new CameraShot(new StagePoint(bike.X + 2.5f, 1.35f, bike.Z - 1.1f), aim, 34f)
                .DriftTo(new StagePoint(bike.X + 2.25f, 1.3f, bike.Z - 0.95f), aim, 31f);
        }

        private static StickerMemory[] Build()
        {
            var list = new List<StickerMemory>();
            foreach (var sticker in CaseNotebook.Stickers)
            {
                if (!sticker.InTray)
                {
                    continue;
                }

                var shot = sticker.Id == CaseNotebook.Streamer
                    ? StreamerShot()
                    : C01Cutscenes.StickerShot(sticker.Id, ShotTransition.Cut);
                if (shot == null)
                {
                    throw new InvalidOperationException("no memory shot for " + sticker.Id);
                }

                list.Add(new StickerMemory(sticker.Id, CaptionFor(sticker.Id), shot.Start, shot.End, LookFor(sticker.Id)));
            }

            return list.ToArray();
        }

        private static MemoryLook LookFor(string stickerId)
        {
            switch (stickerId)
            {
                case CaseNotebook.ValeDoor:
                case CaseNotebook.BenchView:
                case CaseNotebook.StuckDoor:
                    return MemoryLook.DoorShut;
                case CaseNotebook.WoodenPiece:
                case CaseNotebook.OwlPanel:
                case CaseNotebook.LooseArm:
                    return MemoryLook.NookOpen;
                default:
                    return MemoryLook.None;
            }
        }

        private static string CaptionFor(string stickerId)
        {
            switch (stickerId)
            {
                case CaseNotebook.BeaFlash:
                    return Keys.BeaFlash;
                case CaseNotebook.ValeDoor:
                    return Keys.ValeDoor;
                case CaseNotebook.BenchView:
                    return Keys.BenchView;
                case CaseNotebook.StuckDoor:
                    return Keys.StuckDoor;
                case CaseNotebook.Streamer:
                    return Keys.Streamer;
                case CaseNotebook.Cloth:
                    return Keys.Cloth;
                case CaseNotebook.SillSnag:
                    return Keys.Sill;
                case CaseNotebook.FloorHum:
                    return Keys.FloorHum;
                case CaseNotebook.WoodenPiece:
                    return Keys.WoodenPiece;
                case CaseNotebook.OwlPanel:
                    return Keys.OwlPanel;
                case CaseNotebook.OwlBooth:
                    return Keys.OwlBooth;
                case CaseNotebook.LooseArm:
                    return Keys.LooseArm;
                default:
                    throw new InvalidOperationException("no memory caption for " + stickerId);
            }
        }
    }

    /// <summary>
    /// One open memory card: the shot's progress, then the held frame, then a short fade, all on the notebook's clock
    /// (the pause freezes it). Reduced motion shows the settled frame still for the whole time and cuts away at the end.
    /// </summary>
    public sealed class StickerMemoryPlayback
    {
        private readonly bool _reducedMotion;

        public StickerMemoryPlayback(StickerMemory memory, bool reducedMotion)
        {
            Memory = memory ?? throw new ArgumentNullException(nameof(memory));
            _reducedMotion = reducedMotion;
        }

        public StickerMemory Memory { get; }

        public float Elapsed { get; private set; }

        public bool ReducedMotion => _reducedMotion;

        /// <summary>Eased 0..1 through the shot; 1 (the settled frame) from the start under reduced motion.</summary>
        public float Progress => _reducedMotion
            ? 1f
            : VignetteSampler.Apply(Ease.InOut, Math.Min(1f, Elapsed / StickerMemories.ShotSeconds));

        /// <summary>The camera moves this frame (false under reduced motion and once the shot has settled).</summary>
        public bool Moving => !_reducedMotion && Elapsed < StickerMemories.ShotSeconds;

        public CamPose Pose => Memory.PoseAt(Progress);

        /// <summary>The card's opacity: whole, then the closing fade (none under reduced motion).</summary>
        public float Alpha
        {
            get
            {
                float fadeAt = StickerMemories.ShotSeconds + StickerMemories.HoldSeconds;
                if (Elapsed <= fadeAt)
                {
                    return 1f;
                }

                return _reducedMotion ? 0f : Math.Max(0f, 1f - (Elapsed - fadeAt) / StickerMemories.FadeSeconds);
            }
        }

        public bool IsDone => Elapsed >= StickerMemories.CardSeconds;

        public void Advance(float dt)
        {
            if (dt > 0f)
            {
                Elapsed = Math.Min(StickerMemories.CardSeconds, Elapsed + dt);
            }
        }
    }
}
