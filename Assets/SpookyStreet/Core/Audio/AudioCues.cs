using System;

namespace SpookyStreet.Core.Audio
{
    /// <summary>
    /// Fire-and-forget sound hooks for code that should not know about the audio rig (S003): the notebook rebuild on
    /// <c>feature/s002.5a-deduction</c> calls these when a sticker is picked up, pressed into a slot, wobbles back from
    /// a wrong set, or a question glows solved. The game's <c>GameAudio</c> listens to <see cref="Requested"/> and
    /// plays the event through the rig; with nothing listening (EditMode tests, a scene without the rig) the calls do
    /// nothing. Engine-free, so Core code may call it too.
    /// </summary>
    public static class AudioCues
    {
        /// <summary>Raised with the event id of every cue (see <see cref="AudioEvents"/>).</summary>
        public static event Action<string> Requested;

        /// <summary>A sticker lifts off the tray or a slot (peel).</summary>
        public static void StickerPick() => Raise(AudioEvents.NoteStickerPick);

        /// <summary>A sticker is pressed into a question slot.</summary>
        public static void StickerDrop() => Raise(AudioEvents.NoteStickerDrop);

        /// <summary>A wrong set: the stickers wobble back (friendly, never a buzzer; D26).</summary>
        public static void StickerWobble() => Raise(AudioEvents.NoteStickerWobble);

        /// <summary>A question is answered: the warm glow.</summary>
        public static void CorrectGlow() => Raise(AudioEvents.NoteCorrectGlow);

        /// <summary>A notebook page turns (the same paper turn as the thresholds).</summary>
        public static void PageTurn() => Raise(AudioEvents.UiPageTurn);

        /// <summary>A tap on a control that is not a uGUI Button (buttons get their tap automatically).</summary>
        public static void UiTap() => Raise(AudioEvents.UiTap);

        /// <summary>Any catalog event by id (for hooks not listed above).</summary>
        public static void Play(string eventId) => Raise(eventId);

        /// <summary>Removes every listener (test teardown, scene unload).</summary>
        public static void ClearListeners() => Requested = null;

        private static void Raise(string eventId) => Requested?.Invoke(eventId);
    }
}
